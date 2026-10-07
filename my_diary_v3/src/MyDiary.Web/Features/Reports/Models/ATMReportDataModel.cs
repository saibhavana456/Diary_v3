namespace MyDiary.Web.Features.Reports.Models
{
    public class ATMReportDataModel
    {
        public string ATM_Report_ID { get; set; }
        public string SolID { get; set; }
        public string Br_Code { get; set; }
        public string Br_Name { get; set; }
        public string RO_Code { get; set; }
        public string RO_Name { get; set; }
        public string ZO_Code { get; set; }
        public string ZO_Name { get; set; }

        public string MandatoryReportDate { get; set; }   // string because NVARCHAR2(15)
        public string Q1OPT { get; set; }
        public string Q2OPT { get; set; }
        public string Q3OPT { get; set; }
        public string Q4OPT { get; set; }
        public string Q5OPT { get; set; }

        public string ATMID { get; set; }

        public string ENTRY_BY { get; set; }
        public DateTime? ENTRY_DATE { get; set; }
        public string PF_NAME_ENTRY { get; set; }

        public string DATE_TIME_ENTRY { get; set; }        // NVARCHAR2(500)
        public string STATUS { get; set; }
        public string Status_Desc { get; set; }

        public DateTime? ResponseSubmittedDate { get; set; }
        public string MODIFY_BY { get; set; }
        public DateTime? MODIFY_DATE { get; set; }

        public string Submit_by { get; set; }
        public DateTime? Submit_Date { get; set; }

    }
}
