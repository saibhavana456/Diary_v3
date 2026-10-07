using System.Text;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminGrievanceService : IAdminGrievanceService
{
    private static readonly TimeSpan DropdownCacheDuration = TimeSpan.FromMinutes(15);
    private const string LocationsCacheKey = "AdminGrievanceService:Locations";

    private readonly string _connString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdminGrievanceService> _logger;

    public AdminGrievanceService(IConfiguration config, IMemoryCache cache, ILogger<AdminGrievanceService> logger)
    {
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<string>> GetLocationsAsync()
    {
        if (_cache.TryGetValue(LocationsCacheKey, out List<string>? cached) && cached is not null)
            return cached;

        const string sql = @"
            SELECT DISTINCT NVL(TRIM(A.UBI_CENTER), '(Unknown)') AS LOCATION
            FROM FLATS.PS_UBI_RQ_APP_PROC A
            WHERE EXISTS (
                SELECT 1 FROM FLATS.PS_UBI_RES_QTR_PAR P
                WHERE P.BUILDING = A.BUILDING AND P.ROOM_NBR = A.ROOM_NBR AND P.UBI_CENTER = A.UBI_CENTER
            )
            ORDER BY LOCATION";

        var result = new List<string>();
        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn);
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(0)) result.Add(r.GetString(0));
            }
            _cache.Set(LocationsCacheKey, result, DropdownCacheDuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLocationsAsync failed");
        }
        return result;
    }

    public async Task<List<string>> GetFlatTypesAsync(string? location)
    {
        var loc = (location ?? string.Empty).Trim();
        var cacheKey = $"AdminGrievanceService:FlatTypes:{loc.ToUpperInvariant()}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;

        var sql = @"
            SELECT DISTINCT NVL(TRIM(P.UBI_ROOM_TYPE), '(Unknown)') AS FLATTYPE
            FROM FLATS.PS_UBI_RQ_APP_PROC A
            JOIN FLATS.PS_UBI_RES_QTR_PAR P
              ON P.BUILDING = A.BUILDING AND P.ROOM_NBR = A.ROOM_NBR AND P.UBI_CENTER = A.UBI_CENTER
            WHERE 1 = 1";

        if (!string.IsNullOrEmpty(loc))
            sql += " AND A.UBI_CENTER = :LOC";
        sql += " ORDER BY FLATTYPE";

        var result = new List<string>();
        try
        {
            await using var cn = new OracleConnection(_connString);
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            if (!string.IsNullOrEmpty(loc))
                cmd.Parameters.Add("LOC", OracleDbType.Varchar2).Value = loc;
            await cn.OpenAsync();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(0)) result.Add(r.GetString(0));
            }
            _cache.Set(cacheKey, result, DropdownCacheDuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFlatTypesAsync failed for location {Location}", loc);
        }
        return result;
    }

    public async Task<AdminGrievanceListResult> GetGrievancesAsync(
        string adminPfNo, string? location, string? flatType, string? empPf, string? flatNo, int pageIndex, int pageSize)
    {
        var result = new List<AdminGrievanceRow>();
        var pfNo = (adminPfNo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pfNo)) return new AdminGrievanceListResult(result, 0);

        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();

            var (scopeType, scopeCode) = await LoadScopeAsync(cn, pfNo);

            var where = new StringBuilder(@"
                FROM FLATS.FLAT_GRIEVANCE g
                JOIN FLATS.PS_UBI_RQ_APP_PROC a ON g.CREATED_BY = a.EMPLID
                JOIN FLATS.PS_UBI_RES_QTR_PAR p
                  ON p.BUILDING = a.BUILDING AND p.ROOM_NBR = a.ROOM_NBR AND p.UBI_CENTER = a.UBI_CENTER
                WHERE 1 = 1");

            var loc = (location ?? string.Empty).Trim();
            var type = (flatType ?? string.Empty).Trim();
            var pf = (empPf ?? string.Empty).Trim();
            var flat = (flatNo ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(loc)) where.Append(" AND a.UBI_CENTER = :LOC");
            if (!string.IsNullOrEmpty(type)) where.Append(" AND p.UBI_ROOM_TYPE = :FLATTYPE");
            if (!string.IsNullOrEmpty(pf)) where.Append(" AND UPPER(a.EMPLID) LIKE UPPER(:EMPPF)");
            if (!string.IsNullOrEmpty(flat)) where.Append(" AND UPPER(g.FLAT_NO) LIKE UPPER(:FLATNO)");
            if (scopeType == "RO" && !string.IsNullOrEmpty(scopeCode)) where.Append(" AND a.UBI_REGION_OFC = :RO");
            else if (scopeType == "ZO" && !string.IsNullOrEmpty(scopeCode))
                where.Append(@" AND (a.UBI_REGION_OFC = :ZO OR a.UBI_REGION_OFC IN (
                            SELECT RM.REGION_CODE FROM FLATS.REGION_MASTER RM WHERE RM.ZONE_CODE = :ZO))");

            void BindFilters(OracleCommand c)
            {
                if (!string.IsNullOrEmpty(loc)) c.Parameters.Add("LOC", OracleDbType.Varchar2).Value = loc;
                if (!string.IsNullOrEmpty(type)) c.Parameters.Add("FLATTYPE", OracleDbType.Varchar2).Value = type;
                if (!string.IsNullOrEmpty(pf)) c.Parameters.Add("EMPPF", OracleDbType.Varchar2).Value = "%" + pf + "%";
                if (!string.IsNullOrEmpty(flat)) c.Parameters.Add("FLATNO", OracleDbType.Varchar2).Value = "%" + flat + "%";
                if (scopeType == "RO" && !string.IsNullOrEmpty(scopeCode))
                    c.Parameters.Add("RO", OracleDbType.Varchar2).Value = scopeCode;
                else if (scopeType == "ZO" && !string.IsNullOrEmpty(scopeCode))
                    c.Parameters.Add("ZO", OracleDbType.Varchar2).Value = scopeCode;
            }

            int totalCount;
            await using (var countCmd = new OracleCommand("SELECT COUNT(*) " + where, cn) { BindByName = true })
            {
                BindFilters(countCmd);
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var listSql = @"
                SELECT
                    g.REF_NO, g.FLAT_ID, g.FLAT_NO, a.UBI_CENTER, g.CATEGORY, g.STATUS, g.CREATED_ON,
                    g.CREATED_NAME || ' (' || g.CREATED_BY || ')' AS CREATED_DISPLAY "
                + where + @"
                ORDER BY g.REF_NO DESC
                OFFSET :OFFSET_ROWS ROWS FETCH NEXT :PAGE_SIZE ROWS ONLY";

            await using (var listCmd = new OracleCommand(listSql, cn) { BindByName = true })
            {
                BindFilters(listCmd);
                listCmd.Parameters.Add("OFFSET_ROWS", OracleDbType.Int32).Value = Math.Max(0, pageIndex) * pageSize;
                listCmd.Parameters.Add("PAGE_SIZE", OracleDbType.Int32).Value = pageSize;

                await using var r = await listCmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    result.Add(new AdminGrievanceRow(
                        RefNo: r.GetString(0),
                        FlatId: r.IsDBNull(1) ? null : r.GetString(1),
                        FlatNo: r.IsDBNull(2) ? null : r.GetString(2),
                        Location: r.IsDBNull(3) ? null : r.GetString(3),
                        Category: r.IsDBNull(4) ? null : r.GetString(4),
                        Status: r.IsDBNull(5) ? null : r.GetString(5),
                        CreatedOn: SafeGetDateTime(r, 6),
                        CreatedByDisplay: r.IsDBNull(7) ? null : r.GetString(7)));
                }
            }

            return new AdminGrievanceListResult(result, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGrievancesAsync failed for admin {PfNo}", pfNo);
            return new AdminGrievanceListResult(result, 0);
        }
    }

    private async Task<(string ScopeType, string ScopeCode)> LoadScopeAsync(OracleConnection cn, string pfNo)
    {
        const string sql = @"
            SELECT ADMINSCOPETYPE, ADMINSCOPECODE
            FROM FLATS.USER_MASTER
            WHERE UPPER(TRIM(EMP_ID)) = UPPER(TRIM(:PF))
              AND USER_TYPE = 'admin'
              AND STATUS = 'Active'
            ORDER BY TRANS_ID DESC
            FETCH FIRST 1 ROWS ONLY";

        try
        {
            await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
            cmd.Parameters.Add("PF", OracleDbType.Varchar2).Value = pfNo;
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ("", "");

            var scopeType = r.IsDBNull(0) ? "" : r.GetString(0).Trim().ToUpperInvariant();
            var scopeCode = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
            return (scopeType, scopeCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadScopeAsync failed for admin {PfNo}; treating as unscoped", pfNo);
            return ("", "");
        }
    }

    private static DateTime? SafeGetDateTime(OracleDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        try
        {
            return r.GetDateTime(ordinal);
        }
        catch
        {
            try
            {
                var raw = r.GetValue(ordinal);
                if (raw is DateTime dt) return dt;
                if (raw is not null && DateTime.TryParse(raw.ToString(), out var parsed)) return parsed;
            }
            catch
            {
            }
            return null;
        }
    }
}
