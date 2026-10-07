namespace MyDiary.Web.Features.StrikeCorner.Models
{
    public class StrikeCommitteeMeeting
    {
        public long Id { get; set; }

        public DateTime MeetingDate { get; set; }

        public string? Remarks { get; set; }

        public string? FileName { get; set; }

        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        public string? EntryBy { get; set; }

        public DateTime? EntryAt { get; set; }

        public int AttachmentCount { get; set; }
    }
}
