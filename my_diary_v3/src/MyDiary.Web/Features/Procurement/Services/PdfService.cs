using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using MyDiary.Core.Services;
using MyDiary.Web.Features.Procurement.Data;
using MyDiary.Web.Features.Procurement.Services;
using MyDiary.Web.Features.Procurement.Models.VIEWS;
using static MyDiary.Web.Features.Procurement.Components.ATMIndentEditPage;

namespace MyDiary.Web.Features.Procurement.Services
{
    public interface IPdfService
    {
        Task<byte[]> GeneratePoPdf(ProcurementEditVm vm, string sessionUserId);
    }

    public class PdfService : IPdfService
    {
        private readonly MyDiary.Web.Core.Extensions.IStaticAssetPathResolver _assets;

        private readonly ProcurementDbContext _dbContext;

        public PdfService(MyDiary.Web.Core.Extensions.IStaticAssetPathResolver assets, ProcurementDbContext dbContext)
        {
            _assets = assets;
            _dbContext = dbContext;
        }

        public async Task<byte[]> GeneratePoPdf(ProcurementEditVm vm, string sessionUserId)
        {
            var userDetails = getUserDetails(vm.RaisedBy);
            var CurrentuserDetails = getUserDetails(sessionUserId);

            using var document = new PdfDocument();
            PdfPage page = null;
            XGraphics gfx = null;

            // Page State Variables
            double pagePadding = 40;
            double bottomMargin = 800; // Threshold to trigger a new page
            double y = 30;

            // Internal Helper to manage page creation
            void AddNewPage()
            {
                page = document.AddPage();
                page.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = 40; // Reset Y for new page
            }

            // Initialize first page
            AddNewPage();

            // Fonts
            var boldFont = new XFont("Times New Roman", 12, XFontStyle.Bold);
            var normalFont = new XFont("Times New Roman", 12, XFontStyle.Regular);
            var italicFont = new XFont("Times New Roman", 11, XFontStyle.Italic);
            var smallFont = new XFont("Times New Roman", 9, XFontStyle.Regular);

            // ================= LOGOS (Page 1 Only) =================
            // Logos are resolved from the NFS-aware assets root (wwwroot/MyDiary/images
            // under NFS, wwwroot/images locally). Missing files are skipped so PDF
            // generation never crashes on an absent logo.
            var leftPath = _assets.Resolve("images/union_ease.png");
            var centerPath = _assets.Resolve("images/logubi.png");
            var rightPath = _assets.Resolve("images/75_year_India.png");

            double leftW = 70; double leftH = 50;
            double centerW = 160; double centerH = 60;
            double rightW = 80; double rightH = 50;

            if (File.Exists(leftPath))
                gfx.DrawImage(XImage.FromFile(leftPath), pagePadding, y, leftW, leftH);
            if (File.Exists(centerPath))
                gfx.DrawImage(XImage.FromFile(centerPath), (page.Width - centerW) / 2, y - 5, centerW, centerH);
            if (File.Exists(rightPath))
                gfx.DrawImage(XImage.FromFile(rightPath), page.Width - pagePadding - rightW, y, rightW, rightH);

            y += centerH + 20;

            // ================= HEADER =================
            gfx.DrawString($"Ref: {vm.PoRefno}", boldFont, XBrushes.Black, pagePadding, y);
            gfx.DrawString($"Date: {AppTime.Now:dd-MM-yyyy}", boldFont, XBrushes.Black, page.Width - pagePadding - 100, y);
            y += 30;

            // ================= RECIPIENT =================
            gfx.DrawString("To,", normalFont, XBrushes.Black, pagePadding, y);
            y += 20;
            gfx.DrawString(vm.Rows.FirstOrDefault()?.VendorName ?? "<Company Name>", boldFont, XBrushes.Black, pagePadding, y);
            y += 18;

            string vendorAddress = vm.Rows.FirstOrDefault()?.VendorAddress ?? "<Address>";
            // Pass gfx as ref so it updates if a new page is triggered during wrapping
            y = DrawWrappedTextDynamic(ref gfx, vendorAddress, boldFont, pagePadding, y, 250, 15, bottomMargin, AddNewPage);

            if (y > bottomMargin - 40) AddNewPage();
            y += 15;
            gfx.DrawString("Dear Sir,", normalFont, XBrushes.Black, pagePadding, y);
            y += 25;

            // ================= SUBJECT =================
            string subText = $"Sub: - Purchase order for Procurement of IT Assets to {userDetails.BR_ADD1} {userDetails.BR_ADD2} {userDetails.BR_ADD3} {userDetails.STATE_NAME_ENG} {userDetails.DISTRICT_NAME_ENG}";
            y = DrawWrappedTextDynamic(ref gfx, subText, boldFont, pagePadding + 15, y, page.Width - (pagePadding * 2) - 15, 18, bottomMargin, AddNewPage);
            y += 10;

            // ================= BODY TEXT =================
            string allItemNames = string.Join(", ", vm.Rows.Select(r => r.ItemName).Distinct());
            string fullBodyText = $"We refer to the rate contract {vm.Rows.FirstOrDefault()?.VendorName} " +
                                  $"dated {vm.PoIssuedOn:dd-MM-yyyy} for the purchase of the {allItemNames}. " +
                                  $"We are pleased to confirm the indent for the below mentioned items. " +
                                  $"Kindly arrange for the delivery of the items at the earliest.";

            y = DrawWrappedTextDynamic(ref gfx, fullBodyText, normalFont, pagePadding, y, page.Width - (pagePadding * 2), 18, bottomMargin, AddNewPage);
            y += 25;

            // ================= PRICE DETAILS TABLE =================
            if (y > bottomMargin - 60) AddNewPage();
            gfx.DrawString("Price Details:", boldFont, XBrushes.Black, pagePadding, y);
            y += 15;

            double[] colWidths = { 40, 220, 70, 80, 95 };
            string[] headers = { "Sl.", "Description", "Quantity", "Unit Price", "Total Price" };
            double rowH = 25;

            // Header Row
            double tableX = pagePadding;
            for (int i = 0; i < headers.Length; i++)
            {
                gfx.DrawRectangle(new XPen(XColors.Black, 1.2), tableX, y, colWidths[i], rowH);
                gfx.DrawString(headers[i], boldFont, XBrushes.Black, new XRect(tableX + 5, y, colWidths[i], rowH), XStringFormats.CenterLeft);
                tableX += colWidths[i];
            }
            y += rowH;

            // Item Rows
            int sl = 1;
            decimal grandTotal = 0;
            foreach (var item in vm.Rows)
            {
                if (y > bottomMargin - rowH) AddNewPage(); // Trigger new page if table row won't fit

                tableX = pagePadding;
                string[] rowData = {
                    sl++.ToString(),
                    item.ItemName ?? "",
                    item.QuantityApproved?.ToString() ?? "0",
                    item.ItemCost?.ToString("N2") ?? "0.00",
                    item.CostBeforeTax?.ToString("N2") ?? "0.00"
                };

                for (int i = 0; i < rowData.Length; i++)
                {
                    gfx.DrawRectangle(XPens.Black, tableX, y, colWidths[i], rowH);
                    var format = i >= 3 ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
                    gfx.DrawString(rowData[i], normalFont, XBrushes.Black, new XRect(tableX + 5, y, colWidths[i] - 10, rowH), format);
                    tableX += colWidths[i];
                }
                grandTotal += item.CostBeforeTax ?? 0;
                y += rowH;
            }

            // Total Row
            if (y > bottomMargin - rowH) AddNewPage();
            double labelWidth = colWidths[0] + colWidths[1] + colWidths[2] + colWidths[3];
            gfx.DrawRectangle(new XPen(XColors.Black, 1.2), pagePadding, y, labelWidth, rowH);
            gfx.DrawString("Grand Total (Excl. Tax)", boldFont, XBrushes.Black, new XRect(pagePadding, y, labelWidth - 5, rowH), XStringFormats.CenterRight);
            gfx.DrawRectangle(new XPen(XColors.Black, 1.2), pagePadding + labelWidth, y, colWidths[4], rowH);
            gfx.DrawString(grandTotal.ToString("N2"), boldFont, XBrushes.Black, new XRect(pagePadding + labelWidth, y, colWidths[4] - 5, rowH), XStringFormats.CenterRight);

            y += 40;

            // ================= TERMS & CONDITIONS =================
            if (y > bottomMargin - 20) AddNewPage();
            gfx.DrawString("*GST will be applicable as per the terms.", italicFont, XBrushes.Black, pagePadding, y);
            y += 25;

            if (y > bottomMargin - 20) AddNewPage();
            string amountInWords = "Amount in Words: " + ConvertToWords(grandTotal) + " Only";
            gfx.DrawString(amountInWords, boldFont, XBrushes.Black, pagePadding, y);
            y += 30;

            if (y > bottomMargin - 40) AddNewPage();
            gfx.DrawString("Payment terms:", boldFont, XBrushes.Black, pagePadding, y);
            gfx.DrawLine(XPens.Black, pagePadding, y + 2, pagePadding + 80, y + 2);
            y += 20;

            string[] payTerms = {
                "No advance payment shall be made.",
                "The payment will be made only after the delivery and installation of the items.",
                "Payment Mode: NEFT/RTGS",
                "Amount is inclusive/exclusive of tax",
                "Vendor must show the bifurcation/details of GST (CGST/SGST/IGST) in the invoice."
            };

            foreach (var term in payTerms)
            {
                if (y > bottomMargin - 20) AddNewPage();
                gfx.DrawString("• " + term, normalFont, XBrushes.Black, pagePadding + 15, y);
                y += 18;
            }

            // Delivery Details
            if (y > bottomMargin - 20) AddNewPage();
            gfx.DrawString("• Delivery Location: " + userDetails.NAME, normalFont, XBrushes.Black, pagePadding + 15, y);
            y += 18;

            string fullDeliveryAddress = $"{userDetails.BR_ADD1} {userDetails.BR_ADD2} {userDetails.BR_ADD3} {userDetails.STATE_NAME_ENG} {userDetails.DISTRICT_NAME_ENG}";
            y = DrawWrappedTextDynamic(ref gfx, fullDeliveryAddress, normalFont, pagePadding + 25, y, 450, 15, bottomMargin, AddNewPage);

            if (y > bottomMargin - 20) AddNewPage();
            y += 5;
            gfx.DrawString($"Contact: {userDetails.CONTACT_NO}, Email: {userDetails.EMAIL_ID}", normalFont, XBrushes.Black, pagePadding + 30, y);
            y += 30;

            if (y > bottomMargin - 20) AddNewPage();
            gfx.DrawString("Service Terms and Conditions:", boldFont, XBrushes.Black, pagePadding, y);
            y += 20;

            // Service Terms Wrapping
            string term1 = "• As per the terms of the RFP, on receipt of this order, delivery / installation is to be carried out within RFP/rate contract hours/days else applicable penalty shall be recovered.";
            y = DrawWrappedTextDynamic(ref gfx, term1, normalFont, pagePadding + 15, y, page.Width - (pagePadding * 2) - 15, 18, bottomMargin, AddNewPage);

            string term2 = "• All the terms and conditions as mentioned in the original PO / RFP will be applicable and will form part of the agreement.";
            y = DrawWrappedTextDynamic(ref gfx, term2, normalFont, pagePadding + 15, y, page.Width - (pagePadding * 2) - 15, 18, bottomMargin, AddNewPage);

            if (y > bottomMargin - 20) AddNewPage();
            gfx.DrawString("• All order cancellation terms as applicable in the RFP.", normalFont, XBrushes.Black, pagePadding + 15, y);
            y += 25;

            // Signature Area

            if (y > bottomMargin - 150) AddNewPage();

            gfx.DrawString("Thanking You,", normalFont, XBrushes.Black, pagePadding, y);

            // 1. Define the Center of your signature block
            // We take the right side of the page and move in about 100 units
            double signatureCenterX = page.Width - pagePadding - 90;
            double signatureMaxWidth = 180;

            // 2. "Yours Faithfully" (Centered)
            XSize sizeYF = gfx.MeasureString("Yours Faithfully,", normalFont);
            gfx.DrawString("Yours Faithfully,", normalFont, XBrushes.Black,
                           signatureCenterX - (sizeYF.Width / 2), y);

            y += 45; // Space for signature

            // 3. "SD" (Centered)
            string sdText = "SD";
            XSize sdSize = gfx.MeasureString(sdText, italicFont);
            gfx.DrawString(sdText, italicFont, XBrushes.Black,
                           signatureCenterX - (sdSize.Width / 2), y);

            y += 20;

            // 4. Name (Centered)
            string signerName = $"({CurrentuserDetails.NAME})";
            y = DrawCenteredWrappedText(ref gfx, signerName, normalFont, signatureCenterX, y, signatureMaxWidth, 15, bottomMargin, AddNewPage);

            // 5. Job Description (Centered)
            string jobDesc = CurrentuserDetails.JOB_DESC ?? "";
            y = DrawCenteredWrappedText(ref gfx, jobDesc, normalFont, signatureCenterX, y, signatureMaxWidth, 15, bottomMargin, AddNewPage);

            // 6. Location (Centered & Cleaned)
            string cleanLocation = (CurrentuserDetails.LOCATION_DESC ?? "")
                .Replace("CENTRAL OFFICE - ", "")
                .Replace("CENTRAL OFFICE", "")
                .Trim();
            y = DrawCenteredWrappedText(ref gfx, cleanLocation, normalFont, signatureCenterX, y, signatureMaxWidth, 15, bottomMargin, AddNewPage); y += 30;
            if (y > page.Height - 30) AddNewPage();
            string autoGenNote = "This document is computer generated and requires no physical signature.";
            XSize textSize = gfx.MeasureString(autoGenNote, smallFont);
            gfx.DrawString(autoGenNote, smallFont, XBrushes.Gray, (page.Width - textSize.Width) / 2, y);

            using var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }

