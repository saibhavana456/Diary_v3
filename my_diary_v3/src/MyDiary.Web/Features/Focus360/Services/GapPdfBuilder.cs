using System.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MyDiary.Web.Features.Focus360.Models;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace MyDiary.Web.Features.Focus360.Services;

// ============================================================================
//  FOCUS 360 – PDF Report Builder  (A4 Portrait, single-page-optimised)
//
//  Usage:
//      var bytes = Focus360PdfService.Build(reportViewModel, settings,
//                      appLogoPath: "wwwroot/images/app-logo.png",
//                      coLogoPath:  "wwwroot/images/logo.png");
//
//  Section order (mirrors the web page):
//   1. Branch Details
//   2. Staff Strength
//   3. Performance Pivot  (TOTAL_BUSINESS / DEPOSITS / ADVANCES / PRIORITY SECTOR)
//   4. Business Activity  – Loan Sanctions | Deposit Account Activity
//   5. Asset Quality      – NPA (Gross) | SMA / Stress
//   6. Operations         – Income | Lockers | ATMs + KYC
//   7. Digital Banking    – Registered vs Eligible
//   8. Digital Loans      – tabular
//   9. Financial Inclusion + Third Party Products
//  10. JanSamarth         – 6-column (Scheme / Total / Sanctioned / Disbursed / Rejected / Pending)
// ============================================================================
public static class GapPdfBuilder
{
    // ── Colours ──────────────────────────────────────────────────────────────
    private static readonly Color C_BRANCH = Color.FromHex("#0D47A1");
    private static readonly Color C_STAFF = Color.FromHex("#1565C0");
    private static readonly Color C_PERF = Color.FromHex("#004D40");
    private static readonly Color C_BIZ = Color.FromHex("#2E7D32");
    private static readonly Color C_AQ = Color.FromHex("#B71C1C");
    private static readonly Color C_OPS = Color.FromHex("#37474F");
    private static readonly Color C_DIGBK = Color.FromHex("#006064");
    private static readonly Color C_DIGLOAN = Color.FromHex("#1A237E");
    private static readonly Color C_FI = Color.FromHex("#33691E");
    private static readonly Color C_JS = Color.FromHex("#880E4F");

    private static readonly Color BORDER_H = Color.FromHex("#90A4AE");
    private static readonly Color BORDER_L = Color.FromHex("#E0E0E0");
    private static readonly Color NEG_C = Color.FromHex("#C62828");
    private static readonly Color POS_C = Color.FromHex("#1B5E20");

    // ── Font size constants ───────────────────────────────────────────────────
    private const float F5 = 5f, F55 = 5.5f, F6 = 6f, F7 = 7f, F9 = 9f;
    private const float P1 = 1f, P15 = 1.5f, P2 = 2f, P3 = 3f;

    // ── Entry point ───────────────────────────────────────────────────────────
    /// <summary>
    /// Generates the Focus 360 PDF and returns the bytes.
    /// </summary>
    /// <param name="r">Populated view-model.</param>
    /// <param name="s">Settings (report name, bank name, logo paths, footer text).</param>
    /// <param name="appLogoPath">Absolute path to app/portal logo file (null = show text placeholder).</param>
    /// <param name="coLogoPath">Absolute path to bank/company logo file (null = show text placeholder).</param>
    public static byte[] Build(GapReportViewModel r, GapReportSettings s,
                                string? appLogoPath = null, string? coLogoPath = null,
                                DataSet? dashboardData = null, bool isBranchLevel = true)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var asOnLbl = r.AsOnDate?.ToString("dd-MMM-yy") ?? r.GeneratedAt.ToString("dd-MMM-yy");

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(10);
            page.MarginBottom(8);
            page.MarginHorizontal(18);
            page.DefaultTextStyle(x => x.FontSize(F6).FontFamily("Arial"));

            page.Header().Element(e => RenderHeader(e, r, s, appLogoPath, coLogoPath, asOnLbl));

            page.Content().Column(col =>
            {
                col.Spacing(2);
                col.Item().Element(e => BranchGrid(e, r, asOnLbl));

                if (r.Staff.Count > 0)
                    col.Item().Element(e => StaffRow(e, r, isBranchLevel));

                if (r.Performance.Count > 0)
                    col.Item().Element(e => PerfTable(e, r, asOnLbl));

                if (r.LoanActivity.Any(x => x.HasData) || r.DepositActivity.Any(x => x.HasData))
                    col.Item().Element(e => BizActivity(e, r, asOnLbl));

                if (r.NpaRows.Count > 0 || r.SmaRows.Count > 0)
                    col.Item().Element(e => AssetQuality(e, r, asOnLbl));

                if (r.IncomeRows.Count > 0 || r.LockerRows.Count > 0 || r.ChannelRows.Count > 0)
                    col.Item().Element(e => Operations(e, r));

                if (r.DigitalBankingTable.Count > 0 || r.DigitalLoans.Count > 0 || r.ThirdParty.Count > 0)
                    col.Item().Element(e => DigitalAndThirdParty(e, r));

                //if (r.FinancialInclusion.Count > 0) // || r.JanSamarthTable.Any(x => x.HasData)
                //    col.Item().Element(e => FiAndJanSamarth(e, r, asOnLbl));

                if (dashboardData != null)
                    col.Item().Element(e => OperationsDashboard(e, dashboardData));
            });

