namespace MyDiary.Web.Features.OperationDashboard.Models
{
    public class UCICAadhaarReportDto
    {
        // Present Day
        public DateTime PrevDayDt { get; set; }
        public decimal PrevDayTotal { get; set; }

        // Previous Day
        public DateTime PrevDay2Dt { get; set; }
        public decimal PrevDay2Total { get; set; }

        // Previous Month
        public DateTime PrevMonthDt { get; set; }
        public decimal PrevMonthTotal { get; set; }

        public string TableName { get; set; }
    }
}