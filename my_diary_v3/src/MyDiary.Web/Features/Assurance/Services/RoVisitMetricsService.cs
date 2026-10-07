using System.Data;
using Microsoft.Data.SqlClient;
using RequestPortal.Core.Models;

namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>
/// Auto-fetches "ZAH: RO Visit Report" metrics from the existing BUSINESS_360_DATA /
/// BUSINESS_360_PARAMETER_MASTER tables (same source Focus360/Assurance Snapshot use)
/// via dbo.usp_ROVisit_GetMetrics, keyed by the Region's own SOL ID (a Regional Office
/// carries its own SOL/branch code in CBS, same as any branch — there is no separate
/// region hierarchy table). See RoVisitPanelCatalog for how these rows map onto the
/// report's panels/rows.
/// </summary>
public sealed class RoVisitMetricsService
{
    private readonly string _connectionString;

    public RoVisitMetricsService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("SQLServerConnection")!;
    }

    public async Task<List<RoVisitSourceMetric>> GetMetricsAsync(string regionSolId, CancellationToken ct = default)
    {
        if (!int.TryParse(regionSolId, out var branchId))
            return new List<RoVisitSourceMetric>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("dbo.usp_ROVisit_GetMetrics", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@BranchId", SqlDbType.Int) { Value = branchId });

        var result = new List<RoVisitSourceMetric>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            result.Add(new RoVisitSourceMetric
            {
                ParameterName = rdr["PARAMETER_NAME"] as string ?? "",
                Category = rdr["CATEGORY"] as string ?? "",
                SubCategory = rdr["SUB_CATEGORY"] as string,
                ActualsAsOn = rdr["ACTUALS_AS_ON"] as decimal?,
                BaseCurrentFy = rdr["BASE_CURRENT_FY"] as decimal?
            });
        }
        return result;
    }
}