            page.Footer().Element(e => RenderFooter(e, s));
        })).GeneratePdf();
    }

    // ── Shared section-header helpers ─────────────────────────────────────────
    private static void SecHdr(IContainer c, Color col, string title) =>
        c.Background(col).PaddingVertical(P15).PaddingHorizontal(P2)
         .Text(title).FontColor(Colors.White).Bold().FontSize(F6);

    private static void SubHdr(IContainer c, Color col, string title) =>
        c.Background(col).PaddingVertical(P1).PaddingHorizontal(P2)
         .Text(title).FontColor(Colors.White).Bold().FontSize(F55);

    // ── HEADER ────────────────────────────────────────────────────────────────
    private static void RenderHeader(IContainer c, GapReportViewModel r, GapReportSettings s,
                                      string? aLogo, string? cLogo, string asOnLbl)
    {
        c.Background(Colors.White).PaddingVertical(P3).PaddingHorizontal(P3).Row(row =>
        {
            // Left logo
            if (!string.IsNullOrEmpty(aLogo) && File.Exists(aLogo))
                row.ConstantItem(44).AlignMiddle().Image(aLogo).FitArea();
            else
                row.ConstantItem(44).AlignMiddle()
                   .Background(Colors.White).Border(1).BorderColor(C_STAFF)
                   .AlignCenter().AlignMiddle().Text("F360").Bold().FontSize(F7).FontColor(C_STAFF);

            row.ConstantItem(5);

            // Centre text
            row.RelativeItem().AlignMiddle().Column(col =>
            {
                col.Item().AlignCenter()
                   .Text(s.ReportName).FontSize(F9).Bold().FontColor(C_BRANCH);
                if (!string.IsNullOrEmpty(s.BankName))
                    col.Item().AlignCenter()
                       .Text(s.BankName).FontSize(F9).FontColor(C_STAFF);
                col.Item().AlignCenter()
                   .Text($" As On: {asOnLbl}   |   Generated: {r.GeneratedAt:dd-MMM-yyyy HH:mm}")
                   .FontSize(F6).FontColor(C_STAFF);
            });

            row.ConstantItem(5);

            // Right logo
            if (!string.IsNullOrEmpty(cLogo) && File.Exists(cLogo))
                row.ConstantItem(90).AlignMiddle().Image(cLogo).FitArea();
            else
                row.ConstantItem(90).AlignMiddle()
                   .Background(Colors.White).Border(1).BorderColor(C_STAFF)
                   .AlignCenter().AlignMiddle().Text("LOGO").Bold().FontSize(F7).FontColor(C_STAFF);
        });
    }

    // ── 1. BRANCH GRID ────────────────────────────────────────────────────────
    private static void BranchGrid(IContainer c, GapReportViewModel r, string asOnLbl)
    {
        var b = r.Branch;
        c.Column(col =>
        {
            col.Item().Element(e => SecHdr(e, C_BRANCH, $"Branch / ZO Details — As On {asOnLbl}"));
            col.Item().Border(0.5f).BorderColor(BORDER_H).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(32); cd.RelativeColumn(2f);
                    cd.ConstantColumn(28); cd.RelativeColumn(2f);
                    cd.ConstantColumn(36); cd.RelativeColumn(2f);
                    cd.ConstantColumn(38); cd.RelativeColumn(1.4f);
                });

                void L(string lbl, int row = 0) =>
                    t.Cell().Background(row % 2 == 0 ? Color.FromHex("#E3F2FD") : Colors.White)
                     .BorderBottom(0.25f).BorderColor(BORDER_L).Padding(P15)
                     .Text(lbl).Bold().FontSize(F55).FontColor(C_BRANCH);

                void V(string val, int row = 0) =>
                    t.Cell().Background(row % 2 == 0 ? Color.FromHex("#E3F2FD") : Colors.White)
                     .BorderBottom(0.25f).BorderColor(BORDER_L).Padding(P15)
                     .Text(val).FontSize(F6);

                L("Branch"); V(b.BranchName);
                L("Code"); V(b.BranchCode);
                L("Region"); V(b.RegionName);
                L("Zone"); V(b.ZoneName);
               

                L("BM", 1); V($"{r.Branch.ZMBMName} ({r.Branch.ZMBMCode})", 1);
                //L("Scale", 1); V(b.Scale, 1);
                //
                L("Working Since", 1); V(b.WorkingSince?.ToString("dd-MMM-yy") ?? "—", 1);
                L("Open Date", 1); V(b.BranchOpenDate?.ToString("dd-MMM-yy") ?? "—", 1);

                L("License No.", 1); V($"{b.License}", 1);

                //L("Guardian", 2); V(b.GuardianExec, 2);
                //L("Timings", 2); V(b.BranchTimeDisplay, 2);
                L("", 2); V("", 2); L("", 2); V("", 2);
            });
        });
    }

    // ── 2. STAFF ROW ──────────────────────────────────────────────────────────
    private static void StaffRow(IContainer c, GapReportViewModel r, bool isBranchLevel = true)
    {
        var grades = r.Staff.OrderBy(x => x.SortOrder).ToList();
        c.Column(col =>
        {
            col.Item().Element(e => SecHdr(e, C_STAFF, "Staff Strength & Key Metrics"));
            col.Item().Border(0.5f).BorderColor(BORDER_H).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    foreach (var _ in grades) cd.RelativeColumn();
                    cd.RelativeColumn(1.3f);
                    if (isBranchLevel)
                    {
                        cd.RelativeColumn(1.3f);
                    }
                });

                foreach (var g in grades)
                    t.Cell().Background(Color.FromHex("#BBDEFB")).BorderRight(0.25f).BorderColor(BORDER_L)
                     .Padding(P1).AlignCenter().Text(g.GradeCode).Bold().FontSize(F55).FontColor(C_BRANCH);
                t.Cell().Background(Color.FromHex("#90CAF9")).BorderRight(0.25f).BorderColor(BORDER_L)
                 .Padding(P1).AlignCenter().Text("Total").Bold().FontSize(F55).FontColor(C_BRANCH);
                if (isBranchLevel)
                {
                    t.Cell().Background(Color.FromHex("#A5D6A7"))
                     .Padding(P1).AlignCenter().Text("PEB (Cr)").Bold().FontSize(F5).FontColor(C_BIZ);
                }

                foreach (var g in grades)
                    t.Cell().BorderRight(0.25f).BorderColor(BORDER_L)
                     .Padding(P1).AlignCenter().Text(g.HeadCount.ToString()).FontSize(F6);
                t.Cell().BorderRight(0.25f).BorderColor(BORDER_L)
                 .Padding(P1).AlignCenter().Text(r.TotalStaff.ToString()).Bold().FontSize(F6).FontColor(C_BRANCH);
                if (isBranchLevel)
                {
                    t.Cell().Padding(P1).AlignCenter()
                     .Text(r.PerEmployeeBusiness.ToString("N2")).FontSize(F6).FontColor(C_BIZ);
                }               
            });
        });
    }

    // ── 3. PERFORMANCE PIVOT TABLE ────────────────────────────────────────────
    private static void PerfTable(IContainer c, GapReportViewModel r, string asOnLbl)
    {
        c.Column(col =>
        {
            col.Item().Element(e => SecHdr(e, C_PERF,
                $"Performance Parameters (₹ in Crore) — As On {asOnLbl}"));
            col.Item().Border(0.5f).BorderColor(BORDER_H).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2.6f);
                    cd.RelativeColumn(0.45f);
                    cd.RelativeColumn(1f);
                    cd.RelativeColumn(1f);
                    cd.RelativeColumn(1f);
                    cd.RelativeColumn(1f);
                    cd.RelativeColumn(1f);
                    cd.RelativeColumn(0.75f);
                });

                t.Header(h =>
                {
                    foreach (var hh in new[] {
                        "Parameter", "Unit", "Last FY", "Curr FY Base",
                        $"{asOnLbl}\nActual", "FY Target", "Gap to Target", "Gap to Target%"
                    })
                        h.Cell().Background(C_PERF).PaddingVertical(P15).PaddingHorizontal(P1)
                         .AlignCenter().Text(hh).FontColor(Colors.White).Bold().FontSize(F5);
                });

                string? lastCat = null;
                int ri = 0;
                foreach (var p in r.Performance)
                {
                    if (p.Category != lastCat)
                    {
                        var cc = Color.FromHex(r.GetCategoryColor(p.Category));
                        t.Cell().ColumnSpan(8).Background(cc)
                         .PaddingVertical(P1).PaddingHorizontal(P2)
                         .Text(p.Category).Bold().FontSize(F55).FontColor(Colors.White);
                        lastCat = p.Category;
                    }

                    var bg = ri++ % 2 == 0 ? Colors.White : Color.FromHex("#F5F5F5");

                    void DC(string? v, bool neg = false, bool pos = false)
                    {
                        t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                         .PaddingVertical(P1).PaddingHorizontal(P1)
                         .AlignRight().Text(td =>
                         {
                             var sp = td.Span(v ?? "—").FontSize(F55);
                             if (neg) sp.FontColor(NEG_C).Bold();
                             if (pos) sp.FontColor(POS_C);
                         });
                    }

                    void DCL(string v) =>
                        t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                         .PaddingVertical(P1).PaddingHorizontal(P1)
                         .Text(v).FontSize(F55);

                    DCL(p.ParameterName);
                    DC(p.UnitLabel);
                    DC(Fmt(p.BaseLastFyCr));
                    DC(Fmt(p.BaseCurrentFyCr));
                    DC(Fmt(p.ActualCr));
                    DC(Fmt(p.TargetCr));
                    DC(Fmt(p.Variance),
                       neg: p.Variance.HasValue && p.Variance.Value < 0,
                       pos: p.Variance.HasValue && p.Variance.Value > 0);
                    DC(p.VariancePct.HasValue ? $"{p.VariancePct:F1}%" : "—",
                       neg: p.VariancePct.HasValue && p.VariancePct.Value < 0,
                       pos: p.VariancePct.HasValue && p.VariancePct.Value > 0);
                }
            });
        });
    }

    // ── 4. BUSINESS ACTIVITY ─────────────────────────────────────────────────
    private static void BizActivity(IContainer c, GapReportViewModel r, string asOnLbl)
    {
        bool hasL = r.LoanActivity.Any(x => x.HasData);
        bool hasD = r.DepositActivity.Any(x => x.HasData);

        c.Column(col =>
        {
            col.Item().Element(e => SecHdr(e, C_BIZ,
                $"Business Activity — Current Month (as on {asOnLbl})"));
            col.Item().Border(0.5f).BorderColor(BORDER_H).Row(row =>
            {
                if (hasL)
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Element(e => SubHdr(e, Color.FromHex("#388E3C"), "Loan Sanctions & Disbursements"));
                        inner.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            { cd.RelativeColumn(1.2f); cd.RelativeColumn(1f); cd.RelativeColumn(1f); });
                            t.Header(h =>
                            {
                                foreach (var hh in new[] { "Segment", "Sanctioned (No.)", "Disbursed (Cr)" })
                                    h.Cell().Background(Color.FromHex("#4CAF50")).Padding(P1)
                                     .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                            });
                            int i = 0;
                            foreach (var lr in r.LoanActivity)
                            {
                                var bg = i++ % 2 == 0 ? Color.FromHex("#F1F8E9") : Colors.White;
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).Text(lr.Segment).Bold().FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(lr.Sanctioned).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(lr.Disbursed).FontSize(F55);
                            }
                        });
                    });
                }

                if (hasL && hasD) row.ConstantItem(4);

                if (hasD)
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Element(e => SubHdr(e, Color.FromHex("#1976D2"), "Deposit Account Activity"));
                        inner.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            { cd.RelativeColumn(1.4f); cd.RelativeColumn(1f); cd.RelativeColumn(1f); });
                            t.Header(h =>
                            {
                                foreach (var hh in new[] { "Segment", "A/cs Opened (No.)", "Amount (Cr)" })
                                    h.Cell().Background(Color.FromHex("#2196F3")).Padding(P1)
                                     .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                            });
                            int i = 0;
                            foreach (var dr in r.DepositActivity)
                            {
                                var bg = i++ % 2 == 0 ? Color.FromHex("#E3F2FD") : Colors.White;
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).Text(dr.Segment).Bold().FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(dr.Accounts).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(dr.Amount).FontSize(F55);
                            }
                        });
                    });
                }
            });
        });
    }

    // ── 5. ASSET QUALITY ─────────────────────────────────────────────────────
    private static void AssetQuality(IContainer c, GapReportViewModel r, string asOnLbl)
    {
        c.Column(col =>
        {
            col.Item().Element(e => SecHdr(e, C_AQ,
                $"Asset Quality — NPA & Stress Accounts (as on {asOnLbl})"));
            col.Item().Border(0.5f).BorderColor(BORDER_H).Row(row =>
            {
                if (r.NpaRows.Count > 0)
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Element(e => SubHdr(e, Color.FromHex("#C62828"), "NPA (Gross)"));
                        inner.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            { cd.RelativeColumn(2f); cd.RelativeColumn(1f); cd.RelativeColumn(1f); });
                            t.Header(h =>
                            {
                                foreach (var hh in new[] { "Type", "Last FY (Cr)", "Actual (Cr)" })
                                    h.Cell().Background(Color.FromHex("#D32F2F")).Padding(P1)
                                     .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                            });
                            int i = 0;
                            foreach (var p in r.NpaRows)
                            {
                                var bg = i++ % 2 == 0 ? Color.FromHex("#FFEBEE") : Colors.White;
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).Text(p.ParameterName).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(Fmt(p.BaseCurrentFyCr)).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(Fmt(p.ActualCr)).FontSize(F55).Bold().FontColor(C_AQ);
                            }
                        });
                    });
                    row.ConstantItem(4);
                }

                if (r.SmaRows.Count > 0)
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Element(e => SubHdr(e, Color.FromHex("#D32F2F"), "SMA / Stress Accounts"));
                        inner.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            { cd.RelativeColumn(2f); cd.RelativeColumn(1f); cd.RelativeColumn(1f); });
                            t.Header(h =>
                            {
                                foreach (var hh in new[] { "Category", "Amount (Cr)", "Percentage (%)" })
                                    h.Cell().Background(Color.FromHex("#E53935")).Padding(P1)
                                     .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                            });
                            var smaAmounts = r.SmaRows.Where(x => x.DataType != "PERCENTAGE").ToList();
                            var smaPcts = r.SmaRows.Where(x => x.DataType == "PERCENTAGE").ToList();
                            int i = 0;
                            foreach (var amt in smaAmounts)
                            {
                                var category = amt.ParameterName.Replace(" AMOUNT", "").Replace(" AMT", "").Trim();
                                var pctRow = smaPcts.FirstOrDefault(p => p.ParameterName.StartsWith(category, StringComparison.OrdinalIgnoreCase));
                                var bg = i++ % 2 == 0 ? Color.FromHex("#FFEBEE") : Colors.White;
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).Text(category).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(Fmt(amt.ActualCr)).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(pctRow?.ActualCr != null ? $"{pctRow.ActualCr:F2}" : "—").FontSize(F55);
                            }
                        });
                    });
                }
            });
        });
    }

    // ── 6. OPERATIONS ────────────────────────────────────────────────────────
    private static void Operations(IContainer c, GapReportViewModel r)
    {
        c.Column(col =>
        {
            col.Item().Element(e => SecHdr(e, C_OPS, "Operations & Channels"));
            col.Item().Border(0.5f).BorderColor(BORDER_H).Row(row =>
            {
                if (r.IncomeRows.Count > 0)
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Element(e => SubHdr(e, Color.FromHex("#455A64"), "Fee Income / Expense (Cr)"));
                        inner.Item().Element(e => SimpleCol(e, r.IncomeRows, Color.FromHex("#ECEFF1")));
                    });
                    row.ConstantItem(3);
                }
                if (r.LockerRows.Count > 0)
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Element(e => SubHdr(e, Color.FromHex("#455A64"), "Locker Status"));
                        inner.Item().Element(e => SimpleCol(e, r.LockerRows, Color.FromHex("#ECEFF1")));
                    });
                    row.ConstantItem(3);
                }
                if (r.ChannelRows.Count > 0)
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Element(e => SubHdr(e, Color.FromHex("#455A64"), "ATMs / Channels / KYC Pending"));
                        inner.Item().Element(e => SimpleCol(e, r.ChannelRows, Color.FromHex("#ECEFF1")));
                    });
                }
            });
        });
    }

    // ── 7 & 8. DIGITAL BANKING + DIGITAL LOANS + THIRD PARTY ─────────────────
    private static void DigitalAndThirdParty(IContainer c, GapReportViewModel r)
    {
        c.Column(col =>
        {
            col.Item().Element(e => SecHdr(e, C_DIGLOAN,
                "Digital Banking & Loans, Third Party Products and Financial Inclusion"));
            col.Item().Border(0.5f).BorderColor(BORDER_H).Row(row =>
            {
                // LEFT: Digital Banking (top) + Third Party (bottom)
                row.RelativeItem().Column(left =>
                {
                    if (r.DigitalBankingTable.Count > 0)
                    {
                        left.Item().Element(e => SubHdr(e, C_DIGBK, "Digital Banking — Registrations & Eligibility"));
                        left.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            { cd.RelativeColumn(2f); cd.RelativeColumn(1f); cd.RelativeColumn(1f); });
                            t.Header(h =>
                            {
                                foreach (var hh in new[] { "Product", "Registered (No.)", "Eligible (No.)" })
                                    h.Cell().Background(C_DIGBK).Padding(P1)
                                     .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                            });
                            int i = 0;
                            foreach (var db in r.DigitalBankingTable)
                            {
                                var bg = i++ % 2 == 0 ? Color.FromHex("#E0F7FA") : Colors.White;
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).Text(db.Product).Bold().FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(db.Registered).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(db.Eligible).FontSize(F55);
                            }
                        });
                    }

                    if (r.ThirdParty.Count > 0)
                    {
                        left.Item().Element(e => SubHdr(e, Color.FromHex("#6A1B9A"), "Third Party Products"));
                        left.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            { cd.RelativeColumn(2f); cd.RelativeColumn(1f); });
                            t.Header(h =>
                            {
                                foreach (var hh in new[] { "Product", "Actual"/*, "Target (Cr)"*/})
                                    h.Cell().Background(Color.FromHex("#7B1FA2")).Padding(P1)
                                     .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                            });
                            int i = 0;
                            foreach (var p in r.ThirdParty)
                            {
                                var bg = i++ % 2 == 0 ? Color.FromHex("#F3E5F5") : Colors.White;
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).Text(p.ParameterName).FontSize(F55);
                                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                 .Padding(P1).AlignRight().Text(p.ActualDisplay).FontSize(F55);
                                //t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                // .Padding(P1).AlignRight().Text(Fmt(p.TargetCr)).FontSize(F55);
                            }
                        });
                    }
                });

                row.ConstantItem(3);

                // RIGHT: Digital Loans

                row.RelativeItem().Column(right =>
                    {
                        if (r.DigitalLoans.Count > 0)
                        {
                            right.Item().Element(e => SubHdr(e, Color.FromHex("#283593"), "Digital Loans (₹ in Crore)"));
                            right.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cd =>
                                { cd.RelativeColumn(2.2f); cd.RelativeColumn(1f); });
                                t.Header(h =>
                                {
                                    foreach (var hh in new[] { "Loan Type", "Actual (Cr)"/*, "Target (Cr)"*/ })
                                        h.Cell().Background(C_DIGLOAN).Padding(P1)
                                         .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                                });
                                int i = 0;
                                foreach (var p in r.DigitalLoans)
                                {
                                    var bg = i++ % 2 == 0 ? Color.FromHex("#E8EAF6") : Colors.White;
                                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                     .Padding(P1).Text(p.ParameterName).FontSize(F55);
                                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                     .Padding(P1).AlignRight().Text(Fmt(p.ActualCr)).FontSize(F55);
                                    //t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                    // .Padding(P1).AlignRight().Text(Fmt(p.TargetCr) == "0.00" ? "—" : Fmt(p.TargetCr)).FontSize(F55);
                                }
                            });
                        }

                        if (r.FinancialInclusion.Count > 0)
                        {
                            right.Item().Element(e => SubHdr(e, Color.FromHex("#558B2F"), "Govt Schemes & Financial Inclusion"));
                            right.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cd =>
                                { cd.RelativeColumn(2f); cd.RelativeColumn(1f); cd.RelativeColumn(1f); });
                                t.Header(h =>
                                {
                                    foreach (var hh in new[] { "Scheme", "Count", "Target" })
                                        h.Cell().Background(Color.FromHex("#689F38")).Padding(P1)
                                         .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
                                });
                                int i = 0;
                                foreach (var p in r.FinancialInclusion)
                                {
                                    var bg = i++ % 2 == 0 ? Color.FromHex("#F1F8E9") : Colors.White;
                                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                     .Padding(P1).Text(p.ParameterName).FontSize(F55);
                                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                     .Padding(P1).AlignRight().Text(p.ActualDisplay).FontSize(F55);
                                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                                     .Padding(P1).AlignRight().Text(Fmt(p.TargetCr) == "0.00" ? "—" : Fmt(p.TargetCr)).FontSize(F55);
                                }
                            });
                        }
                    });
            });
        });
    }

    // ── 9 & 10. FINANCIAL INCLUSION + JANSAMARTH ─────────────────────────────
    //private static void FiAndJanSamarth(IContainer c, GapReportViewModel r, string asOnLbl)
    //{
    //    c.Column(col =>
    //    {
    //        col.Item().Element(e => SecHdr(e, C_FI, "Financial Inclusion")); //  & JanSamarth

    //        if (r.FinancialInclusion.Count > 0)
    //        {
    //            col.Item().Element(e => SubHdr(e, Color.FromHex("#558B2F"), "Govt Schemes & Financial Inclusion"));
    //            col.Item().Border(0.5f).BorderColor(BORDER_H).Table(t =>
    //            {
    //                t.ColumnsDefinition(cd =>
    //                { cd.RelativeColumn(3f); cd.RelativeColumn(1f); cd.RelativeColumn(1f); });
    //                t.Header(h =>
    //                {
    //                    foreach (var hh in new[] { "Scheme", "Count", "Target" })
    //                        h.Cell().Background(Color.FromHex("#689F38")).Padding(P1)
    //                         .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
    //                });
    //                int i = 0;
    //                foreach (var p in r.FinancialInclusion)
    //                {
    //                    var bg = i++ % 2 == 0 ? Color.FromHex("#F1F8E9") : Colors.White;
    //                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //                     .Padding(P1).Text(p.ParameterName).FontSize(F55);
    //                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //                     .Padding(P1).AlignRight().Text(p.ActualDisplay).FontSize(F55);
    //                    t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //                     .Padding(P1).AlignRight().Text(Fmt(p.TargetCr) == "0.00" ? "—" : Fmt(p.TargetCr)).FontSize(F55);
    //                }
    //            });
    //        }

    //        //if (r.JanSamarthTable.Any(x => x.HasData))
    //        //{
    //        //    col.Item().Element(e => SubHdr(e, C_JS,
    //        //        $"JanSamarth — Scheme-wise Status (as on {asOnLbl})"));
    //        //    col.Item().Border(0.5f).BorderColor(BORDER_H).Table(t =>
    //        //    {
    //        //        t.ColumnsDefinition(cd =>
    //        //        {
    //        //            cd.RelativeColumn(1.3f);
    //        //            cd.RelativeColumn(1f); cd.RelativeColumn(1f);
    //        //            cd.RelativeColumn(1f); cd.RelativeColumn(1f);
    //        //            cd.RelativeColumn(1f);
    //        //        });
    //        //        t.Header(h =>
    //        //        {
    //        //            foreach (var hh in new[] { "Scheme", "Total", "Sanctioned", "Disbursed", "Rejected", "Pending" })
    //        //                h.Cell().Background(C_JS).PaddingVertical(P15).PaddingHorizontal(P1)
    //        //                 .AlignCenter().Text(hh).Bold().FontSize(F5).FontColor(Colors.White);
    //        //        });
    //        //        int i = 0;
    //        //        foreach (var jsRow in r.JanSamarthTable)
    //        //        {
    //        //            var bg = i++ % 2 == 0 ? Color.FromHex("#FCE4EC") : Colors.White;
    //        //            t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //        //             .Padding(P1).Text(jsRow.Scheme).Bold().FontSize(F55).FontColor(C_JS);
    //        //            t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //        //             .Padding(P1).AlignRight().Text(jsRow.Total).FontSize(F55);
    //        //            t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //        //             .Padding(P1).AlignRight().Text(jsRow.Sanctioned).FontSize(F55);
    //        //            t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //        //             .Padding(P1).AlignRight().Text(jsRow.Disbursed).FontSize(F55);
    //        //            t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //        //             .Padding(P1).AlignRight().Text(td =>
    //        //                 td.Span(jsRow.Rejected).FontSize(F55)
    //        //                   .FontColor(jsRow.Rejected is not ("—" or "0") ? NEG_C : Colors.Black));
    //        //            t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
    //        //             .Padding(P1).AlignRight().Text(jsRow.Pending).FontSize(F55).Bold();
    //        //        }
    //        //    });
    //        //}
    //    });
    //}

    // ── Shared: simple 2-column (label | value) table ─────────────────────────
    private static void SimpleCol(IContainer c, List<GapPerformanceRow> rows, Color tint)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(2f); cd.RelativeColumn(1f); });
            int i = 0;
            foreach (var p in rows)
            {
                var bg = i++ % 2 == 0 ? tint : Colors.White;
                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                 .Padding(P1).Text(p.ParameterName).FontSize(F55);
                t.Cell().Background(bg).BorderBottom(0.25f).BorderColor(BORDER_L)
                 .Padding(P1).AlignRight().Text(td =>
                 {
                     var sp = td.Span(p.ActualDisplay).FontSize(F55);
                     if (p.ActualAsOn < 0) sp.FontColor(NEG_C).Bold();
                 });
            }
        });
    }

    // ── FOOTER ────────────────────────────────────────────────────────────────
    private static void RenderFooter(IContainer c, GapReportSettings s) =>
        c.BorderTop(0.5f).BorderColor(C_BRANCH).PaddingTop(P2).Row(row =>
        {
            row.RelativeItem()
               .Text(s.ReportFooter).FontSize(F5).FontColor(Colors.Grey.Darken2);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.Span("Page ").FontSize(F5).FontColor(Colors.Grey.Darken2);
                t.CurrentPageNumber().FontSize(F5).FontColor(C_BRANCH);
                t.Span(" of ").FontSize(F5).FontColor(Colors.Grey.Darken2);
                t.TotalPages().FontSize(F5).FontColor(C_BRANCH);
            });
        });

    // ── Format helper ─────────────────────────────────────────────────────────
    private static string Fmt(decimal? v) => v.HasValue ? v.Value.ToString("N2") : "—";

    // ── OPERATIONS DASHBOARD ──────────────────────────────────────────────────
    private static void OperationsDashboard(IContainer container, DataSet ds)
    {
        DataRow? suspRow = null, sundRow = null, acctRow = null, aadhaarRow = null, panRow = null, complRow = null, cashRow = null;
        foreach (System.Data.DataTable table in ds.Tables)
        {
            if (table.Rows.Count == 0 || !table.Columns.Contains("TableName")) continue;
            switch (table.Rows[0]["TableName"]?.ToString())
            {
                case "SuspenseOSReport": suspRow = table.Rows[0]; break;
                case "SundryOSReport": sundRow = table.Rows[0]; break;
                case "AccountStatusReport": acctRow = table.Rows[0]; break;
                case "UCICAadhaarReport": aadhaarRow = table.Rows[0]; break;
                case "UCIC_PAN_Report": panRow = table.Rows[0]; break;
                case "BOComplaintReport": complRow = table.Rows[0]; break;
                case "CashHoldingReport": cashRow = table.Rows[0]; break;
            }
        }

        container.Column(col =>
        {
            col.Item().Background(Color.FromHex("#0070C0")).PaddingVertical(P15).PaddingHorizontal(P2)
               .Text("Operations").FontColor(Colors.White).Bold().FontSize(F6);

            col.Item().Row(mainRow =>
            {
                // Left panel
                mainRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Column(left =>
                {
                    left.Item().Background(Color.FromHex("#204060")).Padding(P1)
                        .Text("Suspense and Sundry Entries").FontColor(Colors.White).Bold().FontSize(F55);

                    left.Item().Table(t => {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        PdfHeader(t);
                    });

                    PdfOpsTable(left, "Suspense Entries", "#2F75B5", suspRow);
                    PdfOpsTable(left, "Sundry Entries", "#F4B084", sundRow);
                });

                // Right panel
                mainRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Column(right =>
                {
                    right.Item().Background(Color.FromHex("#204060")).Padding(P1)
                         .Text("Defaulting Accounts & UCIC Pendency").FontColor(Colors.White).Bold().FontSize(F5);

                    right.Item().Table(t => {
                        t.ColumnsDefinition(cd =>  { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        PdfHeader(t);
                    });

                    PdfRightSection(right, "Defaulting Accounts", "#9BC2E6", true,
                        ("Dormant", PI(acctRow, "PrevMonthNoOfDormant"), PI(acctRow, "PrevDay2NoOfDormant"), PI(acctRow, "PrevDayNoOfDormant")),
                        ("Inactive", PI(acctRow, "PrevMonthNoOfInactive"), PI(acctRow, "PrevDay2NoOfInactive"), PI(acctRow, "PrevDayNoOfInactive")),
                        ("No Nomination", PI(acctRow, "PrevMonthNoOfNoNomination"), PI(acctRow, "PrevDay2NoOfNoNomination"), PI(acctRow, "PrevDayNoOfNoNomination")));
                    PdfRightSection(right, "Lien Marked", "#548235", false,
                        ("No. of Accounts", PI(acctRow, "PrevMonthLienMarked"), PI(acctRow, "PrevDay2LienMarked"), PI(acctRow, "PrevDayLienMarked")),
                        ("Amount", PD(acctRow, "PrevMonthAmountLienMarked"), PD(acctRow, "PrevDay2AmountLienMarked"), PD(acctRow, "PrevDayAmountLienMarked")));
                    PdfRightSection(right, "DEAF", "#00B0F0", false,
                        ("No. of Accounts", PI(acctRow, "PrevMonthNoOfDeaf"), PI(acctRow, "PrevDay2NoOfDeaf"), PI(acctRow, "PrevDayNoOfDeaf")),
                        ("Amount", PD(acctRow, "PrevMonthAmountDeaf"), PD(acctRow, "PrevDay2AmountDeaf"), PD(acctRow, "PrevDayAmountDeaf")));
                    PdfRightSection(right, "UCIC Pendency", "#F4B084", false,
                        ("Aadhaar", PI(aadhaarRow, "PrevMonthTotal"), PI(aadhaarRow, "PrevDay2Total"), PI(aadhaarRow, "PrevDayTotal")),
                        ("PAN", PI(panRow, "PrevMonthTotal"), PI(panRow, "PrevDay2Total"), PI(panRow, "PrevDayTotal")));
                });
            });

            // Cash Holding and Customer Complaints — separate panels below
            col.Item().Row(bottomRow =>
            {
                bottomRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Column(cashCol =>
                {
                    cashCol.Item().Background(Color.FromHex("#204060")).Padding(P1)
                           .Text("Cash Holding").FontColor(Colors.White).Bold().FontSize(F55);
                    cashCol.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        PdfHeader(t);
                        PdfRow(t, "Limit (INR Lacs)", PD(cashRow, "PrevMonthLimit"), PD(cashRow, "PrevDay2Limit"), PD(cashRow, "PrevDayLimit"));
                        PdfRow(t, "Actual (INR Lacs)", PD(cashRow, "PrevMonthTotal"), PD(cashRow, "PrevDay2Total"), PD(cashRow, "PrevDayTotal"));
                    });
                });

                bottomRow.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Column(complCol =>
                {
                    complCol.Item().Background(Color.FromHex("#204060")).Padding(P1)
                            .Text("Customer Complaints").FontColor(Colors.White).Bold().FontSize(F55);
                    complCol.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        PdfHeader(t);
                        PdfRow(t, "Number of Complaints", PD(complRow, "PrevMonthNoOfComplaints"), PD(complRow, "PrevDay2NoOfComplaints"), PD(complRow, "PrevDayNoOfComplaints"));
                    });
                });
            });
        });
    }

    private static void PdfOpsTable(ColumnDescriptor col, string title, string color, DataRow? dr)
    {
        col.Item().Background(Color.FromHex(color)).Padding(P1)
           .Text(title).FontColor(Colors.White).Bold().FontSize(F5);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
           
            // Data rows
            PdfRow(t, "No. Of Entries", PI(dr, "PrevMonthEntries"), PI(dr, "PrevDay2Entries"), PI(dr, "PrevDayEntries"));
            PdfRow(t, "Outstanding", PD(dr, "PrevMonthTotal"), PD(dr, "PrevDay2Total"), PD(dr, "PrevDayTotal"));
            PdfRow(t, "w/w Older than 90 Days", "", PI(dr, "PrevDayEntries90Days"), PD(dr, "PrevDayTotal90Days"));
            PdfRow(t, "w/w Older than 180 Days", "", PI(dr, "PrevDayEntries180Days"), PD(dr, "PrevDayTotal180Days"));

            // Variations row: PrevDay vs PrevMonth | Today vs PrevDay
            var prevMonthTotal = SafePdfDec(dr, "PrevMonthTotal");
            var prevDayTotal = SafePdfDec(dr, "PrevDay2Total");
            var todayTotal = SafePdfDec(dr, "PrevDayTotal");

            var varPrevDay = prevDayTotal - prevMonthTotal;
            var varPrevDayPct = prevMonthTotal != 0 ? (varPrevDay / prevMonthTotal) * 100m : 0m;

            var varToday = todayTotal - prevDayTotal;
            var varTodayPct = prevDayTotal != 0 ? (varToday / prevDayTotal) * 100m : 0m;

            PdfRow(t, "Variations :", "", $"{varPrevDay:N0} | {varPrevDayPct:0.00}%", $"{varToday:N0} | {varTodayPct:0.00}%");
        });
    }

    private static void PdfHeader(TableDescriptor t)
    {
        t.Header(h =>
        {
            h.Cell().Padding(1).Text("").FontSize(F5);
            h.Cell().Padding(1).Background(Color.FromHex("#C6E0B4")).Text("Prev Month").Bold().FontSize(F5);
            h.Cell().Padding(1).Background(Color.FromHex("#C6E0B4")).Text("Prev Day").Bold().FontSize(F5);
            h.Cell().Padding(1).Background(Color.FromHex("#C6E0B4")).Text("Present Day").Bold().FontSize(F5);
        });
    }

    private static void PdfRightSection(ColumnDescriptor col, string title, string color, bool darkText, params (string L, string PM, string PDay, string Today)[] rows)
    {
        col.Item().Background(Color.FromHex(color)).Padding(P1)
           .Text(title).FontColor(darkText ? Colors.Black : Colors.White).Bold().FontSize(F5);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
            foreach (var r in rows) PdfRow(t, r.L, r.PM, r.PDay, r.Today);
        });
    }

    private static void PdfRow(TableDescriptor t, string lbl, string pm, string pd, string td)
    {
        t.Cell().Padding(1).Text(lbl).FontSize(F5);
        t.Cell().Padding(1).Text(pm).FontSize(F5);
        t.Cell().Padding(1).Text(pd).FontSize(F5);
        t.Cell().Padding(1).Text(td).Bold().FontSize(F5);
    }

    private static string PI(DataRow? r, string col)
    {
        if (r == null) return "—";
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToInt32(r[col]).ToString("N0") : "—"; } catch { return "—"; }
    }

    private static string PD(DataRow? r, string col)
    {
        if (r == null) return "—";
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDecimal(r[col]).ToString("N2") : "—"; } catch { return "—"; }
    }

    private static decimal SafePdfDec(DataRow? r, string col)
    {
        if (r == null) return 0m;
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDecimal(r[col]) : 0m; } catch { return 0m; }
    }

    private static string PdfDate(DataRow? r, string col)
    {
        if (r == null) return "—";
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDateTime(r[col]).ToString("dd/MM/yyyy") : "—"; } catch { return "—"; }
    }
}
