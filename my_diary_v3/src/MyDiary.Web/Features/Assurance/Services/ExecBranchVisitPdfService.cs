using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RequestPortal.Core.Models;

namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>
/// Renders a submitted "Executive Branch Visit" to PDF, in the same sequence as
/// Final_Branch_Visit_Format_Blue_colour change.docx/pdf: 1. Branch & Visit Details,
/// A. Business, B. Physical Infrastructure, C. Security, D. Credit Monitoring/Recovery,
/// E. Audit, F. Views of Branch Visiting Officials. Reads straight from the frozen
/// ExecBranchVisit.Fields/OpenRows a submitted record carries — the same FIELD_KEY
/// scheme ExecutiveBranchVisit.razor uses to build/read cells — so what's on screen at
/// submit time is exactly what prints. Follows RoVisitPdfService's conventions (Calibri,
/// logo header, thin-bordered tables) but with this form's own navy/light-blue section
/// banners (#1F4E79/#BDD7EE/#DDEBF7), matching what's on screen and in the source PDF.
/// </summary>
public sealed class ExecBranchVisitPdfService
{
    private const string BodyFont = "Calibri";
    private const float BodySize = 8.5f;
    private const float HeadingSize = 10f;

    private readonly string[] _businessFigureRows =
    {
        "Total Deposits","Current Deposits","Savings Deposits","Term Deposits","Total Advances",
        "Priority Sector","Agriculture","MSME","Retail","NPA Recovery","Non-Interest Income"
    };

    public byte[] GeneratePdf(ExecBranchVisit visit, string? appLogoPath = null, string? bankLogoPath = null)
    {
        string V(string key) => visit.Fields.TryGetValue(key, out var v) ? v ?? "" : "";
        List<string> Rows(string sectionCode, ExecVisitTable t) =>
            t.OpenEnded && visit.OpenRows.TryGetValue($"{sectionCode}_{t.Title}", out var rows) && rows.Count > 0
                ? rows : t.Rows.ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontFamily(BodyFont).FontSize(BodySize).FontColor(Colors.Black));

                page.Header().Column(col =>
                {
                    col.Item().Row(hr =>
                    {
                        if (!string.IsNullOrEmpty(appLogoPath) && File.Exists(appLogoPath))
                            hr.ConstantItem(40).AlignMiddle().Image(appLogoPath).FitArea();
                        else
                            hr.ConstantItem(40);

                        hr.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().AlignCenter().Text("Executive Branch Visit Report")
                                .FontFamily(BodyFont).FontSize(13).Bold();
                            titleCol.Item().AlignCenter().Text($"Branch: {visit.BranchCode} {(string.IsNullOrWhiteSpace(visit.BranchName) ? "" : $"— {visit.BranchName}")}")
                                .FontFamily(BodyFont).FontSize(BodySize).Italic();
                        });

                        if (!string.IsNullOrEmpty(bankLogoPath) && File.Exists(bankLogoPath))
                            hr.ConstantItem(80).AlignMiddle().Image(bankLogoPath).FitArea();
                        else
                            hr.ConstantItem(80);
                    });
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Spacing(3);

                    // ── 1. Branch & Visit Details ──────────────────────────────────
                    col.Item().Element(e => SectionTitle(e, "1. Branch & Visit Details"));
                    col.Item().Element(e => KeyValueTable(e, new[]
                    {
                        ("Branch (Name-SOL)", $"{visit.BranchCode} {(string.IsNullOrWhiteSpace(visit.BranchName) ? "" : visit.BranchName)}".Trim()),
                        ("Date of Opening", V("T1_DateOfOpening")),
                        ("Region", V("T1_Region")), ("Branch Category", V("T1_BranchCategory")),
                        ("Zone", V("T1_Zone")), ("Audit Rating", V("T1_AuditRating")),
                        ("Branch Manager", visit.BranchManagerName ?? ""), ("Since", visit.BmWorkingSince?.ToString("dd-MMM-yyyy") ?? ""),
                        ("Name of Visiting Official", visit.VisitingOfficialName), ("Designation", V("T1_Designation")),
                        ("Date of Visit", visit.DateOfVisit?.ToString("dd-MMM-yyyy") ?? ""), ("Status", visit.Status),
                    }));

