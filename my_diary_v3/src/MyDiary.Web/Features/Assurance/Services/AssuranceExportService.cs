using System.Text;
using ClosedXML.Excel;
using MyDiary.Core.Services;
using MyDiary.Web.Features.Assurance.Models;
using MyDiary.Web.Features.Focus360.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>
/// Exports the Assurance Snapshot (Excel / CSV / PDF) from the same data-driven panel grid
/// (<see cref="AssuranceCategory"/>) the page renders — see AssurancePanelService and
/// db/sqlserver/GAP_04_assurance_panels.sql. The report header mirrors Focus 360 (two logos +
/// centred title/bank/as-on). Categories render as red bands, panels within a category as
/// side-by-side blue-titled tables with their own column set, scaled to a single A4 page —
/// same layout the on-screen page and the source Excel/PDF use.
/// </summary>
public sealed class AssuranceExportService
{



    // ── Excel ────────────────────────────────────────────────────────────────
   

    public byte[] ExportToExcel(
    GapReportViewModel r,
    List<AssuranceCategory> categories,
    List<F360CategoryColor> categoryColors,
    string title,
    string bankName,
    string? appLogo,
    string? coLogo)
    {
        using var wb = new XLWorkbook();

        var ws = wb.Worksheets.Add("Assurance Snapshot");

        const int lastCol = 16;

        // =====================================================
        // TITLE HEADER
        // =====================================================

        ws.Range(1, 1, 3, lastCol).Merge();

        var titleCell = ws.Cell(1, 1);
        titleCell.Value = title;

        titleCell.Style.Fill.BackgroundColor =
            XLColor.White;

        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontColor = XLColor.FromHtml("#0b4ea2");
        titleCell.Style.Font.FontSize = 16;

        titleCell.Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;

        titleCell.Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;

        titleCell.Style.Alignment.WrapText = true;

        ws.Row(1).Height = 24;
        ws.Row(2).Height = 20;
        ws.Row(3).Height = 20;

        // Logos
        TryAddPicture(ws, appLogo, 1, 1, 110, 40);
        TryAddPicture(ws, coLogo, 1, 14, 120, 40);

        // =====================================================
        // BANK NAME
        // =====================================================

        ws.Range(4, 1, 4, lastCol).Merge();

        var bankCell = ws.Cell(4, 1);

        bankCell.Value = bankName;

        bankCell.Style.Font.Bold = true;
        bankCell.Style.Font.FontSize = 11;

        bankCell.Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;

        // =====================================================
        // BRANCH INFO BAND
        // =====================================================

        int row = 6;

        ws.Range(row, 1, row, lastCol).Merge();

        var infoBand = ws.Cell(row, 1);

        infoBand.Value =
            $"Branch / ZO Details   As on {(r.AsOnDate ?? AppTime.Today):dd-MMM-yyyy}";

        infoBand.Style.Fill.BackgroundColor =
            XLColor.FromHtml("#c8102e");

        infoBand.Style.Font.Bold = true;
        infoBand.Style.Font.FontColor = XLColor.White;
        infoBand.Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;

        row++;

        // =====================================================
        // BRANCH HEADER TABLE
        // =====================================================

        string[] hdr =
        {
        "Branch",
        "Code",
        "Zone",
        "Region",
        "BM",
        "Working Since",
        "Assurance Head",
        "Working Since"
    };

        for (int i = 0; i < hdr.Length; i++)
        {
            var c = ws.Cell(row, i + 1);

            c.Value = hdr[i];

            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#eef1f8");

            c.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            ApplyBorder(c);
        }

        row++;

        string[] vals =
        {
        r.Branch.BranchName,
        r.Branch.BranchCode,
        r.Branch.ZoneName,
        r.Branch.RegionName,
        r.Branch.ZMBMName,
        r.Branch.WorkingSince?.ToString("dd-MMM-yyyy") ?? "",
        "",
        ""
    };

        for (int i = 0; i < vals.Length; i++)
        {
            var c = ws.Cell(row, i + 1);

            c.Value = vals[i];

            ApplyBorder(c);
        }

        row += 2;

        // =====================================================
        // CATEGORIES
        // =====================================================

        foreach (var cat in categories)
        {
            ws.Range(row, 1, row, lastCol).Merge();

            var band = ws.Cell(row, 1);

            band.Value = cat.Label;

            //band.Style.Fill.BackgroundColor =
            //    XLColor.FromHtml("#c0143c");

            band.Style.Fill.BackgroundColor =
            XLColor.FromHtml(
            GetPanelColor(
            cat.Label,
            categoryColors));

            band.Style.Font.Bold = true;
            band.Style.Font.FontColor = XLColor.White;
            band.Style.Font.FontSize = 9;

            row++;

            foreach (var group in cat.PanelRowGroups)
            {
                int panelCount = Math.Max(1, group.Panels.Count);

                int span = lastCol / panelCount;

                int currentColumn = 1;

                int maxRow = row;

                foreach (var panel in group.Panels)
                {
                    int panelEndRow =
                        RenderExcelPanel(
                            ws,
                            panel,
                            categoryColors,
                            currentColumn,
                            row,
                            span);

                    maxRow = Math.Max(maxRow, panelEndRow);

                    currentColumn += span;
                }

                row = maxRow + 1;
            }

            row++;
        }

        // =====================================================
        // COLUMN WIDTHS
        // =====================================================

        for (int c = 1; c <= lastCol; c++)
        {
            ws.Column(c).Width = 12;
        }

        // =====================================================
        // SHEET SETTINGS
        // =====================================================

        ws.SheetView.FreezeRows(8);

        ws.PageSetup.PageOrientation =
            XLPageOrientation.Landscape;

        ws.PageSetup.PaperSize =
            XLPaperSize.A4Paper;

        ws.PageSetup.CenterHorizontally = true;

        ws.PageSetup.PagesWide = 1;
        ws.PageSetup.PagesTall = 0;

        ws.PageSetup.Margins.Left = 0.2;
        ws.PageSetup.Margins.Right = 0.2;
        ws.PageSetup.Margins.Top = 0.3;
        ws.PageSetup.Margins.Bottom = 0.3;

        using var ms = new MemoryStream();

        wb.SaveAs(ms);

        return ms.ToArray();
    }
    private static int RenderExcelPanel(
    IXLWorksheet ws,
    AssurancePanel panel,
    List<F360CategoryColor> categoryColors,
    int c0,
    int startRow,
    int span)
    {
        int last = c0 + span - 1;

        int row = startRow;

        bool hasRowLabel =
            panel.Rows.Any(x =>
                !string.IsNullOrWhiteSpace(x.RowLabel));

        ws.Range(row, c0, row, last).Merge();

        var title = ws.Cell(row, c0);

        title.Value = panel.Title;

        title.Style.Fill.BackgroundColor =
        XLColor.FromHtml(
        GetPanelColor(
            panel.Title,
            categoryColors));

        //title.Style.Fill.BackgroundColor =
        //    XLColor.FromHtml("#1b4a98");

        title.Style.Font.Bold = true;
        title.Style.Font.FontColor = XLColor.White;

        row++;

        var headers = new List<string>();

        if (hasRowLabel)
            headers.Add(panel.RowLabelHeader ?? "Parameter");

        headers.AddRange(panel.ColumnHeaders);

        WriteExcelRow(ws, row, c0, headers, true);

        row++;

        foreach (var r in panel.Rows)
        {
            var values = new List<string>();

            if (hasRowLabel)
                values.Add(r.RowLabel);

            values.AddRange(r.Cells);

            WriteExcelRow(ws, row, c0, values);

            row++;
        }

        return row;
    }

