using Microsoft.Extensions.Caching.Memory;
using HusnaFactory.Models;

namespace HusnaFactory.Services;

/// <summary>
/// Shared in-memory cache for Factory data. All heavy queries go through here.
/// Cache lives 2 minutes then auto-expires. Any write operation calls Invalidate()
/// so the next read gets fresh data.
/// </summary>
public class FactoryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly FactoryOrderService _orders;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private const string KeyAllOrders = "factory:all_orders";
    private const string KeyStageAverages = "factory:stage_averages";
    private const string KeyGarmentSummary = "factory:garment_summary";
    private const string KeyPaymentReminders = "factory:payment_reminders";
    private const string KeyThroughput = "factory:throughput";
    private const string KeyProductionReport = "factory:production_report";
    private const string KeySentToFactory = "factory:sent_to_factory";
    private const string KeyOutsourcedOrders = "factory:outsourced_orders";

    private static readonly string[] AllKeys =
    {
        KeyAllOrders, KeyStageAverages, KeyGarmentSummary, KeyPaymentReminders,
        KeyThroughput, KeyProductionReport, KeySentToFactory, KeyOutsourcedOrders
    };

    public FactoryCacheService(IMemoryCache cache, FactoryOrderService orders)
    {
        _cache = cache;
        _orders = orders;
    }

    public void Invalidate()
    {
        foreach (var key in AllKeys)
            _cache.Remove(key);
    }

    public Task<List<FactoryOrder>> GetAllOrdersAsync() =>
        _cache.GetOrCreateAsync(KeyAllOrders, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetAllOrdersAsync();
        })!;

    public Task<List<StageAverage>> GetStageAveragesAsync() =>
        _cache.GetOrCreateAsync(KeyStageAverages, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetStageAveragesAsync();
        })!;

    public Task<List<GarmentTypeSummary>> GetGarmentTypeSummaryAsync() =>
        _cache.GetOrCreateAsync(KeyGarmentSummary, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetGarmentTypeSummaryAsync();
        })!;

    public Task<List<FactoryOrder>> GetOverduePaymentsAsync() =>
        _cache.GetOrCreateAsync(KeyPaymentReminders, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetOverduePaymentsAsync();
        })!;

    public Task<(int Received, int Completed, int TotalChanges)> GetThroughputAsync(DateTime date) =>
        _cache.GetOrCreateAsync(KeyThroughput, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetThroughputAsync(date);
        })!;

    public Task<List<ProductionReportRow>> GetProductionReportAsync(int months = 6) =>
        _cache.GetOrCreateAsync(KeyProductionReport, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetProductionReportAsync(months);
        })!;

    public Task<List<FactoryOrder>> GetSentToFactoryAsync() =>
        _cache.GetOrCreateAsync(KeySentToFactory, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetSentToFactoryAsync();
        })!;

    public Task<List<FactoryOrder>> GetOutsourcedOrdersAsync() =>
        _cache.GetOrCreateAsync(KeyOutsourcedOrders, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _orders.GetOutsourcedOrdersAsync();
        })!;
}
