using Dapper;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Data.Repositories;

/// <summary>
/// Dapper repository for the Escalation Matrix. Column aliases are double-quoted so
/// Oracle preserves case and Dapper maps them straight onto the model properties.
/// </summary>
public sealed class EscalationMatrixRepo : IEscalationMatrixRepo
{
    private readonly IDbConnectionFactory _factory;
    public EscalationMatrixRepo(IDbConnectionFactory factory) => _factory = factory;

    private const string SelectCols = @"
        m.ID              AS ""Id"",
        m.REQUEST_TYPE_ID AS ""RequestTypeId"",
        m.ACTIVITY_ID     AS ""ActivityId"",
        m.UNIT_ID         AS ""UnitId"",
        m.LEVEL_NO        AS ""LevelNo"",
        m.PF_NUMBER       AS ""PfNumber"",
        m.IP_NUMBER       AS ""IpNumber"",
        m.GENERIC_MAIL    AS ""GenericMail"",
        e.EMP_NAME        AS ""EmpName"",
        e.SCALE           AS ""Scale"",
        e.EMP_ROLE        AS ""EmpRole"",
        e.EMAIL           AS ""Email"",
        e.MOBILE          AS ""Mobile"",
        rt.NAME           AS ""RequestTypeName"",
        ut.NAME           AS ""UnitTypeName"",
        u.NAME            AS ""UnitName"",
        v.NAME            AS ""VerticalName"",
        d.NAME            AS ""DepartmentName"",
        a.NAME            AS ""ActivityName"",
        m.IS_ACTIVE       AS ""IsActive""";

    private const string FromJoins = @"
        FROM RP_ESCALATION_MATRIX m
        LEFT JOIN RP_M_EMPLOYEE     e  ON e.PF_NUMBER    = m.PF_NUMBER
        JOIN      RP_M_ACTIVITY     a  ON a.ID           = m.ACTIVITY_ID
        LEFT JOIN RP_M_DEPARTMENT   d  ON d.ID           = a.DEPARTMENT_ID
        LEFT JOIN RP_M_VERTICAL     v  ON v.ID           = d.VERTICAL_ID
        LEFT JOIN RP_M_UNIT         u  ON u.ID           = m.UNIT_ID
        LEFT JOIN RP_M_UNIT_TYPE    ut ON ut.ID          = u.UNIT_TYPE_ID
        LEFT JOIN RP_M_REQUEST_TYPE rt ON rt.ID          = m.REQUEST_TYPE_ID";

    public async Task<IReadOnlyList<EscalationMatrixEntry>> GetByActivityAsync(long activityId, long? unitId, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = $@"SELECT {SelectCols} {FromJoins}
                     WHERE m.ACTIVITY_ID = :activityId 
                       AND (:unitId IS NULL OR m.UNIT_ID = :unitId OR m.UNIT_ID IS NULL)
                     ORDER BY m.LEVEL_NO, e.EMP_NAME";
        var rows = await c.QueryAsync<EscalationMatrixEntry>(new CommandDefinition(sql, new { activityId, unitId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<EscalationMatrixEntry>> GetAllAsync(CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = $@"SELECT {SelectCols} {FromJoins}
                     WHERE m.IS_ACTIVE = 1
                     ORDER BY v.NAME, d.NAME, a.NAME, m.LEVEL_NO";
        var rows = await c.QueryAsync<EscalationMatrixEntry>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<EmployeeViewRecord?> LookupEmployeeAsync(string pfNumber, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = @"SELECT PF_NUMBER AS ""PfNumber"", EMP_NAME AS ""Name"", SCALE AS ""Scale"",
                           EMP_ROLE AS ""Role"", EMAIL AS ""Email"", MOBILE AS ""Mobile""
                    FROM RP_M_EMPLOYEE WHERE PF_NUMBER = :pf AND IS_ACTIVE = 1";
        return await c.QuerySingleOrDefaultAsync<EmployeeViewRecord>(
            new CommandDefinition(sql, new { pf = pfNumber }, cancellationToken: ct));
    }

    public async Task<long> UpsertAsync(EscalationMatrixEntry e, string user_id, CancellationToken ct = default)
    {
        using var c = (OracleConnection)await _factory.OpenAsync(ct);
        var sql = @"
            MERGE INTO RP_ESCALATION_MATRIX t
            USING (SELECT :activityId AS ACTIVITY_ID, :unitId AS UNIT_ID, :levelNo AS LEVEL_NO, :pf AS PF_NUMBER FROM dual) s
            ON (t.ACTIVITY_ID = s.ACTIVITY_ID
                AND NVL(t.UNIT_ID,-1) = NVL(s.UNIT_ID,-1)
                AND t.LEVEL_NO = s.LEVEL_NO
                AND t.PF_NUMBER = s.PF_NUMBER)
            WHEN MATCHED THEN UPDATE SET
                t.REQUEST_TYPE_ID = :requestTypeId, t.IP_NUMBER = :ipNumber, t.GENERIC_MAIL = :genericMail,
                t.IS_ACTIVE = 1, t.UPDATED_BY = :user_id, t.UPDATED_AT = SYSTIMESTAMP
            WHEN NOT MATCHED THEN
                INSERT (ID, REQUEST_TYPE_ID, ACTIVITY_ID, UNIT_ID, LEVEL_NO, PF_NUMBER, IP_NUMBER, GENERIC_MAIL, IS_ACTIVE, CREATED_BY, CREATED_AT)
                VALUES (RP_ESCALATION_SEQ.NEXTVAL, :requestTypeId, :activityId, :unitId, :levelNo, :pf, :ipNumber, :genericMail, 1, :user_id, SYSTIMESTAMP)";
        await c.ExecuteAsync(new CommandDefinition(sql, new
        {
            requestTypeId = e.RequestTypeId,
            activityId = e.ActivityId,
            unitId = e.UnitId,
            levelNo = e.LevelNo,
            pf = e.PfNumber,
            ipNumber = e.IpNumber,
            genericMail = e.GenericMail,
            user_id
        }, cancellationToken: ct));
        return e.Id;
    }

    public async Task DeleteAsync(long id, string user_id, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = @"UPDATE RP_ESCALATION_MATRIX SET IS_ACTIVE = 0, UPDATED_BY = :user_id, UPDATED_AT = SYSTIMESTAMP
                    WHERE ID = :id";
        await c.ExecuteAsync(new CommandDefinition(sql, new { id, user_id }, cancellationToken: ct));
    }

    public async Task<int> BulkImportAsync(IEnumerable<EscalationMatrixEntry> rows, string user, CancellationToken ct = default)
    {
        var n = 0;
        foreach (var r in rows) { await UpsertAsync(r, user, ct); n++; }
        return n;
    }

    // ── Name → id resolvers (ID-free upload) ───────────────────────────────
    public Task<long?> ResolveActivityIdAsync(string name, CancellationToken ct = default)
        => ResolveAsync("RP_M_ACTIVITY", name, ct);
    public Task<long?> ResolveUnitIdAsync(string name, CancellationToken ct = default)
        => ResolveAsync("RP_M_UNIT", name, ct);
    public Task<long?> ResolveRequestTypeIdAsync(string name, CancellationToken ct = default)
        => ResolveAsync("RP_M_REQUEST_TYPE", name, ct);

    private async Task<long?> ResolveAsync(string table, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        using var c = await _factory.OpenAsync(ct);
        // Match by NAME or CODE (case-insensitive)
        var sql = $@"SELECT ID FROM {table}
                     WHERE UPPER(NAME) = UPPER(:n) OR UPPER(CODE) = UPPER(:n)
                     FETCH FIRST 1 ROWS ONLY";
        return await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(sql, new { n = name.Trim() }, cancellationToken: ct));
    }
}
