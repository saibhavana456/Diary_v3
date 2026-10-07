namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>Generic (Item x Column) manual-entry table shape, used for the many
/// checklist/register tables in Sections B-F that have no BUSINESS_360_DATA
/// equivalent (row labels + column labels lifted verbatim from
/// Final_Branch_Visit_Format_Blue_colour change.docx).
/// <paramref name="OpenEnded"/>: true for tables whose real-world entry count is
/// inherently variable per visit (register-style "Sr.No / description / ..." tables
/// where the docx's numbered placeholder rows are just "however many fit on a printed
/// page", not a business limit). Open-ended tables render with Add-Row/Remove-Row
/// instead of the fixed Rows array. See ExecutiveBranchVisit.razor for the per-table
/// decision list.</summary>
public sealed record ExecVisitTable(string Title, string[] Columns, string[] Rows, bool OpenEnded = false);

/// <summary>
/// Row/column catalog for Sections B, C, D, E, F of the "Executive Branch Visit" form.
/// Section A is handled specially in ExecutiveBranchVisit.razor (its Table 1/2/5/8
/// have fixed numeric-grid shapes, some auto-filled — see ExecBranchVisitPanelCatalog).
/// Every table here is manual entry: all are Yes/No/rating checklists, registers, or
/// free-text remarks with no corresponding parameter in Business_360_Parameter_Master.xlsx.
/// </summary>
public static class ExecVisitFormCatalog
{
    private static readonly string[] ClxCols = { "Satisfactory (Yes/No)", "Rating (1-5)", "Remarks" };
    private static readonly string[] RespCols = { "Response (Yes/No)", "Rating (1-5)", "Remarks" };

    /// <summary>
    /// Section A, Tables 2-8 (Table 1 is the auto-filled business-figures grid,
    /// handled specially in the .razor). None of these have a matching parameter
    /// in the xlsx master (no "average a/c opening", "leads", "negativity",
    /// "loss making" or "BMDP delegation use" category exists there), so — despite
    /// the task's initial hint that 1/2/5/8 auto-populate — only Table 1 actually
    /// has real source data; 2, 5 and 8 are manual here (see final report deviation note).
    /// </summary>
    public static readonly IReadOnlyList<ExecVisitTable> SectionA_Extra = new List<ExecVisitTable>
    {
        new("2. Average A/c Opening Per Day in Last Month", new[] { "Number of Accounts" }, new[] { "SB", "CD", "RTD", "Retail", "Agri", "MSME" }),
        new("3. Pending Files for Sanction", new[] { "Customer Name", "Delegation", "Amount (Lacs)", "Pending Since", "Remarks" }, new[] { "1", "2", "3" }, OpenEnded: true),
        new("4. Pending for Disbursement", new[] { "Customer Name", "Amount (Lacs)", "Pending Since", "Remarks" }, new[] { "1", "2", "3" }, OpenEnded: true),
        new("5. New Leads (FY 2026-27 till date, sent to RLP/MLP)", new[] { "No of Accounts", "Amount (Lacs)", "No of A/cs/Amount Converted" }, new[] { "Retail Leads", "MSME Leads", "Agri Leads", "Corporate Leads" }),
        new("6. Negativity", new[] { "Negative Parameter", "Negativity Amount (Lacs)", "Expected Date of Positivity", "Remarks" }, new[] { "1", "2", "3" }, OpenEnded: true),
        new("7. Loss Making", new[] { "Response" }, new[] { "Whether the branch is in loss? (Yes/No)", "Loss amount as per Last Quarter (Lacs)", "Strategy to come out of loss", "Expected Time Period to come in Profit" }),
        new("8. Delegation Use (FY 2026-27 till date) — BMDP Figure", new[] { "No of A/cs", "Amount (Lacs)" }, new[] { "BMDP" }),
    };

    public static readonly IReadOnlyList<ExecVisitTable> SectionB = new List<ExecVisitTable>
    {
        new("1. Cleanliness", ClxCols, new[]
        {
            "Customer Lobby/Sitting Area","PCs Working Condition","Floor","Washroom","Locker Area",
            "Front Counter","Fan","Whether Branch Renovation Required","Wall Painting Done in Year",
            "Pest Control Status","ATM Lobby"
        }),
        new("2. Availability/Working Condition of Other Facilities", RespCols, new[]
        {
            "PCs","Passbook Printer","Other Printers","Scanner","Tablet","UPS & Battery","Locker",
            "Fire Extinguisher","Air conditioning","Drinking water","Lighting","Record Room",
            "Customer Chair Available in Customer Lobby","Safe Vault (Cash)","Safe Vault (Gold)",
            "Branch Glow Sign board","ATM Sign board","Counter Signage (Cash/Loan/SWO)","Male Toilet",
            "Female Toilet","Ramp Facility","FRFC/Cabinets (Fire Resistant Filing Cabinet)","CCTV",
            "Alarm (Fire & Emergency)","Biometric Position"
        }),
        new("3. Notice Board/Display of Important Information", new[] { "Availability (Yes/No)", "Rating (1-5)", "Remarks" }, new[]
        {
            "Business Hours","BCSBI","Ombudsman","CVO (Name & Add)","ZH/RH (Name & Contact No)",
            "Service Charges","Interest Rate Chart","Important Contacts (Local Authorities)",
            "Complaint Box","Suggestion Register","Cash Notice (Soiled/Mutilated Exchange)",
            "Product Availability (Locker/Forex/Etc)","Branding"
        }),
        new("4. Record Keeping", new[] { "Response", "Rating (1-5)", "Remarks" }, new[]
        {
            "Oldest Records Availability Date","Old records — Date of last sending to outsourced Agency",
            "Proper Upkeeping of Records"
        }),
        new("5. Register Maintenance", new[] { "Response (Yes/No)", "Rating (1-5)", "Remarks (On properly maintained or not)" }, new[]
        {
            "EM Register","Security Item Register","Cash In/Out Register","Attendance Register",
            "Cash/Vault Key Register","Gold Safe In/Out","Key Movement Register","Complaint Register","Cash Book"
        }),
    };

