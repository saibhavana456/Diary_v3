namespace MyDiary.Web.Features.Menus.Models
{
    public class MenuModel
    {
        public string MenuCode { get; set; }
        public string MenuName { get; set; }
        public string MenuUrl { get; set; }
        public string MenuType { get; set; } // e.g., "PARENT", "SUBPARENT", "CHILD"
        public string ParentMenuCode { get; set; }
    }
}
