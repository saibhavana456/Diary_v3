using MyDiary.Web.Features.Landing_Dashboard_V2.Models;

namespace MyDiary.Web.Features.Business.Models
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ADVANCES  (SP_PAGETWO_DASHBOARD_ADVANCES)
    //  Scopes: total | retail | msme | agri | corporate
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds all data returned by SP_PAGETWO_DASHBOARD_ADVANCES in a single call.
    /// Covers all Advances sub-tabs: Total, Retail, MSME, Agriculture, Corporate.
    /// </summary>
    public class BusinessNewResult
    {
        public DateTime? AsOnDate { get; set; }

        // Chart 1 – Performance (already scaled by SP)
        public BusinessPerformance Performance { get; set; } = new();

        // Chart 2 – Portfolio (raw INR — divide by 10,000,000 for Crores)
        // Populated for: total, retail, msme  (not agri / corporate)
        public BusinessPortfolio Portfolio { get; set; } = new();

        // Chart 3 – Outstanding Book / Growth (raw INR)
        public BusinessOutstandingBook OutstandingBook { get; set; } = new();

        // Chart 4 – Disbursements (multiple rows, one per month)
        public List<BusinessDisbursementRow> Disbursements { get; set; } = new();
    }

    /// <summary>Performance card — values already scaled by SP (e.g. '000 Crores).</summary>
    public class BusinessPerformance
    {
        public decimal CurrentFy    { get; set; }
        public decimal TargetFy     { get; set; }
        public decimal ActualTotal  { get; set; }
        public string  ScaleText    { get; set; } = "";
    }

    /// <summary>
    /// Advances Portfolio breakdown — raw INR values.
    /// Fields used depend on scope:
    ///   total    → MSME / Retail / Agriculture / Corporate
    ///   retail   → Vehicle / Mortgage / Home
    ///   msme     → Micro / Small / Medium
    /// </summary>
    public class BusinessPortfolio
    {
        // total scope
        public decimal MsmeAdvances        { get; set; }
        public decimal RetailAdvances      { get; set; }
        public decimal AgricultureAdvances { get; set; }
        public decimal CorporateAdvances   { get; set; }

        // retail scope
        public decimal VehicleAdvances     { get; set; }
        public decimal MortgageAdvances    { get; set; }
        public decimal HomeAdvances        { get; set; }

        // msme scope
        public decimal MicroAdvances       { get; set; }
        public decimal SmallAdvances       { get; set; }
        public decimal MediumAdvances      { get; set; }

        public string ScaleText { get; set; } = "";
    }

    /// <summary>Outstanding Book historical trend — raw INR values.</summary>
    public class BusinessOutstandingBook
    {
        public decimal Base        { get; set; }
        public decimal LastFy      { get; set; }
        public decimal LastQuarter { get; set; }
        public decimal LastMonth   { get; set; }
        public decimal AsOn        { get; set; }
        public string ScaleText { get; set; } = "";
    }

    /// <summary>
    /// One row from the Disbursements result set.
    /// Sanctions = Number of Accounts (NOA).
    /// Disbursements = Amount (AMT) in raw INR.
    /// </summary>
    public class BusinessDisbursementRow
    {
        public DateTime AsOnDate { get; set; }
        public decimal  Noa      { get; set; }   // Sanctions column
        public decimal  Amt      { get; set; }   // disbursements column (raw INR)
    }


    // ═══════════════════════════════════════════════════════════════════════════
    //  DEPOSITS  (SP_PAGETWO_DASHBOARD_DEPOSITS)
    //  Scopes: total | td | savings | current | corporate
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds all data returned by SP_PAGETWO_DASHBOARD_DEPOSITS in a single call.
    /// Covers all NDeposits sub-tabs: Total Deposits, Term Deposits, Savings, Current.
    /// </summary>
    public class DepositsNewResult
    {
        public DateTime? AsOnDate { get; set; }

        // Chart 1 – Performance (already scaled by SP)
        public BusinessPerformance Performance { get; set; } = new();

        // Chart 2 – Portfolio (raw INR)
        // Populated for: total, td  (not savings / current)
        public DepositsPortfolio Portfolio { get; set; } = new();

        // Chart 3 – Outstanding Book / Growth (raw INR)
        public BusinessOutstandingBook OutstandingBook { get; set; } = new();

        // Chart 4 – Opened / Disbursements (multiple rows, one per month)
        public List<BusinessDisbursementRow> Opened { get; set; } = new();
    }

    /// <summary>
    /// Deposits Portfolio breakdown — raw INR values.
    /// Fields used depend on scope:
    ///   total → CD / SB / TD / RTD
    ///   td    → RetailTD / BulkTD  (mapped from VEHICLE_ADVANCES / MORTGAGE_ADVANCES columns)
    /// </summary>
    public class DepositsPortfolio
    {
        // total scope
        public decimal CdDeposits         { get; set; }
        public decimal SbDeposits         { get; set; }
        public decimal TdDeposits         { get; set; }
        public decimal RetailTermDeposits { get; set; }

        // td scope  (SP reuses VEHICLE_ADVANCES / MORTGAGE_ADVANCES column names)
        public decimal RetailTd           { get; set; }   // VEHICLE_ADVANCES col
        public decimal BulkTd             { get; set; }   // MORTGAGE_ADVANCES col
        public string ScaleText { get; set; } = "";
    }


    // ═══════════════════════════════════════════════════════════════════════════
    //  NPA  (SP_PAGETWO_DASHBOARD_NPA)
    //  No scope parameter — single call returns all NPA data
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds all data returned by SP_PAGETWO_DASHBOARD_NPA in a single call.
    /// </summary>
    public class NpaNewResult
    {
        public DateTime? AsOnDate { get; set; }

        // Chart 1 – NPA Performance (already scaled by SP)
        public NpaBusinessPerformance Performance { get; set; } = new();

        // Chart 2 – NPA Portfolio breakdown (raw INR)
        public NpaBusinessPortfolio Portfolio { get; set; } = new();

        // Chart 3 – NPA Book / Growth (raw INR)
        public BusinessOutstandingBook OutstandingBook { get; set; } = new();
    }

    public class NpaBusinessPerformance
    {
        public decimal Npa { get; set; }
        public decimal Advances { get; set; }
        public string ScaleText { get; set; } = "";
    }

    /// <summary>
    /// NPA Portfolio breakdown — raw INR values.
    /// Columns from SP: CD_DEPOSITS (MSME), SB_DEPOSITS (Retail), TD_DEPOSITS (Agri),
    /// RETAIL_TERM_DEPOSITS (Corporate), total, gross_npa, others.
    /// </summary>
    public class NpaBusinessPortfolio
    {
        public decimal MsmeNpa      { get; set; }   // CD_DEPOSITS  col
        public decimal RetailNpa    { get; set; }   // SB_DEPOSITS  col
        public decimal AgriNpa      { get; set; }   // TD_DEPOSITS  col
        public decimal CorporateNpa { get; set; }   // RETAIL_TERM_DEPOSITS col
        public decimal Total        { get; set; }
        public decimal GrossNpa     { get; set; }
        public decimal Others       { get; set; }
        public string ScaleText { get; set; } = "";
    }


    // ═══════════════════════════════════════════════════════════════════════════
    //  PROFITABILITY  (SP_PAGETWO_DASHBOARD_PROFITABILITY)
    //  No scope parameter — single @SOL_ID call returns all profitability data
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds all data returned by SP_PAGETWO_DASHBOARD_PROFITABILITY in a single call.
    /// Used for both Income and Expense sections.
    /// </summary>
    public class ProfitabilityNewResult
    {
        public DateTime? AsOnDate { get; set; }

        /// <summary>RS 1: Fee Month-On-Month Comparison (multiple rows, one per AS_ON_DATE)</summary>
        public List<ProfitabilityMomRow> FeeMom { get; set; } = new();

        /// <summary>RS 2: Fee Portfolio — Interest and Fee amounts for the latest date</summary>
        public ProfitabilityPortfolioData Portfolio { get; set; } = new();

        /// <summary>RS 3: Interest Month-On-Month Comparison (multiple rows, one per AS_ON_DATE)</summary>
        public List<ProfitabilityMomRow> InterestMom { get; set; } = new();

        /// <summary>RS 4: Expense Month-On-Month Comparison (multiple rows, one per AS_ON_DATE)</summary>
        public List<ProfitabilityMomRow> ExpenseMom { get; set; } = new();
    }

    /// <summary>One row from the Fee/Interest MOM result sets.</summary>
    public class ProfitabilityMomRow
    {
        public DateTime AsOnDate { get; set; }
        public decimal Data { get; set; }
    }

    /// <summary>Fee Portfolio breakdown — raw INR values.</summary>
    public class ProfitabilityPortfolioData
    {
        public decimal Interest { get; set; }
        public decimal Fee { get; set; }
        public string ScaleText { get; set; } = "";
    }
}
