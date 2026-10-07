namespace MyDiary.Core.Abstractions;

/// <summary>
/// Provides the identity of the currently authenticated user,
/// resolved from claims set by CustomAuthenticationStateProvider (Project B pattern)
/// and enriched with RBAC roles from the database (Project A pattern).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Employee ID / EMPLID from HR system.</summary>
    string? EmplId { get; }

    string? Name      { get; }
    string? Email     { get; }
    string? Phone     { get; }
    string? Privilege { get; }

    // Organisational hierarchy
    string? BranchCode  { get; }
    string? BranchSolid { get; }
    string? BranchName  { get; }
    string? RegionCode  { get; }
    string? RegionSolid { get; }
    string? ZoneCode    { get; }
    string? ZoneSolid   { get; }

    // RBAC (Project A)
    IReadOnlySet<RoleCode> Roles        { get; }
    bool                   IsSuperAdmin { get; }
    bool                   IsAppAdmin   { get; }

    bool IsInRole(RoleCode role);
    bool IsAuthenticated { get; }
}
