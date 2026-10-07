using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>Corporate Credit Portal (CCP) RBAC — CoLcv/CoMcv role assignment and the CCP Vertical Admin
/// who manages them. Two dedicated tables (RP_RBAC_CCP_VERT_ADMIN, RP_RBAC_CCP_ROLE) — see
/// db/rbac/001_ccp_vertical_rbac.sql for why these aren't a reuse of RP_RBAC_VERTICAL_LEVEL/ADMIN (those
/// key off VERTICAL_ID -> RP_M_VERTICAL, an existing Request Portal ticket-routing master this change
/// doesn't need to disturb).</summary>
public sealed class CcpRbacRepo : ICcpRbacRepo
{
    private readonly AdoUnitOfWork _uow;

    public CcpRbacRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<CcpVerticalAdmin>> ListVerticalAdminsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC FROM RP_RBAC_CCP_VERT_ADMIN ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);
        return await cmd.QueryAsync(MapVerticalAdmin, ct);
    }

    public async Task<bool> IsVerticalAdminAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT COUNT(*) FROM RP_RBAC_CCP_VERT_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_emp) AND IS_ACTIVE=1",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.ScalarAsync<int>(ct) > 0;
    }

    public async Task UpsertVerticalAdminAsync(CcpVerticalAdmin a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            MERGE INTO RP_RBAC_CCP_VERT_ADMIN t USING (SELECT :p_emp AS EMP_CODE FROM DUAL) s
            ON (UPPER(t.EMP_CODE)=UPPER(s.EMP_CODE))
            WHEN MATCHED THEN UPDATE SET IS_ACTIVE=:p_active,ASSIGNED_BY_EMP=:p_by,ASSIGNED_AT_UTC=:p_at
            WHEN NOT MATCHED THEN INSERT(ID,EMP_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,s.EMP_CODE,:p_active,:p_by,:p_at)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", a.EmpCode); cmd.Parameters.AddIn("p_active", a.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_by", a.AssignedByEmp); cmd.Parameters.AddIn("p_at", a.AssignedAtUtc);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<CcpRoleAssignment>> ListRoleAssignmentsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,ROLE_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC FROM RP_RBAC_CCP_ROLE ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);
        return await cmd.QueryAsync(MapAssignment, ct);
    }

    public async Task<CcpRoleAssignment?> GetActiveRoleAssignmentAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT * FROM (
              SELECT ID,EMP_CODE,ROLE_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC FROM RP_RBAC_CCP_ROLE
              WHERE UPPER(EMP_CODE)=UPPER(:p_emp) AND IS_ACTIVE=1 ORDER BY ASSIGNED_AT_UTC DESC
            ) WHERE ROWNUM=1", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.QueryOneAsync(MapAssignment, ct);
    }

    /// <summary>Deactivates any existing active CCP role assignment for this employee before inserting the
    /// new one, so an employee never ends up with two simultaneously-active CoLcv/CoMcv rows.</summary>
    public async Task<long> UpsertRoleAssignmentAsync(CcpRoleAssignment a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var existing = await GetActiveRoleAssignmentAsync(a.EmpCode, ct);
        if (existing is not null)
        {
            using var deact = _uow.OracleConn.Cmd(
                "UPDATE RP_RBAC_CCP_ROLE SET IS_ACTIVE=0 WHERE ID=:p_id", _uow.OracleTx);
            deact.Parameters.AddIn("p_id", existing.Id);
            await deact.ExecAsync(ct);
        }

        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_RBAC_CCP_ROLE(ID,EMP_CODE,ROLE_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_emp,:p_role,1,:p_by,:p_at)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", a.EmpCode); cmd.Parameters.AddIn("p_role", a.RoleCode);
        cmd.Parameters.AddIn("p_by", a.AssignedByEmp); cmd.Parameters.AddIn("p_at", a.AssignedAtUtc);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task DeactivateRoleAssignmentAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_RBAC_CCP_ROLE SET IS_ACTIVE=0 WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static CcpVerticalAdmin MapVerticalAdmin(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"), IsActive = Bool(r, "IS_ACTIVE"),
        AssignedByEmp = Str(r, "ASSIGNED_BY_EMP"), AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC")
    };

    private static CcpRoleAssignment MapAssignment(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"), RoleCode = Str(r, "ROLE_CODE"),
        IsActive = Bool(r, "IS_ACTIVE"), AssignedByEmp = Str(r, "ASSIGNED_BY_EMP"),
        AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC")
    };
}


