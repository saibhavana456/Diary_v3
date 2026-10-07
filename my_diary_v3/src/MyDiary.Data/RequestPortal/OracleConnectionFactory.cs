using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace RequestPortal.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> OpenAsync(CancellationToken ct = default);
}

/// <summary>
/// Connection factory for the RP_Owner database (Request Portal tables).
/// </summary>
public sealed class OracleConnectionFactory : IDbConnectionFactory
{
    private readonly string _cs;

    public OracleConnectionFactory(IConfiguration cfg)
    {
        _cs = cfg.GetConnectionString("RP_Owner")
            ?? throw new InvalidOperationException("ConnectionStrings:RP_Owner is not configured.");
    }

    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new OracleConnection(_cs);
        // Oracle.ManagedDataAccess.Core does not implement true async I/O —
        // OpenAsync() runs synchronously and deadlocks against the Blazor
        // SynchronizationContext unless we escape it with ConfigureAwait(false).
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return conn;
    }
}

/// <summary>
/// Connection factory for the Organisations database (staff_details, VW_STAFF_USER_SUMMARY, etc.).
/// Uses the "Organisations" connection string from appsettings.json.
/// </summary>
public interface IOrganisationsDbFactory
{
    Task<OracleConnection> OpenAsync(CancellationToken ct = default);
}

public sealed class OrganisationsConnectionFactory : IOrganisationsDbFactory
{
    private readonly string _cs;

    public OrganisationsConnectionFactory(IConfiguration cfg)
    {
        _cs = cfg.GetConnectionString("Organisations")
            ?? throw new InvalidOperationException("ConnectionStrings:Organisations is not configured.");
    }

    public async Task<OracleConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new OracleConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return conn;
    }
}
