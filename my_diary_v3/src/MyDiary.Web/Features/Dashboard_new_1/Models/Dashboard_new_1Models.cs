namespace MyDiary.Web.Features.Dashboard_new_1.Models
{
    /// <summary>
    /// A single KPI row returned by SP_PAGETHREE_DASHBOARD_KPI.
    /// Values are raw (paise/units) — divide by 10,000,000 for Crores display.
    /// </summary>
    public class KpiItem1
    {
        public string ParameterName        { get; set; } = "";
        public decimal ActualLastToLastFY  { get; set; }
        public decimal ActualLastFY        { get; set; }
        public decimal ActualLastQtr       { get; set; }
        public decimal ActualAsOnDate      { get; set; }
        public decimal TargetNextQtr       { get; set; }
        public decimal TargetNextMar       { get; set; }
        public decimal GrowthOver          { get; set; }
        public decimal GapToTarget         { get; set; }
    }

    /// <summary>
    /// Wraps the full SP result: the KPI rows plus the dynamic column header strings
    /// extracted from the SP's dynamic-SQL column names.
    /// </summary>
    public class KpiDashboard1Result
    {
        public List<KpiItem1> Items           { get; set; } = new();

        // Dynamic header labels (populated from SP column names)
        public string HeaderLastToLastFY      { get; set; } = "";
        public string HeaderLastFY            { get; set; } = "";
        public string HeaderLastQtr           { get; set; } = "";
        public string HeaderAsOnDate          { get; set; } = "";
        public string HeaderTargetQtr         { get; set; } = "";
        public string HeaderTargetFY          { get; set; } = "";
        public string HeaderGrowthOver        { get; set; } = "";
        public string HeaderTargetFYGap       { get; set; } = "";
    }
}
