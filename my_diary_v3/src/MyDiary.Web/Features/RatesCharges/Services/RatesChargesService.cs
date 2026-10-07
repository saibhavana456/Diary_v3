using System.Data;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.RatesCharges.Models;

namespace MyDiary.Web.Features.RatesCharges.Services
{
    public class RatesChargesService : IRatesChargesService
    {
        private readonly SqlDbProvider _db;

        public RatesChargesService(SqlDbProvider db)
        {
            _db = db;
        }

        public async Task<List<DepositCategory>> GetCategoriesAsync()
        {
            try
            {
                const string sql = @"
                    SELECT CategoryId, CategoryCode, CategoryName, CategoryName_HI, SortOrder, ContentMode
                    FROM dbo.DepositCategory
                    WHERE IsActive = 1
                    ORDER BY SortOrder";

                var dt = await _db.ExecuteQueryAsync(sql);
                var list = new List<DepositCategory>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new DepositCategory
                    {
                        CategoryId = Convert.ToInt32(row["CategoryId"]),
                        CategoryCode = row["CategoryCode"].ToString()!,
                        CategoryName = row["CategoryName"].ToString()!,
                        CategoryName_HI = row["CategoryName_HI"]?.ToString() ?? "",
                        SortOrder = Convert.ToInt32(row["SortOrder"]),
                        ContentMode = row["ContentMode"]?.ToString() is { Length: > 0 } cm ? cm : "STRUCTURED"   // ✅ NEW
                    });
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { AppLogger.LogWarning($"RatesChargesService: DB query failed, using fallback — {ex.Message}"); }

            return GetFallbackCategories();
        }

        public async Task<List<DepositRate>> GetRatesByCategoryAsync(int categoryId)
        {
            try
            {
                string sql = $@"
                    SELECT Period, Period_HI, ColumnHeader, Rate, EffectiveDate, SortOrder
FROM dbo.DepositRate
WHERE CategoryId = {categoryId} AND IsActive = 1
ORDER BY SortOrder";

                var dt = await _db.ExecuteQueryAsync(sql);
                var list = new List<DepositRate>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new DepositRate
                    {
                        Period = row["Period"].ToString()!,
                        Period_HI = row["Period_HI"]?.ToString() ?? "",
                        ColumnHeader = row["ColumnHeader"]?.ToString() ?? "",
                        Rate = row["Rate"].ToString()!,
                        EffectiveDate = Convert.ToDateTime(row["EffectiveDate"]),
                        SortOrder = Convert.ToInt32(row["SortOrder"])
                    });
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { AppLogger.LogWarning($"RatesChargesService: DB query failed for sub-categories, using fallback — {ex.Message}"); }

            // Resolve category code from fallback categories
            var code = GetFallbackCategories()
                .FirstOrDefault(c => c.CategoryId == categoryId)?.CategoryCode ?? "";
            return GetFallbackSingleRates(code);
        }

        public async Task<List<MultiCurrencySection>> GetMultiCurrencyRatesAsync(int categoryId)
        {
            try
            {
                string sql = $@"
                    SELECT SubSection, SubSection_HI, Period, Period_HI, Currency, Rate, EffectiveDate, SortOrder
                    FROM dbo.DepositRateMultiCurrency WHERE CategoryId = {categoryId} AND IsActive = 1 ORDER BY SubSection, SortOrder, Currency";

                var dt = await _db.ExecuteQueryAsync(sql);
                var rawList = new List<DepositRateMultiCurrency>();
                foreach (DataRow row in dt.Rows)
                {
                    rawList.Add(new DepositRateMultiCurrency
                    {
                        SubSection = row["SubSection"]?.ToString() ?? "",
                        SubSection_HI = row["SubSection_HI"]?.ToString() ?? "",
                        Period = row["Period"].ToString()!,
                        Period_HI = row["Period_HI"]?.ToString() ?? "",
                        Currency = row["Currency"].ToString()!,
                        Rate = row["Rate"].ToString()!,
                        EffectiveDate = Convert.ToDateTime(row["EffectiveDate"]),
                        SortOrder = Convert.ToInt32(row["SortOrder"])
                    });

                }
                if (rawList.Count > 0) return PivotMultiCurrency(rawList);
            }
            catch (Exception ex) { AppLogger.LogWarning($"RatesChargesService: DB query failed for rates, using fallback — {ex.Message}"); }

            var code = GetFallbackCategories()
                .FirstOrDefault(c => c.CategoryId == categoryId)?.CategoryCode ?? "";
            return GetFallbackMultiCurrencyRates(code);
        }

