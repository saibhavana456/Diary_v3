namespace MyDiary.Web.Features.OperationDashboard.Models
{
    // Maps directly to the SQL stored procedure output for TableName = 'SundryOSReport'
    public class SundryOSReportDto
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

    // A helper record for clean UI rendering and safe math calculations
    public record VariationMetrics
    {
        public decimal AbsoluteChange { get; init; }
        public decimal PercentageChange { get; init; }

        // Secure Math: Prevents DivideByZeroException which would crash the Blazor circuit
        public static VariationMetrics Calculate(decimal currentValue, decimal previousValue)
        {
            decimal absolute = currentValue - previousValue;
            decimal percentage = previousValue == 0 ? 0 : (absolute / previousValue) * 100;

            return new VariationMetrics
            {
                AbsoluteChange = absolute,
                PercentageChange = percentage
            };
        }
    }
}