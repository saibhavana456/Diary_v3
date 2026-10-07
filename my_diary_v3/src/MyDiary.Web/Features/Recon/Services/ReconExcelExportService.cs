using ClosedXML.Excel;
using RequestPortal.Core.Models;

namespace MyDiary.Web.Features.Recon.Services;

/// <summary>Exact port of the source app's ExcelExportUtility.GeneratePendingBranchesExcel — same
/// ClosedXML library, same columns/header styling, only the row source type differs (a plain tuple here
/// instead of the source's BRANCH_MASTERDto, since that class doesn't exist in my_diary_v3_1).</summary>
public static class ReconExcelExportService
{
    public static byte[] GeneratePendingBranchesExcel(
        IReadOnlyList<(string BranchCode, string BranchName, string Status, string? RegionCode, string? RegionName, string? ZoneCode, string? ZoneName)> branches,
        string year, string month)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add($"Pending_{month}_{year}");

        worksheet.Cell(1, 1).Value = "Branch Code";
        worksheet.Cell(1, 2).Value = "Branch Name";
        worksheet.Cell(1, 3).Value = "Status";
        worksheet.Cell(1, 4).Value = "Region Code";
        worksheet.Cell(1, 5).Value = "Region Name";
        worksheet.Cell(1, 6).Value = "Zone Code";
        worksheet.Cell(1, 7).Value = "Zone Name";

        var headerRange = worksheet.Range("A1:G1");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3");

