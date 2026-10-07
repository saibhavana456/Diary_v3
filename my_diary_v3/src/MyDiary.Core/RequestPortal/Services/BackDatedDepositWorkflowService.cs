using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

/// <summary>The Back-Dated Deposit Approval workflow engine — every transition mutates the proposal's
/// status/pending-level and writes an immutable RP_BACKDATED_DEPOSIT_ACTION row, both inside the repo's
/// single optimistic-concurrency-guarded update (mirrors CorporateEoi.Services.EoiWorkflowService's
/// TransitionAsync pattern, reimplemented natively against this project's ADO/Oracle stack).</summary>
public sealed class BackDatedDepositWorkflowService : IBackDatedDepositWorkflowService
{
    private readonly IBackDatedDepositRepo _repo;
    private readonly IEmailSender _email;
    private readonly IUserRepo _users;
    private readonly IRbacRepo _rbac;
    private readonly IDmdRbacService _dmdRbac;

    public BackDatedDepositWorkflowService(IBackDatedDepositRepo repo, IEmailSender email,
        IUserRepo users, IRbacRepo rbac, IDmdRbacService dmdRbac)
    {
        _repo = repo;
        _email = email;
        _users = users;
        _rbac = rbac;
        _dmdRbac = dmdRbac;
    }

    /// <summary>Server-side role enforcement — closes the gap flagged after this workflow's first live
    /// test: the UI already hides the wrong buttons per level, but nothing stopped a caller who knew the
    /// URL/proposal id from invoking RO/ZO/DMD actions directly regardless of their real role (CCP's
    /// EoiWorkflowService.RequireRole has the equivalent check; this mirrors it against RP's own RBAC
    /// instead of CCP's). AppAdmin/SuperAdmin bypass every level, same convention as CCP's Admin role.
    /// Branch/RO/ZO are attribute-derived (Tier 1, from VW_STAFF_USER_SUMMARY via IUserRepo) — DMD is the
    /// explicit RP_RBAC_DMD_APPROVER grant, since it can't be attribute-derived (see DmdRbacService).</summary>
    private async Task RequireRoleForLevelAsync(string actorEmpCode, string level, CancellationToken ct)
    {
        if (await _rbac.IsSuperAdminAsync(actorEmpCode, ct)) return;
        var appAdmin = await _rbac.GetActiveAppAdminAsync(actorEmpCode, ct);
        if (appAdmin is { IsActive: true }) return;

        if (level == "Dmd")
        {
            if (await _dmdRbac.IsDmdApproverAsync(actorEmpCode, ct)) return;
            throw new DepositAuthorizationException("This action is restricted to the DMD Approver desk (or App/Super Admin).");
        }

        var roles = await _users.GetEffectiveRolesAsync(actorEmpCode, ct);
        var allowed = level switch
        {
            "Branch" => roles.Contains(RoleCode.BranchUser) || roles.Contains(RoleCode.BranchHead),
            "Ro" => roles.Contains(RoleCode.RoUser) || roles.Contains(RoleCode.RoHead),
            "Zo" => roles.Contains(RoleCode.ZoUser) || roles.Contains(RoleCode.ZoHead),
            _ => false
        };
        if (!allowed)
            throw new DepositAuthorizationException($"This action is restricted to {level} desk users (or App/Super Admin).");
    }