        private double DrawCenteredWrappedText(ref XGraphics gfx, string text, XFont font, double centerX, double y, double maxWidth, double lineHeight, double bottomMargin, Action addNewPage)
        {
            var lines = GetWrappedLines(text, font, maxWidth, gfx); // Use your existing GetWrappedLines
            foreach (var line in lines)
            {
                if (y > bottomMargin) { addNewPage(); y = 40; }

                XSize lineSize = gfx.MeasureString(line, font);
                // Calculate X to make the line center-aligned to centerX
                double lineX = centerX - (lineSize.Width / 2);

                gfx.DrawString(line, font, XBrushes.Black, lineX, y);
                y += lineHeight;
            }
            return y;
        }
        private List<string> GetWrappedLines(string text, XFont font, double maxWidth, XGraphics gfx)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (gfx.MeasureString(testLine, font).Width > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines;
        }

        // UPDATED WRAPPING HELPER TO HANDLE PAGES
        private double DrawWrappedTextDynamic(ref XGraphics gfx, string text, XFont font, double x, double y, double maxWidth, double lineHeight, double bottomMargin, Action addNewPage)
        {
            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                XSize size = gfx.MeasureString(testLine, font);

                if (size.Width > maxWidth)
                {
                    if (y > bottomMargin)
                    {
                        addNewPage();
                        y = 40; // Use reset Y from helper logic
                    }
                    gfx.DrawString(currentLine, font, XBrushes.Black, x, y);
                    y += lineHeight;
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
            {
                if (y > bottomMargin)
                {
                    addNewPage();
                    y = 40;
                }
                gfx.DrawString(currentLine, font, XBrushes.Black, x, y);
                y += lineHeight;
            }
            return y;
        }

        // Keep existing helper methods exactly as they are
        private string ConvertToWords(decimal number) { /* ... Your existing logic ... */ return NumberToWords((long)number); }
        private string NumberToWords(long number)
        {
            if (number == 0) return "Zero";
            if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));
            string words = "";
            if ((number / 10000000) > 0) { words += NumberToWords(number / 10000000) + " Crore "; number %= 10000000; }
            if ((number / 100000) > 0) { words += NumberToWords(number / 100000) + " Lakh "; number %= 100000; }
            if ((number / 1000) > 0) { words += NumberToWords(number / 1000) + " Thousand "; number %= 1000; }
            if ((number / 100) > 0) { words += NumberToWords(number / 100) + " Hundred "; number %= 100; }
            if (number > 0)
            {
                if (words != "") words += "and ";
                var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
                var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
                if (number < 20) words += unitsMap[number];
                else { words += tensMap[number / 10]; if ((number % 10) > 0) words += "-" + unitsMap[number % 10]; }
            }
            return words.Trim();
        }

        public UserMasterView getUserDetails(string pfNo)
        {
            return _dbContext.UserMasterView.Where(x => x.PF_NO == pfNo).FirstOrDefault();
        }
    }
}