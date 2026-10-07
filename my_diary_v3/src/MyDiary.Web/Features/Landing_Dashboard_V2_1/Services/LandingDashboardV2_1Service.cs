using MyDiary.Web.Core.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using MyDiary.Web.Features.Landing_Dashboard_V2_1.Models;
using System.Data;

namespace MyDiary.Web.Features.Landing_Dashboard_V2_1.Services
{
    public class LandingDashboardV2_1Service : ILandingDashboardV2_1Service
    {
        private readonly string _connectionString;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public LandingDashboardV2_1Service(IConfiguration configuration, IMemoryCache cache)
        {
            _connectionString = configuration.GetConnectionString("SQLServerConnection")!;
            _cache = cache;
        }

        /// <summary>
        /// Resolves the correct SOL ID to pass to SP_PAGEONE_DASHBOARD based on privilege type:
        ///   CO / Branch  → Branch SOL ID
        ///   Region       → Region SOL ID
        ///   Zone         → Zone SOL ID
        /// </summary>
        private static int ResolveSolId(string role, string brSolId, string regionSolId, string zoneSolId)
        {
            string upper = role?.ToUpper() ?? "";

            if (upper.Contains("ZONE") || upper.Contains("ZO"))
            {
                if (int.TryParse(zoneSolId, out int zoneId)) return zoneId;
            }
            else if (upper.Contains("REGION") || upper.Contains("RO"))
            {
                if (int.TryParse(regionSolId, out int regionId)) return regionId;
            }

            // Default: CO or Branch → use branch SOL ID
            if (int.TryParse(brSolId, out int branchId)) return branchId;

            return 0;
        }

        // SP_PAGEONE_DASHBOARD reflects an "AS ON DATE" snapshot that's refreshed by a
        // periodic data-load job, not real-time transactional data — safe to cache briefly
        // so every page load/refresh from every user on the same SOL doesn't re-run the
        // same heavy multi-result-set stored procedure call.
        public async Task<PageOneDashboardResult> GetPageOneDashboardAsync(
            string role, string brSolId, string regionSolId, string zoneSolId)
        {
            int cacheSolId = ResolveSolId(role, brSolId, regionSolId, zoneSolId);
            string cacheKey = $"PageOneDashboard_{cacheSolId}";

            if (_cache.TryGetValue(cacheKey, out PageOneDashboardResult? cached) && cached != null)
                return cached;

            var fresh = await GetPageOneDashboardCoreAsync(role, brSolId, regionSolId, zoneSolId);
            _cache.Set(cacheKey, fresh, CacheDuration);
            return fresh;
        }

        private async Task<PageOneDashboardResult> GetPageOneDashboardCoreAsync(
            string role, string brSolId, string regionSolId, string zoneSolId)
        {
            var result = new PageOneDashboardResult();
            int solId = ResolveSolId(role, brSolId, regionSolId, zoneSolId);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_PAGEONE_DASHBOARD", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };
                cmd.Parameters.Add(new SqlParameter("@SOL_ID", SqlDbType.Int) { Value = solId });

                using var reader = await cmd.ExecuteReaderAsync();

                // ── Result Set 0: AS_ON_DATE ──────────────────────────────────────────
                if (await reader.ReadAsync())
                {
                    result.AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                        ? Convert.ToDateTime(reader["AS_ON_DATE"])
                        : (DateTime?)null;
                }

                // ── Result Set 1: Chart 1 – Business KPIs ────────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart1 = new Chart1Kpi
                    {
                        Deposits = GetDecimal(reader, "deposits"),
                        Casa     = GetDecimal(reader, "casa"),
                        Advances = GetDecimal(reader, "advances"),
                        Business = GetDecimal(reader, "business")
                    };
                }

