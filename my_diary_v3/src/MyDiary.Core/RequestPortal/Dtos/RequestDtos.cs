namespace RequestPortal.Core.Dtos;

public sealed record CreateRequestDto(
    long RequestTypeId,
    long UnitId,
    long VerticalId,
    long DepartmentId,
    long ActivityId,
    string Subject,
    string Description);

public sealed record RequestListItem(
    long Id,
    string ReqNo,
    string RequestTypeName,
    string Subject,
    int CurrentLevel,
    string Status,
    DateTime? SlaDueUtc,
    DateTime CreatedAt,
    string RaisedByName,
    string UnitName);

public sealed record RequestDetail(
    long Id,
    string ReqNo,
    string RequestTypeName,
    string UnitName,
    string VerticalName,
    string DepartmentName,
    string ActivityName,
    string Subject,
    string Description,
    int CurrentLevel,
    string Status,
    DateTime? SlaDueUtc,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    string RaisedByName,
    string? RaisedByEmail);

public sealed record TimelineEvent(
    DateTime At,
    int LevelNo,
    string Action,
    string? Remarks,
    string ActorName);

public sealed record ActionDto(
    long RequestId,
    string Action,
    string Remarks,
    int? ForwardToLevel = null,
    string? CloseReason = null);

public sealed record ClarificationDto(
    long RequestId,
    string Question);

public sealed record ClarificationReplyDto(
    long ClarificationId,
    string Answer);

public sealed record DashboardKpis(
    int Open,
    int Breached,
    int Resolved30d,
    int Reopened30d,
    double AvgResolutionWorkingDays);

public sealed record PendencyByLevel(int LevelNo, int Open, int Breached);

public sealed record AgingBucket(string Bucket, int Count);

public sealed record FilterDto(
    long? RequestTypeId = null,
    long? UnitTypeId = null,
    long? UnitId = null,
    long? VerticalId = null,
    long? DepartmentId = null,
    long? ActivityId = null,
    string? Status = null,
    int? Level = null,
    bool? BreachedOnly = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 25,
    string? SortBy = null,
    bool SortDesc = true);
