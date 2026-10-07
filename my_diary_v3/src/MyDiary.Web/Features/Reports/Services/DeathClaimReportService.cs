
using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Services;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.Reports.Services
{
    public class DeathClaimReportService
    {
        private readonly string _sqlConnectionString;
        private readonly string _oracleConnectionString;
        private readonly IEncryptionService _encryptionService;

        public DeathClaimReportService(IConfiguration configuration, IEncryptionService encryptionService)
        {
            // Fetching BOTH connection strings
            _sqlConnectionString = configuration.GetConnectionString("SQLServerConnection")
                ?? throw new InvalidOperationException("SQL Connection string not found.");

            _oracleConnectionString = configuration.GetConnectionString("DeathClaim_Live")
                ?? throw new InvalidOperationException("Oracle Connection string not found.");

            _encryptionService = encryptionService;
        }

        // =========================================================================
        // --- MASTER DROPDOWN DATA METHODS (SQL SERVER) ---
        // =========================================================================

        public async Task<List<DropdownItem>> GetZonesAsync()
        {
            var list = new List<DropdownItem>();
            using var conn = new SqlConnection(_sqlConnectionString);
            // Fetching from SQL Server. Using zone_code as the value for Oracle compatibility.
            using var cmd = new SqlCommand("SELECT zone_code, zone_name_eng FROM dbo.zone_master WHERE status='A' ORDER BY zone_name_eng", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem
                {
                    Value = reader["zone_code"].ToString() ?? "",
                    Text = reader["zone_name_eng"].ToString() ?? ""
                });
            }
            return list;
        }

        public async Task<List<DropdownItem>> GetRegionsAsync(string zoneCode)
        {
            var list = new List<DropdownItem>();
            using var conn = new SqlConnection(_sqlConnectionString);
            using var cmd = new SqlCommand("SELECT region_code, region_name FROM dbo.region_master WHERE zone_code = @ZoneCode ORDER BY region_name", conn);
            cmd.Parameters.AddWithValue("@ZoneCode", zoneCode);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem
                {
                    Value = reader["region_code"].ToString() ?? "",
                    Text = reader["region_name"].ToString() ?? ""
                });
            }
            return list;
        }

        public async Task<List<DropdownItem>> GetBranchesAsync(string regionCode)
        {
            var list = new List<DropdownItem>();
            using var conn = new SqlConnection(_sqlConnectionString);
            using var cmd = new SqlCommand("SELECT branch_code, CONCAT(UPPER(branch_name), ' (', branch_code, ')') AS Branch_Name FROM dbo.branch_master WHERE region_code = @RegionCode ORDER BY branch_name", conn);
            cmd.Parameters.AddWithValue("@RegionCode", regionCode);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem
                {
                    Value = reader["branch_code"].ToString() ?? "",
                    Text = reader["Branch_Name"].ToString() ?? ""
                });
            }
            return list;
        }


        // =========================================================================
        // --- REPORT DATA METHODS (ORACLE) ---
        // =========================================================================

        public async Task<(List<Dictionary<string, object>> Summary, List<Dictionary<string, object>> Details)> GetDashboardDataAsync(
            string zoneCode, string regionCode, string branchCode, string pendency)
        {
            var summaryData = await GetSummaryDataAsync(zoneCode, regionCode, branchCode);
            var detailData = await GetDetailDataAsync(zoneCode, regionCode, branchCode, pendency);

            return (summaryData, detailData);
        }

        private async Task<List<Dictionary<string, object>>> GetSummaryDataAsync(string zoneCode, string regionCode, string branchCode)
        {
            var summaryList = new List<Dictionary<string, object>>();
            int sumTotal = 0, sum0to10 = 0, sum10to15 = 0, sumBeyond15 = 0;

            using var conn = new OracleConnection(_oracleConnectionString);
            using var cmd = new OracleCommand("Dashboard_Summary_For_My_Diary", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("G_ZONE_CODE", OracleDbType.Varchar2).Value = zoneCode;
            cmd.Parameters.Add("G_REGION_CODE", OracleDbType.Varchar2).Value = regionCode;
            cmd.Parameters.Add("G_BRANCH_CODE", OracleDbType.Varchar2).Value = branchCode;
            cmd.Parameters.Add("C1", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                sumTotal += Convert.ToInt32(row["Total Pendency"] ?? 0);
                sum0to10 += Convert.ToInt32(row["0 to 10 days"] ?? 0);
                sum10to15 += Convert.ToInt32(row["10 to 15 days"] ?? 0);
                sumBeyond15 += Convert.ToInt32(row["Beyond 15 days"] ?? 0);

                summaryList.Add(row);
            }

            if (summaryList.Count > 0)
            {
                var totalRow = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Zone/Region/Branch", "GRAND TOTAL" },
                    { "Total Pendency", sumTotal },
                    { "0 to 10 days", sum0to10 },
                    { "10 to 15 days", sum10to15 },
                    { "Beyond 15 days", sumBeyond15 }
                };
                summaryList.Add(totalRow);
            }

            return summaryList;
        }

        private async Task<List<Dictionary<string, object>>> GetDetailDataAsync(string zoneCode, string regionCode, string branchCode, string pendency)
        {
            var detailList = new List<Dictionary<string, object>>();

            using var conn = new OracleConnection(_oracleConnectionString);
            using var cmd = new OracleCommand("Dashboard_For_My_Diary", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("G_REFNO", OracleDbType.Varchar2).Value = "ALL";
            cmd.Parameters.Add("G_BRANCH_CODE", OracleDbType.Varchar2).Value = branchCode;
            cmd.Parameters.Add("G_REGION_CODE", OracleDbType.Varchar2).Value = regionCode;
            cmd.Parameters.Add("G_ZONE_CODE", OracleDbType.Varchar2).Value = zoneCode;
            cmd.Parameters.Add("G_STATUS", OracleDbType.Varchar2).Value = "ALL";
            cmd.Parameters.Add("G_NO_OF_DAYS", OracleDbType.Varchar2).Value = "ALL";
            cmd.Parameters.Add("G_DEATH_RSN", OracleDbType.Varchar2).Value = "ALL";
            cmd.Parameters.Add("G_Submission_Date", OracleDbType.Varchar2).Value = "ALL";
            cmd.Parameters.Add("G_Pendency", OracleDbType.Varchar2).Value = pendency;
            cmd.Parameters.Add("C1", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i);
                    object? val = reader.IsDBNull(i) ? null : reader.GetValue(i);

                    // Decrypt the Account No on the fly
                    if (colName.Equals("ACCOUNT_NO", StringComparison.OrdinalIgnoreCase) && val != null)
                    {
                        val = DecryptAccountNo(val.ToString()!);
                    }

                    row[colName] = val;
                }
                detailList.Add(row);
            }

            return detailList;
        }

        // =========================================================================
        // --- DATA ENCRYPTION ---
        // =========================================================================

        private string DecryptAccountNo(string encryptedText)
        {
            try
            {
                return _encryptionService.DecryptAesCbc(encryptedText, "Sector@2873");
            }
            catch
            {
                return "Invalid";
            }
        }
    }
}