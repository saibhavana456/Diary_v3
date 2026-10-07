using RequestPortal.Core.Models;

namespace RequestPortal.Core.Abstractions;

/// <summary>Physical storage for event photos (filesystem on the app server).</summary>
public interface IEventMediaStore
{
    /// <summary>Persist an image and return the storage-relative path.</summary>
    Task<string> SaveAsync(long postId, string fileName, byte[] data, CancellationToken ct = default);
    /// <summary>Move a stored file into the archive area; returns the new relative path.</summary>
    Task<string> MoveToArchiveAsync(string relativePath, CancellationToken ct = default);
    /// <summary>Open a stored file for reading, or null if missing.</summary>
    Stream? OpenRead(string relativePath);
}

public interface IEventFeedRepo
{
    // Verticals
    Task<IReadOnlyList<Vertical>> ListActiveVerticalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Vertical>> ListAdminVerticalsAsync(string empCode, CancellationToken ct = default);

    // Tags
    Task<IReadOnlyList<EventTag>> ListTagsAsync(bool onlyEnabled, CancellationToken ct = default);
    Task<long> InsertTagAsync(string name, string actorEmp, CancellationToken ct = default);
    Task SetTagEnabledAsync(long id, bool enabled, CancellationToken ct = default);

    // Events
    Task<long> InsertEventAsync(CreateEventDto dto, string actorEmp, CancellationToken ct = default);
    Task<IReadOnlyList<UpcomingEventItem>> ListEventsAsync(long? verticalId, CancellationToken ct = default);

    // Posts
    Task<long> InsertPostAsync(CreatePostDto dto, string actorEmp, CancellationToken ct = default);
    Task InsertPostImageAsync(long postId, string filePath, string fileName, string contentType, long size, int seqNo, CancellationToken ct = default);
    Task<IReadOnlyList<FeedPost>> ListFeedAsync(long? verticalId, int take, CancellationToken ct = default);
    Task<FeedPostImage?> GetImageAsync(long imageId, CancellationToken ct = default);

    // Activity log (retained 7 years)
    Task LogAsync(string activity, string entityType, long? entityId, string actorEmp, long? verticalId, string? details, CancellationToken ct = default);

    // Archival
    Task<IReadOnlyList<FeedPost>> ListPostsToArchiveAsync(DateTime olderThan, CancellationToken ct = default);
    Task MarkPostArchivedAsync(long postId, CancellationToken ct = default);
    Task UpdateImagePathArchivedAsync(long imageId, string newPath, CancellationToken ct = default);
    Task<int> PurgeLogsOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}

public interface IEventFeedService
{
    /// <summary>Verticals the actor may post to (all for super admin, mapped set for CO vertical admin).</summary>
    Task<IReadOnlyList<Vertical>> GetPostableVerticalsAsync(string actorEmp, bool isSuperAdmin, CancellationToken ct = default);

    Task<IReadOnlyList<Vertical>> ListAllVerticalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UpcomingEventItem>> ListEventsAsync(long? verticalId, CancellationToken ct = default);
    Task<IReadOnlyList<FeedPost>> GetFeedAsync(long? verticalId, int take, CancellationToken ct = default);

    Task<long> CreateEventAsync(CreateEventDto dto, string actorEmp, bool isSuperAdmin, CancellationToken ct = default);
    Task<long> CreatePostAsync(CreatePostDto dto, string actorEmp, bool isSuperAdmin, CancellationToken ct = default);

    // Tags (read for all admins; manage for super admin)
    Task<IReadOnlyList<EventTag>> ListTagsAsync(bool onlyEnabled, CancellationToken ct = default);
    Task<long> AddTagAsync(string name, string actorEmp, CancellationToken ct = default);
    Task SetTagEnabledAsync(long id, bool enabled, string actorEmp, CancellationToken ct = default);

    // Archival (called by background job)
    Task<int> ArchiveOldContentAsync(int olderThanDays, CancellationToken ct = default);
    Task<int> PurgeOldLogsAsync(int retentionYears, CancellationToken ct = default);
}
