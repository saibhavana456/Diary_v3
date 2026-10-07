using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using Oracle.ManagedDataAccess.Client;
using Renci.SshNet;
using RequestPortal.Core.Models;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Text;

namespace MyDiary.Web.Features.ReportModule.Services;

/// <summary>
/// Executes a saved ReportDef against its configured source and returns a
/// generic tabular result. Every source is capped at <see cref="MaxRows"/> rows
/// so the screen never has to render more than the agreed ceiling.
/// </summary>
public sealed class ReportExecutionService
{
    public const int MaxRows = 200;

    private readonly IConfiguration _config;

    public ReportExecutionService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// filterValues: ColumnName → value the caller entered, for whichever of the report's
    /// admin-defined ReportFilterDef rows the user actually filled in. Only applies to the
    /// three database source types (Zone/Region/Branch/Custom filters have no meaning against
    /// a flat file/SFTP download) — ignored for FilePath/Sftp sources.
    /// </summary>
    /// <summary>
    /// Every configured ConnectionStrings key, for the Report Developer "Connection Name"
    /// dropdown — the admin sees everything available and picks the one appropriate to the
    /// source type they chose, rather than the app guessing a single fixed mapping.
    /// </summary>
    public IReadOnlyList<string> ListAvailableConnectionNames()
        => _config.GetSection("ConnectionStrings").GetChildren()
            .Select(c => c.Key)
            .Where(k => !k.StartsWith("__", StringComparison.Ordinal) && k != "//")
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<ReportResult> ExecuteAsync(ReportDef def, IReadOnlyDictionary<string, string?>? filterValues = null, CancellationToken ct = default)
    {
        if (!def.IsActive)
            return new ReportResult { Disabled = true, Error = "Report disabled" };

        try
        {
            var result = def.SourceType switch
            {
                ReportSourceType.OracleDb => await RunOracleAsync(def, filterValues, ct),
                ReportSourceType.SqlServerDb => await RunSqlServerAsync(def, filterValues, ct),
                ReportSourceType.Impala => await RunImpalaAsync(def, filterValues, ct),
                ReportSourceType.FilePath => ReadFile(File.OpenRead(def.EffectiveSource().FilePath ?? throw new InvalidOperationException("File path not configured.")), def.FileType),
                ReportSourceType.Sftp => await ReadSftpAsync(def),
                _ => new ReportResult { Error = "Unsupported source type." }
            };
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "ReportExecutionService: report execution failed");
            return new ReportResult { Error = UserFacingError.Generic };
        }
    }

    // ── Oracle DB ────────────────────────────────────────────────────────────
    private async Task<ReportResult> RunOracleAsync(ReportDef def, IReadOnlyDictionary<string, string?>? filterValues, CancellationToken ct)
    {
        var conn = GetReportConnectionString(def.ConnectionName);
        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(def.SqlQuery))
            return new ReportResult { Error = $"No connection selected/configured for '{def.ConnectionName}', or query is empty ({SiteLabel(def)})." };

        var filters = BuildFilterFragments(filterValues, "fp");

        await using var oracleConn = new OracleConnection(conn);
        await oracleConn.OpenAsync(ct);
        using var cmd = oracleConn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM ({StripTrailingSemicolon(def.SqlQuery)}) t WHERE ROWNUM <= {MaxRows}" +
            string.Concat(filters.Select(f => $" AND {f.Column} = :{f.ParamName}"));
        foreach (var f in filters) cmd.Parameters.Add(new OracleParameter(f.ParamName, f.Value));
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return ReadGeneric(reader);
    }

    // ── SQL Server DB ────────────────────────────────────────────────────────
    private async Task<ReportResult> RunSqlServerAsync(ReportDef def, IReadOnlyDictionary<string, string?>? filterValues, CancellationToken ct)
    {
        var conn = GetReportConnectionString(def.ConnectionName);
        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(def.SqlQuery))
            return new ReportResult { Error = $"No connection selected/configured for '{def.ConnectionName}', or query is empty ({SiteLabel(def)})." };

        var filters = BuildFilterFragments(filterValues, "fp");

        await using var sqlConn = new SqlConnection(conn);
        await sqlConn.OpenAsync(ct);
        using var cmd = sqlConn.CreateCommand();
        cmd.CommandText = $"SELECT TOP {MaxRows} * FROM ({StripTrailingSemicolon(def.SqlQuery)}) AS q WHERE 1=1" +
            string.Concat(filters.Select(f => $" AND {f.Column} = @{f.ParamName}"));
        foreach (var f in filters) cmd.Parameters.AddWithValue($"@{f.ParamName}", (object?)f.Value ?? DBNull.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return ReadGeneric(reader);
    }

    // ── Hive/Impala via Apache Knox (Cloudera Impala ODBC driver, basic auth over HTTP) ─
    private async Task<ReportResult> RunImpalaAsync(ReportDef def, IReadOnlyDictionary<string, string?>? filterValues, CancellationToken ct)
    {
        var conn = GetReportConnectionString(def.ConnectionName);
        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(def.SqlQuery))
            return new ReportResult { Error = $"No connection selected/configured for '{def.ConnectionName}', or query is empty ({SiteLabel(def)})." };

        var filters = BuildFilterFragments(filterValues, "fp");

        await using var odbcConn = new OdbcConnection(conn);
        await odbcConn.OpenAsync(ct);
        using var cmd = odbcConn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM ({StripTrailingSemicolon(def.SqlQuery)}) t WHERE 1=1" +
            string.Concat(filters.Select(f => $" AND {f.Column} = ?")) + $" LIMIT {MaxRows}";
        foreach (var f in filters) cmd.Parameters.AddWithValue("?", f.Value ?? (object)DBNull.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return ReadGeneric(reader);
    }

    private string? GetReportConnectionString(string? connectionName)
        => string.IsNullOrWhiteSpace(connectionName) ? null : _config.GetConnectionString(connectionName);

    /// <summary>
    /// Turns the caller-supplied filter values into (Column, ParamName, Value) triples, skipping
    /// blanks and any column name that isn't a plain SQL identifier — column names come from the
    /// admin-defined ReportFilterDef.ColumnName and can't be bind-parameterized like values, so
    /// they're validated instead of interpolated blindly.
    /// </summary>
    private static List<(string Column, string ParamName, string? Value)> BuildFilterFragments(
        IReadOnlyDictionary<string, string?>? filterValues, string paramPrefix)
    {
        var result = new List<(string, string, string?)>();
        if (filterValues is null) return result;

        int i = 0;
        foreach (var kv in filterValues)
        {
            if (string.IsNullOrWhiteSpace(kv.Value)) continue;
            if (!IsValidColumnName(kv.Key)) continue;
            result.Add((kv.Key, $"{paramPrefix}{i}", kv.Value));
            i++;
        }
        return result;
    }

    private static bool IsValidColumnName(string col) =>
        System.Text.RegularExpressions.Regex.IsMatch(col, "^[A-Za-z_][A-Za-z0-9_]*$");

    private static string SiteLabel(ReportDef def) => def.RunningFrom == ReportSite.Dr ? "DR" : "DC";

    private static string StripTrailingSemicolon(string sql) => sql.TrimEnd().TrimEnd(';');

    private static ReportResult ReadGeneric(IDataReader reader)
    {
        var result = new ReportResult();
        for (int i = 0; i < reader.FieldCount; i++)
            result.Columns.Add(reader.GetName(i));

        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result.Rows.Add(row);
        }
        return result;
    }

    // ── Server file path / SFTP file → XLSX or CSV ──────────────────────────
    private static ReportResult ReadFile(Stream stream, string? fileType)
    {
        using (stream)
        {
            return (fileType ?? "").ToUpperInvariant() switch
            {
                "CSV" => ReadCsv(stream),
                _ => ReadXlsx(stream)
            };
        }
    }

    private static ReportResult ReadXlsx(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed();
        if (used is null) return new ReportResult();

        var result = new ReportResult();
        var firstRow = used.FirstRow();
        foreach (var cell in firstRow.Cells())
            result.Columns.Add(cell.GetString());

        int taken = 0;
        foreach (var row in used.RowsUsed().Skip(1))
        {
            if (taken >= MaxRows) { result.Truncated = true; break; }
            var values = new object?[result.Columns.Count];
            for (int c = 0; c < result.Columns.Count; c++)
                values[c] = row.Cell(c + 1).GetString();
            result.Rows.Add(values);
            taken++;
        }
        return result;
    }

    private static ReportResult ReadCsv(Stream stream)
    {
        var result = new ReportResult();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? header = reader.ReadLine();
        if (header is null) return result;
        result.Columns.AddRange(SplitCsvLine(header));

        int taken = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (taken >= MaxRows) { result.Truncated = true; break; }
            var fields = SplitCsvLine(line);
            var values = new object?[result.Columns.Count];
            for (int c = 0; c < result.Columns.Count && c < fields.Count; c++)
                values[c] = fields[c];
            result.Rows.Add(values);
            taken++;
        }
        return result;
    }

    /// <summary>Minimal RFC4180-style CSV splitter (handles quoted fields with embedded commas/quotes).</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    // ── SFTP ─────────────────────────────────────────────────────────────────
    private static async Task<ReportResult> ReadSftpAsync(ReportDef def)
    {
        var src = def.EffectiveSource();
        if (string.IsNullOrWhiteSpace(src.SftpHost) || string.IsNullOrWhiteSpace(src.FilePath))
            return new ReportResult { Error = $"SFTP host and remote file path are required ({SiteLabel(def)})." };

        using var client = new SftpClient(src.SftpHost, src.SftpPort ?? 22, src.SftpUser ?? "", src.SftpPassword ?? "");
        await Task.Run(() => client.Connect());
        try
        {
            using var ms = new MemoryStream();
            await Task.Run(() => client.DownloadFile(src.FilePath, ms));
            ms.Position = 0;
            return ReadFile(ms, def.FileType);
        }
        finally
        {
            if (client.IsConnected) client.Disconnect();
        }
    }
}
