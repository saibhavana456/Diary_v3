namespace MyDiary.Web.Features.DitDiary.Models.DTO
{
    public class BudgetDashboardData
    {
        public decimal SpilloverAllocated { get; set; }
        public decimal FreshAllocated { get; set; }
        public decimal TotalAllocated { get; set; }
        public decimal UtilizedTill { get; set; }
        public decimal UtilizedSpilloverTill { get; set; }
        public decimal UtilizedFreshTill { get; set; }
        public decimal ThisMonth { get; set; }
        public decimal ProjectedNext { get; set; }
        public decimal ProjectedSpilloverNext { get; set; }
        public decimal ProjectedFreshNext { get; set; }
        public decimal Remaining => TotalAllocated - UtilizedTill;
    }
}