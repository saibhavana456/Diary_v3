using MyDiary.Web.Features.BranchFoundation.Models;

namespace MyDiary.Web.Features.BranchFoundation.Services;

public interface IBranchFoundationService
{
    /// <summary>
    /// Fetches branches based on the user's maximum allowed scope.
    /// </summary>
    Task<List<BranchFoundationDto>> GetBranchesAsync(string? zoneName = null, string? regionName = null, string? solId = null);

    byte[] ExportToExcel(List<BranchFoundationDto> branches);
}