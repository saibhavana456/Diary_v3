using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Features.Shared.Services;

namespace MyDiary.Web.Features.Reports.Services
{
    public class SundryReportService
    {
        private readonly DynamicReportService _db;

        public SundryReportService(DynamicReportService db)
        {
            _db = db;
        }

        public async Task<List<Dictionary<string, object>>> GetSundryDataAsync(
            string privilege, string userSolid, string zoneVal, string regionVal, string branchVal, bool is90Days)
        {
            try
            {
            string columns;
            string whereClause = "region_solid <> '' ";
            string groupBy = "";

            // Aging Logic from ListSundryData90Days.aspx.cs
            if (is90Days)
            {
                whereClause += " AND (DATEDIFF(DAY, CONVERT(datetime, (case when ValueDate='0001-01-01' then TranDate else ValueDate end), 105), GETDATE())) >= 90 ";
            }

            // Replicating Privilege logic
            if (privilege == "CO")
            {
                if (zoneVal == "1" || string.IsNullOrEmpty(zoneVal)) // All(Summary)
                {
                    columns = "count(TranAmount) as TranCount, sum(diff)/100000 as Outstanding, 'All' As Branch_Name, 'All' As region_name, 'All' As zone_name_eng, 'All' As SolId";
                }
                else if (zoneVal.ToUpper() == "ALL") // All(Zonewise)
                {
                    if (regionVal == "1")
                    {
                        columns = "count(TranAmount) as TranCount, sum(diff)/100000 as Outstanding, 'All' As Branch_Name, 'All' As region_name, zone_name_eng, Zone_SolId As SolId";
                        groupBy = " GROUP BY Zone_SolId, zone_name_eng";
                    }
                    else if (regionVal.ToUpper() == "ALL")
                    {
                        if (branchVal == "1") // Region Summary
                        {
                            columns = "count(TranAmount) as TranCount, sum(diff)/100000 as Outstanding, 'All' As Branch_Name, b.region_name, zone_name_eng, region_SolId As SolId";
                            groupBy = " GROUP BY b.region_name, region_SolId, zone_name_eng";
                        }
                        else // Detailed Data
                        {
                            columns = "zone_name_eng, b.region_name, b.BRANCH_NAME, b.sol_id branchsol, a.AccountNo, a.accountname, a.tranid, a.trandate, a.valuedate, a.trannarration, a.tranamount, a.diff as Outstanding";
                        }
                    }
                    else // Specific Region
                    {
                        whereClause += $" AND region_solid = '{regionVal}'";
                        columns = "zone_name_eng, b.region_name, b.BRANCH_NAME, b.sol_id branchsol, a.AccountNo, a.accountname, a.tranid, a.trandate, a.valuedate, a.trannarration, a.tranamount, a.diff as Outstanding";
                    }
                }
                else // Specific Zone Selection
                {
                    whereClause += $" AND zone_solid = '{zoneVal}'";
                    columns = "count(TranAmount) as TranCount, sum(diff)/100000 as Outstanding, 'All' As Branch_Name, 'All' As region_name, zone_name_eng, Zone_SolId As SolId";
                    groupBy = " GROUP BY Zone_SolId, zone_name_eng";
                }
            }
            else // ZONE, REGION, BRANCH Logic
            {
                string solField = privilege == "ZONE" ? "zone_solid" : (privilege == "REGION" ? "region_solid" : "b.sol_id");
                whereClause += $" AND {solField} = '{userSolid}'";
                columns = "zone_name_eng, b.region_name, b.BRANCH_NAME, b.sol_id branchsol, a.AccountNo, a.accountname, a.tranid, a.trandate, a.valuedate, a.trannarration, a.tranamount, a.diff as Outstanding";
            }

            string query = $"SELECT {columns} FROM Sundry_OAP_Agewise A JOIN Branch_Master B On A.SolID = B.SOL_ID WHERE {whereClause} {groupBy}";
            return await _db.ExecuteRawQueryAsync(query);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[SundryReportService] GetSundryDataAsync failed: " + ex.Message);
                return new List<Dictionary<string, object>>();
            }
        }
    }
}
