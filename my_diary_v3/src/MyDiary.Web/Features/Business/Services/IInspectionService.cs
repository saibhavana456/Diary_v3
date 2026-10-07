using MyDiary.Web.Features.Business.Models;

namespace MyDiary.Web.Features.Business.Services
{
    public interface IInspectionService
    {
        Task SaveInspection(MonthlyGoldInspectionEntity model);

        Task<List<Dictionary<string, object>>> GetSubmittedReports();
    }
}