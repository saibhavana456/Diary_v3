using ClosedXML.Excel;
using MyDiary.Web.Features.BcpDrill.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyDiary.Web.Features.BcpDrill.Services;

/// <summary>PDF certificates (Annexure-I / Annexure-II) and MIS exports (Excel + PDF) for the BCP/DR module.</summary>
public static class BcpDocuments
{
    private const string Navy = "#1E3A8A", Grey = "#6B7280", Line = "#9CA3AF", Head = "#EAF0FB";

    public static byte[]? LoadLogo(string webRoot, string file)
    {
        try
        {
            var p = Path.Combine(webRoot, "images", file);
            return File.Exists(p) ? File.ReadAllBytes(p) : null;
        }
        catch { return null; }
    }

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s;
    private static string Amt(decimal? v) => v.HasValue ? v.Value.ToString("N2") : "-";

    private static IContainer Cell(IContainer c, bool head = false) =>
        c.Border(0.5f).BorderColor(Line).Background(head ? Head : Colors.White).Padding(4);

    // ── Annexure-I ──────────────────────────────────────────────────────────────────────────────
    public static byte[] DrillPdf(BcpDrillEntry d, byte[]? ubiLogo, byte[]? appLogo)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(t => t.FontSize(9.5f).FontFamily("Arial"));

            page.Header().Column(h =>
            {
                h.Item().Row(r =>
                {
                    if (appLogo is not null) r.ConstantItem(70).Height(34).Image(appLogo).FitArea(); else r.ConstantItem(70);
                    r.RelativeItem().AlignCenter().Column(c =>
                    {
                        c.Item().AlignCenter().Text("Annexure - I").Bold().FontSize(10).FontColor(Grey);
                        c.Item().AlignCenter().Text("Format for BCP testing / DR drill of Branch / Office").Bold().FontSize(13).FontColor(Navy);
                    });
                    if (ubiLogo is not null) r.ConstantItem(100).Height(30).AlignRight().Image(ubiLogo).FitArea(); else r.ConstantItem(100);
                });
                h.Item().PaddingTop(4).AlignCenter().Text($"(Branch Name: {d.BranchName}    Sol Id: {d.SolId})").FontSize(10);
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Spacing(8);
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Drill type: {BcpTypes.Label(d.DrillType)}").Bold();
                    r.RelativeItem().AlignCenter().Text($"Financial year: {Dash(d.FinYear)}");
                    r.RelativeItem().AlignRight().Text($"Ref: {d.RefNo}").FontColor(Grey);
                });

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(x => { x.RelativeColumn(2.2f); x.RelativeColumn(3); x.RelativeColumn(3); });
                    t.Header(h =>
                    {
                        h.Cell().Element(c => Cell(c, true)).Text("Particulars").Bold();
                        h.Cell().Element(c => Cell(c, true)).Text("Branch identified for BCP testing").Bold();
                        h.Cell().Element(c => Cell(c, true)).Text("Branch identified where the operations will be carried out").Bold();
                    });
                    void Row(string label, string? a, string? b)
                    {
                        t.Cell().Element(c => Cell(c)).Text(label);
                        t.Cell().Element(c => Cell(c)).Text(Dash(a));
                        t.Cell().Element(c => Cell(c)).Text(Dash(b));
                    }
                    Row("Name of the Branch", d.BranchName, d.AltBranchName);
                    Row("Branch Code", d.BranchCode ?? d.SolId, d.AltBranchCode ?? d.AltSolId);
                    Row("Region", d.RegionName ?? d.RegionCode, d.AltRegion);
                    Row("Zone", d.ZoneName ?? d.ZoneCode, d.AltZone);
                    Row("Category", d.Category, d.AltCategory);
                    Row("No of Staff (Award staff / Officers)", d.StaffNormal?.ToString(), d.StaffAlt?.ToString());

