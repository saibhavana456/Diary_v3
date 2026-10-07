using System.Data;
using Oracle.ManagedDataAccess.Client;
using MyDiary.Web.Features.Shared.Models;
using OfficeOpenXml;

namespace MyDiary.Web.Features.Shared.Services;

public interface IComplianceSummaryService
{
    //Task<(List<Dictionary<string, object>> Rows, string retvalue)>GetAsync(ComplianceFilters f, CancellationToken ct = default);
    Task<(List<Dictionary<string, object>> Rows, string retvalue)> GetSummaryAsync(ComplianceFilters f, string flag, CancellationToken ct = default);

    Task<(List<string> DeptNames, List<string> LetterTypes, List<string> AddressedTos)> GetFiltersAsync(ComplianceFilters f, CancellationToken ct = default);
    //Task<List<ComplianceStatusDetail>> GetStatusDetailAsync(ComplianceFilters f, string authority, string status, CancellationToken ct = default);

    Task<List<ComplianceStatusDetail>> GetStatusDetailAsync(ComplianceFilters f, string value, string status, bool isDepartmentWise, CancellationToken ct = default);

    byte[] ExportToExcel(List<Dictionary<string, object>> data, List<ColumnConfig> columns, string sheetName);

    Task<ExecutiveSummary> GetExecutiveSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);

    Task<(List<Dictionary<string, object>> Rows, string retvalue)> GetDVCSummaryAsync(ComplianceFilters f, string flag, CancellationToken ct = default);

    Task<(List<string> DeptNames, List<string> LetterTypes, List<string> AddressedTos)> GetDVCFiltersAsync(ComplianceFilters f, CancellationToken ct = default);

    Task<List<ComplianceStatusDetail>> GetDVCStatusDetailAsync(ComplianceFilters f, string value, string status, bool isDepartmentWise, CancellationToken ct = default);

    Task<DVCExecutiveSummary> GetDVCExecutiveSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public class ComplianceSummaryService(IConfiguration config) : IComplianceSummaryService
{
    private readonly string _conn = config.GetConnectionString("UCPDBConnection")
        ?? throw new InvalidOperationException("Missing Oracle connection string.");
    private List<Dictionary<string, object>> summaryData = new();

    private const int FISCAL_START_MONTH = 4;

    private static (DateTime from, DateTime to) FyWindow(int fyStartYear)
    {
        var from = new DateTime(fyStartYear, FISCAL_START_MONTH, 1);
        var to = from.AddYears(1).AddDays(-1); // up to Mar 31 next year for Apr start
        return (from, to);
    }

    public async Task<(List<Dictionary<string, object>> Rows, string retvalue)> GetSummaryAsync(ComplianceFilters f, string flag, CancellationToken ct = default)
    {
        DateTime from;
        DateTime to;

        // Use custom date range if provided, otherwise FY window
        if (f.StartDate.HasValue && f.EndDate.HasValue)
        {
            from = f.StartDate.Value.Date;
            to = f.EndDate.Value.Date;
        }
        else
        {
            (from, to) = FyWindow(f.FyStartYear);
        }

        var rows = new List<Dictionary<string, object>>();

        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandType = CommandType.StoredProcedure;
        //cmd.CommandText = "SP_GetSummaryDataByReceivedDate1";

        //cmd.CommandText = "SP_GetSummaryDataByReceivedDateNew";
        cmd.CommandText = "SP_GetSummaryDataByReceivedDateNewPendency";

        // ----- Input parameters -----
        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);

        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);

        cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, f.DeptName ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_letter_type", OracleDbType.Varchar2, f.LetterType ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_addressed_to", OracleDbType.Varchar2, f.AddressedTo ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_dash_status", OracleDbType.Varchar2, f.DashStatus ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_timeliness", OracleDbType.Varchar2, f.Timeliness ?? "All", ParameterDirection.Input);

        // IMPORTANT:
        // "D" = Department-wise
        // Anything else = Authority-wise
        cmd.Parameters.Add("p_flag", OracleDbType.Varchar2, flag, ParameterDirection.Input);

        cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, f.Authority ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_pending_bucket", OracleDbType.Varchar2, f.PendingBucket ?? "All", ParameterDirection.Input);

        // ----- Output cursor -----
        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

        // ----- Read result -----
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }

            rows.Add(row);
        }

        return (rows, "Success");
    }

    public async Task<(List<Dictionary<string, object>> Rows, string retvalue)> GetDVCSummaryAsync(ComplianceFilters f, string flag, CancellationToken ct = default)
    {
        DateTime from;
        DateTime to;

        // Use custom date range if provided, otherwise FY window
        if (f.StartDate.HasValue && f.EndDate.HasValue)
        {
            from = f.StartDate.Value.Date;
            to = f.EndDate.Value.Date;
        }
        else
        {
            (from, to) = FyWindow(f.FyStartYear);
        }

        var rows = new List<Dictionary<string, object>>();

        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandType = CommandType.StoredProcedure;
        //cmd.CommandText = "SP_GetSummaryDataByReceivedDate1";

        //cmd.CommandText = "SP_GetSummaryDataByReceivedDateNew";
        cmd.CommandText = "SP_DVC_GetSummaryDataByReceivedDateNewPendency";

        // ----- Input parameters -----
        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);

        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);

        cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, f.DeptName ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_letter_type", OracleDbType.Varchar2, f.LetterType ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_addressed_to", OracleDbType.Varchar2, f.AddressedTo ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_dash_status", OracleDbType.Varchar2, f.DashStatus ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_timeliness", OracleDbType.Varchar2, f.Timeliness ?? "All", ParameterDirection.Input);

        // IMPORTANT:
        // "D" = Department-wise
        // Anything else = Authority-wise
        cmd.Parameters.Add("p_flag", OracleDbType.Varchar2, flag, ParameterDirection.Input);

        cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, f.Authority ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_pending_bucket", OracleDbType.Varchar2, f.PendingBucket ?? "All", ParameterDirection.Input);

        // ----- Output cursor -----
        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

        // ----- Read result -----
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }

            rows.Add(row);
        }

        return (rows, "Success");
    }


    //public async Task<(List<Dictionary<string, object>> Rows, string retvalue)> GetAsync(ComplianceFilters f, CancellationToken ct = default)
    //{
    //    //var (from, to) = FyWindow(f.FyStartYear);

    //    DateTime from;
    //    DateTime to;

    //    // ? Use custom date range if provided
    //    if (f.StartDate.HasValue && f.EndDate.HasValue)
    //    {
    //        from = f.StartDate.Value.Date;
    //        to = f.EndDate.Value.Date;
    //    }
    //    else
    //    {
    //        (from, to) = FyWindow(f.FyStartYear);
    //    }

    //    var rows = new List<Dictionary<string, object>>();
    //    var deptNames = new List<string>();
    //    var letterTypes = new List<string>();
    //    var addressedTos = new List<string>();
    //    await using var conn = new OracleConnection(_conn);
    //    await conn.OpenAsync(ct);

    //    // 1) Main summary
    //    await using (var cmd = conn.CreateCommand())
    //    {
    //        cmd.BindByName = true;
    //        cmd.CommandType = CommandType.StoredProcedure;
    //        cmd.CommandText = "SP_GetSummaryDataByReceivedDate";

    //        cmd.Parameters.Add("p_from",         OracleDbType.Date, from, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_to",           OracleDbType.Date, to,   ParameterDirection.Input);
    //        cmd.Parameters.Add("p_dept_name",    OracleDbType.Varchar2, (object?)f.DeptName ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_letter_type",  OracleDbType.Varchar2, (object?)f.LetterType ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_addressed_to", OracleDbType.Varchar2, (object?)f.AddressedTo ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_dash_status",  OracleDbType.Varchar2, (object?)f.DashStatus ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_timeliness",   OracleDbType.Varchar2, (object?)f.Timeliness ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

    //        await using var r = await cmd.ExecuteReaderAsync(ct);
    //        while (await r.ReadAsync(ct))
    //        {
    //            var row = new Dictionary<string, object>();
    //            for (int i = 0; i < r.FieldCount; i++) row.Add(r.GetName(i), r.GetValue(i));
    //            rows.Add(row);
    //        }
    //    }
    //    return (rows,"Success");
    //}


    public async Task<(List<string> DeptNames, List<string> LetterTypes, List<string> AddressedTos)> GetFiltersAsync(ComplianceFilters f, CancellationToken ct = default)
    {
        var (from, to) = FyWindow(f.FyStartYear);
        var rows = new List<ComplianceSummaryRow>();
        var deptNames = new List<string>();
        var letterTypes = new List<string>();
        var addressedTos = new List<string>();
        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        // 2) Distinct filter values within the FY window (so dropdowns remain relevant)
        await using (var cmd2 = conn.CreateCommand())
        {
            cmd2.BindByName = true;
            cmd2.CommandType = CommandType.StoredProcedure;
            cmd2.CommandText = "SP_GetDeptByReceivedDate";
            cmd2.Parameters.Add("pf", OracleDbType.Date, from, ParameterDirection.Input);
            cmd2.Parameters.Add("pt", OracleDbType.Date, to, ParameterDirection.Input);
            cmd2.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

            await using var r2 = await cmd2.ExecuteReaderAsync(ct);
            while (await r2.ReadAsync(ct))
            {
                if (!r2.IsDBNull(0)) deptNames.Add(r2.GetString(0));
                if (!r2.IsDBNull(1)) letterTypes.Add(r2.GetString(1));
                if (!r2.IsDBNull(2)) addressedTos.Add(r2.GetString(2));
            }
        }

        return (deptNames.Distinct().OrderBy(x => x).ToList(),
            letterTypes.Distinct().OrderBy(x => x).ToList(),
            addressedTos.Distinct().OrderBy(x => x).ToList());
    }

    public async Task<(List<string> DeptNames, List<string> LetterTypes, List<string> AddressedTos)> GetDVCFiltersAsync(ComplianceFilters f, CancellationToken ct = default)
    {
        var (from, to) = FyWindow(f.FyStartYear);
        var rows = new List<ComplianceSummaryRow>();
        var deptNames = new List<string>();
        var letterTypes = new List<string>();
        var addressedTos = new List<string>();
        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        // 2) Distinct filter values within the FY window (so dropdowns remain relevant)
        await using (var cmd2 = conn.CreateCommand())
        {
            cmd2.BindByName = true;
            cmd2.CommandType = CommandType.StoredProcedure;
            //cmd2.CommandText = "SP_GetDeptByReceivedDate";
            cmd2.CommandText = "SP_DVC_GetDeptByReceivedDate";
            cmd2.Parameters.Add("pf", OracleDbType.Date, from, ParameterDirection.Input);
            cmd2.Parameters.Add("pt", OracleDbType.Date, to, ParameterDirection.Input);
            cmd2.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

            await using var r2 = await cmd2.ExecuteReaderAsync(ct);
            while (await r2.ReadAsync(ct))
            {
                if (!r2.IsDBNull(0)) deptNames.Add(r2.GetString(0));
                if (!r2.IsDBNull(1)) letterTypes.Add(r2.GetString(1));
                if (!r2.IsDBNull(2)) addressedTos.Add(r2.GetString(2));
            }
        }

        return (deptNames.Distinct().OrderBy(x => x).ToList(),
            letterTypes.Distinct().OrderBy(x => x).ToList(),
            addressedTos.Distinct().OrderBy(x => x).ToList());
    }

    //public async Task<List<ComplianceStatusDetail>> GetStatusDetailAsync(
    //ComplianceFilters f,
    //string authority,
    //string status,
    //CancellationToken ct = default)
    //{
    //    DateTime from;
    //    DateTime to;

    //    if (f.StartDate.HasValue && f.EndDate.HasValue)
    //    {
    //        from = f.StartDate.Value.Date;
    //        to = f.EndDate.Value.Date;
    //    }
    //    else
    //    {
    //        (from, to) = FyWindow(f.FyStartYear);
    //    }

    //    // ?? Split "Submitted - Intime" into parts
    //    // Examples coming from UI:
    //    // "Submitted"
    //    // "Submitted - Intime"
    //    // "Pending - Delayed"
    //    var dashStatus = "All";
    //    var timeliness = "All";

    //    if (!string.IsNullOrWhiteSpace(status))
    //    {
    //        var parts = status.Split('-', StringSplitOptions.TrimEntries);
    //        if (parts.Length > 0)
    //            dashStatus = parts[0];

    //        if (parts.Length > 1)
    //            timeliness = parts[1];
    //    }

    //    var result = new List<ComplianceStatusDetail>();

    //    try
    //    {
    //        await using var conn = new OracleConnection(_conn);
    //        await conn.OpenAsync(ct);

    //        await using var cmd = conn.CreateCommand();
    //        cmd.BindByName = true;
    //        cmd.CommandType = CommandType.StoredProcedure;
    //        cmd.CommandText = "SP_GetDataByReceivedDate_Dashboard";

    //        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, (object?)f.DeptName ?? "All", ParameterDirection.Input);
    //        cmd.Parameters.Add("p_letter_type", OracleDbType.Varchar2, (object?)f.LetterType ?? "All", ParameterDirection.Input);
    //        cmd.Parameters.Add("p_addressed_to", OracleDbType.Varchar2, (object?)f.AddressedTo ?? "All", ParameterDirection.Input);
    //        cmd.Parameters.Add("p_dash_status", OracleDbType.Varchar2, dashStatus, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_timeliness", OracleDbType.Varchar2, timeliness, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, authority, ParameterDirection.Input);
    //        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

    //        await using var r = await cmd.ExecuteReaderAsync(ct);

    //        int sno = 1;
    //        while (await r.ReadAsync(ct))
    //        {
    //            result.Add(new ComplianceStatusDetail
    //            {
    //                SNo = sno++,
    //                LetterRefNo = r["SRL_NO"]?.ToString() ?? "",
    //                Subject = r["STATUS_DESC"]?.ToString() ?? "",
    //                DeptName = r["DEPT_NAME"]?.ToString() ?? "",
    //                LetterType = r["LETTER_TYPE"]?.ToString() ?? "",
    //                AddressedTo = r["ADDRESSED_TO"]?.ToString() ?? "",
    //                ReceivedDate = r["RECEIVED_DATE"] != DBNull.Value
    //                    ? Convert.ToDateTime(r["RECEIVED_DATE"]).ToString("dd/MM/yyyy")
    //                    : "",
    //                DueDate = r["TARGET_DATE"] != DBNull.Value
    //                    ? Convert.ToDateTime(r["TARGET_DATE"]).ToString("dd/MM/yyyy")
    //                    : "",
    //                Status = r["DASH_STATUS"]?.ToString() ?? "",
    //                Timeliness = r["TIMELINESS"]?.ToString() ?? ""
    //            });
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        AppLogger.LogError(ex, $"Error fetching status detail: {ex.Message}");
    //        // ? No mock data recommended in prod
    //        // Let UI show "No detail records found"
    //    }

    //    return result;
    //}

    public async Task<List<ComplianceStatusDetail>> GetStatusDetailAsync(ComplianceFilters f, string value, string status, bool isDepartmentWise, CancellationToken ct = default)
    {
        DateTime from;
        DateTime to;

        if (f.StartDate.HasValue && f.EndDate.HasValue)
        {
            from = f.StartDate.Value.Date;
            to = f.EndDate.Value.Date;
        }
        else
        {
            (from, to) = FyWindow(f.FyStartYear);
        }

        var dashStatus = "All";
        var timeliness = "All";

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parts = status.Split('-', StringSplitOptions.TrimEntries);
            dashStatus = parts[0];
            if (parts.Length > 1)
                timeliness = parts[1];
        }

        var result = new List<ComplianceStatusDetail>();

        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "SP_GetDataByReceivedDate_Modal";

        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);
        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);

        cmd.Parameters.Add("p_letter_type", OracleDbType.Varchar2, f.LetterType ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_addressed_to", OracleDbType.Varchar2, f.AddressedTo ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_dash_status", OracleDbType.Varchar2, dashStatus, ParameterDirection.Input);

        cmd.Parameters.Add("p_timeliness", OracleDbType.Varchar2, timeliness, ParameterDirection.Input);

        // ? THIS IS THE CORE CHANGE
        if (isDepartmentWise)
        {
            // Department?wise popup
            cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, value, ParameterDirection.Input);

            cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, "All", ParameterDirection.Input);
        }
        else
        {
            // Authority?wise popup
            cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, f.DeptName ?? "All", ParameterDirection.Input);

            cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, value, ParameterDirection.Input);
        }

        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        int sno = 1;

        while (await r.ReadAsync(ct))
        {
            //result.Add(new ComplianceStatusDetail
            //{
            //    SNo = sno++,
            //    LetterRefNo = r["SRL_NO"]?.ToString() ?? "",
            //    Subject = r["STATUS_DESC"]?.ToString() ?? "",
            //    DeptName = r["DEPT_NAME"]?.ToString() ?? "",
            //    LetterType = r["LETTER_TYPE"]?.ToString() ?? "",
            //    AddressedTo = r["ADDRESSED_TO"]?.ToString() ?? "",
            //    ReceivedDate = r["RECEIVED_DATE"] != DBNull.Value
            //        ? Convert.ToDateTime(r["RECEIVED_DATE"]).ToString("dd/MM/yyyy")
            //        : "",
            //    DueDate = r["TARGET_DATE"] != DBNull.Value
            //        ? Convert.ToDateTime(r["TARGET_DATE"]).ToString("dd/MM/yyyy")
            //        : "",
            //    Status = r["DASH_STATUS"]?.ToString() ?? "",
            //    Timeliness = r["TIMELINESS"]?.ToString() ?? ""
            //});


            result.Add(new ComplianceStatusDetail
            {
                SNo = sno++,

                LetterRefNo = r["SRL_NO"]?.ToString() ?? "",
                Authority = r["AUTHORITY"]?.ToString() ?? "",
                Subject = r["SUBJECT"]?.ToString() ?? "",
                DeptName = r["DEPT_NAME"]?.ToString() ?? "",
                DueDate = r["TARGET_DATE"] != DBNull.Value ? Convert.ToDateTime(r["TARGET_DATE"]).ToString("dd/MM/yyyy") : "",
                LetterDate = r["LETTER_DATE"] != DBNull.Value ? Convert.ToDateTime(r["LETTER_DATE"]).ToString("dd/MM/yyyy") : "",
                ReceivedDate = r["RECEIVED_DATE"] != DBNull.Value ? Convert.ToDateTime(r["RECEIVED_DATE"]).ToString("dd/MM/yyyy") : "",
                ActionTaken = r["ACTION_TAKEN"]?.ToString() ?? "",
                ActionDate = r["ACTION_DATE"] != DBNull.Value ? Convert.ToDateTime(r["ACTION_DATE"]).ToString("dd/MM/yyyy") : "",
                Status = r["DASH_STATUS"]?.ToString() ?? "", Timeliness = r["TIMELINESS"]?.ToString() ?? ""
            });
        }

        return result;
    }

    public async Task<List<ComplianceStatusDetail>> GetDVCStatusDetailAsync(ComplianceFilters f, string value, string status, bool isDepartmentWise, CancellationToken ct = default)
    {
        DateTime from;
        DateTime to;

        if (f.StartDate.HasValue && f.EndDate.HasValue)
        {
            from = f.StartDate.Value.Date;
            to = f.EndDate.Value.Date;
        }
        else
        {
            (from, to) = FyWindow(f.FyStartYear);
        }

        var dashStatus = "All";
        var timeliness = "All";

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parts = status.Split('-', StringSplitOptions.TrimEntries);
            dashStatus = parts[0];
            if (parts.Length > 1)
                timeliness = parts[1];
        }

        var result = new List<ComplianceStatusDetail>();

        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandType = CommandType.StoredProcedure;
        //cmd.CommandText = "SP_GetDataByReceivedDate_Modal";
        cmd.CommandText = "SP_DVC_GetDataByReceivedDate_Modal";

        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);
        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);

        cmd.Parameters.Add("p_letter_type", OracleDbType.Varchar2, f.LetterType ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_addressed_to", OracleDbType.Varchar2, f.AddressedTo ?? "All", ParameterDirection.Input);

        cmd.Parameters.Add("p_dash_status", OracleDbType.Varchar2, dashStatus, ParameterDirection.Input);

        cmd.Parameters.Add("p_timeliness", OracleDbType.Varchar2, timeliness, ParameterDirection.Input);

        // ? THIS IS THE CORE CHANGE
        if (isDepartmentWise)
        {
            // Department?wise popup
            cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, value, ParameterDirection.Input);

            cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, "All", ParameterDirection.Input);
        }
        else
        {
            // Authority?wise popup
            cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, f.DeptName ?? "All", ParameterDirection.Input);

            cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, value, ParameterDirection.Input);
        }

        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        int sno = 1;

        while (await r.ReadAsync(ct))
        {
            //result.Add(new ComplianceStatusDetail
            //{
            //    SNo = sno++,
            //    LetterRefNo = r["SRL_NO"]?.ToString() ?? "",
            //    Subject = r["STATUS_DESC"]?.ToString() ?? "",
            //    DeptName = r["DEPT_NAME"]?.ToString() ?? "",
            //    LetterType = r["LETTER_TYPE"]?.ToString() ?? "",
            //    AddressedTo = r["ADDRESSED_TO"]?.ToString() ?? "",
            //    ReceivedDate = r["RECEIVED_DATE"] != DBNull.Value
            //        ? Convert.ToDateTime(r["RECEIVED_DATE"]).ToString("dd/MM/yyyy")
            //        : "",
            //    DueDate = r["TARGET_DATE"] != DBNull.Value
            //        ? Convert.ToDateTime(r["TARGET_DATE"]).ToString("dd/MM/yyyy")
            //        : "",
            //    Status = r["DASH_STATUS"]?.ToString() ?? "",
            //    Timeliness = r["TIMELINESS"]?.ToString() ?? ""
            //});


            result.Add(new ComplianceStatusDetail
            {
                SNo = sno++,

                LetterRefNo = r["SRL_NO"]?.ToString() ?? "",
                Authority = r["AUTHORITY"]?.ToString() ?? "",
                Subject = r["SUBJECT"]?.ToString() ?? "",
                DeptName = r["DEPT_NAME"]?.ToString() ?? "",
                DueDate = r["TARGET_DATE"] != DBNull.Value ? Convert.ToDateTime(r["TARGET_DATE"]).ToString("dd/MM/yyyy") : "",
                LetterDate = r["LETTER_DATE"] != DBNull.Value ? Convert.ToDateTime(r["LETTER_DATE"]).ToString("dd/MM/yyyy") : "",
                ReceivedDate = r["RECEIVED_DATE"] != DBNull.Value ? Convert.ToDateTime(r["RECEIVED_DATE"]).ToString("dd/MM/yyyy") : "",
                ActionTaken = r["ACTION_TAKEN"]?.ToString() ?? "",
                ActionDate = r["ACTION_DATE"] != DBNull.Value ? Convert.ToDateTime(r["ACTION_DATE"]).ToString("dd/MM/yyyy") : "",
                Status = r["DASH_STATUS"]?.ToString() ?? "",
                Timeliness = r["TIMELINESS"]?.ToString() ?? ""
            });
        }

        return result;
    }


    //public async Task<List<ComplianceStatusDetail>> GetStatusDetailAsync(ComplianceFilters f, string authority, string status, CancellationToken ct = default)
    //{
    //    DateTime from;
    //    DateTime to;

    //    if (f.StartDate.HasValue && f.EndDate.HasValue)
    //    {
    //        from = f.StartDate.Value.Date;
    //        to = f.EndDate.Value.Date;
    //    }
    //    else
    //    {
    //        (from, to) = FyWindow(f.FyStartYear);
    //    }

    //    var result = new List<ComplianceStatusDetail>();

    //    try
    //    {
    //        await using var conn = new OracleConnection(_conn);
    //        await conn.OpenAsync(ct);

    //        await using var cmd = conn.CreateCommand();
    //        cmd.BindByName = true;
    //        cmd.CommandType = CommandType.StoredProcedure;
    //        cmd.CommandText = "SP_GetDataByReceivedDate_Dashboard";

    //        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_dept_name", OracleDbType.Varchar2, (object?)f.DeptName ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_letter_type", OracleDbType.Varchar2, (object?)f.LetterType ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_addressed_to", OracleDbType.Varchar2, (object?)f.AddressedTo ?? DBNull.Value, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_authority", OracleDbType.Varchar2, authority, ParameterDirection.Input);
    //        cmd.Parameters.Add("p_status", OracleDbType.Varchar2, status, ParameterDirection.Input);
    //        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

    //        await using var r = await cmd.ExecuteReaderAsync(ct);
    //        int sno = 1;
    //        while (await r.ReadAsync(ct))
    //        {
    //            result.Add(new ComplianceStatusDetail
    //            {
    //                SNo = sno++,
    //                LetterRefNo = r["LETTER_REF_NO"]?.ToString() ?? "",
    //                Subject = r["SUBJECT"]?.ToString() ?? "",
    //                DeptName = r["DEPT_NAME"]?.ToString() ?? "",
    //                LetterType = r["LETTER_TYPE"]?.ToString() ?? "",
    //                AddressedTo = r["ADDRESSED_TO"]?.ToString() ?? "",
    //                ReceivedDate = r["RECEIVED_DATE"] != DBNull.Value ? Convert.ToDateTime(r["RECEIVED_DATE"]).ToString("dd/MM/yyyy") : "",
    //                DueDate = r["DUE_DATE"] != DBNull.Value ? Convert.ToDateTime(r["DUE_DATE"]).ToString("dd/MM/yyyy") : "",
    //                Status = r["STATUS"]?.ToString() ?? "",
    //                Timeliness = r["TIMELINESS"]?.ToString() ?? ""
    //            });
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        AppLogger.LogError(ex, $"Error fetching status detail: {ex.Message}");
    //        // Mock data fallback
    //        result = Enumerable.Range(1, 5).Select(i => new ComplianceStatusDetail
    //        {
    //            SNo = i,
    //            LetterRefNo = $"REF/{authority}/{i:D3}",
    //            Subject = $"Sample compliance item {i} - {status}",
    //            DeptName = "Compliance Dept",
    //            LetterType = "Circular",
    //            AddressedTo = authority,
    //            ReceivedDate = DateTime.Today.AddDays(-i * 10).ToString("dd/MM/yyyy"),
    //            DueDate = DateTime.Today.AddDays(i * 5).ToString("dd/MM/yyyy"),
    //            Status = status,
    //            Timeliness = i % 2 == 0 ? "Intime" : "Delayed"
    //        }).ToList();
    //    }

    //    return result;
    //}

    public byte[] ExportToExcel(List<Dictionary<string, object>> data, List<ColumnConfig> columns, string sheetName)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add(sheetName);
        for (int i = 0; i < columns.Count; i++)
        {
            ws.Cells[1, i + 1].Value = columns[i].Header;
            ws.Cells[1, i + 1].Style.Font.Bold = true;
        }
        for (int r = 0; r < data.Count; r++)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                var key = columns[c].PropertyName;
                ws.Cells[r + 2, c + 1].Value = data[r].ContainsKey(key) ? data[r][key] : "";
            }
        }
        ws.Cells.AutoFitColumns();
        return package.GetAsByteArray();
    }

    public async Task<ExecutiveSummary> GetExecutiveSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var result = new ExecutiveSummary();

        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "SP_GetExecutiveSummary";

        // ----- Input parameters -----
        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);

        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);

        // ----- Output cursor -----
        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            result.TotalRequests =
                reader["TOTAL_REQUESTS"] != DBNull.Value
                    ? Convert.ToInt32(reader["TOTAL_REQUESTS"])
                    : 0;

            result.PendingRequests =
                reader["PENDING_REQUESTS"] != DBNull.Value
                    ? Convert.ToInt32(reader["PENDING_REQUESTS"])
                    : 0;

            result.CompletedRequests =
                reader["COMPLETED_REQUESTS"] != DBNull.Value
                    ? Convert.ToInt32(reader["COMPLETED_REQUESTS"])
                    : 0;

            result.TatCompliancePercent =
                reader["TAT_COMPLIANCE_PCNT"] != DBNull.Value
                    ? Convert.ToDecimal(reader["TAT_COMPLIANCE_PCNT"])
                    : 0m;

            result.DelayedRequests =
                reader["DELAYED_REQUESTS"] != DBNull.Value
                    ? Convert.ToInt32(reader["DELAYED_REQUESTS"])
                    : 0;
        }

        return result;
    }

    public async Task<DVCExecutiveSummary> GetDVCExecutiveSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var result = new DVCExecutiveSummary();

        await using var conn = new OracleConnection(_conn);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandType = CommandType.StoredProcedure;
        //cmd.CommandText = "SP_GetExecutiveSummary";
        cmd.CommandText = "SP_DVC_GetExecutiveSummary";

        // ----- Input parameters -----
        cmd.Parameters.Add("p_from", OracleDbType.Date, from, ParameterDirection.Input);

        cmd.Parameters.Add("p_to", OracleDbType.Date, to, ParameterDirection.Input);

        // ----- Output cursor -----
        cmd.Parameters.Add("C1", OracleDbType.RefCursor, ParameterDirection.Output);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            result.TotalRequestReceived =
                reader["TOTAL_REQUEST_RECEIVED"] != DBNull.Value
                    ? Convert.ToInt32(reader["TOTAL_REQUEST_RECEIVED"])
                    : 0;

            result.SubmittedWithinTimeline =
                reader["SUBMITTED_WITHIN_TIMELINE"] != DBNull.Value
                    ? Convert.ToInt32(reader["SUBMITTED_WITHIN_TIMELINE"])
                    : 0;

            result.SubmittedBeyondTimeline =
                reader["SUBMITTED_BEYOND_TIMELINE"] != DBNull.Value
                    ? Convert.ToInt32(reader["SUBMITTED_BEYOND_TIMELINE"])
                    : 0;

            result.PendingWithinTimeline =
                reader["PENDING_WITHIN_TIMELINE"] != DBNull.Value
                    ? Convert.ToInt32(reader["PENDING_WITHIN_TIMELINE"])
                    : 0;

            result.PendingBeyondTimeline =
                reader["PENDING_BEYOND_TIMELINE"] != DBNull.Value
                    ? Convert.ToInt32(reader["PENDING_BEYOND_TIMELINE"])
                    : 0;

            result.TatPercentage =
                reader["TAT_PERCENTAGE"] != DBNull.Value
                    ? Convert.ToDecimal(reader["TAT_PERCENTAGE"])
                    : 0m;
        }
        return result;
    }

}