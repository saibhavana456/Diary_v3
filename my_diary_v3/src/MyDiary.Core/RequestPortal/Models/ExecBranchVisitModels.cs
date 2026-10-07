namespace RequestPortal.Core.Models;

/// <summary>
/// "Executive Branch Visit" (Assurance 360) — header + flat field dictionary +
/// open-row lists, matching RP_EXEC_BRANCH_VISIT / _FIELD / _OPENROW (RP_39).
/// Unlike RoVisitReport's decomposed panel/row/col children, this form's own
/// page code already keys every editable cell with one flat string (see
/// ExecutiveBranchVisit.razor's _manual dictionary), so Fields mirrors that
/// directly instead of re-encoding it.
/// </summary>
public sealed class ExecBranchVisit
{
    public long Id { get; set; }
    public string BranchCode { get; set; } = "";
    public string? BranchName { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
    public string? BranchCategory { get; set; }
    public string? AuditRating { get; set; }
    public DateTime? DateOfOpening { get; set; }
    public string? BranchManagerName { get; set; }
    public DateTime? BmWorkingSince { get; set; }
    public string VisitingOfficialEmpCode { get; set; } = "";
    public string VisitingOfficialName { get; set; } = "";
    public string? Designation { get; set; }
    public DateTime? DateOfVisit { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string CreatedByEmp { get; set; } = "";
    public DateTime? SavedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    /// <summary>FIELD_KEY -> VALUE_TEXT, e.g. "A1_Total Deposits_Actual2526" -> "715.10".
    /// Auto-filled cells are saved here too (IsAutoFilled tracked separately per key
    /// in AutoFilledKeys) so a submitted snapshot is self-contained even if the
    /// underlying BUSINESS_360_DATA figures change later.</summary>
    public Dictionary<string, string> Fields { get; set; } = new();

    /// <summary>Subset of Fields' keys that were auto-populated rather than typed —
    /// mirrors RP_RO_VISIT_METRIC.IS_AUTO_FILLED, tracked per-key here since Fields
    /// itself is a flat dictionary rather than a row-typed table.</summary>
    public HashSet<string> AutoFilledKeys { get; set; } = new();

    /// <summary>TABLE_KEY -> ordered row labels, for the Add-Row tables (Pending
    /// Sanctions, Pending Disbursement, Negativity, Nature of Irregularities).</summary>
    public Dictionary<string, List<string>> OpenRows { get; set; } = new();
}

/// <summary>Lightweight row for the dashboard's visit list.</summary>
public sealed class ExecBranchVisitSummary
{
    public long Id { get; set; }
    public string BranchCode { get; set; } = "";
    public string? BranchName { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
    public string VisitingOfficialName { get; set; } = "";
    public DateTime? DateOfVisit { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTime? SavedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

/// <summary>Dashboard filter — every field optional/blank means "no filter".</summary>
public sealed class ExecBranchVisitFilter
{
    public string? BranchCode { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class ExecBranchVisitCounts
{
    public int TotalDraft { get; set; }
    public int TotalSubmitted { get; set; }

    /// <summary>Submitted-visit count grouped by REGION_NAME (as typed on Tab 1 — free
    /// text, not a master lookup, so this is "whatever the visiting officials entered",
    /// including a "(Not specified)" bucket for blanks) — feeds the dashboard's bar chart.</summary>
    public Dictionary<string, int> ByRegion { get; set; } = new();
}
