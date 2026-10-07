using Dapper;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Data.Repositories;

/// <summary>
/// Resolves the audience for a broadcast from live user data (RP_USER).
///   • Bank-wide  → every active user.
///   • Vertical   → active users assigned to that vertical (RP_RBAC_VERTICAL_LEVEL).
///   • Zone/RO/Branch → active users (RP_USER carries no org hierarchy in this
///     build, so these fall back to all active users).
/// Replaces the placeholder EmptyBroadcastDirectory.
/// </summary>
public sealed class SqlBroadcastDirectory : IBroadcastDirectory
{
    private readonly IDbConnectionFactory _factory;
    public SqlBroadcastDirectory(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<AppUser>> ResolveAudienceAsync(BroadcastScope scope, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        //const string cols = @"ID AS ""Id"", EMP_CODE AS ""EmpCode"", NAME AS ""Name"",
        //                      EMAIL AS ""Email"", MOBILE AS ""Mobile"", AD_SAM AS ""AdSamAccountName"",
        //                      IS_ACTIVE AS ""IsActive""";

        const string cols = @"pf_number AS ""Id"", pf_number AS ""EmpCode"", emp_NAME AS ""Name"",
                              EMAIL AS ""Email"", MOBILE AS ""Mobile"", pf_number AS ""AdSamAccountName"",
                              IS_ACTIVE AS ""IsActive""";

        string sql;
        object? args = null;

        if (scope.Kind == BroadcastScopeKind.Vertical && scope.VerticalId is not null)
        {
            //sql = $@"SELECT {cols} FROM rp_m_employee u
            //         WHERE u.IS_ACTIVE = 1
            //           AND EXISTS (SELECT 1 FROM RP_RBAC_VERTICAL_LEVEL vl
            //                       WHERE vl.EMP_CODE = u.EMP_CODE AND vl.IS_ACTIVE = 1
            //                         AND vl.VERTICAL_ID = :vid AND vl.REVOKED_AT_UTC IS NULL)
            //         ORDER BY u.NAME";

            sql = $@"SELECT {cols} FROM rp_m_employee u
                     WHERE u.IS_ACTIVE = 1
                       AND EXISTS (SELECT 1 FROM RP_RBAC_VERTICAL_LEVEL vl
                                   WHERE vl.EMP_CODE = u.EMP_CODE AND vl.IS_ACTIVE = 1
                                     AND vl.VERTICAL_ID = :vid AND vl.REVOKED_AT_UTC IS NULL)
                     ORDER BY u.NAME";

            args = new { vid = scope.VerticalId.Value };
        }
        else
        {
            // Bank-wide (and org-scoped fallback): all active users.
            sql = $@"SELECT {cols} FROM rp_m_employee WHERE IS_ACTIVE = 1 ORDER BY EMP_NAME";
        }

        return (await c.QueryAsync<AppUser>(new CommandDefinition(sql, args, cancellationToken: ct))).AsList();
    }
}
