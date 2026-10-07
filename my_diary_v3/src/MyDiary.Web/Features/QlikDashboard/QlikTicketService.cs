using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MyDiary.Web.Features.QlikDashboard;

public sealed record QlikTicket(string Ticket, string UserId, string RedeemUrl);

public interface IQlikTicketService
{
    /// <summary>Requests a QPS login ticket for the given user (mutual-TLS,
    /// server-to-server). The browser then redeems it silently to establish the
    /// Qlik session — no redirect, no login prompt.</summary>
    Task<QlikTicket> GetTicketAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Calls the Qlik Sense Proxy Service (QPS) ticket API:
///   POST https://{host}:4243/qps/{prefix}/ticket?Xrfkey={xrf}
///   headers: X-Qlik-Xrfkey, client certificate (PFX from QMC)
///   body:    { "UserDirectory", "UserId", "Attributes":[] }
///   returns: { "Ticket": "...", ... }
/// The ticket is redeemed by the browser at
///   {host}/{prefix}/?qlikTicket={ticket}   (session cookie planted, no redirect).
/// </summary>
public sealed class QlikTicketService : IQlikTicketService
{
    private readonly QlikOptions _o;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public QlikTicketService(IOptions<QlikOptions> o) => _o = o.Value;

    public async Task<QlikTicket> GetTicketAsync(string userId, CancellationToken ct = default)
    {
        if (_o.IsMock)
            return new QlikTicket("mock-ticket", userId, "");
        if (string.IsNullOrWhiteSpace(_o.Host))
            throw new InvalidOperationException("Qlik:Host is not configured.");
        if (string.IsNullOrWhiteSpace(_o.ClientCertPath))
            throw new InvalidOperationException("Qlik:ClientCertPath is not configured for QPS ticket auth.");

        var xrf = NewXrfKey();
        var prefix = string.IsNullOrWhiteSpace(_o.VirtualProxyPrefix) ? "" : _o.VirtualProxyPrefix.Trim('/') + "/";
        var qpsBase = string.IsNullOrWhiteSpace(_o.QpsUri) ? DefaultQps(_o.Host) : _o.QpsUri.TrimEnd('/');
        var url = $"{qpsBase}/qps/{prefix}ticket?Xrfkey={xrf}";

        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
        };
        handler.ClientCertificates.Add(
            new X509Certificate2(_o.ClientCertPath, _o.ClientCertPassword));
        if (_o.AllowInvalidServerCert)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        using var http = new HttpClient(handler);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("X-Qlik-Xrfkey", xrf);
        var body = JsonSerializer.Serialize(
            new { UserDirectory = _o.UserDirectory, UserId = userId, Attributes = Array.Empty<object>() });
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(req, ct);
        var payload = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"QPS ticket request failed ({(int)resp.StatusCode}): {payload}");

        using var doc = JsonDocument.Parse(payload);
        var ticket = doc.RootElement.GetProperty("Ticket").GetString()
                     ?? throw new InvalidOperationException("QPS response had no Ticket.");

        var redeem = $"{_o.Host.TrimEnd('/')}/{prefix}?qlikTicket={ticket}";
        return new QlikTicket(ticket, userId, redeem);
    }

    private static string DefaultQps(string host)
    {
        // QPS API listens on 4243 by default; derive from the proxy host.
        var u = new Uri(host);
        return $"{u.Scheme}://{u.Host}:4243";
    }

    private static string NewXrfKey()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new StringBuilder(16);
        var rnd = Random.Shared;
        for (var i = 0; i < 16; i++) sb.Append(chars[rnd.Next(chars.Length)]);
        return sb.ToString();
    }
}
