namespace MyDiary.Web.Features.ECircular.Services;

/// <summary>
/// Audit service for logging Circular/Policy document access.
/// </summary>
public interface ICircularAuditService
{
    /// <summary>Logs a document view/download action.</summary>
    Task LogDocumentAccessAsync(CircularAuditEntry entry, CancellationToken ct = default);

    /// <summary>Logs a search action with criteria.</summary>
    Task LogSearchAsync(CircularAuditEntry entry, CancellationToken ct = default);
}

/// <summary>
/// Represents a single audit log entry for circular/policy access.
/// </summary>
public sealed class CircularAuditEntry
{
    public string EmployeePfNo { get; set; } = "";
    public string? DocumentId { get; set; }
    public string? CircularNo { get; set; }
    public string ActionType { get; set; } = "VIEW"; // VIEW, DOWNLOAD, SEARCH
    public string Module { get; set; } = "CIRCULARS"; // CIRCULARS, POLICIES, ECIRCULAR_SEARCH
    public string? IpAddress { get; set; }
    public string? WatermarkRef { get; set; }
    public string? SearchCriteria { get; set; }
    public string? CircularType { get; set; }
    public string? Department { get; set; }
    public string? Subject { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? UserAgent { get; set; }
}
