using System.IO.Compression;
using System.Net.Mail;
using ClosedXML.Excel;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using MyDiary.Web.Features.PositiveBranchList.Models.DTO;

namespace MyDiary.Web.Features.PositiveBranchList.Services;

/// <summary>
/// Positive Branch List (Assurance Corner) — Oracle-backed, self-contained (Flat Management
/// style: DTOs + service directly under Features/, no Core/Data layer). Aggregation itself runs
/// in the DB via the PBL_AGGREGATE stored procedure (db/positive_branch_list/004_pbl_aggregate_sp.sql);
/// this service drives the daily pipeline around it — data-availability gate, per-zone Excel/zip
/// report generation, SMTP send, and the status/mail-log bookkeeping that back the dashboard.
/// </summary>
public sealed class PositiveBranchListService : IPositiveBranchListService
{
    private readonly string _connString;
    private readonly IConfiguration _config;
    private readonly ILogger<PositiveBranchListService> _logger;

    public PositiveBranchListService(IConfiguration config, ILogger<PositiveBranchListService> logger)
    {
        _connString = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner in appsettings.json");
        _config = config;
        _logger = logger;
    }

    private OracleConnection Conn() => new(_connString);

    /// <summary>Last day of the PREVIOUS Indian financial year (Apr-Mar), per the explicit
    /// instruction — not a rolling date. For any date in Apr-2026..Mar-2027, this returns
    /// 31-Mar-2026 (today's "currently 31.03.2026").</summary>
    private static DateTime FyEndAnchor(DateTime asOf) =>
        asOf.Month >= 4 ? new DateTime(asOf.Year, 3, 31) : new DateTime(asOf.Year - 1, 3, 31);

    public async Task<PblJobStatus> GetTodayStatusAsync()
    {
        var today = DateTime.Today;
        await using var cn = Conn();
        await cn.OpenAsync();
        return await EnsureStatusRowAsync(cn, null, today);
    }

    private static async Task<PblJobStatus> EnsureStatusRowAsync(OracleConnection cn, OracleTransaction? tx, DateTime statusDate)
    {
        await using (var cmd = new OracleCommand(
            "SELECT STATUS_DATE,LATEST_DATA_DATE,DATA_AVAILABLE_FLAG,AGG_STATUS,MAIL_SENT_FLAG,MAIL_SENT_AT,RETRY_COUNT,LAST_CHECKED_AT,LAST_ERROR,TRIGGERED_BY " +
            "FROM RP_PBL_JOB_STATUS WHERE STATUS_DATE = :p_date", cn) { Transaction = tx })
        {
            cmd.Parameters.Add("p_date", statusDate);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new PblJobStatus
                {
                    StatusDate = r.GetDateTime(0),
                    LatestDataDate = r.IsDBNull(1) ? null : r.GetDateTime(1),
                    DataAvailable = r.GetString(2) == "Y",
                    AggStatus = r.GetString(3),
                    MailSent = r.GetString(4) == "Y",
                    MailSentAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
                    RetryCount = Convert.ToInt32(r.GetDecimal(6)),
                    LastCheckedAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
                    LastError = r.IsDBNull(8) ? null : r.GetString(8),
                    TriggeredBy = r.IsDBNull(9) ? null : r.GetString(9)
                };
            }
        }

        await using (var ins = new OracleCommand(
            "INSERT INTO RP_PBL_JOB_STATUS (ID, STATUS_DATE, CREATED_DATE) VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_date, SYSTIMESTAMP)", cn) { Transaction = tx })
        {
            ins.Parameters.Add("p_date", statusDate);
            await ins.ExecuteNonQueryAsync();
        }