                    t.Cell().Element(c => Cell(c, true)).Text("Particulars of Operations").Bold();
                    t.Cell().Element(c => Cell(c, true)).Text("Details of Branch operations (in terms of number, amount etc) during normal days").Bold().FontSize(8.5f);
                    t.Cell().Element(c => Cell(c, true)).Text("Details of Branch operations (in terms of number, amount etc) from where the operations will be carried out").Bold().FontSize(8.5f);

                    foreach (var def in BcpMetrics.All)
                    {
                        var m = d.Metrics.FirstOrDefault(x => x.Code == def.Code);
                        string Fmt(long? n, decimal? a) => def.HasAmount
                            ? (n is null && a is null ? "-" : $"No: {(n?.ToString("N0") ?? "-")}   Amt: {Amt(a)}")
                            : (n?.ToString("N0") ?? "-");
                        Row(def.Label, Fmt(m?.NormalCount, m?.NormalAmount), Fmt(m?.AltCount, m?.AltAmount));
                    }
                });

                col.Item().PaddingTop(4).Text(t => { t.Span("Remarks: ").Bold(); t.Span(Dash(d.Remarks)); });
                col.Item().Text(t => { t.Span("BCP testing / DR drill conducted on: ").Bold(); t.Span(d.DrillDate.ToString("dd-MMM-yyyy")); });
                col.Item().Text(t => { t.Span("Compliance certified: ").Bold(); t.Span(d.IsCompliant ? "Yes - drill carried out as per checklist" : "No"); });

                col.Item().PaddingTop(46).Row(r =>
                {
                    r.RelativeItem().Column(c => { c.Item().LineHorizontal(0.6f); c.Item().PaddingTop(2).AlignCenter().Text("Signature of Branch Head").FontSize(9); });
                    r.ConstantItem(20);
                    r.RelativeItem().Column(c => { c.Item().LineHorizontal(0.6f); c.Item().PaddingTop(2).AlignCenter().Text("Signature of FGMO Security officer / BCP In-charge").FontSize(9); });
                    r.ConstantItem(20);
                    r.RelativeItem().Column(c => { c.Item().LineHorizontal(0.6f); c.Item().PaddingTop(2).AlignCenter().Text("Signature of FGMO Risk Officer").FontSize(9); });
                });

                col.Item().PaddingTop(10).Text($"Submitted on {d.EntryAt:dd-MMM-yyyy HH:mm} by {Dash(d.EntryBy)}. Status: {d.Status}"
                    + (d.ReviewAt.HasValue ? $" ({d.ReviewBy}, {d.ReviewAt:dd-MMM-yyyy})" : "")).FontSize(8).FontColor(Grey);
                col.Item().Text("System-generated from My Diary. Take a printout and obtain the physical signatures above where required.").FontSize(8).FontColor(Grey);
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Grey));
                t.Span("Classification: Internal   |   Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages();
            });
        })).GeneratePdf();
    }

    // ── Annexure-II ─────────────────────────────────────────────────────────────────────────────
    public static byte[] CertPdf(BcpRoCert c, byte[]? ubiLogo, byte[]? appLogo)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(t => t.FontSize(9.5f).FontFamily("Arial"));

            page.Header().Row(r =>
            {
                if (appLogo is not null) r.ConstantItem(70).Height(34).Image(appLogo).FitArea(); else r.ConstantItem(70);
                r.RelativeItem().AlignCenter().Column(x =>
                {
                    x.Item().AlignCenter().Text("Annexure - II").Bold().FontSize(10).FontColor(Grey);
                    x.Item().AlignCenter().Text("Certificate of BCP DRILL and linking / mapping of all branches").Bold().FontSize(13).FontColor(Navy);
                });
                if (ubiLogo is not null) r.ConstantItem(100).Height(30).AlignRight().Image(ubiLogo).FitArea(); else r.ConstantItem(100);
            });

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(6);
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("Regional Office / Department: ").Bold(); t.Span(Dash(c.RegionName ?? c.RegionCode)); });
                    r.RelativeItem().Text(t => { t.Span("Zonal Office: ").Bold(); t.Span(Dash(c.ZoneName ?? c.ZoneCode)); });
                    r.RelativeItem().AlignRight().Text(t => { t.Span("Date: ").Bold(); t.Span(c.CertDate.ToString("dd-MMM-yyyy")); });
                });

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(x =>
                    {
                        x.ConstantColumn(28); x.RelativeColumn(2.4f); x.RelativeColumn(1.6f); x.ConstantColumn(64);
                        x.RelativeColumn(2); x.RelativeColumn(2); x.RelativeColumn(1.6f);
                    });
                    t.Header(h =>
                    {
                        foreach (var s in new[] { "S. No.", "Name of identified Branch", "Name of Disaster", "BCP Drill Date", "Mapped / Linked Branch 1", "Mapped / Linked Branch 2", "Remarks" })
                            h.Cell().Element(x => Cell(x, true)).Text(s).Bold().FontSize(8.5f);
                    });
                    foreach (var l in c.Lines)
                    {
                        t.Cell().Element(x => Cell(x)).Text(l.SrNo.ToString());
                        t.Cell().Element(x => Cell(x)).Text($"{l.BranchName} ({l.SolId})");
                        t.Cell().Element(x => Cell(x)).Text(Dash(l.DisasterName));
                        t.Cell().Element(x => Cell(x)).Text(l.DrillDate?.ToString("dd-MMM-yy") ?? "-");
                        t.Cell().Element(x => Cell(x)).Text(l.Map1Name is null ? "-" : $"{l.Map1Name}{(l.Map1SolId is null ? "" : $" ({l.Map1SolId})")}");
                        t.Cell().Element(x => Cell(x)).Text(l.Map2Name is null ? "-" : $"{l.Map2Name}{(l.Map2SolId is null ? "" : $" ({l.Map2SolId})")}");
                        t.Cell().Element(x => Cell(x)).Text(Dash(l.Remarks));
                    }
                });

                col.Item().ShowEntire().PaddingTop(8).Column(x =>
                {
                    x.Item().Text("We confirm that all above mentioned Branches have carried out BCP Drill as per checklist and have been Mapped / Linked to nearby two Branches. "
                        + "All the precautions have been taken for Business continuation during BCP Drill. In case of disaster occurrence, Branch operations will be carried out from Linked / Mapped Branch.");
                    x.Item().PaddingTop(34).AlignRight().Column(s =>
                    {
                        s.Item().AlignRight().Text("Regional Head / Regional Assurance Head / Vertical Head").Bold();
                        s.Item().AlignRight().PaddingTop(2).Text($"(Name: {Dash(c.SignatoryName)})");
                    });
                    x.Item().PaddingTop(6).Text($"Certificate no. RO/{c.RegionCode}/{c.Id}; recorded {c.EntryAt:dd-MMM-yyyy HH:mm} by {Dash(c.EntryBy)}. Classification: Confidential")
                        .FontSize(8).FontColor(Grey);
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Grey));
                t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages();
            });
        })).GeneratePdf();
    }

    // ── MIS: shared table shapes ────────────────────────────────────────────────────────────────
    public static (string[] Headers, List<string[]> Rows) BranchTable(IEnumerable<BcpBranchMisRow> rows) => (
        new[] { "Sr", "Branch Name", "Branch Code", "SOL ID", "RO Name", "RO Code", "ZO Name", "ZO Code", "Drill Type", "BCP Drill Date", "BCP Branch Name", "BCP Branch SOL ID", "Status" },
        rows.Select((r, i) => new[]
        {
            (i + 1).ToString(), r.BranchName, r.BranchCode ?? "", r.SolId, r.RegionName ?? "", r.RegionCode ?? "", r.ZoneName ?? "", r.ZoneCode ?? "",
            r.DrillType is null ? "" : BcpTypes.Label(r.DrillType), r.DrillDate?.ToString("dd-MMM-yyyy") ?? "", r.AltBranchName ?? "", r.AltSolId ?? "", r.ComplianceLabel
        }).ToList());

    public static (string[] Headers, List<string[]> Rows) RegionTable(IEnumerable<BcpZoneMisRow> zones)
    {
        var rows = new List<string[]>();
        foreach (var z in zones)
        {
            foreach (var r in z.Regions)
                rows.Add(new[] { z.ZoneName ?? z.ZoneCode, r.RegionName ?? r.RegionCode, r.MappedBranches.ToString(), r.Submitted.ToString(), r.Pending.ToString(),
                                 r.CompliancePct.ToString("N1") + "%", r.Acknowledged.ToString(), r.RoCertificates.ToString() });
            rows.Add(new[] { (z.ZoneName ?? z.ZoneCode) + " - TOTAL", "", z.MappedBranches.ToString(), z.Submitted.ToString(), z.Pending.ToString(), z.CompliancePct.ToString("N1") + "%", "", "" });
        }
        return (new[] { "Zone", "Region (RO)", "Mapped Branches", "Submissions", "Pending", "Compliance %", "Acknowledged", "RO Certificates" }, rows);
    }

    // ── MIS: Excel ──────────────────────────────────────────────────────────────────────────────
    public static byte[] Xlsx(string sheetName, string title, string subtitle, string[] headers, List<string[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName.Length > 31 ? sheetName[..31] : sheetName);
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = subtitle;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(4, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF0FB");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(5 + r, c + 1);
                var s = c < rows[r].Length ? rows[r][c] : "";
                if (long.TryParse(s, out var n) && s.Length < 12 && !headers[c].Contains("SOL") && !headers[c].Contains("Code")) cell.Value = n; else cell.Value = s;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        ws.SheetView.FreezeRows(4);
        ws.Columns().AdjustToContents(4, 4 + rows.Count, 8, 50);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── MIS: PDF ────────────────────────────────────────────────────────────────────────────────
    public static byte[] MisPdf(string title, string subtitle, string[] headers, List<string[]> rows, byte[]? ubiLogo, byte[]? appLogo)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(t => t.FontSize(8).FontFamily("Arial"));

            page.Header().PaddingBottom(6).Row(r =>
            {
                if (appLogo is not null) r.ConstantItem(60).Height(30).Image(appLogo).FitArea(); else r.ConstantItem(60);
                r.RelativeItem().AlignCenter().Column(c =>
                {
                    c.Item().AlignCenter().Text(title).Bold().FontSize(13).FontColor(Navy);
                    c.Item().AlignCenter().Text(subtitle).FontSize(8).FontColor(Grey);
                });
                if (ubiLogo is not null) r.ConstantItem(90).Height(26).AlignRight().Image(ubiLogo).FitArea(); else r.ConstantItem(90);
            });

            page.Content().Table(t =>
            {
                t.ColumnsDefinition(x =>
                {
                    foreach (var h in headers)
                    {
                        if (h == "Sr") x.ConstantColumn(22);
                        else if (h.Contains("Name") || h.StartsWith("Zone") || h.StartsWith("Region")) x.RelativeColumn(1.7f);
                        else x.RelativeColumn();
                    }
                });
                t.Header(h => { foreach (var s in headers) h.Cell().Element(x => Cell(x, true)).Text(s).Bold().FontSize(7.5f); });
                foreach (var r in rows)
                    for (var c = 0; c < headers.Length; c++)
                        t.Cell().Element(x => Cell(x)).Text(c < r.Length ? r[c] : "");
            });

            page.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Generated {DateTime.Now:dd-MMM-yyyy HH:mm}   |   Classification: Internal").FontSize(7).FontColor(Grey);
                r.ConstantItem(90).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Grey));
                    t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages();
                });
            });
        })).GeneratePdf();
    }
}
