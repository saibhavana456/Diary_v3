using MyDiary.Web.Features.Landing_Dashboard_V2_1.Models;

namespace MyDiary.Web.Features.Landing_Dashboard_V2_1.Services
{
    public interface ILandingDashboardV2_1Service
    {
        /// <summary>
        /// Calls SP_PAGEONE_DASHBOARD with the resolved SOL ID and returns all 8 chart datasets
        /// plus the additional parameter rows (IDs 27-50) in a single round-trip.
        /// </summary>
        Task<PageOneDashboardResult> GetPageOneDashboardAsync(string role, string brSolId, string regionSolId, string zoneSolId);
    }
}
