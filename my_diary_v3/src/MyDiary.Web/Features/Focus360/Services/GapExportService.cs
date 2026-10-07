using System.Data;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using MyDiary.Web.Core.Extensions;
using QuestPDF.Infrastructure;
using MyDiary.Web.Features.Focus360.Models;
using QuestPDF.Fluent;

namespace MyDiary.Web.Features.Focus360.Services;

public sealed class GapExportService
{
    private readonly GapReportSettings _s;
    private readonly IStaticAssetPathResolver _assets;

    public GapExportService(IOptions<GapReportSettings> opts, IStaticAssetPathResolver assets)
    { _s = opts.Value; _assets = assets; }

    // ── EXCEL ─────────────────────────────────────────────────────────────────
    public byte[] ExportToExcel(GapReportViewModel r, DataSet? dashboardData = null)
    {
        AppLogger.LogInfo($"GapExportService: ExportToExcel started for branch {r.Branch.BranchCode}");
        QuestPDF.Settings.License = LicenseType.Community;
        using var wb = new XLWorkbook();

        // ── Sheet 1: Performance ─────────────────────────────────────────────
        var ws = wb.Worksheets.Add("Performance");
        int row = 1;

        var appLogoPath = _assets.Resolve(_s.AppLogoPath);
        var coLogoPath = _assets.Resolve(_s.LogoPath);
        if (File.Exists(appLogoPath) && new FileInfo(appLogoPath).Length > 100)
            ws.AddPicture(appLogoPath).MoveTo(ws.Cell(row, 1)).WithSize(135, 75);
        if (File.Exists(coLogoPath) && new FileInfo(coLogoPath).Length > 100)
            ws.AddPicture(coLogoPath).MoveTo(ws.Cell(row, 8)).WithSize(135, 75);
        if (File.Exists(appLogoPath) || File.Exists(coLogoPath)) row += 4;

        // Report header
        // Report header
        ws.Cell(row, 1).Value = _s.ReportName;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Range(row, 1, row, 9).Merge();
        ws.Range(row, 1, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;

        ws.Cell(row, 1).Value = $"{_s.BankName}  |  Branch: {r.Branch.BranchName} ({r.Branch.BranchCode})";
        ws.Range(row, 1, row, 9).Merge();
        ws.Range(row, 1, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;

        var asOnLbl = r.AsOnDate?.ToString("dd-MMM-yyyy") ?? r.GeneratedAt.ToString("dd-MMM-yyyy");
        ws.Cell(row, 1).Value = $"Zone: {r.Branch.ZoneName}  |  ZM/BM: {r.Branch.ZMBMName}  |  FY: {r.FinancialYear}  |  As On: {asOnLbl}";
        ws.Range(row, 1, row, 9).Merge();
        ws.Range(row, 1, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row += 2;


        // Column headers
        string[] headers = ["Parameters", "Unit", "Last FY", "Curr FY Base", $"Actual ({asOnLbl})", "FY Target", "Gap to Target", "Gap to Target%", "Sub-Category"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        row++;

        string? lastCat = null;
        foreach (var p in r.Performance)
        {
            if (p.Category != lastCat)
            {
                ws.Cell(row, 1).Value = p.Category;
                ws.Range(row, 1, row, 9).Merge();
                var catColor = XLColor.FromHtml(r.GetCategoryColor(p.Category));
                ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = catColor;
                ws.Range(row, 1, row, 9).Style.Font.FontColor = XLColor.White;
                ws.Range(row, 1, row, 9).Style.Font.Bold = true;
                lastCat = p.Category; row++;
            }
            ws.Cell(row, 1).Value = p.ParameterName;
            ws.Cell(row, 2).Value = p.UnitLabel;
            if (p.BaseLastFyCr.HasValue) ws.Cell(row, 3).Value = (double)p.BaseLastFyCr.Value;
            if (p.BaseCurrentFyCr.HasValue) ws.Cell(row, 4).Value = (double)p.BaseCurrentFyCr.Value;
            if (p.ActualCr.HasValue) ws.Cell(row, 5).Value = (double)p.ActualCr.Value;
            if (p.TargetCr.HasValue) ws.Cell(row, 6).Value = (double)p.TargetCr.Value;
            if (p.Variance.HasValue) ws.Cell(row, 7).Value = (double)p.Variance.Value;
            ws.Cell(row, 8).Value = p.VariancePct.HasValue ? $"{p.VariancePct:F2}%" : null;
            ws.Cell(row, 9).Value = p.SubCategory;
            if (p.Variance < 0) ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            if (p.Variance > 0) ws.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml("#1B5E20");
            // COUNT rows (e.g. Lockers, Channels) show as whole numbers; amounts keep 2 decimals.
            var perfNumFmt = p.DataType == "COUNT" ? "#,##0" : "#,##0.00";
            for (int c = 3; c <= 7; c++) ws.Cell(row, c).Style.NumberFormat.Format = perfNumFmt;
            row++;
        }

        // LOAN ACTIVITY

        var wsLoan = wb.Worksheets.Add("Loan Activity");

        wsLoan.Cell(1, 1).Value = "Segment";
        wsLoan.Cell(1, 2).Value = "Sanctioned";
        wsLoan.Cell(1, 3).Value = "Disbursed";

        int lr = 2;

        foreach (var l in r.LoanActivity)
        {
            wsLoan.Cell(lr, 1).Value = l.Segment;
            wsLoan.Cell(lr, 2).Value = l.Sanctioned;
            wsLoan.Cell(lr, 3).Value = l.Disbursed;
            lr++;
        }

        // DEPOSIT ACTIVITY

        var wsDep = wb.Worksheets.Add("Deposit Activity");

        wsDep.Cell(1, 1).Value = "Segment";
        wsDep.Cell(1, 2).Value = "Accounts";
        wsDep.Cell(1, 3).Value = "Amount";

        int dr = 2;

        foreach (var d in r.DepositActivity)
        {
            wsDep.Cell(dr, 1).Value = d.Segment;
            wsDep.Cell(dr, 2).Value = d.Accounts;
            wsDep.Cell(dr, 3).Value = d.Amount;
            dr++;
        }

        // ASSET QUALITY NPA and SMA

        var wsAQ = wb.Worksheets.Add("Asset Quality");

        wsAQ.Cell(1, 1).Value = "Type";
        wsAQ.Cell(1, 2).Value = "Last FY";
        wsAQ.Cell(1, 3).Value = "Actual";

        int ar = 2;

        foreach (var n in r.NpaRows)
        {
            wsAQ.Cell(ar, 1).Value = n.ParameterName;
            wsAQ.Cell(ar, 2).Value = (double?)n.BaseLastFyCr;
            wsAQ.Cell(ar, 3).Value = (double?)n.ActualCr;
            ar++;
        }

        ar += 2;

        wsAQ.Cell(ar, 1).Value = "Category";
        wsAQ.Cell(ar, 2).Value = "Amount (Cr)";
        wsAQ.Cell(ar, 3).Value = "Percentage (%)";
        ar++;

        var smaAmountsExport = r.SmaRows.Where(x => x.DataType != "PERCENTAGE").ToList();
        var smaPctsExport = r.SmaRows.Where(x => x.DataType == "PERCENTAGE").ToList();
        foreach (var amt in smaAmountsExport)
        {
            var category = amt.ParameterName.Replace(" AMOUNT", "").Replace(" AMT", "").Trim();
            var pctRow = smaPctsExport.FirstOrDefault(p => p.ParameterName.StartsWith(category, StringComparison.OrdinalIgnoreCase));
            wsAQ.Cell(ar, 1).Value = category;
            wsAQ.Cell(ar, 2).Value = (double?)amt.ActualCr;
            wsAQ.Cell(ar, 3).Value = pctRow?.ActualCr != null ? (double?)pctRow.ActualCr : null;
            ar++;
        }

        //OPERATIONS

        var wsOps = wb.Worksheets.Add("Operations");

        int or = 1;

        wsOps.Cell(or++, 1).Value = "Income";
        foreach (var i in r.IncomeRows)
        {
            wsOps.Cell(or, 1).Value = i.ParameterName;
            wsOps.Cell(or, 2).Value = i.ActualDisplay;
            or++;
        }

        or += 2;

        wsOps.Cell(or++, 1).Value = "Locker";
        foreach (var l in r.LockerRows)
        {
            wsOps.Cell(or, 1).Value = l.ParameterName;
            wsOps.Cell(or, 2).Value = l.ActualDisplay;
            or++;
        }

        or += 2;

        wsOps.Cell(or++, 1).Value = "Channels";
        foreach (var c in r.ChannelRows)
        {
            wsOps.Cell(or, 1).Value = c.ParameterName;
            wsOps.Cell(or, 2).Value = c.ActualDisplay;
            or++;
        }

        // DIGITAL BANKING and LOANS

        var wsDigital = wb.Worksheets.Add("Digital");

        int drow = 1;

        wsDigital.Cell(drow++, 1).Value = "Digital Banking";

        foreach (var d in r.DigitalBankingTable)
        {
            wsDigital.Cell(drow, 1).Value = d.Product;
            wsDigital.Cell(drow, 2).Value = d.Registered;
            wsDigital.Cell(drow, 3).Value = d.Eligible;
            drow++;
        }

        drow += 2;

        wsDigital.Cell(drow++, 1).Value = "Digital Loans";

        foreach (var l in r.DigitalLoans)
        {
            wsDigital.Cell(drow, 1).Value = l.ParameterName;
            wsDigital.Cell(drow, 2).Value = (double?)l.ActualCr;
            //wsDigital.Cell(drow, 3).Value = (double?)l.TargetCr;
            drow++;
        }

        // THIRD PARTY

        var wsTP = wb.Worksheets.Add("Third Party");

        int tpr = 1;

        foreach (var t in r.ThirdParty)
        {
            wsTP.Cell(tpr, 1).Value = t.ParameterName;
            wsTP.Cell(tpr, 2).Value = t.ActualDisplay;
            wsTP.Cell(tpr, 3).Value = (double?)t.TargetCr;
            tpr++;
        }

        // FINANCIAL INCLUSION

        var wsFI = wb.Worksheets.Add("Financial Inclusion");

        int fir = 1;

        foreach (var f in r.FinancialInclusion)
        {
            wsFI.Cell(fir, 1).Value = f.ParameterName;
            wsFI.Cell(fir, 2).Value = f.ActualDisplay;
            wsFI.Cell(fir, 3).Value = (double?)f.TargetCr;
            fir++;
        }

        // JANSAMARTH

        //var wsJS = wb.Worksheets.Add("JanSamarth");

        //int jsr = 1;

        //wsJS.Cell(jsr, 1).Value = "Scheme";

        //jsr++;

        //foreach (var j in r.JanSamarthTable)
        //{
        //    wsJS.Cell(jsr, 1).Value = j.Scheme;
        //    wsJS.Cell(jsr, 2).Value = j.Total;
        //    wsJS.Cell(jsr, 3).Value = j.Sanctioned;
        //    wsJS.Cell(jsr, 4).Value = j.Disbursed;
        //    wsJS.Cell(jsr, 5).Value = j.Rejected;
        //    wsJS.Cell(jsr, 6).Value = j.Pending;
        //    jsr++;
        //}

        ws.Columns().AdjustToContents();

        // ── Sheet 2: All Data ────────────────────────────────────────────────
        var ws2 = wb.Worksheets.Add("All Data");
        int r2 = 1;
        ws2.Cell(r2, 1).Value = $"{_s.ReportName} — Full Data Extract";
        ws2.Cell(r2, 1).Style.Font.Bold = true; r2 += 2;

        string[] ah = ["Category", "Sub-Category", "Parameter", "Unit", "Last FY", "Curr FY Base", $"Actual ({asOnLbl})", "FY Target", "Gap to Target", "Gap to Target%"];
        for (int c = 0; c < ah.Length; c++)
        {
            ws2.Cell(r2, c + 1).Value = ah[c];
            ws2.Cell(r2, c + 1).Style.Font.Bold = true;
            ws2.Cell(r2, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            ws2.Cell(r2, c + 1).Style.Font.FontColor = XLColor.White;
        }
        r2++;

        foreach (var p in r.AllData.OrderBy(x => x.SortOrder))
        {
            ws2.Cell(r2, 1).Value = p.Category;
            ws2.Cell(r2, 2).Value = p.SubCategory;
            ws2.Cell(r2, 3).Value = p.ParameterName;
            ws2.Cell(r2, 4).Value = p.UnitLabel;
            if (p.BaseLastFyCr.HasValue) ws2.Cell(r2, 5).Value = (double)p.BaseLastFyCr.Value;
            if (p.BaseCurrentFyCr.HasValue) ws2.Cell(r2, 6).Value = (double)p.BaseCurrentFyCr.Value;
            if (p.ActualCr.HasValue) ws2.Cell(r2, 7).Value = (double)p.ActualCr.Value;
            if (p.TargetCr.HasValue) ws2.Cell(r2, 8).Value = (double)p.TargetCr.Value;
            if (p.Variance.HasValue) ws2.Cell(r2, 9).Value = (double)p.Variance.Value;
            ws2.Cell(r2, 10).Value = p.VariancePct.HasValue ? $"{p.VariancePct:F2}%" : null;
            // COUNT rows (e.g. Lockers, Channels) show as whole numbers; amounts keep 2 decimals.
            var allDataNumFmt = p.DataType == "COUNT" ? "#,##0" : "#,##0.00";
            for (int c = 5; c <= 9; c++) ws2.Cell(r2, c).Style.NumberFormat.Format = allDataNumFmt;
            r2++;
        }
        ws2.Columns().AdjustToContents();

        // ── Operations Dashboard Data ─────────────────────────────────────────
        if (dashboardData != null)
        {
            WriteDashboardSheet(wb, dashboardData);
        }

        foreach (var sheet in wb.Worksheets)
        {
            sheet.PageSetup.Footer.Right.AddText(_s.ReportFooter);
            sheet.PageSetup.Footer.Left.AddText($"Generated: {r.GeneratedAt:dd-MMM-yyyy HH:mm}");
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── EXCEL 1 ─────────────────────────────────────────────────────────────────
    public byte[] ExportToExcel_Styled(GapReportViewModel r, DataSet? dashboardData = null, bool isBranchLevel = true)
    {
        AppLogger.LogInfo($"GapExportService: ExportToExcel_Styled started for branch {r.Branch.BranchCode}");
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Focus360 Report");

        int row = 1;

        // ============================================================
        // ✅ HELPER METHODS
        // ============================================================

        void SectionHeader(string title, string color)
        {
            ws.Cell(row, 1).Value = title;
            ws.Range(row, 1, row, 8).Merge();

            ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = XLColor.FromHtml(color);
            ws.Range(row, 1, row, 8).Style.Font.FontColor = XLColor.White;
            ws.Range(row, 1, row, 8).Style.Font.Bold = true;

            row++;
        }

        void LabelValue(int col, string label, string value)
        {
            ws.Cell(row, col).Value = label;
            ws.Cell(row, col).Style.Font.Bold = true;
            ws.Cell(row, col).Style.Font.FontColor = XLColor.FromHtml("#1E3A5F");
            //ws.Cell(row, col).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E3EDF5"));

            ws.Cell(row + 1, col).Value = value;
            ws.Cell(row + 1, col).Style.Font.Bold = false;
            //ws.Cell(row + 1, col).Style.Fill.SetBackgroundColor(XLColor.White);
        }

        //void ApplyCardBackground(int startRow, int endRow)
        //{
        //    ws.Range(startRow, 1, endRow, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3EDF5");
        //}

        void ApplyCardBackground(int startRow, int endRow)
        {
            for (int r = startRow; r <= endRow; r += 2) // apply only label rows
            {
                ws.Range(r, 1, r, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
            }
        }

        void StaffBox(int colStart, string title, string value, string color, bool highlightRow)
        {
            int r1 = row;
            int r2 = row + 1;

            // Merge 2 rows x 1 column (compact card)
            ws.Range(r1, colStart, r2, colStart).Merge();

            var cell = ws.Cell(r1, colStart);

            cell.Value = $"{title}\n{value}";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;

            // ✅ Apply color always
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(color);

            // ✅ Apply bold ONLY for selected rows
            cell.Style.Font.Bold = highlightRow;

            // Optional: slightly darker text for highlighted rows
            cell.Style.Font.FontColor = highlightRow
                ? XLColor.Black
                : XLColor.FromHtml("#37474F");
        }

        //void StaffBox(int colStart, string title, string value, string color)
        //{
        //    int r1 = row;
        //    int r2 = row + 1;

        //    ws.Range(r1, colStart, r2, colStart + 1).Merge();

        //    ws.Cell(r1, colStart).Value = $"{title}\n{value}";
        //    ws.Cell(r1, colStart).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        //    ws.Cell(r1, colStart).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        //    ws.Cell(r1, colStart).Style.Alignment.WrapText = true;         

        //    for (int r = r1; r <= r2; r += 2) // apply only label rows
        //    {
        //        ws.Cell(r1, colStart).Style.Fill.BackgroundColor = XLColor.FromHtml(color);
        //        //ws.Range(r, 1, r, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        //    }

        //    ws.Cell(r1, colStart).Style.Font.Bold = false;
        //}

        void TableHeader(params string[] cols)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = cols[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            row++;
        }

        XLColor RowAlt(int i) =>
            i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#F5F5F5");

        void Space(int n = 1) => row += n;

        // ============================================================
        // ✅ HEADER (Report Title)
        // ============================================================        

        var appLogoPath = _assets.Resolve(_s.AppLogoPath);
        var coLogoPath = _assets.Resolve(_s.LogoPath);
        if (File.Exists(appLogoPath) && new FileInfo(appLogoPath).Length > 100)
            ws.AddPicture(appLogoPath).MoveTo(ws.Cell(row, 1)).WithSize(135, 75);
        // coLogo is placed after AdjustToContents() for accurate right-alignment
        int _coLogoRow = row; // remember the row for deferred logo placement
        if (File.Exists(appLogoPath) || File.Exists(coLogoPath)) row += 4;

        // Report header
        ws.Cell(row, 1).Value = _s.ReportName;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Range(row, 1, row, 8).Merge();
        ws.Range(row, 1, row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;

        ws.Cell(row, 1).Value = $"{_s.BankName}  |  Branch: {r.Branch.BranchName} ({r.Branch.BranchCode})";
        ws.Range(row, 1, row, 8).Merge();
        ws.Range(row, 1, row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;

        var asOnLbl = r.AsOnDate?.ToString("dd-MMM-yyyy") ?? r.GeneratedAt.ToString("dd-MMM-yyyy");
        ws.Cell(row, 1).Value = $"Zone: {r.Branch.ZoneName}  |  ZM/BM: {r.Branch.ZMBMName}  |  FY: {r.FinancialYear}  |  As On: {asOnLbl}";
        ws.Range(row, 1, row, 8).Merge();
        ws.Range(row, 1, row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;


        // ============================================================
        // ✅ 1. BRANCH / ZO DETAILS (MATCH UI EXACTLY)
        // ============================================================

        var asOn = r.AsOnDate?.ToString("dd-MMM-yyyy") ?? "—";

        SectionHeader($"Branch / ZO Details   As on {asOn}", "#1F4E9E");

        int startRow = row;

        // FIRST ROW
        LabelValue(1, "BRANCH", r.Branch.BranchName);
        LabelValue(2, "CODE", r.Branch.BranchCode);
        LabelValue(3, "ZONE", r.Branch.ZoneName);
        LabelValue(4, "REGION", r.Branch.RegionName);
        LabelValue(5, "BM", $"{r.Branch.ZMBMName} ({r.Branch.ZMBMCode})");
        LabelValue(6, "WORKING SINCE", r.Branch.WorkingSince?.ToString("dd-MMM-yy") ?? "—");

        row += 2;

        // SECOND ROW
        LabelValue(1, "OPEN DATE", r.Branch.BranchOpenDate?.ToString("dd-MMM-yy") ?? "—");
        LabelValue(2, "LICENSE NO.", r.Branch.License ?? "—");

        int endRow = row + 1;

        ApplyCardBackground(startRow, endRow);

        Space(3);

        // ============================================================
        // ✅ 2. STAFF STRENGTH (CHIP STYLE GRID)
        // ============================================================

        SectionHeader("Staff Strength & Key Metrics", "#2B67B2");

        int col = 1;
        int itemIndex = 0;

        foreach (var s in r.Staff.OrderBy(x => x.SortOrder))
        {
            // Determine which visual row (1-based)
            int gridRow = (itemIndex / 7) + 1;

            // ✅ Highlight only 1st and 3rd row
            bool highlight = (gridRow == 1 || gridRow == 3);

            string bg = s.HeadCount == 0 ? "#ECEFF1" : "#BBDEFB";

            StaffBox(col, s.GradeCode, s.HeadCount.ToString(), bg, highlight);

            col++;
            itemIndex++;

            // wrap after 7 columns
            if (col > 7)
            {
                row += 2;   // move down properly (since each box is 2 rows)
                col = 1;
            }
        }

        // ✅ TOTAL (same row logic)
        int totalRow = (itemIndex / 7) + 1;
        bool totalHighlight = (totalRow == 1 || totalRow == 3);

        StaffBox(col, "TOTAL", r.TotalStaff.ToString(), "#90CAF9", totalHighlight);
        col++;
        itemIndex++;

        // ✅ PEB
        int pebRow = (itemIndex / 7) + 1;
        bool pebHighlight = (pebRow == 1 || pebRow == 3);

        if (isBranchLevel)
        {
            StaffBox(col, "PEB (Cr)", r.PerEmployeeBusiness.ToString("N2"), "#C8E6C9", pebHighlight);
        }

        // Move after section
        row += 3;
        // ============================================================
        // ✅ 3. PERFORMANCE
        // ============================================================

        SectionHeader("Performance", "#004D40");

        TableHeader("Parameter", "Unit", "Last FY", "Current FY", "Actual", "FY Target", "Gap to Target", "Gap to Target%");

        int pi = 0;
        foreach (var p in r.Performance)
        {
            var bg = RowAlt(pi++);

            ws.Cell(row, 1).Value = p.ParameterName;
            ws.Cell(row, 2).Value = p.UnitLabel;
            ws.Cell(row, 3).Value = (double?)p.BaseLastFyCr;
            ws.Cell(row, 4).Value = (double?)p.BaseCurrentFyCr;
            ws.Cell(row, 5).Value = (double?)p.ActualCr;
            ws.Cell(row, 6).Value = (double?)p.TargetCr;
            ws.Cell(row, 7).Value = (double?)p.Variance;
            ws.Cell(row, 8).Value = p.VariancePct;

            if ((p.Variance ?? 0) < 0)
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            else if ((p.Variance ?? 0) > 0)
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.Green;

            ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = bg;

            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 4. LOAN ACTIVITY
        // ============================================================

        SectionHeader("Loan Activity", "#2E7D32");
        TableHeader("Segment", "Sanctioned", "Disbursed");

        foreach (var l in r.LoanActivity)
        {
            ws.Cell(row, 1).Value = l.Segment;
            ws.Cell(row, 2).Value = l.Sanctioned;
            ws.Cell(row, 3).Value = l.Disbursed;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 5. DEPOSIT ACTIVITY
        // ============================================================

        SectionHeader("Deposit Activity", "#1976D2");
        TableHeader("Segment", "Accounts", "Amount");

        foreach (var d in r.DepositActivity)
        {
            ws.Cell(row, 1).Value = d.Segment;
            ws.Cell(row, 2).Value = d.Accounts;
            ws.Cell(row, 3).Value = d.Amount;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 6. ASSET QUALITY (NPA + SMA)
        // ============================================================

        //SectionHeader("Asset Quality", "#B71C1C");
        //TableHeader("Type", "Amount");

        //foreach (var n in r.NpaRows)
        //{
        //    ws.Cell(row, 1).Value = n.ParameterName;
        //    ws.Cell(row, 2).Value = (double?)n.ActualCr;
        //    row++;
        //}

        //foreach (var s in r.SmaRows)
        //{
        //    ws.Cell(row, 1).Value = s.ParameterName;
        //    ws.Cell(row, 2).Value = (double?)s.ActualCr;
        //    row++;
        //}

        //Space(2);

        // ============================================================
        // ✅ ASSET QUALITY (SIDE-BY-SIDE)
        // ============================================================

        SectionHeader("Asset Quality", "#B71C1C");

        // ✅ Sub headers (row 1)
        ws.Cell(row, 1).Value = "NPA (Gross)";
        ws.Cell(row, 4).Value = "SMA / Stress Accounts";

        ws.Range(row, 1, row, 3).Merge();
        ws.Range(row, 4, row, 6).Merge();

        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#D32F2F");
        ws.Range(row, 1, row, 6).Style.Font.FontColor = XLColor.White;
        ws.Range(row, 1, row, 6).Style.Font.Bold = true;

        row++;

        // ✅ Column headers (row 2)
        ws.Cell(row, 1).Value = "Type";
        ws.Cell(row, 2).Value = "Last FY (Cr)";
        ws.Cell(row, 3).Value = "Actual (Cr)";

        ws.Cell(row, 4).Value = "Category";
        ws.Cell(row, 5).Value = "Amount (Cr)";
        ws.Cell(row, 6).Value = "Percentage (%)";

        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFCDD2");
        ws.Range(row, 1, row, 6).Style.Font.Bold = true;

        row++;

        // ✅ Data alignment
        int npaCount = r.NpaRows.Count;
        var smaAmtRows = r.SmaRows.Where(x => x.DataType != "PERCENTAGE").ToList();
        var smaPctRows = r.SmaRows.Where(x => x.DataType == "PERCENTAGE").ToList();
        int smaCount = smaAmtRows.Count;
        int maxRows1 = Math.Max(npaCount, smaCount);

        for (int i = 0; i < maxRows1; i++)
        {
            var bg = (i % 2 == 0)
                ? XLColor.White
                : XLColor.FromHtml("#FFEBEE");

            // ---------------------------
            // ✅ NPA SECTION
            // ---------------------------
            if (i < npaCount)
            {
                var n = r.NpaRows[i];

                ws.Cell(row, 1).Value = n.ParameterName;
                ws.Cell(row, 2).Value = (double?)n.BaseCurrentFyCr;
                ws.Cell(row, 3).Value = (double?)n.ActualCr;
            }

            // ---------------------------
            // ✅ SMA SECTION (merged Amount + Percentage)
            // ---------------------------
            if (i < smaCount)
            {
                var amt = smaAmtRows[i];
                var category = amt.ParameterName.Replace(" AMOUNT", "").Replace(" AMT", "").Trim();
                var pctRow = smaPctRows.FirstOrDefault(p => p.ParameterName.StartsWith(category, StringComparison.OrdinalIgnoreCase));

                ws.Cell(row, 4).Value = category;
                ws.Cell(row, 5).Value = (double?)amt.ActualCr;
                ws.Cell(row, 6).Value = pctRow?.ActualCr != null ? (double?)pctRow.ActualCr : null;
            }

            // ✅ Row styling
            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = bg;

            row++;
        }

        row += 2;


        // ============================================================
        // ✅ OPERATIONS & CHANNELS (MAIN HEADER)
        // ============================================================

        SectionHeader("Operations & Channels", "#455A64");


        // ============================================================
        // ✅ SUB-SECTION TITLES (3 COLUMN HEADER)
        // ============================================================

        ws.Cell(row, 1).Value = "Fee Income / Expense (Cr)";
        ws.Cell(row, 3).Value = "Locker Status";
        ws.Cell(row, 5).Value = "ATMs / Channels / KYC Pending";

        ws.Range(row, 1, row, 2).Merge();
        ws.Range(row, 3, row, 4).Merge();
        ws.Range(row, 5, row, 6).Merge();

        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#607D8B");
        ws.Range(row, 1, row, 6).Style.Font.FontColor = XLColor.White;
        ws.Range(row, 1, row, 6).Style.Font.Bold = true;

        row++;


        // ============================================================
        // ✅ TABLE HEADERS (3 SECTIONS)
        // ============================================================

        ws.Cell(row, 1).Value = "Particulars";
        ws.Cell(row, 2).Value = "Actual";

        ws.Cell(row, 3).Value = "Metric";
        ws.Cell(row, 4).Value = "Value";

        ws.Cell(row, 5).Value = "Metric";
        ws.Cell(row, 6).Value = "Value";

        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#B0BEC5");
        ws.Range(row, 1, row, 6).Style.Font.Bold = true;

        row++;


        // ============================================================
        // ✅ SECTION 1: INCOME / EXPENSE
        // ============================================================

        int incomeCount = r.IncomeRows.Count;


        // ============================================================
        // ✅ SECTION 2: LOCKER STATUS
        // ============================================================

        int lockerCount = r.LockerRows.Count;


        // ============================================================
        // ✅ SECTION 3: CHANNELS / KYC
        // ============================================================

        int channelCount = r.ChannelRows.Count;


        // ============================================================
        // ✅ COMBINED DATA LOOP (ALIGN ALL 3 SECTIONS)
        // ============================================================

        int maxRows = Math.Max(incomeCount, Math.Max(lockerCount, channelCount));

        for (int i = 0; i < maxRows; i++)
        {
            var bg = (i % 2 == 0)
                ? XLColor.White
                : XLColor.FromHtml("#ECEFF1");

            // -------------------------------
            // ✅ INCOME / EXPENSE
            // -------------------------------
            if (i < incomeCount)
            {
                var inc = r.IncomeRows[i];

                ws.Cell(row, 1).Value = inc.ParameterName;
                ws.Cell(row, 2).Value = inc.ActualDisplay;

                // ✅ Negative values in red
                if (inc.ActualAsOn < 0)
                    ws.Cell(row, 2).Style.Font.FontColor = XLColor.Red;
            }

            // -------------------------------
            // ✅ LOCKER STATUS
            // -------------------------------
            if (i < lockerCount)
            {
                var l = r.LockerRows[i];

                ws.Cell(row, 3).Value = l.ParameterName;
                ws.Cell(row, 4).Value = l.ActualDisplay;
            }

            // -------------------------------
            // ✅ CHANNELS / KYC
            // -------------------------------
            if (i < channelCount)
            {
                var c = r.ChannelRows[i];

                ws.Cell(row, 5).Value = c.ParameterName;
                ws.Cell(row, 6).Value = c.ActualDisplay;
            }

            // ✅ Apply alternating row color
            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = bg;

            row++;
        }


        // ============================================================
        // ✅ BORDER (CARD LOOK)
        // ============================================================

        //ws.Range(row - maxRows, 1, row - 1, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        //ws.Range(row - maxRows, 1, row - 1, 8).Style.Border.InsideBorder = XLBorderStyleValues.Dotted;


        // ============================================================
        // ✅ SPACING AFTER SECTION
        // ============================================================

        Space(2);


        // ============================================================
        // ✅ 7. DIGITAL BANKING
        // ============================================================

        SectionHeader("Digital Banking", "#006064");
        TableHeader("Product", "Registered", "Eligible");

        foreach (var d in r.DigitalBankingTable)
        {
            ws.Cell(row, 1).Value = d.Product;
            ws.Cell(row, 2).Value = d.Registered;
            ws.Cell(row, 3).Value = d.Eligible;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 8. DIGITAL LOANS
        // ============================================================

        SectionHeader("Digital Loans", "#1A237E");
        TableHeader("Loan Type", "Actual"/*, "Target"*/);

        foreach (var l in r.DigitalLoans)
        {
            ws.Cell(row, 1).Value = l.ParameterName;
            ws.Cell(row, 2).Value = (double?)l.ActualCr;
            // ws.Cell(row, 3).Value = (double?)l.TargetCr;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 9. THIRD PARTY
        // ============================================================

        SectionHeader("Third Party Products", "#6A1B9A");
        TableHeader("Product", "Actual");

        foreach (var t in r.ThirdParty)
        {
            ws.Cell(row, 1).Value = t.ParameterName;
            ws.Cell(row, 2).Value = t.ActualDisplay;
            //ws.Cell(row, 3).Value = (double?)t.TargetCr;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 10. FINANCIAL INCLUSION
        // ============================================================

        SectionHeader("Financial Inclusion", "#33691E");
        TableHeader("Scheme", "Count", "Target");

        foreach (var f in r.FinancialInclusion)
        {
            ws.Cell(row, 1).Value = f.ParameterName;
            ws.Cell(row, 2).Value = f.ActualDisplay;
            ws.Cell(row, 3).Value = (double?)f.TargetCr;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 11. JANSAMARTH
        // ============================================================

        //SectionHeader("JanSamarth", "#880E4F");

        //TableHeader("Scheme", "Total", "Sanctioned", "Disbursed", "Rejected", "Pending");

        //foreach (var j in r.JanSamarthTable)
        //{
        //    ws.Cell(row, 1).Value = j.Scheme;
        //    ws.Cell(row, 2).Value = j.Total;
        //    ws.Cell(row, 3).Value = j.Sanctioned;
        //    ws.Cell(row, 4).Value = j.Disbursed;
        //    ws.Cell(row, 5).Value = j.Rejected;
        //    ws.Cell(row, 6).Value = j.Pending;
        //    row++;
        //}

        //// ============================================================
        //// ✅ FINAL FORMATTING
        //// ============================================================

        ws.Columns().AdjustToContents();

        // ── Deferred logo placement (after column widths are finalized) ───────
        if (File.Exists(coLogoPath) && new FileInfo(coLogoPath).Length > 100)
        {
            var pic = ws.AddPicture(coLogoPath);

            // Target size: 135px width × 75px height (matches appLogo proportions)
            // If original aspect ratio differs, WithSize will respect the dimensions.
            int targetW = 135;
            int targetH = 75;

            // Calculate total pixel width of columns 1–8 using actual adjusted widths
            // Excel formula: pixels ≈ charWidth * 7 + 5
            double totalWidthPx = 0;
            for (int c = 1; c <= 8; c++)
                totalWidthPx += (int)(ws.Column(c).Width * 7 + 5);

            // If target width exceeds sheet width, reduce proportionally
            if (targetW > totalWidthPx)
            {
                double ratio = totalWidthPx / targetW;
                targetW = (int)totalWidthPx;
                targetH = (int)(targetH * ratio);
            }

            // Right-align: calculate X offset from column 1 so right edge hits end of column 8
            double xOffset = totalWidthPx - targetW;

            // Walk through columns to find which column the offset lands in
            double accumulated = 0;
            int startCol = 1;
            int colPixelOffset = 0;
            for (int c = 1; c <= 8; c++)
            {
                double colPx = (int)(ws.Column(c).Width * 7 + 5);
                if (accumulated + colPx > xOffset)
                {
                    startCol = c;
                    colPixelOffset = (int)(xOffset - accumulated);
                    break;
                }
                accumulated += colPx;
            }

            pic.MoveTo(ws.Cell(_coLogoRow, startCol), colPixelOffset, 0).WithSize(targetW, targetH);
        }

        // ── Operations Dashboard Data (separate sheet) ────────────────────────
        if (dashboardData != null)
        {
            WriteDashboardSheet(wb, dashboardData);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }


    // ── EXCEL 2 ───────────────────────────────────────────────────────────────────

    public byte[] ExportToExcel_FullSingleSheet(GapReportViewModel r)
    {
        using var wb = new XLWorkbook();

        // ✅ SINGLE SHEET
        var ws = wb.Worksheets.Add("Focus360 Report");

        int row = 1;

        // ============================================================
        // ✅ HELPER METHODS
        // ============================================================

        void SectionHeader(string title, string color)
        {
            ws.Cell(row, 1).Value = title;
            ws.Range(row, 1, row, 10).Merge();

            ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml(color);
            ws.Range(row, 1, row, 10).Style.Font.FontColor = XLColor.White;
            ws.Range(row, 1, row, 10).Style.Font.Bold = true;

            row++;
        }

        void TableHeader(params string[] cols)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = cols[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            row++;
        }

        XLColor RowAlt(int i) =>
            i % 2 == 0 ? XLColor.White : XLColor.FromHtml("#F5F5F5");

        void Space(int n = 1) => row += n;

        // ============================================================
        // ✅ HEADER (Report Title)
        // ============================================================        

        var appLogoPath = _assets.Resolve(_s.AppLogoPath);
        var coLogoPath = _assets.Resolve(_s.LogoPath);
        if (File.Exists(appLogoPath) && new FileInfo(appLogoPath).Length > 100)
            ws.AddPicture(appLogoPath).MoveTo(ws.Cell(row, 1)).WithSize(135, 75);
        if (File.Exists(coLogoPath) && new FileInfo(coLogoPath).Length > 100)
            ws.AddPicture(coLogoPath).MoveTo(ws.Cell(row, 8)).WithSize(135, 75);
        if (File.Exists(appLogoPath) || File.Exists(coLogoPath)) row += 4;

        // Report header
        // Report header
        ws.Cell(row, 1).Value = _s.ReportName;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Range(row, 1, row, 9).Merge();
        ws.Range(row, 1, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;

        ws.Cell(row, 1).Value = $"{_s.BankName}  |  Branch: {r.Branch.BranchName} ({r.Branch.BranchCode})";
        ws.Range(row, 1, row, 9).Merge();
        ws.Range(row, 1, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row++;

        var asOnLbl = r.AsOnDate?.ToString("dd-MMM-yyyy") ?? r.GeneratedAt.ToString("dd-MMM-yyyy");
        ws.Cell(row, 1).Value = $"Zone: {r.Branch.ZoneName}  |  ZM/BM: {r.Branch.ZMBMName}  |  FY: {r.FinancialYear}  |  As On: {asOnLbl}";
        ws.Range(row, 1, row, 9).Merge();
        ws.Range(row, 1, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row += 2;

        // ============================================================
        // ✅ 2. STAFF STRENGTH
        // ============================================================

        SectionHeader("Staff Strength", "#1565C0");

        TableHeader("Grade", "Count");

        int si = 0;
        foreach (var s in r.Staff)
        {
            var bg = RowAlt(si++);
            ws.Cell(row, 1).Value = s.GradeCode;
            ws.Cell(row, 2).Value = s.HeadCount;

            ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = bg;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 3. PERFORMANCE
        // ============================================================

        SectionHeader("Performance", "#004D40");

        TableHeader("Parameter", "Unit", "Last FY", "Current FY", "Actual", "FY Target", "Gap to Target", "Gap to Target%");

        int pi = 0;
        foreach (var p in r.Performance)
        {
            var bg = RowAlt(pi++);

            ws.Cell(row, 1).Value = p.ParameterName;
            ws.Cell(row, 2).Value = p.UnitLabel;
            ws.Cell(row, 3).Value = (double?)p.BaseLastFyCr;
            ws.Cell(row, 4).Value = (double?)p.BaseCurrentFyCr;
            ws.Cell(row, 5).Value = (double?)p.ActualCr;
            ws.Cell(row, 6).Value = (double?)p.TargetCr;
            ws.Cell(row, 7).Value = (double?)p.Variance;
            ws.Cell(row, 8).Value = p.VariancePct;

            if ((p.Variance ?? 0) < 0)
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            else if ((p.Variance ?? 0) > 0)
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.Green;

            ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = bg;

            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 4. LOAN ACTIVITY
        // ============================================================

        SectionHeader("Loan Activity", "#2E7D32");
        TableHeader("Segment", "Sanctioned", "Disbursed");

        foreach (var l in r.LoanActivity)
        {
            ws.Cell(row, 1).Value = l.Segment;
            ws.Cell(row, 2).Value = l.Sanctioned;
            ws.Cell(row, 3).Value = l.Disbursed;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 5. DEPOSIT ACTIVITY
        // ============================================================

        SectionHeader("Deposit Activity", "#1976D2");
        TableHeader("Segment", "Accounts", "Amount");

        foreach (var d in r.DepositActivity)
        {
            ws.Cell(row, 1).Value = d.Segment;
            ws.Cell(row, 2).Value = d.Accounts;
            ws.Cell(row, 3).Value = d.Amount;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 6. ASSET QUALITY (NPA + SMA)
        // ============================================================

        SectionHeader("Asset Quality", "#B71C1C");
        TableHeader("Type", "Amount");

        foreach (var n in r.NpaRows)
        {
            ws.Cell(row, 1).Value = n.ParameterName;
            ws.Cell(row, 2).Value = (double?)n.ActualCr;
            row++;
        }

        foreach (var s in r.SmaRows.Where(x => x.DataType != "PERCENTAGE"))
        {
            var category = s.ParameterName.Replace(" AMOUNT", "").Replace(" AMT", "").Trim();
            var pctRow = r.SmaRows.Where(x => x.DataType == "PERCENTAGE")
                .FirstOrDefault(p => p.ParameterName.StartsWith(category, StringComparison.OrdinalIgnoreCase));
            ws.Cell(row, 1).Value = category;
            ws.Cell(row, 2).Value = (double?)s.ActualCr;
            ws.Cell(row, 3).Value = pctRow?.ActualCr != null ? (double?)pctRow.ActualCr : null;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 7. DIGITAL BANKING
        // ============================================================

        SectionHeader("Digital Banking", "#006064");
        TableHeader("Product", "Registered", "Eligible");

        foreach (var d in r.DigitalBankingTable)
        {
            ws.Cell(row, 1).Value = d.Product;
            ws.Cell(row, 2).Value = d.Registered;
            ws.Cell(row, 3).Value = d.Eligible;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 8. DIGITAL LOANS
        // ============================================================

        SectionHeader("Digital Loans", "#1A237E");
        TableHeader("Loan Type", "Actual"/*, "Target"*/);

        foreach (var l in r.DigitalLoans)
        {
            ws.Cell(row, 1).Value = l.ParameterName;
            ws.Cell(row, 2).Value = (double?)l.ActualCr;
            // ws.Cell(row, 3).Value = (double?)l.TargetCr;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 9. THIRD PARTY
        // ============================================================

        SectionHeader("Third Party Products", "#6A1B9A");
        TableHeader("Product", "Actual", "Target");

        foreach (var t in r.ThirdParty)
        {
            ws.Cell(row, 1).Value = t.ParameterName;
            ws.Cell(row, 2).Value = t.ActualDisplay;
            ws.Cell(row, 3).Value = (double?)t.TargetCr;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 10. FINANCIAL INCLUSION
        // ============================================================

        SectionHeader("Financial Inclusion", "#33691E");
        TableHeader("Scheme", "Count", "Target");

        foreach (var f in r.FinancialInclusion)
        {
            ws.Cell(row, 1).Value = f.ParameterName;
            ws.Cell(row, 2).Value = f.ActualDisplay;
            ws.Cell(row, 3).Value = (double?)f.TargetCr;
            row++;
        }

        Space(2);

        // ============================================================
        // ✅ 11. JANSAMARTH
        // ============================================================

        //SectionHeader("JanSamarth", "#880E4F");

        //TableHeader("Scheme", "Total", "Sanctioned", "Disbursed", "Rejected", "Pending");

        //foreach (var j in r.JanSamarthTable)
        //{
        //    ws.Cell(row, 1).Value = j.Scheme;
        //    ws.Cell(row, 2).Value = j.Total;
        //    ws.Cell(row, 3).Value = j.Sanctioned;
        //    ws.Cell(row, 4).Value = j.Disbursed;
        //    ws.Cell(row, 5).Value = j.Rejected;
        //    ws.Cell(row, 6).Value = j.Pending;
        //    row++;
        //}

        // ============================================================
        // ✅ FINAL FORMATTING
        // ============================================================

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportToPdf(GapReportViewModel report)
    {
        return new Focus360PdfDocument(report).GeneratePdf();
    }


    // ── CSV ───────────────────────────────────────────────────────────────────
    public byte[] ExportToCsv(GapReportViewModel r, DataSet? dashboardData = null, bool isBranchLevel = true)
    {
        AppLogger.LogInfo($"GapExportService: ExportToCsv started for branch {r.Branch.BranchCode}");
        var sb = new StringBuilder();
        var asOnLbl = r.AsOnDate?.ToString("dd-MMM-yyyy") ?? r.GeneratedAt.ToString("dd-MMM-yyyy");

        sb.AppendLine($"# {_s.ReportName}");
        sb.AppendLine($"# {_s.BankName}");
        sb.AppendLine($"# Branch: {r.Branch.BranchName} ({r.Branch.BranchCode})  |  Zone: {r.Branch.ZoneName}");
        sb.AppendLine($"# Financial Year: {r.FinancialYear}  |  As On: {asOnLbl}");
        sb.AppendLine($"# Generated: {r.GeneratedAt:dd-MMM-yyyy HH:mm}");
        sb.AppendLine();


        // ============================================================
        // ✅ 1. BRANCH / ZO DETAILS
        // ============================================================
        var asOn = r.AsOnDate?.ToString("dd-MMM-yyyy") ?? "—";

        sb.AppendLine($"# ============================================================");
        sb.AppendLine($"# Branch / ZO Details   As on {asOn}");
        sb.AppendLine($"# ============================================================");

        // First row
        sb.AppendLine("BRANCH,CODE,ZONE,REGION,BM,WORKING SINCE");
        sb.AppendLine(string.Join(",", new[]
        {
            Csv(r.Branch.BranchName),
            Csv(r.Branch.BranchCode),
            Csv(r.Branch.ZoneName),
            Csv(r.Branch.RegionName),
            Csv($"{r.Branch.ZMBMName} ({r.Branch.ZMBMCode})"),
            Csv(r.Branch.WorkingSince?.ToString("dd-MMM-yy") ?? "—")
        }));

        sb.AppendLine();

        // Second row
        sb.AppendLine("OPEN DATE,LICENSE NO.");
        sb.AppendLine(string.Join(",", new[]
        {
            Csv(r.Branch.BranchOpenDate?.ToString("dd-MMM-yy") ?? "—"),
            Csv(string.IsNullOrWhiteSpace(r.Branch.License) ? "—" : r.Branch.License)
        }));

        sb.AppendLine();



        // ============================================================
        // ✅ 2. STAFF STRENGTH
        // ============================================================
        sb.AppendLine($"# ============================================================");
        sb.AppendLine("# Staff Strength & Key Metrics");
        sb.AppendLine($"# ============================================================");

        sb.AppendLine("GRADE,HEADCOUNT");

        foreach (var s in r.Staff.OrderBy(x => x.SortOrder))
        {
            sb.AppendLine($"{Csv(s.GradeCode)},{Csv(s.HeadCount.ToString())}");
        }

        sb.AppendLine($"TOTAL,{r.TotalStaff}");
        if (isBranchLevel)
        {
            sb.AppendLine($"PEB (Cr),{r.PerEmployeeBusiness:N2}");
        }
        sb.AppendLine();


        sb.AppendLine("=== Performance Parameters (₹ in Crore) ===");
        sb.AppendLine($"Category,Sub-Category,Parameter,Unit,Last FY,Curr FY Base,Actual ({asOnLbl}),FY Target,Gap to Target,Gap to Target%");

        foreach (var p in r.AllData.OrderBy(x => x.SortOrder))
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Csv(p.Category), Csv(p.SubCategory), Csv(p.ParameterName), Csv(p.UnitLabel),
                Csv(p.BaseLastFyCr?.ToString("N2")),
                Csv(p.BaseCurrentFyCr?.ToString("N2")),
                // ActualDisplay is type-aware: whole-number counts (e.g. Lockers),
                // percentages keep %, amounts keep 2 decimals.
                Csv(p.ActualDisplay == "—" ? null : p.ActualDisplay),
                Csv(p.TargetCr?.ToString("N2")),
                Csv(p.Variance?.ToString("N2")),
                Csv(p.VariancePct.HasValue ? $"{p.VariancePct:F2}%" : null)
            }));
        }

        // ── Operations Dashboard Data ─────────────────────────────────────────
        if (dashboardData != null)
        {
            AppendDashboardCsv(sb, dashboardData);
        }

        sb.AppendLine();
        sb.AppendLine($"# {_s.ReportFooter}");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private void AppendDashboardCsv(StringBuilder sb, DataSet ds)
    {
        DataRow? suspRow = null, sundRow = null, acctRow = null, aadhaarRow = null, panRow = null, complRow = null, cashRow = null;
        foreach (DataTable table in ds.Tables)
        {
            if (table.Rows.Count == 0 || !table.Columns.Contains("TableName")) continue;
            var name = table.Rows[0]["TableName"]?.ToString();
            switch (name)
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

        sb.AppendLine();
        sb.AppendLine("=== Operations ===");
        sb.AppendLine(",Previous Month,Previous Day,Present Day");

        sb.AppendLine();
        sb.AppendLine("--- Suspense Entries ---");
        sb.AppendLine($"No. Of Entries,{DI(suspRow, "PrevMonthEntries")},{DI(suspRow, "PrevDay2Entries")},{DI(suspRow, "PrevDayEntries")}");
        sb.AppendLine($"Outstanding,{DD(suspRow, "PrevMonthTotal")},{DD(suspRow, "PrevDay2Total")},{DD(suspRow, "PrevDayTotal")}");
        sb.AppendLine($"w/w Older > 90 Days,,{DI(suspRow, "PrevDayEntries90Days")},{DD(suspRow, "PrevDayTotal90Days")}");
        sb.AppendLine($"w/w Older > 180 Days,,{DI(suspRow, "PrevDayEntries180Days")},{DD(suspRow, "PrevDayTotal180Days")}");
        var csvSuspVar = SafeDec2(suspRow, "PrevDay2Total") - SafeDec2(suspRow, "PrevMonthTotal");
        var csvSuspPct = SafeDec2(suspRow, "PrevMonthTotal") != 0 ? (csvSuspVar / SafeDec2(suspRow, "PrevMonthTotal")) * 100 : 0;
        var csvSuspVarToday = SafeDec2(suspRow, "PrevDayTotal") - SafeDec2(suspRow, "PrevDay2Total");
        var csvSuspPctToday = SafeDec2(suspRow, "PrevDay2Total") != 0 ? (csvSuspVarToday / SafeDec2(suspRow, "PrevDay2Total")) * 100 : 0;
        sb.AppendLine($"Variations :,,{csvSuspVar:N0} | {csvSuspPct:0.00}%,{csvSuspVarToday:N0} | {csvSuspPctToday:0.00}%");

        sb.AppendLine();
        sb.AppendLine("--- Sundry Entries ---");
        sb.AppendLine($"No. Of Entries,{DI(sundRow, "PrevMonthEntries")},{DI(sundRow, "PrevDay2Entries")},{DI(sundRow, "PrevDayEntries")}");
        sb.AppendLine($"Outstanding,{DD(sundRow, "PrevMonthTotal")},{DD(sundRow, "PrevDay2Total")},{DD(sundRow, "PrevDayTotal")}");
        sb.AppendLine($"w/w Older > 90 Days,,{DI(sundRow, "PrevDayEntries90Days")},{DD(sundRow, "PrevDayTotal90Days")}");
        sb.AppendLine($"w/w Older > 180 Days,,{DI(sundRow, "PrevDayEntries180Days")},{DD(sundRow, "PrevDayTotal180Days")}");
        var csvSundVar = SafeDec2(sundRow, "PrevDay2Total") - SafeDec2(sundRow, "PrevMonthTotal");
        var csvSundPct = SafeDec2(sundRow, "PrevMonthTotal") != 0 ? (csvSundVar / SafeDec2(sundRow, "PrevMonthTotal")) * 100 : 0;
        var csvSundVarToday = SafeDec2(sundRow, "PrevDayTotal") - SafeDec2(sundRow, "PrevDay2Total");
        var csvSundPctToday = SafeDec2(sundRow, "PrevDay2Total") != 0 ? (csvSundVarToday / SafeDec2(sundRow, "PrevDay2Total")) * 100 : 0;
        sb.AppendLine($"Variations :,,{csvSundVar:N0} | {csvSundPct:0.00}%,{csvSundVarToday:N0} | {csvSundPctToday:0.00}%");
        sb.AppendLine();
        sb.AppendLine("--- Defaulting Accounts ---");
        sb.AppendLine($"Dormant,{DI(acctRow, "PrevMonthNoOfDormant")},{DI(acctRow, "PrevDay2NoOfDormant")},{DI(acctRow, "PrevDayNoOfDormant")}");
        sb.AppendLine($"Inactive,{DI(acctRow, "PrevMonthNoOfInactive")},{DI(acctRow, "PrevDay2NoOfInactive")},{DI(acctRow, "PrevDayNoOfInactive")}");
        sb.AppendLine($"No Nomination,{DI(acctRow, "PrevMonthNoOfNoNomination")},{DI(acctRow, "PrevDay2NoOfNoNomination")},{DI(acctRow, "PrevDayNoOfNoNomination")}");

        sb.AppendLine();
        sb.AppendLine("--- Lien Marked ---");
        sb.AppendLine($"No. of Accounts,{DI(acctRow, "PrevMonthLienMarked")},{DI(acctRow, "PrevDay2LienMarked")},{DI(acctRow, "PrevDayLienMarked")}");
        sb.AppendLine($"Amount,{DD(acctRow, "PrevMonthAmountLienMarked")},{DD(acctRow, "PrevDay2AmountLienMarked")},{DD(acctRow, "PrevDayAmountLienMarked")}");

        sb.AppendLine();
        sb.AppendLine("--- DEAF ---");
        sb.AppendLine($"No. of Accounts,{DI(acctRow, "PrevMonthNoOfDeaf")},{DI(acctRow, "PrevDay2NoOfDeaf")},{DI(acctRow, "PrevDayNoOfDeaf")}");
        sb.AppendLine($"Amount,{DD(acctRow, "PrevMonthAmountDeaf")},{DD(acctRow, "PrevDay2AmountDeaf")},{DD(acctRow, "PrevDayAmountDeaf")}");

        sb.AppendLine();
        sb.AppendLine("--- UCIC Pendency ---");
        sb.AppendLine($"Aadhaar,{DI(aadhaarRow, "PrevMonthTotal")},{DI(aadhaarRow, "PrevDay2Total")},{DI(aadhaarRow, "PrevDayTotal")}");
        sb.AppendLine($"PAN,{DI(panRow, "PrevMonthTotal")},{DI(panRow, "PrevDay2Total")},{DI(panRow, "PrevDayTotal")}");

        sb.AppendLine();
        sb.AppendLine("--- Customer Complaints ---");
        sb.AppendLine($"Complaints,{DD(complRow, "PrevMonthNoOfComplaints")},{DD(complRow, "PrevDay2NoOfComplaints")},{DD(complRow, "PrevDayNoOfComplaints")}");

        sb.AppendLine();
        sb.AppendLine("--- Cash Holding ---");
        sb.AppendLine($"Limit,{DD(cashRow, "PrevMonthLimit")},{DD(cashRow, "PrevDay2Limit")},{DD(cashRow, "PrevDayLimit")}");
        sb.AppendLine($"Actual,{DD(cashRow, "PrevMonthTotal")},{DD(cashRow, "PrevDay2Total")},{DD(cashRow, "PrevDayTotal")}");
    }

    private void WriteDashboardSheet(XLWorkbook wb, DataSet ds)
    {
        var ws = wb.Worksheets.First();
        int startRow = (ws.LastRowUsed()?.RowNumber() ?? 0) + 3;

        // Extract data
        DataRow? suspRow = null, sundRow = null, acctRow = null, aadhaarRow = null, panRow = null, complRow = null, cashRow = null;
        foreach (DataTable table in ds.Tables)
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

        // Dates
        var pmDt = SafeDateStr(suspRow, "PrevMonthDt") ?? SafeDateStr(acctRow, "PrevMonthDt") ?? "";
        var pdDt = SafeDateStr(suspRow, "PrevDay2Dt") ?? SafeDateStr(acctRow, "PrevDay2Dt") ?? "";
        var tdDt = SafeDateStr(suspRow, "PrevDayDt") ?? SafeDateStr(acctRow, "PrevDayDt") ?? "";

        // ── "Operations" banner (full width) ──
        int row = startRow;
        WR(ws, row, 1, 8, "Operations", "#0070C0", true); row++;

        // ── Left panel header ──
        int leftStart = row;
        WR(ws, row, 1, 4, "Suspense and Sundry Entries", "#204060", true); row++;
        WD(ws, row, 1, pmDt, pdDt, tdDt); row++;
        WR(ws, row, 1, 4, "Suspense Entries", "#2F75B5", true); row++;
        WL(ws, row, 1, "No. Of Entries", DI(suspRow, "PrevMonthEntries"), DI(suspRow, "PrevDay2Entries"), DI(suspRow, "PrevDayEntries")); row++;
        WL(ws, row, 1, "Outstanding", DD(suspRow, "PrevMonthTotal"), DD(suspRow, "PrevDay2Total"), DD(suspRow, "PrevDayTotal")); row++;
        WL(ws, row, 1, "w/w Older than 90 Days :", "", DI(suspRow, "PrevDayEntries90Days"), DD(suspRow, "PrevDayTotal90Days")); row++;
        WL(ws, row, 1, "w/w Older than 180 Days :", "", DI(suspRow, "PrevDayEntries180Days"), DD(suspRow, "PrevDayTotal180Days")); row++;
        var suspVar = SafeDec2(suspRow, "PrevDay2Total") - SafeDec2(suspRow, "PrevMonthTotal");
        var suspPct = SafeDec2(suspRow, "PrevMonthTotal") != 0 ? (suspVar / SafeDec2(suspRow, "PrevMonthTotal")) * 100 : 0;
        var suspVarToday = SafeDec2(suspRow, "PrevDayTotal") - SafeDec2(suspRow, "PrevDay2Total");
        var suspPctToday = SafeDec2(suspRow, "PrevDay2Total") != 0 ? (suspVarToday / SafeDec2(suspRow, "PrevDay2Total")) * 100 : 0;
        WL(ws, row, 1, "Variations :", "", $"{suspVar:N0} | {suspPct:0.00}%", $"{suspVarToday:N0} | {suspPctToday:0.00}%"); row++;
        WR(ws, row, 1, 4, "Sundry Entries", "#F4B084", true); row++;
        WL(ws, row, 1, "No. Of Entries", DI(sundRow, "PrevMonthEntries"), DI(sundRow, "PrevDay2Entries"), DI(sundRow, "PrevDayEntries")); row++;
        WL(ws, row, 1, "Outstanding", DD(sundRow, "PrevMonthTotal"), DD(sundRow, "PrevDay2Total"), DD(sundRow, "PrevDayTotal")); row++;
        WL(ws, row, 1, "w/w Older than 90 Days :", "", DI(sundRow, "PrevDayEntries90Days"), DD(sundRow, "PrevDayTotal90Days")); row++;
        WL(ws, row, 1, "w/w Older than 180 Days :", "", DI(sundRow, "PrevDayEntries180Days"), DD(sundRow, "PrevDayTotal180Days")); row++;
        var sundVar = SafeDec2(sundRow, "PrevDay2Total") - SafeDec2(sundRow, "PrevMonthTotal");
        var sundPct = SafeDec2(sundRow, "PrevMonthTotal") != 0 ? (sundVar / SafeDec2(sundRow, "PrevMonthTotal")) * 100 : 0;
        var sundVarToday = SafeDec2(sundRow, "PrevDayTotal") - SafeDec2(sundRow, "PrevDay2Total");
        var sundPctToday = SafeDec2(sundRow, "PrevDay2Total") != 0 ? (sundVarToday / SafeDec2(sundRow, "PrevDay2Total")) * 100 : 0;
        WL(ws, row, 1, "Variations :", "", $"{sundVar:N0} | {sundPct:0.00}%", $"{sundVarToday:N0} | {sundPctToday:0.00}%"); row++;
        int leftEnd = row;

        // ── Right panel (starts from same row as left panel) ──
        row = leftStart;
        WR(ws, row, 5, 8, "Defaulting Accounts  & UCIC Pendency", "#204060", true); row++;
        WD(ws, row, 5, pmDt, pdDt, tdDt); row++;
        WR(ws, row, 5, 8, "Defaulting Accounts", "#9BC2E6", true); row++;
        WL(ws, row, 5, "Dormant", DI(acctRow, "PrevMonthNoOfDormant"), DI(acctRow, "PrevDay2NoOfDormant"), DI(acctRow, "PrevDayNoOfDormant")); row++;
        WL(ws, row, 5, "Inactive", DI(acctRow, "PrevMonthNoOfInactive"), DI(acctRow, "PrevDay2NoOfInactive"), DI(acctRow, "PrevDayNoOfInactive")); row++;
        WL(ws, row, 5, "No Nomination", DI(acctRow, "PrevMonthNoOfNoNomination"), DI(acctRow, "PrevDay2NoOfNoNomination"), DI(acctRow, "PrevDayNoOfNoNomination")); row++;
        WR(ws, row, 5, 8, "Lien Marked", "#548235", true); row++;
        WL(ws, row, 5, "No. of Accounts", DI(acctRow, "PrevMonthLienMarked"), DI(acctRow, "PrevDay2LienMarked"), DI(acctRow, "PrevDayLienMarked")); row++;
        WL(ws, row, 5, "Amount", DD(acctRow, "PrevMonthAmountLienMarked"), DD(acctRow, "PrevDay2AmountLienMarked"), DD(acctRow, "PrevDayAmountLienMarked")); row++;
        WR(ws, row, 5, 8, "DEAF", "#00B0F0", true); row++;
        WL(ws, row, 5, "No. of Accounts", DI(acctRow, "PrevMonthNoOfDeaf"), DI(acctRow, "PrevDay2NoOfDeaf"), DI(acctRow, "PrevDayNoOfDeaf")); row++;
        WL(ws, row, 5, "Amount", DD(acctRow, "PrevMonthAmountDeaf"), DD(acctRow, "PrevDay2AmountDeaf"), DD(acctRow, "PrevDayAmountDeaf")); row++;
        WR(ws, row, 5, 8, "UCIC Pendency", "#F4B084", true); row++;
        WL(ws, row, 5, "Aadhaar", DI(aadhaarRow, "PrevMonthTotal"), DI(aadhaarRow, "PrevDay2Total"), DI(aadhaarRow, "PrevDayTotal")); row++;
        WL(ws, row, 5, "PAN", DI(panRow, "PrevMonthTotal"), DI(panRow, "PrevDay2Total"), DI(panRow, "PrevDayTotal")); row++;
        int rightEnd = row;

        // ── Cash Holding + Customer Complaints (below both panels) ──
        row = Math.Max(leftEnd, rightEnd) + 1;
        var cashPmDt = SafeDateStr(cashRow, "PrevMonthDt") ?? pmDt;
        var cashPdDt = SafeDateStr(cashRow, "PrevDay2Dt") ?? pdDt;
        var cashTdDt = SafeDateStr(cashRow, "PrevDayDt") ?? tdDt;

        WR(ws, row, 1, 4, "Cash Holding", "#204060", true);
        WR(ws, row, 5, 8, "Customer Complaints", "#204060", true); row++;
        WD(ws, row, 1, cashPmDt, cashPdDt, cashTdDt);
        WD(ws, row, 5, cashPmDt, cashPdDt, cashTdDt); row++;
        WL(ws, row, 1, "Limit (INR Lacs)", DD(cashRow, "PrevMonthLimit"), DD(cashRow, "PrevDay2Limit"), DD(cashRow, "PrevDayLimit"));
        WL(ws, row, 5, "Number of Complaints", DD(complRow, "PrevMonthNoOfComplaints"), DD(complRow, "PrevDay2NoOfComplaints"), DD(complRow, "PrevDayNoOfComplaints")); row++;
        WL(ws, row, 1, "Actual (INR Lacs)", DD(cashRow, "PrevMonthTotal"), DD(cashRow, "PrevDay2Total"), DD(cashRow, "PrevDayTotal")); row++;

        ws.Columns(1, 8).AdjustToContents();
    }

    // ── Excel helpers ─────────────────────────────────────────────────────────
    private static void WR(IXLWorksheet ws, int row, int c1, int c2, string text, string bgHex, bool bold)
    {
        ws.Cell(row, c1).Value = text;
        ws.Range(row, c1, row, c2).Merge();
        ws.Range(row, c1, row, c2).Style.Fill.BackgroundColor = XLColor.FromHtml(bgHex);
        ws.Range(row, c1, row, c2).Style.Font.FontColor = XLColor.White;
        ws.Range(row, c1, row, c2).Style.Font.Bold = bold;
    }

    private static void WD(IXLWorksheet ws, int row, int startCol, string pm, string pd, string td)
    {

        ws.Cell(row, startCol + 1).Value = string.IsNullOrEmpty(pm) ? "Previous Month" : $"Previous Month\n({pm})";
        ws.Cell(row, startCol + 2).Value = string.IsNullOrEmpty(pd) ? "Previous Day" : $"Previous Day\n({pd})";
        ws.Cell(row, startCol + 3).Value = string.IsNullOrEmpty(td) ? "Present Day" : $"Present Day\n({td})";

        for (int c = startCol + 1; c <= startCol + 3; c++)
        {
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6E0B4");
            ws.Cell(row, c).Style.Alignment.WrapText = true;
            ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void WL(IXLWorksheet ws, int row, int startCol, string label, string pm, string pd, string td)
    {
        ws.Cell(row, startCol).Value = label;
        ws.Cell(row, startCol + 1).Value = pm;
        ws.Cell(row, startCol + 2).Value = pd;
        ws.Cell(row, startCol + 3).Value = td;
    }

    private static string? SafeDateStr(DataRow? r, string col)
    {
        if (r == null) return null;
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDateTime(r[col]).ToString("dd/MM/yyyy") : null; }
        catch { return null; }
    }

    private static string DI(DataRow? r, string col)
    {
        if (r == null) return "—";
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToInt32(r[col]).ToString("N0") : "—"; }
        catch { return "—"; }
    }

    private static string DD(DataRow? r, string col)
    {
        if (r == null) return "—";
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDecimal(r[col]).ToString("N2") : "—"; }
        catch { return "—"; }
    }

    private static decimal SafeDec2(DataRow? r, string col)
    {
        if (r == null) return 0m;
        try { return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDecimal(r[col]) : 0m; }
        catch { return 0m; }
    }

    private static string Csv(string? v)
    {
        if (v is null) return string.Empty;
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
