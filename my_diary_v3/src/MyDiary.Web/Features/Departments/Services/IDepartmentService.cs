using MyDiary.Web.Features.Departments.Models;

namespace MyDiary.Web.Features.Departments.Services
{
    public interface IDepartmentService
    {
        //Task<List<Department>> GetDepartmentsAsync();
        Task<List<Department>> GetActiveDepartmentsAsync(string sectionType = "Dept");
    }
}
