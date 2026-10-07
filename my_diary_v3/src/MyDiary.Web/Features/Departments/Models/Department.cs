namespace MyDiary.Web.Features.Departments.Models
{
    public class Department
    {
        public int DepartmentID { get; set; }
        public string DepartmentName { get; set; } = "";
        public string DepartmentName_HI { get; set; } = "";   // ✅ NEW

        public string DepartmentCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconClass { get; set; } = "";
        public string Status { get; set; } = "";

        // ✅ Language-aware display helper
        public string DisplayName(string lang) =>
            lang == "hi" && !string.IsNullOrWhiteSpace(DepartmentName_HI)
                ? DepartmentName_HI
                : DepartmentName;
    }
}