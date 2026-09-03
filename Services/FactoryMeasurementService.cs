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

    // ── Writes — used by the Customers page so factory staff can update a
    // person's sizing hand-to-hand without going through the POS app. ──

    public async Task SaveShirtAsync(string branchKey, ShirtMeasurement m)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM shirt WHERE customerid=@id", new { id = m.CustomerId });
        if (count > 0)
            await conn.ExecuteAsync(@"UPDATE shirt SET
                length=NULLIF(@Length,''), chest=NULLIF(@Chest,''), waist=NULLIF(@Waist,''),
                hips=NULLIF(@Hips,''), shoulder=NULLIF(@Shoulder,''), sleeve=NULLIF(@Sleeve,''),
                collar=NULLIF(@Collar,''), cuff=NULLIF(@Cuff,''),
                comments=NULLIF(@Comments,'')
                WHERE customerid=@CustomerId", NullSafe(m));
        else
            await conn.ExecuteAsync(@"INSERT INTO shirt(customerid,length,chest,waist,hips,shoulder,sleeve,collar,cuff,comments)
                VALUES(@CustomerId,NULLIF(@Length,''),NULLIF(@Chest,''),NULLIF(@Waist,''),
                NULLIF(@Hips,''),NULLIF(@Shoulder,''),NULLIF(@Sleeve,''),
                NULLIF(@Collar,''),NULLIF(@Cuff,''),NULLIF(@Comments,''))", NullSafe(m));
    }

    public async Task SavePantAsync(string branchKey, PantMeasurement m)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM pant WHERE customerid=@id", new { id = m.CustomerId });
        if (count > 0)
            await conn.ExecuteAsync(@"UPDATE pant SET
                waist=NULLIF(@Waist,''), hips=NULLIF(@Hips,''), f_length=NULLIF(@FLength,''),
                inner_length=NULLIF(@InnerLength,''), k_balance=NULLIF(@KBalance,''),
                bottom=NULLIF(@Bottom,''), thigh=NULLIF(@Thigh,''), fly=NULLIF(@Fly,''),
                b_fly=NULLIF(@BFly,''), seat=NULLIF(@Seat,''),
                comments=NULLIF(@Comments,'')
                WHERE customerid=@CustomerId", NullSafe(m));
        else
            await conn.ExecuteAsync(@"INSERT INTO pant(customerid,waist,hips,f_length,inner_length,k_balance,bottom,thigh,fly,b_fly,seat,comments)
                VALUES(@CustomerId,NULLIF(@Waist,''),NULLIF(@Hips,''),NULLIF(@FLength,''),
                NULLIF(@InnerLength,''),NULLIF(@KBalance,''),NULLIF(@Bottom,''),
                NULLIF(@Thigh,''),NULLIF(@Fly,''),NULLIF(@BFly,''),NULLIF(@Seat,''),NULLIF(@Comments,''))", NullSafe(m));
    }

    public async Task SaveWaistcoatAsync(string branchKey, WaistcoatMeasurement m)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM waistcoat WHERE customerid=@id", new { id = m.CustomerId });
        if (count > 0)
            await conn.ExecuteAsync(@"UPDATE waistcoat SET
                length=NULLIF(@Length,''), chest=NULLIF(@Chest,''), waist=NULLIF(@Waist,''),
                hips=NULLIF(@Hips,''), shoulder=NULLIF(@Shoulder,''),
                cross_back=NULLIF(@CrossBack,''), armhole=NULLIF(@Armhole,''),
                comments=NULLIF(@Comments,'')
                WHERE customerid=@CustomerId", NullSafe(m));
        else
            await conn.ExecuteAsync(@"INSERT INTO waistcoat(customerid,length,chest,waist,hips,shoulder,cross_back,armhole,comments)
                VALUES(@CustomerId,NULLIF(@Length,''),NULLIF(@Chest,''),NULLIF(@Waist,''),
                NULLIF(@Hips,''),NULLIF(@Shoulder,''),NULLIF(@CrossBack,''),
                NULLIF(@Armhole,''),NULLIF(@Comments,''))", NullSafe(m));
    }

    public async Task SaveShalwarKameezAsync(string branchKey, ShalwarKameezMeasurement m)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM shalwar_kameez WHERE customerid=@id", new { id = m.CustomerId });
        if (count > 0)
            await conn.ExecuteAsync(@"UPDATE shalwar_kameez SET
                k_length=NULLIF(@KLength,''), k_chest=NULLIF(@KChest,''), k_waist=NULLIF(@KWaist,''),
                k_hips=NULLIF(@KHips,''), k_shoulder=NULLIF(@KShoulder,''), k_sleeve=NULLIF(@KSleeve,''),
                k_collar=NULLIF(@KCollar,''), k_daman=NULLIF(@KDaman,''),
                s_length=NULLIF(@SLength,''), s_waist=NULLIF(@SWaist,''), s_hips=NULLIF(@SHips,''),
                s_bottom=NULLIF(@SBottom,''), s_thigh=NULLIF(@SThigh,''),
                comments=NULLIF(@Comments,'')
                WHERE customerid=@CustomerId", NullSafe(m));
        else
            await conn.ExecuteAsync(@"INSERT INTO shalwar_kameez(customerid,k_length,k_chest,k_waist,k_hips,k_shoulder,k_sleeve,k_collar,k_daman,s_length,s_waist,s_hips,s_bottom,s_thigh,comments)
                VALUES(@CustomerId,NULLIF(@KLength,''),NULLIF(@KChest,''),NULLIF(@KWaist,''),NULLIF(@KHips,''),
                NULLIF(@KShoulder,''),NULLIF(@KSleeve,''),NULLIF(@KCollar,''),NULLIF(@KDaman,''),
                NULLIF(@SLength,''),NULLIF(@SWaist,''),NULLIF(@SHips,''),NULLIF(@SBottom,''),NULLIF(@SThigh,''),NULLIF(@Comments,''))", NullSafe(m));
    }

    public async Task SaveCoatAsync(string branchKey, CoatMeasurement m)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM coat WHERE customerid=@id AND coat_type=@t", new { id = m.CustomerId, t = m.CoatType });
        if (count > 0)
            await conn.ExecuteAsync(@"UPDATE coat SET
                length=NULLIF(@Length,''), chest=NULLIF(@Chest,''), waist=NULLIF(@Waist,''),
                hips=NULLIF(@Hips,''), shoulder=NULLIF(@Shoulder,''), sleeve=NULLIF(@Sleeve,''),
                back_length=NULLIF(@BackLength,''), cross_back=NULLIF(@CrossBack,''),
                armhole=NULLIF(@Armhole,''), collar=NULLIF(@Collar,''), lapel=NULLIF(@Lapel,''),
                comments=NULLIF(@Comments,'')
                WHERE customerid=@CustomerId AND coat_type=@CoatType", NullSafe(m));
        else
            await conn.ExecuteAsync(@"INSERT INTO coat(customerid,coat_type,length,chest,waist,hips,shoulder,sleeve,back_length,cross_back,armhole,collar,lapel,comments)
                VALUES(@CustomerId,@CoatType,NULLIF(@Length,''),NULLIF(@Chest,''),NULLIF(@Waist,''),
                NULLIF(@Hips,''),NULLIF(@Shoulder,''),NULLIF(@Sleeve,''),NULLIF(@BackLength,''),
                NULLIF(@CrossBack,''),NULLIF(@Armhole,''),NULLIF(@Collar,''),NULLIF(@Lapel,''),NULLIF(@Comments,''))", NullSafe(m));
    }

    public async Task SaveSuit2PieceAsync(string branchKey, Suit2PieceMeasurement m)
    {
        using var conn = _db.CreateForBranch(branchKey);
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM suit_2piece WHERE customerid=@id", new { id = m.CustomerId });
        if (count > 0)
            await conn.ExecuteAsync(@"UPDATE suit_2piece SET
                j_length=NULLIF(@JLength,''), j_chest=NULLIF(@JChest,''), j_waist=NULLIF(@JWaist,''),
                j_hips=NULLIF(@JHips,''), j_shoulder=NULLIF(@JShoulder,''), j_sleeve=NULLIF(@JSleeve,''),
                j_back_length=NULLIF(@JBackLength,''), j_cross_back=NULLIF(@JCrossBack,''),
                t_waist=NULLIF(@TWaist,''), t_hips=NULLIF(@THips,''), t_length=NULLIF(@TLength,''),
                t_inner_length=NULLIF(@TInnerLength,''), t_thigh=NULLIF(@TThigh,''),
                t_bottom=NULLIF(@TBottom,''), comments=NULLIF(@Comments,'')
                WHERE customerid=@CustomerId", NullSafe(m));
        else
            await conn.ExecuteAsync(@"INSERT INTO suit_2piece(customerid,j_length,j_chest,j_waist,j_hips,j_shoulder,j_sleeve,j_back_length,j_cross_back,t_waist,t_hips,t_length,t_inner_length,t_thigh,t_bottom,comments)
                VALUES(@CustomerId,NULLIF(@JLength,''),NULLIF(@JChest,''),NULLIF(@JWaist,''),NULLIF(@JHips,''),
                NULLIF(@JShoulder,''),NULLIF(@JSleeve,''),NULLIF(@JBackLength,''),NULLIF(@JCrossBack,''),
                NULLIF(@TWaist,''),NULLIF(@THips,''),NULLIF(@TLength,''),NULLIF(@TInnerLength,''),
                NULLIF(@TThigh,''),NULLIF(@TBottom,''),NULLIF(@Comments,''))", NullSafe(m));
    }

    static object NullSafe<T>(T obj)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in typeof(T).GetProperties())
        {
            var val = prop.GetValue(obj);
            dict[prop.Name] = val is string s ? (object)(s ?? string.Empty) : val;
        }
        return dict;
    }
}
