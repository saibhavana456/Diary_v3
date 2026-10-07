using MyDiary.Web.Features.Reports.Models;

namespace MyDiary.Web.Features.Reports.Services
{
    public interface IRFAService
    {
        /// <summary>
        /// Get all distinct upload dates for the "Position as of Date" dropdown.
        /// </summary>
        Task<List<RFAUploadDate>> GetUploadDatesAsync();

        /// <summary>
        /// Get paginated, filtered, searchable RFA records for a given date.
        /// </summary>
        Task<RFAListResult> GetRecordsAsync(
            DateTime positionDate,
            string? search,
            string? zonalOffice,
            string? regionalOffice,
            int page,
            int pageSize);

        /// <summary>
        /// Get the list of authorised CCM users for managing the RFA records.
        /// </summary>
        Task<bool> HasManageAccessAsync(string employeeId);

        /// <summary>
        /// Import Excel for "Add New" — inserts records for a new date.
        /// Unique key = PositionAsOfDate + PAN.
        /// </summary>
        Task<RFAImportResult> ImportAddNewAsync(
            Stream stream,
            DateTime positionAsOfDate,
            string userId);

        /// <summary>
        /// Import Excel for "Modify Existing" — updates/inserts records for an existing date.
        /// Unique key = PositionAsOfDate + PAN.
        /// </summary>
        Task<RFAImportResult> ImportModifyExistingAsync(
            Stream stream,
            DateTime positionAsOfDate,
            string userId);

        /// <summary>
        /// Update a single RFA record inline (with audit log).
        /// </summary>
        Task<bool> UpdateRecordAsync(
            RFAEditModel model,
            string userId);

        /// <summary>
        /// Soft-delete a single RFA record.
        /// </summary>
        Task<bool> DeleteRecordAsync(
            long sno,
            string userId);

        /// <summary>
        /// Soft-delete ALL RFA records for a given position date. CCM-only action.
        /// </summary>
        Task<bool> DeleteAllForDateAsync(
            DateTime positionDate,
            string userId);

        /// <summary>
        /// Export records for a given date as Excel (.xlsx) bytes.
        /// </summary>
        Task<byte[]> ExportExcelAsync(DateTime positionDate, string? zonalOffice, string? regionalOffice, string? search);

        /// <summary>
        /// Export records for a given date as PDF bytes with watermark.
        /// Watermark: PF No | Date | Time | My Diary Portal | Confidentiality Clause.
        /// </summary>
        Task<byte[]> ExportPdfAsync(DateTime positionDate, string? zonalOffice, string? regionalOffice, string? search, string userPfNumber);


        Task<List<RFARecordModel>> GetPdfDataAsync(DateTime positionDate, string? zonalOffice, string? regionalOffice, string? search);

        /// <summary>
        /// Maintaines logs for every Data Downloaded event.
        /// </summary>
        Task LogDownloadAsync(DateTime positionDate, string downloadType, string userId, string? zone, string? region, string? search);

        /// <summary>
        /// Get distinct Zonal Offices for filter dropdown.
        /// </summary>
        Task<List<string>> GetZonalOfficesAsync(DateTime positionDate);

        /// <summary>
        /// Get distinct Regional Offices for filter dropdown (optionally filtered by zone).
        /// </summary>
        Task<List<string>> GetRegionalOfficesAsync(DateTime positionDate,string? zonalOffice);

        /// <summary>
        /// Get Upload History for different version files.
        /// </summary>
        Task<List<RFAUploadHistoryModel>> GetUploadHistoryAsync(DateTime? positionDate);

        /// <summary>
        /// Download Uploaded Old version Excel files.
        /// </summary>
        Task<(byte[] FileBytes, string FileName, string ContentType)> GetUploadVersionAsync(long uploadId);

        /// <summary>
        /// Log report Excel with different sheets with summary and pdf download details
        /// </summary>
        Task<byte[]> ExportAuditTrailExcelAsync();
    }
}
