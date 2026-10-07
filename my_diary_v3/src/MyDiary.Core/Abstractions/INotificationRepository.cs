using MyDiary.Core.Models;

namespace MyDiary.Core.Abstractions;

public interface INotificationRepository
{
    // Outbox (email/SMS)
    Task<NotifTemplate?>                GetTemplateAsync(string eventCode, NotifChannel channel, CancellationToken ct = default);
    Task<long>                          EnqueueAsync(NotifOutboxItem item, CancellationToken ct = default);
    Task<IReadOnlyList<NotifOutboxItem>> ListPendingAsync(int batchSize, CancellationToken ct = default);
    Task                                MarkSentAsync(long id, DateTime sentAt, CancellationToken ct = default);
    Task                                MarkFailedAsync(long id, string error, CancellationToken ct = default);

    // Template CRUD
    Task<IReadOnlyList<NotifTemplate>> ListTemplatesAsync(CancellationToken ct = default);
    Task<long>  InsertTemplateAsync(NotifTemplate m, CancellationToken ct = default);
    Task        UpdateTemplateAsync(NotifTemplate m, CancellationToken ct = default);
    Task        SetTemplateActiveAsync(long id, bool isActive, CancellationToken ct = default);

    // In-app bell
    Task<long>                              CreateInAppAsync(InAppNotification n, CancellationToken ct = default);
    Task<IReadOnlyList<InAppNotification>>  ListInAppAsync(string empCode, int take, CancellationToken ct = default);
    Task<int>                               UnreadCountAsync(string empCode, CancellationToken ct = default);
    Task                                    MarkInAppReadAsync(long id, CancellationToken ct = default);
    Task                                    MarkAllInAppReadAsync(string empCode, CancellationToken ct = default);
}
