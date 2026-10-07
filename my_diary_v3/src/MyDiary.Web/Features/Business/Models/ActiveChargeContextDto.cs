public class ActiveChargeContextDto
{
    public int? ReportID { get; set; }
    public bool IsOutgoing { get; set; }
    public bool IsIncoming { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsTakenOver { get; set; }
    public string? IncomingEmployeeNo { get; set; }
}
