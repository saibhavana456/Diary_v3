namespace MyDiary.Web.Features.Dashboards.Models
{
    public record RecordData(
        string Name,
        DateOnly DateOfData,
        string Location,
        string Owner,
        string icons,
        string link,
        string menuCategory,
       string menuSubCategory
    )
    {
        public string? OwnerVertical { get; init; }
        public string? Source { get; init; }
        public string? FrequencyOfReportUpdation { get; init; }

        /// <summary>
        /// The Blazor route for internal navigation, or an "EXTERNAL:" prefixed URL for new-tab opens.
        /// NULL means no route is configured — app should redirect to Not Found.
        /// </summary>
        public string? MenuRoute { get; init; }

        // ⬇️ Add this so service can inject diagnostic text
        public string? DebugInfo { get; init; }
    }
    public class NavigationMenu
    {
        public int MenuId { get; set; }

        public string MenuName { get; set; } = "";

        public string MenuRoute { get; set; } = "";

        public string MenuIcon { get; set; } = "";

        public int DisplayOrder { get; set; }

        public string MenuType { get; set; } = "";

        public bool OpenInNewTab { get; set; }

        public string? RoleName { get; set; }
        public string IsActive { get; set; } = "Y";

        /// <summary>
        /// Full external redirect URL for SSO, QLIK, and EXTERNAL menu types.
        /// NULL for INTERNAL routes (which use MenuRoute for Blazor navigation).
        /// </summary>
        public string? RedirectUrl { get; set; }
    }

}