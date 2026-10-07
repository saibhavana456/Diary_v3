using MyDiary.Web.Features.Landing_Dashboard_V2.Models;

namespace MyDiary.Web.Features.Landing_Dashboard_V2.Services
{
    public interface ILandingDashboardV2Service
    {
        Task<TotalAdvancesPerformance> GetAdvancesPerformanceAsync(string zoneName, string regionName);
        Task<List<ProspectiveBusinessSummary>> GetProspectiveBusinessSummaryAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<List<FinancialInclusionSummary>> GetFinancialInclusionSummaryAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<AdvancesPortfolioSummary> GetAdvancesPortfolioSummaryAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositPortfolioSummary> GetDepositPortfolioSummaryAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DigitalBankingSummary> GetDigitalBankingSummaryAsync(string role, string solId, string regionCd, string zoneCd);
        Task<OperationsSummary> GetOperationsSummaryAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<BusinessKpiSummary> GetBusinessKpiCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<BusinessKpiSummary> GetBusinessKpiTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalAdvancesActual> GetTotalAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalAdvancesTarget> GetTotalAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalDepositsActual> GetTotalDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalDepositsTarget> GetTotalDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalAdvancesGrowthCurrent> GetAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalAdvancesGrowthLast> GetAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalDepositsGrowthCurrent> GetDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalDepositsGrowthLast> GetDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TotalBusinessSummary> GetTotalBusinessSummaryAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<ThirdPartyProductsSummary> GetThirdPartyProductsAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<PendingPositionSummary> GetPendingPositionAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<StaffSummary> GetStaffSummaryAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<List<StaffDetail>> GetStaffDetailsAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<BranchProfile> GetBranchProfileAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<List<BranchStaffMember>> GetBranchStaffMembersAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<List<BranchStaffMember>> GetBranchStaffMembersAsync(string branchCode);
        Task<(List<BranchStaffMember> Items, int TotalCount)> GetBranchStaffMembersPagedAsync(string role, string solId, string regionSolId, string zoneSolId, int page, int pageSize, string? searchFilter = null);
        Task<EntityCountSummary> GetEntityCountSummaryAsync();
        #region Advances - Retail
        Task<RetailAdvancesActual> GetRetailAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<RetailAdvancesTarget> GetRetailAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<RetailAdvancesPortfolio> GetRetailAdvancesPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<RetailAdvancesGrowthCurrent> GetRetailAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<RetailAdvancesGrowthLast> GetRetailAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        // --- NEW RETAIL DISBURSEMENT METHODS (NOA & AMT) ---
        Task<DepositsOpenedSummary> GetRetailAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetRetailAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Advances - MSME
        Task<MsmeAdvancesActual> GetMsmeAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<MsmeAdvancesTarget> GetMsmeAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<MsmeAdvancesPortfolio> GetMsmeAdvancesPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<MsmeAdvancesGrowthCurrent> GetMsmeAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<MsmeAdvancesGrowthLast> GetMsmeAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        // --- NEW MSME DISBURSEMENT METHODS (NOA & AMT) ---
        Task<DepositsOpenedSummary> GetMsmeAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetMsmeAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Advances - Agriculture
        // --- NEW AGRICULTURE ADVANCES METHODS ---
        Task<AgriAdvancesActual> GetAgriAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<AgriAdvancesTarget> GetAgriAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<AgriAdvancesGrowthCurrent> GetAgriAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<AgriAdvancesGrowthLast> GetAgriAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        // --- NEW AGRICULTURE DISBURSEMENT METHODS (NOA & AMT) ---
        Task<DepositsOpenedSummary> GetAgriAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetAgriAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Advances - Corporate
        // --- NEW CORPORATE ADVANCES METHODS ---
        Task<CorpAdvancesActual> GetCorpAdvancesActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<CorpAdvancesTarget> GetCorpAdvancesTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<CorpAdvancesGrowthCurrent> GetCorpAdvancesGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<CorpAdvancesGrowthLast> GetCorpAdvancesGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Advances - Total Advances
        // --- NEW ADVANCES DISBURSEMENT METHODS (NOA & AMT) ---
        Task<DepositsOpenedSummary> GetAdvancesDisbursementNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetAdvancesDisbursementAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Deposits - Total Deposits - Opened
        Task<DepositsOpenedSummary> GetDepositsOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetDepositsOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Deposits - Savings
        // --- NEW SAVINGS DEPOSITS METHODS ---
        Task<SavingsDepositsActual> GetSavingsDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<SavingsDepositsTarget> GetSavingsDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<SavingsDepositsGrowthCurrent> GetSavingsDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<SavingsDepositsGrowthLast> GetSavingsDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        // --- NEW SAVINGS OPENED METHODS (NOA & AMT) ---
        Task<DepositsOpenedSummary> GetSavingsOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetSavingsOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Deposits - Current
        // --- NEW CURRENT DEPOSITS METHODS ---
        Task<CurrentDepositsActual> GetCurrentDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<CurrentDepositsTarget> GetCurrentDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<CurrentDepositsGrowthCurrent> GetCurrentDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<CurrentDepositsGrowthLast> GetCurrentDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        // --- NEW CURRENT OPENED METHODS (NOA & AMT) ---
        Task<DepositsOpenedSummary> GetCurrentOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetCurrentOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Deposits - Term Deposits
        // --- NEW TERM DEPOSITS METHODS ---
        Task<TermDepositsActual> GetTermDepositsActualAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TermDepositsTarget> GetTermDepositsTargetAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TermDepositsPortfolio> GetTermDepositsPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TermDepositsGrowthCurrent> GetTermDepositsGrowthCurrentAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<TermDepositsGrowthLast> GetTermDepositsGrowthLastAsync(string role, string solId, string regionSolId, string zoneSolId);
        // --- NEW TERM DEPOSITS OPENED METHODS (NOA & AMT) ---
        Task<DepositsOpenedSummary> GetTermDepositsOpenedNoaAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<DepositsOpenedSummary> GetTermDepositsOpenedAmtAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region NPA - Total NPA
        // --- NEW NPA METHODS ---
        Task<NpaPerformance> GetNpaPerformanceAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<NpaPortfolio> GetNpaPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<NpaBook> GetNpaBookAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Profitability - Fee Income
        // --- NEW PROFITABILITY METHODS ---
        Task<ProfitabilityMomSummary> GetProfitabilityFeeIncomeMomAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<ProfitabilityPortfolio> GetProfitabilityPortfolioAsync(string role, string solId, string regionSolId, string zoneSolId);
        Task<ProfitabilityProfitBook> GetProfitabilityProfitBookAsync(string role, string solId, string regionSolId, string zoneSolId);
        #endregion
        #region Filters
        Task<List<ZoneDto>> GetZonesAsync();
        Task<List<RegionDto>> GetRegionsByZoneAsync(string zoneSolid);
        Task<List<BranchDto>> GetBranchesByRegionAsync(string regionSolid);
        #endregion

        Task<List<StaffDetailsUnionHub>> GetBranchStaffDataAsync();
    }
}