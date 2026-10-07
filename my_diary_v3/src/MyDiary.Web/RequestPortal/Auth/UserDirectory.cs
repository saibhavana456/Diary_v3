using MyDiary.Web.Core.Extensions;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Web.Auth;

public sealed class UserDirectory : IUserDirectory
{
    private readonly IUserRepo _users;
    private readonly IUnitOfWork _uow;

    public UserDirectory(IUserRepo users, IUnitOfWork uow)
    {
        _users = users; _uow = uow;
    }

    public async Task<AppUser?> EnsureUserAsync(string adSamAccountName, string? displayName, string? email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adSamAccountName)) return null;
        var u = await _users.GetByAdAsync(adSamAccountName, ct);
        if (u is not null)
        {
            if (!string.IsNullOrWhiteSpace(displayName) && displayName != u.Name
                || !string.IsNullOrWhiteSpace(email) && email != u.Email)
            {
                await _users.UpdateProfileAsync(u.Id, displayName, email, ct);
                u.Name = displayName ?? u.Name;
                u.Email = email ?? u.Email;
            }
            return u;
        }

        await _uow.BeginAsync(ct);
        try
        {
            var id = await _users.InsertAsync(new AppUser
            {
                AdSamAccountName = adSamAccountName,
                EmpCode = adSamAccountName,
                Name = displayName ?? adSamAccountName,
                Email = email,
                IsActive = true
            }, ct);
            await _uow.CommitAsync(ct);
            // return await _users.GetByIdAsync(id, ct);
            return await _users.GetByIdAsync(adSamAccountName, ct);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"UserDirectory: failed to insert new user for '{adSamAccountName}'");
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public Task<IReadOnlyList<RoleCode>> GetEffectiveRolesAsync(string empCode, CancellationToken ct = default)
        => _users.GetEffectiveRolesAsync(empCode, ct);
}
