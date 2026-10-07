using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class NotificationRepo : INotificationRepo
{
    private readonly AdoUnitOfWork _uow;
    private readonly IDbConnectionFactory _factory;
    public NotificationRepo(AdoUnitOfWork uow, IDbConnectionFactory factory)
    {
        _uow = uow;
        _factory = factory;
    }

    public async Task<long> InsertAsync(NotificationItem n, CancellationToken ct = default)
    {
        // Kept on _uow: this insert runs INSIDE NotificationService.EnqueueAsync's
        // explicit _uow transaction (bell + outbox rows must commit atomically), so
        // it must share that connection/transaction. It is NOT on the login/nav hot
        // path — only fires on workflow/broadcast events.
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_NOTIFICATION(ID,USER_ID,TITLE,MESSAGE,CATEGORY,REDIRECT_URL,SOURCE,EVENT_CODE,REQUEST_ID)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_uid,:p_title,:p_msg,:p_cat,:p_url,:p_src,:p_evt,:p_reqid)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_uid", n.UserId);
        cmd.Parameters.AddIn("p_title", n.Title);
        cmd.Parameters.AddIn("p_msg", OracleDbType.Varchar2, n.Message);
        cmd.Parameters.AddIn("p_cat", OracleDbType.Varchar2, n.Category);
        cmd.Parameters.AddIn("p_url", OracleDbType.Varchar2, n.RedirectUrl);
        cmd.Parameters.AddIn("p_src", n.Source.ToString());
        cmd.Parameters.AddIn("p_evt", OracleDbType.Varchar2, n.EventCode);
        cmd.Parameters.AddIn("p_reqid", OracleDbType.Int64, n.RequestId);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<IReadOnlyList<NotificationItem>> ListForUserAsync(long userId, int take, bool unreadOnly, CancellationToken ct = default)
    {
        // Read from the notification bell / notifications page — no shared
        // transaction. Short-lived pooled connection so it doesn't pin the circuit
        // _uow connection.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        var filter = unreadOnly ? "AND READ_AT IS NULL" : "";
        using var cmd = conn.Cmd($@"
            SELECT * FROM (
                SELECT ID,USER_ID,TITLE,MESSAGE,CATEGORY,REDIRECT_URL,SOURCE,EVENT_CODE,REQUEST_ID,CREATED_AT,READ_AT
                FROM RP_NOTIFICATION WHERE USER_ID=:p_uid {filter}
                ORDER BY CREATED_AT DESC
            ) WHERE ROWNUM<=:p_take");
        cmd.Parameters.AddIn("p_uid", userId);
        cmd.Parameters.AddIn("p_take", take);
        return await cmd.QueryAsync(Map, ct);
    }

    public async Task<int> GetUnreadCountAsync(long userId, CancellationToken ct = default)
    {
        // HOT PATH: NotificationBell.OnInitializedAsync calls this for EVERY
        // authenticated user on every circuit. Pure read, no transaction — use a
        // short-lived pooled connection released immediately, instead of pinning the
        // circuit-scoped _uow connection for the whole SignalR session.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(
            "SELECT COUNT(*) FROM RP_NOTIFICATION WHERE USER_ID=:p_uid AND READ_AT IS NULL");
        cmd.Parameters.AddIn("p_uid", userId);
        return await cmd.ScalarAsync<int>(ct);
    }

    public async Task MarkReadAsync(long userId, long notificationId, DateTime readAt, CancellationToken ct = default)
    {
        // User-action autocommit UPDATE from the bell — not inside any transaction.
        // Short-lived pooled connection.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(
            "UPDATE RP_NOTIFICATION SET READ_AT=:p_at WHERE ID=:p_id AND USER_ID=:p_uid AND READ_AT IS NULL");
        cmd.Parameters.AddIn("p_at", readAt);
        cmd.Parameters.AddIn("p_id", notificationId);
        cmd.Parameters.AddIn("p_uid", userId);
        await cmd.ExecAsync(ct);
    }

    public async Task MarkAllReadAsync(long userId, DateTime readAt, CancellationToken ct = default)
    {
        // User-action autocommit UPDATE from the bell — not inside any transaction.
        // Short-lived pooled connection.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(
            "UPDATE RP_NOTIFICATION SET READ_AT=:p_at WHERE USER_ID=:p_uid AND READ_AT IS NULL");
        cmd.Parameters.AddIn("p_at", readAt);
        cmd.Parameters.AddIn("p_uid", userId);
        await cmd.ExecAsync(ct);
    }

    private static NotificationItem Map(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), UserId = Long(r, "USER_ID"),
        Title = Str(r, "TITLE"), Message = StrN(r, "MESSAGE"),
        Category = StrN(r, "CATEGORY"), RedirectUrl = StrN(r, "REDIRECT_URL"),
        Source = Enum.TryParse<NotificationSource>(StrN(r, "SOURCE"), out var s) ? s : NotificationSource.System,
        EventCode = StrN(r, "EVENT_CODE"), RequestId = LongN(r, "REQUEST_ID"),
        CreatedAt = Dt(r, "CREATED_AT"), ReadAt = DtN(r, "READ_AT")
    };
}
