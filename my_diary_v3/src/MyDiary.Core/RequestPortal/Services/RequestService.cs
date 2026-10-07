using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;
using System.Runtime.InteropServices.JavaScript;

namespace RequestPortal.Core.Services;

public sealed class RequestService : IRequestService
{
    private readonly IRequestRepo _repo;
    private readonly IRoutingService _routing;
    private readonly ISlaCalculator _sla;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;
    private readonly IUnitOfWork _uow;
    private readonly INotificationApi _notificationApi;

    public RequestService(
        IRequestRepo repo, IRoutingService routing, ISlaCalculator sla,
        IAuditService audit, INotificationService notify, IUnitOfWork uow, INotificationApi notificationApi)
    {
        _repo = repo; _routing = routing; _sla = sla;
        _audit = audit; _notify = notify; _uow = uow;
        _notificationApi = notificationApi;
    }

    public async Task<long> CreateAsync(CreateRequestDto dto, string actorEmpCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Subject)) throw new ArgumentException("Subject required");
        if (string.IsNullOrWhiteSpace(dto.Description)) throw new ArgumentException("Description required");

        var due = await _sla.ComputeDueUtcAsync(dto.RequestTypeId, 1, DateTime.UtcNow, null, ct);
        var reqNo = $"RP{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(1000, 9999)}";

        await _uow.BeginAsync(ct);
        try
        {
            var id = await _repo.InsertAsync(new Request
            {
                ReqNo = reqNo, RequestTypeId = dto.RequestTypeId, UnitId = dto.UnitId,
                VerticalId = dto.VerticalId, DepartmentId = dto.DepartmentId, ActivityId = dto.ActivityId,
                RaisedByEmp = actorEmpCode,
                Subject = dto.Subject.Trim(), Description = dto.Description.Trim(),
                CurrentLevel = 1, Status = RequestStatus.InProgress, SlaDueUtc = due
            }, ct);

            var assignees = await _routing.ResolveL1AssigneesAsync(
                dto.RequestTypeId, dto.UnitId, dto.VerticalId, dto.DepartmentId, dto.ActivityId, ct);
            foreach (var emp in assignees.Distinct())
                await _repo.InsertAssigneeAsync(new RequestAssignee { RequestId = id, LevelNo = 1, EmpCode = emp, IsActive = true, AssignedAt = DateTime.UtcNow }, ct);

            await _repo.InsertActionAsync(new RequestAction { RequestId = id, LevelNo = 1, ActorEmpCode = actorEmpCode, Action = "Created", Remarks = "Request raised", ActedAt = DateTime.UtcNow }, ct);
            await _audit.LogAsync("RP_REQUEST", id, "Create", actorEmpCode, null, new { dto.RequestTypeId, dto.UnitId, dto.Subject }, ct: ct);
            await _uow.CommitAsync(ct);
            await _notify.EnqueueAsync("Created", id, assignees.Append(actorEmpCode), ct);
            return id;
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public Task<RequestDetail?> GetAsync(long id, CancellationToken ct = default) => _repo.GetDetailAsync(id, ct);

    public Task<(IReadOnlyList<RequestListItem> Items, int Total)> ListAsync(FilterDto filter, string? assignedToEmpCode, string? raisedByEmpCode, CancellationToken ct = default)
        => _repo.ListAsync(filter, assignedToEmpCode, raisedByEmpCode, ct);

    public Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(long id, CancellationToken ct = default) => _repo.GetTimelineAsync(id, ct);

    public async Task ActAsync(ActionDto dto, string actorEmpCode, CancellationToken ct = default)
    {
        var r = await _repo.GetAsync(dto.RequestId, ct) ?? throw new InvalidOperationException("Request not found");

        await _uow.BeginAsync(ct);
        try
        {
            switch (dto.Action.ToLowerInvariant())
            {
                case "forward":
                {
                    var next = Math.Clamp(dto.ForwardToLevel ?? r.CurrentLevel + 1, 1, 5);
                    if (next <= r.CurrentLevel) throw new InvalidOperationException("Forward must go to a higher level");
                    await _repo.DeactivateAssigneesAsync(r.Id, r.CurrentLevel, ct);
                    var assignees = await _routing.ResolveLevelAssigneesAsync(r.Id, next, ct);
                    foreach (var emp in assignees.Distinct())
                        await _repo.InsertAssigneeAsync(new RequestAssignee { RequestId = r.Id, LevelNo = next, EmpCode = emp, IsActive = true, AssignedAt = DateTime.UtcNow }, ct);
                    var due = await _sla.ComputeDueUtcAsync(r.RequestTypeId, next, DateTime.UtcNow, null, ct);
                    await _repo.UpdateStatusLevelAsync(r.Id, RequestStatus.InProgress, next, due, ct);
                    await _repo.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = next, ActorEmpCode = actorEmpCode, Action = "Forwarded", Remarks = dto.Remarks, ActedAt = DateTime.UtcNow }, ct);
                    await _audit.LogAsync("RP_REQUEST", r.Id, "Forward", actorEmpCode, new { fromLevel = r.CurrentLevel }, new { toLevel = next, dto.Remarks }, ct: ct);
                    await _uow.CommitAsync(ct);                    
                    await _notify.EnqueueAsync("Forwarded", dto.RequestId, assignees, ct);
                    return;
                }
                case "resolve":
                {                      
                    await _repo.UpdateStatusLevelAsync(r.Id, RequestStatus.Resolved, r.CurrentLevel, r.SlaDueUtc, ct);
                    await _repo.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = r.CurrentLevel, ActorEmpCode = actorEmpCode, Action = "Resolved", Remarks = dto.Remarks, ActedAt = DateTime.UtcNow }, ct);
                    await _audit.LogAsync("RP_REQUEST", r.Id, "Resolve", actorEmpCode, null, new { dto.Remarks }, ct: ct);
                    await _uow.CommitAsync(ct);
                    IEnumerable<string> recipientEmpCodes = new[] { actorEmpCode };
                    await _notify.EnqueueAsync("Resolved", dto.RequestId, recipientEmpCodes, ct);
                    return;
                }
                case "close":
                {
                    var closeRemarks = string.IsNullOrWhiteSpace(dto.CloseReason) ? dto.Remarks : $"[{FormatCloseReason(dto.CloseReason!)}] {dto.Remarks}";
                    await _repo.SetClosedAsync(r.Id, DateTime.UtcNow, ct);
                    await _repo.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = r.CurrentLevel, ActorEmpCode = actorEmpCode, Action = "Closed", Remarks = closeRemarks, ActedAt = DateTime.UtcNow }, ct);
                    await _audit.LogAsync("RP_REQUEST", r.Id, "Close", actorEmpCode, null, new { dto.Remarks, dto.CloseReason }, ct: ct);
                    await _uow.CommitAsync(ct);
                    IEnumerable<string> recipientEmpCodes = new[] { actorEmpCode };
                    await _notify.EnqueueAsync("Closed", dto.RequestId, recipientEmpCodes, ct);
                    return;
                }
                case "cancel":
                {
                    await _repo.UpdateStatusLevelAsync(r.Id, RequestStatus.Cancelled, r.CurrentLevel, r.SlaDueUtc, ct);
                    await _repo.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = r.CurrentLevel, ActorEmpCode = actorEmpCode, Action = "Cancelled", Remarks = dto.Remarks, ActedAt = DateTime.UtcNow }, ct);
                    await _audit.LogAsync("RP_REQUEST", r.Id, "Cancel", actorEmpCode, null, new { dto.Remarks }, ct: ct);
                    await _uow.CommitAsync(ct);
                    IEnumerable<string> recipientEmpCodes = new[] { actorEmpCode };
                    await _notify.EnqueueAsync("Cancelled", dto.RequestId, recipientEmpCodes, ct);
                    return;
                }
                default:
                    throw new InvalidOperationException($"Unknown action {dto.Action}");
            }
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task SeekClarificationAsync(ClarificationDto dto, string actorEmpCode, CancellationToken ct = default)
    {
        var r = await _repo.GetAsync(dto.RequestId, ct) ?? throw new InvalidOperationException("Request not found");
        await _uow.BeginAsync(ct);
        try
        {
            await _repo.InsertClarificationAsync(new Clarification { RequestId = r.Id, AskedByEmpCode = actorEmpCode, AskedAt = DateTime.UtcNow, Question = dto.Question }, ct);
            await _repo.InsertSlaPauseAsync(new SlaPause { RequestId = r.Id, PausedAt = DateTime.UtcNow, Reason = "ClarificationSought" }, ct);
            await _repo.UpdateStatusLevelAsync(r.Id, RequestStatus.ClarificationSought, r.CurrentLevel, r.SlaDueUtc, ct);
            await _repo.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = r.CurrentLevel, ActorEmpCode = actorEmpCode, Action = "ClarificationSought", Remarks = dto.Question, ActedAt = DateTime.UtcNow }, ct);
            await _audit.LogAsync("RP_REQUEST", r.Id, "SeekClarification", actorEmpCode, null, new { dto.Question }, ct: ct);
            await _uow.CommitAsync(ct);
            IEnumerable<string> recipientEmpCodes = new[] { actorEmpCode };
            await _notify.EnqueueAsync("SeekClarification", dto.RequestId, recipientEmpCodes, ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task ProvideClarificationAsync(ClarificationReplyDto dto, string actorEmpCode, CancellationToken ct = default)
    {
        var c = await _repo.GetClarificationAsync(dto.ClarificationId, ct) ?? throw new InvalidOperationException("Clarification not found");
        var r = await _repo.GetAsync(c.RequestId, ct) ?? throw new InvalidOperationException("Request not found");

        await _uow.BeginAsync(ct);
        try
        {
            await _repo.UpdateClarificationReplyAsync(c.Id, dto.Answer, DateTime.UtcNow, ct);
            await _repo.ResumeOpenSlaPauseAsync(r.Id, DateTime.UtcNow, ct);
            var newDue = await _sla.RecomputeForRequestAsync(r.Id, ct);
            await _repo.UpdateStatusLevelAsync(r.Id, RequestStatus.InProgress, r.CurrentLevel, newDue, ct);
            await _repo.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = r.CurrentLevel, ActorEmpCode = actorEmpCode, Action = "ClarificationProvided", Remarks = dto.Answer, ActedAt = DateTime.UtcNow }, ct);
            await _audit.LogAsync("RP_REQUEST", r.Id, "ProvideClarification", actorEmpCode, null, new { dto.ClarificationId }, ct: ct);
            await _uow.CommitAsync(ct);
            var assignees = await _repo.GetActiveAssigneeEmpCodesAsync(r.Id, r.CurrentLevel, ct);
            await _notify.EnqueueAsync("ClarificationProvided", r.Id, assignees, ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task ReplyToOpenClarificationAsync(long requestId, string answer, string actorEmpCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(answer)) throw new ArgumentException("Answer required");
        var open = await _repo.GetLatestOpenClarificationAsync(requestId, ct)
            ?? throw new InvalidOperationException("No open clarification on this request");
        await ProvideClarificationAsync(new ClarificationReplyDto(open.Id, answer.Trim()), actorEmpCode, ct);
    }

    public async Task ReopenAsync(long requestId, string justification, string actorEmpCode, CancellationToken ct = default)
    {
        var r = await _repo.GetAsync(requestId, ct) ?? throw new InvalidOperationException("Request not found");
        if (r.Status != RequestStatus.Closed && r.Status != RequestStatus.Resolved)
            throw new InvalidOperationException("Only closed/resolved requests can be reopened");

        await _uow.BeginAsync(ct);
        try
        {
            var assignees = await _repo.GetActiveAssigneeEmpCodesAsync(r.Id, r.CurrentLevel, ct);
            if (assignees.Count == 0)
                assignees = (await _routing.ResolveLevelAssigneesAsync(r.Id, r.CurrentLevel, ct)).ToList();
            foreach (var emp in assignees)
                await _repo.InsertAssigneeAsync(new RequestAssignee { RequestId = r.Id, LevelNo = r.CurrentLevel, EmpCode = emp, IsActive = true, AssignedAt = DateTime.UtcNow }, ct);

            var due = await _sla.ComputeDueUtcAsync(r.RequestTypeId, r.CurrentLevel, DateTime.UtcNow, null, ct);
            await _repo.UpdateStatusLevelAsync(r.Id, RequestStatus.Reopened, r.CurrentLevel, due, ct);
            await _repo.InsertActionAsync(new RequestAction { RequestId = r.Id, LevelNo = r.CurrentLevel, ActorEmpCode = actorEmpCode, Action = "Reopened", Remarks = justification, ActedAt = DateTime.UtcNow }, ct);
            await _audit.LogAsync("RP_REQUEST", r.Id, "Reopen", actorEmpCode, null, new { justification }, ct: ct);
            await _uow.CommitAsync(ct);
            await _notify.EnqueueAsync("Reopened", r.Id, assignees.Append(actorEmpCode), ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    public async Task SubmitFeedbackAsync(long requestId, int rating, string? comments, string actorEmpCode, CancellationToken ct = default)
    {
        if (rating is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(rating));
        var r = await _repo.GetAsync(requestId, ct) ?? throw new InvalidOperationException("Request not found");

        await _uow.BeginAsync(ct);
        try
        {
            await _repo.InsertFeedbackAsync(new Feedback { RequestId = r.Id, Rating = rating, Comments = comments, GivenAt = DateTime.UtcNow }, ct);
            await _audit.LogAsync("RP_REQUEST_FEEDBACK", r.Id, "Create", actorEmpCode, null, new { rating, comments }, ct: ct);
            await _uow.CommitAsync(ct);
        }
        catch { await _uow.RollbackAsync(ct); throw; }
    }

    private static string FormatCloseReason(string raw)
    {
        if (Enum.TryParse<CloseReason>(raw, true, out var parsed))
            return System.Text.RegularExpressions.Regex.Replace(parsed.ToString(), "(\\B[A-Z])", " $1");
        return raw;
    }
}
