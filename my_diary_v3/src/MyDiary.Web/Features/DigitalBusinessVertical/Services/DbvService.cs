using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Features.DigitalBusinessVertical.Jobs;
using MyDiary.Web.Features.DigitalBusinessVertical.Models;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.DigitalBusinessVertical.Services;

/// <summary>Digital Business Vertical - Campaigns + Important Correspondence. Self-contained raw Oracle ADO (same style as
/// StaffVendorConnectService / SubsidiaryJvService). Ported from the V3 DbvRepo and extended per the DBV BRD (UBI_BRD_V1.0).</summary>
public sealed class DbvService : IDbvService
{
    private readonly string _cs;
    private readonly IAuthorizationService _authz;
    private readonly IWebHostEnvironment _env;
    private readonly IStaticAssetPathResolver _assets;
    private readonly ILogger<DbvService> _log;
    private const long MaxFileBytes = 10 * 1024 * 1024;

    public DbvService(IConfiguration config, IAuthorizationService authz, IWebHostEnvironment env, IStaticAssetPathResolver assets, ILogger<DbvService> log)
    {
        _cs = config.GetConnectionString("RP_Owner") ?? throw new InvalidOperationException("Missing ConnectionStrings:MyDiaryDBConnection");
        _authz = authz; _env = env; _assets = assets; _log = log;
    }

    private OracleConnection Conn() => new(_cs);
    private static object V(object? o) => o ?? DBNull.Value;
    private static string? S(OracleDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static string? Clob(OracleDataReader r, int i) => r.IsDBNull(i) ? null : r.GetOracleClob(i).Value;

    // ══ Access ══
    public async Task<bool> IsAdminAsync(ClaimsPrincipal user)
    {
       // if ((await _authz.AuthorizeAsync(user, "AppAdmin")).Succeeded) return true;
        var emp = user.FindFirst("emplid")?.Value;
        if (string.IsNullOrWhiteSpace(emp)) return false;
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT COUNT(*) FROM RP_DBV_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_e) AND IS_ACTIVE=1", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", emp);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<List<(string EmpCode, string AddedBy, DateTime AddedAt)>> ListAdminsAsync()
    {
        var l = new List<(string, string, DateTime)>();
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT EMP_CODE, NVL(ADDED_BY,''), ADDED_AT FROM RP_DBV_ADMIN WHERE IS_ACTIVE=1 ORDER BY EMP_CODE", cn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) l.Add((r.GetString(0), r.GetString(1), r.GetDateTime(2)));
        return l;
    }

    public async Task AddAdminAsync(string empCode, string actor)
    {
        empCode = (empCode ?? "").Trim(); if (empCode.Length == 0) return;
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_DBV_ADMIN t USING (SELECT :p_e AS E FROM DUAL) s ON (UPPER(t.EMP_CODE)=UPPER(s.E))
            WHEN MATCHED THEN UPDATE SET IS_ACTIVE=1, ADDED_BY=:p_by, ADDED_AT=SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (EMP_CODE, IS_ACTIVE, ADDED_BY) VALUES (s.E, 1, :p_by2)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_by2", actor);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveAdminAsync(string empCode)
    {
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("UPDATE RP_DBV_ADMIN SET IS_ACTIVE=0 WHERE UPPER(EMP_CODE)=UPPER(:p_e)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode);
        await cmd.ExecuteNonQueryAsync();
    }

    // ══ Campaigns: read ══
    private const string CampCols = "CAMPAIGN_ID, CAMPAIGN_NAME, PRODUCT_NAME, TARGET_SEGMENT, DESCRIPTION, START_DATE, END_DATE, IS_ONGOING, REMARKS, OUTCOME_SUMMARY, IS_ARCHIVED, CREATED_BY_EMP, CREATED_AT, UPDATED_BY_EMP, UPDATED_AT";

    private static DbvCampaign MapCampaign(OracleDataReader r) => new()
    {
        Id = Convert.ToInt64(r.GetDecimal(0)), CampaignName = r.GetString(1), ProductName = r.GetString(2), TargetSegment = r.GetString(3), Description = S(r, 4),
        StartDate = r.GetDateTime(5), EndDate = r.IsDBNull(6) ? null : r.GetDateTime(6), IsOngoing = Convert.ToInt32(r.GetDecimal(7)) == 1, Remarks = S(r, 8),
        OutcomeSummary = Clob(r, 9), IsArchived = Convert.ToInt32(r.GetDecimal(10)) == 1, CreatedByEmp = r.GetString(11), CreatedAt = r.GetDateTime(12),
        UpdatedByEmp = S(r, 13), UpdatedAt = r.IsDBNull(14) ? null : r.GetDateTime(14)
    };

    private static (string Where, List<(string, object?)> Ps) BuildWhere(DbvFilter f)
    {
        var w = new List<string>(); var ps = new List<(string, object?)>();
        if (f.ArchivedOnly) w.Add("c.IS_ARCHIVED = 1"); else if (!f.IncludeArchived) w.Add("c.IS_ARCHIVED = 0");
        if (!string.IsNullOrWhiteSpace(f.Product)) { w.Add("(UPPER(c.PRODUCT_NAME) LIKE '%' || UPPER(:f_prod) || '%' OR UPPER(c.CAMPAIGN_NAME) LIKE '%' || UPPER(:f_prod2) || '%')"); ps.Add(("f_prod", f.Product.Trim())); ps.Add(("f_prod2", f.Product.Trim())); }
        if (f.From.HasValue)
        {
            w.Add("TRUNC(c.START_DATE) >= :f_from");
            ps.Add(("f_from", f.From.Value.Date));
        }

        if (f.To.HasValue)
        {
            w.Add("TRUNC(c.START_DATE) <= :f_to");
            ps.Add(("f_to", f.To.Value.Date));
        }
        if (f.Status == "ONGOING") w.Add("(c.IS_ONGOING = 1 OR c.END_DATE >= TRUNC(SYSDATE))");
        else if (f.Status == "COMPLETED") w.Add("(c.IS_ONGOING = 0 AND c.END_DATE < TRUNC(SYSDATE))");
        var commConds = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Mode)) { commConds.Add("m.CAMPAIGN_MODE = :f_mode"); ps.Add(("f_mode", f.Mode)); }
        if (!string.IsNullOrWhiteSpace(f.FinancialYear) && int.TryParse(f.FinancialYear.Split('-')[0], out var fy))
        { commConds.Add("m.COMM_DATE BETWEEN :f_fy1 AND :f_fy2"); ps.Add(("f_fy1", new DateTime(fy, 4, 1))); ps.Add(("f_fy2", new DateTime(fy + 1, 3, 31))); }
        if (!string.IsNullOrWhiteSpace(f.Quarter))
        {
            var months = f.Quarter switch { "Q1" => "4,5,6", "Q2" => "7,8,9", "Q3" => "10,11,12", "Q4" => "1,2,3", _ => null };
            if (months != null) commConds.Add($"EXTRACT(MONTH FROM m.COMM_DATE) IN ({months})");
        }
        if (commConds.Count > 0) w.Add($"EXISTS (SELECT 1 FROM RP_DBV_CAMPAIGN_COMM m WHERE m.CAMPAIGN_ID = c.CAMPAIGN_ID AND {string.Join(" AND ", commConds)})");
        return (w.Count == 0 ? "1=1" : string.Join(" AND ", w), ps);
    }

