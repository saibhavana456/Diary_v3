using MyDiary.Core;
using MyDiary.Core.Models;
using MyDiary.Core.Abstractions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Data.Oracle;

/// <summary>
/// User and RBAC role queries against Oracle (MD_USER, MD_ROLE, MD_USER_ROLE tables).
/// Uses Dapper with parameterized queries only — no dynamic SQL construction.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly string _connString;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(IConfiguration config, ILogger<UserRepository> logger)
    {
        _connString = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing 'MyDiaryDBConnection' connection string.");
        _logger = logger;
    }

    public async Task<AppUser?> GetByEmplIdAsync(string emplId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EMPL_ID AS EmpCode, NAME, EMAIL, MOBILE, IS_ACTIVE AS IsActive
            FROM MD_USER
            WHERE EMPL_ID = :EmplId
              AND ROWNUM = 1
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            return await conn.QueryFirstOrDefaultAsync<AppUser>(new CommandDefinition(sql, new { EmplId = emplId }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user by EmplId={EmplId}", emplId);
            throw;
        }
    }

    public async Task<AppUser?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EMPL_ID AS EmpCode, NAME, EMAIL, MOBILE, IS_ACTIVE AS IsActive
            FROM MD_USER
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            return await conn.QueryFirstOrDefaultAsync<AppUser>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user by Id={UserId}", id);
            throw;
        }
    }

    public async Task<long> InsertAsync(AppUser u, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO MD_USER (EMPL_ID, NAME, EMAIL, MOBILE, IS_ACTIVE, CREATED_AT)
            VALUES (:EmplId, :Name, :Email, :Mobile, 1, SYSTIMESTAMP)
            RETURNING ID INTO :NewId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.OpenAsync(ct);

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("EmplId", u.EmpCode));
            cmd.Parameters.Add(new OracleParameter("Name",   u.Name));
            cmd.Parameters.Add(new OracleParameter("Email",  (object?)u.Email ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("Mobile", (object?)u.Mobile ?? DBNull.Value));
            var outParam = new OracleParameter("NewId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);

            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt64(outParam.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting user with EmplId={EmplId}", u.EmpCode);
            throw;
        }
    }

    public async Task UpdateProfileAsync(long id, string? name, string? email, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE MD_USER
            SET NAME = :Name, EMAIL = :Email, UPDATED_AT = SYSTIMESTAMP
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { Name = name, Email = email, Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for UserId={UserId}", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<RoleCode>> GetEffectiveRolesAsync(string emplId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.CODE
            FROM MD_USER_ROLE ur
            JOIN MD_ROLE r ON r.ID = ur.ROLE_ID
            JOIN MD_USER u ON u.ID = ur.USER_ID
            WHERE u.EMPL_ID = :EmplId
              AND ur.IS_ACTIVE = 1
              AND (ur.VALID_TO IS NULL OR ur.VALID_TO >= SYSDATE)
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var codes = await conn.QueryAsync<int>(new CommandDefinition(sql, new { EmplId = emplId }, cancellationToken: ct));
            return codes.Select(c => (RoleCode)c).ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching effective roles for EmplId={EmplId}", emplId);
            throw;
        }
    }

    public async Task<IReadOnlyList<AppUser>> SearchAsync(string query, int take, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EMPL_ID AS EmpCode, NAME, EMAIL, MOBILE, IS_ACTIVE AS IsActive
            FROM MD_USER
            WHERE IS_ACTIVE = 1
              AND (UPPER(NAME) LIKE UPPER(:Q) OR UPPER(EMPL_ID) LIKE UPPER(:Q))
              AND ROWNUM <= :Take
            ORDER BY NAME
            """;

        try
        {
            var pattern = $"%{query}%";
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<AppUser>(new CommandDefinition(sql, new { Q = pattern, Take = take }, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with query={Query}", query);
            throw;
        }
    }

    public async Task<IReadOnlyList<AppRole>> ListRolesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT ID, CODE, NAME FROM MD_ROLE ORDER BY CODE";

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<AppRole>(new CommandDefinition(sql, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing roles");
            throw;
        }
    }

    public async Task<IReadOnlyList<Delegation>> ListDelegationsAsync(bool currentOnly, CancellationToken ct = default)
    {
        var sql = """
            SELECT ID, FROM_EMPL_ID AS FromEmpCode, TO_EMPL_ID AS ToEmpCode,
                   ROLE_CODE AS RoleCode, VERTICAL_CODE AS VerticalCode,
                   VALID_FROM AS ValidFrom, VALID_TO AS ValidTo,
                   REASON, MODIFIED_BY_EMP AS ModifiedByEmp, MODIFIED_AT AS ModifiedAt
            FROM MD_DELEGATION
            """ + (currentOnly ? " WHERE VALID_FROM <= SYSDATE AND VALID_TO >= SYSDATE" : "") +
            " ORDER BY VALID_FROM DESC";

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<Delegation>(new CommandDefinition(sql, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing delegations (currentOnly={CurrentOnly})", currentOnly);
            throw;
        }
    }

    public async Task<long> InsertDelegationAsync(Delegation d, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO MD_DELEGATION
                (FROM_EMPL_ID, TO_EMPL_ID, ROLE_CODE, VERTICAL_CODE, VALID_FROM, VALID_TO, REASON, CREATED_AT)
            VALUES (:FromEmpCode, :ToEmpCode, :RoleCode, :VerticalCode, :ValidFrom, :ValidTo, :Reason, SYSTIMESTAMP)
            RETURNING ID INTO :NewId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.OpenAsync(ct);

            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("FromEmpCode",   d.FromEmpCode));
            cmd.Parameters.Add(new OracleParameter("ToEmpCode",     d.ToEmpCode));
            cmd.Parameters.Add(new OracleParameter("RoleCode",      d.RoleCode));
            cmd.Parameters.Add(new OracleParameter("VerticalCode",  (object?)d.VerticalCode ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("ValidFrom",     d.ValidFrom));
            cmd.Parameters.Add(new OracleParameter("ValidTo",       d.ValidTo));
            cmd.Parameters.Add(new OracleParameter("Reason",        (object?)d.Reason ?? DBNull.Value));
            var outParam = new OracleParameter("NewId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);

            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt64(outParam.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting delegation from {FromEmpCode} to {ToEmpCode}", d.FromEmpCode, d.ToEmpCode);
            throw;
        }
    }

    public async Task UpdateDelegationAsync(Delegation d, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE MD_DELEGATION
            SET VALID_TO = :ValidTo, REASON = :Reason,
                MODIFIED_BY_EMP = :ModifiedByEmp, MODIFIED_AT = SYSTIMESTAMP
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { d.ValidTo, d.Reason, d.ModifiedByEmp, d.Id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating delegation Id={DelegationId}", d.Id);
            throw;
        }
    }

    public async Task DeleteDelegationAsync(long id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM MD_DELEGATION WHERE ID = :Id";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting delegation Id={DelegationId}", id);
            throw;
        }
    }
}
