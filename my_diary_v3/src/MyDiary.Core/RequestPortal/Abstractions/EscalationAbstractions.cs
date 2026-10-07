using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

/// <summary>
/// Data access for the Escalation Matrix feature (Request Portal).
/// </summary>
public interface IEscalationMatrixRepo
{
    /// <summary>All L1–L5 mappings for an activity (optionally a specific unit), with
    /// employee contact details joined from the employee view.</summary>
    Task<IReadOnlyList<EscalationMatrixEntry>> GetByActivityAsync(long activityId, long? unitId, CancellationToken ct = default);

    /// <summary>Every mapping (for download/export).</summary>
    Task<IReadOnlyList<EscalationMatrixEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Look up an employee's Scale/Role/Email/Mobile by PF number.</summary>
    Task<EmployeeViewRecord?> LookupEmployeeAsync(string pfNumber, CancellationToken ct = default);

    /// <summary>Insert or update a single mapping row (returns its id).</summary>
    Task<long> UpsertAsync(EscalationMatrixEntry e, string user, CancellationToken ct = default);

    /// <summary>Soft-delete (deactivate) a mapping row.</summary>
    Task DeleteAsync(long id, string user, CancellationToken ct = default);

    /// <summary>Bulk import (upsert by activity+unit+level+PF) — used by the upload.</summary>
    Task<int> BulkImportAsync(IEnumerable<EscalationMatrixEntry> rows, string user, CancellationToken ct = default);

    // ── Name → id resolution for ID-free Excel upload ──────────────────────
    Task<long?> ResolveActivityIdAsync(string name, CancellationToken ct = default);
    Task<long?> ResolveUnitIdAsync(string name, CancellationToken ct = default);
    Task<long?> ResolveRequestTypeIdAsync(string name, CancellationToken ct = default);
}
