using MyDiary.Web.Features.UnionHubVideoLaunch.Models.DTO;

namespace MyDiary.Web.Features.UnionHubVideoLaunch.Services;

public interface IUnionHubVideoLaunchService
{
    Task<List<UhvVertical>> ListVerticalsAsync(CancellationToken ct = default);

    /// <summary>Published launches for the employee feed, newest first, optionally filtered by vertical.</summary>
    Task<List<UhvLaunch>> ListPublishedAsync(long? verticalId, string viewerEmpCode, CancellationToken ct = default);

    /// <summary>All launches (draft + published) for the admin's own management list.</summary>
    Task<List<UhvLaunch>> ListAllForAdminAsync(CancellationToken ct = default);

    Task<UhvLaunch?> GetAsync(long launchId, string viewerEmpCode, CancellationToken ct = default);

    Task<long> CreateDraftAsync(UhvCreateDraftRequest req, CancellationToken ct = default);

    /// <summary>Marks a draft as published and returns it so the caller can fan out notifications.</summary>
    Task<UhvLaunch?> PublishAsync(long launchId, CancellationToken ct = default);

    Task<(Stream Stream, string Mime, string FileName)?> ReadVideoAsync(long launchId, CancellationToken ct = default);
    Task<(Stream Stream, string Mime, string FileName)?> ReadAttachmentAsync(long attachmentId, CancellationToken ct = default);

    Task ToggleLikeAsync(long launchId, string empCode, CancellationToken ct = default);
    Task AddCommentAsync(long launchId, string empCode, string empName, string text, CancellationToken ct = default);

    /// <summary>Active employee PF numbers, used to fan out the "new launch published" in-app notification.</summary>
    Task<List<string>> ListActiveEmployeeCodesAsync(CancellationToken ct = default);
}
