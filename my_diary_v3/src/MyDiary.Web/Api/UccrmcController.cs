using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Dtos;

namespace MyDiary.Web.Api;

/// <summary>
/// Server-to-server integration endpoints for the UCCRMC command centre.
/// All endpoints require the API-key scheme (X-Api-Key header).
///   POST /api/uccrmc/alerts                     — raise an alert ticket
///   GET  /api/uccrmc/alerts/{alertId}/status    — status of a raised alert
///   GET  /api/uccrmc/alerts/dashboard           — closure / pendency figures
/// </summary>
[ApiController]
[Route("api/uccrmc")]
[Authorize(Policy = "ApiKey")]
public sealed class UccrmcController : ControllerBase
{
    private readonly IUccrmcService _svc;
    private readonly ILogger<UccrmcController> _logger;

    public UccrmcController(IUccrmcService svc, ILogger<UccrmcController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    /// <summary>Raise (or return the existing) alert ticket for a UCCRMC alert.</summary>
    [HttpPost("alerts")]
    public async Task<ActionResult<CreateUccrmcAlertResponse>> Create(
        [FromBody] CreateUccrmcAlertRequest req, CancellationToken ct)
    {
        if (req is null) return BadRequest(new { error = "Body required." });
        try
        {
            var res = await _svc.CreateAlertAsync(req, ct);
            return CreatedAtAction(nameof(Status), new { alertId = res.AlertId }, res);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "UCCRMC Create alert rejected — invalid argument: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "UCCRMC Create alert rejected — invalid operation: {Message}", ex.Message);
            return UnprocessableEntity(new { error = ex.Message });
        }
    }

    /// <summary>Status of the ticket raised for a given UCCRMC Alert ID.</summary>
    [HttpGet("alerts/{alertId}/status")]
    public async Task<ActionResult<UccrmcAlertStatus>> Status(string alertId, CancellationToken ct)
    {
        var s = await _svc.GetStatusAsync(alertId, ct);
        if (s is null)
        {
            _logger.LogWarning("UCCRMC alert status not found for AlertId={AlertId}", alertId);
        }
        return s is null ? NotFound(new { error = $"No UCCRMC alert found for '{alertId}'." }) : Ok(s);
    }

    /// <summary>Closure / pendency aggregates for UCCRMC alert tickets.</summary>
    [HttpGet("alerts/dashboard")]
    public async Task<ActionResult<UccrmcDashboard>> Dashboard(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var fromUtc = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : (DateTime?)null;
        var toUtc   = to.HasValue   ? DateTime.SpecifyKind(to.Value,   DateTimeKind.Utc) : (DateTime?)null;
        return Ok(await _svc.GetDashboardAsync(fromUtc, toUtc, ct));
    }
}
