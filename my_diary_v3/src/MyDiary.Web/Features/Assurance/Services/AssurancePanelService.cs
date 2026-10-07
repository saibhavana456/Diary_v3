using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Features.Assurance.Models;

namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>
/// Fetches the Assurance Snapshot's data-driven panel grid (dbo.usp_Assurance_GetPanelGrid —
/// see db/sqlserver/GAP_04_assurance_panels.sql) and reshapes the flat result set into
/// Category → side-by-side PanelRowGroup → Panel → Row → Cell, which the Razor page and the
/// PDF/Excel/CSV exporters all render from identically.
/// </summary>
public sealed class AssurancePanelService
{
    private readonly string _connectionString;

    public AssurancePanelService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("SQLServerConnection")!;
    }

    private static string FormatValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        // Handle values like "19737.000000 | 53629729.000000"
        if (value.Contains("|"))
        {
            return string.Join(" | ",
                value.Split('|')
                     .Select(v => FormatSingleValue(v.Trim())));
        }

        return FormatSingleValue(value);
    }

    private static string FormatSingleValue(string value)
    {
        if (decimal.TryParse(value, out decimal d))
            return d.ToString("0.######");   // removes trailing zeros

        return value;
    }

    public async Task<List<AssuranceCategory>> GetPanelGridAsync(string branchId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("dbo.usp_Assurance_GetPanelGrid", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@BranchId", SqlDbType.NVarChar) { Value = branchId });

        await using var rdr = await cmd.ExecuteReaderAsync();

        // categoryLabel -> pairGroup -> panelId -> panel
        var categories = new List<(string Label, int Sort)>();
        var byCategory = new Dictionary<string, List<(int PairGroup, List<AssurancePanel> Panels)>>();
        var panelById = new Dictionary<int, AssurancePanel>();

        while (await rdr.ReadAsync())
        {
            var panelId = rdr.GetInt32(rdr.GetOrdinal("PANEL_ID"));
            var categoryLabel = rdr.GetString(rdr.GetOrdinal("CATEGORY_LABEL"));
            var categorySort = rdr.GetInt32(rdr.GetOrdinal("CATEGORY_SORT"));
            var panelTitle = rdr.GetString(rdr.GetOrdinal("PANEL_TITLE"));
            var pairGroup = rdr.GetInt32(rdr.GetOrdinal("PAIR_GROUP"));
            var rowLabelHeader = rdr.IsDBNull(rdr.GetOrdinal("ROW_LABEL_HEADER")) ? null : rdr.GetString(rdr.GetOrdinal("ROW_LABEL_HEADER"));
            var colOrder = rdr.GetInt32(rdr.GetOrdinal("COL_ORDER"));
            var colLabel = rdr.IsDBNull(rdr.GetOrdinal("COL_LABEL")) ? "" : rdr.GetString(rdr.GetOrdinal("COL_LABEL"));
            var rowOrder = rdr.GetInt32(rdr.GetOrdinal("ROW_ORDER"));
            var rowLabel = rdr.IsDBNull(rdr.GetOrdinal("ROW_LABEL")) ? "" : rdr.GetString(rdr.GetOrdinal("ROW_LABEL"));
            var paramId = rdr.IsDBNull(rdr.GetOrdinal("PARAMETER_ID")) ? (long?)null : Convert.ToInt64(rdr["PARAMETER_ID"]);
            var value = rdr.IsDBNull(rdr.GetOrdinal("ACTUALS_AS_ON")) ? "-" : FormatValue(rdr.GetString(rdr.GetOrdinal("ACTUALS_AS_ON")));
            var asOnDate = rdr.IsDBNull(rdr.GetOrdinal("AS_ON_DATE"))? "": rdr.GetString(rdr.GetOrdinal("AS_ON_DATE"));
            //rdr.IsDBNull(rdr.GetOrdinal("ACTUALS_AS_ON")) ? (long?)null : Convert.ToInt64(rdr["ACTUALS_AS_ON"]);

            if (!categories.Any(c => c.Label == categoryLabel))
                categories.Add((categoryLabel, categorySort));

            if (!panelById.TryGetValue(panelId, out var panel))
            {
                panel = new AssurancePanel { PanelId = panelId, Title = panelTitle, RowLabelHeader = rowLabelHeader };
                panelById[panelId] = panel;

                if (!byCategory.TryGetValue(categoryLabel, out var pairList))
                    byCategory[categoryLabel] = pairList = new List<(int, List<AssurancePanel>)>();
                var group = pairList.FirstOrDefault(g => g.PairGroup == pairGroup);
                if (group.Panels is null)
                {
                    group = (pairGroup, new List<AssurancePanel>());
                    pairList.Add(group);
                }
                group.Panels.Add(panel);
            }

            // SP orders by (category, pair, panel, row, col), so rows/cols for a given panel
            // always arrive in increasing order — safe to index directly by (rowOrder, colOrder).
            while (panel.ColumnHeaders.Count < colOrder) panel.ColumnHeaders.Add("");
            panel.ColumnHeaders[colOrder - 1] = $"{colLabel} ({asOnDate})";            

            while (panel.Rows.Count < rowOrder) panel.Rows.Add(new AssurancePanelRow());
            var row = panel.Rows[rowOrder - 1];
            row.RowLabel = rowLabel;
            row.ParameterId = paramId;
            while (row.Cells.Count < panel.ColumnHeaders.Count)
                row.Cells.Add("");

            while (row.Cells.Count < colOrder)
                row.Cells.Add("");

            row.Cells[colOrder - 1] = value;
            //while (row.Cells.Count < colOrder) row.Cells.Add("");
            //row.Cells[colOrder - 1] = value.ToString();
        }

        return categories
            .OrderBy(c => c.Sort)
            .Select(c => new AssuranceCategory
            {
                Label = c.Label,
                SortOrder = c.Sort,
                PanelRowGroups = byCategory.TryGetValue(c.Label, out var groups)
                    ? groups.OrderBy(g => g.PairGroup).Select(g => new AssurancePanelRowGroup { Panels = g.Panels }).ToList()
                    : new List<AssurancePanelRowGroup>()
            })
            .ToList();
    }
}
