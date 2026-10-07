using MyDiary.Web.Features.Shared.Models;
using System.Globalization;

namespace MyDiary.Web.Features.Shared.Services
{
    /// <summary>
    /// Monthly ATR MIS reports (BRD "Development of Region-wise, Branch-wise and RO-wise
    /// Monthly ATR MIS Reports in My Diary Portal", 07.08.2026).
    ///
    /// Deliberately kept OUT of DynamicReportService (which several other pages depend on,
    /// e.g. ATRStatusReport.razor, DynamicDashboard.razor) so this feature can evolve without
    /// any risk to that shared service. It composes DynamicReportService via DI and reuses its
    /// existing, already-reviewed ADO.NET plumbing (ExecuteStoredProcAsync,
    /// ExecuteStoredProcedureMultiAsync, ExportToExcel) rather than duplicating it.
    ///
    /// SPs: usp_getATRMonthlyRegionReport / usp_getATRMonthlyBranchReport /
    ///      usp_getATRMonthlyRegionSummary  (see ATR_Monthly_Reports.sql)
    /// Status codes from SP: C = Submitted-complied, NC = Submitted-not complied, NS = Not submitted
    /// </summary>
    public class AtrMonthlyReportService
    {
        private readonly DynamicReportService _report;

        public AtrMonthlyReportService(DynamicReportService report) => _report = report;

        private static string AtrStr(Dictionary<string, object> r, string key)
            => r.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? v.ToString()!.Trim() : string.Empty;

        private static int AtrInt(Dictionary<string, object> r, string key)
            => r.TryGetValue(key, out var v) && v != null && v != DBNull.Value
               && int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var n) ? n : 0;

        private static DateTime AtrDate(Dictionary<string, object> r, string key)
        {
            if (r.TryGetValue(key, out var v) && v != null && v != DBNull.Value)
            {
                if (v is DateTime dt) return dt.Date;
                if (DateTime.TryParse(v.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var p)) return p.Date;
            }
            return DateTime.MinValue;
        }

        private static string AtrDayKey(DateTime d) => "d" + d.ToString("yyyyMMdd");

        private static decimal AtrPct(int complied, int total)
            => total <= 0 ? 0m : Math.Round(complied * 100m / total, 1);

        private static Dictionary<string, object> AtrMonthParams(DateTime month)
            => new() { ["@Month"] = new DateTime(month.Year, month.Month, 1) };

