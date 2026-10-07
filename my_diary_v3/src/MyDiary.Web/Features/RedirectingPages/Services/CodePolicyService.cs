using System.Data;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;

namespace MyDiary.Web.Features.RedirectingPages.Services
{
    public class CodePolicyService : ICodePolicyService
    {
        private readonly SqlDbProvider _sql;

        public CodePolicyService(SqlDbProvider sql)
        {
            _sql = sql;
        }

        public async Task<List<CodePolicyDocument>> GetAllDocumentsAsync()
        {
            try
            {
                const string query = @"
                    SELECT DataId, FileName, FilePath, BaseUrl, Data_Date
                    FROM   dbo.Data_RecoveryPolicy
                    ORDER  BY Data_Date DESC";

                var dt = await _sql.ExecuteQueryAsync(query);

                return dt.AsEnumerable().Select(row => new CodePolicyDocument
                {
                    DataId   = row["DataId"]?.ToString()   ?? "",
                    FileName = row["FileName"]?.ToString()  ?? "",
                    FilePath = row["FilePath"]?.ToString()  ?? "",
                    BaseUrl  = row["BaseUrl"]?.ToString()   ?? "",
                    Date     = row["Data_Date"] == DBNull.Value
                                   ? null
                                   : Convert.ToDateTime(row["Data_Date"])
                }).ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[CodePolicyService] GetAllDocumentsAsync failed: " + ex.Message);
                return new List<CodePolicyDocument>();
            }
        }
    }
}
