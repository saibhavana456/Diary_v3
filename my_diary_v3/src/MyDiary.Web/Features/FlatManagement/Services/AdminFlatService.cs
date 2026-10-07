using System.Text;
using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.FlatManagement.Services;

public class AdminFlatService : IAdminFlatService
{
    private readonly string _connString;
    private readonly ILogger<AdminFlatService> _logger;

    public AdminFlatService(IConfiguration config, ILogger<AdminFlatService> logger)
    {
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<List<AdminFlatRow>> GetAllFlatsAsync(string adminPfNo, string? search)
    {
        var result = new List<AdminFlatRow>();
        var pfNo = (adminPfNo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pfNo)) return result;

        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();

            var (scopeType, scopeCode) = await LoadScopeAsync(cn, pfNo);

            var sql = new StringBuilder(@"
                SELECT
                    A.EMPLID, A.UBI_CENTER, A.ROOM_NBR, A.UBI_REGION_OFC,
                    P.EFFDT1, P.UBI_ROOM_TYPE
                FROM FLATS.PS_UBI_RQ_APP_PROC A
                JOIN FLATS.PS_UBI_RES_QTR_PAR P
                  ON A.BUILDING = P.BUILDING
                 AND A.ROOM_NBR = P.ROOM_NBR
                 AND A.UBI_CENTER = P.UBI_CENTER
                WHERE A.EMPLID IS NOT NULL");

            var q = (search ?? string.Empty).Trim();
            var hasSearch = !string.IsNullOrEmpty(q) && !string.Equals(q, "All", StringComparison.OrdinalIgnoreCase);
            if (hasSearch)
            {
                sql.Append(@"
                  AND (
                        UPPER(A.UBI_CENTER)   LIKE UPPER(:Q)
                     OR UPPER(A.ADDRESSLONG1) LIKE UPPER(:Q)
                     OR UPPER(A.ROOM_NBR)     LIKE UPPER(:Q)
                  )");
            }

            if (scopeType == "RO" && !string.IsNullOrEmpty(scopeCode))
            {
                sql.Append(" AND A.UBI_REGION_OFC = :RO");
            }
            else if (scopeType == "ZO" && !string.IsNullOrEmpty(scopeCode))
            {
                sql.Append(@"
                  AND (
                        A.UBI_REGION_OFC = :ZO
                     OR A.UBI_REGION_OFC IN (
                            SELECT RM.REGION_CODE FROM FLATS.REGION_MASTER RM WHERE RM.ZONE_CODE = :ZO
                        )
                  )");
            }

            sql.Append(" ORDER BY A.UBI_CENTER");

            await using var cmd = new OracleCommand(sql.ToString(), cn) { BindByName = true };
            if (hasSearch)
                cmd.Parameters.Add("Q", OracleDbType.Varchar2).Value = "%" + q + "%";
            if (scopeType == "RO" && !string.IsNullOrEmpty(scopeCode))
                cmd.Parameters.Add("RO", OracleDbType.Varchar2).Value = scopeCode;
            else if (scopeType == "ZO" && !string.IsNullOrEmpty(scopeCode))
                cmd.Parameters.Add("ZO", OracleDbType.Varchar2).Value = scopeCode;

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new AdminFlatRow(
                    EmplId: r.IsDBNull(0) ? null : r.GetString(0),
                    UbiCenter: r.IsDBNull(1) ? null : r.GetString(1),
                    RoomNbr: r.IsDBNull(2) ? null : r.GetString(2),
                    UbiRegionOfc: r.IsDBNull(3) ? null : r.GetString(3),
                    PurchasedDate: SafeGetDateTime(r, 4),
                    RoomType: r.IsDBNull(5) ? null : r.GetString(5)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAllFlatsAsync failed for admin {PfNo}", pfNo);
        }

        return result;
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
