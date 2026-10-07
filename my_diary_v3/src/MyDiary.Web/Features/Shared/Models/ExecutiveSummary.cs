namespace MyDiary.Web.Features.Shared.Models
{
    public class ExecutiveSummary
    {
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int CompletedRequests { get; set; }
        public decimal TatCompliancePercent { get; set; }
        public int DelayedRequests { get; set; }
    }

}
