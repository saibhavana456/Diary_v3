using System.Security.Claims;
using MyDiary.Web.Features.StrikeCorner.Models;
using MyDiary.Web.Features.StrikeCorner.Pages;

namespace MyDiary.Web.Features.StrikeCorner.Services;

public interface IStrikeCornerService
{
    /// <summary>Card 1 ("Create MOM") is visible to: Super Admin / App Admin; an employee listed in
    /// RP_STRIKE_ADMIN; or an employee at Scale 8+ posted at CO (see StrikeCornerService for the exact
    /// claims used -- flagged as a follow-up decision since the HR/scale data source may need refinement).</summary>
    Task<bool> CanCreateMomAsync(ClaimsPrincipal user);

    Task<List<(string EmpCode, string AddedBy, DateTime AddedAt)>> ListMomAdminsAsync();
    Task AddMomAdminAsync(string empCode, string actor);
    Task RemoveMomAdminAsync(string empCode);

    Task<List<StrikeFaqDoc>> ListFaqDocsAsync(string? category = null, bool activeOnly = true);
    Task<StrikeFaqDoc?> SaveFaqDocAsync(string category, string title, byte[] bytes, string contentType, string originalFileName, string actor, long? existingId = null);
    Task<(byte[] Bytes, string ContentType, string FileName)?> GetFaqDocFileAsync(long docId);
    Task DeleteFaqDocAsync(long docId);

    Task<List<StrikeFaqDoc>> ListStrikeDataDocsAsync(bool activeOnly = true);

    Task SaveStrikeDataDocAsync(
        string title,
        byte[] bytes,
        string contentType,
        string originalFileName,
        string actor);

    Task<(byte[] Bytes, string ContentType, string FileName)?>
        GetStrikeDataDocFileAsync(long docId);

    Task DeleteStrikeDataDocAsync(long docId);

    Task<List<Models.StrikeCommitteeMeeting>>  ListStrikeCommitteeMeetingsAsync();

    Task<long> SaveStrikeCommitteeMeetingAsync(
        long? meetingId,
        DateTime meetingDate,
        string remarks,
        byte[] fileBytes,
        string contentType,
        string fileName,
        string actor);

    Task DeleteStrikeCommitteeMeetingAsync(
        long meetingId);

    Task<(byte[] Bytes, string ContentType, string FileName)?>
        GetStrikeCommitteeFileAsync(
            long meetingId);

    Task<List<StrikeCommitteeAttachment>>
        ListStrikeCommitteeAttachmentsAsync(
            long meetingId);

    Task SaveStrikeCommitteeAttachmentAsync(
        long meetingId,
        byte[] fileBytes,
        string contentType,
        string fileName,
        string actor);

    Task<(byte[] Bytes, string ContentType, string FileName)?>
        GetStrikeCommitteeAttachmentAsync(
            long attachmentId);
}
