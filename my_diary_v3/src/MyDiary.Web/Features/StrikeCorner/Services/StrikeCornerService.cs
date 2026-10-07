using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MyDiary.Web.Features.StrikeCorner.Models;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.StrikeCorner.Services;

/// <summary>Strike Corner. Self-contained raw Oracle ADO (same style as SubsidiaryJvService).</summary>
public sealed class StrikeCornerService : IStrikeCornerService
{
    private readonly string _cs;
    private readonly IAuthorizationService _authz;
    private readonly ILogger<StrikeCornerService> _log;
    private static readonly HashSet<string> DocContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };
    private static readonly string[] DocExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" };

    public StrikeCornerService(IConfiguration config, IAuthorizationService authz, ILogger<StrikeCornerService> log)
    {
        _cs = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner");
        _authz = authz;
        _log = log;
    }

    private OracleConnection Conn() => new(_cs);

    // ══ Card 1 gate: Super Admin / App Admin, or the RP_STRIKE_ADMIN allow-list ══
    //
    // The requirement is "Super Admin, and scale 8+ at CO" -- but this codebase has no existing
    // "scale + CO location" authorization helper to reuse, and the user confirmed (2026-09-22) their
    // HR scale-code format isn't settled yet, so automatic scale-based detection was dropped rather
    // than guessed. Until that's confirmed, add scale-8+/CO employees to RP_STRIKE_ADMIN by employee
    // code (same allow-list Super Admin uses to manage this feature) to grant them this card.
    public async Task<bool> CanCreateMomAsync(ClaimsPrincipal user)
    {
        if ((await _authz.AuthorizeAsync(user, "AppAdmin")).Succeeded) return true;

        var emp = user.FindFirst("emplid")?.Value;
        if (string.IsNullOrWhiteSpace(emp)) return false;
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT COUNT(*) FROM RP_STRIKE_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_e) AND IS_ACTIVE=1", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", emp);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<List<(string EmpCode, string AddedBy, DateTime AddedAt)>> ListMomAdminsAsync()
    {
        var l = new List<(string, string, DateTime)>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT EMP_CODE, NVL(ADDED_BY,''), ADDED_AT FROM RP_STRIKE_ADMIN WHERE IS_ACTIVE=1 ORDER BY EMP_CODE", cn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) l.Add((r.GetString(0), r.GetString(1), r.GetDateTime(2)));
        return l;
    }

    public async Task AddMomAdminAsync(string empCode, string actor)
    {
        empCode = (empCode ?? "").Trim();
        if (empCode.Length == 0) return;
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_STRIKE_ADMIN t USING (SELECT :p_e AS E FROM DUAL) s ON (UPPER(t.EMP_CODE)=UPPER(s.E))
            WHEN MATCHED THEN UPDATE SET IS_ACTIVE=1, ADDED_BY=:p_by, ADDED_AT=SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (EMP_CODE, IS_ACTIVE, ADDED_BY) VALUES (s.E, 1, :p_by2)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_by2", actor);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveMomAdminAsync(string empCode)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("UPDATE RP_STRIKE_ADMIN SET IS_ACTIVE=0 WHERE UPPER(EMP_CODE)=UPPER(:p_e)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode);
        await cmd.ExecuteNonQueryAsync();
    }

    // ══ FAQs and SOPs document library — BLOB-in-table, same convention as RP_SJV_ENTITY.LOGO_BLOB ══
    public async Task<List<StrikeFaqDoc>> ListFaqDocsAsync(string? category = null, bool activeOnly = true)
    {
        var list = new List<StrikeFaqDoc>();
        await using var cn = Conn();
        await cn.OpenAsync();
        var sql = @"SELECT DOC_ID, DOC_CATEGORY, DOC_TITLE, FILE_NAME, CONTENT_TYPE, FILE_SIZE, DISPLAY_ORDER, IS_ACTIVE,
                            ENTRY_BY, ENTRY_AT, UPDATED_BY, UPDATED_AT
                     FROM RP_STRIKE_FAQ_DOC
                     WHERE 1=1" +
                  (activeOnly ? " AND IS_ACTIVE=1" : "") +
                  (category is not null ? " AND DOC_CATEGORY=:p_cat" : "") +
                  " ORDER BY DOC_CATEGORY, DISPLAY_ORDER, DOC_TITLE";
        await using var cmd = new OracleCommand(sql, cn) { BindByName = true };
        if (category is not null) cmd.Parameters.Add("p_cat", category);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new StrikeFaqDoc
            {
                Id = Convert.ToInt64(r.GetDecimal(0)),
                Category = r.GetString(1),
                Title = r.GetString(2),
                FileName = r.GetString(3),
                ContentType = r.GetString(4),
                FileSize = Convert.ToInt64(r.GetDecimal(5)),
                DisplayOrder = Convert.ToInt32(r.GetDecimal(6)),
                IsActive = Convert.ToInt32(r.GetDecimal(7)) == 1,
                EntryBy = r.IsDBNull(8) ? null : r.GetString(8),
                EntryAt = r.IsDBNull(9) ? null : r.GetDateTime(9),
                UpdatedBy = r.IsDBNull(10) ? null : r.GetString(10),
                UpdatedAt = r.IsDBNull(11) ? null : r.GetDateTime(11),
            });
        }
        return list;
    }

    public async Task<StrikeFaqDoc?> SaveFaqDocAsync(string category, string title, byte[] bytes, string contentType, string originalFileName, string actor, long? existingId = null)
    {
        category = (category ?? "").Trim().ToUpperInvariant();
        if (category != "FAQ" && category != "SOP") throw new InvalidOperationException("Category must be FAQ or SOP.");
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("Title is required.");
        if (bytes is null || bytes.Length == 0) throw new InvalidOperationException("File is empty.");
        var ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
        if (!DocExtensions.Contains(ext)) throw new InvalidOperationException("File must be a PDF or Office document (pdf, doc, docx, xls, xlsx, ppt, pptx).");
        var safeName = new string((originalFileName ?? "document").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
        if (safeName.Length == 0) safeName = "document" + ext;

        await using var cn = Conn();
        await cn.OpenAsync();
        if (existingId is long id)
        {
            await using var cmd = new OracleCommand(@"
                UPDATE RP_STRIKE_FAQ_DOC SET DOC_CATEGORY=:p_cat, DOC_TITLE=:p_title, FILE_NAME=:p_fn, CONTENT_TYPE=:p_ct,
                    FILE_BLOB=:p_blob, FILE_SIZE=:p_size, UPDATED_BY=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE DOC_ID=:p_id", cn) { BindByName = true };
            cmd.Parameters.Add("p_cat", category); cmd.Parameters.Add("p_title", title.Trim()); cmd.Parameters.Add("p_fn", safeName); cmd.Parameters.Add("p_ct", contentType);
            cmd.Parameters.Add(new OracleParameter("p_blob", OracleDbType.Blob) { Value = bytes });
            cmd.Parameters.Add("p_size", bytes.Length); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_id", id);
            var n = await cmd.ExecuteNonQueryAsync();
            if (n == 0) throw new InvalidOperationException("Document not found.");
        }
        else
        {
            await using var cmd = new OracleCommand(@"
                INSERT INTO RP_STRIKE_FAQ_DOC (DOC_CATEGORY, DOC_TITLE, FILE_NAME, CONTENT_TYPE, FILE_BLOB, FILE_SIZE, DISPLAY_ORDER, ENTRY_BY)
                VALUES (:p_cat, :p_title, :p_fn, :p_ct, :p_blob, :p_size,
                    (SELECT NVL(MAX(DISPLAY_ORDER),0)+1 FROM RP_STRIKE_FAQ_DOC WHERE DOC_CATEGORY=:p_cat2), :p_by)
                RETURNING DOC_ID INTO :p_id", cn) { BindByName = true };
            cmd.Parameters.Add("p_cat", category); cmd.Parameters.Add("p_title", title.Trim()); cmd.Parameters.Add("p_fn", safeName); cmd.Parameters.Add("p_ct", contentType);
            cmd.Parameters.Add(new OracleParameter("p_blob", OracleDbType.Blob) { Value = bytes });
            cmd.Parameters.Add("p_size", bytes.Length); cmd.Parameters.Add("p_cat2", category); cmd.Parameters.Add("p_by", actor);
            var outId = new OracleParameter("p_id", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outId);
            await cmd.ExecuteNonQueryAsync();
        }
        _log.LogInformation("Strike Corner FAQ/SOP doc saved: {Title} ({Category}) by {Actor}", title, category, actor);
        return (await ListFaqDocsAsync(category, activeOnly: false)).FirstOrDefault(d => d.FileName == safeName && d.Title == title.Trim());
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFaqDocFileAsync(long docId)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT FILE_BLOB, CONTENT_TYPE, FILE_NAME FROM RP_STRIKE_FAQ_DOC WHERE DOC_ID=:p_id AND IS_ACTIVE=1", cn) { BindByName = true };
        cmd.Parameters.Add("p_id", docId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync() || r.IsDBNull(0)) return null;
        using var blob = r.GetOracleBlob(0);
        var bytes = new byte[blob.Length];
        if (bytes.Length > 0) blob.Read(bytes, 0, bytes.Length);
        return (bytes, r.IsDBNull(1) ? "application/octet-stream" : r.GetString(1), r.IsDBNull(2) ? "document" : r.GetString(2));
    }

    public async Task DeleteFaqDocAsync(long docId)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("UPDATE RP_STRIKE_FAQ_DOC SET IS_ACTIVE=0 WHERE DOC_ID=:p_id", cn) { BindByName = true };
        cmd.Parameters.Add("p_id", docId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<StrikeFaqDoc>> ListStrikeDataDocsAsync(bool activeOnly = true)
    {
        var list = new List<StrikeFaqDoc>();

        await using var cn = Conn();
        await cn.OpenAsync();

        var sql = @"SELECT DOC_ID, DOC_TITLE, FILE_NAME, CONTENT_TYPE,
                       FILE_SIZE, DISPLAY_ORDER, IS_ACTIVE,
                       ENTRY_BY, ENTRY_AT, UPDATED_BY, UPDATED_AT
                FROM RP_STRIKE_DATA_DOC
                WHERE 1=1"
                  + (activeOnly ? " AND IS_ACTIVE=1" : "")
                  + " ORDER BY DISPLAY_ORDER, DOC_TITLE";

        await using var cmd = new OracleCommand(sql, cn);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new StrikeFaqDoc
            {
                Id = Convert.ToInt64(r.GetDecimal(0)),
                Title = r.GetString(1),
                FileName = r.GetString(2),
                ContentType = r.GetString(3),
                FileSize = Convert.ToInt64(r.GetDecimal(4)),
                DisplayOrder = Convert.ToInt32(r.GetDecimal(5)),
                IsActive = Convert.ToInt32(r.GetDecimal(6)) == 1,
                EntryBy = r.IsDBNull(7) ? null : r.GetString(7),
                EntryAt = r.IsDBNull(8) ? null : r.GetDateTime(8),
                UpdatedBy = r.IsDBNull(9) ? null : r.GetString(9),
                UpdatedAt = r.IsDBNull(10) ? null : r.GetDateTime(10)
            });
        }

        return list;
    }

    public async Task SaveStrikeDataDocAsync(
    string title,
    byte[] bytes,
    string contentType,
    string originalFileName,
    string actor)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Title is required.");

        if (bytes == null || bytes.Length == 0)
            throw new InvalidOperationException("File is empty.");

        var ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();

        if (!DocExtensions.Contains(ext))
            throw new InvalidOperationException(
                "File must be a PDF or Office document.");

        var safeName = new string(
            (originalFileName ?? "document")
            .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
            .ToArray());

        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        INSERT INTO RP_STRIKE_DATA_DOC
        (
            DOC_TITLE,
            FILE_NAME,
            CONTENT_TYPE,
            FILE_BLOB,
            FILE_SIZE,
            DISPLAY_ORDER,
            ENTRY_BY
        )
        VALUES
        (
            :p_title,
            :p_fn,
            :p_ct,
            :p_blob,
            :p_size,
            (SELECT NVL(MAX(DISPLAY_ORDER),0)+1
               FROM RP_STRIKE_DATA_DOC),
            :p_by
        )", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_title", title.Trim());
        cmd.Parameters.Add("p_fn", safeName);
        cmd.Parameters.Add("p_ct", contentType);

        cmd.Parameters.Add(
            new OracleParameter("p_blob", OracleDbType.Blob)
            {
                Value = bytes
            });

        cmd.Parameters.Add("p_size", bytes.Length);
        cmd.Parameters.Add("p_by", actor);

        await cmd.ExecuteNonQueryAsync();
    }


    public async Task<(byte[] Bytes, string ContentType, string FileName)?>
    GetStrikeDataDocFileAsync(long docId)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        SELECT FILE_BLOB,
               CONTENT_TYPE,
               FILE_NAME
        FROM RP_STRIKE_DATA_DOC
        WHERE DOC_ID = :p_id
          AND IS_ACTIVE = 1", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", docId);

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync() || r.IsDBNull(0))
            return null;

        using var blob = r.GetOracleBlob(0);

        var bytes = new byte[blob.Length];

        if (bytes.Length > 0)
            blob.Read(bytes, 0, bytes.Length);

        return (
            bytes,
            r.GetString(1),
            r.GetString(2)
        );
    }

    public async Task DeleteStrikeDataDocAsync(long docId)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        UPDATE RP_STRIKE_DATA_DOC
           SET IS_ACTIVE = 0
         WHERE DOC_ID = :p_id", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", docId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<StrikeCommitteeMeeting>> ListStrikeCommitteeMeetingsAsync()
    {
        var list = new List<StrikeCommitteeMeeting>();

        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        SELECT M.MEETING_ID,
               M.MEETING_DATE,
               M.REMARKS,
               M.FILE_NAME,
               M.CONTENT_TYPE,
               M.FILE_SIZE,
               M.ENTRY_BY,
               M.ENTRY_AT,
               (SELECT COUNT(*)
                  FROM RP_STRIKE_COMMITTEE_ATTACHMENT A
                 WHERE A.MEETING_ID = M.MEETING_ID) ATTACHMENT_COUNT
        FROM RP_STRIKE_COMMITTEE_MEETING M
        WHERE M.IS_ACTIVE = 1
        ORDER BY M.MEETING_DATE DESC", cn);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new StrikeCommitteeMeeting
            {
                Id = Convert.ToInt64(r.GetDecimal(0)),
                MeetingDate = r.GetDateTime(1),
                Remarks = r.GetString(2),
                FileName = r.GetString(3),
                ContentType = r.GetString(4),
                FileSize = Convert.ToInt64(r.GetDecimal(5)),
                EntryBy = r.IsDBNull(6) ? null : r.GetString(6),
                EntryAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
                AttachmentCount = Convert.ToInt32(r.GetDecimal(8))
            });
        }

        return list;
    }

    public async Task<long> SaveStrikeCommitteeMeetingAsync(
    long? meetingId,
    DateTime meetingDate,
    string remarks,
    byte[] fileBytes,
    string contentType,
    string fileName,
    string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        if (meetingId.HasValue)
        {
            await using var cmd = new OracleCommand(@"
            UPDATE RP_STRIKE_COMMITTEE_MEETING
               SET MEETING_DATE = :p_date,
                   REMARKS = :p_remarks,
                   FILE_NAME = :p_file,
                   CONTENT_TYPE = :p_type,
                   FILE_BLOB = :p_blob,
                   FILE_SIZE = :p_size,
                   UPDATED_BY = :p_by,
                   UPDATED_AT = SYSTIMESTAMP
             WHERE MEETING_ID = :p_id", cn)
            { BindByName = true };

            cmd.Parameters.Add("p_date", meetingDate);
            cmd.Parameters.Add("p_remarks", remarks);
            cmd.Parameters.Add("p_file", fileName);
            cmd.Parameters.Add("p_type", contentType);
            cmd.Parameters.Add(new OracleParameter("p_blob", OracleDbType.Blob) { Value = fileBytes });
            cmd.Parameters.Add("p_size", fileBytes.Length);
            cmd.Parameters.Add("p_by", actor);
            cmd.Parameters.Add("p_id", meetingId.Value);

            await cmd.ExecuteNonQueryAsync();

            return meetingId.Value;
        }

        await using var insert = new OracleCommand(@"
        INSERT INTO RP_STRIKE_COMMITTEE_MEETING
        (
            MEETING_DATE,
            REMARKS,
            FILE_NAME,
            CONTENT_TYPE,
            FILE_BLOB,
            FILE_SIZE,
            ENTRY_BY
        )
        VALUES
        (
            :p_date,
            :p_remarks,
            :p_file,
            :p_type,
            :p_blob,
            :p_size,
            :p_by
        )
        RETURNING MEETING_ID INTO :p_id", cn)
        { BindByName = true };

        insert.Parameters.Add("p_date", meetingDate);
        insert.Parameters.Add("p_remarks", remarks);
        insert.Parameters.Add("p_file", fileName);
        insert.Parameters.Add("p_type", contentType);
        insert.Parameters.Add(new OracleParameter("p_blob", OracleDbType.Blob) { Value = fileBytes });
        insert.Parameters.Add("p_size", fileBytes.Length);
        insert.Parameters.Add("p_by", actor);

        var idParam = new OracleParameter("p_id", OracleDbType.Int64)
        {
            Direction = System.Data.ParameterDirection.Output
        };

        insert.Parameters.Add(idParam);

        await insert.ExecuteNonQueryAsync();

        return Convert.ToInt64(idParam.Value.ToString());
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?>
    GetStrikeCommitteeFileAsync(long meetingId)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        SELECT FILE_BLOB,
               CONTENT_TYPE,
               FILE_NAME
          FROM RP_STRIKE_COMMITTEE_MEETING
         WHERE MEETING_ID = :p_id
           AND IS_ACTIVE = 1", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", meetingId);

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
            return null;

        using var blob = r.GetOracleBlob(0);

        var bytes = new byte[blob.Length];

        if (bytes.Length > 0)
            blob.Read(bytes, 0, bytes.Length);

        return (
            bytes,
            r.GetString(1),
            r.GetString(2)
        );
    }

    public async Task DeleteStrikeCommitteeMeetingAsync(long meetingId)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        UPDATE RP_STRIKE_COMMITTEE_MEETING
           SET IS_ACTIVE = 0
         WHERE MEETING_ID = :p_id", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", meetingId);

        await cmd.ExecuteNonQueryAsync();
    }


    public async Task<List<StrikeCommitteeAttachment>>
    ListStrikeCommitteeAttachmentsAsync(long meetingId)
    {
        var list = new List<StrikeCommitteeAttachment>();

        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        SELECT ATTACHMENT_ID,
               FILE_NAME,
               CONTENT_TYPE
          FROM RP_STRIKE_COMMITTEE_ATTACHMENT
         WHERE MEETING_ID = :p_id
         ORDER BY ATTACHMENT_ID", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", meetingId);

        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new StrikeCommitteeAttachment
            {
                Id = Convert.ToInt64(r.GetDecimal(0)),
                MeetingId = meetingId,
                FileName = r.GetString(1),
                ContentType = r.GetString(2)
            });
        }

        return list;
    }

    public async Task SaveStrikeCommitteeAttachmentAsync(
    long meetingId,
    byte[] fileBytes,
    string contentType,
    string fileName,
    string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        INSERT INTO RP_STRIKE_COMMITTEE_ATTACHMENT
        (
            MEETING_ID,
            FILE_NAME,
            CONTENT_TYPE,
            FILE_BLOB,
            FILE_SIZE,
            ENTRY_BY
        )
        VALUES
        (
            :p_meeting,
            :p_file,
            :p_type,
            :p_blob,
            :p_size,
            :p_by
        )", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_meeting", meetingId);
        cmd.Parameters.Add("p_file", fileName);
        cmd.Parameters.Add("p_type", contentType);
        cmd.Parameters.Add(new OracleParameter("p_blob", OracleDbType.Blob) { Value = fileBytes });
        cmd.Parameters.Add("p_size", fileBytes.Length);
        cmd.Parameters.Add("p_by", actor);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?>
    GetStrikeCommitteeAttachmentAsync(long attachmentId)
    {
        await using var cn = Conn();
        await cn.OpenAsync();

        await using var cmd = new OracleCommand(@"
        SELECT FILE_BLOB,
               CONTENT_TYPE,
               FILE_NAME
          FROM RP_STRIKE_COMMITTEE_ATTACHMENT
         WHERE ATTACHMENT_ID = :p_id", cn)
        { BindByName = true };

        cmd.Parameters.Add("p_id", attachmentId);

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync())
            return null;

        using var blob = r.GetOracleBlob(0);

        var bytes = new byte[blob.Length];

        if (bytes.Length > 0)
            blob.Read(bytes, 0, bytes.Length);

        return (
            bytes,
            r.GetString(1),
            r.GetString(2)
        );
    }
}
