using Microsoft.AspNetCore.SignalR;
using MyDiary.Core.Abstractions;
using MyDiary.Web.Hubs;

namespace MyDiary.Web.Jobs;

/// <summary>
/// Hangfire recurring job: dispatches pending outbox notifications (email/SMS)
/// and pushes new in-app notifications via SignalR (from Project A's pattern).
/// Runs every 1 minute.
/// </summary>
public sealed class NotificationDispatchJob
{
    private readonly INotificationRepository        _repo;
    private readonly IHubContext<NotificationHub>   _hub;
    private readonly IEmailSender                   _email;
    private readonly ISmsSender                     _sms;
    private readonly global::RequestPortal.Core.Abstractions.INotificationService _rpNotify;
    private readonly ILogger<NotificationDispatchJob> _logger;

    public NotificationDispatchJob(
        INotificationRepository repo,
        IHubContext<NotificationHub> hub,
        IEmailSender email,
        ISmsSender sms,
        global::RequestPortal.Core.Abstractions.INotificationService rpNotify,
        ILogger<NotificationDispatchJob> logger)
    {
        _repo   = repo;
        _hub    = hub;
        _email  = email;
        _sms    = sms;
        _rpNotify = rpNotify;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // Dispatch the Request Portal outbox (RP_NOTIF_OUTBOX → email/SMS for
        // request lifecycle events: created / forwarded / resolved / …).
        try
        {
            var rpSent = await _rpNotify.DispatchPendingAsync(50, ct);
            if (rpSent > 0) _logger.LogInformation("Dispatched {Count} Request Portal notifications", rpSent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Request Portal notification dispatch failed");
        }

        var pending = await _repo.ListPendingAsync(batchSize: 50, ct);
        if (pending.Count == 0) return;

        _logger.LogInformation("Dispatching {Count} pending notifications", pending.Count);

        foreach (var item in pending)
        {
            try
            {
                switch (item.Channel)
                {
                    case MyDiary.Core.NotifChannel.InApp:
                        await NotificationHub.PushToUserAsync(_hub, item.ToEmpCode, new
                        {
                            id        = item.Id,
                            title     = item.EventCode,
                            createdAt = item.CreatedAt
                        });
                        break;

                    case MyDiary.Core.NotifChannel.Email:
                        // Render the subject/body from the template; fall back to payload
                        var tpl = await _repo.GetTemplateAsync(item.EventCode, MyDiary.Core.NotifChannel.Email, ct);
                        var subject = tpl?.Subject ?? item.EventCode;
                        var body    = tpl?.Body ?? item.PayloadJson ?? string.Empty;
                        await _email.SendAsync(item.ToAddress, subject, body, ct);
                        break;

                    case MyDiary.Core.NotifChannel.Sms:
                        await _sms.SendAsync(item.ToAddress, item.PayloadJson ?? item.EventCode, ct);
                        break;
                }

                await _repo.MarkSentAsync(item.Id, DateTime.UtcNow, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch notification {Id}", item.Id);
                await _repo.MarkFailedAsync(item.Id, ex.Message, ct);
            }
        }
    }
}
