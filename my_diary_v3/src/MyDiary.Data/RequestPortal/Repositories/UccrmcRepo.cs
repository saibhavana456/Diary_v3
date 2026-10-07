using Dapper;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;

namespace RequestPortal.Data.Repositories;

/// <summary>
/// Read-side Dapper repo for the UCCRMC alert integration: resolves the fixed
/// classification chain (seeded by RP_08) and reads alert-ticket status and
/// closure/pendency aggregates. Ticket creation goes through the standard
/// request repo/UoW inside <see cref="RequestPortal.Core.Services.UccrmcService"/>.
/// </summary>
public sealed class UccrmcRepo : IUccrmcRepo
{
    private readonly IDbConnectionFactory _factory;
    public UccrmcRepo(IDbConnectionFactory factory) => _factory = factory;

    // Status numeric codes (RequestStatus): Submitted=1, InProgress=2, Resolved=5, Closed=6, Cancelled=8
    private const string OpenStatuses     = "(1,2,3,4,7)";  // not resolved/closed/cancelled
    private const string ClosedStatuses   = "(6)";
    private const string ResolvedStatus   = "5";
    private const string CancelledStatus  = "8";

    public async Task<UccrmcClassification?> GetClassificationAsync(CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = @"
            SELECT
                NVL((SELECT ID FROM RP_M_REQUEST_TYPE WHERE CODE='UCCRMC_ALERT'),0) AS ""RequestTypeId"",
                NVL((SELECT ID FROM RP_M_UNIT         WHERE CODE='UCCRMC_UNIT'),0)  AS ""UnitId"",
                NVL((SELECT ID FROM RP_M_VERTICAL     WHERE CODE='UCCRMC_VERT'),0)  AS ""VerticalId"",
                NVL((SELECT ID FROM RP_M_DEPARTMENT   WHERE CODE='UCCRMC_DEPT'),0)  AS ""DepartmentId"",
                NVL((SELECT ID FROM RP_M_ACTIVITY     WHERE CODE='UCCRMC_ACT'),0)   AS ""ActivityId""
            FROM DUAL";
        var row = await c.QueryFirstOrDefaultAsync<ClsRow>(new CommandDefinition(sql, cancellationToken: ct));
        // If any id is missing the seed hasn't been run — treat as not configured.
        if (row is null || row.RequestTypeId == 0 || row.UnitId == 0 || row.VerticalId == 0
            || row.DepartmentId == 0 || row.ActivityId == 0)
            return null;
        return new UccrmcClassification(row.RequestTypeId, row.UnitId, row.VerticalId, row.DepartmentId, row.ActivityId);
    }

    public async Task<UccrmcAlertStatus?> GetByAlertIdAsync(string alertId, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = @"
            SELECT * FROM (
                SELECT
                    r.EXT_ALERT_ID                                   AS ""AlertId"",
                    r.ID                                             AS ""RequestId"",
                    r.REQ_NO                                         AS ""ReqNo"",
                    CASE r.STATUS
                        WHEN 1 THEN 'Submitted' WHEN 2 THEN 'InProgress'
                        WHEN 3 THEN 'ClarificationSought' WHEN 4 THEN 'ReturnedToEmployee'
                        WHEN 5 THEN 'Resolved' WHEN 6 THEN 'Closed'
                        WHEN 7 THEN 'Reopened' WHEN 8 THEN 'Cancelled' ELSE 'Unknown' END AS ""Status"",
                    r.CURRENT_LEVEL                                  AS ""CurrentLevel"",
                    r.RAISED_BY_EMP                                  AS ""RequesterEmpCode"",
                    (SELECT MIN(a.EMP_CODE) FROM RP_REQUEST_ASSIGNEE a
                       WHERE a.REQUEST_ID = r.ID AND a.IS_ACTIVE = 1)  AS ""AssigneeEmpCode"",
                    CAST(r.CREATED_AT AS TIMESTAMP)                  AS ""CreatedAt"",
                    CAST(r.CLOSED_AT  AS TIMESTAMP)                  AS ""ClosedAt"",
                    CAST(r.SLA_DUE_UTC AS TIMESTAMP)                 AS ""SlaDueUtc"",
                    CASE WHEN r.STATUS IN " + OpenStatuses + @" AND r.SLA_DUE_UTC IS NOT NULL
                              AND r.SLA_DUE_UTC < SYS_EXTRACT_UTC(SYSTIMESTAMP)
                         THEN 1 ELSE 0 END                          AS ""IsOverdue""
                FROM RP_REQUEST r
                WHERE r.EXT_SOURCE = 'UCCRMC' AND r.EXT_ALERT_ID = :alertId
                ORDER BY r.ID DESC
            ) WHERE ROWNUM = 1";
        var r = await c.QueryFirstOrDefaultAsync<StatusRow>(
            new CommandDefinition(sql, new { alertId }, cancellationToken: ct));
        if (r is null) return null;
        return new UccrmcAlertStatus(
            r.AlertId, r.RequestId, r.ReqNo, r.Status, r.CurrentLevel,
            r.RequesterEmpCode, r.AssigneeEmpCode, r.CreatedAt, r.ClosedAt, r.SlaDueUtc, r.IsOverdue == 1);
    }

