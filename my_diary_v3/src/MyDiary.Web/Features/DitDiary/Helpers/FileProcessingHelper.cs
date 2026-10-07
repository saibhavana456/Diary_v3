using System.Reflection;
using ClosedXML.Excel;

namespace MyDiary.Web.Features.DitDiary.Helpers
{
    public static class FileProcessingHelper
    {
        public static byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            // Get public properties to use as headers
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // 1. Create Headers with Styling
            for (int i = 0; i < properties.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = properties[i].Name.Replace("_", " ");
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // 2. Add Data Rows
            int rowIndex = 2;
            foreach (var item in data)
            {
                for (int i = 0; i < properties.Length; i++)
                {
                    var value = properties[i].GetValue(item);
                    var cell = worksheet.Cell(rowIndex, i + 1);

                    // Handle Numeric/Currency strings (like item.COST)
                    if (properties[i].Name.ToUpper().Contains("COST") && value is string strValue)
                    {
                        if (double.TryParse(strValue, out var numericVal))
                        {
                            cell.Value = numericVal;
                            cell.Style.NumberFormat.Format = "[$₹-439] #,##,##0.00";
                            continue;
                        }
                    }

                    cell.Value = value?.ToString();
                }
                rowIndex++;
            }

            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public static List<T> ImportFromExcel<T>(Stream fileStream) where T : new()
        {
            var list = new List<T>();
            try
            {
                // 1. Copy the BrowserFileStream to a seekable MemoryStream
                using var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0; // Reset position to start for ClosedXML

                // 2. Open the workbook from the MemoryStream
                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheet(1);
                var properties = typeof(T).GetProperties();

                // Ensure the worksheet actually has data
                var range = worksheet.RangeUsed();
                if (range == null) return list;

                var rows = range.RowsUsed().Skip(1); // Skip Header

                foreach (var row in rows)
                {
                    var item = new T();
                    for (int i = 0; i < properties.Length; i++)
                    {
                        var cell = row.Cell(i + 1);
                        if (cell == null || cell.IsEmpty()) continue;

                        var prop = properties[i];
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                        try
                        {
                            // Use GetValue<string>() for better compatibility with ClosedXML types
                            var cellValue = cell.GetValue<string>();

                            if (!string.IsNullOrEmpty(cellValue))
                            {
                                var convertedValue = Convert.ChangeType(cellValue, targetType);
                                prop.SetValue(item, convertedValue);
                            }
                        }
                        catch (Exception valEx)
                        {
                            // Log specific row/column errors if conversion fails
                            Console.WriteLine($"Error converting column {i + 1} at row {row.RowNumber()}: {valEx.Message}");
                        }
                    }
                    list.Add(item);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel Import Fatal Error: {ex.Message}");
            }
            return list;
        }
    }
}
