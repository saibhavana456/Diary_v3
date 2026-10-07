using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace RequestPortal.Data;

/// <summary>
/// Thin ADO.NET helpers used by all repos.
/// All reader helpers use column-name lookup so order in SELECT does not matter.
/// </summary>
internal static class OracleHelper
{
    // ── Command factory ───────────────────────────────────────────────────────

    internal static OracleCommand Cmd(this OracleConnection conn, string sql, OracleTransaction? tx = null)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        // Bind parameters by name (:p_x) rather than by position, so a placeholder
        // referenced multiple times in one statement is satisfied by a single
        // parameter (Oracle defaults to positional binding → ORA-01008 otherwise).
        cmd.BindByName = true;
        return cmd;
    }

    // ── Parameter helpers ─────────────────────────────────────────────────────

    internal static OracleParameter AddOut(this OracleParameterCollection p, string name, OracleDbType type)
    {
        var param = p.Add(name, type);
        param.Direction = ParameterDirection.Output;
        return param;
    }

    internal static void AddIn(this OracleParameterCollection p, string name, object? value)
        => p.Add(name, value ?? DBNull.Value);

    internal static void AddIn(this OracleParameterCollection p, string name, OracleDbType type, object? value)
    {
        var param = p.Add(name, type);
        param.Value = value ?? DBNull.Value;
    }

    // Read the generated ID from a RETURNING INTO output param
    internal static long OutId(this OracleParameter param)
        => Convert.ToInt64(((OracleDecimal)param.Value).ToInt64());

    // ── Reader value helpers ──────────────────────────────────────────────────

    internal static long   Long (OracleDataReader r, string col) => Convert.ToInt64(r[col]);
    internal static long?  LongN(OracleDataReader r, string col) => r[col] is DBNull ? null : Convert.ToInt64(r[col]);
    internal static int    Int  (OracleDataReader r, string col) => Convert.ToInt32(r[col]);
    internal static int?   IntN (OracleDataReader r, string col) => r[col] is DBNull ? null : Convert.ToInt32(r[col]);
    internal static string Str  (OracleDataReader r, string col) => r[col]?.ToString() ?? string.Empty;
    internal static string? StrN(OracleDataReader r, string col) => r[col] is DBNull ? null : r[col]?.ToString();
    internal static bool   Bool (OracleDataReader r, string col) => Convert.ToInt32(r[col]) == 1;
    internal static DateTime  Dt (OracleDataReader r, string col) => ToDateTime(r[col]);
    internal static DateTime? DtN(OracleDataReader r, string col) => r[col] is DBNull ? null : ToDateTime(r[col]);

    // TIMESTAMP columns come back as DateTime; TIMESTAMP WITH TIME ZONE come back
    // as DateTimeOffset (which Convert.ToDateTime can't handle) — normalise to UTC.
    private static DateTime ToDateTime(object v) => v switch
    {
        DateTimeOffset dto => dto.UtcDateTime,
        _                  => Convert.ToDateTime(v)
    };
    internal static decimal   Dec(OracleDataReader r, string col) => Convert.ToDecimal(r[col]);

    // ── Execute helpers ───────────────────────────────────────────────────────

    internal static async Task<int> ExecAsync(this OracleCommand cmd, CancellationToken ct = default)
        => await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

    internal static async Task<T?> ScalarAsync<T>(this OracleCommand cmd, CancellationToken ct = default)
    {
        var val = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (val is null || val is DBNull) return default;
        // Unwrap Nullable<T> so Convert.ChangeType gets the underlying type
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(val, targetType);
    }

    // ── Query helpers ─────────────────────────────────────────────────────────

    internal static async Task<List<T>> QueryAsync<T>(
        this OracleCommand cmd,
        Func<OracleDataReader, T> map,
        CancellationToken ct = default)
    {
        var list = new List<T>();
        using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            list.Add(map(reader));
        return list;
    }

    internal static async Task<T?> QueryOneAsync<T>(
        this OracleCommand cmd,
        Func<OracleDataReader, T> map,
        CancellationToken ct = default) where T : class
    {
        using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return map(reader);
        return null;
    }

    internal static async Task<T?> QueryOneValueAsync<T>(
        this OracleCommand cmd,
        Func<OracleDataReader, T> map,
        CancellationToken ct = default) where T : struct
    {
        using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return map(reader);
        return null;
    }
}
