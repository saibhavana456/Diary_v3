using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MyDiary.Web.Features.Focus360.Models;


namespace MyDiary.Web.Features.Focus360.Services
{

    public class Focus360PdfDocument : IDocument
    {
        private readonly GapReportViewModel _r;

        public Focus360PdfDocument(GapReportViewModel report)
        {
            _r = report;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4.Portrait());
                page.Margin(20);

                page.Header().Text(text =>
                {
                    text.AlignCenter();
                    text.Span("FOCUS 360: Financial Operational and Compliance Unit Snapshot")
                        .FontSize(14).Bold();
                });

                page.Content().Column(col =>
                {
                    col.Spacing(15);

                    AddBranchDetails(col);
                    AddStaff(col);
                    AddPerformance(col);
                    AddLoanDeposit(col);
                    AddNpa(col);
                    AddOperations(col);
                    AddDigital(col);
                    AddFinancialInclusion(col);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                    x.Span("  |  Generated: " + _r.GeneratedAt.ToString("dd-MMM-yyyy HH:mm"));
                });
            });
        }

        private void AddBranchDetails(ColumnDescriptor col)
        {
            var asOn = _r.AsOnDate?.ToString("dd-MMM-yyyy") ?? "—";
            col.Item().Text($"Branch Details — As on {asOn}").Bold().FontSize(12);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                void Row(string l1, string v1, string l2, string v2)
                {
                    table.Cell().Text(l1).SemiBold().FontSize(9);
                    table.Cell().Text(v1 ?? "-").FontSize(9);
                    table.Cell().Text(l2).SemiBold().FontSize(9);
                    table.Cell().Text(v2 ?? "-").FontSize(9);
                }

                Row("Branch", _r.Branch.BranchName, "Code", _r.Branch.BranchCode);
                Row("Zone", _r.Branch.ZoneName, "Region", _r.Branch.RegionName);
                Row("BM", _r.Branch.ZMBMName, "Scale", _r.Branch.Scale);
                Row("Open Date", _r.Branch.BranchOpenDate?.ToString("dd-MMM-yy") ?? "—",
                    "Timings", _r.Branch.BranchTimeDisplay);
            });
        }

        private void AddStaff(ColumnDescriptor col)
        {
            if (!_r.Staff.Any()) return;

            col.Item().Text("Staff Strength").Bold();

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.ConstantColumn(80);
                });

                table.Header(h =>
                {
                    h.Cell().Text("Grade").Bold();
                    h.Cell().Text("Count").Bold();
                });

                foreach (var s in _r.Staff)
                {
                    table.Cell().Text(s.GradeCode);
                    table.Cell().Text(s.HeadCount.ToString());
                }

                table.Cell().Text("TOTAL").Bold();
                table.Cell().Text(_r.TotalStaff.ToString()).Bold();
            });
        }

        private void AddPerformance(ColumnDescriptor col)
        {
            if (!_r.Performance.Any()) return;

            col.Item().Text("Performance Parameters (₹ in Crore)").Bold().FontSize(12);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3); // Parameter
                    c.RelativeColumn(1); // Unit
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                table.Header(h =>
                {
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Parameter").FontColor(Colors.White).FontSize(8).Bold();
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Unit").FontColor(Colors.White).FontSize(8).Bold();
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Last FY").FontColor(Colors.White).FontSize(8).Bold();
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Curr FY").FontColor(Colors.White).FontSize(8).Bold();
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Actual").FontColor(Colors.White).FontSize(8).Bold();
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Target").FontColor(Colors.White).FontSize(8).Bold();
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Var").FontColor(Colors.White).FontSize(8).Bold();
                    h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Var %").FontColor(Colors.White).FontSize(8).Bold();
                });

                string lastCat = null;

                foreach (var p in _r.Performance)
                {
                    if (p.Category != lastCat)
                    {
                        lastCat = p.Category;

                        table.Cell().ColumnSpan(8)
                            .Background(Colors.Grey.Lighten2)
                            .Padding(2)
                            .Text(p.Category).Bold().FontSize(9);
                    }

                    table.Cell().Padding(2).Text(p.ParameterName).FontSize(8);
                    table.Cell().Padding(2).Text(p.UnitLabel).FontSize(8);
                    table.Cell().Padding(2).Text(Fmt(p.BaseLastFyCr)).FontSize(8);
                    table.Cell().Padding(2).Text(Fmt(p.BaseCurrentFyCr)).FontSize(8);
                    table.Cell().Padding(2).Text(Fmt(p.ActualCr)).FontSize(8);
                    table.Cell().Padding(2).Text(Fmt(p.TargetCr)).FontSize(8);
                    table.Cell().Padding(2).Text(Fmt(p.Variance)).FontSize(8);
                    table.Cell().Padding(2).Text(p.VariancePct.HasValue ? $"{p.VariancePct.Value:F1}%" : "-").FontSize(8);
                }
            });
        }

        private void AddLoanDeposit(ColumnDescriptor col)
        {
            if (!_r.LoanActivity.Any() && !_r.DepositActivity.Any()) return;

            col.Item().Text("Business Activity").Bold();

            col.Item().Row(row =>
            {
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Segment").Bold();
                        h.Cell().Text("Sanctioned").Bold();
                        h.Cell().Text("Disbursed").Bold();
                    });

                    foreach (var r in _r.LoanActivity)
                    {
                        table.Cell().Text(r.Segment);
                        table.Cell().Text(r.Sanctioned.ToString());
                        table.Cell().Text(r.Disbursed.ToString());
                    }
                });

                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Segment").Bold();
                        h.Cell().Text("Accounts").Bold();
                        h.Cell().Text("Amount").Bold();
                    });

                    foreach (var d in _r.DepositActivity)
                    {
                        table.Cell().Text(d.Segment);
                        table.Cell().Text(d.Accounts.ToString());
                        table.Cell().Text(d.Amount.ToString());
                    }
                });
            });
        }

        private void AddNpa(ColumnDescriptor col)
        {
            if (!_r.NpaRows.Any()) return;

            col.Item().Text("NPA Details").Bold();

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                table.Header(h =>
                {
                    h.Cell().Text("Type").Bold();
                    h.Cell().Text("Last FY").Bold();
                    h.Cell().Text("Actual").Bold();
                });

                foreach (var n in _r.NpaRows)
                {
                    table.Cell().Text(n.ParameterName);
                    table.Cell().Text(Fmt(n.BaseLastFyCr));
                    table.Cell().Text(Fmt(n.ActualCr));
                }
            });
        }

        private void AddOperations(ColumnDescriptor col)
        {
            if (!_r.IncomeRows.Any() && !_r.LockerRows.Any()) return;

            col.Item().Text("Operations").Bold();

            AddSimpleTable(col, _r.IncomeRows, "Particulars", "Value");
            AddSimpleTable(col, _r.LockerRows, "Metric", "Value");
            AddSimpleTable(col, _r.ChannelRows, "Metric", "Value");
        }

        private void AddDigital(ColumnDescriptor col)
        {
            if (!_r.DigitalLoans.Any() && !_r.ThirdParty.Any()) return;

            col.Item().Text("Digital & Third Party").Bold();

            AddSimpleTable(col, _r.DigitalLoans, "Loan", "Actual");
            AddSimpleTable(col, _r.ThirdParty, "Product", "Actual");
        }

        private void AddFinancialInclusion(ColumnDescriptor col)
        {
            if (!_r.FinancialInclusion.Any()) return;

            col.Item().Text("Financial Inclusion").Bold();

            AddSimpleTable(col, _r.FinancialInclusion, "Scheme", "Count");
        }

        private void AddSimpleTable(ColumnDescriptor col,
        List<GapPerformanceRow> rows,
        string c1, string c2)
        {
            if (!rows.Any()) return;

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                table.Header(h =>
                {
                    h.Cell().Text(c1).Bold();
                    h.Cell().Text(c2).Bold();
                });

                foreach (var r in rows)
                {
                    table.Cell().Text(r.ParameterName);
                    table.Cell().Text(r.ActualDisplay ?? "0");
                }
            });
        }

        private string Fmt(decimal? v) =>
            v.HasValue ? v.Value.ToString("N2") : "-";
    }

}

