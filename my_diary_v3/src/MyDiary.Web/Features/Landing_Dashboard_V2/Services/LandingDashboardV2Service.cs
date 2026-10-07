using Microsoft.Data.SqlClient;
using MyDiary.Core.Services;
using MyDiary.Web.Features.Landing_Dashboard_V2.Models;
using System.Data;

namespace MyDiary.Web.Features.Landing_Dashboard_V2.Services
{
    public class LandingDashboardV2Service : ILandingDashboardV2Service
    {
        private readonly string _connectionString;

        // 1. Inject IConfiguration instead of a shared DB Provider
        public LandingDashboardV2Service(IConfiguration configuration)
        {
            // Fetch the connection string directly. 
            // Note: Make sure "DefaultConnection" matches the name in your appsettings.json
            _connectionString = configuration.GetConnectionString("SQLServerConnection");
        }

        public async Task<TotalAdvancesPerformance> GetAdvancesPerformanceAsync(string zoneName, string regionName)
        {
            var result = new TotalAdvancesPerformance();

            // Secure, parameterized query
            string query = @"
                SELECT TOP 1
                    Zone_Name AS ZoneName,
                    Region_Name AS RegionName,
                    TRY_CAST([Actual Total] AS DECIMAL(18,2)) AS ActualTotal,
                    TRY_CAST([Current] AS DECIMAL(18,2)) AS [Current],
                    TRY_CAST([Target] AS DECIMAL(18,2)) AS Target
                FROM TOTAL_ADVANCES_PERFORMANCE_VIEW";

            try
            {
                // 2. Establish a dedicated connection just for this feature
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Add parameters to prevent SQL injection
                        cmd.Parameters.AddWithValue("@Zone", zoneName);
                        cmd.Parameters.AddWithValue("@Region", regionName);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                // Safely map data from the reader to the C# object
                                result.ZoneName = reader["ZoneName"]?.ToString();
                                result.RegionName = reader["RegionName"]?.ToString();

                                result.ActualTotal = reader["ActualTotal"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["ActualTotal"]) / 10000000m: 0m;

                                result.Current = reader["Current"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["Current"]) / 10000000m : 0m;

                                result.Target = reader["Target"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["Target"]) / 10000000m : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "LandingDashboardV2Service: GetAdvancesPerformanceAsync failed — returning fallback data");
                result = new TotalAdvancesPerformance
                {
                    ActualTotal = 273.80m,
                    Current = 238.86m,
                    Target = 297.85m
                };
            }

