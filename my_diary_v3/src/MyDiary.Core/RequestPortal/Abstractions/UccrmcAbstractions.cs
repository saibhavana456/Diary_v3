using RequestPortal.Core.Dtos;

namespace RequestPortal.Core.Abstractions;

/// <summary>
/// Data access for the UCCRMC alert integration. Resolves the fixed UCCRMC
/// classification chain and reads alert-ticket state for the status/dashboard
/// APIs. Ticket creation itself reuses the standard request repo/UoW.
/// </summary>
public interface IUccrmcRepo
{
    /// <summary>Resolve the seeded UCCRMC classification (by RP_08 codes).</summary>
    Task<UccrmcClassification?> GetClassificationAsync(CancellationToken ct = default);

    /// <summary>Latest alert ticket for a given UCCRMC Alert ID, or null.</summary>
    Task<UccrmcAlertStatus?> GetByAlertIdAsync(string alertId, CancellationToken ct = default);

    /// <summary>Closure / pendency aggregates for UCCRMC alert tickets.</summary>
    Task<UccrmcDashboard> GetDashboardAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

/// <summary>Resolved FK ids for the fixed UCCRMC alert classification.</summary>
public sealed record UccrmcClassification(
    long RequestTypeId, long UnitId, long VerticalId, long DepartmentId, long ActivityId);

/// <summary>
/// Orchestrates UCCRMC alert tickets: creation with an explicit assignee
/// (bypassing routing), status lookup, and dashboard aggregation.
/// </summary>
public interface IUccrmcService
{
    Task<CreateUccrmcAlertResponse> CreateAlertAsync(CreateUccrmcAlertRequest req, CancellationToken ct = default);
    Task<UccrmcAlertStatus?> GetStatusAsync(string alertId, CancellationToken ct = default);
    Task<UccrmcDashboard> GetDashboardAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

/// <summary>Shared constants for the UCCRMC integration.</summary>
public static class Uccrmc
{
    public const string Source = "UCCRMC";
}
