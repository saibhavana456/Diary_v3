namespace MyDiary.Web.Features.Landing_Dashboard_V2.Models
{
    public class TotalAdvancesPerformance
    {
        public string ZoneName { get; set; }
        public string RegionName { get; set; }
        public decimal ActualTotal { get; set; }
        public decimal Current { get; set; }
        public decimal Target { get; set; }
    }

    public class StaffDetailsUnionHub
    {
        public string EmpCode { get; set; }
        public string Name { get; set; } = "";
        public string Branch { get; set; } = "";
        public string SolId { get; set; } = "";
        public string Region { get; set; } = "";
        public string Zone { get; set; } = "";
        public string Designation { get; set; } = "";
        public string EmpScaleDescr { get; set; } = "";
        public string ContactNo { get; set; } = "";
        public string Email { get; set; } = "";
    }

    // --- NEW MODEL ADDED HERE ---
    public class ProspectiveBusinessSummary
    {
        public string Parameter { get; set; }
        public decimal Sanction_Amount { get; set; }
    }

    public class FinancialInclusionSummary
    {
        public string Parameter { get; set; }
        public int Total_Enrollment { get; set; } // Enforcing Integer data type
    }
    public class AdvancesPortfolioSummary
    {
        public decimal Retail_Amount { get; set; }
        public decimal Agri_Amount { get; set; }
        public decimal MSME_Amount { get; set; }
        public decimal Corporate_Amount { get; set; }
    }
    public class DepositPortfolioSummary
    {
        public decimal CA_Amount { get; set; }
        public decimal SB_Amount { get; set; }
        public decimal TD_Amount { get; set; }
        public decimal RTD_Amount { get; set; }
    }
    public class DigitalBankingSummary
    {
        public decimal Debit_Card_Eligible_Count { get; set; }
        public decimal Debit_Card_Issued_Count { get; set; }

        public decimal Mobile_Banking_Eligible_Count { get; set; }
        public decimal Mobile_Banking_Issued_Count { get; set; }

        public decimal Internet_Banking_Eligible_Count { get; set; }
        public decimal Internet_Banking_Issued_Count { get; set; }

        public decimal Total_Eligible_Count { get; set; }
        public decimal Any_One_Facility_Availed_Count { get; set; }
    }
    public class OperationsSummary
    {
        public int Total_Lockers { get; set; }
        public int Occupied_Lockers { get; set; }

        // Calculated Property: Automatically handles the math for you!
        public int Vacant_Lockers => Total_Lockers - Occupied_Lockers;
    }
    public class BusinessKpiSummary
    {
        public decimal Business { get; set; }
        public decimal Casa { get; set; }
        public decimal Deposits { get; set; }
        public decimal Advances { get; set; }
    }
    public class TotalAdvancesActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class TotalAdvancesTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class TotalDepositsActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class TotalDepositsTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }
    public class TotalAdvancesGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class TotalAdvancesGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    public class TotalDepositsGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class TotalDepositsGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    public class TotalBusinessSummary
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_2_Month_Value { get; set; }
        public decimal Last_3_Month_Value { get; set; }
    }
    public class ThirdPartyProductsSummary
    {
        public decimal GeneralInsurance { get; set; }
        public decimal HealthInsurance { get; set; }
        public decimal LifeInsurance { get; set; }
        public decimal MutualFunds { get; set; }
    }
    public class PendingPositionSummary
    {
        public decimal Ckyc_Pendency { get; set; }
        public decimal ReKyc_Pendency { get; set; }
        public decimal Locker_Rent_Overdue { get; set; }
    }
    #region Business - Advances - Retail
    public class RetailAdvancesActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class RetailAdvancesTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class RetailAdvancesPortfolio
    {
        public decimal Education_Amount { get; set; }
        public decimal Home_Amount { get; set; }
        public decimal Vehicle_Amount { get; set; }
        public decimal Mortage_Amount { get; set; } // Matches SQL alias
        public decimal Personal_Amount { get; set; }
    }

    public class RetailAdvancesGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class RetailAdvancesGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    #endregion
    #region Business - Advances - MSME
    public class MsmeAdvancesActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class MsmeAdvancesTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class MsmeAdvancesPortfolio
    {
        public decimal Micro_Amount { get; set; }
        public decimal Small_Amount { get; set; }
        public decimal Medium_Amount { get; set; }
    }

    public class MsmeAdvancesGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class MsmeAdvancesGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    #endregion
    #region Business - Advances - Agriculture
    // ==========================================
    // --- NEW MODELS FOR AGRICULTURE ADVANCES ---
    // ==========================================

    public class AgriAdvancesActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class AgriAdvancesTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class AgriAdvancesGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class AgriAdvancesGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    #endregion
    #region Business - Advances - Corporate
    // ==========================================
    // --- NEW MODELS FOR CORPORATE ADVANCES ---
    // ==========================================

    public class CorpAdvancesActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class CorpAdvancesTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class CorpAdvancesGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class CorpAdvancesGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    #endregion
    #region Business - Deposits - Savings
    // ==========================================
    // --- NEW MODELS FOR SAVINGS DEPOSITS ---
    // ==========================================

    public class SavingsDepositsActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class SavingsDepositsTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class SavingsDepositsGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class SavingsDepositsGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    #endregion
    #region Business - Deposits - Current
    // ==========================================
    // --- NEW MODELS FOR CURRENT DEPOSITS ---
    // ==========================================

    public class CurrentDepositsActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class CurrentDepositsTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class CurrentDepositsGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class CurrentDepositsGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    #endregion
    #region Business - Deposits - Term Deposits
    // ==========================================
    // --- NEW MODELS FOR TERM DEPOSITS ---
    // ==========================================

    public class TermDepositsActual
    {
        public decimal Actual_Total { get; set; }
    }

    public class TermDepositsTarget
    {
        public decimal Target { get; set; }
        public decimal Last_Target { get; set; }
    }

    public class TermDepositsPortfolio
    {
        public decimal Bulk_Amount { get; set; }
        public decimal Retail_Amount { get; set; }
    }

    public class TermDepositsGrowthCurrent
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }

    public class TermDepositsGrowthLast
    {
        public decimal Last_Year_Value { get; set; }
    }
    #endregion
    #region Business - Total Deposits - Opened
    public class DepositsOpenedSummary
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_2_Month_Value { get; set; }
        public decimal Last_3_Month_Value { get; set; }
    }
    #endregion
    #region Business - NPA - Total NPA
    // ==========================================
    // --- NEW MODELS FOR TOTAL NPA ---
    // ==========================================

    public class NpaPerformance
    {
        public decimal NPA_Total { get; set; }
        public decimal ADVANCES_Total { get; set; }
    }

    public class NpaPortfolio
    {
        public decimal MSME_AMOUNT { get; set; }
        public decimal AGRICULTURE_AMOUNT { get; set; }
        public decimal RETAIL_AMOUNT { get; set; }
        public decimal CORP_AMOUNT { get; set; }
        public decimal OTHERS_AMOUNT { get; set; }
    }

    public class NpaBook
    {
        public decimal Current_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
    }
    #endregion
    #region Profitability - Fee Income
    // ==========================================
    // --- NEW MODELS FOR PROFITABILITY (FEE INCOME) ---
    // ==========================================

    public class ProfitabilityMomSummary
    {
        public decimal Current_Month_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_2nd_Month_Value { get; set; }
        public decimal Last_3rd_Month_Value { get; set; }
    }

    public class ProfitabilityPortfolio
    {
        public decimal FEE_AMOUNT { get; set; }
        public decimal INTEREST_AMOUNT { get; set; }
    }

    public class ProfitabilityProfitBook
    {
        public decimal As_On_Value { get; set; }
        public decimal Last_Month_Value { get; set; }
        public decimal Last_Quater_Value { get; set; }
        public decimal Last_FY_Value { get; set; }
    }
    #endregion
    #region Filters
    public class ZoneDto
    {
        public string zone_solid { get; set; }
        public string zone_code { get; set; }
        public string zone_name_eng { get; set; }
    }

    public class RegionDto
    {
        public string region_solid { get; set; }
        public string region_code { get; set; }
        public string region_name { get; set; }
        public string zone_solid { get; set; }
    }

    public class BranchDto
    {
        public string SOL_ID { get; set; }
        public string branch_code { get; set; }
        public string branch_name { get; set; }
        public string region_solid { get; set; }
    }
    #endregion
}

