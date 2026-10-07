using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

public sealed class UserRepo : IUserRepo
{
    private readonly AdoUnitOfWork _uow;
    private readonly IOrganisationsDbFactory _orgFactory;
    private readonly IDbConnectionFactory _factory;

    public UserRepo(AdoUnitOfWork uow, IOrganisationsDbFactory orgFactory, IDbConnectionFactory factory)
    {
        _uow = uow;
        _orgFactory = orgFactory;
        _factory = factory;
    }

    public async Task<AppUser?> GetByAdAsync(string sam, CancellationToken ct = default)
    {
        await using var orgConn = await _orgFactory.OpenAsync(ct);
        using var cmd = orgConn.Cmd(
            "select pf_no as ID,pf_no as EMP_CODE,pf_no as AD_SAM,name,email_id as EMAIL,contact_no as MOBILE,(case when empl_status='A' then 1 else 0 end) as IS_ACTIVE from staff_details WHERE pf_no=:p_sam");
        cmd.Parameters.AddIn("p_sam", sam);
        return await cmd.QueryOneAsync(MapUser, ct);
    }

    public async Task<AppUser?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await using var orgConn = await _orgFactory.OpenAsync(ct);
        using var cmd = orgConn.Cmd(
            "select pf_no as ID,pf_no as EMP_CODE,pf_no as AD_SAM,name,email_id as EMAIL,contact_no as MOBILE,(case when empl_status='A' then 1 else 0 end) as IS_ACTIVE from staff_details WHERE pf_no=:p_id");
        cmd.Parameters.AddIn("p_id", id);
        return await cmd.QueryOneAsync(MapUser, ct);
    }

    public async Task<long> InsertAsync(AppUser u, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "INSERT INTO RP_USER(ID,EMP_CODE,AD_SAM,NAME,EMAIL,MOBILE,IS_ACTIVE) VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_emp,:p_sam,:p_name,:p_email,:p_mobile,1) RETURNING ID INTO :p_id",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_emp", u.EmpCode); cmd.Parameters.AddIn("p_sam", u.AdSamAccountName);
        cmd.Parameters.AddIn("p_name", u.Name); cmd.Parameters.AddIn("p_email", u.Email);
        cmd.Parameters.AddIn("p_mobile", u.Mobile);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateProfileAsync(long id, string? name, string? email, CancellationToken ct = default)
    {
        // Called from UserDirectory.EnsureUserAsync on the login/nav hot path (when a
        // user's name/email changed), OUTSIDE any transaction — the only _uow tx there
        // wraps the new-user InsertAsync, not this update. A single autocommit UPDATE,
        // so use a SHORT-LIVED pooled connection rather than pinning the circuit _uow
        // connection for the session.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.Cmd(
            "UPDATE RP_USER SET NAME=COALESCE(:p_name,NAME),EMAIL=COALESCE(:p_email,EMAIL),UPDATED_AT=SYSTIMESTAMP WHERE ID=:p_id");
        cmd.Parameters.AddIn("p_name", OracleDbType.Varchar2, name);
        cmd.Parameters.AddIn("p_email", OracleDbType.Varchar2, email);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<RoleCode>> GetEffectiveRolesAsync(string empCode, CancellationToken ct = default)
    {
        // Runs for EVERY authenticated user on every Blazor circuit (via
        // CurrentUserService.Resolve → MainLayout). These are pure reads with no
        // shared transaction, so they use a SHORT-LIVED pooled connection that is
        // released the moment this method returns — rather than the circuit-scoped
        // _uow connection, which would stay pinned for the user's whole session and
        // exhaust the Oracle session pool under load. All six reads share this one
        // connection and it is disposed by `await using` at method end.
        await using var conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        var result = new List<RoleCode>();

        // Tier 1 — org role from HRMS view
        using (var cmd = conn.Cmd(
            "SELECT USER_LEVEL,IS_HEAD FROM VW_STAFF_USER_SUMMARY WHERE EMPLOYEE_CODE=:p_emp"))
        {
            cmd.Parameters.AddIn("p_emp", empCode);
            var rows = await cmd.QueryAsync(r => (UserLevel: StrN(r, "USER_LEVEL"), IsHead: Int(r, "IS_HEAD")), ct);
            foreach (var row in rows.Take(1))
                if (row.UserLevel is not null)
                    result.Add(RoleDerivation.FromView(row.UserLevel, row.IsHead == 1));
        }

        // SuperAdmin
        using (var cmd = conn.Cmd(
            "SELECT COUNT(*) FROM RP_RBAC_SUPER_ADMIN WHERE EMP_CODE=:p_emp"))
        {
            cmd.Parameters.AddIn("p_emp", empCode);
            if (await cmd.ScalarAsync<int>(ct) > 0) result.Add(RoleCode.SuperAdmin);
        }

        // AppAdmin
        using (var cmd = conn.Cmd(
            "SELECT COUNT(*) FROM RP_RBAC_APP_ADMIN WHERE EMP_CODE=:p_emp AND IS_ACTIVE=1"))
        {
            cmd.Parameters.AddIn("p_emp", empCode);
            if (await cmd.ScalarAsync<int>(ct) > 0) result.Add(RoleCode.AppAdmin);
        }

        // Vertical level assignments
        using (var cmd = conn.Cmd(
            "SELECT LEVEL_CODE FROM RP_RBAC_VERTICAL_LEVEL WHERE EMP_CODE=:p_emp AND IS_ACTIVE=1"))
        {
            cmd.Parameters.AddIn("p_emp", empCode);
            var levels = await cmd.QueryAsync(r => Str(r, "LEVEL_CODE"), ct);
            foreach (var l in levels)
                if (Enum.TryParse<RoleCode>(l, out var rc)) result.Add(rc);
        }

        // Vertical admin
        using (var cmd = conn.Cmd(
            "SELECT COUNT(*) FROM RP_RBAC_VERTICAL_ADMIN WHERE EMP_CODE=:p_emp AND IS_ACTIVE=1"))
        {
            cmd.Parameters.AddIn("p_emp", empCode);
            if (await cmd.ScalarAsync<int>(ct) > 0) result.Add(RoleCode.CoVertAdmin);
        }

        // Inbound delegations
        using (var cmd = conn.Cmd(
            "SELECT ROLE_CODE FROM RP_DELEGATION WHERE TO_EMP_CODE=:p_emp AND VALID_FROM<=SYSTIMESTAMP AND VALID_TO>SYSTIMESTAMP"))
        {
            cmd.Parameters.AddIn("p_emp", empCode);
            var codes = await cmd.QueryAsync(r => Str(r, "ROLE_CODE"), ct);
            foreach (var d in codes)
                if (Enum.TryParse<RoleCode>(d, out var rc)) result.Add(rc);
        }

        if (result.Count == 0) result.Add(RoleCode.BranchUser);
        return result.Distinct().ToList();
    }

    public async Task<IReadOnlyList<AppUser>> SearchAsync(string q, int take, CancellationToken ct = default)
    {
        await using var orgConn = await _orgFactory.OpenAsync(ct);
        using var cmd = orgConn.Cmd(@"
            SELECT * FROM (
            select pf_no as ID,pf_no as EMP_CODE,pf_no as AD_SAM,name,email_id as EMAIL,contact_no as MOBILE,(case when empl_status='A' then 1 else 0 end) 
            as IS_ACTIVE from staff_details WHERE
            empl_status='A'
            AND (LOWER(NAME) LIKE LOWER('%' || :p_q || '%') OR PF_NO LIKE '%' || :p_q || '%' OR LOWER(EMAIL_ID) LIKE LOWER('%' || :p_q || '%'))
            ORDER BY NAME
            ) WHERE ROWNUM<=:p_take");
        cmd.Parameters.AddIn("p_q", q);
        cmd.Parameters.AddIn("p_take", take);
        return await cmd.QueryAsync(MapUser, ct);
    }

    public async Task<IReadOnlyList<AppUser>> GetByEmpCodesAsync(IEnumerable<string> empCodes, CancellationToken ct = default)
    {
        var codes = empCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (codes.Count == 0) return Array.Empty<AppUser>();

        await using var orgConn = await _orgFactory.OpenAsync(ct);
        // Oracle IN clause limit is 1000; chunk if needed
        var results = new List<AppUser>();
        foreach (var chunk in Chunk(codes, 900))
        {
            var inList = string.Join(",", chunk.Select((_, i) => $":p{i}"));
            using var cmd = orgConn.Cmd(
                $"select pf_no as ID,pf_no as EMP_CODE,pf_no as AD_SAM,name,email_id as EMAIL,contact_no as MOBILE,(case when empl_status='A' then 1 else 0 end) as IS_ACTIVE from staff_details WHERE pf_no IN ({inList})");
            for (int i = 0; i < chunk.Count; i++)
                cmd.Parameters.AddIn($"p{i}", chunk[i]);
            results.AddRange(await cmd.QueryAsync(MapUser, ct));
        }
        return results;
    }

    private static List<List<T>> Chunk<T>(List<T> source, int size)
    {
        var chunks = new List<List<T>>();
        for (int i = 0; i < source.Count; i += size)
            chunks.Add(source.GetRange(i, Math.Min(size, source.Count - i)));
        return chunks;
    }

    public async Task<long?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("SELECT ID FROM RP_ROLE WHERE UPPER(CODE)=UPPER(:p_code)", _uow.OracleTx);
        cmd.Parameters.AddIn("p_code", roleCode);
        return await cmd.ScalarAsync<long?>(ct);
    }

    public async Task<IReadOnlyList<AppRole>> ListRolesAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("SELECT ID,CODE,NAME FROM RP_ROLE ORDER BY ID", _uow.OracleTx);
        var result = new List<AppRole>();
        var rows = await cmd.QueryAsync(r => (Id: Long(r, "ID"), Code: Str(r, "CODE"), Name: Str(r, "NAME")), ct);
        foreach (var row in rows)
        {
            // RP_ROLE.CODE is stored snake_case (e.g. "CO_USER", "BRANCH_HEAD") but
            // RoleCode enum members are PascalCase with no separators (CoUser,
            // BranchHead) — strip underscores before parsing, or every row silently
            // fails to match and the Role Permissions dropdown renders empty.
            var normalized = row.Code.Replace("_", "");
            if (Enum.TryParse<RoleCode>(normalized, true, out var rc))
                result.Add(new AppRole { Id = row.Id, Code = rc, Name = row.Name });
        }
        return result;
    }

    public async Task<IReadOnlyList<Delegation>> ListDelegationsAsync(bool currentOnly, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        var extra = currentOnly ? "WHERE VALID_FROM<=SYSTIMESTAMP AND VALID_TO>SYSTIMESTAMP" : "";
        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT ID,FROM_EMP_CODE,TO_EMP_CODE,ROLE_CODE,VERTICAL_CODE,VALID_FROM,VALID_TO,REASON,MODIFIED_BY_EMP,MODIFIED_AT
            FROM RP_DELEGATION {extra} ORDER BY VALID_FROM DESC", _uow.OracleTx);
        return await cmd.QueryAsync(MapDelegation, ct);
    }

    public async Task<long> InsertDelegationAsync(Delegation d, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_DELEGATION(ID,FROM_EMP_CODE,TO_EMP_CODE,ROLE_CODE,VERTICAL_CODE,VALID_FROM,VALID_TO,REASON)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_from,:p_to,:p_role,:p_vert,:p_vf,:p_vt,:p_reason)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", d.FromEmpCode); cmd.Parameters.AddIn("p_to", d.ToEmpCode);
        cmd.Parameters.AddIn("p_role", d.RoleCode); cmd.Parameters.AddIn("p_vert", d.VerticalCode);
        cmd.Parameters.AddIn("p_vf", d.ValidFrom); cmd.Parameters.AddIn("p_vt", d.ValidTo);
        cmd.Parameters.AddIn("p_reason", d.Reason);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct); return outId.OutId();
    }

    public async Task UpdateDelegationAsync(Delegation d, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            UPDATE RP_DELEGATION SET FROM_EMP_CODE=:p_from,TO_EMP_CODE=:p_to,ROLE_CODE=:p_role,
            VERTICAL_CODE=:p_vert,VALID_FROM=:p_vf,VALID_TO=:p_vt,REASON=:p_reason,
            MODIFIED_BY_EMP=:p_modby,MODIFIED_AT=:p_modat WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_from", d.FromEmpCode); cmd.Parameters.AddIn("p_to", d.ToEmpCode);
        cmd.Parameters.AddIn("p_role", d.RoleCode); cmd.Parameters.AddIn("p_vert", d.VerticalCode);
        cmd.Parameters.AddIn("p_vf", d.ValidFrom); cmd.Parameters.AddIn("p_vt", d.ValidTo);
        cmd.Parameters.AddIn("p_reason", d.Reason); cmd.Parameters.AddIn("p_modby", d.ModifiedByEmp);
        cmd.Parameters.AddIn("p_modat", OracleDbType.TimeStamp, d.ModifiedAt);
        cmd.Parameters.AddIn("p_id", d.Id); await cmd.ExecAsync(ct);
    }

    public async Task DeleteDelegationAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd("DELETE FROM RP_DELEGATION WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id); await cmd.ExecAsync(ct);
    }

    private static AppUser MapUser(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), EmpCode = Str(r, "EMP_CODE"), AdSamAccountName = Str(r, "AD_SAM"),
        Name = Str(r, "NAME"), Email = StrN(r, "EMAIL"), Mobile = StrN(r, "MOBILE"),
        IsActive = Bool(r, "IS_ACTIVE")
    };

    private static Delegation MapDelegation(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), FromEmpCode = Str(r, "FROM_EMP_CODE"), ToEmpCode = Str(r, "TO_EMP_CODE"),
        RoleCode = Str(r, "ROLE_CODE"), VerticalCode = StrN(r, "VERTICAL_CODE"),
        ValidFrom = Dt(r, "VALID_FROM"), ValidTo = Dt(r, "VALID_TO"),
        Reason = StrN(r, "REASON"), ModifiedByEmp = StrN(r, "MODIFIED_BY_EMP"),
        ModifiedAt = DtN(r, "MODIFIED_AT")
    };
}
