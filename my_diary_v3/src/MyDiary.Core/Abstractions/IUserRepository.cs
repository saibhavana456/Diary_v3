using MyDiary.Core.Models;

namespace MyDiary.Core.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> GetByEmplIdAsync(string emplId, CancellationToken ct = default);
    Task<AppUser?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<long>     InsertAsync(AppUser u, CancellationToken ct = default);
    Task           UpdateProfileAsync(long id, string? name, string? email, CancellationToken ct = default);

    Task<IReadOnlyList<RoleCode>> GetEffectiveRolesAsync(string emplId, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>>  SearchAsync(string query, int take, CancellationToken ct = default);

    Task<IReadOnlyList<AppRole>> ListRolesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Delegation>> ListDelegationsAsync(bool currentOnly, CancellationToken ct = default);
    Task<long>  InsertDelegationAsync(Delegation d, CancellationToken ct = default);
    Task        UpdateDelegationAsync(Delegation d, CancellationToken ct = default);
    Task        DeleteDelegationAsync(long id, CancellationToken ct = default);
}
