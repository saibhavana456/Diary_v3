namespace MyDiary.Web.Features.StaffVendorConnect.Services;

/// <summary>Server-side PII masking — this is Blazor Server, so the full rendered value would
/// otherwise cross the SignalR circuit to the browser even if only hidden with CSS/JS. Every
/// caller must mask the value in C# BEFORE it becomes part of a component's render output, never
/// rely on front-end-only hiding. Reused by StaffVendorConnectService for both dynamic vendor
/// record fields flagged IS_PII (RP_SVC_FIELD_DEFINITION) and Staff Directory's fixed
/// CONTACT_NO/EMAIL_ID-style columns.</summary>
public static class PiiMasker
{
    /// <summary>Masks an email address: first character of the local part + domain kept,
    /// e.g. "jane.doe@bank.com" -> "j***@bank.com".</summary>
    public static string? MaskEmail(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var at = value.IndexOf('@');
        if (at <= 0) return MaskGeneric(value); // not a recognizable email shape — fall back
        var local = value[..at];
        var domain = value[at..]; // includes the leading '@'
        return $"{local[0]}***{domain}";
    }

    /// <summary>Masks a phone/mobile number: keeps the last 2 digits, e.g. "9876543210" ->
    /// "********10".</summary>
    public static string? MaskPhone(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= 2) return new string('*', value.Length);
        return new string('*', value.Length - 2) + value[^2..];
    }

    /// <summary>Generic PII mask (e.g. PAN): keeps first 2 and last 2 characters, masks the
    /// middle, e.g. "ABCPD1234E" -> "AB******4E". Values under 5 characters are masked entirely —
    /// too short for a partial mask to make sense.</summary>
    public static string? MaskGeneric(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length < 5) return new string('*', value.Length);
        return value[..2] + new string('*', value.Length - 4) + value[^2..];
    }

    /// <summary>Picks the right mask format from a field key / type hint. Emails contain '@';
    /// phone-like values are mostly digits and roughly 10 characters; everything else flagged PII
    /// gets the generic first-2/last-2 mask.</summary>
    public static string? Mask(string? value, string fieldKeyOrLabel)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Contains('@')) return MaskEmail(value);

        var digitCount = value.Count(char.IsDigit);
        if (digitCount >= value.Length - 2 && value.Length is >= 8 and <= 15) return MaskPhone(value);

        var keyUpper = fieldKeyOrLabel.ToUpperInvariant();
        if (keyUpper.Contains("MOBILE") || keyUpper.Contains("PHONE") || keyUpper.Contains("CONTACT"))
            return MaskPhone(value);
        if (keyUpper.Contains("EMAIL"))
            return MaskEmail(value);

        return MaskGeneric(value);
    }
}