        private static List<MultiCurrencySection> PivotMultiCurrency(List<DepositRateMultiCurrency> rawList)
        {
            return rawList
    .GroupBy(r => new { r.SubSection, r.SubSection_HI })
    .Select(g =>
    {
        var currencies = g.Select(r => r.Currency).Distinct().ToList();
        var rows = g.GroupBy(r => new { r.Period, r.Period_HI, r.SortOrder })
            .OrderBy(rg => rg.Key.SortOrder)
            .Select(rg => new MultiCurrencyRow
            {
                Period = rg.Key.Period,
                Period_HI = rg.Key.Period_HI,
                RateByCurrency = rg.ToDictionary(r => r.Currency, r => r.Rate)
            }).ToList();

        return new MultiCurrencySection
        {
            SubSection = g.Key.SubSection,
            SubSection_HI = g.Key.SubSection_HI,
            Currencies = currencies,
            Rows = rows,
            EffectiveDate = g.First().EffectiveDate
        };
    }).ToList();
        }

        // ===================== HTML-DRIVEN CONTENT (✅ NEW) =====================

        public async Task<List<CategoryContentBlock>> GetCategoryContentAsync(int categoryId)
        {
            try
            {
                const string sql = @"
                    SELECT BlockId, CategoryId, EffectiveDate, HtmlContent, HtmlContent_HI, SortOrder
                    FROM dbo.CategoryContentBlock
                    WHERE CategoryId = @CategoryId AND IsActive = 1
                    ORDER BY SortOrder";

                var dt = await _db.ExecuteQueryWithParamsAsync(sql,
                    new[] { new Microsoft.Data.SqlClient.SqlParameter("@CategoryId", categoryId) });

                var list = new List<CategoryContentBlock>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new CategoryContentBlock
                    {
                        BlockId = Convert.ToInt32(row["BlockId"]),
                        CategoryId = Convert.ToInt32(row["CategoryId"]),
                        EffectiveDate = Convert.ToDateTime(row["EffectiveDate"]),
                        HtmlContent = row["HtmlContent"]?.ToString() ?? "",
                        HtmlContent_HI = row["HtmlContent_HI"]?.ToString() ?? "",
                        SortOrder = Convert.ToInt32(row["SortOrder"])
                    });
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { AppLogger.LogWarning($"RatesChargesService: DB query failed for category content, using fallback — {ex.Message}"); }

            var code = GetFallbackCategories()
                .FirstOrDefault(c => c.CategoryId == categoryId)?.CategoryCode ?? "";
            return GetFallbackCategoryContent(code);
        }

