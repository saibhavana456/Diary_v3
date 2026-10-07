using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.Api;

/// <summary>
/// Serves Request Portal attachment content for the "View"/"Download" actions on
/// RequestPortal/Pages/Requests/Detail.razor.
///
/// Root cause of the redirect-to-login bug: this app's login flow
/// (Login.razor.CompleteLoginAsync) never calls HttpContext.SignInAsync — it only
/// sets Blazor-circuit-scoped state (encrypted browser sessionStorage +
/// CustomAuthenticationStateProvider). No real ASP.NET Core auth cookie is ever
/// issued, because SignInAsync can't write a Set-Cookie header from inside an
/// already-connected interactive Blazor Server circuit (the original HTTP
/// response has already completed by that point). So a plain [Authorize] here —
/// which checks that cookie — always saw an unauthenticated request from the new
/// browser tab opened by Target="_blank" and redirected to /login, even though
/// the user was genuinely logged into the app.
///
/// Fix: use ASP.NET Core Data Protection to issue a short-lived, signed,
/// single-attachment download token embedded in the link itself, generated
/// server-side by Detail.razor (which already knows the authenticated user via
/// the Blazor circuit's ICurrentUser) at render time. This is the same approach
/// EventsMediaController's own comments already call out as the intended
/// hardening path for this exact limitation — applied here properly instead of
/// relying on cookie auth that this app's login flow doesn't establish.
///
///   GET /api/attachments/{id}?token=...                  — inline (opens in a new tab)
///   GET /api/attachments/{id}?token=...&amp;download=true     — forces Content-Disposition: attachment
/// </summary>
[ApiController]
[Route("api/attachments")]
public sealed class AttachmentsController : ControllerBase
{
    /// <summary>Data Protection purpose string — must match AttachmentLinkSigner exactly.</summary>
    public const string Purpose = "MyDiary.Attachments.DownloadLink.v1";

    private readonly IAttachmentService _attachments;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(IAttachmentService attachments, IDataProtectionProvider dataProtection, ILogger<AttachmentsController> logger)
    {
        _attachments = attachments;
        _dataProtection = dataProtection;
        _logger = logger;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, [FromQuery] string? token, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "Missing download token." });

        string payload;
        try
        {
            payload = _dataProtection.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(token);
        }
        catch (Exception ex)
        {
            // Tampered, malformed, or expired (default protector lifetime check throws too).
            _logger.LogWarning(ex, "Attachment token validation failed for AttachmentId={AttachmentId}. Token may be tampered, malformed, or expired.", id);
            return Unauthorized(new { error = "Invalid or expired download link." });
        }

        // Payload shape: "{attachmentId}|{empCode}" — see AttachmentLinkSigner.
        var parts = payload.Split('|', 2);
        if (parts.Length != 2 || !long.TryParse(parts[0], out var tokenAttachmentId) || tokenAttachmentId != id)
        {
            _logger.LogWarning("Attachment token mismatch: token payload does not match requested AttachmentId={AttachmentId}.", id);
            return Unauthorized(new { error = "Token does not match the requested attachment." });
        }

        var actorEmpCode = parts[1];

        Stream stream;
        string mime;
        string fileName;
        try
        {
            (stream, mime, fileName) = await _attachments.ReadAsync(id, actorEmpCode, ct);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Attachment file not found for AttachmentId={AttachmentId}, Actor={ActorEmpCode}.", id, actorEmpCode);
            return NotFound();
        }

        Response.Headers[HeaderNames.ContentDisposition] =
            new ContentDispositionHeaderValue(download ? "attachment" : "inline")
            {
                FileNameStar = fileName
            }.ToString();

        return File(stream, string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime);
    }
}
