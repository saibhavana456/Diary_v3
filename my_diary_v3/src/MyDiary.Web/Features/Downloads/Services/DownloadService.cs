using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Downloads.Models;
using System.Data;
using System.Diagnostics;

namespace MyDiary.Web.Features.Downloads.Services
{
    public class DownloadService : IDownloadService
    {
        private readonly SqlDbProvider _db;
        private const string ConnName = "PortalDBConnection";

        public DownloadService(SqlDbProvider db)
        {
            _db = db;
        }

        public async Task<List<Download>> GetDownloadsAsync()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                string query = @"
    SELECT ActivityID,
           ActivityName,
           ActivityName_HI,
           ActivityCode,
           Description,
           IconClass,
           Status
    FROM UBINET_Activities
    WHERE DepartmentID = '50'
      AND Status = 'Active'
    ORDER BY SortOrder, ActivityName";

                var dbSw = Stopwatch.StartNew();
                DataTable dt = await _db.ExecuteQueryAsync(query, ConnName);
                dbSw.Stop();
                AppLogger.LogInfo($"[DownloadService] DB query executed in {dbSw.ElapsedMilliseconds}ms, rows returned: {dt.Rows.Count}");

                var downloads = new List<Download>(dt.Rows.Count);

                foreach (DataRow row in dt.Rows)
                {
                    downloads.Add(new Download
                    {
                        ActivityID = Convert.ToInt32(row["ActivityID"]),
                        ActivityName = row["ActivityName"]?.ToString() ?? "",
                        ActivityName_HI = row["ActivityName_HI"]?.ToString() ?? "",
                        ActivityCode = row["ActivityCode"]?.ToString() ?? "",
                        Description = row["Description"]?.ToString() ?? "",
                        IconClass = row["IconClass"]?.ToString() ?? "",
                        Status = row["Status"]?.ToString() ?? ""
                    });
                }

                sw.Stop();
                AppLogger.LogInfo($"[DownloadService] GetDownloadsAsync completed in {sw.ElapsedMilliseconds}ms total ({downloads.Count} items)");
                return downloads;
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppLogger.LogError(ex, $"[DownloadService] GetDownloadsAsync failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                return new List<Download>();
            }
        }
    }
}
