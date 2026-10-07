using System.Data;
using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Features.Reports.Models;

namespace MyDiary.Web.Features.Reports.Services
{
    public class DebitCardTrackingService : IDebitCardTrackingService
    {
        private readonly string _connectionString;

        public DebitCardTrackingService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SQLServerConnection");
        }

        public async Task<DebitCardTrackingResult> GetTrackingDataAsync(string accountNumber = null)
        {
            var result = new DebitCardTrackingResult();

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    using (var cmd = new SqlCommand("dbo.usp_GetDebitCardTracking", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        if (string.IsNullOrWhiteSpace(accountNumber))
                        {
                            cmd.Parameters.AddWithValue("@AccountNumber", DBNull.Value);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@AccountNumber", accountNumber.Trim());
                        }

                        var asOnDateParam = new SqlParameter("@AsOnDate", SqlDbType.DateTime)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(asOnDateParam);

                        await con.OpenAsync();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Records.Add(new DebitCardTrackingModel
                                {
                                    SeqNo = Convert.ToInt32(reader["SeqNo"]),
                                    CardNumber = reader["CardNumber"].ToString(),
                                    ENCName = reader["ENCName"].ToString(),
                                    AccountNumber = reader["AccountNumber"].ToString(),
                                    RefOrSoleId = reader["REF_or_sole_ID"].ToString(),
                                    ContactNo = reader["ContactNo"].ToString(),
                                    BarCode = reader["BARCode"].ToString(),
                                    CourierName = reader["CourierName"].ToString(),
                                    DispatchDate = reader["DispatchDate"].ToString()
                                });
                            }
                        }

                        if (asOnDateParam.Value != DBNull.Value)
                        {
                            result.AsOnDate = Convert.ToDateTime(asOnDateParam.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[DebitCardTrackingService] GetTrackingDataAsync failed: " + ex.Message);
            }

            return result;
        }
    }
}