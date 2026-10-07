using MyDiary.Web.Features.DitDiary.Models.DTO;
using MyDiary.Web.Features.DitDiary.Models.Entities;

namespace MyDiary.Web.Features.DitDiary.Interfaces
{
    public interface IProjectMasterService
    {
        // General List & Dashboard
        Task<List<ProjectMaster>> GetProjectMastersAsync();

        string GetProjectTimelineStatus(string? status, DateTime? expectedCompletion);

        Task<List<ProjectsMaster>> GetProjectsMastersAsync();

        Task<bool> AddFullProjectEntryAsync(ProjectsMaster proj, ProjectMilestone m, ProjectIssue i, ProjectResource r, Document d);
        ProjectDashboardStats GetDashboardStats(List<ProjectMaster> projects);


        // Add to IProjectMasterService.cs

        Task<ProjectMilestone?> GetMilestoneByProjectAsync(string projectCode);
        Task<ProjectIssue?> GetIssueByProjectAsync(string projectCode);
        Task<ProjectResource?> GetResourceByProjectAsync(string projectCode);
        Task<Document?> GetDocumentByProjectAsync(string projectCode);



        // Standardize list fetching names
        Task<List<ProjectMilestone>> GetMilestoneListByProjectAsync(string projectCode);
        Task<List<ProjectIssue>> GetIssueListByProjectAsync(string projectCode);
        Task<List<ProjectResource>> GetResourceListByProjectAsync(string projectCode);
        Task<List<Document>> GetDocumentListByProjectAsync(string projectCode);

        // Tab-by-Tab Save Methods    

        Task<bool> SaveMilestoneAsync(ProjectMilestone m);
        Task<bool> SaveIssueAsync(ProjectIssue i);
        Task<bool> SaveResourceAsync(ProjectResource r);
        Task<bool> SaveDocumentAsync(Document d);



        // Individual CRUD
        Task<bool> AddProjectAsync(ProjectsMaster project);
        Task<bool> UpdateProjectAsync(ProjectsMaster project); 
        Task<bool> DeleteProjectAsync(int projectId);


        Task<ProjectsMaster?> SaveProjectGeneralAsync(ProjectsMaster proj);
        Task<List<ProjectMilestone>> GetMilestonesByProjectAsync(string projectCode);
        Task<List<ProjectIssue>> GetIssuesByProjectAsync(string projectCode);
        Task<List<ProjectResource>> GetResourcesByProjectAsync(string projectCode);
        Task<List<Document>> GetDocumentsByProjectAsync(string projectCode);



        // Utilities
        byte[] GenerateExcelExport(IEnumerable<ProjectMaster> projects);
        string GetProjectTimelineStatus(ProjectMaster project);
        IEnumerable<ProjectMaster> FilterProjects(List<ProjectMaster> projects, string search, string status, string timelineFilter = "All", string department = "All", string section = "All");
        Task<(int SuccessCount, string ErrorMessage)> UploadProjectExcelAsync(Stream fileStream);
        byte[] GenerateProjectTemplate();
    }
}