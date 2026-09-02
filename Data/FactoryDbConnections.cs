using MySqlConnector;

namespace HusnaFactory.Data;

/// <summary>
/// Unlike the POS app (which switches between ONE branch at a time), the factory app
/// needs all three databases available simultaneously: it reads/writes both branch
/// databases (to merge orders from both shops and sync status back) and reads/writes
/// its own separate factory_hub database (factory staff accounts, independent from
/// the POS's `user` table).
/// </summary>
public class FactoryDbConnections
{
    private readonly IConfiguration _config;
    public FactoryDbConnections(IConfiguration config) => _config = config;

    public MySqlConnection CreateMalirConnection()   => Create("MalirConnection");
    public MySqlConnection CreateBukhariConnection() => Create("NewBranchConnection");
    public MySqlConnection CreateFactoryHubConnection() => Create("FactoryHubConnection");

    /// <summary>Resolve a branch-tagged connection by key ("malir" / "bukhari") — used
    /// when writing a status update back to whichever database the order came from.</summary>
    public MySqlConnection CreateForBranch(string branchKey) => branchKey switch
    {
        Models.Branches.Malir   => CreateMalirConnection(),
        Models.Branches.Bukhari => CreateBukhariConnection(),
        _ => throw new ArgumentOutOfRangeException(nameof(branchKey), branchKey, "Unknown branch key")
    };

    private MySqlConnection Create(string key)
    {
        var connStr = _config.GetConnectionString(key);
        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException(
                $"Connection string '{key}' is not set. Set it via the ConnectionStrings__{key} " +
                "environment variable in production (appsettings.json intentionally ships blank).");
        return new MySqlConnection(connStr);
    }
}
