namespace MyDiary.Web.Features.OperationDashboard.Models
{
    public class CashHoldingReportDto
    {
        // Present Day (Current)
        public DateTime PrevDayDt { get; set; }
        public decimal PrevDayLimit { get; set; }
        public decimal PrevDayTotal { get; set; } // Cash Holding Actual

        // Previous Day
        public DateTime PrevDay2Dt { get; set; }
        public decimal PrevDay2Limit { get; set; }
        public decimal PrevDay2Total { get; set; } // Cash Holding Actual

        // Previous Month (Average Range)
        public DateTime PrevMonthFromDt { get; set; }
        public DateTime PrevMonthDt { get; set; }
        public decimal PrevMonthLimit { get; set; }
        public decimal PrevMonthTotal { get; set; } // Cash Holding Actual

        public string TableName { get; set; }
    }
}