        /// <summary>Report-1: Region-wise monthly compliance (branches x dates).</summary>
        public async Task<AtrGrid> GetAtrMonthlyRegionGridAsync(DateTime month, string? zoneSolid, string regionSolid)
        {
            var p = AtrMonthParams(month);
            p["@Zone_Solid"] = string.IsNullOrWhiteSpace(zoneSolid) ? (object)DBNull.Value : zoneSolid!;
            p["@Region_Solid"] = regionSolid;

            var rows = await _report.ExecuteStoredProcAsync("usp_getATRMonthlyRegionReport", p, 60);
            var grid = new AtrGrid();
            if (rows.Count == 0) return grid;

            grid.Days = rows.Select(r => AtrDate(r, "Report_Date")).Distinct().OrderBy(d => d).ToList();

            grid.Columns.Add(new ColumnConfig { Header = "Branch Name", PropertyName = "branch_name" });
            grid.Columns.Add(new ColumnConfig { Header = "Branch Code", PropertyName = "branch_code" });
            grid.Columns.Add(new ColumnConfig { Header = "Branch SOL", PropertyName = "sol_id" });
            foreach (var d in grid.Days)
                grid.Columns.Add(new ColumnConfig { Header = d.ToString("dd-MM-yy"), PropertyName = AtrDayKey(d) });
            grid.Columns.Add(new ColumnConfig { Header = "Submitted-complied", PropertyName = "cnt_c", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Submitted-not complied", PropertyName = "cnt_nc", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Not submitted", PropertyName = "cnt_ns", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Compliance %", PropertyName = "pct", IsNumeric = true, Format = "0.0" });

            foreach (var g in rows.GroupBy(r => AtrStr(r, "Sol_Id"))
                                  .OrderBy(g => AtrStr(g.First(), "Branch_Name"), StringComparer.OrdinalIgnoreCase))
            {
                var first = g.First();
                var row = new Dictionary<string, object>
                {
                    ["branch_name"] = AtrStr(first, "Branch_Name"),
                    ["branch_code"] = AtrStr(first, "Branch_Code"),
                    ["sol_id"] = g.Key
                };
                int c = 0, nc = 0, ns = 0;
                foreach (var d in grid.Days) row[AtrDayKey(d)] = AtrStatus.NotSubmitted;
                foreach (var r in g)
                {
                    var text = AtrStatus.FromCode(AtrStr(r, "Status_Code"));
                    row[AtrDayKey(AtrDate(r, "Report_Date"))] = text;
                    if (text == AtrStatus.Complied) c++;
                    else if (text == AtrStatus.NotComplied) nc++;
                    else ns++;
                }
                row["cnt_c"] = c; row["cnt_nc"] = nc; row["cnt_ns"] = ns;
                row["pct"] = AtrPct(c, c + nc + ns);
                grid.Rows.Add(row);
            }

            // Day-wise counts across the region's branches
            var labels = new (string label, string status, string totalKey)[]
            {
                ("Day wise count - Submitted-complied", AtrStatus.Complied, "cnt_c"),
                ("Day wise count - Submitted-not complied", AtrStatus.NotComplied, "cnt_nc"),
                ("Day wise count - Not submitted", AtrStatus.NotSubmitted, "cnt_ns")
            };
            foreach (var (label, status, totalKey) in labels)
            {
                var f = new Dictionary<string, object> { ["branch_name"] = label, ["branch_code"] = "", ["sol_id"] = "" };
                foreach (var d in grid.Days)
                    f[AtrDayKey(d)] = grid.Rows.Count(r => (string)r[AtrDayKey(d)] == status);
                f["cnt_c"] = ""; f["cnt_nc"] = ""; f["cnt_ns"] = ""; f["pct"] = "";
                f[totalKey] = grid.Rows.Sum(r => (int)r[totalKey]);
                grid.FooterRows.Add(f);
            }
            return grid;
        }

        /// <summary>Report-2: Branch-wise monthly submission status (one branch, one row per day).</summary>
        public async Task<AtrGrid> GetAtrMonthlyBranchGridAsync(DateTime month, string solId)
        {
            var p = AtrMonthParams(month);
            p["@Sol_Id"] = solId;

            var sets = await _report.ExecuteStoredProcedureMultiAsync("usp_getATRMonthlyBranchReport", p);
            var grid = new AtrGrid();
            if (sets.Count == 0 || sets[0].Count == 0) return grid;

            var h = sets[0][0];
            grid.Header = new Dictionary<string, object>
            {
                ["branch_name"] = AtrStr(h, "Branch_Name"),
                ["branch_code"] = AtrStr(h, "Branch_Code"),
                ["sol_id"] = AtrStr(h, "Sol_Id"),
                ["ro_code"] = AtrStr(h, "RO_Code"),
                ["ro_name"] = AtrStr(h, "RO_Name"),
                ["zone_name"] = AtrStr(h, "Zone_Name")
            };

            grid.Columns.Add(new ColumnConfig { Header = "Date", PropertyName = "date" });
            grid.Columns.Add(new ColumnConfig { Header = "Submission Status", PropertyName = "status" });

            int c = 0, nc = 0, ns = 0;
            if (sets.Count > 1)
            {
                foreach (var r in sets[1])
                {
                    var d = AtrDate(r, "Report_Date");
                    var text = AtrStatus.FromCode(AtrStr(r, "Status_Code"));
                    grid.Days.Add(d);
                    grid.Rows.Add(new Dictionary<string, object> { ["date"] = d.ToString("dd-MM-yyyy"), ["status"] = text });
                    if (text == AtrStatus.Complied) c++;
                    else if (text == AtrStatus.NotComplied) nc++;
                    else ns++;
                }
            }
            grid.FooterRows.Add(new() { ["date"] = "Branch-wise count for the month - " + AtrStatus.Complied, ["status"] = c });
            grid.FooterRows.Add(new() { ["date"] = "Branch-wise count for the month - " + AtrStatus.NotComplied, ["status"] = nc });
            grid.FooterRows.Add(new() { ["date"] = "Branch-wise count for the month - " + AtrStatus.NotSubmitted, ["status"] = ns });
            return grid;
        }

        /// <summary>Raw region x date counts (Report-3 and Report-4 are both built from this).</summary>
        public Task<List<Dictionary<string, object>>> GetAtrMonthlyRegionSummaryAsync(DateTime month, string? zoneSolid, string? regionSolid)
        {
            var p = AtrMonthParams(month);
            p["@Zone_Solid"] = string.IsNullOrWhiteSpace(zoneSolid) ? (object)DBNull.Value : zoneSolid!;
            p["@Region_Solid"] = string.IsNullOrWhiteSpace(regionSolid) ? (object)DBNull.Value : regionSolid!;
            return _report.ExecuteStoredProcAsync("usp_getATRMonthlyRegionSummary", p, 90);
        }

        /// <summary>Report-3: RO-wise monthly consolidated (regions x dates x 3 counts + month totals).</summary>
        public AtrGrid BuildAtrRegionDayGrid(List<Dictionary<string, object>> rows)
        {
            var grid = new AtrGrid();
            if (rows.Count == 0) return grid;

            grid.Days = rows.Select(r => AtrDate(r, "Report_Date")).Distinct().OrderBy(d => d).ToList();

            grid.Columns.Add(new ColumnConfig { Header = "Zone", PropertyName = "zone_name" });
            grid.Columns.Add(new ColumnConfig { Header = "Region", PropertyName = "region_name" });
            foreach (var d in grid.Days)
            {
                var k = AtrDayKey(d);
                var h = d.ToString("dd-MM-yy");
                grid.Columns.Add(new ColumnConfig { Header = h + " - Submitted-complied", PropertyName = k + "_c", IsNumeric = true, Format = "N0" });
                grid.Columns.Add(new ColumnConfig { Header = h + " - Submitted-not complied", PropertyName = k + "_nc", IsNumeric = true, Format = "N0" });
                grid.Columns.Add(new ColumnConfig { Header = h + " - Not submitted", PropertyName = k + "_ns", IsNumeric = true, Format = "N0" });
            }
            grid.Columns.Add(new ColumnConfig { Header = "Month - Submitted-complied", PropertyName = "cnt_c", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Month - Submitted-not complied", PropertyName = "cnt_nc", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Month - Not submitted", PropertyName = "cnt_ns", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Compliance %", PropertyName = "pct", IsNumeric = true, Format = "0.0" });

            foreach (var g in rows.GroupBy(r => AtrStr(r, "Region_Solid"))
                                  .OrderBy(g => AtrStr(g.First(), "Zone_Name"), StringComparer.OrdinalIgnoreCase)
                                  .ThenBy(g => AtrStr(g.First(), "Region_Name"), StringComparer.OrdinalIgnoreCase))
            {
                var first = g.First();
                var row = new Dictionary<string, object>
                {
                    ["zone_name"] = AtrStr(first, "Zone_Name"),
                    ["region_name"] = AtrStr(first, "Region_Name")
                };
                int c = 0, nc = 0, ns = 0;
                foreach (var d in grid.Days) { var k = AtrDayKey(d); row[k + "_c"] = 0; row[k + "_nc"] = 0; row[k + "_ns"] = 0; }
                foreach (var r in g)
                {
                    var k = AtrDayKey(AtrDate(r, "Report_Date"));
                    int rc = AtrInt(r, "Complied"), rnc = AtrInt(r, "Not_Complied"), rns = AtrInt(r, "Not_Submitted");
                    row[k + "_c"] = rc; row[k + "_nc"] = rnc; row[k + "_ns"] = rns;
                    c += rc; nc += rnc; ns += rns;
                }
                row["cnt_c"] = c; row["cnt_nc"] = nc; row["cnt_ns"] = ns;
                row["pct"] = AtrPct(c, c + nc + ns);
                grid.Rows.Add(row);
            }

            var f = new Dictionary<string, object> { ["zone_name"] = "Day wise count for all Regions", ["region_name"] = "" };
            foreach (var col in grid.Columns.Skip(2))
            {
                if (col.PropertyName == "pct") continue;
                f[col.PropertyName] = grid.Rows.Sum(r => (int)r[col.PropertyName]);
            }
            f["pct"] = AtrPct((int)f["cnt_c"], (int)f["cnt_c"] + (int)f["cnt_nc"] + (int)f["cnt_ns"]);
            grid.FooterRows.Add(f);
            return grid;
        }

        /// <summary>Report-4: Region-wise monthly totals for a zone (sums of branch-days over the month).</summary>
        public AtrGrid BuildAtrRegionMonthGrid(List<Dictionary<string, object>> rows)
        {
            var grid = new AtrGrid();
            if (rows.Count == 0) return grid;

            grid.Columns.Add(new ColumnConfig { Header = "Zone", PropertyName = "zone_name" });
            grid.Columns.Add(new ColumnConfig { Header = "Region Name", PropertyName = "region_name" });
            grid.Columns.Add(new ColumnConfig { Header = "Submitted-Complied", PropertyName = "cnt_c", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Submitted-Not complied", PropertyName = "cnt_nc", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Not submitted", PropertyName = "cnt_ns", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Total", PropertyName = "total", IsNumeric = true, Format = "N0" });
            grid.Columns.Add(new ColumnConfig { Header = "Compliance %", PropertyName = "pct", IsNumeric = true, Format = "0.0" });

            foreach (var g in rows.GroupBy(r => AtrStr(r, "Region_Solid"))
                                  .OrderBy(g => AtrStr(g.First(), "Zone_Name"), StringComparer.OrdinalIgnoreCase)
                                  .ThenBy(g => AtrStr(g.First(), "Region_Name"), StringComparer.OrdinalIgnoreCase))
            {
                var first = g.First();
                int c = g.Sum(r => AtrInt(r, "Complied"));
                int nc = g.Sum(r => AtrInt(r, "Not_Complied"));
                int ns = g.Sum(r => AtrInt(r, "Not_Submitted"));
                grid.Rows.Add(new Dictionary<string, object>
                {
                    ["zone_name"] = AtrStr(first, "Zone_Name"),
                    ["region_name"] = AtrStr(first, "Region_Name"),
                    ["cnt_c"] = c, ["cnt_nc"] = nc, ["cnt_ns"] = ns,
                    ["total"] = c + nc + ns,
                    ["pct"] = AtrPct(c, c + nc + ns)
                });
            }

            int tc = grid.Rows.Sum(r => (int)r["cnt_c"]);
            int tnc = grid.Rows.Sum(r => (int)r["cnt_nc"]);
            int tns = grid.Rows.Sum(r => (int)r["cnt_ns"]);
            grid.FooterRows.Add(new Dictionary<string, object>
            {
                ["zone_name"] = "TOTAL", ["region_name"] = "",
                ["cnt_c"] = tc, ["cnt_nc"] = tnc, ["cnt_ns"] = tns,
                ["total"] = tc + tnc + tns, ["pct"] = AtrPct(tc, tc + tnc + tns)
            });
            return grid;
        }

        /// <summary>Passthrough so report pages only need to inject this one service.</summary>
        public byte[] ExportToExcel(List<Dictionary<string, object>> data, List<ColumnConfig> columns, string sheetName)
            => _report.ExportToExcel(data, columns, sheetName);
    }
}
