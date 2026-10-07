using System.Data;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;

namespace MyDiary.Web.Features.Landing.Services;

/// <summary>
/// Loads landing page carousel images and board members from SQL Server.
/// Uses SqlDbProvider (same as LeftNavService and other landing page services).
/// </summary>
public sealed class LandingCarouselService : ILandingCarouselService
{
    private readonly SqlDbProvider _db;

    public LandingCarouselService(SqlDbProvider db)
    {
        _db = db;
    }

    public async Task<List<CarouselImageItem>> GetActiveImagesAsync()
    {
        const string sql = @"
            SELECT ID, FILE_NAME, ALT_TEXT, SORT_ORDER
            FROM MD_LANDING_CAROUSEL
            WHERE IS_ACTIVE = 1
            ORDER BY SORT_ORDER";

        try
        {
            var dt = await _db.ExecuteQueryAsync(sql);
            var items = new List<CarouselImageItem>();

            foreach (DataRow row in dt.Rows)
            {
                items.Add(new CarouselImageItem
                {
                    Id = Convert.ToInt64(row["ID"]),
                    FileName = row["FILE_NAME"]?.ToString()?.Trim() ?? "",
                    AltText = row["ALT_TEXT"]?.ToString()?.Trim() ?? "",
                    SortOrder = Convert.ToInt32(row["SORT_ORDER"])
                });
            }

            return items;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "LandingCarouselService: failed to load carousel images from DB");
            return new List<CarouselImageItem>();
        }
    }

    public async Task<List<BoardMemberItem>> GetActiveBoardMembersAsync()
    {
        const string sql = @"
    SELECT ID,
           NAME_KEY,
           ROLE_KEY,
           IMAGE_FILE_NAME,
           DIALOG_IMAGE_FILE_NAME,
           DIALOG_IMAGE_HINDI_FILE,
           SUMMARY_I18N_KEY,
           BIO_I18N_KEY,
           IS_MD,
           SORT_ORDER
    FROM MD_LANDING_BOARD_MEMBERS
    WHERE IS_ACTIVE = 1
    ORDER BY SORT_ORDER";

        try
        {
            var dt = await _db.ExecuteQueryAsync(sql);
            var items = new List<BoardMemberItem>();

            foreach (DataRow row in dt.Rows)
            {
                items.Add(new BoardMemberItem
                {
                    Id = Convert.ToInt64(row["ID"]),
                    NameKey = row["NAME_KEY"]?.ToString()?.Trim() ?? "",
                    RoleKey = row["ROLE_KEY"]?.ToString()?.Trim() ?? "",
                    ImageFileName = row["IMAGE_FILE_NAME"]?.ToString()?.Trim() ?? "",
                    DialogImageFileName = row["DIALOG_IMAGE_FILE_NAME"]?.ToString()?.Trim() ?? "",
                    DialogImageHindiFileName = row["DIALOG_IMAGE_HINDI_FILE"]?.ToString()?.Trim() ?? "",
                    SummaryI18nKey = row["SUMMARY_I18N_KEY"]?.ToString()?.Trim() ?? "",
                    BioI18nKey = row["BIO_I18N_KEY"]?.ToString()?.Trim() ?? "",
                    IsMD = Convert.ToInt32(row["IS_MD"]) == 1,
                    SortOrder = Convert.ToInt32(row["SORT_ORDER"])
                });
            }

            return items;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "LandingCarouselService: failed to load board members from DB");
            return new List<BoardMemberItem>();
        }
    }
}
