using MyDiary.Core.Models;

namespace MyDiary.Core.Abstractions;

public interface IRbacRepository
{
    Task<IReadOnlyList<AppModule>>    ListModulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AppMenu>>      ListMenusAsync(long moduleId, CancellationToken ct = default);
    Task<IReadOnlyList<RoleModulePerm>> ListRolePermsAsync(long roleId, CancellationToken ct = default);
    Task<IReadOnlyList<UserModulePerm>> ListUserPermsAsync(string empCode, CancellationToken ct = default);

    Task<bool> HasPermissionAsync(string empCode, IEnumerable<RoleCode> roles, string moduleCode, PermAction action, CancellationToken ct = default);

    // Admin CRUD
    Task<long> InsertModuleAsync(AppModule m, CancellationToken ct = default);
    Task UpdateModuleAsync(AppModule m, CancellationToken ct = default);
    Task DeleteModuleAsync(long id, CancellationToken ct = default);

    Task<long> InsertMenuAsync(AppMenu m, CancellationToken ct = default);
    Task UpdateMenuAsync(AppMenu m, CancellationToken ct = default);
    Task SetMenuActiveAsync(long id, bool isActive, CancellationToken ct = default);
    Task DeleteMenuAsync(long id, CancellationToken ct = default);

    Task UpsertRolePermAsync(RoleModulePerm p, CancellationToken ct = default);
    Task UpsertUserPermAsync(UserModulePerm p, CancellationToken ct = default);
    Task DeleteUserPermAsync(long id, CancellationToken ct = default);
}
