using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Departments.Models;
using MyDiary.Web.Features.Menus.Models;

namespace MyDiary.Web.Features.Menus.Services
{
    public class MenuService : IMenuService
    {
        private readonly OracleDbProvider _dbProvider;

        public MenuService(OracleDbProvider dbProvider)
        {
            _dbProvider = dbProvider;
        }

        public async Task<List<MenuModel>> GetMenusAsync()
        {
            try
            {
                return await _dbProvider.ExecuteProcedureAsync("GET_USER_MENUS", reader => new MenuModel
                {
                    MenuCode = reader["MENU_CODE"]?.ToString(),
                    MenuName = reader["MENU_NAME"]?.ToString(),
                    MenuUrl = reader["MENU_URL"]?.ToString(),
                    MenuType = reader["MENU_TYPE"]?.ToString(),
                    ParentMenuCode = reader["PARENT_MENU_CODE"]?.ToString()
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "MenuService: GetMenusAsync failed");
                return new List<MenuModel>();
            }
        }

        public async Task<List<Department_New>> GetDepartmentsAsync()
        {
            try
            {
                return await _dbProvider.ExecuteProcedureAsync("GET_DEPARTMENTS", r => new Department_New
                {
                    S_NO = r.GetInt32(0),
                    CATEGORY = r.GetString(1),
                    SUB_CATEGORY = r.GetString(2),
                    VERTICAL = r.GetString(3)
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "MenuService: GetDepartmentsAsync failed");
                return new List<Department_New>();
            }
        }
    }
}