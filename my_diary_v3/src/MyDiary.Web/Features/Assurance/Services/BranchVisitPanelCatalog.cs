namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>
/// Fixed row/column shape for every table in the "RAH: Branch Visit Report"
/// (Assurance 360). Mirrors <see cref="RoVisitPanelCatalog"/> in structure and
/// reuses the same <see cref="RoVisitPanelDef"/>/<see cref="RoVisitPanelColumn"/>/
/// <see cref="RoVisitPanelRow"/> record types and the same generic RP_RO_VISIT_METRIC
/// child table (PanelCode/RowCode/ColCode) — only the panel codes are new, so no
/// schema change was needed for these grids. Panels not present in
/// <see cref="AutoFetchMap"/> are entirely manual entry (checklists, registers,
/// ambience, controls/security) even though a few of them are still rendered with the
/// auto-fetch-eligible grid layout for visual consistency with the RO form.
/// </summary>
public static class BranchVisitPanelCatalog
{
    private static readonly RoVisitPanelColumn ValueCol = new("VALUE", "Value");

    public static readonly IReadOnlyList<RoVisitPanelDef> Panels = new List<RoVisitPanelDef>
    {
        // 3. KYC — identical shape to RO's KYC panel.
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

        // 4. Data Cleansing Pendency (continuation of KYC table, own panel number in the RO form's convention).
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

        // 5. Credit Monitoring & Advance Controls.
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

        // 5.1 Gold Loan Controls (manual checklist, continuation of Credit Monitoring).
        new("GOLD_LOAN", "Gold Loan Controls", "Checkpoint",
            new[]
            {
                new RoVisitPanelColumn("STATUS", "Status (Yes/No/NA)"),
                new RoVisitPanelColumn("REMARKS", "Remarks"),
            },
            new[]
            {
                new RoVisitPanelRow("NORMS_ADHERED", "Gold loan norms strictly adhered"),
                new RoVisitPanelRow("REAPPRAISAL", "Re-appraisal of gold loans done as per extant guidelines"),
                new RoVisitPanelRow("CCTV_COVERAGE", "Gold loan appraisal covered under CCTV"),
                new RoVisitPanelRow("SAFE_REGISTER", "Gold safe in-and-out register maintained"),
            }),

        // 6. Stressed Accounts.
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

        // 7. Suspense, Sundry, POB & Impersonal Accounts.
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

        // 8. Deposit Operations, Dormant/Inactive Accounts, DEA & Nomination.
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

        // 9. Customer Service, Complaints & BO/CPGRAMS.
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
                new RoVisitPanelRow("CS_INDEX_SCORE", "CS / CSEC Index Score"),
                new RoVisitPanelRow("CS_RANK", "Rank"),
                new RoVisitPanelRow("CUST_STAFF_FEEDBACK", "Customer / Staff feedback"),
            }),

        // 8.1 Customer Service Compliance Checklist (manual).
        new("CS_CHECKLIST", "Customer Service Compliance Checklist", "Checkpoint",
            new[]
            {
                new RoVisitPanelColumn("STATUS", "Status (Yes/No/NA)"),
                new RoVisitPanelColumn("REMARKS", "Remarks"),
            },
            new[]
            {
                new RoVisitPanelRow("NOTICE_BOARD", "Comprehensive notice board displayed"),
                new RoVisitPanelRow("ECASH_SMS_NOTICE", "Notice for e-cash receipt in SMS form displayed near cash receipt counter"),
                new RoVisitPanelRow("CVO_ZH_RM_ADDRESS", "Addresses of CVO / Zonal Head / Regional Manager displayed"),
                new RoVisitPanelRow("BUSINESS_HOURS", "Business hours displayed"),
                new RoVisitPanelRow("COMPLAINT_BOX", "Complaint box available and accessible"),
                new RoVisitPanelRow("CUSTOMER_MEETS", "Periodical customer meets held and minutes available"),
                new RoVisitPanelRow("DOORSTEP_BANKING", "Doorstep banking service extended to eligible senior citizens / differently-abled persons"),
                new RoVisitPanelRow("FEEDBACK_REVIEWED", "Customer feedback reviewed and actionable points recorded"),
            }),

        // 9. Cash Holding.
        new("CASH_HOLDING", "Cash Holding", "Parameter",
            new[] { ValueCol },
            new[]
            {
                new RoVisitPanelRow("CRL", "CRL / Cash Retention Limit"),
                new RoVisitPanelRow("CASH_IN_HAND", "Cash in hand"),
                new RoVisitPanelRow("EXCESS_CASH", "Excess Cash"),
                new RoVisitPanelRow("PREV_MONTH_AVG_CASH", "Previous month average cash holding"),
                new RoVisitPanelRow("PREV_DAY_CASH", "Previous day cash holding"),
                new RoVisitPanelRow("PRESENT_DAY_CASH", "Present day cash holding"),
                new RoVisitPanelRow("DENOM_REGISTER_VERIFIED", "Cash denomination register verified by Branch Head (Yes/No)"),
                new RoVisitPanelRow("CASH_WITHIN_CRL", "Cash maintained within CRL (Yes/No)"),
            }),

        // 9. Locker, Death Claim & ATM/Channels (continuation of Cash Holding).
        new("LOCKER_ATM", "Locker, Death Claim & ATM / Channels", "Parameter",
            new[] { ValueCol },
            new[]
            {
                new RoVisitPanelRow("TOTAL_LOCKERS", "Total Lockers"),
                new RoVisitPanelRow("OCCUPIED_LOCKERS", "Occupied Lockers"),
                new RoVisitPanelRow("VACANT_LOCKERS", "Vacant Lockers"),
                new RoVisitPanelRow("OVERDUE_LT1Y", "Overdue < 1 year"),
                new RoVisitPanelRow("OVERDUE_1TO3Y", "Overdue 1-3 years"),
                new RoVisitPanelRow("OVERDUE_GT3Y", "Overdue > 3 years"),
                new RoVisitPanelRow("TOTAL_OVERDUE", "Total overdue accounts / amount"),
                new RoVisitPanelRow("DEATH_CLAIM_UPTO10D", "Death claim pendency upto 10 days"),
                new RoVisitPanelRow("DEATH_CLAIM_ABOVE15D", "Death claim pendency Above 15 days"),
                new RoVisitPanelRow("LIVE_ATMS", "Live ATMs"),
                new RoVisitPanelRow("ATM_CASH_POSITION", "ATM cash position"),
                new RoVisitPanelRow("ATM_ISSUE_OBSERVED", "ATM / channel issue observed"),
            }),

        // 10. Maintenance of Registers & Housekeeping (manual).
        new("REGISTERS", "Maintenance of Registers & Housekeeping", "Register / Record",
            new[]
            {
                new RoVisitPanelColumn("MAINTAINED", "Maintained (Yes/No/NA)"),
                new RoVisitPanelColumn("LAST_VERIFIED", "Last Verified On"),
                new RoVisitPanelColumn("REMARKS", "Remarks"),
            },
            new[]
            {
                new RoVisitPanelRow("LOAN_APP_REGISTER", "Loan Application Register"),
                new RoVisitPanelRow("DOCUMENT_REGISTER", "Document Register"),
                new RoVisitPanelRow("INSURANCE_REGISTER", "Insurance Register"),
                new RoVisitPanelRow("CASH_KEY_REGISTER", "Cash Key Register"),
                new RoVisitPanelRow("GOLD_SAFE_REGISTER", "Gold Safe Register"),
                new RoVisitPanelRow("COMPLAINT_REGISTER", "Complaint Register"),
                new RoVisitPanelRow("BM_DIARY", "Branch Manager Diary"),
                new RoVisitPanelRow("CASH_SAFE_INOUT", "Cash safe in-and-out register"),
                new RoVisitPanelRow("GOLD_SAFE_INOUT", "Gold safe in-and-out register"),
                new RoVisitPanelRow("SECURITY_ITEMS_REGISTER", "Security items safe custody register"),
            }),

        // 11. Exercise of Controls & Security (manual).
        new("CONTROLS_SECURITY", "Exercise of Controls & Security", "Control Checkpoint",
            new[]
            {
                new RoVisitPanelColumn("STATUS", "Status (Yes/No/NA)"),
                new RoVisitPanelColumn("REMARKS_GAPS", "Remarks / Gaps"),
            },
            new[]
            {
                new RoVisitPanelRow("MANDATORY_REPORTS", "All mandatory reports generated / scrutinized"),
                new RoVisitPanelRow("FINACLE_EXCEPTIONS", "Finacle exception reports attended on daily basis"),
                new RoVisitPanelRow("PASSWORD_SECRECY", "Password secrecy maintained"),
                new RoVisitPanelRow("DUAL_CONTROL_KEYS", "Dual control over cash and gold safe keys maintained"),
                new RoVisitPanelRow("SECURITY_ALARM", "Security alarm functioning and last security officer visit recorded"),
                new RoVisitPanelRow("CCTV_FUNCTIONING", "CCTV cameras functioning and coverage adequate"),
                new RoVisitPanelRow("STAMPS_VERIFICATION", "Physical verification of Indian stamps / stamps on hand balanced with Finacle"),
                new RoVisitPanelRow("SECURITY_ITEMS_CUSTODY", "Security items like cheque books / debit cards / credit cards and loose cheques kept in safe custody"),
            }),

        // 12. Audit, Compliance & Rectification.
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
                new RoVisitPanelRow("IDPMS_PENDENCY", "IDPMS pendency"),
                new RoVisitPanelRow("CONCURRENT_AUDIT_PENDING", "Concurrent audit pending observations"),
                new RoVisitPanelRow("SPECIAL_AUDIT_PENDING", "Special audit / report pendency"),
                new RoVisitPanelRow("STAFF_ACCOUNTABILITY", "Staff accountability cases"),
            }),

        // Branch Ambience and Infrastructure (manual).
        new("AMBIENCE", "Branch Ambience and Infrastructure", "Parameter",
            new[] { new RoVisitPanelColumn("AVAILABILITY", "Availability (Y/N)") },
            new[]
            {
                new RoVisitPanelRow("RAMP_FACILITY", "Ramp facility"),
                new RoVisitPanelRow("SEATING_ARRANGEMENT", "Seating Arrangement"),
                new RoVisitPanelRow("DRINKING_WATER", "Drinking water"),
                new RoVisitPanelRow("MAY_I_HELP_YOU", "May I help you counter"),
                new RoVisitPanelRow("AMBIENCE_CLEANLINESS", "Overall ambience / Cleanliness (Comments)"),
            }),
    };

    /// <summary>(PanelCode, RowCode, ColCode) -> (BUSINESS_360 PARAMETER_NAME, use BaseCurrentFy
    /// instead of ActualsAsOn). Reuses the exact same parameter names as RoVisitPanelCatalog for
    /// the panels that are shared in shape (KYC/DATA_CLEANSING/CREDIT_MON/DEPOSIT_OPS/CUST_SERVICE/
    /// AUDIT_COMP) — usp_ROVisit_GetMetrics is called with the Branch's own SOL ID (same @BranchId
    /// convention as the RO report), so these auto-populate identically once BUSINESS_360_DATA
    /// carries a row for that Branch SOL. Only cells with a real matching parameter appear here;
    /// everything else (including all manual-only panels below) stays manual entry.</summary>
    public static readonly IReadOnlyDictionary<(string Panel, string Row, string Col), (string ParamName, bool UseBaseFy)> AutoFetchMap =
        new Dictionary<(string, string, string), (string, bool)>
        {
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

            [("DATA_CLEANSING", "ANNUAL_INCOME", "PENDING")] = ("Annual Income", false),
            [("DATA_CLEANSING", "ANNUAL_TURNOVER", "PENDING")] = ("Annual Turnover", false),
            [("DATA_CLEANSING", "OVD", "PENDING")] = ("OVD", false),
            [("DATA_CLEANSING", "PAN_FORM60", "PENDING")] = ("PAN / Form60", false),
            [("DATA_CLEANSING", "CONSTITUTION_CODE", "PENDING")] = ("Constitution Code", false),
            [("DATA_CLEANSING", "OCCUPATION", "PENDING")] = ("Occupation", false),
            [("DATA_CLEANSING", "DATE_OF_INCORPORATION", "PENDING")] = ("Date of Incorporation", false),
            [("DATA_CLEANSING", "CUSTOMER_TYPE", "PENDING")] = ("Customer Type", false),

            [("CREDIT_MON", "RENEWAL_LT30", "VALUE")] = ("Release Pendency < 30 days", false),
            [("CREDIT_MON", "RENEWAL_GT30", "VALUE")] = ("Release Pendency > 30 days", false),
            [("CREDIT_MON", "REL_SEC_RETAIL", "VALUE")] = ("Renewal - Retail", false),
            [("CREDIT_MON", "REL_SEC_AGRI", "VALUE")] = ("Renewal - Agri", false),
            [("CREDIT_MON", "REL_SEC_MSME", "VALUE")] = ("Renewal - MSME", false),
            [("CREDIT_MON", "CERSAI_PENDENCY", "VALUE")] = ("CERSAI Satisfaction Pending", false),
            [("CREDIT_MON", "STOCK_STMT_90", "VALUE")] = ("Stock Statement > 90 days", false),
            [("CREDIT_MON", "QUICK_MORTALITY", "VALUE")] = ("Quick Mortality A/cs", false),

            [("DEPOSIT_OPS", "DORMANT", "VALUE")] = ("Dormant", false),
            [("DEPOSIT_OPS", "INACTIVE", "VALUE")] = ("Inactive", false),
            [("DEPOSIT_OPS", "NO_NOMINATION", "VALUE")] = ("No Nomination", false),
            [("DEPOSIT_OPS", "DEA_FUND_ACCTS", "VALUE")] = ("DEAF Accounts", false),
            [("DEPOSIT_OPS", "LIEN_MARKED", "VALUE")] = ("Lien Marked Accounts", false),

            [("CUST_SERVICE", "CRM_PENDING_BOY", "VALUE")] = ("CRM Complaints Pending", false),
            [("CUST_SERVICE", "PENDING_BEYOND_TAT", "VALUE")] = ("Complaints Beyond TAT", false),
            [("CUST_SERVICE", "BO_PENDING", "VALUE")] = ("BO Pending Complaints", false),
            [("CUST_SERVICE", "COMPLAINTS_BEYOND_7D", "VALUE")] = ("Complaints Beyond 7 Days", false),
            [("CUST_SERVICE", "CPGRAMS_PENDING", "VALUE")] = ("CPGRAMs Pending", false),

            [("AUDIT_COMP", "COR_RELEASED", "VALUE")] = ("COR Reports Released", false),
            [("AUDIT_COMP", "COR_PENDING", "VALUE")] = ("COR Pending", false),
            [("AUDIT_COMP", "FLASH_RELEASED", "VALUE")] = ("FLASH Reports Released", false),
            [("AUDIT_COMP", "FLASH_PENDING", "VALUE")] = ("FLASH Pending", false),
            [("AUDIT_COMP", "LOI_PENDENCY", "VALUE")] = ("LOI Pendency (Rs lakh)", false),
            [("AUDIT_COMP", "SPOT_RECTIFICATION", "VALUE")] = ("Spot Rectification", false),
            [("AUDIT_COMP", "EDPMS_UPTO_10L", "VALUE")] = ("EDPMS Outstanding upto Rs 10 L", false),
            [("AUDIT_COMP", "EDPMS_ABOVE_10L", "VALUE")] = ("EDPMS Outstanding above Rs 10 L", false),

            // Cash Holding / Locker / ATM — CASH_LOCKER category, added to
            // usp_ROVisit_GetMetrics specifically for this report (see GAP_05).
            [("CASH_HOLDING", "CRL", "VALUE")] = ("CRL (INR Lacs)", false),
            [("CASH_HOLDING", "CASH_IN_HAND", "VALUE")] = ("Cash in Hand (INR Lacs)", false),
            [("CASH_HOLDING", "EXCESS_CASH", "VALUE")] = ("Excess Cash", false),
            [("LOCKER_ATM", "TOTAL_LOCKERS", "VALUE")] = ("Total Lockers", false),
            [("LOCKER_ATM", "VACANT_LOCKERS", "VALUE")] = ("Vacant Lockers", false),
            [("LOCKER_ATM", "OCCUPIED_LOCKERS", "VALUE")] = ("Occupied Lockers", false),
            [("LOCKER_ATM", "DEATH_CLAIM_UPTO10D", "VALUE")] = ("Death Claim Upto 10 Days", false),
            [("LOCKER_ATM", "DEATH_CLAIM_ABOVE15D", "VALUE")] = ("Death Claim Above 15 Days", false),
            [("LOCKER_ATM", "LIVE_ATMS", "VALUE")] = ("Live ATMs", false),
            [("LOCKER_ATM", "TOTAL_OVERDUE", "VALUE")] = ("Locker Overdue", false),
            [("LOCKER_ATM", "ATM_CASH_POSITION", "VALUE")] = ("ATM Cash Position", false),
        };

    public static readonly IReadOnlyList<(string SrNo, string FocusArea)> ExecSummaryFocusAreas = new List<(string, string)>
    {
        ("1", "Critical control / compliance issues observed"),
        ("2", "High pendency items requiring immediate closure"),
        ("3", "Customer service / complaint issues"),
        ("4", "Credit monitoring concerns"),
        ("5", "Cash, locker, ATM, security or housekeeping concerns"),
        ("6", "Best practices / positive observations"),
    };

    public static readonly IReadOnlyList<(string Code, string Label)> AssessmentAreas = new List<(string, string)>
    {
        ("CONTROL_ENV", "Overall control environment"),
        ("COMPLIANCE_CULTURE", "Compliance culture at branch"),
        ("CUST_SERVICE_QUALITY", "Customer service quality"),
        ("HOUSEKEEPING", "Operational housekeeping"),
        ("CREDIT_MON_QUALITY", "Credit monitoring quality"),
    };
}