                    // ── A. Business ─────────────────────────────────────────────────
                    col.Item().PaddingTop(6).Element(e => SectionTitle(e, "A. Business — 1. Current Business Figures (Amt. in Lacs)"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2f);
                            for (var i = 0; i < 6; i++) c.RelativeColumn(1.3f);
                        });
                        Header(t, "Parameter", "2025-26 March (Actual)", "2026-27 March (Target)", "Prev Qtr (Target)", "Prev Qtr (Actual)", "Curr Qtr (Target)", "Prev Day Figure");
                        foreach (var row in _businessFigureRows)
                        {
                            Row(t, row,
                                V($"A1_{row}_Actual2526"), V($"A1_{row}_Target2627"), V($"A1_{row}_PrevQtrTarget"),
                                V($"A1_{row}_PrevQtrActual"), V($"A1_{row}_CurQtrTarget"), V($"A1_{row}_PrevDay"));
                        }
                    });
                    foreach (var t in ExecVisitFormCatalog.SectionA_Extra)
                        RenderGenericTable(col, "A", t, Rows("A", t), V);

                    // ── B. Physical Infrastructure ───────────────────────────────────
                    col.Item().PaddingTop(6).Element(e => SectionTitle(e, "B. Physical Infrastructure"));
                    foreach (var t in ExecVisitFormCatalog.SectionB)
                        RenderGenericTable(col, "B", t, Rows("B", t), V);

                    // ── C. Security ───────────────────────────────────────────────────
                    col.Item().PaddingTop(6).Element(e => SectionTitle(e, "C. Security"));
                    foreach (var t in ExecVisitFormCatalog.SectionC.Take(2))
                        RenderGenericTable(col, "C", t, Rows("C", t), V);

                    col.Item().PaddingTop(3).Text("2.i. Impersonal Accounts — Sundry").FontFamily(BodyFont).FontSize(BodySize).Bold();
                    ImpersonalTable(col, "C_Sundry", V);
                    col.Item().PaddingTop(3).Text("2.ii. Impersonal Accounts — Suspense").FontFamily(BodyFont).FontSize(BodySize).Bold();
                    ImpersonalTable(col, "C_Suspense", V);

                    foreach (var t in ExecVisitFormCatalog.SectionC.Skip(2))
                        RenderGenericTable(col, "C", t, Rows("C", t), V);

                    // ── D. Credit Monitoring/Recovery ────────────────────────────────
                    col.Item().PaddingTop(6).Element(e => SectionTitle(e, "D. Credit Monitoring/Recovery"));
                    foreach (var t in ExecVisitFormCatalog.SectionD)
                        RenderGenericTable(col, "D", t, Rows("D", t), V);

                    // ── E. Audit ──────────────────────────────────────────────────────
                    col.Item().PaddingTop(6).Element(e => SectionTitle(e, "E. Audit"));
                    foreach (var t in ExecVisitFormCatalog.SectionE)
                        RenderGenericTable(col, "E", t, Rows("E", t), V);

                    // ── F. Views of Branch Visiting Officials ────────────────────────
                    col.Item().PaddingTop(6).Element(e => SectionTitle(e, "F. Views of Branch Visiting Officials"));
                    foreach (var t in ExecVisitFormCatalog.SectionF)
                        RenderGenericTable(col, "F", t, Rows("F", t), V);

                    col.Item().PaddingTop(16).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Name of Visiting Official: {visit.VisitingOfficialName}").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(12).Text("Signature: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Branch Manager: {visit.BranchManagerName}").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(12).Text("Signature: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(y => y.FontFamily(BodyFont).FontSize(7));
                    x.Span($"Submitted {visit.SubmittedAt:dd-MMM-yyyy HH:mm} — Page ");
                    x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
            });
        });
        return doc.GeneratePdf();
    }

    private static void RenderGenericTable(QuestPDF.Fluent.ColumnDescriptor col, string sectionCode, ExecVisitTable t, List<string> rows, Func<string, string> v)
    {
        col.Item().PaddingTop(4).Text(t.Title).FontFamily(BodyFont).FontSize(BodySize).Bold();
        col.Item().Table(tb =>
        {
            tb.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.8f);
                foreach (var _ in t.Columns) c.RelativeColumn(1f);
            });
            Header(tb, new[] { "Item" }.Concat(t.Columns).ToArray());
            foreach (var r in rows)
                Row(tb, new[] { r }.Concat(t.Columns.Select(c => v($"{sectionCode}_{t.Title}_{r}_{c}"))).ToArray());
        });
    }

    private static void ImpersonalTable(QuestPDF.Fluent.ColumnDescriptor col, string code, Func<string, string> v)
    {
        col.Item().Table(tb =>
        {
            tb.ColumnsDefinition(c => { c.RelativeColumn(1.8f); for (var i = 0; i < 6; i++) c.RelativeColumn(1f); });
            Header(tb, "Item", "No of Entries", "Value", "Outstanding >180d", "Outstanding 90-180d", "Outstanding <90d", "Remarks");
            Row(tb, "Impersonal Account", v($"{code}_entries"), v($"{code}_value"), v($"{code}_gt180"), v($"{code}_90to180"), v($"{code}_lt90"), v($"{code}_remarks"));
        });
    }

    private static void SectionTitle(IContainer c, string title) =>
        c.Background("#1F4E79").Padding(4).Text(title).FontFamily(BodyFont).FontSize(HeadingSize).Bold().FontColor(Colors.White);

    private static void KeyValueTable(IContainer c, (string Label, string Value)[] pairs)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(1); cd.RelativeColumn(1.4f); cd.RelativeColumn(1); cd.RelativeColumn(1.4f); });
            for (var i = 0; i < pairs.Length; i += 2)
            {
                t.Cell().Background("#BDD7EE").Border(0.5f).Padding(3).Text(pairs[i].Label).FontFamily(BodyFont).FontSize(BodySize).Bold();
                t.Cell().Border(0.5f).Padding(3).Text(pairs[i].Value).FontFamily(BodyFont).FontSize(BodySize);
                if (i + 1 < pairs.Length)
                {
                    t.Cell().Background("#BDD7EE").Border(0.5f).Padding(3).Text(pairs[i + 1].Label).FontFamily(BodyFont).FontSize(BodySize).Bold();
                    t.Cell().Border(0.5f).Padding(3).Text(pairs[i + 1].Value).FontFamily(BodyFont).FontSize(BodySize);
                }
                else { t.Cell().Border(0.5f); t.Cell().Border(0.5f); }
            }
        });
    }

    private static void Header(QuestPDF.Fluent.TableDescriptor t, params string[] labels)
    {
        foreach (var l in labels)
            t.Cell().Background("#BDD7EE").Border(0.5f).Padding(2).Text(l).FontFamily(BodyFont).FontSize(BodySize).Bold();
    }

    private static void Row(QuestPDF.Fluent.TableDescriptor t, params string[] values)
    {
        foreach (var v in values)
            t.Cell().Border(0.5f).Padding(2).Text(v ?? "").FontFamily(BodyFont).FontSize(BodySize);
    }
}
