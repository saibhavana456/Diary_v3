using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using MyDiary.Web.Features.DeploymentPulse.Models;

namespace MyDiary.Web.Features.DeploymentPulse.Services
{
    /// <summary>
    /// Async Oracle data access for the Deployment Pulse report, targeting the same
    /// DEPLOYMENT_PULSE_ITSM_VIEW used by the existing WebForms page. Built with
    /// Oracle.ManagedDataAccess.Core (NuGet) since this runs under Blazor Server /
    /// .NET Core, not the classic ODP.NET the WebForms app uses.
    ///
    /// Requires a "PMS_DB_Connection" entry under ConnectionStrings in appsettings.
    /// If this app decrypts its other connection strings via a shared helper (per the
    /// encrypted blobs already in appsettings), call that helper here instead of
    /// reading the raw value directly - swap the one line in the constructor.
    /// </summary>
    public class DeploymentPulseService : IDeploymentPulseService
    {
        private readonly string _connectionString;

        private const string ViewName = "DEPLOYMENT_PULSE_ITSM_VIEW";
        private const string AllColumns =
            "CRQ_NUMBER, VERTICAL, DESCRIPTION, IMPACT, TENTATIVE_GO_LIVE_DATE, ACTUAL_GO_LIVE_DATE, SPOC_DETAILS, PUBLISH_FLAG, STATUS";

        public DeploymentPulseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("PMS_DB_Connection");
            // TODO: if connection strings here are stored encrypted (as elsewhere in
            // this app's appsettings), decrypt _connectionString here before use.

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'PMS_DB_Connection' was not found in configuration.");
            }
        }

        public async Task<List<DeploymentPulseItem>> GetDeploymentsAsync(DeploymentPulseFilter filter, bool onlyPublished = true)
        {
            filter ??= new DeploymentPulseFilter();

            var where = new StringBuilder(" STATUS IS NOT NULL ");
            var parameters = new List<OracleParameter>();

            if (onlyPublished)
            {
                where.Append(" AND PUBLISH_FLAG = 'Y' ");
            }

            if (!string.IsNullOrWhiteSpace(filter.CrqNumber))
            {
                where.Append(" AND UPPER(CRQ_NUMBER) LIKE UPPER(:crNo) ");
                parameters.Add(new OracleParameter("crNo", OracleDbType.Varchar2) { Value = $"%{filter.CrqNumber.Trim()}%" });
            }

            if (!string.IsNullOrWhiteSpace(filter.Vertical))
            {
                where.Append(" AND UPPER(VERTICAL) LIKE UPPER(:vertical) ");
                parameters.Add(new OracleParameter("vertical", OracleDbType.Varchar2) { Value = $"%{filter.Vertical.Trim()}%" });
            }

            if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status.ToUpperInvariant() != "ALL")
            {
                where.Append(" AND STATUS = :status ");
                parameters.Add(new OracleParameter("status", OracleDbType.Varchar2) { Value = filter.Status });
            }

            if (!string.IsNullOrWhiteSpace(filter.PublishYear) && filter.PublishYear.ToUpperInvariant() != "ALL")
            {
                where.Append(" AND TO_CHAR(ACTUAL_GO_LIVE_DATE,'YYYY') = :publishYear ");
                parameters.Add(new OracleParameter("publishYear", OracleDbType.Varchar2) { Value = filter.PublishYear });
            }

            if (filter.FromDate.HasValue)
            {
                where.Append(" AND TENTATIVE_GO_LIVE_DATE >= :fromDate ");
                parameters.Add(new OracleParameter("fromDate", OracleDbType.Date) { Value = filter.FromDate.Value.Date });
            }

            if (filter.ToDate.HasValue)
            {
                where.Append(" AND TENTATIVE_GO_LIVE_DATE < :toDate ");
                parameters.Add(new OracleParameter("toDate", OracleDbType.Date) { Value = filter.ToDate.Value.Date.AddDays(1) });
            }

            where.Append(" ORDER BY CRQ_NUMBER DESC ");

            string sql = $"SELECT {AllColumns} FROM {ViewName} WHERE {where}";

            var results = new List<DeploymentPulseItem>();

            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn) { CommandTimeout = 60 };
            if (parameters.Count > 0)
            {
                cmd.Parameters.AddRange(parameters.ToArray());
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new DeploymentPulseItem
                {
                    CrqNumber = reader["CRQ_NUMBER"] as string,
                    Vertical = reader["VERTICAL"] as string,
                    Description = reader["DESCRIPTION"] as string,
                    Impact = reader["IMPACT"] as string,
                    TentativeGoLiveDate = reader["TENTATIVE_GO_LIVE_DATE"] as DateTime?,
                    ActualGoLiveDate = reader["ACTUAL_GO_LIVE_DATE"] as DateTime?,
                    SpocDetails = reader["SPOC_DETAILS"] as string,
                    PublishFlag = reader["PUBLISH_FLAG"] as string,
                    Status = reader["STATUS"] as string
                });
            }

            return results;
        }

        public async Task<List<string>> GetPublishYearsAsync()
        {
            var years = new List<string>();

            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            const string sql =
                "SELECT PUBLISH_YEARS FROM DEPLOYMENT_PULSE_PUBLISH_YEARS_VIEW " +
                "WHERE PUBLISH_YEARS IS NOT NULL ORDER BY PUBLISH_YEARS ASC";

            await using var cmd = new OracleCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                years.Add(reader["PUBLISH_YEARS"].ToString());
            }

            return years;
        }
    }
}
