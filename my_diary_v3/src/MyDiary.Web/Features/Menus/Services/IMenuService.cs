using MyDiary.Web.Features.Departments.Models;
using MyDiary.Web.Features.Menus.Models;

namespace MyDiary.Web.Features.Menus.Services
{
    public interface IMenuService
    {
        Task<List<MenuModel>> GetMenusAsync();

        Task<List<Department_New>> GetDepartmentsAsync();
    }
}
