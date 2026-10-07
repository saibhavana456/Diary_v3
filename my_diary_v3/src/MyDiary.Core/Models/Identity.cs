namespace MyDiary.Core.Models;

// ── Core application user (from Project A's RP_USER table) ───────────────
public sealed class AppUser
{
    public long    Id                { get; set; }
    public string  EmpCode           { get; set; } = "";
    public string  Name              { get; set; } = "";
    public string? Email             { get; set; }
    public string? Mobile            { get; set; }
    public string? AdSamAccountName  { get; set; }
    public bool    IsActive          { get; set; } = true;
}

public sealed class AppRole
{
    public long     Id   { get; set; }
    public RoleCode Code { get; set; }
    public string   Name { get; set; } = "";
}

public sealed class UserRole
{
    public long      Id           { get; set; }
    public string    EmpCode      { get; set; } = "";
    public long      RoleId       { get; set; }
    public long?     UnitId       { get; set; }
    public long?     VerticalId   { get; set; }
    public long?     DepartmentId { get; set; }
    public DateTime  ValidFrom    { get; set; }
    public DateTime? ValidTo      { get; set; }
}

public sealed class Delegation
{
    public long      Id             { get; set; }
    public string    FromEmpCode    { get; set; } = "";
    public string    ToEmpCode      { get; set; } = "";
    public string    RoleCode       { get; set; } = "";
    public string?   VerticalCode   { get; set; }
    public DateTime  ValidFrom      { get; set; }
    public DateTime  ValidTo        { get; set; }
    public string?   Reason         { get; set; }
    public string?   ModifiedByEmp  { get; set; }
    public DateTime? ModifiedAt     { get; set; }
}

// ── Extended staff details from My Diary (Project B) AD authentication ───
public sealed class StaffDetails
{
    public string  EmplId                    { get; set; } = "";
    public string  Name                      { get; set; } = "";
    public string  UserName                  { get; set; } = "";
    public string? Privilege                 { get; set; }
    public string? Email                     { get; set; }
    public string? Phone                     { get; set; }
    public string? Sex                       { get; set; }
    public string? Location                  { get; set; }
    public string? DepartmentDescr           { get; set; }
    public string? DepartmentDescrExtra      { get; set; }
    public string? StaffRegionCode           { get; set; }
    public string? StaffRegionName           { get; set; }
    public string? StaffDivisionCode         { get; set; }
    public string? StaffDivisionName         { get; set; }
    public string? EmpDesignation            { get; set; }
    public string? EmpDesignationDesc        { get; set; }
    public string? EmpScaleCode              { get; set; }
    public string? EmpScaleDescr             { get; set; }
    public DateTime? DateOfBirth             { get; set; }
    public DateTime? JoiningDate             { get; set; }
    public DateTime? ExpectedEndDate         { get; set; }
    public DateTime? PostingDate             { get; set; }
    public DateTime? DataAsOn               { get; set; }
    public DateTime? LastUpdateDate          { get; set; }
    public string? AccNum                    { get; set; }
    public string? BranchEcCode             { get; set; }
    public string? BrSolid                  { get; set; }
    public string? Access                    { get; set; }
    public string? Status                    { get; set; }
    public string? RegionSolid              { get; set; }
    public string? RegionCode               { get; set; }
    public string? RegionName               { get; set; }
    public string? ZoneSolid                { get; set; }
    public string? ZoneCode                 { get; set; }
    public string? ZoneName                 { get; set; }
    public string? BranchSolid             { get; set; }
    public string? BranchCode              { get; set; }
    public string? BranchName              { get; set; }
    public string? Mobile                   { get; set; }
    public string? LastEntryDate            { get; set; }
    public DateTime? LastWeekStart          { get; set; }
    public DateTime? LastWeekEnd            { get; set; }
    public string? UserType                 { get; set; }
}

// ── Session record stored in SQL Server ──────────────────────────────────
public sealed class UserSession
{
    public string    Username   { get; set; } = "";
    public string    IpAddress  { get; set; } = "";
    public DateTime  LoginTime  { get; set; }
}

// ── RBAC models ───────────────────────────────────────────────────────────
public sealed class AppModule
{
    public long       Id          { get; set; }
    public string     Code        { get; set; } = "";
    public string     DisplayName { get; set; } = "";
    public long?      ParentId    { get; set; }
    public ModuleKind Kind        { get; set; }
    public int        SortOrder   { get; set; }
    public bool       IsActive    { get; set; } = true;
}

public sealed class AppMenu
{
    public long    Id          { get; set; }
    public long    ModuleId    { get; set; }
    public string  Label       { get; set; } = "";
    public string? Href        { get; set; }
    public string? Icon        { get; set; }
    public int     SortOrder   { get; set; }
    public bool    IsActive    { get; set; } = true;
}

public sealed class RoleModulePerm
{
    public long      Id           { get; set; }
    public long      RoleId       { get; set; }
    public long      ModuleId     { get; set; }
    public bool      CanView      { get; set; }
    public bool      CanAdd       { get; set; }
    public bool      CanModify    { get; set; }
    public bool      CanDelete    { get; set; }
    public bool      CanAuthorize { get; set; }
}

public sealed class UserModulePerm
{
    public long    Id           { get; set; }
    public string  EmpCode      { get; set; } = "";
    public long    ModuleId     { get; set; }
    public bool    CanView      { get; set; }
    public bool    CanAdd       { get; set; }
    public bool    CanModify    { get; set; }
    public bool    CanDelete    { get; set; }
    public bool    CanAuthorize { get; set; }
    // When true this row DENIES rather than grants
    public bool    IsDeny       { get; set; }
}
