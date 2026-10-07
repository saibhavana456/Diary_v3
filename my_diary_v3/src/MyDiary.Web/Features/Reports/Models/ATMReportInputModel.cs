namespace MyDiary.Web.Features.Reports.Models
{
    public class ATMReportInputModel
    {
            public string ATM_Report_ID { get; set; } = default!;
            public string SolID { get; set; } = default!;
            public string Br_Code { get; set; } = default!;
            public string Br_Name { get; set; } = default!;
            public string RO_Code { get; set; } = default!;
            public string RO_Name { get; set; } = default!;
            public string ZO_Code { get; set; } = default!;
            public string ZO_Name { get; set; } = default!;

            public string Q1OPT { get; set; } = default!;
            public string Q2OPT { get; set; } = default!;
            public string Q3OPT { get; set; } = default!;
            public string Q4OPT { get; set; } = default!;
            public string Q5OPT { get; set; } = default!;
            public string ATMID { get; set; } = default!;

            public string PF_NAME_ENTRY { get; set; } = default!;
            public DateTime DATE_TIME_ENTRY { get; set; } = default!;   // e.g. "dd/MM/yyyy HH:mm"
            public DateTime MandatoryReportDate { get; set; } = default!; // e.g. "MM/dd/yyyy"
            public string ENTRY_BY { get; set; } = default!;

            public string STATUS { get; set; } = default!;
            public string STATUS_DESC { get; set; } = default!;

            public string ENTRY_MODE { get; set; } = default!;
        
    }
}
