using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MyDiary.Web.Features.UnionHubVideoLaunch.Services;

namespace MyDiary.Web.Api;

/// <summary>
/// Streams Union Hub Video Launch media (the protected video, and the openly-downloadable
/// image/infographic/PPT collateral) through a same-origin, signed-token endpoint —
/// same approach as AttachmentsController/AttachmentLinkSigner, needed for the same reason:
/// this app's login flow never issues a real ASP.NET Core auth cookie (see
/// AttachmentsController's remarks), so plain [Authorize] cannot gate a &lt;video&gt;/&lt;img&gt;
/// request. The token is minted server-side by the Blazor page (which knows the
/// authenticated user via the circuit's ICurrentUser) and is short-lived and single-purpose.
///
/// IMPORTANT — this is a UX-level deterrent against casual sharing, NOT DRM: it stops a bare
/// link from working for anyone who doesn't have a freshly-minted token, and the UI hides any
/// download/share affordance and disables the native browser download menu on the video.
/// A determined user can still capture screen output; no browser-video approach can prevent
/// that, and this endpoint makes no claim to.
/// </summary>
[ApiController]
[Route("api/uhv")]
public sealed class UhvMediaController : ControllerBase
{
    public const string Purpose = "MyDiary.UhvMedia.Link.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(20);

    private readonly IUnionHubVideoLaunchService _svc;
    private readonly IDataProtectionProvider _dataProtection;

    public UhvMediaController(IUnionHubVideoLaunchService svc, IDataProtectionProvider dataProtection)
    { _svc = svc; _dataProtection = dataProtection; }

    public static string BuildVideoUrl(IDataProtectionProvider dp, long launchId, string empCode)
        => BuildUrl(dp, "video", launchId, empCode);

    public static string BuildAttachmentUrl(IDataProtectionProvider dp, long attachmentId, string empCode, bool download)
    {
        var url = BuildUrl(dp, "attachment", attachmentId, empCode);
        return download ? url + "&download=true" : url;
    }

    private static string BuildUrl(IDataProtectionProvider dp, string kind, long id, string empCode)
    {
        var payload = $"{kind}|{id}|{empCode}";
        var protector = dp.CreateProtector(Purpose).ToTimeLimitedDataProtector();
        var token = protector.Protect(payload, DateTimeOffset.UtcNow.Add(Lifetime));
        return $"api/uhv/{kind}/{id}?token={Uri.EscapeDataString(token)}";
    }

    private bool TryValidate(string kind, long id, string? token, out string empCode)
    {
        empCode = "";
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var payload = _dataProtection.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(token);
            var parts = payload.Split('|', 3);
            if (parts.Length != 3 || parts[0] != kind || !long.TryParse(parts[1], out var pid) || pid != id) return false;
            empCode = parts[2];
            return true;
        }
        catch
        {
            return false; // tampered, malformed, or expired
        }
    }

    /// <summary>Streams the protected launch video inline only — never as an attachment,
    /// and range-enabled so the &lt;video&gt; tag can seek without the browser needing to
    /// buffer/save the whole file (which would otherwise tempt a "save as" workaround).</summary>
    [HttpGet("video/{launchId:long}")]
    public async Task<IActionResult> Video(long launchId, [FromQuery] string? token, CancellationToken ct)
    {
        if (!TryValidate("video", launchId, token, out _)) return Unauthorized();
        var result = await _svc.ReadVideoAsync(launchId, ct);
        if (result is null) return NotFound();
        var (stream, mime, fileName) = result.Value;

        Response.Headers[HeaderNames.ContentDisposition] =
            new ContentDispositionHeaderValue("inline") { FileNameStar = fileName }.ToString();
        //return File(stream, mime, enableRangeProcessing: true);
        return File(stream, "video/mp4", enableRangeProcessing: true);
    }

    /// <summary>Supporting collateral (image/infographic/PPT) — fine to download/open normally.</summary>
    [HttpGet("attachment/{attachmentId:long}")]
    public async Task<IActionResult> Attachment(long attachmentId, [FromQuery] string? token, [FromQuery] bool download, CancellationToken ct)
    {
        if (!TryValidate("attachment", attachmentId, token, out _)) return Unauthorized();
        var result = await _svc.ReadAttachmentAsync(attachmentId, ct);
        if (result is null) return NotFound();
        var (stream, mime, fileName) = result.Value;

        Response.Headers[HeaderNames.ContentDisposition] =
            new ContentDispositionHeaderValue(download ? "attachment" : "inline") { FileNameStar = fileName }.ToString();
        return File(stream, mime, enableRangeProcessing: true);
    }
}
