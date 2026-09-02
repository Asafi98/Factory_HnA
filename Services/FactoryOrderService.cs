using Dapper;
using HusnaFactory.Data;
using HusnaFactory.Models;
using MySqlConnector;

namespace HusnaFactory.Services;

public class FactoryOrderService
{
    private readonly FactoryDbConnections _db;
    public FactoryOrderService(FactoryDbConnections db) => _db = db;

    // Loads every stitching/tailoring order (item category "Unstitched") from BOTH
    // branch databases and merges them into one newest-first list, each tagged with
    // which branch it came from.
    public async Task<List<FactoryOrder>> GetAllOrdersAsync()
    {
        var malir   = await LoadBranchOrdersAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhari = await LoadBranchOrdersAsync(Branches.Bukhari, "Bukhari Branch");

        return malir.Concat(bukhari)
            .Where(o => o.Categories.Any(c => string.Equals(c, "Unstitched", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
    }

    private async Task<List<FactoryOrder>> LoadBranchOrdersAsync(string branchKey, string branchLabel)
    {
        using var conn = _db.CreateForBranch(branchKey);

        var orders = (await conn.QueryAsync<FactoryOrder>(@"
            SELECT o.order_id AS OrderId, o.invoice_id AS InvoiceId,
                   o.customerid AS CustomerId, o.customername AS CustomerName,
                   ol.outlet_name AS OutletName, o.order_status AS OrderStatus,
                   o.factory_stage AS FactoryStage,
                   o.trial_date AS TrialDate, o.delivery_date AS DeliveryDate,
                   o.order_notes AS OrderNotes, o.created_at AS CreatedAt,
                   IFNULL(i.is_doorstep, 0) AS IsDoorstep
            FROM orders o
            LEFT JOIN outlets ol ON o.outlet_id = ol.outlet_id
            LEFT JOIN invoices i ON o.invoice_id = i.invoice_id
            ORDER BY o.created_at DESC")).ToList();

        if (orders.Any())
        {
            var ids = orders.Select(o => o.OrderId).ToList();
            var items = (await conn.QueryAsync<FactoryOrderItem>(@"
                SELECT oi.order_item_id AS OrderItemId, oi.order_id AS OrderId,
                       oi.pro_id AS ProId, oi.product_name AS ProductName,
                       IFNULL(p.pro_cat, '') AS ProCat,
                       oi.quantity AS Quantity, oi.item_notes AS ItemNotes
                FROM order_items oi
                LEFT JOIN products p ON oi.pro_id = p.pro_id
                WHERE oi.order_id IN @ids", new { ids })).ToList();

            var itemsMap = items.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var o in orders)
            {
                o.Items = itemsMap.TryGetValue(o.OrderId, out var oi) ? oi : new();
                o.BranchKey = branchKey;
                o.BranchLabel = branchLabel;
            }
        }

        return orders;
    }

    public async Task<List<FactoryStatusHistory>> GetStageHistoryAsync(string branchKey, int orderId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var r = await conn.QueryAsync<FactoryStatusHistory>(@"
            SELECT history_id AS HistoryId, order_id AS OrderId, stage AS Stage,
                   changed_at AS ChangedAt, changed_by AS ChangedBy
            FROM factory_status_history
            WHERE order_id=@id
            ORDER BY changed_at ASC, history_id ASC", new { id = orderId });
        return r.ToList();
    }

    // Updates the factory stage (+ its own auto-stamped history), and — for the stages
    // mapped in FactoryStages.ShopStatusSync — also updates the shop-facing order_status
    // and its history, so shop staff see accurate progress without a second manual update.
    public async Task UpdateStageAsync(string branchKey, int orderId, string stage, string? changedBy)
    {
        using var conn = _db.CreateForBranch(branchKey);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            await conn.ExecuteAsync(
                "UPDATE orders SET factory_stage=@s WHERE order_id=@id",
                new { s = stage, id = orderId }, tx);

            await conn.ExecuteAsync(@"
                INSERT INTO factory_status_history(order_id, stage, changed_by)
                VALUES(@id, @s, NULLIF(@by, ''))",
                new { id = orderId, s = stage, by = changedBy ?? string.Empty }, tx);

            if (FactoryStages.ShopStatusSync.TryGetValue(stage, out var shopStatus))
            {
                await conn.ExecuteAsync(
                    "UPDATE orders SET order_status=@s WHERE order_id=@id",
                    new { s = shopStatus, id = orderId }, tx);

                await conn.ExecuteAsync(@"
                    INSERT INTO order_status_history(order_id, status, changed_by)
                    VALUES(@id, @s, NULLIF(@by, ''))",
                    new { id = orderId, s = shopStatus, by = changedBy ?? string.Empty }, tx);
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }
}
