namespace RequestPortal.Core.Models;

// ── Sol ID (organisational unit — branch / RO / ZO / CO Vertical) ────────
public sealed class SolIdRecord
{
    public long          Id           { get; set; }
    public string        Code         { get; set; } = "";
    public string        Name         { get; set; } = "";
    public SolIdType     Type         { get; set; }
    public long?         ParentSolId  { get; set; }
    public bool          IsActive     { get; set; } = true;
}

// ── Employee master (extends AppUser with organisational context) ─────────
public sealed class EmployeeMaster
{
    public long             Id           { get; set; }
    public long             UserId       { get; set; }
    public string           EmpCode      { get; set; } = "";
    public string           Name         { get; set; } = "";
    public string?          Email        { get; set; }
    public string?          Mobile       { get; set; }
    public long             SolId        { get; set; }
    public SolIdType        SolIdType    { get; set; }
    public EmployeePosition Position     { get; set; }
    public string?          Designation  { get; set; }
    public bool             IsActive     { get; set; } = true;
}

// ── Role allocation master ────────────────────────────────────────────────
public sealed class RoleAllocationRecord
{
    public long       Id          { get; set; }
    public string     EmpCode     { get; set; } = "";
    public RoleCode   Role        { get; set; }
    public string?    ModuleScope { get; set; }
    public string?    Notes       { get; set; }
    public DateTime   ValidFrom   { get; set; }
    public DateTime?  ValidTo     { get; set; }
    public bool       IsActive    { get; set; } = true;
}

public sealed class AppUser
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public string? AdSamAccountName { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AppRole
{
    public long Id { get; set; }
    public RoleCode Code { get; set; }
    public string Name { get; set; } = "";
}

public sealed class UserRole
{
    public long Id { get; set; }
    public string EmpCode { get; set; } = "";
    public long RoleId { get; set; }
    public long? UnitId { get; set; }
    public long? VerticalId { get; set; }
    public long? DepartmentId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

public sealed class Delegation
{
    public long Id { get; set; }
    public string FromEmpCode { get; set; } = "";
    public string ToEmpCode { get; set; } = "";
    public string RoleCode { get; set; } = "";       // RoleCode enum name (e.g. "VertL2")
    public string? VerticalCode { get; set; }        // null = portal-wide delegation
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? Reason { get; set; }
    public string? ModifiedByEmp { get; set; }       // vertical admin or ticket assignee who last changed this
    public DateTime? ModifiedAt { get; set; }
}
