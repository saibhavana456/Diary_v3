using MyDiary.Web.Core.Extensions;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace MyDiary.Web.Data
{
    public class OracleDbProvider
    {
        private readonly string _connectionString;

        public OracleDbProvider(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("MyDiaryDBConnection")!;
        }


        public async Task<DataTable> ExecuteQueryAsync(string query)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                using var cmd = new OracleCommand(query, conn);

                var dt = new DataTable();
                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                dt.Load(reader);
                return dt;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"OracleDbProvider: ExecuteQueryAsync failed — {query[..Math.Min(100, query.Length)]}");
                throw;
            }
        }
        public async Task<List<T>> ExecuteProcedureAsync<T>(string procedureName, Func<OracleDataReader, T> map)
        {
            try
            {
                var list = new List<T>();
                using var conn = new OracleConnection(_connectionString);
                using var cmd = new OracleCommand(procedureName, conn) { CommandType = CommandType.StoredProcedure };

                cmd.Parameters.Add("O_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(map(reader));
                }
                return list;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"OracleDbProvider: ExecuteProcedureAsync failed — SP: {procedureName}");
                throw;
            }
        }
    }
}
