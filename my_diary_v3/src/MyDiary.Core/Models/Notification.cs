namespace MyDiary.Core.Models;

public sealed class NotifOutboxItem
{
    public long              Id          { get; set; }
    public string            EventCode   { get; set; } = "";
    public NotifChannel      Channel     { get; set; }
    public string            ToAddress   { get; set; } = "";
    public string            ToEmpCode   { get; set; } = "";
    public string?           PayloadJson { get; set; }
    public NotifOutboxStatus Status      { get; set; }
    public int               Attempts    { get; set; }
    public string?           LastError   { get; set; }
    public DateTime          CreatedAt   { get; set; }
    public DateTime?         SentAt      { get; set; }
}

public sealed class NotifTemplate
{
    public long    Id        { get; set; }
    public string  EventCode { get; set; } = "";
    public NotifChannel Channel  { get; set; }
    public string? Subject   { get; set; }
    public string  Body      { get; set; } = "";
    public bool    IsActive  { get; set; } = true;
}

// ── In-app notification bell ──────────────────────────────────────────────
public sealed class InAppNotification
{
    public long      Id         { get; set; }
    public string    EmpCode    { get; set; } = "";
    public string    Title      { get; set; } = "";
    public string?   Body       { get; set; }
    public string?   Href       { get; set; }
    public bool      IsRead     { get; set; }
    public DateTime  CreatedAt  { get; set; }
}
