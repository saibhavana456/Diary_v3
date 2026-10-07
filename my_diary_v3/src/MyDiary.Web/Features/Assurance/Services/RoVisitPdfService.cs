using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RequestPortal.Core.Models;

namespace MyDiary.Web.Features.Assurance.Services;

/// <summary>
/// Renders a "ZAH: RO Visit Report" to PDF, styled to match the source Word
/// document (RO_Visit_Format_ZRAH.docx) as closely as QuestPDF allows: plain
/// bold black section headings (not a colored banner), standard thin-black-
/// bordered tables, Calibri body text, continuous page flow (no forced page
/// breaks between sections — QuestPDF paginates naturally when content
/// overflows, same as Word would).
/// </summary>
public sealed class RoVisitPdfService
{
    private const string BodyFont = "Calibri";
    private const float BodySize = 9.5f;
    private const float HeadingSize = 10f;

    /// <summary>appLogoPath/bankLogoPath: absolute file paths, same convention as
    /// AssuranceExportService — My Diary logo on the left, UBI logo on the right of the
    /// header row (QuestPDF repeats page.Header() on every page, same as Word would with
    /// a "first page header" that's identical across pages here).</summary>
    public byte[] GenerateRoVisitPdf(RoVisitReport report, List<RoVisitMetricCell> cells, string? appLogoPath = null, string? bankLogoPath = null)
    {
        var cellLookup = cells.ToDictionary(c => (c.PanelCode, c.RowCode, c.ColCode), c => c.ValueText);
        string Cell(string panel, string row, string col) => cellLookup.TryGetValue((panel, row, col), out var v) ? v ?? "" : "";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(BodyFont).FontSize(BodySize).FontColor(Colors.Black));

                page.Header().Column(col =>
                {
                    col.Item().Row(hr =>
                    {
                        if (!string.IsNullOrEmpty(appLogoPath) && File.Exists(appLogoPath))
                            hr.ConstantItem(44).AlignMiddle().Image(appLogoPath).FitArea();
                        else
                            hr.ConstantItem(44);

                        hr.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().AlignCenter().Text("क्षेत्रीय कार्यालय निरीक्षण रिपोर्ट / Regional Office Visit Report")
                                .FontFamily(BodyFont).FontSize(14).Bold();
                            titleCol.Item().AlignCenter().Text("(For Zonal Assurance Head)").FontFamily(BodyFont).FontSize(BodySize).Italic();
                        });

                        if (!string.IsNullOrEmpty(bankLogoPath) && File.Exists(bankLogoPath))
                            hr.ConstantItem(88).AlignMiddle().Image(bankLogoPath).FitArea();
                        else
                            hr.ConstantItem(88);
                    });
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(4);

