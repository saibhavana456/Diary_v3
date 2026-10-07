namespace MyDiary.Web.Features.Landing_Dashboard_V2_1.Models
{
    /// <summary>
    /// Holds all data returned by SP_PAGEONE_DASHBOARD in a single call.
    /// </summary>
    public class PageOneDashboardResult
    {
        public DateTime? AsOnDate { get; set; }

        // Chart 1 – Business KPIs (% target achieved)
        public Chart1Kpi Chart1 { get; set; } = new();

        // Chart 2 – Total Advances Performance (scaled)
        public ChartPerformance Chart2 { get; set; } = new();

        // Chart 3 – Total Advances Growth (historical)
        public ChartGrowth Chart3 { get; set; } = new();

        // Chart 4 – Advances Portfolio breakdown
        public Chart4Portfolio Chart4 { get; set; } = new();

        // Chart 5 – Total Business trend
        public ChartGrowth Chart5 { get; set; } = new();

        // Chart 6 – Total Deposits Performance (scaled)
        public ChartPerformance Chart6 { get; set; } = new();

        // Chart 7 – Total Deposits Growth (historical)
        public ChartGrowth Chart7 { get; set; } = new();

        // Chart 8 – Deposits Portfolio breakdown
        public Chart8Portfolio Chart8 { get; set; } = new();

        // Additional rows (PARAMETER_ID 27-50) keyed by PARAMETER_NAME
        public Dictionary<string, AdditionalDataItem> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	}

    public class Chart1Kpi
    {
        public decimal Deposits { get; set; }
        public decimal Casa { get; set; }
        public decimal Advances { get; set; }
        public decimal Business { get; set; }
    }

    /// <summary>Shared shape for Charts 2 and 6 (performance with scaling).</summary>
    public class ChartPerformance
    {
        public decimal CurrentFy { get; set; }
        public decimal TargetFy { get; set; }
        public decimal ActualTotal { get; set; }
        public string ScaleText { get; set; } = "";
    }

    /// <summary>Shared shape for Charts 3, 5, and 7 (historical trend).</summary>
    public class ChartGrowth
    {
		public decimal Base { get; set; }
		public decimal LastFy { get; set; }
        public decimal LastQuarter { get; set; }
        public decimal LastMonth { get; set; }
        public decimal AsOn { get; set; }
    }

    public class Chart4Portfolio
    {
        public decimal MsmeAdvances { get; set; }
        public decimal RetailAdvances { get; set; }
        public decimal AgricultureAdvances { get; set; }
        public decimal CorporateAdvances { get; set; }
    }

    public class Chart8Portfolio
    {
        public decimal CdDeposits { get; set; }
        public decimal SbDeposits { get; set; }
        public decimal TdDeposits { get; set; }
        public decimal RetailTermDeposits { get; set; }
    }

	public class AdditionalDataItem
	{
		public decimal Value { get; set; }
		public DateTime? AsOnDate { get; set; }
		public int CardId { get; set; }
	}

}
