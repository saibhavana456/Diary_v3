using MyDiary.Web.Features.Departments.Models;

namespace MyDiary.Web.Features.Departments.Services
{
    public interface IActivityService
    {
        Task<List<Activity>> GetActivitiesByDeptIdAsync(int deptId);
    }
}