    public static readonly IReadOnlyList<ExecVisitTable> SectionC = new List<ExecVisitTable>
    {
        new("General Controls", RespCols, new[]
        {
            "Credit File Properly Kept","EM/Title Deed Properly Kept",
            "Surprise Cash Visit Report Held in Record","Security Docs kept in Fireproof Cabinet"
        }),
        new("1. Housekeeping", RespCols, new[] { "Bundle Making", "General Filing", "Voucher Binding" }),
        // 2. Impersonal Accounts Position — Sundry (i) and Suspense (ii) are rendered
        // specially in the .razor with auto-fill for No of Entries/Value. POB (iii)
        // has no matching xlsx category and is rendered here as fully manual.
        new("2.iii. Impersonal Accounts — POB", new[] { "No of Entries", "Value", "Outstanding >180 days", "Outstanding 90-180 days", "Outstanding <90 days", "Remarks" }, new[] { "POB" }),
        new("2.iv. Locker", new[] { "Value" }, new[] { "Total No. of Locker", "No. of Lockers rented out", "Amt (Lacs). of Locker rent overdue" }),
        new("2.v. ReKYC/CKYC/EDD Pendency", new[] { "Pendency", "Remarks" }, new[] { "CKYC Pendency", "Re-KYC", "EDD" }),
        new("2.vi. Cash Holding Limit", new[] { "Value (Actual)" }, new[] { "Cash holding limit", "Daily Average Cash holdings during current Financial", "Remarks" }),
        new("2.vii. ATM Reconciliation", new[] { "Yes/No", "Amount (Lacs)" }, new[] { "ATM Cash tallied with Finacle" }),
    };

    public static readonly IReadOnlyList<ExecVisitTable> SectionD = new List<ExecVisitTable>
    {
        new("1. CMRD Visit", new[] { "Response", "Last Visit Date", "Irregularities Closure (Yes/No)" }, new[] { "CMRD Visit" }),
        new("i. SMA (No of A/c & Amount — Retail/Agri/MSME)", new[] { "No of A/c - Retail", "No of A/c - Agri", "No of A/c - MSME", "Amount (Lacs) - Retail", "Amount (Lacs) - Agri", "Amount (Lacs) - MSME" }, new[] { "SMA-0", "SMA-1", "SMA-2", "Overall stress %" }),
        new("ii. Review/Renewal Pendency", new[] { "Response", "Remarks" }, new[]
        {
            "No of Accounts Due for Renewal","Renewal Pending for more than 3 months",
            "Renewal Pending for >2 & <3 months","Renewal Pending for >1 & <2 months",
            "Renewal Pending up to 1 month"
        }),
    };

    public static readonly IReadOnlyList<ExecVisitTable> SectionE = new List<ExecVisitTable>
    {
        new("Status of COR Submission", new[] { "Previous Year - Date of Audit", "Previous Year - Date of Submission", "Current Year - Date of Audit", "Current Year - Date of Submission" }, new[]
        {
            "Internal Audit","Statutory / Concurrent","Revenue / RBI","Stock Audit","Electrical Audit","IS Audit"
        }),
        new("Status of Latest Internal Audit Details", new[] { "No of Accounts", "Date of Closure" }, new[] { "Pending COR", "Flash Report", "Special Report" }),
        new("Nature of Irregularities", new[] { "Nature of Irregularities (Fraud/Leakage of Income)", "Details of irregularities", "Action Taken", "Position as on Date" }, new[] { "1", "2", "3" }, OpenEnded: true),
        new("Documents Completion Status", new[] { "Status (Yes/No)" }, new[]
        {
            "Account Opening Form Duly Filled","Loan Application","Security Documents"
        }),
    };

    public static readonly IReadOnlyList<ExecVisitTable> SectionF = new List<ExecVisitTable>
    {
        new("Views / Open Items", new[] { "Views", "Open Items" }, new[] { "Branch Staff", "Visiting Officer" }),
    };
}
