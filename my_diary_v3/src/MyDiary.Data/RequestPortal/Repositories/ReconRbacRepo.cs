using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>Recon Certificates RBAC — ReconVerticalUser role assignment and the Recon Admin who manages
/// them. Byte-for-byte the same shape as DmdRbacRepo, over two fully separate tables
/// (RP_RBAC_RECON_ADMIN, RP_RBAC_RECON_ROLE) — see db/recon/001_recon_rbac.sql.</summary>
public sealed class ReconRbacRepo : IReconRbacRepo
{
    private readonly AdoUnitOfWork _uow;

    public ReconRbacRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<ReconAdmin>> ListAdminsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC FROM RP_RBAC_RECON_ADMIN ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);
        return await cmd.QueryAsync(MapAdmin, ct);
    }

    public async Task<bool> IsAdminAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT COUNT(*) FROM RP_RBAC_RECON_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_emp) AND IS_ACTIVE=1",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.ScalarAsync<int>(ct) > 0;
    }

    public async Task UpsertAdminAsync(ReconAdmin a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            MERGE INTO RP_RBAC_RECON_ADMIN t USING (SELECT :p_emp AS EMP_CODE FROM DUAL) s
            ON (UPPER(t.EMP_CODE)=UPPER(s.EMP_CODE))
            WHEN MATCHED THEN UPDATE SET IS_ACTIVE=:p_active,ASSIGNED_BY_EMP=:p_by,ASSIGNED_AT_UTC=:p_at
            WHEN NOT MATCHED THEN INSERT(ID,EMP_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,s.EMP_CODE,:p_active,:p_by,:p_at)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", a.EmpCode); cmd.Parameters.AddIn("p_active", a.IsActive ? 1 : 0);
        cmd.Parameters.AddIn("p_by", a.AssignedByEmp); cmd.Parameters.AddIn("p_at", a.AssignedAtUtc);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<ReconVerticalUserAssignment>> ListVerticalUserAssignmentsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,EMP_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC FROM RP_RBAC_RECON_ROLE ORDER BY ASSIGNED_AT_UTC DESC",
            _uow.OracleTx);
        return await cmd.QueryAsync(MapAssignment, ct);
    }

    public async Task<ReconVerticalUserAssignment?> GetActiveVerticalUserAsync(string empCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT * FROM (
              SELECT ID,EMP_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC FROM RP_RBAC_RECON_ROLE
              WHERE UPPER(EMP_CODE)=UPPER(:p_emp) AND IS_ACTIVE=1 ORDER BY ASSIGNED_AT_UTC DESC
            ) WHERE ROWNUM=1", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", empCode);
        return await cmd.QueryOneAsync(MapAssignment, ct);
    }

    /// <summary>Deactivates any existing active ReconVerticalUser assignment for this employee before
    /// inserting the new one, so an employee never ends up with two simultaneously-active rows.</summary>
    public async Task<long> UpsertVerticalUserAssignmentAsync(ReconVerticalUserAssignment a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);

        var existing = await GetActiveVerticalUserAsync(a.EmpCode, ct);
        if (existing is not null)
        {
            using var deact = _uow.OracleConn.Cmd(
                "UPDATE RP_RBAC_RECON_ROLE SET IS_ACTIVE=0 WHERE ID=:p_id", _uow.OracleTx);
            deact.Parameters.AddIn("p_id", existing.Id);
            await deact.ExecAsync(ct);
        }

        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_RBAC_RECON_ROLE(ID,EMP_CODE,ROLE_CODE,IS_ACTIVE,ASSIGNED_BY_EMP,ASSIGNED_AT_UTC)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_emp,'ReconVerticalUser',1,:p_by,:p_at)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", a.EmpCode);
        cmd.Parameters.AddIn("p_by", a.AssignedByEmp); cmd.Parameters.AddIn("p_at", a.AssignedAtUtc);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task DeactivateVerticalUserAssignmentAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_RBAC_RECON_ROLE SET IS_ACTIVE=0 WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        await cmd.ExecAsync(ct);
    }

    private static ReconAdmin MapAdmin(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"), IsActive = Bool(r, "IS_ACTIVE"),
        AssignedByEmp = Str(r, "ASSIGNED_BY_EMP"), AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC")
    };

    private static ReconVerticalUserAssignment MapAssignment(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"), IsActive = Bool(r, "IS_ACTIVE"),
        AssignedByEmp = Str(r, "ASSIGNED_BY_EMP"), AssignedAtUtc = Dt(r, "ASSIGNED_AT_UTC")
    };
}
