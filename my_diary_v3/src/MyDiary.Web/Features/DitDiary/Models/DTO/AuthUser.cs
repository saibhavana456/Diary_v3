namespace MyDiary.Web.Features.DitDiary.Models.DTO
{
    public class AuthUser
    {
        public string PfNumber { get; set; } = "";
        public string Name { get; set; } = "";
        public string Designation { get; set; } = "";
        public string ScaleText { get; set; } = "";
        public int Scale { get; set; }
        public string Role { get; set; } = "User";
        public string BranchName { get; set; } = "";
        public string RegionName { get; set; } = "";
        public string ZoneName { get; set; } = "";

        public bool IsAdmin { get; set; }    // CM, AGM, DGM, GM, CGM
        public bool CanEdit => true;      // scale 1-3 = view only
        public List<long> DeptIds { get; set; } = new();
        public List<long> SectionIds { get; set; } = new();
        public bool HasScope => SectionIds.Count > 0;
    }
}