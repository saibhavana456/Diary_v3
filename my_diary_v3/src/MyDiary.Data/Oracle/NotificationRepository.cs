using MyDiary.Core;
using MyDiary.Core.Models;
using MyDiary.Core.Abstractions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Data.Oracle;

/// <summary>
/// Notification outbox (email/SMS) and in-app bell queries against Oracle.
/// All SQL uses parameterized Dapper queries — no dynamic SQL.
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly string _connString;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(IConfiguration config, ILogger<NotificationRepository> logger)
    {
        _connString = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing 'RP_Owner' connection string.");
        _logger = logger;
    }

    // ── Outbox ──────────────────────────────────────────────────────────────

    public async Task<NotifTemplate?> GetTemplateAsync(string eventCode, NotifChannel channel, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EVENT_CODE AS EventCode, CHANNEL AS Channel,
                   SUBJECT, BODY, IS_ACTIVE AS IsActive
            FROM MD_NOTIF_TEMPLATE
            WHERE EVENT_CODE = :EventCode AND CHANNEL = :Channel AND IS_ACTIVE = 1
              AND ROWNUM = 1
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            return await conn.QueryFirstOrDefaultAsync<NotifTemplate>(
                new CommandDefinition(sql, new { EventCode = eventCode, Channel = (int)channel }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification template for EventCode={EventCode}, Channel={Channel}", eventCode, channel);
            throw;
        }
    }

    public async Task<long> EnqueueAsync(NotifOutboxItem item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO MD_NOTIF_OUTBOX
                (EVENT_CODE, CHANNEL, TO_ADDRESS, TO_EMPL_CODE, PAYLOAD_JSON, STATUS, ATTEMPTS, CREATED_AT)
            VALUES (:EventCode, :Channel, :ToAddress, :ToEmplCode, :PayloadJson, 0, 0, SYSTIMESTAMP)
            RETURNING ID INTO :NewId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.OpenAsync(ct);

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("EventCode",   item.EventCode));
            cmd.Parameters.Add(new OracleParameter("Channel",     (int)item.Channel));
            cmd.Parameters.Add(new OracleParameter("ToAddress",   item.ToAddress));
            cmd.Parameters.Add(new OracleParameter("ToEmplCode",  item.ToEmpCode));
            cmd.Parameters.Add(new OracleParameter("PayloadJson", (object?)item.PayloadJson ?? DBNull.Value));
            var outParam = new OracleParameter("NewId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);

            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt64(outParam.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueueing notification for EventCode={EventCode}, ToEmpCode={ToEmpCode}", item.EventCode, item.ToEmpCode);
            throw;
        }
    }

    public async Task<IReadOnlyList<NotifOutboxItem>> ListPendingAsync(int batchSize, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EVENT_CODE AS EventCode, CHANNEL AS Channel,
                   TO_ADDRESS AS ToAddress, TO_EMPL_CODE AS ToEmpCode,
                   PAYLOAD_JSON AS PayloadJson, STATUS AS Status,
                   ATTEMPTS, LAST_ERROR AS LastError, CREATED_AT AS CreatedAt, SENT_AT AS SentAt
            FROM MD_NOTIF_OUTBOX
            WHERE STATUS = 0 AND ATTEMPTS < 5
              AND ROWNUM <= :BatchSize
            ORDER BY CREATED_AT
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<NotifOutboxItem>(
                new CommandDefinition(sql, new { BatchSize = batchSize }, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pending notifications (batchSize={BatchSize})", batchSize);
            throw;
        }
    }

    public async Task MarkSentAsync(long id, DateTime sentAt, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE MD_NOTIF_OUTBOX
            SET STATUS = 1, SENT_AT = :SentAt
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { SentAt = sentAt, Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as sent for Id={NotifId}", id);
            throw;
        }
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE MD_NOTIF_OUTBOX
            SET STATUS = 2, ATTEMPTS = ATTEMPTS + 1, LAST_ERROR = :Error
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { Error = error, Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as failed for Id={NotifId}", id);
            throw;
        }
    }

    // ── Template CRUD ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<NotifTemplate>> ListTemplatesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EVENT_CODE AS EventCode, CHANNEL AS Channel,
                   SUBJECT, BODY, IS_ACTIVE AS IsActive
            FROM MD_NOTIF_TEMPLATE
            ORDER BY EVENT_CODE, CHANNEL
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<NotifTemplate>(new CommandDefinition(sql, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing notification templates");
            throw;
        }
    }

    public async Task<long> InsertTemplateAsync(NotifTemplate m, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO MD_NOTIF_TEMPLATE (EVENT_CODE, CHANNEL, SUBJECT, BODY, IS_ACTIVE)
            VALUES (:EventCode, :Channel, :Subject, :Body, :IsActive)
            RETURNING ID INTO :NewId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.OpenAsync(ct);

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("EventCode", m.EventCode));
            cmd.Parameters.Add(new OracleParameter("Channel",   (int)m.Channel));
            cmd.Parameters.Add(new OracleParameter("Subject",   (object?)m.Subject ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("Body",      m.Body));
            cmd.Parameters.Add(new OracleParameter("IsActive",  m.IsActive ? 1 : 0));
            var outParam = new OracleParameter("NewId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);

            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt64(outParam.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting notification template for EventCode={EventCode}", m.EventCode);
            throw;
        }
    }

    public async Task UpdateTemplateAsync(NotifTemplate m, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE MD_NOTIF_TEMPLATE
            SET EVENT_CODE = :EventCode, CHANNEL = :Channel,
                SUBJECT = :Subject, BODY = :Body
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { m.EventCode, Channel = (int)m.Channel, m.Subject, m.Body, m.Id },
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification template Id={TemplateId}", m.Id);
            throw;
        }
    }

    public async Task SetTemplateActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        const string sql = "UPDATE MD_NOTIF_TEMPLATE SET IS_ACTIVE = :IsActive WHERE ID = :Id";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { IsActive = isActive ? 1 : 0, Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting template active state for Id={TemplateId}, IsActive={IsActive}", id, isActive);
            throw;
        }
    }

    // ── In-app bell ──────────────────────────────────────────────────────────

    public async Task<long> CreateInAppAsync(InAppNotification n, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO MD_INAPP_NOTIF (EMPL_CODE, TITLE, BODY, HREF, IS_READ, CREATED_AT)
            VALUES (:EmplCode, :Title, :Body, :Href, 0, SYSTIMESTAMP)
            RETURNING ID INTO :NewId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.OpenAsync(ct);

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("EmplCode", n.EmpCode));
            cmd.Parameters.Add(new OracleParameter("Title",    n.Title));
            cmd.Parameters.Add(new OracleParameter("Body",     (object?)n.Body ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("Href",     (object?)n.Href ?? DBNull.Value));
            var outParam = new OracleParameter("NewId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);

            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt64(outParam.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating in-app notification for EmpCode={EmpCode}", n.EmpCode);
            throw;
        }
    }

    public async Task<IReadOnlyList<InAppNotification>> ListInAppAsync(string empCode, int take, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EMPL_CODE AS EmpCode, TITLE, BODY, HREF, IS_READ AS IsRead, CREATED_AT AS CreatedAt
            FROM MD_INAPP_NOTIF
            WHERE EMPL_CODE = :EmplCode
              AND ROWNUM <= :Take
            ORDER BY CREATED_AT DESC
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<InAppNotification>(
                new CommandDefinition(sql, new { EmplCode = empCode, Take = take }, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing in-app notifications for EmpCode={EmpCode}", empCode);
            throw;
        }
    }

    public async Task<int> UnreadCountAsync(string empCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1) FROM MD_INAPP_NOTIF
            WHERE EMPL_CODE = :EmplCode AND IS_READ = 0
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new { EmplCode = empCode }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching unread notification count for EmpCode={EmpCode}", empCode);
            throw;
        }
    }

    public async Task MarkInAppReadAsync(long id, CancellationToken ct = default)
    {
        const string sql = "UPDATE MD_INAPP_NOTIF SET IS_READ = 1 WHERE ID = :Id";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking in-app notification as read for Id={NotifId}", id);
            throw;
        }
    }

    public async Task MarkAllInAppReadAsync(string empCode, CancellationToken ct = default)
    {
        const string sql = "UPDATE MD_INAPP_NOTIF SET IS_READ = 1 WHERE EMPL_CODE = :EmplCode AND IS_READ = 0";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { EmplCode = empCode }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all in-app notifications as read for EmpCode={EmpCode}", empCode);
            throw;
        }
    }
}
