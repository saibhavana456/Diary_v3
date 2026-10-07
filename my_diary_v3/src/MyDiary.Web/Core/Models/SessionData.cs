namespace MyDiary.Web.Core.Models
{
    public class SessionData
    {
        public List<SessionClaim> Claims { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public class SessionClaim
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
