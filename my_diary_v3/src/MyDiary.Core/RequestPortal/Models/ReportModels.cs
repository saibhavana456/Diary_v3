namespace RequestPortal.Core.Models;

public enum ReportSourceType
{
    FilePath,
    Sftp,
    OracleDb,
    SqlServerDb,
    /// <summary>Hive/Impala reached through an Apache Knox gateway, via the Cloudera
    /// Impala ODBC driver over HTTP transport (basic-auth). See ReportExecutionService.RunImpalaAsync.</summary>
    Impala
}

public enum ReportSite { Dc, Dr }

/// <summary>A saved report definition authored by a Super Admin in Report Developer.
/// Every connection field is kept as a DC/DR pair; <see cref="RunningFrom"/> tells the
/// execution engine which site is currently live for this report.</summary>
public sealed class ReportDef
{
    public long Id { get; set; }
    /// <summary>Stable, admin-entered short identifier (e.g. "RPT-KYC-01"), unique across all
    /// reports. Used for cross-referencing in audit logs, URLs, or external callers — does not
    /// change even if ReportName or the underlying query is edited. Distinct from Id (an internal
    /// surrogate key) and ReportName (a free-text display label, not guaranteed unique).</summary>
    public string ReportCode { get; set; } = "";
    public string ReportName { get; set; } = "";
    public long VerticalId { get; set; }
    public string? VerticalName { get; set; }
    public long DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public ReportSourceType SourceType { get; set; }
    public ReportSite RunningFrom { get; set; } = ReportSite.Dc;

    /// <summary>For OracleDb/SqlServerDb/Impala only — the exact appsettings ConnectionStrings
    /// key the admin picked from a dropdown listing every available connection name (e.g.
    /// "ReportsOracleDc"). Resolved at execution time via IConfiguration.GetConnectionString.
    /// Not used for FilePath/Sftp, which keep their own DC/DR field pairs below.</summary>
    public string? ConnectionName { get; set; }

    // FILE_PATH / SFTP — shared
    public string? FileType { get; set; }      // XLSX | CSV

    // DC (primary)
    public string? FilePath { get; set; }
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUser { get; set; }
    public string? SftpPassword { get; set; }
    public string? DbConnectionString { get; set; }

    // DR (secondary) — mirrors every DC connection field
    public string? FilePathDr { get; set; }
    public string? SftpHostDr { get; set; }
    public int? SftpPortDr { get; set; }
    public string? SftpUserDr { get; set; }
    public string? SftpPasswordDr { get; set; }
    public string? DbConnectionStringDr { get; set; }

    // Shared across DC/DR — same logical report, same query/shape.
    public string? SqlQuery { get; set; }

    public bool IsActive { get; set; } = true;
    public string CreatedByEmp { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    /// <summary>Effective connection values for whichever site is currently RunningFrom.</summary>
    public (string? FilePath, string? SftpHost, int? SftpPort, string? SftpUser, string? SftpPassword, string? DbConnectionString) EffectiveSource()
        => RunningFrom == ReportSite.Dr
            ? (FilePathDr, SftpHostDr, SftpPortDr, SftpUserDr, SftpPasswordDr, DbConnectionStringDr)
            : (FilePath, SftpHost, SftpPort, SftpUser, SftpPassword, DbConnectionString);
}

public enum ReportFilterType { Zone, Region, Branch, Custom }

/// <summary>One admin-defined filter available on a report's viewing page. Zone/Region/Branch
/// are the standard scope filters; Custom lets the admin expose any other query-result column
/// as a filter by typing its column name. ColumnName must match a column the report's query
/// actually returns/can filter on — validated as a plain identifier (letters/digits/underscore)
/// before being interpolated into a WHERE clause, since it can't be parameterized like a value.</summary>
public sealed class ReportFilterDef
{
    public long Id { get; set; }
    public long ReportId { get; set; }
    public ReportFilterType FilterType { get; set; }
    public string ColumnName { get; set; } = "";
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Form payload used by Report Developer create/edit.</summary>
public sealed class ReportDefForm
{
    public long? Id { get; set; }
    public string ReportCode { get; set; } = "";
    public string ReportName { get; set; } = "";
    public long? VerticalId { get; set; }
    public long? DepartmentId { get; set; }
    public ReportSourceType SourceType { get; set; } = ReportSourceType.OracleDb;
    public ReportSite RunningFrom { get; set; } = ReportSite.Dc;
    public string? ConnectionName { get; set; }

    public string? FileType { get; set; } = "XLSX";

    public string? FilePath { get; set; }
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; } = 22;
    public string? SftpUser { get; set; }
    public string? SftpPassword { get; set; }
    public string? DbConnectionString { get; set; }

    public string? FilePathDr { get; set; }
    public string? SftpHostDr { get; set; }
    public int? SftpPortDr { get; set; } = 22;
    public string? SftpUserDr { get; set; }
    public string? SftpPasswordDr { get; set; }
    public string? DbConnectionStringDr { get; set; }

    public string? SqlQuery { get; set; }
}

/// <summary>Generic tabular result — columns auto-detected from the source, capped at 200 rows.</summary>
public sealed class ReportResult
{
    public List<string> Columns { get; set; } = new();
    public List<object?[]> Rows { get; set; } = new();
    public bool Truncated { get; set; }        // true when the source had more than the 200-row cap
    public bool Disabled { get; set; }         // true when the report definition is inactive
    public string? Error { get; set; }
}

/// <summary>An entry in the report definition audit trail (create/update/delete/enable/disable).</summary>
public sealed class ReportAuditEntry
{
    public long Id { get; set; }
    public long ReportId { get; set; }
    public string ReportName { get; set; } = "";
    public string Action { get; set; } = "";   // CREATE | UPDATE | DELETE | ENABLE | DISABLE
    public string ActorEmp { get; set; } = "";
    public DateTime ActorAt { get; set; }
    public string? Details { get; set; }
}

/// <summary>An entry in the report access log (every load attempt, successful or not).</summary>
public sealed class ReportAccessEntry
{
    public long Id { get; set; }
    public long ReportId { get; set; }
    public string ReportName { get; set; } = "";
    public string AccessedBy { get; set; } = "";
    public DateTime AccessedAt { get; set; }
    public ReportSite? RunningFrom { get; set; }
    public int? RowsReturned { get; set; }
    public bool Success { get; set; }
    public string? ErrorMsg { get; set; }
}
