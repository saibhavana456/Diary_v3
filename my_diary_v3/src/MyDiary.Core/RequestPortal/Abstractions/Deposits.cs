using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

// ---------------------------------------------------------------------------
// Back-Dated Deposit Approval — repo + workflow-service + DMD RBAC contracts.
// ---------------------------------------------------------------------------

public interface IBackDatedDepositRepo
{
    Task<long> InsertProposalAsync(BackDatedDepositProposal p, CancellationToken ct = default);
    Task<BackDatedDepositProposal?> GetAsync(long id, CancellationToken ct = default);
    Task<BackDatedDepositProposal?> GetByRefNoAsync(string refNo, CancellationToken ct = default);

    /// <summary>Applies <paramref name="mutate"/> to the row's mutable columns and bumps ROW_VERSION,
    /// guarded by a WHERE ROW_VERSION=:old — returns false (no rows affected) on a stale token.</summary>
    Task<bool> UpdateProposalAsync(BackDatedDepositProposal p, long expectedRowVersion, CancellationToken ct = default);

    Task<IReadOnlyList<BackDatedDepositAccount>> ListAccountsAsync(long proposalId, CancellationToken ct = default);
    Task ReplaceAccountsAsync(long proposalId, IReadOnlyList<BackDatedDepositAccount> accounts, CancellationToken ct = default);

    Task<long> InsertActionAsync(BackDatedDepositAction a, CancellationToken ct = default);
    Task<IReadOnlyList<BackDatedDepositAction>> ListActionsAsync(long proposalId, CancellationToken ct = default);
    Task<IReadOnlyList<BackDatedDepositAction>> ListAllActionsAsync(CancellationToken ct = default);

    Task<long> InsertDocumentAsync(BackDatedDepositDocument d, CancellationToken ct = default);
    Task<IReadOnlyList<BackDatedDepositDocument>> ListDocumentsAsync(long proposalId, CancellationToken ct = default);

    Task<IReadOnlyList<BackDatedDepositProposal>> ListByBranchAsync(string branchSolId, CancellationToken ct = default);
    Task<IReadOnlyList<BackDatedDepositProposal>> ListPendingAtRoAsync(string roSolId, CancellationToken ct = default);
    Task<IReadOnlyList<BackDatedDepositProposal>> ListPendingAtZoAsync(string zoSolId, CancellationToken ct = default);
    Task<IReadOnlyList<BackDatedDepositProposal>> ListPendingAtDmdAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BackDatedDepositProposal>> ListAllAsync(CancellationToken ct = default);
}

/// <summary>The TransitionAsync-style workflow engine — every state mutation goes through here so status
/// change + immutable audit row are always written together in one transaction (mirrors
/// CorporateEoi.Services.EoiWorkflowService's TransitionAsync). SPOC capture and the mandatory-document-
/// on-recommend rule are enforced here, not just in the UI.</summary>
public interface IBackDatedDepositWorkflowService
{
    Task<BackDatedDepositProposal> SubmitAsync(BackDatedDepositProposal proposal, IReadOnlyList<BackDatedDepositAccount> accounts,
        string actorEmpCode, CancellationToken ct = default);

    Task RoRecommendAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole,
        string remarks, string spocName, string spocEmail, string spocMobile, string spocIp, bool hasDocument, CancellationToken ct = default);
    Task RoReturnAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);
    Task RoSeekClarificationAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);

    /// <summary>ZO's single positive action. If the proposal is Retail + Back-Dated Renewal this is
    /// terminal (Sanctioned) right here at ZO — no DMD escalation. Every other combination (Bulk of either
    /// purpose, or Retail Back-Dated Opening) instead routes on to DMD CO for the final decision.</summary>
    Task ZoRecommendAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole,
        string remarks, string spocName, string spocEmail, string spocMobile, string spocIp, bool hasDocument, CancellationToken ct = default);

    /// <summary>Only valid when the proposal is Retail + Back-Dated Renewal — ZO is the final authority
    /// for that one combination and can terminally reject it. Every other combination throws
    /// DepositWorkflowException; only DMD CO can finally reject those (ZO can Recommend it onward instead).</summary>
    Task ZoRejectAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);
    Task ZoReturnAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);
    Task ZoSeekClarificationAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);

    Task DmdApproveAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);
    Task DmdRejectAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);
    Task DmdReturnAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);
    Task DmdSeekClarificationAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default);

    /// <summary>Branch resubmission after an RO/ZO/DMD return or clarification-seek — re-enters at the
    /// level that most recently sent it back (Ro after an RO or ZO round-trip lands back at Branch/RO;
    /// see the service's own doc comment on exactly which level is re-entered per source state).</summary>
    Task ResubmitAsync(long proposalId, long rowVersion, string actorEmpCode, IReadOnlyList<BackDatedDepositAccount> accounts,
        string reason, CancellationToken ct = default);
}

/// <summary>DMD RBAC — DmdApprover role assignment + the DMD Vertical Admin who manages them. Two
/// dedicated tables (RP_RBAC_DMD_VERT_ADMIN, RP_RBAC_DMD_APPROVER), fully separate from CCP's
/// RP_RBAC_CCP_* tables — mirrors CcpRbacRepo/CcpRbacService exactly, see DmdRbacRepo's doc comment.</summary>
public interface IDmdRbacRepo
{
    Task<IReadOnlyList<DmdVerticalAdmin>> ListVerticalAdminsAsync(CancellationToken ct = default);
    Task<bool> IsVerticalAdminAsync(string empCode, CancellationToken ct = default);
    Task UpsertVerticalAdminAsync(DmdVerticalAdmin a, CancellationToken ct = default);

    Task<IReadOnlyList<DmdApproverAssignment>> ListApproverAssignmentsAsync(CancellationToken ct = default);
    Task<DmdApproverAssignment?> GetActiveApproverAsync(string empCode, CancellationToken ct = default);
    Task<long> UpsertApproverAssignmentAsync(DmdApproverAssignment a, CancellationToken ct = default);
    Task DeactivateApproverAssignmentAsync(long id, CancellationToken ct = default);
}

public interface IDmdRbacService
{
    Task<bool> IsDmdApproverAsync(string empCode, CancellationToken ct = default);

    Task<IReadOnlyList<DmdVerticalAdmin>> ListVerticalAdminsAsync(CancellationToken ct = default);
    Task GrantVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);
    Task RevokeVerticalAdminAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);

    Task<IReadOnlyList<DmdApproverAssignment>> ListApproverAssignmentsAsync(CancellationToken ct = default);
    Task AssignApproverAsync(string actorEmpCode, string targetEmpCode, CancellationToken ct = default);
    Task RevokeApproverAsync(string actorEmpCode, long assignmentId, CancellationToken ct = default);

    Task<bool> CanManageApproversAsync(string empCode, CancellationToken ct = default);
    Task<bool> CanManageVerticalAdminsAsync(string empCode, CancellationToken ct = default);
}
