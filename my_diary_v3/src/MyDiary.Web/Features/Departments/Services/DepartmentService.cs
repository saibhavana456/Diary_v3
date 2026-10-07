using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Departments.Models;
using System.Data;
using System.Diagnostics;

namespace MyDiary.Web.Features.Departments.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly SqlDbProvider _db;
        private const string ConnectionName = "PortalDBConnection";

        public DepartmentService(SqlDbProvider db)
        {
            _db = db;
        }

        public async Task<List<Department>> GetActiveDepartmentsAsync(string sectionType = "Dept")
        {
            var sw = Stopwatch.StartNew();

            try
            {
                string query = $@"
    SELECT DepartmentID,
           DepartmentName,
           DepartmentName_HI,
           DepartmentCode,
           Description,
           IconClass,
           Status
    FROM UBINET_Departments
    WHERE Status = 'Active'
      AND SectionType = '{sectionType}'
    ORDER BY SortOrder, DepartmentName";

                var dbSw = Stopwatch.StartNew();
                DataTable dt = await _db.ExecuteQueryAsync(query, ConnectionName);
                dbSw.Stop();
                AppLogger.LogInfo($"[DepartmentService] DB query executed in {dbSw.ElapsedMilliseconds}ms, rows returned: {dt.Rows.Count}");

                var list = new List<Department>(dt.Rows.Count);

                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new Department
                    {
                        DepartmentID = Convert.ToInt32(row["DepartmentID"]),
                        DepartmentName = row["DepartmentName"].ToString() ?? "",
                        DepartmentName_HI = row["DepartmentName_HI"]?.ToString() ?? "",
                        DepartmentCode = row["DepartmentCode"].ToString() ?? "",
                        Description = row["Description"].ToString() ?? "",
                        IconClass = row["IconClass"].ToString() ?? "",
                        Status = row["Status"].ToString() ?? ""
                    });
                }

                sw.Stop();
                AppLogger.LogInfo($"[DepartmentService] GetActiveDepartmentsAsync completed in {sw.ElapsedMilliseconds}ms total ({list.Count} items)");
                return list;
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppLogger.LogError(ex, $"[DepartmentService] GetActiveDepartmentsAsync failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                return new List<Department>();
            }
        }
    }
}
