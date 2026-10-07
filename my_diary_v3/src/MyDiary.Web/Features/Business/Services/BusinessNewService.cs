using MyDiary.Web.Core.Extensions;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Features.Business.Models;
using MyDiary.Web.Features.Landing_Dashboard_V2.Models;
using System.Data;

namespace MyDiary.Web.Features.Business.Services
{
    /// <inheritdoc />
    public class BusinessNewService : IBusinessNewService
    {
        private readonly string _connectionString;
        private readonly ILogger<BusinessNewService> _logger;

        public BusinessNewService(IConfiguration configuration, ILogger<BusinessNewService> logger)
        {
            _connectionString = configuration.GetConnectionString("SQLServerConnection")!;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the correct integer SOL ID to pass to the SP based on the user's role.
        ///   Zone   → Zone SOL ID
        ///   Region → Region SOL ID
        ///   CO / Branch (default) → Branch SOL ID
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

            if (int.TryParse(brSolId, out int branchId)) return branchId;
            return 0;
        }

        private static decimal GetDecimal(SqlDataReader reader, string column)
        {
            try
            {
                return reader[column] != DBNull.Value ? Convert.ToDecimal(reader[column]) : 0m;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[BusinessNewService] Error reading decimal column: " + ex.Message);
                return 0m;
            }
        }

