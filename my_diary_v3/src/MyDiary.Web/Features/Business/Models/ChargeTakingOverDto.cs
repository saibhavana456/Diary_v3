namespace MyDiary.Web.Features.Business.Models
{
    public class ChargeTakingOverDto
    {
        public string OfficerName { get; set; } = "";
        public string EmployeeNo { get; set; } = "";
        public string Designation { get; set; } = "";
        public DateTime? DateReportedForDuty { get; set; }
        public DateTime? DateChargeTaken { get; set; }
        public string? Comments { get; set; }
    }
}