using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Dashboard.Models;

namespace MyDiary.Web.Features.Dashboard.Services
{
    public interface IDashboardService
    {
        Task<List<Zone>> GetZonesAsync();
        Task<List<Region>> GetRegionsAsync(string zoneSolid);
        Task<List<Branch>> GetBranchesAsync(string regionSolid);
        Task<List<KpiItem>> GetKpiDataAsync(string solId);
    }

    public class DashboardService : IDashboardService
    {
        private readonly SqlDbProvider _db;

        public DashboardService(SqlDbProvider db)
        {
            _db = db;
        }

        public async Task<List<Zone>> GetZonesAsync()
        {
            try
            {
                // Logic from ddlZone_bind
                string query = "SELECT zone_name_eng, zone_solid FROM zone_master WHERE status='A' ORDER BY zone_name_eng";
                var dt = await _db.ExecuteQueryAsync(query);

                var list = new List<Zone>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new Zone
                    {
                        ZoneName = row["zone_name_eng"].ToString()!,
                        ZoneSolid = row["zone_solid"].ToString()!
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DashboardService] GetZonesAsync failed: " + ex.Message);
                return new List<Zone>();
            }
        }

        public async Task<List<Region>> GetRegionsAsync(string zoneSolid)
        {
            try
            {
                string query = $@"
                    SELECT region_name, region_solid FROM region_master WHERE zone_solid='{zoneSolid}'
                    UNION
                    SELECT B.Branch_Name as region_name, B.Sol_ID as region_solid 
                    FROM SpecializedBranches A 
                    JOIN Branch_Master B On A.SolId = B.SOL_ID 
                    WHERE zone_solid='{zoneSolid}' And UnderZO = 'Y'
                    ORDER BY region_name";

                var dt = await _db.ExecuteQueryAsync(query);
                var list = new List<Region>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new Region
                    {
                        RegionName = row["region_name"].ToString()!,
                        RegionSolid = row["region_solid"].ToString()!
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DashboardService] GetRegionsAsync failed: " + ex.Message);
                return new List<Region>();
            }
        }

        public async Task<List<Branch>> GetBranchesAsync(string regionSolid)
        {
            try
            {
                string query = @"SELECT CONCAT(UPPER(branch_name),'  (',branch_code,')') As Branch_Name, SOL_ID
                                  FROM branch_master WHERE region_solid=@regionSolid ORDER BY Branch_Name";

                var dt = await _db.ExecuteQueryWithParamsAsync(query, new[] { new Microsoft.Data.SqlClient.SqlParameter("@regionSolid", regionSolid) });
                var list = new List<Branch>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new Branch
                    {
                        BranchName = row["Branch_Name"].ToString()!,
                        SolId = row["SOL_ID"].ToString()!
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DashboardService] GetBranchesAsync failed: " + ex.Message);
                return new List<Branch>();
            }
        }

        public async Task<List<KpiItem>> GetKpiDataAsync(string solId)
        {
            try
            {
                var p = new SqlParameter[] { new SqlParameter("@SolId", solId) };
                var dt = await _db.ExecuteStoredProcAsync("uspGetKPIDataToDisplay_SUBSTD", p);

                var list = new List<KpiItem>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new KpiItem
                    {
                        ParameterName = row["parameter"].ToString()!,
                        ActualLastToLastFY = Convert.ToDecimal(row["ActualLastToLastFY"]),
                        ActualLastFY = Convert.ToDecimal(row["ActualLastFY"]),
                        ActualLastQtr = Convert.ToDecimal(row["ActualLastQtr"]),
                        ActualPrevDate = Convert.ToDecimal(row["ActualPrevDate"]),
                        ActualAsOnDate = Convert.ToDecimal(row["ActualAsOnDate"]),
                        TargetNextQtr = Convert.ToDecimal(row["TargetNextQtr"]),
                        TargetNextMar = Convert.ToDecimal(row["TargetNextMar"]),

                        // Dynamic Headers Data
                        LastFYDt = Convert.ToDateTime(row["LastFYDt"]),
                        LastToLastFYDt = Convert.ToDateTime(row["LastToLastFYDt"]),
                        NextQtrDt = Convert.ToDateTime(row["NextQtrDt"]),
                        NextFYDt = Convert.ToDateTime(row["NextFYDt"]),
                        KPIDate = Convert.ToDateTime(row["KPIDate"])
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DashboardService] GetKpiDataAsync failed: " + ex.Message);
                return new List<KpiItem>();
            }
        }
    }
}