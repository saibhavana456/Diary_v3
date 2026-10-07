
using System.Data;
using Microsoft.Data.SqlClient;

namespace MyDiary.Web.Features.Reports.Services
{
    public class ChequeIssuedReportService
    {
        private readonly string _connectionString;

        public ChequeIssuedReportService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SQLServerConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        // --- MASTER DROPDOWN DATA METHODS ---
        public async Task<List<DropdownItem>> GetZonesAsync()
        {
            var list = new List<DropdownItem>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT zone_solid, zone_name_eng FROM dbo.zone_master WHERE status='A' ORDER BY zone_name_eng", conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem { Value = reader["zone_solid"].ToString() ?? "", Text = reader["zone_name_eng"].ToString() ?? "" });
            }
            return list;
        }

        public async Task<List<DropdownItem>> GetRegionsAsync(string zoneSolid)
        {
            var list = new List<DropdownItem>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT region_solid, region_name FROM dbo.region_master WHERE zone_solid = @ZoneSolid ORDER BY region_name", conn);
            cmd.Parameters.AddWithValue("@ZoneSolid", zoneSolid);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem { Value = reader["region_solid"].ToString() ?? "", Text = reader["region_name"].ToString() ?? "" });
            }
            return list;
        }

        public async Task<List<DropdownItem>> GetBranchesAsync(string regionSolid)
        {
            var list = new List<DropdownItem>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT SOL_ID, CONCAT(UPPER(branch_name),' (',branch_code,')') AS Branch_Name FROM dbo.branch_master WHERE region_solid = @RegionSolid ORDER BY branch_name", conn);
            cmd.Parameters.AddWithValue("@RegionSolid", regionSolid);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem { Value = reader["SOL_ID"].ToString() ?? "", Text = reader["Branch_Name"].ToString() ?? "" });
            }
            return list;
        }

        // --- LATEST UPLOAD DATE ---
        public async Task<string> GetLatestUploadDateAsync()
        {
            const string sql = @"
                SELECT TOP (1) CAST(LAST_UPDATE_DATE AS date) AS UploadDate
                FROM dbo.Cheque_Issued_Not_Ack
                WHERE LAST_UPDATE_DATE IS NOT NULL
                ORDER BY LAST_UPDATE_DATE DESC;";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();

            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToDateTime(result).ToString("dd-MM-yyyy");
            }
            return "N/A";
        }

        // --- MAIN REPORT DATA ---
        public async Task<List<Dictionary<string, object>>> GetChequeIssuedReportDataAsync(
            string zoneMode, string regionMode, string branchMode,
            string? zoneSolid, string? regionSolid, string? branchSolId)
        {
            var resultList = new List<Dictionary<string, object>>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("dbo.USP_Get_Cheque_Issued_Report", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ZoneMode", zoneMode);
            cmd.Parameters.AddWithValue("@RegionMode", regionMode);
            cmd.Parameters.AddWithValue("@BranchMode", branchMode);
            cmd.Parameters.AddWithValue("@ZoneSolid", (object?)zoneSolid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RegionSolid", (object?)regionSolid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BranchSolId", (object?)branchSolId ?? DBNull.Value);

            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Utilizing OrdinalIgnoreCase for safe mapping to MudTable headers
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                resultList.Add(row);
            }

            return resultList;
        }
    }
}