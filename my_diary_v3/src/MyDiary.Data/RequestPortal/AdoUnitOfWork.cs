using System.Data;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;

namespace RequestPortal.Data;

/// <summary>
/// Pure ADO.NET unit of work — one OracleConnection per Blazor scope,
/// optional shared OracleTransaction across repos.
/// </summary>
public sealed class AdoUnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly IDbConnectionFactory _factory;
    private OracleConnection? _conn;
    private OracleTransaction? _tx;

    public AdoUnitOfWork(IDbConnectionFactory factory) => _factory = factory;

    // Typed accessors used by repos
    public OracleConnection  OracleConn => _conn ?? throw new InvalidOperationException("Call EnsureConnectionAsync first.");
    public OracleTransaction? OracleTx  => _tx;

    public async Task<IDbConnection> EnsureConnectionAsync(CancellationToken ct = default)
    {
        if (_conn is null)
            _conn = (OracleConnection)await _factory.OpenAsync(ct).ConfigureAwait(false);
        return _conn;
    }

    public async Task BeginAsync(CancellationToken ct = default)
    {
        await EnsureConnectionAsync(ct).ConfigureAwait(false);
        _tx = _conn!.BeginTransaction(IsolationLevel.ReadCommitted);
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        _tx?.Commit();
        _tx?.Dispose();
        _tx = null;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        _tx?.Rollback();
        _tx?.Dispose();
        _tx = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_tx is not null) { try { _tx.Rollback(); } catch { } _tx.Dispose(); }
        if (_conn is not null) { _conn.Close(); _conn.Dispose(); }
        await Task.CompletedTask;
    }
}
