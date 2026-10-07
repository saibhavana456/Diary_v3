namespace RequestPortal.Core.Models;

/// <summary>
/// "ZAH: RO Visit Report" — a Zonal Assurance Head's Regional Office visit
/// report (Assurance 360 module). Table 1 fields (Region/Zone/Regional Head/
/// Assurance Head) are derived once at save time from RP_CSBE_ORG_EMPLOYEE and
/// frozen here so the record doesn't silently change if the organogram changes
/// later. Tables 3-9 + 10 live in <see cref="Metrics"/> (see RoVisitPanelCatalog
/// in MyDiary.Web for the row/column shape and BUSINESS_360_DATA auto-fetch
/// mapping); the rest are plain child lists.
/// </summary>
public sealed class RoVisitReport
{
    public long Id { get; set; }

    /// <summary>Discriminator: 'RO_VISIT' (ZAH: RO Visit Report, default) or 'BRANCH_VISIT'
    /// (RAH: Branch Visit Report). Both report families share this same table/model —
    /// RegionSolId doubles as the Branch's own SOL ID for a BRANCH_VISIT row.</summary>
    public string ReportType { get; set; } = "RO_VISIT";

    public string RegionSolId { get; set; } = "";
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
    public string? RegionalHeadEmpCode { get; set; }
    public string? RegionalHeadName { get; set; }
    public DateTime? RhWorkingSince { get; set; }
    public string? AssuranceHeadEmpCode { get; set; }
    public string? AssuranceHeadName { get; set; }
    public DateTime? AhWorkingSince { get; set; }
    public string VisitingOfficialEmpCode { get; set; } = "";
    public string VisitingOfficialName { get; set; } = "";
    public string Designation { get; set; } = "Zonal Assurance Head";
    public DateTime? DateOfVisit { get; set; }
    public DateTime? ReportDate { get; set; }
    public string? SuspenseComments { get; set; }
    public string? OverallRemarks { get; set; }
    public string? RahName { get; set; }
    public string? RahEmpCode { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    // ── Branch Visit Report (RAH) specific nullable header fields ──────────
    public string? BranchName { get; set; }
    public string? BranchCode { get; set; }
    public DateTime? DateOfOpening { get; set; }
    public string? BranchCategory { get; set; }
    public string? AuditRating { get; set; }
    public string? QuarterYear { get; set; }
    public string? BranchManagerName { get; set; }
    public string? BranchManagerEmpCode { get; set; }
    public DateTime? BmWorkingSince { get; set; }
    /// <summary>Branch Manager's name as frozen for the signature block (may differ
    /// slightly in wording from BranchManagerName at print time; kept separate per spec).</summary>
    public string? BranchManagerSignoffName { get; set; }
    public string? RegistersComments { get; set; }

    public List<RoVisitExecSummaryRow> ExecSummary { get; set; } = new();
    public List<RoVisitMetricCell> Metrics { get; set; } = new();
    public List<RoVisitFindingRow> Findings { get; set; } = new();
    public List<RoVisitPendingIssueRow> PendingIssues { get; set; } = new();
    public List<RoVisitAssessmentRow> Assessment { get; set; } = new();
}

public sealed class RoVisitExecSummaryRow
{
    public long Id { get; set; }
    public int SrNo { get; set; }
    public string FocusArea { get; set; } = "";
    public string? Observation { get; set; }
    public string? ActionRequired { get; set; }
    public DateTime? TargetDate { get; set; }
}

/// <summary>One cell of a table-3-to-10 grid: (PanelCode, RowCode, ColCode) -> value.
/// IsAutoFilled=true means it was populated from BUSINESS_360_DATA at fetch time
/// (still editable afterward — the flag is just a UI hint, not a lock).</summary>
public sealed class RoVisitMetricCell
{
    public string PanelCode { get; set; } = "";
    public string RowCode { get; set; } = "";
    public string ColCode { get; set; } = "";
    public string? ValueText { get; set; }
    public bool IsAutoFilled { get; set; }
}

public sealed class RoVisitFindingRow
{
    public long Id { get; set; }
    public int SrNo { get; set; }
    public string? Finding { get; set; }
    public string? RiskSeverity { get; set; }
    public string? ActionRequired { get; set; }
    public DateTime? TargetDate { get; set; }
}

public sealed class RoVisitPendingIssueRow
{
    public long Id { get; set; }
    public int SrNo { get; set; }
    public string? Issue { get; set; }
    public string? SupportRequired { get; set; }
    public string? Priority { get; set; }
    public DateTime? ExpectedClosure { get; set; }
}

public sealed class RoVisitAssessmentRow
{
    public long Id { get; set; }
    public string AreaCode { get; set; } = "";
    public string AreaLabel { get; set; } = "";
    public string? Assessment { get; set; }
    public string? Comments { get; set; }
}

/// <summary>Table-1 lookup result, derived from RP_CSBE_ORG_EMPLOYEE by SOL_ID +
/// a ROLE_NAME convention ("...Regional Head..." / "...Assurance Head..."/"RAH").</summary>
public sealed class RoVisitRegionInfo
{
    public string? RegionName { get; set; }
    public string? RegionalHeadEmpCode { get; set; }
    public string? RegionalHeadName { get; set; }
    public DateTime? RhWorkingSince { get; set; }
    public string? AssuranceHeadEmpCode { get; set; }
    public string? AssuranceHeadName { get; set; }
    public DateTime? AhWorkingSince { get; set; }
}

/// <summary>Summary row for the zone/region visit-tracking dashboard list — covers
/// both draft (RP_RO_VISIT_REPORT) and submitted (RP_RO_VISIT_REPORT_SUBMITTED) rows,
/// discriminated by <see cref="IsSubmitted"/>.</summary>
public sealed class RoVisitSummary
{
    public long Id { get; set; }
    public long? SubmittedId { get; set; }
    public string RegionSolId { get; set; } = "";
    public string? RegionName { get; set; }
    public string? ZoneName { get; set; }
    public string VisitingOfficialName { get; set; } = "";
    public DateTime? DateOfVisit { get; set; }
    public DateTime? SavedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool IsSubmitted { get; set; }
}

public sealed class RoVisitDashboardCounts
{
    public int TotalDraft { get; set; }
    public int TotalSubmitted { get; set; }
}

/// <summary>Zone/Region/Branch/date-range filter for the dashboard and its drill-down list.</summary>
public sealed class RoVisitDashboardFilter
{
    public string? ZoneName { get; set; }
    public string? RegionSolId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>'RO_VISIT' or 'BRANCH_VISIT' — null means unfiltered (both). The RO and
    /// Branch dashboards each pass their own fixed value here.</summary>
    public string? ReportType { get; set; }
}

/// <summary>Table-1 lookup result for a Branch Visit Report, derived from
/// RP_CSBE_ORG_EMPLOYEE by the branch's own SOL_ID + ROLE_NAME convention
/// ("...Branch Manager..." / "...Assurance Head...").</summary>
public sealed class BranchVisitInfo
{
    public string? BranchName { get; set; }
    public string? BranchManagerEmpCode { get; set; }
    public string? BranchManagerName { get; set; }
    public DateTime? BmWorkingSince { get; set; }
    public string? AssuranceHeadEmpCode { get; set; }
    public string? AssuranceHeadName { get; set; }
    public DateTime? AhWorkingSince { get; set; }
}

/// <summary>One row returned by SQL Server's usp_ROVisit_GetMetrics — a flat
/// projection of BUSINESS_360_DATA/PARAMETER_MASTER for a given Region SOL ID.</summary>
public sealed class RoVisitSourceMetric
{
    public string ParameterName { get; set; } = "";
    public string Category { get; set; } = "";
    public string? SubCategory { get; set; }
    public decimal? ActualsAsOn { get; set; }
    public decimal? BaseCurrentFy { get; set; }
}
