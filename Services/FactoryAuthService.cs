using Dapper;
using HusnaFactory.Data;

namespace HusnaFactory.Services;

public class FactoryAuthService
{
    private readonly FactoryDbConnections _db;

    public bool IsLoggedIn { get; private set; }
    public string LoggedInUser { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public int? LoggedInUserId { get; private set; }

    public event Action? OnAuthChanged;

    public FactoryAuthService(FactoryDbConnections db) => _db = db;

    public async Task<bool> LoginAsync(string username, string password)
    {
        using var conn = _db.CreateFactoryHubConnection();
        var user = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT factory_user_id, username, password, full_name
            FROM factory_users
            WHERE username = @u AND is_active = 1",
            new { u = username });

        if (user == null) return false;

        string storedPassword = (string)user.password;
        bool valid;
        if (storedPassword.StartsWith("$2"))
            valid = BCrypt.Net.BCrypt.Verify(password, storedPassword);
        else
            valid = storedPassword == password;

        if (valid)
        {
            IsLoggedIn = true;
            LoggedInUserId = (int)user.factory_user_id;
            LoggedInUser = (string)(user.username ?? username);
            FullName = (string)(user.full_name ?? user.username ?? username);
            OnAuthChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void Logout()
    {
        IsLoggedIn = false;
        LoggedInUser = "";
        FullName = "";
        LoggedInUserId = null;
        OnAuthChanged?.Invoke();
    }
}