        return new PblJobStatus { StatusDate = statusDate, AggStatus = "PENDING" };
    }

    private static async Task UpdateStatusAsync(OracleConnection cn, OracleTransaction? tx, DateTime statusDate,
        DateTime? latestDataDate, bool? dataAvailable, string? aggStatus, bool? mailSent,
        int? retryCount, string? lastError, string? triggeredBy)
    {
        await using var cmd = new OracleCommand(@"
            UPDATE RP_PBL_JOB_STATUS SET
                LATEST_DATA_DATE = NVL(:p_latest, LATEST_DATA_DATE),
                DATA_AVAILABLE_FLAG = NVL(:p_avail, DATA_AVAILABLE_FLAG),
                AGG_STATUS = NVL(:p_agg, AGG_STATUS),
                MAIL_SENT_FLAG = NVL(:p_sent, MAIL_SENT_FLAG),
                MAIL_SENT_AT = CASE WHEN :p_sent = 'Y' THEN SYSTIMESTAMP ELSE MAIL_SENT_AT END,
                RETRY_COUNT = NVL(:p_retry, RETRY_COUNT),
                LAST_CHECKED_AT = SYSTIMESTAMP,
                LAST_ERROR = :p_err,
                TRIGGERED_BY = NVL(:p_by, TRIGGERED_BY),
                UPDATED_DATE = SYSTIMESTAMP
            WHERE STATUS_DATE = :p_date", cn) { Transaction = tx, BindByName = true };

        cmd.Parameters.Add("p_latest", (object?)latestDataDate ?? DBNull.Value);
        cmd.Parameters.Add("p_avail", (object?)(dataAvailable.HasValue ? (dataAvailable.Value ? "Y" : "N") : null) ?? DBNull.Value);
        cmd.Parameters.Add("p_agg", (object?)aggStatus ?? DBNull.Value);
        cmd.Parameters.Add("p_sent", (object?)(mailSent.HasValue ? (mailSent.Value ? "Y" : "N") : null) ?? DBNull.Value);
        cmd.Parameters.Add("p_retry", (object?)retryCount ?? DBNull.Value);
        cmd.Parameters.Add("p_err", (object?)lastError ?? DBNull.Value);
        cmd.Parameters.Add("p_by", (object?)triggeredBy ?? DBNull.Value);
        cmd.Parameters.Add("p_date", statusDate);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<PblMailLogRow>> GetMailLogAsync(int days = 30)
    {
        var result = new List<PblMailLogRow>();
        await using var cn = Conn();
        await using var cmd = new OracleCommand(@"
            SELECT ID, RUN_DATE, ZONE_NAME, RECIPIENT_EMAIL, SUBJECT, BODY, ATTACHMENT_FILE_NAME,
                   SEND_STATUS, SENT_AT, ERROR_MESSAGE, TRIGGERED_BY
            FROM RP_PBL_MAIL_LOG
            WHERE CREATED_DATE >= SYSTIMESTAMP - :p_days
            ORDER BY CREATED_DATE DESC", cn);
        cmd.Parameters.Add("p_days", days);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new PblMailLogRow
            {
                Id = r.GetInt64(0),
                RunDate = r.GetDateTime(1),
                ZoneName = r.GetString(2),
                RecipientEmail = r.GetString(3),
                Subject = r.GetString(4),
                Body = r.IsDBNull(5) ? null : r.GetOracleClob(5).Value,
                AttachmentFileName = r.IsDBNull(6) ? null : r.GetString(6),
                SendStatus = r.GetString(7),
                SentAt = r.IsDBNull(8) ? null : r.GetDateTime(8),
                ErrorMessage = r.IsDBNull(9) ? null : r.GetString(9),
                TriggeredBy = r.GetString(10)
            });
        }
        return result;
    }

    public async Task<(string FileName, byte[] Content)?> GetAttachmentAsync(long mailLogId)
    {
        await using var cn = Conn();
        await using var cmd = new OracleCommand(
            "SELECT ATTACHMENT_FILE_NAME, ATTACHMENT_CONTENT FROM RP_PBL_MAIL_LOG WHERE ID = :p_id", cn);
        cmd.Parameters.Add("p_id", mailLogId);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync() || r.IsDBNull(0) || r.IsDBNull(1)) return null;

        var fileName = r.GetString(0);
        var blob = r.GetOracleBlob(1);
        return (fileName, blob.Value);
    }

    public async Task<PblRunResult> RunAggregationAndSendAsync(string triggeredBy, bool force)
    {
        var today = DateTime.Today;
        var currentDataDate = today.AddDays(-1);
        var anchorDate = FyEndAnchor(today);

        var result = new PblRunResult { CurrentDataDate = currentDataDate, AnchorDate = anchorDate };

        await using var cn = Conn();
        await cn.OpenAsync();

        var status = await EnsureStatusRowAsync(cn, null, today);
        if (status.MailSent && !force)
        {
            result.DataAvailable = true;
            result.MailSent = true;
            result.Message = "Already sent today — skipping (automated run sends at most once per day).";
            return result;
        }

        int dataCount;
        await using (var chk = new OracleCommand(
            "SELECT COUNT(*) FROM RP_PBL_BRANCH_METRICS_DAILY WHERE BUSINESS_DATE = :p_date", cn))
        {
            chk.Parameters.Add("p_date", currentDataDate);
            dataCount = Convert.ToInt32(await chk.ExecuteScalarAsync());
        }

        if (dataCount == 0)
        {
            await UpdateStatusAsync(cn, null, today, null, dataAvailable: false, aggStatus: null, mailSent: null,
                retryCount: status.RetryCount + 1, lastError: $"No data yet for {currentDataDate:dd-MMM-yyyy}.", triggeredBy: triggeredBy);
            result.DataAvailable = false;
            result.Message = $"No data available yet for {currentDataDate:dd-MMM-yyyy} (T-1). Will retry.";
            return result;
        }

        await UpdateStatusAsync(cn, null, today, latestDataDate: currentDataDate, dataAvailable: true,
            aggStatus: "RUNNING", mailSent: null, retryCount: null, lastError: null, triggeredBy: triggeredBy);
        result.DataAvailable = true;

        try
        {
            await using (var sp = new OracleCommand("PBL_AGGREGATE", cn) { CommandType = System.Data.CommandType.StoredProcedure })
            {
                sp.Parameters.Add("p_run_date", OracleDbType.Date).Value = currentDataDate;
                sp.Parameters.Add("p_anchor_date", OracleDbType.Date).Value = anchorDate;
                await sp.ExecuteNonQueryAsync();
            }

            var zones = await GetActiveZonesAsync(cn);
            var paramNames = await GetParamNamesAsync(cn);

            foreach (var (zoneName, zoneEmail) in zones)
            {
                result.ZonesProcessed++;
                try
                {
                    var ok = await BuildAndSendZoneReportAsync(cn, zoneName, zoneEmail, currentDataDate, anchorDate, paramNames, triggeredBy);
                    if (ok) result.ZonesSucceeded++; else result.ZonesFailed++;
                }
                catch (Exception ex)
                {
                    result.ZonesFailed++;
                    _logger.LogError(ex, "Positive Branch List: zone {Zone} failed", zoneName);
                }
            }

            await UpdateStatusAsync(cn, null, today, latestDataDate: null, dataAvailable: null,
                aggStatus: "DONE", mailSent: true, retryCount: null, lastError: null, triggeredBy: triggeredBy);

            result.MailSent = true;
            result.Message = $"Aggregated and mailed {result.ZonesSucceeded}/{result.ZonesProcessed} zone(s) for {currentDataDate:dd-MMM-yyyy} over {anchorDate:dd-MMM-yyyy}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Positive Branch List: aggregation run failed");
            await UpdateStatusAsync(cn, null, today, latestDataDate: null, dataAvailable: null,
                aggStatus: "FAILED", mailSent: null, retryCount: null, lastError: ex.Message, triggeredBy: triggeredBy);
            result.Message = $"Aggregation failed: {ex.Message}";
        }

        return result;
    }

    private static async Task<List<(string Zone, string Email)>> GetActiveZonesAsync(OracleConnection cn)
    {
        var list = new List<(string, string)>();
        await using var cmd = new OracleCommand(
            "SELECT ZONE_NAME, ZONE_EMAIL FROM RP_PBL_ZONE_MASTER WHERE IS_ACTIVE = 1 ORDER BY ZONE_NAME", cn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetString(0), r.GetString(1)));
        return list;
    }

    private static async Task<Dictionary<string, string>> GetParamNamesAsync(OracleConnection cn)
    {
        var map = new Dictionary<string, string>();
        await using var cmd = new OracleCommand(
            "SELECT PARAM_CODE, PARAM_NAME FROM RP_PBL_PARAM_MASTER ORDER BY DISPLAY_ORDER", cn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            map[r.GetString(0)] = r.GetString(1);
        return map;
    }

    private sealed class AggRow
    {
        public string? RoName;
        public string ParamCode = "";
        public int Positive, Negative, B1, B2, B3, B4, B5, B6, Total;
    }

    private sealed record BranchRow(string SolId, string Ro, string ParamCode, decimal VariationRupees, string Range);

    /// <summary>
    /// Bucket boundaries and exact wording match the reference workbook's table1.xml formula
    /// verbatim (including its "Between ₹50L & ₹ 1Cr" — no space after ₹ — which we preserve
    /// rather than "fix", since the goal is an identical report). Cutoffs are in Rupees
    /// (999999 ≈ 10L, 2499999 ≈ 25L, 4999999 ≈ 50L, 9999999 ≈ 1Cr, 49999999 ≈ 5Cr).
    /// </summary>
    private static string RangeLabel(decimal variationRupees) => variationRupees switch
    {
        >= 0 => "Positive",
        var v when Math.Abs(v) < 999999 => "Less than ₹ 10 Lakhs",
        var v when Math.Abs(v) < 2499999 => "Between ₹ 10L & ₹ 25L",
        var v when Math.Abs(v) < 4999999 => "Between ₹ 25L & ₹ 50L",
        var v when Math.Abs(v) < 9999999 => "Between ₹50L & ₹ 1Cr",
        var v when Math.Abs(v) < 49999999 => "Between ₹ 1Cr & ₹ 5Cr",
        _ => "More than ₹ 5 Crores"
    };

    /// <summary>Branch × parameter rows for the Base sheet — the raw material the slicers filter
    /// and the Summary sheet's COUNTIFS read. PARAM_VALUE is stored in Crores; the reference
    /// workbook's Variation column is in plain Rupees (its formula does "*10000000"), so we scale
    /// the same way here.</summary>
    private static async Task<List<BranchRow>> GetBranchRowsAsync(OracleConnection cn, string zoneName, DateTime currentDataDate, DateTime anchorDate)
    {
        var result = new List<BranchRow>();
        await using var cmd = new OracleCommand(@"
            SELECT cur.SOL_ID, cur.RO_NAME, cur.PARAM_CODE, cur.PARAM_VALUE, anc.PARAM_VALUE
            FROM RP_PBL_BRANCH_METRICS_DAILY cur
            JOIN RP_PBL_BRANCH_METRICS_DAILY anc
              ON anc.SOL_ID = cur.SOL_ID AND anc.PARAM_CODE = cur.PARAM_CODE AND anc.BUSINESS_DATE = :p_anchor
            WHERE cur.ZONE_NAME = :p_zone AND cur.BUSINESS_DATE = :p_current
            ORDER BY cur.PARAM_CODE, cur.RO_NAME, cur.SOL_ID", cn);
        cmd.Parameters.Add("p_anchor", anchorDate);
        cmd.Parameters.Add("p_zone", zoneName);
        cmd.Parameters.Add("p_current", currentDataDate);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var variationRupees = (r.GetDecimal(3) - r.GetDecimal(4)) * 10_000_000m;
            result.Add(new BranchRow(
                SolId: r.GetString(0), Ro: r.GetString(1), ParamCode: r.GetString(2),
                VariationRupees: variationRupees, Range: RangeLabel(variationRupees)));
        }
        return result;
    }

    private async Task<bool> BuildAndSendZoneReportAsync(
        OracleConnection cn, string zoneName, string zoneEmail,
        DateTime currentDataDate, DateTime anchorDate, Dictionary<string, string> paramNames, string triggeredBy)
    {
        var rows = new List<AggRow>();
        await using (var cmd = new OracleCommand(@"
            SELECT RO_NAME, PARAM_CODE, POSITIVE_COUNT, NEGATIVE_COUNT,
                   NEG_LT_10L, NEG_10_25L, NEG_25_50L, NEG_50L_1CR, NEG_1CR_5CR, NEG_GT_5CR, TOTAL_BRANCHES
            FROM RP_PBL_AGG_RESULT WHERE RUN_DATE = :p_date AND ZONE_NAME = :p_zone", cn))
        {
            cmd.Parameters.Add("p_date", currentDataDate);
            cmd.Parameters.Add("p_zone", zoneName);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                rows.Add(new AggRow
                {
                    RoName = r.IsDBNull(0) ? null : r.GetString(0),
                    ParamCode = r.GetString(1),
                    Positive = Convert.ToInt32(r.GetDecimal(2)),
                    Negative = Convert.ToInt32(r.GetDecimal(3)),
                    B1 = Convert.ToInt32(r.GetDecimal(4)), B2 = Convert.ToInt32(r.GetDecimal(5)),
                    B3 = Convert.ToInt32(r.GetDecimal(6)), B4 = Convert.ToInt32(r.GetDecimal(7)),
                    B5 = Convert.ToInt32(r.GetDecimal(8)), B6 = Convert.ToInt32(r.GetDecimal(9)),
                    Total = Convert.ToInt32(r.GetDecimal(10))
                });
            }
        }

        // "Previous" positive count for the trend column — the zone-total row from the most recent
        // earlier run date that this zone was actually mailed for (not just any earlier business
        // date), so the report reads "last time we told you vs. this time".
        var previousPositive = new Dictionary<string, int>();
        await using (var cmd = new OracleCommand(@"
            SELECT a.PARAM_CODE, a.POSITIVE_COUNT FROM RP_PBL_AGG_RESULT a
            WHERE a.ZONE_NAME = :p_zone AND a.RO_NAME IS NULL AND a.RUN_DATE = (
                SELECT MAX(m.RUN_DATE) FROM RP_PBL_MAIL_LOG m
                WHERE m.ZONE_NAME = :p_zone2 AND m.SEND_STATUS = 'SENT' AND m.RUN_DATE < :p_date)", cn))
        {
            cmd.Parameters.Add("p_zone", zoneName);
            cmd.Parameters.Add("p_zone2", zoneName);
            cmd.Parameters.Add("p_date", currentDataDate);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                previousPositive[r.GetString(0)] = Convert.ToInt32(r.GetDecimal(1));
        }

        var branchRows = await GetBranchRowsAsync(cn, zoneName, currentDataDate, anchorDate);
        var roNames = rows.Where(r => r.RoName is not null).Select(r => r.RoName!).Distinct().OrderBy(x => x).ToList();
        var excelBytes = BuildWorkbook(zoneName, currentDataDate, anchorDate, branchRows, roNames, previousPositive, paramNames);
        var zipBytes = ZipSingleFile($"PositiveBranchList_{Sanitize(zoneName)}_{currentDataDate:ddMMyy}.xlsx", excelBytes);
        var zipFileName = $"PositiveBranchList_{Sanitize(zoneName)}_{currentDataDate:ddMMyy}.zip";

        var zoneTotal = rows.Where(r => r.RoName is null).ToList();
        var subject = $"Positive Branch List — {zoneName} — as on {currentDataDate:dd.MM.yyyy} (over {anchorDate:dd.MM.yyyy})";
        var body = BuildBodyHtml(zoneName, currentDataDate, anchorDate, zoneTotal, paramNames);

        string sendStatus;
        string? errorMessage = null;
        try
        {
            await SendMailAsync(zoneEmail, subject, body, zipFileName, zipBytes);
            sendStatus = "SENT";
        }
        catch (Exception ex)
        {
            sendStatus = "FAILED";
            errorMessage = ex.Message;
            _logger.LogWarning(ex, "Positive Branch List: send failed for zone {Zone}", zoneName);
        }

        await using (var log = new OracleCommand(@"
            INSERT INTO RP_PBL_MAIL_LOG (
                ID, RUN_DATE, ZONE_NAME, RECIPIENT_EMAIL, SUBJECT, BODY,
                ATTACHMENT_FILE_NAME, ATTACHMENT_CONTENT, SEND_STATUS, SENT_AT, ERROR_MESSAGE, TRIGGERED_BY)
            VALUES (
                RP_SEQ_GLOBAL.NEXTVAL, :p_run, :p_zone, :p_email, :p_subj, :p_body,
                :p_fname, :p_content, :p_status, CASE WHEN :p_status2 = 'SENT' THEN SYSTIMESTAMP ELSE NULL END, :p_err, :p_by)", cn))
        {
            log.Parameters.Add("p_run", currentDataDate);
            log.Parameters.Add("p_zone", zoneName);
            log.Parameters.Add("p_email", zoneEmail);
            log.Parameters.Add("p_subj", subject);
            log.Parameters.Add(new OracleParameter("p_body", OracleDbType.Clob) { Value = body });
            log.Parameters.Add("p_fname", zipFileName);
            var contentParam = new OracleParameter("p_content", OracleDbType.Blob) { Value = zipBytes };
            log.Parameters.Add(contentParam);
            log.Parameters.Add("p_status", sendStatus);
            log.Parameters.Add("p_status2", sendStatus);
            log.Parameters.Add("p_err", (object?)errorMessage ?? DBNull.Value);
            log.Parameters.Add("p_by", triggeredBy);
            await log.ExecuteNonQueryAsync();
        }

        return sendStatus == "SENT";
    }

    private static string Sanitize(string s) => string.Join("_", s.Split(Path.GetInvalidFileNameChars().Append(' ').ToArray()));

    // Base-sheet row layout — a spacer band above the table for the title + the RO/Parameters/Range
    // slicers + the purple instruction box (mirrors the reference workbook's rows 1-16), then the
    // real header/data. SlicerInjector's drawing anchors (rows 2-11, 0-indexed) sit inside this band.
    private const int BaseTitleRow = 1;
    private const int BaseHeaderRow = 14;
    private const int BaseDataStartRow = 15;

    private static readonly XLColor NavyTitle = XLColor.FromHtml("#FF002060");
    private static readonly XLColor HeaderBlue = XLColor.FromHtml("#FF0070C0");
    private static readonly XLColor CyanTableHeader = XLColor.FromHtml("#FF00FFFF");

    /// <summary>
    /// Builds the 2-sheet (Summary + Base) workbook matching the reference "Visakhapatnam.xlsx"
    /// styling and structure, then hands it to <see cref="SlicerInjector"/> to add the real
    /// RO/Parameters/Range table slicers ClosedXML itself can't write. Base holds one literal row
    /// per (branch, parameter) — no external-workbook links, so none of the reference file's
    /// broken "[...]#REF!" cells (visible in the source screenshot) can occur here. Summary's
    /// current-run figures are live COUNTIFS formulas against Base's own table, driven by a
    /// Parameter dropdown exactly like the reference file (only the "previous run" column, which
    /// depends on this app's own send history rather than anything in the file, is a literal).
    /// </summary>
    private static byte[] BuildWorkbook(string zoneName, DateTime currentDataDate, DateTime anchorDate,
        List<BranchRow> branchRows, List<string> roNames, Dictionary<string, int> previousPositive, Dictionary<string, string> paramNames)
    {
        using var workbook = new XLWorkbook();

        var wsSummary = workbook.Worksheets.Add("Summary");
        var wsBase = workbook.Worksheets.Add("Base");

        // ── Base sheet ────────────────────────────────────────────────────────────────────
        wsBase.Cell(BaseTitleRow, 1).Value = $"Position of Branches as on {currentDataDate:dd.MM.yy} over {anchorDate:MMM-yy}";
        wsBase.Range(BaseTitleRow, 1, BaseTitleRow, 7).Merge().Style
            .Font.SetBold().Font.SetFontColor(XLColor.White).Font.SetFontSize(13)
            .Fill.SetBackgroundColor(NavyTitle);

        string[] headers = { "SOLID", "RO", "ZO", "SOL Name", "Parameters", "Variation over Mar-26", "Range" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = wsBase.Cell(BaseHeaderRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(CyanTableHeader);
        }

        var row = BaseDataStartRow;
        foreach (var br in branchRows)
        {
            wsBase.Cell(row, 1).Value = br.SolId;
            wsBase.Cell(row, 2).Value = br.Ro;
            wsBase.Cell(row, 3).Value = zoneName;
            wsBase.Cell(row, 4).Value = br.SolId; // No branch display name in the feed yet — SOLID stands in.
            wsBase.Cell(row, 5).Value = paramNames.TryGetValue(br.ParamCode, out var pn) ? pn : br.ParamCode;
            wsBase.Cell(row, 6).Value = (double)br.VariationRupees;
            wsBase.Cell(row, 7).Value = br.Range;
            row++;
        }

        var lastDataRow = Math.Max(row - 1, BaseDataStartRow);
        var tableRange = wsBase.Range(BaseHeaderRow, 1, lastDataRow, 7);
        var table = tableRange.CreateTable("BaseTbl");
        table.Theme = XLTableTheme.None;
        wsBase.Columns().AdjustToContents();

        // ── Summary sheet ─────────────────────────────────────────────────────────────────
        wsSummary.Cell(1, 2).Value = $"Positive Branch List — {zoneName}";
        wsSummary.Range(1, 2, 1, 11).Merge().Style
            .Font.SetBold().Font.SetFontColor(XLColor.White).Font.SetFontSize(14)
            .Fill.SetBackgroundColor(NavyTitle);

        var paramList = paramNames.Values.ToList();
        var selectorCell = wsSummary.Cell(3, 3);
        selectorCell.Value = paramList.FirstOrDefault() ?? "";
        // ClosedXML writes an inline list's formula1 verbatim — for a literal (non-range) list it
        // must be a quoted string ("A,B,C") or Excel's schema validator strips the whole
        // data-validation on open ("Removed Feature: Data validation").
        selectorCell.SetDataValidation().List("\"" + string.Join(",", paramList) + "\"");
        selectorCell.Style.Fill.SetBackgroundColor(XLColor.LightYellow).Font.SetBold();
        wsSummary.Cell(3, 2).Value = "Select Parameter:";
        wsSummary.Cell(3, 2).Style.Font.SetBold();

        wsSummary.Cell(5, 2).FormulaA1 =
            $"=\"Position of Branches as on {currentDataDate:dd.MM.yy} over {anchorDate:MMM-yy} - \"&C3";
        wsSummary.Range(5, 2, 5, 11).Merge().Style.Font.SetBold().Font.SetFontSize(12);

        wsSummary.Range(7, 3, 7, 4).Merge().Value = "Positive";
        wsSummary.Range(7, 5, 7, 10).Merge().Value = "Negative by amount";
        wsSummary.Range(7, 2, 8, 2).Merge().Value = "Zone / Region";
        wsSummary.Range(7, 11, 8, 11).Merge().Value = "Total Branches";
        wsSummary.Range(7, 2, 7, 11).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(HeaderBlue);

        string[] bucketLabels =
        {
            "Previous Send", "Current", "Less than ₹ 10 Lakhs", "Between ₹ 10L & ₹ 25L",
            "Between ₹ 25L & ₹ 50L", "Between ₹50L & ₹ 1Cr", "Between ₹ 1Cr & ₹ 5Cr", "More than ₹ 5 Crores"
        };
        for (int c = 0; c < bucketLabels.Length; c++)
            wsSummary.Cell(8, 3 + c).Value = bucketLabels[c];
        wsSummary.Range(8, 3, 8, 10).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

        // Hidden helper columns (N/O) — parameter name -> this zone's previous-send positive count.
        // The "Previous Send" figure can't be a COUNTIFS against Base (that history isn't in this
        // file, it's this app's own send log), so it's an INDEX/MATCH against this lookup instead
        // of a plain literal — that keeps it reacting to the Parameter dropdown like every other
        // figure on the sheet, at the cost of only being available at zone level (no per-RO history
        // is retained, so region rows show "—").
        var helperRow = 1;
        foreach (var (code, name) in paramNames)
        {
            wsSummary.Cell(helperRow, 14).Value = name;
            if (previousPositive.TryGetValue(code, out var prev))
                wsSummary.Cell(helperRow, 15).Value = prev;
            else
                wsSummary.Cell(helperRow, 15).Value = "";
            helperRow++;
        }
        var helperLastRow = Math.Max(helperRow - 1, 1);
        wsSummary.Column(14).Hide();
        wsSummary.Column(15).Hide();

        int r0 = 9;
        wsSummary.Cell(r0, 2).Value = zoneName;
        WriteSummaryFormulaRow(wsSummary, r0, "BaseTbl[ZO]",
            $"=IFERROR(INDEX($O$1:$O${helperLastRow},MATCH($C$3,$N$1:$N${helperLastRow},0)),\"—\")");

        var ri = r0 + 1;
        foreach (var ro in roNames)
        {
            wsSummary.Cell(ri, 2).Value = ro;
            WriteSummaryFormulaRow(wsSummary, ri, "BaseTbl[RO]", null);
            ri++;
        }

        wsSummary.Columns(1, 13).AdjustToContents();
        wsSummary.SheetView.Freeze(8, 2);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var rawBytes = stream.ToArray();

        return SlicerInjector.AddBaseSheetSlicers(rawBytes, "Base", "BaseTbl");
    }

    /// <summary>Writes one Summary row's live COUNTIFS formulas (current-positive, 6 negative
    /// buckets, total) filtered by the given Base column ("BaseTbl[ZO]" for the zone-total row,
    /// "BaseTbl[RO]" for a region row) equal to <paramref name="filterValue"/>, plus a literal
    /// "previous send" figure where one is available (zone-total row only — no per-RO history is
    /// kept).</summary>
    private static void WriteSummaryFormulaRow(IXLWorksheet ws, int r, string filterColumn, string? previousCellFormula)
    {
        var cellRef = $"$B{r}";
        if (previousCellFormula is not null)
            ws.Cell(r, 3).FormulaA1 = previousCellFormula;
        else
            ws.Cell(r, 3).Value = "—";

        ws.Cell(r, 4).FormulaA1 = $"=COUNTIFS({filterColumn},{cellRef},BaseTbl[Parameters],$C$3,BaseTbl[Range],\"Positive\")";
        for (int b = 0; b < 6; b++)
        {
            var col = 5 + b;
            var headerRef = $"{GetColumnLetter(col)}$8";
            ws.Cell(r, col).FormulaA1 = $"=COUNTIFS({filterColumn},{cellRef},BaseTbl[Parameters],$C$3,BaseTbl[Range],{headerRef})";
        }
        ws.Cell(r, 11).FormulaA1 = $"=SUM(D{r}:J{r})";
    }

    private static string GetColumnLetter(int col)
    {
        string s = "";
        while (col > 0)
        {
            var rem = (col - 1) % 26;
            s = (char)('A' + rem) + s;
            col = (col - 1) / 26;
        }
        return s;
    }

    private static byte[] ZipSingleFile(string entryName, byte[] content)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(content, 0, content.Length);
        }
        return ms.ToArray();
    }

    private static string BuildBodyHtml(string zoneName, DateTime currentDataDate, DateTime anchorDate,
        List<AggRow> zoneTotalRows, Dictionary<string, string> paramNames)
    {
        var rowsHtml = string.Join("", paramNames.Select(kv =>
        {
            var ar = zoneTotalRows.FirstOrDefault(r => r.ParamCode == kv.Key);
            return $"<tr><td>{kv.Value}</td><td style='text-align:center'>{ar?.Positive ?? 0}</td>" +
                   $"<td style='text-align:center'>{ar?.Negative ?? 0}</td><td style='text-align:center'>{ar?.Total ?? 0}</td></tr>";
        }));

        return $@"
            <p>Dear Team,</p>
            <p>Please find attached the <b>Positive Branch List</b> for <b>{zoneName}</b> — position as on
            <b>{currentDataDate:dd-MMM-yyyy}</b> over <b>{anchorDate:dd-MMM-yyyy}</b> (previous financial year end).</p>
            <table cellpadding='6' style='border-collapse:collapse;border:1px solid #ccc'>
                <tr style='background:#f0f0f0;font-weight:bold'><td>Parameter</td><td>Positive Branches</td><td>Negative Branches</td><td>Total Branches</td></tr>
                {rowsHtml}
            </table>
            <p style='color:#777;font-size:12px'>The attached ZIP contains the detailed region-wise workbook
            (one sheet per parameter, negative-amount bucketed). This is an automated communication from
            My Diary — Positive Branch List.</p>";
    }

    private async Task SendMailAsync(string toEmail, string subject, string htmlBody, string attachmentFileName, byte[] attachmentBytes)
    {
        var host = _config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("SMTP Host is not configured (Smtp:Host).");

        using var message = new MailMessage
        {
            From = new MailAddress(_config["Smtp:FromAddress"] ?? "no-reply@dev.local", _config["Smtp:FromName"] ?? "My Diary"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var attachmentStream = new MemoryStream(attachmentBytes);
        message.Attachments.Add(new Attachment(attachmentStream, attachmentFileName, "application/zip"));

        using var smtp = new SmtpClient(host, int.TryParse(_config["Smtp:Port"], out var port) ? port : 25)
        {
            EnableSsl = bool.TryParse(_config["Smtp:UseStartTls"], out var tls) && tls
        };
        await smtp.SendMailAsync(message);
    }
}
