using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IUserGrievanceService
{
    Task<SubmitGrievanceResult> SubmitGrievanceAsync(SubmitGrievanceRequest request);

    Task<List<TrackGrievanceRow>> GetMyGrievancesAsync(string pfNo);

    Task<SubmitFeedbackResult> SubmitFeedbackAsync(SubmitFeedbackRequest request);
}
