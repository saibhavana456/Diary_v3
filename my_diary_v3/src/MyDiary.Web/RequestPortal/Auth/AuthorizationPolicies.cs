using Microsoft.AspNetCore.Authorization;
using RequestPortal.Core;

namespace RequestPortal.Web.Auth;

public static class AuthorizationPolicies
{
    // ── Policy names ─────────────────────────────────────────────────────────
    public const string AnyStaff        = nameof(AnyStaff);        // all authenticated users
    public const string AnyHandler      = nameof(AnyHandler);      // has any vertical process level L1–L5
    public const string OpsOversight    = nameof(OpsOversight);    // ZO+ org role or any VertL
    public const string VertAdmin       = nameof(VertAdmin);       // CO_VERT_ADMIN for any vertical
    public const string AppAdmin        = nameof(AppAdmin);        // APP_ADMIN or SUPER_ADMIN
    public const string SuperAdmin      = nameof(SuperAdmin);      // SUPER_ADMIN only
    public const string FeedbackAdmin = nameof(FeedbackAdmin);   // FEEDBACK_ADMIN or SUPER_ADMIN

    // Legacy aliases kept for Blazor page @attribute compatibility
    public const string Employee        = AnyStaff;
    public const string Admin           = AppAdmin;
    public const string OversightOrAdmin = OpsOversight;

    public static void AddRequestPortalAuthorization(AuthorizationOptions o)
    {
        // Every authenticated portal user (all org roles qualify)
        o.AddPolicy(AnyStaff, p => p.RequireAssertion(c =>
            HasAnyRole(c,
                RoleCode.BranchUser, RoleCode.BranchHead,
                RoleCode.RoUser,     RoleCode.RoHead,
                RoleCode.ZoUser,     RoleCode.ZoHead,
                RoleCode.CoUser,     RoleCode.CoHead,
                RoleCode.CoVertAdmin, RoleCode.AppAdmin, RoleCode.SuperAdmin,
                RoleCode.VertL1, RoleCode.VertL2, RoleCode.VertL3,
                RoleCode.VertL4, RoleCode.VertL5)));

        // Request handler — holds at least one vertical process level
        o.AddPolicy(AnyHandler, p => p.RequireAssertion(c =>
            HasAnyRole(c, RoleCode.VertL1, RoleCode.VertL2, RoleCode.VertL3,
                          RoleCode.VertL4, RoleCode.VertL5)));

        // Operational oversight — ZO or above organisationally, or any vert level
        o.AddPolicy(OpsOversight, p => p.RequireAssertion(c =>
            HasAnyRole(c, RoleCode.ZoUser, RoleCode.ZoHead,
                          RoleCode.CoUser, RoleCode.CoHead, RoleCode.CoVertAdmin,
                          RoleCode.AppAdmin, RoleCode.SuperAdmin,
                          RoleCode.VertL3, RoleCode.VertL4, RoleCode.VertL5)));

        // Vertical admin — can manage delegations, broadcast, vertical level assignment
        o.AddPolicy(VertAdmin, p => p.RequireAssertion(c =>
            HasAnyRole(c, RoleCode.CoVertAdmin, RoleCode.AppAdmin, RoleCode.SuperAdmin)));

        // Application admin — granted by SuperAdmin; SuperAdmin also satisfies this
        o.AddPolicy(AppAdmin, p => p.RequireAssertion(c =>
            c.User.HasClaim("rp_app_admin", "true") ||
            c.User.HasClaim("rp_super_admin", "true")));

        // Super admin — exactly the two seeded super-admin users
        o.AddPolicy(SuperAdmin, p => p.RequireClaim("rp_super_admin", "true"));

        // Feedback admin — granted by SuperAdmin to specific users for the Feedback
        // Dashboard/list; SuperAdmin also satisfies this
        o.AddPolicy(FeedbackAdmin, p => p.RequireAssertion(c =>
            c.User.HasClaim("rp_feedback_admin", "true") ||
            c.User.HasClaim("rp_super_admin", "true")));
    }

    private static bool HasAnyRole(AuthorizationHandlerContext c, params RoleCode[] roles)
        => roles.Any(r => c.User.HasClaim("rp_role", r.ToString()));
}
