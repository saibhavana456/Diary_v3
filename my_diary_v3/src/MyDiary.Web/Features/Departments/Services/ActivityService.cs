using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Departments.Models;
using System.Data;

namespace MyDiary.Web.Features.Departments.Services
{
    public class ActivityService : IActivityService
    {
        private readonly SqlDbProvider _db;
        private const string ConnectionName = "PortalDBConnection";

        public ActivityService(SqlDbProvider db) => _db = db;

        public async Task<List<Activity>> GetActivitiesByDeptIdAsync(int deptId)
        {
            try
            {
                string query = @"SELECT ActivityID, ActivityName, ActivityCode, 
                                Description, IconClass, Status, EstimatedHours 
                                FROM UBINET_Activities 
                                WHERE DepartmentID = @DeptID 
                                AND Status = 'Active'
                                ORDER BY SortOrder, ActivityName";

                var parameters = new[] { new SqlParameter("@DeptID", deptId) };
                DataTable dt = await _db.ExecuteQueryWithParamsAsync(query, parameters, ConnectionName);

                var list = new List<Activity>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new Activity
                    {
                        ActivityID = Convert.ToInt32(row["ActivityID"]),
                        ActivityName = row["ActivityName"].ToString() ?? "",
                        ActivityCode = row["ActivityCode"].ToString() ?? "",
                        Description = row["Description"].ToString() ?? "",
                        IconClass = row["IconClass"].ToString() ?? "",
                        Status = row["Status"].ToString() ?? "",
                        EstimatedHours = Convert.ToInt32(row["EstimatedHours"])
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[ActivityService] GetActivitiesByDeptIdAsync failed: " + ex.Message);
                return new List<Activity>();
            }
        }
    }
}
