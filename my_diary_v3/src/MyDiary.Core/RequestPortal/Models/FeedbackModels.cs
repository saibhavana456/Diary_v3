namespace RequestPortal.Core.Models;

// ─────────────────────────────────────────────────────────────────────────
// User Feedback — its own system (RP_FEEDBACK_TICKET/RP_FEEDBACK_UPDATE/
// RP_FEEDBACK_ATTACHMENT), deliberately NOT merged into the Request Portal's
// RP_REQUEST ticketing engine. The only thing reused from the Request Portal
// is its Escalation Matrix (IEscalationMatrixRepo / RP_ESCALATION_MATRIX,
// keyed to RP_M_ACTIVITY "Feedback - My Diary 2.0") for L1-L5 PF-number
// lookups — CurrentLevel here only ever changes when an admin explicitly
// reassigns it; there is no auto-escalation for Feedback tickets.
// ─────────────────────────────────────────────────────────────────────────

public enum FeedbackCategory { FunctionalDefects, PerformanceIssues, UiUxImprovements, NewFeatureRequests, DataIssues }

public enum FeedbackSeverity { Low, Medium, High, Critical }

public enum FeedbackStatus { Open, InProgress, Closed }

/// <summary>The logged-in user's identity/org details, auto-filled into the
/// Feedback submission page. Name/Zone/Region/Branch/Mobile are sourced from the
/// authenticated user's claims (AuthState/CustomAuthState) via
/// AuthStateEmployeeProfileService. PfNumber is not carried in AuthState, so it
/// is left blank; all fields remain editable on the form.</summary>
public sealed class EmployeeProfile
{
    public string EmpCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? PfNumber { get; set; }
    public string? Zone { get; set; }
    public string? Region { get; set; }
    public string? Branch { get; set; }
    public string? MobileNumber { get; set; }
}

public sealed class FeedbackTicket
{
    public long Id { get; set; }
    public string TicketNo { get; set; } = "";      // e.g. FDB-000123
    public string EmpCode { get; set; } = "";
    public string EmpName { get; set; } = "";
    public string? PfNumber { get; set; }
    public string? Zone { get; set; }
    public string? Region { get; set; }
    public string? Branch { get; set; }
    public string? MobileNumber { get; set; }
    public string? IpPhoneNumber { get; set; }
    public FeedbackCategory Category { get; set; }
    public string Description { get; set; } = "";
    public FeedbackSeverity? Severity { get; set; }   // set by admin, null until triaged
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;
    /// <summary>1-5, mirrors the Escalation Matrix's L1-L5 — who currently owns
    /// this ticket. Starts at 1 (Admin triage); admin moves it to 2 to hand off
    /// to the Development Team (per RP_ESCALATION_MATRIX for the Feedback
    /// activity), and back to 1 when development hands it back for closure.
    /// Never changes automatically.</summary>
    public int CurrentLevel { get; set; } = 1;
    public int? Rating { get; set; }                  // 1-5 stars, submitter-only, set once Closed
    public string? RatingComments { get; set; }
    public DateTime? RatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<FeedbackUpdateEntry> Updates { get; set; } = new();
    public List<FeedbackAttachment> Attachments { get; set; } = new();
}

/// <summary>One admin/dev update on a ticket — status/severity/level change log.</summary>
public sealed class FeedbackUpdateEntry
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string UpdateText { get; set; } = "";
    public FeedbackStatus? NewStatus { get; set; }
    public FeedbackSeverity? NewSeverity { get; set; }
    public int? NewLevel { get; set; }
    public string UpdatedByEmp { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public sealed class FeedbackAttachment
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? Mime { get; set; }
    public string UploadedByEmp { get; set; } = "";
    public DateTime UploadedAt { get; set; }
}

/// <summary>Aggregate counts/averages for the Feedback Dashboard.</summary>
public sealed class FeedbackDashboardStats
{
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Closed { get; set; }
    public int Total { get; set; }
    public double? AverageRating { get; set; }
    public int RatedCount { get; set; }
    public List<(string Category, int Count)> ByCategory { get; set; } = new();
    public List<(string Severity, int Count)> BySeverity { get; set; } = new();
    public List<(int Level, int Count)> ByLevel { get; set; } = new();
}
