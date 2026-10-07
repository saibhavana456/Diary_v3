namespace MyDiary.Web.Features.Shared.Models
{
    public class UserAccessValidationResult
    {
        public bool IsAuthorized { get; set; }
        public string? ZoneSolid { get; set; }
        public string? ZoneName { get; set; }
    }
}
