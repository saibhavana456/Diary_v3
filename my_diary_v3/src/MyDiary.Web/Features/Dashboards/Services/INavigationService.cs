using MyDiary.Web.Features.Dashboards.Models;

public interface INavigationService
{
    Task<List<NavigationMenu>> GetMenusAsync();

    Task<bool> IsRouteActiveAsync(string route);

    Task<bool> RouteExistsAsync(string route);
}