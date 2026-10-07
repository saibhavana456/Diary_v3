using Microsoft.Data.SqlClient;
using MyDiary.Web.Features.Reports.Models;
using OfficeOpenXml;
using System.Data;

namespace MyDiary.Web.Features.Reports.Services
{
    public class DealerReportService : IDealerReportService
    {
        private readonly string _connectionString;

        public DealerReportService(IConfiguration configuration)
        {
            // Pulling the connection string defined in appsettings.json
            _connectionString = configuration.GetConnectionString("SQLServerConnection");
        }

        public async Task<List<StateMasterModel>> GetStatesAsync()
        {
            var states = new List<StateMasterModel>();

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand("dbo.usp_States_List", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    await con.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            states.Add(new StateMasterModel
                            {
                                StateNameEng = reader["state_name_eng"].ToString(),
                                StateCodeFinacle = reader["state_code_finacle"].ToString()
                            });
                        }
                    }
                }
            }
            return states;
        }

        public async Task<PagedDealerResult> GetDealersAsync(
            string stateCode, int pageNumber, int pageSize, string searchString = null, string sortColumn = "name", string sortDirection = "ASC")
        {
            var result = new PagedDealerResult();

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand("dbo.usp_Dealer_List", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Input Parameters
                    cmd.Parameters.AddWithValue("@StateCode", string.IsNullOrEmpty(stateCode) ? "All" : stateCode);
                    cmd.Parameters.AddWithValue("@IncludeBlacklisted", 1); // As per legacy logic
                    cmd.Parameters.AddWithValue("@Search", (object)searchString ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SortColumn", sortColumn);
                    cmd.Parameters.AddWithValue("@SortDirection", sortDirection);
                    cmd.Parameters.AddWithValue("@PageNumber", pageNumber);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);
                    cmd.Parameters.AddWithValue("@ReturnAll", 0);

                    // Output Parameters
                    var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    var asOnDateParam = new SqlParameter("@AsOnDate", SqlDbType.Date) { Direction = ParameterDirection.Output };

                    cmd.Parameters.Add(totalCountParam);
                    cmd.Parameters.Add(asOnDateParam);

                    await con.OpenAsync();

                    // Read Data
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Dealers.Add(new DealerModel
                            {
                                Name = reader["name"].ToString(),
                                Address = reader["addr3"].ToString(),
                                City = reader["city"].ToString(),
                                State = reader["state"].ToString(),
                                Pincode = reader["pin"].ToString(),
                                MobileNo = reader["mobno"].ToString(),
                                ContactPerson = reader["contper"].ToString(),
                                ContactPersonMobile = reader["contpermob"].ToString(),
                                Email = reader["email"].ToString(),
                                LastUpdateDate = reader["LastUpdateDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastUpdateDate"]) : (DateTime?)null
                            });
                        }
                    }

                    // Read Output Parameters after reader is closed
                    result.TotalCount = totalCountParam.Value != DBNull.Value ? Convert.ToInt32(totalCountParam.Value) : 0;
                    result.AsOnDate = asOnDateParam.Value != DBNull.Value ? Convert.ToDateTime(asOnDateParam.Value) : (DateTime?)null;
                }
            }

            return result;
        }

        // Add this new method inside DealerReportService
        public async Task<byte[]> ExportDealersToExcelAsync(
            string stateCode, string searchString = null, string sortColumn = "name", string sortDirection = "ASC")
        {
            var dealers = new List<DealerModel>();

            using (var con = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand("dbo.usp_Dealer_List", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Notice: @ReturnAll is set to 1 here so we get ALL records for the export, ignoring pagination!
                    cmd.Parameters.AddWithValue("@StateCode", string.IsNullOrEmpty(stateCode) ? "All" : stateCode);
                    cmd.Parameters.AddWithValue("@IncludeBlacklisted", 1);
                    cmd.Parameters.AddWithValue("@Search", (object)searchString ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SortColumn", sortColumn);
                    cmd.Parameters.AddWithValue("@SortDirection", sortDirection);
                    cmd.Parameters.AddWithValue("@PageNumber", 1);
                    cmd.Parameters.AddWithValue("@PageSize", 50);
                    cmd.Parameters.AddWithValue("@ReturnAll", 1); // Magic bullet for export

                    var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    var asOnDateParam = new SqlParameter("@AsOnDate", SqlDbType.Date) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(totalCountParam);
                    cmd.Parameters.Add(asOnDateParam);

                    await con.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            dealers.Add(new DealerModel
                            {
                                Name = reader["name"].ToString(),
                                Address = reader["addr3"].ToString(),
                                City = reader["city"].ToString(),
                                State = reader["state"].ToString(),
                                Pincode = reader["pin"].ToString(),
                                MobileNo = reader["mobno"].ToString(),
                                ContactPerson = reader["contper"].ToString(),
                                ContactPersonMobile = reader["contpermob"].ToString(),
                                Email = reader["email"].ToString()
                            });
                        }
                    }
                }
            }

            // Generate the Excel file using EPPlus
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Dealers Master");

            // Create Headers
            string[] headers = { "S.No.", "NAME", "ADDRESS", "CITY", "STATE", "PINCODE", "MOBILE NO", "CONTACT PERSON", "C.P. MOBILE", "EMAIL ID" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(42, 47, 91)); // Matches your Mudblazor header
                ws.Cells[1, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            // Populate Data
            for (int i = 0; i < dealers.Count; i++)
            {
                var dealer = dealers[i];
                ws.Cells[i + 2, 1].Value = i + 1;
                ws.Cells[i + 2, 2].Value = dealer.Name;
                ws.Cells[i + 2, 3].Value = string.IsNullOrWhiteSpace(dealer.Address) ? "NA" : dealer.Address;
                ws.Cells[i + 2, 4].Value = dealer.City;
                ws.Cells[i + 2, 5].Value = dealer.State;
                ws.Cells[i + 2, 6].Value = dealer.Pincode;
                ws.Cells[i + 2, 7].Value = string.IsNullOrWhiteSpace(dealer.MobileNo) ? "NA" : dealer.MobileNo;
                ws.Cells[i + 2, 8].Value = dealer.ContactPerson;
                ws.Cells[i + 2, 9].Value = dealer.ContactPersonMobile;
                ws.Cells[i + 2, 10].Value = dealer.Email;
            }

            ws.Cells.AutoFitColumns();
            return package.GetAsByteArray();
        }
    }
}