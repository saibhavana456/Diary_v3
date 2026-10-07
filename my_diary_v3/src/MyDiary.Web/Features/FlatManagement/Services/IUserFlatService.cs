using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IUserFlatService
{
    Task<ViewAllocatedFlatResult> GetAllocatedFlatAsync(string pfNo);
}
