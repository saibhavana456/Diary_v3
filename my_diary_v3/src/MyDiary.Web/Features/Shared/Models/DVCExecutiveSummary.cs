namespace MyDiary.Web.Features.Shared.Models;

public class DVCExecutiveSummary
{
    public int TotalRequestReceived { get; set; }

    public int SubmittedWithinTimeline { get; set; }

    public int SubmittedBeyondTimeline { get; set; }

    public int PendingWithinTimeline { get; set; }

    public int PendingBeyondTimeline { get; set; }

    public decimal TatPercentage { get; set; }
}