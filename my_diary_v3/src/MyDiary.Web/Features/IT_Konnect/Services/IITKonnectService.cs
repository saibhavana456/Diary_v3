using MyDiary.Web.Features.Downloads.Models;

namespace MyDiary.Web.Features.ITKonnect.Services
{
    public interface IITKonnectService
    {
        Task<List<Download>> GetItemsAsync();
    }
}