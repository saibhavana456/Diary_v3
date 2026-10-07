using Microsoft.Data.SqlClient;
using MyDiary.Core.Services;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.Reports.Models;
using OfficeOpenXml;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;

namespace MyDiary.Web.Features.Reports.Services
{
    public class RFAService : IRFAService
    {
        private readonly IConfiguration _config;
        //private const string ConnName = "BranchDiaryConnection";
        private const string ConnName = "SQLServerConnection";

        private readonly IStaticAssetPathResolver _assets;

        public RFAService(IConfiguration config, IStaticAssetPathResolver assets)
        {
            _config = config;
            _assets = assets;
        }

        private string GetConnectionString()
            => _config.GetConnectionString(ConnName)!;

        // ───────────────────────────────────────────────
        //  GET UPLOAD DATES (for dropdown)
        // ───────────────────────────────────────────────

        public async Task<List<RFAUploadDate>> GetUploadDatesAsync()
        {
            var list = new List<RFAUploadDate>();

            try
            {
                using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_RFA_GetUploadDates", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new RFAUploadDate
                    {
                        UploadId = reader.GetInt64(reader.GetOrdinal("UploadId")),
                        PositionAsOfDate = reader.GetDateTime(reader.GetOrdinal("PositionAsOfDate"))
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[RFAService] GetUploadDatesAsync failed");
            }

            return list;
        }

        // ───────────────────────────────────────────────
        //  GET list of CCM users for canManage
        // ───────────────────────────────────────────────
        public async Task<bool> HasManageAccessAsync(string employeeId)
        {
            using var conn = new SqlConnection(GetConnectionString());

            await conn.OpenAsync();

            using var cmd = new SqlCommand(
                @"SELECT COUNT(*) FROM RFAAccessControl WHERE EmployeeId = @EmployeeId AND IsActive = 1", conn);

            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);

            return Convert.ToInt32(
                await cmd.ExecuteScalarAsync()) > 0;
        }


        // ───────────────────────────────────────────────
        //  GET RECORDS (paginated, filtered)
        // ───────────────────────────────────────────────