    public async Task<List<DbvCampaign>> ListCampaignsAsync(DbvFilter filter)
    {
        var list = new List<DbvCampaign>();
        var (where, ps) = BuildWhere(filter);
        await using var cn = Conn(); await cn.OpenAsync();
        await using (var cmd = new OracleCommand($"SELECT {CampCols.Replace("CAMPAIGN_ID", "c.CAMPAIGN_ID")} FROM RP_DBV_CAMPAIGN c WHERE {where} ORDER BY c.START_DATE DESC, c.CAMPAIGN_ID DESC", cn) { BindByName = true })
        {
            foreach (var (n, v) in ps) cmd.Parameters.Add(n, v ?? DBNull.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(MapCampaign(r));
        }
        await LoadCommsAsync(cn, list);
        return list;
    }

    public async Task<DbvCampaign?> GetCampaignAsync(long id)
    {
        await using var cn = Conn(); await cn.OpenAsync();
        DbvCampaign? c = null;
        await using (var cmd = new OracleCommand($"SELECT {CampCols} FROM RP_DBV_CAMPAIGN WHERE CAMPAIGN_ID = :p_id", cn) { BindByName = true })
        {
            cmd.Parameters.Add("p_id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) c = MapCampaign(r);
        }
        if (c is not null) await LoadCommsAsync(cn, new List<DbvCampaign> { c });
        return c;
    }

    private static async Task LoadCommsAsync(OracleConnection cn, List<DbvCampaign> camps)
    {
        if (camps.Count == 0) return;
        var byId = camps.ToDictionary(c => c.Id);
        var ids = string.Join(",", byId.Keys);
        await using var cmd = new OracleCommand($@"SELECT COMM_ID, CAMPAIGN_ID, COMM_DATE, CAMPAIGN_MODE, TEMPLATE_NO, MESSAGE_CONTENT, CREATIVE_FILE_NAME, CREATIVE_FILE_PATH
            FROM RP_DBV_CAMPAIGN_COMM WHERE CAMPAIGN_ID IN ({ids}) ORDER BY COMM_DATE, DISPLAY_ORDER, COMM_ID", cn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            if (byId.TryGetValue(Convert.ToInt64(r.GetDecimal(1)), out var c))
                c.Comms.Add(new DbvComm { Id = Convert.ToInt64(r.GetDecimal(0)), Date = r.GetDateTime(2), Mode = r.GetString(3), TemplateNo = S(r, 4), Message = Clob(r, 5), CreativeFileName = S(r, 6), CreativeFilePath = S(r, 7) });
    }

    // ══ Campaigns: write ══
    public async Task<long> SaveCampaignAsync(DbvCampaign c, string actor, bool notify)
    {
        Validate(c);
        await using var cn = Conn(); await cn.OpenAsync();
        await using var tx = cn.BeginTransaction();
        bool isNew = c.Id == 0;
        try
        {
            async Task Exec(string sql, params (string n, object? v)[] ps)
            {
                await using var cmd = new OracleCommand(sql, cn) { BindByName = true, Transaction = tx };
                foreach (var (n, v) in ps) cmd.Parameters.Add(n, v ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            if (isNew)
            {
                await using var idCmd = new OracleCommand("SELECT RP_SEQ_GLOBAL.NEXTVAL FROM DUAL", cn) { Transaction = tx };
                c.Id = Convert.ToInt64(await idCmd.ExecuteScalarAsync());
                await Exec(@"INSERT INTO RP_DBV_CAMPAIGN (CAMPAIGN_ID, CAMPAIGN_NAME, PRODUCT_NAME, TARGET_SEGMENT, DESCRIPTION, START_DATE, END_DATE, IS_ONGOING, REMARKS, OUTCOME_SUMMARY, CREATED_BY_EMP)
                    VALUES (:p_id, :p_n, :p_p, :p_seg, :p_d, :p_s, :p_e, :p_o, :p_r, :p_out, :p_by)",
                    ("p_id", c.Id), ("p_n", c.CampaignName.Trim()), ("p_p", c.ProductName.Trim()), ("p_seg", c.TargetSegment), ("p_d", c.Description), ("p_s", c.StartDate.Date),
                    ("p_e", c.IsOngoing ? null : c.EndDate?.Date), ("p_o", c.IsOngoing ? 1 : 0), ("p_r", c.Remarks), ("p_out", c.OutcomeSummary), ("p_by", actor));
            }
            else
            {
                await Exec(@"UPDATE RP_DBV_CAMPAIGN SET CAMPAIGN_NAME=:p_n, PRODUCT_NAME=:p_p, TARGET_SEGMENT=:p_seg, DESCRIPTION=:p_d, START_DATE=:p_s, END_DATE=:p_e, IS_ONGOING=:p_o,
                    REMARKS=:p_r, OUTCOME_SUMMARY=:p_out, UPDATED_BY_EMP=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE CAMPAIGN_ID=:p_id",
                    ("p_n", c.CampaignName.Trim()), ("p_p", c.ProductName.Trim()), ("p_seg", c.TargetSegment), ("p_d", c.Description), ("p_s", c.StartDate.Date),
                    ("p_e", c.IsOngoing ? null : c.EndDate?.Date), ("p_o", c.IsOngoing ? 1 : 0), ("p_r", c.Remarks), ("p_out", c.OutcomeSummary), ("p_by", actor), ("p_id", c.Id));
                await Exec("DELETE FROM RP_DBV_CAMPAIGN_COMM WHERE CAMPAIGN_ID=:p_id", ("p_id", c.Id));
            }
            int order = 0;
            foreach (var m in c.Comms.OrderBy(x => x.Date))
            {
                order++;
                await Exec(@"INSERT INTO RP_DBV_CAMPAIGN_COMM (COMM_ID, CAMPAIGN_ID, COMM_DATE, CAMPAIGN_MODE, TEMPLATE_NO, MESSAGE_CONTENT, CREATIVE_FILE_NAME, CREATIVE_FILE_PATH, DISPLAY_ORDER)
                    VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_c, :p_dt, :p_m, :p_t, :p_msg, :p_fn, :p_fp, :p_o)",
                    ("p_c", c.Id), ("p_dt", m.Date.Date), ("p_m", m.Mode), ("p_t", m.TemplateNo), ("p_msg", m.Message), ("p_fn", m.CreativeFileName), ("p_fp", m.CreativeFilePath), ("p_o", order));
            }
            tx.Commit();
        }
        catch { tx.Rollback(); if (isNew) c.Id = 0; throw; }

        if (notify && isNew)
        {
            var when = c.IsOngoing ? $"ongoing from {c.StartDate:dd MMM yyyy}" : $"{c.StartDate:dd MMM yyyy} to {c.EndDate:dd MMM yyyy}";
            Push("New DBV campaign: " + c.CampaignName.Trim(), $"{c.ProductName.Trim()} - {when}. Tap to view the campaign details.", "dbv/campaigns", "Digital Business Vertical");
        }
        return c.Id;
    }

    private static void Validate(DbvCampaign c)
    {
        if (string.IsNullOrWhiteSpace(c.CampaignName)) throw new InvalidOperationException("Campaign name is required.");
        if (string.IsNullOrWhiteSpace(c.ProductName)) throw new InvalidOperationException("Product name is required.");
        if (!DbvCampaign.Segments.Contains(c.TargetSegment)) throw new InvalidOperationException("Invalid target segment.");
        if (!c.IsOngoing && c.EndDate is null) throw new InvalidOperationException("End date is required unless the campaign is marked Ongoing.");
        if (!c.IsOngoing && c.EndDate < c.StartDate) throw new InvalidOperationException("End date cannot be before the start date.");
        if (c.Comms.Any(m => !DbvLookups.Modes.Any(x => x.Code == m.Mode))) throw new InvalidOperationException("Every communication row needs a valid mode.");
    }

    public async Task SetCampaignArchivedAsync(long id, bool archived, string actor)
    {
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("UPDATE RP_DBV_CAMPAIGN SET IS_ARCHIVED=:p_a, UPDATED_BY_EMP=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE CAMPAIGN_ID=:p_id", cn) { BindByName = true };
        cmd.Parameters.Add("p_a", archived ? 1 : 0); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteCampaignAsync(long id)
    {
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("DELETE FROM RP_DBV_CAMPAIGN WHERE CAMPAIGN_ID=:p_id", cn) { BindByName = true };   // comm rows cascade
        cmd.Parameters.Add("p_id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ══ Correspondence ══
    public async Task<List<DbvCorrespondence>> ListCorrespondenceAsync(bool includeArchived)
    {
        var l = new List<DbvCorrespondence>();
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand($@"SELECT ID, TITLE, CORR_DATE, DESCRIPTION, ATTACHMENT_FILE_NAME, ATTACHMENT_FILE_PATH, IS_ARCHIVED, CREATED_BY_EMP, CREATED_AT, UPDATED_BY_EMP, UPDATED_AT
            FROM RP_DBV_CORRESPONDENCE {(includeArchived ? "" : "WHERE IS_ARCHIVED = 0")} ORDER BY CORR_DATE DESC, ID DESC", cn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            l.Add(new DbvCorrespondence { Id = Convert.ToInt64(r.GetDecimal(0)), Title = r.GetString(1), CorrDate = r.GetDateTime(2), Description = S(r, 3), AttachmentFileName = S(r, 4), AttachmentFilePath = S(r, 5),
                IsArchived = Convert.ToInt32(r.GetDecimal(6)) == 1, CreatedByEmp = r.GetString(7), CreatedAt = r.GetDateTime(8), UpdatedByEmp = S(r, 9), UpdatedAt = r.IsDBNull(10) ? null : r.GetDateTime(10) });
        return l;
    }

    public async Task<long> SaveCorrespondenceAsync(DbvCorrespondence c, string actor, bool notify)
    {
        if (string.IsNullOrWhiteSpace(c.Title)) throw new InvalidOperationException("Title is required.");
        bool isNew = c.Id == 0;
        await using var cn = Conn(); await cn.OpenAsync();
        if (isNew)
        {
            await using var idCmd = new OracleCommand("SELECT RP_SEQ_GLOBAL.NEXTVAL FROM DUAL", cn);
            c.Id = Convert.ToInt64(await idCmd.ExecuteScalarAsync());
            await using var cmd = new OracleCommand(@"INSERT INTO RP_DBV_CORRESPONDENCE (ID, TITLE, CORR_DATE, DESCRIPTION, ATTACHMENT_FILE_NAME, ATTACHMENT_FILE_PATH, CREATED_BY_EMP)
                VALUES (:p_id, :p_t, :p_d, :p_desc, :p_fn, :p_fp, :p_by)", cn) { BindByName = true };
            cmd.Parameters.Add("p_id", c.Id); cmd.Parameters.Add("p_t", c.Title.Trim()); cmd.Parameters.Add("p_d", c.CorrDate.Date); cmd.Parameters.Add("p_desc", V(c.Description));
            cmd.Parameters.Add("p_fn", V(c.AttachmentFileName)); cmd.Parameters.Add("p_fp", V(c.AttachmentFilePath)); cmd.Parameters.Add("p_by", actor);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = new OracleCommand(@"UPDATE RP_DBV_CORRESPONDENCE SET TITLE=:p_t, CORR_DATE=:p_d, DESCRIPTION=:p_desc, ATTACHMENT_FILE_NAME=:p_fn, ATTACHMENT_FILE_PATH=:p_fp,
                UPDATED_BY_EMP=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id", cn) { BindByName = true };
            cmd.Parameters.Add("p_t", c.Title.Trim()); cmd.Parameters.Add("p_d", c.CorrDate.Date); cmd.Parameters.Add("p_desc", V(c.Description));
            cmd.Parameters.Add("p_fn", V(c.AttachmentFileName)); cmd.Parameters.Add("p_fp", V(c.AttachmentFilePath)); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_id", c.Id);
            await cmd.ExecuteNonQueryAsync();
        }
        if (notify && isNew)
            Push("New DBV correspondence: " + c.Title.Trim(), $"Dated {c.CorrDate:dd MMM yyyy}. Tap to read it.", "dbv/correspondence", "Digital Business Vertical");
        return c.Id;
    }

    public async Task SetCorrespondenceArchivedAsync(long id, bool archived, string actor)
    {
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("UPDATE RP_DBV_CORRESPONDENCE SET IS_ARCHIVED=:p_a, UPDATED_BY_EMP=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id", cn) { BindByName = true };
        cmd.Parameters.Add("p_a", archived ? 1 : 0); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteCorrespondenceAsync(long id)
    {
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("DELETE FROM RP_DBV_CORRESPONDENCE WHERE ID=:p_id", cn) { BindByName = true };
        cmd.Parameters.Add("p_id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ══ Push notification (Hangfire fan-out; never blocks or fails the save) ══
    private void Push(string title, string message, string url, string category)
    {
        try { BackgroundJob.Enqueue<DbvPushJob>(j => j.RunAsync(title, message, url, category)); }
        catch (Exception ex) { _log.LogWarning(ex, "DBV push could not be queued for '{Title}'", title); }
    }

    // ══ Files ══
    private static readonly string[] AllowedExt = { ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".doc", ".docx" };

    public async Task<(string RelativePath, string FileName)> SaveFileAsync(string subFolder, Stream content, string originalFileName)
    {
        var safe = new string((originalFileName ?? "file").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray()).Replace("..", "_");
        if (string.IsNullOrWhiteSpace(safe)) safe = "file";
        var ext = Path.GetExtension(safe).ToLowerInvariant();
        if (!AllowedExt.Contains(ext)) throw new InvalidOperationException("Only images, PDF and Word files are allowed.");
        var folder = subFolder is "campaigns" or "correspondence" ? subFolder : "misc";
        var stored = $"{Guid.NewGuid():N}_{safe}";

        // Save under the NFS-aware static-asset root (wwwroot/MyDiary/uploads/dbv/... under NFS,
        // wwwroot/uploads/dbv/... locally) instead of hard-coding WebRootPath. DBV creatives and
        // attachments are served as plain static-file URLs (see DbvCampaigns / DbvCorrespondencePage
        // etc.), so both the physical write location and the returned URL are resolved through IStaticAssetPathResolver
        var relStorePath = $"uploads/dbv/{folder}/{stored}";
        var dir = _assets.Resolve("uploads", "dbv", folder);
        Directory.CreateDirectory(dir);
        await using (var fs = File.Create(Path.Combine(dir, stored)))
            await content.CopyToAsync(fs);

        // Prefix with the NFS subfolder when NFS is enabled (e.g. MyDiary/uploads/dbv/...) so the
        // static-file middleware finds it; locally this is unchanged ("uploads/dbv/...").
        var relWebPath = _assets.ResolveUrl(relStorePath);
        _log.LogInformation("DBV file saved: Path={RelativePath}", relWebPath);
        return (relWebPath, safe);
    }

    // ══ Saved criteria ══
    public async Task<List<DbvSavedCriteria>> ListSavedCriteriaAsync(string empCode)
    {
        var l = new List<DbvSavedCriteria>();
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT CRITERIA_ID, CRITERIA_NAME, CRITERIA_JSON FROM RP_DBV_SAVED_CRITERIA WHERE UPPER(EMP_CODE)=UPPER(:p_e) ORDER BY CRITERIA_NAME", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) l.Add(new DbvSavedCriteria(Convert.ToInt64(r.GetDecimal(0)), r.GetString(1), r.GetString(2)));
        return l;
    }

    public async Task SaveCriteriaAsync(string empCode, string name, DbvFilter filter)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) throw new InvalidOperationException("Give the criteria a name.");
        var json = JsonSerializer.Serialize(filter);
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_DBV_SAVED_CRITERIA t USING (SELECT :p_e AS E, :p_n AS N FROM DUAL) s ON (UPPER(t.EMP_CODE)=UPPER(s.E) AND UPPER(t.CRITERIA_NAME)=UPPER(s.N))
            WHEN MATCHED THEN UPDATE SET CRITERIA_JSON=:p_j, CREATED_AT=SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (CRITERIA_ID, EMP_CODE, CRITERIA_NAME, CRITERIA_JSON) VALUES (RP_SEQ_GLOBAL.NEXTVAL, s.E, s.N, :p_j2)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode); cmd.Parameters.Add("p_n", name); cmd.Parameters.Add("p_j", json); cmd.Parameters.Add("p_j2", json);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteCriteriaAsync(string empCode, long id)
    {
        await using var cn = Conn(); await cn.OpenAsync();
        await using var cmd = new OracleCommand("DELETE FROM RP_DBV_SAVED_CRITERIA WHERE CRITERIA_ID=:p_id AND UPPER(EMP_CODE)=UPPER(:p_e)", cn) { BindByName = true };
        cmd.Parameters.Add("p_id", id); cmd.Parameters.Add("p_e", empCode);
        await cmd.ExecuteNonQueryAsync();
    }

    public DbvFilter? ParseCriteria(string json) { try { return JsonSerializer.Deserialize<DbvFilter>(json); } catch { return null; } }

    // ══ Excel: MIS format (DBV_FORMAT.xlsx) ══
    // First 8 columns are the department's MIS format exactly; the extra columns let an export be edited and re-uploaded.
    private static readonly string[] MisCols =
    {
        "Financial Year", "Quarter (Q1/Q2/Q3/Q4)", "Month", "Date of Communication", "Product name",
        "Campaign mode (SMS /WhatsApp/Email/Other Digital Channel)", "Template ID/No.", "Message Content",
        "Campaign Name", "Campaign Start Date", "Campaign End Date (blank = Ongoing)", "Target Segment (ETB/NTB/BOTH/OTHER)", "Description", "Remarks"
    };
    private const string MisTitle = "MIS Parameters for Campaign Card proposed for Digital Business Vertical";

    private static IXLWorksheet MisSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("MIS Format");
        ws.Cell(1, 1).Value = MisTitle; ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        for (int c = 0; c < MisCols.Length; c++)
        {
            var cell = ws.Cell(2, c + 1); cell.Value = MisCols[c];
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(c < 8 ? XLColor.FromHtml("#1E3A8A") : XLColor.FromHtml("#64748B"));
            cell.Style.Alignment.WrapText = true; cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(2).Height = 46; ws.SheetView.FreezeRows(2);
        double[] w = { 14, 12, 12, 16, 26, 26, 16, 60, 30, 16, 20, 20, 40, 30 };
        for (int c = 0; c < w.Length; c++) ws.Column(c + 1).Width = w[c];
        ws.Range(3, 6, 502, 6).SetDataValidation().List("\"SMS,WhatsApp,Email,Google Ads,Other Digital Channel\"");
        ws.Range(3, 12, 502, 12).SetDataValidation().List("\"ETB,NTB,BOTH,OTHER\"");
        return ws;
    }

    public byte[] GenerateTemplate()
    {
        using var wb = new XLWorkbook();
        MisSheet(wb);
        var ins = wb.Worksheets.Add("Instructions");
        ins.Cell(1, 1).Value = "DBV campaign MIS - upload template"; ins.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(XLColor.FromHtml("#1E3A8A"));
        string[] rules =
        {
            "1. Sheet 'MIS Format' has one row per communication. The first 8 columns are the DBV MIS format; the grey columns after them are needed to create or match the campaign.",
            "2. Rows with the same Campaign Name + Product name belong to ONE campaign (multiple modes / templates / dates in a single campaign). If that campaign already exists, new communication rows are added to it (exact duplicates - same date, mode and template - are skipped).",
            "3. Financial Year, Quarter and Month are derived from 'Date of Communication' (FY = April-March). Leave them blank or fill them - if filled they must match the date.",
            "4. For a new campaign, Campaign Start Date defaults to the earliest Date of Communication. Leave Campaign End Date blank to mark the campaign Ongoing (end date not mandatory).",
            "5. Creatives (images) cannot be uploaded through Excel - attach them from the campaign screen afterwards.",
            "6. The upload is validated first; nothing is saved until you confirm. Errors block the upload; warnings do not.",
        };
        int r = 3; foreach (var t in rules) { ins.Cell(r, 1).Value = t; ins.Range(r, 1, r, 5).Merge().Style.Alignment.WrapText = true; ins.Row(r).Height = 32; r++; }
        r++;
        string[] h = { "Column", "Required", "Format / allowed values", "Description", "Example" };
        for (int i = 0; i < h.Length; i++) { ins.Cell(r, i + 1).Value = h[i]; ins.Cell(r, i + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F7")); }
        r++;
        (string, string, string, string, string)[] rows =
        {
            (MisCols[0], "No", "yyyy-yy, e.g. 2025-26", "Financial year of the communication (derived from the date).", "2025-26"),
            (MisCols[1], "No", "Q1 / Q2 / Q3 / Q4", "Q1 Apr-Jun, Q2 Jul-Sep, Q3 Oct-Dec, Q4 Jan-Mar (derived).", "Q2"),
            (MisCols[2], "No", "Month name", "Month of the communication (derived).", "August"),
            (MisCols[3], "Yes", "Real Excel date (dd-mmm-yyyy)", "Date the communication / campaign execution went out.", "19-Aug-2026"),
            (MisCols[4], "Yes", "Text, max 150", "Product the campaign promotes.", "Digital Savings Account"),
            (MisCols[5], "Yes", "SMS / WhatsApp / Email / Google Ads / Other Digital Channel", "Mode of communication.", "WhatsApp"),
            (MisCols[6], "No", "Text, max 100", "Approved template ID / number used.", "TMPL-2026-0142"),
            (MisCols[7], "No", "Long text", "The message content that was sent.", "Open your Union Digital Savings account in 5 minutes..."),
            (MisCols[8], "Yes", "Text, max 200", "Groups rows into a campaign (with Product name).", "Union Digital Savings Push"),
            (MisCols[9], "No", "Real Excel date", "Campaign start. Defaults to the earliest communication date.", "01-Aug-2026"),
            (MisCols[10], "No", "Real Excel date or blank", "Campaign end. Blank = Ongoing.", "31-Oct-2026"),
            (MisCols[11], "No", "ETB / NTB / BOTH / OTHER", "Target segment (existing-to-bank / new-to-bank). Default BOTH.", "ETB"),
            (MisCols[12], "No", "Text, max 2000", "Campaign description.", "Push to open digital savings accounts."),
            (MisCols[13], "No", "Text, max 2000", "Any other relevant campaign details.", "Applies to metro branches first."),
        };
        foreach (var x in rows)
        {
            ins.Cell(r, 1).Value = x.Item1; ins.Cell(r, 2).Value = x.Item2; ins.Cell(r, 3).Value = x.Item3; ins.Cell(r, 4).Value = x.Item4; ins.Cell(r, 5).Value = x.Item5;
            ins.Range(r, 1, r, 5).Style.Alignment.WrapText = true; ins.Range(r, 1, r, 5).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top; r++;
        }
        double[] iw = { 36, 10, 36, 56, 36 }; for (int i = 0; i < iw.Length; i++) ins.Column(i + 1).Width = iw[i];
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    public async Task<byte[]> ExportAsync(DbvFilter filter)
    {
        var camps = await ListCampaignsAsync(filter);
        using var wb = new XLWorkbook();
        var ws = MisSheet(wb);
        int row = 3;
        foreach (var c in camps)
        {
            var comms = c.Comms.Count > 0 ? c.Comms : new List<DbvComm>();
            IEnumerable<DbvComm?> src = comms.Count > 0 ? comms : new List<DbvComm?> { null };
            foreach (var m in src)
            {
                if (m is not null)
                {
                    ws.Cell(row, 1).Value = DbvLookups.FinancialYear(m.Date); ws.Cell(row, 2).Value = DbvLookups.Quarter(m.Date); ws.Cell(row, 3).Value = DbvLookups.MonthName(m.Date);
                    ws.Cell(row, 4).Value = m.Date; ws.Cell(row, 4).Style.DateFormat.Format = "dd-mmm-yyyy";
                    ws.Cell(row, 6).Value = DbvLookups.ModeLabel(m.Mode); ws.Cell(row, 7).Value = m.TemplateNo; ws.Cell(row, 8).Value = m.Message;
                }
                ws.Cell(row, 5).Value = c.ProductName; ws.Cell(row, 9).Value = c.CampaignName;
                ws.Cell(row, 10).Value = c.StartDate; ws.Cell(row, 10).Style.DateFormat.Format = "dd-mmm-yyyy";
                if (!c.IsOngoing && c.EndDate.HasValue) { ws.Cell(row, 11).Value = c.EndDate.Value; ws.Cell(row, 11).Style.DateFormat.Format = "dd-mmm-yyyy"; }
                ws.Cell(row, 12).Value = c.TargetSegment; ws.Cell(row, 13).Value = c.Description; ws.Cell(row, 14).Value = c.Remarks;
                row++;
            }
        }
        // criteria used, for the record
        var info = wb.Worksheets.Add("Criteria");
        info.Cell(1, 1).Value = "Export criteria"; info.Cell(1, 1).Style.Font.SetBold();
        var lines = new (string, string)[]
        {
            ("Generated", DateTime.Now.ToString("dd MMM yyyy HH:mm")), ("Scope", filter.ArchivedOnly ? "Archived campaigns" : filter.IncludeArchived ? "Active + archived" : "Active campaigns"),
            ("Product / campaign contains", filter.Product ?? "(any)"), ("From", filter.From?.ToString("dd MMM yyyy") ?? "(any)"), ("To", filter.To?.ToString("dd MMM yyyy") ?? "(any)"),
            ("Status", filter.Status), ("Mode", filter.Mode is null ? "(any)" : DbvLookups.ModeLabel(filter.Mode)), ("Financial year", filter.FinancialYear ?? "(any)"), ("Quarter", filter.Quarter ?? "(any)"),
            ("Campaigns exported", camps.Count.ToString()),
        };
        int i = 2; foreach (var (k, v) in lines) { info.Cell(i, 1).Value = k; info.Cell(i, 2).Value = v; i++; }
        info.Column(1).Width = 30; info.Column(2).Width = 40;
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    // ══ Excel upload ══
    private sealed record UpRow(int Line, string Campaign, string Product, DateTime Date, string Mode, string? Template, string? Message, DateTime? Start, DateTime? End, string Segment, string? Desc, string? Remarks);

    public async Task<DbvUploadResult> ProcessUploadAsync(Stream file, string actor, bool apply)
    {
        var res = new DbvUploadResult();
        XLWorkbook wb;
        try { wb = new XLWorkbook(file); } catch (Exception ex) { res.Errors.Add("The file could not be read as an Excel (.xlsx) workbook: " + ex.Message); return res; }
        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("MIS Format", StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheets.First();
            // locate header row (first 6 rows) by the "Product name" header
            static string Norm(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            int hdr = 0; var cols = new Dictionary<string, int>();
            for (int r = 1; r <= 6 && hdr == 0; r++)
            {
                var found = new Dictionary<string, int>();
                for (int c = 1; c <= (ws.LastColumnUsed()?.ColumnNumber() ?? 0); c++)
                {
                    var n = Norm(ws.Cell(r, c).GetString());
                    if (n.Length == 0) continue;
                    if (n.StartsWith("financialyear")) found["fy"] = c; else if (n.StartsWith("quarter")) found["q"] = c; else if (n == "month") found["m"] = c;
                    else if (n.StartsWith("dateofcommunication")) found["date"] = c; else if (n.StartsWith("productname")) found["prod"] = c;
                    else if (n.StartsWith("campaignmode")) found["mode"] = c; else if (n.StartsWith("templateid")) found["tmpl"] = c; else if (n.StartsWith("messagecontent")) found["msg"] = c;
                    else if (n.StartsWith("campaignname")) found["name"] = c; else if (n.StartsWith("campaignstartdate")) found["start"] = c; else if (n.StartsWith("campaignenddate")) found["end"] = c;
                    else if (n.StartsWith("targetsegment")) found["seg"] = c; else if (n == "description") found["desc"] = c; else if (n == "remarks") found["rem"] = c;
                }
                if (found.ContainsKey("prod") && found.ContainsKey("date")) { hdr = r; cols = found; }
            }
            if (hdr == 0) { res.Errors.Add("Header row not found. The sheet must contain the columns 'Date of Communication' and 'Product name' (use the downloaded template)."); return res; }
            foreach (var need in new[] { ("mode", "Campaign mode"), ("name", "Campaign Name") })
                if (!cols.ContainsKey(need.Item1)) res.Errors.Add($"Required column '{need.Item2}' is missing.");
            if (res.Errors.Count > 0) return res;

            string? Str(int r, string k) { if (!cols.TryGetValue(k, out var c)) return null; var s = ws.Cell(r, c).GetString().Trim(); return s.Length == 0 ? null : s; }
            DateTime? Dt(int r, string k, string label)
            {
                if (!cols.TryGetValue(k, out var c)) return null; var cell = ws.Cell(r, c); if (cell.IsEmpty()) return null;
                if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();
                if (DateTime.TryParse(cell.GetString(), CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out var d)) return d;
                res.Errors.Add($"Row {r}: {label} '{cell.GetString()}' is not a valid date."); return null;
            }

            var rows = new List<UpRow>();
            int last = ws.LastRowUsed()?.RowNumber() ?? hdr;
            for (int r = hdr + 1; r <= last; r++)
            {
                if (cols.Values.All(c => ws.Cell(r, c).IsEmpty())) continue;
                res.Rows++;
                var name = Str(r, "name"); var prod = Str(r, "prod"); var date = Dt(r, "date", "Date of Communication"); var modeTxt = Str(r, "mode");
                if (name is null) res.Errors.Add($"Row {r}: Campaign Name is required.");
                if (prod is null) res.Errors.Add($"Row {r}: Product name is required.");
                if (date is null && !res.Errors.Any(e => e.StartsWith($"Row {r}:"))) res.Errors.Add($"Row {r}: Date of Communication is required.");
                var mode = DbvLookups.ModeCode(modeTxt);
                if (mode is null) res.Errors.Add($"Row {r}: Campaign mode '{modeTxt}' must be SMS, WhatsApp, Email, Google Ads or Other Digital Channel.");
                var seg = (Str(r, "seg") ?? "BOTH").ToUpperInvariant();
                if (!DbvCampaign.Segments.Contains(seg)) res.Errors.Add($"Row {r}: Target Segment must be ETB, NTB, BOTH or OTHER.");
                var start = Dt(r, "start", "Campaign Start Date"); var end = Dt(r, "end", "Campaign End Date");
                if (start.HasValue && end.HasValue && end < start) res.Errors.Add($"Row {r}: Campaign End Date is before the Start Date.");
                if (date.HasValue)
                {
                    var fy = Str(r, "fy"); var q = Str(r, "q");
                    if (fy != null && !fy.Replace("FY", "").Trim().Equals(DbvLookups.FinancialYear(date.Value), StringComparison.OrdinalIgnoreCase)) res.Warnings.Add($"Row {r}: Financial Year '{fy}' does not match the date (should be {DbvLookups.FinancialYear(date.Value)}); the date is used.");
                    if (q != null && !q.Trim().Equals(DbvLookups.Quarter(date.Value), StringComparison.OrdinalIgnoreCase)) res.Warnings.Add($"Row {r}: Quarter '{q}' does not match the date (should be {DbvLookups.Quarter(date.Value)}); the date is used.");
                }
                if (name is not null && prod is not null && date.HasValue && mode is not null && DbvCampaign.Segments.Contains(seg))
                    rows.Add(new UpRow(r, name, prod, date.Value, mode, Str(r, "tmpl"), Str(r, "msg"), start, end, seg, Str(r, "desc"), Str(r, "rem")));
            }
            if (res.Rows == 0) res.Errors.Add("No data rows found below the header row.");
            if (res.Errors.Count > 0) return res;

            // group + compare with existing campaigns
            var groups = rows.GroupBy(x => (x.Campaign.ToUpperInvariant(), x.Product.ToUpperInvariant())).ToList();
            res.Campaigns = groups.Count;
            var existing = await ListCampaignsAsync(new DbvFilter { IncludeArchived = true });
            var plan = new List<(DbvCampaign camp, bool isNew, List<DbvComm> add)>();
            foreach (var g in groups)
            {
                var first = g.First();
                var camp = existing.FirstOrDefault(c => c.CampaignName.Equals(first.Campaign, StringComparison.OrdinalIgnoreCase) && c.ProductName.Equals(first.Product, StringComparison.OrdinalIgnoreCase));
                bool isNew = camp is null;
                if (isNew)
                {
                    var start = g.Select(x => x.Start).FirstOrDefault(x => x.HasValue) ?? g.Min(x => x.Date);
                    var end = g.Select(x => x.End).FirstOrDefault(x => x.HasValue);
                    camp = new DbvCampaign { CampaignName = first.Campaign, ProductName = first.Product, TargetSegment = first.Segment, Description = g.Select(x => x.Desc).FirstOrDefault(x => x != null),
                        Remarks = g.Select(x => x.Remarks).FirstOrDefault(x => x != null), StartDate = start, EndDate = end, IsOngoing = end is null };
                    res.NewCampaigns++;
                }
                var add = new List<DbvComm>();
                foreach (var x in g)
                {
                    if (camp!.Comms.Any(c => c.Date.Date == x.Date.Date && c.Mode == x.Mode && (c.TemplateNo ?? "") == (x.Template ?? "")) || add.Any(c => c.Date.Date == x.Date.Date && c.Mode == x.Mode && (c.TemplateNo ?? "") == (x.Template ?? "")))
                    { res.Skipped++; res.Warnings.Add($"Row {x.Line}: duplicate communication (same date, mode and template) skipped."); continue; }
                    add.Add(new DbvComm { Date = x.Date, Mode = x.Mode, TemplateNo = x.Template, Message = x.Message });
                }
                res.CommsAdded += add.Count;
                plan.Add((camp!, isNew, add));
            }
            if (!apply) return res;

            try
            {
                foreach (var (camp, isNew, add) in plan)
                {
                    if (add.Count == 0 && !isNew) continue;
                    camp.Comms.AddRange(add);
                    await SaveCampaignAsync(camp, actor, notify: false);
                }
                res.Applied = true;
            }
            catch (Exception ex) { _log.LogError(ex, "DBV upload failed"); res.Errors.Add("Saving failed: " + ex.Message); }
        }
        return res;
    }

    public async Task<List<DbvDownload>> ListDownloadsAsync(bool includeArchived)
    {
        var l = new List<DbvDownload>();

        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand($@"
        SELECT ID,
               TITLE,
               DESCRIPTION,
               FILE_NAME,
               FILE_PATH,
               IS_ARCHIVED,
               CREATED_BY_EMP,
               CREATED_AT,
               UPDATED_BY_EMP,
               UPDATED_AT
        FROM RP_DBV_DOWNLOADS
        {(includeArchived ? "" : "WHERE IS_ARCHIVED = 0")}
        ORDER BY ID DESC", cn);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            l.Add(new DbvDownload
            {
                Id = Convert.ToInt64(r.GetDecimal(0)),
                Title = r.GetString(1),
                Description = Clob(r, 2),
                FileName = S(r, 3),
                FilePath = S(r, 4),
                IsArchived = Convert.ToInt32(r.GetDecimal(5)) == 1,
                CreatedByEmp = r.GetString(6),
                CreatedAt = r.GetDateTime(7),
                UpdatedByEmp = S(r, 8),
                UpdatedAt = r.IsDBNull(9) ? null : r.GetDateTime(9)
            });
        }

        return l;
    }
    public async Task<long> SaveDownloadAsync(
    DbvDownload d,
    string actor,
    bool notify)
    {
        if (string.IsNullOrWhiteSpace(d.Title))
            throw new InvalidOperationException("Title is required.");

        bool isNew = d.Id == 0;

        await using var cn = Conn();
        await cn.OpenAsync();

        if (isNew)
        {
            await using var idCmd =
                new OracleCommand(
                    "SELECT RP_SEQ_GLOBAL.NEXTVAL FROM DUAL",
                    cn);

            d.Id = Convert.ToInt64(
                await idCmd.ExecuteScalarAsync());

            await using var cmd = new OracleCommand(@"
            INSERT INTO RP_DBV_DOWNLOADS
            (
                ID,
                TITLE,
                DESCRIPTION,
                FILE_NAME,
                FILE_PATH,
                CREATED_BY_EMP
            )
            VALUES
            (
                :p_id,
                :p_t,
                :p_desc,
                :p_fn,
                :p_fp,
                :p_by
            )", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_id", d.Id);
            cmd.Parameters.Add("p_t", d.Title.Trim());
            cmd.Parameters.Add("p_desc", V(d.Description));
            cmd.Parameters.Add("p_fn", V(d.FileName));
            cmd.Parameters.Add("p_fp", V(d.FilePath));
            cmd.Parameters.Add("p_by", actor);

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = new OracleCommand(@"
            UPDATE RP_DBV_DOWNLOADS
               SET TITLE=:p_t,
                   DESCRIPTION=:p_desc,
                   FILE_NAME=:p_fn,
                   FILE_PATH=:p_fp,
                   UPDATED_BY_EMP=:p_by,
                   UPDATED_AT=SYSTIMESTAMP
             WHERE ID=:p_id", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_t", d.Title.Trim());
            cmd.Parameters.Add("p_desc", V(d.Description));
            cmd.Parameters.Add("p_fn", V(d.FileName));
            cmd.Parameters.Add("p_fp", V(d.FilePath));
            cmd.Parameters.Add("p_by", actor);
            cmd.Parameters.Add("p_id", d.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        if (notify && isNew)
        {
            Push(
                "New DBV Download: " + d.Title,
                "A new downloadable file has been published.",
                "dbv/downloads",
                "Digital Business Vertical");
        }

        return d.Id;
    }
    public async Task SetDownloadArchivedAsync(
    long id,
    bool archived,
    string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        UPDATE RP_DBV_DOWNLOADS
           SET IS_ARCHIVED=:p_a,
               UPDATED_BY_EMP=:p_by,
               UPDATED_AT=SYSTIMESTAMP
         WHERE ID=:p_id", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_a", archived ? 1 : 0);
        cmd.Parameters.Add("p_by", actor);
        cmd.Parameters.Add("p_id", id);

        await cmd.ExecuteNonQueryAsync();
    }
    public async Task DeleteDownloadAsync(long id)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(
            "DELETE FROM RP_DBV_DOWNLOADS WHERE ID=:p_id",
            cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<DbvOtherInitiative>> ListOtherInitiativesAsync(bool includeArchived)
    {
        var list = new List<DbvOtherInitiative>();

        await using var cn = Conn();

        await cn.OpenAsync();

        await using var cmd = new OracleCommand($@"
        SELECT ID,
               TITLE,
               DESCRIPTION,
               FILE_NAME,
               FILE_PATH,
               IS_ARCHIVED,
               CREATED_BY_EMP,
               CREATED_AT,
               UPDATED_BY_EMP,
               UPDATED_AT
        FROM RP_DBV_OTHER_INITIATIVES
        {(includeArchived ? "" : "WHERE IS_ARCHIVED = 0")}
        ORDER BY ID DESC",
            cn);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new DbvOtherInitiative
            {
                Id = Convert.ToInt64(r.GetDecimal(0)),
                Title = r.GetString(1),
                Description = Clob(r, 2),
                FileName = S(r, 3),
                FilePath = S(r, 4),
                IsArchived = Convert.ToInt32(r.GetDecimal(5)) == 1,
                CreatedByEmp = r.GetString(6),
                CreatedAt = r.GetDateTime(7),
                UpdatedByEmp = S(r, 8),
                UpdatedAt = r.IsDBNull(9) ? null : r.GetDateTime(9)
            });
        }

        return list;
    }

    public async Task<long> SaveOtherInitiativeAsync(
    DbvOtherInitiative item,
    string actor,
    bool notify)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
            throw new InvalidOperationException("Title is required.");

        bool isNew = item.Id == 0;

        await using var cn = Conn();

        await cn.OpenAsync();

        if (isNew)
        {
            await using var idCmd =
                new OracleCommand(
                    "SELECT RP_SEQ_GLOBAL.NEXTVAL FROM DUAL",
                    cn);

            item.Id = Convert.ToInt64(
                await idCmd.ExecuteScalarAsync());

            await using var cmd = new OracleCommand(@"
            INSERT INTO RP_DBV_OTHER_INITIATIVES
            (
                ID,
                TITLE,
                DESCRIPTION,
                FILE_NAME,
                FILE_PATH,
                CREATED_BY_EMP
            )
            VALUES
            (
                :p_id,
                :p_title,
                :p_desc,
                :p_fn,
                :p_fp,
                :p_by
            )",
                cn)
            { BindByName = true };

            cmd.Parameters.Add("p_id", item.Id);
            cmd.Parameters.Add("p_title", item.Title.Trim());
            cmd.Parameters.Add("p_desc", V(item.Description));
            cmd.Parameters.Add("p_fn", V(item.FileName));
            cmd.Parameters.Add("p_fp", V(item.FilePath));
            cmd.Parameters.Add("p_by", actor);

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = new OracleCommand(@"
            UPDATE RP_DBV_OTHER_INITIATIVES
            SET TITLE=:p_title,
                DESCRIPTION=:p_desc,
                FILE_NAME=:p_fn,
                FILE_PATH=:p_fp,
                UPDATED_BY_EMP=:p_by,
                UPDATED_AT=SYSTIMESTAMP
            WHERE ID=:p_id",
                cn)
            { BindByName = true };

            cmd.Parameters.Add("p_title", item.Title.Trim());
            cmd.Parameters.Add("p_desc", V(item.Description));
            cmd.Parameters.Add("p_fn", V(item.FileName));
            cmd.Parameters.Add("p_fp", V(item.FilePath));
            cmd.Parameters.Add("p_by", actor);
            cmd.Parameters.Add("p_id", item.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        if (notify && isNew)
        {
            Push(
                "New DBV Initiative: " + item.Title,
                "A new initiative has been published.",
                "dbv/otherinitiatives",
                "Digital Business Vertical");
        }

        return item.Id;
    }

    public async Task SetOtherInitiativeArchivedAsync(
    long id,
    bool archived,
    string actor)
    {
        await using var cn = Conn();

        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        UPDATE RP_DBV_OTHER_INITIATIVES
        SET IS_ARCHIVED=:p_a,
            UPDATED_BY_EMP=:p_by,
            UPDATED_AT=SYSTIMESTAMP
        WHERE ID=:p_id",
            cn)
        { BindByName = true };

        cmd.Parameters.Add("p_a", archived ? 1 : 0);
        cmd.Parameters.Add("p_by", actor);
        cmd.Parameters.Add("p_id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteOtherInitiativeAsync(long id)
    {
        await using var cn = Conn();

        await cn.OpenAsync();

        await using var cmd = new OracleCommand(
            "DELETE FROM RP_DBV_OTHER_INITIATIVES WHERE ID=:p_id",
            cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", id);

        await cmd.ExecuteNonQueryAsync();
    }
    public async Task<List<DbvInitiative>> ListInitiativesAsync(
    bool includeArchived)
    {
        var list = new List<DbvInitiative>();

        await using var cn = Conn();

        await cn.OpenAsync();

        var sql = $@"
        SELECT ID,
               TITLE,
               DESCRIPTION,
               FILE_NAME,
               FILE_PATH,
               IS_ARCHIVED,
               CREATED_BY_EMP,
               CREATED_AT,
               UPDATED_BY_EMP,
               UPDATED_AT
          FROM RP_DBV_INITIATIVES
          {(includeArchived ? "" : "WHERE IS_ARCHIVED = 0")}
      ORDER BY ID DESC";

        await using var cmd = new OracleCommand(sql, cn);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new DbvInitiative
            {
                Id = Convert.ToInt64(r.GetDecimal(0)),
                Title = r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                FileName = r.IsDBNull(3) ? null : r.GetString(3),
                FilePath = r.IsDBNull(4) ? null : r.GetString(4),
                IsArchived = Convert.ToInt32(r.GetDecimal(5)) == 1,
                CreatedByEmp = r.GetString(6),
                CreatedAt = r.GetDateTime(7),
                UpdatedByEmp = r.IsDBNull(8) ? null : r.GetString(8),
                UpdatedAt = r.IsDBNull(9) ? null : r.GetDateTime(9)
            });
        }

        return list;
    }
    public async Task<long> SaveInitiativeAsync(
    DbvInitiative item,
    string actor,
    bool notify)
    {
        await using var cn = Conn();

        await cn.OpenAsync();

        if (item.Id == 0)
        {
            await using var seqCmd =
                new OracleCommand(
                    "SELECT RP_SEQ_GLOBAL.NEXTVAL FROM DUAL",
                    cn);

            item.Id = Convert.ToInt64(
                await seqCmd.ExecuteScalarAsync());

            await using var cmd = new OracleCommand(@"
            INSERT INTO RP_DBV_INITIATIVES
            (
                ID,
                TITLE,
                DESCRIPTION,
                FILE_NAME,
                FILE_PATH,
                IS_ARCHIVED,
                CREATED_BY_EMP,
                CREATED_AT
            )
            VALUES
            (
                :ID,
                :TITLE,
                :DESCRIPTION,
                :FILE_NAME,
                :FILE_PATH,
                0,
                :CREATED_BY_EMP,
                SYSTIMESTAMP
            )", cn);

            cmd.Parameters.Add("ID", item.Id);
            cmd.Parameters.Add("TITLE", item.Title);
            cmd.Parameters.Add("DESCRIPTION", (object?)item.Description ?? DBNull.Value);
            cmd.Parameters.Add("FILE_NAME", (object?)item.FileName ?? DBNull.Value);
            cmd.Parameters.Add("FILE_PATH", (object?)item.FilePath ?? DBNull.Value);
            cmd.Parameters.Add("CREATED_BY_EMP", actor);

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = new OracleCommand(@"
            UPDATE RP_DBV_INITIATIVES
               SET TITLE          = :TITLE,
                   DESCRIPTION    = :DESCRIPTION,
                   FILE_NAME      = :FILE_NAME,
                   FILE_PATH      = :FILE_PATH,
                   UPDATED_BY_EMP = :UPDATED_BY_EMP,
                   UPDATED_AT     = SYSTIMESTAMP
             WHERE ID = :ID", cn);

            cmd.Parameters.Add("TITLE", item.Title);
            cmd.Parameters.Add("DESCRIPTION", (object?)item.Description ?? DBNull.Value);
            cmd.Parameters.Add("FILE_NAME", (object?)item.FileName ?? DBNull.Value);
            cmd.Parameters.Add("FILE_PATH", (object?)item.FilePath ?? DBNull.Value);
            cmd.Parameters.Add("UPDATED_BY_EMP", actor);
            cmd.Parameters.Add("ID", item.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        return item.Id;
    }
    public async Task SetInitiativeArchivedAsync(
    long id,
    bool archived,
    string actor)
    {
        await using var cn = Conn();

        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        UPDATE RP_DBV_INITIATIVES
           SET IS_ARCHIVED   = :IS_ARCHIVED,
               UPDATED_BY_EMP = :UPDATED_BY_EMP,
               UPDATED_AT     = SYSTIMESTAMP
         WHERE ID = :ID", cn);

        cmd.Parameters.Add(
            "IS_ARCHIVED",
            archived ? 1 : 0);

        cmd.Parameters.Add(
            "UPDATED_BY_EMP",
            actor);

        cmd.Parameters.Add(
            "ID",
            id);

        await cmd.ExecuteNonQueryAsync();
    }
    public async Task DeleteInitiativeAsync(
    long id)
    {
        await using var cn = Conn();

        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        DELETE
          FROM RP_DBV_INITIATIVES
         WHERE ID = :ID", cn);

        cmd.Parameters.Add("ID", id);

        await cmd.ExecuteNonQueryAsync();
    }

}
