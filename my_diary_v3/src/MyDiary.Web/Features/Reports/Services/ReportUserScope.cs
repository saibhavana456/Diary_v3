using MyDiary.Core.Abstractions;

namespace MyDiary.Web.Features.Reports.Services;

/// <summary>
/// The logged-in user's report access scope, resolved once from <see cref="ICurrentUser"/>
/// (which reads the auth claims set at login by CustomAuthenticationStateProvider).
///
/// Fail-closed: an unknown/missing privilege claim resolves to BRANCH (the most restrictive scope)
/// so a bad or absent claim can never silently widen access to all zones/regions/branches.
/// </summary>
public sealed record ReportUserScope(
    string Privilege,
    string ZoneSolid,
    string RegionSolid,
    string BranchSolid,
    string ZoneCode,
    string RegionCode,
    string BranchCode)
{
    /// <summary>Value used for a missing SOL ID / code — matches the legacy hardcoded default the
    /// report WHERE-clauses already treat as "no specific unit".</summary>
    public const string None = "00000";

    public bool IsCo => Privilege == "CO";
    public bool IsZone => Privilege == "ZONE";
    public bool IsRegion => Privilege == "REGION";
    public bool IsBranch => Privilege == "BRANCH";

    /// <summary>Resolves the current user's report scope from their claims.</summary>
    public static ReportUserScope Resolve(ICurrentUser user)
    {
        var privilege = (user.Privilege ?? "").Trim().ToUpperInvariant() switch
        {
            "CO" => "CO",
            "ZONE" => "ZONE",
            "REGION" => "REGION",
            "BRANCH" => "BRANCH",
            _ => "BRANCH"   // fail closed
        };

        static string OrNone(string? v) => string.IsNullOrWhiteSpace(v) ? None : v!.Trim();

        return new ReportUserScope(
            privilege,
            OrNone(user.ZoneSolid),
            OrNone(user.RegionSolid),
            OrNone(user.BranchSolid),
            OrNone(user.ZoneCode),
            OrNone(user.RegionCode),
            OrNone(user.BranchCode));
    }
}
