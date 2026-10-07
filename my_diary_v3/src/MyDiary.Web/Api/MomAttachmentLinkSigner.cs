using Microsoft.AspNetCore.DataProtection;

namespace MyDiary.Web.Api;

/// <summary>
/// Builds short-lived signed tokens for /api/mom-attachments/... links.
/// Mirrors AttachmentLinkSigner's shape exactly, but with its own purpose
/// string (MomAttachmentsController.Purpose) and a "kind" discriminator
/// (ENTRY = primary MoM file, ATTACH = supporting document) since MoM
/// entries store their primary file inline while supporting docs live in a
/// separate RP_MOM_ATTACHMENT table.
/// </summary>
public static class MomAttachmentLinkSigner
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Builds "/api/mom-attachments/entry/{entryId}?token=...&amp;download=..." for an entry's primary file.</summary>
    public static string BuildEntryUrl(IDataProtectionProvider dataProtection, long entryId, string empCode, bool download = false)
    {
        var token = Protect(dataProtection, "ENTRY", entryId, empCode);
        var url = $"api/mom-attachments/entry/{entryId}?token={Uri.EscapeDataString(token)}";
        if (download) url += "&download=true";
        return url;
    }

    /// <summary>Builds "/api/mom-attachments/attachment/{attachmentId}?entryId=...&amp;token=...&amp;download=..." for a supporting document.</summary>
    public static string BuildAttachmentUrl(IDataProtectionProvider dataProtection, long attachmentId, long entryId, string empCode, bool download = false)
    {
        var token = Protect(dataProtection, "ATTACH", attachmentId, empCode);
        var url = $"api/mom-attachments/attachment/{attachmentId}?entryId={entryId}&token={Uri.EscapeDataString(token)}";
        if (download) url += "&download=true";
        return url;
    }

    private static string Protect(IDataProtectionProvider dataProtection, string kind, long id, string empCode)
    {
        var payload = $"{kind}|{id}|{empCode}";
        var protector = dataProtection.CreateProtector(MomAttachmentsController.Purpose).ToTimeLimitedDataProtector();
        return protector.Protect(payload, DateTimeOffset.UtcNow.Add(Lifetime));
    }
}
