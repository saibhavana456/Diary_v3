using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Downloads.Models;
using System.Data;

namespace MyDiary.Web.Features.ITKonnect.Services
{
    public class ITKonnectService : IITKonnectService
    {
        private readonly SqlDbProvider _db;
        private const string ConnName = "PortalDBConnection";

        public ITKonnectService(SqlDbProvider db)
        {
            _db = db;
        }

        public async Task<List<Download>> GetItemsAsync()
        {
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
                    WHERE (DepartmentID = '54' or DepartmentID = '56')
                      AND Status = 'Active'
                    ORDER BY SortOrder, ActivityName";

                DataTable dt = await _db.ExecuteQueryAsync(query, ConnName);

                var items = new List<Download>();

                foreach (DataRow row in dt.Rows)
                {
                    items.Add(new Download
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

                return items;
            }
            catch
            {
                return new List<Download>();
            }
        }
    }
}