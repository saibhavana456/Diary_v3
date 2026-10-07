using MyDiary.Web.Features.Business.Models;

namespace MyDiary.Web.Features.Business.Services;

public interface IAmenitiesService
{
    /// <summary>
    /// Fetches amenities from the CSEC API for the given branch.
    /// Returns only items that have both a description and a preview image.
    /// Falls back to mock data if the API is unreachable.
    /// </summary>
    Task<List<AmenityItem>> GetAmenitiesAsync(string branchCode, string agendaHeader);
}
