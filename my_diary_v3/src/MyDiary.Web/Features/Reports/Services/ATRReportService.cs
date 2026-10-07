using Microsoft.Data.SqlClient;
using MyDiary.Core.Services;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Features.Reports.Components;
using MyDiary.Web.Features.Reports.Models;
using MyDiary.Web.Features.Shared.Services;
using System.Data;
using System.Globalization;
using System.Reflection;

namespace MyDiary.Web.Features.Reports.Services
{
    /// <summary>
    /// SQL Server-based service for ATM/ATR reports.
    /// Replaces the previous Oracle-based implementation.
    /// Uses DynamicReportService for direct SQL queries (same pattern as SundryReportService).
    /// </summary>
    public class ATRReportService
    {
        private readonly DynamicReportService _db;
        private readonly string _connString;

        public ATRReportService(IConfiguration config, DynamicReportService db)
        {
            _db = db;
            _connString = config.GetConnectionString("SQLServerConnection")
                ?? throw new InvalidOperationException("Missing SQLServerConnection connection string.");
        }

        // --------- SELECT helper (replaces Oracle SelectValues SP) ----------
        // Executes: SELECT {columns} FROM {tableName} WHERE {condition}
        private async Task<DataTable> SelectValuesAsync(string tableName, string columns, string condition)
        {
            AppLogger.LogInfo($"[ATRReportService.SelectValuesAsync] Table={tableName}, Columns={columns}, Condition={condition}");
            var dt = new DataTable();
            var sql = $"SELECT {columns} FROM {tableName} WHERE {condition}";

            await using var conn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(dt);

            AppLogger.LogInfo($"[ATRReportService.SelectValuesAsync] Returned {dt.Rows.Count} rows.");
            return dt;
        }

        // --------- SELECT with ORDER BY (replaces Oracle SelectValuesOrderWise) ----------
        private async Task<DataTable> SelectValuesOrderWiseAsync(string tableName, string columns, string condition, string orderBy)
        {
            var dt = new DataTable();
            var sql = $"SELECT {columns} FROM {tableName} WHERE {condition} ORDER BY {orderBy}";

            await using var conn = new SqlConnection(_connString);
            await using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(dt);

            return dt;
        }

        // --------- INSERT/UPDATE ATR Report Details ----------
        public async Task<int> InsertUpdateATRDetailsAsync(ATRReportDataModel report, bool isSubmit, CancellationToken ct = default)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            AppLogger.LogInfo($"[ATRReportService.InsertUpdateATRDetailsAsync] SolID={report.SOLID}, IsSubmit={isSubmit}");

            bool isInsert = string.IsNullOrWhiteSpace(report.ATR_REPORT_ID);
            string entryMode = isInsert ? "CUR_INSERT_ENTRY" : "CUR_UPDATE_ENTRY";
            string status = isSubmit ? "S" : (isInsert ? "E" : "M");

            string dateTimeEntry = string.IsNullOrWhiteSpace(report.DATE_TIME_ENTRY)
                ? AppTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : report.DATE_TIME_ENTRY;

            string mandatoryReportDate = (report.MANDATORYREPORTDATE == default ? AppTime.Now : report.MANDATORYREPORTDATE)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            await using var conn = new SqlConnection(_connString);
            await conn.OpenAsync(ct);
            await using var tx = conn.BeginTransaction();

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "USP_INSERT_UPDATE_ATRREPORTDETAILS";

