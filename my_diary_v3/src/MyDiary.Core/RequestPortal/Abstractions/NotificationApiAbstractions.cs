using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

public interface INotificationApi
{
    Task<NotificationView> CreateAsync(CreateNotificationRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationView>> ListForUserAsync(long userId, int take, bool unreadOnly, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default);
    Task MarkReadAsync(long userId, long notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(long userId, CancellationToken ct = default);
}

public interface INotificationRepo
{
    Task<long> InsertAsync(NotificationItem n, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationItem>> ListForUserAsync(long userId, int take, bool unreadOnly, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default);
    Task MarkReadAsync(long userId, long notificationId, DateTime readAt, CancellationToken ct = default);
    Task MarkAllReadAsync(long userId, DateTime readAt, CancellationToken ct = default);
}

public interface INotificationPublisher
{
    Task PublishAsync(NotificationView notification, CancellationToken ct = default);
}
