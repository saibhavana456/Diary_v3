using MyDiary.Web.Features.Business.Models;

public interface IChargeReportService
{
    Task SavePartAAsync(ChargePartAViewModel partA);
    Task SavePartBAsync(int reportId, ChargePartBViewModel partB);
    Task<ActiveChargeContextDto> GetActiveChargeForUserAsync(string employeeNo);

    /// <summary>Saves Part A as a draft (not submitted). Returns the ReportID.</summary>
    Task<int> SaveDraftPartAAsync(ChargePartAViewModel partA, int knownReportId = 0);

    /// <summary>Saves Part B as a draft (not submitted). Sets IsTakenOver=1.</summary>
    Task SaveDraftPartBAsync(int reportId, ChargePartBViewModel partB);

    /// <summary>Finalises Part A and marks it as submitted.</summary>
    Task<int> SubmitPartAAsync(ChargePartAViewModel partA, int knownReportId = 0);

    /// <summary>Finalises Part B and marks the report as taken over.</summary>
    Task SubmitPartBAsync(int reportId, ChargePartBViewModel partB);

    /// <summary>Loads a previously saved Part A draft for the given report.</summary>
    Task<ChargePartAViewModel?> LoadDraftPartAAsync(int reportId);

    /// <summary>Loads a previously saved Part B draft for the given report.</summary>
    Task<ChargePartBViewModel?> LoadDraftPartBAsync(int reportId);

    Task<string?> GetStaffNameByEmplIdAsync(string emplId);
}