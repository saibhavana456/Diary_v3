using System.Security.Claims;
using MyDiary.Web.Features.SubsidiaryJv.Models;

namespace MyDiary.Web.Features.SubsidiaryJv.Services;

public interface ISubsidiaryJvService
{
    Task<List<SjvEntity>> GetEntitiesAsync(bool activeOnly = true);
    Task<SjvEntityDetail?> GetDetailAsync(string entityCode);

    /// <summary>App Admin, or an employee listed in RP_SJV_ADMIN.</summary>
    Task<bool> IsAdminAsync(ClaimsPrincipal user);
    Task<List<(string EmpCode, string AddedBy, DateTime AddedAt)>> ListAdminsAsync();
    Task AddAdminAsync(string empCode, string actor);
    Task RemoveAdminAsync(string empCode);

    /// <summary>Excel template: sheet 1 = Entity, sheet 2 = Instructions (descriptions), then one sheet per section.</summary>
    byte[] GenerateTemplate();

    /// <summary>Validates the workbook; when <paramref name="apply"/> and there are no errors, writes it in one transaction.</summary>
    Task<SjvUploadResult> ProcessUploadAsync(Stream file, string fileName, string actor, bool apply, bool canCreateEntities);

    /// <summary>Super Admin: define/rename/activate an entity in the master list (code, name, type, order).</summary>
    Task<long> SaveEntityMasterAsync(SjvEntity e, string actor);
    /// <summary>Super Admin: permanently deletes an entity and everything under it (office presence, shareholding,
    /// activities, board, KMP, financials, updates, logo). Cannot be undone.</summary>
    Task DeleteEntityAsync(string entityCode);
    /// <summary>Super Admin: stores the entity's logo as a BLOB on its row; replaces any previous logo.</summary>
    Task SaveLogoAsync(string entityCode, byte[] bytes, string contentType, string originalFileName, string actor);
    /// <summary>The logo bytes and content type for the /sjv/logo/{code} endpoint, or null if the entity has none.</summary>
    Task<(byte[] Bytes, string ContentType)?> GetLogoAsync(string entityCode);
    /// <summary>JV Admin: KPI strip, company profile and contact fields of an existing entity.</summary>
    Task SaveOverviewAsync(SjvEntity e, string actor);
    /// <summary>JV Admin: replace one section (offices, shareholding, activities, board, kmp, financials, updates).</summary>
    Task SaveSectionAsync(long entityId, string section, SjvEntityDetail d, string actor);
    /// <summary>Current data (one entity, or all) in the upload-template layout.</summary>
    Task<byte[]> ExportAsync(string? entityCode);
}
