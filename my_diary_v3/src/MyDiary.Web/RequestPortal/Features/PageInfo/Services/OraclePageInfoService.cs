using Dapper;
using MyDiary.Web.Core.Extensions;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Data;
using PageInfoRecord = RequestPortal.Web.Features.PageInfo.Models.PageInfo;
using PageInfoForm   = RequestPortal.Web.Features.PageInfo.Models.PageInfoForm;

namespace RequestPortal.Web.Features.PageInfo.Services;

/// <summary>
/// Reads and writes page metadata from RP_PAGE_INFO Oracle table.
/// Uses the shared IDbConnectionFactory (ConnectionStrings:Oracle).
/// Run db/page_info/01_PAGE_INFO_DDL.sql before switching to production.
/// </summary>
public sealed class OraclePageInfoService(IDbConnectionFactory factory) : IPageInfoService
{
    private async Task<OracleConnection> Conn()
        => (OracleConnection)await factory.OpenAsync();

    public async Task<PageInfoRecord?> GetAsync(string pageKey)
    {
        const string sql = """
            SELECT PAGE_KEY, PAGE_NAME, PAGE_TYPE, DESCRIPTION,
                   OWNER_VERTICAL, MODULE_OWNER, IP_NUMBER, MAIL_ID,
                   CREATED_DATE, VERSION, STATUS
            FROM   RP_PAGE_INFO
            WHERE  PAGE_KEY = :key
            """;
        await using var conn = await Conn();
        var rows = await conn.QueryAsync(sql, new { key = pageKey });
        return rows.Select(Map).FirstOrDefault();
    }

    public async Task<List<PageInfoRecord>> GetAllAsync()
    {
        const string sql = """
            SELECT PAGE_KEY, PAGE_NAME, PAGE_TYPE, DESCRIPTION,
                   OWNER_VERTICAL, MODULE_OWNER, IP_NUMBER, MAIL_ID,
                   CREATED_DATE, VERSION, STATUS
            FROM   RP_PAGE_INFO
            ORDER  BY PAGE_NAME
            """;
        await using var conn = await Conn();
        var rows = await conn.QueryAsync(sql);
        return rows.Select(Map).ToList();
    }

    public async Task<bool> UpsertAsync(PageInfoForm f, bool isNew)
    {
        await using var conn = await Conn();
        try
        {
            if (isNew)
            {
                const string ins = """
                    INSERT INTO RP_PAGE_INFO
                      (PAGE_KEY, PAGE_NAME, PAGE_TYPE, DESCRIPTION, OWNER_VERTICAL,
                       MODULE_OWNER, IP_NUMBER, MAIL_ID, CREATED_DATE, VERSION, STATUS)
                    VALUES
                      (:pk, :pn, :pt, :ds, :ov, :mo, :ip, :mi, SYSDATE, :vr, :st)
                    """;
                await conn.ExecuteAsync(ins,
                    new { pk=f.PageKey, pn=f.PageName, pt=f.PageType, ds=f.Description,
                          ov=f.OwnerVertical, mo=f.ModuleOwner, ip=f.IpNumber,
                          mi=f.MailId, vr=f.Version, st=f.Status });
            }
            else
            {
                const string upd = """
                    UPDATE RP_PAGE_INFO
                    SET    PAGE_NAME=:pn, PAGE_TYPE=:pt, DESCRIPTION=:ds,
                           OWNER_VERTICAL=:ov, MODULE_OWNER=:mo, IP_NUMBER=:ip,
                           MAIL_ID=:mi, VERSION=:vr, STATUS=:st,
                           UPDATED_DATE=SYSDATE
                    WHERE  PAGE_KEY=:pk
                    """;
                await conn.ExecuteAsync(upd,
                    new { pn=f.PageName, pt=f.PageType, ds=f.Description,
                          ov=f.OwnerVertical, mo=f.ModuleOwner, ip=f.IpNumber,
                          mi=f.MailId, vr=f.Version, st=f.Status, pk=f.PageKey });
            }
            return true;
        }
        catch (Exception ex) { AppLogger.LogError(ex, $"OraclePageInfoService: UpsertAsync failed for PageKey={f.PageKey}"); return false; }
    }

    public async Task<bool> DeleteAsync(string pageKey)
    {
        await using var conn = await Conn();
        try
        {
            await conn.ExecuteAsync(
                "DELETE FROM RP_PAGE_INFO WHERE PAGE_KEY = :pk",
                new { pk = pageKey });
            return true;
        }
        catch (Exception ex) { AppLogger.LogError(ex, $"OraclePageInfoService: DeleteAsync failed for PageKey={pageKey}"); return false; }
    }

    private static PageInfoRecord Map(dynamic r) => new(
        (string)r.PAGE_KEY,    (string)r.PAGE_NAME,       (string)r.PAGE_TYPE,
        (string)r.DESCRIPTION, (string)r.OWNER_VERTICAL,  (string)r.MODULE_OWNER,
        (string)r.IP_NUMBER,   (string)r.MAIL_ID,
        (DateTime)r.CREATED_DATE, (string)r.VERSION, (string)r.STATUS);
}
