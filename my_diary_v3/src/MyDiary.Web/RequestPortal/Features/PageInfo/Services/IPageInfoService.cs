using PageInfoRecord = RequestPortal.Web.Features.PageInfo.Models.PageInfo;
using PageInfoForm    = RequestPortal.Web.Features.PageInfo.Models.PageInfoForm;

namespace RequestPortal.Web.Features.PageInfo.Services;

public interface IPageInfoService
{
    Task<PageInfoRecord?>       GetAsync(string pageKey);
    Task<List<PageInfoRecord>>  GetAllAsync();
    Task<bool>                  UpsertAsync(PageInfoForm form, bool isNew);
    Task<bool>                  DeleteAsync(string pageKey);
}
