using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using System.Data;

namespace MyDiary.Web.Data
{
    public class SqlDbProvider
    {
        private readonly IConfiguration _config;

        public SqlDbProvider(IConfiguration config)
        {
            _config = config;
        }

        // Helper to get the correct connection string by name
        private string GetConnectionString(string connectionName)
        {
            return _config.GetConnectionString(connectionName)
                   ?? _config.GetConnectionString("SQLServerConnection")!;
        }
        public async Task<int> ExecuteNonQueryAsync(
    string sql,
    SqlParameter[] parameters,
    string connectionName)
        {
            using SqlConnection con =
                new SqlConnection(GetConnectionString(connectionName));

            using SqlCommand cmd =
                new SqlCommand(sql, con);

            if (parameters != null)
                cmd.Parameters.AddRange(parameters);

            await con.OpenAsync();

            return await cmd.ExecuteNonQueryAsync();
        }


        public async Task<DataTable> ExecuteQueryAsync(string query, string connName = "SQLServerConnection")
        {
            try
            {
                using var conn = new SqlConnection(GetConnectionString(connName));
                using var cmd = new SqlCommand(query, conn);

                var dt = new DataTable();
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                dt.Load(reader);
                return dt;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"SqlDbProvider: ExecuteQueryAsync failed — {query[..Math.Min(100, query.Length)]}");
                throw;
            }
        }

        public async Task<DataTable> ExecuteStoredProcAsync(string spName, SqlParameter[] parameters, string connName = "SQLServerConnection")
        {
            try
            {
                using var conn = new SqlConnection(GetConnectionString(connName));
                using var cmd = new SqlCommand(spName, conn) { CommandType = CommandType.StoredProcedure };

                if (parameters != null)
                {
                    cmd.Parameters.AddRange(parameters);
                }

                var dt = new DataTable();
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                dt.Load(reader);
                return dt;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"SqlDbProvider: ExecuteStoredProcAsync failed — SP: {spName}");
                throw;
            }
        }

        // Add this to SqlDbProvider.cs to handle parameters with standard queries
        public async Task<DataTable> ExecuteQueryWithParamsAsync(string query, SqlParameter[] parameters, string connName = "SQLServerConnection")
        {
            using var conn = new SqlConnection(GetConnectionString(connName));
            using var cmd = new SqlCommand(query, conn);

            if (parameters != null && parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }

            var dt = new DataTable();
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }

        public async Task<DataTable> ExecuteQueryAsync(
    string query,
    SqlParameter[] parameters,
    string connName = "SQLServerConnection")
        {
            using var conn = new SqlConnection(GetConnectionString(connName));
            using var cmd = new SqlCommand(query, conn);

            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            var dt = new DataTable();
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);
            return dt;
        }
        public async Task<DataSet> ExecuteStoredProcMultipleAsync(string spName, SqlParameter[] parameters, string connName = "SQLServerConnection")
        {
            try
            {
                using var conn = new SqlConnection(GetConnectionString(connName));
                using var cmd = new SqlCommand(spName, conn) { CommandType = CommandType.StoredProcedure };
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                var ds = new DataSet();
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                int tableIndex = 0;
                do
                {
                    var dt = new DataTable($"Table{tableIndex++}");
                    while (await reader.ReadAsync())
                    {
                        if (dt.Columns.Count == 0)
                            for (int i = 0; i < reader.FieldCount; i++)
                                dt.Columns.Add(reader.GetName(i), reader.GetFieldType(i));

                        var row = dt.NewRow();
                        for (int i = 0; i < reader.FieldCount; i++)
                            row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                        dt.Rows.Add(row);
                    }
                    ds.Tables.Add(dt);
                }
                while (await reader.NextResultAsync());
                return ds;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"SqlDbProvider: ExecuteStoredProcMultipleAsync failed — SP: {spName}");
                throw;
            }
        }
    }
}