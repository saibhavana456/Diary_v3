using MyDiary.Web.Features.IPDirectory.Models;

namespace MyDiary.Web.Features.IPDirectory.Services
{
    public interface IIPDirectoryService
    {
       Task<IPDirectoryResult> GetDirectoryAsync(string dept, string search, int page, int pageSize);
        Task<bool> AddDirectoryEntryAsync(
    IPDirectoryEntry entry,
    string userId);

        Task<bool> UpdateDirectoryEntryAsync(
            IPDirectoryEntry entry,
            string userId);

        Task<bool> DeleteDirectoryEntryAsync(
    string name,
    string userId);

        Task<(bool Success, string Message)> ImportIPDirectoryExcelAsync(
    Stream stream,
    string userId);

        Task<byte[]> ExportIPDirectoryExcelAsync();

    }
}
