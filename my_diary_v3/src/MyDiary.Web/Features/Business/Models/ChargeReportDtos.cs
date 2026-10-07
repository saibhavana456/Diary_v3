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

public class ChargeTakingOverDto
{
    public string OfficerName { get; set; } = "";
    public string EmployeeNo { get; set; } = "";
    public string Designation { get; set; } = "";
    public DateTime? DateReportedForDuty { get; set; }
    public DateTime? DateChargeTaken { get; set; }
    public string? Comments { get; set; }
}