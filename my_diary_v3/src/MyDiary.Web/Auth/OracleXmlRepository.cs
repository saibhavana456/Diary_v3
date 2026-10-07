using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Oracle.ManagedDataAccess.Client;

namespace MyDiary.Web.Auth;

/// <summary>
/// Persists the ASP.NET Core Data Protection key ring to the shared RP_OWNER
/// Oracle database (table RP_DATAPROTECTION_KEYS) instead of local/ephemeral
/// disk storage. Every K8s pod and every IIS instance already connects to
/// this same database (it's the "RP_Owner" connection string used throughout
/// RequestPortal.Data, and by Hangfire's own distributed job storage) — so
/// this gives every instance a shared, durable key ring with zero additional
/// infrastructure (no PVC, no UNC share, no IIS-console work).
///
/// Without this, AddDataProtection() defaults to a per-process ephemeral key
/// ring: any request that lands on a different pod/instance (or the same one
/// after a restart) can't decrypt a cookie/token issued before that point,
/// which silently breaks the user's session/SignalR circuit.
///
/// Table DDL (run once against RP_OWNER):
///   CREATE TABLE RP_DATAPROTECTION_KEYS (
///     ID              NUMBER(19,0)   NOT NULL PRIMARY KEY,
///     FRIENDLY_NAME   VARCHAR2(400),
///     XML_DATA        CLOB           NOT NULL,
///     CREATED_AT_UTC  TIMESTAMP(6)   DEFAULT SYSTIMESTAMP NOT NULL
///   );
/// (uses the existing RP_SEQ_GLOBAL sequence for ID, same as every other
/// RP_OWNER table in this app.)
/// </summary>
public sealed class OracleXmlRepository : IXmlRepository
{
    private readonly string _connectionString;

    public OracleXmlRepository(string connectionString) => _connectionString = connectionString;

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var elements = new List<XElement>();
        using var conn = new OracleConnection(_connectionString);
        conn.Open();
        using var cmd = new OracleCommand("SELECT XML_DATA FROM RP_DATAPROTECTION_KEYS ORDER BY ID", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            using var clob = reader.GetOracleClob(0);
            elements.Add(XElement.Parse(clob.Value));
        }
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();
        using var cmd = new OracleCommand(
            "INSERT INTO RP_DATAPROTECTION_KEYS (ID, FRIENDLY_NAME, XML_DATA) " +
            "VALUES (RP_SEQ_GLOBAL.NEXTVAL, :p_name, :p_xml)", conn);
        cmd.Parameters.Add("p_name", OracleDbType.Varchar2).Value = (object?)friendlyName ?? DBNull.Value;
        cmd.Parameters.Add("p_xml", OracleDbType.Clob).Value = element.ToString(SaveOptions.DisableFormatting);
        cmd.ExecuteNonQuery();
    }
}
