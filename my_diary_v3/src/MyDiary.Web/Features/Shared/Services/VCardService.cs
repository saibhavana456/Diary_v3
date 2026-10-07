using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.Shared.Services
{
    /// <inheritdoc />
    public class VCardService : IVCardService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<VCardService> _logger;

        public VCardService(IConfiguration configuration, ILogger<VCardService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> BuildVCardUrlAsync(string baseUrl, string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                _logger.LogWarning("VCard: employeeId is empty — returning base URL.");
                return baseUrl;
            }

            string? connStr = _configuration.GetConnectionString("VCardDBConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                _logger.LogWarning("VCard: VCardDBConnection is not configured — returning base URL.");
                return baseUrl;
            }

            try
            {
                string? empKey = null;

                using (var conn = new OracleConnection(connStr))
                {
                    await conn.OpenAsync();
                    const string query =
                        "SELECT emp_number, emp_key FROM employee_card " +
                        "WHERE emp_number = :empNumber AND ROWNUM = 1";

                    using var cmd = new OracleCommand(query, conn);
                    cmd.Parameters.Add(new OracleParameter("empNumber", employeeId));

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                        empKey = reader["emp_key"]?.ToString();
                }

                if (string.IsNullOrWhiteSpace(empKey))
                {
                    _logger.LogInformation(
                        "VCard: No emp_key found for employee {EmployeeId} — returning base URL.", employeeId);
                    return baseUrl;
                }

                long epochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return $"{baseUrl}{empKey}&q={epochMs}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VCard: Error building URL for employee {EmployeeId}.", employeeId);
                return baseUrl;
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetProfileImageAsync(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                return null;

            string? connStr = _configuration.GetConnectionString("VCardDBConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                _logger.LogWarning("VCard: VCardDBConnection is not configured — cannot fetch profile image.");
                return null;
            }

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                // File_Content is stored as BLOB (byte[]), File_Type is the MIME type
                // e.g. "image/jpeg", "image/png"
                const string query =
                    "SELECT File_Content, File_Type FROM employee_card " +
                    "WHERE emp_number = :empNumber " +
                    "  AND File_Content IS NOT NULL " +
                    "  AND ROWNUM = 1";

                using var cmd = new OracleCommand(query, conn);
                cmd.Parameters.Add(new OracleParameter("empNumber", employeeId));

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;

                // Read the binary image data
                if (reader.IsDBNull(reader.GetOrdinal("File_Content")))
                    return null;

                byte[] imageBytes = (byte[])reader["File_Content"];
                if (imageBytes.Length == 0)
                    return null;

                // Determine MIME type — default to jpeg if not stored
                string mimeType = "image/jpeg";
                if (!reader.IsDBNull(reader.GetOrdinal("File_Type")))
                {
                    var storedType = reader["File_Type"]?.ToString()?.Trim().ToLowerInvariant() ?? "";
                    if (!string.IsNullOrWhiteSpace(storedType))
                    {
                        // Accept both "image/png" and bare "png"
                        mimeType = storedType.Contains('/') ? storedType : $"image/{storedType}";
                    }
                }

                string base64 = Convert.ToBase64String(imageBytes);
                return $"data:{mimeType};base64,{base64}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VCard: Error fetching profile image for employee {EmployeeId}.", employeeId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<VisitingCardDto?> GetEmployeeCardAsync(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId)) return null;

            string? connStr = _configuration.GetConnectionString("VCardDBConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                _logger.LogWarning("VCard: VCardDBConnection is not configured — cannot fetch card.");
                return null;
            }

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                const string query = @"
                    SELECT EMP_NUMBER, EMP_KEY, EMP_NAME, ORGANIZATION_NAME, REGION, ZONE,
                           POSITION_DESIGNATION, EMAIL_ID, MOBILE_NO, USER_LOCATION,
                           FILE_CONTENT, FILE_TYPE
                    FROM EMPLOYEE_CARD
                    WHERE EMP_NUMBER = :empNumber AND ROWNUM = 1";

                using var cmd = new OracleCommand(query, conn);
                cmd.Parameters.Add(new OracleParameter("empNumber", employeeId));

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;

                string? photoUri = null;
                if (!reader.IsDBNull(reader.GetOrdinal("FILE_CONTENT")))
                {
                    var bytes = (byte[])reader["FILE_CONTENT"];
                    if (bytes.Length > 0)
                    {
                        var mime = reader.IsDBNull(reader.GetOrdinal("FILE_TYPE"))
                            ? "image/jpeg" : (reader["FILE_TYPE"]?.ToString() ?? "image/jpeg");
                        photoUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                    }
                }

                return new VisitingCardDto(
                    EmpNumber: reader["EMP_NUMBER"]?.ToString() ?? employeeId,
                    EmpKey: reader["EMP_KEY"]?.ToString(),
                    EmpName: reader["EMP_NAME"]?.ToString(),
                    OrganizationName: reader["ORGANIZATION_NAME"]?.ToString(),
                    Region: reader["REGION"]?.ToString(),
                    Zone: reader["ZONE"]?.ToString(),
                    PositionDesignation: reader["POSITION_DESIGNATION"]?.ToString(),
                    EmailId: reader["EMAIL_ID"]?.ToString(),
                    MobileNo: reader["MOBILE_NO"]?.ToString(),
                    UserLocation: reader["USER_LOCATION"]?.ToString(),
                    PhotoDataUri: photoUri);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VCard: Error fetching card for employee {EmployeeId}.", employeeId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UploadPhotoAsync(string employeeId, byte[] fileBytes, string contentType)
        {
            if (string.IsNullOrWhiteSpace(employeeId) || fileBytes.Length == 0) return false;

            string? connStr = _configuration.GetConnectionString("VCardDBConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                _logger.LogWarning("VCard: VCardDBConnection is not configured — cannot upload photo.");
                return false;
            }

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                const string sql = @"
                    UPDATE EMPLOYEE_CARD
                       SET FILE_CONTENT = :fileContent, FILE_TYPE = :fileType
                     WHERE EMP_NUMBER = :empNumber";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("fileContent", OracleDbType.Blob) { Value = fileBytes });
                cmd.Parameters.Add(new OracleParameter("fileType", contentType));
                cmd.Parameters.Add(new OracleParameter("empNumber", employeeId));

                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VCard: Error uploading photo for employee {EmployeeId}.", employeeId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<VisitingCardProductRate>> GetActiveProductRatesAsync()
        {
            var result = new List<VisitingCardProductRate>();

            string? connStr = _configuration.GetConnectionString("VCardDBConnection");
            if (string.IsNullOrWhiteSpace(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                const string query = @"
                    SELECT PRODUCT_NAME, PRODUCT_TEXT, INTEREST_TEXT, ENQUIRY_URL
                    FROM EMPLOYEE_CARD_PRODUCT_RATES
                    WHERE ACTIVE_FLAG = 'Y'
                    ORDER BY PRODUCT_NAME";

                using var cmd = new OracleCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new VisitingCardProductRate(
                        ProductName: reader["PRODUCT_NAME"]?.ToString() ?? "",
                        ProductText: reader["PRODUCT_TEXT"]?.ToString(),
                        InterestText: reader["INTEREST_TEXT"]?.ToString(),
                        EnquiryUrl: reader["ENQUIRY_URL"]?.ToString()));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VCard: Error fetching product rates.");
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<VisitingCardSettings> GetCardSettingsAsync()
        {
            // Fail open — a card with everything visible is the safer default than a
            // blank one if settings can't be read.
            var fallback = new VisitingCardSettings(true, true, true);

            string? connStr = _configuration.GetConnectionString("VCardDBConnection");
            if (string.IsNullOrWhiteSpace(connStr)) return fallback;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                const string query = @"
                    SELECT SHOW_PROFILE, SHOW_PRODUCTS, SHOW_CONTACT
                    FROM EMPLOYEE_CARD_GLOBAL_SETTINGS
                    WHERE ACTIVE_FLAG = 'Y' AND ROWNUM = 1";

                using var cmd = new OracleCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return fallback;

                bool Flag(string col) =>
                    !reader.IsDBNull(reader.GetOrdinal(col)) &&
                    string.Equals(reader[col]?.ToString(), "Y", StringComparison.OrdinalIgnoreCase);

                return new VisitingCardSettings(Flag("SHOW_PROFILE"), Flag("SHOW_PRODUCTS"), Flag("SHOW_CONTACT"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VCard: Error fetching card settings.");
                return fallback;
            }
        }
    }
}
