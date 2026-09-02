using Dapper;
using HusnaFactory.Data;
using HusnaFactory.Models;

namespace HusnaFactory.Services;

/// <summary>Filter sources — categories and outlets, unioned across both branch
/// databases so a category or outlet added on either side shows up automatically.</summary>
public class FactoryLookupService
{
    private readonly FactoryDbConnections _db;
    public FactoryLookupService(FactoryDbConnections db) => _db = db;

    public async Task<List<string>> GetCategoryNamesAsync()
    {
        var names = new List<string>();
        foreach (var branch in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(branch);
            var r = await conn.QueryAsync<string>(
                "SELECT cat_name FROM categories WHERE is_active=1 ORDER BY cat_name");
            names.AddRange(r);
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
    }

    public async Task<List<string>> GetOutletNamesAsync()
    {
        var names = new List<string>();
        foreach (var branch in new[] { Branches.Malir, Branches.Bukhari })
        {
            using var conn = _db.CreateForBranch(branch);
            var r = await conn.QueryAsync<string>(
                "SELECT outlet_name FROM outlets WHERE is_active=1 ORDER BY outlet_name");
            names.AddRange(r);
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
    }
}
