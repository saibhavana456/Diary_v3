using Microsoft.AspNetCore.Authorization;

namespace MyDiary.Web.Auth;

/// <summary>
/// Unified authorization policy set:
///   - Hierarchy policies from Project B (Branch/Region/Zone/CO)
///   - RBAC admin policies from Project A (AppAdmin/SuperAdmin)
/// All Project B pages default to RequireAuthenticated until explicit RBAC is wired.
/// </summary>
public static class AuthorizationPolicies
{
    public static void AddMyDiaryAuthorization(AuthorizationOptions opts)
    {
        // ── My Diary hierarchy policies (Project B) ───────────────────────
        opts.AddPolicy("RequireAuthenticated", p => p.RequireAuthenticatedUser());

        opts.AddPolicy("RequireBranch", p =>
            p.RequireAuthenticatedUser()
             .RequireClaim("privilege", "BRANCH", "REGION", "ZONE", "CO"));

        opts.AddPolicy("RequireRegion", p =>
            p.RequireAuthenticatedUser()
             .RequireClaim("privilege", "REGION", "ZONE", "CO"));

        opts.AddPolicy("RequireZone", p =>
            p.RequireAuthenticatedUser()
             .RequireClaim("privilege", "ZONE", "CO"));

        opts.AddPolicy("RequireCO", p =>
            p.RequireAuthenticatedUser()
             .RequireClaim("privilege", "CO"));

        // ── Request Portal RBAC policies (Project A — owns AnyStaff/AppAdmin/
        //    SuperAdmin/AnyHandler/OpsOversight/VertAdmin, all rp_role-claim based) ─
        global::RequestPortal.Web.Auth.AuthorizationPolicies.AddRequestPortalAuthorization(opts);

        // ── Server-to-server API key (external systems e.g. UCCRMC command centre) ─
        opts.AddPolicy("ApiKey", p =>
            p.AddAuthenticationSchemes(MyDiary.Web.Auth.ApiKeyAuthenticationHandler.SchemeName)
             .RequireAuthenticatedUser()
             .RequireClaim("api_client"));

        // NOTE: No global FallbackPolicy. A fallback "require authenticated"
        // policy also gates Blazor's own /_blazor circuit endpoint, which blocks
        // anonymous users on /login from ever establishing an interactive circuit
        // (forms then do a static POST and fail). Page protection is enforced via
        // AuthorizeRouteView + NavigationGuard (client-side redirect) and per-page
        // [Authorize] attributes (Project A pages) instead.
    }
}
