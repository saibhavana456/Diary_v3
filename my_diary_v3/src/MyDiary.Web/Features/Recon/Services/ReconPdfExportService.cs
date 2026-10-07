using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RequestPortal.Core.Models;

namespace MyDiary.Web.Features.Recon.Services;

/// <summary>Recon Certificates PDF export. Re-implements the source app's GenerateAnnexure3Pdf /
/// GenerateAnnexure4Pdf certificate CONTENT and layout (same tables, same figures, same certificate
/// declaration text, same signature block) using QuestPDF instead of the source's DinkToPdf/wkhtmltopdf
/// HTML-to-PDF pipeline — my_diary_v3_1 already uses QuestPDF everywhere else (see Program.cs's license
/// config), so this avoids introducing a second, native-binary-dependent PDF stack for one module.
/// RO/ZO consolidated certificate PDFs are ported as a 2-page document (Certificate of Review for
/// Office Accounts + unit roster) covering the source's Annexure4A/5's content — the source's separate
/// 4C/5B "detailed interbank breakup" page is NOT yet ported (that data isn't stored on the consolidated
/// row itself; would need re-querying every underlying branch's child tables — see class-level TODO).</summary>
public static class ReconPdfExportService
{
    /// <summary>Exact-content port of GenerateAnnexure4AHtml/GenerateAnnexure5Html — "Certificate of
    /// Review for Office Accounts" at RO or ZO level, plus a roster of the units (branches, for RO; or
    /// regions, for ZO) it consolidates, plus (when interbankBreakup is supplied) a 3rd page matching
    /// source's Annexure4C/5B "Inter Bank Reconciliation Report" — the same period-wise summary table
    /// already aggregated onto `aggregated`, plus the entry-wise breakup listing (Section B). Source
    /// populates this listing by re-querying every underlying branch's ANNEXURE4_INTERBANK_BREAKUP rows
    /// (the consolidated row itself never stores them) — same here via
    /// IReconAnnexure4Service.GetInterbankBreakupForRegionAsync/ForZoneAsync. ownerLabel/unitLabel are
    /// "Region"/"Branch" for the RO-level call and "Zone"/"Region" for the ZO-level call.</summary>
    public static byte[] GenerateConsolidatedPdf(
        ReconAnnexure4 aggregated, string ownerLabel, string ownerName, string parentLabel, string parentName,
        int totalUnits, int unbalancedUnits, string unitLabel,
        IReadOnlyList<(string Code, string Name, string? Head, string? Mobile, string? Ip)> unitDetails,
        IReadOnlyList<ReconA4InterbankBreakup>? interbankBreakup = null)
    {
        var currentDate = DateTime.Now.ToString("dd/MM/yyyy");

        var sunTotalEntries = (aggregated.SunUpto3MEntries ?? 0) + (aggregated.SunThree6MEntries ?? 0) + (aggregated.SunOver6MEntries ?? 0);
        var sunTotalAmount = (aggregated.SunUpto3MAmount ?? 0) + (aggregated.SunThree6MAmount ?? 0) + (aggregated.SunOver6MAmount ?? 0);
        var susTotalEntries = (aggregated.SusUpto3MEntries ?? 0) + (aggregated.SusThree6MEntries ?? 0) + (aggregated.SusOver6MEntries ?? 0);
        var susTotalAmount = (aggregated.SusUpto3MAmount ?? 0) + (aggregated.SusThree6MAmount ?? 0) + (aggregated.SusOver6MAmount ?? 0);
        var pobTotalEntries = (aggregated.PobUpto3MEntries ?? 0) + (aggregated.PobThree6MEntries ?? 0) + (aggregated.PobOver6MEntries ?? 0);
        var pobTotalAmount = (aggregated.PobUpto3MAmount ?? 0) + (aggregated.PobThree6MAmount ?? 0) + (aggregated.PobOver6MAmount ?? 0);
        var ibkTotalEntries = (aggregated.IbkUpto3MEntries ?? 0) + (aggregated.IbkThree6MEntries ?? 0) + (aggregated.IbkOver6MEntries ?? 0);
        var ibkTotalAmount = (aggregated.IbkUpto3MAmount ?? 0) + (aggregated.IbkThree6MAmount ?? 0) + (aggregated.IbkOver6MAmount ?? 0);

        var doc = Document.Create(container =>
        {
            // ── Page 1: Certificate of Review for Office Accounts ──────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Column(h =>
                {
                    h.Item().AlignRight().Text("Inter Office Letter").Bold().FontSize(14);
                    h.Item().AlignCenter().Text("A. CERTIFICATE OF REVIEW FOR OFFICE ACCOUNTS").Bold().FontSize(13);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text($"{ownerLabel}: {ownerName}    |    {parentLabel}: {parentName}    |    Period: {aggregated.ReportMonth} {aggregated.ReportYear}").Bold();

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(0.6f); c.RelativeColumn(4); });
                        t.Cell().Element(BodyCell).Text("1");
                        t.Cell().Element(BodyCell).Text($"Total number of SOLs (including Branches, Admin Offices, Service Branches, etc) under this {ownerLabel}: {totalUnits}");
                        t.Cell().Element(BodyCell).Text("2");
                        t.Cell().Element(BodyCell).Text($"No. of SOLs where all books are not balanced: {unbalancedUnits}");
                    });