        private static List<CategoryContentBlock> GetFallbackCategoryContent(string code) => code switch
        {
            "NRE" => new()
            {
                new()
                {
                    BlockId = 1,
                    EffectiveDate = new DateTime(2026, 8, 4),
                    SortOrder = 1,
                    // Mirrors the richer "Callable / Non-Callable" layout from the source Word doc
                    // (NRE_Deposit_in_UBINET) — impossible in the old fixed two-column schema,
                    // trivial here because it's just a table shape stored as HTML.
                    HtmlContent = @"
<table>
  <thead>
    <tr>
      <th rowspan=""2"">Period</th>
      <th>Callable Deposits<br/>Rates in % per annum<br/>&lt; Rs. 3 Cr</th>
      <th>Non-Callable Deposits #<br/>Rates in % per annum<br/>&gt; 1 Cr to &lt; Rs. 3 Cr</th>
    </tr>
  </thead>
  <tbody>
    <tr><td>&gt; 1 Year to 399 days</td><td>6.20</td><td>No Slab</td></tr>
    <tr><td>400 days</td><td>6.25</td><td>6.75</td></tr>
    <tr><td>401 to 443 days</td><td>6.25</td><td>No Slab</td></tr>
    <tr><td>444 days (New Slab)</td><td>6.50</td><td>6.80</td></tr>
    <tr><td>445 days to 554 days</td><td>6.15</td><td>No Slab</td></tr>
    <tr><td>555 days</td><td>6.55</td><td>6.65</td></tr>
    <tr><td>556 days to 996 days</td><td>6.15</td><td rowspan=""3"">No Slab</td></tr>
    <tr><td>997 days</td><td>6.10</td></tr>
    <tr><td>998 days to 3 yrs</td><td>6.10</td></tr>
    <tr><td>&gt; 3 yrs to 10 yrs</td><td>6.00</td><td>No Slab</td></tr>
  </tbody>
</table>
<ul>
  <li>Term Deposits of 3 Crore and above (both new and renewal) will not be accepted without the prior permission of Deposit Mobilization Department.</li>
  <li>In case of deposits of 3 Crore and above, where rates have been provided by Deposit Mobilization Department, Branches to print the receipt after change in the rate of interest in the system by Deposit Mobilization Department.</li>
  <li>For flexi Deposits of 3 Crore and above, passbook or statement should be provided to the customer after change in rate of interest in the system by Deposit Mobilization Department.</li>
</ul>"
                }
            },
            _ => new()
        };

        // ===================== FALLBACK DATA =====================

        private static List<DepositCategory> GetFallbackCategories() => new()
        {
            new() { CategoryId = 1, CategoryCode = "TERM",   CategoryName = "Domestic / NRO Term Deposit", SortOrder = 1 },
            new() { CategoryId = 2, CategoryCode = "NRE",    CategoryName = "NRE Deposit",                 SortOrder = 2, ContentMode = "HTML" },   // ✅ NEW — first category migrated to the HTML path
            new() { CategoryId = 3, CategoryCode = "FCNR",   CategoryName = "FCNR(B) Deposit",             SortOrder = 3 },
            new() { CategoryId = 4, CategoryCode = "RFC",    CategoryName = "RFC Deposit",                  SortOrder = 4 },
            new() { CategoryId = 5, CategoryCode = "OBU",    CategoryName = "OBU Deposit",                  SortOrder = 5 },
            new() { CategoryId = 6, CategoryCode = "GOVT",   CategoryName = "Government Deposit Schemes",   SortOrder = 6 },
            new() { CategoryId = 7, CategoryCode = "SAVING", CategoryName = "Saving Bank Deposits",         SortOrder = 7 },
        };

        private static List<DepositRate> GetFallbackSingleRates(string code) => code switch
        {
            "TERM" => GetFallbackTermDeposit(),
            "NRE" => GetFallbackNREDeposit(),
            "GOVT" => GetFallbackGovtDeposit(),
            "SAVING" => GetFallbackSavingDeposit(),
            _ => new()
        };

