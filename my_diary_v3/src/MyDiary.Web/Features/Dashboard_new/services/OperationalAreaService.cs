using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Dashboard_new.Models;

namespace MyDiary.Web.Features.Dashboard_new.services
{
    public interface IOperationalAreasService
    {
        Task<List<OperationalItem>> GetOperationalItemsAsync(string solId);
        Task<bool> HasSubmittedSurveyAsync(string? pfNo);
    }

    public sealed class OperationalAreasService : IOperationalAreasService
    {
        private readonly SqlDbProvider _db;

        public OperationalAreasService(SqlDbProvider db)
        {
            _db = db;
        }

        public async Task<List<OperationalItem>> GetOperationalItemsAsync(string solId)
        {
            try
            {
                var p = new SqlParameter[] { new("@SolID", solId) };
                var dt = await _db.ExecuteStoredProcAsync("uspGetTargetActualsForDashboard", p);

                var list = new List<OperationalItem>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new OperationalItem
                    {
                        Parameter = row["data_type"]?.ToString() ?? string.Empty,
                        PrevDay2No = SafeDecimal(row["PrevDay2NoOfDormant"]),
                        PrevDayNo = SafeDecimal(row["PrevDayNoOfDormant"]),
                        PrevDay2Date = SafeDate(row["PrevDay2Dt_dormant"]),
                        PrevDayDate = SafeDate(row["PrevDayDt_dormant"]),
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[OperationalAreasService] GetOperationalItemsAsync failed: " + ex.Message);
                return new List<OperationalItem>();
            }
        }

        public async Task<bool> HasSubmittedSurveyAsync(string? pfNo)
        {
            if (string.IsNullOrWhiteSpace(pfNo)) return false;

            try
            {
                string sql = "SELECT TOP 1 1 FROM MyDiary_Med_Survey_Data WHERE PFNO = @pfNo";

                var dt = await _db.ExecuteQueryWithParamsAsync(sql, new[] { new Microsoft.Data.SqlClient.SqlParameter("@pfNo", pfNo) });
                return dt.Rows.Count > 0;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[OperationalAreasService] HasSubmittedSurveyAsync failed: " + ex.Message);
                return false;
            }
        }

        private static decimal SafeDecimal(object o) =>
            o == DBNull.Value ? 0 : Convert.ToDecimal(o);

        private static DateTime SafeDate(object o) =>
            o == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(o);
    }
}