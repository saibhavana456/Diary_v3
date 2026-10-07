using RequestPortal.Core.Dtos;
using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

public interface IRequestService
{
    Task<long> CreateAsync(CreateRequestDto dto, string actorEmpCode, CancellationToken ct = default);
    Task<RequestDetail?> GetAsync(long id, CancellationToken ct = default);
    Task<(IReadOnlyList<RequestListItem> Items, int Total)> ListAsync(FilterDto filter, string? assignedToEmpCode, string? raisedByEmpCode, CancellationToken ct = default);
    Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(long id, CancellationToken ct = default);
    Task ActAsync(ActionDto dto, string actorEmpCode, CancellationToken ct = default);
    Task SeekClarificationAsync(ClarificationDto dto, string actorEmpCode, CancellationToken ct = default);
    Task ProvideClarificationAsync(ClarificationReplyDto dto, string actorEmpCode, CancellationToken ct = default);
    Task ReplyToOpenClarificationAsync(long requestId, string answer, string actorEmpCode, CancellationToken ct = default);
    Task ReopenAsync(long requestId, string justification, string actorEmpCode, CancellationToken ct = default);
    Task SubmitFeedbackAsync(long requestId, int rating, string? comments, string actorEmpCode, CancellationToken ct = default);
}

public interface IWorkflowEngine
{
    Task EscalateOnceAsync(long requestId, CancellationToken ct = default);
    Task<int> RunDueEscalationsAsync(CancellationToken ct = default);
}

public interface IRoutingService
{
    Task<IReadOnlyList<string>> ResolveL1AssigneesAsync(long requestTypeId, long unitId, long verticalId, long departmentId, long activityId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ResolveLevelAssigneesAsync(long requestId, int levelNo, CancellationToken ct = default);
}

public interface ISlaCalculator
{
    Task<DateTime> ComputeDueUtcAsync(long requestTypeId, int levelNo, DateTime startUtc, long? holidayCalendarId, CancellationToken ct = default);
    Task<DateTime> RecomputeForRequestAsync(long requestId, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(string entity, long entityId, string action, string? actorEmpCode, object? oldVal, object? newVal, string? ip = null, string? userAgent = null, CancellationToken ct = default);
    Task<bool> VerifyChainAsync(CancellationToken ct = default);
}

public interface INotificationService
{
    Task EnqueueAsync(string eventCode, long requestId, IEnumerable<string> recipientEmpCodes, CancellationToken ct = default);
    Task<int> DispatchPendingAsync(int batchSize, CancellationToken ct = default);
}

public interface IAttachmentService
{
    Task<long> StoreAsync(long requestId, long? actionId, string fileName, string mime, Stream content, string uploadedByEmp, CancellationToken ct = default);
    Task<(Stream Stream, string Mime, string FileName)> ReadAsync(long attachmentId, string actorEmpCode, CancellationToken ct = default);
}

public interface ICurrentUser
{
    long? UserId { get; }
    string? EmpCode { get; }
    string? Name { get; }
    string? Email { get; }
    IReadOnlySet<RoleCode> Roles { get; }
    bool IsInRole(RoleCode role);
    /// <summary>Force re-resolution of the principal in an async context (avoids
    /// blocking the sync property getters on a session read).</summary>
    Task RefreshAsync();
}

public interface IUserDirectory
{
    Task<AppUser?> EnsureUserAsync(string adSamAccountName, string? displayName, string? email, CancellationToken ct = default);
    Task<IReadOnlyList<RoleCode>> GetEffectiveRolesAsync(string empCode, CancellationToken ct = default);
}

public enum BroadcastScopeKind
{
    BankWide = 0,
    Vertical = 1,
    Zone     = 2,
    RegionalOffice = 3,
    Branch   = 4,
}

public sealed record BroadcastScope(BroadcastScopeKind Kind, long? UnitId = null, long? VerticalId = null);

public interface IBroadcastDirectory
{
    Task<IReadOnlyList<AppUser>> ResolveAudienceAsync(BroadcastScope scope, CancellationToken ct = default);
}
