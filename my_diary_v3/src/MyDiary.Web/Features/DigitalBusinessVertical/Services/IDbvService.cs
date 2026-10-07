using System.Security.Claims;
using MyDiary.Web.Features.DigitalBusinessVertical.Models;
namespace MyDiary.Web.Features.DigitalBusinessVertical.Services;

public interface IDbvService
{
    // access
    /// <summary>DBV department user (RP_DBV_ADMIN) or App Admin.</summary>
    Task<bool> IsAdminAsync(ClaimsPrincipal user);
    Task<List<(string EmpCode, string AddedBy, DateTime AddedAt)>> ListAdminsAsync();
    Task AddAdminAsync(string empCode, string actor);
    Task RemoveAdminAsync(string empCode);

    // campaigns
    Task<List<DbvCampaign>> ListCampaignsAsync(DbvFilter filter);
    Task<DbvCampaign?> GetCampaignAsync(long id);
    /// <summary>Insert (Id = 0) or update; replaces the communication rows. notify = push an in-app notification to everyone (new campaigns only).</summary>
    Task<long> SaveCampaignAsync(DbvCampaign c, string actor, bool notify);
    Task SetCampaignArchivedAsync(long id, bool archived, string actor);
    Task DeleteCampaignAsync(long id);

    // correspondence
    Task<List<DbvCorrespondence>> ListCorrespondenceAsync(bool includeArchived);
    Task<long> SaveCorrespondenceAsync(DbvCorrespondence c, string actor, bool notify);
    Task SetCorrespondenceArchivedAsync(long id, bool archived, string actor);
    Task DeleteCorrespondenceAsync(long id);

    // files (creatives / attachments) under the NFS-aware static-asset root
    // ({AssetsRoot}/uploads/dbv/{subFolder} — wwwroot/MyDiary/uploads/dbv/... under NFS,
    // wwwroot/uploads/dbv/... locally). Returns the browser-facing relative URL.
    Task<(string RelativePath, string FileName)> SaveFileAsync(string subFolder, Stream content, string originalFileName);

    // saved criteria (department users)
    Task<List<DbvSavedCriteria>> ListSavedCriteriaAsync(string empCode);
    Task SaveCriteriaAsync(string empCode, string name, DbvFilter filter);
    Task DeleteCriteriaAsync(string empCode, long id);
    DbvFilter? ParseCriteria(string json);

    // Excel (MIS format from DBV_FORMAT.xlsx)
    Task<byte[]> ExportAsync(DbvFilter filter);
    byte[] GenerateTemplate();
    Task<DbvUploadResult> ProcessUploadAsync(Stream file, string actor, bool apply);

    Task<List<DbvDownload>> ListDownloadsAsync(bool includeArchived);

    Task<long> SaveDownloadAsync(
        DbvDownload download,
        string actor,
        bool notify);

    Task SetDownloadArchivedAsync(
        long id,
        bool archived,
        string actor);

    Task DeleteDownloadAsync(long id);
    Task<List<DbvOtherInitiative>> ListOtherInitiativesAsync(bool includeArchived);

    Task<long> SaveOtherInitiativeAsync(
        DbvOtherInitiative item,
        string actor,
        bool notify);

    Task SetOtherInitiativeArchivedAsync(
        long id,
        bool archived,
        string actor);

    Task DeleteOtherInitiativeAsync(long id);

    Task<List<DbvInitiative>> ListInitiativesAsync(
    bool includeArchived);

    Task<long> SaveInitiativeAsync(
        DbvInitiative item,
        string actor,
        bool notify);

    Task SetInitiativeArchivedAsync(
        long id,
        bool archived,
        string actor);

    Task DeleteInitiativeAsync(
        long id);
}
