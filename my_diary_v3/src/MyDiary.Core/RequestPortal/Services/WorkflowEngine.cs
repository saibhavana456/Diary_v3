using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Services;

public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IRequestRepo _requests;
    private readonly IRoutingService _routing;
    private readonly ISlaCalculator _sla;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;
    private readonly IUnitOfWork _uow;

    public WorkflowEngine(IRequestRepo requests, IRoutingService routing, ISlaCalculator sla, IAuditService audit, INotificationService notify, IUnitOfWork uow)
    {
        _requests = requests; _routing = routing; _sla = sla;
        _audit = audit; _notify = notify; _uow = uow;
    }

    public async Task EscalateOnceAsync(long requestId, CancellationToken ct = default)
    {
        var r = await _requests.GetAsync(requestId, ct);
        if (r is null || r.Status != RequestStatus.InProgress || r.CurrentLevel >= 5) return;

        await _uow.BeginAsync(ct);
        try
        {
            var nextLevel = r.CurrentLevel + 1;
            await _requests.DeactivateAssigneesAsync(r.Id, r.CurrentLevel, ct);

            var assignees = await _routing.ResolveLevelAssigneesAsync(r.Id, nextLevel, ct);
            foreach (var emp in assignees.Distinct())
                await _requests.InsertAssigneeAsync(new RequestAssignee { RequestId = r.Id, LevelNo = nextLevel, EmpCode = emp, IsActive = true, AssignedAt = DateTime.UtcNow }, ct);

            var newDue = await _sla.ComputeDueUtcAsync(r.RequestTypeId, nextLevel, DateTime.UtcNow, null, ct);
            await _requests.UpdateStatusLevelAsync(r.Id, RequestStatus.InProgress, nextLevel, newDue, ct);
            await _requests.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = nextLevel, ActorEmpCode = "SYSTEM", Action = "Escalated", Remarks = $"Auto-escalated to L{nextLevel} after SLA breach", ActedAt = DateTime.UtcNow }, ct);
            await _audit.LogAsync("RP_REQUEST", r.Id, "Escalate", "SYSTEM", new { fromLevel = r.CurrentLevel }, new { toLevel = nextLevel }, ct: ct);
            await _uow.CommitAsync(ct);
            await _notify.EnqueueAsync("Escalated", r.Id, assignees, ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task<int> RunDueEscalationsAsync(CancellationToken ct = default)
    {
        var ids = await _requests.ListIdsDueForEscalationAsync(200, ct);
        var n = 0;
        foreach (var id in ids)
        {
            try { await EscalateOnceAsync(id, ct); n++; }
            catch { /* logged elsewhere */ }
        }
        return n;
    }
}