/// <summary>
/// DMD RBAC repository for the Back-Dated Deposit Approval workflow.
/// Manages DMD Vertical Admins and DMD Approver assignments.
/// Uses dedicated tables:
/// RP_RBAC_DMD_VERT_ADMIN
/// RP_RBAC_DMD_APPROVER
/// </summary>
public sealed class DmdRbacRepo : IDmdRbacRepo
{
    private readonly AdoUnitOfWork _uow;

    public DmdRbacRepo(AdoUnitOfWork uow)
    {
        _uow = uow;
    }

    // =========================================================
    // DMD Vertical Admin
    // =========================================================

    public async Task<IReadOnlyList<DmdVerticalAdmin>>
        ListVerticalAdminsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT
                ID,
                EMP_CODE,
                IS_ACTIVE,
                ASSIGNED_BY_EMP,
                ASSIGNED_AT_UTC
              FROM RP_RBAC_DMD_VERT_ADMIN
              ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);

        return await cmd.QueryAsync(MapVerticalAdmin, ct);
    }


    public async Task<bool> IsVerticalAdminAsync(
        string empCode,
        CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT COUNT(*)
              FROM RP_RBAC_DMD_VERT_ADMIN
              WHERE UPPER(EMP_CODE) = UPPER(:p_emp)
                AND IS_ACTIVE = 1",
            _uow.OracleTx);

        cmd.Parameters.AddIn("p_emp", empCode);

        return await cmd.ScalarAsync<int>(ct) > 0;
    }


    public async Task UpsertVerticalAdminAsync(
        DmdVerticalAdmin a,
        CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var cmd = _uow.OracleConn.Cmd(
            @"MERGE INTO RP_RBAC_DMD_VERT_ADMIN t
              USING (
                  SELECT :p_emp AS EMP_CODE
                  FROM DUAL
              ) s
              ON (
                  UPPER(t.EMP_CODE) = UPPER(s.EMP_CODE)
              )
              WHEN MATCHED THEN
                  UPDATE SET
                      IS_ACTIVE = :p_active,
                      ASSIGNED_BY_EMP = :p_by,
                      ASSIGNED_AT_UTC = :p_at
              WHEN NOT MATCHED THEN
                  INSERT (
                      ID,
                      EMP_CODE,
                      IS_ACTIVE,
                      ASSIGNED_BY_EMP,
                      ASSIGNED_AT_UTC
                  )
                  VALUES (
                      RP_SEQ_GLOBAL.NEXTVAL,
                      s.EMP_CODE,
                      :p_active,
                      :p_by,
                      :p_at
                  )",
            _uow.OracleTx);

        cmd.Parameters.AddIn("p_emp", a.EmpCode);
        cmd.Parameters.AddIn("p_active", a.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_by", a.AssignedByEmp);
        cmd.Parameters.AddIn("p_at", a.AssignedAtUtc);

        await cmd.ExecAsync(ct);
    }


    // =========================================================
    // DMD Approver
    // =========================================================

    public async Task<IReadOnlyList<DmdApproverAssignment>>
        ListApproverAssignmentsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT
                ID,
                EMP_CODE,
                IS_ACTIVE,
                ASSIGNED_BY_EMP,
                ASSIGNED_AT_UTC
              FROM RP_RBAC_DMD_APPROVER
              ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);

        return await cmd.QueryAsync(MapApproverAssignment, ct);
    }


    public async Task<DmdApproverAssignment?>
        GetActiveApproverAsync(
            string empCode,
            CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var cmd = _uow.OracleConn.Cmd(
            @"SELECT *
              FROM (
                  SELECT
                      ID,
                      EMP_CODE,
                      IS_ACTIVE,
                      ASSIGNED_BY_EMP,
                      ASSIGNED_AT_UTC
                  FROM RP_RBAC_DMD_APPROVER
                  WHERE UPPER(EMP_CODE) = UPPER(:p_emp)
                    AND IS_ACTIVE = 1
                  ORDER BY ASSIGNED_AT_UTC DESC
              )
              WHERE ROWNUM = 1",
            _uow.OracleTx);

        cmd.Parameters.AddIn("p_emp", empCode);

        return await cmd.QueryOneAsync(
            MapApproverAssignment,
            ct);
    }


    /// <summary>
    /// Ensures an employee has only one active DMD Approver
    /// assignment. Existing active assignments for the employee
    /// are deactivated before inserting the new assignment.
    /// </summary>
    public async Task<long> UpsertApproverAssignmentAsync(
        DmdApproverAssignment a,
        CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var existing =
            await GetActiveApproverAsync(a.EmpCode, ct);

        if (existing is not null)
        {
            using var deactivateCmd =
                _uow.OracleConn.Cmd(
                    @"UPDATE RP_RBAC_DMD_APPROVER
                      SET IS_ACTIVE = 0
                      WHERE ID = :p_id",
                    _uow.OracleTx);

            deactivateCmd.Parameters.AddIn(
                "p_id",
                existing.Id);

            await deactivateCmd.ExecAsync(ct);
        }

        using var cmd = _uow.OracleConn.Cmd(
            @"INSERT INTO RP_RBAC_DMD_APPROVER
              (
                  ID,
                  EMP_CODE,
                  IS_ACTIVE,
                  ASSIGNED_BY_EMP,
                  ASSIGNED_AT_UTC
              )
              VALUES
              (
                  RP_SEQ_GLOBAL.NEXTVAL,
                  :p_emp,
                  1,
                  :p_by,
                  :p_at
              )
              RETURNING ID INTO :p_id",
            _uow.OracleTx);

        cmd.Parameters.AddIn(
            "p_emp",
            a.EmpCode);

        cmd.Parameters.AddIn(
            "p_by",
            a.AssignedByEmp);

        cmd.Parameters.AddIn(
            "p_at",
            a.AssignedAtUtc);

        var outId = cmd.Parameters.AddOut(
            "p_id",
            OracleDbType.Int64);

        await cmd.ExecAsync(ct);

        return outId.OutId();
    }


    public async Task DeactivateApproverAssignmentAsync(
        long id,
        CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        using var cmd = _uow.OracleConn.Cmd(
            @"UPDATE RP_RBAC_DMD_APPROVER
              SET IS_ACTIVE = 0
              WHERE ID = :p_id",
            _uow.OracleTx);

        cmd.Parameters.AddIn("p_id", id);

        await cmd.ExecAsync(ct);
    }


    // =========================================================
    // Mapping
    // =========================================================

    private static DmdVerticalAdmin MapVerticalAdmin(
        OracleDataReader r) => new()
        {
            Id = Long(r, "ID"),
            EmpCode = Str(r, "EMP_CODE"),
            IsActive = Bool(r, "IS_ACTIVE"),
            AssignedByEmp = Str(r, "ASSIGNED_BY_EMP"),
            AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC")
        };


    private static DmdApproverAssignment MapApproverAssignment(
        OracleDataReader r) => new()
        {
            Id = Long(r, "ID"),
            EmpCode = Str(r, "EMP_CODE"),
            IsActive = Bool(r, "IS_ACTIVE"),
            AssignedByEmp = Str(r, "ASSIGNED_BY_EMP"),
            AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC")
        };
}
