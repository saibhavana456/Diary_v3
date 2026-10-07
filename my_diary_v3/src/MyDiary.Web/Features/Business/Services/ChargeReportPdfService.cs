using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using System.IO;
using System.Linq;
using MyDiary.Web.Features.Business.Models;

public class ChargeReportPdfService
{
    private const double Left = 40;
    private const double PageBottomMargin = 760;
    private const double LineHeight = 18;

    private XFont TitleFont = new("Times New Roman", 16, XFontStyleEx.Bold);
    private XFont SectionFont = new("Times New Roman", 12, XFontStyleEx.Bold);
    private XFont BodyFont = new("Times New Roman", 10, XFontStyleEx.Regular);
    private XFont SmallFont = new("Times New Roman", 9, XFontStyleEx.Italic);

    private PdfPage page;
    private XGraphics gfx;
    private double y;

    /* =========================================================
       PUBLIC METHODS
       ========================================================= */

    public byte[] ExportPartAToPdf(ChargePartAViewModel m)
    {
        using var doc = CreateDocument("Handing Over Report");

        Section("Official Submitting the Charge");
        KV("Name", m.OfficerName);
        KV("Employee No (PF)", m.EmployeeNo);
        KV("Designation", m.Designation);

        KV("Branch", m.BranchName);
        KV("Regional Office (RO)", m.RegionName);
        KV("Zonal Office (ZO)", m.ZoneName);

        KV("Date Charge Taken", F(m.ChargeTakenDate));
        KV("Date Relieved", F(m.RelievedDate));
        KV("Relieved By", m.RelievedBy);
        Badge("Joint Custodian", m.IsJointCustodian);

        Section("1. Document Availability");
        foreach (var d in m.Documents)
            Bullet($"{d.DocumentName} – {(d.IsAvailable ? "Available" : "Not Available")}");

        Section("2. Safe Custody & Keys");
        Badge("Safe custody receipt", m.SafeCustodyReceiptAvailable);
        KV("First key holder", m.FirstKeyHolder);
        KV("Second key holder", m.SecondKeyHolder);
        KV("Duplicate keys lodged at", m.DuplicateKeysLodgedAt);
        KV("Date", F(m.DuplicateKeysLodgedDate));

        Section("3. Adhoc / OD Accounts");
        DrawTable(
            new[] { "Sl", "Account No", "Borrower", "Remarks" },
            m.AdhocAccounts.Select((x, i) =>
                new[] { (i + 1).ToString(), x.AccountNo, x.BorrowerName, x.Remarks })
        );

        Section("4. Review / Renewal Pending");
        DrawTable(
            new[] { "Sl", "Account No", "Borrower", "Remarks" },
            m.ReviewPendingAccounts.Select((x, i) =>
                new[] { (i + 1).ToString(), x.AccountNo, x.BorrowerName, x.Remarks })
        );

        Section("5. Seized Documents");
        DrawTable(
            new[] { "Sl", "Account No", "Borrower", "Remarks" },
            m.SeizedDocuments.Select((x, i) =>
                new[] { (i + 1).ToString(), x.AccountNo, x.BorrowerName, x.Remarks })
        );

        Section("6. Service Charge Concessions");
        DrawTable(
            new[] { "Sl", "Party", "Concession", "Remarks" },
            m.ServiceChargeConcessions.Select((x, i) =>
                new[] { (i + 1).ToString(), x.PartyName, x.ConcessionDetails, x.Remarks })
        );

        TextSection("7. KYC Pending", m.KycPendingComments);
        TextSection("8. Loan / Title Deeds", m.LoanDocumentException);
        TextSection("9. Time Barred Debts", m.TimeBarredDebts);
        TextSection("10. Audit Pending", m.AuditPendingStatus);

        Section("11. Permanent File");
        Paragraph(m.PermanentFileUpdated
            ? "Permanent file is available and updated."
            : "Permanent file is not available / not updated.");

        TextSection("12. Any Other Matter", m.OtherMatters);

        Section("Declaration & Signature");
        Paragraph("I hereby declare that the information furnished above is true and correct.");
        KV("Declaration Date", F(m.DeclarationDate));
        KV("Signature", m.OutgoingOfficerSignature);
        KV("Incoming Officer PF", m.IncomingEmployeeNo);

        return Save(doc);
    }