    private static void WriteExcelRow(
    IXLWorksheet ws,
    int row,
    int c0,
    List<string> vals,
    bool header = false)
    {
        for (int i = 0; i < vals.Count; i++)
        {
            var cell = ws.Cell(row, c0 + i);

            cell.Value = vals[i];

            cell.Style.Font.FontSize = 8;

            ApplyBorder(cell);

            if (header)
            {
                cell.Style.Font.Bold = true;

                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#f4f6fb");
            }
            else
            {
                cell.Style.Alignment.Horizontal =
                    i == 0
                        ? XLAlignmentHorizontalValues.Left
                        : XLAlignmentHorizontalValues.Right;
            }
        }
    }

    private static void ApplyBorder(IXLCell cell)
    {
        cell.Style.Border.TopBorder =
            XLBorderStyleValues.Thin;

        cell.Style.Border.BottomBorder =
            XLBorderStyleValues.Thin;

        cell.Style.Border.LeftBorder =
            XLBorderStyleValues.Thin;

        cell.Style.Border.RightBorder =
            XLBorderStyleValues.Thin;

        cell.Style.Border.TopBorderColor =
            XLColor.FromHtml("#dce1ee");

        cell.Style.Border.BottomBorderColor =
            XLColor.FromHtml("#dce1ee");

        cell.Style.Border.LeftBorderColor =
            XLColor.FromHtml("#dce1ee");

        cell.Style.Border.RightBorderColor =
            XLColor.FromHtml("#dce1ee");
    }
    private static void TryAddPicture(IXLWorksheet ws, string? path, int row, int col, int w, int h)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            ws.AddPicture(path).MoveTo(ws.Cell(row, col)).WithSize(w, h);
        }
        catch { /* logo optional */ }
    }

    // ── CSV — structured to match the Focus 360 CSV layout ─────────────────────
    // Content/sections mirror the Assurance Snapshot EXCEL (title, bank, branch/ZO
    // band + branch header table incl. Assurance Head, then each category with its
    // panels/headers/rows). Formatting (comment header block, "# ===" section
    // banners, CAPS column headers, "=== panel ===" dividers, blank-line spacing,
    // trailing footer, UTF-8 BOM) mirrors GapExportService.ExportToCsv (Focus 360).
    public byte[] ExportToCsv(
        GapReportViewModel r,
        List<AssuranceCategory> categories,
        string? title = null,
        string? bankName = null)
    {
        const string reportFooter = "Confidential | Internal Use Only";
        var reportName = string.IsNullOrWhiteSpace(title) ? "Assurance Snapshot" : title!;
        var asOnLbl = (r.AsOnDate ?? AppTime.Today).ToString("dd-MMM-yyyy");

        var sb = new StringBuilder();

        // ── Comment metadata header (mirrors Focus 360) ────────────────────────
        sb.AppendLine($"# {reportName}");
        if (!string.IsNullOrWhiteSpace(bankName))
            sb.AppendLine($"# {bankName}");
        sb.AppendLine($"# Branch: {r.Branch.BranchName} ({r.Branch.BranchCode})  |  Zone: {r.Branch.ZoneName}");
        sb.AppendLine($"# As On: {asOnLbl}");
        sb.AppendLine($"# Generated: {AppTime.Now:dd-MMM-yyyy HH:mm}");
        sb.AppendLine();

        // ============================================================
        // BRANCH / ZO DETAILS  (same fields/columns as the Excel)
        // ============================================================
        sb.AppendLine("# ============================================================");
        sb.AppendLine($"# Branch / ZO Details   As on {asOnLbl}");
        sb.AppendLine("# ============================================================");

        sb.AppendLine("BRANCH,CODE,ZONE,REGION,BM,WORKING SINCE,ASSURANCE HEAD,WORKING SINCE");
        sb.AppendLine(string.Join(",", new[]
        {
            Csv(r.Branch.BranchName),
            Csv(r.Branch.BranchCode),
            Csv(r.Branch.ZoneName),
            Csv(r.Branch.RegionName),
            Csv(r.Branch.ZMBMName),
            Csv(r.Branch.WorkingSince?.ToString("dd-MMM-yyyy") ?? ""),
            Csv(""),   // Assurance Head (blank in Excel source)
            Csv("")    // Working Since (blank in Excel source)
        }));
        sb.AppendLine();

        // ============================================================
        // CATEGORIES → PANELS  (identical content/structure to the Excel)
        // ============================================================
        foreach (var cat in categories)
        {
            sb.AppendLine("# ============================================================");
            sb.AppendLine($"# {cat.Label}");
            sb.AppendLine("# ============================================================");

            foreach (var group in cat.PanelRowGroups)
            {
                foreach (var panel in group.Panels)
                {
                    // Panel divider (mirrors Focus 360's "=== Section ===")
                    sb.AppendLine($"=== {panel.Title} ===");

                    // Header row: row-label header (when present) + column headers
                    bool hasRowLabel = panel.Rows.Any(x => !string.IsNullOrWhiteSpace(x.RowLabel));
                    var headers = new List<string>();
                    if (hasRowLabel)
                        headers.Add(panel.RowLabelHeader ?? "Parameter");
                    headers.AddRange(panel.ColumnHeaders);
                    sb.AppendLine(string.Join(",", headers.Select(Csv)));

                    // Data rows: row label (when present) + cells
                    foreach (var row in panel.Rows)
                    {
                        var values = new List<string>();
                        if (hasRowLabel)
                            values.Add(row.RowLabel);
                        values.AddRange(row.Cells);
                        sb.AppendLine(string.Join(",", values.Select(Csv)));
                    }

                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine($"# {reportFooter}");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ── PDF — Focus 360 header + category/panel stack, single A4 page ──────────

    public byte[] ExportToPdf(
    GapReportViewModel r,
    List<AssuranceCategory> categories,
    List<F360CategoryColor> categoryColors,
    string title,
    string bankName,
    string? appLogo,
    string? coLogo)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(10);

                page.DefaultTextStyle(x =>
                    x.FontSize(6));

                // =====================================================
                // HEADER
                // =====================================================

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        if (!string.IsNullOrWhiteSpace(appLogo)
                            && File.Exists(appLogo))
                        {
                            row.ConstantItem(50)
                               .Height(35)
                               .Image(appLogo)
                               .FitArea();
                        }
                        else
                        {
                            row.ConstantItem(50)
                               .Height(35)
                               .Border(1)
                               .AlignCenter()
                               .AlignMiddle()
                               .Text("MD");
                        }

                        row.RelativeItem().Column(col =>
                        {
                            col.Item()
                               .AlignCenter()
                               .Text(title)
                               .FontSize(14)
                               .Bold()
                               .FontColor("#0b4ea2");

                            if (!string.IsNullOrWhiteSpace(bankName))
                            {
                                col.Item()
                                   .AlignCenter()
                                   .Text(bankName)
                                   .FontSize(9);
                            }

                            col.Item()
                               .AlignCenter()
                               .Text(
                                   $"Union Bank of India | ZO/RO/Branch : {r.Branch.BranchName} ({r.Branch.BranchCode})")
                               .FontSize(7)
                               .FontColor("#455A64");
                        });

                        if (!string.IsNullOrWhiteSpace(coLogo)
                            && File.Exists(coLogo))
                        {
                            row.ConstantItem(90)
                               .Height(35)
                               .Image(coLogo)
                               .FitArea();
                        }
                        else
                        {
                            row.ConstantItem(90)
                               .Border(1)
                               .AlignCenter()
                               .AlignMiddle()
                               .Text("LOGO");
                        }
                    });

                    // ==========================================
                    // BRANCH DETAILS BAND
                    // ==========================================

                    h.Item()
                     .PaddingTop(5)
                     .Background("#c8102e")
                     .Padding(3)
                     .AlignCenter()
                     .Text(
                        $"Branch / ZO Details  As on {(r.AsOnDate ?? AppTime.Today):dd-MMM-yyyy}")
                     .Bold()
                     .FontColor(Colors.White);

                    // ==========================================
                    // BRANCH TABLE
                    // ==========================================

                    h.Item()
                     .PaddingTop(3)
                     .Table(t =>
                     {
                         t.ColumnsDefinition(c =>
                         {
                             for (int i = 0; i < 8; i++)
                                 c.RelativeColumn();
                         });

                         AddPdfHeader(t, "Branch");
                         AddPdfHeader(t, "Code");
                         AddPdfHeader(t, "Zone");
                         AddPdfHeader(t, "Region");
                         AddPdfHeader(t, "BM");
                         AddPdfHeader(t, "Working Since");
                         AddPdfHeader(t, "Assurance Head");
                         AddPdfHeader(t, "Working Since");

                         AddPdfCell(t, r.Branch.BranchName);
                         AddPdfCell(t, r.Branch.BranchCode);
                         AddPdfCell(t, r.Branch.ZoneName);
                         AddPdfCell(t, r.Branch.RegionName);
                         AddPdfCell(t, r.Branch.ZMBMName);
                         AddPdfCell(t, r.Branch.WorkingSince?.ToString("dd-MMM-yyyy") ?? "");
                         AddPdfCell(t, "");
                         AddPdfCell(t, "");
                     });
                });

                // =====================================================
                // CONTENT
                // =====================================================

                page.Content()
                    .PaddingTop(5)
                    .Column(col =>
                    {
                        foreach (var category in categories)
                        {
                            // CATEGORY BAND

                            col.Item()
                            .Background(
                                GetPanelColor(
                                    category.Label,
                                    categoryColors))
                               //.Background("#c0143c")
                               .Padding(3)
                               .Text(category.Label)
                               .Bold()
                               .FontColor(Colors.White)
                               .FontSize(7);

                            foreach (var group in category.PanelRowGroups)
                            {
                                col.Item()
                                   .PaddingTop(2)
                                   .Row(panelRow =>
                                   {
                                       foreach (var panel in group.Panels)
                                       {
                                           panelRow.RelativeItem()
                                                   .Element(x =>
                                                       RenderPdfPanel(x, panel, categoryColors));
                                       }
                                   });
                            }

                            col.Item().Height(4);
                        }
                    });

                // =====================================================
                // FOOTER
                // =====================================================

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        });

        return doc.GeneratePdf();
    }

    //private static void RenderPdfPanel(
    //IContainer container,
    //AssurancePanel panel, List<F360CategoryColor> categoryColors)
    //{
    //    bool hasRowLabel =
    //        panel.Rows.Any(x =>
    //            !string.IsNullOrWhiteSpace(x.RowLabel));

    //    container.PaddingRight(4)
    //             .Column(col =>
    //             {
    //                 // PANEL TITLE

    //                 col.Item()
    //                 .Background(
    //                    GetPanelColor(
    //                        panel.Title,
    //                        categoryColors))
    //                    //.Background("#1b4a98")
    //                    .Padding(2)
    //                    .Text(panel.Title)
    //                    .Bold()
    //                    .FontColor(Colors.White)
    //                    .FontSize(6);

    //                 // PANEL TABLE

    //                 col.Item()
    //                    .Table(table =>
    //                    {
    //                        table.ColumnsDefinition(colDef =>
    //                        {
    //                            if (hasRowLabel)
    //                                colDef.RelativeColumn(2);

    //                            foreach (var _ in panel.ColumnHeaders)
    //                                colDef.RelativeColumn();
    //                        });

    //                        if (hasRowLabel)
    //                        {
    //                            table.Cell()
    //                                 .Background("#eef1f8")
    //                                 .Border(0.5f)
    //                                 .Padding(1)
    //                                 .Text(panel.RowLabelHeader ?? "Parameter")
    //                                 .Bold()
    //                                 .FontSize(5);
    //                        }

    //                        foreach (var h in panel.ColumnHeaders)
    //                        {
    //                            table.Cell()
    //                                 .Background("#eef1f8")
    //                                 .Border(0.5f)
    //                                 .Padding(1)
    //                                 .Text(h)
    //                                 .Bold()
    //                                 .FontSize(5);
    //                        }

    //                        foreach (var row in panel.Rows)
    //                        {
    //                            if (hasRowLabel)
    //                            {
    //                                table.Cell()
    //                                     .Border(0.5f)
    //                                     .Padding(1)
    //                                     .Text(row.RowLabel)
    //                                     .FontSize(5);
    //                            }

    //                            foreach (var cell in row.Cells)
    //                            {
    //                                table.Cell()
    //                                     .Border(0.5f)
    //                                     .Padding(1)
    //                                     .AlignRight()
    //                                     .Text(cell)
    //                                     .FontSize(5);
    //                            }
    //                        }
    //                    });
    //             });
    //}

    private static void RenderPdfPanel(
    IContainer container,
    AssurancePanel panel,
    List<F360CategoryColor> categoryColors)
    {
        bool hasRowLabel =
            panel.Rows.Any(x =>
                !string.IsNullOrWhiteSpace(x.RowLabel));

        int totalColumns =
            panel.ColumnHeaders.Count +
            (hasRowLabel ? 1 : 0);

        container
            .PaddingRight(4)
            .Column(col =>
            {
                // Panel Title
                col.Item()
                    .Background(
                        GetPanelColor(
                            panel.Title,
                            categoryColors))
                    .Padding(2)
                    .Text(panel.Title)
                    .Bold()
                    .FontColor(Colors.White)
                    .FontSize(6);

                col.Item()
                    .Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            if (hasRowLabel)
                                cols.RelativeColumn(4);

                            foreach (var _ in panel.ColumnHeaders)
                                cols.RelativeColumn(2);
                        });

                        // HEADER ROW

                        if (hasRowLabel)
                        {
                            table.Cell()
                                .Background("#eef1f8")
                                .Border(0.5f)
                                .Padding(1)
                                .Text(panel.RowLabelHeader ?? "Parameter")
                                .Bold()
                                .FontSize(5);
                        }

                        foreach (var h in panel.ColumnHeaders)
                        {
                            table.Cell()
                                .Background("#eef1f8")
                                .Border(0.5f)
                                .Padding(1)
                                .Text(h)
                                .Bold()
                                .FontSize(5);
                        }

                        // DATA ROWS

                        foreach (var row in panel.Rows)
                        {
                            int renderedCells = 0;

                            if (hasRowLabel)
                            {
                                table.Cell()
                                    .Border(0.5f)
                                    .Padding(1)
                                    .Text(row.RowLabel ?? "")
                                    .FontSize(5);

                                renderedCells++;
                            }

                            foreach (var cell in row.Cells)
                            {
                                table.Cell()
                                    .Border(0.5f)
                                    .Padding(1)
                                    .AlignRight()
                                    .Text(cell ?? "")
                                    .FontSize(5);

                                renderedCells++;
                            }

                            // VERY IMPORTANT
                            // Fill missing cells so QuestPDF
                            // doesn't shift columns.

                            while (renderedCells < totalColumns)
                            {
                                table.Cell()
                                    .Border(0.5f)
                                    .Padding(1)
                                    .Text("");

                                renderedCells++;
                            }
                        }
                    });
            });
    }

    private static void AddPdfHeader(
    TableDescriptor table,
    string value)
    {
        table.Cell()
             .Background("#eef1f8")
             .Border(0.5f)
             .Padding(2)
             .Text(value)
             .Bold()
             .FontSize(6);
    }

    private static void AddPdfCell(
    TableDescriptor table,
    string value)
    {
        table.Cell()
             .Border(0.5f)
             .Padding(2)
             .Text(value ?? "")
             .FontSize(5);
    }


    // ── helpers ────────────────────────────────────────────────────────────────
    private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    private static string GetPanelColor(
    string panelTitle,
    List<F360CategoryColor> categoryColors)
    {
        var color = categoryColors.FirstOrDefault(x =>
            string.Equals(
                x.SectionLabel,
                panelTitle,
                StringComparison.OrdinalIgnoreCase));

        return color?.ColorHex ?? "#1b4a98";
    }

}
