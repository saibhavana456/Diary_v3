using MyDiary.Web.Features.Shared.Models;

namespace MyDiary.Web.Features.Shared.Services;

public interface IPageInfoService
{
   
    Task<PageInfoModel?> GetPageInfoAsync(string pageKey);
}
