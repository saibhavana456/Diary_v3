using Oracle.ManagedDataAccess.Client;
using MyDiary.Web.Features.UnionHubVideoLaunch.Models.DTO;

namespace MyDiary.Web.Features.UnionHubVideoLaunch.Services;

/// <summary>
/// Union Hub Video Launch — Oracle-backed, self-contained (Flat Management / Positive
/// Branch List style): DTOs + service directly under Features/, raw OracleConnection/
/// OracleCommand ADO, no Core/Data project changes. Auto-registered by
/// AddApplicationServices() via the XyzService/IXyzService naming convention.
/// </summary>
public sealed class UnionHubVideoLaunchService : IUnionHubVideoLaunchService
{
    private readonly string _connString;
    private readonly ILogger<UnionHubVideoLaunchService> _logger;

    public UnionHubVideoLaunchService(IConfiguration config, ILogger<UnionHubVideoLaunchService> logger)
    {
        _connString = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner in appsettings.json");
        _logger = logger;
    }

    private OracleConnection Conn() => new(_connString);

    public async Task<List<UhvVertical>> ListVerticalsAsync(CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using var cmd = new OracleCommand(
            "SELECT ID, NAME FROM RP_UHV_VERTICAL WHERE IS_ACTIVE = 1 ORDER BY NAME", cn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UhvVertical>();
        while (await r.ReadAsync(ct))
            list.Add(new UhvVertical { Id = r.GetInt64(0), Name = r.GetString(1) });
        return list;
    }

    public async Task<List<UhvLaunch>> ListPublishedAsync(long? verticalId, string viewerEmpCode, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);

        var sql = @"SELECT l.ID, l.VERTICAL_ID, v.NAME, l.PRODUCT_NAME, l.LAUNCH_DATE, l.DESCRIPTION,
                            CASE WHEN l.VIDEO_CONTENT IS NULL THEN 0 ELSE 1 END, l.VIDEO_FILE_NAME,
                            l.STATUS, l.CREATED_BY_EMP, l.CREATED_AT, l.PUBLISHED_AT
                     FROM RP_UHV_LAUNCH l JOIN RP_UHV_VERTICAL v ON v.ID = l.VERTICAL_ID
                     WHERE l.STATUS = 'PUBLISHED'" +
                    (verticalId.HasValue ? " AND l.VERTICAL_ID = :p_vert" : "") +
                    " ORDER BY l.PUBLISHED_AT DESC";

        await using var cmd = new OracleCommand(sql, cn);
        if (verticalId.HasValue) cmd.Parameters.Add("p_vert", verticalId.Value);

        var list = new List<UhvLaunch>();
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
                list.Add(MapLaunch(r));
        }

        foreach (var launch in list)
            await HydrateAsync(cn, launch, viewerEmpCode, ct);

