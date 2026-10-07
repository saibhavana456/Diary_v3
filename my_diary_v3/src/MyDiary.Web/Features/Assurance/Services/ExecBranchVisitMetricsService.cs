using System.Data;
using Microsoft.Data.SqlClient;

namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>Flat metric row returned by dbo.usp_ExecBranchVisit_GetMetrics (GAP_09).</summary>
public sealed record ExecVisitMetric(
    int ParameterId,
    string ParameterName,
    string? MdDisplayName,
    string Category,
    string? SubCategory,
    decimal? ActualsAsOn,
    decimal? BaseCurrentFy,
    DateTime? AsOnDate,
    decimal? PrevDayActual = null,
    decimal? PrevQtrActual = null);

/// <summary>
/// Auto-fetches "Executive Branch Visit" metrics from BUSINESS_360_DATA /
/// BUSINESS_360_PARAMETER_MASTER via dbo.usp_ExecBranchVisit_GetMetrics (GAP_09),
/// which — unlike usp_ROVisit_GetMetrics — returns every category and dedupes to the
/// latest AS_ON_DATE per PARAMETER_ID. See ExecBranchVisitPanelCatalog for how these
/// rows map onto the form's auto-populated tables.
/// </summary>
public sealed class ExecBranchVisitMetricsService
{
    private readonly string _connectionString;

    public ExecBranchVisitMetricsService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("SQLServerConnection")!;
    }

    public async Task<List<ExecVisitMetric>> GetMetricsAsync(string branchCode, CancellationToken ct = default)
    {
        if (!int.TryParse(branchCode, out var branchId))
            return new List<ExecVisitMetric>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("dbo.usp_ExecBranchVisit_GetMetrics", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@BranchId", SqlDbType.Int) { Value = branchId });

        var result = new List<ExecVisitMetric>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            result.Add(new ExecVisitMetric(
                ParameterId: rdr.GetInt32(rdr.GetOrdinal("PARAMETER_ID")),
                ParameterName: rdr["PARAMETER_NAME"] as string ?? "",
                MdDisplayName: rdr["MD_DISPLAY_NAME"] as string,
                Category: rdr["CATEGORY"] as string ?? "",
                SubCategory: rdr["SUB_CATEGORY"] as string,
                ActualsAsOn: rdr["ACTUALS_AS_ON"] as decimal?,
                BaseCurrentFy: rdr["BASE_CURRENT_FY"] as decimal?,
                AsOnDate: rdr["AS_ON_DATE"] as DateTime?,
                PrevDayActual: rdr["PREV_DAY_ACTUAL"] as decimal?,
                PrevQtrActual: rdr["PREV_QTR_ACTUAL"] as decimal?));
        }
        return result;
    }
}
