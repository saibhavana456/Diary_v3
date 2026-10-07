using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Dashboard_new.Models;
using MyDiary.Web.Features.Dashboard_new_1.Models;

namespace MyDiary.Web.Features.Dashboard_new_1.Services
{
    public interface IDashboard1Service
    {
        Task<List<Zone>>   GetZonesAsync();
        Task<List<Region>> GetRegionsAsync(string zoneSolid);
        Task<List<Branch>> GetBranchesAsync(string regionSolid);

        /// <summary>
        /// Calls SP_PAGETHREE_DASHBOARD_KPI with the resolved SOL ID.
        /// The SP returns a single result set whose column names embed the dynamic
        /// date labels, so we read by ordinal and capture the column names for headers.
        /// </summary>
        Task<KpiDashboard1Result> GetKpiDataAsync(
            string role, string brSolId, string regionSolId, string zoneSolId,
            string? overrideSolId = null);
    }

    public class Dashboard1Service : IDashboard1Service
    {
        private readonly SqlDbProvider _db;
        private readonly IConfiguration _config;

        public Dashboard1Service(SqlDbProvider db, IConfiguration config)
        {
            _db     = db;
            _config = config;
        }

        // ── Master-data helpers (shared with Dashboard_new) ──────────────────────

        public async Task<List<Zone>> GetZonesAsync()
        {
            try
            {
                const string sql = "SELECT zone_name_eng, zone_solid FROM zone_master WHERE status='A' ORDER BY zone_name_eng";
                var dt = await _db.ExecuteQueryAsync(sql);
                var list = new List<Zone>();
                foreach (DataRow row in dt.Rows)
                    list.Add(new Zone { ZoneName = row["zone_name_eng"].ToString()!, ZoneSolid = row["zone_solid"].ToString()! });
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[Dashboard1Service] GetZonesAsync failed: " + ex.Message);
                return new List<Zone>();
            }
        }

        public async Task<List<Region>> GetRegionsAsync(string zoneSolid)
        {
            try
            {
                string sql = $@"
                    SELECT region_name, region_solid FROM region_master WHERE zone_solid='{zoneSolid}'
                    UNION
                    SELECT B.Branch_Name, B.Sol_ID
                    FROM SpecializedBranches A JOIN Branch_Master B ON A.SolId = B.SOL_ID
                    WHERE zone_solid='{zoneSolid}' AND UnderZO = 'Y'
                    ORDER BY region_name";
                var dt = await _db.ExecuteQueryAsync(sql);
                var list = new List<Region>();
                foreach (DataRow row in dt.Rows)
                    list.Add(new Region { RegionName = row["region_name"].ToString()!, RegionSolid = row["region_solid"].ToString()! });
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[Dashboard1Service] GetRegionsAsync failed: " + ex.Message);
                return new List<Region>();
            }
        }

        public async Task<List<Branch>> GetBranchesAsync(string regionSolid)
        {
            try
            {
                string sql = @"SELECT CONCAT(UPPER(branch_name),'  (',branch_code,')') AS Branch_Name, SOL_ID
                                FROM branch_master WHERE region_solid=@regionSolid ORDER BY Branch_Name";
                var dt = await _db.ExecuteQueryWithParamsAsync(sql, new[] { new Microsoft.Data.SqlClient.SqlParameter("@regionSolid", regionSolid) });
                var list = new List<Branch>();
                foreach (DataRow row in dt.Rows)
                    list.Add(new Branch { BranchName = row["Branch_Name"].ToString()!, SolId = row["SOL_ID"].ToString()! });
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[Dashboard1Service] GetBranchesAsync failed: " + ex.Message);
                return new List<Branch>();
            }
        }

        // ── KPI data ─────────────────────────────────────────────────────────────

        public async Task<KpiDashboard1Result> GetKpiDataAsync(
            string role, string brSolId, string regionSolId, string zoneSolId,
            string? overrideSolId = null)
        {
            try
            {
                // Resolve which solid to pass — same logic as Landing_Dashboard_V2_1
                int solId = overrideSolId != null
                    ? ParseSolId(overrideSolId)
                    : ResolveSolId(role, brSolId, regionSolId, zoneSolId);

                var connStr = _config.GetConnectionString("SQLServerConnection")!;
                var result  = new KpiDashboard1Result();

                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_PAGETHREE_DASHBOARD_KPI", conn)
                {
                    CommandType    = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };
                cmd.Parameters.Add(new SqlParameter("@SOL_ID", SqlDbType.Int) { Value = solId });

                using var reader = await cmd.ExecuteReaderAsync();

                bool headersRead = false;

                while (await reader.ReadAsync())
                {
                    if (!headersRead)
                    {
                        result.HeaderLastToLastFY = reader.GetName(1);
                        result.HeaderLastFY       = reader.GetName(2);
                        result.HeaderLastQtr      = reader.GetName(3);
                        result.HeaderAsOnDate     = reader.GetName(4);
                        result.HeaderTargetQtr    = reader.GetName(5);
                        result.HeaderTargetFY     = reader.GetName(6);
                        result.HeaderGrowthOver   = reader.GetName(7);
                        result.HeaderTargetFYGap  = reader.GetName(8);
                        headersRead = true;
                    }

                    result.Items.Add(new KpiItem1
                    {
                        ParameterName     = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        ActualLastToLastFY = SafeDecimal(reader, 1),
                        ActualLastFY       = SafeDecimal(reader, 2),
                        ActualLastQtr      = SafeDecimal(reader, 3),
                        ActualAsOnDate     = SafeDecimal(reader, 4),
                        TargetNextQtr      = SafeDecimal(reader, 5),
                        TargetNextMar      = SafeDecimal(reader, 6),
                        GrowthOver         = SafeDecimal(reader, 7),
                        GapToTarget        = SafeDecimal(reader, 8)
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[Dashboard1Service] GetKpiDataAsync failed: " + ex.Message);
                return new KpiDashboard1Result();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Mirrors LandingDashboardV2_1Service.ResolveSolId:
        ///   ZONE/ZO  → zone solid
        ///   REGION/RO → region solid
        ///   else      → branch solid
        /// </summary>
        private static int ResolveSolId(string role, string brSolId, string regionSolId, string zoneSolId)
        {
            string upper = role?.ToUpper() ?? "";
            if (upper.Contains("ZONE") || upper.Contains("ZO"))
            {
                if (int.TryParse(zoneSolId, out int z)) return z;
            }
            else if (upper.Contains("REGION") || upper.Contains("RO"))
            {
                if (int.TryParse(regionSolId, out int r)) return r;
            }
            if (int.TryParse(brSolId, out int b)) return b;
            return 0;
        }

        private static int ParseSolId(string? s) =>
            int.TryParse(s, out int v) ? v : 0;

        private static decimal SafeDecimal(SqlDataReader r, int ordinal) =>
            r.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(r.GetValue(ordinal));
    }
}
