using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.SqlClient;
using MyDiary.Core.Services;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.IPDirectory.Models;
using System.Data;
using System.Text;
using OfficeOpenXml;
using System.Globalization;

namespace MyDiary.Web.Features.IPDirectory.Services
{
    public class IPDirectoryService : IIPDirectoryService
    {
        private readonly SqlDbProvider _db;
        private readonly IConfiguration _config;
        private const string ConnName = "PortalDBConnection";

        private readonly string[] RequiredSheets =
 {
    "Central Office",
    "Information Technology",
    "DR Site",
    "CISO",
    "STC",
    "Zonal Office",
    "Regional Office",
    "Branches"
};

        public IPDirectoryService(
    SqlDbProvider db,
    IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<(bool Success, string Message)>
ImportIPDirectoryExcelAsync(
    Stream stream,
    string userId)
        {
            SqlConnection? conn = null;
            SqlTransaction? tran = null;

            try
            {
                using var package =
                    new ExcelPackage(stream);

                var wb = package.Workbook;

                //----------------------------------
                // Validate sheets BEFORE backup
                //----------------------------------

                foreach (var sheet in RequiredSheets)
                {
                    if (wb.Worksheets[sheet] == null)
                    {
                        return (
     false,
     $"Invalid Excel format.\n\nRequired sheet not found: {sheet}");
                    }
                }
               
                //----------------------------------
                // Begin Transaction
                //----------------------------------
                var connectionString =
    _config.GetConnectionString(
        "PortalDBConnection");
                

                conn = new SqlConnection(connectionString);

                await conn.OpenAsync();

                tran = conn.BeginTransaction();

                //----------------------------------
                // Backup
                //----------------------------------

                string backupTable =
    "IPDirectory_Backup_" +
    AppTime.Now.ToString("yyyyMMdd_HHmmssfff");

                await CreateBackupAsync(
                    conn,
                    tran,
                    backupTable);

                //----------------------------------
                // Delete Existing
                //----------------------------------

                string existingSql = @"
SELECT
    Name,
    DepartmentName,
    IPPhone,
    IsDeleted
FROM IPDirectory";

                DataTable existingDt =
                    await _db.ExecuteQueryWithParamsAsync(
                        existingSql,
                        Array.Empty<SqlParameter>(),
                        ConnName);

                var existingRecords =
    existingDt.AsEnumerable()
        .GroupBy(
            x => $"{x["Name"]}|{x["DepartmentName"]}|{x["IPPhone"]}",
            StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            g => g.Key,
            g => g.First(),
            StringComparer.OrdinalIgnoreCase);


                var importTable = new DataTable();

                importTable.Columns.Add("Name");
                importTable.Columns.Add("DepartmentName");
                importTable.Columns.Add("IPPhone");
                importTable.Columns.Add("CreatedBy");
                importTable.Columns.Add("CreatedOn", typeof(DateTime));
                importTable.Columns.Add("ModifiedBy");
                importTable.Columns.Add("ModifiedOn", typeof(DateTime));
                importTable.Columns.Add("IsDeleted");

                var excelKeys =
    new HashSet<string>(
        StringComparer.OrdinalIgnoreCase);

                int totalUpdated = 0;
                int totalUnchanged = 0;

                int totalInserted = 0;
                int totalSkipped = 0;

                foreach (var sheetName in RequiredSheets)
                {
                    var ws = wb.Worksheets[sheetName];

                    if (ws.Dimension == null)
                        continue;

                    int rowCount = ws.Dimension.Rows;

                    for (int row = 2; row <= rowCount; row++)
                    {
                        string name;
                        string ip;

                        if (sheetName == "Zonal Office" ||
                            sheetName == "Regional Office")
                        {
                            name = ws.Cells[row, 2].Text.Trim();
                            ip = ws.Cells[row, 3].Text.Trim();
                        }
                        else
                        {
                            name = ws.Cells[row, 1].Text.Trim();
                            ip = ws.Cells[row, 2].Text.Trim();
                        }

                        if (string.IsNullOrWhiteSpace(name) ||
                            string.IsNullOrWhiteSpace(ip))
                        {
                            totalSkipped++;
                            continue;
                        }

                        string department =
                            sheetName == "STC"
                                ? "Training Centres"
                                : sheetName;

                        string key =
     $"{name}|{department}|{ip}";

                        excelKeys.Add(key);

                        if (existingRecords.ContainsKey(key))
                        {
                            totalUnchanged++;
                        }
                        else
                        {
                            importTable.Rows.Add(
                                name,
                                department,
                                ip,
                                userId,
                                AppTime.Now,
                                userId,
                                AppTime.Now,
                                "N");

                            totalInserted++;
                        }

                    }
                }

                if (importTable.Rows.Count > 0)
                {
                    await BulkInsertAsync(
                        conn,
                        tran,
                        importTable);
                }

                foreach (var dbRecord in existingRecords)
                {
                    if (!excelKeys.Contains(dbRecord.Key))
                    {
                        var values =
    dbRecord.Key.Split('|');

                        await SoftDeleteImportRecordAsync(
                            conn,
                            tran,
                            values[0],    // Name
                            values[1],    // Department
                            values[2],    // IP
                            userId);
                    }
                }

                await tran.CommitAsync();

                return (
    true,
    $"Sync completed successfully.\n\n" +
    $"Inserted : {totalInserted}\n" +
    $"Updated : {totalUpdated}\n" +
    $"Unchanged : {totalUnchanged}");
            }
            catch (Exception ex)
            {
                if (tran != null)
                    await tran.RollbackAsync();

                AppLogger.LogError(
                    ex,
                    "ImportIPDirectoryExcelAsync");

                return (
                    false,
                    $"Import failed. Changes rolled back. {ex.Message}");
            }
            finally
            {
                if (conn != null)
                    await conn.DisposeAsync();
            }
        }


        private async Task SoftDeleteImportRecordAsync(
    SqlConnection conn,
    SqlTransaction tran,
    string name,
    string department,
    string ip,
    string userId)
        {
            string sql = @"
UPDATE IPDirectory
SET
    IsDeleted = 'Y',
    ModifiedBy = @ModifiedBy,
    ModifiedOn = GETDATE()
WHERE Name = @Name
AND DepartmentName = @DepartmentName
AND IPPhone = @IPPhone";

            using var cmd =
                new SqlCommand(
                    sql,
                    conn,
                    tran);

            cmd.Parameters.AddWithValue(
                "@Name",
                name);
            cmd.Parameters.AddWithValue("@IPPhone", ip);
            cmd.Parameters.AddWithValue(
                "@DepartmentName",
                department);

            cmd.Parameters.AddWithValue(
                "@ModifiedBy",
                userId);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateImportRecordAsync(
    SqlConnection conn,
    SqlTransaction tran,
    string name,
    string department,
    string ip,
    string userId)
        {
            string sql = @"
UPDATE IPDirectory
SET
    IPPhone = @IPPhone,
    ModifiedBy = @ModifiedBy,
    ModifiedOn = GETDATE(),
    IsDeleted = 'N'
WHERE Name = @Name
AND DepartmentName = @DepartmentName";

            using var cmd =
                new SqlCommand(
                    sql,
                    conn,
                    tran);

            cmd.Parameters.AddWithValue(
                "@Name",
                name);

            cmd.Parameters.AddWithValue(
                "@DepartmentName",
                department);

            cmd.Parameters.AddWithValue(
                "@IPPhone",
                ip);

            cmd.Parameters.AddWithValue(
                "@ModifiedBy",
                userId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<byte[]>
    ExportIPDirectoryExcelAsync()
        {
            using var package = new ExcelPackage();

            foreach (var sheet in RequiredSheets)
            {
                var ws =
                    package.Workbook.Worksheets.Add(sheet);

                if (sheet == "Zonal Office" ||
                    sheet == "Regional Office")
                {
                    ws.Cells[1, 1].Value =
                        sheet == "Zonal Office"
                            ? "Zone"
                            : "Region";

                    ws.Cells[1, 2].Value = "Name";
                    ws.Cells[1, 3].Value = "IP Number";
                }
                else
                {
                    ws.Cells[1, 1].Value = "Name";
                    ws.Cells[1, 2].Value = "IP Number";
                }
            }

            string sql = @"
SELECT
    Name,
    DepartmentName,
    IPPhone
FROM IPDirectory
WHERE ISNULL(IsDeleted,'N')='N'";


            var dt =
                await _db.ExecuteQueryWithParamsAsync(
                    sql,
                    Array.Empty<SqlParameter>(),
                    ConnName);

            var sheetMap =
                package.Workbook.Worksheets
                    .ToDictionary(
                        x => x.Name,
                        x => 2);

            foreach (DataRow row in dt.Rows)
            {
                string department =
                    row["DepartmentName"]?.ToString()?.Trim() ?? "";

                string name =
                    row["Name"]?.ToString()?.Trim() ?? "";

                string ip =
                    row["IPPhone"]?.ToString()?.Trim() ?? "";

                string targetSheet =
                    department == "Training Centres"
                        ? "STC"
                        : department;

                if (!sheetMap.ContainsKey(targetSheet))
                    continue;

                var ws =
                    package.Workbook.Worksheets[targetSheet];

                int excelRow =
                    sheetMap[targetSheet]++;

                if (targetSheet == "Zonal Office")
                {
                    ws.Cells[excelRow, 1].Value = "";
                    ws.Cells[excelRow, 2].Value = name;
                    ws.Cells[excelRow, 3].Value = ip;
                }
                else if (targetSheet == "Regional Office")
                {
                    ws.Cells[excelRow, 1].Value = "";
                    ws.Cells[excelRow, 2].Value = name;
                    ws.Cells[excelRow, 3].Value = ip;
                }
                else
                {
                    ws.Cells[excelRow, 1].Value = name;
                    ws.Cells[excelRow, 2].Value = ip;
                }
            }

            foreach (var ws in package.Workbook.Worksheets)
            {
                if (ws.Dimension != null)
                {
                    ws.Cells[
                        ws.Dimension.Address]
                        .AutoFitColumns();
                }
            }

            return package.GetAsByteArray();
        }

        private async Task BulkInsertAsync(
    SqlConnection conn,
    SqlTransaction tran,
    DataTable table)
        {
            using var bulkCopy =
                new SqlBulkCopy(
                    conn,
                    SqlBulkCopyOptions.Default,
                    tran);

            bulkCopy.DestinationTableName =
                "IPDirectory";

            bulkCopy.BatchSize = 5000;

            bulkCopy.BulkCopyTimeout = 600;

            bulkCopy.ColumnMappings.Add("Name", "Name");
            bulkCopy.ColumnMappings.Add("DepartmentName", "DepartmentName");
            bulkCopy.ColumnMappings.Add("IPPhone", "IPPhone");
            bulkCopy.ColumnMappings.Add("CreatedBy", "CreatedBy");
            bulkCopy.ColumnMappings.Add("CreatedOn", "CreatedOn");
            bulkCopy.ColumnMappings.Add("ModifiedBy", "ModifiedBy");
            bulkCopy.ColumnMappings.Add("ModifiedOn", "ModifiedOn");
            bulkCopy.ColumnMappings.Add("IsDeleted", "IsDeleted");

            await bulkCopy.WriteToServerAsync(table);
        }

        private async Task CreateBackupAsync(
     SqlConnection conn,
     SqlTransaction tran,
     string backupTable)
        {
            string sql = $@"
SELECT *
INTO {backupTable}
FROM IPDirectory";

            using var cmd = new SqlCommand(sql, conn, tran);

            await cmd.ExecuteNonQueryAsync();
        }




        public async Task<IPDirectoryResult> GetDirectoryAsync(string dept, string search, int page, int pageSize)
        {
            var result = new IPDirectoryResult();

            try
            {

            // Guard page args
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

                // Base filter: exclude null/empty IPs (trimmed)
                // NOTE: This starts with "WHERE 1=1 ..." and will be appended AFTER CROSS APPLY.
                var filter = new StringBuilder(
        @" WHERE 1=1
       AND ISNULL(IsDeleted,'N') = 'N'
       AND IPPhone IS NOT NULL
       AND LTRIM(RTRIM(IPPhone)) <> ''"
    );

                var countParams = new List<SqlParameter>();
            var dataParams = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(dept))
            {
                filter.Append(" AND DepartmentName = @Dept");
                countParams.Add(new SqlParameter("@Dept", dept));
                dataParams.Add(new SqlParameter("@Dept", dept));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                // NOTE: Search remains plain LIKE; we can normalize it similarly if you'd like.
                var like = $"%{search}%";
                filter.Append(" AND (Name LIKE @Search OR IPPhone LIKE @Search OR DepartmentName LIKE @Search)");
                countParams.Add(new SqlParameter("@Search", like));
                dataParams.Add(new SqlParameter("@Search", like));
            }

            // 1) Count
            var countSql = $"SELECT COUNT(*) FROM IPDirectory {filter}";
            DataTable countDt = await _db.ExecuteQueryWithParamsAsync(countSql, countParams.ToArray(), ConnName);
            result.TotalRecords = (countDt.Rows.Count > 0) ? Convert.ToInt32(countDt.Rows[0][0]) : 0;

            // 2) Data with normalized sorting:
            // Correct order: FROM ... CROSS APPLY ... WHERE ... ORDER BY ... OFFSET/FETCH
            var dataSql = $@"
SELECT Name, DepartmentName, IPPhone
FROM IPDirectory
CROSS APPLY (
    SELECT
        DeptSort = UPPER(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(LTRIM(RTRIM(DepartmentName)), ' ', ''),  -- regular spaces
                            CHAR(160), ''),                                   -- NBSP
                        CHAR(9), ''),                                         -- TAB
                    CHAR(13), ''),                                            -- CR
                CHAR(10), '')                                                 -- LF
        ),
        NameSort = UPPER(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(LTRIM(RTRIM(Name)), ' ', ''),
                            CHAR(160), ''),
                        CHAR(9), ''),
                    CHAR(13), ''),
                CHAR(10), '')
        ),
        IpSort = UPPER(LTRIM(RTRIM(IPPhone)))
) S
{filter}
ORDER BY
    -- Department: NULL/empty last
    CASE WHEN DepartmentName IS NULL OR LTRIM(RTRIM(DepartmentName)) = '' THEN 1 ELSE 0 END,
    DeptSort,
    -- Name: NULL/empty last
    CASE WHEN Name IS NULL OR LTRIM(RTRIM(Name)) = '' THEN 1 ELSE 0 END,
    NameSort,
    -- Stable tiebreaker(s)
    IpSort
OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;";

            dataParams.Add(new SqlParameter("@Offset", (page - 1) * pageSize));
            dataParams.Add(new SqlParameter("@Limit", pageSize));

            DataTable dataDt = await _db.ExecuteQueryWithParamsAsync(dataSql, dataParams.ToArray(), ConnName);

            foreach (DataRow row in dataDt.Rows)
            {
                result.Entries.Add(new IPDirectoryEntry
                {
                    Name = row["Name"]?.ToString()?.Trim() ?? string.Empty,
                    DepartmentName = row["DepartmentName"]?.ToString()?.Trim() ?? string.Empty,
                    IPPhone = row["IPPhone"]?.ToString()?.Trim() ?? string.Empty
                });
            }

            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[IPDirectoryService] GetDirectoryAsync failed: " + ex.Message);
            }

            return result;
        }

        public async Task<bool> AddDirectoryEntryAsync(
     IPDirectoryEntry entry,
     string userId)
        {
            try
            {
                string sql = @"
INSERT INTO IPDirectory
(
    Name,
    DepartmentName,
    IPPhone,
    CreatedBy,
    CreatedOn,
    ModifiedBy,
    ModifiedOn,
    IsDeleted
)
VALUES
(
    @Name,
    @DepartmentName,
    @IPPhone,
    @CreatedBy,
    GETDATE(),
    @ModifiedBy,
    GETDATE(),
    'N'
)";

                await _db.ExecuteNonQueryAsync(
                    sql,
                    new[]
                    {
                new SqlParameter("@Name", entry.Name),
new SqlParameter("@DepartmentName", entry.DepartmentName),
new SqlParameter("@IPPhone", entry.IPPhone),
new SqlParameter("@CreatedBy", userId),
new SqlParameter("@ModifiedBy", userId)
                    },
                    ConnName);

                return true;
            }
            catch
            {
                return false;
            }
        }

    public async Task<bool> DeleteDirectoryEntryAsync(
string name,
string userId)
    {
        try
        {
            string sql = @"
UPDATE IPDirectory
SET
    IsDeleted = 'Y',
    ModifiedBy = @ModifiedBy,
    ModifiedOn = GETDATE()
WHERE Name = @Name";

            await _db.ExecuteNonQueryAsync(
                sql,
                new[]
                {
                new SqlParameter("@Name", name),
                new SqlParameter("@ModifiedBy", userId)
                },
                ConnName);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateDirectoryEntryAsync(
    IPDirectoryEntry entry,
    string userId)
        {
            try
            {
                string sql = @"
UPDATE IPDirectory
SET
    DepartmentName=@DepartmentName,
    IPPhone=@IPPhone,
    ModifiedBy=@ModifiedBy,
    ModifiedOn=GETDATE()
WHERE Name=@Name";

                await _db.ExecuteNonQueryAsync(
                    sql,
                    new[]
                    {
                new SqlParameter("@Name", entry.Name),
new SqlParameter("@DepartmentName", entry.DepartmentName),
new SqlParameter("@IPPhone", entry.IPPhone),
new SqlParameter("@ModifiedBy", userId)
                    },
                    ConnName);

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}