namespace MyDiary.Web.Features.Business.Models
{
    public class ChargeHandingOverDto
    {
        public string OfficerName { get; set; } = "";
        public string EmployeeNo { get; set; } = "";
        public string Designation { get; set; } = "";
        public bool IsJointCustodian { get; set; }
        public DateTime? DateChargeTaken { get; set; }
        public DateTime? DateRelieved { get; set; }
        public string? Comments { get; set; }
    }
}