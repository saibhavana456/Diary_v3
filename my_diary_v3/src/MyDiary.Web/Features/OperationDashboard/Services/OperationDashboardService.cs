using Microsoft.Data.SqlClient;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using MyDiary.Web.Features.OperationDashboard.Models;
using System.Data;

namespace MyDiary.Web.Features.OperationDashboard.Services
{
    public class OperationDashboardService : IOperationDashboardService
    {
        private readonly SqlDbProvider _sqlDbProvider;
        private readonly IConfiguration _configuration;
        private string _connectionString;

        public OperationDashboardService(SqlDbProvider sqlDbProvider, IConfiguration configuration)
        {
            _sqlDbProvider = sqlDbProvider;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("SQLServerConnection")
                                      ?? _configuration.GetConnectionString("SQLServerConnection");
        }

        #region Sundry Report
        public async Task<SundryOSReportDto> GetSundryEntriesDataAsync(string solId)
        {
            var result = new SundryOSReportDto();

            try
            {

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        // Loop through all the multiple result sets returned by the Stored Procedure
                        do
                        {
                            // Read the first row of the current result set
                            if (await reader.ReadAsync())
                            {
                                // Check if this result set is the "SundryOSReport" table
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "SundryOSReport")
                                {
                                    // Manual ADO.NET Mapping
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayEntries = Convert.ToInt32(reader["PrevDayEntries"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    result.PrevDayEntries90Days = Convert.ToInt32(reader["PrevDayEntries90Days"]);
                                    result.PrevDayTotal90Days = Convert.ToDecimal(reader["PrevDayTotal90Days"]);

                                    result.PrevDayEntries180Days = Convert.ToInt32(reader["PrevDayEntries180Days"]);
                                    result.PrevDayTotal180Days = Convert.ToDecimal(reader["PrevDayTotal180Days"]);

                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Entries = Convert.ToInt32(reader["PrevDay2Entries"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthEntries = Convert.ToInt32(reader["PrevMonthEntries"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;

                                    found = true;
                                }
                            }
                        }
                        // Advance to the next result set if we haven't found the right table yet
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[OperationDashboardService] GetSundryEntriesDataAsync failed: " + ex.Message);
            }

            return result;
        }
        #endregion
        #region Suspense Report
        public async Task<SuspenseOSReportDto> GetSuspenseEntriesDataAsync(string solId)
        {
            var result = new SuspenseOSReportDto();

            try
            {
            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                // Target the Suspense table
                                if (tableName == "SuspenseOSReport")
                                {
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayEntries = Convert.ToInt32(reader["PrevDayEntries"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    result.PrevDayEntries90Days = Convert.ToInt32(reader["PrevDayEntries90Days"]);
                                    result.PrevDayTotal90Days = Convert.ToDecimal(reader["PrevDayTotal90Days"]);

                                    result.PrevDayEntries180Days = Convert.ToInt32(reader["PrevDayEntries180Days"]);
                                    result.PrevDayTotal180Days = Convert.ToDecimal(reader["PrevDayTotal180Days"]);

                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Entries = Convert.ToInt32(reader["PrevDay2Entries"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthEntries = Convert.ToInt32(reader["PrevMonthEntries"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;

                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[OperationDashboardService] GetSuspenseEntriesDataAsync failed: " + ex.Message);
            }

            return result;
        }
        #endregion
        #region Locker Report
        public async Task<LockerReportDto> GetLockerReportDataAsync(string solId)
        {
            var result = new LockerReportDto();

            try
            {
            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "LockerReport")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayNoOfLockers = Convert.ToInt32(reader["PrevDayNoOfLockers"]);
                                    result.PrevDayNoOfVacantLockers = Convert.ToInt32(reader["PrevDayNoOfVacantLockers"]);
                                    result.PrevDayNoOfLockersWithDueRent = Convert.ToInt32(reader["PrevDayNoOfLockersWithDueRent"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2NoOfLockers = Convert.ToInt32(reader["PrevDay2NoOfLockers"]);
                                    result.PrevDay2NoOfVacantLockers = Convert.ToInt32(reader["PrevDay2NoOfVacantLockers"]);
                                    result.PrevDay2NoOfLockersWithDueRent = Convert.ToInt32(reader["PrevDay2NoOfLockersWithDueRent"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    // Map Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthNoOfLockers = Convert.ToInt32(reader["PrevMonthNoOfLockers"]);
                                    result.PrevMonthNoOfVacantLockers = Convert.ToInt32(reader["PrevMonthNoOfVacantLockers"]);
                                    result.PrevMonthNoOfLockersWithDueRent = Convert.ToInt32(reader["PrevMonthNoOfLockersWithDueRent"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[OperationDashboardService] GetLockerReportDataAsync failed: " + ex.Message);
            }

            return result;
        }
        #endregion
        #region Defaulting Accounts
        public async Task<AccountStatusReportDto> GetDefaultingAccountsDataAsync(string solId)
        {
            var result = new AccountStatusReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "AccountStatusReport")
                                {
                                    // Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayNoOfDormant = Convert.ToInt32(reader["PrevDayNoOfDormant"]);
                                    result.PrevDayNoOfInactive = Convert.ToInt32(reader["PrevDayNoOfInactive"]);
                                    result.PrevDayNoOfDeaf = Convert.ToInt32(reader["PrevDayNoOfDeaf"]);
                                    result.PrevDayNoOfNoNomination = Convert.ToInt32(reader["PrevDayNoOfNoNomination"]);
                                    result.PrevDayLienMarked = Convert.ToInt32(reader["PrevDayLienMarked"]);
                                    result.PrevDayAmountDormant = Convert.ToDecimal(reader["PrevDayAmountDormant"]);
                                    result.PrevDayAmountInactive = Convert.ToDecimal(reader["PrevDayAmountInactive"]);
                                    result.PrevDayAmountDeaf = Convert.ToDecimal(reader["PrevDayAmountDeaf"]);
                                    result.PrevDayAmountNoNomination = Convert.ToDecimal(reader["PrevDayAmountNoNomination"]);
                                    result.PrevDayAmountLienMarked = Convert.ToDecimal(reader["PrevDayAmountLienMarked"]);

                                    // Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2NoOfDormant = Convert.ToInt32(reader["PrevDay2NoOfDormant"]);
                                    result.PrevDay2NoOfInactive = Convert.ToInt32(reader["PrevDay2NoOfInactive"]);
                                    result.PrevDay2NoOfDeaf = Convert.ToInt32(reader["PrevDay2NoOfDeaf"]);
                                    result.PrevDay2NoOfNoNomination = Convert.ToInt32(reader["PrevDay2NoOfNoNomination"]);
                                    result.PrevDay2LienMarked = Convert.ToInt32(reader["PrevDay2LienMarked"]);
                                    result.PrevDay2AmountDormant = Convert.ToDecimal(reader["PrevDay2AmountDormant"]);
                                    result.PrevDay2AmountInactive = Convert.ToDecimal(reader["PrevDay2AmountInactive"]);
                                    result.PrevDay2AmountDeaf = Convert.ToDecimal(reader["PrevDay2AmountDeaf"]);
                                    result.PrevDay2AmountNoNomination = Convert.ToDecimal(reader["PrevDay2AmountNoNomination"]);
                                    result.PrevDay2AmountLienMarked = Convert.ToDecimal(reader["PrevDay2AmountLienMarked"]);

                                    // Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthNoOfDormant = Convert.ToInt32(reader["PrevMonthNoOfDormant"]);
                                    result.PrevMonthNoOfInactive = Convert.ToInt32(reader["PrevMonthNoOfInactive"]);
                                    result.PrevMonthNoOfDeaf = Convert.ToInt32(reader["PrevMonthNoOfDeaf"]);
                                    result.PrevMonthNoOfNoNomination = Convert.ToInt32(reader["PrevMonthNoOfNoNomination"]);
                                    result.PrevMonthLienMarked = Convert.ToInt32(reader["PrevMonthLienMarked"]);
                                    result.PrevMonthAmountDormant = Convert.ToDecimal(reader["PrevMonthAmountDormant"]);
                                    result.PrevMonthAmountInactive = Convert.ToDecimal(reader["PrevMonthAmountInactive"]);
                                    result.PrevMonthAmountDeaf = Convert.ToDecimal(reader["PrevMonthAmountDeaf"]);
                                    result.PrevMonthAmountNoNomination = Convert.ToDecimal(reader["PrevMonthAmountNoNomination"]);
                                    result.PrevMonthAmountLienMarked = Convert.ToDecimal(reader["PrevMonthAmountLienMarked"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion
        #region Cash Holding
        public async Task<CashHoldingReportDto> GetCashHoldingDataAsync(string solId)
        {
            var result = new CashHoldingReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "CashHoldingReport")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayLimit = Convert.ToDecimal(reader["PrevDayLimit"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Limit = Convert.ToDecimal(reader["PrevDay2Limit"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    // Map Previous Month (Includes Range)
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthFromDt = Convert.ToDateTime(reader["PrevMonthFromDt"]);
                                    result.PrevMonthLimit = Convert.ToDecimal(reader["PrevMonthLimit"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion
        #region CKYC Pendency
        public async Task<CKYCPendingReportDto> GetCKYCPendencyDataAsync(string solId)
        {
            var result = new CKYCPendingReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "CKYCPendingReport")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    // Map Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion
        #region ReKYC Pendency
        public async Task<ReKYCPendencyReportDto> GetReKYCPendencyDataAsync(string solId)
        {
            var result = new ReKYCPendencyReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "ReKYCPendencyDataReport")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    // Map Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion
        #region UCIC Aadhaar Pendency 
        public async Task<UCICAadhaarReportDto> GetUCICAadhaarPendencyDataAsync(string solId)
        {
            var result = new UCICAadhaarReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "UCICAadhaarReport")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    // Map Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion
        #region UCIC PAN Pendency
        public async Task<UCICPanReportDto> GetUCICPanPendencyDataAsync(string solId)
        {
            var result = new UCICPanReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "UCIC_PAN_Report")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    // Map Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion
        #region Beneficiary Owner Pendency
        public async Task<BeneficiaryOwnerReportDto> GetBeneficiaryOwnerDataAsync(string solId)
        {
            var result = new BeneficiaryOwnerReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "BeneficiaryOwnerDataReport")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayTotal = Convert.ToDecimal(reader["PrevDayTotal"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2Total = Convert.ToDecimal(reader["PrevDay2Total"]);

                                    // Map Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthTotal = Convert.ToDecimal(reader["PrevMonthTotal"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion

        public async Task<DataSet> GetDashboardDataAsync(string solId)
        {
            AppLogger.LogInfo($"OperationDashboardService: GetDashboardDataAsync called � solId: {solId}");
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("uspGetPeformanceDataForDashboard", conn);

                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@SolID",
                    string.IsNullOrWhiteSpace(solId) ? DBNull.Value : solId);

                var ds = new DataSet();

                using var adapter = new SqlDataAdapter(cmd);

                await conn.OpenAsync();
                adapter.Fill(ds);

                AppLogger.LogInfo($"OperationDashboardService: Dashboard loaded � {ds.Tables.Count} tables returned");
                return ds;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"OperationDashboardService: GetDashboardDataAsync failed � solId: {solId}");
                throw;
            }
        }




        #region Customer Complaints (Banking Ombudsman)
        public async Task<BOComplaintReportDto> GetCustomerComplaintsDataAsync(string solId)
        {
            var result = new BOComplaintReportDto();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspGetPeformanceDataForDashboard", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    if (string.IsNullOrEmpty(solId))
                    {
                        command.Parameters.AddWithValue("@SolID", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@SolID", solId);
                    }

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool found = false;

                        do
                        {
                            if (await reader.ReadAsync())
                            {
                                string tableName = reader["TableName"]?.ToString();

                                if (tableName == "BOComplaintReport")
                                {
                                    // Map Present Day
                                    result.PrevDayDt = Convert.ToDateTime(reader["PrevDayDt"]);
                                    result.PrevDayNoOfComplaints = Convert.ToInt32(reader["PrevDayNoOfComplaints"]);

                                    // Map Previous Day
                                    result.PrevDay2Dt = Convert.ToDateTime(reader["PrevDay2Dt"]);
                                    result.PrevDay2NoOfComplaints = Convert.ToInt32(reader["PrevDay2NoOfComplaints"]);

                                    // Map Previous Month
                                    result.PrevMonthDt = Convert.ToDateTime(reader["PrevMonthDt"]);
                                    result.PrevMonthNoOfComplaints = Convert.ToInt32(reader["PrevMonthNoOfComplaints"]);

                                    result.TableName = tableName;
                                    found = true;
                                }
                            }
                        }
                        while (!found && await reader.NextResultAsync());
                    }
                }
            }

            return result;
        }
        #endregion
        #region Drop-downs
        public async Task<IEnumerable<DropdownItemDto>> GetDropdownDataAsync(string type, string parentSolId = null)
        {
            var result = new List<DropdownItemDto>();

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand("dbo.uspPerformanceDashboardDrpDwn", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    // Map strictly to your new SP parameters
                    command.Parameters.AddWithValue("@Type", type);

                    // ADO.NET requires explicit DBNull for null values in stored procedures
                    if (string.IsNullOrEmpty(parentSolId))
                    {
                        command.Parameters.AddWithValue("@ParentSolId", DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@ParentSolId", parentSolId);
                    }

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new DropdownItemDto
                            {
                                // Safely converting index 0 to Value, index 1 to Text
                                Value = reader.GetValue(0)?.ToString() ?? "",
                                Text = reader.GetValue(1)?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return result;
        }
        #endregion
    }
}