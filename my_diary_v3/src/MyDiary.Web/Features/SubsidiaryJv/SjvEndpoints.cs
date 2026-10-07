using MyDiary.Web.Features.SubsidiaryJv.Services;

namespace MyDiary.Web.Features.SubsidiaryJv;

/// <summary>Minimal API endpoints for the Subsidiaries and JV Corner feature. Kept out of the Blazor render
/// pipeline because a logo is a BLOB on RP_SJV_ENTITY -- streaming it here avoids loading it into every
/// list/page render, and lets a plain &lt;img src="..."&gt; be used instead of a base64 data URI.</summary>
public static class SjvEndpoints
{
    public static void MapSjv(this WebApplication app)
    {
        // Requires the same cookie auth as the rest of the app -- this is not public content.
        app.MapGet("/sjv/logo/{code}", async (string code, ISubsidiaryJvService svc) =>
        {
            var logo = await svc.GetLogoAsync(code);
            return logo is null ? Results.NotFound() : Results.File(logo.Value.Bytes, logo.Value.ContentType);
        }).AllowAnonymous();
           // .RequireAuthorization();
    }
}
