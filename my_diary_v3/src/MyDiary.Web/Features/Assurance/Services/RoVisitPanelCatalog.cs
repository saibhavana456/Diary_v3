namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>Column definition within a RO Visit Report panel (tables 3-9 + 10).</summary>
public sealed record RoVisitPanelColumn(string ColCode, string Label);

/// <summary>Row definition within a panel — RowCode is the stable DB key, Label is what's shown.</summary>
public sealed record RoVisitPanelRow(string RowCode, string Label);

public sealed record RoVisitPanelDef(string PanelCode, string Title, string RowLabelHeader,
    IReadOnlyList<RoVisitPanelColumn> Columns, IReadOnlyList<RoVisitPanelRow> Rows);

/// <summary>
/// Fixed row/column shape for every auto-populated table in the RO Visit Report
/// (tables 3-9, plus table 10 "Audit, Compliance &amp; Rectification" which is the
/// same shape as the others despite not being SOL-driven per se). These shapes come
/// directly from RO_Visit_Format_ZRAH.docx and are not admin-configurable — unlike
/// Assurance Snapshot's panel grid, there's no reason for these to vary per report.
///
/// <see cref="AutoFetchMap"/> is the deliberately-approximate mapping from each
/// (PanelCode, RowCode) to a BUSINESS_360_PARAMETER_MASTER parameter name, built by
/// matching the docx's field names against what's actually seeded in
/// BUSINESS_360_PARAMETER_MASTER today. Several docx rows have no matching parameter
/// there (they were tracked nowhere before this report existed) and are intentionally
/// left out of the map — those cells stay blank/manual-entry. A few mappings (the
/// Credit Monitoring "Renewal"/"Release" pair in particular — the two systems use
/// those words in swapped senses) are a best-effort content match, not a confirmed
/// 1:1 identity; flagged here for the business owner to verify.
/// </summary>
public static class RoVisitPanelCatalog
{
    private static readonly RoVisitPanelColumn ValueCol = new("VALUE", "Value");

