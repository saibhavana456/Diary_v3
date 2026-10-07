
using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;

namespace MyDiary.Web.Features.Reports.Services
{
    // A simple model to hold our dropdown data
    public class DropdownItem
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class DCFEMenuReportService
    {
        private readonly string _connectionString;

        public DCFEMenuReportService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SQLServerConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        // --- NEW DROPDOWN METHODS ---

        public async Task<List<DropdownItem>> GetZonesAsync()
        {
            var list = new List<DropdownItem>();
            try
            {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("dbo.usp_GetZones", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem
                {
                    Value = reader["zone_solid"].ToString() ?? "",
                    Text = reader["zone_name_eng"].ToString() ?? ""
                });
            }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DCFEMenuReportService] GetZonesAsync failed: " + ex.Message);
            }
            return list;
        }

        public async Task<List<DropdownItem>> GetRegionsAsync(string zoneSolid)
        {
            var list = new List<DropdownItem>();
            try
            {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("dbo.usp_GetRegionsByZone", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ZoneSolid", string.IsNullOrEmpty(zoneSolid) ? "ALL" : zoneSolid);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem
                {
                    Value = reader["region_solid"].ToString() ?? "",
                    Text = reader["region_name"].ToString() ?? ""
                });
            }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DCFEMenuReportService] GetRegionsAsync failed: " + ex.Message);
            }
            return list;
        }

        public async Task<List<DropdownItem>> GetBranchesAsync(string regionSolid)
        {
            var list = new List<DropdownItem>();
            try
            {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("dbo.usp_GetBranchesByRegion", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@RegionSolid", string.IsNullOrEmpty(regionSolid) ? "ALL" : regionSolid);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DropdownItem
                {
                    Value = reader["SOL_ID"].ToString() ?? "",
                    // Matching legacy string concatenation: BRANCH_NAME (branch_code)
                    Text = $"{reader["branch_name"]} ({reader["branch_code"]})"
                });
            }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DCFEMenuReportService] GetBranchesAsync failed: " + ex.Message);
            }
            return list;
        }

        // --- EXISTING DATA METHOD ---

        public async Task<(List<Dictionary<string, object>> Data, string LastUpdatedDate)> GetDCFEMenuDataAsync(
            string zoneCode, string regionCode, string branchCode, string pendencySelected)
        {
            var resultList = new List<Dictionary<string, object>>();
            string lastUpdated = string.Empty;

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("dbo.usp_GetDCFE_Menu", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add("@ZOCODE", SqlDbType.NVarChar, 50).Value = string.IsNullOrEmpty(zoneCode) ? "ALL" : zoneCode;
            command.Parameters.Add("@ROCODE", SqlDbType.NVarChar, 50).Value = string.IsNullOrEmpty(regionCode) ? "ALL" : regionCode;
            command.Parameters.Add("@BRCODE", SqlDbType.NVarChar, 50).Value = string.IsNullOrEmpty(branchCode) ? "ALL" : branchCode;
            command.Parameters.Add("@PENDENCY_SELECTED", SqlDbType.NVarChar, 50).Value = string.IsNullOrEmpty(pendencySelected) ? "ALL" : pendencySelected;
            command.Parameters.Add("@OnlyOpenAccounts", SqlDbType.Bit).Value = false;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                resultList.Add(row);
            }

            if (await reader.NextResultAsync() && await reader.ReadAsync() && !reader.IsDBNull(0))
            {
                lastUpdated = Convert.ToDateTime(reader.GetValue(0)).ToString("dd/MM/yyyy");
            }

            return (resultList, lastUpdated);
        }
    }
}