using System.Text.Json;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

public interface IEmailSender
{
    Task SendAsync(string toAddr, string subject, string htmlBody, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task SendAsync(string toNumber, string text, CancellationToken ct = default);
}

public interface ITemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, object?> model);
}

public sealed class NotificationService : INotificationService
{
    private readonly INotifRepo _notif;
    private readonly IRequestRepo _requests;
    private readonly IUserRepo _users;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly ITemplateRenderer _renderer;
    private readonly INotificationApi _bell;
    private readonly IUnitOfWork _uow;

    public NotificationService(INotifRepo notif, IRequestRepo requests, IUserRepo users,
        IEmailSender email, ISmsSender sms, ITemplateRenderer renderer, INotificationApi bell, IUnitOfWork uow)
    {
        _notif = notif; _requests = requests; _users = users;
        _email = email; _sms = sms; _renderer = renderer; _bell = bell; _uow = uow;
    }

    public async Task EnqueueAsync(string eventCode, long requestId, IEnumerable<string> recipientEmpCodes, CancellationToken ct = default)
    {
        var detail = await _requests.GetDetailAsync(requestId, ct);
        if (detail is null) return;

        // Callers invoke this AFTER committing the request transaction, so the
        // outbox/bell inserts need their own committed transaction — otherwise
        // they run in an uncommitted implicit transaction and are lost on dispose.
        await _uow.BeginAsync(ct);
        try
        {
        // Batch-resolve all recipients in one DB call instead of N+1
        var distinctCodes = recipientEmpCodes.Distinct().ToList();
        var allUsers = await _users.GetByEmpCodesAsync(distinctCodes, ct);
        var userMap = allUsers
            .Where(u => u.IsActive)
            .GroupBy(u => u.EmpCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var empCode in distinctCodes)
        {
            if (!userMap.TryGetValue(empCode, out var u)) continue;

            var payload = JsonSerializer.Serialize(new
            {
                ReqNo = detail.ReqNo,
                Subject = detail.Subject,
                RequestTypeName = detail.RequestTypeName,
                CurrentLevel = detail.CurrentLevel,
                Status = detail.Status,
                RaisedByName = detail.RaisedByName,
                RecipientName = u.Name,
                RecipientEmpCode = empCode
            });

            if (!string.IsNullOrWhiteSpace(u.Email))
            {
                await _notif.EnqueueAsync(new NotifOutboxItem
                {
                    EventCode = eventCode,
                    Channel = NotifChannel.Email,
                    ToAddr = u.Email!,
                    PayloadJson = payload,
                    Status = NotifOutboxStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
            if (!string.IsNullOrWhiteSpace(u.Mobile))
            {
                await _notif.EnqueueAsync(new NotifOutboxItem
                {
                    EventCode = eventCode,
                    Channel = NotifChannel.Sms,
                    ToAddr = u.Mobile!,
                    PayloadJson = payload,
                    Status = NotifOutboxStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }

            var source = !string.IsNullOrWhiteSpace(u.Email) ? NotificationSource.Mail
                       : !string.IsNullOrWhiteSpace(u.Mobile) ? NotificationSource.Sms
                       : NotificationSource.System;
            try
            {
                await _bell.CreateAsync(new CreateNotificationRequest
                {
                    UserId = Convert.ToInt32(empCode),
                    Title = NotificationCopy.Title(eventCode, detail),
                    Message = NotificationCopy.Message(eventCode, detail),
                    Category = eventCode,
                    RedirectUrl = $"/requests/{detail.Id}",
                    Source = source,
                    EventCode = eventCode,
                    RequestId = detail.Id
                }, ct);
            }
            catch
            {
                // Bell failure must not break primary outbox enqueue.
            }
        }
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var items = await _notif.ListPendingAsync(batchSize, ct);
        var sent = 0;
        if (items.Count == 0) return 0;

        // Persist the Sent/Failed status updates in a committed transaction —
        // otherwise the marks are rolled back and every item is re-sent next tick.
        await _uow.BeginAsync(ct);
        try
        {
        foreach (var item in items)
        {
            try
            {
                var tpl = await _notif.GetTemplateAsync(item.EventCode, item.Channel, ct);
                if (tpl is null) { await _notif.MarkFailedAsync(item.Id, $"Missing template for {item.EventCode}/{item.Channel}", ct); continue; }

                var model = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.PayloadJson) ?? new();
                var subject = _renderer.Render(tpl.Subject ?? "", model);
                var body = _renderer.Render(tpl.Body, model);

                if (item.Channel == NotifChannel.Email)
                    await _email.SendAsync(item.ToAddr, subject, body, ct);
                else
                    await _sms.SendAsync(item.ToAddr, body, ct);

                await _notif.MarkSentAsync(item.Id, DateTime.UtcNow, ct);
                sent++;
            }
            catch (Exception ex)
            {
                await _notif.MarkFailedAsync(item.Id, ex.Message, ct);
            }
        }
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
        return sent;
    }
}
