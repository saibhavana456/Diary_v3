using Microsoft.AspNetCore.Authorization;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;

namespace RequestPortal.Web.Auth;

/// <summary>
/// Policy-based permission requirement. Pair a module code with an action:
///     [Authorize(Policy = PermissionPolicy.For("MASTERS.USERS", PermAction.Modify))]
/// or
///     <AuthorizeView Policy="@PermissionPolicy.For(\"MASTERS.USERS\", PermAction.View)">…</AuthorizeView>
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string ModuleCode { get; }
    public PermAction Action { get; }

    public PermissionRequirement(string moduleCode, PermAction action)
    {
        ModuleCode = moduleCode;
        Action = action;
    }
}

public static class PermissionPolicy
{
    public const string Prefix = "rp.perm:";
    public static string For(string moduleCode, PermAction action) =>
        $"{Prefix}{moduleCode}:{action}";

    public static bool TryParse(string policyName, out string moduleCode, out PermAction action)
    {
        moduleCode = "";
        action = PermAction.View;
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var rest = policyName[Prefix.Length..];
        var split = rest.LastIndexOf(':');
        if (split <= 0) return false;
        moduleCode = rest[..split];
        return Enum.TryParse(rest[(split + 1)..], ignoreCase: true, out action);
    }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRbacService _rbac;
    private readonly ILogger<PermissionAuthorizationHandler> _log;

    public PermissionAuthorizationHandler(IRbacService rbac, ILogger<PermissionAuthorizationHandler> log)
    {
        _rbac = rbac;
        _log = log;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var empCode = context.User.FindFirst("rp_emp_code")?.Value;
        if (string.IsNullOrEmpty(empCode)) return;

        try
        {
            if (await _rbac.CheckAsync(empCode, requirement.ModuleCode, requirement.Action))
            {
                context.Succeed(requirement);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Permission check failed for user {EmpCode} module {Module} action {Action}",
                empCode, requirement.ModuleCode, requirement.Action);
        }
    }
}

/// <summary>
/// Dynamically materialises policies of the form "rp.perm:MODULE:ACTION".
/// Avoids registering a policy for every module-action combination at startup.
/// </summary>
public sealed class PermissionPolicyProvider : Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider
{
    private readonly Microsoft.Extensions.Options.IOptions<AuthorizationOptions> _options;
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    {
        _options = options;
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionPolicy.TryParse(policyName, out var module, out var action))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(module, action))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}
