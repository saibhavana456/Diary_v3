#nullable enable
using System.Data;
using Microsoft.VisualBasic;
using MudBlazor;
using MyDiary.Core.Services;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Dashboards.Models;

namespace MyDiary.Web.Features.Dashboards.Services
{
    /// <summary>Which hub is asking — maps to a SHOW_IN_&lt;HUB&gt;_HUB column
    /// on MYDIARYDB.APP_USERS_ROLE_ACCESS_MENU_MASTER (see RP_46 migration).</summary>
    public enum DashboardHub
    {
        Assurance,
        Business
    }

    public interface IDashboardsService
    {
        Task<List<RecordData>> GetAllDashboardsAsync();
        Task<List<RecordData>> GetRecordDataAsync(string menu);

        /// <summary>Cards/tabs for a single hub — membership is entirely
        /// DB-driven via SHOW_IN_ASSURANCE_HUB / SHOW_IN_BUSINESS_HUB,
        /// no hardcoded category list in code.</summary>
        Task<List<RecordData>> GetHubDashboardsAsync(DashboardHub hub);
    }

    public class DashboardsServices : IDashboardsService
    {
        private readonly OracleDbProvider _dbProvider;
        private const string DefaultDashboardUrl = "http://localhost:5100/loginDashboard";

        // ✅ Turn this ON to fetch all rows and print debug values in UI
        private static bool DiagnosticMode = true;

        private const string SelectColumns = @"
            SELECT MENU_NAME, MENU_URL, MENU_ICONS,
                   OWNER_VERTICAL, SOURCE, FREQUENCY_OF_REPORT_UPDATION,
                   PARENT_MENU_CODE, IS_COMPLETED, MENU_CATEGORY, MENU_SUBCATEGORY,
                   MENU_ROUTE
              FROM MYDIARYDB.APP_USERS_ROLE_ACCESS_MENU_MASTER";

        public DashboardsServices(OracleDbProvider dbProvider)
        {
            _dbProvider = dbProvider;
        }

        public async Task<List<RecordData>> GetAllDashboardsAsync()
        {
            string sql = $@"{SelectColumns}
                         WHERE IS_COMPLETED = 'Y'
                           AND PARENT_MENU_CODE <> 'IN_HOUSE_PORTALS'
                         ORDER BY MENU_NAME";

            return await RunAsync(sql, "menu");
        }

        public async Task<List<RecordData>> GetHubDashboardsAsync(DashboardHub hub)
        {
            // Column name is a fixed, hardcoded literal from the enum — not
            // user input — so string-building it here is safe (ExecuteQueryAsync
            // has no parameter support to bind it as a param instead).
            var hubColumn = hub switch
            {
                DashboardHub.Assurance => "SHOW_IN_ASSURANCE_HUB",
                DashboardHub.Business  => "SHOW_IN_BUSINESS_HUB",
                _ => throw new ArgumentOutOfRangeException(nameof(hub))
            };

            string sql = $@"{SelectColumns}
                         WHERE IS_COMPLETED = 'Y'
                           AND PARENT_MENU_CODE <> 'IN_HOUSE_PORTALS'
                           AND {hubColumn} = 'Y'
                         ORDER BY MENU_NAME";

            return await RunAsync(sql, $"hub '{hub}'");
        }

        public async Task<List<RecordData>> GetRecordDataAsync(string menu)
        {
            var type = GetPageType(menu);

            string sql;
            if (DiagnosticMode || type == "ALL")
            {
                sql = $@"{SelectColumns}
                     WHERE IS_COMPLETED = 'Y'
                     ORDER BY MENU_NAME";
            }
            else
            {
                sql = $@"{SelectColumns}
                     WHERE PARENT_MENU_CODE = '{type}'
                       AND IS_COMPLETED = 'Y'
                     ORDER BY MENU_NAME";
            }

            return await RunAsync(sql, $"menu '{menu}' (type '{type}')");
        }

        private async Task<List<RecordData>> RunAsync(string sql, string context)
        {
            DataTable dt;
            try
            {
                dt = await _dbProvider.ExecuteQueryAsync(sql);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"DB call failed for {context}.", ex);
            }