    public static readonly IReadOnlyList<RoVisitPanelDef> Panels = new List<RoVisitPanelDef>
    {
        new("KYC", "KYC, CKYC, Re-KYC & Transaction Monitoring", "Parameter",
            new[]
            {
                new RoVisitPanelColumn("PREV_FY", "Previous FY / Opening Pendency"),
                new RoVisitPanelColumn("ADDED", "Added During FY"),
                new RoVisitPanelColumn("COMPLETED", "Completed During FY"),
                new RoVisitPanelColumn("PRESENT", "Present Pendency"),
            },
            new[]
            {
                new RoVisitPanelRow("RE_KYC", "Re-KYC"),
                new RoVisitPanelRow("CKYC", "CKYC"),
                new RoVisitPanelRow("LEGACY_CKYC", "Legacy CKYC / CKYC on Re-KYC"),
                new RoVisitPanelRow("EDD", "EDD"),
                new RoVisitPanelRow("NEGATIVE_LIST", "Negative List"),
                new RoVisitPanelRow("SMULE", "SMULE"),
                new RoVisitPanelRow("VAJRA", "Vajra"),
                new RoVisitPanelRow("ALERTS_PENDENCY", "Alerts Pendency"),
                new RoVisitPanelRow("DOWNGRADATION", "Downgradation during Periodic Review"),
            }),

        new("DATA_CLEANSING", "Data Cleansing Pendency", "Parameter",
            new[]
            {
                new RoVisitPanelColumn("PENDING", "Pending / Deficient Accounts"),
                new RoVisitPanelColumn("REMARKS", "Remarks / Action Plan"),
            },
            new[]
            {
                new RoVisitPanelRow("ANNUAL_INCOME", "Annual Income"),
                new RoVisitPanelRow("ANNUAL_TURNOVER", "Annual Turnover"),
                new RoVisitPanelRow("OVD", "OVD"),
                new RoVisitPanelRow("PAN_FORM60", "PAN / Form 60"),
                new RoVisitPanelRow("CONSTITUTION_CODE", "Constitution Code"),
                new RoVisitPanelRow("OCCUPATION", "Occupation"),
                new RoVisitPanelRow("DATE_OF_INCORPORATION", "Date of Incorporation"),
                new RoVisitPanelRow("CUSTOMER_TYPE", "Customer Type"),
                new RoVisitPanelRow("BENEFICIAL_OWNER", "Beneficial Owner Update"),
                new RoVisitPanelRow("UCIC_UPDATION", "UCIC Updation"),
            }),

        new("CREDIT_MON", "Credit Monitoring & Advance Controls", "Parameter",
            new[] { ValueCol },
            new[]
            {
                new RoVisitPanelRow("RENEWAL_LT30", "Renewal Pendency < 30 days"),
                new RoVisitPanelRow("RENEWAL_GT30", "Renewal Pendency > 30 days"),
                new RoVisitPanelRow("RENEWAL_TOTAL", "Renewal Pendency Total"),
                new RoVisitPanelRow("REL_SEC_RETAIL", "Release of Security Documents - Retail"),
                new RoVisitPanelRow("REL_SEC_AGRI", "Release of Security Documents - Agri"),
                new RoVisitPanelRow("REL_SEC_MSME", "Release of Security Documents - MSME / Others"),
                new RoVisitPanelRow("CERSAI_PENDENCY", "CERSAI Satisfaction Pendency"),
                new RoVisitPanelRow("STOCK_STMT_90", "Stock Statement Pendency > 90 Days"),
                new RoVisitPanelRow("QUICK_MORTALITY", "Quick Mortality Accounts"),
                new RoVisitPanelRow("PENDING_NACH", "Pending NACH"),
                new RoVisitPanelRow("NACH_WAIVER", "Waiver of NACH permitted"),
                new RoVisitPanelRow("ACC_PENDING_DISB", "Accounts pending disbursement"),
                new RoVisitPanelRow("LOAN_REPAID_NOT_CLOSED", "Loan fully repaid but not closed"),
                new RoVisitPanelRow("NEG_AMORTISATION", "Negative amortisation"),
                new RoVisitPanelRow("EDU_LOAN_INT", "Education Loan where interest not capitalised"),
                new RoVisitPanelRow("RATING_PENDENCY", "Pendency in rating"),
                new RoVisitPanelRow("FRAUD_EXAM_PENDENCY", "Pendency in Fraud Examination"),
            }),

        new("STRESSED", "Stressed Accounts", "Stressed Account Category",
            new[]
            {
                new RoVisitPanelColumn("NUM_ACCTS", "No. of A/cs"),
                new RoVisitPanelColumn("AMOUNT_OS", "Amount O/S"),
            },
            new[]
            {
                new RoVisitPanelRow("SMA0", "SMA-0"),
                new RoVisitPanelRow("SMA1", "SMA-1"),
                new RoVisitPanelRow("SMA2", "SMA-2"),
            }),

        new("SUSPENSE_A", "Suspense, Sundry, POB & Impersonal Accounts", "Account / Entry Type",
            new[]
            {
                new RoVisitPanelColumn("PREV_MONTH_NO", "Previous Month No."),
                new RoVisitPanelColumn("PREV_MONTH_AMT", "Previous Month Amount"),
                new RoVisitPanelColumn("PRESENT_DAY_NO", "Present Day No."),
                new RoVisitPanelColumn("PRESENT_DAY_AMT", "Present Day Amount"),
            },
            new[]
            {
                new RoVisitPanelRow("SUSPENSE_ENTRIES", "Suspense Entries"),
                new RoVisitPanelRow("SUNDRY_ENTRIES", "Sundry Entries"),
                new RoVisitPanelRow("POB_CASH_2D", "POB - Cash above 2 days"),
                new RoVisitPanelRow("POB_OTHERS_15D", "POB - Others above 15 days"),
            }),

        new("SUSPENSE_B", "Ageing — Suspense / Sundry / POB", "Category",
            new[]
            {
                new RoVisitPanelColumn("OVER_90", "> 90 days"),
                new RoVisitPanelColumn("OVER_180", "> 180 days"),
            },
            new[]
            {
                new RoVisitPanelRow("SUSPENSE_ACCOUNTS", "Suspense Accounts"),
                new RoVisitPanelRow("SUNDRY_DEPOSITS", "Sundry Deposits"),
                new RoVisitPanelRow("POB", "POB"),
            }),

        new("DEPOSIT_OPS", "Deposit Operations, Dormant / Inactive Accounts, DEA & Nomination", "Parameter",
            new[] { ValueCol },
            new[]
            {
                new RoVisitPanelRow("DORMANT", "Dormant Accounts"),
                new RoVisitPanelRow("INACTIVE", "Inactive Accounts"),
                new RoVisitPanelRow("NO_NOMINATION", "No Nomination accounts"),
                new RoVisitPanelRow("DEA_FUND_ACCTS", "DEA Fund - No. of accounts"),
                new RoVisitPanelRow("DEA_FUND_AMT", "DEA Fund Amount"),
                new RoVisitPanelRow("AAPKI_PUNJI", "Aapki Punji Aapka Adhikar cases"),
                new RoVisitPanelRow("LIEN_MARKED", "Lien marked accounts"),
                new RoVisitPanelRow("LIEN_AMOUNT", "Lien amount"),
                new RoVisitPanelRow("ACTIVATION_STATUS", "Activation monitoring status"),
            }),

        new("CUST_SERVICE", "Customer Service, Complaints & BO / CPGRAMS", "Parameter",
            new[] { ValueCol },
            new[]
            {
                new RoVisitPanelRow("CRM_PENDING_BOY", "CRM complaints pending as on Beginning of Year"),
                new RoVisitPanelRow("RECEIVED_FY", "Received during FY"),
                new RoVisitPanelRow("RESOLVED_FY", "Resolved during FY"),
                new RoVisitPanelRow("PENDING_WITHIN_TAT", "Pending within TAT"),
                new RoVisitPanelRow("PENDING_BEYOND_TAT", "Pending beyond TAT"),
                new RoVisitPanelRow("TOTAL_PENDING", "Total pending"),
                new RoVisitPanelRow("BO_PENDING", "BO pending complaints"),
                new RoVisitPanelRow("COMPLAINTS_BEYOND_7D", "Complaints beyond 7 days"),
                new RoVisitPanelRow("CPGRAMS_PENDING", "CPGRAMS pending"),
            }),

        new("AUDIT_COMP", "Audit, Compliance & Rectification", "Parameter",
            new[] { ValueCol },
            new[]
            {
                new RoVisitPanelRow("COR_RELEASED", "COR reports released"),
                new RoVisitPanelRow("COR_PENDING", "COR pending"),
                new RoVisitPanelRow("COR_BEYOND_TAT", "Beyond TAT (COR)"),
                new RoVisitPanelRow("FLASH_RELEASED", "FLASH reports released"),
                new RoVisitPanelRow("FLASH_PENDING", "FLASH pending"),
                new RoVisitPanelRow("FLASH_BEYOND_TAT", "Beyond TAT (FLASH)"),
                new RoVisitPanelRow("LOI_PENDENCY", "LOI pendency (₹ lakh)"),
                new RoVisitPanelRow("SPOT_RECTIFICATION", "Spot rectification status"),
                new RoVisitPanelRow("FALSE_COMPLIANCE", "False compliance, if any"),
                new RoVisitPanelRow("EDPMS_UPTO_10L", "EDPMS outstanding upto ₹10 lakh"),
                new RoVisitPanelRow("EDPMS_ABOVE_10L", "EDPMS above ₹10 lakh"),
                new RoVisitPanelRow("IDPMS_UPTO_10L", "IDPMS outstanding upto ₹10 lakh"),
                new RoVisitPanelRow("IDPMS_ABOVE_10L", "IDPMS outstanding above ₹10 lakh"),
                new RoVisitPanelRow("CONCURRENT_AUDIT_PENDING", "Concurrent audit pending observations"),
                new RoVisitPanelRow("SPECIAL_AUDIT_PENDING", "Special audit / report pendency"),
                new RoVisitPanelRow("STAFF_ACCOUNTABILITY", "Staff accountability cases"),
            }),
    };