            return result;
        }
        public async Task<List<ProspectiveBusinessSummary>> GetProspectiveBusinessSummaryAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new List<ProspectiveBusinessSummary>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("usp_GetProspectiveBusinessSummary_Csv", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // 1. Map the Claim Role to the SP @Scope parameter
                        string scope = "BRANCH"; // Default fallback
                        string upperRole = role?.ToUpper() ?? "";

                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        // 2. Reporting Date (Adjust this if your app picks a specific date from a calendar filter)
                        string fiRpDt = AppTime.Now.ToString("yyyyMMdd");
                        fiRpDt = "20260311";
                        // 3. Add secure parameters (handling possible nulls from claims)
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@FiRpDt", fiRpDt);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);

                        // Optional: Pass the exact string if needed, or leave it to trigger the SP's default fallback
                        // cmd.Parameters.AddWithValue("@ParameterCsv", "Shishu Mudra,Mudra Tarun Kishore,Nari Shakti,Union Education Digital");

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new ProspectiveBusinessSummary
                                {
                                    Parameter = reader["Parameter"]?.ToString(),
                                    Sanction_Amount = reader["Sanction_Amount"] != DBNull.Value
                                        ? Convert.ToDecimal(reader["Sanction_Amount"]) : 0m
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // In your offline environment, replace this with your proper ILogger logging
                AppLogger.LogError(ex, $"Error fetching Prospective Business: {ex.Message}");
            }

            return result;
        }
        // --- NEW METHOD IMPLEMENTATION ---
        public async Task<List<FinancialInclusionSummary>> GetFinancialInclusionSummaryAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new List<FinancialInclusionSummary>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("usp_GetFinancialInclusionSummary_Csv", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // 1. Map the Claim Role to the SP @Scope parameter
                        string scope = "BRANCH"; // Default fallback
                        string upperRole = role?.ToUpper() ?? "";

                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        // 2. Reporting Date formatted strictly for SQL 'DATE' type (yyyy-MM-dd)
                        string fiRpDt = AppTime.Now.ToString("yyyy-MM-dd");
                        fiRpDt = "2026-03-11";
                        // 3. Add secure parameters
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@FiRpDt", fiRpDt);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);

                        // We pass 0 to @UseDecimal to hint to the SP we want FLOAT/INT behavior natively
                        cmd.Parameters.AddWithValue("@UseDecimal", 0);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new FinancialInclusionSummary
                                {
                                    Parameter = reader["Parameter"]?.ToString(),
                                    // Safely cast the returned float/decimal/int into a strict C# integer
                                    Total_Enrollment = reader["Total_Enrollment"] != DBNull.Value
                                        ? Convert.ToInt32(reader["Total_Enrollment"]) : 0
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Financial Inclusion: {ex.Message}");
            }

            return result;
        }
        // --- NEW METHOD IMPLEMENTATION ---
        public async Task<AdvancesPortfolioSummary> GetAdvancesPortfolioSummaryAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new AdvancesPortfolioSummary();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("usp_GetAdvancesPortfolioSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // 1. Map the Claim Role to the SP @Scope parameter
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";

                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        // 2. Date mapped to YYYYMMDD string
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        // 3. Add secure parameters
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1); // 1 = DECIMAL(18,2) natively from SP

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            // Because it's a single row, we use 'if' instead of 'while'
                            if (await reader.ReadAsync())
                            {
                                result.Retail_Amount = reader["RETAIL_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["RETAIL_AMOUNT"]) : 0m;
                                result.Agri_Amount = reader["AGRI_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["AGRI_AMOUNT"]) : 0m;
                                result.MSME_Amount = reader["MSME_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["MSME_AMOUNT"]) : 0m;
                                result.Corporate_Amount = reader["COPORATE_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["COPORATE_AMOUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Advances Portfolio: {ex.Message}");
            }

            return result;
        }
        // --- NEW METHOD IMPLEMENTATION ---
        public async Task<DepositPortfolioSummary> GetDepositPortfolioSummaryAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new DepositPortfolioSummary();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("usp_GetDepositPortfolioSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // 1. Map the Claim Role to the SP @Scope parameter
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";

                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        // 2. Date mapped to YYYYMMDD string
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        // 3. Add secure parameters
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1); // 1 = Return DECIMAL(18,2)

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            // Single row expected
                            if (await reader.ReadAsync())
                            {
                                result.CA_Amount = reader["CA_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["CA_AMOUNT"]) : 0m;
                                result.SB_Amount = reader["SB_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["SB_AMOUNT"]) : 0m;
                                result.TD_Amount = reader["TD_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["TD_AMOUNT"]) : 0m;
                                result.RTD_Amount = reader["RTD_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["RTD_AMOUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Deposit Portfolio: {ex.Message}");
            }

            return result;
        }
        // --- NEW METHOD IMPLEMENTATION ---
        public async Task<DigitalBankingSummary> GetDigitalBankingSummaryAsync(string role, string solId, string regionCd, string zoneCd)
        {
            var result = new DigitalBankingSummary();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("usp_GetDigitalBankingSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // 1. Map the Claim Role to the SP @Scope parameter
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";

                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        // 2. Date mapped to YYYYMMDD string
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260306";
                        // 3. Add secure parameters (Note: Parameter names match the new SP)
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionCd", string.IsNullOrEmpty(regionCd) ? DBNull.Value : regionCd);
                        cmd.Parameters.AddWithValue("@ZoneCd", string.IsNullOrEmpty(zoneCd) ? DBNull.Value : zoneCd);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Debit_Card_Eligible_Count = reader["DEBIT_CARD_ELIGIBLE_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["DEBIT_CARD_ELIGIBLE_COUNT"]) : 0m;
                                result.Debit_Card_Issued_Count = reader["DEBIT_CARD_ISSUED_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["DEBIT_CARD_ISSUED_COUNT"]) : 0m;

                                result.Mobile_Banking_Eligible_Count = reader["MOBILE_BANKING_ELIGIBLE_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["MOBILE_BANKING_ELIGIBLE_COUNT"]) : 0m;
                                result.Mobile_Banking_Issued_Count = reader["MOBILE_BANKING_ISSUED_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["MOBILE_BANKING_ISSUED_COUNT"]) : 0m;

                                result.Internet_Banking_Eligible_Count = reader["INTERNET_BANKING_ELIGIBLE_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["INTERNET_BANKING_ELIGIBLE_COUNT"]) : 0m;
                                result.Internet_Banking_Issued_Count = reader["INTERNET_BANKING_ISSUED_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["INTERNET_BANKING_ISSUED_COUNT"]) : 0m;

                                result.Total_Eligible_Count = reader["TOTAL_ELIGIBLE_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["TOTAL_ELIGIBLE_COUNT"]) : 0m;
                                result.Any_One_Facility_Availed_Count = reader["ANY_ONE_FACILITY_AVAILED_COUNT"] != DBNull.Value ? Convert.ToDecimal(reader["ANY_ONE_FACILITY_AVAILED_COUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Digital Banking: {ex.Message}");
            }

            return result;
        }
        // --- NEW METHOD IMPLEMENTATION ---
        public async Task<OperationsSummary> GetOperationsSummaryAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new OperationsSummary();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (SqlCommand cmd = new SqlCommand("usp_GetOperationsLockerSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // 1. Map the Claim Role to the SP @Scope parameter
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";

                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        // 2. Date mapped to YYYYMMDD string
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        // 3. Add secure parameters (Matches @RegionSolId and @ZoneSolId)
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 0); // We want pure numbers, no decimals needed

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                // Convert raw SQL data into strict C# integers
                                result.Total_Lockers = reader["TOTAL_LOCKERS"] != DBNull.Value ? Convert.ToInt32(reader["TOTAL_LOCKERS"]) : 0;
                                result.Occupied_Lockers = reader["OCCUPIED_LOCKERS"] != DBNull.Value ? Convert.ToInt32(reader["OCCUPIED_LOCKERS"]) : 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Operations: {ex.Message}");
            }

            return result;
        }
        // --- NEW METHOD 1: CURRENT DATA ---
        public async Task<BusinessKpiSummary> GetBusinessKpiCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new BusinessKpiSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetBusinessKpiBusinessSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Business = reader["Business"] != DBNull.Value ? Convert.ToDecimal(reader["Business"]) : 0m;
                                result.Casa = reader["Casa"] != DBNull.Value ? Convert.ToDecimal(reader["Casa"]) : 0m;
                                result.Deposits = reader["Deposits"] != DBNull.Value ? Convert.ToDecimal(reader["Deposits"]) : 0m;
                                result.Advances = reader["Advances"] != DBNull.Value ? Convert.ToDecimal(reader["Advances"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Current KPIs: {ex.Message}"); }
            return result;
        }

        // --- NEW METHOD 2: TARGET DATA ---
        public async Task<BusinessKpiSummary> GetBusinessKpiTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new BusinessKpiSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetBusinessKpiTargetSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Business = reader["Business"] != DBNull.Value ? Convert.ToDecimal(reader["Business"]) : 0m;
                                result.Casa = reader["Casa"] != DBNull.Value ? Convert.ToDecimal(reader["Casa"]) : 0m;
                                result.Deposits = reader["Deposits"] != DBNull.Value ? Convert.ToDecimal(reader["Deposits"]) : 0m;
                                result.Advances = reader["Advances"] != DBNull.Value ? Convert.ToDecimal(reader["Advances"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Target KPIs: {ex.Message}"); }
            return result;
        }
        // --- NEW METHOD 1: ACTUAL DATA ---
        public async Task<TotalAdvancesActual> GetTotalAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalAdvancesActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalAdvancesActualSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";

                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";

                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Actual Advances: {ex.Message}"); }
            return result;
        }

        // --- NEW METHOD 2: TARGET DATA ---
        public async Task<TotalAdvancesTarget> GetTotalAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalAdvancesTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalAdvancesTargetSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "202603";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // Parameter differs here!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Target Advances: {ex.Message}"); }
            return result;
        }
        // --- NEW METHOD 1: DEPOSITS ACTUAL DATA ---
        public async Task<TotalDepositsActual> GetTotalDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalDepositsActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalDepositsActualSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Actual Deposits: {ex.Message}"); }
            return result;
        }

        // --- NEW METHOD 2: DEPOSITS TARGET DATA ---
        public async Task<TotalDepositsTarget> GetTotalDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalDepositsTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalDepositsTargetSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // Note the specific parameter name
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Target Deposits: {ex.Message}"); }
            return result;
        }
        // --- NEW METHOD 1: CURRENT FY GROWTH DATA ---
        public async Task<TotalAdvancesGrowthCurrent> GetAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalAdvancesGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalAdvancesGrowthCurrentFySummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Growth Current: {ex.Message}"); }
            return result;
        }

        // --- NEW METHOD 2: LAST FY GROWTH DATA ---
        public async Task<TotalAdvancesGrowthLast> GetAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalAdvancesGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalAdvancesGrowthLastFySummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Growth Last FY: {ex.Message}"); }
            return result;
        }
        // --- NEW METHOD 1: DEPOSITS CURRENT FY GROWTH DATA ---
        public async Task<TotalDepositsGrowthCurrent> GetDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalDepositsGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalDepositsGrowthCurrentFySummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Deposits Growth Current: {ex.Message}"); }
            return result;
        }

        // --- NEW METHOD 2: DEPOSITS LAST FY GROWTH DATA ---
        public async Task<TotalDepositsGrowthLast> GetDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalDepositsGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalDepositsGrowthLastFySummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Deposits Growth Last FY: {ex.Message}"); }
            return result;
        }
        // --- NEW METHOD IMPLEMENTATION: TOTAL BUSINESS ---
        public async Task<TotalBusinessSummary> GetTotalBusinessSummaryAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TotalBusinessSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTotalBusinessSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = "BRANCH";
                        string upperRole = role?.ToUpper() ?? "";
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260313";
                        if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                        else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                        else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_2_Month_Value = reader["Last_2_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_2_Month_Value"]) : 0m;
                                result.Last_3_Month_Value = reader["Last_3_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_3_Month_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Total Business: {ex.Message}"); }
            return result;
        }
        // --- NEW METHOD IMPLEMENTATION: THIRD PARTY PRODUCTS ---
        public async Task<ThirdPartyProductsSummary> GetThirdPartyProductsAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new ThirdPartyProductsSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Determine scope once
                    string scope = "BRANCH";
                    string upperRole = role?.ToUpper() ?? "";
                    if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                    else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                    else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                    // Execute the 4 SPs cleanly using a helper method
                    result.GeneralInsurance = await FetchSingleTppValueAsync(conn, "usp_GetNonLifeInsuranceSummary", scope, solId, regionSolId, zoneSolId);
                    result.HealthInsurance = await FetchSingleTppValueAsync(conn, "usp_GetHealthInsuranceSummary", scope, solId, regionSolId, zoneSolId);
                    result.LifeInsurance = await FetchSingleTppValueAsync(conn, "usp_GetLifeInsuranceSummary", scope, solId, regionSolId, zoneSolId);
                    result.MutualFunds = await FetchSingleTppValueAsync(conn, "usp_GetMutualFundSummary", scope, solId, regionSolId, zoneSolId);
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching TPP: {ex.Message}"); }

            return result;
        }

        // --- PRIVATE HELPER FOR TPP CALLS ---
        private async Task<decimal> FetchSingleTppValueAsync(SqlConnection conn, string spName, string scope, string solId, string regionSolId, string zoneSolId)
        {
            using (SqlCommand cmd = new SqlCommand(spName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                // Note: Parameter names match the specific TPP Stored Procedures!
                cmd.Parameters.AddWithValue("@Scope", scope);
                cmd.Parameters.AddWithValue("@BranchSolid", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                cmd.Parameters.AddWithValue("@RegionSolid", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                cmd.Parameters.AddWithValue("@ZoneSolid", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);

                // ExecuteScalar grabs just the single value returned by the SP
                var val = await cmd.ExecuteScalarAsync();
                return (val != DBNull.Value && val != null) ? Convert.ToDecimal(val) : 0m;
            }
        }
        // --- NEW METHOD IMPLEMENTATION: PENDING POSITION ---
        public async Task<PendingPositionSummary> GetPendingPositionAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new PendingPositionSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Determine scope once
                    string scope = "BRANCH";
                    string upperRole = role?.ToUpper() ?? "";
                    if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                    else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                    else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                    // 1. Fetch C-KYC Pendency
                    using (SqlCommand cmd = new SqlCommand("usp_GetCKYCPendencySummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@BranchSolid", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolid", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolid", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);

                        var val = await cmd.ExecuteScalarAsync();
                        result.Ckyc_Pendency = (val != DBNull.Value && val != null) ? Convert.ToDecimal(val) : 0m;
                    }

                    // 2. Fetch Re-KYC Pendency
                    using (SqlCommand cmd = new SqlCommand("usp_GetReKYCPendencySummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@BranchSolid", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolid", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolid", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);

                        var val = await cmd.ExecuteScalarAsync();
                        result.ReKyc_Pendency = (val != DBNull.Value && val != null) ? Convert.ToDecimal(val) : 0m;
                    }

                    // 3. Fetch Locker Rent Overdue (Note the different parameter names and the added Date!)
                    using (SqlCommand cmd = new SqlCommand("usp_GetLockerOverdueSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260316";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars required here!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        var val = await cmd.ExecuteScalarAsync();
                        result.Locker_Rent_Overdue = (val != DBNull.Value && val != null) ? Convert.ToDecimal(val) : 0m;
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Pending Position: {ex.Message}"); }

            return result;
        }

        public async Task<StaffSummary> GetStaffSummaryAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new StaffSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string scope = "BRANCH";
                    string upperRole = role?.ToUpper() ?? "";
                    if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                    else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                    else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                    using (SqlCommand cmd = new SqlCommand("usp_GetBranchStaffSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.OfficerCount = reader["OFFICER_COUNT"] != DBNull.Value ? Convert.ToInt32(reader["OFFICER_COUNT"]) : 0;
                                result.CSACount = reader["CSA_COUNT"] != DBNull.Value ? Convert.ToInt32(reader["CSA_COUNT"]) : 0;
                                result.SubStaffCount = reader["SUB_STAFF_COUNT"] != DBNull.Value ? Convert.ToInt32(reader["SUB_STAFF_COUNT"]) : 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Staff Summary: {ex.Message}"); }

            return result;
        }

        public async Task<List<StaffDetail>> GetStaffDetailsAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new List<StaffDetail>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string type = "ALL";
                object? code = DBNull.Value;

                string upperRole = role?.ToUpper() ?? "";

                if (upperRole.Contains("ZONE") || upperRole.Contains("ZO"))
                {
                    type = "SZONEBALL";
                    code = zoneSolId;
                }
                else if (upperRole.Contains("REGION") || upperRole.Contains("RO"))
                {
                    type = "SREGIONBALL";
                    code = regionSolId;
                }
                else if (upperRole.Contains("BRANCH"))
                {
                    type = "SREGIONSB";
                    code = solId;
                }

                using var cmd = new SqlCommand("MD_sp_get_staff_data", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@code", code ?? DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();

                int sno = 1;
                while (await reader.ReadAsync())
                {
                    result.Add(new StaffDetail
                    {
                        SNo = sno++,
                        SolId = reader["branch_code"]?.ToString() ?? "",
                        Zone = reader["zone_name_eng"]?.ToString() ?? "All",
                        Region = reader["region_name"]?.ToString() ?? "All",
                        Branch = reader["BRANCH_NAME"]?.ToString() ?? "All",

                        Clerk = Convert.ToInt32(reader["clerk"] ?? 0),
                        Scale1 = Convert.ToInt32(reader["scale1"] ?? 0),
                        Scale2 = Convert.ToInt32(reader["scale2"] ?? 0),
                        Scale3 = Convert.ToInt32(reader["scale3"] ?? 0),
                        Scale4 = Convert.ToInt32(reader["scale4"] ?? 0),
                        Scale5 = Convert.ToInt32(reader["scale5"] ?? 0),
                        Scale6 = Convert.ToInt32(reader["scale6"] ?? 0),
                        Scale7 = Convert.ToInt32(reader["scale7"] ?? 0),
                        Scale8 = Convert.ToInt32(reader["scale8"] ?? 0),
                        SubStaff = Convert.ToInt32(reader["substaff"] ?? 0)
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Staff Details: {ex.Message}");
            }

            return result;
        }

        public async Task<BranchProfile> GetBranchProfileAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new BranchProfile();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string scope = "BRANCH";
                    string upperRole = role?.ToUpper() ?? "";
                    if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) scope = "CO";
                    else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) scope = "ZONE";
                    else if (upperRole.Contains("REGION") || upperRole.Contains("RO")) scope = "REGION";

                    using (SqlCommand cmd = new SqlCommand("usp_GetBranchProfile", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.SolId = reader["sol_id"]?.ToString() ?? "";
                                result.SolDesc = reader["sol_desc"]?.ToString() ?? "";
                                result.IfscCd = reader["ifsc_cd"]?.ToString() ?? "";
                                result.SolType = reader["sol_type"]?.ToString() ?? "";
                                result.SolOpnDt = reader["sol_opn_dt"] != DBNull.Value ? Convert.ToDateTime(reader["sol_opn_dt"]) : null;
                                result.BranchCategory = reader["branch_category"]?.ToString() ?? "";
                                result.BranchBusinessCategory = reader["branch_business_category"]?.ToString() ?? "";
                                result.LicenseNumber = reader["license_number"]?.ToString() ?? "";
                                result.ZoneName = reader["zone_name"]?.ToString() ?? "";
                                result.AesolZoneCd = reader["aesol_zone_cd"]?.ToString() ?? "";
                                result.RegionName = reader["region_name"]?.ToString() ?? "";
                                result.AesolRegionCd = reader["aesol_region_cd"]?.ToString() ?? "";
                                result.BranchEmail = reader["branch_email"]?.ToString() ?? "";
                                result.MicrCd = reader["micr_cd"]?.ToString() ?? "";
                                result.BranchAddr1 = reader["branch_addr_1"]?.ToString() ?? "";
                                result.BranchAddr2 = reader["branch_addr_2"]?.ToString() ?? "";
                                result.SubDistrictName = reader["sub_district_name"]?.ToString() ?? "";
                                result.DistrictName = reader["district_name"]?.ToString() ?? "";
                                result.CityName = reader["city_name"]?.ToString() ?? "";
                                result.BranchHeadName = reader["branch_head_name"]?.ToString() ?? "";
                                result.BranchHeadPostedSinceDt = reader["branch_head_posted_since_dt"] != DBNull.Value ? Convert.ToDateTime(reader["branch_head_posted_since_dt"]) : null;
                                result.BranchStaffCnt = reader["branch_staff_cnt"] != DBNull.Value ? Convert.ToInt32(reader["branch_staff_cnt"]) : 0;
                                result.BranchOfficersCnt = reader["branch_officers_cnt"] != DBNull.Value ? Convert.ToInt32(reader["branch_officers_cnt"]) : 0;
                                result.BranchClerksCnt = reader["branch_clerks_cnt"] != DBNull.Value ? Convert.ToInt32(reader["branch_clerks_cnt"]) : 0;
                                result.BranchSubStaffCnt = reader["branch_sub_staff_cnt"] != DBNull.Value ? Convert.ToInt32(reader["branch_sub_staff_cnt"]) : 0;
                                result.LastUpdatedDt = reader["last_updated_dt"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_dt"]) : null;

                                // Read lease_validity if available in the result set
                                try { result.LeaseValidity = reader["lease_validity"]?.ToString() ?? ""; } catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Branch Profile: {ex.Message}"); }

            return result;
        }

        public async Task<List<BranchStaffMember>> GetBranchStaffMembersAsync(string role, string solId,string regionSolId, string zoneSolId)
        {
            var result = new List<BranchStaffMember>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string type = "BRANCH";
                    string codeToSend = solId;
                    string upperRole = role?.ToUpper() ?? "";

                    if (upperRole.Contains("CENTRAL") || upperRole.Contains("CO"))
                    {
                        type = "CO";
                        codeToSend = "";
                    }
                    else if (upperRole.Contains("ZONE") || upperRole.Contains("ZO"))
                    {
                        type = "ZONE";
                        codeToSend = zoneSolId;
                    }
                    else if (upperRole.Contains("REGION") || upperRole.Contains("RO"))
                    {
                        type = "REGION";
                        codeToSend = regionSolId;
                    }

                    using (SqlCommand cmd = new SqlCommand("MD_sp_get_bank_contacts", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@type", type);
                        cmd.Parameters.AddWithValue("@code", codeToSend ?? "");

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            int sno = 1;

                            while (await reader.ReadAsync())
                            {
                                result.Add(new BranchStaffMember
                                {
                                    SNo = sno++,
                                    Name = reader["NAME"]?.ToString() ?? "",
                                    Branch = reader["DESCR"]?.ToString() ?? "",  // Branch Name
                                    SolId = reader["BRSOLID"]?.ToString() ?? "",
                                    Region = reader["REGION_NAME"]?.ToString() ?? "",
                                    Zone = reader["DIVISION_NAME"]?.ToString() ?? "",
                                    Designation = reader["EMP_DESGN_DESC"]?.ToString() ?? "",
                                    EmpScaleDescr = reader["EMP_SCALE_DESCR"]?.ToString() ?? "",
                                    ContactNo = reader["PHONE"]?.ToString() ?? "",
                                    Email = reader["EMAIL"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Branch Staff Members: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Fetches staff members with server-side paging and search.
        /// Used for CO/Zone/Region users where record counts are large.
        /// </summary>
        public async Task<(List<BranchStaffMember> Items, int TotalCount)> GetBranchStaffMembersPagedAsync(
            string role, string solId, string regionSolId, string zoneSolId,
            int page, int pageSize, string? searchFilter = null)
        {
            var result = new List<BranchStaffMember>();
            int totalCount = 0;

            try
            {
                // Scope resolution priority: the MOST SPECIFIC identifier that is supplied wins,
                // regardless of the logged-in role string. This is what makes "View Details" honour
                // the SELECTED/CLICKED row instead of always widening to the whole bank for CO users.
                //   • branch  SOL ID present  -> that single branch
                //   • region  SOL ID present  -> all branches in that region
                //   • zone    SOL ID present  -> all branches in that zone (incl. its regions/branches)
                //   • nothing present         -> whole bank (CO)
                // Previously the scope was chosen ONLY from the role, so a CO user viewing a zone-level
                // aggregate row (scale >= 4) always got type="CO" and every zone's staff came back.
                string type;
                string codeToSend;

                if (!string.IsNullOrWhiteSpace(solId))
                { type = "BRANCH"; codeToSend = solId.Trim(); }
                else if (!string.IsNullOrWhiteSpace(regionSolId))
                { type = "REGION"; codeToSend = regionSolId.Trim(); }
                else if (!string.IsNullOrWhiteSpace(zoneSolId))
                { type = "ZONE"; codeToSend = zoneSolId.Trim(); }
                else
                {
                    // No specific scope supplied — whole bank (CO). Only reached for a genuine CO
                    // "All zones/regions/branches" aggregate row.
                    type = "CO";
                    codeToSend = "";
                }

                // Build the base WHERE clause. The scope code is passed as a parameter (@ScopeCode)
                // to avoid SQL injection; the search filter is likewise parameterised (@Search).
                string whereClause = type switch
                {
                    "CO" => "BRSOLID IN (SELECT Sol_ID FROM Branch_Master)",
                    "ZONE" => "BRSOLID IN (SELECT Sol_ID FROM Branch_Master WHERE zone_solid = @ScopeCode)",
                    "REGION" => "BRSOLID IN (SELECT Sol_ID FROM Branch_Master WHERE region_solid = @ScopeCode)",
                    _ => "BRSOLID = (SELECT Sol_Id FROM Branch_Master WHERE branch_code = @ScopeCode)"
                };

                // Add search filter (parameterised)
                bool hasSearch = !string.IsNullOrWhiteSpace(searchFilter);
                if (hasSearch)
                {
                    whereClause += " AND (NAME LIKE @Search OR DESCR LIKE @Search OR EMP_DESGN_DESC LIKE @Search OR BRSOLID LIKE @Search)";
                }

                // Exclude ED/CMD
                whereClause += " AND ISNULL(EMP_SCALE_DESCR,'') <> 'ED/CMD'";

                int offset = page * pageSize;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                void AddScopeParams(SqlCommand c)
                {
                    if (type != "CO")
                        c.Parameters.Add(new SqlParameter("@ScopeCode", SqlDbType.NVarChar) { Value = codeToSend ?? "" });
                    if (hasSearch)
                        c.Parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar) { Value = "%" + searchFilter + "%" });
                }

                // Get total count
                var countSql = $"SELECT COUNT(*) FROM StaffDetails WHERE {whereClause}";
                using (var countCmd = new SqlCommand(countSql, conn))
                {
                    AddScopeParams(countCmd);
                    totalCount = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
                }

                // Get paged data
                var dataSql = $@"SELECT * FROM StaffDetails
                    WHERE {whereClause}
                    ORDER BY CONVERT(NUMERIC, EMP_SCALE_CODE)
                    OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

                using (var cmd = new SqlCommand(dataSql, conn))
                {
                    AddScopeParams(cmd);
                    using var reader = await cmd.ExecuteReaderAsync();
                    int sno = offset + 1;
                    while (await reader.ReadAsync())
                    {
                        result.Add(new BranchStaffMember
                        {
                            SNo = sno++,
                            Name = reader["NAME"]?.ToString() ?? "",
                            Branch = reader["DESCR"]?.ToString() ?? "",
                            SolId = reader["BRSOLID"]?.ToString() ?? "",
                            Region = reader["REGION_NAME"]?.ToString() ?? "",
                            Zone = reader["DIVISION_NAME"]?.ToString() ?? "",
                            Designation = reader["EMP_DESGN_DESC"]?.ToString() ?? "",
                            EmpScaleDescr = reader["EMP_SCALE_DESCR"]?.ToString() ?? "",
                            ContactNo = reader["PHONE"]?.ToString() ?? "",
                            Email = reader["EMAIL"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching paged staff members: {ex.Message}");
            }

            return (result, totalCount);
        }

        /// <summary>
        /// Fetches staff members for a specific branch by its branch_code.
        /// Used when clicking "View" on a specific branch row in Staff Details.
        /// </summary>
        public async Task<List<BranchStaffMember>> GetBranchStaffMembersAsync(string branchCode)
        {
            var result = new List<BranchStaffMember>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("MD_sp_get_bank_contacts", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@type", "BRANCH");
                cmd.Parameters.AddWithValue("@code", branchCode ?? "");

                using var reader = await cmd.ExecuteReaderAsync();
                int sno = 1;
                while (await reader.ReadAsync())
                {
                    result.Add(new BranchStaffMember
                    {
                        SNo = sno++,
                        Name = reader["NAME"]?.ToString() ?? "",
                        Branch = reader["DESCR"]?.ToString() ?? "",
                        SolId = reader["BRSOLID"]?.ToString() ?? "",
                        Region = reader["REGION_NAME"]?.ToString() ?? "",
                        Zone = reader["DIVISION_NAME"]?.ToString() ?? "",
                        Designation = reader["EMP_DESGN_DESC"]?.ToString() ?? "",
                        EmpScaleDescr = reader["EMP_SCALE_DESCR"]?.ToString() ?? "",
                        ContactNo = reader["PHONE"]?.ToString() ?? "",
                        Email = reader["EMAIL"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Branch Staff Members by code: {ex.Message}");
            }
            return result;
        }

        public async Task<EntityCountSummary> GetEntityCountSummaryAsync()
        {
            var result = new EntityCountSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetNetworkOverviewCounts", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.ZoneCount = reader["zone_count"] != DBNull.Value ? Convert.ToInt32(reader["zone_count"]) : 0;
                                result.RegionCount = reader["region_count"] != DBNull.Value ? Convert.ToInt32(reader["region_count"]) : 0;
                                result.BranchCount = reader["branch_count"] != DBNull.Value ? Convert.ToInt32(reader["branch_count"]) : 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Entity Count Summary: {ex.Message}"); }
            return result;
        }

        #region Advances - Retail
        // 1. RETAIL PERFORMANCE - ACTUAL
        public async Task<RetailAdvancesActual> GetRetailAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new RetailAdvancesActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetRetailAdvancesCurrentSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string scope = GetScopeFromRole(role); // Using a cleaner inline evaluation
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.Parameters.AddWithValue("@Scope", scope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Retail Actual: {ex.Message}"); }
            return result;
        }

        // 2. RETAIL PERFORMANCE - TARGET
        public async Task<RetailAdvancesTarget> GetRetailAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new RetailAdvancesTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetRetailAdvancesTargetSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Retail Target: {ex.Message}"); }
            return result;
        }

        // 3. RETAIL PORTFOLIO
        public async Task<RetailAdvancesPortfolio> GetRetailAdvancesPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new RetailAdvancesPortfolio();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetRetailAdvancesPortfolioSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Education_Amount = reader["EDUCATION_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["EDUCATION_AMOUNT"]) : 0m;
                                result.Home_Amount = reader["HOME_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["HOME_AMOUNT"]) : 0m;
                                result.Vehicle_Amount = reader["VEHICLE_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["VEHICLE_AMOUNT"]) : 0m;
                                result.Mortage_Amount = reader["MORTAGE_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["MORTAGE_AMOUNT"]) : 0m;
                                result.Personal_Amount = reader["PERSONAL_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["PERSONAL_AMOUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Retail Portfolio: {ex.Message}"); }
            return result;
        }

        // 4. RETAIL OUTSTANDING BOOK - CURRENT
        public async Task<RetailAdvancesGrowthCurrent> GetRetailAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new RetailAdvancesGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetRetailAdvancesOutstandingBookSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Retail Growth Current: {ex.Message}"); }
            return result;
        }

        // 5. RETAIL OUTSTANDING BOOK - LAST FY
        public async Task<RetailAdvancesGrowthLast> GetRetailAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new RetailAdvancesGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetRetailAdvancesOutstandingBookLastFySummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Retail Growth Last FY: {ex.Message}"); }
            return result;
        }
        // ==========================================
        // --- RETAIL ADVANCES DISBURSEMENT IMPLEMENTATIONS ---
        // ==========================================

        public async Task<DepositsOpenedSummary> GetRetailAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetRetailAdvancesDisbursement_NOA", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetRetailAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetRetailAdvancesDisbursement_AMT", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }
        // --- Helper Method to keep code DRY ---
        private string GetScopeFromRole(string role)
        {
            string upperRole = role?.ToUpper() ?? "";
            if (upperRole.Contains("CO") || upperRole.Contains("CENTRAL")) return "CO";
            if (upperRole.Contains("ZONE") || upperRole.Contains("ZO")) return "ZONE";
            if (upperRole.Contains("REGION") || upperRole.Contains("RO")) return "REGION";
            return "BRANCH";
        }
        #endregion
        #region Advances - MSME
        // ==========================================
        // --- MSME ADVANCES IMPLEMENTATIONS ---
        // ==========================================

        // 1. MSME PERFORMANCE - ACTUAL
        public async Task<MsmeAdvancesActual> GetMsmeAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new MsmeAdvancesActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetMSMEAdvancesCurrentSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching MSME Actual: {ex.Message}"); }
            return result;
        }

        // 2. MSME PERFORMANCE - TARGET
        public async Task<MsmeAdvancesTarget> GetMsmeAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new MsmeAdvancesTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetMSMEAdvancesTargetSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching MSME Target: {ex.Message}"); }
            return result;
        }

        // 3. MSME PORTFOLIO
        public async Task<MsmeAdvancesPortfolio> GetMsmeAdvancesPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new MsmeAdvancesPortfolio();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetMSMEAdvancesPortfolioSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Micro_Amount = reader["MICRO_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["MICRO_AMOUNT"]) : 0m;
                                result.Small_Amount = reader["SMALL_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["SMALL_AMOUNT"]) : 0m;
                                result.Medium_Amount = reader["MEDIUM_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["MEDIUM_AMOUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching MSME Portfolio: {ex.Message}"); }
            return result;
        }

        // 4. MSME OUTSTANDING BOOK - CURRENT
        public async Task<MsmeAdvancesGrowthCurrent> GetMsmeAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new MsmeAdvancesGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetMSMEAdvancesOutstandingCurrentFYSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching MSME Growth Current: {ex.Message}"); }
            return result;
        }

        // 5. MSME OUTSTANDING BOOK - LAST FY
        public async Task<MsmeAdvancesGrowthLast> GetMsmeAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new MsmeAdvancesGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetMSMEAdvancesLastYearSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching MSME Growth Last FY: {ex.Message}"); }
            return result;
        }
        // ==========================================
        // --- MSME ADVANCES DISBURSEMENT IMPLEMENTATIONS ---
        // ==========================================

        public async Task<DepositsOpenedSummary> GetMsmeAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetMSMEAdvancesDisbursement_NOA", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetMsmeAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetMSMEAdvancesDisbursement_AMT", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }
        #endregion
        #region Advances - Agriculture
        // ==========================================
        // --- AGRICULTURE ADVANCES IMPLEMENTATIONS ---
        // ==========================================

        // 1. AGRICULTURE PERFORMANCE - ACTUAL
        public async Task<AgriAdvancesActual> GetAgriAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new AgriAdvancesActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetAgricultureAdvancesCurrentSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Agri Actual: {ex.Message}"); }
            return result;
        }

        // 2. AGRICULTURE PERFORMANCE - TARGET
        public async Task<AgriAdvancesTarget> GetAgriAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new AgriAdvancesTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetAgricultureAdvancesTargetSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Agri Target: {ex.Message}"); }
            return result;
        }

        // 3. AGRICULTURE OUTSTANDING BOOK - CURRENT
        public async Task<AgriAdvancesGrowthCurrent> GetAgriAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new AgriAdvancesGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetAgricultureAdvancesOutstandingCurrentFYSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Agri Growth Current: {ex.Message}"); }
            return result;
        }

        // 4. AGRICULTURE OUTSTANDING BOOK - LAST FY
        public async Task<AgriAdvancesGrowthLast> GetAgriAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new AgriAdvancesGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetAgricultureAdvancesLastYearSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Agri Growth Last FY: {ex.Message}"); }
            return result;
        }
        // ==========================================
        // --- AGRICULTURE ADVANCES DISBURSEMENT IMPLEMENTATIONS ---
        // ==========================================

        public async Task<DepositsOpenedSummary> GetAgriAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetAgricultureAdvancesDisbursement_NOA", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetAgriAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetAgricultureAdvancesDisbursement_AMT", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }
        #endregion
        #region Advances - Corporate
        // ==========================================
        // --- CORPORATE ADVANCES IMPLEMENTATIONS ---
        // ==========================================

        // 1. CORPORATE PERFORMANCE - ACTUAL
        public async Task<CorpAdvancesActual> GetCorpAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CorpAdvancesActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCorporateAdvancesCurrentSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Corp Actual: {ex.Message}"); }
            return result;
        }

        // 2. CORPORATE PERFORMANCE - TARGET
        public async Task<CorpAdvancesTarget> GetCorpAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CorpAdvancesTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCorporateAdvancesTargetSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Corp Target: {ex.Message}"); }
            return result;
        }

        // 3. CORPORATE OUTSTANDING BOOK - CURRENT
        public async Task<CorpAdvancesGrowthCurrent> GetCorpAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CorpAdvancesGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCorporateAdvancesOutstandingCurrentFYSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Corp Growth Current: {ex.Message}"); }
            return result;
        }

        // 4. CORPORATE OUTSTANDING BOOK - LAST FY
        public async Task<CorpAdvancesGrowthLast> GetCorpAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CorpAdvancesGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCorporateAdvancesLastYearSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Corp Growth Last FY: {ex.Message}"); }
            return result;
        }
        #endregion
        #region Advances - Total Advances
        // ==========================================
        // --- TOTAL ADVANCES DISBURSEMENT IMPLEMENTATIONS ---
        // ==========================================

        public async Task<DepositsOpenedSummary> GetAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetAdvancesDisbursement4MonthTrend", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchAdvancesDisbursementAsync("usp_GetAdvancesDisbursement4MonthTrend_AMT", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        // --- Private Helper to Execute Both SPs cleanly ---
        private async Task<DepositsOpenedSummary> FetchAdvancesDisbursementAsync(string spName, string formattedScope, string solId, string regionSolId, string zoneSolId)
        {
            var result = new DepositsOpenedSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(spName, conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260329";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", formattedScope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_2_Month_Value = reader["Last_2_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_2_Month_Value"]) : 0m;
                                result.Last_3_Month_Value = reader["Last_3_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_3_Month_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching {spName}: {ex.Message}"); }
            return result;
        }
        #endregion
        #region Deposits - Opened
        // ==========================================
        // --- DEPOSITS OPENED IMPLEMENTATIONS ---
        // ==========================================
        public async Task<DepositsOpenedSummary> GetDepositsOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchDepositsOpenedAsync("usp_GetDepositsAccountsOpenedSummary", role, solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetDepositsOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchDepositsOpenedAsync("usp_GetDepositsOutstandingBalanceSummary", role, solId, regionSolId, zoneSolId);
        }

        // --- Private Helper to Execute Both SPs ---
        private async Task<DepositsOpenedSummary> FetchDepositsOpenedAsync(string spName, string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new DepositsOpenedSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(spName, conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_2_Month_Value = reader["Last_2_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_2_Month_Value"]) : 0m;
                                result.Last_3_Month_Value = reader["Last_3_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_3_Month_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching {spName}: {ex.Message}"); }
            return result;
        }
        #endregion
        #region Deposits - Savings
        // ==========================================
        // --- SAVINGS DEPOSITS IMPLEMENTATIONS ---
        // ==========================================

        // 1. SAVINGS PERFORMANCE - ACTUAL
        public async Task<SavingsDepositsActual> GetSavingsDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new SavingsDepositsActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetSavingsDepositsCurrentSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Savings Actual: {ex.Message}"); }
            return result;
        }

        // 2. SAVINGS PERFORMANCE - TARGET
        public async Task<SavingsDepositsTarget> GetSavingsDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new SavingsDepositsTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetSavingsDepositsTargetSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Savings Target: {ex.Message}"); }
            return result;
        }

        // 3. SAVINGS DEPOSITS BOOK - CURRENT
        public async Task<SavingsDepositsGrowthCurrent> GetSavingsDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new SavingsDepositsGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetSavingsDepositsBookCurrentFYSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Savings Growth Current: {ex.Message}"); }
            return result;
        }

        // 4. SAVINGS DEPOSITS BOOK - LAST FY
        public async Task<SavingsDepositsGrowthLast> GetSavingsDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new SavingsDepositsGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetSavingsDepositsLastYearSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Savings Growth Last FY: {ex.Message}"); }
            return result;
        }
        // ==========================================
        // --- SAVINGS OPENED IMPLEMENTATIONS ---
        // ==========================================

        public async Task<DepositsOpenedSummary> GetSavingsOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            // UPDATED: Now uses the standard GetScopeFromRole since the SP was fixed!
            return await FetchSavingsOpenedAsync("usp_GetSavingsAccountsOpenedSummary", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetSavingsOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchSavingsOpenedAsync("usp_GetSavingsOutstandingBalanceSummary", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        // --- Private Helper to Execute Both SPs cleanly ---
        private async Task<DepositsOpenedSummary> FetchSavingsOpenedAsync(string spName, string formattedScope, string solId, string regionSolId, string zoneSolId)
        {
            var result = new DepositsOpenedSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(spName, conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", formattedScope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_2_Month_Value = reader["Last_2_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_2_Month_Value"]) : 0m;
                                result.Last_3_Month_Value = reader["Last_3_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_3_Month_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching {spName}: {ex.Message}"); }
            return result;
        }
        #endregion
        #region Deposits - Current
        // ==========================================
        // --- CURRENT DEPOSITS IMPLEMENTATIONS ---
        // ==========================================

        // 1. CURRENT DEPOSITS PERFORMANCE - ACTUAL
        public async Task<CurrentDepositsActual> GetCurrentDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CurrentDepositsActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCurrentDepositsCurrentSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Current Deposits Actual: {ex.Message}"); }
            return result;
        }

        // 2. CURRENT DEPOSITS PERFORMANCE - TARGET
        public async Task<CurrentDepositsTarget> GetCurrentDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CurrentDepositsTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCurrentDepositsTargetSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Current Deposits Target: {ex.Message}"); }
            return result;
        }

        // 3. CURRENT DEPOSITS BOOK - CURRENT FY
        public async Task<CurrentDepositsGrowthCurrent> GetCurrentDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CurrentDepositsGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCurrentDepositsBookCurrentFYSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Current Deposits Growth Current: {ex.Message}"); }
            return result;
        }

        // 4. CURRENT DEPOSITS BOOK - LAST FY
        public async Task<CurrentDepositsGrowthLast> GetCurrentDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new CurrentDepositsGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetCurrentDepositsLastYearSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Current Deposits Growth Last FY: {ex.Message}"); }
            return result;
        }
        // ==========================================
        // --- CURRENT OPENED IMPLEMENTATIONS ---
        // ==========================================

        public async Task<DepositsOpenedSummary> GetCurrentOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchCurrentOpenedAsync("usp_GetCurrentAccountsOpenedSummary", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetCurrentOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchCurrentOpenedAsync("usp_GetCurrentOutstandingBalanceSummary", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        // --- Private Helper to Execute Both SPs cleanly ---
        private async Task<DepositsOpenedSummary> FetchCurrentOpenedAsync(string spName, string formattedScope, string solId, string regionSolId, string zoneSolId)
        {
            var result = new DepositsOpenedSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(spName, conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", formattedScope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_2_Month_Value = reader["Last_2_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_2_Month_Value"]) : 0m;
                                result.Last_3_Month_Value = reader["Last_3_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_3_Month_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching {spName}: {ex.Message}"); }
            return result;
        }
        #endregion
        #region Deposits - Term Deposits
        // ==========================================
        // --- TERM DEPOSITS IMPLEMENTATIONS ---
        // ==========================================

        // 1. TERM DEPOSITS PERFORMANCE - ACTUAL
        public async Task<TermDepositsActual> GetTermDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TermDepositsActual();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTermDepositsCurrentSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Actual_Total = reader["Actual_Total"] != DBNull.Value ? Convert.ToDecimal(reader["Actual_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Term Deposits Actual: {ex.Message}"); }
            return result;
        }

        // 2. TERM DEPOSITS PERFORMANCE - TARGET
        public async Task<TermDepositsTarget> GetTermDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TermDepositsTarget();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTermDepositsTargetSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Target = reader["TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["TARGET"]) : 0m;
                                result.Last_Target = reader["LAST_TARGET"] != DBNull.Value ? Convert.ToDecimal(reader["LAST_TARGET"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Term Deposits Target: {ex.Message}"); }
            return result;
        }

        // 3. TERM DEPOSITS PORTFOLIO
        public async Task<TermDepositsPortfolio> GetTermDepositsPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TermDepositsPortfolio();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTermDepositsPortfolioSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Bulk_Amount = reader["BULK_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["BULK_AMOUNT"]) : 0m;
                                result.Retail_Amount = reader["RETAIL_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["RETAIL_AMOUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Term Deposits Portfolio: {ex.Message}"); }
            return result;
        }

        // 4. TERM DEPOSITS BOOK - CURRENT FY
        public async Task<TermDepositsGrowthCurrent> GetTermDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TermDepositsGrowthCurrent();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTermDepositsBookCurrentFYSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260324";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Term Deposits Growth Current: {ex.Message}"); }
            return result;
        }

        // 5. TERM DEPOSITS BOOK - LAST FY
        public async Task<TermDepositsGrowthLast> GetTermDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new TermDepositsGrowthLast();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetTermDepositsLastYearSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMM");
                        yearMonthDay = "202603";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@EkamTrgtRpDt", yearMonthDay); // 6 Chars!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Last_Year_Value = reader["Last_Year_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Year_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Term Deposits Growth Last FY: {ex.Message}"); }
            return result;
        }
        // ==========================================
        // --- TERM DEPOSITS OPENED IMPLEMENTATIONS ---
        // ==========================================

        public async Task<DepositsOpenedSummary> GetTermDepositsOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchTermDepositsOpenedAsync("usp_GetTermDepositsAccountsOpenedSummary", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        public async Task<DepositsOpenedSummary> GetTermDepositsOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            return await FetchTermDepositsOpenedAsync("usp_GetTermDepositsOutstandingBalanceSummary", GetScopeFromRole(role), solId, regionSolId, zoneSolId);
        }

        // --- Private Helper to Execute Both SPs cleanly ---
        private async Task<DepositsOpenedSummary> FetchTermDepositsOpenedAsync(string spName, string formattedScope, string solId, string regionSolId, string zoneSolId)
        {
            var result = new DepositsOpenedSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(spName, conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260321";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", formattedScope);
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_2_Month_Value = reader["Last_2_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_2_Month_Value"]) : 0m;
                                result.Last_3_Month_Value = reader["Last_3_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_3_Month_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching {spName}: {ex.Message}"); }
            return result;
        }
        #endregion
        #region NPA - Total NPA
        // ==========================================
        // --- NPA IMPLEMENTATIONS ---
        // ==========================================

        // 1. NPA PERFORMANCE
        public async Task<NpaPerformance> GetNpaPerformanceAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new NpaPerformance();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetNPAPerformanceSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260326";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay); // 8 Chars
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.NPA_Total = reader["NPA_Total"] != DBNull.Value ? Convert.ToDecimal(reader["NPA_Total"]) : 0m;
                                result.ADVANCES_Total = reader["ADVANCES_Total"] != DBNull.Value ? Convert.ToDecimal(reader["ADVANCES_Total"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching NPA Performance: {ex.Message}"); }
            return result;
        }

        // 2. NPA PORTFOLIO
        public async Task<NpaPortfolio> GetNpaPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new NpaPortfolio();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetNPAPortfolioSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260326";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.MSME_AMOUNT = reader["MSME_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["MSME_AMOUNT"]) : 0m;
                                result.AGRICULTURE_AMOUNT = reader["AGRICULTURE_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["AGRICULTURE_AMOUNT"]) : 0m;
                                result.RETAIL_AMOUNT = reader["RETAIL_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["RETAIL_AMOUNT"]) : 0m;
                                result.CORP_AMOUNT = reader["CORP_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["CORP_AMOUNT"]) : 0m;
                                result.OTHERS_AMOUNT = reader["OTHERS_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["OTHERS_AMOUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching NPA Portfolio: {ex.Message}"); }
            return result;
        }

        // 3. NPA BOOK
        public async Task<NpaBook> GetNpaBookAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new NpaBook();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetNPABookSummary", conn))
                    {
                        string yearMonthDay = AppTime.Now.ToString("yyyyMMdd");
                        yearMonthDay = "20260326";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@YearMonthDay", yearMonthDay);
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Value = reader["Current_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching NPA Book: {ex.Message}"); }
            return result;
        }
        #endregion
        #region Profitability - Fee Income
        // ==========================================
        // --- PROFITABILITY IMPLEMENTATIONS ---
        // ==========================================

        // 1. PROFITABILITY MOM
        public async Task<ProfitabilityMomSummary> GetProfitabilityFeeIncomeMomAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new ProfitabilityMomSummary();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetProfitabilityFeeIncomeMoMSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        // No Date Parameter required for this SP!
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.Current_Month_Value = reader["Current_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Current_Month_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_2nd_Month_Value = reader["Last_2nd_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_2nd_Month_Value"]) : 0m;
                                result.Last_3rd_Month_Value = reader["Last_3rd_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_3rd_Month_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Profitability MOM: {ex.Message}"); }
            return result;
        }

        // 2. PROFITABILITY PORTFOLIO
        public async Task<ProfitabilityPortfolio> GetProfitabilityPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new ProfitabilityPortfolio();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetProfitabilityPortfolioSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.FEE_AMOUNT = reader["FEE_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["FEE_AMOUNT"]) : 0m;
                                result.INTEREST_AMOUNT = reader["INTEREST_AMOUNT"] != DBNull.Value ? Convert.ToDecimal(reader["INTEREST_AMOUNT"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Profitability Portfolio: {ex.Message}"); }
            return result;
        }

        // 3. PROFITABILITY PROFIT BOOK
        public async Task<ProfitabilityProfitBook> GetProfitabilityProfitBookAsync(string role, string solId, string regionSolId, string zoneSolId)
        {
            var result = new ProfitabilityProfitBook();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetProfitabilityFeeIncomeTrendSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Scope", GetScopeFromRole(role));
                        cmd.Parameters.AddWithValue("@SolId", string.IsNullOrEmpty(solId) ? DBNull.Value : solId);
                        cmd.Parameters.AddWithValue("@RegionSolId", string.IsNullOrEmpty(regionSolId) ? DBNull.Value : regionSolId);
                        cmd.Parameters.AddWithValue("@ZoneSolId", string.IsNullOrEmpty(zoneSolId) ? DBNull.Value : zoneSolId);
                        cmd.Parameters.AddWithValue("@UseDecimal", 1);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.As_On_Value = reader["As_On_Value"] != DBNull.Value ? Convert.ToDecimal(reader["As_On_Value"]) : 0m;
                                result.Last_Month_Value = reader["Last_Month_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Month_Value"]) : 0m;
                                result.Last_Quater_Value = reader["Last_Quater_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_Quater_Value"]) : 0m;
                                result.Last_FY_Value = reader["Last_FY_Value"] != DBNull.Value ? Convert.ToDecimal(reader["Last_FY_Value"]) : 0m;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Profitability Profit Book: {ex.Message}"); }
            return result;
        }
        #endregion
        #region Filters
        // ==========================================
        // --- HIERARCHY DROPDOWN IMPLEMENTATIONS ---
        // ==========================================

        public async Task<List<ZoneDto>> GetZonesAsync()
        {
            var result = new List<ZoneDto>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetZones", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new ZoneDto
                                {
                                    zone_solid = reader["zone_solid"].ToString(),
                                    zone_code = reader["zone_code"].ToString(),
                                    zone_name_eng = reader["zone_name_eng"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Zones: {ex.Message}"); }
            return result;
        }

        public async Task<List<RegionDto>> GetRegionsByZoneAsync(string zoneSolid)
        {
            var result = new List<RegionDto>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetRegionsByZone", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ZoneSolid", string.IsNullOrEmpty(zoneSolid) ? DBNull.Value : zoneSolid);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new RegionDto
                                {
                                    region_solid = reader["region_solid"].ToString(),
                                    region_code = reader["region_code"].ToString(),
                                    region_name = reader["region_name"].ToString(),
                                    zone_solid = reader["zone_solid"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Regions: {ex.Message}"); }
            return result;
        }

        public async Task<List<BranchDto>> GetBranchesByRegionAsync(string regionSolid)
        {
            var result = new List<BranchDto>();
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("usp_GetBranchesByRegion", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@RegionSolid", string.IsNullOrEmpty(regionSolid) ? DBNull.Value : regionSolid);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new BranchDto
                                {
                                    SOL_ID = reader["SOL_ID"].ToString(),
                                    branch_code = reader["branch_code"].ToString(),
                                    branch_name = reader["branch_name"].ToString(),
                                    region_solid = reader["region_solid"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, $"Error fetching Branches: {ex.Message}"); }
            return result;
        }
        #endregion


        public async Task<List<StaffDetailsUnionHub>> GetBranchStaffDataAsync()
        {
            var result = new List<StaffDetailsUnionHub>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();


                    using (SqlCommand cmd = new SqlCommand("select * from staffdetails", conn))
                    {

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            int sno = 1;

                            while (await reader.ReadAsync())
                            {
                                result.Add(new StaffDetailsUnionHub
                                {
                                    EmpCode = reader["EMPLID"]?.ToString() ?? "",
                                    Name = reader["NAME"]?.ToString() ?? "",
                                    Branch = reader["DESCR"]?.ToString() ?? "",  // Branch Name
                                    SolId = reader["BRSOLID"]?.ToString() ?? "",
                                    Region = reader["REGION_NAME"]?.ToString() ?? "",
                                    Zone = reader["DIVISION_NAME"]?.ToString() ?? "",
                                    Designation = reader["EMP_DESGN_DESC"]?.ToString() ?? "",
                                    EmpScaleDescr = reader["EMP_SCALE_DESCR"]?.ToString() ?? "",
                                    ContactNo = reader["PHONE"]?.ToString() ?? "",
                                    Email = reader["EMAIL"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"Error fetching Branch Staff Members: {ex.Message}");
            }

            return result;
        }
    }
}
