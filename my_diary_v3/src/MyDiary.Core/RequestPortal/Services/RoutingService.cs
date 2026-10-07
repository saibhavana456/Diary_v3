using RequestPortal.Core.Abstractions;

namespace RequestPortal.Core.Services;

public sealed class RoutingService : IRoutingService
{
    private readonly IRoutingRepo _routing;
    private readonly IRequestRepo _requests;

    public RoutingService(IRoutingRepo routing, IRequestRepo requests)
    {
        _routing = routing;
        _requests = requests;
    }

    public async Task<IReadOnlyList<string>> ResolveL1AssigneesAsync(long requestTypeId, long unitId, long verticalId, long departmentId, long activityId, CancellationToken ct = default)
    {
        var codes = await _routing.ResolveL1Async(requestTypeId, unitId, verticalId, departmentId, activityId, ct);
        if (codes.Count > 0) return codes;
        return await _routing.ResolveByRoleAsync(RoleCode.VertL1, unitId, verticalId, departmentId, ct);
    }

    public async Task<IReadOnlyList<string>> ResolveLevelAssigneesAsync(long requestId, int levelNo, CancellationToken ct = default)
    {
        var r = await _requests.GetAsync(requestId, ct)
            ?? throw new InvalidOperationException($"Request {requestId} not found");
        var role = levelNo switch
        {
            1 => RoleCode.VertL1,
            2 => RoleCode.VertL2,
            3 => RoleCode.VertL3,
            4 => RoleCode.VertL4,
            5 => RoleCode.VertL5,
            _ => throw new ArgumentOutOfRangeException(nameof(levelNo))
        };
        return await _routing.ResolveByRoleAsync(role, r.UnitId, r.VerticalId, r.DepartmentId, ct);
    }
}