    /// <summary>(PanelCode, RowCode, ColCode) -> (BUSINESS_360 PARAMETER_NAME, use BaseCurrentFy instead of ActualsAsOn).
    /// Only cells with a real matching parameter appear here — everything else stays manual.</summary>
    public static readonly IReadOnlyDictionary<(string Panel, string Row, string Col), (string ParamName, bool UseBaseFy)> AutoFetchMap =
        new Dictionary<(string, string, string), (string, bool)>
        {
            // KYC — Present Pendency = ActualsAsOn, Previous FY = BaseCurrentFy
            [("KYC", "RE_KYC", "PRESENT")] = ("ReKYC", false),
            [("KYC", "RE_KYC", "PREV_FY")] = ("ReKYC", true),
            [("KYC", "CKYC", "PRESENT")] = ("CKYC", false),
            [("KYC", "CKYC", "PREV_FY")] = ("CKYC", true),
            [("KYC", "LEGACY_CKYC", "PRESENT")] = ("Legacy CKYC", false),
            [("KYC", "LEGACY_CKYC", "PREV_FY")] = ("Legacy CKYC", true),
            [("KYC", "EDD", "PRESENT")] = ("EDD Pendency", false),
            [("KYC", "SMULE", "PRESENT")] = ("Smule", false),
            [("KYC", "VAJRA", "PRESENT")] = ("Vajra", false),
            [("KYC", "NEGATIVE_LIST", "PRESENT")] = ("Negative List", false),

            // Data Cleansing — Pending/Deficient = ActualsAsOn
            [("DATA_CLEANSING", "ANNUAL_INCOME", "PENDING")] = ("Annual Income", false),
            [("DATA_CLEANSING", "ANNUAL_TURNOVER", "PENDING")] = ("Annual Turnover", false),
            [("DATA_CLEANSING", "OVD", "PENDING")] = ("OVD", false),
            [("DATA_CLEANSING", "PAN_FORM60", "PENDING")] = ("PAN / Form60", false),
            [("DATA_CLEANSING", "CONSTITUTION_CODE", "PENDING")] = ("Constitution Code", false),
            [("DATA_CLEANSING", "OCCUPATION", "PENDING")] = ("Occupation", false),
            [("DATA_CLEANSING", "DATE_OF_INCORPORATION", "PENDING")] = ("Date of Incorporation", false),
            [("DATA_CLEANSING", "CUSTOMER_TYPE", "PENDING")] = ("Customer Type", false),

            // Credit Monitoring — best-effort match, "Renewal"/"Release" senses are swapped
            // between this report's wording and BUSINESS_360's parameter names; verify with
            // the business before trusting these two rows specifically.
            [("CREDIT_MON", "RENEWAL_LT30", "VALUE")] = ("Release Pendency < 30 days", false),
            [("CREDIT_MON", "RENEWAL_GT30", "VALUE")] = ("Release Pendency > 30 days", false),
            [("CREDIT_MON", "REL_SEC_RETAIL", "VALUE")] = ("Renewal - Retail", false),
            [("CREDIT_MON", "REL_SEC_AGRI", "VALUE")] = ("Renewal - Agri", false),
            [("CREDIT_MON", "REL_SEC_MSME", "VALUE")] = ("Renewal - MSME", false),
            [("CREDIT_MON", "CERSAI_PENDENCY", "VALUE")] = ("CERSAI Satisfaction Pending", false),
            [("CREDIT_MON", "STOCK_STMT_90", "VALUE")] = ("Stock Statement > 90 days", false),
            [("CREDIT_MON", "QUICK_MORTALITY", "VALUE")] = ("Quick Mortality A/cs", false),

            // Suspense/Sundry/POB — Present Day = ActualsAsOn, Previous Month = BaseCurrentFy
            [("SUSPENSE_A", "SUSPENSE_ENTRIES", "PRESENT_DAY_NO")] = ("Suspense - No. of Entries", false),
            [("SUSPENSE_A", "SUSPENSE_ENTRIES", "PRESENT_DAY_AMT")] = ("Suspense - Outstanding (Lacs)", false),
            [("SUSPENSE_A", "SUSPENSE_ENTRIES", "PREV_MONTH_NO")] = ("Suspense - No. of Entries", true),
            [("SUSPENSE_A", "SUSPENSE_ENTRIES", "PREV_MONTH_AMT")] = ("Suspense - Outstanding (Lacs)", true),
            [("SUSPENSE_A", "SUNDRY_ENTRIES", "PRESENT_DAY_NO")] = ("Sundry - No. of Entries", false),
            [("SUSPENSE_A", "SUNDRY_ENTRIES", "PRESENT_DAY_AMT")] = ("Sundry - Outstanding (Lacs)", false),
            [("SUSPENSE_A", "SUNDRY_ENTRIES", "PREV_MONTH_NO")] = ("Sundry - No. of Entries", true),
            [("SUSPENSE_A", "SUNDRY_ENTRIES", "PREV_MONTH_AMT")] = ("Sundry - Outstanding (Lacs)", true),
            [("SUSPENSE_A", "POB_CASH_2D", "PRESENT_DAY_NO")] = ("POB Cash - No. Of Entries", false),
            [("SUSPENSE_A", "POB_CASH_2D", "PRESENT_DAY_AMT")] = ("POB Cash - Outstanding", false),
            [("SUSPENSE_A", "POB_OTHERS_15D", "PRESENT_DAY_NO")] = ("POB Others - No. Of Entries", false),
            [("SUSPENSE_A", "POB_OTHERS_15D", "PRESENT_DAY_AMT")] = ("POB Others - Outstanding", false),
            [("SUSPENSE_B", "SUSPENSE_ACCOUNTS", "OVER_90")] = ("Suspense > 90 days", false),

            // Deposit Ops / Dormant / DEA / Nomination
            [("DEPOSIT_OPS", "DORMANT", "VALUE")] = ("Dormant", false),
            [("DEPOSIT_OPS", "INACTIVE", "VALUE")] = ("Inactive", false),
            [("DEPOSIT_OPS", "NO_NOMINATION", "VALUE")] = ("No Nomination", false),
            [("DEPOSIT_OPS", "DEA_FUND_ACCTS", "VALUE")] = ("DEAF Accounts", false),
            [("DEPOSIT_OPS", "LIEN_MARKED", "VALUE")] = ("Lien Marked Accounts", false),

            // Customer Service / Complaints / BO / CPGRAMS
            [("CUST_SERVICE", "CRM_PENDING_BOY", "VALUE")] = ("CRM Complaints Pending", false),
            [("CUST_SERVICE", "PENDING_BEYOND_TAT", "VALUE")] = ("Complaints Beyond TAT", false),
            [("CUST_SERVICE", "BO_PENDING", "VALUE")] = ("BO Pending Complaints", false),
            [("CUST_SERVICE", "COMPLAINTS_BEYOND_7D", "VALUE")] = ("Complaints Beyond 7 Days", false),
            [("CUST_SERVICE", "CPGRAMS_PENDING", "VALUE")] = ("CPGRAMs Pending", false),

            // Audit, Compliance & Rectification
            [("AUDIT_COMP", "COR_RELEASED", "VALUE")] = ("COR Reports Released", false),
            [("AUDIT_COMP", "COR_PENDING", "VALUE")] = ("COR Pending", false),
            [("AUDIT_COMP", "FLASH_RELEASED", "VALUE")] = ("FLASH Reports Released", false),
            [("AUDIT_COMP", "FLASH_PENDING", "VALUE")] = ("FLASH Pending", false),
            [("AUDIT_COMP", "LOI_PENDENCY", "VALUE")] = ("LOI Pendency (Rs lakh)", false),
            [("AUDIT_COMP", "SPOT_RECTIFICATION", "VALUE")] = ("Spot Rectification", false),
            [("AUDIT_COMP", "EDPMS_UPTO_10L", "VALUE")] = ("EDPMS Outstanding upto Rs 10 L", false),
            [("AUDIT_COMP", "EDPMS_ABOVE_10L", "VALUE")] = ("EDPMS Outstanding above Rs 10 L", false),
            [("AUDIT_COMP", "IDPMS_UPTO_10L", "VALUE")] = ("IDPMS Outstanding upto Rs 10 L", false),
            [("AUDIT_COMP", "IDPMS_ABOVE_10L", "VALUE")] = ("IDPMS Outstanding above Rs 10 L", false),
        };

    public static readonly IReadOnlyList<(string SrNo, string FocusArea)> ExecSummaryFocusAreas = new List<(string, string)>
    {
        ("1", "Critical control / compliance issues observed"),
        ("2", "High pendency items requiring immediate closure"),
        ("3", "Customer service / complaint issues"),
        ("4", "Credit monitoring concerns"),
        ("5", "Best practices / positive observations"),
    };

    public static readonly IReadOnlyList<(string Code, string Label)> AssessmentAreas = new List<(string, string)>
    {
        ("CONTROL_ENV", "Overall control environment"),
        ("COMPLIANCE_CULTURE", "Compliance culture across Branches"),
        ("CUST_SERVICE_QUALITY", "Customer service quality"),
        ("CREDIT_MON_QUALITY", "Credit monitoring quality"),
    };
}
