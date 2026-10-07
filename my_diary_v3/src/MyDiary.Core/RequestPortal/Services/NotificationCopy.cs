using RequestPortal.Core.Dtos;

namespace RequestPortal.Core.Services;

/// <summary>
/// Static, language-neutral title/message rendering for the in-app bell.
/// Kept separate from Scriban templates (which are mail/SMS-specific) so
/// the bell stays available even if a template row is missing.
/// </summary>
internal static class NotificationCopy
{
    public static string Title(string eventCode, RequestDetail r) => eventCode switch
    {
        "Created"               => $"New request {r.ReqNo}",
        "Assigned"              => $"Assigned: {r.ReqNo}",
        "Forwarded"             => $"Forwarded to L{r.CurrentLevel}: {r.ReqNo}",
        "Escalated"             => $"Escalated to L{r.CurrentLevel}: {r.ReqNo}",
        "ClarificationSought"   => $"Clarification requested: {r.ReqNo}",
        "ClarificationProvided" => $"Clarification provided: {r.ReqNo}",
        "Resolved"              => $"Resolved: {r.ReqNo}",
        "Reopened"              => $"Reopened: {r.ReqNo}",
        "Closed"                => $"Closed: {r.ReqNo}",
        "Cancelled"             => $"Cancelled: {r.ReqNo}",
        "FeedbackRequested"     => $"Feedback requested: {r.ReqNo}",
        _                       => $"{eventCode}: {r.ReqNo}"
    };

    public static string Message(string eventCode, RequestDetail r)
        => $"{r.RequestTypeName} — {Truncate(r.Subject, 140)}";

    private static string Truncate(string s, int n)
        => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n] + "…";
}
