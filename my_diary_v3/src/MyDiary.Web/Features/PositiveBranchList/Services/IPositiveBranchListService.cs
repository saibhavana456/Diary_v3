using MyDiary.Web.Features.PositiveBranchList.Models.DTO;

namespace MyDiary.Web.Features.PositiveBranchList.Services;

public interface IPositiveBranchListService
{
    /// <summary>Today's job-status row (creates one if this is the first check of the day).</summary>
    Task<PblJobStatus> GetTodayStatusAsync();

    /// <summary>Recent "sent communications" for the dashboard, newest first.</summary>
    Task<List<PblMailLogRow>> GetMailLogAsync(int days = 30);

    Task<(string FileName, byte[] Content)?> GetAttachmentAsync(long mailLogId);

    /// <summary>
    /// Core pipeline: checks T-1 data availability, runs PBL_AGGREGATE, builds and emails one
    /// zip-attached workbook per active zone, and logs everything. <paramref name="force"/> lets
    /// a manual send go ahead even if the automated job already sent today's mail (an operator
    /// override for corrections); the automated job always calls with force:false so "mail sent
    /// only once per day" holds for the unattended path.
    /// </summary>
    Task<PblRunResult> RunAggregationAndSendAsync(string triggeredBy, bool force);
}
