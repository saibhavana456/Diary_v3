namespace MyDiary.Web.Features.Feedback.Components;

/// <summary>Hardcoded master-data IDs for the Feedback activity, per the user's
/// explicit instruction (2026-08-19): "Request Type will be 'Feedback - My Diary
/// 2.0', vertical Strategy, department PMO, team My Diary Team" — seeded once
/// in RP_63_feedback_escalation.sql. Feedback tickets don't let the submitter
/// pick these (unlike the Request Portal's New.razor, which has a full
/// Vertical/Department/Activity picker) — they're fixed, so the submission
/// page only asks for what's actually Feedback-specific.</summary>
internal static class FeedbackConstants
{
    /// <summary>RP_M_ACTIVITY.ID for "Feedback - My Diary 2.0" — the key the
    /// Escalation Matrix (RP_ESCALATION_MATRIX) is read by for L1-L5 PF lookups.</summary>
    public const long ActivityId = 10889;
}
