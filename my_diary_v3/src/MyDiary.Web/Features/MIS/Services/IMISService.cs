using MyDiary.Web.Features.MIS.Models;
using MyDiary.Web.Features.Reports.Models;

namespace MyDiary.Web.Features.MIS.Services
{
    public interface IMISService
    {
        Task<List<MISModel>> GetAllMISDataAsync();

        Task<UserResponse?> GetTicketAsync(string userId);
    }
}
