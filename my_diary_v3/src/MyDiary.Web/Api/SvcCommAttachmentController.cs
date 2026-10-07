using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Api;

/// <summary>
/// Streams a Send Communication attachment via a same-origin, signed-token URL — same pattern
/// as UhvMediaController — needed here specifically so the Karix WhatsApp gateway can fetch a
/// MEDIA_TEMPLATE's media.url itself (that API takes a URL it dereferences server-side, never raw
/// bytes). NOTE: this only works once the app is reachable at a real public URL
/// (WhatsApp:AppPublicBaseUrl) — a localhost dev box is not reachable by Karix's servers, so
/// MEDIA_TEMPLATE sends will fail in local dev even with full gateway credentials configured;
/// the plain TEMPLATE (text-only) flow has no such constraint.
/// </summary>
[ApiController]
[Route("api/svc-comm")]
public sealed class SvcCommAttachmentController : ControllerBase
{
    public const string Purpose = "MyDiary.SvcCommAttachment.Link.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly IConfiguration _config;
    private readonly IDataProtectionProvider _dataProtection;

    public SvcCommAttachmentController(IConfiguration config, IDataProtectionProvider dataProtection)
    { _config = config; _dataProtection = dataProtection; }

    public static string BuildPublicUrl(IConfiguration config, IDataProtectionProvider dp, long attachmentId)
    {
        var protector = dp.CreateProtector(Purpose).ToTimeLimitedDataProtector();
        var token = protector.Protect(attachmentId.ToString(), DateTimeOffset.UtcNow.Add(Lifetime));
        var baseUrl = (config["WhatsApp:AppPublicBaseUrl"] ?? "").TrimEnd('/');
        return $"{baseUrl}/api/svc-comm/attachment/{attachmentId}?token={Uri.EscapeDataString(token)}";
    }

    private bool TryValidate(long attachmentId, string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var payload = _dataProtection.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(token);
            return long.TryParse(payload, out var pid) && pid == attachmentId;
        }
        catch
        {
            return false; // tampered, malformed, or expired
        }
    }

    [HttpGet("attachment/{attachmentId:long}")]
    public async Task<IActionResult> Attachment(long attachmentId, [FromQuery] string? token)
    {
        if (!TryValidate(attachmentId, token)) return Unauthorized();

        var connString = _config.GetConnectionString("MyDiaryDBConnection");
        await using var cn = new OracleConnection(connString);
        await using var cmd = new OracleCommand("SELECT FILE_NAME, MIME, CONTENT FROM RP_SVC_COMM_ATTACHMENT WHERE ID = :p_id", cn);
        cmd.Parameters.Add("p_id", attachmentId);
        await cn.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess);
        if (!await r.ReadAsync()) return NotFound();

        var fileName = r.GetString(0);
        var mime = r.IsDBNull(1) ? "application/octet-stream" : r.GetString(1);
        await using var blobStream = r.GetStream(2);
        using var ms = new MemoryStream();
        await blobStream.CopyToAsync(ms);
        return File(ms.ToArray(), mime, fileName);
    }
}
