namespace RequestPortal.Core.Models;

// ---------------------------------------------------------------------------
// Recon Certificates — Annexure 4 (Branch-level Inter-Branch/Sundry/Suspense/POB reconciliation
// certificate), migrated field-for-field from the standalone Recon_Certificates app's ANNEXURE4 table +
// its 6 child breakup tables (see MyDiary.Web/Services/... code-behind logic in the source app's
// Annexure4.razor, ported unchanged into ReconAnnexure4Service.SaveOrSubmitAsync).
// Status literals are kept IDENTICAL to source: '98' Draft, '101' Submitted (locked), '105' Returned to
// Branch by RO Operations (unlocked for resubmission) — do not renumber these, other levels' queries
// (RO/ZO consolidation) depend on the exact same literals.
// ---------------------------------------------------------------------------

public sealed class ReconAnnexure4
{
    public long Id { get; set; }
    public string ReportId { get; set; } = "";
    public string? ReportMonth { get; set; }
    public string? ReportYear { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchHead { get; set; }
    public string? BranchName { get; set; }
    public string? MobileNo { get; set; }
    public string? IpNo { get; set; }

    public long? BrMaintainIbAcc { get; set; }
    public long? BrMaintainIbAccNotRecon { get; set; }

    // 1 Month
    public long? OurDebitEntries1M { get; set; }
    public decimal? OurDebitAmount1M { get; set; }
    public long? OurCreditEntries1M { get; set; }
    public decimal? OurCreditAmount1M { get; set; }
    public long? TheirDebitEntries1M { get; set; }
    public decimal? TheirDebitAmount1M { get; set; }
    public long? TheirCreditEntries1M { get; set; }
    public decimal? TheirCreditAmount1M { get; set; }

    // 3 Month
    public long? OurDebitEntries3M { get; set; }
    public decimal? OurDebitAmount3M { get; set; }
    public long? OurCreditEntries3M { get; set; }
    public decimal? OurCreditAmount3M { get; set; }
    public long? TheirDebitEntries3M { get; set; }
    public decimal? TheirDebitAmount3M { get; set; }
    public long? TheirCreditEntries3M { get; set; }
    public decimal? TheirCreditAmount3M { get; set; }

    // 6 Month
    public long? OurDebitEntries6M { get; set; }
    public decimal? OurDebitAmount6M { get; set; }
    public long? OurCreditEntries6M { get; set; }
    public decimal? OurCreditAmount6M { get; set; }
    public long? TheirDebitEntries6M { get; set; }
    public decimal? TheirDebitAmount6M { get; set; }
    public long? TheirCreditEntries6M { get; set; }
    public decimal? TheirCreditAmount6M { get; set; }

    // 1 Year
    public long? OurDebitEntries1Y { get; set; }
    public decimal? OurDebitAmount1Y { get; set; }
    public long? OurCreditEntries1Y { get; set; }
    public decimal? OurCreditAmount1Y { get; set; }
    public long? TheirDebitEntries1Y { get; set; }
    public decimal? TheirDebitAmount1Y { get; set; }
    public long? TheirCreditEntries1Y { get; set; }
    public decimal? TheirCreditAmount1Y { get; set; }

    // Above 1 Year
    public long? OurDebitEntriesAbove1Y { get; set; }
    public decimal? OurDebitAmountAbove1Y { get; set; }
    public long? OurCreditEntriesAbove1Y { get; set; }
    public decimal? OurCreditAmountAbove1Y { get; set; }
    public long? TheirDebitEntriesAbove1Y { get; set; }
    public decimal? TheirDebitAmountAbove1Y { get; set; }
    public long? TheirCreditEntriesAbove1Y { get; set; }
    public decimal? TheirCreditAmountAbove1Y { get; set; }

    // Sundry
    public long? SunUpto3MEntries { get; set; }
    public decimal? SunUpto3MAmount { get; set; }
    public long? SunThree6MEntries { get; set; }
    public decimal? SunThree6MAmount { get; set; }
    public long? SunOver6MEntries { get; set; }
    public decimal? SunOver6MAmount { get; set; }
    public long? SunNewOver6MEntries { get; set; }
    public decimal? SunNewOver6MAmount { get; set; }
    public string? SunReason { get; set; }
    public string? SunEfforts { get; set; }

    // Suspense
    public long? SusUpto3MEntries { get; set; }
    public decimal? SusUpto3MAmount { get; set; }
    public long? SusThree6MEntries { get; set; }
    public decimal? SusThree6MAmount { get; set; }
    public long? SusOver6MEntries { get; set; }
    public decimal? SusOver6MAmount { get; set; }
    public long? SusNewOver6MEntries { get; set; }
    public decimal? SusNewOver6MAmount { get; set; }
    public string? SusReason { get; set; }
    public string? SusEfforts { get; set; }

    // POB
    public long? PobUpto3MEntries { get; set; }
    public decimal? PobUpto3MAmount { get; set; }
    public long? PobThree6MEntries { get; set; }
    public decimal? PobThree6MAmount { get; set; }
    public long? PobOver6MEntries { get; set; }
    public decimal? PobOver6MAmount { get; set; }
    public long? PobNewOver6MEntries { get; set; }
    public decimal? PobNewOver6MAmount { get; set; }
    public string? PobReason { get; set; }
    public string? PobEfforts { get; set; }

    // Inter-bank (IBK)
    public long? IbkUpto3MEntries { get; set; }
    public decimal? IbkUpto3MAmount { get; set; }
    public long? IbkThree6MEntries { get; set; }
    public decimal? IbkThree6MAmount { get; set; }
    public long? IbkOver6MEntries { get; set; }
    public decimal? IbkOver6MAmount { get; set; }
    public long? IbkNewOver6MEntries { get; set; }
    public decimal? IbkNewOver6MAmount { get; set; }
    public string? IbkReason { get; set; }
    public string? IbkEfforts { get; set; }

    public string DelFlag { get; set; } = "N";
    public string? EntryBy { get; set; }
    public DateTime? EntryDate { get; set; }
    public string? ModifyBy { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string? DelBy { get; set; }
    public DateTime? DelDate { get; set; }
    /// <summary>'98' Draft, '101' Submitted (locked), '105' Returned to Branch by RO Operations.
    /// Exact same literals as the source app — do not renumber.</summary>
    public string? Status { get; set; }

    public string? RoUserId { get; set; }
    public DateTime? RoActionDate { get; set; }
    public string? RoOperationsUserId { get; set; }
    public DateTime? RoOperationsActionDate { get; set; }
    public string? RoCheckerUserId { get; set; }
    public DateTime? RoCheckerActionDate { get; set; }
    public string? ZoOperationsUserId { get; set; }
    public DateTime? ZoOperationsActionDate { get; set; }
    public string? ZoCheckerUserId { get; set; }
    public DateTime? ZoCheckerActionDate { get; set; }
    public string? ReconUserId { get; set; }
    public DateTime? ReconActionDate { get; set; }
    /// <summary>'BRANCH' | 'RO' | 'ZO' — same convention as source. This is what distinguishes a plain
    /// branch submission from the RO/ZO consolidated rollup rows.</summary>
    public string ReportType { get; set; } = "BRANCH";

    public List<ReconA4InterbankBreakup> BreakupEntries { get; set; } = new();
    public List<ReconA4OsGt3MonBreakup> SundryEntries { get; set; } = new();
    public List<ReconA4OsGt3MonBreakup> SuspenseEntries { get; set; } = new();
    public List<ReconA4OsGt3MonBreakup> PobEntries { get; set; } = new();
    public List<ReconA4BooksNotTallied> BooksNotTallied { get; set; } = new();
    public List<ReconA4IbReconBrPosition> BrPositionReconciled { get; set; } = new();
    public List<ReconA4IbReconBrPosition> BrPositionNotReconciled { get; set; } = new();
}

public sealed class ReconA4InterbankBreakup
{
    public long Id { get; set; }
    public string ReportId { get; set; } = "";
    public DateTime? DateOfEntry { get; set; }
    public string? BranchName { get; set; }
    public string? InterBankName { get; set; }
    public decimal? OurDebitAmount { get; set; }
    public decimal? OurCreditAmount { get; set; }
    public decimal? TheirDebitAmount { get; set; }
    public decimal? TheirCreditAmount { get; set; }
    public string? TransactionNatureDetails { get; set; }
}

/// <summary>Shared shape for Sundry/Suspense/POB "outstanding > 3 months" breakup rows — the three source
/// tables (ANNEXURE4_SUNDRY_OS_GT_3_MON_BREAKUP / _SUSPENSE_ / _POB_) are structurally identical, only the
/// owning table differs. Kept as one class + a Kind discriminator (not persisted) rather than three
/// near-duplicate classes.</summary>
public sealed class ReconA4OsGt3MonBreakup
{
    public long Id { get; set; }
    public string ReportId { get; set; } = "";
    public string? NatureOfEntry { get; set; }
    public DateTime? OriginatedDate { get; set; }
    public decimal? OutstandingAmountRupees { get; set; }
    public string? StatusReasonPendency { get; set; }
    public DateTime? ExpectedReversalDate { get; set; }
}

public sealed class ReconA4BooksNotTallied
{
    public long Id { get; set; }
    public string ReportId { get; set; } = "";
    public string? BranchName { get; set; }
    public string? HeadsNotTallied { get; set; }
    public DateTime? NotTalliedSinceDate { get; set; }
    public decimal? DifferenceAmountRupees { get; set; }
    public string? ActionInitiatedReconExpDate { get; set; }
    public string? Reason { get; set; }
}

public sealed class ReconA4IbReconBrPosition
{
    public long Id { get; set; }
    public string ReportId { get; set; } = "";
    public string? IbrAccountBank { get; set; }
    public string? IbrAccountBankBranch { get; set; }
    /// <summary>'Reconciled' | 'Not Reconciled' — exact same literal values as source.</summary>
    public string ReconciliationStatus { get; set; } = "";
}

/// <summary>Immutable action-history row — one per workflow action, exact port of
/// ANNEXURE4_ACTION_HISTORY. Mirrors BackDatedDepositAction's role in the Deposits workflow.</summary>
public sealed class ReconA4ActionHistory
{
    public long Id { get; set; }
    public string ReportId { get; set; } = "";
    public string ActionLevel { get; set; } = "";   // 'BRANCH' | 'RO' | 'ZO'
    public string ActionType { get; set; } = "";    // 'SAVE_DRAFT' | 'SUBMIT' | 'RESUBMIT' | ...
    public string UserId { get; set; } = "";
    public DateTime ActionDate { get; set; } = DateTime.Now;
    public string? StatusAfter { get; set; }
    public string? Remarks { get; set; }
    public string DelFlag { get; set; } = "N";
}
