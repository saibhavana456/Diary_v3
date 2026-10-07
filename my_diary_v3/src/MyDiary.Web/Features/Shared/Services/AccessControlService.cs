using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace MyDiary.Web.Features.Shared.Services;

/// <summary>One configured zone allowed to access the application.</summary>
public sealed class AllowedZone
{
    public string ZoneSolid { get; set; } = "";
    public string ZoneName { get; set; } = "";
}

/// <summary>
/// Reads the whole closed-group access-control configuration from the Oracle
/// MyDiaryDB (MD_ACCESS_SETTING / MD_ALLOWED_PF / MD_ALLOWED_ZONE) instead of
/// appsettings, so access can be enabled/disabled and zones/PF-IDs added without
/// a config or Kubernetes change.
/// </summary>
public interface IAccessControlService
{
    /// <summary>The master on/off flag (MD_ACCESS_SETTING key 'ENABLED' = 'Y').</summary>
    Task<bool> IsEnabledAsync(CancellationToken ct = default);

    /// <summary>Active PF-ID whitelist (grants access regardless of zone).</summary>
    Task<IReadOnlyList<string>> GetAllowedPfIdsAsync(CancellationToken ct = default);

    /// <summary>All active allowed zones (SOL id + display name).</summary>
    Task<IReadOnlyList<AllowedZone>> GetAllowedZonesAsync(CancellationToken ct = default);
}

public sealed class AccessControlService : IAccessControlService
{
    private readonly IConfiguration _configuration;

    public AccessControlService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private OracleConnection CreateConnection()
        => new(_configuration.GetConnectionString("MyDiaryDBConnection"));

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            await using var con = CreateConnection();
            await con.OpenAsync(ct);

            const string sql = @"
                SELECT SETTING_VALUE
                FROM MD_ACCESS_SETTING
                WHERE UPPER(TRIM(SETTING_KEY)) = 'ENABLED'";

            await using var cmd = new OracleCommand(sql, con) { BindByName = true };
            var value = (await cmd.ExecuteScalarAsync(ct))?.ToString()?.Trim();

            var enabled = string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                       || value == "1";

            AppLogger.LogInfo($"AccessControlService: ENABLED = {enabled} (raw '{value}').");
            return enabled;
        }
        catch (Exception ex)
        {
            // Fail closed: if we can't read the flag, treat access control as OFF so
            // we don't accidentally lock everyone out on a transient DB error. (The
            // zone/PF checks still gate access when the flag IS on.)
            AppLogger.LogError(ex, "AccessControlService: failed to read ENABLED flag from MD_ACCESS_SETTING; defaulting to disabled.");
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetAllowedPfIdsAsync(CancellationToken ct = default)
    {
        var pfIds = new List<string>();
        try
        {
            await using var con = CreateConnection();
            await con.OpenAsync(ct);

            const string sql = @"
                SELECT PF_ID
                FROM MD_ALLOWED_PF
                WHERE IS_ACTIVE = 'Y'";

            await using var cmd = new OracleCommand(sql, con) { BindByName = true };
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var pf = reader["PF_ID"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(pf))
                    pfIds.Add(pf);
            }

            AppLogger.LogInfo($"AccessControlService: loaded {pfIds.Count} active allowed PF id(s).");
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "AccessControlService: failed to load allowed PF ids from MD_ALLOWED_PF.");
        }
        return pfIds;
    }

    public async Task<IReadOnlyList<AllowedZone>> GetAllowedZonesAsync(CancellationToken ct = default)
    {
        var zones = new List<AllowedZone>();
        try
        {
            await using var con = CreateConnection();
            await con.OpenAsync(ct);

            const string sql = @"
                SELECT ZONE_SOLID, ZONE_NAME
                FROM MD_ALLOWED_ZONE
                WHERE IS_ACTIVE = 'Y'
                ORDER BY ZONE_NAME";

            await using var cmd = new OracleCommand(sql, con) { BindByName = true };
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                zones.Add(new AllowedZone
                {
                    ZoneSolid = reader["ZONE_SOLID"]?.ToString()?.Trim() ?? "",
                    ZoneName = reader["ZONE_NAME"]?.ToString()?.Trim() ?? ""
                });
            }

            AppLogger.LogInfo($"AccessControlService: loaded {zones.Count} active allowed zone(s).");
        }
        catch (Exception ex)
        {
            // Fail closed on the caller's side: empty list => access denied.
            AppLogger.LogError(ex, "AccessControlService: failed to load allowed zones from MD_ALLOWED_ZONE.");
        }
        return zones;
    }
}