                // Header
                cmd.Parameters.AddWithValue("@ATR_Report_ID", (object?)report.ATR_REPORT_ID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SolID", (object?)report.SOLID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Br_Code", (object?)report.BR_CODE ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Br_Name", (object?)report.BR_NAME ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RO_Code", (object?)report.RO_CODE ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RO_Name", (object?)report.RO_NAME ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ZO_Code", (object?)report.ZO_CODE ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ZO_Name", (object?)report.ZO_NAME ?? DBNull.Value);

                // Q1..Q34
                var type = typeof(ATRReportDataModel);
                for (int i = 1; i <= 34; i++)
                {
                    var optProp = type.GetProperty($"Q{i}OPT", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    var cmtProp = type.GetProperty($"Q{i}COMMENT", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    var opt = optProp?.GetValue(report) as string;
                    var cmt = cmtProp?.GetValue(report) as string;

                    cmd.Parameters.AddWithValue($"@Q{i}OPT", (object?)opt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue($"@Q{i}Comment", (object?)cmt ?? DBNull.Value);
                }

                // Meta
                cmd.Parameters.AddWithValue("@PF_NAME_ENTRY", (object?)report.PF_NAME_ENTRY ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DATE_TIME_ENTRY", dateTimeEntry);
                cmd.Parameters.AddWithValue("@MandatoryReportDate", mandatoryReportDate);
                cmd.Parameters.AddWithValue("@ENTRY_BY", (object?)report.ENTRY_BY ?? DBNull.Value);

                // Status / Mode
                cmd.Parameters.AddWithValue("@STATUS", status);
                cmd.Parameters.AddWithValue("@STATUS_DESC", (object?)report.STATUS_DESC ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ENTRY_MODE", entryMode);

                var rows = await cmd.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);

                AppLogger.LogInfo($"[ATRReportService.InsertUpdateATRDetailsAsync] Success. Mode={entryMode}, Status={status}, Rows={rows}");
                return rows;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                AppLogger.LogError(ex, $"[ATRReportService.InsertUpdateATRDetailsAsync] Failed for SolID={report.SOLID}");
                throw;
            }
        }

        // --------- INSERT/UPDATE ATM Report Details ----------
        public async Task<int> InsertUpdateATMDetailsAsync(ATMReportInputModel input, CancellationToken ct = default)
        {
            AppLogger.LogInfo($"[ATRReportService.InsertUpdateATMDetailsAsync] SolID={input.SolID}, ATMID={input.ATMID}");

            await using var conn = new SqlConnection(_connString);
            await conn.OpenAsync(ct);
            await using var tx = conn.BeginTransaction();

            string dateTimeEntry = input.DATE_TIME_ENTRY.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string mandatoryReportDate = input.MandatoryReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);


            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "USP_INSERT_UPDATE_ATMREPORTDETAILS";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@ATM_Report_ID", input.ATM_Report_ID ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@SolID", input.SolID ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Br_Code", input.Br_Code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Br_Name", input.Br_Name ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@RO_Code", input.RO_Code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@RO_Name", input.RO_Name ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ZO_Code", input.ZO_Code ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ZO_Name", input.ZO_Name ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("@Q1OPT", input.Q1OPT ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Q2OPT", input.Q2OPT ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Q3OPT", input.Q3OPT ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Q4OPT", input.Q4OPT ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Q5OPT", input.Q5OPT ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ATM_ID", input.ATMID ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("@PF_NAME_ENTRY", input.PF_NAME_ENTRY ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DATE_TIME_ENTRY", dateTimeEntry);
                cmd.Parameters.AddWithValue("@MandatoryReportDate", mandatoryReportDate);
                cmd.Parameters.AddWithValue("@ENTRY_BY", input.ENTRY_BY ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("@STATUS", input.STATUS ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@STATUS_DESC", input.STATUS_DESC ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ENTRY_MODE", input.ENTRY_MODE ?? (object)DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);

                AppLogger.LogInfo($"[ATRReportService.InsertUpdateATMDetailsAsync] Success. Rows={rows}");
                return rows;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                AppLogger.LogError(ex, $"[ATRReportService.InsertUpdateATMDetailsAsync] Failed for SolID={input.SolID}");
                throw;
            }
        }

        // --------- GET ATR REPORT DATES (for dropdown) ----------
        public async Task<List<string>> GetATRReportDatesAsync(string solId, CancellationToken ct = default)
        {
            AppLogger.LogInfo($"[ATRReportService.GetATRReportDatesAsync] SolID={solId}");
            var dates = new List<string>();
            const string sql = "SELECT WORKING_DAY FROM ATR_WORKING_DAYS WHERE SOL_ID = @pSolID ORDER BY WORKING_DAY DESC";

            await using var conn = new SqlConnection(_connString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pSolID", solId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                dates.Add(reader.GetString(0));
            }

            AppLogger.LogInfo($"[ATRReportService.GetATRReportDatesAsync] Found {dates.Count} dates.");
            return dates;
        }

        // --------- GET ATM IDs for a branch ----------
        public async Task<List<string>> GetATMIdsAsync(string solId, CancellationToken ct = default)
        {
            AppLogger.LogInfo($"[ATRReportService.GetATMIdsAsync] SolID={solId}");
            var ids = new List<string>();
            const string sql = "SELECT ATM_CRM_ID FROM ATM_CRM_List WHERE SOL_ID = @pSolID ORDER BY ATM_CRM_ID";

            await using var conn = new SqlConnection(_connString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pSolID", solId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                ids.Add(reader.GetString(0));
            }

            AppLogger.LogInfo($"[ATRReportService.GetATMIdsAsync] Found {ids.Count} ATM IDs.");
            return ids;
        }

        // --------- GET EXISTING ATM REPORT DATA ----------
        public async Task<ATMReportDataModel?> GetATMReportDataAsync(string solId, string reportDate, string? atmId = null, CancellationToken ct = default)
        {
            AppLogger.LogInfo($"[ATRReportService.GetATMReportDataAsync] SolID={solId}, Date={reportDate}, ATMID={atmId}");

            var condition = $"SolID = '{solId}' AND MandatoryReportDate = '{reportDate}'";
            if (!string.IsNullOrEmpty(atmId))
                condition += $" AND ATMID = '{atmId}'";

            var dt = await SelectValuesAsync("ATMReportData", "*", condition);
            if (dt.Rows.Count == 0)
            {
                AppLogger.LogInfo("[ATRReportService.GetATMReportDataAsync] No existing record found.");
                return null;
            }

            var row = dt.Rows[0];
            return new ATMReportDataModel
            {
                ATM_Report_ID = row["ATM_Report_ID"]?.ToString()?.Trim(),
                SolID = row["SolID"]?.ToString()?.Trim(),
                Br_Code = row["Br_Code"]?.ToString()?.Trim(),
                Br_Name = row["Br_Name"]?.ToString()?.Trim(),
                RO_Code = row["RO_Code"]?.ToString()?.Trim(),
                RO_Name = row["RO_Name"]?.ToString()?.Trim(),
                ZO_Code = row["ZO_Code"]?.ToString()?.Trim(),
                ZO_Name = row["ZO_Name"]?.ToString()?.Trim(),
                MandatoryReportDate = row["MandatoryReportDate"]?.ToString()?.Trim(),
                Q1OPT = row["Q1OPT"]?.ToString()?.Trim(),
                Q2OPT = row["Q2OPT"]?.ToString()?.Trim(),
                Q3OPT = row["Q3OPT"]?.ToString()?.Trim(),
                Q4OPT = row["Q4OPT"]?.ToString()?.Trim(),
                Q5OPT = row["Q5OPT"]?.ToString()?.Trim(),
                ATMID = row["ATMID"]?.ToString()?.Trim(),
                PF_NAME_ENTRY = row["PF_NAME_ENTRY"]?.ToString()?.Trim(),
                STATUS = row["STATUS"]?.ToString()?.Trim(),
                ENTRY_BY = row["ENTRY_BY"]?.ToString()?.Trim(),
            };
        }

        // --------- GET EXISTING ATR REPORT DATA ----------
        public async Task<ATRReportDataModel?> GetATRReportDataAsync(string solId, string reportDate, CancellationToken ct = default)
        {
            AppLogger.LogInfo($"[ATRReportService.GetATRReportDataAsync] SolID={solId}, Date={reportDate}");

            var condition = $"SolID = '{solId}' AND MandatoryReportDate = '{reportDate}'";
            var dt = await SelectValuesAsync("ATRReportData", "*", condition);
            if (dt.Rows.Count == 0)
            {
                AppLogger.LogInfo("[ATRReportService.GetATRReportDataAsync] No existing record found.");
                return null;
            }

            var row = dt.Rows[0];
            var model = new ATRReportDataModel
            {
                ATR_REPORT_ID = row["ATR_Report_ID"]?.ToString()?.Trim(),
                SOLID = row["SolID"]?.ToString()?.Trim(),
                BR_NAME = row["Br_Name"]?.ToString()?.Trim(),
                RO_NAME = row["RO_Name"]?.ToString()?.Trim(),
                ZO_NAME = row["ZO_Name"]?.ToString()?.Trim(),
                PF_NAME_ENTRY = row["PF_NAME_ENTRY"]?.ToString()?.Trim(),
                STATUS = row["STATUS"]?.ToString()?.Trim(),
            };

            // Load Q1..Q34
            for (int i = 1; i <= 34; i++)
            {
                var optCol = $"Q{i}OPT";
                var cmtCol = $"Q{i}Comment";
                if (dt.Columns.Contains(optCol))
                {
                    var prop = typeof(ATRReportDataModel).GetProperty($"Q{i}OPT");
                    prop?.SetValue(model, row[optCol]?.ToString()?.Trim());
                }
                if (dt.Columns.Contains(cmtCol))
                {
                    var prop = typeof(ATRReportDataModel).GetProperty($"Q{i}COMMENT");
                    prop?.SetValue(model, row[cmtCol]?.ToString()?.Trim());
                }
            }

            return model;
        }

        // --------- CHECK EXISTING REPORT BY (SolID, MandatoryReportDate, ATMID) ----------
        public async Task<string?> GetExistingReportIdAsync(
            string solId,
            DateTime mandatoryReportDate,
            string atmId,
            CancellationToken ct = default)
        {
            AppLogger.LogInfo($"[ATRReportService.GetExistingReportIdAsync] SolID={solId}, Date={mandatoryReportDate:yyyy-MM-dd}, ATMID={atmId}");

            const string sql = @"
                SELECT TOP 1 ATM_Report_ID
                FROM ATMReportData
                WHERE SolID = @pSolID
                  AND CAST(MandatoryReportDate AS DATE) = @pMandatoryReportDate
                  AND ATMID = @pATMID";

            await using var conn = new SqlConnection(_connString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@pSolID", solId);
            cmd.Parameters.AddWithValue("@pMandatoryReportDate", mandatoryReportDate.Date);
            cmd.Parameters.AddWithValue("@pATMID", atmId);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result == null || result is DBNull ? null : Convert.ToString(result);
        }

        // --------- GET ATR MIS REPORT DATA (ATRMISReport.aspx logic) ----------
        // Uses direct SQL SELECT with JOINs, matching the ASPX SelectValues calls exactly
        public async Task<List<Dictionary<string, object>>> GetATRMISReportDataAsync(
            string privilege, string reportDate,
            string? zoneSolid, string? regionSolid, string? branchSolId)
        {
            AppLogger.LogInfo($"[ATRReportService.GetATRMISReportDataAsync] Privilege={privilege}, Date={reportDate}, Zone={zoneSolid}, Region={regionSolid}, Branch={branchSolId}");

            string baseTable = "ATRReportData A JOIN Branch_Master C ON A.SolId = C.Sol_Id";
            string baseCondition = $"region_solid <> '00000' AND region_solid <> ' ' AND MandatoryReportDate = '{reportDate}'";
            string columns;
            string orderBy = " ORDER BY Zone_Name_Eng, Region_Name, Branch_Name";

            switch (privilege.ToUpper())
            {
                case "CO":
                    if (string.IsNullOrEmpty(zoneSolid) || zoneSolid == "-1" || zoneSolid == "0")
                    {
                        // All summary
                        columns = "A.*, Branch_Name, region_name, zone_name_eng, SolId";
                    }
                    else if (zoneSolid.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(regionSolid) || regionSolid == "0")
                        {
                            columns = "A.*, Branch_Name, region_name, zone_name_eng, Zone_SolId As SolId";
                        }
                        else if (regionSolid.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(branchSolId) || branchSolId == "0")
                                columns = "A.*, Branch_Name, region_name, zone_name_eng, region_SolId As SolId";
                            else
                                columns = "A.*, Branch_Name, region_name, zone_name_eng, Sol_Id As SolId";
                        }
                        else
                        {
                            baseCondition += $" AND region_solid = '{regionSolid}'";
                            if (!string.IsNullOrEmpty(branchSolId) && branchSolId != "0" && !branchSolId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                                baseCondition += $" AND sol_id = '{branchSolId}'";
                            columns = "A.*, Branch_Name, region_name, zone_name_eng, Sol_Id As SolId";
                        }
                    }
                    else
                    {
                        // Specific zone
                        baseCondition += $" AND zone_solid = '{zoneSolid}'";
                        if (!string.IsNullOrEmpty(regionSolid) && regionSolid != "0" && !regionSolid.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        {
                            baseCondition += $" AND region_solid = '{regionSolid}'";
                            if (!string.IsNullOrEmpty(branchSolId) && branchSolId != "0" && !branchSolId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                                baseCondition += $" AND sol_id = '{branchSolId}'";
                        }
                        columns = "A.*, Branch_Name, region_name, zone_name_eng, Sol_Id As SolId";
                    }
                    break;

                case "ZONE":
                    baseCondition += $" AND Zone_solid = '{zoneSolid}'";
                    if (!string.IsNullOrEmpty(regionSolid) && regionSolid != "0" && !regionSolid.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    {
                        baseCondition += $" AND region_solid = '{regionSolid}'";
                        if (!string.IsNullOrEmpty(branchSolId) && branchSolId != "0" && !branchSolId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                            baseCondition += $" AND sol_id = '{branchSolId}'";
                    }
                    columns = "A.*, Branch_Name, region_name, zone_name_eng, Sol_Id As SolId";
                    break;

                case "REGION":
                    baseCondition += $" AND region_solid = '{regionSolid}'";
                    if (!string.IsNullOrEmpty(branchSolId) && branchSolId != "0" && !branchSolId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        baseCondition += $" AND sol_id = '{branchSolId}'";
                    columns = "A.*, Branch_Name, region_name, zone_name_eng, Sol_Id As SolId";
                    break;

                case "BRANCH":
                    baseCondition += $" AND sol_id = '{branchSolId}'";
                    columns = "A.*, Branch_Name, region_name, zone_name_eng, Sol_Id As SolId";
                    break;

                default:
                    // Fail closed: an unknown/empty privilege must NOT return all-zone/all-region/all-branch data (that was the RBAC leak).
                    columns = "A.*, Branch_Name, region_name, zone_name_eng, Sol_Id As SolId";
                    if (!string.IsNullOrEmpty(branchSolId) && branchSolId != "0" && !branchSolId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        baseCondition += $" AND sol_id = '{branchSolId}'";
                    else
                        baseCondition += " AND 1 = 0";
                    AppLogger.LogError(new InvalidOperationException("Unknown privilege"),
                        $"[ATRReportService.GetATRMISReportDataAsync] Unknown privilege '{privilege}' — returning scoped/empty result to avoid data leak.");
                    break;
            }

            string query = $"SELECT {columns} FROM {baseTable} WHERE {baseCondition}{orderBy}";
            AppLogger.LogInfo($"[ATRReportService.GetATRMISReportDataAsync] Executing query.");
            return await _db.ExecuteRawQueryAsync(query);
        }

        // --------- GET STAFF NAME ----------
        public async Task<string> GetStaffNameAsync(string pfNumber)
        {
            var dt = await SelectValuesAsync("StaffDetails", "NAME", $"EMPLID = '{pfNumber}'");
            return dt.Rows.Count > 0 ? dt.Rows[0]["NAME"]?.ToString()?.Trim() ?? "" : "";
        }

        // --------- VALIDATE BH/DBH ----------
        public async Task<string> ValidateBHDBHAsync(string pfNumber, string bhDbhCodes)
        {
            var dt = await SelectValuesAsync("StaffDetails", "EMP_DESGN", $"EMPLID = '{pfNumber}'");
            if (dt.Rows.Count > 0)
            {
                var desgn = dt.Rows[0]["EMP_DESGN"]?.ToString()?.Trim() ?? "";
                if (bhDbhCodes.Contains(desgn))
                    return "";
                return "Report Can be submitted by BH/DBH only.";
            }
            return "Report Can be submitted by BH/DBH only.";
        }
    }
}
