using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Features.ECircular.Services;

/// <summary>
/// Oracle-backed audit service for Circular/Policy document access logging.
/// Writes are fire-and-forget to avoid blocking the user experience.
/// </summary>
public sealed class CircularAuditService : ICircularAuditService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CircularAuditService> _logger;

    public CircularAuditService(IConfiguration configuration, ILogger<CircularAuditService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("MyDiaryDBConnection")
        ?? throw new InvalidOperationException("MyDiaryDBConnection is not configured.");

    public async Task LogDocumentAccessAsync(CircularAuditEntry entry, CancellationToken ct = default)
    {
        await InsertAuditLogAsync(entry, ct);
    }

    public async Task LogSearchAsync(CircularAuditEntry entry, CancellationToken ct = default)
    {
        await InsertAuditLogAsync(entry, ct);
    }

    private async Task InsertAuditLogAsync(CircularAuditEntry entry, CancellationToken ct)
    {
        try
        {
            await using var connection = new OracleConnection(ConnectionString);
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO E_CIRCULAR_AUDIT_LOG (
                    EMPLOYEE_PF_NO, DOCUMENT_ID, CIRCULAR_NO, ACTION_TYPE,
                    MODULE, IP_ADDRESS, WATERMARK_REF, SEARCH_CRITERIA,
                    CIRCULAR_TYPE, DEPARTMENT, SUBJECT,
                    SUCCESS, ERROR_MESSAGE, USER_AGENT
                ) VALUES (
                    :pfNo, :docId, :circNo, :actionType,
                    :module, :ip, :watermarkRef, :searchCriteria,
                    :circType, :dept, :subject,
                    :success, :errorMsg, :userAgent
                )";

            cmd.Parameters.Add(new OracleParameter("pfNo", entry.EmployeePfNo ?? ""));
            cmd.Parameters.Add(new OracleParameter("docId", (object?)entry.DocumentId ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("circNo", (object?)entry.CircularNo ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("actionType", entry.ActionType));
            cmd.Parameters.Add(new OracleParameter("module", entry.Module));
            cmd.Parameters.Add(new OracleParameter("ip", (object?)entry.IpAddress ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("watermarkRef", (object?)entry.WatermarkRef ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("searchCriteria", (object?)entry.SearchCriteria ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("circType", (object?)entry.CircularType ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("dept", (object?)entry.Department ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("subject", (object?)entry.Subject ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("success", entry.Success ? 1 : 0));
            cmd.Parameters.Add(new OracleParameter("errorMsg", (object?)entry.ErrorMessage ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("userAgent", (object?)entry.UserAgent ?? DBNull.Value));

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit logging should never break the main flow
            _logger.LogError(ex, "[CircularAudit] Failed to write audit log for PF:{PfNo}, Action:{Action}",
                entry.EmployeePfNo, entry.ActionType);
        }
    }
}
