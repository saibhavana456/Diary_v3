using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

/// <summary>
/// Interlink mappings that drive the cascading classification filters:
///   Request Type → Unit Type → Unit → Vertical → Department → Activity.
/// (UnitType→Unit, Vertical→Department, Department→Activity are plain FKs.)
/// </summary>
public interface IClassificationMapRepo
{
    /// <summary>Unit types mapped to a request type (for the cascade).</summary>
    Task<IReadOnlyList<UnitType>> GetUnitTypesForRequestTypeAsync(long requestTypeId, CancellationToken ct = default);

    /// <summary>Verticals mapped to a unit (for the cascade).</summary>
    Task<IReadOnlyList<Vertical>> GetVerticalsForUnitAsync(long unitId, CancellationToken ct = default);

    // ── Master-page management ─────────────────────────────────────────────
    Task<IReadOnlyList<long>> GetMappedUnitTypeIdsAsync(long requestTypeId, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetMappedVerticalIdsAsync(long unitId, CancellationToken ct = default);
    Task SetRequestTypeUnitTypesAsync(long requestTypeId, IEnumerable<long> unitTypeIds, CancellationToken ct = default);
    Task SetUnitVerticalsAsync(long unitId, IEnumerable<long> verticalIds, CancellationToken ct = default);
}
