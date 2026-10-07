using MyDiary.Web.Core.Extensions;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Features.Shared.Models;
using OfficeOpenXml;
using System.Data;

namespace MyDiary.Web.Features.Shared.Services
{
    public class DynamicReportService
    {
        private readonly string _connectionString;
        public DynamicReportService(IConfiguration config) => _connectionString = config.GetConnectionString("SQLServerConnection");

        public async Task<string> GetAsOnDateAsync(string tableName)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[\w\.]+$"))
                throw new ArgumentException("Invalid table name", nameof(tableName));

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var cmd = new SqlCommand($"SELECT convert(varchar, max(LastUpdateDate), 103) FROM {tableName}", conn) { CommandTimeout = 10 };
                await conn.OpenAsync();
                return (await cmd.ExecuteScalarAsync())?.ToString() ?? "N/A";
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"[DynamicReportService] GetAsOnDateAsync failed for table: {tableName}");
                return "N/A";
            }
        }

        public async Task<List<Dictionary<string, object>>> GetLookupDataAsync(string tableName, string columns, string condition, string order)
        {
            var rows = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("SelectValuesOrderWise", conn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ParamterValues", columns);
                cmd.Parameters.AddWithValue("@Condt", condition);
                cmd.Parameters.AddWithValue("@OrderWise", order);
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++) row.Add(reader.GetName(i), reader.GetValue(i));
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"[DynamicReportService] GetLookupDataAsync failed for table: {tableName}");
            }
            return rows;
        }

        public async Task<List<Dictionary<string, object>>> ExecuteRawQueryAsync(string query)
        {
            var rows = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(query, conn) { CommandTimeout = 30 };
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++) row.Add(reader.GetName(i), reader.GetValue(i));
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"[DynamicReportService] ExecuteRawQueryAsync failed. Query: {query[..Math.Min(query.Length, 200)]}");
            }
            return rows;
        }

        public async Task<List<Dictionary<string, object>>> ExecuteStoredProcAsync(string procName,IDictionary<string, object>? parameters = null,int commandTimeoutSeconds = 30)
        {
            var rows = new List<Dictionary<string, object>>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(procName, conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = commandTimeoutSeconds
                };

                if (parameters != null)
                {
                    foreach (var kvp in parameters)
                    {
                        var name = kvp.Key.StartsWith("@", StringComparison.Ordinal) ? kvp.Key : "@" + kvp.Key;
                        cmd.Parameters.AddWithValue(name, kvp.Value ?? DBNull.Value);
                    }
                }
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null! : reader.GetValue(i);
                    }
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"[DynamicReportService] ExecuteStoredProcAsync failed for proc: {procName}");
            }
            return rows;
        }

        public byte[] ExportToExcel(List<Dictionary<string, object>> data, List<ColumnConfig> columns, string sheetName)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(sheetName);
            for (int i = 0; i < columns.Count; i++)
            {
                ws.Cells[1, i + 1].Value = columns[i].Header;
                ws.Cells[1, i + 1].Style.Font.Bold = true;
            }
            for (int r = 0; r < data.Count; r++)
            {
                for (int c = 0; c < columns.Count; c++)
                {
                    var key = columns[c].PropertyName;
                    ws.Cells[r + 2, c + 1].Value = data[r].ContainsKey(key) ? data[r][key] : "";
                }
            }
            ws.Cells.AutoFitColumns();
            return package.GetAsByteArray();
        }

        // -------------------------
        // ATR specific (kept intact)
        // -------------------------

        public class AtrReportResult
        {
            public List<Dictionary<string, object>> Summary { get; set; } = new();
            public List<Dictionary<string, object>> Details { get; set; } = new();
        }

        /// <summary>
        /// UNSAFE if used with user input. Prefer the parameterized overload below.
        /// </summary>
        //public async Task<List<Dictionary<string, object>>> ExecuteRawQueryAsync(string query)
        //    => await ExecuteRawQueryAsync(query, null);

        /// <summary>
        /// SAFE parameterized execution of raw SQL text.
        /// Example:
        /// await ExecuteRawQueryAsync("SELECT * FROM T WHERE d=@d", new Dictionary{["@d"]= "01-28-2026"});
        /// </summary>
        public async Task<List<Dictionary<string, object>>> ExecuteRawQueryAsync(
            string sql,
            IDictionary<string, object?>? parameters)
        {
            var rows = new List<Dictionary<string, object>>(capacity: 256);

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text, CommandTimeout = 30 };

            if (parameters is not null)
            {
                foreach (var kv in parameters)
                {
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                }
            }

            await conn.OpenAsync().ConfigureAwait(false);
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = await reader.IsDBNullAsync(i).ConfigureAwait(false) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = val ?? "";
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// Final: Calls your SP: usp_getATRStatusReport(@User_Type,@Selected_ZO,@Selected_RO,@Selected_BR,@Report_Date)
        /// NOTE:
        /// - userType:   "CO","ZO","RO","BR" (mapped from privilege)
        /// - selectedZO: Zone parameter ("-1","0","ALL", or specific zone_id/solid)
        /// - selectedRO: Region parameter ("", "0","ALL", or specific region_solid)
        /// - selectedBR: Branch parameter ("", "0","ALL", or specific sol_id)
        /// - reportDate: "MM-dd-yyyy"
        /// </summary>
        public async Task<AtrReportResult> ExecuteGetAtrReportStatusAsync(
            string reportDate, string userType, string selectedZO, string selectedRO, string selectedBR,
            CancellationToken ct = default)
        {
            var ds = new DataSet();

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("usp_getATRStatusReport", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@User_Type", userType ?? string.Empty);
                cmd.Parameters.AddWithValue("@Selected_ZO", selectedZO ?? string.Empty);
                cmd.Parameters.AddWithValue("@Selected_RO", selectedRO ?? string.Empty);
                cmd.Parameters.AddWithValue("@Selected_BR", selectedBR ?? string.Empty);
                cmd.Parameters.AddWithValue("@Report_Date", reportDate ?? string.Empty);

                using var da = new SqlDataAdapter(cmd);
                await Task.Run(() => da.Fill(ds), ct).ConfigureAwait(false);
            }

            var res = new AtrReportResult();
            if (ds.Tables.Count > 0)
                res.Summary = ToDictList(ds.Tables[0]);
            if (ds.Tables.Count > 1)
                res.Details = ToDictList(ds.Tables[1]);

            // Normalize for UI bindings
            EnsureAlias(res.Summary, from: "sol_Id", to: "solid");
            EnsureAlias(res.Details, from: "sol_Id", to: "sol_id");

            return res;
        }

        private static List<Dictionary<string, object>> ToDictList(DataTable dt)
        {
            var list = new List<Dictionary<string, object>>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn c in dt.Columns)
                {
                    dict[c.ColumnName] = r[c] is DBNull ? "" : r[c]!;
                }
                list.Add(dict);
            }
            return list;
        }

        private static void EnsureAlias(List<Dictionary<string, object>> rows, string from, string to)
        {
            foreach (var d in rows)
            {
                if (!d.ContainsKey(to) && d.TryGetValue(from, out var val))
                    d[to] = val ?? "";
            }
        }

        /// <summary>
        /// Two-sheet export (Summary + BranchWise), used by ATR.
        /// </summary>
        public byte[] ExportTwoSheets(
            (string name, List<Dictionary<string, object>> rows, List<ColumnConfig> cols) summary,
            (string name, List<Dictionary<string, object>> rows, List<ColumnConfig> cols) detail)
        {
            using var package = new ExcelPackage();

            void WriteSheet(string sheetName, List<Dictionary<string, object>> data, List<ColumnConfig> cols)
            {
                var ws = package.Workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet" : sheetName);

                // Headers
                for (int c = 0; c < cols.Count; c++)
                {
                    ws.Cells[1, c + 1].Value = cols[c].Header ?? cols[c].PropertyName;
                    ws.Cells[1, c + 1].Style.Font.Bold = true;
                }

                // Rows
                for (int r = 0; r < data.Count; r++)
                {
                    var row = data[r];
                    for (int c = 0; c < cols.Count; c++)
                    {
                        var col = cols[c];
                        row.TryGetValue(col.PropertyName, out var raw);
                        var cell = ws.Cells[r + 2, c + 1];

                        if (raw is null || raw is DBNull)
                        {
                            cell.Value = null;
                            continue;
                        }

                        if (col.IsNumeric && decimal.TryParse(raw.ToString(), out var n))
                        {
                            cell.Value = n;
                            if (!string.IsNullOrWhiteSpace(col.Format))
                                cell.Style.Numberformat.Format = col.Format;
                        }
                        else if (col.IsDate && DateTime.TryParse(raw.ToString(), out var dt))
                        {
                            cell.Value = dt;
                            cell.Style.Numberformat.Format = "yyyy-mm-dd";
                        }
                        else
                        {
                            cell.Value = raw?.ToString();
                        }
                    }
                }

                ws.Cells.AutoFitColumns();
            }

            WriteSheet(summary.name, summary.rows, summary.cols);
            WriteSheet(detail.name, detail.rows, detail.cols);

            return package.GetAsByteArray();
        }

        public async Task<List<List<Dictionary<string, object>>>> ExecuteStoredProcedureMultiAsync(
            string storedProcName, IDictionary<string, object> parameters)
        {
            var result = new List<List<Dictionary<string, object>>>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(storedProcName, conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 30
                };

                foreach (var p in parameters)
                    cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                do
                {
                    var rows = new List<Dictionary<string, object>>();
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null! : reader.GetValue(i);
                        }
                        rows.Add(row);
                    }
                    result.Add(rows);
                } while (await reader.NextResultAsync());
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"[DynamicReportService] ExecuteStoredProcedureMultiAsync failed for proc: {storedProcName}");
            }
            return result;
        }


        public async Task<DataSet> GetATMVisitReportDailyDashboardDataSetAsync(
               string reportDate, string userType, string zo, string ro, string br, CancellationToken ct = default)
        {
            var ds = new DataSet();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("usp_getATMVisitReportDailyDashboard", conn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30
            };

            cmd.Parameters.AddWithValue("@User_Type", (object?)userType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Selected_ZO", (object?)zo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Selected_RO", (object?)ro ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Selected_BR", (object?)br ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Report_Date", (object?)reportDate ?? DBNull.Value);

            using var sda = new SqlDataAdapter(cmd);
            // Fill is synchronous; wrap in Task.Run to avoid blocking the UI thread.
            await Task.Run(() => sda.Fill(ds), ct);

            return ds;
        }

        public async Task<List<Dictionary<string, object>>> InsertQlikTicketdetails(string tableName, string strParameters, string strParameterValue)
        {
            var rows = new List<Dictionary<string, object>>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();
            try
            {
                using var cmd = new SqlCommand("AddValues", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Transaction = trans;
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@Parameters", strParameters);
                cmd.Parameters.AddWithValue("@ParameterValue", strParameterValue);
                int intReturn = await cmd.ExecuteNonQueryAsync();
                trans.Commit();
                var row = new Dictionary<string, object>();
                row.Add("retVal", intReturn);
                rows.Add(row);
                return rows;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DynamicReportService] Insert error");
                trans.Rollback();
                var row = new Dictionary<string, object>();
                row.Add("Insert Error", UserFacingError.Generic);
                rows.Add(row);
                return rows;
            }
            finally
            {
                conn.Close();
            }
        }
    }
}