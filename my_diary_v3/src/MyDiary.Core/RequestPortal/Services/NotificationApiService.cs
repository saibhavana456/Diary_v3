using MyDiary.Core.Abstractions;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

public sealed class NotificationApiService : INotificationApi
{
    private readonly INotificationRepo _repo;
    private readonly INotificationPublisher _publisher;
    private readonly INotificationRepository _notification;

    public NotificationApiService(INotificationRepo repo, INotificationPublisher publisher, INotificationRepository notification)
    {
        _repo = repo; _publisher = publisher;
        _notification = notification;
    }

    public async Task<NotificationView> CreateAsync(CreateNotificationRequest req, CancellationToken ct = default)
    {
        if (req.UserId <= 0) throw new ArgumentException("UserId is required", nameof(req));
        if (string.IsNullOrWhiteSpace(req.Title)) throw new ArgumentException("Title is required", nameof(req));

        var item = new NotificationItem
        {
            UserId = req.UserId,
            Title = Trim(req.Title, 200)!,
            Message = Trim(req.Message, 2000),
            Category = Trim(req.Category, 50),
            RedirectUrl = Trim(req.RedirectUrl, 500),
            Source = req.Source,
            EventCode = Trim(req.EventCode, 50),
            RequestId = req.RequestId,
            CreatedAt = DateTime.UtcNow
        };       

        item.Id = await _repo.InsertAsync(item, ct);

        //await _notification.CreateInAppAsync(item, ct);

        var view = NotificationView.From(item);
        try { await _publisher.PublishAsync(view, ct); }
        catch { /* push best-effort; persistence already succeeded */ }
        return view;
    }

    public async Task<IReadOnlyList<NotificationView>> ListForUserAsync(long userId, int take, bool unreadOnly, CancellationToken ct = default)
    {
        var rows = await _repo.ListForUserAsync(userId, Math.Clamp(take, 1, 200), unreadOnly, ct);
        return rows.Select(NotificationView.From).ToList();
    }

    public Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default)
        => _repo.GetUnreadCountAsync(userId, ct);

    public Task MarkReadAsync(long userId, long notificationId, CancellationToken ct = default)
        => _repo.MarkReadAsync(userId, notificationId, DateTime.UtcNow, ct);

    public Task MarkAllReadAsync(long userId, CancellationToken ct = default)
        => _repo.MarkAllReadAsync(userId, DateTime.UtcNow, ct);

    private static string? Trim(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s!.Length <= max ? s : s[..max]);
}
