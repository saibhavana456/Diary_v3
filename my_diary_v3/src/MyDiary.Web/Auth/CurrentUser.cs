using Microsoft.AspNetCore.Components.Authorization;
using MyDiary.Core;
using MyDiary.Core.Abstractions;
using System.Security.Claims;

namespace MyDiary.Web.Auth;

/// <summary>
/// Reads claims from the authenticated ClaimsPrincipal and exposes them
/// via ICurrentUser. Registered as Scoped — one instance per Blazor circuit.
/// Auto-resolves the principal from AuthenticationStateProvider on first access.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private ClaimsPrincipal? _principal;
    private bool _resolved;

    public CurrentUser(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    public void SetPrincipal(ClaimsPrincipal principal)
    {
        _principal = principal;
        _resolved = true;
    }

    private void EnsureResolved()
    {
        if (_resolved) return;
        _resolved = true;
        // CustomAuthenticationStateProvider.GetAuthenticationStateAsync() is only cached
        // for 30s and otherwise awaits ProtectedSessionStorage (a real JS interop round
        // trip) plus an RBAC DB lookup. Blocking on that task directly with
        // .GetAwaiter().GetResult() from a component's synchronous render path would run
        // on the circuit's single-threaded RendererSynchronizationContext — the same
        // context the JS interop response needs to be dispatched back onto to complete
        // the awaited task — which deadlocks the circuit (the exact "deadlocked the
        // Blazor Server circuit" failure mode documented elsewhere in this codebase, e.g.
        // OracleConnectionFactory.cs / EoiSsoService.cs). Task.Run hands the call to a
        // thread-pool thread with no captured SynchronizationContext, so the inner
        // JS-interop continuation is free to complete independently of this thread.
        var state = Task.Run(() => _authStateProvider.GetAuthenticationStateAsync()).GetAwaiter().GetResult();
        _principal = state.User;
    }

    public bool   IsAuthenticated { get { EnsureResolved(); return _principal?.Identity?.IsAuthenticated == true; } }
    public string? EmplId         { get { EnsureResolved(); return Claim("emplid") ?? Claim(ClaimTypes.NameIdentifier); } }
    public string? Name           { get { EnsureResolved(); return Claim("employee_name") ?? Claim(ClaimTypes.Name); } }
    public string? Email          { get { EnsureResolved(); return Claim("email"); } }
    public string? Phone          { get { EnsureResolved(); return Claim("phone"); } }
    public string? Privilege      { get { EnsureResolved(); return Claim("privilege"); } }
    public string? BranchCode     { get { EnsureResolved(); return Claim("branch_code"); } }
    public string? BranchSolid    { get { EnsureResolved(); return Claim("branch_solid"); } }
    public string? BranchName     { get { EnsureResolved(); return Claim("branch_name"); } }
    public string? RegionCode     { get { EnsureResolved(); return Claim("region_code"); } }
    public string? RegionSolid    { get { EnsureResolved(); return Claim("region_solid"); } }
    public string? ZoneCode       { get { EnsureResolved(); return Claim("zone_code"); } }
    public string? ZoneSolid      { get { EnsureResolved(); return Claim("zone_solid"); } }

    public IReadOnlySet<RoleCode> Roles { get { EnsureResolved(); return _roleSet ??= BuildRoles(); } }
    private IReadOnlySet<RoleCode>? _roleSet;

    public bool IsSuperAdmin => Roles.Contains(RoleCode.SuperAdmin);
    public bool IsAppAdmin   => Roles.Contains(RoleCode.AppAdmin) || IsSuperAdmin;

    public bool IsInRole(RoleCode role) => Roles.Contains(role);

    private string? Claim(string type) =>
        _principal?.FindFirst(type)?.Value;

    private HashSet<RoleCode> BuildRoles()
    {
        var roles = new HashSet<RoleCode>();
        if (_principal is null) return roles;

        foreach (var c in _principal.FindAll("md_role"))
        {
            if (Enum.TryParse<RoleCode>(c.Value, out var r))
                roles.Add(r);
        }

        // Also derive a base role from the My Diary privilege claim
        var privilege = Claim("privilege")?.ToUpperInvariant() ?? "";
        var derived = privilege switch
        {
            "BRANCH" => RoleCode.BranchUser,
            "REGION" => RoleCode.RoUser,
            "ZONE"   => RoleCode.ZoUser,
            "CO"     => RoleCode.CoUser,
            _        => (RoleCode?)null
        };
        if (derived.HasValue) roles.Add(derived.Value);

        return roles;
    }
}

/// <summary>
/// Initialises CurrentUser from the active AuthenticationState
/// so it's available in Blazor components via DI.
/// </summary>
public sealed class CurrentUserInitializer : IHostedService
{
    // Intentionally empty — wiring is done in App.razor's CascadingAuthenticationState
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct)  => Task.CompletedTask;
}
