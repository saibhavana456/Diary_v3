namespace MyDiary.Web.Features.Shared.Models
{
    /// <summary>
    /// Status vocabulary for the monthly ATR MIS reports. SP returns codes C / NC / NS.
    /// </summary>
    public static class AtrStatus
    {
        public const string Complied = "Submitted-complied";
        public const string NotComplied = "Submitted-not complied";
        public const string NotSubmitted = "Not submitted";

        public static string FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
        {
            "C" => Complied,
            "NC" => NotComplied,
            _ => NotSubmitted
        };

        public static string Short(string? text) => text switch
        {
            Complied => "C",
            NotComplied => "NC",
            _ => "NS"
        };

        public static string Css(string? text) => text switch
        {
            Complied => "atr-c",
            NotComplied => "atr-nc",
            _ => "atr-ns"
        };
    }

    /// <summary>
    /// Effective filter emitted by ATRMonthFilter. Zone/Region/Branch are already
    /// resolved against the logged-in user's privilege (null = no restriction).
    /// </summary>
    public record AtrFilterSelection(
        DateTime Month,
        string? Zone,
        string? Region,
        string? Branch,
        string? ZoneName,
        string? RegionName,
        string? BranchName);

    /// <summary>
    /// Pivoted result of a monthly ATR report. Columns + Rows + FooterRows are shaped so the
    /// same data drives both the on-screen table and DynamicReportService.ExportToExcel.
    /// </summary>
    public class AtrGrid
    {
        public List<ColumnConfig> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public List<Dictionary<string, object>> FooterRows { get; set; } = new();
        public List<DateTime> Days { get; set; } = new();

        /// <summary>Report-2 branch header (branch_name, branch_code, ro_code, ro_name, zone_name).</summary>
        public Dictionary<string, object> Header { get; set; } = new();
    }
}
