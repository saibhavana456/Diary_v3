using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using System.Data;

namespace MyDiary.Web.Core.Services.Authentication
{
	public class UserSessionService
	{
		private readonly string _connString;

		public UserSessionService(IConfiguration config)
		{
			_connString = config.GetConnectionString("SQLServerConnection")
				?? throw new InvalidOperationException("Missing SQLServerConnection connection string.");
		}

		// --------- SESSION MANAGEMENT ----------

		/// <summary>
		/// Checks if the user already has an active session (within last 20 minutes).
		/// Returns true if the user can login (no active session), false if blocked.
		/// </summary>
		public async Task<bool> ValidateSessionAsync(string userId)
		{
			var dt = new DataTable();
			var sql = "SELECT * FROM session_dtls WHERE USERNAME = @userId AND DATEADD(mi, 20, login_time) >= getdate()";
			await using (var conn = new SqlConnection(_connString))
			await using (var cmd = new SqlCommand(sql, conn))
			{
				cmd.Parameters.AddWithValue("@userId", userId);
				await conn.OpenAsync();
				using var da = new SqlDataAdapter(cmd);
				da.Fill(dt);
			}
			return dt.Rows.Count == 0; // true = no active session, can proceed
		}

		/// <summary>
		/// Records the user's login in session_dtls table.
		/// </summary>
		public async Task AddSessionDetailsAsync(string userId, string ipAddress)
		{
			try
			{
				var sql = "INSERT INTO session_dtls (USERNAME, IP_ADDRESS, LOGIN_TIME) VALUES (@userId, @ipAddress, GETDATE())";
				await using var conn = new SqlConnection(_connString);
				await conn.OpenAsync();
				await using var cmd = new SqlCommand(sql, conn);
				cmd.Parameters.AddWithValue("@userId", userId);
				cmd.Parameters.AddWithValue("@ipAddress", ipAddress);
				await cmd.ExecuteNonQueryAsync();
				AppLogger.LogInfo($"[UserSessionService.AddSessionDetailsAsync] Session recorded for {userId}");
			}
			catch (Exception ex)
			{
				AppLogger.LogError(ex, $"[UserSessionService.AddSessionDetailsAsync] Failed for {userId}");
			}
		}

		/// <summary>
		/// Removes the user's session record on logout.
		/// </summary>
		public async Task RemoveSessionDetailsAsync(string userId)
		{
			try
			{
				var sql = "DELETE FROM session_dtls WHERE USERNAME = @userId";
				await using var conn = new SqlConnection(_connString);
				await conn.OpenAsync();
				await using var cmd = new SqlCommand(sql, conn);
				cmd.Parameters.AddWithValue("@userId", userId);
				await cmd.ExecuteNonQueryAsync();
				AppLogger.LogInfo($"[UserSessionService.RemoveSessionDetailsAsync] Session cleared for {userId}");
			}
			catch (Exception ex)
			{
				AppLogger.LogError(ex, $"[UserSessionService.RemoveSessionDetailsAsync] Failed for {userId}");
			}
		}

		// Executes: SELECT {columns} FROM {tableName} WHERE {condition}
		private async Task<DataTable> SelectValuesAsync(string tableName, string columns, string condition)
		{
			AppLogger.LogInfo($"[UserSessionService.SelectValuesAsync] Table={tableName}, Columns={columns}, Condition={condition}");
			var dt = new DataTable();
			var sql = $"SELECT {columns} FROM {tableName} WHERE {condition}";

			await using var conn = new SqlConnection(_connString);
			await using var cmd = new SqlCommand(sql, conn);
			await conn.OpenAsync();
			using var da = new SqlDataAdapter(cmd);
			da.Fill(dt);

			AppLogger.LogInfo($"[UserSessionService.SelectValuesAsync] Returned {dt.Rows.Count} rows.");
			return dt;
		}
	}
}
