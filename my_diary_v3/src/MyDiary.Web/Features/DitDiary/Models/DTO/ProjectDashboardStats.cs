namespace MyDiary.Web.Features.DitDiary.Models.DTO
{
    public class ProjectDashboardStats
    {
        public string[] PieLabels { get; set; } = Array.Empty<string>();
        public double[] PieData { get; set; } = Array.Empty<double>();
        public decimal TotalCost { get; set; }
        public List<string> DistinctStatuses { get; set; } = new();

        public int ActiveProjectsCount { get; set; }
        public int TotalVendorsCount { get; set; }
        public int TotalProjects { get; set; }
        public int OnTrackCount { get; set; }
        public int DelayedCount { get; set; }
        public int DroppedCount { get; set; }
        public decimal AverageProjectCost => TotalProjects > 0 ? TotalCost / TotalProjects : 0;

        public string[] VendorLabels { get; set; } = Array.Empty<string>();
        public double[] VendorData { get; set; } = Array.Empty<double>();

        public string[] TimelineLabels { get; set; } = Array.Empty<string>();
        public double[] TimelineData { get; set; } = Array.Empty<double>();
        

        public double[] TimelineStatusData { get; set; } = Array.Empty<double>();
        public string[] TimelineStatusLabels { get; set; } = new[] { "On Track", "Delayed", "Dropped" };
    }

}
