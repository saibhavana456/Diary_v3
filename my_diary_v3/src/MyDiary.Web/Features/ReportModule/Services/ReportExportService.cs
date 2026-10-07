using ClosedXML.Excel;
using MyDiary.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RequestPortal.Core.Models;
using System.Text;

namespace MyDiary.Web.Features.ReportModule.Services;

/// <summary>
/// Exports a generic <see cref="ReportResult"/> to Excel / CSV / PDF, with the
/// same header treatment used by Focus 360 (two logos + centred title/bank name).
/// Unlike the on-screen view (paged, capped at 200), export always emits the
/// FULL row set the caller passes in.
/// </summary>
public sealed class ReportExportService
{
    public byte[] ExportToExcel(string title, string bankName, string? appLogo, string? coLogo, List<string> columns, IReadOnlyList<object?[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Report");
        int lastCol = Math.Max(columns.Count, 1);

        ws.Range(1, 1, 1, lastCol).Merge();
        var t = ws.Cell(1, 1);
        t.Value = title + (string.IsNullOrWhiteSpace(bankName) ? "" : "\n" + bankName);
        t.Style.Fill.BackgroundColor = XLColor.FromHtml("#0b4ea2");
        t.Style.Font.FontColor = XLColor.White; t.Style.Font.Bold = true; t.Style.Font.FontSize = 12;
        t.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        t.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        t.Style.Alignment.WrapText = true;
        ws.Row(1).Height = 34;
        TryAddPicture(ws, appLogo, 1, 1, 90, 30);
        if (lastCol > 1) TryAddPicture(ws, coLogo, 1, lastCol, 90, 30);

        ws.Range(2, 1, 2, lastCol).Merge();
        ws.Cell(2, 1).Value = $"Generated {AppTime.Now:dd-MMM-yyyy HH:mm} · {rows.Count} row(s)";
        ws.Cell(2, 1).Style.Font.Italic = true; ws.Cell(2, 1).Style.Font.FontSize = 8;

        int headerRow = 4;
        for (int c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = columns[c];
            cell.Style.Font.Bold = true; cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        int row = headerRow + 1;
        foreach (var r in rows)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = c < r.Length ? (r[c]?.ToString() ?? "") : "";
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#dce1ee");
                cell.Style.Font.FontSize = 9;
            }
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(headerRow);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportToCsv(List<string> columns, IReadOnlyList<object?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', columns.Select(Csv)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(',', columns.Select((_, i) => Csv(i < r.Length ? r[i]?.ToString() : ""))));
        return new UTF8Encoding(true).GetBytes(sb.ToString());
    }

    public byte[] ExportToPdf(string title, string bankName, string? appLogo, string? coLogo, List<string> columns, IReadOnlyList<object?[]> rows)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(columns.Count > 8 ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(14);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Background(Colors.White).PaddingVertical(2).Row(hr =>
                {
                    if (!string.IsNullOrEmpty(appLogo) && File.Exists(appLogo))
                        hr.ConstantItem(52).AlignMiddle().Image(appLogo).FitArea();
                    hr.ConstantItem(6);
                    hr.RelativeItem().AlignMiddle().Column(col =>
                    {
                        col.Item().AlignCenter().Text(title).FontSize(12).Bold().FontColor("#0b4ea2");
                        if (!string.IsNullOrWhiteSpace(bankName))
                            col.Item().AlignCenter().Text(bankName).FontSize(9).FontColor("#455A64");
                        col.Item().AlignCenter().Text($"Generated {AppTime.Now:dd-MMM-yyyy HH:mm} · {rows.Count} row(s)").FontSize(7).FontColor("#455A64");
                    });
                    hr.ConstantItem(6);
                    if (!string.IsNullOrEmpty(coLogo) && File.Exists(coLogo))
                        hr.ConstantItem(96).AlignMiddle().Image(coLogo).FitArea();
                });

                page.Content().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(cd => { foreach (var _ in columns) cd.RelativeColumn(); });
                    foreach (var h in columns)
                        t.Cell().Background("#1E3A8A").Padding(3).Text(h).Bold().FontColor(Colors.White).FontSize(7);
                    foreach (var r in rows)
                        for (int c = 0; c < columns.Count; c++)
                            t.Cell().BorderBottom(0.3f).BorderColor("#dce1ee").Padding(3)
                                .Text(c < r.Length ? (r[c]?.ToString() ?? "") : "").FontSize(6.5f);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
                });
            });
        });
        return doc.GeneratePdf();
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

    private static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
}
