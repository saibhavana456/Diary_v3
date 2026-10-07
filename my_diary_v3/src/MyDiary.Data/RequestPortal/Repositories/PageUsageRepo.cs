using MyDiary.Core.Services;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>Raw ADO.NET implementation backing the Adoption Dashboard (Logins /
/// Modules Usage / Menu Usage / Reports Generation / Insights tabs).
/// See IPageUsageRepo for the read/write contract this fulfils.</summary>
public sealed class PageUsageRepo : IPageUsageRepo
{
    private readonly AdoUnitOfWork _uow;
    private readonly IOrganisationsDbFactory _orgFactory;
    private readonly IDbConnectionFactory _factory;
    public PageUsageRepo(AdoUnitOfWork uow, IOrganisationsDbFactory orgFactory, IDbConnectionFactory factory)
    {
        _uow = uow;
        _orgFactory = orgFactory;
        _factory = factory;
    }

    /// <summary>Headcount for the given scope from VW_STAFF_USER_SUMMARY (the only
    /// org-wide staff source available). Bank-wide = total row count. For a
    /// specific Zone/Region/Branch, matches on UNIT_CODE = scopeId — this only
    /// resolves when that view's UNIT_CODE scheme happens to align with the SOL
    /// id used for login/visit tracking; returns null (shown as "—", never a
    /// misleading 0%) when nothing matches, rather than guessing.</summary>
    private async Task<int?> GetHeadcountAsync(UsageScopeLevel scopeLevel, string? scopeId, CancellationToken ct)
    {
        try
        {
            await using var conn = await _orgFactory.OpenAsync(ct);
            if (scopeLevel == UsageScopeLevel.Bank)
            {
                using var cmd = conn.Cmd("SELECT COUNT(*) FROM VW_STAFF_USER_SUMMARY");
                return await cmd.ScalarAsync<int>(ct);
            }
            if (string.IsNullOrWhiteSpace(scopeId)) return null;
            using var scoped = conn.Cmd("SELECT COUNT(*) FROM VW_STAFF_USER_SUMMARY WHERE UNIT_CODE = :p_scope");
            scoped.Parameters.AddIn("p_scope", scopeId);
            var count = await scoped.ScalarAsync<int>(ct);
            return count > 0 ? count : null;
        }
        catch
        {
            return null; // Organisations DB unreachable/unseeded locally — degrade to "—", never throw on the render path.
        }
    }

    public async Task InsertVisitAsync(PageVisit v, CancellationToken ct = default)
    {
        // Open a SHORT-LIVED pooled connection scoped to this single insert and
        // dispose it immediately, rather than using the circuit-scoped AdoUnitOfWork
        // connection. This insert is fired once per navigation from MainLayout on
        // every Blazor Server circuit; the circuit's DI scope (and thus its
        // AdoUnitOfWork connection) lives for the entire SignalR session — often
        // hours — so binding this write to it pins one Oracle session per active
        // user until they disconnect, exhausting the server's session pool under
        // load (the ~750 stuck RP_PAGE_VISIT_LOG/RP_LOGIN_LOG connections observed).
        // `await using` returns the connection to the ODP.NET pool as soon as the
        // insert completes.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(@"
            INSERT INTO RP_PAGE_VISIT_LOG (ID, MENU_CODE, ROUTE, EMP_CODE, VISITED_AT_UTC, BRANCH_SOL_ID, REGION_SOL_ID, ZONE_SOL_ID, BRANCH_NAME, REGION_NAME, ZONE_NAME)
            VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_menu, :p_route, :p_emp, :p_at, :p_branch, :p_region, :p_zone, :p_branch_name, :p_region_name, :p_zone_name)");
        cmd.Parameters.AddIn("p_menu", v.MenuCode);
        cmd.Parameters.AddIn("p_route", v.Route);
        cmd.Parameters.AddIn("p_emp", v.EmpCode);
        cmd.Parameters.AddIn("p_at", v.VisitedAtUtc);
        cmd.Parameters.AddIn("p_branch", OracleDbType.Varchar2, v.BranchSolId);
        cmd.Parameters.AddIn("p_region", OracleDbType.Varchar2, v.RegionSolId);
        cmd.Parameters.AddIn("p_zone", OracleDbType.Varchar2, v.ZoneSolId);
        cmd.Parameters.AddIn("p_branch_name", OracleDbType.Varchar2, v.BranchName);
        cmd.Parameters.AddIn("p_region_name", OracleDbType.Varchar2, v.RegionName);
        cmd.Parameters.AddIn("p_zone_name", OracleDbType.Varchar2, v.ZoneName);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<PageVisitTrendPoint>> GetTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        string? menuCode, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var truncFmt = granularity switch
        {
            UsageTrendGranularity.Monthly   => "MM",
            UsageTrendGranularity.Quarterly => "Q",
            _                                => "DD"
        };

        var where = "WHERE a.VISIT_DATE >= :p_from AND a.VISIT_DATE <= :p_to";
        if (!string.IsNullOrWhiteSpace(menuCode)) where += " AND a.MENU_CODE = :p_menu";
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId))
        {
            where += filterLevel switch
            {
                UsageScopeLevel.Zone   => " AND a.ZONE_SOL_ID = :p_scope",
                UsageScopeLevel.Region => " AND a.REGION_SOL_ID = :p_scope",
                UsageScopeLevel.Branch => " AND a.BRANCH_SOL_ID = :p_scope",
                _                       => ""
            };
        }

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT TRUNC(a.VISIT_DATE,'{truncFmt}') AS BUCKET, a.MENU_CODE, MAX(m.LABEL) AS MENU_LABEL,
                   SUM(a.VISIT_COUNT) AS VISIT_COUNT, SUM(a.UNIQUE_USERS) AS UNIQUE_USERS
            FROM RP_PAGE_VISIT_DAILY_AGG a
            LEFT JOIN RP_RBAC_MENU m ON m.CODE = a.MENU_CODE
            {where}
            GROUP BY TRUNC(a.VISIT_DATE,'{truncFmt}'), a.MENU_CODE
            ORDER BY BUCKET, a.MENU_CODE", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (!string.IsNullOrWhiteSpace(menuCode)) cmd.Parameters.AddIn("p_menu", menuCode);
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId)) cmd.Parameters.AddIn("p_scope", filterScopeId);

        return await cmd.QueryAsync(r => new PageVisitTrendPoint(
            Dt(r, "BUCKET"), Str(r, "MENU_CODE"), StrN(r, "MENU_LABEL") ?? Str(r, "MENU_CODE"),
            Long(r, "VISIT_COUNT"), Long(r, "UNIQUE_USERS")), ct);
    }

    public async Task<IReadOnlyList<PageUsageDrilldownRow>> GetDrilldownAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel drillLevel, string? parentScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        if (drillLevel == UsageScopeLevel.Bank)
        {
            using var bankCmd = _uow.OracleConn.Cmd(@"
                SELECT 'BANK' AS SCOPE_ID, SUM(VISIT_COUNT) AS VISIT_COUNT, SUM(UNIQUE_USERS) AS UNIQUE_USERS
                FROM RP_PAGE_VISIT_DAILY_AGG WHERE VISIT_DATE >= :p_from AND VISIT_DATE <= :p_to", _uow.OracleTx);
            bankCmd.Parameters.AddIn("p_from", fromDateUtc.Date);
            bankCmd.Parameters.AddIn("p_to", toDateUtc.Date);
            return await bankCmd.QueryAsync(r => new PageUsageDrilldownRow(
                Str(r, "SCOPE_ID"), Long(r, "VISIT_COUNT"), Long(r, "UNIQUE_USERS")), ct);
        }

        var (groupCol, parentCol) = drillLevel switch
        {
            UsageScopeLevel.Zone   => ("ZONE_SOL_ID", (string?)null),
            UsageScopeLevel.Region => ("REGION_SOL_ID", "ZONE_SOL_ID"),
            UsageScopeLevel.Branch => ("BRANCH_SOL_ID", "REGION_SOL_ID"),
            _ => throw new ArgumentOutOfRangeException(nameof(drillLevel))
        };
        var nameCol = ScopeNameColumn(drillLevel);

        var where = $"WHERE VISIT_DATE >= :p_from AND VISIT_DATE <= :p_to AND {groupCol} != ' '";
        if (parentCol is not null && !string.IsNullOrWhiteSpace(parentScopeId))
            where += $" AND {parentCol} = :p_parent";

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT {groupCol} AS SCOPE_ID, MAX({nameCol}) AS SCOPE_NAME, SUM(VISIT_COUNT) AS VISIT_COUNT, SUM(UNIQUE_USERS) AS UNIQUE_USERS
            FROM RP_PAGE_VISIT_DAILY_AGG
            {where}
            GROUP BY {groupCol}
            ORDER BY VISIT_COUNT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (parentCol is not null && !string.IsNullOrWhiteSpace(parentScopeId)) cmd.Parameters.AddIn("p_parent", parentScopeId);

        return await cmd.QueryAsync(r => new PageUsageDrilldownRow(
            Str(r, "SCOPE_ID"), Long(r, "VISIT_COUNT"), Long(r, "UNIQUE_USERS"), StrN(r, "SCOPE_NAME")), ct);
    }

    public async Task<IReadOnlyList<UnderusedPageInsight>> GetUnderusedPagesAsync(
        UsageScopeLevel scopeLevel, string? scopeId, int take = 10, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var scopeLevelStr = scopeLevel.ToString().ToUpperInvariant();
        var where = "WHERE SCOPE_LEVEL = :p_level";
        if (scopeLevel != UsageScopeLevel.Bank && !string.IsNullOrWhiteSpace(scopeId))
            where += " AND SCOPE_ID = :p_scope";
        else
            where += " AND SCOPE_ID = ' '";

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT MENU_CODE, MENU_LABEL, VISIT_COUNT_30D, RANK_ASC, COMPUTED_AT_UTC
            FROM RP_PAGE_USAGE_INSIGHT
            {where}
            ORDER BY RANK_ASC
            FETCH FIRST :p_take ROWS ONLY", _uow.OracleTx);
        cmd.Parameters.AddIn("p_level", scopeLevelStr);
        if (scopeLevel != UsageScopeLevel.Bank && !string.IsNullOrWhiteSpace(scopeId)) cmd.Parameters.AddIn("p_scope", scopeId);
        cmd.Parameters.AddIn("p_take", take);

        return await cmd.QueryAsync(r => new UnderusedPageInsight(
            Str(r, "MENU_CODE"), StrN(r, "MENU_LABEL"), Long(r, "VISIT_COUNT_30D"), Int(r, "RANK_ASC"), Dt(r, "COMPUTED_AT_UTC")), ct);
    }

    public async Task RunNightlyAggregationAsync(DateTime forDateUtc, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var dayStart = forDateUtc.Date;
        var dayEnd = dayStart.AddDays(1);

        // Explicit transaction: this job runs as a Hangfire background job, not
        // inside a Blazor circuit — relying on ODP.NET's per-statement autocommit
        // (fine for the rest of this app's request-scoped repos) turned out NOT
        // to persist here, most likely because Hangfire's own job-processing
        // machinery runs inside an ambient System.Transactions scope that a
        // freshly-opened OracleConnection auto-enlists into (ODP.NET's default),
        // and that ambient scope was never completed for our statements. An
        // explicit BeginAsync/CommitAsync sidesteps the question entirely — the
        // MERGE + 4 INSERTs land in one transaction we control and definitely
        // commit, which is the right shape for this batch anyway.
        await _uow.BeginAsync(ct);
        try
        {
            await RunNightlyAggregationCoreAsync(dayStart, dayEnd, ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    private async Task RunNightlyAggregationCoreAsync(DateTime dayStart, DateTime dayEnd, CancellationToken ct)
    {
        // 1) Roll the day's raw visits into the daily aggregate (idempotent — MERGE).
        using (var mergeCmd = _uow.OracleConn.Cmd(@"
            MERGE INTO RP_PAGE_VISIT_DAILY_AGG t
            USING (
                SELECT TRUNC(VISITED_AT_UTC) AS VISIT_DATE, MENU_CODE,
                       NVL(BRANCH_SOL_ID,' ') AS BRANCH_SOL_ID, NVL(REGION_SOL_ID,' ') AS REGION_SOL_ID, NVL(ZONE_SOL_ID,' ') AS ZONE_SOL_ID,
                       MAX(BRANCH_NAME) AS BRANCH_NAME, MAX(REGION_NAME) AS REGION_NAME, MAX(ZONE_NAME) AS ZONE_NAME,
                       COUNT(*) AS VISIT_COUNT, COUNT(DISTINCT EMP_CODE) AS UNIQUE_USERS
                FROM RP_PAGE_VISIT_LOG
                WHERE VISITED_AT_UTC >= :p_from AND VISITED_AT_UTC < :p_to
                GROUP BY TRUNC(VISITED_AT_UTC), MENU_CODE, NVL(BRANCH_SOL_ID,' '), NVL(REGION_SOL_ID,' '), NVL(ZONE_SOL_ID,' ')
            ) s
            ON (t.VISIT_DATE = s.VISIT_DATE AND t.MENU_CODE = s.MENU_CODE
                AND t.BRANCH_SOL_ID = s.BRANCH_SOL_ID AND t.REGION_SOL_ID = s.REGION_SOL_ID AND t.ZONE_SOL_ID = s.ZONE_SOL_ID)
            WHEN MATCHED THEN UPDATE SET VISIT_COUNT = s.VISIT_COUNT, UNIQUE_USERS = s.UNIQUE_USERS,
                                         BRANCH_NAME = s.BRANCH_NAME, REGION_NAME = s.REGION_NAME, ZONE_NAME = s.ZONE_NAME
            WHEN NOT MATCHED THEN INSERT (ID, VISIT_DATE, MENU_CODE, BRANCH_SOL_ID, REGION_SOL_ID, ZONE_SOL_ID, BRANCH_NAME, REGION_NAME, ZONE_NAME, VISIT_COUNT, UNIQUE_USERS)
            VALUES (RP_SEQ_GLOBAL.NEXTVAL, s.VISIT_DATE, s.MENU_CODE, s.BRANCH_SOL_ID, s.REGION_SOL_ID, s.ZONE_SOL_ID, s.BRANCH_NAME, s.REGION_NAME, s.ZONE_NAME, s.VISIT_COUNT, s.UNIQUE_USERS)",
            _uow.OracleTx))
        {
            mergeCmd.Parameters.AddIn("p_from", dayStart);
            mergeCmd.Parameters.AddIn("p_to", dayEnd);
            await mergeCmd.ExecAsync(ct);
        }

        // 2) Recompute the underused-page ranking snapshot (fresh — this table
        //    holds only the latest snapshot, not history).
        using (var delCmd = _uow.OracleConn.Cmd("DELETE FROM RP_PAGE_USAGE_INSIGHT", _uow.OracleTx))
            await delCmd.ExecAsync(ct);

        var since30d = dayEnd.AddDays(-30);

        // BANK — every active menu, zero-visit ones included via LEFT JOIN.
        using (var bankCmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_PAGE_USAGE_INSIGHT (ID, SCOPE_LEVEL, SCOPE_ID, MENU_CODE, MENU_LABEL, VISIT_COUNT_30D, RANK_ASC)
            SELECT RP_SEQ_GLOBAL.NEXTVAL, 'BANK', ' ', x.CODE, x.LABEL, x.CNT, x.RNK FROM (
                SELECT m.CODE, m.LABEL, NVL(a.CNT,0) AS CNT,
                       ROW_NUMBER() OVER (ORDER BY NVL(a.CNT,0) ASC) AS RNK
                FROM RP_RBAC_MENU m
                LEFT JOIN (
                    SELECT MENU_CODE, SUM(VISIT_COUNT) AS CNT FROM RP_PAGE_VISIT_DAILY_AGG
                    WHERE VISIT_DATE >= :p_since GROUP BY MENU_CODE
                ) a ON a.MENU_CODE = m.CODE
                WHERE m.IS_ACTIVE = 1 AND m.IS_ENABLED = 1
            ) x WHERE x.RNK <= 15", _uow.OracleTx))
        {
            bankCmd.Parameters.AddIn("p_since", since30d);
            await bankCmd.ExecAsync(ct);
        }

        // ZONE / REGION / BRANCH — same shape, one CROSS JOIN + window function
        // per level, only against org units with any activity in the last 30 days
        // (otherwise every branch bank-wide would get a full menu list nightly).
        foreach (var (level, col) in new[] { ("ZONE", "ZONE_SOL_ID"), ("REGION", "REGION_SOL_ID"), ("BRANCH", "BRANCH_SOL_ID") })
        {
            using var cmd = _uow.OracleConn.Cmd($@"
                INSERT INTO RP_PAGE_USAGE_INSIGHT (ID, SCOPE_LEVEL, SCOPE_ID, MENU_CODE, MENU_LABEL, VISIT_COUNT_30D, RANK_ASC)
                SELECT RP_SEQ_GLOBAL.NEXTVAL, :p_level, x.SCOPE_ID, x.CODE, x.LABEL, x.CNT, x.RNK FROM (
                    SELECT z.{col} AS SCOPE_ID, m.CODE, m.LABEL, NVL(a.CNT,0) AS CNT,
                           ROW_NUMBER() OVER (PARTITION BY z.{col} ORDER BY NVL(a.CNT,0) ASC) AS RNK
                    FROM (SELECT DISTINCT {col} FROM RP_PAGE_VISIT_DAILY_AGG WHERE VISIT_DATE >= :p_since AND {col} != ' ') z
                    CROSS JOIN (SELECT CODE, LABEL FROM RP_RBAC_MENU WHERE IS_ACTIVE = 1 AND IS_ENABLED = 1) m
                    LEFT JOIN (
                        SELECT {col}, MENU_CODE, SUM(VISIT_COUNT) AS CNT FROM RP_PAGE_VISIT_DAILY_AGG
                        WHERE VISIT_DATE >= :p_since GROUP BY {col}, MENU_CODE
                    ) a ON a.{col} = z.{col} AND a.MENU_CODE = m.CODE
                ) x WHERE x.RNK <= 15", _uow.OracleTx);
            cmd.Parameters.AddIn("p_level", level);
            cmd.Parameters.AddIn("p_since", since30d);
            await cmd.ExecAsync(ct);
        }
    }

    private static string ScopeColumn(UsageScopeLevel level) => level switch
    {
        UsageScopeLevel.Zone   => "ZONE_SOL_ID",
        UsageScopeLevel.Region => "REGION_SOL_ID",
        UsageScopeLevel.Branch => "BRANCH_SOL_ID",
        _                       => throw new ArgumentOutOfRangeException(nameof(level))
    };

    // The denormalized name column matching a scope level (populated at write
    // time from session claims — see InsertLoginAsync / InsertVisitAsync).
    private static string ScopeNameColumn(UsageScopeLevel level) => level switch
    {
        UsageScopeLevel.Zone   => "ZONE_NAME",
        UsageScopeLevel.Region => "REGION_NAME",
        UsageScopeLevel.Branch => "BRANCH_NAME",
        _                       => throw new ArgumentOutOfRangeException(nameof(level))
    };

    // ── Logins ────────────────────────────────────────────────────────────────

    public async Task InsertLoginAsync(LoginEvent v, CancellationToken ct = default)
    {
        // Short-lived pooled connection per login insert — see InsertVisitAsync for
        // the full rationale. This is the RP_LOGIN_LOG write fired once per circuit
        // from MainLayout; using the circuit-scoped AdoUnitOfWork connection here is
        // what pinned one Oracle session per logged-in user for the life of their
        // SignalR circuit and exhausted the DB session pool. `await using` frees the
        // connection back to the pool the moment the insert finishes.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(@"
            INSERT INTO RP_LOGIN_LOG (ID, EMP_CODE, LOGIN_AT_UTC, LOGIN_METHOD, BRANCH_SOL_ID, REGION_SOL_ID, ZONE_SOL_ID, BRANCH_NAME, REGION_NAME, ZONE_NAME)
            VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_emp, :p_at, :p_method, :p_branch, :p_region, :p_zone, :p_branch_name, :p_region_name, :p_zone_name)");
        cmd.Parameters.AddIn("p_emp", v.EmpCode);
        cmd.Parameters.AddIn("p_at", v.LoginAtUtc);
        cmd.Parameters.AddIn("p_method", v.LoginMethod);
        cmd.Parameters.AddIn("p_branch", OracleDbType.Varchar2, v.BranchSolId);
        cmd.Parameters.AddIn("p_region", OracleDbType.Varchar2, v.RegionSolId);
        cmd.Parameters.AddIn("p_zone", OracleDbType.Varchar2, v.ZoneSolId);
        cmd.Parameters.AddIn("p_branch_name", OracleDbType.Varchar2, v.BranchName);
        cmd.Parameters.AddIn("p_region_name", OracleDbType.Varchar2, v.RegionName);
        cmd.Parameters.AddIn("p_zone_name", OracleDbType.Varchar2, v.ZoneName);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<LoginTrendPoint>> GetLoginTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var truncFmt = granularity switch { UsageTrendGranularity.Monthly => "MM", UsageTrendGranularity.Quarterly => "Q", _ => "DD" };
        var where = "WHERE VISIT_DATE >= :p_from AND VISIT_DATE <= :p_to";
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId))
            where += $" AND {ScopeColumn(filterLevel.Value)} = :p_scope";

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT TRUNC(VISIT_DATE,'{truncFmt}') AS BUCKET, SUM(LOGIN_COUNT) AS LOGIN_COUNT, SUM(UNIQUE_USERS) AS UNIQUE_USERS
            FROM RP_LOGIN_DAILY_AGG
            {where}
            GROUP BY TRUNC(VISIT_DATE,'{truncFmt}')
            ORDER BY BUCKET", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId)) cmd.Parameters.AddIn("p_scope", filterScopeId);

        return await cmd.QueryAsync(r => new LoginTrendPoint(Dt(r, "BUCKET"), Long(r, "LOGIN_COUNT"), Long(r, "UNIQUE_USERS")), ct);
    }

    public async Task<IReadOnlyList<AdoptionRow>> GetAdoptionDrilldownAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel drillLevel, string? parentScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var col = ScopeColumn(drillLevel);
        var nameCol = ScopeNameColumn(drillLevel);
        var parentCol = drillLevel switch { UsageScopeLevel.Region => "ZONE_SOL_ID", UsageScopeLevel.Branch => "REGION_SOL_ID", _ => null };
        var where = $"WHERE VISIT_DATE >= :p_from AND VISIT_DATE <= :p_to AND {col} != ' '";
        if (parentCol is not null && !string.IsNullOrWhiteSpace(parentScopeId)) where += $" AND {parentCol} = :p_parent";

        // SCOPE_NAME is read straight from the aggregate (denormalized at write
        // time), so no separate branch-master lookup is needed.
        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT {col} AS SCOPE_ID, MAX({nameCol}) AS SCOPE_NAME, SUM(LOGIN_COUNT) AS LOGIN_COUNT, SUM(UNIQUE_USERS) AS UNIQUE_USERS
            FROM RP_LOGIN_DAILY_AGG
            {where}
            GROUP BY {col}
            ORDER BY LOGIN_COUNT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (parentCol is not null && !string.IsNullOrWhiteSpace(parentScopeId)) cmd.Parameters.AddIn("p_parent", parentScopeId);

        var rows = await cmd.QueryAsync(r => (
            ScopeId: Str(r, "SCOPE_ID"),
            ScopeName: StrN(r, "SCOPE_NAME"),
            LoginCount: Long(r, "LOGIN_COUNT"),
            UniqueUsers: Long(r, "UNIQUE_USERS")), ct);

        var result = new List<AdoptionRow>();
        foreach (var row in rows)
        {
            var headcount = await GetHeadcountAsync(drillLevel, row.ScopeId, ct);
            result.Add(new AdoptionRow(row.ScopeId, row.UniqueUsers, row.LoginCount, headcount, row.ScopeName));
        }
        return result;
    }

    public async Task<(long Dau, long Wau, long Mau, int? Headcount)> GetLoginSummaryAsync(
        UsageScopeLevel scopeLevel, string? scopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var today = AppTime.Now.Date;
        var tomorrow = today.AddDays(1);

        // RP_LOGIN_DAILY_AGG is only ever populated for "yesterday" by the 2 AM nightly job
        // (PageUsageAggregationJob.RunAsync) — today's row never exists there until tomorrow night, by
        // which point "today" has moved on again. Reading DAU/WAU/MAU from that table alone means DAU is
        // structurally always 0, and WAU/MAU permanently undercount by exactly one day. Fixed by summing
        // the aggregated days UP TO YESTERDAY plus a LIVE count straight from RP_LOGIN_LOG for today —
        // once tonight's run aggregates today into RP_LOGIN_DAILY_AGG, that day naturally drops out of
        // this live query and picks up in the aggregated sum instead, so there's no double-counting.
        async Task<long> AggregatedUsersSinceAsync(DateTime since)
        {
            var w = "WHERE VISIT_DATE >= :p_since AND VISIT_DATE < :p_today";
            if (scopeLevel != UsageScopeLevel.Bank && !string.IsNullOrWhiteSpace(scopeId))
                w += $" AND {ScopeColumn(scopeLevel)} = :p_scope";
            using var cmd = _uow.OracleConn.Cmd($"SELECT NVL(SUM(UNIQUE_USERS),0) FROM RP_LOGIN_DAILY_AGG {w}", _uow.OracleTx);
            cmd.Parameters.AddIn("p_since", since);
            cmd.Parameters.AddIn("p_today", today);
            if (scopeLevel != UsageScopeLevel.Bank && !string.IsNullOrWhiteSpace(scopeId)) cmd.Parameters.AddIn("p_scope", scopeId);
            return await cmd.ScalarAsync<long>(ct);
        }

        async Task<long> LiveUsersTodayAsync()
        {
            var w = "WHERE LOGIN_AT_UTC >= :p_today AND LOGIN_AT_UTC < :p_tomorrow";
            if (scopeLevel != UsageScopeLevel.Bank && !string.IsNullOrWhiteSpace(scopeId))
                w += $" AND {ScopeColumn(scopeLevel)} = :p_scope";
            using var cmd = _uow.OracleConn.Cmd($"SELECT COUNT(DISTINCT EMP_CODE) FROM RP_LOGIN_LOG {w}", _uow.OracleTx);
            cmd.Parameters.AddIn("p_today", today);
            cmd.Parameters.AddIn("p_tomorrow", tomorrow);
            if (scopeLevel != UsageScopeLevel.Bank && !string.IsNullOrWhiteSpace(scopeId)) cmd.Parameters.AddIn("p_scope", scopeId);
            return await cmd.ScalarAsync<long>(ct);
        }

        var liveToday = await LiveUsersTodayAsync();
        var dau = liveToday;
        var wau = await AggregatedUsersSinceAsync(today.AddDays(-6)) + liveToday;
        var mau = await AggregatedUsersSinceAsync(today.AddDays(-29)) + liveToday;
        var headcount = await GetHeadcountAsync(scopeLevel, scopeId, ct);
        return (dau, wau, mau, headcount);
    }

    // ── Modules Usage ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ModuleUsageRow>> GetModuleUsageAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var where = "WHERE a.VISIT_DATE >= :p_from AND a.VISIT_DATE <= :p_to";
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId))
            where += $" AND a.{ScopeColumn(filterLevel.Value)} = :p_scope";

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT mod.CODE AS MODULE_CODE, mod.NAME AS MODULE_LABEL,
                   SUM(a.VISIT_COUNT) AS VISIT_COUNT, SUM(a.UNIQUE_USERS) AS UNIQUE_USERS
            FROM RP_PAGE_VISIT_DAILY_AGG a
            JOIN RP_RBAC_MENU m ON m.CODE = a.MENU_CODE
            JOIN RP_RBAC_MODULE mod ON mod.ID = m.MODULE_ID
            {where}
            GROUP BY mod.CODE, mod.NAME
            ORDER BY VISIT_COUNT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId)) cmd.Parameters.AddIn("p_scope", filterScopeId);

        return await cmd.QueryAsync(r => new ModuleUsageRow(
            Str(r, "MODULE_CODE"), Str(r, "MODULE_LABEL"), Long(r, "VISIT_COUNT"), Long(r, "UNIQUE_USERS")), ct);
    }

    public async Task<IReadOnlyList<ModuleTrendPoint>> GetModuleTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var truncFmt = granularity switch { UsageTrendGranularity.Monthly => "MM", UsageTrendGranularity.Quarterly => "Q", _ => "DD" };
        var where = "WHERE a.VISIT_DATE >= :p_from AND a.VISIT_DATE <= :p_to";
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId))
            where += $" AND a.{ScopeColumn(filterLevel.Value)} = :p_scope";

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT TRUNC(a.VISIT_DATE,'{truncFmt}') AS BUCKET, mod.CODE AS MODULE_CODE, MAX(mod.NAME) AS MODULE_LABEL, SUM(a.VISIT_COUNT) AS VISIT_COUNT
            FROM RP_PAGE_VISIT_DAILY_AGG a
            JOIN RP_RBAC_MENU m ON m.CODE = a.MENU_CODE
            JOIN RP_RBAC_MODULE mod ON mod.ID = m.MODULE_ID
            {where}
            GROUP BY TRUNC(a.VISIT_DATE,'{truncFmt}'), mod.CODE
            ORDER BY BUCKET", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId)) cmd.Parameters.AddIn("p_scope", filterScopeId);

        return await cmd.QueryAsync(r => new ModuleTrendPoint(
            Dt(r, "BUCKET"), Str(r, "MODULE_CODE"), Str(r, "MODULE_LABEL"), Long(r, "VISIT_COUNT")), ct);
    }

    // ── Reports Generation ────────────────────────────────────────────────────

    public async Task InsertReportGenerationAsync(ReportGenerationEvent v, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_REPORT_GENERATION_LOG (ID, REPORT_KEY, REPORT_LABEL, EMP_CODE, GENERATED_AT_UTC, BRANCH_SOL_ID, REGION_SOL_ID, ZONE_SOL_ID)
            VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_key, :p_label, :p_emp, :p_at, :p_branch, :p_region, :p_zone)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_key", v.ReportKey);
        cmd.Parameters.AddIn("p_label", OracleDbType.Varchar2, v.ReportLabel);
        cmd.Parameters.AddIn("p_emp", v.EmpCode);
        cmd.Parameters.AddIn("p_at", v.GeneratedAtUtc);
        cmd.Parameters.AddIn("p_branch", OracleDbType.Varchar2, v.BranchSolId);
        cmd.Parameters.AddIn("p_region", OracleDbType.Varchar2, v.RegionSolId);
        cmd.Parameters.AddIn("p_zone", OracleDbType.Varchar2, v.ZoneSolId);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<ReportGenerationRow>> GetReportGenerationSummaryAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var where = "WHERE VISIT_DATE >= :p_from AND VISIT_DATE <= :p_to";
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId))
            where += $" AND {ScopeColumn(filterLevel.Value)} = :p_scope";

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT REPORT_KEY, SUM(GEN_COUNT) AS GEN_COUNT, SUM(UNIQUE_USERS) AS UNIQUE_USERS
            FROM RP_REPORT_GENERATION_DAILY_AGG
            {where}
            GROUP BY REPORT_KEY
            ORDER BY GEN_COUNT DESC", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId)) cmd.Parameters.AddIn("p_scope", filterScopeId);

        return await cmd.QueryAsync(r => new ReportGenerationRow(
            Str(r, "REPORT_KEY"), null, Long(r, "GEN_COUNT"), Long(r, "UNIQUE_USERS")), ct);
    }

    public async Task<IReadOnlyList<ReportGenerationTrendPoint>> GetReportGenerationTrendAsync(
        DateTime fromDateUtc, DateTime toDateUtc, UsageTrendGranularity granularity,
        string? reportKey, UsageScopeLevel? filterLevel, string? filterScopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var truncFmt = granularity switch { UsageTrendGranularity.Monthly => "MM", UsageTrendGranularity.Quarterly => "Q", _ => "DD" };
        var where = "WHERE VISIT_DATE >= :p_from AND VISIT_DATE <= :p_to";
        if (!string.IsNullOrWhiteSpace(reportKey)) where += " AND REPORT_KEY = :p_key";
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId))
            where += $" AND {ScopeColumn(filterLevel.Value)} = :p_scope";

        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT TRUNC(VISIT_DATE,'{truncFmt}') AS BUCKET, SUM(GEN_COUNT) AS GEN_COUNT
            FROM RP_REPORT_GENERATION_DAILY_AGG
            {where}
            GROUP BY TRUNC(VISIT_DATE,'{truncFmt}')
            ORDER BY BUCKET", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", fromDateUtc.Date);
        cmd.Parameters.AddIn("p_to", toDateUtc.Date);
        if (!string.IsNullOrWhiteSpace(reportKey)) cmd.Parameters.AddIn("p_key", reportKey);
        if (filterLevel is not null && !string.IsNullOrWhiteSpace(filterScopeId)) cmd.Parameters.AddIn("p_scope", filterScopeId);

        return await cmd.QueryAsync(r => new ReportGenerationTrendPoint(Dt(r, "BUCKET"), Long(r, "GEN_COUNT")), ct);
    }

    // ── Insights (plain aggregation, no ML) ──────────────────────────────────

    public async Task<IReadOnlyList<AdoptionInsight>> GetInsightsAsync(UsageScopeLevel scopeLevel, string? scopeId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var insights = new List<AdoptionInsight>();
        var today = AppTime.Now.Date;

        // Least-used page bank/scope-wide (reuse the nightly snapshot, rank 1).
        var underused = await GetUnderusedPagesAsync(scopeLevel, scopeId, take: 1, ct: ct);
        if (underused.Count > 0)
            insights.Add(new AdoptionInsight("Least-used page", $"{(underused[0].MenuLabel ?? underused[0].MenuCode)} — {underused[0].VisitCount30D} visits in the last 30 days", "Warning"));

        // Zone with highest / lowest login volume this week (bank scope only —
        // a per-branch "lowest zone" doesn't make sense once already drilled in).
        if (scopeLevel == UsageScopeLevel.Bank)
        {
            var weekAgo = today.AddDays(-6);
            using var cmd = _uow.OracleConn.Cmd(@"
                SELECT ZONE_SOL_ID, SUM(UNIQUE_USERS) AS U FROM RP_LOGIN_DAILY_AGG
                WHERE VISIT_DATE >= :p_from AND VISIT_DATE <= :p_to AND ZONE_SOL_ID != ' '
                GROUP BY ZONE_SOL_ID ORDER BY U DESC", _uow.OracleTx);
            cmd.Parameters.AddIn("p_from", weekAgo);
            cmd.Parameters.AddIn("p_to", today);
            var zones = await cmd.QueryAsync(r => (Zone: Str(r, "ZONE_SOL_ID"), Users: Long(r, "U")), ct);
            if (zones.Count > 0)
            {
                insights.Add(new AdoptionInsight("Most active Zone (7d)", $"Zone {zones[0].Zone} — {zones[0].Users} unique users", "Success"));
                if (zones.Count > 1)
                    insights.Add(new AdoptionInsight("Least active Zone (7d)", $"Zone {zones[^1].Zone} — {zones[^1].Users} unique users", "Warning"));
            }
        }

        // Module with the biggest week-over-week visit change.
        {
            var thisWeekStart = today.AddDays(-6);
            var lastWeekStart = today.AddDays(-13);
            var lastWeekEnd = today.AddDays(-7);
            var scopeWhere = scopeLevel == UsageScopeLevel.Bank || string.IsNullOrWhiteSpace(scopeId)
                ? "" : $" AND a.{ScopeColumn(scopeLevel)} = :p_scope";

            using var cmd = _uow.OracleConn.Cmd($@"
                SELECT mod.NAME AS MODULE_LABEL,
                       SUM(CASE WHEN a.VISIT_DATE >= :p_thisStart THEN a.VISIT_COUNT ELSE 0 END) AS THIS_WEEK,
                       SUM(CASE WHEN a.VISIT_DATE >= :p_lastStart AND a.VISIT_DATE <= :p_lastEnd THEN a.VISIT_COUNT ELSE 0 END) AS LAST_WEEK
                FROM RP_PAGE_VISIT_DAILY_AGG a
                JOIN RP_RBAC_MENU m ON m.CODE = a.MENU_CODE
                JOIN RP_RBAC_MODULE mod ON mod.ID = m.MODULE_ID
                WHERE a.VISIT_DATE >= :p_lastStart AND a.VISIT_DATE <= :p_thisEnd{scopeWhere}
                GROUP BY mod.NAME
                ORDER BY (SUM(CASE WHEN a.VISIT_DATE >= :p_thisStart THEN a.VISIT_COUNT ELSE 0 END)
                          - SUM(CASE WHEN a.VISIT_DATE >= :p_lastStart AND a.VISIT_DATE <= :p_lastEnd THEN a.VISIT_COUNT ELSE 0 END)) DESC", _uow.OracleTx);
            cmd.Parameters.AddIn("p_thisStart", thisWeekStart);
            cmd.Parameters.AddIn("p_thisEnd", today);
            cmd.Parameters.AddIn("p_lastStart", lastWeekStart);
            cmd.Parameters.AddIn("p_lastEnd", lastWeekEnd);
            if (scopeWhere.Length > 0) cmd.Parameters.AddIn("p_scope", scopeId);

            var rows = await cmd.QueryAsync(r => (Module: Str(r, "MODULE_LABEL"), ThisWeek: Long(r, "THIS_WEEK"), LastWeek: Long(r, "LAST_WEEK")), ct);
            var grown = rows.Where(x => x.ThisWeek > x.LastWeek).OrderByDescending(x => x.ThisWeek - x.LastWeek).FirstOrDefault();
            if (grown.Module is not null)
                insights.Add(new AdoptionInsight("Fastest-growing module (week over week)", $"{grown.Module} — {grown.LastWeek} → {grown.ThisWeek} visits", "Success"));
        }

        return insights;
    }

    // ── Nightly aggregation (2 AM) — logins + report generations ─────────────

    public async Task RunLoginAggregationAsync(DateTime forDateUtc, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var dayStart = forDateUtc.Date;
        var dayEnd = dayStart.AddDays(1);
        await _uow.BeginAsync(ct);
        try
        {
            using (var cmd = _uow.OracleConn.Cmd(@"
                MERGE INTO RP_LOGIN_DAILY_AGG t
                USING (
                    SELECT TRUNC(LOGIN_AT_UTC) AS VISIT_DATE,
                           NVL(BRANCH_SOL_ID,' ') AS BRANCH_SOL_ID, NVL(REGION_SOL_ID,' ') AS REGION_SOL_ID, NVL(ZONE_SOL_ID,' ') AS ZONE_SOL_ID,
                           MAX(BRANCH_NAME) AS BRANCH_NAME, MAX(REGION_NAME) AS REGION_NAME, MAX(ZONE_NAME) AS ZONE_NAME,
                           COUNT(*) AS LOGIN_COUNT, COUNT(DISTINCT EMP_CODE) AS UNIQUE_USERS
                    FROM RP_LOGIN_LOG
                    WHERE LOGIN_AT_UTC >= :p_from AND LOGIN_AT_UTC < :p_to
                    GROUP BY TRUNC(LOGIN_AT_UTC), NVL(BRANCH_SOL_ID,' '), NVL(REGION_SOL_ID,' '), NVL(ZONE_SOL_ID,' ')
                ) s
                ON (t.VISIT_DATE = s.VISIT_DATE AND t.BRANCH_SOL_ID = s.BRANCH_SOL_ID AND t.REGION_SOL_ID = s.REGION_SOL_ID AND t.ZONE_SOL_ID = s.ZONE_SOL_ID)
                WHEN MATCHED THEN UPDATE SET LOGIN_COUNT = s.LOGIN_COUNT, UNIQUE_USERS = s.UNIQUE_USERS,
                                             BRANCH_NAME = s.BRANCH_NAME, REGION_NAME = s.REGION_NAME, ZONE_NAME = s.ZONE_NAME
                WHEN NOT MATCHED THEN INSERT (ID, VISIT_DATE, BRANCH_SOL_ID, REGION_SOL_ID, ZONE_SOL_ID, BRANCH_NAME, REGION_NAME, ZONE_NAME, LOGIN_COUNT, UNIQUE_USERS)
                VALUES (RP_SEQ_GLOBAL.NEXTVAL, s.VISIT_DATE, s.BRANCH_SOL_ID, s.REGION_SOL_ID, s.ZONE_SOL_ID, s.BRANCH_NAME, s.REGION_NAME, s.ZONE_NAME, s.LOGIN_COUNT, s.UNIQUE_USERS)", _uow.OracleTx))
            {
                cmd.Parameters.AddIn("p_from", dayStart);
                cmd.Parameters.AddIn("p_to", dayEnd);
                await cmd.ExecAsync(ct);
            }
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task RunReportGenerationAggregationAsync(DateTime forDateUtc, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var dayStart = forDateUtc.Date;
        var dayEnd = dayStart.AddDays(1);
        await _uow.BeginAsync(ct);
        try
        {
            using (var cmd = _uow.OracleConn.Cmd(@"
                MERGE INTO RP_REPORT_GENERATION_DAILY_AGG t
                USING (
                    SELECT TRUNC(GENERATED_AT_UTC) AS VISIT_DATE, REPORT_KEY,
                           NVL(BRANCH_SOL_ID,' ') AS BRANCH_SOL_ID, NVL(REGION_SOL_ID,' ') AS REGION_SOL_ID, NVL(ZONE_SOL_ID,' ') AS ZONE_SOL_ID,
                           COUNT(*) AS GEN_COUNT, COUNT(DISTINCT EMP_CODE) AS UNIQUE_USERS
                    FROM RP_REPORT_GENERATION_LOG
                    WHERE GENERATED_AT_UTC >= :p_from AND GENERATED_AT_UTC < :p_to
                    GROUP BY TRUNC(GENERATED_AT_UTC), REPORT_KEY, NVL(BRANCH_SOL_ID,' '), NVL(REGION_SOL_ID,' '), NVL(ZONE_SOL_ID,' ')
                ) s
                ON (t.VISIT_DATE = s.VISIT_DATE AND t.REPORT_KEY = s.REPORT_KEY
                    AND t.BRANCH_SOL_ID = s.BRANCH_SOL_ID AND t.REGION_SOL_ID = s.REGION_SOL_ID AND t.ZONE_SOL_ID = s.ZONE_SOL_ID)
                WHEN MATCHED THEN UPDATE SET GEN_COUNT = s.GEN_COUNT, UNIQUE_USERS = s.UNIQUE_USERS
                WHEN NOT MATCHED THEN INSERT (ID, VISIT_DATE, REPORT_KEY, BRANCH_SOL_ID, REGION_SOL_ID, ZONE_SOL_ID, GEN_COUNT, UNIQUE_USERS)
                VALUES (RP_SEQ_GLOBAL.NEXTVAL, s.VISIT_DATE, s.REPORT_KEY, s.BRANCH_SOL_ID, s.REGION_SOL_ID, s.ZONE_SOL_ID, s.GEN_COUNT, s.UNIQUE_USERS)", _uow.OracleTx))
            {
                cmd.Parameters.AddIn("p_from", dayStart);
                cmd.Parameters.AddIn("p_to", dayEnd);
                await cmd.ExecAsync(ct);
            }
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    // ── 9 AM digest ───────────────────────────────────────────────────────────

    public async Task<(long UniqueLogins, int? Headcount, string? TopModuleLabel, long TopModuleVisits)> GetDailyDigestSummaryAsync(
        DateTime forDateUtc, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var day = forDateUtc.Date;

        using var loginCmd = _uow.OracleConn.Cmd(
            "SELECT NVL(SUM(UNIQUE_USERS),0) FROM RP_LOGIN_DAILY_AGG WHERE VISIT_DATE = :p_day", _uow.OracleTx);
        loginCmd.Parameters.AddIn("p_day", day);
        var uniqueLogins = await loginCmd.ScalarAsync<long>(ct);

        var headcount = await GetHeadcountAsync(UsageScopeLevel.Bank, null, ct);

        using var moduleCmd = _uow.OracleConn.Cmd(@"
            SELECT mod.NAME AS MODULE_LABEL, SUM(a.VISIT_COUNT) AS VISIT_COUNT
            FROM RP_PAGE_VISIT_DAILY_AGG a
            JOIN RP_RBAC_MENU m ON m.CODE = a.MENU_CODE
            JOIN RP_RBAC_MODULE mod ON mod.ID = m.MODULE_ID
            WHERE a.VISIT_DATE = :p_day
            GROUP BY mod.NAME
            ORDER BY VISIT_COUNT DESC
            FETCH FIRST 1 ROW ONLY", _uow.OracleTx);
        moduleCmd.Parameters.AddIn("p_day", day);
        var topRows = await moduleCmd.QueryAsync(r => (Label: Str(r, "MODULE_LABEL"), Visits: Long(r, "VISIT_COUNT")), ct);
        var top = topRows.FirstOrDefault();

        return (uniqueLogins, headcount, top.Label, top.Visits);
    }
}
