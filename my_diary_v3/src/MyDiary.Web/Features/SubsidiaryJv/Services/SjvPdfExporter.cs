using System.Globalization;
using System.Text;
using MyDiary.Web.Features.SubsidiaryJv.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyDiary.Web.Features.SubsidiaryJv.Services;

/// <summary>A4 PDF of one entity's page (QuestPDF, same engine and Community licence as the Recon PDFs).
/// Sections are kept together (ShowEntire) so tables are not cut across pages; each page has a header and a
/// "Page x of y" footer.</summary>
public static class SjvPdfExporter
{
    private const string Navy = "#1E3A8A", Red = "#DC2626", Grey = "#6B7280", Line = "#D5DEEE", Head = "#EAF0FB";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="logoBytes">The entity's own logo (PNG/JPG), if one has been uploaded. Shown small in the
    /// header and footer instead of any Bank branding — this document is the entity's own report.</param>
    public static byte[] Build(SjvEntityDetail d, byte[]? logoBytes = null)
    {
        var e = d.Entity;
        var latest = d.Financials.LastOrDefault();
        var prev = d.Financials.Count > 1 ? d.Financials[^2] : null;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28, Unit.Point);
            page.MarginTop(24); page.MarginBottom(24);
            page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

            page.Header().PaddingBottom(6).BorderBottom(1).BorderColor(Line).Row(r =>
            {
                if (logoBytes is not null) r.ConstantItem(22).Height(22).Image(logoBytes).FitArea();
                r.RelativeItem().PaddingLeft(logoBytes is null ? 0 : 6).AlignMiddle().Text(e.EntityName + "  |  Subsidiaries and JV Corner").FontSize(8).FontColor(Grey);
                r.ConstantItem(150).AlignRight().Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(8).FontColor(Grey);
            });

