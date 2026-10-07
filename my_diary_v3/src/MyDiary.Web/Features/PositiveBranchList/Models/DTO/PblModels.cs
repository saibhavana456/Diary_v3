namespace MyDiary.Web.Features.PositiveBranchList.Models.DTO;

public class PblJobStatus
{
    public DateTime StatusDate { get; set; }
    public DateTime? LatestDataDate { get; set; }
    public bool DataAvailable { get; set; }
    public string AggStatus { get; set; } = "PENDING";
    public bool MailSent { get; set; }
    public DateTime? MailSentAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public string? LastError { get; set; }
    public string? TriggeredBy { get; set; }
}

public class PblMailLogRow
{
    public long Id { get; set; }
    public DateTime RunDate { get; set; }
    public string ZoneName { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string? Body { get; set; }
    public string? AttachmentFileName { get; set; }
    public string SendStatus { get; set; } = "";
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string TriggeredBy { get; set; } = "";
}

/// <summary>Result of one run of the aggregate-and-send pipeline, returned to both the
/// Hangfire job (to decide whether to schedule an hourly retry) and the manual-trigger
/// button (to show the operator what happened).</summary>
public class PblRunResult
{
    public bool DataAvailable { get; set; }
    public DateTime CurrentDataDate { get; set; }
    public DateTime AnchorDate { get; set; }
    public bool MailSent { get; set; }
    public int ZonesProcessed { get; set; }
    public int ZonesSucceeded { get; set; }
    public int ZonesFailed { get; set; }
    public string? Message { get; set; }
}
