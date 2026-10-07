using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>ADO.NET/Oracle implementation of <see cref="IFeedbackRepo"/> — same
/// conventions as <see cref="RoVisitRepo"/>. Feedback is its own system
/// (RP_FEEDBACK_TICKET/RP_FEEDBACK_UPDATE/RP_FEEDBACK_ATTACHMENT); the only
/// Request Portal artifact it touches is read via IEscalationMatrixRepo, not
/// from here.</summary>
public sealed class FeedbackRepo : IFeedbackRepo
{
    private readonly AdoUnitOfWork _uow;
    public FeedbackRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<long> InsertTicketAsync(FeedbackTicket ticket, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var seqCmd = _uow.OracleConn.Cmd("SELECT RP_FEEDBACK_TICKET_SEQ.NEXTVAL FROM dual", _uow.OracleTx);
        var seqVal = await seqCmd.ScalarAsync<long>(ct);
        var ticketNo = $"FDB-{seqVal:000000}";

        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_FEEDBACK_TICKET
                (TICKET_NO, EMP_CODE, EMP_NAME, PF_NUMBER, ZONE, REGION, BRANCH,
                 MOBILE_NUMBER, IP_PHONE_NUMBER, CATEGORY, DESCRIPTION, STATUS, CURRENT_LEVEL)
            VALUES (:p_ticket_no, :p_emp_code, :p_emp_name, :p_pf, :p_zone, :p_region, :p_branch,
                    :p_mobile, :p_ip_phone, :p_category, :p_desc, 'Open', 1)
            RETURNING ID INTO :p_out_id", _uow.OracleTx);

        cmd.Parameters.AddIn("p_ticket_no", ticketNo);
        cmd.Parameters.AddIn("p_emp_code", ticket.EmpCode);
        cmd.Parameters.AddIn("p_emp_name", ticket.EmpName);
        cmd.Parameters.AddIn("p_pf", ticket.PfNumber);
        cmd.Parameters.AddIn("p_zone", ticket.Zone);
        cmd.Parameters.AddIn("p_region", ticket.Region);
        cmd.Parameters.AddIn("p_branch", ticket.Branch);
        cmd.Parameters.AddIn("p_mobile", ticket.MobileNumber);
        cmd.Parameters.AddIn("p_ip_phone", ticket.IpPhoneNumber);
        cmd.Parameters.AddIn("p_category", ticket.Category.ToString());
        cmd.Parameters.AddIn("p_desc", ticket.Description);
        var outId = cmd.Parameters.AddOut("p_out_id", Oracle.ManagedDataAccess.Client.OracleDbType.Decimal);

        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<IReadOnlyList<FeedbackTicket>> ListMyTicketsAsync(string empCode, FeedbackStatus? status = null, FeedbackCategory? category = null, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID, TICKET_NO, EMP_CODE, EMP_NAME, PF_NUMBER, ZONE, REGION, BRANCH,
                   MOBILE_NUMBER, IP_PHONE_NUMBER, CATEGORY, DESCRIPTION, SEVERITY, STATUS,
                   CURRENT_LEVEL, RATING, RATING_COMMENTS, RATED_AT, CREATED_AT, UPDATED_AT
              FROM RP_FEEDBACK_TICKET
             WHERE EMP_CODE = :p_emp
               AND (:p_status IS NULL OR STATUS = :p_status)
               AND (:p_category IS NULL OR CATEGORY = :p_category)
             ORDER BY CREATED_AT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        cmd.Parameters.AddIn("p_status", status?.ToString());
        cmd.Parameters.AddIn("p_category", category?.ToString());

        return await cmd.QueryAsync(MapTicket, ct);
    }

    public async Task<IReadOnlyList<FeedbackTicket>> ListAllTicketsAsync(FeedbackStatus? status = null, FeedbackCategory? category = null, string? zone = null, string? region = null, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID, TICKET_NO, EMP_CODE, EMP_NAME, PF_NUMBER, ZONE, REGION, BRANCH,
                   MOBILE_NUMBER, IP_PHONE_NUMBER, CATEGORY, DESCRIPTION, SEVERITY, STATUS,
                   CURRENT_LEVEL, RATING, RATING_COMMENTS, RATED_AT, CREATED_AT, UPDATED_AT
              FROM RP_FEEDBACK_TICKET
             WHERE (:p_status IS NULL OR STATUS = :p_status)
               AND (:p_category IS NULL OR CATEGORY = :p_category)
               AND (:p_zone IS NULL OR ZONE = :p_zone)
               AND (:p_region IS NULL OR REGION = :p_region)
             ORDER BY CREATED_AT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_status", status?.ToString());
        cmd.Parameters.AddIn("p_category", category?.ToString());
        cmd.Parameters.AddIn("p_zone", zone);
        cmd.Parameters.AddIn("p_region", region);

        return await cmd.QueryAsync(MapTicket, ct);
    }

    public async Task<FeedbackTicket?> GetTicketAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID, TICKET_NO, EMP_CODE, EMP_NAME, PF_NUMBER, ZONE, REGION, BRANCH,
                   MOBILE_NUMBER, IP_PHONE_NUMBER, CATEGORY, DESCRIPTION, SEVERITY, STATUS,
                   CURRENT_LEVEL, RATING, RATING_COMMENTS, RATED_AT, CREATED_AT, UPDATED_AT
              FROM RP_FEEDBACK_TICKET WHERE ID = :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        var ticket = await cmd.QueryOneAsync(MapTicket, ct);
        if (ticket is null) return null;

        using var updCmd = _uow.OracleConn.Cmd(@"
            SELECT ID, TICKET_ID, UPDATE_TEXT, NEW_STATUS, NEW_SEVERITY, NEW_LEVEL, UPDATED_BY_EMP, UPDATED_AT
              FROM RP_FEEDBACK_UPDATE WHERE TICKET_ID = :p_id ORDER BY UPDATED_AT", _uow.OracleTx);
        updCmd.Parameters.AddIn("p_id", id);
        ticket.Updates = (await updCmd.QueryAsync(MapUpdate, ct)).ToList();
        ticket.Attachments = (await ListAttachmentsAsync(id, ct)).ToList();

        return ticket;
    }

    public async Task AddUpdateAsync(long ticketId, string updateText, FeedbackStatus? newStatus, FeedbackSeverity? newSeverity, int? newLevel, string updatedByEmp, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var insCmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_FEEDBACK_UPDATE (TICKET_ID, UPDATE_TEXT, NEW_STATUS, NEW_SEVERITY, NEW_LEVEL, UPDATED_BY_EMP)
            VALUES (:p_ticket_id, :p_text, :p_status, :p_severity, :p_level, :p_by)", _uow.OracleTx);
        insCmd.Parameters.AddIn("p_ticket_id", ticketId);
        insCmd.Parameters.AddIn("p_text", updateText);
        insCmd.Parameters.AddIn("p_status", newStatus?.ToString());
        insCmd.Parameters.AddIn("p_severity", newSeverity?.ToString());
        insCmd.Parameters.AddIn("p_level", newLevel);
        insCmd.Parameters.AddIn("p_by", updatedByEmp);
        await insCmd.ExecAsync(ct);

        if (newStatus is null && newSeverity is null && newLevel is null) return;

        using var updCmd = _uow.OracleConn.Cmd(@"
            UPDATE RP_FEEDBACK_TICKET SET
                STATUS = COALESCE(:p_status, STATUS),
                SEVERITY = COALESCE(:p_severity, SEVERITY),
                CURRENT_LEVEL = COALESCE(:p_level, CURRENT_LEVEL),
                UPDATED_AT = SYSTIMESTAMP
            WHERE ID = :p_id", _uow.OracleTx);
        updCmd.Parameters.AddIn("p_status", newStatus?.ToString());
        updCmd.Parameters.AddIn("p_severity", newSeverity?.ToString());
        updCmd.Parameters.AddIn("p_level", newLevel);
        updCmd.Parameters.AddIn("p_id", ticketId);
        await updCmd.ExecAsync(ct);
    }

    public async Task SubmitRatingAsync(long ticketId, int rating, string? comments, CancellationToken ct = default)
    {
        if (rating is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(rating));
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            UPDATE RP_FEEDBACK_TICKET SET RATING = :p_rating, RATING_COMMENTS = :p_comments, RATED_AT = SYSTIMESTAMP
            WHERE ID = :p_id AND STATUS = 'Closed'", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rating", rating);
        cmd.Parameters.AddIn("p_comments", comments);
        cmd.Parameters.AddIn("p_id", ticketId);
        await cmd.ExecAsync(ct);
    }

    public async Task<long> InsertAttachmentAsync(FeedbackAttachment attachment, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_FEEDBACK_ATTACHMENT (TICKET_ID, FILE_NAME, FILE_PATH, MIME, UPLOADED_BY_EMP)
            VALUES (:p_ticket_id, :p_name, :p_path, :p_mime, :p_by)
            RETURNING ID INTO :p_out_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_ticket_id", attachment.TicketId);
        cmd.Parameters.AddIn("p_name", attachment.FileName);
        cmd.Parameters.AddIn("p_path", attachment.FilePath);
        cmd.Parameters.AddIn("p_mime", attachment.Mime);
        cmd.Parameters.AddIn("p_by", attachment.UploadedByEmp);
        var outId = cmd.Parameters.AddOut("p_out_id", Oracle.ManagedDataAccess.Client.OracleDbType.Decimal);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<IReadOnlyList<FeedbackAttachment>> ListAttachmentsAsync(long ticketId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID, TICKET_ID, FILE_NAME, FILE_PATH, MIME, UPLOADED_BY_EMP, UPLOADED_AT
              FROM RP_FEEDBACK_ATTACHMENT WHERE TICKET_ID = :p_id ORDER BY UPLOADED_AT", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", ticketId);
        return await cmd.QueryAsync(r => new FeedbackAttachment
        {
            Id = Long(r, "ID"),
            TicketId = Long(r, "TICKET_ID"),
            FileName = Str(r, "FILE_NAME"),
            FilePath = Str(r, "FILE_PATH"),
            Mime = StrN(r, "MIME"),
            UploadedByEmp = Str(r, "UPLOADED_BY_EMP"),
            UploadedAt = Dt(r, "UPLOADED_AT"),
        }, ct);
    }

    public async Task<FeedbackDashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var stats = new FeedbackDashboardStats();

        using (var cmd = _uow.OracleConn.Cmd(@"
            SELECT STATUS, COUNT(*) AS CNT FROM RP_FEEDBACK_TICKET GROUP BY STATUS", _uow.OracleTx))
        {
            var rows = await cmd.QueryAsync(r => (Status: Str(r, "STATUS"), Count: Int(r, "CNT")), ct);
            foreach (var (status, count) in rows)
            {
                stats.Total += count;
                if (status == "Open") stats.Open = count;
                else if (status == "InProgress") stats.InProgress = count;
                else if (status == "Closed") stats.Closed = count;
            }
        }

        using (var cmd = _uow.OracleConn.Cmd(@"
            SELECT CATEGORY, COUNT(*) AS CNT FROM RP_FEEDBACK_TICKET GROUP BY CATEGORY ORDER BY CATEGORY", _uow.OracleTx))
        {
            stats.ByCategory = (await cmd.QueryAsync(r => (Str(r, "CATEGORY"), Int(r, "CNT")), ct)).ToList();
        }

        using (var cmd = _uow.OracleConn.Cmd(@"
            SELECT SEVERITY, COUNT(*) AS CNT FROM RP_FEEDBACK_TICKET WHERE SEVERITY IS NOT NULL GROUP BY SEVERITY ORDER BY SEVERITY", _uow.OracleTx))
        {
            stats.BySeverity = (await cmd.QueryAsync(r => (Str(r, "SEVERITY"), Int(r, "CNT")), ct)).ToList();
        }

        using (var cmd = _uow.OracleConn.Cmd(@"
            SELECT CURRENT_LEVEL, COUNT(*) AS CNT FROM RP_FEEDBACK_TICKET WHERE STATUS != 'Closed' GROUP BY CURRENT_LEVEL ORDER BY CURRENT_LEVEL", _uow.OracleTx))
        {
            stats.ByLevel = (await cmd.QueryAsync(r => (Int(r, "CURRENT_LEVEL"), Int(r, "CNT")), ct)).ToList();
        }

        using (var cmd = _uow.OracleConn.Cmd(@"
            SELECT COUNT(*) AS CNT, AVG(RATING) AS AVG_RATING FROM RP_FEEDBACK_TICKET WHERE RATING IS NOT NULL", _uow.OracleTx))
        {
            using var reader = (Oracle.ManagedDataAccess.Client.OracleDataReader)await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                stats.RatedCount = Int(reader, "CNT");
                stats.AverageRating = stats.RatedCount > 0 ? Math.Round(Convert.ToDouble(reader["AVG_RATING"]), 2) : null;
            }
        }

        return stats;
    }

    private static FeedbackTicket MapTicket(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        TicketNo = Str(r, "TICKET_NO"),
        EmpCode = Str(r, "EMP_CODE"),
        EmpName = Str(r, "EMP_NAME"),
        PfNumber = StrN(r, "PF_NUMBER"),
        Zone = StrN(r, "ZONE"),
        Region = StrN(r, "REGION"),
        Branch = StrN(r, "BRANCH"),
        MobileNumber = StrN(r, "MOBILE_NUMBER"),
        IpPhoneNumber = StrN(r, "IP_PHONE_NUMBER"),
        Category = Enum.TryParse<FeedbackCategory>(Str(r, "CATEGORY"), out var cat) ? cat : FeedbackCategory.FunctionalDefects,
        Description = Str(r, "DESCRIPTION"),
        Severity = Enum.TryParse<FeedbackSeverity>(StrN(r, "SEVERITY"), out var sev) ? sev : null,
        Status = Enum.TryParse<FeedbackStatus>(Str(r, "STATUS"), out var st) ? st : FeedbackStatus.Open,
        CurrentLevel = Int(r, "CURRENT_LEVEL"),
        Rating = IntN(r, "RATING"),
        RatingComments = StrN(r, "RATING_COMMENTS"),
        RatedAt = DtN(r, "RATED_AT"),
        CreatedAt = Dt(r, "CREATED_AT"),
        UpdatedAt = DtN(r, "UPDATED_AT"),
    };

    private static FeedbackUpdateEntry MapUpdate(Oracle.ManagedDataAccess.Client.OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        TicketId = Long(r, "TICKET_ID"),
        UpdateText = Str(r, "UPDATE_TEXT"),
        NewStatus = Enum.TryParse<FeedbackStatus>(StrN(r, "NEW_STATUS"), out var st) ? st : null,
        NewSeverity = Enum.TryParse<FeedbackSeverity>(StrN(r, "NEW_SEVERITY"), out var sev) ? sev : null,
        NewLevel = IntN(r, "NEW_LEVEL"),
        UpdatedByEmp = Str(r, "UPDATED_BY_EMP"),
        UpdatedAt = Dt(r, "UPDATED_AT"),
    };
}
