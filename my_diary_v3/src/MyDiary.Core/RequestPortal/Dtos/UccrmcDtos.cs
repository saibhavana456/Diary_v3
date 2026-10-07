namespace RequestPortal.Core.Dtos;

/// <summary>
/// Payload sent by the UCCRMC command-center application to raise an alert
/// ticket in the Request Portal. Identifiers are employee (PF) codes.
/// </summary>
public sealed record CreateUccrmcAlertRequest(
    string AlertId,             // UCCRMC alert identifier — stored on the ticket
    string AssigneeEmpCode,     // employee/PF code the ticket is assigned to (handler)
    string RequesterEmpCode,    // employee/PF code raising the ticket (IT requester)
    string? Subject = null,     // optional; defaults to "UCCRMC Alert {AlertId}"
    string? Description = null); // optional alert details

public sealed record CreateUccrmcAlertResponse(
    long RequestId,
    string ReqNo,
    string AlertId,
    string Status);

/// <summary>Status projection returned to UCCRMC for a raised alert.</summary>
public sealed record UccrmcAlertStatus(
    string AlertId,
    long RequestId,
    string ReqNo,
    string Status,
    int CurrentLevel,
    string RequesterEmpCode,
    string? AssigneeEmpCode,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    DateTime? SlaDueUtc,
    bool IsOverdue);

/// <summary>Aggregated closure / pendency figures for UCCRMC alerts.</summary>
public sealed record UccrmcDashboard(
    int Total,
    int Open,          // Submitted + InProgress
    int Resolved,
    int Closed,
    int Cancelled,
    int Overdue,       // open past SLA
    double ClosureRatePct,
    DateTime? FromUtc,
    DateTime? ToUtc,
    IReadOnlyList<UccrmcDailyPoint> Daily);

public sealed record UccrmcDailyPoint(DateTime Day, int Raised, int Closed);