    public byte[] ExportPartBToPdf(ChargePartBViewModel m)
    {
        using var doc = CreateDocument("Taking Over Report");

        Section("Official Submitting the Charge Taking Over Report");
        KV("Name", m.OfficerName);
        KV("Employee No (PF)", m.EmployeeNo);
        KV("Designation", m.Designation);

        KV("Branch", m.BranchName);
        KV("Regional Office (RO)", m.RegionName);
        KV("Zonal Office (ZO)", m.ZoneName);

        KV("Date Reported for Duty", F(m.DateReportedForDuty));
        KV("Date Charge Taken", F(m.DateChargeTaken));
        KV("Last Inspection Date", F(m.LastInspectionDate));

        Section("1. Document Availability");
        foreach (var d in m.Documents)
            Bullet($"{d.DocumentName} – {(d.IsAvailable ? "Available" : "Not Available")}");

        Section("2. Safe Custody & Key Verification");
        Badge("Safe custody receipt", m.SafeCustodyReceiptAvailable);
        KV("First key holder", m.FirstKeyHolder);
        KV("Second key holder", m.SecondKeyHolder);
        KV("Duplicate keys lodged at", m.DuplicateKeysLodgedAt);
        KV("Date", F(m.DuplicateKeysLodgedDate));

        Section("3. Customer Complaints");
        DrawTable(
            new[] { "Sl", "Name", "Date", "Remarks" },
            m.CustomerComplaints.Select((x, i) =>
                new[] { (i + 1).ToString(), x.ComplainantName, F(x.ComplaintDate), x.Remarks })
        );

        Section("4. Consumer Cases");
        DrawTable(
            new[] { "Sl", "Name", "Date", "Remarks" },
            m.ConsumerCases.Select((x, i) =>
                new[] { (i + 1).ToString(), x.ComplainantName, F(x.ComplaintDate), x.Remarks })
        );

        Section("5. Time Barred Accounts");
        DrawTable(
            new[] { "Sl", "Account No", "Borrower", "Remarks" },
            m.TimeBarredAccounts.Select((x, i) =>
                new[] { (i + 1).ToString(), x.AccountNo, x.BorrowerName, x.Remarks })
        );

        Section("6. Capital Assets");
        DrawTable(
            new[] { "Description", "Discrepancy", "Remarks" },
            m.CapitalAssets.Select(x =>
                new[] { x.AssetDescription, x.Discrepancy, x.Remarks })
        );

        Section("7. Sundry Balances");
        DrawTable(
            new[] { "Date", "Amount", "Particulars" },
            m.SundryBalances.Select(x =>
                new[] { F(x.EntryDate), x.Amount.ToString(), x.Particulars })
        );

        Section("8. Vehicle Documents");
        Badge("Documents in order", m.VehicleDocumentsInOrder);
        KV("Insurance renewal due", F(m.InsuranceRenewalDueDate));

        Section("9. Godown Keys");
        DrawTable(
            new[] { "Account No", "Borrower", "Remarks" },
            m.GodownKeysVerification.Select(x =>
                new[] { x.AccountNo, x.BorrowerName, x.Remarks })
        );

        Section("10(a). Gold Verification");
        Badge("Gold bag tally", m.GoldBagCountMatches);

        Section("10(b). Gold Discrepancies");
        DrawTable(
            new[] { "Account No", "Borrower", "Remarks" },
            m.GoldDiscrepancies.Select(x =>
                new[] { x.AccountNo, x.BorrowerName, x.Remarks })
        );

        Section("11. Locker Keys");
        Badge("Keys properly held", m.LockerKeysProperlyHeld);

        Section("12. Gun License");
        Badge("Verified", m.GunLicenseVerified);
        Paragraph(m.GunDiscrepancies);

        Section("13. Cash Verification");
        Badge("Tallies with register", m.CashTalliesWithRegister);

        Section("14. Permanent File");
        Badge("Updated", m.PermanentFileUpdated);

        Section("15. Declaration & Signature");
        Paragraph(m.OtherMatters);
        KV("Declaration Date", F(m.DeclarationDate));
        KV("Signature", m.IncomingOfficerSignature);

        return Save(doc);
    }

