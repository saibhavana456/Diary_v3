using MyDiary.Core.Abstractions;
using MyDiary.Core.Services;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.Jobs;

/// <summary>
/// Hangfire recurring job (9 AM server time): emails a short adoption
/// summary — yesterday's unique logins, adoption % against the
/// VW_STAFF_USER_SUMMARY headcount, and the top module by usage — to the
/// configured recipient list. Runs after the 2 AM aggregation job so
/// yesterday's numbers are already rolled up.
///
/// Recipients: "Adoption:DigestRecipients" in appsettings (comma-separated
/// email addresses). Left empty by default — the job logs a warning and
/// skips sending rather than emailing no one silently or throwing.
/// </summary>
public sealed class AdoptionDigestEmailJob
{
    private readonly IPageUsageRepo _repo;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<AdoptionDigestEmailJob> _logger;

    public AdoptionDigestEmailJob(IPageUsageRepo repo, IEmailSender email, IConfiguration config, ILogger<AdoptionDigestEmailJob> logger)
    {
        _repo = repo;
        _email = email;
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var recipients = (_config["Adoption:DigestRecipients"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning("Adoption digest skipped — Adoption:DigestRecipients is not configured (no recipients).");
            return;
        }

        // "Yesterday" in IST, matching PageUsageAggregationJob's day boundary so
        // the digest reports the same rolled-up day regardless of the pod's UTC clock.
        var forDate = AppTime.Today.AddDays(-1);
        try
        {
            var (uniqueLogins, headcount, topModuleLabel, topModuleVisits) = await _repo.GetDailyDigestSummaryAsync(forDate, ct);
            var adoptionPct = headcount is > 0 ? Math.Round(100.0 * uniqueLogins / headcount.Value, 1) : (double?)null;

            var subject = $"My Diary — Adoption Digest for {forDate:dd MMM yyyy}";
            var body = $@"
                <h2>Adoption Digest — {forDate:dd MMM yyyy}</h2>
                <table cellpadding='6' style='border-collapse:collapse'>
                    <tr><td><b>Unique logins</b></td><td>{uniqueLogins}</td></tr>
                    <tr><td><b>Total headcount</b></td><td>{(headcount?.ToString() ?? "—")}</td></tr>
                    <tr><td><b>Adoption %</b></td><td>{(adoptionPct is not null ? $"{adoptionPct}%" : "—")}</td></tr>
                    <tr><td><b>Top module</b></td><td>{(topModuleLabel ?? "—")} ({topModuleVisits} visits)</td></tr>
                </table>
                <p style='color:#777;font-size:12px'>Full drill-down (Bank/Zone/Region/Branch), module &amp; menu usage,
                report generation, and insights are available on the Adoption Dashboard
                (Access Management → Adoption Dashboard).</p>";

            foreach (var to in recipients)
            {
                try
                {
                    await _email.SendAsync(to, subject, body, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Adoption digest failed to send to {Recipient}", to);
                }
            }

            _logger.LogInformation("Adoption digest sent to {Count} recipient(s) for {Date:yyyy-MM-dd}", recipients.Count, forDate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Adoption digest generation failed for {Date:yyyy-MM-dd}", forDate);
        }
    }
}