        private static bool HasColumn(SqlDataReader reader, string column)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (reader.GetName(i).Equals(column, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>
        /// Safely reads AS_ON_DATE from the current row. Guards against the column
        /// being absent in a given result set — an unguarded reader["AS_ON_DATE"]
        /// throws IndexOutOfRangeException, which would abort the whole multi-result
        /// pipeline (e.g. preventing the Expense result set from ever being read).
        /// </summary>
        private static DateTime ReadAsOnDate(SqlDataReader reader)
        {
            if (!HasColumn(reader, "AS_ON_DATE")) return DateTime.MinValue;
            return reader["AS_ON_DATE"] != DBNull.Value
                ? Convert.ToDateTime(reader["AS_ON_DATE"])
                : DateTime.MinValue;
        }

        // ────────────────────────────────────────────────────────────────────────
        //  ADVANCES  —  SP_PAGETWO_DASHBOARD_ADVANCES
        // ────────────────────────────────────────────────────────────────────────

        /// <inheritdoc />
        public async Task<BusinessNewResult> GetAdvancesDashboardAsync1(
            string role, string brSolId, string regionSolId, string zoneSolId,
            string scope = "total")
        {
            var result = new BusinessNewResult();
            int solId = ResolveSolId(role, brSolId, regionSolId, zoneSolId);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_PAGETWO_DASHBOARD_ADVANCES", conn)
                {
                    CommandType    = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };
                cmd.Parameters.Add(new SqlParameter("@SOL_ID", SqlDbType.Int)          { Value = solId });
                cmd.Parameters.Add(new SqlParameter("@Scope",  SqlDbType.NVarChar, 20) { Value = scope });

                using var reader = await cmd.ExecuteReaderAsync();

                // ── RS 0: AS_ON_DATE ──────────────────────────────────────────
                if (await reader.ReadAsync())
                    result.AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                        ? Convert.ToDateTime(reader["AS_ON_DATE"]) : (DateTime?)null;

                // ── RS 1: Performance ─────────────────────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Performance = new BusinessPerformance
                    {
                        CurrentFy   = GetDecimal(reader, "CURRENT_FY"),
                        TargetFy    = GetDecimal(reader, "TARGET_FY"),
                        ActualTotal = GetDecimal(reader, "ACTUAL_TOTAL"),
                        ScaleText   = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── RS 2: Portfolio ───────────────────────────────────────────
                // Column names differ per scope; use HasColumn to read safely.
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    var p = new BusinessPortfolio();

                    // total scope
                    if (HasColumn(reader, "MSME_ADVANCES"))        p.MsmeAdvances        = GetDecimal(reader, "MSME_ADVANCES");
                    if (HasColumn(reader, "RETAIL_ADVANCES"))      p.RetailAdvances      = GetDecimal(reader, "RETAIL_ADVANCES");
                    if (HasColumn(reader, "AGRICULTURE_ADVANCES")) p.AgricultureAdvances = GetDecimal(reader, "AGRICULTURE_ADVANCES");
                    if (HasColumn(reader, "CORPORATE_ADVANCES"))   p.CorporateAdvances   = GetDecimal(reader, "CORPORATE_ADVANCES");

                    // retail scope
                    if (HasColumn(reader, "VEHICLE_ADVANCES"))     p.VehicleAdvances     = GetDecimal(reader, "VEHICLE_ADVANCES");
                    if (HasColumn(reader, "MORTGAGE_ADVANCES"))    p.MortgageAdvances    = GetDecimal(reader, "MORTGAGE_ADVANCES");
                    if (HasColumn(reader, "HOME_ADVANCES"))        p.HomeAdvances        = GetDecimal(reader, "HOME_ADVANCES");

                    // msme scope
                    if (HasColumn(reader, "MICRO_ADVANCES"))       p.MicroAdvances       = GetDecimal(reader, "MICRO_ADVANCES");
                    if (HasColumn(reader, "SMALL_ADVANCES"))       p.SmallAdvances       = GetDecimal(reader, "SMALL_ADVANCES");
                    if (HasColumn(reader, "MEDIUM_ADVANCES"))      p.MediumAdvances      = GetDecimal(reader, "MEDIUM_ADVANCES");

                    p.ScaleText = reader["SCALE_TEXT"]?.ToString() ?? "";
                    result.Portfolio = p;
                }

                // ── RS 3: Outstanding Book ────────────────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.OutstandingBook = new BusinessOutstandingBook
                    {
                        Base        = GetDecimal(reader, "base"),
                        LastFy      = GetDecimal(reader, "LAST_FY"),
                        LastQuarter = GetDecimal(reader, "LAST_QUARTER"),
                        LastMonth   = GetDecimal(reader, "LAST_MONTH"),
                        AsOn        = GetDecimal(reader, "AS_ON"),
                        ScaleText = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── RS 4: Disbursements (multiple rows) ───────────────────────
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Disbursements.Add(new BusinessDisbursementRow
                        {
                            AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                                ? Convert.ToDateTime(reader["AS_ON_DATE"]) : DateTime.MinValue,
                            Noa = GetDecimal(reader, "Sanctions"),
                            Amt = GetDecimal(reader, "disbursements")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BusinessNewService] SP_PAGETWO_DASHBOARD_ADVANCES error — SOL_ID={SolId}, Scope={Scope}",
                    solId, scope);
            }

            return result;
        }

        public async Task<BusinessNewResult> GetAdvancesDashboardAsync(
    string role, string brSolId, string regionSolId, string zoneSolId,
    string scope = "total")
        {
            var result = new BusinessNewResult
            {
                Performance = new BusinessPerformance(),
                Portfolio = new BusinessPortfolio(),
                OutstandingBook = new BusinessOutstandingBook(),
                Disbursements = new List<BusinessDisbursementRow>()
            };

            int solId = ResolveSolId(role, brSolId, regionSolId, zoneSolId);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_PAGETWO_DASHBOARD_ADVANCES", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };
                cmd.Parameters.Add(new SqlParameter("@SOL_ID", SqlDbType.Int) { Value = solId });
                cmd.Parameters.Add(new SqlParameter("@Scope", SqlDbType.NVarChar, 20) { Value = scope });

                using var reader = await cmd.ExecuteReaderAsync();

                // Loop through ALL result sets (count/order can vary by scope)
                do
                {
                    var cols = GetColumnSet(reader);

                    // Skip empty resultset fast
                    if (!reader.HasRows)
                        continue;

                    // 1) AS_ON_DATE resultset (single row / single column)
                    if (cols.Count == 1 && cols.Contains("AS_ON_DATE"))
                    {
                        if (await reader.ReadAsync())
                        {
                            result.AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                                ? Convert.ToDateTime(reader["AS_ON_DATE"])
                                : (DateTime?)null;
                        }
                        continue;
                    }

                    // 2) Performance
                    if (HasAny(cols, "CURRENT_FY", "TARGET_FY", "ACTUAL_TOTAL"))
                    {
                        if (await reader.ReadAsync())
                        {
                            result.Performance.CurrentFy = GetDecimal(reader, "CURRENT_FY");
                            result.Performance.TargetFy = GetDecimal(reader, "TARGET_FY");
                            result.Performance.ActualTotal = GetDecimal(reader, "ACTUAL_TOTAL");
                            result.Performance.ScaleText = cols.Contains("SCALE_TEXT")
                                ? reader["SCALE_TEXT"]?.ToString() ?? ""
                                : "";
                        }
                        continue;
                    }

                    // 3) Outstanding Book
                    if (HasAny(cols, "LAST_FY", "LAST_QUARTER", "LAST_MONTH", "AS_ON") && (cols.Contains("base") || cols.Contains("BASE")))
                    {
                        if (await reader.ReadAsync())
                        {
                            result.OutstandingBook.Base = cols.Contains("base") ? GetDecimal(reader, "base") : GetDecimal(reader, "BASE");
                            result.OutstandingBook.LastFy = GetDecimal(reader, "LAST_FY");
                            result.OutstandingBook.LastQuarter = GetDecimal(reader, "LAST_QUARTER");
                            result.OutstandingBook.LastMonth = GetDecimal(reader, "LAST_MONTH");
                            result.OutstandingBook.AsOn = GetDecimal(reader, "AS_ON");
                            result.OutstandingBook.ScaleText = cols.Contains("SCALE_TEXT")
                                ? reader["SCALE_TEXT"]?.ToString() ?? ""
                                : "";
                        }
                        continue;
                    }

                    // 4) Disbursements (multi-row)
                    if (HasAny(cols, "Sanctions", "disbursements") && cols.Contains("AS_ON_DATE"))
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Disbursements.Add(new BusinessDisbursementRow
                            {
                                AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["AS_ON_DATE"])
                                    : DateTime.MinValue,
                                Noa = GetDecimal(reader, "Sanctions"),
                                Amt = GetDecimal(reader, "disbursements")
                            });
                        }
                        continue;
                    }

                    // 5) Portfolio (single row; columns vary per scope)
                    // Detect by presence of any known *_ADVANCES columns
                    if (cols.Any(c => c.EndsWith("_ADVANCES", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (await reader.ReadAsync())
                        {
                            var p = new BusinessPortfolio();

                            // total
                            if (cols.Contains("MSME_ADVANCES")) p.MsmeAdvances = GetDecimal(reader, "MSME_ADVANCES");
                            if (cols.Contains("RETAIL_ADVANCES")) p.RetailAdvances = GetDecimal(reader, "RETAIL_ADVANCES");
                            if (cols.Contains("AGRICULTURE_ADVANCES")) p.AgricultureAdvances = GetDecimal(reader, "AGRICULTURE_ADVANCES");
                            if (cols.Contains("CORPORATE_ADVANCES")) p.CorporateAdvances = GetDecimal(reader, "CORPORATE_ADVANCES");

                            // retail
                            if (cols.Contains("VEHICLE_ADVANCES")) p.VehicleAdvances = GetDecimal(reader, "VEHICLE_ADVANCES");
                            if (cols.Contains("MORTGAGE_ADVANCES")) p.MortgageAdvances = GetDecimal(reader, "MORTGAGE_ADVANCES");
                            if (cols.Contains("HOME_ADVANCES")) p.HomeAdvances = GetDecimal(reader, "HOME_ADVANCES");

                            // msme
                            if (cols.Contains("MICRO_ADVANCES")) p.MicroAdvances = GetDecimal(reader, "MICRO_ADVANCES");
                            if (cols.Contains("SMALL_ADVANCES")) p.SmallAdvances = GetDecimal(reader, "SMALL_ADVANCES");
                            if (cols.Contains("MEDIUM_ADVANCES")) p.MediumAdvances = GetDecimal(reader, "MEDIUM_ADVANCES");

                            p.ScaleText = cols.Contains("SCALE_TEXT")
                                ? reader["SCALE_TEXT"]?.ToString() ?? ""
                                : "";

                            result.Portfolio = p;
                        }
                        continue;
                    }

                    // If you ever add a new result set in SP later,
                    // it will land here safely (no crash, no wrong mapping).

                } while (await reader.NextResultAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BusinessNewService] SP_PAGETWO_DASHBOARD_ADVANCES error — SOL_ID={SolId}, Scope={Scope}",
                    solId, scope);
            }

