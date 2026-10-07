using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Web.Auth;

/// <summary>
/// Resolves the AD-authenticated principal into our domain user, ensuring a
/// row exists in RP_USER and caching effective roles per Blazor circuit.
/// </summary>
public sealed class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    private readonly AuthenticationStateProvider? _authState;
    private readonly IUserDirectory _directory;
    private readonly ILogger<CurrentUserService> _logger;

    private long? _userId;
    private string? _empCode;
    private string? _name;
    private string? _email;
    private HashSet<RoleCode> _roles = new();
    private bool _resolved;

    /// <summary>Timeout for the synchronous property-getter fallback path.
    /// Prevents indefinite circuit-thread blocking when the DB or auth provider
    /// is slow (the root cause of the Production hang).</summary>
    private static readonly TimeSpan SyncResolveTimeout = TimeSpan.FromSeconds(10);

    public CurrentUserService(IHttpContextAccessor http, IUserDirectory directory, ILogger<CurrentUserService> logger, AuthenticationStateProvider? authState = null)
    {
        _http = http; _directory = directory; _logger = logger; _authState = authState;
    }

    public long? UserId { get { ResolveSafe(); return _userId; } }
    public string? EmpCode { get { ResolveSafe(); return _empCode; } }
    public string? Name { get { ResolveSafe(); return _name; } }
    public string? Email { get { ResolveSafe(); return _email; } }
    public IReadOnlySet<RoleCode> Roles { get { ResolveSafe(); return _roles; } }
    public bool IsInRole(RoleCode role) => Roles.Contains(role);

    public async Task RefreshAsync()
    {
        _resolved = false;
        await Resolve();
    }

    /// <summary>
    /// Synchronous resolve with a timeout. Used by property getters that cannot be async.
    /// If resolution takes longer than <see cref="SyncResolveTimeout"/>, returns cached
    /// (possibly empty) state rather than deadlocking the circuit thread.
    /// </summary>
    private void ResolveSafe()
    {
        if (_resolved) return;

        try
        {
            // Use Task.Run to avoid capturing the Blazor synchronization context,
            // which is the root cause of the deadlock. The inner async work runs on
            // a thread-pool thread and we wait with a timeout.
            var task = Task.Run(async () => await Resolve());
            if (!task.Wait(SyncResolveTimeout))
            {
                _logger.LogWarning("CurrentUserService: synchronous Resolve() timed out after {Timeout}s — returning cached state. Call RefreshAsync() in an async context instead.", SyncResolveTimeout.TotalSeconds);
                _resolved = true; // Mark resolved to prevent retrying on every property access
            }
        }
        catch (AggregateException ae)
        {
            _logger.LogError(ae.InnerException ?? ae, "CurrentUserService: Resolve() failed in sync path");
            _resolved = true; // Prevent infinite retry loops
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CurrentUserService: Resolve() failed in sync path");
            _resolved = true;
        }
    }

    private async Task Resolve()
    {
        if (_resolved) return;

        // In an interactive Blazor circuit the HttpContext (from the SignalR
        // request) carries an *anonymous* principal, because authentication lives
        // in encrypted session storage, not a cookie. So fall back to the
        // AuthenticationStateProvider whenever the HttpContext user isn't
        // authenticated — that provider holds the real, claim-rich principal.
        ClaimsPrincipal? principal = _http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true && _authState is not null)
        {
            var state = await _authState.GetAuthenticationStateAsync();
            principal = state.User;
        }
        if (principal?.Identity?.IsAuthenticated != true)
        {
            _resolved = true;
            return;
        }

        var sam = ExtractSam(principal);
        var displayName = principal.FindFirst(ClaimTypes.GivenName)?.Value
                         ?? principal.Identity?.Name
                         ?? sam;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;

        var u = await _directory.EnsureUserAsync(sam, displayName, email);
        if (u is null) { _resolved = true; return; }

        _userId = u.Id;
        _empCode = u.EmpCode;
        _name = u.Name;
        _email = u.Email;
        _roles = (await _directory.GetEffectiveRolesAsync(u.EmpCode ?? "")).ToHashSet();
        _resolved = true;
    }

    private static string ExtractSam(ClaimsPrincipal p)
    {
        // The session login (Project B) carries the employee id in the "emplid"
        // / NameIdentifier claims; Windows auth would instead give DOMAIN\user via
        // Identity.Name. Prefer the explicit employee id so RP_USER.EMP_CODE (and
        // therefore RP_REQUEST.RAISED_BY_EMP) is always populated.
        var emplid = p.FindFirst("emplid")?.Value
                     ?? p.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(emplid)) return emplid.Trim();

        var name = p.Identity?.Name ?? "";
        var idx = name.IndexOf('\\');
        return idx >= 0 ? name[(idx + 1)..] : name;
    }
}
