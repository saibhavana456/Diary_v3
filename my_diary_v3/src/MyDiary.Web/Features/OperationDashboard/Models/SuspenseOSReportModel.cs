namespace MyDiary.Web.Features.OperationDashboard.Models
{
    public class SuspenseOSReportDto
    {
        public DateTime PrevDayDt { get; set; }
        public int PrevDayEntries { get; set; }
        public decimal PrevDayTotal { get; set; }

        public int PrevDayEntries90Days { get; set; }
        public decimal PrevDayTotal90Days { get; set; }

        public int PrevDayEntries180Days { get; set; }
        public decimal PrevDayTotal180Days { get; set; }

        public DateTime PrevDay2Dt { get; set; }
        public int PrevDay2Entries { get; set; }
        public decimal PrevDay2Total { get; set; }

        public DateTime PrevMonthDt { get; set; }
        public int PrevMonthEntries { get; set; }
        public decimal PrevMonthTotal { get; set; }

        public string TableName { get; set; }
    }
}