            page.Footer().PaddingTop(4).BorderTop(1).BorderColor(Line).Row(r =>
            {
                if (logoBytes is not null) r.ConstantItem(16).Height(16).Image(logoBytes).FitArea();
                r.RelativeItem().PaddingLeft(logoBytes is null ? 0 : 5).AlignMiddle().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(Grey));
                    t.Span(e.EntityName + "   |   Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages();
                });
            });

            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(9);

                col.Item().Column(c =>
                {
                    c.Item().Text(e.EntityName).FontSize(18).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(e.Tagline)) c.Item().Text(e.Tagline).FontSize(10).FontColor("#2563EB");
                    c.Item().PaddingTop(2).Text($"{e.TypeLabel}  •  Status: {e.Status ?? "Active"}").FontSize(8).FontColor(Grey);
                });

                // KPI strip
                col.Item().ShowEntire().Row(r =>
                {
                    r.Spacing(5);
                    Kpi(r, "Year of Incorporation", e.YearOfIncorp?.ToString() ?? "-", null);
                    Kpi(r, "Ownership", e.OwnershipPct.HasValue ? e.OwnershipPct.Value.ToString("0.##") + "%" : "-", e.OwnershipNote);
                    Kpi(r, "Paid-up Capital", e.PaidupCapitalCr.HasValue ? "Rs " + e.PaidupCapitalCr.Value.ToString("N0") + " Cr" : "-", AsOn(e.PaidupAsOn));
                    Kpi(r, "Branches", e.Branches?.ToString("N0") ?? "-", e.BranchesNote);
                    Kpi(r, "Employees", e.Employees.HasValue ? e.Employees.Value.ToString("N0") + "+" : "-", AsOn(e.EmployeesAsOn));
                    Kpi(r, latest is null ? "Total Income" : $"Total Income ({latest.FinYear})", latest?.TotalIncome is decimal ti ? "Rs " + ti.ToString("N0") + " Cr" : "-", null);
                });

                // Company profile
                col.Item().ShowEntire().Element(c => Section(c, "Company Profile", s =>
                {
                    s.Item().Text(string.IsNullOrWhiteSpace(e.Description) ? "-" : e.Description).LineHeight(1.3f);
                    s.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(x => { x.ConstantColumn(95); x.RelativeColumn(); });
                        void Kv(string k, string? v) { t.Cell().PaddingVertical(1.5f).Text(k).FontColor(Grey); t.Cell().PaddingVertical(1.5f).Text(string.IsNullOrWhiteSpace(v) ? "-" : v); }
                        Kv("Registered Office", e.RegisteredOffice); Kv("CIN", e.Cin); Kv("Website", e.Website); Kv("Email", e.Email); Kv("Phone", e.Phone);
                    });
                }));

                // Office presence + Shareholding side by side
                col.Item().ShowEntire().Row(r =>
                {
                    r.Spacing(8);
                    r.RelativeItem().Element(c => Section(c, "Office Presence", s =>
                    {
                        if (d.Offices.Count == 0) { s.Item().Text("-"); return; }
                        s.Item().Table(t =>
                        {
                            t.ColumnsDefinition(x => { x.RelativeColumn(); x.ConstantColumn(60); });
                            t.Header(h => { HCell(h.Cell(), "Location Type"); HCell(h.Cell(), "Number"); });
                            foreach (var o in d.Offices) { BCell(t.Cell(), o.LocationType); BCell(t.Cell(), o.Number.ToString("N0")); }
                        });
                    }));
                    r.RelativeItem().Element(c => Section(c, "Shareholding Pattern", s =>
                    {
                        if (d.Shareholding.Count == 0) { s.Item().Text("-"); return; }
                        var colors = new[] { "#1E40AF", "#DC2626", "#0EA5E9", "#F59E0B", "#10B981", "#8B5CF6", "#64748B" };
                        s.Item().AlignCenter().Width(100).Height(100).Svg(DonutSvg(d.Shareholding, colors));
                        s.Item().PaddingTop(2).Column(l =>
                        {
                            for (int i = 0; i < d.Shareholding.Count; i++)
                                l.Item().Row(rr =>
                                {
                                    rr.ConstantItem(12).Text("■").FontColor(colors[i % colors.Length]);
                                    rr.RelativeItem().Text(d.Shareholding[i].HolderName);
                                    rr.ConstantItem(40).AlignRight().Text(d.Shareholding[i].Pct.ToString("0.##") + "%").Bold();
                                });
                        });
                    }));
                });

                // Key activities
                col.Item().ShowEntire().Element(c => Section(c, "Key Activities Undertaken", s =>
                {
                    if (d.Activities.Count == 0) { s.Item().Text("-"); return; }
                    s.Item().Table(t =>
                    {
                        t.ColumnsDefinition(x => { x.RelativeColumn(); x.RelativeColumn(); });
                        foreach (var a in d.Activities) t.Cell().PaddingVertical(1.5f).Text("•  " + a);
                    });
                }));

                // Board
                col.Item().ShowEntire().Element(c => Section(c, "Board of Directors", s =>
                {
                    s.Item().Table(t =>
                    {
                        t.ColumnsDefinition(x => { x.ConstantColumn(18); x.RelativeColumn(2.2f); x.RelativeColumn(1.6f); x.RelativeColumn(1.8f); x.RelativeColumn(1.8f); x.RelativeColumn(1.3f); x.RelativeColumn(1f); });
                        t.Header(h => { foreach (var x in new[] { "#", "Name", "Designation", "Director Category", "Representing Partner / Bank", "Date of Appointment", "Tenure" }) HCell(h.Cell(), x); });
                        int i = 0;
                        foreach (var b in d.Board)
                        {
                            i++; BCell(t.Cell(), i.ToString()); BCell(t.Cell(), b.Name); BCell(t.Cell(), b.Designation); BCell(t.Cell(), b.Category);
                            BCell(t.Cell(), b.Representing ?? "-"); BCell(t.Cell(), b.AppointedOn?.ToString("dd MMM yyyy") ?? "-"); BCell(t.Cell(), b.Tenure);
                        }
                        if (d.Board.Count == 0) { t.Cell().ColumnSpan(7).Padding(4).Text("-"); }
                    });
                }));

                // KMP
                col.Item().ShowEntire().Element(c => Section(c, "Key Managerial Personnel", s =>
                {
                    s.Item().Table(t =>
                    {
                        t.ColumnsDefinition(x => { x.ConstantColumn(18); x.RelativeColumn(2); x.RelativeColumn(2); x.RelativeColumn(2.4f); });
                        t.Header(h => { foreach (var x in new[] { "#", "Name", "Designation", "Email" }) HCell(h.Cell(), x); });
                        int i = 0;
                        foreach (var k in d.Kmp) { i++; BCell(t.Cell(), i.ToString()); BCell(t.Cell(), k.Name); BCell(t.Cell(), k.Designation); BCell(t.Cell(), k.Email); }
                        if (d.Kmp.Count == 0) { t.Cell().ColumnSpan(4).Padding(4).Text("-"); }
                    });
                }));

                // Key financials + trend
                col.Item().ShowEntire().Element(c => Section(c, $"Key Financials (Rs Cr){(latest is null ? "" : "  " + latest.FinYear)}", s =>
                {
                    s.Item().Row(r =>
                    {
                        r.Spacing(5);
                        Fin(r, "Total Income", latest?.TotalIncome, prev?.TotalIncome);
                        Fin(r, "Net Profit", latest?.NetProfit, prev?.NetProfit);
                        Fin(r, "Net Worth", latest?.NetWorth, prev?.NetWorth);
                        Fin(r, "Total Assets", latest?.TotalAssets, prev?.TotalAssets);
                    });
                }));

                col.Item().ShowEntire().Element(c => Section(c, "Financial Trend (Rs Cr)", s =>
                {
                    if (d.Financials.Count == 0) { s.Item().Text("-"); return; }
                    s.Item().AspectRatio(600f / 250f).Svg(TrendSvg(d.Financials));
                    s.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(x => { x.RelativeColumn(1.6f); foreach (var _ in d.Financials) x.RelativeColumn(); x.RelativeColumn(1.1f); });
                        t.Header(h => { HCell(h.Cell(), "Particulars"); foreach (var f in d.Financials) HCell(h.Cell(), f.FinYear); HCell(h.Cell(), "Growth vs previous year"); });
                        void R(string label, Func<SjvFinancial, decimal?> sel)
                        {
                            var v = d.Financials.Select(sel).ToList();
                            BCell(t.Cell(), label);
                            foreach (var x in v) BCell(t.Cell(), x.HasValue ? x.Value.ToString("N0") : "-");
                            GrowthCell(t.Cell(), v.Count > 1 ? v[^1] : null, v.Count > 1 ? v[^2] : null);
                        }
                        R("Total Income", f => f.TotalIncome); R("Net Profit", f => f.NetProfit); R("Net Worth", f => f.NetWorth); R("Total Assets", f => f.TotalAssets);
                    });
                }));

                // Quarterly (optional - only when the entity has quarter-wise figures)
                if (d.Quarterly.Count > 0)
                    col.Item().ShowEntire().Element(c => Section(c, "Quarterly Financials (Rs Cr)  -  Q1 Apr-Jun, Q2 Jul-Sep, Q3 Oct-Dec, Q4 Jan-Mar", s =>
                    {
                        s.Item().AspectRatio(600f / 250f).Svg(TrendSvg(d.Quarterly.Select(q => q.AsFinancial()).ToList()));
                        s.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(x => { x.RelativeColumn(1.6f); foreach (var _ in d.Quarterly) x.RelativeColumn(); x.RelativeColumn(1.4f); });
                            t.Header(h => { HCell(h.Cell(), "Particulars"); foreach (var q in d.Quarterly) HCell(h.Cell(), q.Label); HCell(h.Cell(), "Change vs previous quarter"); });
                            void R(string label, Func<SjvQuarterly, decimal?> sel)
                            {
                                var v = d.Quarterly.Select(sel).ToList();
                                BCell(t.Cell(), label);
                                foreach (var x in v) BCell(t.Cell(), x.HasValue ? x.Value.ToString("N0") : "-");
                                GrowthCell(t.Cell(), v.Count > 1 ? v[^1] : null, v.Count > 1 ? v[^2] : null);
                            }
                            R("Total Income", q => q.TotalIncome); R("Net Profit", q => q.NetProfit); R("Net Worth", q => q.NetWorth); R("Total Assets", q => q.TotalAssets);
                        });
                    }));

                // Updates
                col.Item().ShowEntire().Element(c => Section(c, "Current Position / Strategic Updates", s =>
                {
                    if (d.Updates.Count == 0) { s.Item().Text("-"); return; }
                    foreach (var u in d.Updates)
                        s.Item().PaddingVertical(1.5f).Text(t => { t.Span("•  " + u.Text); if (u.Date.HasValue) t.Span($"  ({u.Date.Value:dd MMM yyyy})").FontColor(Grey); });
                }));
            });
        })).GeneratePdf();
    }

    // ---- building blocks ----
    private static string? AsOn(string? s) => string.IsNullOrWhiteSpace(s) ? null : "As on " + s;

    private static void Section(IContainer c, string title, Action<ColumnDescriptor> body) =>
        c.Border(0.7f).BorderColor(Line).Padding(7).Column(col =>
        {
            col.Item().PaddingBottom(4).Text(title).Bold().FontSize(11).FontColor(Navy);
            body(col);
        });

    private static void Kpi(RowDescriptor r, string title, string value, string? note) =>
        r.RelativeItem().Border(0.7f).BorderColor(Line).Padding(5).Column(c =>
        {
            c.Item().Text(title).FontSize(7).FontColor(Grey);
            c.Item().Text(value).FontSize(11).Bold().FontColor(Navy);
            if (note != null) c.Item().Text(note).FontSize(6.5f).FontColor(Grey);
        });

    private static string Signed(decimal v) => (v > 0 ? "+" : "") + v.ToString("N0");

    private static void Fin(RowDescriptor r, string label, decimal? cur, decimal? prev)
    {
        var g = SubsidiaryJvService.Growth(cur, prev);
        var ch = SubsidiaryJvService.Change(cur, prev);
        r.RelativeItem().Background("#F8FAFC").Padding(6).Column(c =>
        {
            c.Item().Text(label).FontSize(7.5f).FontColor(Grey);
            c.Item().Text(cur.HasValue ? cur.Value.ToString("N0") + " Cr" : "-").Bold().FontSize(11);
            if (ch.HasValue && (g.HasValue || ch.Value != 0))
                c.Item().Text($"{(ch < 0 ? "▼" : "▲")} {(g.HasValue ? Math.Abs(g.Value).ToString("0.0") + "% " : "")}({Signed(ch.Value)} Cr)").FontSize(8).Bold().FontColor(ch < 0 ? Red : "#16A34A");
        });
    }

    /// <summary>Growth % (vs |previous|) plus the signed amount change, e.g. "150.0% ▲ (+900)" for -600 -> 300.</summary>
    private static void GrowthCell(IContainer c, decimal? cur, decimal? prev)
    {
        var g = SubsidiaryJvService.Growth(cur, prev);
        var ch = SubsidiaryJvService.Change(cur, prev);
        c.Border(0.5f).BorderColor(Line).Padding(3)
         .Text(ch.HasValue ? $"{(g.HasValue ? Math.Abs(g.Value).ToString("0.0") + "% " : "")}{(ch < 0 ? "▼" : "▲")} ({Signed(ch.Value)})" : "-")
         .Bold().FontSize(8.5f).FontColor(ch < 0 ? Red : "#16A34A");
    }

    private static void HCell(IContainer c, string text) =>
        c.Background(Head).Border(0.5f).BorderColor(Line).Padding(3).Text(text).Bold().FontSize(8);

    private static void BCell(IContainer c, string? text) =>
        c.Border(0.5f).BorderColor(Line).Padding(3).Text(text ?? "").FontSize(8.5f);

    // ---- SVG charts ----
    private static string F(double v) => v.ToString("0.##", Inv);

    private static string DonutSvg(List<SjvHolder> data, string[] colors)
    {
        var total = (double)Math.Max(0.0001m, data.Sum(x => x.Pct));
        const double r = 36, c = 2 * Math.PI * r;
        var sb = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 120'>");
        double off = 0;
        for (int i = 0; i < data.Count; i++)
        {
            var len = (double)data[i].Pct / total * c;
            sb.Append($"<circle cx='60' cy='60' r='{F(r)}' fill='none' stroke='{colors[i % colors.Length]}' stroke-width='16' stroke-dasharray='{F(len)} {F(c - len)}' stroke-dashoffset='{F(-off)}' transform='rotate(-90 60 60)'/>");
            off += len;
        }
        sb.Append($"<text x='60' y='65' text-anchor='middle' font-size='15' font-weight='bold' fill='#111827'>{data.Sum(x => x.Pct):0.##}%</text></svg>");
        return sb.ToString();
    }

    private static string TrendSvg(List<SjvFinancial> data)
    {
        const double W = 600, H = 250, L = 46, R = 12, T = 22, B = 30;
        double pw = W - L - R, ph = H - T - B;
        // Axis spans the data including negatives (losses); bars grow from the zero line.
        var vals = data.SelectMany(x => new[] { x.TotalIncome, x.NetProfit, x.NetWorth }).Where(v => v.HasValue).Select(v => (double)v!.Value).ToList();
        var dMin = Math.Min(0d, vals.Count == 0 ? 0d : vals.Min());
        var dMax = Math.Max(0d, vals.Count == 0 ? 0d : vals.Max());
        if (dMax - dMin < 1) dMax = dMin + 1;
        var pow = Math.Pow(10, Math.Floor(Math.Log10((dMax - dMin) / 4)));
        var n = (dMax - dMin) / 4 / pow; var step = (n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : 10) * pow;
        var lo = Math.Floor(dMin / step) * step; var hi = Math.Ceiling(dMax / step) * step;
        if (hi <= lo) hi = lo + step;
        var lines = (int)Math.Round((hi - lo) / step);
        double Y(double v) => T + ph - (v - lo) / (hi - lo) * ph;
        double y0 = Y(0);
        double gw = pw / data.Count;
        var sb = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {F(W)} {F(H)}'>");
        sb.Append($"<rect x='{L}' y='4' width='9' height='9' fill='#DC2626'/><text x='{L + 13}' y='12' font-size='10' fill='#374151'>Total Income</text>");
        sb.Append($"<rect x='{L + 90}' y='4' width='9' height='9' fill='#1E3A8A'/><text x='{L + 103}' y='12' font-size='10' fill='#374151'>Net Profit</text>");
        sb.Append($"<rect x='{L + 170}' y='7' width='16' height='2' fill='#2563EB'/><text x='{L + 190}' y='12' font-size='10' fill='#374151'>Net Worth</text>");
        for (int g = 0; g <= lines; g++)
        {
            var val = lo + step * g; var y = Y(val);
            sb.Append($"<line x1='{L}' x2='{F(W - R)}' y1='{F(y)}' y2='{F(y)}' stroke='{(Math.Abs(val) < step / 1000 ? "#9CA3AF" : "#E5E7EB")}'/><text x='{L - 5}' y='{F(y + 3)}' text-anchor='end' font-size='9' fill='#6B7280'>{val.ToString("N0", Inv)}</text>");
        }
        var pts = new List<string>();
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i]; double cx = L + gw * i + gw / 2; double bw = Math.Min(20, gw / 4);
            if (d.TotalIncome.HasValue) { var y = Y((double)d.TotalIncome.Value); sb.Append($"<rect x='{F(cx - bw - 1)}' y='{F(Math.Min(y, y0))}' width='{F(bw)}' height='{F(Math.Abs(y0 - y))}' fill='#DC2626'/>"); }
            if (d.NetProfit.HasValue) { var y = Y((double)d.NetProfit.Value); sb.Append($"<rect x='{F(cx + 1)}' y='{F(Math.Min(y, y0))}' width='{F(bw)}' height='{F(Math.Abs(y0 - y))}' fill='{(d.NetProfit.Value < 0 ? "#B45309" : "#1E3A8A")}'/>"); }
            sb.Append($"<text x='{F(cx)}' y='{F(H - 10)}' text-anchor='middle' font-size='9' fill='#374151'>{d.FinYear}</text>");
            if (d.NetWorth.HasValue) pts.Add($"{F(cx)},{F(Y((double)d.NetWorth.Value))}");
        }
        if (pts.Count > 1) sb.Append($"<polyline points='{string.Join(" ", pts)}' fill='none' stroke='#2563EB' stroke-width='2'/>");
        foreach (var p in pts) { var xy = p.Split(','); sb.Append($"<circle cx='{xy[0]}' cy='{xy[1]}' r='3' fill='#fff' stroke='#2563EB' stroke-width='2'/>"); }
        sb.Append("</svg>");
        return sb.ToString();
    }
}
