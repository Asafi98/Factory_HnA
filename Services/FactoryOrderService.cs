using Dapper;
using HusnaFactory.Data;
using HusnaFactory.Models;
using MySqlConnector;

namespace HusnaFactory.Services;

public class FactoryOrderService
{
    private readonly FactoryDbConnections _db;
    public FactoryOrderService(FactoryDbConnections db) => _db = db;

    // 1 when order item `oi` is outsourced: its garment type is listed, or its name exactly
    // matches a listed article name (case-insensitive). NULLs are folded so NOT(...) is safe.
    const string OutsourceMatchSql = @"IFNULL((
            IFNULL(oi.garment_type, '') IN (SELECT garment_type FROM outsource_settings
                                            WHERE is_active=1 AND garment_type IS NOT NULL)
            OR CONVERT(TRIM(IFNULL(oi.product_name, '')) USING utf8mb4) COLLATE utf8mb4_general_ci IN
               (SELECT CONVERT(TRIM(article_name) USING utf8mb4) COLLATE utf8mb4_general_ci
                FROM outsource_articles WHERE is_active=1)
        ), 0)";

    public async Task<List<FactoryOrder>> GetAllOrdersAsync()
    {
        var malirTask   = LoadBranchOrdersAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhariTask = LoadBranchOrdersAsync(Branches.Bukhari, "Bukhari Branch");
        await Task.WhenAll(malirTask, bukhariTask);

        return malirTask.Result.Concat(bukhariTask.Result)
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
            SELECT o.order_id AS OrderId, o.book_no AS BookNo, o.invoice_id AS InvoiceId,
                   o.customerid AS CustomerId, o.customername AS CustomerName,
                   c.customerphone AS CustomerPhone,
                   ol.outlet_name AS OutletName, o.order_status AS OrderStatus,
                   o.factory_stage AS FactoryStage,
                   o.trial_date AS TrialDate, o.delivery_date AS DeliveryDate,
                   o.order_notes AS OrderNotes, o.created_at AS CreatedAt,
                   o.closed_at AS ClosedAt,
                   IFNULL(i.is_doorstep, 0) AS IsDoorstep,
                   lsc.last_change AS LastStageChangeAt
            FROM orders o
            LEFT JOIN outlets ol ON o.outlet_id = ol.outlet_id
            LEFT JOIN invoices i ON o.invoice_id = i.invoice_id
            LEFT JOIN customer c ON o.customerid = c.customerid
            LEFT JOIN (SELECT order_id, MAX(changed_at) AS last_change FROM (
                           SELECT order_id, changed_at FROM factory_status_history
                           UNION ALL
                           SELECT order_id, changed_at FROM factory_item_status
                       ) h GROUP BY order_id) lsc
                ON o.order_id = lsc.order_id
            WHERE o.order_status <> 'Cancelled'
              AND (i.invoice_id IS NULL OR i.is_void = 0)
              AND (o.order_status <> 'Delivered' OR o.closed_at >= DATE_SUB(CURDATE(), INTERVAL 90 DAY))
            ORDER BY o.created_at DESC")).ToList();

        if (orders.Any())
        {
            var ids = orders.Select(o => o.OrderId).ToList();
            var items = (await conn.QueryAsync<FactoryOrderItem>(@"
                SELECT oi.order_item_id AS OrderItemId, oi.order_id AS OrderId,
                       oi.pro_id AS ProId, oi.product_name AS ProductName,
                       IFNULL(p.pro_cat, '') AS ProCat,
                       oi.quantity AS Quantity,
                       IFNULL(oi.cancelled_qty, 0) AS CancelledQty,
                       oi.item_notes AS ItemNotes,
                       IFNULL(oi.item_status, 'Pending') AS ItemStatus,
                       oi.garment_type AS GarmentType,
                       oi.factory_stage AS FactoryStage,
                       IFNULL(oi.is_outsourced, 0) AS IsOutsourced,
                       oi.outsource_vendor AS OutsourceVendor,
                       oi.sent_to_factory_at AS SentToFactoryAt,
                       oi.factory_accepted_at AS FactoryAcceptedAt,
                       oi.sent_to_shop_at AS SentToShopAt,
                       " + OutsourceMatchSql + @" AS OutsourceMatchFlag
                FROM order_items oi
                LEFT JOIN products p ON oi.pro_id = p.pro_id
                WHERE oi.order_id IN @ids
                  AND oi.item_status <> 'Cancelled'", new { ids })).ToList();

            // Outsourced lines live on the Outsourcing page only; an order made up
            // entirely of them leaves the in-house lists.
            var fullyOutsourced = items.GroupBy(i => i.OrderId)
                                       .Where(g => g.All(i => i.IsOutsourceMatch))
                                       .Select(g => g.Key).ToHashSet();
            orders.RemoveAll(o => fullyOutsourced.Contains(o.OrderId));
            items.RemoveAll(i => i.IsOutsourceMatch);

            if (items.Any())
            {
                var itemIds = items.Select(i => i.OrderItemId).ToList();
                var units = await conn.QueryAsync<FactoryUnitStage>(@"
                    SELECT order_item_id AS OrderItemId, unit_no AS UnitNo, stage AS Stage
                    FROM factory_unit_stage WHERE order_item_id IN @itemIds", new { itemIds });
                var unitMap = units.GroupBy(u => u.OrderItemId)
                                   .ToDictionary(g => g.Key, g => g.ToDictionary(u => u.UnitNo, u => u.Stage));
                foreach (var i in items)
                    if (unitMap.TryGetValue(i.OrderItemId, out var us)) i.UnitStages = us;
            }

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
        var malirTask   = LoadSentToFactoryBranchAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhariTask = LoadSentToFactoryBranchAsync(Branches.Bukhari, "Bukhari Branch");
        await Task.WhenAll(malirTask, bukhariTask);
        return malirTask.Result.Concat(bukhariTask.Result).OrderBy(o => o.CreatedAt).ToList();
    }

    private async Task<List<FactoryOrder>> LoadSentToFactoryBranchAsync(string branchKey, string branchLabel)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var orders = (await conn.QueryAsync<FactoryOrder>(@"
            SELECT o.order_id AS OrderId, o.book_no AS BookNo, o.invoice_id AS InvoiceId,
                   o.customerid AS CustomerId, o.customername AS CustomerName,
                   c.customerphone AS CustomerPhone,
                   ol.outlet_name AS OutletName, o.order_status AS OrderStatus,
                   o.trial_date AS TrialDate, o.delivery_date AS DeliveryDate,
                   o.order_notes AS OrderNotes, o.created_at AS CreatedAt
            FROM orders o
            LEFT JOIN outlets ol ON o.outlet_id=ol.outlet_id
            LEFT JOIN customer c ON o.customerid=c.customerid
            LEFT JOIN invoices i ON o.invoice_id=i.invoice_id
            INNER JOIN order_items oi ON o.order_id=oi.order_id
            WHERE oi.item_status = 'Sent to Factory'
              AND NOT " + OutsourceMatchSql + @"
              AND o.order_status <> 'Cancelled'
              AND (i.invoice_id IS NULL OR i.is_void = 0)
            GROUP BY o.order_id, o.book_no, o.invoice_id, o.customerid, o.customername,
                     c.customerphone, ol.outlet_name, o.order_status,
                     o.trial_date, o.delivery_date, o.order_notes, o.created_at
            ORDER BY MIN(oi.sent_to_factory_at) ASC")).ToList();

        if (orders.Any())
        {
            var ids = orders.Select(o => o.OrderId).ToList();
            var allItems = (await conn.QueryAsync<FactoryOrderItem>(@"
                SELECT oi.order_item_id AS OrderItemId, oi.order_id AS OrderId,
                       oi.pro_id AS ProId, oi.product_name AS ProductName,
                       IFNULL(p.pro_cat, '') AS ProCat,
                       oi.quantity AS Quantity,
                       IFNULL(oi.cancelled_qty, 0) AS CancelledQty,
                       oi.item_notes AS ItemNotes,
                       IFNULL(oi.item_status, 'Pending') AS ItemStatus,
                       oi.garment_type AS GarmentType,
                       oi.factory_stage AS FactoryStage,
                       IFNULL(oi.is_outsourced, 0) AS IsOutsourced,
                       oi.outsource_vendor AS OutsourceVendor,
                       oi.sent_to_factory_at AS SentToFactoryAt,
                       oi.factory_accepted_at AS FactoryAcceptedAt,
                       oi.sent_to_shop_at AS SentToShopAt
                FROM order_items oi
                LEFT JOIN products p ON oi.pro_id = p.pro_id
                WHERE oi.order_id IN @ids AND oi.item_status = 'Sent to Factory'
                  AND NOT " + OutsourceMatchSql,
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
        var malirTask   = LoadOverdueBranchAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhariTask = LoadOverdueBranchAsync(Branches.Bukhari, "Bukhari Branch");
        await Task.WhenAll(malirTask, bukhariTask);
        return malirTask.Result.Concat(bukhariTask.Result).OrderBy(o => o.ClosedAt).ToList();
    }

    private async Task<List<FactoryOrder>> LoadOverdueBranchAsync(string branchKey, string branchLabel)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var orders = (await conn.QueryAsync<FactoryOrder>(@"
            SELECT o.order_id AS OrderId, o.book_no AS BookNo, o.invoice_id AS InvoiceId,
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

        async Task LoadBranch(string key)
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
                INNER JOIN orders o ON oi.order_id = o.order_id
                WHERE oi.garment_type IS NOT NULL
                  AND oi.item_status <> 'Cancelled'
                  AND o.order_status <> 'Cancelled'
                  AND o.created_at >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)
                GROUP BY oi.garment_type")).ToList();

            lock (result)
            {
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
        }

        await Task.WhenAll(LoadBranch(Branches.Malir), LoadBranch(Branches.Bukhari));

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

    public async Task RemoveOutsourceSettingAsync(string garmentType)
    {
        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            await conn.ExecuteAsync(
                "UPDATE outsource_settings SET is_active=0 WHERE garment_type=@gt",
                new { gt = garmentType });
        }
    }

    public async Task<List<string>> GetOutsourceArticlesAsync()
    {
        using var conn = _db.CreateForBranch(Branches.Malir);
        var r = await conn.QueryAsync<string>(
            "SELECT article_name FROM outsource_articles WHERE is_active=1 ORDER BY article_name");
        return r.ToList();
    }

    public async Task AddOutsourceArticleAsync(string articleName)
    {
        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            await conn.ExecuteAsync(@"
                INSERT INTO outsource_articles(article_name) VALUES(@n)
                ON DUPLICATE KEY UPDATE is_active=1, article_name=VALUES(article_name)",
                new { n = articleName.Trim() });
        }
    }

    public async Task RemoveOutsourceArticleAsync(string articleName)
    {
        foreach (var key in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(key);
            await conn.ExecuteAsync(
                "UPDATE outsource_articles SET is_active=0 WHERE article_name=@n",
                new { n = articleName });
        }
    }

    // Every item name ever sold on an order in either branch, with how many open lines use it,
    // so settings can offer exact names and preview what a name will catch.
    public async Task<List<ArticleNameUsage>> GetArticleNameUsageAsync()
    {
        async Task<List<ArticleNameUsage>> Load(string key)
        {
            using var conn = _db.CreateForBranch(key);
            var r = await conn.QueryAsync<ArticleNameUsage>(@"
                SELECT TRIM(oi.product_name) AS Name,
                       SUM(CASE WHEN o.order_status NOT IN ('Delivered','Cancelled')
                                 AND IFNULL(oi.item_status,'') <> 'Cancelled' THEN 1 ELSE 0 END) AS OpenLines
                FROM order_items oi
                INNER JOIN orders o ON o.order_id = oi.order_id
                WHERE TRIM(IFNULL(oi.product_name,'')) <> ''
                GROUP BY TRIM(oi.product_name)");
            return r.ToList();
        }

        var malir = Load(Branches.Malir);
        var bukhari = Load(Branches.Bukhari);
        await Task.WhenAll(malir, bukhari);
        return malir.Result.Concat(bukhari.Result)
            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ArticleNameUsage { Name = g.First().Name, OpenLines = g.Sum(a => a.OpenLines) })
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task UpdateOutsourceVendorAsync(string branchKey, int orderItemId, string vendor)
    {
        using var conn = _db.CreateForBranch(branchKey);
        await conn.ExecuteAsync(
            "UPDATE order_items SET outsource_vendor=@v WHERE order_item_id=@id",
            new { v = vendor, id = orderItemId });
    }

    // ── Item-level factory stage tracking ──

    // Sets every piece of an item line (e.g. both shirts of "Shirt × 2") to the same stage.
    public async Task UpdateItemStageAsync(string branchKey, int orderId, int orderItemId, string stage, string? changedBy)
    {
        var newStage = string.IsNullOrWhiteSpace(stage) ? null : stage;
        var by = changedBy ?? string.Empty;

        using var conn = _db.CreateForBranch(branchKey);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            await conn.ExecuteAsync(
                "DELETE FROM factory_unit_stage WHERE order_item_id=@iid",
                new { iid = orderItemId }, tx);

            if (newStage != null)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO factory_item_status(order_id, order_item_id, stage, changed_by)
                    VALUES(@oid, @iid, @s, NULLIF(@by, ''))",
                    new { oid = orderId, iid = orderItemId, s = newStage, by }, tx);
            }

            await ApplyItemStageAsync(conn, tx, orderId, orderItemId, newStage, by);
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // Moves one piece of a multi-quantity line (e.g. shirt 2 of 2) on its own. The line's
    // stage becomes its least-progressed piece, with Not Started counting as least.
    public async Task UpdateUnitStageAsync(string branchKey, int orderId, int orderItemId, int unitNo, string stage, string? changedBy)
    {
        var newStage = string.IsNullOrWhiteSpace(stage) ? null : stage;
        var by = changedBy ?? string.Empty;

        using var conn = _db.CreateForBranch(branchKey);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            var item = await conn.QuerySingleAsync<FactoryOrderItem>(@"
                SELECT order_item_id AS OrderItemId, factory_stage AS FactoryStage,
                       quantity AS Quantity, IFNULL(cancelled_qty, 0) AS CancelledQty
                FROM order_items WHERE order_item_id=@iid AND order_id=@oid",
                new { iid = orderItemId, oid = orderId }, tx);

            if (unitNo < 1 || unitNo > item.UnitCount)
                throw new InvalidOperationException("That piece is no longer on this order. Refresh the page and try again.");

            var saved = (await conn.QueryAsync<FactoryUnitStage>(@"
                SELECT order_item_id AS OrderItemId, unit_no AS UnitNo, stage AS Stage
                FROM factory_unit_stage WHERE order_item_id=@iid",
                new { iid = orderItemId }, tx)).ToDictionary(u => u.UnitNo, u => u.Stage);

            var units = Enumerable.Range(1, item.UnitCount)
                .Select(n => n == unitNo ? newStage : saved.TryGetValue(n, out var s) ? s : item.FactoryStage)
                .ToList();

            await conn.ExecuteAsync(@"
                INSERT INTO factory_unit_stage(order_item_id, unit_no, stage) VALUES(@iid, @n, @s)
                ON DUPLICATE KEY UPDATE stage=VALUES(stage)",
                units.Select((s, i) => new { iid = orderItemId, n = i + 1, s }), tx);

            if (newStage != null)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO factory_item_status(order_id, order_item_id, unit_no, stage, changed_by)
                    VALUES(@oid, @iid, @n, @s, NULLIF(@by, ''))",
                    new { oid = orderId, iid = orderItemId, n = unitNo, s = newStage, by }, tx);
            }

            await ApplyItemStageAsync(conn, tx, orderId, orderItemId, EarliestStage(units, requireAll: true), by);
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // Saves an item line's stage, then recomputes the order stage and shop status from all lines.
    async Task ApplyItemStageAsync(MySqlConnection conn, MySqlTransaction tx, int orderId, int orderItemId, string? newStage, string by)
    {
        // Outsourced lines never get a factory stage, so they must not hold back the shop status.
        var items = (await conn.QueryAsync<FactoryOrderItem>(@"
            SELECT oi.order_item_id AS OrderItemId, oi.factory_stage AS FactoryStage FROM order_items oi
            WHERE oi.order_id=@id AND IFNULL(oi.item_status,'') <> 'Cancelled'
              AND (oi.order_item_id=@iid OR NOT " + OutsourceMatchSql + ")",
            new { id = orderId, iid = orderItemId }, tx)).ToList();

        var before = items.Select(i => i.FactoryStage).ToList();
        var after = items.Select(i => i.OrderItemId == orderItemId ? newStage : i.FactoryStage).ToList();

        await conn.ExecuteAsync(
            "UPDATE order_items SET factory_stage=@s WHERE order_item_id=@iid AND order_id=@oid",
            new { s = newStage, iid = orderItemId, oid = orderId }, tx);

        // Order-level stage shown in lists = least-progressed item that has started.
        var prevOrderStage = EarliestStage(before, requireAll: false);
        var orderStage = EarliestStage(after, requireAll: false);

        await conn.ExecuteAsync(
            "UPDATE orders SET factory_stage=@s WHERE order_id=@id",
            new { s = orderStage, id = orderId }, tx);

        // Legacy order-level history feeds the stage-duration analytics, so only
        // record genuine order-level transitions there.
        if (orderStage != null && orderStage != prevOrderStage)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO factory_status_history(order_id, stage, changed_by)
                VALUES(@id, @s, NULLIF(@by, ''))",
                new { id = orderId, s = orderStage, by }, tx);
        }

        // Shop status follows the slowest item; a Not Started item holds it back.
        var prevSync = EarliestStage(before, requireAll: true);
        var newSync = EarliestStage(after, requireAll: true);
        if (newSync == null || newSync == prevSync) return;

        var shopStatus = ShopStatusFor(newSync);
        if (shopStatus == null || shopStatus == ShopStatusFor(prevSync)) return;

        var changed = await conn.ExecuteAsync(
            "UPDATE orders SET order_status=@s WHERE order_id=@id AND IFNULL(order_status,'') NOT IN ('Delivered','Cancelled')",
            new { s = shopStatus, id = orderId }, tx);

        if (changed > 0)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO order_status_history(order_id, status, changed_by)
                VALUES(@id, @s, NULLIF(@by, ''))",
                new { id = orderId, s = shopStatus, by }, tx);
        }
    }

    public static string? EarliestStage(IReadOnlyCollection<string?> stages, bool requireAll)
    {
        if (stages.Count == 0) return null;
        if (requireAll && stages.Any(string.IsNullOrEmpty)) return null;
        return stages.Where(s => !string.IsNullOrEmpty(s))
                     .OrderBy(s => Array.IndexOf(FactoryStages.All, s))
                     .FirstOrDefault();
    }

    // Highest shop-sync stage the order has reached, e.g. "Press & Packing" → "Trial Done".
    public static string? ShopStatusFor(string? stage)
    {
        if (stage == null) return null;
        var idx = Array.IndexOf(FactoryStages.All, stage);
        string? status = null;
        for (var i = 0; i <= idx; i++)
            if (FactoryStages.ShopStatusSync.TryGetValue(FactoryStages.All[i], out var s)) status = s;
        return status;
    }

    public async Task<List<FactoryStatusHistory>> GetItemStageHistoryAsync(string branchKey, int orderId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var r = await conn.QueryAsync<FactoryStatusHistory>(@"
            SELECT fis.id AS HistoryId, fis.order_id AS OrderId,
                   fis.order_item_id AS OrderItemId, fis.unit_no AS UnitNo,
                   fis.stage AS Stage, fis.changed_at AS ChangedAt,
                   fis.changed_by AS ChangedBy,
                   oi.product_name AS ProductName
            FROM factory_item_status fis
            LEFT JOIN order_items oi ON fis.order_item_id = oi.order_item_id
            WHERE fis.order_id=@id
            ORDER BY fis.changed_at ASC, fis.id ASC", new { id = orderId });
        return r.ToList();
    }

    public async Task<List<StageAverage>> GetStageAveragesAsync()
    {
        var stageDurations = new Dictionary<string, List<double>>();

        async Task LoadBranch(string key)
        {
            using var conn = _db.CreateForBranch(key);
            var history = (await conn.QueryAsync<FactoryStatusHistory>(@"
                SELECT history_id AS HistoryId, order_id AS OrderId,
                       stage AS Stage, changed_at AS ChangedAt
                FROM factory_status_history
                WHERE changed_at >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)
                ORDER BY order_id, changed_at, history_id")).ToList();

            var localDurations = new Dictionary<string, List<double>>();
            foreach (var group in history.GroupBy(h => h.OrderId))
            {
                var entries = group.OrderBy(h => h.ChangedAt).ThenBy(h => h.HistoryId).ToList();
                for (int i = 0; i < entries.Count - 1; i++)
                {
                    var days = (entries[i + 1].ChangedAt - entries[i].ChangedAt).TotalDays;
                    if (!localDurations.ContainsKey(entries[i].Stage))
                        localDurations[entries[i].Stage] = new();
                    localDurations[entries[i].Stage].Add(days);
                }
            }

            lock (stageDurations)
            {
                foreach (var kv in localDurations)
                {
                    if (!stageDurations.ContainsKey(kv.Key))
                        stageDurations[kv.Key] = new();
                    stageDurations[kv.Key].AddRange(kv.Value);
                }
            }
        }

        await Task.WhenAll(LoadBranch(Branches.Malir), LoadBranch(Branches.Bukhari));

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
        var lockObj = new object();

        async Task LoadBranch(string key)
        {
            using var conn = _db.CreateForBranch(key);
            var rows = (await conn.QueryAsync<string>(
                "SELECT stage FROM factory_status_history WHERE changed_at >= @d AND changed_at < @d2",
                new { d = date.Date, d2 = date.Date.AddDays(1) })).ToList();
            lock (lockObj)
            {
                totalChanges += rows.Count;
                received += rows.Count(s => s == FactoryStages.Received);
                completed += rows.Count(s => s == FactoryStages.SentToShop);
            }
        }

        await Task.WhenAll(LoadBranch(Branches.Malir), LoadBranch(Branches.Bukhari));
        return (received, completed, totalChanges);
    }

    public async Task UpdateBookNoAsync(int orderId, string branchKey, string? bookNo)
    {
        using var conn = _db.CreateForBranch(branchKey);
        await conn.ExecuteAsync(
            "UPDATE orders SET book_no=NULLIF(@bno, '') WHERE order_id=@id",
            new { bno = bookNo?.Trim() ?? "", id = orderId });
    }

    // ── Production Report (month-wise, by garment type, OTM vs Stitching) ──

    public async Task<List<ProductionReportRow>> GetProductionReportAsync(int months = 6)
    {
        var result = new Dictionary<string, ProductionReportRow>();

        async Task LoadBranch(string key)
        {
            using var conn = _db.CreateForBranch(key);
            var rows = (await conn.QueryAsync<ProductionReportRow>(@"
                SELECT DATE_FORMAT(o.created_at, '%Y-%m') AS MonthKey,
                       DATE_FORMAT(o.created_at, '%b %Y') AS MonthLabel,
                       IFNULL(oi.garment_type, 'Other') AS GarmentType,
                       IFNULL(p.pro_cat, '') AS Category,
                       SUM(oi.quantity) AS Quantity,
                       SUM(CASE WHEN oi.item_status = 'Delivered' OR oi.item_status = 'Sent to Shop' THEN oi.quantity ELSE 0 END) AS Completed
                FROM order_items oi
                INNER JOIN orders o ON oi.order_id = o.order_id
                LEFT JOIN products p ON oi.pro_id = p.pro_id
                WHERE o.order_status <> 'Cancelled'
                  AND oi.item_status <> 'Cancelled'
                  AND o.created_at >= DATE_SUB(CURDATE(), INTERVAL @m MONTH)
                GROUP BY DATE_FORMAT(o.created_at, '%Y-%m'),
                         DATE_FORMAT(o.created_at, '%b %Y'),
                         IFNULL(oi.garment_type, 'Other'),
                         IFNULL(p.pro_cat, '')
                ORDER BY MonthKey DESC, GarmentType", new { m = months })).ToList();

            lock (result)
            {
                foreach (var r in rows)
                {
                    var compositeKey = $"{r.MonthKey}|{r.GarmentType}|{r.Category}";
                    if (result.TryGetValue(compositeKey, out var existing))
                    {
                        existing.Quantity += r.Quantity;
                        existing.Completed += r.Completed;
                    }
                    else
                    {
                        result[compositeKey] = r;
                    }
                }
            }
        }

        await Task.WhenAll(LoadBranch(Branches.Malir), LoadBranch(Branches.Bukhari));

        return result.Values
            .OrderByDescending(r => r.MonthKey)
            .ThenBy(r => r.GarmentType)
            .ToList();
    }

    // ── Outsourced orders (matches by garment_type against active outsource_settings) ──

    public async Task<List<FactoryOrder>> GetOutsourcedOrdersAsync()
    {
        var malirTask   = LoadOutsourcedBranchAsync(Branches.Malir,   "Malir Cantt Branch");
        var bukhariTask = LoadOutsourcedBranchAsync(Branches.Bukhari, "Bukhari Branch");
        await Task.WhenAll(malirTask, bukhariTask);

        return malirTask.Result.Concat(bukhariTask.Result)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
    }

    private async Task<List<FactoryOrder>> LoadOutsourcedBranchAsync(string branchKey, string branchLabel)
    {
        using var conn = _db.CreateForBranch(branchKey);

        var orders = (await conn.QueryAsync<FactoryOrder>(@"
            SELECT DISTINCT o.order_id AS OrderId, o.book_no AS BookNo, o.invoice_id AS InvoiceId,
                   o.customerid AS CustomerId, o.customername AS CustomerName,
                   c.customerphone AS CustomerPhone,
                   ol.outlet_name AS OutletName, o.order_status AS OrderStatus,
                   o.factory_stage AS FactoryStage,
                   o.trial_date AS TrialDate, o.delivery_date AS DeliveryDate,
                   o.order_notes AS OrderNotes, o.created_at AS CreatedAt,
                   o.closed_at AS ClosedAt
            FROM orders o
            LEFT JOIN outlets ol ON o.outlet_id = ol.outlet_id
            LEFT JOIN invoices i ON o.invoice_id = i.invoice_id
            LEFT JOIN customer c ON o.customerid = c.customerid
            INNER JOIN order_items oi ON o.order_id = oi.order_id
            WHERE " + OutsourceMatchSql + @"
              AND o.order_status <> 'Cancelled'
              AND (i.invoice_id IS NULL OR i.is_void = 0)
              AND oi.item_status <> 'Cancelled'
            ORDER BY o.created_at DESC")).ToList();

        if (orders.Any())
        {
            var ids = orders.Select(o => o.OrderId).ToList();
            var items = (await conn.QueryAsync<FactoryOrderItem>(@"
                SELECT oi.order_item_id AS OrderItemId, oi.order_id AS OrderId,
                       oi.pro_id AS ProId, oi.product_name AS ProductName,
                       IFNULL(p.pro_cat, '') AS ProCat,
                       oi.quantity AS Quantity,
                       IFNULL(oi.cancelled_qty, 0) AS CancelledQty,
                       oi.item_notes AS ItemNotes,
                       IFNULL(oi.item_status, 'Pending') AS ItemStatus,
                       oi.garment_type AS GarmentType,
                       oi.factory_stage AS FactoryStage,
                       IFNULL(oi.is_outsourced, 0) AS IsOutsourced,
                       oi.outsource_vendor AS OutsourceVendor,
                       oi.sent_to_factory_at AS SentToFactoryAt,
                       oi.factory_accepted_at AS FactoryAcceptedAt,
                       oi.sent_to_shop_at AS SentToShopAt
                FROM order_items oi
                LEFT JOIN products p ON oi.pro_id = p.pro_id
                WHERE oi.order_id IN @ids
                  AND " + OutsourceMatchSql + @"
                  AND oi.item_status <> 'Cancelled'", new { ids })).ToList();

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
}
