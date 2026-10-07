using MyDiary.Web.Features.Business.Models;

namespace MyDiary.Web.Features.Business.Services
{
    /// <summary>
    /// Service for the Business_New dashboard page.
    /// Each method calls a single stored procedure and returns all result sets
    /// in one round-trip, matching the pattern used for Total Advances.
    /// </summary>
    public interface IBusinessNewService
    {
        // ── Advances ────────────────────────────────────────────────────────────
        /// <summary>
        /// Calls SP_PAGETWO_DASHBOARD_ADVANCES.
        /// scope: "total" | "retail" | "msme" | "agri" | "corporate"
        /// </summary>
        Task<BusinessNewResult> GetAdvancesDashboardAsync(
            string role, string brSolId, string regionSolId, string zoneSolId,
            string scope = "total");

        // ── Deposits ────────────────────────────────────────────────────────────
        /// <summary>
        /// Calls SP_PAGETWO_DASHBOARD_DEPOSITS.
        /// scope: "total" | "td" | "savings" | "current" | "corporate"
        /// </summary>
        Task<DepositsNewResult> GetDepositsDashboardAsync(
            string role, string brSolId, string regionSolId, string zoneSolId,
            string scope = "total");

        // ── NPA ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Calls SP_PAGETWO_DASHBOARD_NPA (no scope parameter).
        /// </summary>
        Task<NpaNewResult> GetNpaDashboardAsync(
            string role, string brSolId, string regionSolId, string zoneSolId);

        // ── Profitability ───────────────────────────────────────────────────────
        /// <summary>
        /// Calls SP_PAGETWO_DASHBOARD_PROFITABILITY (no scope parameter).
        /// Returns Fee MOM, Fee Portfolio, and Interest MOM in a single round-trip.
        /// Used for both Income and Expense sections.
        /// </summary>
        Task<ProfitabilityNewResult> GetProfitabilityDashboardAsync(
            string role, string brSolId, string regionSolId, string zoneSolId);
    }
}