        public async Task<RFAListResult> GetRecordsAsync(
            DateTime positionDate,
            string? search,
            string? zonalOffice,
            string? regionalOffice,
            int page,
            int pageSize)
        {
            var result = new RFAListResult();

            try
            {
                page = Math.Max(page, 1);
                pageSize = Math.Clamp(pageSize, 1, 200);

                using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_RFA_GetRecords", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@PositionDate", positionDate);
                cmd.Parameters.AddWithValue("@ZonalOffice", (object?)zonalOffice ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RegionalOffice", (object?)regionalOffice ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                using var reader = await cmd.ExecuteReaderAsync();

                // Result set 1: total count
                if (await reader.ReadAsync())
                {
                    result.TotalRecords = reader.GetInt32(reader.GetOrdinal("TotalRecords"));
                }

                // Result set 2: paged records
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Records.Add(MapRecordFromReader(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[RFAService] GetRecordsAsync failed");
            }

            return result;
        }

        // ───────────────────────────────────────────────
        //  IMPORT — ADD NEW
        // ───────────────────────────────────────────────

        public async Task<RFAImportResult> ImportAddNewAsync(
            Stream stream,
            DateTime positionAsOfDate,
            string userId)
        {
            SqlConnection? conn = null;
            SqlTransaction? tran = null;

            try
            {
                // Check if date already exists
                bool dateExists = false;

                using (var checkConn = new SqlConnection(GetConnectionString()))
                {
                    await checkConn.OpenAsync();

                    using var checkCmd = new SqlCommand("SP_RFA_CheckDateExists", checkConn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    checkCmd.Parameters.AddWithValue("@PositionDate", positionAsOfDate);

                    var checkResult = await checkCmd.ExecuteScalarAsync();
                    dateExists = Convert.ToInt32(checkResult) > 0;
                }

                if (dateExists)
                {
                    return new RFAImportResult
                    {
                        Success = false,
                        Message = $"Data for {positionAsOfDate:dd-MM-yyyy} already exists. " +
                                  "Please use 'Modify Existing' to update."
                    };
                }


                using var fileMemory = new MemoryStream();
                await stream.CopyToAsync(fileMemory);
                byte[] uploadedFileBytes = fileMemory.ToArray();
                fileMemory.Position = 0;
                using var package = new ExcelPackage(fileMemory);

                //using var package = new ExcelPackage(stream);
                var ws = package.Workbook.Worksheets.FirstOrDefault();

                if (ws?.Dimension == null)
                {
                    return new RFAImportResult
                    {
                        Success = false,
                        Message = "Excel file is empty or has no data."
                    };
                }

                conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();
                tran = conn.BeginTransaction();

                // Create upload master entry via SP
                long uploadId;
                using (var masterCmd = new SqlCommand("SP_RFA_InsertUploadMaster", conn, tran)
                {
                    CommandType = CommandType.StoredProcedure
                })
                {
                    masterCmd.Parameters.AddWithValue("@PositionDate", positionAsOfDate);
                    masterCmd.Parameters.AddWithValue("@UploadedBy", userId);
                    masterCmd.Parameters.AddWithValue("@UploadType", "AddNew");
                    masterCmd.Parameters.AddWithValue("@FileName", ws.Name);

                    masterCmd.Parameters.AddWithValue("@OriginalFileName",$"RFA_{positionAsOfDate:yyyyMMdd}.xlsx");
                    masterCmd.Parameters.AddWithValue("@ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    masterCmd.Parameters.AddWithValue("@FileSize",uploadedFileBytes.Length);
                    masterCmd.Parameters.AddWithValue("@FileContent",uploadedFileBytes);

                    var scalarResult = await masterCmd.ExecuteScalarAsync();
                    uploadId = Convert.ToInt64(scalarResult);
                }

                // Parse rows and bulk insert
                var importTable = BuildImportDataTable();
                int inserted = 0;
                int skipped = 0;
                int rowCount = ws.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    var pan = ws.Cells[row, 7].Text.Trim();

                    if (string.IsNullOrWhiteSpace(pan))
                    {
                        skipped++;
                        continue;
                    }

                    importTable.Rows.Add(
                        uploadId,
                        positionAsOfDate,
                        ws.Cells[row, 1].Text.Trim(),   // Zonal Office
                        ws.Cells[row, 2].Text.Trim(),   // Regional Office
                        ws.Cells[row, 3].Text.Trim(),   // Branch Name
                        ws.Cells[row, 4].Text.Trim(),   // Branch SOL
                        ws.Cells[row, 5].Text.Trim(),   // Name of Borrower
                        ws.Cells[row, 6].Text.Trim(),   // CIF ID
                        pan,                              // PAN
                        ws.Cells[row, 8].Text.Trim(),   // Name of Bank
                        ParseDate(ws.Cells[row, 9].Text),// Date of RFA Classification
                        ws.Cells[row, 10].Text.Trim(),  // Remarks
                        true,                             // IsActive
                        userId,
                        AppTime.Now,
                        userId,
                        AppTime.Now);

                    inserted++;
                }

                if (importTable.Rows.Count > 0)
                {
                    await BulkInsertRecordsAsync(conn, tran, importTable);
                }

                await tran.CommitAsync();

                return new RFAImportResult
                {
                    Success = true,
                    Message = $"Upload completed successfully.\n\n" +
                              $"Position Date : {positionAsOfDate:dd-MM-yyyy}\n" +
                              $"Inserted      : {inserted}\n" +
                              $"Skipped       : {skipped}",
                    Inserted = inserted,
                    Skipped = skipped
                };
            }
            catch (Exception ex)
            {
                if (tran != null) await tran.RollbackAsync();
                AppLogger.LogError(ex, "ImportAddNewAsync");

                return new RFAImportResult
                {
                    Success = false,
                    Message = "Import failed and all changes were rolled back. " + UserFacingError.Generic
                };
            }
            finally
            {
                if (conn != null) await conn.DisposeAsync();
            }
        }

        // ───────────────────────────────────────────────
        //  IMPORT — MODIFY EXISTING
        // ───────────────────────────────────────────────

        public async Task<RFAImportResult> ImportModifyExistingAsync(
            Stream stream,
            DateTime positionAsOfDate,
            string userId)
        {
            SqlConnection? conn = null;
            SqlTransaction? tran = null;

            try
            {

                using var fileMemory = new MemoryStream();
                await stream.CopyToAsync(fileMemory);
                byte[] uploadedFileBytes = fileMemory.ToArray();
                fileMemory.Position = 0;
                using var package = new ExcelPackage(fileMemory);

                //using var package = new ExcelPackage(stream);
                var ws = package.Workbook.Worksheets.FirstOrDefault();

                if (ws?.Dimension == null)
                {
                    return new RFAImportResult
                    {
                        Success = false,
                        Message = "Excel file is empty or has no data."
                    };
                }

                conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();
                tran = conn.BeginTransaction();

                // Get existing records for this date via SP
                var existingByPan = new Dictionary<string, DataRow>(
                    StringComparer.OrdinalIgnoreCase);

                using (var existCmd = new SqlCommand("SP_RFA_GetExistingRecordsByDate", conn, tran)
                {
                    CommandType = CommandType.StoredProcedure
                })
                {
                    existCmd.Parameters.AddWithValue("@PositionDate", positionAsOfDate);

                    using var adapter = new SqlDataAdapter(existCmd);
                    var existDt = new DataTable();
                    adapter.Fill(existDt);

                    foreach (DataRow r in existDt.Rows)
                    {
                        var pan = r["PAN"]?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(pan))
                            existingByPan[pan] = r;
                    }
                }

                // Create upload master entry via SP
                long uploadId;
                using (var masterCmd = new SqlCommand("SP_RFA_InsertUploadMaster", conn, tran)
                {
                    CommandType = CommandType.StoredProcedure
                })
                {
                    masterCmd.Parameters.AddWithValue("@PositionDate", positionAsOfDate);
                    masterCmd.Parameters.AddWithValue("@UploadedBy", userId);
                    masterCmd.Parameters.AddWithValue("@UploadType", "Modify");
                    masterCmd.Parameters.AddWithValue("@FileName", ws.Name);

                    masterCmd.Parameters.AddWithValue("@OriginalFileName", $"RFA_{positionAsOfDate:yyyyMMdd}.xlsx");
                    masterCmd.Parameters.AddWithValue("@ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                    masterCmd.Parameters.AddWithValue("@FileSize", uploadedFileBytes.Length);
                    masterCmd.Parameters.AddWithValue("@FileContent", uploadedFileBytes);


                    var scalarResult = await masterCmd.ExecuteScalarAsync();
                    uploadId = Convert.ToInt64(scalarResult);
                }

                int inserted = 0;
                int updated = 0;
                int skipped = 0;
                int rowCount = ws.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    var pan = ws.Cells[row, 7].Text.Trim();

                    if (string.IsNullOrWhiteSpace(pan))
                    {
                        skipped++;
                        continue;
                    }

                    var zonalOffice = ws.Cells[row, 1].Text.Trim();
                    var regionalOffice = ws.Cells[row, 2].Text.Trim();
                    var branchName = ws.Cells[row, 3].Text.Trim();
                    var branchSOL = ws.Cells[row, 4].Text.Trim();
                    var borrowerName = ws.Cells[row, 5].Text.Trim();
                    var cifId = ws.Cells[row, 6].Text.Trim();
                    var bankName = ws.Cells[row, 8].Text.Trim();
                    var rfaDate = ParseDate(ws.Cells[row, 9].Text);
                    var remarks = ws.Cells[row, 10].Text.Trim();

                    if (existingByPan.TryGetValue(pan, out var existing))
                    {
                        long sno = Convert.ToInt64(existing["SNo"]);

                        // Audit trail for each changed field via SP
                        await AuditFieldIfChanged(conn, tran, sno,
                            "ZonalOffice", existing["ZonalOffice"]?.ToString(), zonalOffice, userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "RegionalOffice", existing["RegionalOffice"]?.ToString(), regionalOffice, userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "BranchName", existing["BranchName"]?.ToString(), branchName, userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "BranchSOL", existing["BranchSOL"]?.ToString(), branchSOL, userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "NameOfBorrower", existing["NameOfBorrower"]?.ToString(), borrowerName, userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "CIFID", existing["CIFID"]?.ToString(), cifId, userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "NameOfBank", existing["NameOfBank"]?.ToString(), bankName, userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "DateOfRFAClassification",
                            existing["DateOfRFAClassification"] == DBNull.Value
                                ? "" : Convert.ToDateTime(existing["DateOfRFAClassification"]).ToString("dd-MM-yyyy"),
                            rfaDate.HasValue ? rfaDate.Value.ToString("dd-MM-yyyy") : "",
                            userId);
                        await AuditFieldIfChanged(conn, tran, sno,
                            "Remarks", existing["Remarks"]?.ToString(), remarks, userId);

                        // Update the record via SP
                        await ExecuteSpNonQuery(conn, tran, "SP_RFA_UpdateRecord",
                            new SqlParameter("@SNo", sno),
                            new SqlParameter("@ZonalOffice", (object?)zonalOffice ?? DBNull.Value),
                            new SqlParameter("@RegionalOffice", (object?)regionalOffice ?? DBNull.Value),
                            new SqlParameter("@BranchName", (object?)branchName ?? DBNull.Value),
                            new SqlParameter("@BranchSOL", (object?)branchSOL ?? DBNull.Value),
                            new SqlParameter("@NameOfBorrower", (object?)borrowerName ?? DBNull.Value),
                            new SqlParameter("@CIFID", (object?)cifId ?? DBNull.Value),
                            new SqlParameter("@PAN", (object?)pan ?? DBNull.Value),
                            new SqlParameter("@NameOfBank", (object?)bankName ?? DBNull.Value),
                            new SqlParameter("@DateOfRFAClassification", (object?)rfaDate ?? DBNull.Value),
                            new SqlParameter("@Remarks", (object?)remarks ?? DBNull.Value),
                            new SqlParameter("@ModifiedBy", userId));

                        updated++;
                    }
                    else
                    {
                        // Insert new record via SP
                        await ExecuteSpNonQuery(conn, tran, "SP_RFA_InsertRecord",
                            new SqlParameter("@UploadId", uploadId),
                            new SqlParameter("@PositionDate", positionAsOfDate),
                            new SqlParameter("@ZonalOffice", (object?)zonalOffice ?? DBNull.Value),
                            new SqlParameter("@RegionalOffice", (object?)regionalOffice ?? DBNull.Value),
                            new SqlParameter("@BranchName", (object?)branchName ?? DBNull.Value),
                            new SqlParameter("@BranchSOL", (object?)branchSOL ?? DBNull.Value),
                            new SqlParameter("@NameOfBorrower", (object?)borrowerName ?? DBNull.Value),
                            new SqlParameter("@CIFID", (object?)cifId ?? DBNull.Value),
                            new SqlParameter("@PAN", pan),
                            new SqlParameter("@NameOfBank", (object?)bankName ?? DBNull.Value),
                            new SqlParameter("@DateOfRFAClassification", (object?)rfaDate ?? DBNull.Value),
                            new SqlParameter("@Remarks", (object?)remarks ?? DBNull.Value),
                            new SqlParameter("@CreatedBy", userId));

                        inserted++;
                    }
                }

                await tran.CommitAsync();

                return new RFAImportResult
                {
                    Success = true,
                    Message = $"Modification completed successfully.\n\n" +
                              $"Position Date : {positionAsOfDate:dd-MM-yyyy}\n" +
                              $"Inserted      : {inserted}\n" +
                              $"Updated       : {updated}\n" +
                              $"Skipped       : {skipped}",
                    Inserted = inserted,
                    Updated = updated,
                    Skipped = skipped
                };
            }
            catch (Exception ex)
            {
                if (tran != null) await tran.RollbackAsync();
                AppLogger.LogError(ex, "ImportModifyExistingAsync");

                return new RFAImportResult
                {
                    Success = false,
                    Message = "Import failed and all changes were rolled back. " + UserFacingError.Generic
                };
            }
            finally
            {
                if (conn != null) await conn.DisposeAsync();
            }
        }

        // ───────────────────────────────────────────────
        //  Getting — Uploaded Excel History
        // ───────────────────────────────────────────────
        public async Task<List<RFAUploadHistoryModel>> GetUploadHistoryAsync(DateTime? positionDate)
        {
            var list = new List<RFAUploadHistoryModel>();
            using var conn = new SqlConnection(GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new SqlCommand( "SP_RFA_GetUploadHistory", conn);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@PositionDate", (object?)positionDate ?? DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RFAUploadHistoryModel
                {
                    UploadId = Convert.ToInt64(reader["UploadId"]),
                    PositionAsOfDate = Convert.ToDateTime(reader["PositionAsOfDate"]),
                    VersionNo = reader["VersionNo"] == DBNull.Value? 1: Convert.ToInt32(reader["VersionNo"]),
                    UploadedBy = reader["UploadedBy"] .ToString() ?? "",
                    UploadedOn = Convert.ToDateTime(reader["UploadedOn"]),
                    UploadType = reader["UploadType"].ToString() ?? "",
                    FileName = reader["OriginalFileName"] == DBNull.Value? "": reader["OriginalFileName"].ToString() ?? "",
                    FileSize = reader["FileSize"] == DBNull.Value? 0 : Convert.ToInt64(reader["FileSize"])
                });
            }
            return list;
        }

        // ───────────────────────────────────────────────
        //  Download Old Version Excel
        // ───────────────────────────────────────────────
        public async Task<(byte[] FileBytes, string FileName, string ContentType)> GetUploadVersionAsync(long uploadId)
        {
            using var conn = new SqlConnection(GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SP_RFA_GetUploadFile",conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@UploadId",uploadId);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return
                (
                    (byte[])reader["FileContent"],reader["OriginalFileName"].ToString()!, reader["ContentType"].ToString()!
                );
            }
            throw new Exception("File not found");
        }

