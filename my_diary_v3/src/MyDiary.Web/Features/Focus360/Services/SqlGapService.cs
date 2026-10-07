using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Core.Services;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Features.Focus360.Models;

namespace MyDiary.Web.Features.Focus360.Services;

/// <summary>
/// SQL Server implementation of IGapService.
/// Reads from:
///   dbo.BUSINESS_360_DATA              — branch KPI data (all numeric parameters)
///   dbo.BUSINESS_360_PARAMETER_MASTER  — parameter definitions (extended with F360_SORT_ORDER, F360_IS_ACTIVE)
///   dbo.F360_CATEGORY_COLOR            — colour coding by category / sub-category
///
/// Uses parameterised SQL against ConnectionStrings:SQLServerConnection.
/// </summary>
public sealed class SqlGapService : IGapService
{
    private readonly string _connectionString;

    public SqlGapService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("SQLServerConnection")!;
    }

    // ── All KPI data rows ──────────────────────────────────────────────────────
    public async Task<List<GapPerformanceRow>> GetAllDataAsync(string branchId, DateTime? asOnDate = null)
    {
        AppLogger.LogInfo($"SqlGapService: Fetching all KPI data for branch {branchId}");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("SP_FOCUS360_REPORT", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        cmd.Parameters.Add(
            new SqlParameter("@BranchId", SqlDbType.VarChar, 10)
            {
                Value = branchId
            });

        await using var rdr = await cmd.ExecuteReaderAsync();

        return MapRows(rdr);
    }

    // ── Category colour coding ─────────────────────────────────────────────────
    public async Task<List<F360CategoryColor>> GetCategoryColorsAsync()
    {

        const string sql = $"""
            SELECT CATEGORY, ISNULL(SUB_CATEGORY,'') AS SUB_CATEGORY,
                   COLOR_HEX, SECTION_LABEL, SORT_ORDER
            FROM   F360_CATEGORY_COLOR
            WHERE  ISNULL(IS_ACTIVE,'Y') = 'Y'
            ORDER  BY SORT_ORDER
            """;
        //const string sql = """
        //    SELECT CATEGORY,
        //           ISNULL(SUB_CATEGORY, '') AS SUB_CATEGORY,
        //           COLOR_HEX,
        //           SECTION_LABEL,
        //           SORT_ORDER
        //    FROM   dbo.F360_CATEGORY_COLOR
        //    WHERE  ISNULL(IS_ACTIVE, 'Y') = 'Y'
        //    ORDER  BY SORT_ORDER
        //    """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();

        var list = new List<F360CategoryColor>();
        while (await rdr.ReadAsync())
        {
            list.Add(new F360CategoryColor
            {
                Category = rdr.GetString(rdr.GetOrdinal("CATEGORY")),
                SubCategory = rdr.IsDBNull(rdr.GetOrdinal("SUB_CATEGORY")) ? "" : rdr.GetString(rdr.GetOrdinal("SUB_CATEGORY")),
                ColorHex = rdr.GetString(rdr.GetOrdinal("COLOR_HEX")),
                SectionLabel = rdr.GetString(rdr.GetOrdinal("SECTION_LABEL")),
                SortOrder = rdr.GetInt32(rdr.GetOrdinal("SORT_ORDER")),
            });
        }
        return list;
    }

    // ── Branch list ────────────────────────────────────────────────────────────
    public async Task<List<GapBranchListItem>> GetBranchesAsync(string? search = null)
    {
        const string sql = """
            SELECT DISTINCT
                   d.BRANCH_ID                          AS BRANCHID,
                   CAST(d.BRANCH_ID AS VARCHAR(20))     AS BRANCHCODE,
                   'Branch ' + CAST(d.BRANCH_ID AS VARCHAR(20)) AS BRANCHNAME,
                   ISNULL(CAST(d.REGION_ID AS VARCHAR(20)), '') AS REGIONNAME,
                   ISNULL(CAST(d.ZONE_ID AS VARCHAR(20)), '')   AS ZONENAME,
                   ''                                   AS ZMMBMNAME
            FROM   dbo.BUSINESS_360_DATA_MyDiary d
            ORDER  BY d.BRANCH_ID
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);

        await using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<GapBranchListItem>();
        while (await rdr.ReadAsync())
        {
			if (rdr.IsDBNull(rdr.GetOrdinal("BRANCHID")) || string.IsNullOrWhiteSpace(rdr["BRANCHID"]?.ToString()))
				continue;

			list.Add(new GapBranchListItem
            {
                BranchId = Convert.ToInt32(rdr["BRANCHID"]),
                BranchCode = rdr["BRANCHCODE"].ToString() ?? "",
                BranchName = rdr["BRANCHNAME"].ToString() ?? "",
                RegionName = rdr["REGIONNAME"].ToString() ?? "",
                ZoneName = rdr["ZONENAME"].ToString() ?? "",
                ZMBMName = rdr["ZMMBMNAME"].ToString() ?? "",
            });
        }
        return list;
    }

    // ── Zone list ──────────────────────────────────────────────────────────────
    public async Task<List<GapZoneListItem>> GetZonesAsync(string? search = null)
    {
        AppLogger.LogInfo("SqlGapService: Fetching zones list");
        const string sql = """
            SELECT DISTINCT
                   aesol_zone_cd AS ZoneId,
                   SUBSTRING(zone_name, 0, 7) AS ZoneCode,
                   SUBSTRING(zone_name, 8, 30) AS ZONENAME
            FROM   dbo.MD_BRANCH_MASTER
            ORDER  BY SUBSTRING(zone_name, 8, 30)
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();

        var list = new List<GapZoneListItem>();
        while (await rdr.ReadAsync())
        {
			if (rdr.IsDBNull(rdr.GetOrdinal("ZoneId")) || string.IsNullOrWhiteSpace(rdr["ZoneId"]?.ToString()))
				continue;

			list.Add(new GapZoneListItem
            {
                ZoneId = Convert.ToInt32(rdr["ZoneId"]),
                ZoneCode = rdr["ZoneCode"]?.ToString() ?? "",
                ZoneName = rdr["ZONENAME"]?.ToString() ?? "",
            });
        }
        return list;
    }

    // ── Region list ────────────────────────────────────────────────────────────
    public async Task<List<GapRegionListItem>> GetRegionsAsync(string? search = null)
    {
        const string sql = """
            SELECT DISTINCT
                   aesol_region_cd AS RegionId,
                   SUBSTRING(region_name, 0, 7) AS RegionCode,
                   SUBSTRING(region_name, 8, 30) AS RegionName
            FROM   dbo.MD_BRANCH_MASTER
            WHERE  aesol_region_cd IS NOT NULL
            ORDER  BY SUBSTRING(region_name, 8, 30)
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync();

        var list = new List<GapRegionListItem>();
        while (await rdr.ReadAsync())
        {
			if (rdr.IsDBNull(rdr.GetOrdinal("RegionId")) || string.IsNullOrWhiteSpace(rdr["RegionId"]?.ToString()))
				continue;

			list.Add(new GapRegionListItem
            {
                RegionId = Convert.ToInt32(rdr["RegionId"]),
                RegionCode = rdr["RegionCode"]?.ToString() ?? "",
                RegionName = rdr["RegionName"]?.ToString() ?? "",
            });
        }
        return list;
    }

    // ── Branch summary ─────────────────────────────────────────────────────────
    public async Task<List<GapBranchSummary?>> GetBranchSummaryAsync(string branchId, DateTime? snapshotDate = null)
    {
        AppLogger.LogInfo($"SqlGapService: Fetching branch summary for branch {branchId}");
        // Stub — replace with your branch master table query when available

        const string sql = """
             select sol_desc as BranchName,sol_id as BranchCode,zone_name as ZoneName,region_name as RegionName,
            branch_head_name as ZMBMName,
            LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ISNULL(branch_head_designation,'')),'(BRANCH HEAD)',''),'BRANCH HEAD',''),'-',' '),' / ','/'),'//','/'),'  ',' '),'/ ','/'))) AS ZMBMCode,
            branch_head_posted_since_dt as WorkingSince,sol_opn_dt as BranchOpenDate,
            ( case when license_number like 'NOT%' then 'Not Available' else license_number end) as License
            from MD_BRANCH_MASTER where sol_id = @BranchId
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Add(new SqlParameter("@BranchId", SqlDbType.Int) { Value = branchId });
        await using var rdr = await cmd.ExecuteReaderAsync();

        var b = new List<GapBranchSummary>();

        if (rdr.Read())
        {
            b.Add(new GapBranchSummary
            {
                BranchId = rdr["BranchCode"]?.ToString() ?? "",
                BranchCode = rdr["BranchCode"]?.ToString() ?? "",
                BranchName = rdr["BranchName"]?.ToString() ?? "",
                RegionName = rdr["RegionName"]?.ToString() ?? "",
                ZoneName = rdr["ZoneName"]?.ToString() ?? "",                
                ZMBMName = rdr["ZMBMName"]?.ToString() ?? "",
                ZMBMCode = rdr["ZMBMCode"]?.ToString() ?? "",
                WorkingSince = rdr["WorkingSince"] != DBNull.Value ? Convert.ToDateTime(rdr["WorkingSince"]) : null,
                BranchOpenDate = rdr["BranchOpenDate"] != DBNull.Value ? Convert.ToDateTime(rdr["BranchOpenDate"]) : null,
                License = rdr["License"]?.ToString() ?? ""
            });
        }
        
        return b;
    }

    // ── Staff details ──────────────────────────────────────────────────────────
    public async Task<List<GapStaffDetail>> GetStaffDetailsAsync(string branchId, DateTime? snapshotDate = null, bool isCOUser = false)
    {
        AppLogger.LogInfo($"SqlGapService: Fetching staff details for branch {branchId}, isCOUser={isCOUser}");
        static string GradeCode(int pid) => pid switch
        {
            100 => "CGM",
            101 => "GM",
            102 => "DGM",
            103 => "AGM",
            104 => "CM",
            105 => "SM",
            106 => "MGR",
            107 => "AM",
            108 => "CSA",
            109 => "SS",
            _ => $"P{pid}"
        };
        static string GradeName(int pid) => pid switch
        {
            100 => "Chief General Manager",
            101 => "General Manager",
            102 => "Deputy General Manager",
            103 => "Assistant General Manager",
            104 => "Chief Manager",
            105 => "Senior Manager",
            106 => "Manager",
            107 => "Assistant Manager",
            108 => "Clerical",
            109 => "Sub-Staff",
            _ => $"Grade {pid}"
        };

        // CO users get the full staff count across all branches;
        // non-CO users are filtered by their specific branch.
        string sql = isCOUser
            ? """
            select 
                sum(case when emp_scale_code='17' then 1 else 0 end) as cgm,
                sum(case when emp_scale_code='2' then 1 else 0 end) as gm,
                sum(case when emp_scale_code='3' then 1 else 0 end) as dgm,
                sum(case when emp_scale_code='4' then 1 else 0 end) as agm,
                sum(case when emp_scale_code='5' then 1 else 0 end) as cm,
                sum(case when emp_scale_code='6' then 1 else 0 end) as sm,
                sum(case when emp_scale_code='7' then 1 else 0 end) as m,
                sum(case when emp_scale_code='8' then 1 else 0 end) as am,
                sum(case when emp_scale_code='10' then 1 else 0 end) as clerk,
                sum(case when emp_scale_code in ('11','16') then 1 else 0 end) as ss
            from staffdetails
            """
            : """
            select 
                sum(case when emp_scale_code='17' then 1 else 0 end) as cgm,
                sum(case when emp_scale_code='2' then 1 else 0 end) as gm,
                sum(case when emp_scale_code='3' then 1 else 0 end) as dgm,
                sum(case when emp_scale_code='4' then 1 else 0 end) as agm,
                sum(case when emp_scale_code='5' then 1 else 0 end) as cm,
                sum(case when emp_scale_code='6' then 1 else 0 end) as sm,
                sum(case when emp_scale_code='7' then 1 else 0 end) as m,
                sum(case when emp_scale_code='8' then 1 else 0 end) as am,
                sum(case when emp_scale_code='10' then 1 else 0 end) as clerk,
                sum(case when emp_scale_code in ('11','16') then 1 else 0 end) as ss
            from staffdetails 
            where brsolid = @BranchId 
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);

        // Add the parameter even for CO users (not used in query but avoids issues
        // if ADO.NET validates parameter presence — harmless either way).
        if (!isCOUser)
            cmd.Parameters.Add(new SqlParameter("@BranchId", SqlDbType.Int) { Value = branchId });

        await using var rdr = await cmd.ExecuteReaderAsync();

        var list = new List<GapStaffDetail>();
        int sort = 1;

        if (await rdr.ReadAsync())
        {
            var data = rdr;

            list.Add(new GapStaffDetail { GradeCode = "CGM", GradeName = "Chief General Manager", HeadCount = GetInt(data, "cgm"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "GM", GradeName = "General Manager", HeadCount = GetInt(data, "gm"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "DGM", GradeName = "Deputy General Manager", HeadCount = GetInt(data, "dgm"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "AGM", GradeName = "Assistant General Manager", HeadCount = GetInt(data, "agm"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "CM", GradeName = "Chief Manager", HeadCount = GetInt(data, "cm"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "SM", GradeName = "Senior Manager", HeadCount = GetInt(data, "sm"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "MGR", GradeName = "Manager", HeadCount = GetInt(data, "m"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "AM", GradeName = "Assistant Manager", HeadCount = GetInt(data, "am"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "CSA", GradeName = "Clerical", HeadCount = GetInt(data, "clerk"), SortOrder = sort++ });
            list.Add(new GapStaffDetail { GradeCode = "SS", GradeName = "Sub-Staff", HeadCount = GetInt(data, "ss"), SortOrder = sort++ });
        }

        return list;

        //await using var rdr = await cmd.ExecuteReaderAsync();
        //var list = new List<GapStaffDetail>();
        //int sort = 1;
        //while (await rdr.ReadAsync())
        //{
        //    int pid = Convert.ToInt32(rdr["PARAMETER_ID"]);
        //    list.Add(new GapStaffDetail
        //    {
        //        GradeCode = GradeCode(pid),
        //        GradeName = GradeName(pid),
        //        HeadCount = rdr["HEADCOUNT"] is DBNull ? 0 : Convert.ToInt32(rdr["HEADCOUNT"]),
        //        SortOrder = sort++,
        //    });
        //}
        //return list;
    }

    // ── Staff totals ───────────────────────────────────────────────────────────
    public async Task<(int TotalStaff, decimal PerEmployeeBusiness)> GetStaffTotalsAsync(
        string branchId, DateTime? snapshotDate = null, bool isCOUser = false)
    {
        var staff = await GetStaffDetailsAsync(branchId, snapshotDate, isCOUser);
        int total = staff.Sum(s => s.HeadCount);
        if (total == 0) return (0, 0);

        const string sql = """
        SELECT ISNULL(d.ACTUALS_AS_ON, d.BASE_CURRENT_FY) AS TOTBIZ
        FROM dbo.BUSINESS_360_DATA_MyDiary d
        WHERE d.BRANCH_ID = @BranchId
          AND d.PARAMETER_ID = 51
          AND d.AS_ON_DATE = (
                SELECT MAX(x.AS_ON_DATE)
                FROM dbo.BUSINESS_360_DATA x
                WHERE x.BRANCH_ID = @BranchId
                  AND x.PARAMETER_ID = d.PARAMETER_ID
          )
        """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@BranchId", SqlDbType.NVarChar) { Value = branchId.ToString() });

        var raw = await cmd.ExecuteScalarAsync();
        decimal totalBiz = raw is DBNull || raw is null ? 0 : Convert.ToDecimal(raw) / 10_000_000m;
        decimal peb = total > 0 ? Math.Round(totalBiz / total, 2) : 0;
        return (total, peb);
    }

    // ── Full report ────────────────────────────────────────────────────────────
    public async Task<GapReportViewModel> BuildReportAsync(
        string branchId, DateTime? snapshotDate = null, bool isCOUser = false)
    {
        AppLogger.LogInfo($"SqlGapService: Building full report for branch {branchId}");
        var branchTask = GetBranchSummaryAsync(branchId, snapshotDate);
        var staffTask = GetStaffDetailsAsync(branchId, snapshotDate, isCOUser);
        var totalsTask = GetStaffTotalsAsync(branchId, snapshotDate, isCOUser);
        var dataTask = GetAllDataAsync(branchId, snapshotDate);
        var colorsTask = GetCategoryColorsAsync();

        await Task.WhenAll(branchTask, staffTask, totalsTask, dataTask, colorsTask);

        var branch = await branchTask;
        var staff = await staffTask;
        var staffTotals = await totalsTask;
        var allData = await dataTask;
        var colors = await colorsTask;

        return new GapReportViewModel
        {
            Branch = branch?.FirstOrDefault() ?? new GapBranchSummary { BranchId = branchId },
            Staff = staff,
            TotalStaff = staffTotals.TotalStaff,
            PerEmployeeBusiness = staffTotals.PerEmployeeBusiness,
            AllData = allData,
            CategoryColors = colors,
            GeneratedAt = AppTime.Now,
            AsOnDate = snapshotDate ?? allData.FirstOrDefault()?.AsOnDate ?? AppTime.Today,
        };
    }

    // ── Regions by Zone (cascading) ────────────────────────────────────────────
    public async Task<List<GapRegionListItem>> GetRegionsByZoneAsync(string zoneSolid)
    {
        AppLogger.LogInfo($"SqlGapService: Fetching regions for zone {zoneSolid}");
        const string sql = """
            SELECT DISTINCT
                   aesol_region_cd AS RegionId,
                   SUBSTRING(region_name, 0, 7) AS RegionCode,
                   SUBSTRING(region_name, 8, 30) AS RegionName
            FROM   dbo.MD_BRANCH_MASTER
            WHERE  aesol_zone_cd = @ZoneSolid
              AND  aesol_region_cd IS NOT NULL
            ORDER  BY SUBSTRING(region_name, 8, 30)
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ZoneSolid", zoneSolid);

        await using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<GapRegionListItem>();
        while (await rdr.ReadAsync())
        {
			if (rdr.IsDBNull(rdr.GetOrdinal("RegionId")) || string.IsNullOrWhiteSpace(rdr["RegionId"]?.ToString()))
				continue;

			list.Add(new GapRegionListItem
            {
                RegionId = Convert.ToInt32(rdr["RegionId"]),
                RegionCode = rdr["RegionCode"]?.ToString() ?? "",
                RegionName = rdr["RegionName"]?.ToString() ?? "",
            });
        }
        return list;
    }

    // ── Branches by Region (cascading) ─────────────────────────────────────────
    public async Task<List<GapBranchListItem>> GetBranchesByRegionAsync(string regionSolid)
    {
        AppLogger.LogInfo($"SqlGapService: Fetching branches for region {regionSolid}");
        const string sql = """
            SELECT DISTINCT
                   SOL_ID AS BRANCHID,
                   CAST(SOL_ID AS VARCHAR) AS BRANCHCODE,
                   CAST(SOL_DESC AS VARCHAR) AS BRANCHNAME,
                   CAST(REGION_NAME AS VARCHAR) AS REGIONNAME,
                   CAST(ZONE_NAME AS VARCHAR) AS ZONENAME,
                   '' AS ZMMBMNAME
            FROM   dbo.MD_BRANCH_MASTER
            WHERE  aesol_region_cd = @RegionSolid
            ORDER  BY SOL_ID
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@RegionSolid", regionSolid);

        await using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<GapBranchListItem>();
        while (await rdr.ReadAsync())
        {
			if (rdr.IsDBNull(rdr.GetOrdinal("BRANCHID")) || string.IsNullOrWhiteSpace(rdr["BRANCHID"]?.ToString()))
				continue;

			list.Add(new GapBranchListItem
            {
                BranchId = Convert.ToInt32(rdr["BRANCHID"]),
                BranchCode = rdr["BRANCHCODE"]?.ToString() ?? "",
                BranchName = rdr["BRANCHNAME"]?.ToString() ?? "",
                RegionName = rdr["REGIONNAME"]?.ToString() ?? "",
                ZoneName = rdr["ZONENAME"]?.ToString() ?? "",
                ZMBMName = rdr["ZMMBMNAME"]?.ToString() ?? "",
            });
        }
        return list;
    }

    // ── Data-reader mapper ─────────────────────────────────────────────────────
    private static List<GapPerformanceRow> MapRows(IDataReader rdr)
    {
        var list = new List<GapPerformanceRow>();
        while (rdr.Read())
        {
            list.Add(new GapPerformanceRow
            {
                ParameterId = GetInt(rdr, "PARAMETER_ID"),
                ParameterName = Str(rdr, "PARAMETERNAME"),
                Category = Str(rdr, "CATEGORY"),
                SubCategory = Str(rdr, "SUBCATEGORY"),
                DataType = Str(rdr, "DATATYPE"),
                UiType = Str(rdr, "UITYPE"),
                DateType = Str(rdr, "DATETYPE"),
                SortOrder = GetInt(rdr, "SORTORDER"),
                BaseLastFy = GetDec(rdr, "BASELASTFY"),
                BaseCurrentFy = GetDec(rdr, "BASECURRENTFY"),
                ActualAsOn = GetDec(rdr, "ACTUALASON"),
                Target = GetDec(rdr, "TARGET"),
                TargetQuarter = GetDec(rdr, "TARGETQUARTER"),
                AsOnDate = GetDate(rdr, "ASONDATE"),
            });
        }
        return list;
    }

    private static int GetInt(IDataReader r, string col)
    {
        try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i)); }
        catch { return 0; }
    }

    private static string Str(IDataReader r, string col)
    {
        try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? "" : r.GetString(i); }
        catch { return ""; }
    }

    private static decimal? GetDec(IDataReader r, string col)
    {
        try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i)); }
        catch { return null; }
    }

    private static DateTime? GetDate(IDataReader r, string col)
    {
        try { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : r.GetDateTime(i); }
        catch { return null; }
    }
}
