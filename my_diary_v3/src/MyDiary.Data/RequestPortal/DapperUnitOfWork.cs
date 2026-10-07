using System.Data;
using RequestPortal.Core.Abstractions;

namespace RequestPortal.Data;

/// <summary>
/// Thin scoped wrapper that opens a single connection per scope and lets
/// services share an optional transaction. Repos pull <see cref="Connection"/>
/// (and <see cref="Transaction"/> when set) from here.
/// </summary>
public sealed class DapperUnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _factory;
    private IDbConnection? _conn;
    private IDbTransaction? _tx;

    public DapperUnitOfWork(IDbConnectionFactory factory) => _factory = factory;

    public IDbConnection Connection => _conn ?? throw new InvalidOperationException("Connection not opened. Call EnsureConnectionAsync first.");
    public IDbTransaction? Transaction => _tx;

    public async Task<IDbConnection> EnsureConnectionAsync(CancellationToken ct = default)
    {
        if (_conn is null) _conn = await _factory.OpenAsync(ct).ConfigureAwait(false);
        return _conn;
    }

    public async Task BeginAsync(CancellationToken ct = default)
    {
        await EnsureConnectionAsync(ct);
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
        if (_tx is not null) { _tx.Rollback(); _tx.Dispose(); }
        if (_conn is not null) { _conn.Close(); _conn.Dispose(); }
        await Task.CompletedTask;
    }
}