    public async Task<UccrmcDashboard> GetDashboardAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var where = "r.EXT_SOURCE = 'UCCRMC'";
        if (fromUtc.HasValue) where += " AND r.CREATED_AT >= :fromUtc";
        if (toUtc.HasValue)   where += " AND r.CREATED_AT < :toUtc";

        var totalsSql = $@"
            SELECT
                COUNT(*)                                                   AS ""Total"",
                SUM(CASE WHEN r.STATUS IN {OpenStatuses} THEN 1 ELSE 0 END) AS ""Open"",
                SUM(CASE WHEN r.STATUS = {ResolvedStatus} THEN 1 ELSE 0 END) AS ""Resolved"",
                SUM(CASE WHEN r.STATUS IN {ClosedStatuses} THEN 1 ELSE 0 END) AS ""Closed"",
                SUM(CASE WHEN r.STATUS = {CancelledStatus} THEN 1 ELSE 0 END) AS ""Cancelled"",
                SUM(CASE WHEN r.STATUS IN {OpenStatuses} AND r.SLA_DUE_UTC IS NOT NULL
                          AND r.SLA_DUE_UTC < SYS_EXTRACT_UTC(SYSTIMESTAMP) THEN 1 ELSE 0 END) AS ""Overdue""
            FROM RP_REQUEST r WHERE {where}";

        var t = await c.QueryFirstAsync<Totals>(new CommandDefinition(totalsSql, new { fromUtc, toUtc }, cancellationToken: ct));

        int total = t.Total, open = t.Open, resolved = t.Resolved,
            closed = t.Closed, cancelled = t.Cancelled, overdue = t.Overdue;
        double closurePct = total > 0 ? Math.Round((double)closed / total * 100.0, 1) : 0.0;

        var dailySql = $@"
            SELECT TRUNC(r.CREATED_AT)                                        AS ""Day"",
                   COUNT(*)                                                   AS ""Raised"",
                   SUM(CASE WHEN r.STATUS IN {ClosedStatuses} THEN 1 ELSE 0 END) AS ""Closed""
            FROM RP_REQUEST r WHERE {where}
            GROUP BY TRUNC(r.CREATED_AT) ORDER BY TRUNC(r.CREATED_AT)";
        var daily = (await c.QueryAsync<DailyRow>(
            new CommandDefinition(dailySql, new { fromUtc, toUtc }, cancellationToken: ct)))
            .Select(d => new UccrmcDailyPoint(d.Day, d.Raised, d.Closed)).ToList();

        return new UccrmcDashboard(total, open, resolved, closed, cancelled, overdue, closurePct, fromUtc, toUtc, daily);
    }

    // Mutable row types — Dapper converts Oracle NUMBER (decimal) into these
    // property types; positional-record DTOs can't be materialised directly.
    private sealed class ClsRow
    {
        public long RequestTypeId { get; set; }
        public long UnitId { get; set; }
        public long VerticalId { get; set; }
        public long DepartmentId { get; set; }
        public long ActivityId { get; set; }
    }

    private sealed class StatusRow
    {
        public string AlertId { get; set; } = "";
        public long RequestId { get; set; }
        public string ReqNo { get; set; } = "";
        public string Status { get; set; } = "";
        public int CurrentLevel { get; set; }
        public string RequesterEmpCode { get; set; } = "";
        public string? AssigneeEmpCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? SlaDueUtc { get; set; }
        public int IsOverdue { get; set; }
    }

    private sealed class Totals
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int Resolved { get; set; }
        public int Closed { get; set; }
        public int Cancelled { get; set; }
        public int Overdue { get; set; }
    }

    private sealed class DailyRow
    {
        public DateTime Day { get; set; }
        public int Raised { get; set; }
        public int Closed { get; set; }
    }
}
