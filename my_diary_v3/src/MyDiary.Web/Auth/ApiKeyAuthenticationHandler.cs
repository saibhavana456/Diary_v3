using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MyDiary.Web.Auth;

/// <summary>
/// Simple shared-secret authentication for server-to-server API callers
/// (e.g. the UCCRMC command centre). Callers present the secret in the
/// <c>X-Api-Key</c> header. Valid keys are configured under
/// <c>ApiKeys:{clientName} = {secret}</c> in configuration; the matched client
/// name is emitted as the <c>api_client</c> claim so policies/controllers can
/// tell who called. This is intentionally minimal — no session, no cookie.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly IConfiguration _config;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
        : base(options, logger, encoder)
    {
        _config = config;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No header → let other schemes/anonymous handle it (don't hard-fail).
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var presented = provided.ToString();

        // Match against every configured ApiKeys:{client} = secret.
        var section = _config.GetSection("ApiKeys");
        foreach (var kv in section.GetChildren())
        {
            // Skip meta/comment keys and unset placeholder values so they can
            // never be used as a real credential.
            if (kv.Key.StartsWith("_") || kv.Key.StartsWith("/")) continue;
            if (string.IsNullOrWhiteSpace(kv.Value) || kv.Value!.StartsWith("<")) continue;

            if (CryptographicEquals(kv.Value!, presented))
            {
                var claims = new[]
                {
                    new Claim("api_client", kv.Key),
                    new Claim(ClaimTypes.Name, $"api:{kv.Key}")
                };
                var identity  = new ClaimsIdentity(claims, SchemeName);
                var principal = new ClaimsPrincipal(identity);
                var ticket    = new AuthenticationTicket(principal, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
    }

    // Constant-time comparison to avoid timing side channels.
    private static bool CryptographicEquals(string a, string b)
    {
        var ab = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}
