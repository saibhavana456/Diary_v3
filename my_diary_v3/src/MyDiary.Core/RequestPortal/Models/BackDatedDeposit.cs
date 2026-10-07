namespace RequestPortal.Core.Models;

// ---------------------------------------------------------------------------
// Back-Dated Retail/Bulk Deposit Approval workflow (Deposit Mobilization
// Department BRD, submitted 01.08.2026) — Branch -> RO -> ZO -> DMD CO.
// Native My Diary feature; see MyDiary.Web/Features/BackDatedDeposits.
// ---------------------------------------------------------------------------

public enum DepositType { Retail = 1, Bulk = 2 }

public enum DepositPurpose { BackDatedOpening = 1, BackDatedRenewal = 2 }

/// <summary>Whose tray the proposal is currently sitting in. "Branch" also covers the initial
/// pre-submission state conceptually, but in practice a row only exists from PendingAtRo onward.</summary>
public enum DepositPendingLevel { Branch = 1, Ro = 2, Zo = 3, Dmd = 4, Closed = 5 }

public enum DepositProposalStatus
{
    PendingAtRo               = 1,
    RoReturned                = 2,   // -> Branch tray (RO "Return with reasons")
    RoClarificationSought     = 3,   // -> Branch tray (RO "Seek Clarification")
    PendingAtZo               = 4,   // after RO Recommend
    ZoClarificationSought     = 5,   // -> RO tray
    ZoReturned                = 6,   // -> RO tray
    PendingAtDmd              = 7,   // after ZO Recommend (non Retail+Renewal only)
    DmdClarificationSought    = 8,   // -> ZO tray
    DmdReturned               = 9,   // -> ZO tray
    Sanctioned                = 10,  // terminal — approved (at ZO for Retail+Renewal, else at DMD)
    Rejected                  = 11,  // terminal — rejected (at ZO for Retail+Renewal, else at DMD)
    New                       = 12,
}

public enum DepositActionType
{
    Submit, Resubmit,
    RoRecommend, RoReturn, RoSeekClarification,
    ZoRecommend, ZoReject, ZoReturn, ZoSeekClarification,
    DmdApprove, DmdReject, DmdReturn, DmdSeekClarification
}

public sealed class BackDatedDepositProposal
{
    public long Id { get; set; }
    public string RefNo { get; set; } = "";
    public DepositType DepositType { get; set; }
    public DepositPurpose Purpose { get; set; }

    public string BranchSolId { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? RoSolId { get; set; }
    public string? RoName { get; set; }
    public string? ZoSolId { get; set; }
    public string? ZoName { get; set; }

    public string CifId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Scheme { get; set; } = "";
    public DateTime OpeningOrLastRenewalDate { get; set; }
    public string Tenure { get; set; } = "";
    public DateTime MaturityDate { get; set; }
    public DateTime CustomerRequestDate { get; set; }
    public string Reason { get; set; } = "";   // brief description, max 500 words — enforced in the service

    public DepositProposalStatus Status { get; set; }
    public DepositPendingLevel PendingLevel { get; set; }

    // Branch SPOC, captured at submission (RO/ZO/DMD capture their own SPOC on the action row instead).
    public string SpocName { get; set; } = "";
    public string SpocEmail { get; set; } = "";
    public string SpocMobile { get; set; } = "";
    public string SpocIp { get; set; } = "";

    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;   // server local (IST) — not UtcNow

    /// <summary>Optimistic-concurrency token — incremented by one on every successful TransitionAsync.
    /// A caller acting on a stale value gets DepositStaleStateException rather than corrupting state.</summary>
    public long RowVersion { get; set; }
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    public List<BackDatedDepositAccount> Accounts { get; set; } = new();
}

public sealed class BackDatedDepositAccount
{
    public long Id { get; set; }
    public long ProposalId { get; set; }
    public string AccountNumber { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>Immutable audit-trail row — one per workflow action, mirroring EoiWorkflowAction's role in
/// CorporateEoi. Never updated or deleted once written.</summary>
public sealed class BackDatedDepositAction
{
    public long Id { get; set; }
    public long ProposalId { get; set; }
    public string ActorEmpCode { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public string Level { get; set; } = "";        // Branch / Ro / Zo / Dmd
    public string Action { get; set; } = "";       // DepositActionType enum name
    public string? Remarks { get; set; }
    public string? SpocName { get; set; }
    public string? SpocEmail { get; set; }
    public string? SpocMobile { get; set; }
    public string? SpocIp { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public DateTime ActedAt { get; set; } = DateTime.Now;
}

public sealed class BackDatedDepositDocument
{
    public long Id { get; set; }
    public long ProposalId { get; set; }
    public string Level { get; set; } = "";        // Branch / Ro / Zo / Dmd — the level it was uploaded at
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";   // key returned by IAttachmentStore.SaveAsync
    public string UploadedByEmp { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.Now;
}

/// <summary>DMD Vertical Admin — delegated admin who manages DMD Approver assignments, mirroring
/// CcpVerticalAdmin. Granted by AppAdmin/SuperAdmin only.</summary>
public sealed class DmdVerticalAdmin
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.Now;
}

/// <summary>One employee's current DMD Approver assignment (RoleCode.DmdApprover). A new assignment
/// deactivates any prior active row for that employee — mirrors CcpRoleAssignment exactly.</summary>
public sealed class DmdApproverAssignment
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.Now;
}

public sealed class DepositStaleStateException : Exception
{
    public DepositStaleStateException()
        : base("This proposal was changed by someone else while you were viewing it. Please refresh and try again.") { }
}

public sealed class DepositWorkflowException : Exception
{
    public DepositWorkflowException(string message) : base(message) { }
}

/// <summary>Thrown by BackDatedDepositWorkflowService.RequireRoleForLevelAsync — the actor isn't RO/ZO/DMD
/// (or App/Super Admin) for the level they're trying to act on.</summary>
public sealed class DepositAuthorizationException : Exception
{
    public DepositAuthorizationException(string message) : base(message) { }
}
