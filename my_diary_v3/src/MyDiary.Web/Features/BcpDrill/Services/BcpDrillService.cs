using System.Data;
using System.Data.Common;
using System.Security.Claims;
using MyDiary.Web.Features.BcpDrill.Models;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace MyDiary.Web.Features.BcpDrill.Services;

/// <summary>BCP / DR Drill Compliance - raw Oracle ADO, same style as MsmeFrrService (BindByName, no ORM).</summary>
public sealed class BcpDrillService : IBcpDrillService
{
    private readonly string _cs;
    private readonly ILogger<BcpDrillService> _log;

    private static readonly string[] DocExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
    private const long MaxDocBytes = 10L * 1024 * 1024;

    public BcpDrillService(IConfiguration config, ILogger<BcpDrillService> log)
    {
        _cs = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner");
        _log = log;
    }

    private OracleConnection Conn() => new(_cs);

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Scope
    // ══════════════════════════════════════════════════════════════════════════════════════════

    public BcpScope GetScope(ClaimsPrincipal user)
    {
        var role = (user.FindFirst(ClaimTypes.Role)?.Value
                    ?? user.FindFirst("Role")?.Value
                    ?? user.FindFirst("privilege")?.Value
                    ?? "CO").Trim().ToUpperInvariant();
        int.TryParse(user.FindFirst("employee_scale_code")?.Value, out var scale);

        var isAdmin = IsTrue(user.FindFirst("rp_super_admin")?.Value)
                   || IsTrue(user.FindFirst("rp_app_admin")?.Value)
                   || user.FindAll("md_role").Any(c => c.Value.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                                                    || c.Value.Equals("AppAdmin", StringComparison.OrdinalIgnoreCase));

        var level = role switch
        {
            "BRANCH" => BcpLevel.Branch,
            "REGION" => BcpLevel.Region,
            "ZONE" => BcpLevel.Zone,
            _ => BcpLevel.Central
        };
        var canViewAll = isAdmin || (level == BcpLevel.Central && scale < 6);

        return new BcpScope
        {
            Level = level,
            IsAdmin = isAdmin,
            CanViewAll = canViewAll,
            ZoneCodes = Candidates(user, "zone_solid", "zone_code"),
            RegionCodes = Candidates(user, "region_solid", "region_code"),
            BranchCodes = Candidates(user, "branch_solid", "branch_code", "br_solid"),
            EmplId = user.FindFirst("emplid")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "UNKNOWN",
        };
    }

    private static bool IsTrue(string? v) => v is not null && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");

    private static List<string> Candidates(ClaimsPrincipal user, params string[] types) =>
        types.Select(t => user.FindFirst(t)?.Value)
             .Where(v => !string.IsNullOrWhiteSpace(v) && v != "0" && v != "00000")
             .Select(v => v!.Trim())
             .Distinct(StringComparer.OrdinalIgnoreCase)
             .ToList();

    /// <summary>Row-scope predicate on ZONE_CODE / REGION_CODE / SOL_ID of the aliased table.</summary>
    private static string ScopePred(BcpScope scope, OracleCommand cmd, string alias, string pfx = "sc")
    {
        if (scope.CanViewAll) return "";
        var (col, vals) = scope.Level switch
        {
            BcpLevel.Region => ($"{alias}.REGION_CODE", scope.RegionCodes),
            BcpLevel.Branch => ($"{alias}.SOL_ID", scope.BranchCodes),
            _ => ($"{alias}.ZONE_CODE", scope.ZoneCodes),
        };
        return InList(cmd, col, vals, pfx);
    }

    /// <summary>Certificates carry no SOL_ID: branches see none, ROs see their region, ZO/CO their zone.</summary>
    private static string CertScopePred(BcpScope scope, OracleCommand cmd, string alias)
    {
        if (scope.CanViewAll) return "";
        return scope.Level switch
        {
            BcpLevel.Branch => " AND 1 = 0",
            BcpLevel.Region => InList(cmd, $"{alias}.REGION_CODE", scope.RegionCodes, "cs"),
            _ => InList(cmd, $"{alias}.ZONE_CODE", scope.ZoneCodes, "cs"),
        };
    }

    private static string InList(OracleCommand cmd, string col, IReadOnlyList<string> vals, string pfx)
    {
        if (vals.Count == 0) return " AND 1 = 0";
        var names = new List<string>();
        for (var i = 0; i < vals.Count; i++)
        {
            var p = $"{pfx}{i}";
            names.Add($"UPPER(:{p})");
            cmd.Parameters.Add(p, vals[i]);
        }
        return $" AND UPPER({col}) IN ({string.Join(", ", names)})";
    }

    private static bool IsReal(string? v) => !string.IsNullOrWhiteSpace(v) && !v.Equals("ALL", StringComparison.OrdinalIgnoreCase);

    private static void Bind(OracleCommand cmd, string name, object? v) =>
        cmd.Parameters.Add(new OracleParameter(name, v ?? DBNull.Value));

    private static string? Trim(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > max ? s.Trim()[..max] : s.Trim());

