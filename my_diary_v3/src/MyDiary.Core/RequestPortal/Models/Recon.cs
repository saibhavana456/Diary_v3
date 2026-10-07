namespace RequestPortal.Core.Models;

// ---------------------------------------------------------------------------
// Recon Certificates — reconciliation-certificate workflow (Annexure 3/4/4A/4B/4C/5/5A/5B/6),
// migrated from the standalone Recon_Certificates app into my_diary_v3_1, following the same
// native-feature pattern as Flat Management and the Back-Dated Deposit Approval workflow.
// Branch -> RO -> ZO -> ReconVerticalUser (CO/Vertical desk), with ReconAdmin managing access —
// see RoleCode.ReconVerticalUser / RoleCode.ReconAdmin in Enums.cs.
// ---------------------------------------------------------------------------

/// <summary>Explicitly-assigned Recon Admin — manages who holds RoleCode.ReconVerticalUser, mirroring
/// DmdVerticalAdmin's role relative to DmdApprover. Granted only by App/Super Admin.</summary>
public sealed class ReconAdmin
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.Now;
}

/// <summary>One employee's current Recon Vertical (CO desk) assignment (RoleCode.ReconVerticalUser). A new
/// assignment deactivates any prior active row for that employee — mirrors DmdApproverAssignment exactly.
/// Branch/RO/ZO roles are NOT assigned here — they reuse RoleCode.BranchUser/BranchHead/RoUser/RoHead/
/// ZoUser/ZoHead directly (attribute-derived from VW_STAFF_USER_SUMMARY, same as CCP/Deposits).</summary>
public sealed class ReconVerticalUserAssignment
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string AssignedByEmp { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.Now;
}