                    col.Item().Element(e => RenderSectionTitle(e, "1. Regional Office & Visit Details"));
                    col.Item().Element(e => RenderKeyValueTable(e, new[]
                    {
                        ("Region", report.RegionName ?? ""), ("Zone", report.ZoneName ?? ""),
                        ("Regional Head", report.RegionalHeadName ?? ""), ("RH Working Since", report.RhWorkingSince?.ToString("dd-MMM-yyyy") ?? ""),
                        ("Assurance Head", report.AssuranceHeadName ?? ""), ("AH Working Since", report.AhWorkingSince?.ToString("dd-MMM-yyyy") ?? ""),
                        ("Name of Visiting Official", report.VisitingOfficialName), ("Designation", report.Designation),
                        ("Date of Visit", report.DateOfVisit?.ToString("dd-MMM-yyyy") ?? ""), ("Report Date", report.ReportDate?.ToString("dd-MMM-yyyy") ?? ""),
                    }));

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "2. Executive Summary of Visit"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.5f); c.RelativeColumn(2); c.RelativeColumn(2.5f); c.RelativeColumn(2.5f); c.RelativeColumn(1); });
                        Header(t, "Sr.", "Focus Area", "Observation", "Action Required", "Target Date");
                        foreach (var r in report.ExecSummary)
                            Row(t, r.SrNo.ToString(), r.FocusArea, r.Observation ?? "", r.ActionRequired ?? "", r.TargetDate?.ToString("dd-MMM-yyyy") ?? "");
                    });

                    int panelNo = 3;
                    foreach (var panel in RoVisitPanelCatalog.Panels)
                    {
                        if (panel.PanelCode != "SUSPENSE_B")
                            col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, $"{panelNo}. {panel.Title}"));
                        else
                            col.Item().PaddingTop(3).Element(e => RenderSectionTitle(e, panel.Title));

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.5f);
                                foreach (var _ in panel.Columns) c.RelativeColumn(1.3f);
                            });
                            Header(t, new[] { panel.RowLabelHeader }.Concat(panel.Columns.Select(pc => pc.Label)).ToArray());
                            foreach (var pr in panel.Rows)
                                Row(t, new[] { pr.Label }.Concat(panel.Columns.Select(pc => Cell(panel.PanelCode, pr.RowCode, pc.ColCode))).ToArray());
                        });

                        if (panel.PanelCode == "SUSPENSE_B" && !string.IsNullOrWhiteSpace(report.SuspenseComments))
                            col.Item().PaddingTop(2).Text($"Visiting Official's comments on long outstanding entries: {report.SuspenseComments}")
                                .FontFamily(BodyFont).FontSize(BodySize).Italic();

                        if (panel.PanelCode != "SUSPENSE_A") panelNo++;
                    }

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "11. Key Findings, Action Plan & Escalation"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.5f); c.RelativeColumn(2.5f); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1); });
                        Header(t, "Sr.", "Finding / Observation", "Risk", "Action Required", "Target Date");
                        foreach (var f in report.Findings)
                            Row(t, f.SrNo.ToString(), f.Finding ?? "", f.RiskSeverity ?? "", f.ActionRequired ?? "", f.TargetDate?.ToString("dd-MMM-yyyy") ?? "");
                    });

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "12. Pending Issues Requiring ZO / CO Support"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.5f); c.RelativeColumn(2.5f); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); });
                        Header(t, "Sr.", "Issue", "Support Required", "Priority", "Expected Closure");
                        foreach (var p in report.PendingIssues)
                            Row(t, p.SrNo.ToString(), p.Issue ?? "", p.SupportRequired ?? "", p.Priority ?? "", p.ExpectedClosure?.ToString("dd-MMM-yyyy") ?? "");
                    });

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "Overall Assessment by Visiting Official"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(2.5f); });
                        Header(t, "Area", "Assessment", "Comments");
                        foreach (var a in report.Assessment)
                            Row(t, a.AreaLabel, a.Assessment ?? "", a.Comments ?? "");
                    });

                    if (!string.IsNullOrWhiteSpace(report.OverallRemarks))
                        col.Item().PaddingTop(4).Text($"Overall remarks / concluding observations: {report.OverallRemarks}")
                            .FontFamily(BodyFont).FontSize(BodySize);

                    col.Item().PaddingTop(20).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Name of Visiting Official: {report.VisitingOfficialName}").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(14).Text("Signature: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(6).Text($"Date: {report.ReportDate:dd-MMM-yyyy}").FontFamily(BodyFont).FontSize(BodySize);
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Regional Assurance Head Name: {report.RahName}").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(14).Text("Signature: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(6).Text("Date: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                        });
                    });

                    col.Item().PaddingTop(10).Text(
                        "(During RO visits, the ZO Assurance Head shall review the bottom 20% branches, identify parameter-wise " +
                        "causes of pendency, and recommend corrective actions to the RO for guiding the concerned branches.)")
                        .FontFamily(BodyFont).FontSize(8).Italic();
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(y => y.FontFamily(BodyFont).FontSize(8));
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
            });
        });
        return doc.GeneratePdf();
    }

    /// <summary>
    /// Renders "RAH: Branch Visit Report" to PDF — same visual style/layout machinery as
    /// <see cref="GenerateRoVisitPdf"/> (reuses every private helper below), driven by
    /// <see cref="BranchVisitPanelCatalog"/> instead, plus the branch-specific header fields
    /// and the Branch Manager (not RAH) name in the second signature block.
    /// </summary>
    public byte[] GenerateBranchVisitPdf(RoVisitReport report, List<RoVisitMetricCell> cells, string? appLogoPath = null, string? bankLogoPath = null)
    {
        var cellLookup = cells.ToDictionary(c => (c.PanelCode, c.RowCode, c.ColCode), c => c.ValueText);
        string Cell(string panel, string row, string col) => cellLookup.TryGetValue((panel, row, col), out var v) ? v ?? "" : "";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(BodyFont).FontSize(BodySize).FontColor(Colors.Black));

                page.Header().Column(col =>
                {
                    col.Item().Row(hr =>
                    {
                        if (!string.IsNullOrEmpty(appLogoPath) && File.Exists(appLogoPath))
                            hr.ConstantItem(44).AlignMiddle().Image(appLogoPath).FitArea();
                        else
                            hr.ConstantItem(44);

                        hr.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().AlignCenter().Text("शाखा निरीक्षण रिपोर्ट / Branch Visit Report")
                                .FontFamily(BodyFont).FontSize(14).Bold();
                            titleCol.Item().AlignCenter().Text("(For Zonal / Regional Assurance Head)").FontFamily(BodyFont).FontSize(BodySize).Italic();
                        });

                        if (!string.IsNullOrEmpty(bankLogoPath) && File.Exists(bankLogoPath))
                            hr.ConstantItem(88).AlignMiddle().Image(bankLogoPath).FitArea();
                        else
                            hr.ConstantItem(88);
                    });
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(4);

                    col.Item().Element(e => RenderSectionTitle(e, "1. Preliminary Branch & Visit Details"));
                    col.Item().Element(e => RenderKeyValueTable(e, new[]
                    {
                        ("Branch Name", report.BranchName ?? ""), ("Branch Code", report.BranchCode ?? ""),
                        ("Region", report.RegionName ?? ""), ("Zone", report.ZoneName ?? ""),
                        ("Date of Opening", report.DateOfOpening?.ToString("dd-MMM-yyyy") ?? ""), ("Branch Category", report.BranchCategory ?? ""),
                        ("Audit Rating", report.AuditRating ?? ""), ("Quarter/Year", report.QuarterYear ?? ""),
                        ("Branch Manager", report.BranchManagerName ?? ""), ("BM Working Since", report.BmWorkingSince?.ToString("dd-MMM-yyyy") ?? ""),
                        ("Assurance Head", report.AssuranceHeadName ?? ""), ("AH Working Since", report.AhWorkingSince?.ToString("dd-MMM-yyyy") ?? ""),
                        ("Name of Visiting Official", report.VisitingOfficialName), ("Designation", report.Designation),
                        ("Date of Visit", report.DateOfVisit?.ToString("dd-MMM-yyyy") ?? ""), ("Report Date", report.ReportDate?.ToString("dd-MMM-yyyy") ?? ""),
                    }));

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "2. Executive Summary of Visit"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.5f); c.RelativeColumn(2); c.RelativeColumn(2.5f); c.RelativeColumn(2.5f); c.RelativeColumn(1); });
                        Header(t, "Sr.", "Focus Area", "Observation", "Action Required", "Target Date");
                        foreach (var r in report.ExecSummary)
                            Row(t, r.SrNo.ToString(), r.FocusArea, r.Observation ?? "", r.ActionRequired ?? "", r.TargetDate?.ToString("dd-MMM-yyyy") ?? "");
                    });

                    int panelNo = 3;
                    foreach (var panel in BranchVisitPanelCatalog.Panels)
                    {
                        bool isContinuation = panel.PanelCode is "SUSPENSE_B" or "GOLD_LOAN" or "CS_CHECKLIST" or "LOCKER_ATM";
                        if (!isContinuation)
                            col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, $"{panelNo}. {panel.Title}"));
                        else
                            col.Item().PaddingTop(3).Element(e => RenderSectionTitle(e, panel.Title));

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.5f);
                                foreach (var _ in panel.Columns) c.RelativeColumn(1.3f);
                            });
                            Header(t, new[] { panel.RowLabelHeader }.Concat(panel.Columns.Select(pc => pc.Label)).ToArray());
                            foreach (var pr in panel.Rows)
                                Row(t, new[] { pr.Label }.Concat(panel.Columns.Select(pc => Cell(panel.PanelCode, pr.RowCode, pc.ColCode))).ToArray());
                        });

                        if (panel.PanelCode == "SUSPENSE_B" && !string.IsNullOrWhiteSpace(report.SuspenseComments))
                            col.Item().PaddingTop(2).Text($"Visiting Official's comments on long outstanding entries: {report.SuspenseComments}")
                                .FontFamily(BodyFont).FontSize(BodySize).Italic();

                        if (!isContinuation) panelNo++;
                    }

                    if (!string.IsNullOrWhiteSpace(report.RegistersComments))
                        col.Item().PaddingTop(2).Text($"Comments of Visiting Official on maintenance of registers and housekeeping: {report.RegistersComments}")
                            .FontFamily(BodyFont).FontSize(BodySize).Italic();

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "Key Findings, Action Plan & Escalation"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.5f); c.RelativeColumn(2.5f); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1); });
                        Header(t, "Sr.", "Finding / Observation", "Risk", "Action Required", "Target Date");
                        foreach (var f in report.Findings)
                            Row(t, f.SrNo.ToString(), f.Finding ?? "", f.RiskSeverity ?? "", f.ActionRequired ?? "", f.TargetDate?.ToString("dd-MMM-yyyy") ?? "");
                    });

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "Pending Issues Requiring RO / ZO / CO Support"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.5f); c.RelativeColumn(2.5f); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); });
                        Header(t, "Sr.", "Issue", "Support Required", "Priority", "Expected Closure");
                        foreach (var p in report.PendingIssues)
                            Row(t, p.SrNo.ToString(), p.Issue ?? "", p.SupportRequired ?? "", p.Priority ?? "", p.ExpectedClosure?.ToString("dd-MMM-yyyy") ?? "");
                    });

                    col.Item().PaddingTop(6).Element(e => RenderSectionTitle(e, "Overall Assessment by Visiting Official"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(2.5f); });
                        Header(t, "Area", "Assessment", "Comments");
                        foreach (var a in report.Assessment)
                            Row(t, a.AreaLabel, a.Assessment ?? "", a.Comments ?? "");
                    });

                    if (!string.IsNullOrWhiteSpace(report.OverallRemarks))
                        col.Item().PaddingTop(4).Text($"Overall remarks / concluding observations: {report.OverallRemarks}")
                            .FontFamily(BodyFont).FontSize(BodySize);

                    col.Item().PaddingTop(20).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Name of Visiting Official: {report.VisitingOfficialName}").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(14).Text("Signature: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(6).Text($"Date: {report.ReportDate:dd-MMM-yyyy}").FontFamily(BodyFont).FontSize(BodySize);
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Branch Manager Name: {report.BranchManagerSignoffName ?? report.BranchManagerName}").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(14).Text("Signature: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                            c.Item().PaddingTop(6).Text("Date: ______________________").FontFamily(BodyFont).FontSize(BodySize);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(y => y.FontFamily(BodyFont).FontSize(8));
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
            });
        });
        return doc.GeneratePdf();
    }

    // Plain bold underlined heading, matching the Word document's style (no colored banner).
    private static void RenderSectionTitle(IContainer c, string title)
        => c.PaddingBottom(2).BorderBottom(0.75f).BorderColor(Colors.Black)
             .Text(title).FontFamily(BodyFont).FontSize(HeadingSize).Bold();

    private static void RenderKeyValueTable(IContainer c, (string Label, string Value)[] pairs)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(1); cd.RelativeColumn(1.5f); cd.RelativeColumn(1); cd.RelativeColumn(1.5f); });
            for (int i = 0; i < pairs.Length; i += 2)
            {
                t.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3).Text(pairs[i].Label).FontFamily(BodyFont).FontSize(BodySize).Bold();
                t.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3).Text(pairs[i].Value).FontFamily(BodyFont).FontSize(BodySize);
                if (i + 1 < pairs.Length)
                {
                    t.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3).Text(pairs[i + 1].Label).FontFamily(BodyFont).FontSize(BodySize).Bold();
                    t.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3).Text(pairs[i + 1].Value).FontFamily(BodyFont).FontSize(BodySize);
                }
                else
                {
                    t.Cell().Border(0.5f).BorderColor(Colors.Black); t.Cell().Border(0.5f).BorderColor(Colors.Black);
                }
            }
        });
    }

    private static void Header(QuestPDF.Fluent.TableDescriptor t, params string[] labels)
    {
        foreach (var l in labels)
            t.Cell().Background("#f2f2f2").Border(0.5f).BorderColor(Colors.Black).Padding(3)
             .Text(l).FontFamily(BodyFont).FontSize(BodySize).Bold();
    }

    private static void Row(QuestPDF.Fluent.TableDescriptor t, params string[] values)
    {
        foreach (var v in values)
            t.Cell().Border(0.5f).BorderColor(Colors.Black).Padding(3).Text(v).FontFamily(BodyFont).FontSize(BodySize);
    }
}
