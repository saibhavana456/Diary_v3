using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using MyDiary.Web.Features.SubsidiaryJv.Models;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.SubsidiaryJv.Services;

/// <summary>Subsidiaries and JV Corner. Self-contained raw Oracle ADO (same style as StaffVendorConnectService).</summary>
public sealed class SubsidiaryJvService : ISubsidiaryJvService
{
    private readonly string _cs;
    private readonly IAuthorizationService _authz;
    private readonly ILogger<SubsidiaryJvService> _log;
    private static readonly Dictionary<string, string> LogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".svg"] = "image/svg+xml", [".webp"] = "image/webp"
    };

    public SubsidiaryJvService(IConfiguration config, IAuthorizationService authz, ILogger<SubsidiaryJvService> log)
    {
        _cs = config.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:RP_Owner");
        _authz = authz;
        _log = log;
    }

    private OracleConnection Conn() => new(_cs);

    // ══ Sheet / column definition — single source for the template, the Instructions sheet and the parser ══
    private sealed record Col(string Name, bool Required, string Type, string Format, string Description, string Example);

    private static readonly string[] EntityTypes = { "SUBSIDIARY", "JOINT_VENTURE", "OTHER", "ASSOCIATE" };
    private static readonly string[] StatusValues = { "Active", "Inactive" };

    private static readonly (string Sheet, string Purpose, Col[] Cols)[] Sheets =
    {
        ("Entity", "One row per Subsidiary / JV / associate. Drives the landing-page card and the entity page header, KPI strip, company profile and contact details.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Max 30, no spaces (A-Z, 0-9, _)", "Unique short key of the entity. Used in EVERY sheet to link rows to this entity. Re-uploading an existing code updates that entity.", "UAMCL"),
            new Col("ENTITY_NAME", true, "Text", "Max 300", "Full legal name shown as the page title and card title.", "Union Asset Management Company Limited (UAMCL)"),
            new Col("SHORT_NAME", false, "Text", "Max 60", "Short name used in the landing summary tiles.", "Union AMC"),
            new Col("ENTITY_TYPE", true, "List", "SUBSIDIARY / JOINT_VENTURE / OTHER", "Category. Decides the summary tile and the label on the card.", "SUBSIDIARY"),
            new Col("TAGLINE", false, "Text", "Max 300", "Line under the entity title.", "A wholly owned subsidiary of Union Bank of India"),
            new Col("YEAR_OF_INCORPORATION", false, "Number", "4 digits, 1900-2100", "KPI tile 'Year of Incorporation'.", "2010"),
            new Col("OWNERSHIP_PCT", false, "Number", "0-100", "KPI tile 'Ownership' (percentage held by the Bank).", "100"),
            new Col("OWNERSHIP_NOTE", false, "Text", "Max 200", "Small text under the ownership figure.", "Owned by Union Bank of India"),
            new Col("PAIDUP_CAPITAL_CR", false, "Number", "Rs crore", "KPI tile 'Paid-up Capital'.", "250"),
            new Col("PAIDUP_AS_ON", false, "Text", "Free text date", "'As on' date for paid-up capital.", "31 Mar 2024"),
            new Col("BRANCHES", false, "Number", "Whole number", "KPI tile 'Branches'.", "145"),
            new Col("BRANCHES_NOTE", false, "Text", "Max 100", "Small text under the branch count.", "Across India"),
            new Col("EMPLOYEES", false, "Number", "Whole number", "KPI tile 'Employees'.", "1250"),
            new Col("EMPLOYEES_AS_ON", false, "Text", "Free text date", "'As on' date for employees.", "31 Mar 2024"),
            new Col("CEO_NAME", false, "Text", "Max 200", "CEO shown on the landing card.", "Mr. Arvind Kapoor"),
            new Col("STATUS", false, "List", "Active / Inactive", "Shown on the card. 'Inactive' hides the entity from the landing page. Default Active.", "Active"),
            new Col("DESCRIPTION", false, "Text", "Long text", "Company Profile paragraph.", "UAMCL is a wholly owned subsidiary ... incorporated in 2010."),
            new Col("REGISTERED_OFFICE", false, "Text", "Max 500", "Company Profile - Registered Office.", "239, Vidhan Bhavan Marg, Nariman Point, Mumbai - 400021"),
            new Col("CIN", false, "Text", "Max 40", "Company Profile - CIN.", "U65990MH2010PLC2011166"),
            new Col("WEBSITE", false, "Text", "Max 200", "Company Profile - Website.", "www.uamcl.co.in"),
            new Col("EMAIL", false, "Text", "Max 200", "Company Profile - Email.", "info@uamcl.co.in"),
            new Col("PHONE", false, "Text", "Max 60", "Company Profile - Phone.", "+91 22 2289 3100"),
            new Col("DISPLAY_ORDER", false, "Number", "Whole number", "Position on the landing page and the left entity list (1 = first).", "1"),
        }),
        ("OfficePresence", "'Office Presence' table on the entity page. Rows are replaced for every ENTITY_CODE that appears in this sheet.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Must exist in Entity sheet or already in the portal", "Entity this row belongs to.", "UAMCL"),
            new Col("LOCATION_TYPE", true, "Text", "Max 100", "Row label.", "Regional Offices"),
            new Col("NUMBER", true, "Number", "Whole number", "Count.", "5"),
            new Col("DISPLAY_ORDER", false, "Number", "Whole number", "Row order (defaults to sheet order).", "2"),
        }),
        ("Shareholding", "'Shareholding Pattern' donut. Rows replaced per ENTITY_CODE. Percentages should add up to 100 (a warning is shown otherwise).", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Existing/uploaded entity", "Entity this row belongs to.", "UAMCL"),
            new Col("HOLDER_NAME", true, "Text", "Max 200", "Shareholder.", "Union Bank of India"),
            new Col("HOLDING_PCT", true, "Number", "0-100", "Percentage held.", "100"),
        }),
        ("Activities", "'Key Activities Undertaken' list. Rows replaced per ENTITY_CODE.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Existing/uploaded entity", "Entity this row belongs to.", "UAMCL"),
            new Col("ACTIVITY", true, "Text", "Max 300", "One activity per row, in display order.", "Mutual Fund Operations"),
        }),
        ("Board", "'Board of Directors' table. Tenure is calculated from the appointment date. Rows replaced per ENTITY_CODE.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Existing/uploaded entity", "Entity this row belongs to.", "UAMCL"),
            new Col("DIRECTOR_NAME", true, "Text", "Max 200", "Director name.", "Shri A. K. Mishra"),
            new Col("DESIGNATION", false, "Text", "Max 100", "Designation.", "Chairman"),
            new Col("DIRECTOR_CATEGORY", false, "Text", "Max 100", "Director category.", "Non-Executive (Chairman)"),
            new Col("REPRESENTING", false, "Text", "Max 200", "Representing partner / bank. Leave blank for '-'.", "Union Bank of India"),
            new Col("DATE_OF_APPOINTMENT", false, "Date", "dd-mmm-yyyy (real Excel date)", "Date of appointment; used for Tenure.", "15-Apr-2021"),
        }),
        ("KMP", "'Key Managerial Personnel' table. Rows replaced per ENTITY_CODE.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Existing/uploaded entity", "Entity this row belongs to.", "UAMCL"),
            new Col("NAME", true, "Text", "Max 200", "Person name.", "Shri Rajeev Sharma"),
            new Col("DESIGNATION", false, "Text", "Max 100", "Designation.", "Chief Executive Officer"),
            new Col("EMAIL", false, "Text", "Max 200", "Official email.", "ceo@uamcl.co.in"),
        }),
        ("Financials", "One row per entity per financial year (Rs crore). Feeds 'Key Financials', 'Financial Trend' and the growth % (vs the previous year). Rows replaced per ENTITY_CODE.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Existing/uploaded entity", "Entity this row belongs to.", "UAMCL"),
            new Col("FINANCIAL_YEAR", true, "Text", "FY yyyy-yy, e.g. FY 2023-24", "Financial year. Latest year is shown on the landing card and Key Financials.", "FY 2023-24"),
            new Col("TOTAL_INCOME", false, "Number", "Rs crore", "Total income.", "1245"),
            new Col("NET_PROFIT", false, "Number", "Rs crore, may be negative", "Net profit (negative = loss).", "312"),
            new Col("NET_WORTH", false, "Number", "Rs crore, may be negative", "Net worth.", "2350"),
            new Col("TOTAL_ASSETS", false, "Number", "Rs crore, may be negative", "Total assets.", "8765"),
        }),
        ("Quarterly", "OPTIONAL quarter-wise figures within a financial year (Rs crore). Q1 = Apr-Jun, Q2 = Jul-Sep, Q3 = Oct-Dec, Q4 = Jan-Mar. Any amount may be negative (a loss). Rows replaced per ENTITY_CODE; leave the sheet empty to keep existing quarters.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Existing/uploaded entity", "Entity this row belongs to.", "UAMCL"),
            new Col("FINANCIAL_YEAR", true, "Text", "FY yyyy-yy, e.g. FY 2023-24", "Financial year the quarter falls in.", "FY 2023-24"),
            new Col("QUARTER", true, "List", "Q1 / Q2 / Q3 / Q4", "Quarter of that financial year (Q1 = Apr-Jun ... Q4 = Jan-Mar).", "Q1"),
            new Col("TOTAL_INCOME", false, "Number", "Rs crore, may be negative", "Total income for the quarter.", "310"),
            new Col("NET_PROFIT", false, "Number", "Rs crore, may be negative", "Net profit for the quarter (negative = loss).", "-45"),
            new Col("NET_WORTH", false, "Number", "Rs crore, may be negative", "Net worth at quarter end.", "2300"),
            new Col("TOTAL_ASSETS", false, "Number", "Rs crore, may be negative", "Total assets at quarter end.", "8600"),
        }),
        ("Updates", "'Current Position / Strategic Updates' list. Rows replaced per ENTITY_CODE.", new[]
        {
            new Col("ENTITY_CODE", true, "Text", "Existing/uploaded entity", "Entity this row belongs to.", "UAMCL"),
            new Col("UPDATE_TEXT", true, "Text", "Max 1000", "One update per row, in display order.", "Strong market position in the asset management industry."),
            new Col("UPDATE_DATE", false, "Date", "dd-mmm-yyyy (real Excel date)", "Date of the update / as-on date shown next to it.", "01-Apr-2024"),
        }),
    };

    // ══ Queries ══
    public async Task<List<SjvEntity>> GetEntitiesAsync(bool activeOnly = true)
    {
        var list = new List<SjvEntity>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using (var cmd = new OracleCommand($@"
            SELECT ENTITY_ID, ENTITY_CODE, ENTITY_NAME, SHORT_NAME, ENTITY_TYPE, TAGLINE, YEAR_OF_INCORP, OWNERSHIP_PCT, OWNERSHIP_NOTE,
                   PAIDUP_CAPITAL_CR, PAIDUP_AS_ON, BRANCHES, BRANCHES_NOTE, EMPLOYEES, EMPLOYEES_AS_ON, CEO_NAME, STATUS, DESCRIPTION,
                   REGISTERED_OFFICE, CIN, WEBSITE, EMAIL, PHONE, DISPLAY_ORDER, IS_ACTIVE,
                   CASE WHEN LOGO_BLOB IS NOT NULL THEN 1 ELSE 0 END AS HAS_LOGO, ENTRY_BY, ENTRY_AT, UPDATED_BY, UPDATED_AT
            FROM RP_SJV_ENTITY {(activeOnly ? "WHERE IS_ACTIVE = 1" : "")} ORDER BY DISPLAY_ORDER, ENTITY_NAME", cn))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) list.Add(MapEntity(r));

        var fin = new Dictionary<long, List<SjvFinancial>>();
        await using (var cmd = new OracleCommand("SELECT ENTITY_ID, FIN_YEAR, TOTAL_INCOME, NET_PROFIT, NET_WORTH, TOTAL_ASSETS FROM RP_SJV_FINANCIAL ORDER BY FIN_YEAR", cn))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
            {
                var id = Convert.ToInt64(r.GetDecimal(0));
                if (!fin.TryGetValue(id, out var l)) fin[id] = l = new();
                l.Add(new SjvFinancial(r.GetString(1), Dec(r, 2), Dec(r, 3), Dec(r, 4), Dec(r, 5)));
            }
        foreach (var e in list)
            if (fin.TryGetValue(e.EntityId, out var f) && f.Count > 0)
            {
                var cur = f[^1];
                e.LatestFy = cur.FinYear; e.LatestNetProfit = cur.NetProfit; e.LatestNetWorth = cur.NetWorth;
                if (f.Count > 1) e.NetProfitGrowthPct = Growth(cur.NetProfit, f[^2].NetProfit);
            }
        return list;
    }

    /// <summary>Signed % change, (cur - prev) / |prev|. The numerator keeps both signs, so a swing from a loss of -600 to a
    /// profit of 300 is +900 (an improvement of 150%), never the -300 that comparing absolute values would give. The
    /// denominator is |prev| so the arrow points the right way when the previous figure is negative.</summary>
    public static decimal? Growth(decimal? cur, decimal? prev) =>
        cur.HasValue && prev.HasValue && prev.Value != 0 ? Math.Round((cur.Value - prev.Value) / Math.Abs(prev.Value) * 100m, 1) : null;

    /// <summary>Signed change in the amount itself: 300 - (-600) = +900.</summary>
    public static decimal? Change(decimal? cur, decimal? prev) =>
        cur.HasValue && prev.HasValue ? cur.Value - prev.Value : null;

    public async Task<SjvEntityDetail?> GetDetailAsync(string entityCode)
    {
        var all = await GetEntitiesAsync(activeOnly: false);
        var ent = all.FirstOrDefault(e => string.Equals(e.EntityCode, entityCode, StringComparison.OrdinalIgnoreCase));
        if (ent is null) return null;
        var d = new SjvEntityDetail { Entity = ent };
        await using var cn = Conn();
        await cn.OpenAsync();

        async Task Read(string sql, Action<OracleDataReader> add)
        {
            await using var c = new OracleCommand(sql, cn) { BindByName = true };
            c.Parameters.Add("p_id", ent.EntityId);
            await using var r = await c.ExecuteReaderAsync();
            while (await r.ReadAsync()) add(r);
        }
        await Read("SELECT LOCATION_TYPE, NUMBER_VALUE FROM RP_SJV_OFFICE_PRESENCE WHERE ENTITY_ID=:p_id ORDER BY DISPLAY_ORDER, ROW_ID",
            r => d.Offices.Add(new SjvOffice(r.GetString(0), Convert.ToInt32(r.GetDecimal(1)))));
        await Read("SELECT HOLDER_NAME, HOLDING_PCT FROM RP_SJV_SHAREHOLDING WHERE ENTITY_ID=:p_id ORDER BY DISPLAY_ORDER, ROW_ID",
            r => d.Shareholding.Add(new SjvHolder(r.GetString(0), r.GetDecimal(1))));
        await Read("SELECT ACTIVITY FROM RP_SJV_ACTIVITY WHERE ENTITY_ID=:p_id ORDER BY DISPLAY_ORDER, ROW_ID", r => d.Activities.Add(r.GetString(0)));
        await Read("SELECT DIRECTOR_NAME, DESIGNATION, DIRECTOR_CATEGORY, REPRESENTING, APPOINTED_ON FROM RP_SJV_BOARD WHERE ENTITY_ID=:p_id ORDER BY DISPLAY_ORDER, ROW_ID",
            r => d.Board.Add(new SjvDirector(r.GetString(0), Str(r, 1), Str(r, 2), Str(r, 3), r.IsDBNull(4) ? null : r.GetDateTime(4))));
        await Read("SELECT PERSON_NAME, DESIGNATION, EMAIL FROM RP_SJV_KMP WHERE ENTITY_ID=:p_id ORDER BY DISPLAY_ORDER, ROW_ID",
            r => d.Kmp.Add(new SjvKmp(r.GetString(0), Str(r, 1), Str(r, 2))));
        await Read("SELECT FIN_YEAR, TOTAL_INCOME, NET_PROFIT, NET_WORTH, TOTAL_ASSETS FROM RP_SJV_FINANCIAL WHERE ENTITY_ID=:p_id ORDER BY FIN_YEAR",
            r => d.Financials.Add(new SjvFinancial(r.GetString(0), Dec(r, 1), Dec(r, 2), Dec(r, 3), Dec(r, 4))));
        await Read("SELECT UPDATE_TEXT, UPDATE_DATE FROM RP_SJV_UPDATE WHERE ENTITY_ID=:p_id ORDER BY DISPLAY_ORDER, ROW_ID", r => d.Updates.Add(new SjvUpdate(r.GetString(0), r.IsDBNull(1) ? null : r.GetDateTime(1))));
        try
        {
            await Read("SELECT FIN_YEAR, QUARTER, TOTAL_INCOME, NET_PROFIT, NET_WORTH, TOTAL_ASSETS FROM RP_SJV_FINANCIAL_QTR WHERE ENTITY_ID=:p_id ORDER BY FIN_YEAR, QUARTER",
                r => d.Quarterly.Add(new SjvQuarterly(r.GetString(0), r.GetString(1), Dec(r, 2), Dec(r, 3), Dec(r, 4), Dec(r, 5))));
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            // Quarterly table not created yet (db script not run on this environment): the annual view still works.
            _log.LogWarning("RP_SJV_FINANCIAL_QTR is missing - quarterly figures are unavailable until the SJV quarterly db script is run.");
        }
        return d;
    }

    private static string? Str(OracleDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static decimal? Dec(OracleDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDecimal(i);
    private static int? Int(OracleDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetDecimal(i));
    private static DateTime? Dt(OracleDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDateTime(i);

    private static SjvEntity MapEntity(OracleDataReader r) => new()
    {
        EntityId = Convert.ToInt64(r.GetDecimal(0)), EntityCode = r.GetString(1), EntityName = r.GetString(2), ShortName = Str(r, 3),
        EntityType = r.GetString(4), Tagline = Str(r, 5), YearOfIncorp = Int(r, 6), OwnershipPct = Dec(r, 7), OwnershipNote = Str(r, 8),
        PaidupCapitalCr = Dec(r, 9), PaidupAsOn = Str(r, 10), Branches = Int(r, 11), BranchesNote = Str(r, 12), Employees = Int(r, 13),
        EmployeesAsOn = Str(r, 14), CeoName = Str(r, 15), Status = Str(r, 16),
        Description = r.IsDBNull(17) ? null : r.GetOracleClob(17).Value,
        RegisteredOffice = Str(r, 18), Cin = Str(r, 19), Website = Str(r, 20), Email = Str(r, 21), Phone = Str(r, 22),
        DisplayOrder = Convert.ToInt32(r.GetDecimal(23)), IsActive = Convert.ToInt32(r.GetDecimal(24)) == 1,
        HasLogo = Convert.ToInt32(r.GetDecimal(25)) == 1, EntryBy = Str(r, 26), EntryAt = Dt(r, 27), UpdatedBy = Str(r, 28), UpdatedAt = Dt(r, 29)
    };

    // ══ Admin access ══
    public async Task<bool> IsAdminAsync(ClaimsPrincipal user)
    {
        if ((await _authz.AuthorizeAsync(user, "AppAdmin")).Succeeded) return true;
        var emp = user.FindFirst("emplid")?.Value;
        if (string.IsNullOrWhiteSpace(emp)) return false;
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT COUNT(*) FROM RP_SJV_ADMIN WHERE UPPER(EMP_CODE)=UPPER(:p_e) AND IS_ACTIVE=1", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", emp);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<List<(string EmpCode, string AddedBy, DateTime AddedAt)>> ListAdminsAsync()
    {
        try
        {

        
        var l = new List<(string, string, DateTime)>();
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT EMP_CODE, NVL(ADDED_BY,''), ADDED_AT FROM RP_SJV_ADMIN WHERE IS_ACTIVE=1 ORDER BY EMP_CODE", cn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) l.Add((r.GetString(0), r.GetString(1), r.GetDateTime(2)));
        return l;
        }
        catch (Exception ex)
        {

            return null;
        }
    }

    public async Task AddAdminAsync(string empCode, string actor)
    {
        empCode = (empCode ?? "").Trim();
        if (empCode.Length == 0) return;
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_SJV_ADMIN t USING (SELECT :p_e AS E FROM DUAL) s ON (UPPER(t.EMP_CODE)=UPPER(s.E))
            WHEN MATCHED THEN UPDATE SET IS_ACTIVE=1, ADDED_BY=:p_by, ADDED_AT=SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (EMP_CODE, IS_ACTIVE, ADDED_BY) VALUES (s.E, 1, :p_by2)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_by2", actor);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveAdminAsync(string empCode)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("UPDATE RP_SJV_ADMIN SET IS_ACTIVE=0 WHERE UPPER(EMP_CODE)=UPPER(:p_e)", cn) { BindByName = true };
        cmd.Parameters.Add("p_e", empCode);
        await cmd.ExecuteNonQueryAsync();
    }

    // ══ Template ══
    public byte[] GenerateTemplate()
    {
        using var wb = BuildTemplateWorkbook();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private XLWorkbook BuildTemplateWorkbook()
    {
        var wb = new XLWorkbook();
        var head = XLColor.FromHtml("#1E3A8A");

        IXLWorksheet Data(string name, Col[] cols)
        {
            var ws = wb.Worksheets.Add(name);
            for (int c = 0; c < cols.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = cols[c].Name;
                cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(cols[c].Required ? head : XLColor.FromHtml("#64748B"));
                cell.CreateComment().AddText($"{(cols[c].Required ? "REQUIRED" : "Optional")} - {cols[c].Description}");
            }
            ws.SheetView.FreezeRows(1);
            ws.Columns(1, cols.Length).Width = 22;
            return ws;
        }

        // Sheet 1: Entity
        var entity = Data(Sheets[0].Sheet, Sheets[0].Cols);
        int typeCol = Array.FindIndex(Sheets[0].Cols, c => c.Name == "ENTITY_TYPE") + 1;
        int statusCol = Array.FindIndex(Sheets[0].Cols, c => c.Name == "STATUS") + 1;
        entity.Range(2, typeCol, 201, typeCol).SetDataValidation().List("\"" + string.Join(",", EntityTypes) + "\"");
        entity.Range(2, statusCol, 201, statusCol).SetDataValidation().List("\"" + string.Join(",", StatusValues) + "\"");

        // Sheet 2: Instructions
        var ins = wb.Worksheets.Add("Instructions");
        int row = 1;
        ins.Cell(row, 1).Value = "Subsidiaries and JV Corner - upload template";
        ins.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(head); row += 2;
        string[] rules =
        {
            "1. Fill the yellow-free data sheets (Entity, OfficePresence, Shareholding, Activities, Board, KMP, Financials, Quarterly [optional], Updates). Do NOT rename sheets or change header names (row 1).",
            "2. Every row is linked to an entity through ENTITY_CODE. The Entity sheet creates/updates the entity; the other sheets fill the entity page.",
            "3. Dark-blue headers are REQUIRED, grey headers are optional. Hover a header to read its description.",
            "4. Replace rule: for every ENTITY_CODE that appears in a sheet, ALL existing rows of that section for the entity are replaced by the rows in the sheet. Sections/sheets you leave empty are not touched.",
            "5. The Entity sheet only updates the entities listed in it (upsert by ENTITY_CODE). Entities not in the file are left unchanged.",
            "6. A row in a section sheet must reference an ENTITY_CODE that is in the Entity sheet or already exists in the portal.",
            "7. Amounts are in Rs crore. Financial amounts may be negative (a loss is entered as -600). Percentages are numbers (100 = 100%). Dates must be real Excel dates. Financial year format: FY 2023-24. The Quarterly sheet is optional.",
            "8. The upload is validated first and nothing is saved until you confirm on the admin page. Any error blocks the upload; warnings do not.",
        };
        foreach (var r in rules) { ins.Cell(row, 1).Value = r; ins.Range(row, 1, row, 7).Merge().Style.Alignment.WrapText = true; ins.Row(row).Height = 32; row++; }
        row++;
        foreach (var (sheet, purpose, cols) in Sheets)
        {
            ins.Cell(row, 1).Value = $"Sheet: {sheet}"; ins.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(head); row++;
            ins.Cell(row, 1).Value = purpose; ins.Range(row, 1, row, 7).Merge().Style.Alignment.WrapText = true; ins.Row(row).Height = 32; row++;
            string[] h = { "Column", "Required", "Type", "Allowed value / format", "Description", "Example" };
            for (int i = 0; i < h.Length; i++) { ins.Cell(row, i + 1).Value = h[i]; ins.Cell(row, i + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F7")); }
            row++;
            foreach (var c in cols)
            {
                ins.Cell(row, 1).Value = c.Name; ins.Cell(row, 2).Value = c.Required ? "Yes" : "No"; ins.Cell(row, 3).Value = c.Type;
                ins.Cell(row, 4).Value = c.Format; ins.Cell(row, 5).Value = c.Description; ins.Cell(row, 6).Value = c.Example;
                ins.Range(row, 1, row, 6).Style.Alignment.WrapText = true; ins.Range(row, 1, row, 6).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                row++;
            }
            row++;
        }
        ins.Column(1).Width = 26; ins.Column(2).Width = 10; ins.Column(3).Width = 10; ins.Column(4).Width = 34; ins.Column(5).Width = 60; ins.Column(6).Width = 40; ins.Column(7).Width = 4;

        // Sheets 3..: data sheets
        foreach (var (sheet, _, cols) in Sheets.Skip(1)) Data(sheet, cols);

        return wb;
    }

    // ══ Upload ══
    private sealed class Row { public int Line; public Dictionary<string, IXLCell> Cells = new(StringComparer.OrdinalIgnoreCase); }

    public async Task<SjvUploadResult> ProcessUploadAsync(Stream file, string fileName, string actor, bool apply, bool canCreateEntities)
    {
        var res = new SjvUploadResult();
        XLWorkbook wb;
        try { wb = new XLWorkbook(file); }
        catch (Exception ex) { res.Errors.Add("The file could not be read as an Excel (.xlsx) workbook: " + ex.Message); return res; }

        using (wb)
        {
            var data = new Dictionary<string, List<Row>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sheet, _, cols) in Sheets)
            {
                var rows = new List<Row>();
                data[sheet] = rows;
                if (!wb.Worksheets.TryGetWorksheet(sheet, out var ws))
                {
                    if (sheet == "Entity") res.Warnings.Add("Sheet 'Entity' not found - only child sheets for existing entities will be processed.");
                    continue;
                }
                var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                for (int c = 1; c <= lastCol; c++)
                {
                    var h = ws.Cell(1, c).GetString().Trim();
                    if (h.Length == 0) continue;
                    if (cols.All(x => !x.Name.Equals(h, StringComparison.OrdinalIgnoreCase))) res.Warnings.Add($"{sheet}: unknown column '{h}' ignored.");
                    else header[h] = c;
                }
                foreach (var col in cols.Where(c => c.Required && !header.ContainsKey(c.Name)))
                    res.Errors.Add($"{sheet}: required column '{col.Name}' is missing.");
                var last = ws.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= last; r++)
                {
                    var row = new Row { Line = r };
                    bool any = false;
                    foreach (var (h, c) in header) { var cell = ws.Cell(r, c); row.Cells[h] = cell; if (!cell.IsEmpty()) any = true; }
                    if (any) rows.Add(row);
                }
            }
            if (res.Errors.Count > 0) return res;

            var existing = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            await using (var cn0 = Conn())
            {
                await cn0.OpenAsync();
                await using var c0 = new OracleCommand("SELECT ENTITY_CODE, ENTITY_ID FROM RP_SJV_ENTITY", cn0);
                await using var r0 = await c0.ExecuteReaderAsync();
                while (await r0.ReadAsync()) existing[r0.GetString(0)] = Convert.ToInt64(r0.GetDecimal(1));
            }

            // ---- helpers ----
            string? S(Row r, string k) { if (!r.Cells.TryGetValue(k, out var c) || c.IsEmpty()) return null; var s = c.GetString().Trim(); return s.Length == 0 ? null : s; }
            string Where(string sheet, Row r) => $"{sheet} row {r.Line}";
            decimal? Num(string sheet, Row r, string k, decimal? min = null, decimal? max = null)
            {
                if (!r.Cells.TryGetValue(k, out var c) || c.IsEmpty()) return null;
                decimal v;
                if (c.DataType == XLDataType.Number) v = (decimal)c.GetDouble();
                else if (!decimal.TryParse(c.GetString().Trim().Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out v)) { res.Errors.Add($"{Where(sheet, r)}: {k} '{c.GetString()}' is not a number."); return null; }
                if ((min.HasValue && v < min) || (max.HasValue && v > max)) res.Errors.Add($"{Where(sheet, r)}: {k} {v} is out of range ({min}-{max}).");
                return v;
            }
            void Len(string sheet, Row r, string k, int max) { var s = S(r, k); if (s != null && s.Length > max) res.Errors.Add($"{Where(sheet, r)}: {k} is longer than {max} characters."); }

            // ---- Entity ----
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entityRows = new List<(Row r, string code)>();
            foreach (var r in data["Entity"])
            {
                var code = S(r, "ENTITY_CODE");
                if (code is null) { res.Errors.Add($"{Where("Entity", r)}: ENTITY_CODE is required."); continue; }
                if (!Regex.IsMatch(code, "^[A-Za-z0-9_]{1,30}$")) res.Errors.Add($"{Where("Entity", r)}: ENTITY_CODE '{code}' must be 1-30 letters/digits/underscore.");
                if (!codes.Add(code)) res.Errors.Add($"{Where("Entity", r)}: duplicate ENTITY_CODE '{code}' in this sheet.");
                if (!canCreateEntities && !existing.ContainsKey(code)) res.Errors.Add($"{Where("Entity", r)}: entity '{code}' is not defined in the Entity Master. Only a Super Admin can add new entities - ask them to add it first.");
                if (S(r, "ENTITY_NAME") is null) res.Errors.Add($"{Where("Entity", r)}: ENTITY_NAME is required.");
                var t = S(r, "ENTITY_TYPE")?.ToUpperInvariant().Replace(' ', '_');
                if (t is null || !EntityTypes.Contains(t)) res.Errors.Add($"{Where("Entity", r)}: ENTITY_TYPE must be SUBSIDIARY, JOINT_VENTURE or OTHER.");
                var st = S(r, "STATUS"); if (st != null && !StatusValues.Contains(st, StringComparer.OrdinalIgnoreCase)) res.Errors.Add($"{Where("Entity", r)}: STATUS must be Active or Inactive.");
                Num("Entity", r, "YEAR_OF_INCORPORATION", 1900, 2100); Num("Entity", r, "OWNERSHIP_PCT", 0, 100);
                Num("Entity", r, "PAIDUP_CAPITAL_CR", 0); Num("Entity", r, "BRANCHES", 0); Num("Entity", r, "EMPLOYEES", 0); Num("Entity", r, "DISPLAY_ORDER", 0, 9999);
                Len("Entity", r, "ENTITY_NAME", 300); Len("Entity", r, "SHORT_NAME", 60); Len("Entity", r, "TAGLINE", 300); Len("Entity", r, "REGISTERED_OFFICE", 500);
                entityRows.Add((r, code));
            }
            res.Entities = entityRows.Count;

            bool KnownCode(string c) => codes.Contains(c) || existing.ContainsKey(c);
            string? Code(string sheet, Row r)
            {
                var c = S(r, "ENTITY_CODE");
                if (c is null) { res.Errors.Add($"{Where(sheet, r)}: ENTITY_CODE is required."); return null; }
                if (!KnownCode(c)) { res.Errors.Add($"{Where(sheet, r)}: ENTITY_CODE '{c}' is not in the Entity sheet and does not exist in the portal."); return null; }
                return c;
            }

            var offices = new List<(string code, string type, int n, int order)>();
            int i2 = 0;
            foreach (var r in data["OfficePresence"]) { i2++; var c = Code("OfficePresence", r); var t = S(r, "LOCATION_TYPE"); var n = Num("OfficePresence", r, "NUMBER", 0);
                if (t is null) res.Errors.Add($"{Where("OfficePresence", r)}: LOCATION_TYPE is required."); if (n is null) res.Errors.Add($"{Where("OfficePresence", r)}: NUMBER is required.");
                var o = Num("OfficePresence", r, "DISPLAY_ORDER", 0); if (c != null && t != null && n != null) offices.Add((c, t, (int)n, (int)(o ?? i2))); }
            res.Offices = offices.Count;

            var holders = new List<(string code, string name, decimal pct, int order)>();
            i2 = 0;
            foreach (var r in data["Shareholding"]) { i2++; var c = Code("Shareholding", r); var n = S(r, "HOLDER_NAME"); var p = Num("Shareholding", r, "HOLDING_PCT", 0, 100);
                if (n is null) res.Errors.Add($"{Where("Shareholding", r)}: HOLDER_NAME is required."); if (p is null) res.Errors.Add($"{Where("Shareholding", r)}: HOLDING_PCT is required.");
                if (c != null && n != null && p != null) holders.Add((c, n, p.Value, i2)); }
            foreach (var g in holders.GroupBy(h => h.code, StringComparer.OrdinalIgnoreCase))
                if (Math.Abs(g.Sum(x => x.pct) - 100m) > 0.01m) res.Warnings.Add($"Shareholding: {g.Key} adds up to {g.Sum(x => x.pct)}%, not 100%.");
            res.Holders = holders.Count;

            var acts = new List<(string code, string text, int order)>();
            i2 = 0;
            foreach (var r in data["Activities"]) { i2++; var c = Code("Activities", r); var t = S(r, "ACTIVITY"); if (t is null) res.Errors.Add($"{Where("Activities", r)}: ACTIVITY is required."); Len("Activities", r, "ACTIVITY", 300); if (c != null && t != null) acts.Add((c, t, i2)); }
            res.Activities = acts.Count;

            var board = new List<(string code, string name, string? des, string? cat, string? rep, DateTime? dt, int order)>();
            i2 = 0;
            foreach (var r in data["Board"]) { i2++; var c = Code("Board", r); var n = S(r, "DIRECTOR_NAME"); if (n is null) res.Errors.Add($"{Where("Board", r)}: DIRECTOR_NAME is required.");
                DateTime? dt = null;
                if (r.Cells.TryGetValue("DATE_OF_APPOINTMENT", out var dc) && !dc.IsEmpty())
                {
                    if (dc.DataType == XLDataType.DateTime) dt = dc.GetDateTime();
                    else if (DateTime.TryParse(dc.GetString(), CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out var pd)) dt = pd;
                    else res.Errors.Add($"{Where("Board", r)}: DATE_OF_APPOINTMENT '{dc.GetString()}' is not a valid date.");
                }
                if (c != null && n != null) board.Add((c, n, S(r, "DESIGNATION"), S(r, "DIRECTOR_CATEGORY"), S(r, "REPRESENTING"), dt, i2)); }
            res.Board = board.Count;

            var kmp = new List<(string code, string name, string? des, string? email, int order)>();
            i2 = 0;
            foreach (var r in data["KMP"]) { i2++; var c = Code("KMP", r); var n = S(r, "NAME"); if (n is null) res.Errors.Add($"{Where("KMP", r)}: NAME is required.");
                var em = S(r, "EMAIL"); if (em != null && !Regex.IsMatch(em, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) res.Errors.Add($"{Where("KMP", r)}: EMAIL '{em}' is not valid.");
                if (c != null && n != null) kmp.Add((c, n, S(r, "DESIGNATION"), em, i2)); }
            res.Kmp = kmp.Count;

            var fins = new List<(string code, string fy, decimal? ti, decimal? np, decimal? nw, decimal? ta)>();
            var seenFy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in data["Financials"]) { var c = Code("Financials", r); var fy = S(r, "FINANCIAL_YEAR");
                if (fy is null) { res.Errors.Add($"{Where("Financials", r)}: FINANCIAL_YEAR is required."); continue; }
                if (!Regex.IsMatch(fy, @"^FY \d{4}-\d{2}$")) res.Errors.Add($"{Where("Financials", r)}: FINANCIAL_YEAR '{fy}' must look like 'FY 2023-24'.");
                if (c != null && !seenFy.Add(c + "|" + fy)) res.Errors.Add($"{Where("Financials", r)}: duplicate {c} / {fy}.");
                var ti = Num("Financials", r, "TOTAL_INCOME"); var np = Num("Financials", r, "NET_PROFIT"); var nw = Num("Financials", r, "NET_WORTH"); var ta = Num("Financials", r, "TOTAL_ASSETS");
                if (c != null) fins.Add((c, fy, ti, np, nw, ta)); }
            res.Financials = fins.Count;

            var quarters = new List<(string code, string fy, string q, decimal? ti, decimal? np, decimal? nw, decimal? ta)>();
            var seenQ = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in data["Quarterly"]) { var c = Code("Quarterly", r); var fy = S(r, "FINANCIAL_YEAR"); var q = S(r, "QUARTER")?.ToUpperInvariant();
                if (fy is null) { res.Errors.Add($"{Where("Quarterly", r)}: FINANCIAL_YEAR is required."); continue; }
                if (!Regex.IsMatch(fy, @"^FY \d{4}-\d{2}$")) res.Errors.Add($"{Where("Quarterly", r)}: FINANCIAL_YEAR '{fy}' must look like 'FY 2023-24'.");
                if (q is null || !SjvQuarterly.Quarters.Contains(q)) { res.Errors.Add($"{Where("Quarterly", r)}: QUARTER must be Q1, Q2, Q3 or Q4."); continue; }
                if (c != null && !seenQ.Add(c + "|" + fy + "|" + q)) res.Errors.Add($"{Where("Quarterly", r)}: duplicate {c} / {fy} / {q}.");
                var ti = Num("Quarterly", r, "TOTAL_INCOME"); var np = Num("Quarterly", r, "NET_PROFIT"); var nw = Num("Quarterly", r, "NET_WORTH"); var ta = Num("Quarterly", r, "TOTAL_ASSETS");
                if (c != null) quarters.Add((c, fy, q, ti, np, nw, ta)); }
            res.Quarterly = quarters.Count;

            var upds = new List<(string code, string text, DateTime? dt, int order)>();
            i2 = 0;
            foreach (var r in data["Updates"]) { i2++; var c = Code("Updates", r); var t = S(r, "UPDATE_TEXT"); if (t is null) res.Errors.Add($"{Where("Updates", r)}: UPDATE_TEXT is required."); Len("Updates", r, "UPDATE_TEXT", 1000);
                DateTime? ud = null;
                if (r.Cells.TryGetValue("UPDATE_DATE", out var uc) && !uc.IsEmpty())
                {
                    if (uc.DataType == XLDataType.DateTime) ud = uc.GetDateTime();
                    else if (DateTime.TryParse(uc.GetString(), CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out var upd)) ud = upd;
                    else res.Errors.Add($"{Where("Updates", r)}: UPDATE_DATE '{uc.GetString()}' is not a valid date.");
                }
                if (c != null && t != null) upds.Add((c, t, ud, i2)); }
            res.Updates = upds.Count;

            if (res.TotalRows == 0) res.Errors.Add("The file contains no data rows.");
            if (!apply || res.Errors.Count > 0) return res;

            // ---- apply, one transaction ----
            await using var cn = Conn();
            await cn.OpenAsync();
            await using var tx = cn.BeginTransaction();
            try
            {
                async Task Exec(string sql, params (string n, object? v)[] ps)
                {
                    await using var c = new OracleCommand(sql, cn) { BindByName = true, Transaction = tx };
                    foreach (var (n, v) in ps) c.Parameters.Add(n, v ?? DBNull.Value);
                    await c.ExecuteNonQueryAsync();
                }
                foreach (var (r, code) in entityRows)
                {
                    var type = S(r, "ENTITY_TYPE")!.ToUpperInvariant().Replace(' ', '_');
                    var status = S(r, "STATUS") ?? "Active";
                    var inactive = status.Equals("Inactive", StringComparison.OrdinalIgnoreCase);
                    (string, object?)[] p =
                    {
                        ("p_code", code), ("p_name", S(r, "ENTITY_NAME")), ("p_short", S(r, "SHORT_NAME")), ("p_type", type), ("p_tag", S(r, "TAGLINE")),
                        ("p_yr", Num("Entity", r, "YEAR_OF_INCORPORATION")), ("p_own", Num("Entity", r, "OWNERSHIP_PCT")), ("p_ownn", S(r, "OWNERSHIP_NOTE")),
                        ("p_paid", Num("Entity", r, "PAIDUP_CAPITAL_CR")), ("p_paidon", S(r, "PAIDUP_AS_ON")), ("p_br", Num("Entity", r, "BRANCHES")), ("p_brn", S(r, "BRANCHES_NOTE")),
                        ("p_emp", Num("Entity", r, "EMPLOYEES")), ("p_empon", S(r, "EMPLOYEES_AS_ON")), ("p_ceo", S(r, "CEO_NAME")), ("p_status", inactive ? "Inactive" : "Active"),
                        ("p_desc", S(r, "DESCRIPTION")), ("p_reg", S(r, "REGISTERED_OFFICE")), ("p_cin", S(r, "CIN")), ("p_web", S(r, "WEBSITE")), ("p_mail", S(r, "EMAIL")),
                        ("p_ph", S(r, "PHONE")), ("p_ord", Num("Entity", r, "DISPLAY_ORDER") ?? 0), ("p_act", inactive ? 0 : 1), ("p_by", actor)
                    };
                    if (existing.ContainsKey(code))
                        await Exec(@"UPDATE RP_SJV_ENTITY SET ENTITY_NAME=:p_name, SHORT_NAME=:p_short, ENTITY_TYPE=:p_type, TAGLINE=:p_tag, YEAR_OF_INCORP=:p_yr, OWNERSHIP_PCT=:p_own,
                            OWNERSHIP_NOTE=:p_ownn, PAIDUP_CAPITAL_CR=:p_paid, PAIDUP_AS_ON=:p_paidon, BRANCHES=:p_br, BRANCHES_NOTE=:p_brn, EMPLOYEES=:p_emp, EMPLOYEES_AS_ON=:p_empon,
                            CEO_NAME=:p_ceo, STATUS=:p_status, DESCRIPTION=:p_desc, REGISTERED_OFFICE=:p_reg, CIN=:p_cin, WEBSITE=:p_web, EMAIL=:p_mail, PHONE=:p_ph,
                            DISPLAY_ORDER=:p_ord, IS_ACTIVE=:p_act, UPDATED_BY=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE ENTITY_CODE=:p_code", p.Select(x => (x.Item1, x.Item2)).ToArray());
                    else
                        await Exec(@"INSERT INTO RP_SJV_ENTITY (ENTITY_ID, ENTITY_CODE, ENTITY_NAME, SHORT_NAME, ENTITY_TYPE, TAGLINE, YEAR_OF_INCORP, OWNERSHIP_PCT, OWNERSHIP_NOTE,
                            PAIDUP_CAPITAL_CR, PAIDUP_AS_ON, BRANCHES, BRANCHES_NOTE, EMPLOYEES, EMPLOYEES_AS_ON, CEO_NAME, STATUS, DESCRIPTION, REGISTERED_OFFICE, CIN, WEBSITE, EMAIL, PHONE,
                            DISPLAY_ORDER, IS_ACTIVE, ENTRY_BY, ENTRY_AT, UPDATED_BY)
                            VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_code, :p_name, :p_short, :p_type, :p_tag, :p_yr, :p_own, :p_ownn, :p_paid, :p_paidon, :p_br, :p_brn, :p_emp, :p_empon,
                            :p_ceo, :p_status, :p_desc, :p_reg, :p_cin, :p_web, :p_mail, :p_ph, :p_ord, :p_act, :p_by, SYSTIMESTAMP, :p_by)", p.Select(x => (x.Item1, x.Item2)).ToArray());
                }

                // entity ids after upsert
                var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                await using (var c = new OracleCommand("SELECT ENTITY_CODE, ENTITY_ID FROM RP_SJV_ENTITY", cn) { Transaction = tx })
                await using (var rd = await c.ExecuteReaderAsync())
                    while (await rd.ReadAsync()) ids[rd.GetString(0)] = Convert.ToInt64(rd.GetDecimal(1));

                async Task Replace<T>(string table, IEnumerable<T> rows, Func<T, string> codeOf, Func<T, (string cols, string binds, (string, object?)[] ps)> ins)
                {
                    var list = rows.ToList();
                    foreach (var cd in list.Select(codeOf).Distinct(StringComparer.OrdinalIgnoreCase))
                        await Exec($"DELETE FROM {table} WHERE ENTITY_ID=:p_id", ("p_id", ids[cd]));
                    foreach (var row in list)
                    {
                        var (cols, binds, ps) = ins(row);
                        var all = new List<(string, object?)> { ("p_id", ids[codeOf(row)]) }; all.AddRange(ps);
                        await Exec($"INSERT INTO {table} (ROW_ID, ENTITY_ID, {cols}) VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_id, {binds})", all.ToArray());
                    }
                }
                await Replace("RP_SJV_OFFICE_PRESENCE", offices, x => x.code, x => ("LOCATION_TYPE, NUMBER_VALUE, DISPLAY_ORDER", ":a,:b,:c", new (string, object?)[] { ("a", x.type), ("b", x.n), ("c", x.order) }));
                await Replace("RP_SJV_SHAREHOLDING", holders, x => x.code, x => ("HOLDER_NAME, HOLDING_PCT, DISPLAY_ORDER", ":a,:b,:c", new (string, object?)[] { ("a", x.name), ("b", x.pct), ("c", x.order) }));
                await Replace("RP_SJV_ACTIVITY", acts, x => x.code, x => ("ACTIVITY, DISPLAY_ORDER", ":a,:b", new (string, object?)[] { ("a", x.text), ("b", x.order) }));
                await Replace("RP_SJV_BOARD", board, x => x.code, x => ("DIRECTOR_NAME, DESIGNATION, DIRECTOR_CATEGORY, REPRESENTING, APPOINTED_ON, DISPLAY_ORDER", ":a,:b,:c,:d,:e,:f",
                    new (string, object?)[] { ("a", x.name), ("b", x.des), ("c", x.cat), ("d", x.rep), ("e", x.dt), ("f", x.order) }));
                await Replace("RP_SJV_KMP", kmp, x => x.code, x => ("PERSON_NAME, DESIGNATION, EMAIL, DISPLAY_ORDER", ":a,:b,:c,:d", new (string, object?)[] { ("a", x.name), ("b", x.des), ("c", x.email), ("d", x.order) }));
                await Replace("RP_SJV_FINANCIAL", fins, x => x.code, x => ("FIN_YEAR, TOTAL_INCOME, NET_PROFIT, NET_WORTH, TOTAL_ASSETS", ":a,:b,:c,:d,:e", new (string, object?)[] { ("a", x.fy), ("b", x.ti), ("c", x.np), ("d", x.nw), ("e", x.ta) }));
                if (quarters.Count > 0)
                    await Replace("RP_SJV_FINANCIAL_QTR", quarters, x => x.code, x => ("FIN_YEAR, QUARTER, TOTAL_INCOME, NET_PROFIT, NET_WORTH, TOTAL_ASSETS", ":a,:b,:c,:d,:e,:f",
                        new (string, object?)[] { ("a", x.fy), ("b", x.q), ("c", x.ti), ("d", x.np), ("e", x.nw), ("f", x.ta) }));
                await Replace("RP_SJV_UPDATE", upds, x => x.code, x => ("UPDATE_TEXT, UPDATE_DATE, DISPLAY_ORDER", ":a,:b,:c", new (string, object?)[] { ("a", x.text), ("b", x.dt), ("c", x.order) }));

                await Exec(@"INSERT INTO RP_SJV_UPLOAD_LOG (LOG_ID, UPLOADED_BY, FILE_NAME, ENTITIES_UPSERTED, ROWS_WRITTEN, SUMMARY) VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_by, :p_f, :p_e, :p_r, :p_s)",
                    ("p_by", actor), ("p_f", fileName), ("p_e", res.Entities), ("p_r", res.TotalRows),
                    ("p_s", $"Entities {res.Entities}, Office {res.Offices}, Holders {res.Holders}, Activities {res.Activities}, Board {res.Board}, KMP {res.Kmp}, Financials {res.Financials}, Quarterly {res.Quarterly}, Updates {res.Updates}"));
                tx.Commit();
                res.Applied = true;
                _log.LogInformation("SJV upload applied by {Actor}: {Rows} rows, {Ent} entities", actor, res.TotalRows, res.Entities);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _log.LogError(ex, "SJV upload failed");
                res.Errors.Add("Saving failed and was rolled back: " + ex.Message);
            }
        }
        return res;
    }

    // ══ Entity master (Super Admin) ══
    public async Task<long> SaveEntityMasterAsync(SjvEntity e, string actor)
    {
        e.EntityCode = (e.EntityCode ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(e.EntityCode, "^[A-Z0-9_]{1,30}$")) throw new InvalidOperationException("Entity code must be 1-30 letters/digits/underscore.");
        if (string.IsNullOrWhiteSpace(e.EntityName)) throw new InvalidOperationException("Entity name is required.");
        if (!EntityTypes.Contains(e.EntityType)) throw new InvalidOperationException("Invalid entity type.");
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            MERGE INTO RP_SJV_ENTITY t USING (SELECT :p_code AS C FROM DUAL) s ON (t.ENTITY_CODE = s.C)
            WHEN MATCHED THEN UPDATE SET ENTITY_NAME=:p_name, SHORT_NAME=:p_short, ENTITY_TYPE=:p_type, DISPLAY_ORDER=:p_ord, IS_ACTIVE=:p_act,
                 STATUS=CASE WHEN :p_act2 = 1 THEN 'Active' ELSE 'Inactive' END, UPDATED_BY=:p_by, UPDATED_AT=SYSTIMESTAMP
            WHEN NOT MATCHED THEN INSERT (ENTITY_ID, ENTITY_CODE, ENTITY_NAME, SHORT_NAME, ENTITY_TYPE, DISPLAY_ORDER, IS_ACTIVE, STATUS, ENTRY_BY, ENTRY_AT, UPDATED_BY)
                 VALUES (RP_SEQ_GLOBAL.NEXTVAL, s.C, :p_name2, :p_short2, :p_type2, :p_ord2, :p_act3, CASE WHEN :p_act4 = 1 THEN 'Active' ELSE 'Inactive' END, :p_by2, SYSTIMESTAMP, :p_by2)", cn) { BindByName = true };
        cmd.Parameters.Add("p_code", e.EntityCode);
        cmd.Parameters.Add("p_name", e.EntityName.Trim()); cmd.Parameters.Add("p_short", (object?)e.ShortName ?? DBNull.Value); cmd.Parameters.Add("p_type", e.EntityType);
        cmd.Parameters.Add("p_ord", e.DisplayOrder); cmd.Parameters.Add("p_act", e.IsActive ? 1 : 0); cmd.Parameters.Add("p_act2", e.IsActive ? 1 : 0); cmd.Parameters.Add("p_by", actor);
        cmd.Parameters.Add("p_name2", e.EntityName.Trim()); cmd.Parameters.Add("p_short2", (object?)e.ShortName ?? DBNull.Value); cmd.Parameters.Add("p_type2", e.EntityType);
        cmd.Parameters.Add("p_ord2", e.DisplayOrder); cmd.Parameters.Add("p_act3", e.IsActive ? 1 : 0); cmd.Parameters.Add("p_act4", e.IsActive ? 1 : 0); cmd.Parameters.Add("p_by2", actor);
        await cmd.ExecuteNonQueryAsync();
        await using var q = new OracleCommand("SELECT ENTITY_ID FROM RP_SJV_ENTITY WHERE ENTITY_CODE=:p_c", cn) { BindByName = true };
        q.Parameters.Add("p_c", e.EntityCode);
        return Convert.ToInt64(await q.ExecuteScalarAsync());
    }

    // ══ Delete an entity (Super Admin) — permanent, cascades to every child table (logo goes with the row) ══
    public async Task DeleteEntityAsync(string entityCode)
    {
        entityCode = (entityCode ?? "").Trim();
        if (entityCode.Length == 0) throw new InvalidOperationException("Entity code is required.");
        await using var cn = Conn();
        await cn.OpenAsync();

        long entityId;
        await using (var q = new OracleCommand("SELECT ENTITY_ID FROM RP_SJV_ENTITY WHERE ENTITY_CODE=:p_c", cn) { BindByName = true })
        {
            q.Parameters.Add("p_c", entityCode);
            var v = await q.ExecuteScalarAsync();
            if (v is null) throw new InvalidOperationException("Entity not found.");
            entityId = Convert.ToInt64(v);
        }

        await using var tx = cn.BeginTransaction();
        try
        {
            async Task Exec(string sql)
            {
                await using var c = new OracleCommand(sql, cn) { BindByName = true, Transaction = tx };
                c.Parameters.Add("p_id", entityId);
                await c.ExecuteNonQueryAsync();
            }
            foreach (var t in new[] { "RP_SJV_OFFICE_PRESENCE", "RP_SJV_SHAREHOLDING", "RP_SJV_ACTIVITY", "RP_SJV_BOARD", "RP_SJV_KMP", "RP_SJV_FINANCIAL", "RP_SJV_UPDATE" })
                await Exec($"DELETE FROM {t} WHERE ENTITY_ID=:p_id");
            await Exec("DELETE FROM RP_SJV_ENTITY WHERE ENTITY_ID=:p_id");   // takes LOGO_BLOB with it
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
        _log.LogInformation("SJV entity {Code} deleted", entityCode);
    }

    // ══ Logo (Super Admin) — stored as a BLOB on the entity row; small image shown on the landing card,
    //    the entity page header and the PDF header/footer, streamed on demand by /sjv/logo/{code} ══
    public async Task SaveLogoAsync(string entityCode, byte[] bytes, string contentType, string originalFileName, string actor)
    {
        entityCode = (entityCode ?? "").Trim();
        if (bytes is null || bytes.Length == 0) throw new InvalidOperationException("Logo file is empty.");
        var ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
        if (!LogoContentTypes.ContainsKey(ext)) throw new InvalidOperationException("Logo must be PNG, JPG, SVG or WEBP.");
        var safeName = new string((originalFileName ?? "logo").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
        if (safeName.Length == 0) safeName = "logo" + ext;

        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(
            "UPDATE RP_SJV_ENTITY SET LOGO_BLOB=:p_blob, LOGO_CONTENT_TYPE=:p_ct, LOGO_FILE_NAME=:p_fn, UPDATED_BY=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE ENTITY_CODE=:p_c",
            cn) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("p_blob", OracleDbType.Blob) { Value = bytes });
        cmd.Parameters.Add("p_ct", LogoContentTypes[ext]); cmd.Parameters.Add("p_fn", safeName); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_c", entityCode);
        var n = await cmd.ExecuteNonQueryAsync();
        if (n == 0) throw new InvalidOperationException("Entity not found.");
        _log.LogInformation("SJV logo saved for {Code} by {Actor} ({Bytes} bytes)", entityCode, actor, bytes.Length);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetLogoAsync(string entityCode)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand("SELECT LOGO_BLOB, LOGO_CONTENT_TYPE FROM RP_SJV_ENTITY WHERE ENTITY_CODE=:p_c", cn) { BindByName = true };
        cmd.Parameters.Add("p_c", (entityCode ?? "").Trim());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync() || r.IsDBNull(0)) return null;
        using var blob = r.GetOracleBlob(0);
        var bytes = new byte[blob.Length];
        if (bytes.Length > 0) blob.Read(bytes, 0, bytes.Length);
        return (bytes, r.IsDBNull(1) ? "application/octet-stream" : r.GetString(1));
    }

    // ══ Profile / KPI fields (JV Admin) ══
    public async Task SaveOverviewAsync(SjvEntity e, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var cmd = new OracleCommand(@"
            UPDATE RP_SJV_ENTITY SET TAGLINE=:p_tag, YEAR_OF_INCORP=:p_yr, OWNERSHIP_PCT=:p_own, OWNERSHIP_NOTE=:p_ownn, PAIDUP_CAPITAL_CR=:p_paid, PAIDUP_AS_ON=:p_paidon,
                BRANCHES=:p_br, BRANCHES_NOTE=:p_brn, EMPLOYEES=:p_emp, EMPLOYEES_AS_ON=:p_empon, CEO_NAME=:p_ceo, DESCRIPTION=:p_desc, REGISTERED_OFFICE=:p_reg,
                CIN=:p_cin, WEBSITE=:p_web, EMAIL=:p_mail, PHONE=:p_ph, UPDATED_BY=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE ENTITY_ID=:p_id", cn) { BindByName = true };
        object V(object? o) => o ?? DBNull.Value;
        cmd.Parameters.Add("p_tag", V(e.Tagline)); cmd.Parameters.Add("p_yr", V(e.YearOfIncorp)); cmd.Parameters.Add("p_own", V(e.OwnershipPct)); cmd.Parameters.Add("p_ownn", V(e.OwnershipNote));
        cmd.Parameters.Add("p_paid", V(e.PaidupCapitalCr)); cmd.Parameters.Add("p_paidon", V(e.PaidupAsOn)); cmd.Parameters.Add("p_br", V(e.Branches)); cmd.Parameters.Add("p_brn", V(e.BranchesNote));
        cmd.Parameters.Add("p_emp", V(e.Employees)); cmd.Parameters.Add("p_empon", V(e.EmployeesAsOn)); cmd.Parameters.Add("p_ceo", V(e.CeoName));
        cmd.Parameters.Add(new OracleParameter("p_desc", OracleDbType.Clob) { Value = V(e.Description) });
        cmd.Parameters.Add("p_reg", V(e.RegisteredOffice)); cmd.Parameters.Add("p_cin", V(e.Cin)); cmd.Parameters.Add("p_web", V(e.Website)); cmd.Parameters.Add("p_mail", V(e.Email));
        cmd.Parameters.Add("p_ph", V(e.Phone)); cmd.Parameters.Add("p_by", actor); cmd.Parameters.Add("p_id", e.EntityId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ══ One section (replace all rows of the section for the entity) ══
    public async Task SaveSectionAsync(long entityId, string section, SjvEntityDetail d, string actor)
    {
        await using var cn = Conn();
        await cn.OpenAsync();
        await using var tx = cn.BeginTransaction();
        try
        {
            async Task Exec(string sql, params (string n, object? v)[] ps)
            {
                await using var c = new OracleCommand(sql, cn) { BindByName = true, Transaction = tx };
                foreach (var (n, v) in ps) c.Parameters.Add(n, v ?? DBNull.Value);
                await c.ExecuteNonQueryAsync();
            }
            async Task Replace<T>(string table, string cols, string binds, IEnumerable<T> rows, Func<T, int, (string, object?)[]> map)
            {
                await Exec($"DELETE FROM {table} WHERE ENTITY_ID=:p_id", ("p_id", entityId));
                int i = 0;
                foreach (var r in rows)
                {
                    i++;
                    var ps = new List<(string, object?)> { ("p_id", entityId) }; ps.AddRange(map(r, i));
                    await Exec($"INSERT INTO {table} (ROW_ID, ENTITY_ID, {cols}) VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_id, {binds})", ps.ToArray());
                }
            }
            switch (section)
            {
                case "offices":
                    if (d.Offices.Any(x => string.IsNullOrWhiteSpace(x.LocationType))) throw new InvalidOperationException("Every office row needs a location type.");
                    await Replace("RP_SJV_OFFICE_PRESENCE", "LOCATION_TYPE, NUMBER_VALUE, DISPLAY_ORDER", ":a,:b,:c", d.Offices, (x, i) => new (string, object?)[] { ("a", x.LocationType.Trim()), ("b", x.Number), ("c", i) }); break;
                case "shareholding":
                    if (d.Shareholding.Any(x => string.IsNullOrWhiteSpace(x.HolderName) || x.Pct < 0 || x.Pct > 100)) throw new InvalidOperationException("Every shareholder needs a name and a percentage between 0 and 100.");
                    await Replace("RP_SJV_SHAREHOLDING", "HOLDER_NAME, HOLDING_PCT, DISPLAY_ORDER", ":a,:b,:c", d.Shareholding, (x, i) => new (string, object?)[] { ("a", x.HolderName.Trim()), ("b", x.Pct), ("c", i) }); break;
                case "activities":
                    await Replace("RP_SJV_ACTIVITY", "ACTIVITY, DISPLAY_ORDER", ":a,:b", d.Activities.Where(a => !string.IsNullOrWhiteSpace(a)), (x, i) => new (string, object?)[] { ("a", x.Trim()), ("b", i) }); break;
                case "board":
                    if (d.Board.Any(x => string.IsNullOrWhiteSpace(x.Name))) throw new InvalidOperationException("Every director needs a name.");
                    await Replace("RP_SJV_BOARD", "DIRECTOR_NAME, DESIGNATION, DIRECTOR_CATEGORY, REPRESENTING, APPOINTED_ON, DISPLAY_ORDER", ":a,:b,:c,:d,:e,:f", d.Board,
                        (x, i) => new (string, object?)[] { ("a", x.Name.Trim()), ("b", x.Designation), ("c", x.Category), ("d", x.Representing), ("e", x.AppointedOn), ("f", i) }); break;
                case "kmp":
                    if (d.Kmp.Any(x => string.IsNullOrWhiteSpace(x.Name))) throw new InvalidOperationException("Every KMP row needs a name.");
                    await Replace("RP_SJV_KMP", "PERSON_NAME, DESIGNATION, EMAIL, DISPLAY_ORDER", ":a,:b,:c,:d", d.Kmp, (x, i) => new (string, object?)[] { ("a", x.Name.Trim()), ("b", x.Designation), ("c", x.Email), ("d", i) }); break;
                case "financials":
                    if (d.Financials.Any(x => !Regex.IsMatch(x.FinYear ?? "", @"^FY \d{4}-\d{2}$"))) throw new InvalidOperationException("Financial year must look like 'FY 2023-24'.");
                    if (d.Financials.GroupBy(x => x.FinYear).Any(g => g.Count() > 1)) throw new InvalidOperationException("A financial year is entered twice.");
                    await Replace("RP_SJV_FINANCIAL", "FIN_YEAR, TOTAL_INCOME, NET_PROFIT, NET_WORTH, TOTAL_ASSETS", ":a,:b,:c,:d,:e", d.Financials,
                        (x, i) => new (string, object?)[] { ("a", x.FinYear), ("b", x.TotalIncome), ("c", x.NetProfit), ("d", x.NetWorth), ("e", x.TotalAssets) }); break;
                case "quarterly":
                    if (d.Quarterly.Any(x => !Regex.IsMatch(x.FinYear ?? "", @"^FY \d{4}-\d{2}$"))) throw new InvalidOperationException("Financial year must look like 'FY 2023-24'.");
                    if (d.Quarterly.Any(x => !SjvQuarterly.Quarters.Contains(x.Quarter))) throw new InvalidOperationException("Quarter must be Q1, Q2, Q3 or Q4.");
                    if (d.Quarterly.GroupBy(x => (x.FinYear, x.Quarter)).Any(g => g.Count() > 1)) throw new InvalidOperationException("A quarter is entered twice.");
                    await Replace("RP_SJV_FINANCIAL_QTR", "FIN_YEAR, QUARTER, TOTAL_INCOME, NET_PROFIT, NET_WORTH, TOTAL_ASSETS", ":a,:b,:c,:d,:e,:f", d.Quarterly,
                        (x, i) => new (string, object?)[] { ("a", x.FinYear), ("b", x.Quarter), ("c", x.TotalIncome), ("d", x.NetProfit), ("e", x.NetWorth), ("f", x.TotalAssets) }); break;
                case "updates":
                    await Replace("RP_SJV_UPDATE", "UPDATE_TEXT, UPDATE_DATE, DISPLAY_ORDER", ":a,:b,:c", d.Updates.Where(u => !string.IsNullOrWhiteSpace(u.Text)),
                        (x, i) => new (string, object?)[] { ("a", x.Text.Trim()), ("b", x.Date), ("c", i) }); break;
                default: throw new InvalidOperationException("Unknown section " + section);
            }
            await Exec("UPDATE RP_SJV_ENTITY SET UPDATED_BY=:p_by, UPDATED_AT=SYSTIMESTAMP WHERE ENTITY_ID=:p_id", ("p_by", actor), ("p_id", entityId));
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    // ══ Export current data in the upload-template layout (for offline edit / re-upload) ══
    public async Task<byte[]> ExportAsync(string? entityCode)
    {
        var entities = (await GetEntitiesAsync(activeOnly: false)).Where(e => entityCode is null || e.EntityCode.Equals(entityCode, StringComparison.OrdinalIgnoreCase)).ToList();
        using var wb = BuildTemplateWorkbook();
        var next = new Dictionary<string, int>();
        void Put(string sheet, params object?[] vals)
        {
            var ws = wb.Worksheet(sheet);
            if (!next.ContainsKey(sheet)) next[sheet] = 2;
            var r = next[sheet]++;
            for (int c = 0; c < vals.Length; c++)
            {
                var v = vals[c]; if (v is null) continue;
                var cell = ws.Cell(r, c + 1);
                switch (v)
                {
                    case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd-mmm-yyyy"; break;
                    case decimal m: cell.Value = m; break;
                    case int i: cell.Value = i; break;
                    default: cell.Value = v.ToString(); break;
                }
            }
        }
        foreach (var e in entities)
        {
            Put("Entity", e.EntityCode, e.EntityName, e.ShortName, e.EntityType, e.Tagline, e.YearOfIncorp, e.OwnershipPct, e.OwnershipNote, e.PaidupCapitalCr, e.PaidupAsOn,
                e.Branches, e.BranchesNote, e.Employees, e.EmployeesAsOn, e.CeoName, e.IsActive ? "Active" : "Inactive", e.Description, e.RegisteredOffice, e.Cin, e.Website, e.Email, e.Phone, e.DisplayOrder);
            var d = await GetDetailAsync(e.EntityCode);
            if (d is null) continue;
            int i = 0;
            foreach (var o in d.Offices) Put("OfficePresence", e.EntityCode, o.LocationType, o.Number, ++i);
            foreach (var h in d.Shareholding) Put("Shareholding", e.EntityCode, h.HolderName, h.Pct);
            foreach (var a in d.Activities) Put("Activities", e.EntityCode, a);
            foreach (var b in d.Board) Put("Board", e.EntityCode, b.Name, b.Designation, b.Category, b.Representing, b.AppointedOn);
            foreach (var k in d.Kmp) Put("KMP", e.EntityCode, k.Name, k.Designation, k.Email);
            foreach (var f in d.Financials) Put("Financials", e.EntityCode, f.FinYear, f.TotalIncome, f.NetProfit, f.NetWorth, f.TotalAssets);
            foreach (var q in d.Quarterly) Put("Quarterly", e.EntityCode, q.FinYear, q.Quarter, q.TotalIncome, q.NetProfit, q.NetWorth, q.TotalAssets);
            foreach (var u in d.Updates) Put("Updates", e.EntityCode, u.Text, u.Date);
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
