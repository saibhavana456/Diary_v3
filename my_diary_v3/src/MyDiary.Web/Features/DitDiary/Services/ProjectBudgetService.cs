using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.DitDiary.Services
{
    public record DeptItem(long DeptId, string DeptName);
    public record SecItem(long SectionId, string SectionName, long DeptId);
    public record FyItem(int FyId, string FyLabel);
    public record AllocInfo(decimal Spillover, decimal Fresh, decimal Total, bool IsRevised);

    public class ProjectBudgetService
    {
        private readonly string _connString;

		public ProjectBudgetService(IConfiguration config)
		{
			// The stored value is encrypted, exactly as Program.cs reads it.
			_connString = config.GetConnectionString("RP_Owner")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner");
		}

		// Opens its OWN connection every call. It must NOT borrow DitDbContext's
		// connection - that one is shared across the circuit and causes ORA-50045.
		private async Task<T> RunAsync<T>(string sql, object?[] args, Func<DbCommand, Task<T>> body)
        {
            using var conn = new OracleConnection(_connString);
            await conn.OpenAsync();

            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;

            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.Add(new OracleParameter("p" + (i + 1), args[i] ?? DBNull.Value));

            return await body(cmd);
        }

        private static decimal Dec(object? o) => o == null || o == DBNull.Value ? 0m : Convert.ToDecimal(o);

        // ---------------------------------------------------------------
        // MASTERS
        // ---------------------------------------------------------------
        public Task<List<DeptItem>> GetDepartmentsAsync()
        {
            const string sql = @"SELECT DEPT_ID, DEPT_NAME FROM RP_DIT_DEPARTMENT
                                  WHERE NVL(STATUS,'Active') = 'Active'
                               ORDER BY NVL(SORT_ORDER,0), DEPT_NAME";

            return RunAsync(sql, Array.Empty<object?>(), async cmd =>
            {
                var list = new List<DeptItem>();
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new DeptItem(Convert.ToInt64(r.GetValue(0)), r.IsDBNull(1) ? "" : r.GetString(1)));
                return list;
            });
        }

        // NOTE: the RP_DIT_SECTION name column is NAME, not SECTION_NAME.
        public Task<List<SecItem>> GetSectionsAsync(long deptId)
        {
            const string sql = @"SELECT SECTION_ID, NAME, DEPT_ID FROM RP_DIT_SECTION
                                  WHERE DEPT_ID = :p1 AND NVL(STATUS,'Active') = 'Active'
                               ORDER BY NVL(SORT_ORDER,0), NAME";

            return RunAsync(sql, new object?[] { deptId }, ReadSections);
        }

        public Task<List<SecItem>> GetAllSectionsAsync()
        {
            const string sql = @"SELECT SECTION_ID, NAME, DEPT_ID FROM RP_DIT_SECTION
                                  WHERE NVL(STATUS,'Active') = 'Active'
                               ORDER BY NVL(SORT_ORDER,0), NAME";

            return RunAsync(sql, Array.Empty<object?>(), ReadSections);
        }

        private static async Task<List<SecItem>> ReadSections(DbCommand cmd)
        {
            var list = new List<SecItem>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new SecItem(
                    Convert.ToInt64(r.GetValue(0)),
                    r.IsDBNull(1) ? "" : r.GetString(1),
                    r.IsDBNull(2) ? 0L : Convert.ToInt64(r.GetValue(2))));
            return list;
        }

        public Task<List<FyItem>> GetFinancialYearsAsync()
        {
            const string sql = @"SELECT FY_ID, FY_LABEL FROM RP_DIT_FINANCIAL_YEAR
                                  WHERE NVL(IS_ACTIVE,'Y') = 'Y' ORDER BY FY_ID DESC";

            return RunAsync(sql, Array.Empty<object?>(), async cmd =>
            {
                var list = new List<FyItem>();
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new FyItem(Convert.ToInt32(r.GetValue(0)), r.IsDBNull(1) ? "" : r.GetString(1)));
                return list;
            });
        }

        public Task<int> GetCurrentFyIdAsync()
        {
            const string sql = @"SELECT NVL(MAX(FY_ID),0) FROM RP_DIT_FINANCIAL_YEAR WHERE IS_CURRENT = 'Y'";

            return RunAsync(sql, Array.Empty<object?>(), async cmd =>
            {
                var v = await cmd.ExecuteScalarAsync();
                return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
            });
        }

        public Task<bool> IsAppAdminAsync(string pf)
        {
            const string sql = @"SELECT COUNT(*) FROM RP_DIT_APP_ADMIN
                                  WHERE UPPER(TRIM(PF_NUMBER)) = UPPER(TRIM(:p1))
                                    AND NVL(STATUS,'Active') = 'Active'";

            return RunAsync(sql, new object?[] { pf ?? "" }, async cmd =>
            {
                var v = await cmd.ExecuteScalarAsync();
                return v != null && v != DBNull.Value && Convert.ToInt32(v) > 0;
            });
        }

        /// <summary>Dropdown values held in RP_DIT_LOOKUP, so new options need no rebuild.</summary>
        public Task<List<string>> GetLookupValuesAsync(string lookupType)
        {
            const string sql = @"SELECT LOOKUP_VALUE FROM RP_DIT_LOOKUP
                                  WHERE UPPER(LOOKUP_TYPE) = UPPER(:p1)
                                    AND UPPER(NVL(STATUS,'ACTIVE')) = 'ACTIVE'
                                  ORDER BY DISPLAY_ORDER, LOOKUP_VALUE";

            return RunAsync(sql, new object?[] { lookupType ?? "" }, async cmd =>
            {
                var list = new List<string>();
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    if (!rd.IsDBNull(0)) list.Add(rd.GetString(0));
                return list;
            });
        }

        /// <summary>Project names must be unique. Returns the existing code, or null.</summary>
        public Task<string?> FindDuplicateNameAsync(string name, int excludeProjectId)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult<string?>(null);

            const string sql = @"SELECT PROJECT_CODE FROM RP_DIT_PROJECT
                                  WHERE UPPER(TRIM(PROJECT_NAME)) = UPPER(TRIM(:p1))
                                    AND PROJECT_ID <> :p2
                                    AND ROWNUM = 1";

            return RunAsync(sql, new object?[] { name, excludeProjectId }, async cmd =>
            {
                var v = await cmd.ExecuteScalarAsync();
                return v == null || v == DBNull.Value ? null : Convert.ToString(v);
            });
        }

        // ---------------------------------------------------------------
        // ALLOTTED / UTILISED - both keyed on PROJECT_ID now
        // ---------------------------------------------------------------
        public Task<Dictionary<int, decimal>> GetAllotmentsAsync(string budgetType, int fyId)
        {
            const string sql = @"SELECT PROJECT_ID, NVL(SUM(TOTAL_ALLOTTED),0)
                                   FROM RP_DIT_BUDGET_ALLOC
                                  WHERE FY_ID = :p1 AND UPPER(BUDGET_TYPE) = UPPER(:p2)
                                    AND NVL(IS_ACTIVE,'Y') = 'Y' AND PROJECT_ID IS NOT NULL
                               GROUP BY PROJECT_ID";

            return RunAsync(sql, new object?[] { fyId, budgetType }, ReadIntMap);
        }

        public Task<Dictionary<int, decimal>> GetCapitalUtilisedAsync(int fyId)
        {
            const string sql = @"SELECT PROJECT_ID, NVL(SUM(NVL(ACTUAL_SPILLOVER,0) + NVL(ACTUAL_FRESH,0)),0)
                                   FROM RP_DIT_CAPITAL_UTIL
                                  WHERE FY_ID = :p1 AND NVL(IS_ACTIVE,'Y') = 'Y'
                               GROUP BY PROJECT_ID";

            return RunAsync(sql, new object?[] { fyId }, ReadIntMap);
        }

        public Task<Dictionary<int, decimal>> GetRevenueUtilisedAsync(int fyId)
        {
            const string sql = @"SELECT PROJECT_ID, NVL(SUM(NVL(AMOUNT,0)),0)
                                   FROM RP_DIT_REVENUE_UTIL
                                  WHERE FY_ID = :p1 AND NVL(IS_ACTIVE,'Y') = 'Y'
                                    AND PROJECT_ID IS NOT NULL
                               GROUP BY PROJECT_ID";

            return RunAsync(sql, new object?[] { fyId }, ReadIntMap);
        }

        private static async Task<Dictionary<int, decimal>> ReadIntMap(DbCommand cmd)
        {
            var map = new Dictionary<int, decimal>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                map[Convert.ToInt32(r.GetValue(0))] = Dec(r.GetValue(1));
            return map;
        }

        // ---------------------------------------------------------------
        // ONE ALLOCATION  (project + type + financial year)
        // ---------------------------------------------------------------
        public Task<AllocInfo?> GetAllocationAsync(string budgetType, int projectId, int fyId)
        {
            const string sql = @"SELECT SPILLOVER_ALLOTTED, FRESH_ALLOTTED, TOTAL_ALLOTTED, NVL(IS_REVISED,'N')
                                   FROM RP_DIT_BUDGET_ALLOC
                                  WHERE FY_ID = :p1 AND UPPER(BUDGET_TYPE) = UPPER(:p2)
                                    AND PROJECT_ID = :p3 AND NVL(IS_ACTIVE,'Y') = 'Y'";

            return RunAsync(sql, new object?[] { fyId, budgetType, projectId }, async cmd =>
            {
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return (AllocInfo?)null;
                return new AllocInfo(Dec(r.GetValue(0)), Dec(r.GetValue(1)), Dec(r.GetValue(2)),
                                     (r.IsDBNull(3) ? "N" : r.GetString(3)) == "Y");
            });
        }

        /// <summary>How much of this project's budget has already been spent this year.</summary>
        public Task<decimal> GetUtilisedAsync(string budgetType, int projectId, int fyId)
        {
            string sql = budgetType.Equals("CAPITAL", StringComparison.OrdinalIgnoreCase)
                ? @"SELECT NVL(SUM(NVL(ACTUAL_SPILLOVER,0) + NVL(ACTUAL_FRESH,0)),0)
                      FROM RP_DIT_CAPITAL_UTIL
                     WHERE PROJECT_ID = :p1 AND FY_ID = :p2 AND NVL(IS_ACTIVE,'Y') = 'Y'"
                : @"SELECT NVL(SUM(NVL(AMOUNT,0)),0)
                      FROM RP_DIT_REVENUE_UTIL
                     WHERE PROJECT_ID = :p1 AND FY_ID = :p2 AND NVL(IS_ACTIVE,'Y') = 'Y'";

            return RunAsync(sql, new object?[] { projectId, fyId }, async cmd => Dec(await cmd.ExecuteScalarAsync()));
        }

        /// <summary>Insert or update. Returns an error message, or null on success.</summary>
        public async Task<string?> SaveAllocationAsync(string budgetType, int projectId, int fyId,
                                                       decimal spillover, decimal fresh, string changedBy)
        {
            if (projectId <= 0) return "Please save the Basic Details first.";
            if (fyId <= 0) return "Please select a Financial Year.";
            if (spillover < 0 || fresh < 0) return "Budget amounts cannot be negative.";

            decimal total = spillover + fresh;

            var used = await GetUtilisedAsync(budgetType, projectId, fyId);
            if (total < used)
                return $"Allotted budget ({total:N2} Cr) cannot be less than the amount already utilised ({used:N2} Cr).";

            var old = await GetAllocationAsync(budgetType, projectId, fyId);
            bool isRevision = old != null;

            const string updateSql = @"UPDATE RP_DIT_BUDGET_ALLOC
                                          SET SPILLOVER_ALLOTTED = :p1,
                                              FRESH_ALLOTTED     = :p2,
                                              TOTAL_ALLOTTED     = :p3,
                                              IS_REVISED         = 'Y',
                                              REVISED_BY         = :p4,
                                              REVISED_ON         = SYSDATE,
                                              UPDATED_BY         = :p5,
                                              UPDATED_DATE       = SYSDATE
                                        WHERE FY_ID = :p6 AND UPPER(BUDGET_TYPE) = UPPER(:p7)
                                          AND PROJECT_ID = :p8 AND NVL(IS_ACTIVE,'Y') = 'Y'";

            int rows = await RunAsync(updateSql,
                new object?[] { spillover, fresh, total, changedBy, changedBy, fyId, budgetType, projectId },
                cmd => cmd.ExecuteNonQueryAsync());

            if (rows == 0)
            {
                const string insertSql = @"INSERT INTO RP_DIT_BUDGET_ALLOC
                        (FY_ID, BUDGET_TYPE, PROJECT_ID, SECTION_ID,
                         SPILLOVER_ALLOTTED, FRESH_ALLOTTED, TOTAL_ALLOTTED, IS_ACTIVE,
                         IS_REVISED, CREATED_BY, CREATED_DATE)
                        VALUES (:p1, :p2, :p3, NULL, :p4, :p5, :p6, 'Y', 'N', :p7, SYSDATE)";

                await RunAsync(insertSql,
                    new object?[] { fyId, budgetType.ToUpperInvariant(), projectId, spillover, fresh, total, changedBy },
                    cmd => cmd.ExecuteNonQueryAsync());
            }

            const string auditSql = @"INSERT INTO RP_DIT_ALLOC_AUDIT
                    (OLD_SPILLOVER, OLD_FRESH, OLD_TOTAL, NEW_SPILLOVER, NEW_FRESH, NEW_TOTAL, CHANGED_BY, REASON)
                    VALUES (:p1, :p2, :p3, :p4, :p5, :p6, :p7, :p8)";

            await RunAsync(auditSql,
                new object?[]
                {
                    old?.Spillover ?? 0m, old?.Fresh ?? 0m, old?.Total ?? 0m,
                    spillover, fresh, total, changedBy,
                    isRevision ? $"{budgetType} allocation revised" : $"Initial {budgetType} allocation"
                },
                cmd => cmd.ExecuteNonQueryAsync());

            return null;
        }
    }
}