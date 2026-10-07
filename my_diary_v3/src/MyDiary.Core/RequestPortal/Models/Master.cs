namespace RequestPortal.Core.Models;

// ── Shared audit stamp mixin ────────────────────────────────────────────────
// Every setup entity carries created/modified metadata for full traceability.
// These fields are populated by the application layer (not DB triggers) so that
// they travel cleanly when migrated to the Master Application.
// ────────────────────────────────────────────────────────────────────────────

public sealed class RequestType
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    /// <summary>How the type may be raised: MANUAL, API or BOTH.</summary>
    public string EntryMode { get; set; } = "BOTH";
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class UnitType
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class Unit
{
    public long Id { get; set; }
    public long UnitTypeId { get; set; }
    public long? ParentUnitId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? HolidayCalId { get; set; }
    public bool IsActive { get; set; } = true;
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class Vertical
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class Department
{
    public long Id { get; set; }
    public long VerticalId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class Activity
{
    public long Id { get; set; }
    public long DepartmentId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class HolidayCalendar
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class Holiday
{
    public long Id { get; set; }
    public long CalId { get; set; }
    public DateTime HolidayDate { get; set; }
    public string? Description { get; set; }
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class SlaConfig
{
    public long Id { get; set; }
    public long RequestTypeId { get; set; }
    public int LevelNo { get; set; }
    public int WorkingDays { get; set; }
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class NotifTemplate
{
    public long Id { get; set; }
    public string EventCode { get; set; } = "";
    public NotifChannel Channel { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsActive { get; set; } = true;
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class RoutingRule
{
    public long Id { get; set; }
    public long RequestTypeId { get; set; }
    public long? UnitTypeId { get; set; }
    public long? VerticalId { get; set; }
    public long? DepartmentId { get; set; }
    public long? ActivityId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    // Audit stamp
    public long? CreatedByUserId  { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public long? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt   { get; set; }
}

public sealed class RoutingAssignee
{
    public long Id { get; set; }
    public long RuleId { get; set; }
    public string EmpCode { get; set; } = "";
    public bool IsPrimary { get; set; }
}
