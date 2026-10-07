using Hangfire;
using MyDiary.Web.Features.PositiveBranchList.Services;

namespace MyDiary.Web.Jobs;

/// <summary>
/// Hangfire recurring job (10:30 AM server time): runs the Positive Branch List
/// aggregate-and-mail pipeline for T-1's data over the previous financial year's last day.
/// If T-1's data isn't in yet, <see cref="IPositiveBranchListService.RunAggregationAndSendAsync"/>
/// leaves today's mail-sent flag false and this job self-reschedules a one-off retry exactly
/// 1 hour later (capped at 12 retries/day as a safety net against an endless loop) — per the
/// explicit "if data is not available then retry the job after 1 hour" instruction.
/// </summary>
public sealed class PositiveBranchListDailyJob
{
    private const int MaxRetriesPerDay = 12;

    private readonly IPositiveBranchListService _service;
    private readonly ILogger<PositiveBranchListDailyJob> _logger;

    public PositiveBranchListDailyJob(IPositiveBranchListService service, ILogger<PositiveBranchListDailyJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var result = await _service.RunAggregationAndSendAsync(triggeredBy: "SYSTEM", force: false);

        if (result.MailSent)
        {
            _logger.LogInformation("Positive Branch List: {Message}", result.Message);
            return;
        }

        if (!result.DataAvailable)
        {
            var status = await _service.GetTodayStatusAsync();
            if (status.RetryCount >= MaxRetriesPerDay)
            {
                _logger.LogWarning("Positive Branch List: giving up for today after {Count} retries — data for {Date:yyyy-MM-dd} never arrived.",
                    status.RetryCount, result.CurrentDataDate);
                return;
            }

            _logger.LogInformation("Positive Branch List: {Message} Retrying in 1 hour (attempt {Count}).", result.Message, status.RetryCount);
            BackgroundJob.Schedule<PositiveBranchListDailyJob>(j => j.RunAsync(CancellationToken.None), TimeSpan.FromHours(1));
        }
        else
        {
            _logger.LogWarning("Positive Branch List: run did not complete — {Message}", result.Message);
        }
    }
}