    /* =========================================================
       LOW‑LEVEL HELPERS
       ========================================================= */

    private PdfDocument CreateDocument(string title)
    {
        var doc = new PdfDocument();
        page = doc.AddPage();
        gfx = XGraphics.FromPdfPage(page);
        y = 70;

        gfx.DrawRectangle(
            new XSolidBrush(XColor.FromArgb(230, 240, 250)),
            0, 0, page.Width, 50);

        gfx.DrawString(title, TitleFont, XBrushes.DarkBlue, 40, 30);
        return doc;
    }

    private void Section(string title)
    {
        NewPageIfNeeded();

        gfx.DrawRectangle(
            new XSolidBrush(XColor.FromArgb(245, 247, 250)),
            Left, y - 2, page.Width - 80, 24);

        gfx.DrawString(title, SectionFont, XBrushes.Black, Left + 6, y + 14);
        y += 30;
    }

    private void KV(string k, string v)
    {
        NewPageIfNeeded();
        gfx.DrawString($"{k}: {v}", BodyFont, XBrushes.Black, Left, y);
        y += LineHeight;
    }

    private void Bullet(string text)
    {
        NewPageIfNeeded();
        gfx.DrawString($"• {text}", BodyFont, XBrushes.Black, Left + 10, y);
        y += LineHeight;
    }

    private void Paragraph(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            y += 8;
            return;
        }

        var tf = new XTextFormatter(gfx);
        var rect = new XRect(Left, y, page.Width - 80, 1000);
        tf.DrawString(text, BodyFont, XBrushes.Black, rect);
        y += LineHeight * text.Split('\n').Length + 10;
    }

    private void Badge(string label, bool value)
    {
        NewPageIfNeeded();

        gfx.DrawString(label + ":", BodyFont, XBrushes.Black, Left, y);

        gfx.DrawRectangle(
            value ? XBrushes.LightGreen : XBrushes.IndianRed,
            Left + 160, y - 12, 40, 16);

        gfx.DrawString(
            value ? "YES" : "NO",
            BodyFont,
            XBrushes.Black,
            Left + 168, y);

        y += LineHeight;
    }

    private void DrawTable(string[] headers, IEnumerable<string[]> rows)
    {
        double width = page.Width - 80;
        double colW = width / headers.Length;

        gfx.DrawRectangle(XBrushes.LightGray, Left, y, width, LineHeight);
        for (int i = 0; i < headers.Length; i++)
            gfx.DrawString(headers[i], BodyFont, XBrushes.Black, Left + i * colW + 4, y + 12);

        y += LineHeight;

        foreach (var r in rows)
        {
            NewPageIfNeeded();
            for (int i = 0; i < r.Length; i++)
            {
                gfx.DrawRectangle(XPens.Black, Left + i * colW, y, colW, LineHeight);
                gfx.DrawString(r[i] ?? "", BodyFont, XBrushes.Black, Left + i * colW + 4, y + 12);
            }
            y += LineHeight;
        }

        y += 12;
    }

    private void TextSection(string title, string text)
    {
        Section(title);
        Paragraph(text);
    }

    private void NewPageIfNeeded()
    {
        if (y > PageBottomMargin)
        {
            page = page.Owner.AddPage();
            gfx = XGraphics.FromPdfPage(page);
            y = 70;
        }
    }

    private byte[] Save(PdfDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    private static string F(DateTime? d) => d?.ToString("dd-MM-yyyy") ?? "";
}