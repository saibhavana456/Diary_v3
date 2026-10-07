using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class RoutingRepo : IRoutingRepo
{
    private readonly AdoUnitOfWork _uow;
    public RoutingRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<string>> ResolveL1Async(long requestTypeId, long unitId, long verticalId, long departmentId, long activityId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            WITH unit_type AS (SELECT UNIT_TYPE_ID FROM RP_M_UNIT WHERE ID=:p_unit),
            scored AS (
                SELECT r.ID, r.PRIORITY,
                    (CASE WHEN r.ACTIVITY_ID   = :p_act THEN 16 ELSE 0 END
                    +CASE WHEN r.DEPARTMENT_ID  = :p_dep THEN  8 ELSE 0 END
                    +CASE WHEN r.VERTICAL_ID    = :p_v   THEN  4 ELSE 0 END
                    +CASE WHEN r.UNIT_TYPE_ID   = (SELECT UNIT_TYPE_ID FROM unit_type) THEN 2 ELSE 0 END
                    ) AS SPECIFICITY
                FROM RP_M_ROUTING_RULE r
                WHERE r.IS_ACTIVE=1 AND r.REQUEST_TYPE_ID=:p_rt
                  AND (r.ACTIVITY_ID   IS NULL OR r.ACTIVITY_ID   = :p_act)
                  AND (r.DEPARTMENT_ID IS NULL OR r.DEPARTMENT_ID = :p_dep)
                  AND (r.VERTICAL_ID   IS NULL OR r.VERTICAL_ID   = :p_v)
                  AND (r.UNIT_TYPE_ID  IS NULL OR r.UNIT_TYPE_ID  = (SELECT UNIT_TYPE_ID FROM unit_type))
            ),
            picked AS (SELECT ID FROM scored ORDER BY SPECIFICITY DESC, PRIORITY ASC FETCH FIRST 1 ROWS ONLY)
            SELECT ra.EMP_CODE FROM RP_M_ROUTING_ASSIGNEE ra JOIN picked p ON p.ID=ra.RULE_ID",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rt", requestTypeId); cmd.Parameters.AddIn("p_unit", unitId);
        cmd.Parameters.AddIn("p_v", verticalId); cmd.Parameters.AddIn("p_dep", departmentId);
        cmd.Parameters.AddIn("p_act", activityId);
        return await cmd.QueryAsync(r => Str(r, "EMP_CODE"), ct);
    }

    public async Task<IReadOnlyList<string>> ResolveByRoleAsync(RoleCode role, long unitId, long verticalId, long departmentId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT DISTINCT vw.EMPLOYEE_CODE AS EMP_CODE FROM RP_RBAC_VERTICAL_LEVEL vl
            JOIN VW_STAFF_USER_SUMMARY vw ON vw.EMPLOYEE_CODE=vl.EMP_CODE
            WHERE vl.LEVEL_CODE=:p_role AND vl.IS_ACTIVE=1
              AND (vl.VERTICAL_ID IS NULL OR vl.VERTICAL_ID=:p_v)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_role", role.ToString()); cmd.Parameters.AddIn("p_v", verticalId);
        return await cmd.QueryAsync(r => Str(r, "EMP_CODE"), ct);
    }

    public async Task<IReadOnlyList<RoutingRule>> ListRulesAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,REQUEST_TYPE_ID,UNIT_TYPE_ID,VERTICAL_ID,DEPARTMENT_ID,ACTIVITY_ID,PRIORITY,IS_ACTIVE
            FROM RP_M_ROUTING_RULE ORDER BY PRIORITY,ID", _uow.OracleTx);
        return await cmd.QueryAsync(MapRule, ct);
    }

    public async Task<long> InsertRuleAsync(RoutingRule r, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_M_ROUTING_RULE(ID,REQUEST_TYPE_ID,UNIT_TYPE_ID,VERTICAL_ID,DEPARTMENT_ID,ACTIVITY_ID,PRIORITY,IS_ACTIVE)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rt,:p_ut,:p_v,:p_dep,:p_act,:p_pri,:p_active)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rt", r.RequestTypeId);
        cmd.Parameters.AddIn("p_ut", OracleDbType.Int64, r.UnitTypeId);
        cmd.Parameters.AddIn("p_v", OracleDbType.Int64, r.VerticalId);
        cmd.Parameters.AddIn("p_dep", OracleDbType.Int64, r.DepartmentId);
        cmd.Parameters.AddIn("p_act", OracleDbType.Int64, r.ActivityId);
        cmd.Parameters.AddIn("p_pri", r.Priority); cmd.Parameters.AddIn("p_active", r.IsActive ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateRuleAsync(RoutingRule r, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            UPDATE RP_M_ROUTING_RULE SET REQUEST_TYPE_ID=:p_rt,UNIT_TYPE_ID=:p_ut,VERTICAL_ID=:p_v,
            DEPARTMENT_ID=:p_dep,ACTIVITY_ID=:p_act,PRIORITY=:p_pri,IS_ACTIVE=:p_active WHERE ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rt", r.RequestTypeId);
        cmd.Parameters.AddIn("p_ut", OracleDbType.Int64, r.UnitTypeId);
        cmd.Parameters.AddIn("p_v", OracleDbType.Int64, r.VerticalId);
        cmd.Parameters.AddIn("p_dep", OracleDbType.Int64, r.DepartmentId);
        cmd.Parameters.AddIn("p_act", OracleDbType.Int64, r.ActivityId);
        cmd.Parameters.AddIn("p_pri", r.Priority); cmd.Parameters.AddIn("p_active", r.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_id", r.Id); await cmd.ExecAsync(ct);
    }

    public async Task SetRuleActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("UPDATE RP_M_ROUTING_RULE SET IS_ACTIVE=:p_a WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_a", isActive ? 1 : 0); cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    public async Task DeleteRuleAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_M_ROUTING_RULE WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<RoutingAssignee>> ListAssigneesAsync(long ruleId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,RULE_ID,EMP_CODE,IS_PRIMARY FROM RP_M_ROUTING_ASSIGNEE WHERE RULE_ID=:p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", ruleId);
        return await cmd.QueryAsync(r => new RoutingAssignee
        {
            Id = Long(r, "ID"), RuleId = Long(r, "RULE_ID"),
            EmpCode = Str(r, "EMP_CODE"), IsPrimary = Bool(r, "IS_PRIMARY")
        }, ct);
    }

    public async Task<long> InsertAssigneeAsync(RoutingAssignee a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_M_ROUTING_ASSIGNEE(ID,RULE_ID,EMP_CODE,IS_PRIMARY)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rule,:p_emp,:p_pri) RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rule", a.RuleId); cmd.Parameters.AddIn("p_emp", a.EmpCode);
        cmd.Parameters.AddIn("p_pri", a.IsPrimary ? 1 : 0);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task DeleteAssigneeAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_M_ROUTING_ASSIGNEE WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    private static RoutingRule MapRule(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), RequestTypeId = Long(r, "REQUEST_TYPE_ID"),
        UnitTypeId = LongN(r, "UNIT_TYPE_ID"), VerticalId = LongN(r, "VERTICAL_ID"),
        DepartmentId = LongN(r, "DEPARTMENT_ID"), ActivityId = LongN(r, "ACTIVITY_ID"),
        Priority = Int(r, "PRIORITY"), IsActive = Bool(r, "IS_ACTIVE")
    };
}
