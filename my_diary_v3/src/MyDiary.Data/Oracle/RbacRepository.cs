using MyDiary.Core;
using MyDiary.Core.Models;
using MyDiary.Core.Abstractions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Data.Oracle;

/// <summary>
/// RBAC module / menu / permission queries (migrated from Project A's RbacRepo).
/// All queries are parameterized — no dynamic SQL concatenation.
/// </summary>
public sealed class RbacRepository : IRbacRepository
{
    private readonly string _connString;
    private readonly ILogger<RbacRepository> _logger;

    public RbacRepository(IConfiguration config, ILogger<RbacRepository> logger)
    {
        _connString = config.GetConnectionString("MyDiaryDBConnection")
            ?? throw new InvalidOperationException("Missing 'MyDiaryDBConnection' connection string.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<AppModule>> ListModulesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, CODE, DISPLAY_NAME AS DisplayName, PARENT_ID AS ParentId,
                   KIND AS Kind, SORT_ORDER AS SortOrder, IS_ACTIVE AS IsActive
            FROM MD_RBAC_MODULE
            ORDER BY SORT_ORDER, CODE
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<AppModule>(new CommandDefinition(sql, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing RBAC modules");
            throw;
        }
    }

    public async Task<IReadOnlyList<AppMenu>> ListMenusAsync(long moduleId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, MODULE_ID AS ModuleId, LABEL, HREF, ICON, SORT_ORDER AS SortOrder, IS_ACTIVE AS IsActive
            FROM MD_RBAC_MENU
            WHERE MODULE_ID = :ModuleId AND IS_ACTIVE = 1
            ORDER BY SORT_ORDER
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<AppMenu>(new CommandDefinition(sql, new { ModuleId = moduleId }, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing menus for ModuleId={ModuleId}", moduleId);
            throw;
        }
    }

    public async Task<IReadOnlyList<RoleModulePerm>> ListRolePermsAsync(long roleId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, ROLE_ID AS RoleId, MODULE_ID AS ModuleId,
                   CAN_VIEW AS CanView, CAN_ADD AS CanAdd, CAN_MODIFY AS CanModify,
                   CAN_DELETE AS CanDelete, CAN_AUTHORIZE AS CanAuthorize
            FROM MD_ROLE_MODULE_PERM
            WHERE ROLE_ID = :RoleId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<RoleModulePerm>(new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing role permissions for RoleId={RoleId}", roleId);
            throw;
        }
    }

    public async Task<IReadOnlyList<UserModulePerm>> ListUserPermsAsync(string empCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID, EMPL_CODE AS EmpCode, MODULE_ID AS ModuleId,
                   CAN_VIEW AS CanView, CAN_ADD AS CanAdd, CAN_MODIFY AS CanModify,
                   CAN_DELETE AS CanDelete, CAN_AUTHORIZE AS CanAuthorize, IS_DENY AS IsDeny
            FROM MD_USER_MODULE_PERM
            WHERE EMPL_CODE = :EmpCode
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var rows = await conn.QueryAsync<UserModulePerm>(new CommandDefinition(sql, new { EmpCode = empCode }, cancellationToken: ct));
            return rows.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing user permissions for EmpCode={EmpCode}", empCode);
            throw;
        }
    }

    public async Task<bool> HasPermissionAsync(
        string empCode, IEnumerable<RoleCode> roles, string moduleCode,
        PermAction action, CancellationToken ct = default)
    {
        // 1. Check explicit user deny override
        const string denyCheck = """
            SELECT COUNT(1) FROM MD_USER_MODULE_PERM p
            JOIN MD_RBAC_MODULE m ON m.ID = p.MODULE_ID
            WHERE p.EMPL_CODE = :EmpCode AND m.CODE = :ModuleCode AND p.IS_DENY = 1
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            var denied = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(denyCheck, new { EmpCode = empCode, ModuleCode = moduleCode }, cancellationToken: ct));
            if (denied > 0) return false;

            // 2. Check explicit user grant
            var col = ColumnForAction(action);
            var userGrant = $"""
                SELECT COUNT(1) FROM MD_USER_MODULE_PERM p
                JOIN MD_RBAC_MODULE m ON m.ID = p.MODULE_ID
                WHERE p.EMPL_CODE = :EmpCode AND m.CODE = :ModuleCode AND p.IS_DENY = 0 AND p.{col} = 1
                """;

            var granted = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(userGrant, new { EmpCode = empCode, ModuleCode = moduleCode }, cancellationToken: ct));
            if (granted > 0) return true;

            // 3. Check role-level permission
            var roleCodes = roles.Select(r => (int)r).ToArray();
            if (roleCodes.Length == 0) return false;

            // Build parameterized IN clause — Dapper handles array expansion
            var roleGrant = $"""
                SELECT COUNT(1) FROM MD_ROLE_MODULE_PERM rp
                JOIN MD_RBAC_MODULE m ON m.ID = rp.MODULE_ID
                JOIN MD_ROLE r ON r.ID = rp.ROLE_ID
                WHERE m.CODE = :ModuleCode AND r.CODE IN :RoleCodes AND rp.{col} = 1
                """;

            var roleGranted = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(roleGrant, new { ModuleCode = moduleCode, RoleCodes = roleCodes }, cancellationToken: ct));
            return roleGranted > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for EmpCode={EmpCode}, Module={ModuleCode}, Action={Action}", empCode, moduleCode, action);
            throw;
        }
    }

    private static string ColumnForAction(PermAction action) => action switch
    {
        PermAction.View      => "CAN_VIEW",
        PermAction.Add       => "CAN_ADD",
        PermAction.Modify    => "CAN_MODIFY",
        PermAction.Delete    => "CAN_DELETE",
        PermAction.Authorize => "CAN_AUTHORIZE",
        _                    => "CAN_VIEW"
    };

    // ── Module CRUD ──────────────────────────────────────────────────────────

    public async Task<long> InsertModuleAsync(AppModule m, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO MD_RBAC_MODULE (CODE, DISPLAY_NAME, PARENT_ID, KIND, SORT_ORDER, IS_ACTIVE)
            VALUES (:Code, :DisplayName, :ParentId, :Kind, :SortOrder, 1)
            RETURNING ID INTO :NewId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.OpenAsync(ct);
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("Code",        m.Code));
            cmd.Parameters.Add(new OracleParameter("DisplayName", m.DisplayName));
            cmd.Parameters.Add(new OracleParameter("ParentId",    (object?)m.ParentId ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("Kind",        (int)m.Kind));
            cmd.Parameters.Add(new OracleParameter("SortOrder",   m.SortOrder));
            var outParam = new OracleParameter("NewId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);
            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt64(outParam.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting RBAC module Code={Code}", m.Code);
            throw;
        }
    }

    public async Task UpdateModuleAsync(AppModule m, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE MD_RBAC_MODULE
            SET CODE = :Code, DISPLAY_NAME = :DisplayName, KIND = :Kind, SORT_ORDER = :SortOrder
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { m.Code, m.DisplayName, Kind = (int)m.Kind, m.SortOrder, m.Id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RBAC module Id={ModuleId}", m.Id);
            throw;
        }
    }

    public async Task DeleteModuleAsync(long id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM MD_RBAC_MODULE WHERE ID = :Id";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting RBAC module Id={ModuleId}", id);
            throw;
        }
    }

    // ── Menu CRUD ────────────────────────────────────────────────────────────

    public async Task<long> InsertMenuAsync(AppMenu m, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO MD_RBAC_MENU (MODULE_ID, LABEL, HREF, ICON, SORT_ORDER, IS_ACTIVE)
            VALUES (:ModuleId, :Label, :Href, :Icon, :SortOrder, 1)
            RETURNING ID INTO :NewId
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.OpenAsync(ct);
            using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("ModuleId",  m.ModuleId));
            cmd.Parameters.Add(new OracleParameter("Label",     m.Label));
            cmd.Parameters.Add(new OracleParameter("Href",      (object?)m.Href ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("Icon",      (object?)m.Icon ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("SortOrder", m.SortOrder));
            var outParam = new OracleParameter("NewId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outParam);
            await cmd.ExecuteNonQueryAsync(ct);
            return Convert.ToInt64(outParam.Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting menu for ModuleId={ModuleId}", m.ModuleId);
            throw;
        }
    }

    public async Task UpdateMenuAsync(AppMenu m, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE MD_RBAC_MENU
            SET LABEL = :Label, HREF = :Href, ICON = :Icon, SORT_ORDER = :SortOrder
            WHERE ID = :Id
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { m.Label, m.Href, m.Icon, m.SortOrder, m.Id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating menu Id={MenuId}", m.Id);
            throw;
        }
    }

    public async Task SetMenuActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        const string sql = "UPDATE MD_RBAC_MENU SET IS_ACTIVE = :Active WHERE ID = :Id";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql,
                new { Active = isActive ? 1 : 0, Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting menu active state for Id={MenuId}, IsActive={IsActive}", id, isActive);
            throw;
        }
    }

    public async Task DeleteMenuAsync(long id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM MD_RBAC_MENU WHERE ID = :Id";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting menu Id={MenuId}", id);
            throw;
        }
    }

    // ── Permission CRUD ──────────────────────────────────────────────────────

    public async Task UpsertRolePermAsync(RoleModulePerm p, CancellationToken ct = default)
    {
        const string sql = """
            MERGE INTO MD_ROLE_MODULE_PERM t
            USING (SELECT :RoleId AS ROLE_ID, :ModuleId AS MODULE_ID FROM DUAL) s
            ON (t.ROLE_ID = s.ROLE_ID AND t.MODULE_ID = s.MODULE_ID)
            WHEN MATCHED THEN UPDATE SET
                CAN_VIEW = :CanView, CAN_ADD = :CanAdd, CAN_MODIFY = :CanModify,
                CAN_DELETE = :CanDelete, CAN_AUTHORIZE = :CanAuthorize
            WHEN NOT MATCHED THEN INSERT
                (ROLE_ID, MODULE_ID, CAN_VIEW, CAN_ADD, CAN_MODIFY, CAN_DELETE, CAN_AUTHORIZE)
            VALUES
                (:RoleId, :ModuleId, :CanView, :CanAdd, :CanModify, :CanDelete, :CanAuthorize)
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                p.RoleId, p.ModuleId,
                CanView      = p.CanView      ? 1 : 0,
                CanAdd       = p.CanAdd       ? 1 : 0,
                CanModify    = p.CanModify    ? 1 : 0,
                CanDelete    = p.CanDelete    ? 1 : 0,
                CanAuthorize = p.CanAuthorize ? 1 : 0
            }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting role permission for RoleId={RoleId}, ModuleId={ModuleId}", p.RoleId, p.ModuleId);
            throw;
        }
    }

