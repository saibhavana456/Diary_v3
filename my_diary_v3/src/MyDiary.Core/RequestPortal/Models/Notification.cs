namespace RequestPortal.Core.Models;

public enum NotificationSource
{
    System = 0,
    Mail = 1,
    Sms = 2
}

public sealed class NotificationItem
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Title { get; set; } = "";
    public string? Message { get; set; }
    public string? Category { get; set; }
    public string? RedirectUrl { get; set; }
    public NotificationSource Source { get; set; }
    public string? EventCode { get; set; }
    public long? RequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