                    col.Item().Text("B. Position of entries outstanding in internal/office accounts (Rs. in lakhs)").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            for (int i = 0; i < 8; i++) c.RelativeColumn(1.2f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Account Type");
                            h.Cell().Element(HeaderCell).Text("Upto 3M\nEntries");
                            h.Cell().Element(HeaderCell).Text("Upto 3M\nAmount");
                            h.Cell().Element(HeaderCell).Text("3-6M\nEntries");
                            h.Cell().Element(HeaderCell).Text("3-6M\nAmount");
                            h.Cell().Element(HeaderCell).Text("Over 6M\nEntries");
                            h.Cell().Element(HeaderCell).Text("Over 6M\nAmount");
                            h.Cell().Element(HeaderCell).Text("Total\nEntries");
                            h.Cell().Element(HeaderCell).Text("Total\nAmount");
                        });
                        AddRow(table, "Sundry", aggregated.SunUpto3MEntries, aggregated.SunUpto3MAmount, aggregated.SunThree6MEntries, aggregated.SunThree6MAmount, aggregated.SunOver6MEntries, aggregated.SunOver6MAmount, sunTotalEntries, sunTotalAmount);
                        AddRow(table, "Suspense", aggregated.SusUpto3MEntries, aggregated.SusUpto3MAmount, aggregated.SusThree6MEntries, aggregated.SusThree6MAmount, aggregated.SusOver6MEntries, aggregated.SusOver6MAmount, susTotalEntries, susTotalAmount);
                        AddRow(table, "POB", aggregated.PobUpto3MEntries, aggregated.PobUpto3MAmount, aggregated.PobThree6MEntries, aggregated.PobThree6MAmount, aggregated.PobOver6MEntries, aggregated.PobOver6MAmount, pobTotalEntries, pobTotalAmount);
                        AddRow(table, "Inter Bank", aggregated.IbkUpto3MEntries, aggregated.IbkUpto3MAmount, aggregated.IbkThree6MEntries, aggregated.IbkThree6MAmount, aggregated.IbkOver6MEntries, aggregated.IbkOver6MAmount, ibkTotalEntries, ibkTotalAmount);
                    });

                    col.Item().Text("C. Details of newly added entries aged more than 6 months during the period").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1.2f); c.RelativeColumn(2.5f); c.RelativeColumn(2.5f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Account Type");
                            h.Cell().Element(HeaderCell).Text("Entries");
                            h.Cell().Element(HeaderCell).Text("Amount");
                            h.Cell().Element(HeaderCell).Text("Reason for outstanding");
                            h.Cell().Element(HeaderCell).Text("Efforts for elimination");
                        });
                        AddNewOver6MRow(table, "Sundry", aggregated.SunNewOver6MEntries, aggregated.SunNewOver6MAmount, aggregated.SunReason, aggregated.SunEfforts);
                        AddNewOver6MRow(table, "Suspense", aggregated.SusNewOver6MEntries, aggregated.SusNewOver6MAmount, aggregated.SusReason, aggregated.SusEfforts);
                        AddNewOver6MRow(table, "POB", aggregated.PobNewOver6MEntries, aggregated.PobNewOver6MAmount, aggregated.PobReason, aggregated.PobEfforts);
                        AddNewOver6MRow(table, "Inter Bank", aggregated.IbkNewOver6MEntries, aggregated.IbkNewOver6MAmount, aggregated.IbkReason, aggregated.IbkEfforts);
                    });

                    col.Item().PaddingTop(10).Text($"We certify that we have reviewed all entries outstanding for the internal/office accounts of our {ownerLabel} as on {currentDate}.");
                    col.Item().Text("We further certify that we have examined staff lapses and initiated appropriate action as per the extant guidelines.");

                    col.Item().PaddingTop(30).AlignRight().Column(sig =>
                    {
                        sig.Item().Text("Signature").Bold();
                        sig.Item().PaddingTop(20).Text($"{ownerLabel} Head").Bold();
                        sig.Item().Text($"Date: {currentDate}").FontSize(8);
                    });
                });

                page.Footer().AlignCenter().Text($"Annexure Consolidated ({ownerLabel}) | Generated on {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(7);
            });

            // ── Page 2: Unit roster ─────────────────────────────────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().AlignCenter().Text($"B. {unitLabel} Details under {ownerName}").Bold().FontSize(13);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.2f); c.RelativeColumn(2.5f); c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text($"{unitLabel} Code");
                        h.Cell().Element(HeaderCell).Text($"{unitLabel} Name");
                        h.Cell().Element(HeaderCell).Text("Head");
                        h.Cell().Element(HeaderCell).Text("Mobile No");
                        h.Cell().Element(HeaderCell).Text("IP No");
                    });
                    foreach (var u in unitDetails)
                    {
                        table.Cell().Element(BodyCell).Text(u.Code);
                        table.Cell().Element(BodyCell).Text(u.Name);
                        table.Cell().Element(BodyCell).Text(u.Head ?? "-");
                        table.Cell().Element(BodyCell).Text(u.Mobile ?? "-");
                        table.Cell().Element(BodyCell).Text(u.Ip ?? "-");
                    }
                });

                page.Footer().AlignCenter().Text($"Annexure Consolidated ({ownerLabel}) | Generated on {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(7);
            });

            // ── Page 3: Inter Bank Reconciliation Report (source's Annexure4C/5B) ───────────────
            if (interbankBreakup is not null)
            {
                var totalOD = (aggregated.OurDebitAmount1M ?? 0) + (aggregated.OurDebitAmount3M ?? 0) + (aggregated.OurDebitAmount6M ?? 0) + (aggregated.OurDebitAmount1Y ?? 0) + (aggregated.OurDebitAmountAbove1Y ?? 0);
                var totalOC = (aggregated.OurCreditAmount1M ?? 0) + (aggregated.OurCreditAmount3M ?? 0) + (aggregated.OurCreditAmount6M ?? 0) + (aggregated.OurCreditAmount1Y ?? 0) + (aggregated.OurCreditAmountAbove1Y ?? 0);
                var totalTD = (aggregated.TheirDebitAmount1M ?? 0) + (aggregated.TheirDebitAmount3M ?? 0) + (aggregated.TheirDebitAmount6M ?? 0) + (aggregated.TheirDebitAmount1Y ?? 0) + (aggregated.TheirDebitAmountAbove1Y ?? 0);
                var totalTC = (aggregated.TheirCreditAmount1M ?? 0) + (aggregated.TheirCreditAmount3M ?? 0) + (aggregated.TheirCreditAmount6M ?? 0) + (aggregated.TheirCreditAmount1Y ?? 0) + (aggregated.TheirCreditAmountAbove1Y ?? 0);

                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                    page.Header().Column(h =>
                    {
                        h.Item().AlignRight().Text("Inter Office Letter").Bold().FontSize(14);
                        h.Item().Text($"From: {ownerLabel} Office — {ownerName}      To: {parentLabel} Office — {parentName}");
                        h.Item().Text($"For the month: {aggregated.ReportMonth} {aggregated.ReportYear}").Bold();
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(6);
                        col.Item().Text("1. INTER BANK RECONCILIATION REPORT — Entry-wise outstanding amount").Bold();
                        col.Item().Text("A. (Rs. in lakhs)").Bold().FontSize(9);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.2f);
                                for (int i = 0; i < 8; i++) c.RelativeColumn(1);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("Period");
                                h.Cell().Element(HeaderCell).Text("Our Debit\nEntry");
                                h.Cell().Element(HeaderCell).Text("Our Debit\nAmt");
                                h.Cell().Element(HeaderCell).Text("Our Credit\nEntry");
                                h.Cell().Element(HeaderCell).Text("Our Credit\nAmt");
                                h.Cell().Element(HeaderCell).Text("Their Debit\nEntry");
                                h.Cell().Element(HeaderCell).Text("Their Debit\nAmt");
                                h.Cell().Element(HeaderCell).Text("Their Credit\nEntry");
                                h.Cell().Element(HeaderCell).Text("Their Credit\nAmt");
                            });

                            AddPeriodRow(table, "Up to 1 month", aggregated.OurDebitEntries1M, aggregated.OurDebitAmount1M, aggregated.OurCreditEntries1M, aggregated.OurCreditAmount1M, aggregated.TheirDebitEntries1M, aggregated.TheirDebitAmount1M, aggregated.TheirCreditEntries1M, aggregated.TheirCreditAmount1M);
                            AddPeriodRow(table, "1-3 months", aggregated.OurDebitEntries3M, aggregated.OurDebitAmount3M, aggregated.OurCreditEntries3M, aggregated.OurCreditAmount3M, aggregated.TheirDebitEntries3M, aggregated.TheirDebitAmount3M, aggregated.TheirCreditEntries3M, aggregated.TheirCreditAmount3M);
                            AddPeriodRow(table, "3-6 months", aggregated.OurDebitEntries6M, aggregated.OurDebitAmount6M, aggregated.OurCreditEntries6M, aggregated.OurCreditAmount6M, aggregated.TheirDebitEntries6M, aggregated.TheirDebitAmount6M, aggregated.TheirCreditEntries6M, aggregated.TheirCreditAmount6M);
                            AddPeriodRow(table, "6 months - 1 year", aggregated.OurDebitEntries1Y, aggregated.OurDebitAmount1Y, aggregated.OurCreditEntries1Y, aggregated.OurCreditAmount1Y, aggregated.TheirDebitEntries1Y, aggregated.TheirDebitAmount1Y, aggregated.TheirCreditEntries1Y, aggregated.TheirCreditAmount1Y);
                            AddPeriodRow(table, "More than 1 year", aggregated.OurDebitEntriesAbove1Y, aggregated.OurDebitAmountAbove1Y, aggregated.OurCreditEntriesAbove1Y, aggregated.OurCreditAmountAbove1Y, aggregated.TheirDebitEntriesAbove1Y, aggregated.TheirDebitAmountAbove1Y, aggregated.TheirCreditEntriesAbove1Y, aggregated.TheirCreditAmountAbove1Y);

                            table.Cell().Element(TotalCell).Text("Total (A)").Bold();
                            table.Cell().Element(TotalCell).Text("-");
                            table.Cell().Element(TotalCell).AlignRight().Text($"{totalOD:N2}").Bold();
                            table.Cell().Element(TotalCell).Text("-");
                            table.Cell().Element(TotalCell).AlignRight().Text($"{totalOC:N2}").Bold();
                            table.Cell().Element(TotalCell).Text("-");
                            table.Cell().Element(TotalCell).AlignRight().Text($"{totalTD:N2}").Bold();
                            table.Cell().Element(TotalCell).Text("-");
                            table.Cell().Element(TotalCell).AlignRight().Text($"{totalTC:N2}").Bold();
                        });

                        col.Item().Text("The above totals should tally with totals of Annexure 'II'.").Italic().FontSize(8);

                        col.Item().Text("B. Outstanding entry wise break-up more than three months (Amt in Rupees)").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(0.5f); c.RelativeColumn(1.6f); c.RelativeColumn(1); c.RelativeColumn(1.6f);
                                c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("Sr.");
                                h.Cell().Element(HeaderCell).Text("Branch");
                                h.Cell().Element(HeaderCell).Text("Date");
                                h.Cell().Element(HeaderCell).Text("Inter-Bank");
                                h.Cell().Element(HeaderCell).Text("Our Dr");
                                h.Cell().Element(HeaderCell).Text("Our Cr");
                                h.Cell().Element(HeaderCell).Text("Their Dr");
                                h.Cell().Element(HeaderCell).Text("Their Cr");
                                h.Cell().Element(HeaderCell).Text("Nature of Transaction");
                            });

                            int sr = 1;
                            foreach (var item in interbankBreakup)
                            {
                                table.Cell().Element(BodyCell).Text($"{sr++}");
                                table.Cell().Element(BodyCell).Text(item.BranchName ?? "-");
                                table.Cell().Element(BodyCell).Text(item.DateOfEntry?.ToString("dd-MM-yyyy") ?? "-");
                                table.Cell().Element(BodyCell).Text(item.InterBankName ?? "-");
                                table.Cell().Element(BodyCell).AlignRight().Text($"{item.OurDebitAmount ?? 0:N2}");
                                table.Cell().Element(BodyCell).AlignRight().Text($"{item.OurCreditAmount ?? 0:N2}");
                                table.Cell().Element(BodyCell).AlignRight().Text($"{item.TheirDebitAmount ?? 0:N2}");
                                table.Cell().Element(BodyCell).AlignRight().Text($"{item.TheirCreditAmount ?? 0:N2}");
                                table.Cell().Element(BodyCell).Text(item.TransactionNatureDetails ?? "-");
                            }
                        });

                        col.Item().PaddingTop(20).AlignRight().Column(sig =>
                        {
                            sig.Item().Text("Signature").Bold();
                            sig.Item().PaddingTop(20).Text($"{ownerLabel} Head").Bold();
                            sig.Item().Text($"Date: {currentDate}").FontSize(8);
                        });
                    });

                    page.Footer().AlignCenter().Text($"Annexure Consolidated ({ownerLabel}) | Generated on {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                });
            }
        });

        return doc.GeneratePdf();
    }

    private static void AddPeriodRow(TableDescriptor table, string label,
        long? oe, decimal? oa, long? oce, decimal? oca, long? tde, decimal? tda, long? tce, decimal? tca)
    {
        table.Cell().Element(BodyCell).Text(label);
        table.Cell().Element(BodyCell).AlignRight().Text($"{oe ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{oa ?? 0:N2}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{oce ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{oca ?? 0:N2}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{tde ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{tda ?? 0:N2}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{tce ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{tca ?? 0:N2}");
    }

    public static byte[] GenerateAnnexure3Pdf(ReconAnnexure3 e, string vertical)
    {
        var currentDate = DateTime.Now.ToString("dd/MM/yyyy");

        var sunTotalEntries = (e.SunUpto3MEntries ?? 0) + (e.SunThree6MEntries ?? 0) + (e.SunOver6MEntries ?? 0);
        var sunTotalAmount = (e.SunUpto3MAmount ?? 0) + (e.SunThree6MAmount ?? 0) + (e.SunOver6MAmount ?? 0);
        var susTotalEntries = (e.SusUpto3MEntries ?? 0) + (e.SusThree6MEntries ?? 0) + (e.SusOver6MEntries ?? 0);
        var susTotalAmount = (e.SusUpto3MAmount ?? 0) + (e.SusThree6MAmount ?? 0) + (e.SusOver6MAmount ?? 0);
        var pobTotalEntries = (e.PobUpto3MEntries ?? 0) + (e.PobThree6MEntries ?? 0) + (e.PobOver6MEntries ?? 0);
        var pobTotalAmount = (e.PobUpto3MAmount ?? 0) + (e.PobThree6MAmount ?? 0) + (e.PobOver6MAmount ?? 0);
        var ibkTotalEntries = (e.IbkUpto3MEntries ?? 0) + (e.IbkThree6MEntries ?? 0) + (e.IbkOver6MEntries ?? 0);
        var ibkTotalAmount = (e.IbkUpto3MAmount ?? 0) + (e.IbkThree6MAmount ?? 0) + (e.IbkOver6MAmount ?? 0);
        var grandTotalEntries = sunTotalEntries + susTotalEntries + pobTotalEntries + ibkTotalEntries;
        var grandTotalAmount = sunTotalAmount + susTotalAmount + pobTotalAmount + ibkTotalAmount;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().AlignRight().Text("Annexure-3").Bold().FontSize(14);

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignCenter().Text("CERTIFICATE OF REVIEW").Bold().FontSize(13);
                    col.Item().Text($"Subject: Certificate of Review for Sundries, Suspense, POB & Inter-Bank accounts of vertical/department SOL ({vertical}).");
                    col.Item().Text("We have reviewed the Sundries, Suspense, POB and Inter-Bank Accounts owned by our vertical as under.");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            for (int i = 0; i < 8; i++) c.RelativeColumn(1.2f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Account Type");
                            h.Cell().Element(HeaderCell).Text("Upto 3M\nEntries");
                            h.Cell().Element(HeaderCell).Text("Upto 3M\nAmount");
                            h.Cell().Element(HeaderCell).Text("3-6M\nEntries");
                            h.Cell().Element(HeaderCell).Text("3-6M\nAmount");
                            h.Cell().Element(HeaderCell).Text("Over 6M\nEntries");
                            h.Cell().Element(HeaderCell).Text("Over 6M\nAmount");
                            h.Cell().Element(HeaderCell).Text("Total\nEntries");
                            h.Cell().Element(HeaderCell).Text("Total\nAmount");
                        });

                        AddRow(table, "Sundry", e.SunUpto3MEntries, e.SunUpto3MAmount, e.SunThree6MEntries, e.SunThree6MAmount, e.SunOver6MEntries, e.SunOver6MAmount, sunTotalEntries, sunTotalAmount);
                        AddRow(table, "Suspense", e.SusUpto3MEntries, e.SusUpto3MAmount, e.SusThree6MEntries, e.SusThree6MAmount, e.SusOver6MEntries, e.SusOver6MAmount, susTotalEntries, susTotalAmount);
                        AddRow(table, "POB", e.PobUpto3MEntries, e.PobUpto3MAmount, e.PobThree6MEntries, e.PobThree6MAmount, e.PobOver6MEntries, e.PobOver6MAmount, pobTotalEntries, pobTotalAmount);
                        AddRow(table, "Inter Bank", e.IbkUpto3MEntries, e.IbkUpto3MAmount, e.IbkThree6MEntries, e.IbkThree6MAmount, e.IbkOver6MEntries, e.IbkOver6MAmount, ibkTotalEntries, ibkTotalAmount);

                        table.Cell().Element(TotalCell).Text("GRAND TOTAL").Bold();
                        for (int i = 0; i < 6; i++) table.Cell().Element(TotalCell).Text("-");
                        table.Cell().Element(TotalCell).Text($"{grandTotalEntries:N0}").Bold();
                        table.Cell().Element(TotalCell).Text($"{grandTotalAmount:N2}").Bold();
                    });

                    col.Item().Text("(Rs. in lakhs)").FontSize(8).AlignRight();

                    col.Item().Text("Details of newly added entries aged more than 6 months during the period").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1.2f); c.RelativeColumn(2.5f); c.RelativeColumn(2.5f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Account Type");
                            h.Cell().Element(HeaderCell).Text("Entries");
                            h.Cell().Element(HeaderCell).Text("Amount");
                            h.Cell().Element(HeaderCell).Text("Reason for outstanding");
                            h.Cell().Element(HeaderCell).Text("Efforts for elimination");
                        });
                        AddNewOver6MRow(table, "Sundry", e.SunNewOver6MEntries, e.SunNewOver6MAmount, e.SunReason, e.SunEfforts);
                        AddNewOver6MRow(table, "Suspense", e.SusNewOver6MEntries, e.SusNewOver6MAmount, e.SusReason, e.SusEfforts);
                        AddNewOver6MRow(table, "POB", e.PobNewOver6MEntries, e.PobNewOver6MAmount, e.PobReason, e.PobEfforts);
                        AddNewOver6MRow(table, "Inter Bank", e.IbkNewOver6MEntries, e.IbkNewOver6MAmount, e.IbkReason, e.IbkEfforts);
                    });

                    col.Item().PaddingTop(10).Text("We certify that we have reviewed all entries outstanding for the internal/office accounts of our vertical SOL " +
                        $"as on {currentDate}. For elimination / adjustment of outstanding entries of Internal/Office Accounts we have initiated appropriate steps.");
                    col.Item().Text("We further certify that we have examined staff lapses in respect of all the entries outstanding for more than six " +
                        "months and initiated appropriate action as per the extant guidelines in the cases where accountability was observed.");
                    col.Item().Text("We note to inform adverse observations in time bound manner, if any, to A&I CO.");

                    col.Item().PaddingTop(30).AlignRight().Column(sig =>
                    {
                        sig.Item().Text("Signature").Bold();
                        sig.Item().PaddingTop(20).Text("Owner Vertical Head").Bold();
                        sig.Item().Text($"Date: {currentDate}").FontSize(8);
                    });
                });

                page.Footer().AlignCenter().Column(f =>
                {
                    f.Item().Text("This is a system generated document and does not require physical signature").FontSize(7);
                    f.Item().Text($"Annexure-3 | Generated on {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                });
            });
        });

        return doc.GeneratePdf();
    }

    public static byte[] GenerateAnnexure4Pdf(ReconAnnexure4 e, string regionName)
    {
        var currentDate = DateTime.Now.ToString("dd/MM/yyyy");

        var sunTotalEntries = (e.SunUpto3MEntries ?? 0) + (e.SunThree6MEntries ?? 0) + (e.SunOver6MEntries ?? 0);
        var sunTotalAmount = (e.SunUpto3MAmount ?? 0) + (e.SunThree6MAmount ?? 0) + (e.SunOver6MAmount ?? 0);
        var susTotalEntries = (e.SusUpto3MEntries ?? 0) + (e.SusThree6MEntries ?? 0) + (e.SusOver6MEntries ?? 0);
        var susTotalAmount = (e.SusUpto3MAmount ?? 0) + (e.SusThree6MAmount ?? 0) + (e.SusOver6MAmount ?? 0);
        var pobTotalEntries = (e.PobUpto3MEntries ?? 0) + (e.PobThree6MEntries ?? 0) + (e.PobOver6MEntries ?? 0);
        var pobTotalAmount = (e.PobUpto3MAmount ?? 0) + (e.PobThree6MAmount ?? 0) + (e.PobOver6MAmount ?? 0);
        var ibkTotalEntries = (e.IbkUpto3MEntries ?? 0) + (e.IbkThree6MEntries ?? 0) + (e.IbkOver6MEntries ?? 0);
        var ibkTotalAmount = (e.IbkUpto3MAmount ?? 0) + (e.IbkThree6MAmount ?? 0) + (e.IbkOver6MAmount ?? 0);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Column(h =>
                {
                    h.Item().AlignRight().Text("Annexure-4").Bold().FontSize(14);
                    h.Item().AlignCenter().Text("BRANCH RECONCILIATION CERTIFICATE").Bold().FontSize(13);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                        t.Cell().Element(HeaderCell).Text("Branch Code").Bold();
                        t.Cell().Element(BodyCell).Text(e.BranchCode ?? "");
                        t.Cell().Element(HeaderCell).Text("Region").Bold();
                        t.Cell().Element(BodyCell).Text(regionName);
                        t.Cell().Element(HeaderCell).Text("Branch Name").Bold();
                        t.Cell().Element(BodyCell).Text(e.BranchName ?? "");
                        t.Cell().Element(HeaderCell).Text("Reporting Period").Bold();
                        t.Cell().Element(BodyCell).Text($"{e.ReportMonth} {e.ReportYear}");
                        t.Cell().Element(HeaderCell).Text("Branch Head").Bold();
                        t.Cell().Element(BodyCell).Text(e.BranchHead ?? "");
                        t.Cell().Element(HeaderCell).Text("Mobile No").Bold();
                        t.Cell().Element(BodyCell).Text(e.MobileNo ?? "");
                    });

                    col.Item().Text("Sundry / Suspense / POB / Inter-Bank — Position of Outstanding Entries").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            for (int i = 0; i < 8; i++) c.RelativeColumn(1.2f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Account Type");
                            h.Cell().Element(HeaderCell).Text("Upto 3M\nEntries");
                            h.Cell().Element(HeaderCell).Text("Upto 3M\nAmount");
                            h.Cell().Element(HeaderCell).Text("3-6M\nEntries");
                            h.Cell().Element(HeaderCell).Text("3-6M\nAmount");
                            h.Cell().Element(HeaderCell).Text("Over 6M\nEntries");
                            h.Cell().Element(HeaderCell).Text("Over 6M\nAmount");
                            h.Cell().Element(HeaderCell).Text("Total\nEntries");
                            h.Cell().Element(HeaderCell).Text("Total\nAmount");
                        });
                        AddRow(table, "Sundry", e.SunUpto3MEntries, e.SunUpto3MAmount, e.SunThree6MEntries, e.SunThree6MAmount, e.SunOver6MEntries, e.SunOver6MAmount, sunTotalEntries, sunTotalAmount);
                        AddRow(table, "Suspense", e.SusUpto3MEntries, e.SusUpto3MAmount, e.SusThree6MEntries, e.SusThree6MAmount, e.SusOver6MEntries, e.SusOver6MAmount, susTotalEntries, susTotalAmount);
                        AddRow(table, "POB", e.PobUpto3MEntries, e.PobUpto3MAmount, e.PobThree6MEntries, e.PobThree6MAmount, e.PobOver6MEntries, e.PobOver6MAmount, pobTotalEntries, pobTotalAmount);
                        AddRow(table, "Inter Bank", e.IbkUpto3MEntries, e.IbkUpto3MAmount, e.IbkThree6MEntries, e.IbkThree6MAmount, e.IbkOver6MEntries, e.IbkOver6MAmount, ibkTotalEntries, ibkTotalAmount);
                    });

                    col.Item().Text("Details of newly added entries aged more than 6 months during the period").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1.2f); c.RelativeColumn(2.5f); c.RelativeColumn(2.5f);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Account Type");
                            h.Cell().Element(HeaderCell).Text("Entries");
                            h.Cell().Element(HeaderCell).Text("Amount");
                            h.Cell().Element(HeaderCell).Text("Reason for outstanding");
                            h.Cell().Element(HeaderCell).Text("Efforts for elimination");
                        });
                        AddNewOver6MRow(table, "Sundry", e.SunNewOver6MEntries, e.SunNewOver6MAmount, e.SunReason, e.SunEfforts);
                        AddNewOver6MRow(table, "Suspense", e.SusNewOver6MEntries, e.SusNewOver6MAmount, e.SusReason, e.SusEfforts);
                        AddNewOver6MRow(table, "POB", e.PobNewOver6MEntries, e.PobNewOver6MAmount, e.PobReason, e.PobEfforts);
                        AddNewOver6MRow(table, "Inter Bank", e.IbkNewOver6MEntries, e.IbkNewOver6MAmount, e.IbkReason, e.IbkEfforts);
                    });

                    if (e.BreakupEntries.Count > 0 || e.SundryEntries.Count > 0 || e.SuspenseEntries.Count > 0 ||
                        e.PobEntries.Count > 0 || e.BooksNotTallied.Count > 0)
                    {
                        col.Item().Text("Note: detailed line-item breakup schedules (Inter-Bank breakup, Sundry/Suspense/POB " +
                            "outstanding-item lists, Books Not Tallied) are recorded in the system and available via Audit " +
                            "Trail, but are not yet included in this PDF export.").Italic().FontSize(8);
                    }

                    col.Item().PaddingTop(30).AlignRight().Column(sig =>
                    {
                        sig.Item().Text("Signature").Bold();
                        sig.Item().PaddingTop(20).Text("Branch Head").Bold();
                        sig.Item().Text($"Date: {currentDate}").FontSize(8);
                    });
                });

                page.Footer().AlignCenter().Column(f =>
                {
                    f.Item().Text("This is a system generated document and does not require physical signature").FontSize(7);
                    f.Item().Text($"Annexure-4 | Generated on {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void AddRow(TableDescriptor table, string label,
        long? upto3MEntries, decimal? upto3MAmount, long? three6MEntries, decimal? three6MAmount,
        long? over6MEntries, decimal? over6MAmount, long totalEntries, decimal totalAmount)
    {
        table.Cell().Element(BodyCell).Text(label).Bold();
        table.Cell().Element(BodyCell).AlignRight().Text($"{upto3MEntries ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{upto3MAmount ?? 0:N2}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{three6MEntries ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{three6MAmount ?? 0:N2}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{over6MEntries ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{over6MAmount ?? 0:N2}");
        table.Cell().Element(TotalCell).AlignRight().Text($"{totalEntries:N0}").Bold();
        table.Cell().Element(TotalCell).AlignRight().Text($"{totalAmount:N2}").Bold();
    }

    private static void AddNewOver6MRow(TableDescriptor table, string label, long? entries, decimal? amount, string? reason, string? efforts)
    {
        table.Cell().Element(BodyCell).Text(label).Bold();
        table.Cell().Element(BodyCell).AlignRight().Text($"{entries ?? 0:N0}");
        table.Cell().Element(BodyCell).AlignRight().Text($"{amount ?? 0:N2}");
        table.Cell().Element(BodyCell).Text(reason ?? "-");
        table.Cell().Element(BodyCell).Text(efforts ?? "-");
    }

    private static IContainer HeaderCell(IContainer c) => c.Background(Colors.Grey.Lighten2).Border(0.5f).Padding(3).AlignMiddle();
    private static IContainer BodyCell(IContainer c) => c.Border(0.5f).Padding(3).AlignMiddle();
    private static IContainer TotalCell(IContainer c) => c.Border(0.5f).Padding(3).AlignMiddle().Background(Colors.Grey.Lighten3);
}