public class StaffSummary
    {
        public int OfficerCount { get; set; }
        public int CSACount { get; set; }
        public int SubStaffCount { get; set; }
    }

    public class StaffDetail
    {
        public int SNo { get; set; }
        public string SolId { get; set; } = "";
        public string Zone { get; set; } = "";
        public string Region { get; set; } = "";
        public string Branch { get; set; } = "";

        /// <summary>
        /// SOL ID of the zone this row belongs to. Populated when the grid can resolve it
        /// (used to scope "View Details" to the clicked zone for CO scale>=4 aggregate rows).
        /// </summary>
        public string ZoneSolId { get; set; } = "";

        /// <summary>
        /// SOL ID of the region this row belongs to. Populated when the grid can resolve it
        /// (used to scope "View Details" to the clicked region for aggregate rows).
        /// </summary>
        public string RegionSolId { get; set; } = "";
        public int Clerk { get; set; }
        public int Scale1 { get; set; }
        public int Scale2 { get; set; }
        public int Scale3 { get; set; }
        public int Scale4 { get; set; }
        public int Scale5 { get; set; }
        public int Scale6 { get; set; }
        public int Scale7 { get; set; }
        public int Scale8 { get; set; }
        public int SubStaff { get; set; }
    }

    public class BranchProfile
    {
        public string SolId { get; set; } = "";
        public string SolDesc { get; set; } = "";
        public string IfscCd { get; set; } = "";
        public string SolType { get; set; } = "";
        public DateTime? SolOpnDt { get; set; }
        public string BranchCategory { get; set; } = "";
        public string BranchBusinessCategory { get; set; } = "";
        public string LicenseNumber { get; set; } = "";
        public string ZoneName { get; set; } = "";
        public string AesolZoneCd { get; set; } = "";
        public string RegionName { get; set; } = "";
        public string AesolRegionCd { get; set; } = "";
        public string BranchEmail { get; set; } = "";
        public string MicrCd { get; set; } = "";
        public string BranchAddr1 { get; set; } = "";
        public string BranchAddr2 { get; set; } = "";
        public string SubDistrictName { get; set; } = "";
        public string DistrictName { get; set; } = "";
        public string CityName { get; set; } = "";
        public string BranchHeadName { get; set; } = "";
        public DateTime? BranchHeadPostedSinceDt { get; set; }
        public int BranchStaffCnt { get; set; }
        public int BranchOfficersCnt { get; set; }
        public int BranchClerksCnt { get; set; }
        public int BranchSubStaffCnt { get; set; }
        public string LeaseValidity { get; set; } = "";
        public DateTime? LastUpdatedDt { get; set; }
    }

    public class BranchStaffMember
    {
        public int SNo { get; set; }
        public string Name { get; set; } = "";
        public string Branch { get; set; } = "";
        public string SolId { get; set; } = "";
        public string Region { get; set; } = "";
        public string Zone { get; set; } = "";
        public string Designation { get; set; } = "";
        public string EmpScaleDescr { get; set; } = "";
        public string ContactNo { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class EntityCountSummary
    {
        public int ZoneCount { get; set; }
        public int RegionCount { get; set; }
        public int BranchCount { get; set; }
    }