                // ── Result Set 2: Chart 2 – Advances Performance ─────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart2 = new ChartPerformance
                    {
                        CurrentFy   = GetDecimal(reader, "CURRENT_FY"),
                        TargetFy    = GetDecimal(reader, "TARGET_FY"),
                        ActualTotal = GetDecimal(reader, "ACTUAL_TOTAL"),
                        ScaleText   = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── Result Set 3: Chart 3 – Advances Growth ──────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart3 = new ChartGrowth
                    {
						Base        = GetDecimal(reader, "base"),
						LastFy      = GetDecimal(reader, "LAST_FY"),
                        LastQuarter = GetDecimal(reader, "LAST_QUARTER"),
                        LastMonth   = GetDecimal(reader, "LAST_MONTH"),
                        AsOn        = GetDecimal(reader, "AS_ON")
                    };
                }

                // ── Result Set 4: Chart 4 – Advances Portfolio ───────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart4 = new Chart4Portfolio
                    {
                        MsmeAdvances        = GetDecimal(reader, "MSME_ADVANCES"),
                        RetailAdvances      = GetDecimal(reader, "RETAIL_ADVANCES"),
                        AgricultureAdvances = GetDecimal(reader, "AGRICULTURE_ADVANCES"),
                        CorporateAdvances   = GetDecimal(reader, "CORPORATE_ADVANCES")
                    };
                }

                // ── Result Set 5: Chart 5 – Total Business ───────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart5 = new ChartGrowth
                    {
                        LastFy      = GetDecimal(reader, "LAST_FY"),
                        LastQuarter = GetDecimal(reader, "LAST_QUARTER"),
                        LastMonth   = GetDecimal(reader, "LAST_MONTH"),
                        AsOn        = GetDecimal(reader, "AS_ON")
                    };
                }

                // ── Result Set 6: Chart 6 – Deposits Performance ─────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart6 = new ChartPerformance
                    {
                        CurrentFy   = GetDecimal(reader, "CURRENT_FY"),
                        TargetFy    = GetDecimal(reader, "TARGET_FY"),
                        ActualTotal = GetDecimal(reader, "ACTUAL_TOTAL"),
                        ScaleText   = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── Result Set 7: Chart 7 – Deposits Growth ──────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart7 = new ChartGrowth
                    {
						Base = GetDecimal(reader, "base"),
						LastFy      = GetDecimal(reader, "LAST_FY"),
                        LastQuarter = GetDecimal(reader, "LAST_QUARTER"),
                        LastMonth   = GetDecimal(reader, "LAST_MONTH"),
                        AsOn        = GetDecimal(reader, "AS_ON")
                    };
                }

                // ── Result Set 8: Chart 8 – Deposits Portfolio ───────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Chart8 = new Chart8Portfolio
                    {
                        CdDeposits         = GetDecimal(reader, "CD_DEPOSITS"),
                        SbDeposits         = GetDecimal(reader, "SB_DEPOSITS"),
                        TdDeposits         = GetDecimal(reader, "TD_DEPOSITS"),
                        RetailTermDeposits = GetDecimal(reader, "RETAIL_TERM_DEPOSITS")
                    };
                }

                // ── Result Set 9: Additional data (PARAMETER_ID 27-50) ───────────────
                if (await reader.NextResultAsync())
                {
					while (await reader.ReadAsync())
					{
						var paramName = reader["PARAMETER_NAME"]?.ToString() ?? "";

						if (string.IsNullOrWhiteSpace(paramName))
							continue;

						result.AdditionalData[paramName] = new AdditionalDataItem
						{
							Value = GetDecimal(reader, "ACTUALS_DATA"),
							AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
										? Convert.ToDateTime(reader["AS_ON_DATE"])
										: null,
							CardId = reader["CARD_ID"] != DBNull.Value
										? Convert.ToInt32(reader["CARD_ID"])
										: 0
						};
					}
				}
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"[LandingDashboardV2_1] SP_PAGEONE_DASHBOARD error: {ex.Message}");
            }

            return result;
        }

        private static decimal GetDecimal(SqlDataReader reader, string column)
        {
            try
            {
                return reader[column] != DBNull.Value ? Convert.ToDecimal(reader[column]) : 0m;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[LandingDashboardV2_1Service] Unhandled error: " + ex.Message);
                return 0m;
            }
        }
    }
}
