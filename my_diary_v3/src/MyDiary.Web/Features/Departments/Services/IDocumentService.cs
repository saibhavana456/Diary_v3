using MyDiary.Web.Features.Departments.Models;

namespace MyDiary.Web.Features.Departments.Services
{
    public interface IDocumentService
    {
        Task<DocumentResult> GetDocumentsByActivityAsync(int activityId, string search, int page, int pageSize);

        Task<List<Document>> GetChildDocumentsByParentUploadedByAsync(int parentDocumentId);
    }


}
