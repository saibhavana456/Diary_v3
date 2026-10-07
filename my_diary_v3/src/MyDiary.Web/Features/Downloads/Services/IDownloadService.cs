using MyDiary.Web.Features.Downloads.Models;

namespace MyDiary.Web.Features.Downloads.Services
{
    public interface IDownloadService
    {
        Task<List<Download>> GetDownloadsAsync();
    }
}
