using Microsoft.AspNetCore.DataProtection;

namespace MyDiary.Web.Api;

/// <summary>
/// Builds short-lived signed tokens for /api/attachments/{id} links. Shared
/// between the controller (validates the token) and any Razor page that
/// renders an attachment link (generates it) — keeps the purpose string and
/// payload format in one place instead of duplicated across files.
/// </summary>
public static class AttachmentLinkSigner
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Builds "/api/attachments/{id}?token=...&amp;download=..." for the given attachment.</summary>
    public static string BuildUrl(IDataProtectionProvider dataProtection, long attachmentId, string empCode, bool download = false)
    {
        var payload = $"{attachmentId}|{empCode}";
        var protector = dataProtection.CreateProtector(AttachmentsController.Purpose).ToTimeLimitedDataProtector();
        // Protect(string, expiration) already returns a URL-safe base64 string —
        // no additional encoding needed before embedding it in the query string.
        var token = protector.Protect(payload, DateTimeOffset.UtcNow.Add(Lifetime));

        var url = $"api/attachments/{attachmentId}?token={Uri.EscapeDataString(token)}";
        if (download) url += "&download=true";
        return url;
    }
}