    public async Task UpsertUserPermAsync(UserModulePerm p, CancellationToken ct = default)
    {
        const string sql = """
            MERGE INTO MD_USER_MODULE_PERM t
            USING (SELECT :EmpCode AS EMPL_CODE, :ModuleId AS MODULE_ID FROM DUAL) s
            ON (t.EMPL_CODE = s.EMPL_CODE AND t.MODULE_ID = s.MODULE_ID)
            WHEN MATCHED THEN UPDATE SET
                CAN_VIEW = :CanView, CAN_ADD = :CanAdd, CAN_MODIFY = :CanModify,
                CAN_DELETE = :CanDelete, CAN_AUTHORIZE = :CanAuthorize, IS_DENY = :IsDeny
            WHEN NOT MATCHED THEN INSERT
                (EMPL_CODE, MODULE_ID, CAN_VIEW, CAN_ADD, CAN_MODIFY, CAN_DELETE, CAN_AUTHORIZE, IS_DENY)
            VALUES
                (:EmpCode, :ModuleId, :CanView, :CanAdd, :CanModify, :CanDelete, :CanAuthorize, :IsDeny)
            """;

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                p.EmpCode, p.ModuleId,
                CanView      = p.CanView      ? 1 : 0,
                CanAdd       = p.CanAdd       ? 1 : 0,
                CanModify    = p.CanModify    ? 1 : 0,
                CanDelete    = p.CanDelete    ? 1 : 0,
                CanAuthorize = p.CanAuthorize ? 1 : 0,
                IsDeny       = p.IsDeny       ? 1 : 0
            }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting user permission for EmpCode={EmpCode}, ModuleId={ModuleId}", p.EmpCode, p.ModuleId);
            throw;
        }
    }

    public async Task DeleteUserPermAsync(long id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM MD_USER_MODULE_PERM WHERE ID = :Id";

        try
        {
            await using var conn = new OracleConnection(_connString);
            await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user permission Id={PermId}", id);
            throw;
        }
    }
}
