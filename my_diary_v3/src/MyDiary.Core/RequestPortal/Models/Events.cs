using MyDiary.Core.Services;

namespace RequestPortal.Core.Models;

// ── Corporate Events Hub — persistent domain models ──────────────────────────

public sealed class EventTag
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}

public sealed class UpcomingEventItem
{
    public long Id { get; set; }
    public long VerticalId { get; set; }
    public string VerticalName { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime? EventDate { get; set; }
    public string? TimeText { get; set; }
    public string? Venue { get; set; }
    public string PostedByEmp { get; set; } = "";
    public DateTime PostedAt { get; set; }

    /// <summary>Composed "Aug 15 · 6:00 PM · Grand Ballroom" line.</summary>
    public string WhenText
    {
        get
        {
            var parts = new List<string>();
            if (EventDate.HasValue) parts.Add(EventDate.Value.ToString("MMM d"));
            if (!string.IsNullOrWhiteSpace(TimeText)) parts.Add(TimeText!);
            if (!string.IsNullOrWhiteSpace(Venue)) parts.Add(Venue!);
            return string.Join(" · ", parts);
        }
    }
}

public sealed class FeedPostImage
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSize { get; set; }
    public int SeqNo { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>Browser URL that streams this media item via the media controller.
    /// Works for both photos and videos — the controller serves the stored CONTENT_TYPE
    /// with HTTP range processing enabled (needed for video seeking).</summary>
    public string Url => $"api/events/media/{Id}";

    /// <summary>True when this attachment is a video (CONTENT_TYPE starts with "video/").
    /// Drives whether the feed/lightbox renders a &lt;video&gt; player or an &lt;img&gt;.</summary>
    public bool IsVideo => ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ?? false;
}

public sealed class FeedPost
{
    public long Id { get; set; }
    public long VerticalId { get; set; }
    public string VerticalCode { get; set; } = "";
    public string VerticalName { get; set; } = "";
    public long? EventId { get; set; }
    public string? EventName { get; set; }
    public long? TagId { get; set; }
    public string? TagName { get; set; }
    public string Caption { get; set; } = "";
    public string PostedByEmp { get; set; } = "";
    public DateTime PostedAt { get; set; }
    public bool IsArchived { get; set; }
    public List<FeedPostImage> Images { get; set; } = new();

    // Client-side only interaction state (not persisted in this prototype).
    public int Likes { get; set; }
    public bool Liked { get; set; }
    public bool Saved { get; set; }
    public bool ShowComments { get; set; }
    public string? Draft { get; set; }
    public List<PostComment> Comments { get; set; } = new();

    public string PostedAgo
    {
        get
        {
            var span = AppTime.Now - PostedAt;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return PostedAt.ToString("dd MMM yyyy");
        }
    }

    /// <summary>2-3 letter avatar token from the vertical name.</summary>
    public string VerticalInitials
    {
        get
        {
            var src = string.IsNullOrWhiteSpace(VerticalName) ? VerticalCode : VerticalName;
            if (string.IsNullOrWhiteSpace(src)) return "?";
            var words = src.Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1) return words[0][..Math.Min(3, words[0].Length)].ToUpperInvariant();
            return (words[0][0].ToString() + words[1][0]).ToUpperInvariant();
        }
    }
}

public sealed record PostComment(string Author, string Text);

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record CreateEventDto(long VerticalId, string Name, DateTime? EventDate, string? TimeText, string? Venue);

public sealed record NewPostImage(string FileName, string ContentType, byte[] Data);

public sealed record CreatePostDto(long VerticalId, long? EventId, long? TagId, string Caption, List<NewPostImage> Images);
