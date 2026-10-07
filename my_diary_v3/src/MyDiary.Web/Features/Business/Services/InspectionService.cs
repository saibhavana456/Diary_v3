using Microsoft.Data.SqlClient;
using MyDiary.Web.Features.Business.Models;

namespace MyDiary.Web.Features.Business.Services
{
    public class InspectionService : IInspectionService
    {
        private readonly string _connectionString;

        public InspectionService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SQLServerConnection")!;
        }
        public async Task<List<Dictionary<string, object>>> GetSubmittedReports()
        {
            var list = new List<Dictionary<string, object>>();

            using var con = new SqlConnection(_connectionString);

            string query = @"SELECT BranchCode, OfficerName, InspectionDate
                            FROM MonthlyGoldInspection
                            ORDER BY InspectionDate DESC";

            using var cmd = new SqlCommand(query, con);

            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>
                {
                    { "BranchCode", reader["BranchCode"] },
                    { "OfficerName", reader["OfficerName"] },
                    { "InspectionDate", reader["InspectionDate"] }
                };

                list.Add(row);
            }

            return list;
        }

        public async Task SaveInspection(MonthlyGoldInspectionEntity model)
        {
            using var con = new SqlConnection(_connectionString);

            string query = @"
            INSERT INTO MonthlyGoldInspection
            (
                InspectionDate, BranchCode, OfficerName,
                PhysicalPacketsAvailable, OutstandingAccounts, ClosedButUndelivered,
                MismatchReason, OtherObservations,

                C1_Compliant, C1_Observation,
                C2_Compliant, C2_Observation,
                C3_Compliant, C3_Observation,
                C4_Compliant, C4_Observation,
                C5_Compliant, C5_Observation,
                C6_Compliant, C6_Observation,
                C7_Compliant, C7_Observation,
                C8_Compliant, C8_Observation,
                C9_Compliant, C9_Observation,
                C10_Compliant, C10_Observation,
                C11_Compliant, C11_Observation,
                C12_Compliant, C12_Observation,
                C13_Compliant, C13_Observation,
                C14_Compliant, C14_Observation,
                C15_Compliant, C15_Observation,
                C16_Compliant, C16_Observation,
                C17_Compliant, C17_Observation,
                C18_Compliant, C18_Observation,
                C19_Compliant, C19_Observation,
                C20_Compliant, C20_Observation,
                C21_Compliant, C21_Observation,
                C22_Compliant, C22_Observation,
                C23_Compliant, C23_Observation,
                C24_Compliant, C24_Observation,
                C25_Compliant, C25_Observation,
                C26_Compliant, C26_Observation,
                C27_Compliant, C27_Observation,
                C28_Compliant, C28_Observation,
                C29_Compliant, C29_Observation,
                C30_Compliant, C30_Observation
            )
            VALUES
            (
                @InspectionDate, @BranchCode, @OfficerName,
                @PhysicalPacketsAvailable, @OutstandingAccounts, @ClosedButUndelivered,
                @MismatchReason, @OtherObservations,

                @C1_Compliant, @C1_Observation,
                @C2_Compliant, @C2_Observation,
                @C3_Compliant, @C3_Observation,
                @C4_Compliant, @C4_Observation,
                @C5_Compliant, @C5_Observation,
                @C6_Compliant, @C6_Observation,
                @C7_Compliant, @C7_Observation,
                @C8_Compliant, @C8_Observation,
                @C9_Compliant, @C9_Observation,
                @C10_Compliant, @C10_Observation,
                @C11_Compliant, @C11_Observation,
                @C12_Compliant, @C12_Observation,
                @C13_Compliant, @C13_Observation,
                @C14_Compliant, @C14_Observation,
                @C15_Compliant, @C15_Observation,
                @C16_Compliant, @C16_Observation,
                @C17_Compliant, @C17_Observation,
                @C18_Compliant, @C18_Observation,
                @C19_Compliant, @C19_Observation,
                @C20_Compliant, @C20_Observation,
                @C21_Compliant, @C21_Observation,
                @C22_Compliant, @C22_Observation,
                @C23_Compliant, @C23_Observation,
                @C24_Compliant, @C24_Observation,
                @C25_Compliant, @C25_Observation,
                @C26_Compliant, @C26_Observation,
                @C27_Compliant, @C27_Observation,
                @C28_Compliant, @C28_Observation,
                @C29_Compliant, @C29_Observation,
                @C30_Compliant, @C30_Observation
            )";

            using var cmd = new SqlCommand(query, con);

            // ✅ Header Parameters
            cmd.Parameters.AddWithValue("@InspectionDate", (object?)model.InspectionDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BranchCode", model.BranchCode);
            cmd.Parameters.AddWithValue("@OfficerName", model.OfficerName);

            cmd.Parameters.AddWithValue("@PhysicalPacketsAvailable", model.PhysicalPacketsAvailable);
            cmd.Parameters.AddWithValue("@OutstandingAccounts", model.OutstandingAccounts);
            cmd.Parameters.AddWithValue("@ClosedButUndelivered", model.ClosedButUndelivered);

            cmd.Parameters.AddWithValue("@MismatchReason", (object?)model.MismatchReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OtherObservations", (object?)model.OtherObservations ?? DBNull.Value);

            // ✅ Auto-map C1–C30 using LOOP (No manual repetition 🎯)
            for (int i = 1; i <= 30; i++)
            {
                var compProp = typeof(MonthlyGoldInspectionEntity)
                    .GetProperty($"C{i}_Compliant");

                var obsProp = typeof(MonthlyGoldInspectionEntity)
                    .GetProperty($"C{i}_Observation");

                var compValue = compProp?.GetValue(model);
                var obsValue = obsProp?.GetValue(model);

                cmd.Parameters.AddWithValue($"@C{i}_Compliant", compValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue($"@C{i}_Observation", obsValue ?? DBNull.Value);
            }

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
    }
}