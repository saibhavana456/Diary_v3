namespace RequestPortal.Core.Models;

public sealed class Request
{
    public long Id { get; set; }
    public string ReqNo { get; set; } = "";
    public long RequestTypeId { get; set; }
    public long UnitId { get; set; }
    public long VerticalId { get; set; }
    public long DepartmentId { get; set; }
    public long ActivityId { get; set; }
    public long RaisedBy { get; set; }           // AppUser.Id (FK to RP_USER.ID)
    public string RaisedByEmp { get; set; } = ""; // Employee code (VARCHAR2, NOT NULL)
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public int CurrentLevel { get; set; } = 1;
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;
    public DateTime? SlaDueUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int RowVersion { get; set; }
    public string? ExtAlertId { get; set; }      // external reference (e.g. UCCRMC Alert ID)
    public string? ExtSource { get; set; }        // originating system tag (e.g. "UCCRMC")
}

public sealed class RequestAssignee
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public int LevelNo { get; set; }
    public string EmpCode { get; set; } = "";
    public string? AssignedByEmp { get; set; }
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
}

public sealed class RequestAction
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public int LevelNo { get; set; }
    public string ActorEmpCode { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Remarks { get; set; }
    public DateTime ActedAt { get; set; }
}

public sealed class Clarification
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public string AskedByEmpCode { get; set; } = "";
    public DateTime AskedAt { get; set; }
    public DateTime? RepliedAt { get; set; }
    public string Question { get; set; } = "";
    public string? Answer { get; set; }
}

public sealed class SlaPause
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public DateTime PausedAt { get; set; }
    public DateTime? ResumedAt { get; set; }
    public string? Reason { get; set; }
}

public sealed class Attachment
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public long? ActionId { get; set; }
    public string FileName { get; set; } = "";
    public string Mime { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";
    public string UploadedByEmp { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class Feedback
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public int Rating { get; set; }
    public string? Comments { get; set; }
    public DateTime GivenAt { get; set; }
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public string Entity { get; set; } = "";
    public long EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? ActorEmpCode { get; set; }
    public DateTime ActedAt { get; set; }
    public string? OldJson { get; set; }
    public string? NewJson { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? HashPrev { get; set; }
    public string HashCurr { get; set; } = "";
}

public sealed class NotifOutboxItem
{
    public long Id { get; set; }
    public string EventCode { get; set; } = "";
    public NotifChannel Channel { get; set; }
    public string ToAddr { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public NotifOutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
