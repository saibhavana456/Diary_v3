namespace MyDiary.Web.Features.Dashboard_new.Models
{
    public class Zone { public string ZoneName { get; set; } = ""; public string ZoneSolid { get; set; } = ""; }
    public class Region { public string RegionName { get; set; } = ""; public string RegionSolid { get; set; } = ""; }
    public class Branch { public string BranchName { get; set; } = ""; public string SolId { get; set; } = ""; }

    public class KpiItem
    {
        public string ParameterName { get; set; } = "";
        // Values are kept as decimal? (nullable) for calculation
        public decimal ActualLastToLastFY { get; set; }
        public decimal ActualLastFY { get; set; }
        public decimal ActualLastQtr { get; set; }
        public decimal ActualPrevDate { get; set; }
        public decimal ActualAsOnDate { get; set; }
        public decimal TargetNextQtr { get; set; }
        public decimal TargetNextMar { get; set; }

        // These dates come from the SP for dynamic headers
        public DateTime LastFYDt { get; set; }
        public DateTime LastToLastFYDt { get; set; }
        public DateTime NextQtrDt { get; set; }
        public DateTime NextFYDt { get; set; }
        public DateTime KPIDate { get; set; } // As On Date
    }
}
