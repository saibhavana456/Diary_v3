using MyDiary.Web.Features.FlatManagement.Models.DTO;

namespace MyDiary.Web.Features.FlatManagement.Services;

public interface IGrievanceActionService
{
    Task<FlatAllocationInfo?> ResolveFlatFromRefNoAsync(string refNo);
    Task<FlatDetailsInfo?> GetFlatDetailsAsync(string emplId);
    Task<GrievanceDetailsInfo?> GetGrievanceAsync(string refNo);
    Task<List<VendorOption>> GetActiveVendorsAsync();
    Task<VendorInfo?> GetVendorInfoAsync(string vendorId);
    Task<SaveActionResult> SaveActionAsync(SaveActionRequest request);
    Task<AssignmentEmailInfo?> GetAssignmentEmailInfoAsync(string createdByPfNo);
    Task NotifyVendorAssignedAsync(string refNo, string enteredByCode, GrievanceDetailsInfo grievance,
        VendorInfo vendor, string? expectedVendorVisitDate);

    // Admin-only: general "your service request was updated" notification sent to the
    // grievance's creator, ported from AdminGrievanceAction.aspx.cs's SendAssignedToUserEmail.
    // Takes AllocatedFlatDetails (not FlatDetailsInfo) because the admin page sources flat info
    // from IUserFlatService.GetAllocatedFlatAsync, which it already needs for the Flat Details /
    // Current Occupant sections -- no need for a second, differently-shaped flat query.
    Task NotifyUserStatusUpdateAsync(string refNo, string enteredByCode, GrievanceDetailsInfo grievance,
        AllocatedFlatDetails? flat, string actionTaken);

    Task<StoredAttachmentRef> SaveAttachmentAsync(string refNo, string category, string fileName, string contentType, byte[] data, string? uploadedBy);
    Task<StoredAttachment?> GetAttachmentAsync(int attachmentId);

    // Ownership checks for the shared attachment download endpoint — GetAttachmentAsync above
    // takes a bare sequential ID with no scoping, which would otherwise let any authenticated
    // vendor enumerate and download attachments belonging to grievances assigned to OTHER
    // vendors, or let one resident download another resident's attachment.
    Task<bool> IsAttachmentAccessibleToVendorAsync(int attachmentId, string vendorMobile);
    Task<bool> IsAttachmentAccessibleToStaffAsync(int attachmentId, string empCode);
    Task<AttachmentMigrationResult> MigrateFolderAttachmentsToDbAsync(string uploadedBy);

    Task<GrievanceListResult> GetVendorGrievancesAsync(string vendorMobile, string? building, string? flatNo, int pageIndex, int pageSize);
    Task<List<string>> GetVendorGrievanceBuildingsAsync(string vendorMobile);

    Task<List<ProductRow>> GetReplacedItemsAsync(string refNo);
    Task<List<ActionHistoryRow>> GetActionHistoryAsync(string refNo);
}