        // ───────────────────────────────────────────────
        //  UPDATE SINGLE RECORD (inline edit with audit)
        // ───────────────────────────────────────────────

        public async Task<bool> UpdateRecordAsync(
            RFAEditModel model,
            string userId)
        {
            try
            {
                using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                // Fetch old values for audit via SP
                DataTable oldDt;
                using (var auditCmd = new SqlCommand("SP_RFA_GetRecordForAudit", conn)
                {
                    CommandType = CommandType.StoredProcedure
                })
                {
                    auditCmd.Parameters.AddWithValue("@SNo", model.SNo);

                    using var adapter = new SqlDataAdapter(auditCmd);
                    oldDt = new DataTable();
                    adapter.Fill(oldDt);
                }

                if (oldDt.Rows.Count == 0) return false;

                var old = oldDt.Rows[0];

                // Build audit entries
                var auditEntries = new List<(string Field, string? OldVal, string? NewVal)>
                {
                    ("ZonalOffice", old["ZonalOffice"]?.ToString(), model.ZonalOffice),
                    ("RegionalOffice", old["RegionalOffice"]?.ToString(), model.RegionalOffice),
                    ("BranchName", old["BranchName"]?.ToString(), model.BranchName),
                    ("BranchSOL", old["BranchSOL"]?.ToString(), model.BranchSOL),
                    ("NameOfBorrower", old["NameOfBorrower"]?.ToString(), model.NameOfBorrower),
                    ("CIFID", old["CIFID"]?.ToString(), model.CIFID),
                    ("PAN", old["PAN"]?.ToString(), model.PAN),
                    ("NameOfBank", old["NameOfBank"]?.ToString(), model.NameOfBank),
                    ("DateOfRFAClassification",
                        old["DateOfRFAClassification"] == DBNull.Value
                            ? "" : Convert.ToDateTime(old["DateOfRFAClassification"]).ToString("dd-MM-yyyy"),
                        model.DateOfRFAClassification?.ToString("dd-MM-yyyy") ?? ""),
                    ("Remarks", old["Remarks"]?.ToString(), model.Remarks)
                };

                // Insert audit logs for changed fields via SP
                foreach (var (field, oldVal, newVal) in auditEntries)
                {
                    if (!string.Equals(oldVal?.Trim(), newVal?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        using var logCmd = new SqlCommand("SP_RFA_InsertAuditLog", conn)
                        {
                            CommandType = CommandType.StoredProcedure
                        };
                        logCmd.Parameters.AddWithValue("@SNo", model.SNo);
                        logCmd.Parameters.AddWithValue("@FieldName", field);
                        logCmd.Parameters.AddWithValue("@OldValue", (object?)oldVal ?? DBNull.Value);
                        logCmd.Parameters.AddWithValue("@NewValue", (object?)newVal ?? DBNull.Value);
                        logCmd.Parameters.AddWithValue("@ModifiedBy", userId);
                        await logCmd.ExecuteNonQueryAsync();
                    }
                }

                // Update the record via SP
                using var updateCmd = new SqlCommand("SP_RFA_UpdateRecord", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                updateCmd.Parameters.AddWithValue("@SNo", model.SNo);
                updateCmd.Parameters.AddWithValue("@ZonalOffice", (object?)model.ZonalOffice ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@RegionalOffice", (object?)model.RegionalOffice ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@BranchName", (object?)model.BranchName ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@BranchSOL", (object?)model.BranchSOL ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@NameOfBorrower", (object?)model.NameOfBorrower ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@CIFID", (object?)model.CIFID ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@PAN", (object?)model.PAN ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@NameOfBank", (object?)model.NameOfBank ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@DateOfRFAClassification", (object?)model.DateOfRFAClassification ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@Remarks", (object?)model.Remarks ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@ModifiedBy", userId);
                await updateCmd.ExecuteNonQueryAsync();

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UpdateRecordAsync");
                return false;
            }
        }

        // ───────────────────────────────────────────────
        //  DELETE (soft)
        // ───────────────────────────────────────────────

        public async Task<bool> DeleteRecordAsync(long sno, string userId)
        {
            try
            {
                using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_RFA_SoftDeleteRecord", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@SNo", sno);
                cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                await cmd.ExecuteNonQueryAsync();

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "DeleteRecordAsync");
                return false;
            }
        }

