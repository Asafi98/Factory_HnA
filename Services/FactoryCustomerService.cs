using Dapper;
using HusnaFactory.Data;
using HusnaFactory.Models;

namespace HusnaFactory.Services;

/// <summary>Customer lookup by name/phone across both branches, for factory staff to
/// find a person and update their sizing hand-to-hand — deliberately never touches
/// the customer/payments tables' financial columns.</summary>
public class FactoryCustomerService
{
    private readonly FactoryDbConnections _db;
    public FactoryCustomerService(FactoryDbConnections db) => _db = db;

    public async Task<List<FactoryCustomer>> SearchAsync(string text)
    {
        var results = new List<FactoryCustomer>();
        if (string.IsNullOrWhiteSpace(text)) return results;

        foreach (var (key, label) in new[]
                 {
                     (Branches.Malir, "Malir Cantt Branch"),
                     (Branches.Bukhari, "Bukhari Branch")
                 })
        {
            using var conn = _db.CreateForBranch(key);
            var r = await conn.QueryAsync<FactoryCustomer>(@"
                SELECT customerid AS CustomerId, customername AS CustomerName,
                       customerphone AS CustomerPhone, customeraddress AS CustomerAddress
                FROM customer
                WHERE is_active=1 AND (customername LIKE @t OR customerphone LIKE @t)
                ORDER BY customername LIMIT 50",
                new { t = $"%{text}%" });

            foreach (var c in r)
            {
                c.BranchKey = key;
                c.BranchLabel = label;
                results.Add(c);
            }
        }
        return results;
    }
}
