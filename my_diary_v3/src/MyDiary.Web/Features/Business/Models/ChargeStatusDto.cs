public class ChargeStatusDto
{
    public int ReportId { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsTakenOver { get; set; }
    public string IncomingEmployeeNo { get; set; } = "";
}
