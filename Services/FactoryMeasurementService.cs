using Dapper;
using HusnaFactory.Data;
using HusnaFactory.Models;

namespace HusnaFactory.Services;

/// <summary>Read-only measurement + scan lookups, scoped to a specific branch's
/// database (customer IDs are branch-local, not shared across pos_malir/pos_bukhari).</summary>
public class FactoryMeasurementService
{
    private readonly FactoryDbConnections _db;
    public FactoryMeasurementService(FactoryDbConnections db) => _db = db;

    public async Task<ShirtMeasurement?> GetShirtAsync(string branchKey, int customerId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        return await conn.QueryFirstOrDefaultAsync<ShirtMeasurement>(@"
            SELECT id AS Id, customerid AS CustomerId, length AS Length, chest AS Chest,
                   waist AS Waist, hips AS Hips, shoulder AS Shoulder, sleeve AS Sleeve,
                   collar AS Collar, cuff AS Cuff, comments AS Comments
            FROM shirt WHERE customerid=@id", new { id = customerId });
    }

    public async Task<PantMeasurement?> GetPantAsync(string branchKey, int customerId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        return await conn.QueryFirstOrDefaultAsync<PantMeasurement>(@"
            SELECT id AS Id, customerid AS CustomerId, waist AS Waist, hips AS Hips,
                   f_length AS FLength, inner_length AS InnerLength, k_balance AS KBalance,
                   bottom AS Bottom, thigh AS Thigh, fly AS Fly, b_fly AS BFly,
                   seat AS Seat, comments AS Comments
            FROM pant WHERE customerid=@id", new { id = customerId });
    }

    public async Task<WaistcoatMeasurement?> GetWaistcoatAsync(string branchKey, int customerId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        return await conn.QueryFirstOrDefaultAsync<WaistcoatMeasurement>(@"
            SELECT id AS Id, customerid AS CustomerId, length AS Length, chest AS Chest,
                   waist AS Waist, hips AS Hips, shoulder AS Shoulder,
                   cross_back AS CrossBack, armhole AS Armhole, comments AS Comments
            FROM waistcoat WHERE customerid=@id", new { id = customerId });
    }

    public async Task<ShalwarKameezMeasurement?> GetShalwarKameezAsync(string branchKey, int customerId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        return await conn.QueryFirstOrDefaultAsync<ShalwarKameezMeasurement>(@"
            SELECT id AS Id, customerid AS CustomerId,
                   k_length AS KLength, k_chest AS KChest, k_waist AS KWaist, k_hips AS KHips,
                   k_shoulder AS KShoulder, k_sleeve AS KSleeve, k_collar AS KCollar, k_daman AS KDaman,
                   s_length AS SLength, s_waist AS SWaist, s_hips AS SHips,
                   s_bottom AS SBottom, s_thigh AS SThigh, comments AS Comments
            FROM shalwar_kameez WHERE customerid=@id", new { id = customerId });
    }

    public async Task<CoatMeasurement?> GetCoatAsync(string branchKey, int customerId, string type)
    {
        using var conn = _db.CreateForBranch(branchKey);
        return await conn.QueryFirstOrDefaultAsync<CoatMeasurement>(@"
            SELECT id AS Id, customerid AS CustomerId, coat_type AS CoatType,
                   length AS Length, chest AS Chest, waist AS Waist, hips AS Hips,
                   shoulder AS Shoulder, sleeve AS Sleeve, back_length AS BackLength,
                   cross_back AS CrossBack, armhole AS Armhole, collar AS Collar,
                   lapel AS Lapel, comments AS Comments
            FROM coat WHERE customerid=@id AND coat_type=@type", new { id = customerId, type });
    }

    public async Task<Suit2PieceMeasurement?> GetSuit2PieceAsync(string branchKey, int customerId)
    {
        using var conn = _db.CreateForBranch(branchKey);
        return await conn.QueryFirstOrDefaultAsync<Suit2PieceMeasurement>(@"
            SELECT id AS Id, customerid AS CustomerId,
                   j_length AS JLength, j_chest AS JChest, j_waist AS JWaist, j_hips AS JHips,
                   j_shoulder AS JShoulder, j_sleeve AS JSleeve, j_back_length AS JBackLength,
                   j_cross_back AS JCrossBack, t_waist AS TWaist, t_hips AS THips,
                   t_length AS TLength, t_inner_length AS TInnerLength,
                   t_thigh AS TThigh, t_bottom AS TBottom, comments AS Comments
            FROM suit_2piece WHERE customerid=@id", new { id = customerId });
    }

    public async Task<MeasurementScan?> GetScanAsync(string branchKey, int customerId, string garmentType)
    {
        using var conn = _db.CreateForBranch(branchKey);
        return await conn.QueryFirstOrDefaultAsync<MeasurementScan>(@"
            SELECT scan_id AS ScanId, customerid AS CustomerId, garment_type AS GarmentType,
                   file_name AS FileName, content_type AS ContentType,
                   file_data AS FileData, uploaded_at AS UploadedAt
            FROM measurement_scans WHERE customerid=@id AND garment_type=@g",
            new { id = customerId, g = garmentType });
    }
}
