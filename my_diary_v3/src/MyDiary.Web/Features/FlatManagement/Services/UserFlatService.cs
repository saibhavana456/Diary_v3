using MyDiary.Web.Features.FlatManagement.Models.DTO;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.FlatManagement.Services;

// Ports Pages_User/ViewAllocatedFlat.aspx.cs. Same connection/matching approach as
// UserAuthService.LookupUserAsync: a single ConStr connection reaches both the FLATS and
// ORGANISATION schemas, and PS_-prefixed PeopleSoft-derived columns get UPPER(TRIM(...))
// comparisons since they've been found to be CHAR-padded / inconsistently cased.
public class UserFlatService : IUserFlatService
{
    private readonly string _connString;
    private readonly ILogger<UserFlatService> _logger;

    public UserFlatService(IConfiguration config, ILogger<UserFlatService> logger)
    {
        _connString = config.GetConnectionString("ConStr")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ConStr in appsettings.json");
        _logger = logger;
    }

    public async Task<ViewAllocatedFlatResult> GetAllocatedFlatAsync(string pfNo)
    {
        var username = (pfNo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(username))
            return new ViewAllocatedFlatResult(false, null, null, "PF No. is required.");

        try
        {
            await using var cn = new OracleConnection(_connString);
            await cn.OpenAsync();

            var (building, roomNbr) = await ResolveFlatFromPfAsync(cn, username);
            if (string.IsNullOrWhiteSpace(building) && string.IsNullOrWhiteSpace(roomNbr))
                return new ViewAllocatedFlatResult(false, null, null, "You are not allotted to any flat.");

            var flat = await GetFlatDetailsAsync(cn, username);
            var occupant = await GetCurrentOccupantAsync(cn, username);

            return new ViewAllocatedFlatResult(true, flat, occupant, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAllocatedFlatAsync failed for PF_NO={PfNo}", username);
            return new ViewAllocatedFlatResult(false, null, null, "Unable to load flat details right now.");
        }
    }

    private static async Task<(string? Building, string? RoomNbr)> ResolveFlatFromPfAsync(OracleConnection cn, string pfNo)
    {
        const string sql = @"
            SELECT BUILDING, ROOM_NBR
            FROM FLATS.PS_UBI_RQ_APP_PROC
            WHERE UPPER(TRIM(EMPLID)) = UPPER(TRIM(:U))
            ORDER BY BEGIN_DT DESC
            FETCH FIRST 1 ROWS ONLY";

        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("U", OracleDbType.Varchar2).Value = pfNo;
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            return (
                r.IsDBNull(0) ? null : r.GetString(0),
                r.IsDBNull(1) ? null : r.GetString(1));
        }
        return (null, null);
    }

    private static async Task<AllocatedFlatDetails?> GetFlatDetailsAsync(OracleConnection cn, string pfNo)
    {
        const string sql = @"
            SELECT FLAT_NO, FLAT_TYPE, CARPET_AREA, BUILDING_NAME, ADDRESS
            FROM VW_FLAT_DETAILS
            WHERE UPPER(TRIM(PF_NO)) = UPPER(TRIM(:U))
            ORDER BY OCCUPIED_FROM DESC
            FETCH FIRST 1 ROWS ONLY";

        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("U", OracleDbType.Varchar2).Value = pfNo;
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new AllocatedFlatDetails(
            FlatNo: r.IsDBNull(0) ? null : r.GetString(0),
            FlatType: r.IsDBNull(1) ? null : r.GetString(1),
            CarpetArea: r.IsDBNull(2) ? null : r.GetString(2),
            BuildingName: r.IsDBNull(3) ? null : r.GetString(3),
            Address: r.IsDBNull(4) ? null : r.GetString(4));
    }

    private static async Task<CurrentOccupantDetails?> GetCurrentOccupantAsync(OracleConnection cn, string pfNo)
    {
        const string sql = @"
            SELECT
                PURAP.EMPLID,
                SD.NAME,
                SD.JOB_DESC,
                SD.CONTACT_NO,
                SD.EMAIL_ID,
                PURAP.BEGIN_DT,
                SD.DEPT_JOIN_DT,
                SD.EXPECTED_RETIREMENT_DATE,
                SD.LOCATION_DESC,
                SD.REGION_NAME,
                SD.ZONE_NAME
            FROM FLATS.PS_UBI_RQ_APP_PROC PURAP
            JOIN ORGANISATION.STAFF_DETAILS SD ON UPPER(TRIM(SD.PF_NO)) = UPPER(TRIM(PURAP.EMPLID))
            WHERE UPPER(TRIM(PURAP.EMPLID)) = UPPER(TRIM(:U))
            FETCH FIRST 1 ROWS ONLY";

        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        cmd.Parameters.Add("U", OracleDbType.Varchar2).Value = pfNo;
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new CurrentOccupantDetails(
            EmplId: r.IsDBNull(0) ? null : r.GetString(0),
            Name: r.IsDBNull(1) ? null : r.GetString(1),
            JobDesc: r.IsDBNull(2) ? null : r.GetString(2),
            ContactNo: r.IsDBNull(3) ? null : r.GetString(3),
            EmailId: r.IsDBNull(4) ? null : r.GetString(4),
            BeginDt: r.IsDBNull(5) ? null : r.GetDateTime(5),
            DeptJoinDt: r.IsDBNull(6) ? null : r.GetDateTime(6),
            ExpectedRetirementDate: r.IsDBNull(7) ? null : r.GetDateTime(7),
            LocationDesc: r.IsDBNull(8) ? null : r.GetString(8),
            RegionName: r.IsDBNull(9) ? null : r.GetString(9),
            ZoneName: r.IsDBNull(10) ? null : r.GetString(10));
    }
}
