namespace MyDiary.Web.Features.Shared.Services
{
    /// <summary>
    /// Builds the VCard redirect URL for the currently logged-in employee,
    /// and fetches the employee's profile picture from the Employee_Card table.
    /// </summary>
    public interface IVCardService
    {
        /// <summary>
        /// Looks up the employee's emp_key in the VCard database and returns the
        /// fully-formed URL ready to open in a new tab.
        /// Falls back to <paramref name="baseUrl"/> when no record is found or on error.
        /// </summary>
        Task<string> BuildVCardUrlAsync(string baseUrl, string employeeId);

        /// <summary>
        /// Fetches the employee's profile picture from the Employee_Card table.
        /// Returns a data-URI string (e.g. "data:image/jpeg;base64,...")
        /// suitable for use directly in an &lt;img src&gt; attribute,
        /// or <c>null</c> if no picture exists or on error.
        /// </summary>
        Task<string?> GetProfileImageAsync(string employeeId);

        /// <summary>Full visiting-card details for the "My Card" page.</summary>
        Task<VisitingCardDto?> GetEmployeeCardAsync(string employeeId);

        /// <summary>Replaces the employee's stored photo (JPEG/PNG bytes).</summary>
        Task<bool> UploadPhotoAsync(string employeeId, byte[] fileBytes, string contentType);

        /// <summary>Active product-rate rows shown on the card, ordered by name.</summary>
        Task<IReadOnlyList<VisitingCardProductRate>> GetActiveProductRatesAsync();

        /// <summary>The single active show/hide-section settings row.</summary>
        Task<VisitingCardSettings> GetCardSettingsAsync();
    }

    public sealed record VisitingCardDto(
        string EmpNumber, string? EmpKey, string? EmpName, string? OrganizationName,
        string? Region, string? Zone, string? PositionDesignation, string? EmailId,
        string? MobileNo, string? UserLocation, string? PhotoDataUri);

    public sealed record VisitingCardProductRate(
        string ProductName, string? ProductText, string? InterestText, string? EnquiryUrl);

    public sealed record VisitingCardSettings(bool ShowProfile, bool ShowProducts, bool ShowContact);
}
