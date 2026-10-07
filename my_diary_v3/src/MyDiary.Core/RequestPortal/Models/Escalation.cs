namespace RequestPortal.Core.Models;

/// <summary>
/// A record from the (sample) employee view — the authoritative source of an
/// employee's Scale, Role, Email and Mobile, keyed by PF number.
/// In production this maps to the bank's HRMS employee view.
/// </summary>
public sealed class EmployeeViewRecord
{
    public string  PfNumber { get; set; } = "";
    public string  Name     { get; set; } = "";
    public string? Scale    { get; set; }
    public string? Role     { get; set; }
    public string? Email    { get; set; }
    public string? Mobile   { get; set; }
}

/// <summary>
/// One row of the escalation matrix: a level (L1–L5) mapped to an employee (by PF)
/// for a given activity (optionally scoped to a unit). IP number and generic mail
/// are entered/stored per mapping; Scale/Role/Email/Mobile are joined live from the
/// employee view.
/// </summary>
public sealed class EscalationMatrixEntry
{
    public long    Id             { get; set; }
    public long?   RequestTypeId  { get; set; }
    public long    ActivityId     { get; set; }
    public long?   UnitId         { get; set; }
    public int     LevelNo        { get; set; }          // 1..5
    public string  PfNumber       { get; set; } = "";

    // Entered + stored per mapping
    public string? IpNumber     { get; set; }
    public string? GenericMail  { get; set; }

    // Joined from the employee view (read-only)
    public string? EmpName      { get; set; }
    public string? Scale        { get; set; }
    public string? EmpRole      { get; set; }
    public string? Email        { get; set; }
    public string? Mobile       { get; set; }

    // Joined for display — full request-portal classification
    public string? RequestTypeName { get; set; }
    public string? UnitTypeName    { get; set; }
    public string? UnitName        { get; set; }
    public string? VerticalName    { get; set; }
    public string? DepartmentName  { get; set; }
    public string? ActivityName    { get; set; }

    public bool    IsActive     { get; set; } = true;

    public string  LevelLabel => $"L{LevelNo}";
}
