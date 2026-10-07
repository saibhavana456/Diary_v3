using Oracle.ManagedDataAccess.Client;
using System.Data;
using MyDiary.Web.Features.Shared.Models;

namespace MyDiary.Web.Features.Shared.Services;

public class PageInfoService : IPageInfoService
{
    private readonly IConfiguration _configuration;

    public PageInfoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<PageInfoModel?> GetPageInfoAsync(string pageKey)
    {
        PageInfoModel? page = null;

        AppLogger.LogInfo($"Page Info Lookup : [{pageKey}]");

        var connectionString =
            _configuration.GetConnectionString("MyDiaryDBConnection");

        await using var con =
            new OracleConnection(connectionString);

        await con.OpenAsync();


        //const string sql = @"
        //    SELECT *
        //    FROM PAGE_INFO
        //    WHERE PAGE_KEY = 'Accounts-Closed-during-this-FY'";


        const string sql = @"
        SELECT
            PAGE_KEY,
            PAGE_TITLE,
            DESCRIPTION,
            CATEGORY,
            STATUS,
            VERSION_NO,
            OWNER_VERTICAL,
            MODULE_OWNER,
            MAIL_ID,
            IP_NUMBER,
            CREATED_DATE,
            PAGE_VERSION
        FROM PAGE_INFO
        WHERE UPPER(TRIM(PAGE_KEY)) = UPPER(TRIM(:PAGE_KEY))
        AND IS_ACTIVE = 'Y'";

        await using var cmd =
            new OracleCommand(sql, con);

        cmd.Parameters.Add(
            "PAGE_KEY",
            OracleDbType.Varchar2).Value = pageKey;

        await using var reader =
            await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            page = new PageInfoModel
            {
                PageKey = reader["PAGE_KEY"]?.ToString(),
                Title = reader["PAGE_TITLE"]?.ToString(),
                Description = reader["DESCRIPTION"]?.ToString(),

                Category = reader["CATEGORY"]?.ToString(),
                Status = reader["STATUS"]?.ToString(),
                Version = reader["VERSION_NO"]?.ToString(),

                OwnerVertical = reader["OWNER_VERTICAL"]?.ToString(),
                ModuleOwner = reader["MODULE_OWNER"]?.ToString(),
                MailId = reader["MAIL_ID"]?.ToString(),

                IpNumber = reader["IP_NUMBER"]?.ToString(),
                PageVersion = reader["PAGE_VERSION"]?.ToString(),

                CreatedDate =
                    reader["CREATED_DATE"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["CREATED_DATE"])
            };
        }

        return page;
    }
}


//using MyDiary.Web.Features.Shared.Models;

//namespace MyDiary.Web.Features.Shared.Services;

//public class PageInfoService : IPageInfoService
//{
//    private readonly Dictionary<string, PageInfoModel> _pages =
//        new(StringComparer.OrdinalIgnoreCase)
//        {
//            {
//                "focus360",
//                new PageInfoModel
//                {
//                    PageKey = "focus360",
//                    Title = "Focus 360",
//                    Description =
//                        "Real-time branch level overview displaying business, operations, compliance, digital banking and performance indicators.",

//                    Category = "DASHBOARD",
//                    Status = "ACTIVE",
//                    Version = "v1.0",

//                    OwnerVertical = "Retail Banking",
//                    ModuleOwner = "Branch Analytics",
//                    MailId = "analytics@company.in",

//                    IpNumber = "IP-RP-2025-0001",
//                    CreatedDate = new DateTime(2025,01,01),
//                    PageVersion = "v1.0"
//                }
//            }
//        };

//    public Task<PageInfoModel?> GetPageInfoAsync(string pageKey)
//    {
//        _pages.TryGetValue(pageKey, out var page);

//        return Task.FromResult(page);
//    }
//}