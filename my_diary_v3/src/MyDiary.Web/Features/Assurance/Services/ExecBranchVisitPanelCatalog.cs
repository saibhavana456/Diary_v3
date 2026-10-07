namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>
/// Maps "Executive Branch Visit" form rows (docx: Final_Branch_Visit_Format_Blue_colour
/// change.docx) to BUSINESS_360_PARAMETER_MASTER.PARAMETER_NAME values (xlsx:
/// Business_360_Parameter_Master.xlsx, 283 rows) so ExecutiveBranchVisit.razor can
/// auto-fill a cell from ExecVisitMetric.ActualsAsOn when a clear name match exists.
/// Only rows listed here are auto-filled; everything else on the form is manual entry.
/// See the task report for the full table-by-table auto/manual reasoning.
/// </summary>
public static class ExecBranchVisitPanelCatalog
{
    /// <summary>
    /// Section A, Table 1 "Current Business Figures" — 9 of 11 rows have a clear
    /// 1:1 PARAMETER_NAME match (DEPOSITS / ADVANCES / PRIORITY SECTOR categories).
    /// "NPA Recovery" and "Non-Interest Income" have no matching parameter in the
    /// xlsx master (closest is FEE_INCOME_OS_BAL, which is not the same concept as
    /// "Non-Interest Income" as a whole) — those two rows stay manual.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> BusinessFiguresMap = new Dictionary<string, string>
    {
        ["Total Deposits"] = "TOTAL_DEPOSITS",
        ["Current Deposits"] = "CURRENT_DEPOSITS",
        ["Savings Deposits"] = "SAVINGS_DEPOSITS",
        ["Term Deposits"] = "TOTAL_TERM_DEPOSITS",
        ["Total Advances"] = "TOTAL_STANDARD_ADVANCES",
        ["Priority Sector"] = "TOTAL_PRIORITY",
        ["Agriculture"] = "AGRI_ADVANCES",
        ["MSME"] = "MSME_ADVANCES",
        ["Retail"] = "TOTAL_RETAIL",
        // ["NPA Recovery"]         -> no match, manual
        // ["Non-Interest Income"]  -> no clear single-parameter match, manual
    };

    /// <summary>
    /// Section C "Impersonal Accounts Position" — Sundry &amp; Suspense sub-tables.
    /// The xlsx "Suspense and Sundry Entries" category tracks entries/amounts by
    /// prev-month/prev-day/current-day snapshot, not by the docx's 180/90/&lt;90-day
    /// ageing buckets, so this is a category-level match, not an exact field match:
    /// only the "No of Entries" and "Value" top-line cells are auto-filled (from the
    /// latest current-day figures); the ageing-bucket breakdown and Remarks stay
    /// manual because no ageing-bucketed parameter exists in the xlsx master.
    /// "POB" (part iii of the same table) has no matching category at all in the
    /// xlsx master and is entirely manual.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SuspenseMap = new Dictionary<string, string>
    {
        ["No of Entries"] = "suspense_cur_day_entries",
        ["Value"] = "suspense_cur_day_amt",
    };

    public static readonly IReadOnlyDictionary<string, string> SundryMap = new Dictionary<string, string>
    {
        ["No of Entries"] = "sundry_cur_day_entries",
        ["Value"] = "sundry_cur_day_amt",
    };
}
