using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Departments.Models;
using System.Data;
using static MyDiary.Web.Features.Departments.Models.Document;

namespace MyDiary.Web.Features.Departments.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly SqlDbProvider _db;
        private const string ConnName = "PortalDBConnection";

        public DocumentService(SqlDbProvider db)
        {
            _db = db;
        }

        public async Task<DocumentResult> GetDocumentsByActivityAsync(int activityId, string search, int page, int pageSize)
        {
            var result = new DocumentResult();

            try
            {
            const string metaQuery = @"
    SELECT a.ActivityName, d.DepartmentName, d.DepartmentID
    FROM UBINET_Activities a
    INNER JOIN UBINET_Departments d ON a.DepartmentID = d.DepartmentID
    WHERE a.ActivityID = @ActivityID";

            var metaParams = new[] { new SqlParameter("@ActivityID", activityId) };
            DataTable metaDt = await _db.ExecuteQueryWithParamsAsync(metaQuery, metaParams, ConnName);

            if (metaDt.Rows.Count > 0)
            {
                result.ActivityName = metaDt.Rows[0]["ActivityName"]?.ToString() ?? string.Empty;
                result.DepartmentName = metaDt.Rows[0]["DepartmentName"]?.ToString() ?? string.Empty;
                result.DepartmentID = Convert.ToInt32(metaDt.Rows[0]["DepartmentID"]);
            }

            // 2) Build filter (use the SAME table for count and data)
            var where = new List<string>
    {
        "ActivityID = @ActivityID",
        "IsActive = 1",

        // NEW: Exclude children of folders from the top-level list.
        // A child has UploadedBy = <parent DocumentID> (stored as text).
        // This condition becomes a no-op if there are no folders in this activity.
        @"
        (
            CASE WHEN ISNUMERIC(LTRIM(RTRIM(UploadedBy))) = 1 AND LTRIM(RTRIM(UploadedBy)) NOT LIKE '%[^0-9]%'
                 THEN CAST(LTRIM(RTRIM(UploadedBy)) AS int) ELSE NULL END IS NULL
            OR CASE WHEN ISNUMERIC(LTRIM(RTRIM(UploadedBy))) = 1 AND LTRIM(RTRIM(UploadedBy)) NOT LIKE '%[^0-9]%'
                    THEN CAST(LTRIM(RTRIM(UploadedBy)) AS int) ELSE NULL END NOT IN
            (
                SELECT p.DocumentID
                FROM UBINET_Documents p
                WHERE p.IsActive = 1
                  AND p.ActivityID = @ActivityID
                  AND UPPER(ISNULL(p.DocumentType, '')) = 'FOLDER'
            )
        )"
    };

            var countParams = new List<SqlParameter>
    {
        new("@ActivityID", activityId)
    };
            var dataParams = new List<SqlParameter>
    {
        new("@ActivityID", activityId),
        new("@Offset", (page - 1) * pageSize),
        new("@Limit", pageSize)
    };

            if (!string.IsNullOrWhiteSpace(search))
            {
                where.Add("DocumentName LIKE @Search");
                countParams.Add(new SqlParameter("@Search", $"%{search}%"));
                dataParams.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            string filter = " WHERE " + string.Join(" AND ", where);

            // 3) Total count (unchanged)
            string countSql = $"SELECT COUNT(*) FROM UBINET_Documents {filter}";
            DataTable countDt = await _db.ExecuteQueryWithParamsAsync(countSql, countParams.ToArray(), ConnName);
            result.TotalRecords = (countDt.Rows.Count > 0) ? Convert.ToInt32(countDt.Rows[0][0]) : 0;

            // 4) Paged data (include IsJspCompleted + NewFilePath)
            string dataSql = $@"
    SELECT
        DocumentID,
        DocumentName,
        DocumentType,
        FilePath,
        FileSize,
        UploadedBy,
        UploadDate,
        Downloads,
        NewFilePath,
        IsJspCompleted
    FROM UBINET_Documents
    {filter}
    ORDER BY
        CASE WHEN ActivityID = 16 THEN DocumentID END ASC,
        CASE WHEN ActivityID <> 16 THEN DocumentName END ASC
    OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

            DataTable dataDt = await _db.ExecuteQueryWithParamsAsync(dataSql, dataParams.ToArray(), ConnName);

            foreach (DataRow row in dataDt.Rows)
            {
                var doc = new Document
                {
                    DocumentID = Convert.ToInt32(row["DocumentID"]),
                    DocumentName = row["DocumentName"]?.ToString() ?? string.Empty,
                    DocumentType = row["DocumentType"]?.ToString() ?? string.Empty,
                    FilePath = row["FilePath"]?.ToString() ?? string.Empty,
                    // Null-safe numeric & date conversions
                    FileSize = row.IsNull("FileSize") ? 0L : Convert.ToInt64(row["FileSize"]),
                    UploadedBy = row["UploadedBy"]?.ToString() ?? string.Empty,
                    UploadDate = row.IsNull("UploadDate") ? DateTime.MinValue : Convert.ToDateTime(row["UploadDate"]),
                    Downloads = row.IsNull("Downloads") ? 0 : Convert.ToInt32(row["Downloads"]),
                    NewFilePath = row["NewFilePath"]?.ToString() ?? string.Empty,
                    // Flag that UI uses to decide View/Hide inline vs Click Here
                    IsJspCompleted = !row.IsNull("IsJspCompleted") && Convert.ToBoolean(row["IsJspCompleted"])
                };

                result.Documents.Add(doc);
            }

            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DocumentService] GetDocumentsByActivityAsync failed: " + ex.Message);
            }

            return result;
        }
        public async Task<List<Document>> GetChildDocumentsByParentUploadedByAsync(int parentDocumentId)
        {
            try
            {
                const string sql = @"
SELECT
    DocumentID,
    DocumentName,
    DocumentType,
    FilePath,
    FileSize,
    UploadedBy,
    UploadDate,
    Downloads,
    NewFilePath,
    IsJspCompleted
FROM UBINET_Documents
WHERE IsActive = 1
  AND ISNUMERIC(LTRIM(RTRIM(UploadedBy))) = 1
  AND LTRIM(RTRIM(UploadedBy)) NOT LIKE '%[^0-9]%'
  AND CAST(LTRIM(RTRIM(UploadedBy)) AS int) = @ParentId
ORDER BY DocumentName ASC;";

            var dt = await _db.ExecuteQueryWithParamsAsync(
                sql,
                new[] { new SqlParameter("@ParentId", parentDocumentId) },
                ConnName);

            var list = new List<Document>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new Document
                {
                    DocumentID = Convert.ToInt32(row["DocumentID"]),
                    DocumentName = row["DocumentName"]?.ToString() ?? string.Empty,
                    DocumentType = row["DocumentType"]?.ToString() ?? string.Empty,
                    FilePath = row["FilePath"]?.ToString() ?? string.Empty,
                    FileSize = row.IsNull("FileSize") ? 0L : Convert.ToInt64(row["FileSize"]),
                    UploadedBy = row["UploadedBy"]?.ToString() ?? string.Empty,
                    UploadDate = row.IsNull("UploadDate") ? DateTime.MinValue : Convert.ToDateTime(row["UploadDate"]),
                    Downloads = row.IsNull("Downloads") ? 0 : Convert.ToInt32(row["Downloads"]),
                    NewFilePath = row["NewFilePath"]?.ToString() ?? string.Empty,
                    IsJspCompleted = !row.IsNull("IsJspCompleted") && Convert.ToBoolean(row["IsJspCompleted"])
                });
            }
            return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DocumentService] GetChildDocumentsByParentUploadedByAsync failed: " + ex.Message);
                return new List<Document>();
            }
        }

    }
}
