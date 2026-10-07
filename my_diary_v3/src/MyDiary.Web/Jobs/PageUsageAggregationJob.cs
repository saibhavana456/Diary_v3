using MyDiary.Core.Services;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.Jobs;

/// <summary>
/// Hangfire recurring job (2 AM server time): rolls yesterday's raw page-visit
/// log into the daily aggregate, then recomputes the underused-page ranking
/// snapshot used by the Menu Access Dashboard's "least-used pages" panel.
/// Idempotent — safe to re-run for the same day.
/// </summary>
public sealed class PageUsageAggregationJob
{
    private readonly IPageUsageRepo _repo;
    private readonly ILogger<PageUsageAggregationJob> _logger;

    public PageUsageAggregationJob(IPageUsageRepo repo, ILogger<PageUsageAggregationJob> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // Aggregate "yesterday" relative to IST (the Indian business day), not the
        // container's UTC clock — otherwise the day boundary is off by 5.5h under
        // Kubernetes and a day's visits get split across two aggregate rows.
        var forDate = AppTime.Today.AddDays(-1);

        try
        {
            await _repo.RunNightlyAggregationAsync(forDate, ct);
            _logger.LogInformation("Page usage aggregation completed for {Date:yyyy-MM-dd}", forDate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Page usage aggregation failed for {Date:yyyy-MM-dd}", forDate);
        }

        try
        {
            await _repo.RunLoginAggregationAsync(forDate, ct);
            _logger.LogInformation("Login aggregation completed for {Date:yyyy-MM-dd}", forDate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Login aggregation failed for {Date:yyyy-MM-dd}", forDate);
        }

        try
        {
            await _repo.RunReportGenerationAggregationAsync(forDate, ct);
            _logger.LogInformation("Report-generation aggregation completed for {Date:yyyy-MM-dd}", forDate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Report-generation aggregation failed for {Date:yyyy-MM-dd}", forDate);
        }
    }
}
