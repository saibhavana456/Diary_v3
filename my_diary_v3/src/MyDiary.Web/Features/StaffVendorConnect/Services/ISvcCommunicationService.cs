using MyDiary.Web.Features.StaffVendorConnect.Models.DTO;

namespace MyDiary.Web.Features.StaffVendorConnect.Services;

/// <summary>Recipient a communication can be sent to — a staff member (email/mobile from the
/// snapshot) or a vendor record's own contact (email/mobile captured on the form, when present).</summary>
public class SvcRecipientCandidate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Mobile { get; set; }
}

public interface ISvcCommunicationService
{
    /// <summary>Candidate recipients for the "Selected" picker — staff snapshot rows for
    /// TargetModule=STAFF, vendor records' captured contact for TargetModule=VENDOR.</summary>
    Task<List<SvcRecipientCandidate>> GetRecipientCandidatesAsync(string targetModule, string? categoryCode);

    Task<(long CommId, int Sent, int Failed)> SendAsync(SvcSendCommunicationRequest request, string actor);

    Task<List<SvcCommLogEntry>> GetCommLogAsync(string? targetModule, int maxRows = 100);
    Task<List<SvcCommRecipientResult>> GetCommRecipientsAsync(long commId);
    Task<List<SvcCommAttachmentInfo>> GetCommAttachmentsAsync(long commId);

    /// <summary>Approved SMS templates from the existing Notification Templates admin page
    /// (RequestPortal's own NotifTemplate store, Channel=Sms, IsActive) — "take template from
    /// notification templates for SMS ... allow selection from there only."</summary>
    Task<List<global::RequestPortal.Core.Models.NotifTemplate>> GetApprovedSmsTemplatesAsync();
}