            return result;
        }

        private static HashSet<string> GetColumnSet(SqlDataReader reader)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                set.Add(reader.GetName(i));
            return set;
        }

        private static bool HasAny(HashSet<string> cols, params string[] names)
            => names.Any(n => cols.Contains(n));
        // ────────────────────────────────────────────────────────────────────────
        //  DEPOSITS  —  SP_PAGETWO_DASHBOARD_DEPOSITS
        // ────────────────────────────────────────────────────────────────────────

        /// <inheritdoc />
        public async Task<DepositsNewResult> GetDepositsDashboardAsync(
     string role, string brSolId, string regionSolId, string zoneSolId,
     string scope = "total")
        {
            var result = new DepositsNewResult
            {
                Performance = new BusinessPerformance(),
                Portfolio = new DepositsPortfolio(),
                OutstandingBook = new BusinessOutstandingBook(),
                Opened = new List<BusinessDisbursementRow>()
            };

            int solId = ResolveSolId(role, brSolId, regionSolId, zoneSolId);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_PAGETWO_DASHBOARD_DEPOSITS", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };

                cmd.Parameters.Add(new SqlParameter("@SOL_ID", SqlDbType.Int) { Value = solId });
                cmd.Parameters.Add(new SqlParameter("@Scope", SqlDbType.NVarChar, 20) { Value = scope });

                using var reader = await cmd.ExecuteReaderAsync();

                // ── RS 0: AS_ON_DATE ──────────────────────────────────────────
                if (await reader.ReadAsync())
                {
                    result.AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                        ? Convert.ToDateTime(reader["AS_ON_DATE"])
                        : (DateTime?)null;
                }

                // ── RS 1: Performance ─────────────────────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Performance = new BusinessPerformance
                    {
                        CurrentFy = GetDecimal(reader, "CURRENT_FY"),
                        TargetFy = GetDecimal(reader, "TARGET_FY"),
                        ActualTotal = GetDecimal(reader, "ACTUAL_TOTAL"),
                        ScaleText = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── RS 2: Portfolio (OPTIONAL) ─────────────────────────────────
                bool portfolioRead = false;

                if (await reader.NextResultAsync())
                {
                    bool hasPortfolioColumns =
                        HasColumn(reader, "CD_DEPOSITS") ||
                        HasColumn(reader, "SB_DEPOSITS") ||
                        HasColumn(reader, "TD_DEPOSITS") ||
                        HasColumn(reader, "RETAIL_TERM_DEPOSITS") ||
                        HasColumn(reader, "RETAIL_DEPOSITS") ||
                        HasColumn(reader, "BULK_DEPOSITS");

                    if (hasPortfolioColumns && reader.HasRows && await reader.ReadAsync())
                    {
                        var p = new DepositsPortfolio();

                        if (HasColumn(reader, "CD_DEPOSITS")) p.CdDeposits = GetDecimal(reader, "CD_DEPOSITS");
                        if (HasColumn(reader, "SB_DEPOSITS")) p.SbDeposits = GetDecimal(reader, "SB_DEPOSITS");
                        if (HasColumn(reader, "TD_DEPOSITS")) p.TdDeposits = GetDecimal(reader, "TD_DEPOSITS");
                        if (HasColumn(reader, "RETAIL_TERM_DEPOSITS")) p.RetailTermDeposits = GetDecimal(reader, "RETAIL_TERM_DEPOSITS");

                        if (HasColumn(reader, "RETAIL_DEPOSITS")) p.RetailTd = GetDecimal(reader, "RETAIL_DEPOSITS");
                        if (HasColumn(reader, "BULK_DEPOSITS")) p.BulkTd = GetDecimal(reader, "BULK_DEPOSITS");

                        p.ScaleText = reader["SCALE_TEXT"]?.ToString() ?? "";
                        result.Portfolio = p;

                        portfolioRead = true;
                    }
                }

                // ── RS 3: Outstanding Book ────────────────────────────────────
                // If portfolio was read → move next, else current RS is already outstanding
                if (portfolioRead)
                {
                    await reader.NextResultAsync();
                }

                if (reader.HasRows && await reader.ReadAsync())
                {
                    result.OutstandingBook = new BusinessOutstandingBook
                    {
                        Base = GetDecimal(reader, "base"),
                        LastFy = GetDecimal(reader, "LAST_FY"),
                        LastQuarter = GetDecimal(reader, "LAST_QUARTER"),
                        LastMonth = GetDecimal(reader, "LAST_MONTH"),
                        AsOn = GetDecimal(reader, "AS_ON"),
                        ScaleText = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── RS 4: Opened / Trend ──────────────────────────────────────
                if (await reader.NextResultAsync())
                {
                    if (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Opened.Add(new BusinessDisbursementRow
                            {
                                AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["AS_ON_DATE"])
                                    : DateTime.MinValue,
                                Noa = GetDecimal(reader, "Account"),
                                Amt = GetDecimal(reader, "Amount")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BusinessNewService] SP_PAGETWO_DASHBOARD_DEPOSITS error — SOL_ID={SolId}, Scope={Scope}",
                    solId, scope);
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────────────────
        //  NPA  —  SP_PAGETWO_DASHBOARD_NPA
        // ────────────────────────────────────────────────────────────────────────

        /// <inheritdoc />
        public async Task<NpaNewResult> GetNpaDashboardAsync(
    string role, string brSolId, string regionSolId, string zoneSolId)
        {
            var result = new NpaNewResult(); // properties initialized in model as shown above
            int solId = ResolveSolId(role, brSolId, regionSolId, zoneSolId);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_PAGETWO_DASHBOARD_NPA", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };
                cmd.Parameters.Add(new SqlParameter("@SOL_ID", SqlDbType.Int) { Value = solId });

                using var reader = await cmd.ExecuteReaderAsync();

                // ── RS 0: AS_ON_DATE ──────────────────────────────────────────
                if (await reader.ReadAsync())
                {
                    result.AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                        ? Convert.ToDateTime(reader["AS_ON_DATE"])
                        : (DateTime?)null;
                }

                // ── RS 1: Performance (NPA vs ADVANCES) ────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Performance = new NpaBusinessPerformance
                    {
                        Npa = GetDecimal(reader, "NPA"),
                        Advances = GetDecimal(reader, "ADVANCES"),
                        ScaleText = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── RS 2: NPA Portfolio ───────────────────────────────────────
                // New Columns: MSME, AGRI, RETAIL, CORPORATE, gross_npa, OTHERS, SCALE_TEXT
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Portfolio = new NpaBusinessPortfolio
                    {
                        MsmeNpa = GetDecimal(reader, "MSME"),
                        AgriNpa = GetDecimal(reader, "AGRI"),
                        RetailNpa = GetDecimal(reader, "RETAIL"),
                        CorporateNpa = GetDecimal(reader, "CORPORATE"),

                        GrossNpa = GetDecimal(reader, "gross_npa"),
                        Others = GetDecimal(reader, "OTHERS"),

                        // If your UI still needs Total, safest is to treat gross_npa as total NPA
                        Total = GetDecimal(reader, "gross_npa"),

                        ScaleText = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }

                // ── RS 3: NPA Book / Growth ───────────────────────────────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.OutstandingBook = new BusinessOutstandingBook
                    {
                        Base = GetDecimal(reader, "base"),
                        LastFy = GetDecimal(reader, "LAST_FY"),
                        LastQuarter = GetDecimal(reader, "LAST_QUARTER"),
                        LastMonth = GetDecimal(reader, "LAST_MONTH"),
                        AsOn = GetDecimal(reader, "AS_ON"),
                        ScaleText = reader["SCALE_TEXT"]?.ToString() ?? ""
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BusinessNewService] SP_PAGETWO_DASHBOARD_NPA error — SOL_ID={SolId}", solId);
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────────────────
        //  PROFITABILITY  —  SP_PAGETWO_DASHBOARD_PROFITABILITY
        // ────────────────────────────────────────────────────────────────────────

        /// <inheritdoc />
        public async Task<ProfitabilityNewResult> GetProfitabilityDashboardAsync(
            string role, string brSolId, string regionSolId, string zoneSolId)
        {
            var result = new ProfitabilityNewResult();
            int solId = ResolveSolId(role, brSolId, regionSolId, zoneSolId);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_PAGETWO_DASHBOARD_PROFITABILITY", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };
                cmd.Parameters.Add(new SqlParameter("@SOL_ID", SqlDbType.Int) { Value = solId });

                using var reader = await cmd.ExecuteReaderAsync();

                // ── RS 0: AS_ON_DATE ──────────────────────────────────────────
                if (await reader.ReadAsync())
                {
                    result.AsOnDate = reader["AS_ON_DATE"] != DBNull.Value
                        ? Convert.ToDateTime(reader["AS_ON_DATE"])
                        : (DateTime?)null;
                }

                // ── RS 1: Fee Month-On-Month Comparison (multiple rows) ───────
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.FeeMom.Add(new ProfitabilityMomRow
                        {
                            AsOnDate = ReadAsOnDate(reader),
                            Data = GetDecimal(reader, "data")
                        });
                    }
                }

                // ── RS 2: Fee Portfolio (single row — Interest + Fee) ─────────
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Portfolio = new ProfitabilityPortfolioData
                    {
                        Interest = GetDecimal(reader, "Interest"),
                        Fee = GetDecimal(reader, "Fee"),
                        ScaleText = HasColumn(reader, "SCALE_TEXT")
                            ? reader["SCALE_TEXT"]?.ToString() ?? ""
                            : ""
                    };
                }

                // ── RS 3: Interest Month-On-Month Comparison (multiple rows) ──
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.InterestMom.Add(new ProfitabilityMomRow
                        {
                            AsOnDate = ReadAsOnDate(reader),
                            Data = GetDecimal(reader, "data")
                        });
                    }
                }

                // ── RS 4: Expense Month-On-Month Comparison (multiple rows) ───
                // The expense value column may be aliased as "data" or "Expense"
                // depending on the SP build, so fall back to "Expense" if needed.
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.ExpenseMom.Add(new ProfitabilityMomRow
                        {
                            AsOnDate = ReadAsOnDate(reader),
                            Data = HasColumn(reader, "data")
                                ? GetDecimal(reader, "data")
                                : GetDecimal(reader, "Expense")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BusinessNewService] SP_PAGETWO_DASHBOARD_PROFITABILITY error — SOL_ID={SolId}", solId);
            }

            return result;
        }
    }
}
