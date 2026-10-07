using RequestPortal.Core.Models;

namespace RequestPortal.Core.Dtos;

public sealed class CreateNotificationRequest
{
    public long UserId { get; set; }
    public string Title { get; set; } = "";
    public string? Message { get; set; }
    public string? Category { get; set; }
    public string? RedirectUrl { get; set; }
    public NotificationSource Source { get; set; } = NotificationSource.System;
    public string? EventCode { get; set; }
    public long? RequestId { get; set; }
}

public sealed class NotificationView
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Title { get; set; } = "";
    public string? Message { get; set; }
    public string? Category { get; set; }
    public string? RedirectUrl { get; set; }
    public string Source { get; set; } = "";
    public string? EventCode { get; set; }
    public long? RequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead => ReadAt.HasValue;

    public static NotificationView From(NotificationItem n) => new()
    {
        Id = n.Id,
        UserId = n.UserId,
        Title = n.Title,
        Message = n.Message,
        Category = n.Category,
        RedirectUrl = n.RedirectUrl,
        Source = n.Source.ToString(),
        EventCode = n.EventCode,
        RequestId = n.RequestId,
        CreatedAt = n.CreatedAt,
        ReadAt = n.ReadAt,
    };
}

public sealed class UnreadCountResult
{
    public int Count { get; set; }
}
