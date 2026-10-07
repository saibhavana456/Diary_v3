using ClosedXML.Excel;
using MyDiary.Web.Features.BranchFoundation.Models;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace MyDiary.Web.Features.BranchFoundation.Services;

public class BranchFoundationService : IBranchFoundationService
{
    private readonly string _cs;

    public BranchFoundationService(IConfiguration config)
    {
        _cs = config.GetConnectionString("SqlServerConnection")
              ?? throw new InvalidOperationException("Missing SqlServerConnection string");
    }

    private SqlConnection Conn() => new(_cs);

    public async Task<List<BranchFoundationDto>> GetBranchesAsync(string? zoneName = null, string? regionName = null, string? solId = null)
    {
        var list = new List<BranchFoundationDto>();
        await using var cn = Conn();
        await cn.OpenAsync();

        var sql = @"SELECT sol_id, branch_name, region_name, zone_name_eng, br_est_date 
                    FROM branch_master 
                    WHERE br_est_date IS NOT NULL AND br_est_date != ''";

        // Build dynamic WHERE clause based on user's highest security scope
        if (!string.IsNullOrWhiteSpace(solId))
        {
            sql += " AND sol_id = @p_solId";
        }
        else if (!string.IsNullOrWhiteSpace(regionName))
        {
            sql += " AND UPPER(region_name) = UPPER(@p_region)";
        }
        else if (!string.IsNullOrWhiteSpace(zoneName))
        {
            sql += " AND UPPER(zone_name_eng) = UPPER(@p_zone)";
        }

        await using var cmd = new SqlCommand(sql, cn);

        if (!string.IsNullOrWhiteSpace(solId)) cmd.Parameters.AddWithValue("@p_solId", solId.Trim());
        else if (!string.IsNullOrWhiteSpace(regionName)) cmd.Parameters.AddWithValue("@p_region", regionName.Trim());
        else if (!string.IsNullOrWhiteSpace(zoneName)) cmd.Parameters.AddWithValue("@p_zone", zoneName.Trim());

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var rawDateString = r.IsDBNull(4) ? null : r.GetValue(4).ToString();

            list.Add(new BranchFoundationDto
            {
                SolId = r.IsDBNull(0) ? "" : r.GetString(0),
                BranchName = r.IsDBNull(1) ? "" : r.GetString(1),
                RegionName = r.IsDBNull(2) ? "" : r.GetString(2),
                ZoneName = r.IsDBNull(3) ? "" : r.GetString(3),
                EstDate = ParseMixedDate(rawDateString)
            });
        }

        return list.OrderBy(x => x.EstDate).ToList();
    }

    private DateTime? ParseMixedDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        dateStr = dateStr.Trim();

        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return parsedDate;

        string[] fallbackFormats = { "MMM dd yyyy hh:mm tt", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yyyy" };
        if (DateTime.TryParseExact(dateStr, fallbackFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            return parsedDate;

        return null;
    }

    public byte[] ExportToExcel(List<BranchFoundationDto> branches)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Branch Foundation List");

        string[] headers = { "Branch Code", "Branch Name", "Region", "Zone", "Opening Date", "Years Completed", "Anniversary Date" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#1E3A8A"));
            cell.Style.Font.SetFontColor(XLColor.White);
        }

        int row = 2;
        foreach (var b in branches)
        {
            ws.Cell(row, 1).Value = b.SolId;
            ws.Cell(row, 2).Value = b.BranchName;
            ws.Cell(row, 3).Value = b.RegionName;
            ws.Cell(row, 4).Value = b.ZoneName;

            if (b.EstDate.HasValue)
            {
                ws.Cell(row, 5).Value = b.EstDate.Value;
                ws.Cell(row, 5).Style.DateFormat.Format = "dd-MMM-yyyy";
            }

            ws.Cell(row, 6).Value = b.YearsCompleted;
            ws.Cell(row, 7).Value = b.AnniversaryDate;
            row++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}