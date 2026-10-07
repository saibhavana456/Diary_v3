using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Dashboards.Models;
using System.Data;

public class NavigationService : INavigationService
{
    private readonly OracleDbProvider _db;

    public NavigationService(OracleDbProvider db)
    {
        _db = db;
    }

    public async Task<bool> IsRouteActiveAsync(string route)
    {
        try
        {
            string safeRoute = (route ?? "").Replace("'", "''").ToLowerInvariant();
            string sql = $@"
                SELECT COUNT(*) CNT
                FROM MYDIARYDB.APP_NAVIGATION_MASTER
                WHERE LOWER(MENU_ROUTE) = '{safeRoute}'
                  AND IS_ACTIVE = 'Y'";

            var dt = await _db.ExecuteQueryAsync(sql);
            return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["CNT"]) > 0;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"[NavigationService] IsRouteActiveAsync failed for route: {route}");
            return false;
        }
    }

    public async Task<bool> RouteExistsAsync(string route)
    {
        try
        {
            string safeRoute = (route ?? "").Replace("'", "''").ToLowerInvariant();
            string sql = $@"
                SELECT COUNT(*) CNT
                FROM MYDIARYDB.APP_NAVIGATION_MASTER
                WHERE LOWER(MENU_ROUTE) = '{safeRoute}'";

            var dt = await _db.ExecuteQueryAsync(sql);
            return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["CNT"]) > 0;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"[NavigationService] RouteExistsAsync failed for route: {route}");
            return false;
        }
    }

    public async Task<List<NavigationMenu>> GetMenusAsync()
    {
        try
        {
            string sql = @"
                SELECT *
                FROM APP_NAVIGATION_MASTER
                WHERE IS_ACTIVE = 'Y'
                   OR UPPER(MENU_ROUTE) = 'LANDING_DASHBOARD_V2_1'
                ORDER BY DISPLAY_ORDER";

            DataTable dt = await _db.ExecuteQueryAsync(sql);

            AppLogger.LogInfo($"[NavigationService] GetMenusAsync returned {dt.Rows.Count} menu items.");

            return dt.Rows.Cast<DataRow>()
                .Select(r => new NavigationMenu
                {
                    MenuId = Convert.ToInt32(r["MENU_ID"]),
                    MenuName = r["MENU_NAME"]?.ToString() ?? "",
                    MenuRoute = r["MENU_ROUTE"]?.ToString() ?? "",
                    MenuIcon = r["MENU_ICON"]?.ToString() ?? "",
                    DisplayOrder = Convert.ToInt32(r["DISPLAY_ORDER"]),
                    MenuType = r["MENU_TYPE"]?.ToString() ?? "",
                    OpenInNewTab = r["OPEN_IN_NEW_TAB"]?.ToString() == "Y",
                    IsActive = r["IS_ACTIVE"]?.ToString() ?? "Y",
                    RedirectUrl = r["REDIRECT_URL"]?.ToString()
                })
                .ToList();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "[NavigationService] GetMenusAsync failed.");
            return new List<NavigationMenu>();
        }
    }
}
