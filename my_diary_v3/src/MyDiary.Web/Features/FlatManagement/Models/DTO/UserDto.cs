using System.ComponentModel.DataAnnotations;

namespace MyDiary.Web.Features.FlatManagement.Model.DBO
{
    public class UserRequest
    {
        public string? user_id { get; set; }
        public string? password { get; set; }
    }
    public class UserDetails
    {
        public string? user_id { get; set; }
        public string? user_name { get; set; }
        public string? validation_status { get; set; }
    }

    public class UserLogins
    {
        [Required]
        public string? PF_NO { get; set; }
        [Required]
        public string? CREDENTIALS { get; set; }
        public UserLogins() { }
    }

    public class UserDeclaration
    {
        public string? PF_NO { get; set; }
        public string? OTP { get; set; }
    }
}
