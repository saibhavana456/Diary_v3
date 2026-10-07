using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RequestPortal.Core.Abstractions;

namespace MyDiary.Web.Api;

/// <summary>
/// Serves MoM (primary + supporting) attachment content for the MoM entry
/// runtime pages (Features/ReportModule/Components/Mom/MomEntries.razor).
///
/// Same redirect-to-login problem and same fix as AttachmentsController (see
/// its header comment): this app never calls HttpContext.SignInAsync, so a
/// plain [Authorize] here would bounce a genuinely-logged-in user opening a
/// download link in a new tab. Deliberately NOT [Authorize] — uses a
/// short-lived Data-Protection-signed token instead, generated server-side
/// where the Blazor circuit already knows ICurrentUser.
///
/// Two kinds of ids are supported via the "kind" segment so a single
/// controller/signer purpose string covers both the primary file (stored on
/// RP_MOM_ENTRY) and supporting files (RP_MOM_ATTACHMENT rows):
///   GET /api/mom-attachments/entry/{entryId}?token=...        — primary file, inline
///   GET /api/mom-attachments/attachment/{attachmentId}?token=... — supporting file, inline
/// Append &amp;download=true to force Content-Disposition: attachment.
/// </summary>
[ApiController]
[Route("api/mom-attachments")]
public sealed class MomAttachmentsController : ControllerBase
{
    /// <summary>Data Protection purpose string — must match MomAttachmentLinkSigner exactly.</summary>
    public const string Purpose = "MyDiary.MomAttachments.DownloadLink.v1";

    private readonly IMomRepo _momRepo;
    private readonly IMomAttachmentStore _store;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly ILogger<MomAttachmentsController> _logger;

    public MomAttachmentsController(IMomRepo momRepo, IMomAttachmentStore store, IDataProtectionProvider dataProtection, ILogger<MomAttachmentsController> logger)
    {
        _momRepo = momRepo;
        _store = store;
        _dataProtection = dataProtection;
        _logger = logger;
    }

    [HttpGet("entry/{entryId:long}")]
    public async Task<IActionResult> GetPrimary(long entryId, [FromQuery] string? token, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        if (!TryUnprotect(token, "ENTRY", entryId, out var error))
            return Unauthorized(new { error });

        var entry = await _momRepo.GetEntryAsync(entryId, ct);
        if (entry is null) return NotFound();

        return await StreamAsync(entry.PrimaryFilePath, entry.PrimaryFileName, download, ct);
    }

    [HttpGet("attachment/{attachmentId:long}")]
    public async Task<IActionResult> GetSupporting(long attachmentId, [FromQuery] long entryId, [FromQuery] string? token, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        if (!TryUnprotect(token, "ATTACH", attachmentId, out var error))
            return Unauthorized(new { error });

        var attachments = await _momRepo.ListAttachmentsAsync(entryId, ct);
        var attachment = attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null) return NotFound();

        return await StreamAsync(attachment.FilePath, attachment.FileName, download, ct);
    }

    private async Task<IActionResult> StreamAsync(string relativePath, string fileName, bool download, CancellationToken ct)
    {
        Stream stream;
        try
        {
            stream = await _store.OpenAsync(relativePath, ct);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "MoM attachment file not found at path: {RelativePath}", relativePath);
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to MoM attachment file at path: {RelativePath}", relativePath);
            return Forbid();
        }

        Response.Headers[HeaderNames.ContentDisposition] =
            new ContentDispositionHeaderValue(download ? "attachment" : "inline")
            {
                FileNameStar = fileName
            }.ToString();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mime = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        return File(stream, mime);
    }

    private bool TryUnprotect(string? token, string expectedKind, long expectedId, out string error)
    {
        error = "";
        if (string.IsNullOrEmpty(token)) { error = "Missing download token."; return false; }

        string payload;
        try
        {
            payload = _dataProtection.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MoM attachment token validation failed for Kind={Kind}, Id={Id}. Token may be tampered, malformed, or expired.", expectedKind, expectedId);
            error = "Invalid or expired download link.";
            return false;
        }

        // Payload shape: "{kind}|{id}|{empCode}" — see MomAttachmentLinkSigner.
        var parts = payload.Split('|', 3);
        if (parts.Length != 3 || parts[0] != expectedKind || !long.TryParse(parts[1], out var tokenId) || tokenId != expectedId)
        {
            _logger.LogWarning("MoM attachment token mismatch: expected Kind={ExpectedKind} Id={ExpectedId}, token payload did not match.", expectedKind, expectedId);
            error = "Token does not match the requested attachment.";
            return false;
        }

        return true;
    }
}
