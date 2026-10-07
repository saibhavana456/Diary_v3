namespace MyDiary.Web.Features.BranchFoundation.Models;

public class BranchFoundationDto
{
    public string SolId { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string ZoneName { get; set; } = "";
    public DateTime? EstDate { get; set; }

    // Derived Properties
    public int? YearsCompleted
    {
        get
        {
            if (!EstDate.HasValue) return null;
            var today = DateTime.Today;
            var age = today.Year - EstDate.Value.Year;
            if (EstDate.Value.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    public string AnniversaryDate => EstDate.HasValue ? EstDate.Value.ToString("dd MMM") : "-";
    public int? AnniversaryMonth => EstDate?.Month;

    // Calculates which financial year a given milestone (25, 50, 100) falls into.
    public bool IsMilestoneInCurrentFY(int milestoneYears, DateTime currentFyStart, DateTime currentFyEnd)
    {
        if (!EstDate.HasValue) return false;
        var milestoneDate = EstDate.Value.AddYears(milestoneYears);
        return milestoneDate >= currentFyStart && milestoneDate <= currentFyEnd;
    }
}