using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MyDiary.Web.Features.ProjectMuskaan.Models;

namespace MyDiary.Web.Features.ProjectMuskaan.Services
{
    /// <summary>
    /// Reads [dbo].[MDSummary ] from the SQL Server behind "MyDiaryDBConnection".
    /// Program.cs decrypts every ConnectionStrings entry at startup and injects the plain
    /// values into configuration, so GetConnectionString returns a usable string here.
    ///
    /// If your project references System.Data.SqlClient instead of Microsoft.Data.SqlClient,
    /// change the two "using"/type names below - the ADO.NET code is otherwise identical.
    /// </summary>
    public class ProjectMuskaanService : IProjectMuskaanService
    {
        private readonly string _connectionString;

        // NOTE: the table name has a trailing space - "[MDSummary ]" - exactly as in the
        // original page. Do not "tidy" it unless the table itself is renamed.
        private const string Sql = @"
            SELECT
                [SNo],
                [Problem_Description],
                [Action_Required],
                [Benefit_to_Branches],
                [Benefit_to_Branches_Ease_of_doing_business],
                [Benefit_to_Branches_cost_cutting],
                [Benefit_to_Branches_Customer_service],
                [Benefit_to_Branches_New_business],
                [Benefit_to_Branches_Risk_mitigation],
                [Requested_By],
                [Actual_Status],
                CONVERT(date, [Completion_Date]) AS Completion_Date
            FROM [dbo].[MDSummary ]";

        public ProjectMuskaanService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SQLServerConnection") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'MyDiaryDBConnection' was not found in configuration.");
            }
        }

        public async Task<List<ProjectMuskaanTicket>> GetTicketsAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<ProjectMuskaanTicket>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(Sql, conn) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ProjectMuskaanTicket
                {
                    SNo = Text(reader, "SNo"),
                    ProblemDescription = Text(reader, "Problem_Description"),
                    ActionRequired = Text(reader, "Action_Required"),
                    BenefitToBranches = Text(reader, "Benefit_to_Branches"),
                    BenefitEaseOfDoingBusiness = Text(reader, "Benefit_to_Branches_Ease_of_doing_business"),
                    BenefitCostCutting = Text(reader, "Benefit_to_Branches_cost_cutting"),
                    BenefitCustomerService = Text(reader, "Benefit_to_Branches_Customer_service"),
                    BenefitNewBusiness = Text(reader, "Benefit_to_Branches_New_business"),
                    BenefitRiskMitigation = Text(reader, "Benefit_to_Branches_Risk_mitigation"),
                    RequestedBy = Text(reader, "Requested_By").Trim(),
                    ActualStatus = Text(reader, "Actual_Status"),
                    CompletionDate = Date(reader, "Completion_Date")
                });
            }

            return results;
        }

        private static string Text(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        private static DateTime? Date(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