        return list;
    }

    public async Task<List<UhvLaunch>> ListAllForAdminAsync(CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        const string sql = @"SELECT l.ID, l.VERTICAL_ID, v.NAME, l.PRODUCT_NAME, l.LAUNCH_DATE, l.DESCRIPTION,
                                     CASE WHEN l.VIDEO_CONTENT IS NULL THEN 0 ELSE 1 END, l.VIDEO_FILE_NAME,
                                     l.STATUS, l.CREATED_BY_EMP, l.CREATED_AT, l.PUBLISHED_AT
                              FROM RP_UHV_LAUNCH l JOIN RP_UHV_VERTICAL v ON v.ID = l.VERTICAL_ID
                              ORDER BY l.CREATED_AT DESC";
        await using var cmd = new OracleCommand(sql, cn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<UhvLaunch>();
        while (await r.ReadAsync(ct))
            list.Add(MapLaunch(r));
        return list;
    }

    public async Task<UhvLaunch?> GetAsync(long launchId, string viewerEmpCode, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        const string sql = @"SELECT l.ID, l.VERTICAL_ID, v.NAME, l.PRODUCT_NAME, l.LAUNCH_DATE, l.DESCRIPTION,
                                     CASE WHEN l.VIDEO_CONTENT IS NULL THEN 0 ELSE 1 END, l.VIDEO_FILE_NAME,
                                     l.STATUS, l.CREATED_BY_EMP, l.CREATED_AT, l.PUBLISHED_AT
                              FROM RP_UHV_LAUNCH l JOIN RP_UHV_VERTICAL v ON v.ID = l.VERTICAL_ID
                              WHERE l.ID = :p_id";
        await using var cmd = new OracleCommand(sql, cn);
        cmd.Parameters.Add("p_id", launchId);
        UhvLaunch? launch = null;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (await r.ReadAsync(ct)) launch = MapLaunch(r);
        }
        if (launch is null) return null;
        await HydrateAsync(cn, launch, viewerEmpCode, ct);
        return launch;
    }

    private static UhvLaunch MapLaunch(OracleDataReader r) => new()
    {
        Id = r.GetInt64(0),
        VerticalId = r.GetInt64(1),
        VerticalName = r.GetString(2),
        ProductName = r.GetString(3),
        LaunchDate = r.GetDateTime(4),
        Description = r.IsDBNull(5) ? null : r.GetOracleClob(5).Value,
        HasVideo = r.GetInt32(6) == 1,
        VideoFileName = r.IsDBNull(7) ? null : r.GetString(7),
        Status = r.GetString(8),
        CreatedByEmp = r.GetString(9),
        CreatedAt = r.GetDateTime(10),
        PublishedAt = r.IsDBNull(11) ? null : r.GetDateTime(11),
    };

    private static async Task HydrateAsync(OracleConnection cn, UhvLaunch launch, string viewerEmpCode, CancellationToken ct)
    {
        await using (var cmd = new OracleCommand(
            "SELECT ID, KIND, FILE_NAME, MIME FROM RP_UHV_ATTACHMENT WHERE LAUNCH_ID = :p_id ORDER BY ID", cn))
        {
            cmd.Parameters.Add("p_id", launch.Id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                launch.Attachments.Add(new UhvAttachment
                {
                    Id = r.GetInt64(0),
                    LaunchId = launch.Id,
                    Kind = r.GetString(1),
                    FileName = r.GetString(2),
                    Mime = r.IsDBNull(3) ? null : r.GetString(3),
                });
        }

        await using (var cmd = new OracleCommand(
            "SELECT COUNT(*) FROM RP_UHV_LIKE WHERE LAUNCH_ID = :p_id", cn))
        {
            cmd.Parameters.Add("p_id", launch.Id);
            launch.LikeCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        await using (var cmd = new OracleCommand(
            "SELECT COUNT(*) FROM RP_UHV_LIKE WHERE LAUNCH_ID = :p_id AND EMP_CODE = :p_emp", cn))
        {
            cmd.Parameters.Add("p_id", launch.Id);
            cmd.Parameters.Add("p_emp", viewerEmpCode ?? "");
            launch.LikedByMe = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
        }

        await using (var cmd = new OracleCommand(
            "SELECT ID, EMP_CODE, EMP_NAME, COMMENT_TEXT, CREATED_AT FROM RP_UHV_COMMENT WHERE LAUNCH_ID = :p_id ORDER BY CREATED_AT ASC", cn))
        {
            cmd.Parameters.Add("p_id", launch.Id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                launch.Comments.Add(new UhvComment
                {
                    Id = r.GetInt64(0),
                    EmpCode = r.GetString(1),
                    EmpName = r.IsDBNull(2) ? r.GetString(1) : r.GetString(2),
                    CommentText = r.GetString(3),
                    CreatedAt = r.GetDateTime(4),
                });
        }
    }

    public async Task<long> CreateDraftAsync(UhvCreateDraftRequest req, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using var tx = cn.BeginTransaction();

        long launchId;
        await using (var cmd = new OracleCommand(@"
            INSERT INTO RP_UHV_LAUNCH
                (ID, VERTICAL_ID, PRODUCT_NAME, LAUNCH_DATE, DESCRIPTION, VIDEO_FILE_NAME, VIDEO_MIME, VIDEO_CONTENT, STATUS, CREATED_BY_EMP)
            VALUES
                (RP_SEQ_GLOBAL.NEXTVAL, :p_vert, :p_name, :p_date, :p_desc, :p_vfn, :p_vmime, :p_vcontent, 'DRAFT', :p_emp)
            RETURNING ID INTO :p_id", cn) { Transaction = tx })
        {
            cmd.Parameters.Add("p_vert", req.VerticalId);
            cmd.Parameters.Add("p_name", req.ProductName);
            cmd.Parameters.Add("p_date", req.LaunchDate);
            cmd.Parameters.Add("p_desc", OracleDbType.Clob).Value = (object?)req.Description ?? DBNull.Value;
            cmd.Parameters.Add("p_vfn", OracleDbType.Varchar2).Value = (object?)req.Video?.FileName ?? DBNull.Value;
            cmd.Parameters.Add("p_vmime", OracleDbType.Varchar2).Value = (object?)req.Video?.Mime ?? DBNull.Value;
            cmd.Parameters.Add("p_vcontent", OracleDbType.Blob).Value = (object?)req.Video?.Content ?? DBNull.Value;
            cmd.Parameters.Add("p_emp", req.CreatedByEmp);
            var outId = cmd.Parameters.Add("p_id", OracleDbType.Int64, System.Data.ParameterDirection.Output);
            await cmd.ExecuteNonQueryAsync(ct);
            launchId = Convert.ToInt64(((Oracle.ManagedDataAccess.Types.OracleDecimal)outId.Value).Value);
        }

        foreach (var (kind, file) in req.Attachments)
        {
            await using var cmd = new OracleCommand(@"
                INSERT INTO RP_UHV_ATTACHMENT (ID, LAUNCH_ID, KIND, FILE_NAME, MIME, CONTENT)
                VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_launch, :p_kind, :p_fn, :p_mime, :p_content)", cn) { Transaction = tx };
            cmd.Parameters.Add("p_launch", launchId);
            cmd.Parameters.Add("p_kind", kind);
            cmd.Parameters.Add("p_fn", file.FileName);
            cmd.Parameters.Add("p_mime", OracleDbType.Varchar2).Value = (object?)file.Mime ?? DBNull.Value;
            cmd.Parameters.Add("p_content", OracleDbType.Blob).Value = file.Content;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        tx.Commit();
        return launchId;
    }

    public async Task<UhvLaunch?> PublishAsync(long launchId, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using (var cmd = new OracleCommand(
            "UPDATE RP_UHV_LAUNCH SET STATUS = 'PUBLISHED', PUBLISHED_AT = SYSDATE WHERE ID = :p_id AND STATUS = 'DRAFT'", cn))
        {
            cmd.Parameters.Add("p_id", launchId);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0) return null; // already published, or doesn't exist
        }
        return await GetAsync(launchId, "", ct);
    }

    public async Task<(Stream Stream, string Mime, string FileName)?> ReadVideoAsync(long launchId, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using var cmd = new OracleCommand(
            "SELECT VIDEO_FILE_NAME, VIDEO_MIME, VIDEO_CONTENT FROM RP_UHV_LAUNCH WHERE ID = :p_id", cn);
        cmd.Parameters.Add("p_id", launchId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || r.IsDBNull(2)) return null;
        var fileName = r.IsDBNull(0) ? "video.mp4" : r.GetString(0);
        var mime = r.IsDBNull(1) ? "video/mp4" : r.GetString(1);
        var blob = r.GetOracleBlob(2);
        return (new MemoryStream(blob.Value), mime, fileName);
    }

    public async Task<(Stream Stream, string Mime, string FileName)?> ReadAttachmentAsync(long attachmentId, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using var cmd = new OracleCommand(
            "SELECT FILE_NAME, MIME, CONTENT FROM RP_UHV_ATTACHMENT WHERE ID = :p_id", cn);
        cmd.Parameters.Add("p_id", attachmentId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || r.IsDBNull(2)) return null;
        var fileName = r.GetString(0);
        var mime = r.IsDBNull(1) ? "application/octet-stream" : r.GetString(1);
        var blob = r.GetOracleBlob(2);
        return (new MemoryStream(blob.Value), mime, fileName);
    }

    public async Task ToggleLikeAsync(long launchId, string empCode, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using (var check = new OracleCommand(
            "SELECT COUNT(*) FROM RP_UHV_LIKE WHERE LAUNCH_ID = :p_id AND EMP_CODE = :p_emp", cn))
        {
            check.Parameters.Add("p_id", launchId);
            check.Parameters.Add("p_emp", empCode);
            var exists = Convert.ToInt32(await check.ExecuteScalarAsync(ct)) > 0;

            if (exists)
            {
                await using var del = new OracleCommand(
                    "DELETE FROM RP_UHV_LIKE WHERE LAUNCH_ID = :p_id AND EMP_CODE = :p_emp", cn);
                del.Parameters.Add("p_id", launchId);
                del.Parameters.Add("p_emp", empCode);
                await del.ExecuteNonQueryAsync(ct);
            }
            else
            {
                await using var ins = new OracleCommand(
                    "INSERT INTO RP_UHV_LIKE (ID, LAUNCH_ID, EMP_CODE) VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_id, :p_emp)", cn);
                ins.Parameters.Add("p_id", launchId);
                ins.Parameters.Add("p_emp", empCode);
                await ins.ExecuteNonQueryAsync(ct);
            }
        }
    }

    public async Task AddCommentAsync(long launchId, string empCode, string empName, string text, CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        await using var cmd = new OracleCommand(@"
            INSERT INTO RP_UHV_COMMENT (ID, LAUNCH_ID, EMP_CODE, EMP_NAME, COMMENT_TEXT)
            VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_id, :p_emp, :p_name, :p_text)", cn);
        cmd.Parameters.Add("p_id", launchId);
        cmd.Parameters.Add("p_emp", empCode);
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2).Value = (object?)empName ?? DBNull.Value;
        cmd.Parameters.Add("p_text", text);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<string>> ListActiveEmployeeCodesAsync(CancellationToken ct = default)
    {
        await using var cn = Conn();
        await cn.OpenAsync(ct);
        // NOTE: UserRepo.cs's "staff_details" source lives in a SEPARATE org database
        // (IOrganisationsDbFactory), not the RP_OWNER schema this feature's Conn() points
        // at — querying it here threw ORA-00942 (caught during live verification). RP_USER
        // is the actual local RP_OWNER table backing RP_NOTIFICATION.USER_ID elsewhere in
        // this schema (see MyDiary.Data/RequestPortal/Repositories/UserRepo.cs GetByEmpCodesAsync
        // for the equivalent org-DB shape), so recipients are resolved from it instead.
        await using var cmd = new OracleCommand(
            "SELECT EMP_CODE FROM RP_USER WHERE IS_ACTIVE = 1", cn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<string>();
        while (await r.ReadAsync(ct))
            list.Add(r.GetValue(0)?.ToString() ?? "");
        return list.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }
}