    private static string? S(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));
    private static DateTime? Dt(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDateTime(i);
    private static decimal? Dec(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
    private static int? I(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    private static long L(DbDataReader r, int i) => Convert.ToInt64(r.GetValue(i));

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Audit
    // ══════════════════════════════════════════════════════════════════════════════════════════

    private static async Task AuditAsync(OracleConnection cn, OracleTransaction? tx, BcpScope scope,
        string entity, object? entityId, string action, string? detail)
    {
        await using var cmd = new OracleCommand(
            @"INSERT INTO RP_BCP_AUDIT (ENTITY, ENTITY_ID, ACTION, DETAIL, ACTOR, ACTOR_LEVEL)
              VALUES (:p_e, :p_id, :p_a, :p_d, :p_by, :p_lv)", cn) { BindByName = true, Transaction = tx };
        Bind(cmd, "p_e", entity);
        Bind(cmd, "p_id", entityId?.ToString());
        Bind(cmd, "p_a", action);
        Bind(cmd, "p_d", Trim(detail, 4000));
        Bind(cmd, "p_by", scope.EmplId);
        Bind(cmd, "p_lv", scope.IsAdmin ? "ADMIN" : scope.Level.ToString().ToUpperInvariant());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<BcpAuditRow>> ListAuditAsync(ClaimsPrincipal user, string? entity, string? entityId, int max = 300)
    {
        var scope = GetScope(user);
        // Full log is for admins / Central Office. A per-record trail is allowed for whoever can read the record.
        if (!scope.CanViewAudit)
        {
            var ok = entity == "DRILL" && long.TryParse(entityId, out var did) && await GetDrillAsync(user, did) is not null
                  || entity == "CERT" && long.TryParse(entityId, out var cid) && await GetCertAsync(user, cid) is not null;
            if (!ok) return new();
        }

        var list = new List<BcpAuditRow>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = "SELECT AUDIT_ID, ENTITY, ENTITY_ID, ACTION, DETAIL, ACTOR, ACTOR_LEVEL, AT FROM RP_BCP_AUDIT WHERE 1 = 1";
        if (!string.IsNullOrWhiteSpace(entity)) { sql += " AND ENTITY = :p_e"; Bind(cmd, "p_e", entity); }
        if (!string.IsNullOrWhiteSpace(entityId)) { sql += " AND ENTITY_ID = :p_id"; Bind(cmd, "p_id", entityId); }
        sql += " ORDER BY AUDIT_ID DESC FETCH FIRST :p_max ROWS ONLY";
        Bind(cmd, "p_max", max);
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new BcpAuditRow
            {
                Id = L(r, 0), Entity = S(r, 1) ?? "", EntityId = S(r, 2), Action = S(r, 3) ?? "",
                Detail = S(r, 4), Actor = S(r, 5), ActorLevel = S(r, 6), At = Dt(r, 7) ?? DateTime.MinValue
            });
        return list;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Org options (from the branch map, so they always agree with the MIS universe)
    // ══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<OrgOption>> GetZonesAsync(ClaimsPrincipal user)
    {
        var scope = GetScope(user);
        var list = new List<OrgOption>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        cmd.CommandText = "SELECT ZONE_CODE, NVL(MAX(ZONE_NAME), ZONE_CODE) FROM RP_BCP_BRANCH_MAP m WHERE IS_ACTIVE = 1"
                          + ScopePred(scope, cmd, "m") + " GROUP BY ZONE_CODE ORDER BY 2";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new OrgOption(S(r, 0)!, S(r, 1)!));
        return list;
    }

    public async Task<List<OrgOption>> GetRegionsAsync(ClaimsPrincipal user, string? zoneCode)
    {
        var scope = GetScope(user);
        var list = new List<OrgOption>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = "SELECT REGION_CODE, NVL(MAX(REGION_NAME), REGION_CODE), MAX(ZONE_CODE) FROM RP_BCP_BRANCH_MAP m WHERE IS_ACTIVE = 1";
        if (IsReal(zoneCode)) { sql += " AND ZONE_CODE = :p_z"; Bind(cmd, "p_z", zoneCode); }
        sql += ScopePred(scope, cmd, "m") + " GROUP BY REGION_CODE ORDER BY 2";
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new OrgOption(S(r, 0)!, S(r, 1)!, S(r, 2)));
        return list;
    }

    public async Task<List<OrgOption>> GetBranchesAsync(ClaimsPrincipal user, string? regionCode)
    {
        var scope = GetScope(user);
        var list = new List<OrgOption>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = "SELECT SOL_ID, BRANCH_NAME || ' (' || SOL_ID || ')', REGION_CODE FROM RP_BCP_BRANCH_MAP m WHERE IS_ACTIVE = 1";
        if (IsReal(regionCode)) { sql += " AND REGION_CODE = :p_r"; Bind(cmd, "p_r", regionCode); }
        sql += ScopePred(scope, cmd, "m") + " ORDER BY BRANCH_NAME";
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new OrgOption(S(r, 0)!, S(r, 1)!, S(r, 2)));
        return list;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Branch map
    // ══════════════════════════════════════════════════════════════════════════════════════════

    private const string BranchCols = "m.SOL_ID, m.BRANCH_CODE, m.BRANCH_NAME, m.REGION_CODE, m.REGION_NAME, m.ZONE_CODE, m.ZONE_NAME, m.CATEGORY, m.LINK1_SOL_ID, m.LINK2_SOL_ID, m.IS_ACTIVE";

    private static BcpBranch ReadBranch(DbDataReader r) => new()
    {
        SolId = S(r, 0)!, BranchCode = S(r, 1), BranchName = S(r, 2)!, RegionCode = S(r, 3)!, RegionName = S(r, 4),
        ZoneCode = S(r, 5)!, ZoneName = S(r, 6), Category = S(r, 7), Link1SolId = S(r, 8), Link2SolId = S(r, 9),
        IsActive = Convert.ToInt32(r.GetValue(10)) == 1
    };

    public async Task<List<BcpBranch>> ListBranchMapAsync(ClaimsPrincipal user, string? regionCode = null, bool includeInactive = false)
    {
        var scope = GetScope(user);
        var list = new List<BcpBranch>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = $"SELECT {BranchCols} FROM RP_BCP_BRANCH_MAP m WHERE 1 = 1";
        if (!(includeInactive && scope.IsAdmin)) sql += " AND m.IS_ACTIVE = 1";
        if (IsReal(regionCode)) { sql += " AND m.REGION_CODE = :p_r"; Bind(cmd, "p_r", regionCode); }
        sql += ScopePred(scope, cmd, "m") + " ORDER BY m.ZONE_NAME, m.REGION_NAME, m.BRANCH_NAME";
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(ReadBranch(r));
        return list;
    }

    public async Task<BcpBranch?> GetMyBranchAsync(ClaimsPrincipal user)
    {
        var scope = GetScope(user);
        if (scope.Level != BcpLevel.Branch || scope.BranchCodes.Count == 0) return null;
        var all = await ListBranchMapAsync(user);
        return all.FirstOrDefault();
    }

    public async Task<List<BcpBranch>> GetPeerBranchesAsync(ClaimsPrincipal user, string solId)
    {
        var scope = GetScope(user);
        var list = new List<BcpBranch>();
        await using var cn = Conn();
        await cn.OpenAsync();
        var home = await LoadBranchAsync(cn, solId);
        if (home is null || !scope.OwnsOffice(home.ZoneCode, home.RegionCode, home.SolId)) return list;
        await using var cmd = new OracleCommand(
            $"SELECT {BranchCols} FROM RP_BCP_BRANCH_MAP m WHERE m.REGION_CODE = :p_r AND m.IS_ACTIVE = 1 AND m.SOL_ID <> :p_s ORDER BY m.BRANCH_NAME", cn) { BindByName = true };
        Bind(cmd, "p_r", home.RegionCode);
        Bind(cmd, "p_s", home.SolId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(ReadBranch(r));
        return list;
    }

    private async Task<BcpBranch?> LoadBranchAsync(OracleConnection cn, string solId, OracleTransaction? tx = null)
    {
        await using var cmd = new OracleCommand($"SELECT {BranchCols} FROM RP_BCP_BRANCH_MAP m WHERE m.SOL_ID = :p_s", cn) { BindByName = true, Transaction = tx };
        Bind(cmd, "p_s", solId);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadBranch(r) : null;
    }

    public async Task SaveBranchAsync(ClaimsPrincipal user, BcpBranch b)
    {
        var scope = GetScope(user);
        if (!scope.CanManageMapping) throw new UnauthorizedAccessException("Only an App Admin may maintain the branch mapping.");
        if (string.IsNullOrWhiteSpace(b.SolId) || string.IsNullOrWhiteSpace(b.BranchName)
            || string.IsNullOrWhiteSpace(b.RegionCode) || string.IsNullOrWhiteSpace(b.ZoneCode))
            throw new InvalidOperationException("SOL ID, branch name, region code and zone code are required.");

        await using var cn = Conn();
        await cn.OpenAsync();
        await using var tx = (OracleTransaction)await cn.BeginTransactionAsync();
        var existed = await LoadBranchAsync(cn, b.SolId.Trim(), tx) is not null;
        await using (var cmd = new OracleCommand(@"
            MERGE INTO RP_BCP_BRANCH_MAP t USING (SELECT :p_sol AS SOL_ID FROM DUAL) s ON (t.SOL_ID = s.SOL_ID)
            WHEN MATCHED THEN UPDATE SET BRANCH_CODE = :p_bc, BRANCH_NAME = :p_bn, REGION_CODE = :p_rc, REGION_NAME = :p_rn,
                 ZONE_CODE = :p_zc, ZONE_NAME = :p_zn, CATEGORY = :p_cat, LINK1_SOL_ID = :p_l1, LINK2_SOL_ID = :p_l2,
                 IS_ACTIVE = :p_act, UPDATED_BY = :p_by, UPDATED_AT = SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (SOL_ID, BRANCH_CODE, BRANCH_NAME, REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME, CATEGORY, LINK1_SOL_ID, LINK2_SOL_ID, IS_ACTIVE, CREATED_BY)
                 VALUES (:p_sol, :p_bc, :p_bn, :p_rc, :p_rn, :p_zc, :p_zn, :p_cat, :p_l1, :p_l2, :p_act, :p_by)", cn) { BindByName = true, Transaction = tx })
        {
            Bind(cmd, "p_sol", b.SolId.Trim());
            Bind(cmd, "p_bc", Trim(b.BranchCode, 20));
            Bind(cmd, "p_bn", Trim(b.BranchName, 200));
            Bind(cmd, "p_rc", Trim(b.RegionCode, 20));
            Bind(cmd, "p_rn", Trim(b.RegionName, 200));
            Bind(cmd, "p_zc", Trim(b.ZoneCode, 20));
            Bind(cmd, "p_zn", Trim(b.ZoneName, 200));
            Bind(cmd, "p_cat", Trim(b.Category, 50));
            Bind(cmd, "p_l1", Trim(b.Link1SolId, 20));
            Bind(cmd, "p_l2", Trim(b.Link2SolId, 20));
            Bind(cmd, "p_act", b.IsActive ? 1 : 0);
            Bind(cmd, "p_by", scope.EmplId);
            await cmd.ExecuteNonQueryAsync();
        }
        await AuditAsync(cn, tx, scope, "MAPPING", b.SolId.Trim(), existed ? "UPDATE" : "CREATE",
            $"{b.BranchName} -> RO {b.RegionCode} / ZO {b.ZoneCode}; links {b.Link1SolId}/{b.Link2SolId}; active={b.IsActive}");
        await tx.CommitAsync();
    }

    public async Task<int> LoadBranchesFromMasterAsync(ClaimsPrincipal user)
    {
        var scope = GetScope(user);
        if (!scope.CanManageMapping) throw new UnauthorizedAccessException("Only an App Admin may load the branch mapping.");
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var tx = (OracleTransaction)await cn.BeginTransactionAsync();
        int n;
        await using (var cmd = new OracleCommand(@"
            INSERT INTO RP_BCP_BRANCH_MAP (SOL_ID, BRANCH_CODE, BRANCH_NAME, REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME, CREATED_BY)
            SELECT SOL_ID, MAX(BRANCH_CODE), MAX(BRANCH_NAME), MAX(REGION_CODE), MAX(REGION_NAME), MAX(ZONE_CODE), MAX(ZONE_NAME), :p_by
              FROM BRANCH_MASTER
             WHERE SOL_ID IS NOT NULL AND BRANCH_NAME IS NOT NULL AND REGION_CODE IS NOT NULL AND ZONE_CODE IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM RP_BCP_BRANCH_MAP m WHERE m.SOL_ID = BRANCH_MASTER.SOL_ID)
             GROUP BY SOL_ID", cn) { BindByName = true, Transaction = tx })
        {
            Bind(cmd, "p_by", scope.EmplId);
            n = await cmd.ExecuteNonQueryAsync();
        }
        await AuditAsync(cn, tx, scope, "MAPPING", "*", "LOAD_MASTER", $"{n} branch(es) loaded from BRANCH_MASTER");
        await tx.CommitAsync();
        return n;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Annexure-I drills
    // ══════════════════════════════════════════════════════════════════════════════════════════

    private const string DrillCols = @"d.DRILL_ID, d.DRILL_TYPE, d.SOL_ID, d.BRANCH_CODE, d.BRANCH_NAME, d.REGION_CODE, d.REGION_NAME,
        d.ZONE_CODE, d.ZONE_NAME, d.CATEGORY, d.STAFF_NORMAL, d.DRILL_DATE, d.ALT_SOL_ID, d.ALT_BRANCH_CODE, d.ALT_BRANCH_NAME,
        d.ALT_REGION, d.ALT_ZONE, d.ALT_CATEGORY, d.STAFF_ALT, d.REMARKS, d.IS_COMPLIANT, d.FIN_YEAR, d.STATUS, d.REVIEW_BY,
        d.REVIEW_AT, d.REVIEW_REMARKS, d.DOC_NAME, d.DOC_SIZE, d.ENTRY_BY, d.ENTRY_AT, d.UPDATED_BY, d.UPDATED_AT";

    private static BcpDrillEntry ReadDrill(DbDataReader r) => new()
    {
        Id = L(r, 0), DrillType = S(r, 1)!, SolId = S(r, 2)!, BranchCode = S(r, 3), BranchName = S(r, 4)!,
        RegionCode = S(r, 5)!, RegionName = S(r, 6), ZoneCode = S(r, 7)!, ZoneName = S(r, 8), Category = S(r, 9),
        StaffNormal = I(r, 10), DrillDate = Dt(r, 11) ?? DateTime.Today, AltSolId = S(r, 12), AltBranchCode = S(r, 13),
        AltBranchName = S(r, 14), AltRegion = S(r, 15), AltZone = S(r, 16), AltCategory = S(r, 17), StaffAlt = I(r, 18),
        Remarks = S(r, 19), IsCompliant = Convert.ToInt32(r.GetValue(20)) == 1, FinYear = S(r, 21), Status = S(r, 22)!,
        ReviewBy = S(r, 23), ReviewAt = Dt(r, 24), ReviewRemarks = S(r, 25), DocName = S(r, 26),
        DocSize = r.IsDBNull(27) ? null : L(r, 27), EntryBy = S(r, 28), EntryAt = Dt(r, 29), UpdatedBy = S(r, 30), UpdatedAt = Dt(r, 31)
    };

    private static string FilterSql(BcpDrillFilter f, OracleCommand cmd, string alias)
    {
        var sql = "";
        if (IsReal(f.ZoneCode)) { sql += $" AND {alias}.ZONE_CODE = :f_z"; Bind(cmd, "f_z", f.ZoneCode); }
        if (IsReal(f.RegionCode)) { sql += $" AND {alias}.REGION_CODE = :f_r"; Bind(cmd, "f_r", f.RegionCode); }
        if (IsReal(f.SolId)) { sql += $" AND {alias}.SOL_ID = :f_b"; Bind(cmd, "f_b", f.SolId); }
        return sql;
    }

    public async Task<List<BcpDrillEntry>> ListDrillsAsync(ClaimsPrincipal user, BcpDrillFilter f)
    {
        var scope = GetScope(user);
        var list = new List<BcpDrillEntry>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = $"SELECT {DrillCols} FROM RP_BCP_DRILL d WHERE d.IS_ACTIVE = 1";
        sql += ScopePred(scope, cmd, "d");
        sql += FilterSql(f, cmd, "d");
        if (BcpTypes.IsValid(f.DrillType)) { sql += " AND d.DRILL_TYPE = :f_t"; Bind(cmd, "f_t", f.DrillType); }
        if (!string.IsNullOrWhiteSpace(f.Status) && !f.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        { sql += " AND d.STATUS = :f_s"; Bind(cmd, "f_s", f.Status); }
        if (f.From.HasValue) { sql += " AND d.DRILL_DATE >= :f_from"; Bind(cmd, "f_from", f.From.Value.Date); }
        if (f.To.HasValue) { sql += " AND d.DRILL_DATE <= :f_to"; Bind(cmd, "f_to", f.To.Value.Date); }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            sql += " AND (UPPER(d.BRANCH_NAME) LIKE UPPER(:f_q) OR d.SOL_ID LIKE :f_q2 OR UPPER(d.ALT_BRANCH_NAME) LIKE UPPER(:f_q3))";
            Bind(cmd, "f_q", "%" + f.Search.Trim() + "%");
            Bind(cmd, "f_q2", "%" + f.Search.Trim() + "%");
            Bind(cmd, "f_q3", "%" + f.Search.Trim() + "%");
        }
        sql += " ORDER BY d.DRILL_DATE DESC, d.DRILL_ID DESC";
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(ReadDrill(r));
        return list;
    }

    public async Task<BcpDrillEntry?> GetDrillAsync(ClaimsPrincipal user, long id)
    {
        var scope = GetScope(user);
        BcpDrillEntry? d;
        await using var cn = Conn();
        await cn.OpenAsync();
        await using (var cmd = new OracleCommand { Connection = cn, BindByName = true })
        {
            cmd.CommandText = $"SELECT {DrillCols} FROM RP_BCP_DRILL d WHERE d.DRILL_ID = :p_id AND d.IS_ACTIVE = 1" + ScopePred(scope, cmd, "d");
            Bind(cmd, "p_id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            d = await r.ReadAsync() ? ReadDrill(r) : null;
        }
        if (d is null) return null;

        await using var mc = new OracleCommand(
            "SELECT METRIC_CODE, NORMAL_COUNT, NORMAL_AMOUNT, ALT_COUNT, ALT_AMOUNT FROM RP_BCP_DRILL_METRIC WHERE DRILL_ID = :p_id", cn) { BindByName = true };
        Bind(mc, "p_id", id);
        await using var mr = await mc.ExecuteReaderAsync();
        while (await mr.ReadAsync())
            d.Metrics.Add(new BcpMetricValue
            {
                Code = S(mr, 0)!, NormalCount = mr.IsDBNull(1) ? null : L(mr, 1), NormalAmount = Dec(mr, 2),
                AltCount = mr.IsDBNull(3) ? null : L(mr, 3), AltAmount = Dec(mr, 4)
            });
        return d;
    }

    public async Task<long> SaveDrillAsync(ClaimsPrincipal user, BcpDrillEntry d, byte[]? docBytes, string? docType, string? docName)
    {
        var scope = GetScope(user);
        if (!scope.CanSubmitDrill)
            throw new UnauthorizedAccessException($"{scope.DisplayLevel} users cannot submit branch drill compliance. It is submitted by the branch; the Regional Office reviews it.");
        if (!BcpTypes.IsValid(d.DrillType)) throw new InvalidOperationException("Drill type must be BCP Testing or DR Drill.");
        if (d.DrillDate.Date > DateTime.Today) throw new InvalidOperationException("Drill date cannot be in the future.");
        if (d.DrillDate.Date < DateTime.Today.AddYears(-3)) throw new InvalidOperationException("Drill date is too old.");
        if (string.IsNullOrWhiteSpace(d.SolId)) throw new InvalidOperationException("Branch is required.");
        if (string.IsNullOrWhiteSpace(d.AltBranchName) && string.IsNullOrWhiteSpace(d.AltSolId))
            throw new InvalidOperationException("Enter the branch where operations will be carried out.");
        if (d.StaffNormal < 0 || d.StaffAlt < 0) throw new InvalidOperationException("Staff counts cannot be negative.");
        foreach (var m in d.Metrics)
            if (m.NormalCount < 0 || m.AltCount < 0 || m.NormalAmount < 0 || m.AltAmount < 0)
                throw new InvalidOperationException("Operation figures cannot be negative.");

        string? docExt = null;
        if (docBytes is { Length: > 0 })
        {
            if (docBytes.Length > MaxDocBytes) throw new InvalidOperationException("Supporting document is larger than 10 MB.");
            docExt = Path.GetExtension(docName ?? "").ToLowerInvariant();
            if (!DocExtensions.Contains(docExt)) throw new InvalidOperationException("Supporting document must be pdf, doc, docx, xls, xlsx, jpg or png.");
        }

        await using var cn = Conn();
        await cn.OpenAsync();
        await using var tx = (OracleTransaction)await cn.BeginTransactionAsync();

        // Real branch identity comes from the map, never from the client.
        var home = await LoadBranchAsync(cn, d.SolId.Trim(), tx);
        if (home is null || !home.IsActive) throw new InvalidOperationException("This branch is not in the BCP branch mapping.");
        if (!scope.OwnsOffice(home.ZoneCode, home.RegionCode, home.SolId))
            throw new UnauthorizedAccessException("You may only submit for your own branch.");

        var alt = string.IsNullOrWhiteSpace(d.AltSolId) ? null : await LoadBranchAsync(cn, d.AltSolId.Trim(), tx);

        var isUpdate = d.Id > 0;
        string prevStatus = BcpStatus.Submitted;
        if (isUpdate)
        {
            await using var q = new OracleCommand("SELECT SOL_ID, STATUS FROM RP_BCP_DRILL WHERE DRILL_ID = :p_id AND IS_ACTIVE = 1", cn) { BindByName = true, Transaction = tx };
            Bind(q, "p_id", d.Id);
            await using var qr = await q.ExecuteReaderAsync();
            if (!await qr.ReadAsync()) throw new InvalidOperationException("Submission not found.");
            if (!string.Equals(S(qr, 0), home.SolId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The branch of a submission cannot be changed.");
            prevStatus = S(qr, 1)!;
            if (prevStatus == BcpStatus.Acknowledged && !scope.IsAdmin)
                throw new InvalidOperationException("This submission has been acknowledged by the Regional Office and can no longer be edited.");
        }

        var fy = BcpPeriod.FinYear(d.DrillDate);
        var pars = new Action<OracleCommand>(cmd =>
        {
            Bind(cmd, "p_type", d.DrillType);
            Bind(cmd, "p_sol", home.SolId);
            Bind(cmd, "p_bc", home.BranchCode);
            Bind(cmd, "p_bn", home.BranchName);
            Bind(cmd, "p_rc", home.RegionCode);
            Bind(cmd, "p_rn", home.RegionName);
            Bind(cmd, "p_zc", home.ZoneCode);
            Bind(cmd, "p_zn", home.ZoneName);
            Bind(cmd, "p_cat", Trim(home.Category ?? d.Category, 50));
            Bind(cmd, "p_sn", d.StaffNormal);
            Bind(cmd, "p_date", d.DrillDate.Date);
            Bind(cmd, "p_asol", alt?.SolId ?? Trim(d.AltSolId, 20));
            Bind(cmd, "p_abc", alt?.BranchCode ?? Trim(d.AltBranchCode, 20));
            Bind(cmd, "p_abn", alt?.BranchName ?? Trim(d.AltBranchName, 200));
            Bind(cmd, "p_areg", alt?.RegionName ?? Trim(d.AltRegion, 200));
            Bind(cmd, "p_azone", alt?.ZoneName ?? Trim(d.AltZone, 200));
            Bind(cmd, "p_acat", alt?.Category ?? Trim(d.AltCategory, 50));
            Bind(cmd, "p_sa", d.StaffAlt);
            Bind(cmd, "p_rem", Trim(d.Remarks, 4000));
            Bind(cmd, "p_comp", d.IsCompliant ? 1 : 0);
            Bind(cmd, "p_fy", fy);
            Bind(cmd, "p_by", scope.EmplId);
        });

        long id;
        if (!isUpdate)
        {
            await using var cmd = new OracleCommand(@"
                INSERT INTO RP_BCP_DRILL (DRILL_TYPE, SOL_ID, BRANCH_CODE, BRANCH_NAME, REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME, CATEGORY,
                    STAFF_NORMAL, DRILL_DATE, ALT_SOL_ID, ALT_BRANCH_CODE, ALT_BRANCH_NAME, ALT_REGION, ALT_ZONE, ALT_CATEGORY, STAFF_ALT,
                    REMARKS, IS_COMPLIANT, FIN_YEAR, ENTRY_BY)
                VALUES (:p_type, :p_sol, :p_bc, :p_bn, :p_rc, :p_rn, :p_zc, :p_zn, :p_cat, :p_sn, :p_date, :p_asol, :p_abc, :p_abn,
                    :p_areg, :p_azone, :p_acat, :p_sa, :p_rem, :p_comp, :p_fy, :p_by)
                RETURNING DRILL_ID INTO :p_id", cn) { BindByName = true, Transaction = tx };
            pars(cmd);
            var outId = new OracleParameter("p_id", OracleDbType.Int64) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outId);
            await cmd.ExecuteNonQueryAsync();
            id = Convert.ToInt64(((OracleDecimal)outId.Value!).Value);
        }
        else
        {
            id = d.Id;
            await using var cmd = new OracleCommand(@"
                UPDATE RP_BCP_DRILL SET DRILL_TYPE = :p_type, CATEGORY = :p_cat, STAFF_NORMAL = :p_sn, DRILL_DATE = :p_date,
                    ALT_SOL_ID = :p_asol, ALT_BRANCH_CODE = :p_abc, ALT_BRANCH_NAME = :p_abn, ALT_REGION = :p_areg, ALT_ZONE = :p_azone,
                    ALT_CATEGORY = :p_acat, STAFF_ALT = :p_sa, REMARKS = :p_rem, IS_COMPLIANT = :p_comp, FIN_YEAR = :p_fy,
                    STATUS = 'SUBMITTED', REVIEW_BY = NULL, REVIEW_AT = NULL, REVIEW_REMARKS = NULL,
                    UPDATED_BY = :p_by, UPDATED_AT = SYSTIMESTAMP
                 WHERE DRILL_ID = :p_id", cn) { BindByName = true, Transaction = tx };
            Bind(cmd, "p_type", d.DrillType);
            Bind(cmd, "p_cat", Trim(home.Category ?? d.Category, 50));
            Bind(cmd, "p_sn", d.StaffNormal);
            Bind(cmd, "p_date", d.DrillDate.Date);
            Bind(cmd, "p_asol", alt?.SolId ?? Trim(d.AltSolId, 20));
            Bind(cmd, "p_abc", alt?.BranchCode ?? Trim(d.AltBranchCode, 20));
            Bind(cmd, "p_abn", alt?.BranchName ?? Trim(d.AltBranchName, 200));
            Bind(cmd, "p_areg", alt?.RegionName ?? Trim(d.AltRegion, 200));
            Bind(cmd, "p_azone", alt?.ZoneName ?? Trim(d.AltZone, 200));
            Bind(cmd, "p_acat", alt?.Category ?? Trim(d.AltCategory, 50));
            Bind(cmd, "p_sa", d.StaffAlt);
            Bind(cmd, "p_rem", Trim(d.Remarks, 4000));
            Bind(cmd, "p_comp", d.IsCompliant ? 1 : 0);
            Bind(cmd, "p_fy", fy);
            Bind(cmd, "p_by", scope.EmplId);
            Bind(cmd, "p_id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Metrics: replace wholesale.
        await using (var del = new OracleCommand("DELETE FROM RP_BCP_DRILL_METRIC WHERE DRILL_ID = :p_id", cn) { BindByName = true, Transaction = tx })
        {
            Bind(del, "p_id", id);
            await del.ExecuteNonQueryAsync();
        }
        foreach (var def in BcpMetrics.All)
        {
            var m = d.Metrics.FirstOrDefault(x => x.Code == def.Code);
            if (m is null || (m.NormalCount is null && m.NormalAmount is null && m.AltCount is null && m.AltAmount is null)) continue;
            await using var ins = new OracleCommand(
                @"INSERT INTO RP_BCP_DRILL_METRIC (DRILL_ID, METRIC_CODE, NORMAL_COUNT, NORMAL_AMOUNT, ALT_COUNT, ALT_AMOUNT)
                  VALUES (:p_id, :p_code, :p_nc, :p_na, :p_ac, :p_aa)", cn) { BindByName = true, Transaction = tx };
            Bind(ins, "p_id", id);
            Bind(ins, "p_code", def.Code);
            Bind(ins, "p_nc", m.NormalCount);
            Bind(ins, "p_na", def.HasAmount ? m.NormalAmount : null);
            Bind(ins, "p_ac", m.AltCount);
            Bind(ins, "p_aa", def.HasAmount ? m.AltAmount : null);
            await ins.ExecuteNonQueryAsync();
        }

        if (docBytes is { Length: > 0 })
        {
            var safe = new string((docName ?? "document").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
            if (safe.Length == 0) safe = "document" + docExt;
            await using var up = new OracleCommand(
                "UPDATE RP_BCP_DRILL SET DOC_NAME = :p_n, DOC_TYPE = :p_t, DOC_SIZE = :p_s, DOC_BLOB = :p_b WHERE DRILL_ID = :p_id", cn) { BindByName = true, Transaction = tx };
            Bind(up, "p_n", safe);
            Bind(up, "p_t", string.IsNullOrWhiteSpace(docType) ? "application/octet-stream" : docType);
            Bind(up, "p_s", docBytes.Length);
            up.Parameters.Add(new OracleParameter("p_b", OracleDbType.Blob) { Value = docBytes });
            Bind(up, "p_id", id);
            await up.ExecuteNonQueryAsync();
        }

        await AuditAsync(cn, tx, scope, "DRILL", id, isUpdate ? "UPDATE" : "CREATE",
            $"{BcpTypes.Label(d.DrillType)} on {d.DrillDate:dd-MMM-yyyy} at {home.BranchName} ({home.SolId}); operations from {alt?.BranchName ?? d.AltBranchName}"
            + (isUpdate && prevStatus != BcpStatus.Submitted ? $"; resubmitted (was {prevStatus})" : ""));
        await tx.CommitAsync();
        _log.LogInformation("BCP drill {Id} {Action} for {Sol} by {Emp}", id, isUpdate ? "updated" : "created", home.SolId, scope.EmplId);
        return id;
    }

    private async Task<(string? Zone, string? Region, string? Sol, string? Status)?> LoadDrillOwnershipAsync(OracleConnection cn, long id)
    {
        await using var cmd = new OracleCommand("SELECT ZONE_CODE, REGION_CODE, SOL_ID, STATUS FROM RP_BCP_DRILL WHERE DRILL_ID = :p_id AND IS_ACTIVE = 1", cn) { BindByName = true };
        Bind(cmd, "p_id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return (S(r, 0), S(r, 1), S(r, 2), S(r, 3));
    }

    public async Task ReviewDrillAsync(ClaimsPrincipal user, long id, bool acknowledge, string? remarks)
    {
        var scope = GetScope(user);
        if (!scope.CanReview) throw new UnauthorizedAccessException($"{scope.DisplayLevel} users cannot review submissions. Review is done by the Regional Office.");
        if (!acknowledge && string.IsNullOrWhiteSpace(remarks)) throw new InvalidOperationException("Remarks are required when returning a submission.");

        await using var cn = Conn();
        await cn.OpenAsync();
        var own = await LoadDrillOwnershipAsync(cn, id) ?? throw new InvalidOperationException("Submission not found.");
        if (!scope.OwnsOffice(own.Zone, own.Region, own.Sol)) throw new UnauthorizedAccessException("This submission belongs to another region.");
        if (own.Status != BcpStatus.Submitted) throw new InvalidOperationException("Only a submitted (pending review) entry can be reviewed.");

        await using var tx = (OracleTransaction)await cn.BeginTransactionAsync();
        await using (var cmd = new OracleCommand(
            "UPDATE RP_BCP_DRILL SET STATUS = :p_s, REVIEW_BY = :p_by, REVIEW_AT = SYSTIMESTAMP, REVIEW_REMARKS = :p_rem WHERE DRILL_ID = :p_id", cn) { BindByName = true, Transaction = tx })
        {
            Bind(cmd, "p_s", acknowledge ? BcpStatus.Acknowledged : BcpStatus.Returned);
            Bind(cmd, "p_by", scope.EmplId);
            Bind(cmd, "p_rem", Trim(remarks, 2000));
            Bind(cmd, "p_id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        await AuditAsync(cn, tx, scope, "DRILL", id, acknowledge ? "ACKNOWLEDGE" : "RETURN", Trim(remarks, 500));
        await tx.CommitAsync();
    }

    public async Task DeleteDrillAsync(ClaimsPrincipal user, long id)
    {
        var scope = GetScope(user);
        await using var cn = Conn();
        await cn.OpenAsync();
        var own = await LoadDrillOwnershipAsync(cn, id) ?? throw new InvalidOperationException("Submission not found.");
        if (!scope.CanSubmitDrill || !scope.OwnsOffice(own.Zone, own.Region, own.Sol))
            throw new UnauthorizedAccessException("Only the submitting branch (or an App Admin) may withdraw a submission.");
        if (own.Status == BcpStatus.Acknowledged && !scope.IsAdmin)
            throw new InvalidOperationException("An acknowledged submission cannot be withdrawn.");

        await using var tx = (OracleTransaction)await cn.BeginTransactionAsync();
        await using (var cmd = new OracleCommand("UPDATE RP_BCP_DRILL SET IS_ACTIVE = 0, UPDATED_BY = :p_by, UPDATED_AT = SYSTIMESTAMP WHERE DRILL_ID = :p_id", cn) { BindByName = true, Transaction = tx })
        {
            Bind(cmd, "p_by", scope.EmplId);
            Bind(cmd, "p_id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        await AuditAsync(cn, tx, scope, "DRILL", id, "WITHDRAW", $"Submission for {own.Sol} withdrawn");
        await tx.CommitAsync();
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetDrillDocAsync(ClaimsPrincipal user, long id)
    {
        var scope = GetScope(user);
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        cmd.CommandText = "SELECT DOC_BLOB, DOC_TYPE, DOC_NAME FROM RP_BCP_DRILL d WHERE d.DRILL_ID = :p_id AND d.IS_ACTIVE = 1" + ScopePred(scope, cmd, "d");
        Bind(cmd, "p_id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync() || r.IsDBNull(0)) return null;
        using var blob = ((OracleDataReader)r).GetOracleBlob(0);
        var bytes = new byte[blob.Length];
        if (bytes.Length > 0) blob.Read(bytes, 0, bytes.Length);
        return (bytes, S(r, 1) ?? "application/octet-stream", S(r, 2) ?? "document");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Annexure-II certificates
    // ══════════════════════════════════════════════════════════════════════════════════════════

    public async Task<List<BcpRoCert>> ListCertsAsync(ClaimsPrincipal user, string? regionCode = null)
    {
        var scope = GetScope(user);
        var list = new List<BcpRoCert>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = "SELECT c.CERT_ID, c.REGION_CODE, c.REGION_NAME, c.ZONE_CODE, c.ZONE_NAME, c.CERT_DATE, c.SIGNATORY_NAME, c.FIN_YEAR, c.ENTRY_BY, c.ENTRY_AT FROM RP_BCP_RO_CERT c WHERE c.IS_ACTIVE = 1";
        if (IsReal(regionCode)) { sql += " AND c.REGION_CODE = :p_r"; Bind(cmd, "p_r", regionCode); }
        sql += CertScopePred(scope, cmd, "c") + " ORDER BY c.CERT_DATE DESC, c.CERT_ID DESC";
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new BcpRoCert
            {
                Id = L(r, 0), RegionCode = S(r, 1)!, RegionName = S(r, 2), ZoneCode = S(r, 3)!, ZoneName = S(r, 4),
                CertDate = Dt(r, 5) ?? DateTime.Today, SignatoryName = S(r, 6), FinYear = S(r, 7), EntryBy = S(r, 8), EntryAt = Dt(r, 9)
            });
        return list;
    }

    public async Task<BcpRoCert?> GetCertAsync(ClaimsPrincipal user, long id)
    {
        var scope = GetScope(user);
        BcpRoCert? c;
        await using var cn = Conn();
        await cn.OpenAsync();
        await using (var cmd = new OracleCommand { Connection = cn, BindByName = true })
        {
            cmd.CommandText = "SELECT c.CERT_ID, c.REGION_CODE, c.REGION_NAME, c.ZONE_CODE, c.ZONE_NAME, c.CERT_DATE, c.SIGNATORY_NAME, c.FIN_YEAR, c.ENTRY_BY, c.ENTRY_AT FROM RP_BCP_RO_CERT c WHERE c.CERT_ID = :p_id AND c.IS_ACTIVE = 1"
                              + CertScopePred(scope, cmd, "c");
            Bind(cmd, "p_id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            c = !await r.ReadAsync() ? null : new BcpRoCert
            {
                Id = L(r, 0), RegionCode = S(r, 1)!, RegionName = S(r, 2), ZoneCode = S(r, 3)!, ZoneName = S(r, 4),
                CertDate = Dt(r, 5) ?? DateTime.Today, SignatoryName = S(r, 6), FinYear = S(r, 7), EntryBy = S(r, 8), EntryAt = Dt(r, 9)
            };
        }
        if (c is null) return null;

        await using var lc = new OracleCommand(
            @"SELECT SR_NO, SOL_ID, BRANCH_NAME, DISASTER_NAME, DRILL_DATE, MAP1_SOL_ID, MAP1_NAME, MAP2_SOL_ID, MAP2_NAME, REMARKS
                FROM RP_BCP_RO_CERT_LINE WHERE CERT_ID = :p_id ORDER BY SR_NO", cn) { BindByName = true };
        Bind(lc, "p_id", id);
        await using var lr = await lc.ExecuteReaderAsync();
        while (await lr.ReadAsync())
            c.Lines.Add(new BcpCertLine
            {
                SrNo = I(lr, 0) ?? 0, SolId = S(lr, 1)!, BranchName = S(lr, 2), DisasterName = S(lr, 3), DrillDate = Dt(lr, 4),
                Map1SolId = S(lr, 5), Map1Name = S(lr, 6), Map2SolId = S(lr, 7), Map2Name = S(lr, 8), Remarks = S(lr, 9)
            });
        return c;
    }

    /// <summary>Pre-fills an Annexure-II draft from the region's branch map and each branch's latest BCP drill.</summary>
    public async Task<BcpRoCert> BuildCertDraftAsync(ClaimsPrincipal user, string regionCode)
    {
        var scope = GetScope(user);
        if (!scope.CanCertifyRegion) throw new UnauthorizedAccessException("Only the Regional Office (or an App Admin) records the Annexure-II certificate.");
        var branches = await ListBranchMapAsync(user, regionCode);
        if (branches.Count == 0) throw new InvalidOperationException("No branches are mapped to this region.");
        var first = branches[0];
        if (!scope.OwnsOffice(first.ZoneCode, first.RegionCode, null)) throw new UnauthorizedAccessException("This region is not yours.");
        var byName = branches.ToDictionary(b => b.SolId, b => b.BranchName, StringComparer.OrdinalIgnoreCase);

        var latest = new Dictionary<string, (DateTime Date, string? Alt, string? AltName)>(StringComparer.OrdinalIgnoreCase);
        await using (var cn = Conn())
        {
            await cn.OpenAsync();
            await using var cmd = new OracleCommand(@"
                SELECT SOL_ID, DRILL_DATE, ALT_SOL_ID, ALT_BRANCH_NAME FROM (
                    SELECT SOL_ID, DRILL_DATE, ALT_SOL_ID, ALT_BRANCH_NAME,
                           ROW_NUMBER() OVER (PARTITION BY SOL_ID ORDER BY DRILL_DATE DESC, DRILL_ID DESC) rn
                      FROM RP_BCP_DRILL WHERE IS_ACTIVE = 1 AND DRILL_TYPE = 'BCP' AND REGION_CODE = :p_r) WHERE rn = 1", cn) { BindByName = true };
            Bind(cmd, "p_r", regionCode);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) latest[S(r, 0)!] = (Dt(r, 1) ?? DateTime.Today, S(r, 2), S(r, 3));
        }

        var cert = new BcpRoCert
        {
            RegionCode = first.RegionCode, RegionName = first.RegionName, ZoneCode = first.ZoneCode, ZoneName = first.ZoneName,
            CertDate = DateTime.Today, FinYear = BcpPeriod.FinYear(DateTime.Today)
        };
        var sr = 1;
        foreach (var b in branches)
        {
            latest.TryGetValue(b.SolId, out var l);
            var m1 = l.Alt ?? b.Link1SolId;
            var m1Name = l.AltName ?? (m1 is not null && byName.TryGetValue(m1, out var n1) ? n1 : null);
            var m2 = b.Link2SolId;
            cert.Lines.Add(new BcpCertLine
            {
                SrNo = sr++, SolId = b.SolId, BranchName = b.BranchName,
                DrillDate = l.Alt is null && l.Date == default ? null : l.Date,
                Map1SolId = m1, Map1Name = m1Name,
                Map2SolId = m2, Map2Name = m2 is not null && byName.TryGetValue(m2, out var n2) ? n2 : null
            });
        }
        return cert;
    }

    public async Task<long> SaveCertAsync(ClaimsPrincipal user, BcpRoCert c)
    {
        var scope = GetScope(user);
        if (!scope.CanCertifyRegion) throw new UnauthorizedAccessException("Only the Regional Office (or an App Admin) records the Annexure-II certificate.");
        if (c.Lines.Count == 0) throw new InvalidOperationException("The certificate has no branches.");
        if (c.CertDate.Date > DateTime.Today) throw new InvalidOperationException("Certificate date cannot be in the future.");
        if (string.IsNullOrWhiteSpace(c.SignatoryName)) throw new InvalidOperationException("Name of the Regional Head / Regional Assurance Head is required.");

        await using var cn = Conn();
        await cn.OpenAsync();
        await using var tx = (OracleTransaction)await cn.BeginTransactionAsync();

        // Region + every line's branch are re-resolved from the map; the client's names are ignored.
        var mapped = new Dictionary<string, BcpBranch>(StringComparer.OrdinalIgnoreCase);
        await using (var mc = new OracleCommand($"SELECT {BranchCols} FROM RP_BCP_BRANCH_MAP m WHERE m.REGION_CODE = :p_r AND m.IS_ACTIVE = 1", cn) { BindByName = true, Transaction = tx })
        {
            Bind(mc, "p_r", c.RegionCode);
            await using var mr = await mc.ExecuteReaderAsync();
            while (await mr.ReadAsync()) { var b = ReadBranch(mr); mapped[b.SolId] = b; }
        }
        if (mapped.Count == 0) throw new InvalidOperationException("No branches are mapped to this region.");
        var any = mapped.Values.First();
        if (!scope.OwnsOffice(any.ZoneCode, any.RegionCode, null)) throw new UnauthorizedAccessException("You may only certify your own region.");

        foreach (var l in c.Lines)
        {
            if (!mapped.ContainsKey(l.SolId)) throw new InvalidOperationException($"Branch {l.SolId} does not belong to this region.");
            if (string.IsNullOrWhiteSpace(l.Map1SolId) && string.IsNullOrWhiteSpace(l.Map1Name))
                throw new InvalidOperationException($"{mapped[l.SolId].BranchName}: Mapped/Linked Branch 1 is required.");
            if (l.Map1SolId is not null && !mapped.ContainsKey(l.Map1SolId.Trim()) && string.IsNullOrWhiteSpace(l.Map1Name))
                throw new InvalidOperationException($"{mapped[l.SolId].BranchName}: linked branch {l.Map1SolId} is unknown.");
        }

        long id;
        await using (var cmd = new OracleCommand(@"
            INSERT INTO RP_BCP_RO_CERT (REGION_CODE, REGION_NAME, ZONE_CODE, ZONE_NAME, CERT_DATE, SIGNATORY_NAME, FIN_YEAR, ENTRY_BY)
            VALUES (:p_rc, :p_rn, :p_zc, :p_zn, :p_date, :p_sig, :p_fy, :p_by) RETURNING CERT_ID INTO :p_id", cn) { BindByName = true, Transaction = tx })
        {
            Bind(cmd, "p_rc", any.RegionCode);
            Bind(cmd, "p_rn", any.RegionName);
            Bind(cmd, "p_zc", any.ZoneCode);
            Bind(cmd, "p_zn", any.ZoneName);
            Bind(cmd, "p_date", c.CertDate.Date);
            Bind(cmd, "p_sig", Trim(c.SignatoryName, 200));
            Bind(cmd, "p_fy", BcpPeriod.FinYear(c.CertDate));
            Bind(cmd, "p_by", scope.EmplId);
            var outId = new OracleParameter("p_id", OracleDbType.Int64) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outId);
            await cmd.ExecuteNonQueryAsync();
            id = Convert.ToInt64(((OracleDecimal)outId.Value!).Value);
        }

        var sr = 1;
        foreach (var l in c.Lines)
        {
            var home = mapped[l.SolId];
            var m1 = string.IsNullOrWhiteSpace(l.Map1SolId) ? null : l.Map1SolId.Trim();
            var m2 = string.IsNullOrWhiteSpace(l.Map2SolId) ? null : l.Map2SolId.Trim();
            var m1Name = m1 is not null && mapped.TryGetValue(m1, out var b1) ? b1.BranchName : Trim(l.Map1Name, 200);
            var m2Name = m2 is not null && mapped.TryGetValue(m2, out var b2) ? b2.BranchName : Trim(l.Map2Name, 200);

            await using var ins = new OracleCommand(@"
                INSERT INTO RP_BCP_RO_CERT_LINE (CERT_ID, SR_NO, SOL_ID, BRANCH_NAME, DISASTER_NAME, DRILL_DATE, MAP1_SOL_ID, MAP1_NAME, MAP2_SOL_ID, MAP2_NAME, REMARKS)
                VALUES (:p_c, :p_sr, :p_sol, :p_bn, :p_dis, :p_dd, :p_m1, :p_m1n, :p_m2, :p_m2n, :p_rem)", cn) { BindByName = true, Transaction = tx };
            Bind(ins, "p_c", id);
            Bind(ins, "p_sr", sr++);
            Bind(ins, "p_sol", home.SolId);
            Bind(ins, "p_bn", home.BranchName);
            Bind(ins, "p_dis", Trim(l.DisasterName, 200));
            Bind(ins, "p_dd", l.DrillDate?.Date);
            Bind(ins, "p_m1", m1);
            Bind(ins, "p_m1n", m1Name);
            Bind(ins, "p_m2", m2);
            Bind(ins, "p_m2n", m2Name);
            Bind(ins, "p_rem", Trim(l.Remarks, 1000));
            await ins.ExecuteNonQueryAsync();

            // The certified linking becomes the standing mapping ("updating and maintaining the mapping").
            if (m1 is not null && mapped.ContainsKey(m1))
            {
                await using var up = new OracleCommand(
                    "UPDATE RP_BCP_BRANCH_MAP SET LINK1_SOL_ID = :p_m1, LINK2_SOL_ID = :p_m2, UPDATED_BY = :p_by, UPDATED_AT = SYSTIMESTAMP WHERE SOL_ID = :p_sol", cn) { BindByName = true, Transaction = tx };
                Bind(up, "p_m1", m1);
                Bind(up, "p_m2", m2 is not null && mapped.ContainsKey(m2) ? m2 : null);
                Bind(up, "p_by", scope.EmplId);
                Bind(up, "p_sol", home.SolId);
                await up.ExecuteNonQueryAsync();
            }
        }

        await AuditAsync(cn, tx, scope, "CERT", id, "CREATE", $"Annexure-II for {any.RegionName ?? any.RegionCode}: {c.Lines.Count} branch(es), signed by {c.SignatoryName}");
        await tx.CommitAsync();
        return id;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // MIS
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Branch universe (the map) LEFT JOINed to each branch's latest active drill inside the filter window.</summary>
    private static string MisFrom(BcpScope scope, BcpDrillFilter f, OracleCommand cmd)
    {
        var inner = "x.SOL_ID = m.SOL_ID AND x.IS_ACTIVE = 1";
        if (BcpTypes.IsValid(f.DrillType)) { inner += " AND x.DRILL_TYPE = :f_t"; Bind(cmd, "f_t", f.DrillType); }
        if (f.From.HasValue) { inner += " AND x.DRILL_DATE >= :f_from"; Bind(cmd, "f_from", f.From.Value.Date); }
        if (f.To.HasValue) { inner += " AND x.DRILL_DATE <= :f_to"; Bind(cmd, "f_to", f.To.Value.Date); }
        var sql = $@" FROM RP_BCP_BRANCH_MAP m
                      LEFT JOIN RP_BCP_DRILL d ON d.DRILL_ID = (SELECT MAX(x.DRILL_ID) FROM RP_BCP_DRILL x WHERE {inner})
                     WHERE m.IS_ACTIVE = 1";
        sql += ScopePred(scope, cmd, "m");
        sql += FilterSql(f, cmd, "m");
        return sql;
    }

    public async Task<List<BcpBranchMisRow>> GetBranchMisAsync(ClaimsPrincipal user, BcpDrillFilter f)
    {
        var scope = GetScope(user);
        var list = new List<BcpBranchMisRow>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = @"SELECT m.SOL_ID, m.BRANCH_CODE, m.BRANCH_NAME, m.REGION_CODE, m.REGION_NAME, m.ZONE_CODE, m.ZONE_NAME,
                           d.DRILL_TYPE, d.DRILL_DATE, d.ALT_BRANCH_NAME, d.ALT_SOL_ID, d.STATUS"
                  + MisFrom(scope, f, cmd);
        if (string.Equals(f.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase)) sql += " AND d.DRILL_ID IS NOT NULL";
        if (string.Equals(f.Status, "NOT", StringComparison.OrdinalIgnoreCase)) sql += " AND d.DRILL_ID IS NULL";
        sql += " ORDER BY m.ZONE_NAME, m.REGION_NAME, m.BRANCH_NAME";
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new BcpBranchMisRow
            {
                SolId = S(r, 0)!, BranchCode = S(r, 1), BranchName = S(r, 2)!, RegionCode = S(r, 3), RegionName = S(r, 4),
                ZoneCode = S(r, 5), ZoneName = S(r, 6), DrillType = S(r, 7), DrillDate = Dt(r, 8),
                AltBranchName = S(r, 9), AltSolId = S(r, 10), DrillStatus = S(r, 11)
            });
        return list;
    }

    public async Task<List<BcpZoneMisRow>> GetRegionMisAsync(ClaimsPrincipal user, BcpDrillFilter f)
    {
        var scope = GetScope(user);
        var rows = new List<BcpRegionMisRow>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var certWin = "";
        if (f.From.HasValue) certWin += " AND c.CERT_DATE >= :f_from";
        if (f.To.HasValue) certWin += " AND c.CERT_DATE <= :f_to";
        var sql = $@"SELECT m.ZONE_CODE, MAX(m.ZONE_NAME), m.REGION_CODE, MAX(m.REGION_NAME),
                            COUNT(*) AS MAPPED, COUNT(d.DRILL_ID) AS SUBMITTED,
                            SUM(CASE WHEN d.STATUS = 'ACKNOWLEDGED' THEN 1 ELSE 0 END) AS ACK,
                            (SELECT COUNT(*) FROM RP_BCP_RO_CERT c WHERE c.REGION_CODE = m.REGION_CODE AND c.IS_ACTIVE = 1{certWin}) AS CERTS"
                  + MisFrom(scope, f, cmd)
                  + " GROUP BY m.ZONE_CODE, m.REGION_CODE ORDER BY MAX(m.ZONE_NAME), MAX(m.REGION_NAME)";
        cmd.CommandText = sql;
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
                rows.Add(new BcpRegionMisRow
                {
                    ZoneCode = S(r, 0)!, ZoneName = S(r, 1), RegionCode = S(r, 2)!, RegionName = S(r, 3),
                    MappedBranches = I(r, 4) ?? 0, Submitted = I(r, 5) ?? 0, Acknowledged = I(r, 6) ?? 0, RoCertificates = I(r, 7) ?? 0
                });
        }
        return rows.GroupBy(x => x.ZoneCode)
                   .Select(g => new BcpZoneMisRow { ZoneCode = g.Key, ZoneName = g.First().ZoneName, Regions = g.OrderBy(x => x.RegionName ?? x.RegionCode).ToList() })
                   .OrderBy(z => z.ZoneName ?? z.ZoneCode).ToList();
    }

    public async Task<List<BcpPeriodRow>> GetPeriodMisAsync(ClaimsPrincipal user, BcpDrillFilter f)
    {
        var scope = GetScope(user);
        var list = new List<BcpPeriodRow>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand { Connection = cn, BindByName = true };
        var sql = @"SELECT d.FIN_YEAR,
                           CASE WHEN TO_NUMBER(TO_CHAR(d.DRILL_DATE,'MM')) BETWEEN 4 AND 6 THEN 'Q1'
                                WHEN TO_NUMBER(TO_CHAR(d.DRILL_DATE,'MM')) BETWEEN 7 AND 9 THEN 'Q2'
                                WHEN TO_NUMBER(TO_CHAR(d.DRILL_DATE,'MM')) BETWEEN 10 AND 12 THEN 'Q3' ELSE 'Q4' END AS QTR,
                           d.DRILL_TYPE, COUNT(*), COUNT(DISTINCT d.SOL_ID)
                      FROM RP_BCP_DRILL d WHERE d.IS_ACTIVE = 1";
        sql += ScopePred(scope, cmd, "d") + FilterSql(f, cmd, "d");
        if (BcpTypes.IsValid(f.DrillType)) { sql += " AND d.DRILL_TYPE = :f_t"; Bind(cmd, "f_t", f.DrillType); }
        if (f.From.HasValue) { sql += " AND d.DRILL_DATE >= :f_from"; Bind(cmd, "f_from", f.From.Value.Date); }
        if (f.To.HasValue) { sql += " AND d.DRILL_DATE <= :f_to"; Bind(cmd, "f_to", f.To.Value.Date); }
        sql += " GROUP BY d.FIN_YEAR, CASE WHEN TO_NUMBER(TO_CHAR(d.DRILL_DATE,'MM')) BETWEEN 4 AND 6 THEN 'Q1' WHEN TO_NUMBER(TO_CHAR(d.DRILL_DATE,'MM')) BETWEEN 7 AND 9 THEN 'Q2' WHEN TO_NUMBER(TO_CHAR(d.DRILL_DATE,'MM')) BETWEEN 10 AND 12 THEN 'Q3' ELSE 'Q4' END, d.DRILL_TYPE ORDER BY 1 DESC, 2, 3";
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new BcpPeriodRow { FinYear = S(r, 0) ?? "", Quarter = S(r, 1) ?? "", DrillType = S(r, 2) ?? "", Drills = I(r, 3) ?? 0, Branches = I(r, 4) ?? 0 });
        return list;
    }
}
