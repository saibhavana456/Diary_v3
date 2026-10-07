namespace MyDiary.Web.Features.DitDiary.Models.DTO
{
    public class BudgetDashboardStats
    {
        public double[] PieData { get; set; } = Array.Empty<double>();
        public string[] PieLabels { get; set; } = Array.Empty<string>();
        public List<string> DistinctFY { get; set; } = new();
        public List<string> DistinctVerticals { get; set; } = new();

        // Summary Aggregates
        public decimal TotalAllotted { get; set; }
        public decimal TotalUtilized { get; set; }
        public decimal TotalFresh { get; set; }
        public decimal TotalSpillover { get; set; }
        public string? SECTION { get; set; }
 
    }
}
