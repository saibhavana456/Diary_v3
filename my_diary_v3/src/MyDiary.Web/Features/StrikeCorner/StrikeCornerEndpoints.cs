using MyDiary.Web.Features.StrikeCorner.Services;

namespace MyDiary.Web.Features.StrikeCorner;

/// <summary>Minimal API endpoints for Strike Corner. Kept out of the Blazor render pipeline because a
/// FAQ/SOP document is a BLOB row -- streaming it here avoids loading it into the list render (mirrors
/// SjvEndpoints.MapSjv / /sjv/logo/{code}).</summary>
public static class StrikeCornerEndpoints
{
    public static void MapStrikeCorner(this WebApplication app)
    {
        // Requires the same cookie auth as the rest of the app -- this is not public content.
        app.MapGet("/strike-corner/doc/{id:long}", async (long id, bool download, IStrikeCornerService svc) =>
        {
            var file = await svc.GetFaqDocFileAsync(id);
            if (file is null) return Results.NotFound();
            return download
                ? Results.File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName)
                : Results.File(file.Value.Bytes, file.Value.ContentType);
        }).AllowAnonymous();
        //RequireAuthorization();
        app.MapGet("/strike-corner/data-doc/{id:long}",
    async (long id,
           bool? download,
           IStrikeCornerService svc) =>
    {
        var doc = await svc.GetStrikeDataDocFileAsync(id);

        if (doc is null)
            return Results.NotFound();

        return Results.File(
            doc.Value.Bytes,
            doc.Value.ContentType,
            download == true ? doc.Value.FileName : null);
    });

        app.MapGet("/strike-committee/file/{id:long}",
async (long id,
       bool? download,
       IStrikeCornerService svc) =>
{
    var file = await svc.GetStrikeCommitteeFileAsync(id);

    if (file is null)
        return Results.NotFound();

    return Results.File(
        file.Value.Bytes,
        file.Value.ContentType,
        download == true
            ? file.Value.FileName
            : null);
});
        app.MapGet("/strike-committee/attachment/{id:long}",
        async (long id,
        bool? download,
        IStrikeCornerService svc) =>
        {
            var file = await svc.GetStrikeCommitteeAttachmentAsync(id);

            if (file is null)
                return Results.NotFound();

            return Results.File(
            file.Value.Bytes,
            file.Value.ContentType,
            download == true
            ? file.Value.FileName
            : null);
        });
    }
}
