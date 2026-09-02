namespace HusnaFactory.Models;

// ─────────────────────────────────────────
//  The 10-stage factory production workflow
// ─────────────────────────────────────────
public static class FactoryStages
{
    public const string Received        = "Received";
    public const string Processing      = "Processing";
    public const string Cutting         = "Cutting";
    public const string Stitching       = "Stitching";
    public const string CollarFinishing = "Collar Finishing";
    public const string Button          = "Button";
    public const string TrialSent       = "Trial Sent";
    public const string TrialReceived   = "Trial Received";
    public const string PressAndPacking = "Press & Packing";
    public const string SentToShop      = "Sent to Shop";

    public static readonly string[] All =
    {
        Received, Processing, Cutting, Stitching, CollarFinishing,
        Button, TrialSent, TrialReceived, PressAndPacking, SentToShop
    };

    public static readonly Dictionary<string, string> Descriptions = new()
    {
        [Received]        = "Order arrived at factory",
        [Processing]      = "Fabric is being processed",
        [Cutting]         = "Fabric is being cut",
        [Stitching]       = "Fabric is in body stitching",
        [CollarFinishing] = "Collar is being attached",
        [Button]          = "Button being attached",
        [TrialSent]       = "Trial is sent to the shop",
        [TrialReceived]   = "Trial is back from the shop",
        [PressAndPacking] = "Being pressed & packed",
        [SentToShop]      = "Order is sent to shop",
    };

    // Which shop-facing order_status (Pending/In Progress/Trial Awaited/Trial Done/
    // Received/Delivered/Cancelled) a factory stage transition should also set on the
    // originating branch database. A stage not listed here leaves the shop status alone.
    public static readonly Dictionary<string, string> ShopStatusSync = new()
    {
        [Received]      = "In Progress",
        [TrialSent]     = "Trial Awaited",
        [TrialReceived] = "Trial Done",
        [SentToShop]    = "Received",
    };
}

// The two branches this app pulls from. BranchKey selects which connection to use
// for writes; BranchLabel is what's shown in the UI and used for the location filter.
public static class Branches
{
    public const string Malir   = "malir";
    public const string Bukhari = "bukhari";
}

public class FactoryOrder
{
    public string BranchKey { get; set; } = "";
    public string BranchLabel { get; set; } = "";
    public int OrderId { get; set; }
    public int? InvoiceId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? OutletName { get; set; }
    public string OrderStatus { get; set; } = "Pending";
    public string? FactoryStage { get; set; }
    public DateTime? TrialDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? OrderNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDoorstep { get; set; }
    public List<FactoryOrderItem> Items { get; set; } = new();

    public string ItemsSummary =>
        Items.Any() ? string.Join(", ", Items.Select(i => $"{i.ProductName} ×{i.Quantity}")) : "—";

    public IEnumerable<string> Categories =>
        Items.Select(i => i.ProCat).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()!;

    public string CurrentStageLabel => string.IsNullOrEmpty(FactoryStage) ? "Not Started" : FactoryStage;
}

public class FactoryOrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int? ProId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProCat { get; set; }
    public int Quantity { get; set; } = 1;
    public string? ItemNotes { get; set; }
}

public class FactoryStatusHistory
{
    public int HistoryId { get; set; }
    public int OrderId { get; set; }
    public string Stage { get; set; } = "";
    public DateTime ChangedAt { get; set; }
    public string? ChangedBy { get; set; }
}

public class FactoryUser
{
    public int FactoryUserId { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Category
{
    public int CatId { get; set; }
    public string CatName { get; set; } = "";
}

public class Outlet
{
    public int OutletId { get; set; }
    public string OutletName { get; set; } = "";
}

// ─────────────────────────────────────────
//  Measurement models — read-only mirror of the POS's schema, used to show
//  "sizing of that person" on an order, or "No size has been entered" if absent.
// ─────────────────────────────────────────

public class ShirtMeasurement
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? Length { get; set; }
    public string? Chest { get; set; }
    public string? Waist { get; set; }
    public string? Hips { get; set; }
    public string? Shoulder { get; set; }
    public string? Sleeve { get; set; }
    public string? Collar { get; set; }
    public string? Cuff { get; set; }
    public string? Comments { get; set; }
}

public class PantMeasurement
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? Waist { get; set; }
    public string? Hips { get; set; }
    public string? FLength { get; set; }
    public string? InnerLength { get; set; }
    public string? KBalance { get; set; }
    public string? Bottom { get; set; }
    public string? Thigh { get; set; }
    public string? Fly { get; set; }
    public string? BFly { get; set; }
    public string? Seat { get; set; }
    public string? Comments { get; set; }
}

public class WaistcoatMeasurement
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? Length { get; set; }
    public string? Chest { get; set; }
    public string? Waist { get; set; }
    public string? Hips { get; set; }
    public string? Shoulder { get; set; }
    public string? CrossBack { get; set; }
    public string? Armhole { get; set; }
    public string? Comments { get; set; }
}

public class ShalwarKameezMeasurement
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? KLength { get; set; }
    public string? KChest { get; set; }
    public string? KWaist { get; set; }
    public string? KHips { get; set; }
    public string? KShoulder { get; set; }
    public string? KSleeve { get; set; }
    public string? KCollar { get; set; }
    public string? KDaman { get; set; }
    public string? SLength { get; set; }
    public string? SWaist { get; set; }
    public string? SHips { get; set; }
    public string? SBottom { get; set; }
    public string? SThigh { get; set; }
    public string? Comments { get; set; }
}

public class CoatMeasurement
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CoatType { get; set; } = "Coat";
    public string? Length { get; set; }
    public string? Chest { get; set; }
    public string? Waist { get; set; }
    public string? Hips { get; set; }
    public string? Shoulder { get; set; }
    public string? Sleeve { get; set; }
    public string? BackLength { get; set; }
    public string? CrossBack { get; set; }
    public string? Armhole { get; set; }
    public string? Collar { get; set; }
    public string? Lapel { get; set; }
    public string? Comments { get; set; }
}

public class Suit2PieceMeasurement
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? JLength { get; set; }
    public string? JChest { get; set; }
    public string? JWaist { get; set; }
    public string? JHips { get; set; }
    public string? JShoulder { get; set; }
    public string? JSleeve { get; set; }
    public string? JBackLength { get; set; }
    public string? JCrossBack { get; set; }
    public string? TWaist { get; set; }
    public string? THips { get; set; }
    public string? TLength { get; set; }
    public string? TInnerLength { get; set; }
    public string? TThigh { get; set; }
    public string? TBottom { get; set; }
    public string? Comments { get; set; }
}

public class MeasurementScan
{
    public int ScanId { get; set; }
    public int CustomerId { get; set; }
    public string GarmentType { get; set; } = "";
    public string? FileName { get; set; }
    public string ContentType { get; set; } = "";
    public byte[] FileData { get; set; } = Array.Empty<byte>();
    public DateTime UploadedAt { get; set; }

    public string DataUri => $"data:{ContentType};base64,{Convert.ToBase64String(FileData)}";
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    public bool IsPdf => ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
}
