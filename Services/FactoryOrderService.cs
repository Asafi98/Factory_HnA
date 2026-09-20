using Dapper;
using HusnaFactory.Data;
using HusnaFactory.Models;
using MySqlConnector;

namespace HusnaFactory.Services;

public class FactoryOrderService
{
    private readonly FactoryDbConnections _db;
    public FactoryOrderService(FactoryDbConnections db) => _db = db;

    public async Task<List<FactoryOrder>> GetAllOrdersAsync()
    {
        var malir   = await LoadBranchOrdersAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhari = await LoadBranchOrdersAsync(Branches.Bukhari, "Bukhari Branch");

        return malir.Concat(bukhari)
            .Where(o => o.Categories.Any(c =>
                string.Equals(c, "Unstitched", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, "Services", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
    }

    private async Task<List<FactoryOrder>> LoadBranchOrdersAsync(string branchKey, string branchLabel)
    {
        using var conn = _db.CreateForBranch(branchKey);

        var orders = (await conn.QueryAsync<FactoryOrder>(@"
            SELECT o.order_id AS OrderId, o.invoice_id AS InvoiceId,
                   o.customerid AS CustomerId, o.customername AS CustomerName,
                   c.customerphone AS CustomerPhone,
                   ol.outlet_name AS OutletName, o.order_status AS OrderStatus,
                   o.factory_stage AS FactoryStage,
                   o.trial_date AS TrialDate, o.delivery_date AS DeliveryDate,
                   o.order_notes AS OrderNotes, o.created_at AS CreatedAt,
                   o.closed_at AS ClosedAt,
                   IFNULL(i.is_doorstep, 0) AS IsDoorstep,
                   (SELECT MAX(fsh.changed_at) FROM factory_status_history fsh
                    WHERE fsh.order_id = o.order_id) AS LastStageChangeAt
            FROM orders o
            LEFT JOIN outlets ol ON o.outlet_id = ol.outlet_id
            LEFT JOIN invoices i ON o.invoice_id = i.invoice_id
            LEFT JOIN customer c ON o.customerid = c.customerid
            ORDER BY o.created_at DESC")).ToList();

        if (orders.Any())
        {
            var ids = orders.Select(o => o.OrderId).ToList();
            var items = (await conn.QueryAsync<FactoryOrderItem>(@"
                SELECT oi.order_item_id AS OrderItemId, oi.order_id AS OrderId,
                       oi.pro_id AS ProId, oi.product_name AS ProductName,
                       IFNULL(p.pro_cat, '') AS ProCat,
                       oi.quantity AS Quantity, oi.item_notes AS ItemNotes,
                       IFNULL(oi.item_status, 'Pending') AS ItemStatus,
                       oi.garment_type AS GarmentType,
                       IFNULL(oi.is_outsourced, 0) AS IsOutsourced,
                       oi.outsource_vendor AS OutsourceVendor,
                       oi.sent_to_factory_at AS SentToFactoryAt,
                       oi.factory_accepted_at AS FactoryAcceptedAt,
                       oi.sent_to_shop_at AS SentToShopAt
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

    // ── Per-item status tracking ──

    public async Task UpdateItemStatusAsync(string branchKey, int orderItemId, string status, string? changedBy)
    {
        using var conn = _db.CreateForBranch(branchKey);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            var orderId = await conn.QuerySingleAsync<int>(
                "SELECT order_id FROM order_items WHERE order_item_id=@id",
                new { id = orderItemId }, tx);

            await conn.ExecuteAsync(
                "UPDATE order_items SET item_status=@s WHERE order_item_id=@id",
                new { s = status, id = orderItemId }, tx);

            if (status == "Sent to Factory")
                await conn.ExecuteAsync(
                    "UPDATE order_items SET sent_to_factory_at=NOW() WHERE order_item_id=@id",
                    new { id = orderItemId }, tx);
            else if (status == "Arrived at Factory")
                await conn.ExecuteAsync(
                    "UPDATE order_items SET factory_accepted_at=NOW() WHERE order_item_id=@id",
                    new { id = orderItemId }, tx);
            else if (status == "Sent to Shop")
                await conn.ExecuteAsync(
                    "UPDATE order_items SET sent_to_shop_at=NOW() WHERE order_item_id=@id",
                    new { id = orderItemId }, tx);

            await conn.ExecuteAsync(@"
                INSERT INTO order_status_history(order_id, order_item_id, status, changed_by)
                VALUES(@oid, @iid, @s, NULLIF(@by, ''))",
                new { oid = orderId, iid = orderItemId, s = status,
                      by = changedBy ?? string.Empty }, tx);

            var itemStatuses = (await conn.QueryAsync<string>(
                "SELECT item_status FROM order_items WHERE order_id=@id",
                new { id = orderId }, tx)).ToList();

            string orderStatus;
            var distinct = itemStatuses.Distinct().ToList();
            if (distinct.Count == 1)
                orderStatus = distinct[0];
            else if (distinct.All(s => s == "Delivered"))
                orderStatus = "Delivered";
            else if (distinct.All(s => s == "Cancelled"))
                orderStatus = "Cancelled";
            else
                orderStatus = "Partial";

            await conn.ExecuteAsync(
                "UPDATE orders SET order_status=@s WHERE order_id=@id",
                new { s = orderStatus, id = orderId }, tx);

            if (orderStatus == "Delivered")
                await conn.ExecuteAsync(
                    "UPDATE orders SET closed_at=NOW() WHERE order_id=@id AND closed_at IS NULL",
                    new { id = orderId }, tx);

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // ── Items sent to factory (pending acceptance) ──

    public async Task<List<FactoryOrder>> GetSentToFactoryAsync()
    {
        var malir   = await LoadSentToFactoryBranchAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhari = await LoadSentToFactoryBranchAsync(Branches.Bukhari, "Bukhari Branch");
        return malir.Concat(bukhari).OrderBy(o => o.CreatedAt).ToList();
    }

    private async Task<List<FactoryOrder>> LoadSentToFactoryBranchAsync(string branchKey, string branchLabel)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var orders = (await conn.QueryAsync<FactoryOrder>(@"
            SELECT DISTINCT o.order_id AS OrderId, o.invoice_id AS InvoiceId,
                   o.customerid AS CustomerId, o.customername AS CustomerName,
                   c.customerphone AS CustomerPhone,
                   ol.outlet_name AS OutletName, o.order_status AS OrderStatus,
                   o.trial_date AS TrialDate, o.delivery_date AS DeliveryDate,
                   o.order_notes AS OrderNotes, o.created_at AS CreatedAt
            FROM orders o
            LEFT JOIN outlets ol ON o.outlet_id=ol.outlet_id
            LEFT JOIN customer c ON o.customerid=c.customerid
            INNER JOIN order_items oi ON o.order_id=oi.order_id
            WHERE oi.item_status = 'Sent to Factory'
            ORDER BY oi.sent_to_factory_at ASC")).ToList();

        if (orders.Any())
        {
            var ids = orders.Select(o => o.OrderId).ToList();
            var allItems = (await conn.QueryAsync<FactoryOrderItem>(@"
                SELECT oi.order_item_id AS OrderItemId, oi.order_id AS OrderId,
                       oi.pro_id AS ProId, oi.product_name AS ProductName,
                       IFNULL(p.pro_cat, '') AS ProCat,
                       oi.quantity AS Quantity, oi.item_notes AS ItemNotes,
                       IFNULL(oi.item_status, 'Pending') AS ItemStatus,
                       oi.garment_type AS GarmentType,
                       IFNULL(oi.is_outsourced, 0) AS IsOutsourced,
                       oi.outsource_vendor AS OutsourceVendor,
                       oi.sent_to_factory_at AS SentToFactoryAt,
                       oi.factory_accepted_at AS FactoryAcceptedAt,
                       oi.sent_to_shop_at AS SentToShopAt
                FROM order_items oi
                LEFT JOIN products p ON oi.pro_id = p.pro_id
                WHERE oi.order_id IN @ids AND oi.item_status = 'Sent to Factory'",
                new { ids })).ToList();

            var map = allItems.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var o in orders)
            {
                o.Items = map.TryGetValue(o.OrderId, out var items) ? items : new();
                o.BranchKey = branchKey;
                o.BranchLabel = branchLabel;
            }
        }

        return orders;
    }

    // ── Overdue payment reminders ──

    public async Task<List<FactoryOrder>> GetOverduePaymentsAsync()
    {
        var malir   = await LoadOverdueBranchAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhari = await LoadOverdueBranchAsync(Branches.Bukhari, "Bukhari Branch");
        return malir.Concat(bukhari).OrderBy(o => o.ClosedAt).ToList();
    }

    private async Task<List<FactoryOrder>> LoadOverdueBranchAsync(string branchKey, string branchLabel)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var orders = (await conn.QueryAsync<FactoryOrder>(@"
            SELECT o.order_id AS OrderId, o.invoice_id AS InvoiceId,
                   o.customerid AS CustomerId, o.customername AS CustomerName,
                   ol.outlet_name AS OutletName, o.order_status AS OrderStatus,
                   o.created_at AS CreatedAt, o.closed_at AS ClosedAt
            FROM orders o
            LEFT JOIN outlets ol ON o.outlet_id=ol.outlet_id
            LEFT JOIN invoices i ON o.invoice_id=i.invoice_id
            WHERE o.closed_at IS NOT NULL
              AND DATEDIFF(CURDATE(), o.closed_at) > 2
              AND i.balance > 0
              AND i.is_void = 0
            ORDER BY o.closed_at ASC")).ToList();

        foreach (var o in orders)
        {
            o.BranchKey = branchKey;
            o.BranchLabel = branchLabel;
        }
        return orders;
    }

    // ── Garment type summary ──

    public async Task<List<GarmentTypeSummary>> GetGarmentTypeSummaryAsync()
    {
        var result = new Dictionary<string, GarmentTypeSummary>();

        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            var rows = (await conn.QueryAsync<GarmentTypeSummary>(@"
                SELECT IFNULL(oi.garment_type, 'Unknown') AS GarmentType,
                       SUM(oi.quantity) AS TotalOrdered,
                       SUM(CASE WHEN oi.item_status='Delivered' THEN oi.quantity ELSE 0 END) AS Delivered,
                       SUM(CASE WHEN oi.item_status IN ('In Progress','Arrived at Factory') THEN oi.quantity ELSE 0 END) AS InProgress,
                       SUM(CASE WHEN oi.item_status='Resent for Fixing' THEN oi.quantity ELSE 0 END) AS Returned,
                       SUM(CASE WHEN oi.item_status='Resent for Fixing' THEN oi.quantity ELSE 0 END) AS Altered,
                       SUM(CASE WHEN oi.item_status='Trial Done' THEN oi.quantity ELSE 0 END) AS TrialDone
                FROM order_items oi
                WHERE oi.garment_type IS NOT NULL
                GROUP BY oi.garment_type")).ToList();

            foreach (var r in rows)
            {
                if (result.TryGetValue(r.GarmentType, out var existing))
                {
                    existing.TotalOrdered += r.TotalOrdered;
                    existing.Delivered += r.Delivered;
                    existing.InProgress += r.InProgress;
                    existing.Returned += r.Returned;
                    existing.Altered += r.Altered;
                    existing.TrialDone += r.TrialDone;
                }
                else
                {
                    result[r.GarmentType] = r;
                }
            }
        }

        return result.Values.OrderByDescending(g => g.TotalOrdered).ToList();
    }

    // ── Remarks ──

    public async Task AddRemarkAsync(string branchKey, int orderId, int orderItemId,
        string text, string? remarkType = null, string? createdBy = null)
    {
        using var conn = _db.CreateForBranch(branchKey);
        await conn.ExecuteAsync(@"
            INSERT INTO order_item_remarks(order_id, order_item_id, remark_text, remark_type, created_by)
            VALUES(@oid, @iid, @txt, @rt, NULLIF(@by, ''))",
            new { oid = orderId, iid = orderItemId, txt = text,
                  rt = remarkType ?? (object)System.DBNull.Value,
                  by = createdBy ?? string.Empty });
    }

    public async Task<List<OrderItemRemark>> GetRemarksAsync(string branchKey, int orderItemId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var r = await conn.QueryAsync<OrderItemRemark>(@"
            SELECT remark_id AS RemarkId, order_id AS OrderId, order_item_id AS OrderItemId,
                   remark_text AS RemarkText, remark_type AS RemarkType,
                   created_at AS CreatedAt, created_by AS CreatedBy
            FROM order_item_remarks
            WHERE order_item_id=@id
            ORDER BY created_at ASC", new { id = orderItemId });
        return r.ToList();
    }

    // ── Outsource settings (reads from first branch, writes to both) ──

    public async Task<List<OutsourceSetting>> GetOutsourceSettingsAsync()
    {
        using var conn = _db.CreateForBranch(Branches.Malir);
        var r = await conn.QueryAsync<OutsourceSetting>(@"
            SELECT id AS Id, garment_type AS GarmentType, is_active AS IsActive,
                   created_at AS CreatedAt
            FROM outsource_settings WHERE is_active=1 ORDER BY garment_type");
        return r.ToList();
    }

    public async Task AddOutsourceSettingAsync(string garmentType)
    {
        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            await conn.ExecuteAsync(
                "INSERT IGNORE INTO outsource_settings(garment_type) VALUES(@gt)",
                new { gt = garmentType });
        }
    }

    public async Task RemoveOutsourceSettingAsync(int id)
    {
        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            await conn.ExecuteAsync(
                "UPDATE outsource_settings SET is_active=0 WHERE id=@id",
                new { id });
        }
    }

    public async Task UpdateOutsourceVendorAsync(string branchKey, int orderItemId, string vendor)
    {
        using var conn = _db.CreateForBranch(branchKey);
        await conn.ExecuteAsync(
            "UPDATE order_items SET outsource_vendor=@v WHERE order_item_id=@id",
            new { v = vendor, id = orderItemId });
    }

    // ── Existing 10-stage methods (kept for backward compatibility) ──

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

    public async Task<List<StageAverage>> GetStageAveragesAsync()
    {
        var stageDurations = new Dictionary<string, List<double>>();

        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            var history = (await conn.QueryAsync<FactoryStatusHistory>(@"
                SELECT history_id AS HistoryId, order_id AS OrderId,
                       stage AS Stage, changed_at AS ChangedAt
                FROM factory_status_history
                ORDER BY order_id, changed_at, history_id")).ToList();

            foreach (var group in history.GroupBy(h => h.OrderId))
            {
                var entries = group.OrderBy(h => h.ChangedAt).ThenBy(h => h.HistoryId).ToList();
                for (int i = 0; i < entries.Count - 1; i++)
                {
                    var days = (entries[i + 1].ChangedAt - entries[i].ChangedAt).TotalDays;
                    if (!stageDurations.ContainsKey(entries[i].Stage))
                        stageDurations[entries[i].Stage] = new();
                    stageDurations[entries[i].Stage].Add(days);
                }
            }
        }

        return FactoryStages.All
            .Where(s => stageDurations.ContainsKey(s))
            .Select(s => new StageAverage
            {
                Stage = s,
                AvgDays = Math.Round(stageDurations[s].Average(), 1),
                OrderCount = stageDurations[s].Count
            })
            .ToList();
    }

    public async Task<(int Received, int Completed, int TotalChanges)> GetThroughputAsync(DateTime date)
    {
        int received = 0, completed = 0, totalChanges = 0;
        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            var rows = (await conn.QueryAsync<string>(
                "SELECT stage FROM factory_status_history WHERE DATE(changed_at)=@d",
                new { d = date })).ToList();
            totalChanges += rows.Count;
            received += rows.Count(s => s == FactoryStages.Received);
            completed += rows.Count(s => s == FactoryStages.SentToShop);
        }
        return (received, completed, totalChanges);
    }
}
