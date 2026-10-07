using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Banner.Models;

namespace MyDiary.Web.Features.Banner.Services;

public class BannerService : IBannerService
{
    private readonly SqlDbProvider _db;

    public BannerService(SqlDbProvider db)
    {
        _db = db;
    }

    public async Task<BannerConfig?> GetActiveBannerAsync()
    {
        try
        {
            var query = @"
                SELECT TOP 1 
                    BANNER_ID, IMAGE_URL, REDIRECT_URL, TITLE,
                    IS_ACTIVE, DISPLAY_ORDER, START_DATE, END_DATE, CREATED_DATE
                FROM APP_BANNER_CONFIG
                WHERE IS_ACTIVE = 1
                  AND (START_DATE IS NULL OR START_DATE <= GETDATE())
                  AND (END_DATE IS NULL OR END_DATE >= GETDATE())
                ORDER BY DISPLAY_ORDER ASC, CREATED_DATE DESC";

            var dt = await _db.ExecuteQueryAsync(query, "SQLServerConnection");

            if (dt.Rows.Count == 0)
                return null;

            var row = dt.Rows[0];
            return new BannerConfig
            {
                BannerId = Convert.ToInt32(row["BANNER_ID"]),
                ImageUrl = row["IMAGE_URL"]?.ToString() ?? "",
                RedirectUrl = row["REDIRECT_URL"]?.ToString() ?? "",
                Title = row["TITLE"]?.ToString(),
                IsActive = Convert.ToBoolean(row["IS_ACTIVE"]),
                DisplayOrder = Convert.ToInt32(row["DISPLAY_ORDER"]),
                StartDate = row["START_DATE"] == DBNull.Value ? null : Convert.ToDateTime(row["START_DATE"]),
                EndDate = row["END_DATE"] == DBNull.Value ? null : Convert.ToDateTime(row["END_DATE"]),
                CreatedDate = Convert.ToDateTime(row["CREATED_DATE"])
            };
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "[BannerService] Failed to fetch active banner.");
            return null;
        }
    }
}