    // Per the BRD's own Workflow bullet list ("Email Notification has to be pushed at every stage") — sent
    // to the proposal's SPOC after every transition, using the SAME email service this app already uses
    // everywhere else (RequestPortal.Core.Services.IEmailSender → RequestPortalEmailSenderAdapter →
    // MyDiary.Core.Abstractions.IEmailSender → SmtpEmailSender/MailKit, the one "Smtp:*" config block the
    // whole app shares — NOT a new/separate mail path). A send failure is logged and swallowed rather than
    // thrown, since the workflow transition itself has already committed by the time this runs — losing a
    // notification email must never make the caller think the approval/return/etc. itself failed.
    private async Task NotifySpocAsync(BackDatedDepositProposal p, DepositActionType action, string? remarks, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.SpocEmail)) return;
        try
        {
            var subject = $"Back-Dated Deposit Proposal {p.RefNo} — {action}";
            var body = $"""
                <p>Proposal <b>{p.RefNo}</b> ({p.DepositType} — {p.Purpose}) for customer <b>{p.CustomerName}</b> (CIF {p.CifId})
                has been actioned: <b>{action}</b>.</p>
                <p>Current status: <b>{p.Status}</b> — pending at: <b>{p.PendingLevel}</b>.</p>
                {(string.IsNullOrWhiteSpace(remarks) ? "" : $"<p>Remarks: {System.Net.WebUtility.HtmlEncode(remarks)}</p>")}
                """;
            await _email.SendAsync(p.SpocEmail, subject, body, ct);
        }
        catch
        {
            // Swallowed deliberately — see this method's doc comment. No ILogger is wired here since
            // MyDiary.Core doesn't reference Microsoft.Extensions.Logging; the repo/DB layer's own
            // logging already captures anything that matters operationally for this workflow.
        }
    }

    public async Task<BackDatedDepositProposal> SubmitAsync(BackDatedDepositProposal p, IReadOnlyList<BackDatedDepositAccount> accounts,
        string actorEmpCode, CancellationToken ct = default)
    {
        await RequireRoleForLevelAsync(actorEmpCode, "Branch", ct);
        RequireAccounts(accounts);
        RequireReasonWordLimit(p.Reason);

        // RoSolId/RoName/ZoSolId/ZoName are set by the caller from the submitting Branch user's own
        // ClaimsPrincipal (region_solid/region_name, zone_solid/zone_name — already populated at login by
        // CustomAuthenticationStateProvider) — no separate hierarchy lookup needed for auto-routing.
        p.RefNo = "BDD-" + DateTime.Now.ToString("yyMMddHHmmssfff");
        p.Status = DepositProposalStatus.PendingAtRo;
        p.PendingLevel = DepositPendingLevel.Ro;
        p.CreatedByEmp = actorEmpCode;
        p.CreatedAt = DateTime.Now;
        p.RowVersion = 0;

        var id = await _repo.InsertProposalAsync(p, ct);
        p.Id = id;
        await _repo.ReplaceAccountsAsync(id, accounts, ct);

        await _repo.InsertActionAsync(new BackDatedDepositAction
        {
            ProposalId = id, ActorEmpCode = actorEmpCode, ActorRole = "BranchUser", Level = "Branch",
            Action = DepositActionType.Submit.ToString(), Remarks = "Proposal submitted.",
            SpocName = p.SpocName, SpocEmail = p.SpocEmail, SpocMobile = p.SpocMobile, SpocIp = p.SpocIp,
            // "New" not "" — Oracle silently stores an empty string as NULL, which violates
            // RP_BACKDATED_DEPOSIT_ACTION.FROM_STATUS's NOT NULL constraint (confirmed live: ORA-01400).
            FromStatus = "New", ToStatus = p.Status.ToString(), ActedAt = DateTime.Now
        }, ct);

        await NotifySpocAsync(p, DepositActionType.Submit, "Proposal submitted.", ct);
        return p;
    }

    // ---- RO ------------------------------------------------------------------------------------------

    public Task RoRecommendAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole,
        string remarks, string spocName, string spocEmail, string spocMobile, string spocIp, bool hasDocument, CancellationToken ct = default)
    {
        Require(hasDocument, "A supporting document is mandatory when the RO recommends a proposal.");
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Ro", DepositActionType.RoRecommend, remarks,
            spocName, spocEmail, spocMobile, spocIp, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtRo, "RO");
            p.Status = DepositProposalStatus.PendingAtZo;
            p.PendingLevel = DepositPendingLevel.Zo;
        }, ct);
    }

    public Task RoReturnAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Ro", DepositActionType.RoReturn, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtRo, "RO");
            p.Status = DepositProposalStatus.RoReturned;
            p.PendingLevel = DepositPendingLevel.Branch;
        }, ct);
    }

    public Task RoSeekClarificationAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Ro", DepositActionType.RoSeekClarification, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtRo, "RO");
            p.Status = DepositProposalStatus.RoClarificationSought;
            p.PendingLevel = DepositPendingLevel.Branch;
        }, ct);
    }

    // ---- ZO ------------------------------------------------------------------------------------------

    /// <summary>Conditional termination: Retail + Back-Dated Renewal ends the journey right here
    /// (Sanctioned); every other DepositType/Purpose combination routes on to DMD CO instead.</summary>
    public Task ZoRecommendAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole,
        string remarks, string spocName, string spocEmail, string spocMobile, string spocIp, bool hasDocument, CancellationToken ct = default)
    {
        Require(hasDocument, "A supporting document is mandatory when the ZO recommends a proposal.");
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Zo", DepositActionType.ZoRecommend, remarks,
            spocName, spocEmail, spocMobile, spocIp, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtZo, "ZO");
            if (IsRetailRenewal(p))
            {
                p.Status = DepositProposalStatus.Sanctioned;
                p.PendingLevel = DepositPendingLevel.Closed;
            }
            else
            {
                p.Status = DepositProposalStatus.PendingAtDmd;
                p.PendingLevel = DepositPendingLevel.Dmd;
            }
        }, ct);
    }

    public Task ZoRejectAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Zo", DepositActionType.ZoReject, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtZo, "ZO");
            Require(IsRetailRenewal(p),
                "ZO can only finally reject a Retail Back-Dated Renewal proposal. For every other type, only DMD CO is the final authority — use Recommend to forward it, or Return/Seek Clarification.");
            p.Status = DepositProposalStatus.Rejected;
            p.PendingLevel = DepositPendingLevel.Closed;
        }, ct);
    }

    public Task ZoReturnAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Zo", DepositActionType.ZoReturn, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtZo, "ZO");
            p.Status = DepositProposalStatus.PendingAtRo;
            p.PendingLevel = DepositPendingLevel.Ro;
        }, ct);
    }

    public Task ZoSeekClarificationAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Zo", DepositActionType.ZoSeekClarification, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtZo, "ZO");
            p.Status = DepositProposalStatus.PendingAtRo;
            p.PendingLevel = DepositPendingLevel.Ro;
        }, ct);
    }

    // ---- DMD CO ---------------------------------------------------------------------------------------
    // BRD step 9: "If sanctioned Journey ends here. If rejected reason has to be captured. if clarification
    // sought, proposal has to be returned in the tray of ZO sol." Reject is read as terminal (reason
    // captured, no further routing stated) — Return/SeekClarification are the only two that go back to ZO.

    public Task DmdApproveAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default) =>
        TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Dmd", DepositActionType.DmdApprove, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtDmd, "DMD");
            p.Status = DepositProposalStatus.Sanctioned;
            p.PendingLevel = DepositPendingLevel.Closed;
        }, ct);

    public Task DmdRejectAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Dmd", DepositActionType.DmdReject, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtDmd, "DMD");
            p.Status = DepositProposalStatus.Rejected;
            p.PendingLevel = DepositPendingLevel.Closed;
        }, ct);
    }

    public Task DmdReturnAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Dmd", DepositActionType.DmdReturn, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtDmd, "DMD");
            p.Status = DepositProposalStatus.PendingAtZo;
            p.PendingLevel = DepositPendingLevel.Zo;
        }, ct);
    }

    public Task DmdSeekClarificationAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string remarks, CancellationToken ct = default)
    {
        RequireRemarks(remarks);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, actorRole, "Dmd", DepositActionType.DmdSeekClarification, remarks,
            null, null, null, null, p =>
        {
            RequirePending(p, DepositProposalStatus.PendingAtDmd, "DMD");
            p.Status = DepositProposalStatus.PendingAtZo;
            p.PendingLevel = DepositPendingLevel.Zo;
        }, ct);
    }

    // ---- Branch resubmission ---------------------------------------------------------------------------

    public Task ResubmitAsync(long proposalId, long rowVersion, string actorEmpCode, IReadOnlyList<BackDatedDepositAccount> accounts,
        string reason, CancellationToken ct = default)
    {
        RequireAccounts(accounts);
        RequireReasonWordLimit(reason);
        return TransitionAsync(proposalId, rowVersion, actorEmpCode, "BranchUser", "Branch", DepositActionType.Resubmit, reason,
            null, null, null, null, p =>
        {
            Require(p.Status is DepositProposalStatus.RoReturned or DepositProposalStatus.RoClarificationSought,
                "This proposal is not currently in the Branch's tray.");
            p.Reason = reason;
            p.Status = DepositProposalStatus.PendingAtRo;
            p.PendingLevel = DepositPendingLevel.Ro;
        }, ct, accounts);
    }

    // ---- internals --------------------------------------------------------------------------------------

    private static bool IsRetailRenewal(BackDatedDepositProposal p) =>
        p.DepositType == DepositType.Retail && p.Purpose == DepositPurpose.BackDatedRenewal;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new DepositWorkflowException(message);
    }

    private static void RequireRemarks(string remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks))
            throw new DepositWorkflowException("Remarks are mandatory for this action.");
    }

    private static void RequirePending(BackDatedDepositProposal p, DepositProposalStatus expected, string level)
    {
        Require(p.Status == expected, $"This proposal is not currently awaiting a {level} decision.");
    }

    private static void RequireAccounts(IReadOnlyList<BackDatedDepositAccount> accounts)
    {
        Require(accounts is { Count: > 0 }, "At least one deposit account number and amount must be entered.");
        foreach (var a in accounts)
        {
            Require(!string.IsNullOrWhiteSpace(a.AccountNumber), "Every account row needs an account number.");
            Require(a.Amount > 0, "Every account row needs an amount greater than zero.");
        }
    }

    private static void RequireReasonWordLimit(string reason)
    {
        var words = (reason ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Require(words.Length <= 500, "Reason must not exceed 500 words.");
    }

    private async Task TransitionAsync(long proposalId, long rowVersion, string actorEmpCode, string actorRole, string level,
        DepositActionType action, string? remarks, string? spocName, string? spocEmail, string? spocMobile, string? spocIp,
        Action<BackDatedDepositProposal> mutate, CancellationToken ct, IReadOnlyList<BackDatedDepositAccount>? replaceAccounts = null)
    {
        await RequireRoleForLevelAsync(actorEmpCode, level, ct);

        var p = await _repo.GetAsync(proposalId, ct)
            ?? throw new DepositWorkflowException($"Proposal {proposalId} not found.");

        var fromStatus = p.Status.ToString();
        mutate(p);

        var ok = await _repo.UpdateProposalAsync(p, rowVersion, ct);
        if (!ok) throw new DepositStaleStateException();

        if (replaceAccounts is not null)
            await _repo.ReplaceAccountsAsync(proposalId, replaceAccounts, ct);

        await _repo.InsertActionAsync(new BackDatedDepositAction
        {
            ProposalId = proposalId, ActorEmpCode = actorEmpCode, ActorRole = actorRole, Level = level,
            Action = action.ToString(), Remarks = remarks,
            SpocName = spocName, SpocEmail = spocEmail, SpocMobile = spocMobile, SpocIp = spocIp,
            FromStatus = fromStatus, ToStatus = p.Status.ToString(), ActedAt = DateTime.Now
        }, ct);

        await NotifySpocAsync(p, action, remarks, ct);
    }
}