        // ───────────────────────────────────────────────
        //  DELETE ALL
        // ───────────────────────────────────────────────


        public async Task<bool> DeleteAllForDateAsync(DateTime positionDate, string userId)
        {
            try
            {
                using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SP_RFA_DeleteAllByDate", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@PositionDate", positionDate);
                await cmd.ExecuteNonQueryAsync();

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "DeleteAllForDateAsync");
                return false;
            }
        }

        // ───────────────────────────────────────────────
        //  EXPORT EXCEL
        // ───────────────────────────────────────────────

        public async Task<byte[]> ExportExcelAsync(DateTime positionDate,string? zonalOffice,string? regionalOffice,string? search)
        {
            var dt = await ExecuteSpDataTable("SP_RFA_GetRecordsForExport",
                new SqlParameter("@PositionDate", positionDate),
                new SqlParameter("@ZonalOffice",(object?)zonalOffice ?? DBNull.Value),
                new SqlParameter("@RegionalOffice",(object?)regionalOffice ?? DBNull.Value),
                new SqlParameter("@Search",(object?)search ?? DBNull.Value));

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("RFA Records");

            // Headers
            string[] headers = {
                "Zonal Office", "Regional Office", "Branch Name", "Branch SOL",
                "Name of Borrower", "CIF ID", "PAN", "Name of Bank",
                "Date of RFA Classification", "Remarks/Present Status", "Modified On"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType =
                    OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor
                    .SetColor(System.Drawing.Color.FromArgb(30, 58, 138));
                ws.Cells[1, i + 1].Style.Font.Color
                    .SetColor(System.Drawing.Color.White);
            }

            int row = 2;
            foreach (DataRow dr in dt.Rows)
            {
                ws.Cells[row, 1].Value = dr["ZonalOffice"]?.ToString();
                ws.Cells[row, 2].Value = dr["RegionalOffice"]?.ToString();
                ws.Cells[row, 3].Value = dr["BranchName"]?.ToString();
                ws.Cells[row, 4].Value = dr["BranchSOL"]?.ToString();
                ws.Cells[row, 5].Value = dr["NameOfBorrower"]?.ToString();
                ws.Cells[row, 6].Value = dr["CIFID"]?.ToString();
                ws.Cells[row, 7].Value = dr["PAN"]?.ToString();
                ws.Cells[row, 8].Value = dr["NameOfBank"]?.ToString();

                if (dr["DateOfRFAClassification"] != DBNull.Value)
                    ws.Cells[row, 9].Value = Convert.ToDateTime(dr["DateOfRFAClassification"]).ToString("dd-MM-yyyy");

                ws.Cells[row, 10].Value = dr["Remarks"]?.ToString();

                if (dr["ModifiedDate"] != DBNull.Value)
                    ws.Cells[row, 11].Value = Convert.ToDateTime(dr["ModifiedDate"]).ToString("dd-MM-yyyy HH:mm");

                row++;
            }

            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }

        // ───────────────────────────────────────────────
        //  EXPORT PDF (with watermark)
        // ───────────────────────────────────────────────

        public async Task<byte[]> ExportPdfAsync(DateTime positionDate,string? zonalOffice, string? regionalOffice, string? search, string userPfNumber)
        {
            var dt = await ExecuteSpDataTable("SP_RFA_GetRecordsForExport",
                new SqlParameter("@PositionDate", positionDate),
                new SqlParameter("@ZonalOffice", (object?)zonalOffice ?? DBNull.Value),
                new SqlParameter("@RegionalOffice",(object?)regionalOffice ?? DBNull.Value),
                new SqlParameter("@Search",(object?)search ?? DBNull.Value));

            DateTime? initialUploadOn = null;
            if (dt.Rows.Count > 0 && dt.Rows[0]["InitialUploadOn"] != DBNull.Value)
            {
                initialUploadOn = Convert.ToDateTime(dt.Rows[0]["InitialUploadOn"]);
            }

            var now = AppTime.Now;
            var watermarkText =
                $"PF No: {userPfNumber} | Date: {now:dd-MM-yyyy} | " +
                $"Time: {now:HH:mm:ss} | My Diary Portal | " +
                "Confidentiality Clause";
            var diagonalWatermark =
                $"PF: {userPfNumber}\n" +
                $"My Diary Portal\n" +
                $"{now:dd-MM-yyyy} | {now:HH:mm:ss}";

            // Resolve from the NFS-aware assets root (wwwroot/MyDiary/images under NFS,
            // wwwroot/images locally).
            var logoPath = _assets.Resolve("images/UBI_Logo.png");
            byte[] logoBytes = Array.Empty<byte>();
            if (File.Exists(logoPath))
                logoBytes = await File.ReadAllBytesAsync(logoPath);


            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);

                    // Code for Diagonal Watermark 
                    page.Foreground()
                     .PaddingTop(120)
                     .AlignCenter()
                     .AlignMiddle()
                     .Rotate(-35)
                     .Text(diagonalWatermark)
                     .FontSize(22)
                     .FontColor("#D6D6D6");


                    page.DefaultTextStyle(x => x.FontSize(8));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem(2);

                            var logoCell = row.RelativeItem(5).AlignCenter().Height(35);
                            if (logoBytes.Length > 0)
                            {
                                logoCell.Image(logoBytes);
                            }
                            else
                            {
                                logoCell.AlignMiddle().Text("Union Bank of India").FontSize(14).Bold().FontColor("#C00000");
                            }


                            row.RelativeItem(2);
                        });

                        col.Item().PaddingTop(5).AlignCenter().Text(
                            $"RFA by Other/Our Bank — Position as of {positionDate:dd-MM-yyyy} , Uploaded on {initialUploadOn:dd-MM-yyyy HH:mm}")
                            .FontSize(12).Bold();

                        col.Item().PaddingTop(5).AlignCenter().Text(
                            "Classification: Internal").FontSize(7).Italic();

                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#1E3A8A");
                    });

                    // Content table
                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);     // #
                            columns.RelativeColumn(2);     // Zonal Office
                            columns.RelativeColumn(2);     // Regional Office
                            columns.RelativeColumn(2);     // Branch Name
                            columns.RelativeColumn(1.2f);  // Branch SOL
                            columns.RelativeColumn(2.5f);  // Borrower
                            columns.RelativeColumn(1.5f);  // CIF ID
                            columns.RelativeColumn(1.5f);  // PAN
                            columns.RelativeColumn(2);     // Bank
                            columns.RelativeColumn(1.5f);  // RFA Date
                            columns.RelativeColumn(2.5f);  // Remarks
                            columns.RelativeColumn(1.8f); // Last Modified
                        });

                        // Table header
                        table.Header(header =>
                        {
                            string[] cols = {
                                "S No.", "Zonal Office", "Regional Office",
                                "Branch Name", "Branch SOL", "Name of Borrower",
                                "CIF ID", "PAN", "Name of Bank",
                                "RFA Classification Date", "Remarks", "Modified On"
                            };

                            foreach (var h in cols)
                            {
                                header.Cell()
                                    .Background("#1E3A8A")
                                    .Padding(4)
                                    .Text(h)
                                    .FontColor("#FFFFFF")
                                    .FontSize(7)
                                    .Bold();
                            }
                        });

                        int sr = 1;
                        foreach (DataRow dr in dt.Rows)
                        {
                            var bgColor = sr % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

                            table.Cell().Background(bgColor).Padding(3)
                                .Text(sr.ToString()).FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["ZonalOffice"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["RegionalOffice"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["BranchName"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["BranchSOL"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["NameOfBorrower"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["CIFID"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["PAN"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["NameOfBank"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["DateOfRFAClassification"] == DBNull.Value
                                    ? ""
                                    : Convert.ToDateTime(dr["DateOfRFAClassification"])
                                        .ToString("dd-MM-yyyy")).FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["Remarks"]?.ToString() ?? "").FontSize(7);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(dr["ModifiedDate"] == DBNull.Value ? "": Convert.ToDateTime(dr["ModifiedDate"]).ToString("dd-MM-yyyy HH:mm")).FontSize(7);

                            sr++;
                        }
                    });

                    // Footer with watermark
                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingBottom(3).BorderTop(1).BorderColor("#E5E7EB")
                            .Row(footerRow =>
                            {
                                footerRow.RelativeItem().AlignLeft().Text(watermarkText)
                                    .FontSize(6).FontColor("#9CA3AF");
                                footerRow.RelativeItem().AlignRight().Text(text =>
                                {
                                    text.Span("Page ").FontSize(6).FontColor("#9CA3AF");
                                    text.CurrentPageNumber().FontSize(6).FontColor("#9CA3AF");
                                    text.Span(" of ").FontSize(6).FontColor("#9CA3AF");
                                    text.TotalPages().FontSize(6).FontColor("#9CA3AF");
                                });
                            });

                        col.Item().AlignCenter().Text(
                            "This is a confidential document. Do not Share.")
                            .FontSize(6).FontColor("#EF4444").Bold();
                    });
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }

        public async Task<List<RFARecordModel>> GetPdfDataAsync(DateTime positionDate, string? zonalOffice, string? regionalOffice, string? search)
        {
            var list = new List<RFARecordModel>();

            using var conn =
                new SqlConnection(GetConnectionString());

            await conn.OpenAsync();

            using var cmd =
                new SqlCommand(
                    "SP_RFA_GetRecordsForExport",
                    conn);

            cmd.CommandType =
                CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue(
                "@PositionDate",
                positionDate);

            cmd.Parameters.AddWithValue(
                "@ZonalOffice",
                (object?)zonalOffice ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@RegionalOffice",
                (object?)regionalOffice ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@Search",
                (object?)search ?? DBNull.Value);

            using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new RFARecordModel
                {
                    ZonalOffice =
                        reader["ZonalOffice"]?.ToString(),

                    RegionalOffice =
                        reader["RegionalOffice"]?.ToString(),

                    BranchName =
                        reader["BranchName"]?.ToString(),

                    BranchSOL =
                        reader["BranchSOL"]?.ToString(),

                    NameOfBorrower =
                        reader["NameOfBorrower"]?.ToString(),

                    CIFID =
                        reader["CIFID"]?.ToString(),

                    PAN =
                        reader["PAN"]?.ToString(),

                    NameOfBank =
                        reader["NameOfBank"]?.ToString(),

                    Remarks =
                        reader["Remarks"]?.ToString(),

                    DateOfRFAClassification =
                        reader["DateOfRFAClassification"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(
                                reader["DateOfRFAClassification"]),

                    ModifiedDate =
                        reader["ModifiedDate"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(
                                reader["ModifiedDate"])
                });
            }

            return list;
        }

        // ───────────────────────────────────────────────
        //  Download log
        // ───────────────────────────────────────────────
        public async Task LogDownloadAsync(DateTime positionDate,string downloadType,string userId,string? zone,string? region,string? search)
        {
            using var conn = new SqlConnection(GetConnectionString());

            await conn.OpenAsync();

            using var cmd = new SqlCommand(
                "SP_RFA_InsertDownloadAudit",
                conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@PositionAsOfDate", positionDate);
            cmd.Parameters.AddWithValue("@DownloadType", downloadType);
            cmd.Parameters.AddWithValue("@DownloadedBy", userId);
            cmd.Parameters.AddWithValue("@ZonalOffice",
                (object?)zone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RegionalOffice",
                (object?)region ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SearchText",
                (object?)search ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        // ───────────────────────────────────────────────
        //  FILTER DROPDOWNS
        // ───────────────────────────────────────────────

        public async Task<List<string>> GetZonalOfficesAsync(DateTime positionDate)
        {
            var dt = await ExecuteSpDataTable("SP_RFA_GetZonalOffices",
                new SqlParameter("@PositionDate", positionDate));

            return dt.AsEnumerable()
                .Select(r => r["ZonalOffice"].ToString()!.Trim())
                .ToList();
        }

        public async Task<List<string>> GetRegionalOfficesAsync(
            DateTime positionDate,
            string? zonalOffice)
        {
            var dt = await ExecuteSpDataTable("SP_RFA_GetRegionalOffices",
                new SqlParameter("@PositionDate", positionDate),
                new SqlParameter("@ZonalOffice", (object?)zonalOffice ?? DBNull.Value));

            return dt.AsEnumerable()
                .Select(r => r["RegionalOffice"].ToString()!.Trim())
                .ToList();
        }

        // ═══════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═══════════════════════════════════════════════

        private static RFARecordModel MapRecordFromReader(SqlDataReader reader)
        {
            return new RFARecordModel
            {
                SNo = reader.GetInt64(reader.GetOrdinal("SNo")),
                UploadId = reader.GetInt64(reader.GetOrdinal("UploadId")),
                PositionAsOfDate = reader.GetDateTime(reader.GetOrdinal("PositionAsOfDate")),
                ZonalOffice = reader.IsDBNull(reader.GetOrdinal("ZonalOffice"))
                    ? null : reader.GetString(reader.GetOrdinal("ZonalOffice")).Trim(),
                RegionalOffice = reader.IsDBNull(reader.GetOrdinal("RegionalOffice"))
                    ? null : reader.GetString(reader.GetOrdinal("RegionalOffice")).Trim(),
                BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName"))
                    ? null : reader.GetString(reader.GetOrdinal("BranchName")).Trim(),
                BranchSOL = reader.IsDBNull(reader.GetOrdinal("BranchSOL"))
                    ? null : reader.GetString(reader.GetOrdinal("BranchSOL")).Trim(),
                NameOfBorrower = reader.IsDBNull(reader.GetOrdinal("NameOfBorrower"))
                    ? null : reader.GetString(reader.GetOrdinal("NameOfBorrower")).Trim(),
                CIFID = reader.IsDBNull(reader.GetOrdinal("CIFID"))
                    ? null : reader.GetString(reader.GetOrdinal("CIFID")).Trim(),
                PAN = reader.IsDBNull(reader.GetOrdinal("PAN"))
                    ? null : reader.GetString(reader.GetOrdinal("PAN")).Trim(),
                NameOfBank = reader.IsDBNull(reader.GetOrdinal("NameOfBank"))
                    ? null : reader.GetString(reader.GetOrdinal("NameOfBank")).Trim(),
                DateOfRFAClassification = reader.IsDBNull(reader.GetOrdinal("DateOfRFAClassification"))
                    ? null : reader.GetDateTime(reader.GetOrdinal("DateOfRFAClassification")),
                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate"))
                    ? null : reader.GetDateTime(reader.GetOrdinal("ModifiedDate")),
                Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks"))
                    ? null : reader.GetString(reader.GetOrdinal("Remarks")).Trim(),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
            };
        }

        private static DataTable BuildImportDataTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("UploadId", typeof(long));
            dt.Columns.Add("PositionAsOfDate", typeof(DateTime));
            dt.Columns.Add("ZonalOffice");
            dt.Columns.Add("RegionalOffice");
            dt.Columns.Add("BranchName");
            dt.Columns.Add("BranchSOL");
            dt.Columns.Add("NameOfBorrower");
            dt.Columns.Add("CIFID");
            dt.Columns.Add("PAN");
            dt.Columns.Add("NameOfBank");
            dt.Columns.Add("DateOfRFAClassification", typeof(DateTime));
            dt.Columns.Add("Remarks");
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("CreatedBy");
            dt.Columns.Add("CreatedDate", typeof(DateTime));
            dt.Columns.Add("ModifiedBy");
            dt.Columns.Add("ModifiedDate", typeof(DateTime));
            return dt;
        }

        private static async Task BulkInsertRecordsAsync(
            SqlConnection conn,
            SqlTransaction tran,
            DataTable table)
        {
            using var bulkCopy = new SqlBulkCopy(
                conn,
                SqlBulkCopyOptions.Default,
                tran);

            bulkCopy.DestinationTableName = "RFARecords";
            bulkCopy.BatchSize = 5000;
            bulkCopy.BulkCopyTimeout = 600;

            bulkCopy.ColumnMappings.Add("UploadId", "UploadId");
            bulkCopy.ColumnMappings.Add("PositionAsOfDate", "PositionAsOfDate");
            bulkCopy.ColumnMappings.Add("ZonalOffice", "ZonalOffice");
            bulkCopy.ColumnMappings.Add("RegionalOffice", "RegionalOffice");
            bulkCopy.ColumnMappings.Add("BranchName", "BranchName");
            bulkCopy.ColumnMappings.Add("BranchSOL", "BranchSOL");
            bulkCopy.ColumnMappings.Add("NameOfBorrower", "NameOfBorrower");
            bulkCopy.ColumnMappings.Add("CIFID", "CIFID");
            bulkCopy.ColumnMappings.Add("PAN", "PAN");
            bulkCopy.ColumnMappings.Add("NameOfBank", "NameOfBank");
            bulkCopy.ColumnMappings.Add("DateOfRFAClassification", "DateOfRFAClassification");
            bulkCopy.ColumnMappings.Add("Remarks", "Remarks");
            bulkCopy.ColumnMappings.Add("IsActive", "IsActive");
            bulkCopy.ColumnMappings.Add("CreatedBy", "CreatedBy");
            bulkCopy.ColumnMappings.Add("CreatedDate", "CreatedDate");
            bulkCopy.ColumnMappings.Add("ModifiedBy", "ModifiedBy");
            bulkCopy.ColumnMappings.Add("ModifiedDate", "ModifiedDate");

            await bulkCopy.WriteToServerAsync(table);
        }

        /// <summary>
        /// Inserts an audit log entry via SP if the old and new values differ.
        /// Used during import-modify within a transaction.
        /// </summary>
        private static async Task AuditFieldIfChanged(
            SqlConnection conn,
            SqlTransaction tran,
            long sno,
            string fieldName,
            string? oldValue,
            string? newValue,
            string userId)
        {
            if (string.Equals(oldValue?.Trim(), newValue?.Trim(),
                StringComparison.OrdinalIgnoreCase))
                return;

            await ExecuteSpNonQuery(conn, tran, "SP_RFA_InsertAuditLog",
                new SqlParameter("@SNo", sno),
                new SqlParameter("@FieldName", fieldName),
                new SqlParameter("@OldValue", (object?)oldValue ?? DBNull.Value),
                new SqlParameter("@NewValue", (object?)newValue ?? DBNull.Value),
                new SqlParameter("@ModifiedBy", userId));
        }

        private static DateTime? ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string[] formats = {
                "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd",
                "dd-MMM-yyyy", "MM/dd/yyyy", "dd.MM.yyyy"
            };

            if (DateTime.TryParseExact(
                text.Trim(), formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
                return dt;

            if (DateTime.TryParse(text.Trim(), out var dt2))
                return dt2;

            return null;
        }

        // ═══════════════════════════════════════════════
        //  GENERIC SP EXECUTION HELPERS
        //  (uses raw ADO.NET — no SqlDbProvider dependency)
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Executes a stored procedure and returns a DataTable.
        /// Opens its own connection (no transaction).
        /// </summary>
        private async Task<DataTable> ExecuteSpDataTable(
            string spName,
            params SqlParameter[] parms)
        {
            using var conn = new SqlConnection(GetConnectionString());
            await conn.OpenAsync();

            using var cmd = new SqlCommand(spName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            if (parms.Length > 0) cmd.Parameters.AddRange(parms);

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            adapter.Fill(dt);
            return dt;
        }

        /// <summary>
        /// Executes a stored procedure (non-query) within an existing transaction.
        /// </summary>
        private static async Task ExecuteSpNonQuery(SqlConnection conn, SqlTransaction tran, string spName, params SqlParameter[] parms)
        {
            using var cmd = new SqlCommand(spName, conn, tran)
            {
                CommandType = CommandType.StoredProcedure
            };
            if (parms.Length > 0) cmd.Parameters.AddRange(parms);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Generates an Excel with log report consisting of two sheets where one has Summary and Second sheet has download details.
        /// </summary>
        public async Task<byte[]> ExportAuditTrailExcelAsync()
        {
            using var conn = new SqlConnection(GetConnectionString());
            await conn.OpenAsync();

            var summaryList = new List<RFAAuditSummaryModel>();
            var downloadList = new List<RFADownloadDetailModel>();
            using var cmd = new SqlCommand("SP_RFA_GetAuditTrailReport",conn);
            cmd.CommandType = CommandType.StoredProcedure;
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                summaryList.Add(
                    new RFAAuditSummaryModel
                    {
                        PositionAsOfDate = Convert.ToDateTime(reader["PositionAsOfDate"]),
                        VersionNo = Convert.ToInt32(reader["VersionNo"]),
                        UploadType = reader["UploadType"]?.ToString() ?? "",
                        UploadedBy = reader["UploadedBy"]?.ToString() ?? "",
                        UploadedOn = Convert.ToDateTime(reader["UploadedOn"]),
                        DownloadCount = Convert.ToInt32(reader["DownloadCount"])
                    });
            }

            await reader.NextResultAsync();

            while (await reader.ReadAsync())
            {
                downloadList.Add(
                    new RFADownloadDetailModel
                    {
                        PositionAsOfDate = Convert.ToDateTime(reader["PositionAsOfDate"]),
                        DownloadedBy = reader["DownloadedBy"]?.ToString() ?? "",
                        DownloadedDate = Convert.ToDateTime(reader["DownloadedDate"]),
                        DownloadType = reader["DownloadType"]?.ToString() ?? ""
                    });
            }

            using var package = new ExcelPackage();

            //---------------------------------
            // Sheet 1
            //---------------------------------
            var ws1 = package.Workbook.Worksheets.Add("Upload Summary");
            string[] hdr1 = {"Position Date","Version","Upload Type","Uploaded By","Uploaded On","Total Downloads"};

            for (int i = 0; i < hdr1.Length; i++)
            {
                ws1.Cells[1, i + 1].Value = hdr1[i];
                ws1.Cells[1, i + 1].Style.Font.Bold = true;
            }

            int r = 2;

            foreach (var item in summaryList)
            {
                ws1.Cells[r, 1].Value = item.PositionAsOfDate.ToString("dd-MM-yyyy");
                ws1.Cells[r, 2].Value =$"V{item.VersionNo}";
                ws1.Cells[r, 3].Value = item.UploadType;
                ws1.Cells[r, 4].Value = item.UploadedBy;
                ws1.Cells[r, 5].Value = item.UploadedOn.ToString("dd-MM-yyyy HH:mm");
                ws1.Cells[r, 6].Value = item.DownloadCount;
                r++;
            }

            //---------------------------------
            // Sheet 2
            //---------------------------------
            var ws2 = package.Workbook.Worksheets.Add("Download Details");
            string[] hdr2 ={"Position Date", "Downloaded By", "Download Date", "Download Type"};

            for (int i = 0; i < hdr2.Length; i++)
            {
                ws2.Cells[1, i + 1].Value = hdr2[i];
                ws2.Cells[1, i + 1].Style.Font.Bold = true;
            }

            r = 2;

            foreach (var item in downloadList)
            {
                ws2.Cells[r, 1].Value = item.PositionAsOfDate.ToString("dd-MM-yyyy");
                ws2.Cells[r, 2].Value = item.DownloadedBy;
                ws2.Cells[r, 3].Value = item.DownloadedDate.ToString("dd-MM-yyyy HH:mm");
                ws2.Cells[r, 4].Value = item.DownloadType;
                r++;
            }
            ws1.Cells.AutoFitColumns();
            ws2.Cells.AutoFitColumns();
            return package.GetAsByteArray();
        }

    }
}
