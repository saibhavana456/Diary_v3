using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class RbacRepo : IRbacRepo
{
    private readonly AdoUnitOfWork _uow;
    private readonly IDbConnectionFactory _factory;
    public RbacRepo(AdoUnitOfWork uow, IDbConnectionFactory factory)
    {
        _uow = uow;
        _factory = factory;
    }

    // ── Modules ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AppModule>> ListModulesAsync(CancellationToken ct = default)
    {
        // Hot path: MainLayout + NavMenu call this (and ListMenusAsync) for EVERY
        // authenticated user on every circuit. Pure read, no shared transaction, so
        // use a SHORT-LIVED pooled connection released immediately rather than the
        // circuit-scoped _uow connection (which would stay pinned for the whole
        // SignalR session and exhaust the Oracle session pool). Write/transactional
        // methods below deliberately keep using _uow.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(
            "SELECT ID,CODE,NAME,KIND,PARENT_ID,ICON,SORT_ORDER,IS_ACTIVE FROM RP_RBAC_MODULE ORDER BY SORT_ORDER,NAME");
        return await cmd.QueryAsync(MapModule, ct);
    }

    public async Task<AppModule?> GetModuleAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,CODE,NAME,KIND,PARENT_ID,ICON,SORT_ORDER,IS_ACTIVE FROM RP_RBAC_MODULE WHERE ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(MapModule, ct);
    }

    public async Task<long> UpsertModuleAsync(AppModule m, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        if (m.Id == 0)
        {
            using var cmd = _uow.OracleConn.Cmd(@"
                INSERT INTO RP_RBAC_MODULE(ID,CODE,NAME,KIND,PARENT_ID,ICON,SORT_ORDER,IS_ACTIVE)
                VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_code,:p_name,:p_kind,:p_parent,:p_icon,:p_sort,:p_active)
                RETURNING ID INTO :p_id", _uow.OracleTx);
            cmd.Parameters.AddIn("p_code", m.Code); cmd.Parameters.AddIn("p_name", m.Name);
            cmd.Parameters.AddIn("p_kind", (int)m.Kind); cmd.Parameters.AddIn("p_parent", OracleDbType.Int64, m.ParentId);
            cmd.Parameters.AddIn("p_icon", OracleDbType.Varchar2, m.Icon);
            cmd.Parameters.AddIn("p_sort", m.SortOrder); cmd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
            var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
            await cmd.ExecAsync(ct); m.Id = outId.OutId(); return m.Id;
        }
        using var upd = _uow.OracleConn.Cmd(@"
            UPDATE RP_RBAC_MODULE SET CODE=:p_code,NAME=:p_name,KIND=:p_kind,PARENT_ID=:p_parent,
            ICON=:p_icon,SORT_ORDER=:p_sort,IS_ACTIVE=:p_active,UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id",
            _uow.OracleTx);
        upd.Parameters.AddIn("p_code", m.Code); upd.Parameters.AddIn("p_name", m.Name);
        upd.Parameters.AddIn("p_kind", (int)m.Kind); upd.Parameters.AddIn("p_parent", OracleDbType.Int64, m.ParentId);
        upd.Parameters.AddIn("p_icon", OracleDbType.Varchar2, m.Icon);
        upd.Parameters.AddIn("p_sort", m.SortOrder); upd.Parameters.AddIn("p_active", m.IsActive ? 1 : 0);
        upd.Parameters.AddIn("p_id", m.Id); await upd.ExecAsync(ct); return m.Id;
    }

    public async Task DeleteModuleAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_RBAC_MODULE SET IS_ACTIVE=0,UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    // ── Menus ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AppMenu>> ListMenusAsync(CancellationToken ct = default)
    {
        // Hot path (see ListModulesAsync): short-lived pooled connection, released
        // immediately, instead of the circuit-pinned _uow connection.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(
            "SELECT ID,MODULE_ID,CODE,LABEL,ICON,ROUTE,SORT_ORDER,IS_ENABLED,IS_ACTIVE FROM RP_RBAC_MENU ORDER BY SORT_ORDER,LABEL");
        return await cmd.QueryAsync(MapMenu, ct);
    }

    public async Task<AppMenu?> GetMenuAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,MODULE_ID,CODE,LABEL,ICON,ROUTE,SORT_ORDER,IS_ENABLED,IS_ACTIVE FROM RP_RBAC_MENU WHERE ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(MapMenu, ct);
    }

    public async Task<long> UpsertMenuAsync(AppMenu menu, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        if (menu.Id == 0)
        {
            using var cmd = _uow.OracleConn.Cmd(@"
                INSERT INTO RP_RBAC_MENU(ID,MODULE_ID,CODE,LABEL,ICON,ROUTE,SORT_ORDER,IS_ENABLED,IS_ACTIVE)
                VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_mod,:p_code,:p_label,:p_icon,:p_route,:p_sort,:p_enabled,:p_active)
                RETURNING ID INTO :p_id", _uow.OracleTx);
            cmd.Parameters.AddIn("p_mod", menu.ModuleId); cmd.Parameters.AddIn("p_code", menu.Code);
            cmd.Parameters.AddIn("p_label", menu.Label); cmd.Parameters.AddIn("p_icon", OracleDbType.Varchar2, menu.Icon);
            cmd.Parameters.AddIn("p_route", OracleDbType.Varchar2, menu.Route);
            cmd.Parameters.AddIn("p_sort", menu.SortOrder);
            cmd.Parameters.AddIn("p_enabled", menu.IsEnabled ? 1 : 0); cmd.Parameters.AddIn("p_active", menu.IsActive ? 1 : 0);
            var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
            await cmd.ExecAsync(ct); menu.Id = outId.OutId(); return menu.Id;
        }
        using var upd = _uow.OracleConn.Cmd(@"
            UPDATE RP_RBAC_MENU SET MODULE_ID=:p_mod,CODE=:p_code,LABEL=:p_label,ICON=:p_icon,ROUTE=:p_route,
            SORT_ORDER=:p_sort,IS_ENABLED=:p_enabled,IS_ACTIVE=:p_active,UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id",
            _uow.OracleTx);
        upd.Parameters.AddIn("p_mod", menu.ModuleId); upd.Parameters.AddIn("p_code", menu.Code);
        upd.Parameters.AddIn("p_label", menu.Label); upd.Parameters.AddIn("p_icon", OracleDbType.Varchar2, menu.Icon);
        upd.Parameters.AddIn("p_route", OracleDbType.Varchar2, menu.Route);
        upd.Parameters.AddIn("p_sort", menu.SortOrder);
        upd.Parameters.AddIn("p_enabled", menu.IsEnabled ? 1 : 0); upd.Parameters.AddIn("p_active", menu.IsActive ? 1 : 0);
        upd.Parameters.AddIn("p_id", menu.Id); await upd.ExecAsync(ct); return menu.Id;
    }

    public async Task DeleteMenuAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_RBAC_MENU SET IS_ACTIVE=0,UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    // ── Role Permissions ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RoleModulePerm>> ListRolePermsAsync(long? roleId = null, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var filter = roleId.HasValue ? "WHERE ROLE_ID=:p_role" : "WHERE 1=1";
        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT ID,ROLE_ID,MODULE_ID,CAN_VIEW,CAN_ADD,CAN_MODIFY,CAN_DELETE,CAN_AUTHORIZE,
                   UPDATED_AT_UTC,UPDATED_BY_EMP FROM RP_RBAC_ROLE_PERM {filter}", _uow.OracleTx);
        if (roleId.HasValue) cmd.Parameters.AddIn("p_role", roleId.Value);
        return await cmd.QueryAsync(MapRolePerm, ct);
    }

    public async Task UpsertRolePermAsync(RoleModulePerm p, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            MERGE INTO RP_RBAC_ROLE_PERM t
            USING (SELECT :p_role AS ROLE_ID, :p_module AS MODULE_ID FROM DUAL) s
            ON (t.ROLE_ID=s.ROLE_ID AND t.MODULE_ID=s.MODULE_ID)
            WHEN MATCHED THEN UPDATE SET CAN_VIEW=:p_view,CAN_ADD=:p_add,CAN_MODIFY=:p_mod,
                CAN_DELETE=:p_del,CAN_AUTHORIZE=:p_auth,UPDATED_AT_UTC=:p_at,UPDATED_BY_EMP=:p_actor
            WHEN NOT MATCHED THEN INSERT(ID,ROLE_ID,MODULE_ID,CAN_VIEW,CAN_ADD,CAN_MODIFY,CAN_DELETE,
                CAN_AUTHORIZE,UPDATED_AT_UTC,UPDATED_BY_EMP)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,s.ROLE_ID,s.MODULE_ID,:p_view,:p_add,:p_mod,:p_del,:p_auth,:p_at,:p_actor)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_role", p.RoleId); cmd.Parameters.AddIn("p_module", p.ModuleId);
        cmd.Parameters.AddIn("p_view", p.CanView ? 1 : 0); cmd.Parameters.AddIn("p_add", p.CanAdd ? 1 : 0);
        cmd.Parameters.AddIn("p_mod", p.CanModify ? 1 : 0); cmd.Parameters.AddIn("p_del", p.CanDelete ? 1 : 0);
        cmd.Parameters.AddIn("p_auth", p.CanAuthorize ? 1 : 0);
        cmd.Parameters.AddIn("p_at", p.UpdatedAtUtc); cmd.Parameters.AddIn("p_actor", OracleDbType.Varchar2, p.UpdatedByEmp);
        await cmd.ExecAsync(ct);
    }

    public async Task DeleteRolePermAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_RBAC_ROLE_PERM WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    // ── User Overrides ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<UserModulePerm>> ListUserOverridesAsync(string? empCode = null, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var filter = empCode is not null ? "WHERE UPPER(EMP_CODE)=UPPER(:p_emp)" : "WHERE 1=1";
        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT ID,EMP_CODE,MODULE_ID,IS_DENY,CAN_VIEW,CAN_ADD,CAN_MODIFY,CAN_DELETE,
                   CAN_AUTHORIZE,UPDATED_AT_UTC,UPDATED_BY_EMP FROM RP_RBAC_USER_PERM {filter}",
            _uow.OracleTx);
        if (empCode is not null) cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.QueryAsync(MapUserPerm, ct);
    }

    public async Task UpsertUserOverrideAsync(UserModulePerm p, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            MERGE INTO RP_RBAC_USER_PERM t
            USING (SELECT :p_user AS EMP_CODE,:p_module AS MODULE_ID,:p_deny AS IS_DENY FROM DUAL) s
            ON (UPPER(t.EMP_CODE)=UPPER(s.EMP_CODE) AND t.MODULE_ID=s.MODULE_ID AND t.IS_DENY=s.IS_DENY)
            WHEN MATCHED THEN UPDATE SET CAN_VIEW=:p_view,CAN_ADD=:p_add,CAN_MODIFY=:p_mod,
                CAN_DELETE=:p_del,CAN_AUTHORIZE=:p_auth,UPDATED_AT_UTC=:p_at,UPDATED_BY_EMP=:p_actor
            WHEN NOT MATCHED THEN INSERT(ID,EMP_CODE,MODULE_ID,IS_DENY,CAN_VIEW,CAN_ADD,CAN_MODIFY,
                CAN_DELETE,CAN_AUTHORIZE,UPDATED_AT_UTC,UPDATED_BY_EMP)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,s.EMP_CODE,s.MODULE_ID,s.IS_DENY,:p_view,:p_add,:p_mod,:p_del,:p_auth,:p_at,:p_actor)",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_user", p.EmpCode); cmd.Parameters.AddIn("p_module", p.ModuleId);
        cmd.Parameters.AddIn("p_deny", p.IsDeny ? 1 : 0);
        cmd.Parameters.AddIn("p_view", p.CanView ? 1 : 0); cmd.Parameters.AddIn("p_add", p.CanAdd ? 1 : 0);
        cmd.Parameters.AddIn("p_mod", p.CanModify ? 1 : 0); cmd.Parameters.AddIn("p_del", p.CanDelete ? 1 : 0);
        cmd.Parameters.AddIn("p_auth", p.CanAuthorize ? 1 : 0);
        cmd.Parameters.AddIn("p_at", p.UpdatedAtUtc); cmd.Parameters.AddIn("p_actor", OracleDbType.Varchar2, p.UpdatedByEmp);
        await cmd.ExecAsync(ct);
    }

    public async Task DeleteUserOverrideAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_RBAC_USER_PERM WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    // ── AppAdmin ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AppAdmin>> ListAppAdminsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,ASSIGNED_AT_UTC,ASSIGNED_BY_EMP,IS_ACTIVE,NOTE FROM RP_RBAC_APP_ADMIN ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);
        return await cmd.QueryAsync(MapAppAdmin, ct);
    }

    public async Task<AppAdmin?> GetActiveAppAdminAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,ASSIGNED_AT_UTC,ASSIGNED_BY_EMP,IS_ACTIVE,NOTE FROM RP_RBAC_APP_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_emp) AND IS_ACTIVE=1",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.QueryOneAsync(MapAppAdmin, ct);
    }

    public async Task<long> UpsertAppAdminAsync(AppAdmin a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var merge = _uow.OracleConn.Cmd(@"
            MERGE INTO RP_RBAC_APP_ADMIN t USING (SELECT :p_emp AS EMP_CODE FROM DUAL) s
            ON (UPPER(t.EMP_CODE)=UPPER(s.EMP_CODE))
            WHEN MATCHED THEN UPDATE SET ASSIGNED_AT_UTC=:p_at,ASSIGNED_BY_EMP=:p_by,IS_ACTIVE=:p_active,NOTE=:p_note
            WHEN NOT MATCHED THEN INSERT(ID,EMP_CODE,ASSIGNED_AT_UTC,ASSIGNED_BY_EMP,IS_ACTIVE,NOTE)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,s.EMP_CODE,:p_at,:p_by,:p_active,:p_note)", _uow.OracleTx);
        merge.Parameters.AddIn("p_emp", a.EmpCode); merge.Parameters.AddIn("p_at", a.AssignedAtUtc);
        merge.Parameters.AddIn("p_by", OracleDbType.Varchar2, a.AssignedByEmp);
        merge.Parameters.AddIn("p_active", a.IsActive ? 1 : 0); merge.Parameters.AddIn("p_note", OracleDbType.Varchar2, a.Note);
        await merge.ExecAsync(ct);

        using var sel = _uow.OracleConn.Cmd("SELECT ID FROM RP_RBAC_APP_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_emp)", _uow.OracleTx);
        sel.Parameters.AddIn("p_emp", a.EmpCode);
        return await sel.ScalarAsync<long>(ct);
    }

     // ── FeedbackAdmin ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<FeedbackAdmin>> ListFeedbackAdminsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,ASSIGNED_AT_UTC,ASSIGNED_BY_EMP,IS_ACTIVE,NOTE FROM RP_RBAC_FEEDBACK_ADMIN ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);
        return await cmd.QueryAsync(MapFeedbackAdmin, ct);
    }

    public async Task<FeedbackAdmin?> GetActiveFeedbackAdminAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,ASSIGNED_AT_UTC,ASSIGNED_BY_EMP,IS_ACTIVE,NOTE FROM RP_RBAC_FEEDBACK_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_emp) AND IS_ACTIVE=1",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.QueryOneAsync(MapFeedbackAdmin, ct);
    }

    public async Task<long> UpsertFeedbackAdminAsync(FeedbackAdmin a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var merge = _uow.OracleConn.Cmd(@"
            MERGE INTO RP_RBAC_FEEDBACK_ADMIN t USING (SELECT :p_emp AS EMP_CODE FROM DUAL) s
            ON (UPPER(t.EMP_CODE)=UPPER(s.EMP_CODE))
            WHEN MATCHED THEN UPDATE SET ASSIGNED_AT_UTC=:p_at,ASSIGNED_BY_EMP=:p_by,IS_ACTIVE=:p_active,NOTE=:p_note
            WHEN NOT MATCHED THEN INSERT(ID,EMP_CODE,ASSIGNED_AT_UTC,ASSIGNED_BY_EMP,IS_ACTIVE,NOTE)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,s.EMP_CODE,:p_at,:p_by,:p_active,:p_note)", _uow.OracleTx);
        merge.Parameters.AddIn("p_emp", a.EmpCode); merge.Parameters.AddIn("p_at", a.AssignedAtUtc);
        merge.Parameters.AddIn("p_by", OracleDbType.Varchar2, a.AssignedByEmp);
        merge.Parameters.AddIn("p_active", a.IsActive ? 1 : 0); merge.Parameters.AddIn("p_note", OracleDbType.Varchar2, a.Note);
        await merge.ExecAsync(ct);

        using var sel = _uow.OracleConn.Cmd("SELECT ID FROM RP_RBAC_FEEDBACK_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_emp)", _uow.OracleTx);
        sel.Parameters.AddIn("p_emp", a.EmpCode);
        return await sel.ScalarAsync<long>(ct);
    }

    // ── SuperAdmin ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SuperAdmin>> ListSuperAdminsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,ASSIGNED_AT_UTC,ASSIGNED_BY FROM RP_RBAC_SUPER_ADMIN ORDER BY ASSIGNED_AT_UTC",
            _uow.OracleTx);
        return await cmd.QueryAsync(r => new SuperAdmin
        {
            Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"),
            AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC"), AssignedBy = StrN(r, "ASSIGNED_BY")
        }, ct);
    }

    public async Task<bool> IsSuperAdminAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT COUNT(*) FROM RP_RBAC_SUPER_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_emp)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.ScalarAsync<int>(ct) > 0;
    }

    // ── RBAC Audit ────────────────────────────────────────────────────────────

    public async Task AppendAuditAsync(RbacAudit row, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_RBAC_AUDIT(ID,AT_UTC,ACTOR_EMP_CODE,ACTION,TARGET,DETAILS)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_at,:p_actor,:p_action,:p_target,:p_details)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_at", row.AtUtc); cmd.Parameters.AddIn("p_actor", row.ActorEmpCode);
        cmd.Parameters.AddIn("p_action", row.Action.ToString()); cmd.Parameters.AddIn("p_target", row.Target);
        cmd.Parameters.AddIn("p_details", OracleDbType.Varchar2, row.Details);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<RbacAudit>> ListAuditAsync(int take = 200, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT * FROM (SELECT ID,AT_UTC,ACTOR_EMP_CODE,ACTION,TARGET,DETAILS FROM RP_RBAC_AUDIT ORDER BY AT_UTC DESC)
            WHERE ROWNUM<=:p_take", _uow.OracleTx);
        cmd.Parameters.AddIn("p_take", take);
        return await cmd.QueryAsync(r => new RbacAudit
        {
            Id = Long(r, "ID"), AtUtc = Dt(r, "AT_UTC"), ActorEmpCode = Str(r, "ACTOR_EMP_CODE"),
            Action = Enum.TryParse<RbacAuditAction>(Str(r, "ACTION"), true, out var a) ? a : RbacAuditAction.UpsertModule,
            Target = Str(r, "TARGET"), Details = StrN(r, "DETAILS")
        }, ct);
    }

    // ── Private mappers ───────────────────────────────────────────────────────

    private static AppModule MapModule(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), Code = Str(r, "CODE"), Name = Str(r, "NAME"),
        Kind = (ModuleKind)Int(r, "KIND"), ParentId = LongN(r, "PARENT_ID"),
        Icon = StrN(r, "ICON"), SortOrder = Int(r, "SORT_ORDER"), IsActive = Bool(r, "IS_ACTIVE")
    };

    private static AppMenu MapMenu(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), ModuleId = Long(r, "MODULE_ID"), Code = Str(r, "CODE"),
        Label = Str(r, "LABEL"), Icon = StrN(r, "ICON"), Route = StrN(r, "ROUTE"),
        SortOrder = Int(r, "SORT_ORDER"), IsEnabled = Bool(r, "IS_ENABLED"), IsActive = Bool(r, "IS_ACTIVE")
    };

    private static RoleModulePerm MapRolePerm(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), RoleId = Long(r, "ROLE_ID"), ModuleId = Long(r, "MODULE_ID"),
        CanView = Bool(r, "CAN_VIEW"), CanAdd = Bool(r, "CAN_ADD"), CanModify = Bool(r, "CAN_MODIFY"),
        CanDelete = Bool(r, "CAN_DELETE"), CanAuthorize = Bool(r, "CAN_AUTHORIZE"),
        UpdatedAtUtc = Dt(r, "UPDATED_AT_UTC"), UpdatedByEmp = StrN(r, "UPDATED_BY_EMP")
    };

    private static UserModulePerm MapUserPerm(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"), ModuleId = Long(r, "MODULE_ID"),
        IsDeny = Bool(r, "IS_DENY"), CanView = Bool(r, "CAN_VIEW"), CanAdd = Bool(r, "CAN_ADD"),
        CanModify = Bool(r, "CAN_MODIFY"), CanDelete = Bool(r, "CAN_DELETE"), CanAuthorize = Bool(r, "CAN_AUTHORIZE"),
        UpdatedAtUtc = Dt(r, "UPDATED_AT_UTC"), UpdatedByEmp = StrN(r, "UPDATED_BY_EMP")
    };

    private static AppAdmin MapAppAdmin(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"),
        AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC"), AssignedByEmp = StrN(r, "ASSIGNED_BY_EMP"),
        IsActive = Bool(r, "IS_ACTIVE"), Note = StrN(r, "NOTE")
    };

    private static FeedbackAdmin MapFeedbackAdmin(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"),
        EmpCode = Str(r, "EMP_CODE"),
        AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC"),
        AssignedByEmp = StrN(r, "ASSIGNED_BY_EMP"),
        IsActive = Bool(r, "IS_ACTIVE"),
        Note = StrN(r, "NOTE")
    };
}