        // --- TermDeposit.html (Effective 11/02/2026) ---
        private static List<DepositRate> GetFallbackTermDeposit()
        {
            var d = new DateTime(2026, 2, 11);
            const string h = "< Rs. 3 Cr";
            int s = 0;
            return new()
            {
                new() { Period = "7-14 Days",              ColumnHeader = h, Rate = "2.75", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "15-30 Days",             ColumnHeader = h, Rate = "3.00", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "31-45 Days",             ColumnHeader = h, Rate = "3.25", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "46-90 Days",             ColumnHeader = h, Rate = "4.50", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "91-120 Days",            ColumnHeader = h, Rate = "4.75", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "121-180 Days",           ColumnHeader = h, Rate = "5.50", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "181-270 Days",           ColumnHeader = h, Rate = "5.75", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "271-364 Days",           ColumnHeader = h, Rate = "6.10", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "1 Yr.",                  ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> 1 Year to 399 days",  ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "400 Days",               ColumnHeader = h, Rate = "6.40", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "401 to 443 Days",        ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "444 Days (New Slab)",    ColumnHeader = h, Rate = "6.60", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "445 Days to 2 Yrs",     ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> 2 Yrs to 996 days",   ColumnHeader = h, Rate = "6.25", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "997 Days",               ColumnHeader = h, Rate = "6.20", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "998 days to 3 Years",    ColumnHeader = h, Rate = "6.25", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> 3 Year to 10 years",  ColumnHeader = h, Rate = "6.00", EffectiveDate = d, SortOrder = ++s },
            };
        }

        // --- NREDeposit.html (Effective 11/02/2026) ---
        private static List<DepositRate> GetFallbackNREDeposit()
        {
            var d = new DateTime(2026, 2, 11);
            const string h = "< Rs. 3 Cr";
            int s = 0;
            return new()
            {
                new() { Period = "1 Year",                 ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> 1 Year to 399 days",  ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "400 days",               ColumnHeader = h, Rate = "6.40", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "401 to 443 days",        ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "444 days (New Slab)",    ColumnHeader = h, Rate = "6.60", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "445 days to 2 yrs",     ColumnHeader = h, Rate = "6.30", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> 2 yrs to 996 days",   ColumnHeader = h, Rate = "6.25", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "997 days",               ColumnHeader = h, Rate = "6.20", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "998 days to 3 yrs",     ColumnHeader = h, Rate = "6.25", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> 3 yrs to 10 yrs",    ColumnHeader = h, Rate = "6.00", EffectiveDate = d, SortOrder = ++s },
            };
        }

        // --- GOVTDeposits.html (Effective 01/04/2023) ---
        private static List<DepositRate> GetFallbackGovtDeposit()
        {
            var d = new DateTime(2023, 4, 1);
            const string h = "Rate of Interest (p.a.)";
            int s = 0;
            return new()
            {
                new() { Period = "5 Year Senior Citizens Savings Scheme (SCSS2004)", ColumnHeader = h, Rate = "8.20%",                       EffectiveDate = d, SortOrder = ++s },
                new() { Period = "Public Provident Fund Scheme, 1968 (PPF)",         ColumnHeader = h, Rate = "7.10%",                       EffectiveDate = d, SortOrder = ++s },
                new() { Period = "Kisan Vikas Patra",                                ColumnHeader = h, Rate = "7.50% (Maturity 115 months)", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "Sukanya Samridhi Account Scheme",                  ColumnHeader = h, Rate = "8.20%",                       EffectiveDate = d, SortOrder = ++s },
            };
        }

        // --- SavingDeposit.html (Effective 29/07/2025) ---
        private static List<DepositRate> GetFallbackSavingDeposit()
        {
            var d = new DateTime(2025, 7, 29);
            const string h = "Interest Rate (per annum)";
            int s = 0;
            return new()
            {
                new() { Period = "Up to Rs.50 lakhs",                ColumnHeader = h, Rate = "2.50%", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> Rs.50 lakhs to Rs.100 Crores",   ColumnHeader = h, Rate = "3.00%", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> Rs.100 Crores to Rs.500 Crores",  ColumnHeader = h, Rate = "3.40%", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> Rs.500 Crores to Rs.1000 Crores", ColumnHeader = h, Rate = "4.25%", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> Rs.1000 Crores to 1500 Crores",   ColumnHeader = h, Rate = "4.50%", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "> Rs.1500 Crores to 2000 Crores",   ColumnHeader = h, Rate = "4.50%", EffectiveDate = d, SortOrder = ++s },
                new() { Period = "Above Rs. 2000 Crores",             ColumnHeader = h, Rate = "4.75%", EffectiveDate = d, SortOrder = ++s },
            };
        }

        private static List<MultiCurrencySection> GetFallbackMultiCurrencyRates(string code) => code switch
        {
            "FCNR" => GetFallbackFCNR(),
            "OBU" => GetFallbackOBU(),
            "RFC" => GetFallbackRFC(),
            _ => new()
        };

        // --- FCNRDeposit.html ---
        private static List<MultiCurrencySection> GetFallbackFCNR()
        {
            var d = new DateTime(2022, 7, 21);
            var currencies = new List<string> { "USD", "GBP", "EUR" };
            return new()
            {
                new()
                {
                    SubSection = "For Deposits of USD 100,000 / GBP 60,000 / EUR 70,000 and above",
                    Currencies = currencies, EffectiveDate = d,
                    Rows = new()
                    {
                        MkRow("6 mths < 1yr",  "1.84", "2.12", "1.85"),
                        MkRow("1 yr < 2 yrs",  "2.07", "2.39", "2.02"),
                        MkRow("2 yrs < 3 yrs", "1.72", "2.59", "1.71"),
                        MkRow("3 yrs only",    "2.08", "2.92", "1.84"),
                    }
                },
                new()
                {
                    SubSection = "For Deposits of less than USD 100,000 / GBP 60,000 / EUR 70,000",
                    Currencies = currencies, EffectiveDate = d,
                    Rows = new()
                    {
                        MkRow("6 mths < 1yr",  "1.09", "1.37", "1.10"),
                        MkRow("1 yr < 2 yrs",  "1.32", "1.64", "1.27"),
                        MkRow("2 yrs < 3 yrs", "1.22", "2.09", "1.21"),
                        MkRow("3 yrs only",    "1.58", "2.42", "1.34"),
                    }
                }
            };
        }

        // --- OBUDeposits.html ---
        private static List<MultiCurrencySection> GetFallbackOBU()
        {
            var d = new DateTime(2014, 2, 1);
            var currencies = new List<string> { "USD", "GBP", "EUR" };
            return new()
            {
                new()
                {
                    SubSection = "For Deposits of USD 100,000 / GBP 60,000 / EUR 70,000 and above",
                    Currencies = currencies, EffectiveDate = d,
                    Rows = new()
                    {
                        MkRow("6 mths < 1yr",  "1.84", "2.12", "1.85"),
                        MkRow("1 yr < 2 yrs",  "2.07", "2.39", "2.02"),
                        MkRow("2 yrs < 3 yrs", "1.72", "2.59", "1.71"),
                        MkRow("3 yrs only",    "2.08", "2.92", "1.84"),
                    }
                },
                new()
                {
                    SubSection = "For Deposits of less than USD 100,000 / GBP 60,000 / EUR 70,000",
                    Currencies = currencies, EffectiveDate = d,
                    Rows = new()
                    {
                        MkRow("6 mths < 1yr",  "1.09", "1.37", "1.10"),
                        MkRow("1 yr < 2 yrs",  "1.32", "1.64", "1.27"),
                        MkRow("2 yrs < 3 yrs", "1.22", "2.09", "1.21"),
                        MkRow("3 yrs only",    "1.58", "2.42", "1.34"),
                    }
                }
            };
        }

        // --- RFCDeposit.html (Effective 10/03/2026) ---
        private static List<MultiCurrencySection> GetFallbackRFC()
        {
            var d = new DateTime(2026, 3, 10);
            var currencies = new List<string> { "USD", "GBP", "EUR" };
            return new()
            {
                new()
                {
                    SubSection = "RFC Deposits (< USD 200,000 equivalent)",
                    Currencies = currencies, EffectiveDate = d,
                    Rows = new()
                    {
                        MkRow("6 mths < 1yr",  "1.09", "1.37", "1.10"),
                        MkRow("1 yr < 2 yrs",  "1.32", "1.64", "1.27"),
                        MkRow("2 yrs < 3 yrs", "1.22", "2.09", "1.21"),
                        MkRow("3 yrs only",    "1.58", "2.42", "1.34"),
                    }
                }
            };
        }

        /// <summary>Helper to build a multi-currency row for USD/GBP/EUR</summary>
        private static MultiCurrencyRow MkRow(string period, string usd, string gbp, string eur) => new()
        {
            Period = period,
            RateByCurrency = new Dictionary<string, string>
            {
                ["USD"] = usd,
                ["GBP"] = gbp,
                ["EUR"] = eur
            }
        };

        // ===================== MCLR / BASE RATE / BPLR =====================

        public async Task<List<MclrRate>> GetMclrRatesAsync()
        {
            try
            {
                const string sql = @"
                    SELECT Tenor, Tenor_HI, Rate, EffectiveFrom, EffectiveTo, SortOrder
FROM dbo.MclrRate
WHERE IsActive = 1
ORDER BY SortOrder";

                var dt = await _db.ExecuteQueryAsync(sql);
                var list = new List<MclrRate>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new MclrRate
                    {
                        Tenor = row["Tenor"].ToString()!,
                        Tenor_HI = row["Tenor_HI"]?.ToString() ?? "",
                        Rate = row["Rate"].ToString()!,
                        EffectiveFrom = Convert.ToDateTime(row["EffectiveFrom"]),
                        EffectiveTo = Convert.ToDateTime(row["EffectiveTo"]),
                        SortOrder = Convert.ToInt32(row["SortOrder"])
                    });
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { AppLogger.LogWarning($"RatesChargesService: DB query failed for MCLR rates, using fallback — {ex.Message}"); }

            return GetFallbackMclrRates();
        }

        public async Task<List<BaseRateBplr>> GetBaseRateBplrAsync()
        {
            try
            {
                const string sql = @"
                    SELECT RateType, RateType_HI, EffectiveDate, Rate, SortOrder
FROM dbo.BaseRateBplr
WHERE IsActive = 1
ORDER BY SortOrder";

                var dt = await _db.ExecuteQueryAsync(sql);
                var list = new List<BaseRateBplr>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new BaseRateBplr
                    {
                        RateType = row["RateType"].ToString()!,
                        RateType_HI = row["RateType_HI"]?.ToString() ?? "",

                        EffectiveDate = row["EffectiveDate"].ToString()!,
                        Rate = row["Rate"].ToString()!,
                        SortOrder = Convert.ToInt32(row["SortOrder"])
                    });
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { AppLogger.LogWarning($"RatesChargesService: DB query failed for base rate/BPLR, using fallback — {ex.Message}"); }

            return GetFallbackBaseRateBplr();
        }

        private static List<MclrRate> GetFallbackMclrRates()
        {
            var from = new DateTime(2026, 3, 11);
            var to = new DateTime(2026, 4, 10);
            return new()
            {
                new() { Tenor = "Overnight MCLR", Rate = "7.80%", EffectiveFrom = from, EffectiveTo = to, SortOrder = 1 },
                new() { Tenor = "1 Month MCLR",   Rate = "7.90%", EffectiveFrom = from, EffectiveTo = to, SortOrder = 2 },
                new() { Tenor = "3 Months MCLR",  Rate = "8.15%", EffectiveFrom = from, EffectiveTo = to, SortOrder = 3 },
                new() { Tenor = "6 Months MCLR",  Rate = "8.45%", EffectiveFrom = from, EffectiveTo = to, SortOrder = 4 },
                new() { Tenor = "1 Year MCLR",    Rate = "8.60%", EffectiveFrom = from, EffectiveTo = to, SortOrder = 5 },
                new() { Tenor = "2 Year MCLR",    Rate = "8.75%", EffectiveFrom = from, EffectiveTo = to, SortOrder = 6 },
                new() { Tenor = "3 Year MCLR",    Rate = "8.90%", EffectiveFrom = from, EffectiveTo = to, SortOrder = 7 },
            };
        }

        private static List<BaseRateBplr> GetFallbackBaseRateBplr() => new()
        {
            new() { RateType = "Base Rate",                          EffectiveDate = "w.e.f 01/01/2026", Rate = "10.55%", SortOrder = 1 },
            new() { RateType = "Benchmark Prime Lending Rate [BPLR]", EffectiveDate = "w.e.f 01/04/2020", Rate = "13.25%", SortOrder = 2 },
            new() { RateType = "EBLR (Repo Rate)",                   EffectiveDate = "w.e.f 11/02/2026", Rate = "8.00%",  SortOrder = 3 },
        };

        // ===================== RATES & CHARGES LINKS =====================

        public async Task<List<RatesChargesLink>> GetLinksBySectionAsync(string sectionCode)
        {
            try
            {
                const string sql = @"
                    SELECT SectionCode, Title, Title_HI, Url, SortOrder
FROM dbo.RatesChargesLink
WHERE IsActive = 1 AND SectionCode = @SectionCode
ORDER BY SortOrder";

                var dt = await _db.ExecuteQueryWithParamsAsync(sql,
                    new[] { new Microsoft.Data.SqlClient.SqlParameter("@SectionCode", sectionCode) });
                var list = new List<RatesChargesLink>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new RatesChargesLink
                    {
                        Section = row["SectionCode"].ToString()!,
                        Title = row["Title"].ToString()!,
                        Title_HI = row["Title_HI"]?.ToString() ?? "",
                        Url = row["Url"].ToString()!,
                        SortOrder = Convert.ToInt32(row["SortOrder"])
                    });
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { AppLogger.LogWarning($"RatesChargesService: DB query failed for links, using fallback — {ex.Message}"); }

            return GetFallbackLinks(sectionCode);
        }

        private static List<RatesChargesLink> GetFallbackLinks(string sectionCode) => sectionCode switch
        {
            "CIRCULARS" => new()
            {
                new() { Section = "CIRCULARS", Title = "RBI Master Direction on Interest Rate on Advances",             Url = "/download/Circulars/RBI_MD_Interest_Rate_Advances.pdf",  SortOrder = 1 },
                new() { Section = "CIRCULARS", Title = "Circular on Revision of MCLR w.e.f. 11.03.2026",               Url = "/download/Circulars/MCLR_Revision_Mar2026.pdf",          SortOrder = 2 },
                new() { Section = "CIRCULARS", Title = "Guidelines on Reset of Interest Rates on Floating Rate Loans",  Url = "/download/Circulars/Reset_Floating_Rate_Guidelines.pdf", SortOrder = 3 },
                new() { Section = "CIRCULARS", Title = "Circular on External Benchmark Based Lending Rate (EBLR)",      Url = "/download/Circulars/EBLR_Circular.pdf",                  SortOrder = 4 },
                new() { Section = "CIRCULARS", Title = "Base Rate Computation Methodology",                             Url = "/download/Circulars/Base_Rate_Methodology.pdf",          SortOrder = 5 },
            },
            "DOCLINKS" => new()
            {
                new() { Section = "DOCLINKS", Title = "RBI Repo Rate History",               Url = "https://www.rbi.org.in/Scripts/BS_NSDPDisplay.aspx",  SortOrder = 1 },
                new() { Section = "DOCLINKS", Title = "Schedule of Charges - Lending",       Url = "/download/ScheduleOfCharges/Lending_Charges.pdf",     SortOrder = 2 },
                new() { Section = "DOCLINKS", Title = "Interest Rate on Deposits",           Url = "/rates-charges",                                      SortOrder = 3 },
                new() { Section = "DOCLINKS", Title = "Loan EMI Calculator",                 Url = "/download/Tools/EMI_Calculator.pdf",                  SortOrder = 4 },
                new() { Section = "DOCLINKS", Title = "Fair Practices Code for Lenders",     Url = "/download/Circulars/Fair_Practices_Code_Lenders.pdf", SortOrder = 5 },
            },
            _ => new()
        };

        public async Task<List<MultiCurrencySection>> GetBulkDepositRatesAsync()
        {
            try
            {
                var effectiveDate =
                    await GetBulkEffectiveDateAsync();

                var allRows =
                    new List<DepositRateMultiCurrency>();

                allRows.AddRange(await GetBulkSectionAsync(
                    "Callable Deposits Rates",
                    "Callable_Deposit_MainChart",
                    "Callable_Deposit_Days_Description",
                    "Callable_Deposit_Amount_Description",
                    effectiveDate));

                allRows.AddRange(await GetBulkSectionAsync(
                    "Notice Period Deposits Rates",
                    "Notice_Period_Deposit_Mainchart",
                    "Notice_Period_Deposit_Days_Description",
                    "Notice_Period_Deposit_Amount_Description",
                    effectiveDate));

                allRows.AddRange(await GetBulkSectionAsync(
                    "Non Callable Deposits Rates",
                    "Non_Callable_Deposit_Mainchart",
                    "Non_Callable_Deposit_Days_Description",
                    "Non_Callable_Deposit_Amount_Description",
                    effectiveDate));

                allRows.AddRange(await GetBulkSectionAsync(
                    "NRE Callable Deposits Rates",
                    "NRE_Callable_Deposit_Mainchart",
                    "NRE_Callable_Deposit_Days_Description",
                    "NRE_Callable_Deposit_Amount_Description",
                    effectiveDate));

                return PivotMultiCurrency(allRows);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex,
                    "Failed loading bulk deposit rates");

                return new List<MultiCurrencySection>();
            }
        }

        private async Task<List<DepositRateMultiCurrency>> GetBulkSectionAsync(
        string sectionName,
        string mainTable,
        string daysTable,
        string amountTable,
        DateTime effectiveDate)
        {
            string sql = $@"
        SELECT
            '{sectionName}' AS SubSection,
            d.Description AS Period,
            a.Description AS Currency,
            CAST(m.rate AS VARCHAR(20)) AS Rate,
            d.daysId AS SortOrder
        FROM {mainTable} m
        INNER JOIN {daysTable} d
            ON m.daysId = d.daysId
        INNER JOIN {amountTable} a
            ON m.amountId = a.amountId
        WHERE
            ISNULL(m.status,'') = 'Active'
            AND ISNULL(d.status,'') = 'Active'
            AND ISNULL(a.status,'') = 'Active'
        ORDER BY d.daysId, a.amountId";

            var dt =
                await _db.ExecuteQueryAsync(
                    sql,
                    "BulkDepositRatesCon");

            var list =
                new List<DepositRateMultiCurrency>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new DepositRateMultiCurrency
                {
                    SubSection =
                        row["SubSection"]?.ToString() ?? "",

                    Period =
                        row["Period"]?.ToString() ?? "",

                    Currency =
                        row["Currency"]?.ToString() ?? "",

                    Rate =
                        row["Rate"]?.ToString() ?? "",

                    EffectiveDate = effectiveDate,

                    SortOrder =
                        Convert.ToInt32(row["SortOrder"])
                });
            }

            return list;
        }

        public async Task<DateTime> GetBulkEffectiveDateAsync()
        {
            var dt =
                await _db.ExecuteQueryAsync(
                    @"
            SELECT TOP 1 UpdateDate
            FROM DateChangeRecords
            ORDER BY UpdateDate DESC",
                    "BulkDepositRatesCon");

            if (dt.Rows.Count > 0)
                return Convert.ToDateTime(
                    dt.Rows[0]["UpdateDate"]);

            return DateTime.Today;
        }

    }
}
