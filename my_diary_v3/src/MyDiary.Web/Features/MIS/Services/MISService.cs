using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.MIS.Models;
using MyDiary.Web.Features.Reports.Models;
using MyDiary.Web.Features.Shared.Services;
using MyDiary.Web.Services;

namespace MyDiary.Web.Features.MIS.Services
{
    public class MISService : IMISService
    {
        private readonly SqlDbProvider _sqlProvider;
        private readonly DynamicReportService _reportService;
        private readonly UserApiClient _userApiClient;
        private readonly IConfiguration _configuration;

        public MISService(SqlDbProvider sqlProvider, DynamicReportService reportService, UserApiClient userApiClient, IConfiguration configuration)
        {
            _sqlProvider = sqlProvider;
            _reportService = reportService;
            _userApiClient = userApiClient;
            _configuration = configuration;
        }

        public async Task<List<MISModel>> GetAllMISDataAsync()
        {
            AppLogger.LogInfo("[MISService.GetAllMISDataAsync] Fetching MIS master data from QLIK_URL_MASTER.");

            try
            {
                string query = "SELECT S_NO, VERTICAL, SUB_CATEGORY, CATEGORY, URL FROM QLIK_URL_MASTER ORDER BY S_NO";
                DataTable dt = await _sqlProvider.ExecuteQueryAsync(query);

                var results = dt.AsEnumerable().Select(row => new MISModel
                {
                    S_NO = Convert.ToInt32(row["S_NO"]),
                    VERTICAL = row["VERTICAL"]?.ToString() ?? "",
                    SUB_CATEGORY = row["SUB_CATEGORY"]?.ToString() ?? "",
                    CATEGORY = row["CATEGORY"]?.ToString() ?? "",
                    URL = Clean(row["URL"]?.ToString())

                }).ToList();

                AppLogger.LogInfo($"[MISService.GetAllMISDataAsync] Successfully loaded {results.Count} MIS records.");
                return results;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[MISService.GetAllMISDataAsync] Failed to fetch MIS master data.");
                throw;
            }
        }

        public async Task<UserResponse?> GetTicketAsync(string userId)
        {
            AppLogger.LogInfo($"[MISService.GetTicketAsync] Requesting ticket for userId: {userId}");

            try
            {
                // 1. Check for existing ticket within last 20 minutes
                string formattedUserId = $"unionbank\\{userId}";

                string checkQuery = @"SELECT *
                                        FROM (
                                            SELECT *,
                                                CASE 
                                                    -- Format 1: MM/dd/yyyy hh:mm:ss AM/PM
                                                    WHEN ISDATE(entry_date_timestamp) = 1 
                                                         AND entry_date_timestamp LIKE '%/%'
                                                    THEN CONVERT(datetime, entry_date_timestamp, 101)

                                                    -- Format 2: dd-MM-yyyy HH:mm:ss
                                                    WHEN entry_date_timestamp LIKE '[0-3][0-9]-[0-1][0-9]-[1-2][0-9][0-9][0-9]%'
                                                         AND ISDATE(entry_date_timestamp) = 1
                                                    THEN CONVERT(datetime, entry_date_timestamp, 105)

                                                    ELSE NULL
                                                END AS entry_dt
                                            FROM Qlik_ticket_details
                                        ) t
                                        WHERE user_id = @UserId 
                                            AND entry_dt IS NOT NULL
                                            AND DATEDIFF(MINUTE, entry_dt, GETDATE()) < 20
                                            AND status = 'A'
                                        ORDER BY entry_dt DESC";

                var parameters = new Dictionary<string, object?> { ["@UserId"] = formattedUserId };
                var existingData = await _reportService.ExecuteRawQueryAsync(checkQuery, parameters);

                if (existingData.Any())
                {
                    AppLogger.LogInfo($"[MISService.GetTicketAsync] Found existing valid ticket for userId: {userId}. Reusing cached ticket.");
                    return new UserResponse
                    {
                        ticket = existingData[0]["qlik_ticket"]?.ToString() ?? "",
                        hubUrl = existingData[0]["qlik_url"]?.ToString() ?? ""
                    };
                }

                AppLogger.LogInfo($"[MISService.GetTicketAsync] No valid cached ticket found for userId: {userId}. Calling external API.");

                // 2. If no valid ticket, call External API
                var baseUrl = _configuration["ApiEndpoints:MISTicketApiUrl"];
                if (string.IsNullOrEmpty(baseUrl))
                {
                    AppLogger.LogWarning("[MISService.GetTicketAsync] MISTicketApiUrl is not configured. Cannot obtain ticket.");
                    return null;
                }

                var apiUser = await _userApiClient.GetUserAsync(baseUrl, userId);

                if (apiUser == null)
                {
                    AppLogger.LogWarning($"[MISService.GetTicketAsync] External API returned null for userId: {userId}.");
                    return null;
                }

                AppLogger.LogInfo($"[MISService.GetTicketAsync] External API returned ticket successfully for userId: {userId}. Inserting into DB.");

                // 3. Insert the new ticket using the 'AddValues' stored procedure
                string paramNames = "user_id,qlik_url,qlik_ticket,entry_date_timestamp,status";
                string paramValues = $"'{apiUser.user}','{apiUser.hubUrl}','{apiUser.ticket}','{apiUser.timeStamp}','A'";

                var insertResult = await _reportService.InsertQlikTicketdetails("Qlik_ticket_details", paramNames, paramValues);

                // Ensure retVal is 1 indicating success
                if (insertResult.Any() && insertResult[0].ContainsKey("retVal") && insertResult[0]["retVal"].ToString() == "1")
                {
                    AppLogger.LogInfo($"[MISService.GetTicketAsync] Ticket inserted successfully for userId: {userId}.");
                    return apiUser;
                }

                var retVal = insertResult.Any() && insertResult[0].ContainsKey("retVal") ? insertResult[0]["retVal"]?.ToString() : "N/A";
                AppLogger.LogWarning($"[MISService.GetTicketAsync] Ticket insert failed for userId: {userId}. retVal={retVal}");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"[MISService.GetTicketAsync] Unhandled exception for userId: {userId}");
                throw;
            }
        }

        private static string Clean(string? value)
        {
            return value?
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim() ?? "";
        }
    }
}