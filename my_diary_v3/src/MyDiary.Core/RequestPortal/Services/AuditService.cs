using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

public sealed class AuditService : IAuditService
{
    private readonly IAuditRepo _repo;

    public AuditService(IAuditRepo repo) => _repo = repo;

    public async Task LogAsync(string entity, long entityId, string action, string? actorEmpCode, object? oldVal, object? newVal, string? ip = null, string? userAgent = null, CancellationToken ct = default)
    {
        var oldJson = oldVal is null ? null : JsonSerializer.Serialize(oldVal);
        var newJson = newVal is null ? null : JsonSerializer.Serialize(newVal);

        var prev = await _repo.GetLastHashAsync(ct);
        var canon = $"{entity}|{entityId}|{action}|{actorEmpCode}|{DateTime.UtcNow:O}|{oldJson}|{newJson}|{ip}|{userAgent}";
        var hash = ComputeHash(prev, canon);

        await _repo.InsertAsync(new AuditLog
        {
            Entity = entity, EntityId = entityId, Action = action,
            ActorEmpCode = actorEmpCode, ActedAt = DateTime.UtcNow,
            OldJson = oldJson, NewJson = newJson, Ip = ip, UserAgent = userAgent,
            HashPrev = prev, HashCurr = hash
        }, ct);
    }

    public Task<bool> VerifyChainAsync(CancellationToken ct = default) => _repo.VerifyChainAsync(ct);

    private static string ComputeHash(string? prev, string canonical)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes((prev ?? "") + "" + canonical);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}