            var colsReturned = string.Join(", ", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            string[] expected = ["OWNER_VERTICAL", "SOURCE", "FREQUENCY_OF_REPORT_UPDATION"];
            var missing = expected.Where(ec => !dt.Columns.Cast<DataColumn>().Any(c => c.ColumnName.Equals(ec, StringComparison.OrdinalIgnoreCase))).ToList();

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Expected column(s) missing: {string.Join(", ", missing)} | Returned: {colsReturned}. " +
                    $"Check schema/migration."
                );
            }

            var list = new List<RecordData>(dt.Rows.Count);

            foreach (DataRow row in dt.Rows)
            {
                // Read strings (case-insensitive + Trim)
                string? GetStr(string c) => GetString(row, c)?.Trim();

                var menuName = GetStr("MENU_NAME") ?? "—";
                var url = GetStr("MENU_URL") ?? DefaultDashboardUrl;
                var iconRaw = GetStr("MENU_ICONS") ?? string.Empty;
                var ownerVert = GetStr("OWNER_VERTICAL");
                var source = GetStr("SOURCE");
                var freq = GetStr("FREQUENCY_OF_REPORT_UPDATION");

                // Build a per-row diagnostic string we can show on the card
                var pcode = GetStr("PARENT_MENU_CODE");
                var completed = GetStr("IS_COMPLETED");
                var debugInfo = $"[MENU_NAME='{menuName}'; PARENT='{pcode}'; IS_COMPLETED='{completed}'; " +
                                 $"OWNER_VERTICAL='{ownerVert ?? "<null>"}'; SOURCE='{source ?? "<null>"}'; FREQ='{freq ?? "<null>"}']";
                var menuCategory = GetStr("MENU_CATEGORY") ?? string.Empty;
                var menuSubCategory = GetStr("MENU_SUBCATEGORY") ?? string.Empty;
                var menuRoute = GetStr("MENU_ROUTE");

                var record = new RecordData(
                    Name: menuName,
                    DateOfData: DateOnly.FromDateTime(AppTime.Today),
                    Location: string.Empty,
                    Owner: string.Empty,
                    icons: GetMudIcon(iconRaw),
                    link: url,
                    menuCategory: menuCategory,
                    menuSubCategory: menuSubCategory
                )
                {
                    OwnerVertical = ownerVert,
                    Source = source,
                    FrequencyOfReportUpdation = freq,
                    MenuRoute = menuRoute,

                    // ⬇️ new temporary diagnostic field to expose data on UI
                    DebugInfo = debugInfo
                };

                list.Add(record);
            }

            return list;
        }

        // Column reader: case-insensitive, null-safe
        private static string? GetString(DataRow row, string wantedName)
        {
            if (row.Table.Columns.Contains(wantedName))
            {
                var v = row[wantedName];
                return v == DBNull.Value ? null : Convert.ToString(v);
            }
            var col = row.Table.Columns
                .Cast<DataColumn>()
                .FirstOrDefault(c => string.Equals(c.ColumnName, wantedName, StringComparison.OrdinalIgnoreCase));
            if (col == null) return null;
            var val = row[col];
            return val == DBNull.Value ? null : Convert.ToString(val);
        }

        public string GetMudIcon(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName))
                return Icons.Material.Outlined.Help;

            var clean = iconName
                .Replace("Icons.Material.Outlined.", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Icons.Material.Filled.", "", StringComparison.OrdinalIgnoreCase)
                .Replace("MudBlazor.", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            var outlined = typeof(Icons.Material.Outlined).GetField(clean);
            if (outlined?.GetValue(null) is string ov) return ov;

            var filled = typeof(Icons.Material.Filled).GetField(clean);
            if (filled?.GetValue(null) is string fv) return fv;

            return Icons.Material.Outlined.Help;
        }

        public string GetPageType(string menu) => menu switch
        {
            "All" => "ALL",
            "Dash" => "DASHBOARDS",
            "AppDashboard" => "DASHBOARDS",
            "Utilities" => "UTILITIES",
            "Alerts" => "ALERTS",
            "InHousePortals" => "IN_HOUSE_PORTALS",
            "Leads" => "LEADS_DATA",
            "Reports" => "REPORTS",
            "DefaultAccounts" => "DEFAULTING_ACCOUNTS",
            "SUDCorner" => "SUDCORNER",
            "Complaints" => "COMPLAINTS",
            "Clearing" => "CLEARING",
            _ => "DASHBOARDS"
        };
    }
}
