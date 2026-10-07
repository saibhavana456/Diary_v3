namespace MyDiary.Web.Features.Shared.Models
{
    // API 1: Validate Domain User Request
    public class ADLoginRequest
    {
        // No hardcoded values here anymore
        public string app_code { get; set; }
        public string login_type { get; set; }
        public string mfa_flg { get; set; }
        public string email_otp_flg { get; set; }
        public string password { get; set; }
        public string user_id { get; set; }
        public string validateUserAPIKey { get; set; }
    }

    public class ADLoginResponse
    {
        public string status { get; set; }
        public string message { get; set; }
        public string rData { get; set; }
        public string rData1 { get; set; }
    }

    public class StaffDetailsResult
    {
        public StaffDetailsObj staffdetails { get; set; }
        public string userType { get; set; }
    }

    public class StaffDetailsObj
    {
        public string EMPLID { get; set; }
        public string NAME { get; set; }
        public string LOCATION { get; set; }
        public string DEPARTMENT_Descr { get; set; }
        public string DEPARTMENT_Descr_EXTRA { get; set; }
        public string Staff_Region_Code { get; set; }
        public string Staff_Region_Name { get; set; }
        public string Staff_Division_Code { get; set; }
        public string Staff_Division_Name { get; set; }
        public string EMP_DESGN { get; set; }
        public string EMP_DESGN_DESC { get; set; }
        public string EMP_SCALE_CODE { get; set; }
        public string EMP_SCALE_DESCR { get; set; }
        public DateTime? EMP_DATE_OF_BIRTH { get; set; }
        public DateTime? EMP_JOINING_DATE { get; set; }
        public DateTime? EXPECTED_END_DATE { get; set; }
        public string ACC_NUM { get; set; }
        public string BRANCH_EC_CD { get; set; }
        public string SEX { get; set; }
        public DateTime? POSTING_DATE { get; set; }
        public string PHONE { get; set; }
        public string EMAIL { get; set; }
        public string BRSOLID { get; set; }
        public DateTime? DataAsOn { get; set; }
        public DateTime? LastUpdateDate { get; set; }
        public string UserName { get; set; }
        public string Privilege { get; set; }
        public string Access { get; set; }
        public string MUD_Region_Solid { get; set; }
        public string MUD_Region_Code { get; set; }
        public string MUD_Region_Name { get; set; }
        public string MUD_Zone_Solid { get; set; }
        public string MUD_Zone_Code { get; set; }
        public string MUD_Zone_Name { get; set; }
        public string MUD_Branch_Solid { get; set; }
        public string MUD_Branch_Code { get; set; }
        public string MUD_Branch_Name { get; set; }
        public string MUD_Mobile { get; set; }
        public string MUD_Status { get; set; }
        public string MUD_Last_Entry_Date { get; set; }
        public DateTime? MUD_Last_Week_Start_Date { get; set; }
        public DateTime? MUD_Last_Week_End_Date { get; set; }
    }

    public class OtpVerifyRequest
    {
        public string mobileNo { get; set; }
        public string uid { get; set; }
        public string otp { get; set; }
    }

    public class OtpVerifyResponse
    {
        public string status { get; set; }
        public string message { get; set; }
    }

    public class DevSettings
    {
        public bool BypassAuth { get; set; }
        public TestUser TestUser { get; set; } = new();
    }

    public class TestUser
    {
        public string Username { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public string Privilege { get; set; }
        public string DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string DepartmentExtraDescription { get; set; }
        public string EmployeeDesignationDescription { get; set; }
        public string BranchCode { get; set; }
        public string BranchName { get; set; }
        public string BranchSolId { get; set; }
        public string RegionCode { get; set; }
        public string RegionName { get; set; }
        public string RegionSolId { get; set; }
        public string ZoneCode { get; set; }
        public string ZoneName { get; set; }
        public string ZoneSolId { get; set; }
    }

    public class UserSession
    {
        public string EmpId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "BRANCH";
        public string BranchSolid { get; set; } = "";
        public string ZoneSolid { get; set; } = "";
        public string RegionSolid { get; set; } = "";
    }
}