        int row = 2;
        foreach (var b in branches)
        {
            worksheet.Cell(row, 1).Value = b.BranchCode;
            worksheet.Cell(row, 2).Value = b.BranchName;
            worksheet.Cell(row, 3).Value = b.Status;
            worksheet.Cell(row, 4).Value = b.RegionCode;
            worksheet.Cell(row, 5).Value = b.RegionName;
            worksheet.Cell(row, 6).Value = b.ZoneCode;
            worksheet.Cell(row, 7).Value = b.ZoneName;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Same pattern, for the ZO Operations pending-regions list (no source equivalent — the
    /// source app never had a ZO-level pending-regions export button, but the shape is identical).</summary>
    public static byte[] GeneratePendingRegionsExcel(IReadOnlyList<(string RegionCode, string RegionName)> regions, string year, string month)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add($"Pending_{month}_{year}");

        worksheet.Cell(1, 1).Value = "Region Code";
        worksheet.Cell(1, 2).Value = "Region Name";

        var headerRange = worksheet.Range("A1:B1");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3");

        int row = 2;
        foreach (var r in regions)
        {
            worksheet.Cell(row, 1).Value = r.RegionCode;
            worksheet.Cell(row, 2).Value = r.RegionName;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Excel export of the RO/ZO consolidated certificate itself — a new addition beyond the
    /// source app (which only ever exported pending lists to Excel, never the certificate figures). Two
    /// sheets: the same aggregated Sundry/Suspense/POB/Inter-Bank summary shown on the consolidated PDF's
    /// page 1, and the unit roster shown on page 2 — same data, spreadsheet form for further analysis.</summary>
    public static byte[] GenerateConsolidatedExcel(
        ReconAnnexure4 aggregated, string ownerLabel, string ownerName, string parentLabel, string parentName,
        int totalUnits, int unbalancedUnits, string unitLabel,
        IReadOnlyList<(string Code, string Name, string? Head, string? Mobile, string? Ip)> unitDetails)
    {
        using var workbook = new XLWorkbook();

        // ── Sheet 1: Certificate summary ────────────────────────────────────────────────────
        var summary = workbook.Worksheets.Add("Certificate Summary");
        summary.Cell(1, 1).Value = $"{ownerLabel}:"; summary.Cell(1, 2).Value = ownerName;
        summary.Cell(2, 1).Value = $"{parentLabel}:"; summary.Cell(2, 2).Value = parentName;
        summary.Cell(3, 1).Value = "Period:"; summary.Cell(3, 2).Value = $"{aggregated.ReportMonth} {aggregated.ReportYear}";
        summary.Cell(4, 1).Value = $"Total {unitLabel}s:"; summary.Cell(4, 2).Value = totalUnits;
        summary.Cell(5, 1).Value = "Unbalanced SOLs:"; summary.Cell(5, 2).Value = unbalancedUnits;
        summary.Range("A1:A5").Style.Font.Bold = true;

        int r = 7;
        summary.Cell(r, 1).Value = "Category";
        summary.Cell(r, 2).Value = "Upto 3M Entries"; summary.Cell(r, 3).Value = "Upto 3M Amount";
        summary.Cell(r, 4).Value = "3-6M Entries"; summary.Cell(r, 5).Value = "3-6M Amount";
        summary.Cell(r, 6).Value = "Over 6M Entries"; summary.Cell(r, 7).Value = "Over 6M Amount";
        summary.Cell(r, 8).Value = "New Over 6M Entries"; summary.Cell(r, 9).Value = "New Over 6M Amount";
        summary.Cell(r, 10).Value = "Reason"; summary.Cell(r, 11).Value = "Efforts";
        var headerRow = summary.Range(r, 1, r, 11);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3");
        r++;

        void AddCategoryRow(string label, long? e1, decimal? a1, long? e2, decimal? a2, long? e3, decimal? a3, long? e4, decimal? a4, string? reason, string? efforts)
        {
            summary.Cell(r, 1).Value = label;
            summary.Cell(r, 2).Value = e1 ?? 0; summary.Cell(r, 3).Value = a1 ?? 0;
            summary.Cell(r, 4).Value = e2 ?? 0; summary.Cell(r, 5).Value = a2 ?? 0;
            summary.Cell(r, 6).Value = e3 ?? 0; summary.Cell(r, 7).Value = a3 ?? 0;
            summary.Cell(r, 8).Value = e4 ?? 0; summary.Cell(r, 9).Value = a4 ?? 0;
            summary.Cell(r, 10).Value = reason ?? "-"; summary.Cell(r, 11).Value = efforts ?? "-";
            r++;
        }

        AddCategoryRow("Sundry", aggregated.SunUpto3MEntries, aggregated.SunUpto3MAmount, aggregated.SunThree6MEntries, aggregated.SunThree6MAmount,
            aggregated.SunOver6MEntries, aggregated.SunOver6MAmount, aggregated.SunNewOver6MEntries, aggregated.SunNewOver6MAmount, aggregated.SunReason, aggregated.SunEfforts);
        AddCategoryRow("Suspense", aggregated.SusUpto3MEntries, aggregated.SusUpto3MAmount, aggregated.SusThree6MEntries, aggregated.SusThree6MAmount,
            aggregated.SusOver6MEntries, aggregated.SusOver6MAmount, aggregated.SusNewOver6MEntries, aggregated.SusNewOver6MAmount, aggregated.SusReason, aggregated.SusEfforts);
        AddCategoryRow("POB", aggregated.PobUpto3MEntries, aggregated.PobUpto3MAmount, aggregated.PobThree6MEntries, aggregated.PobThree6MAmount,
            aggregated.PobOver6MEntries, aggregated.PobOver6MAmount, aggregated.PobNewOver6MEntries, aggregated.PobNewOver6MAmount, aggregated.PobReason, aggregated.PobEfforts);
        AddCategoryRow("Inter Bank", aggregated.IbkUpto3MEntries, aggregated.IbkUpto3MAmount, aggregated.IbkThree6MEntries, aggregated.IbkThree6MAmount,
            aggregated.IbkOver6MEntries, aggregated.IbkOver6MAmount, aggregated.IbkNewOver6MEntries, aggregated.IbkNewOver6MAmount, aggregated.IbkReason, aggregated.IbkEfforts);

        summary.Columns().AdjustToContents();

        // ── Sheet 2: Unit roster ────────────────────────────────────────────────────────────
        var roster = workbook.Worksheets.Add($"{unitLabel} Details");
        roster.Cell(1, 1).Value = $"{unitLabel} Code";
        roster.Cell(1, 2).Value = $"{unitLabel} Name";
        roster.Cell(1, 3).Value = "Head";
        roster.Cell(1, 4).Value = "Mobile No";
        roster.Cell(1, 5).Value = "IP No";
        var rosterHeader = roster.Range("A1:E1");
        rosterHeader.Style.Font.Bold = true;
        rosterHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3");

        int rr = 2;
        foreach (var u in unitDetails)
        {
            roster.Cell(rr, 1).Value = u.Code;
            roster.Cell(rr, 2).Value = u.Name;
            roster.Cell(rr, 3).Value = u.Head ?? "-";
            roster.Cell(rr, 4).Value = u.Mobile ?? "-";
            roster.Cell(rr, 5).Value = u.Ip ?? "-";
            rr++;
        }
        roster.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
