using global::RequestPortal.Core;
using global::RequestPortal.Core.Abstractions;
using global::RequestPortal.Core.Dtos;
using global::RequestPortal.Core.Models;

namespace MyDiary.Web.Features.DigitalBusinessVertical.Jobs;

/// <summary>Hangfire background job: BRD "in-app push notifications whenever a new campaign is created, or a new
/// correspondence is added". Fans out one in-app notification per user in the bank-wide audience — done in the
/// background so saving a campaign does not wait on thousands of inserts (same mechanism as the Broadcast admin page).</summary>
public sealed class DbvPushJob
{
    private readonly IBroadcastDirectory _directory;
    private readonly INotificationApi _api;
    private readonly ILogger<DbvPushJob> _log;

    public DbvPushJob(IBroadcastDirectory directory, INotificationApi api, ILogger<DbvPushJob> log)
    {
        _directory = directory; _api = api; _log = log;
    }

    public async Task RunAsync(string title, string message, string redirectUrl, string category)
    {
        var audience = await _directory.ResolveAudienceAsync(new BroadcastScope(BroadcastScopeKind.BankWide));
        int ok = 0, failed = 0;
        foreach (var u in audience)
        {
            try
            {
                await _api.CreateAsync(new CreateNotificationRequest
                {
                    UserId = u.Id, Title = title, Message = message, Category = category,
                    EventCode = "DbvUpdate", Source = NotificationSource.System, RedirectUrl = redirectUrl
                });
                ok++;
            }
            catch (Exception ex) { failed++; _log.LogWarning(ex, "DBV push failed for user {UserId}", u.Id); }
        }
        _log.LogInformation("DBV push '{Title}': {Ok} sent, {Failed} failed", title, ok, failed);
    }
}
