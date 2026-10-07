namespace MyDiary.Web.Features.StrikeCorner.Models
{
    public class StrikeCommitteeAttachment
    {
        public long Id { get; set; }

        public long MeetingId { get; set; }

        public string FileName { get; set; } = "";

        public string ContentType { get; set; } = "";
    }
}
