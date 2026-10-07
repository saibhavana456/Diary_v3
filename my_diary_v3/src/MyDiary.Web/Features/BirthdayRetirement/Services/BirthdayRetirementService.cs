using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Features.BirthdayRetirement.Models;

namespace MyDiary.Web.Features.BirthdayRetirement.Services
{
    public class BirthdayRetirementService : IBirthdayRetirementService
    {
        private readonly string _connectionString;
        private readonly string _brdiaryConnectionString;
        public BirthdayRetirementService(IConfiguration configuration)
        {
            // Using the connection string name from your legacy code
            _connectionString = configuration.GetConnectionString("OrganisationsSQLCon");
            _brdiaryConnectionString = configuration.GetConnectionString("SQLServerConnection");
        }

        public async Task<IEnumerable<StaffEventDto>> GetStaffEventsAsync(string zone, string region, string branch, string eventType)
        {
            var staffEvents = new List<StaffEventDto>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    using (var command = new SqlCommand("usp_Birthday_and_retirement", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add("@branch", SqlDbType.NVarChar, 10).Value = branch;
                        command.Parameters.Add("@region", SqlDbType.NVarChar, 10).Value = region;
                        command.Parameters.Add("@zone", SqlDbType.NVarChar, 10).Value = zone;
                        command.Parameters.Add("@type", SqlDbType.NVarChar, 30).Value = eventType;

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                staffEvents.Add(new StaffEventDto
                                {
                                    PfNo = reader["PF No"].ToString(),
                                    Name = reader["Name"].ToString(),
                                    PostedAt = reader["Posted At"].ToString(),
                                    Designation = reader["Designation"].ToString(),
                                    EventDate = reader.GetValue(4).ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"BirthdayRetirementService: GetStaffEventsAsync failed — type: {eventType}");
            }

            return staffEvents;
        }
        public async Task<IEnumerable<LocationDropdownDto>> GetZonesAsync()
        {
            var zones = new List<LocationDropdownDto>();
            try
            {
                string query = @"SELECT zone_code, CONCAT(UPPER(zone_name_eng),'  (',zone_code,')') as zone_name_eng 
                     FROM zone_master WHERE status='A' ORDER BY zone_name_eng";

                using (var connection = new SqlConnection(_brdiaryConnectionString))
                using (var command = new SqlCommand(query, connection))
                {
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Preserving the legacy string replacement logic
                            string originalName = reader["zone_name_eng"].ToString();
                            originalName = originalName.Replace("FIELD GENERAL MANAGER", "ZONAL");

                            zones.Add(new LocationDropdownDto
                            {
                                Code = reader["zone_code"].ToString(),
                                Name = originalName
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[BirthdayRetirementService] GetZonesAsync failed: " + ex.Message);
            }
            return zones;
        }

        public async Task<IEnumerable<LocationDropdownDto>> GetRegionsAsync(string zoneCode)
        {
            var regions = new List<LocationDropdownDto>();
            try
            {
                string query = @"SELECT region_code, CONCAT(UPPER(region_name),'  (',region_code,')') as region_name 
                     FROM region_master WHERE zone_code = @ZoneCode ORDER BY region_name";

                using (var connection = new SqlConnection(_brdiaryConnectionString))
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ZoneCode", zoneCode);
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            regions.Add(new LocationDropdownDto
                            {
                                Code = reader["region_code"].ToString(),
                                Name = reader["region_name"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[BirthdayRetirementService] GetRegionsAsync failed: " + ex.Message);
            }
            return regions;
        }

        public async Task<IEnumerable<LocationDropdownDto>> GetBranchesAsync(string regionCode)
        {
            var branches = new List<LocationDropdownDto>();
            try
            {
                string query = @"SELECT branch_code, CONCAT(UPPER(branch_name),'  (',branch_code,')') as branch_name 
                     FROM branch_master WHERE region_code = @RegionCode ORDER BY branch_name";

                using (var connection = new SqlConnection(_brdiaryConnectionString))
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@RegionCode", regionCode);
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            branches.Add(new LocationDropdownDto
                            {
                                Code = reader["branch_code"].ToString(),
                                Name = reader["branch_name"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[BirthdayRetirementService] GetBranchesAsync failed: " + ex.Message);
            }
            return branches;
        }
    }
}