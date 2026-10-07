using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

/// <summary>
/// Orchestrates UCCRMC alert tickets. Unlike the interactive New Request flow,
/// the command centre supplies an explicit assignee (bypassing routing) and an
/// external Alert ID that is stored on the ticket for later status lookups.
/// Idempotent on Alert ID: re-posting an existing alert returns its ticket.
/// </summary>
public sealed class UccrmcService : IUccrmcService
{
    private readonly IUccrmcRepo _uccrmc;
    private readonly IRequestRepo _repo;
    private readonly ISlaCalculator _sla;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;
    private readonly IUnitOfWork _uow;

    public UccrmcService(
        IUccrmcRepo uccrmc, IRequestRepo repo, ISlaCalculator sla,
        IAuditService audit, INotificationService notify, IUnitOfWork uow)
    {
        _uccrmc = uccrmc; _repo = repo; _sla = sla;
        _audit = audit; _notify = notify; _uow = uow;
    }

    public async Task<CreateUccrmcAlertResponse> CreateAlertAsync(CreateUccrmcAlertRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.AlertId))          throw new ArgumentException("AlertId is required");
        if (string.IsNullOrWhiteSpace(req.AssigneeEmpCode))  throw new ArgumentException("AssigneeEmpCode is required");
        if (string.IsNullOrWhiteSpace(req.RequesterEmpCode)) throw new ArgumentException("RequesterEmpCode is required");

        // Idempotency: if this alert already has a ticket, return it unchanged.
        var existing = await _uccrmc.GetByAlertIdAsync(req.AlertId.Trim(), ct);
        if (existing is not null)
            return new CreateUccrmcAlertResponse(existing.RequestId, existing.ReqNo, existing.AlertId, existing.Status);

        var cls = await _uccrmc.GetClassificationAsync(ct)
                  ?? throw new InvalidOperationException("UCCRMC classification is not configured. Run RP_08_uccrmc.sql.");

        var subject = string.IsNullOrWhiteSpace(req.Subject) ? $"UCCRMC Alert {req.AlertId.Trim()}" : req.Subject!.Trim();
        var description = string.IsNullOrWhiteSpace(req.Description)
            ? $"Alert {req.AlertId.Trim()} raised via UCCRMC command centre."
            : req.Description!.Trim();

        var due = await _sla.ComputeDueUtcAsync(cls.RequestTypeId, 1, DateTime.UtcNow, null, ct);
        var reqNo = $"UC{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(1000, 9999)}";
        var assignee = req.AssigneeEmpCode.Trim();
        var requester = req.RequesterEmpCode.Trim();

        await _uow.BeginAsync(ct);
        try
        {
            var id = await _repo.InsertAsync(new Request
            {
                ReqNo = reqNo,
                RequestTypeId = cls.RequestTypeId, UnitId = cls.UnitId, VerticalId = cls.VerticalId,
                DepartmentId = cls.DepartmentId, ActivityId = cls.ActivityId,
                RaisedByEmp = requester, Subject = subject, Description = description,
                CurrentLevel = 1, Status = RequestStatus.InProgress, SlaDueUtc = due,
                ExtAlertId = req.AlertId.Trim(), ExtSource = Uccrmc.Source
            }, ct);

            // Explicit assignee (command centre decides the handler; no routing).
            await _repo.InsertAssigneeAsync(new RequestAssignee
            {
                RequestId = id, LevelNo = 1, EmpCode = assignee,
                AssignedByEmp = requester, IsActive = true, AssignedAt = DateTime.UtcNow
            }, ct);

            await _repo.InsertActionAsync(new RequestAction
            {
                RequestId = id, LevelNo = 1, ActorEmpCode = requester,
                Action = "Created", Remarks = $"UCCRMC alert {req.AlertId.Trim()} — assigned to {assignee}",
                ActedAt = DateTime.UtcNow
            }, ct);

            await _audit.LogAsync("RP_REQUEST", id, "UccrmcCreate", requester, null,
                new { req.AlertId, assignee, requester, cls.RequestTypeId }, ct: ct);

            await _uow.CommitAsync(ct);
            await _notify.EnqueueAsync("Created", id, new[] { assignee, requester }, ct);

            return new CreateUccrmcAlertResponse(id, reqNo, req.AlertId.Trim(), RequestStatus.InProgress.ToString());
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public Task<UccrmcAlertStatus?> GetStatusAsync(string alertId, CancellationToken ct = default)
        => _uccrmc.GetByAlertIdAsync((alertId ?? "").Trim(), ct);

    public Task<UccrmcDashboard> GetDashboardAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
        => _uccrmc.GetDashboardAsync(fromUtc, toUtc, ct);
}
