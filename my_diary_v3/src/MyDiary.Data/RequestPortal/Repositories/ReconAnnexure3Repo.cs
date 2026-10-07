using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>Raw-Oracle repo for RP_RECON_ANNEXURE3 — see db/recon/002_recon_annexure_schema.sql.</summary>
public sealed class ReconAnnexure3Repo : IReconAnnexure3Repo
{
    private readonly AdoUnitOfWork _uow;

    public ReconAnnexure3Repo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<bool> ExistsAsync(string vertical, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT COUNT(*) FROM RP_RECON_ANNEXURE3 WHERE VERTICAL=:p_v AND REPORT_MONTH=:p_m AND REPORT_YEAR=:p_y",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_v", vertical); cmd.Parameters.AddIn("p_m", reportMonth); cmd.Parameters.AddIn("p_y", reportYear);
        return await cmd.ScalarAsync<int>(ct) > 0;
    }

    public async Task<ReconAnnexure3?> GetAsync(string vertical, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT ID,REPORT_ID,REPORT_MONTH,REPORT_YEAR,VERTICAL,
                   SUN_UPTO3M_ENTRIES,SUN_UPTO3M_AMOUNT,SUN_THREE6M_ENTRIES,SUN_THREE6M_AMOUNT,SUN_OVER6M_ENTRIES,SUN_OVER6M_AMOUNT,
                   SUN_NEWOVER6M_ENTRIES,SUN_NEWOVER6M_AMOUNT,SUN_REASON,SUN_EFFORTS,
                   SUS_UPTO3M_ENTRIES,SUS_UPTO3M_AMOUNT,SUS_THREE6M_ENTRIES,SUS_THREE6M_AMOUNT,SUS_OVER6M_ENTRIES,SUS_OVER6M_AMOUNT,
                   SUS_NEWOVER6M_ENTRIES,SUS_NEWOVER6M_AMOUNT,SUS_REASON,SUS_EFFORTS,
                   POB_UPTO3M_ENTRIES,POB_UPTO3M_AMOUNT,POB_THREE6M_ENTRIES,POB_THREE6M_AMOUNT,POB_OVER6M_ENTRIES,POB_OVER6M_AMOUNT,
                   POB_NEWOVER6M_ENTRIES,POB_NEWOVER6M_AMOUNT,POB_REASON,POB_EFFORTS,
                   IBK_UPTO3M_ENTRIES,IBK_UPTO3M_AMOUNT,IBK_THREE6M_ENTRIES,IBK_THREE6M_AMOUNT,IBK_OVER6M_ENTRIES,IBK_OVER6M_AMOUNT,
                   IBK_NEWOVER6M_ENTRIES,IBK_NEWOVER6M_AMOUNT,IBK_REASON,IBK_EFFORTS,
                   DEL_FLAG,ENTRY_BY,ENTRY_DATE,STATUS
            FROM RP_RECON_ANNEXURE3 WHERE VERTICAL=:p_v AND REPORT_MONTH=:p_m AND REPORT_YEAR=:p_y", _uow.OracleTx);
        cmd.Parameters.AddIn("p_v", vertical); cmd.Parameters.AddIn("p_m", reportMonth); cmd.Parameters.AddIn("p_y", reportYear);
        return await cmd.QueryOneAsync(r => new ReconAnnexure3
        {
            Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), ReportMonth = Str(r, "REPORT_MONTH"), ReportYear = Str(r, "REPORT_YEAR"), Vertical = Str(r, "VERTICAL"),
            SunUpto3MEntries = LongN(r, "SUN_UPTO3M_ENTRIES"), SunUpto3MAmount = Dec(r, "SUN_UPTO3M_AMOUNT"),
            SunThree6MEntries = LongN(r, "SUN_THREE6M_ENTRIES"), SunThree6MAmount = Dec(r, "SUN_THREE6M_AMOUNT"),
            SunOver6MEntries = LongN(r, "SUN_OVER6M_ENTRIES"), SunOver6MAmount = Dec(r, "SUN_OVER6M_AMOUNT"),
            SunNewOver6MEntries = LongN(r, "SUN_NEWOVER6M_ENTRIES"), SunNewOver6MAmount = Dec(r, "SUN_NEWOVER6M_AMOUNT"),
            SunReason = StrN(r, "SUN_REASON"), SunEfforts = StrN(r, "SUN_EFFORTS"),
            SusUpto3MEntries = LongN(r, "SUS_UPTO3M_ENTRIES"), SusUpto3MAmount = Dec(r, "SUS_UPTO3M_AMOUNT"),
            SusThree6MEntries = LongN(r, "SUS_THREE6M_ENTRIES"), SusThree6MAmount = Dec(r, "SUS_THREE6M_AMOUNT"),
            SusOver6MEntries = LongN(r, "SUS_OVER6M_ENTRIES"), SusOver6MAmount = Dec(r, "SUS_OVER6M_AMOUNT"),
            SusNewOver6MEntries = LongN(r, "SUS_NEWOVER6M_ENTRIES"), SusNewOver6MAmount = Dec(r, "SUS_NEWOVER6M_AMOUNT"),
            SusReason = StrN(r, "SUS_REASON"), SusEfforts = StrN(r, "SUS_EFFORTS"),
            PobUpto3MEntries = LongN(r, "POB_UPTO3M_ENTRIES"), PobUpto3MAmount = Dec(r, "POB_UPTO3M_AMOUNT"),
            PobThree6MEntries = LongN(r, "POB_THREE6M_ENTRIES"), PobThree6MAmount = Dec(r, "POB_THREE6M_AMOUNT"),
            PobOver6MEntries = LongN(r, "POB_OVER6M_ENTRIES"), PobOver6MAmount = Dec(r, "POB_OVER6M_AMOUNT"),
            PobNewOver6MEntries = LongN(r, "POB_NEWOVER6M_ENTRIES"), PobNewOver6MAmount = Dec(r, "POB_NEWOVER6M_AMOUNT"),
            PobReason = StrN(r, "POB_REASON"), PobEfforts = StrN(r, "POB_EFFORTS"),
            IbkUpto3MEntries = LongN(r, "IBK_UPTO3M_ENTRIES"), IbkUpto3MAmount = Dec(r, "IBK_UPTO3M_AMOUNT"),
            IbkThree6MEntries = LongN(r, "IBK_THREE6M_ENTRIES"), IbkThree6MAmount = Dec(r, "IBK_THREE6M_AMOUNT"),
            IbkOver6MEntries = LongN(r, "IBK_OVER6M_ENTRIES"), IbkOver6MAmount = Dec(r, "IBK_OVER6M_AMOUNT"),
            IbkNewOver6MEntries = LongN(r, "IBK_NEWOVER6M_ENTRIES"), IbkNewOver6MAmount = Dec(r, "IBK_NEWOVER6M_AMOUNT"),
            IbkReason = StrN(r, "IBK_REASON"), IbkEfforts = StrN(r, "IBK_EFFORTS"),
            DelFlag = Str(r, "DEL_FLAG"), EntryBy = StrN(r, "ENTRY_BY"), EntryDate = DtN(r, "ENTRY_DATE"), Status = StrN(r, "STATUS")
        }, ct);
    }

    public async Task InsertAsync(ReconAnnexure3 e, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_RECON_ANNEXURE3(
                ID,REPORT_ID,REPORT_MONTH,REPORT_YEAR,VERTICAL,
                SUN_UPTO3M_ENTRIES,SUN_UPTO3M_AMOUNT,SUN_THREE6M_ENTRIES,SUN_THREE6M_AMOUNT,SUN_OVER6M_ENTRIES,SUN_OVER6M_AMOUNT,
                SUN_NEWOVER6M_ENTRIES,SUN_NEWOVER6M_AMOUNT,SUN_REASON,SUN_EFFORTS,
                SUS_UPTO3M_ENTRIES,SUS_UPTO3M_AMOUNT,SUS_THREE6M_ENTRIES,SUS_THREE6M_AMOUNT,SUS_OVER6M_ENTRIES,SUS_OVER6M_AMOUNT,
                SUS_NEWOVER6M_ENTRIES,SUS_NEWOVER6M_AMOUNT,SUS_REASON,SUS_EFFORTS,
                POB_UPTO3M_ENTRIES,POB_UPTO3M_AMOUNT,POB_THREE6M_ENTRIES,POB_THREE6M_AMOUNT,POB_OVER6M_ENTRIES,POB_OVER6M_AMOUNT,
                POB_NEWOVER6M_ENTRIES,POB_NEWOVER6M_AMOUNT,POB_REASON,POB_EFFORTS,
                IBK_UPTO3M_ENTRIES,IBK_UPTO3M_AMOUNT,IBK_THREE6M_ENTRIES,IBK_THREE6M_AMOUNT,IBK_OVER6M_ENTRIES,IBK_OVER6M_AMOUNT,
                IBK_NEWOVER6M_ENTRIES,IBK_NEWOVER6M_AMOUNT,IBK_REASON,IBK_EFFORTS,
                DEL_FLAG,ENTRY_BY,ENTRY_DATE,STATUS)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_m,:p_y,:p_v,
                :sun1,:sun2,:sun3,:sun4,:sun5,:sun6,:sun7,:sun8,:sun9,:sun10,
                :sus1,:sus2,:sus3,:sus4,:sus5,:sus6,:sus7,:sus8,:sus9,:sus10,
                :pob1,:pob2,:pob3,:pob4,:pob5,:pob6,:pob7,:pob8,:pob9,:pob10,
                :ibk1,:ibk2,:ibk3,:ibk4,:ibk5,:ibk6,:ibk7,:ibk8,:ibk9,:ibk10,
                :p_del,:p_by,:p_at,:p_status)", _uow.OracleTx);

        cmd.Parameters.AddIn("p_rid", e.ReportId); cmd.Parameters.AddIn("p_m", e.ReportMonth);
        cmd.Parameters.AddIn("p_y", e.ReportYear); cmd.Parameters.AddIn("p_v", e.Vertical);

        cmd.Parameters.AddIn("sun1", e.SunUpto3MEntries); cmd.Parameters.AddIn("sun2", e.SunUpto3MAmount);
        cmd.Parameters.AddIn("sun3", e.SunThree6MEntries); cmd.Parameters.AddIn("sun4", e.SunThree6MAmount);
        cmd.Parameters.AddIn("sun5", e.SunOver6MEntries); cmd.Parameters.AddIn("sun6", e.SunOver6MAmount);
        cmd.Parameters.AddIn("sun7", e.SunNewOver6MEntries); cmd.Parameters.AddIn("sun8", e.SunNewOver6MAmount);
        cmd.Parameters.AddIn("sun9", OracleDbType.Varchar2, e.SunReason); cmd.Parameters.AddIn("sun10", OracleDbType.Varchar2, e.SunEfforts);

        cmd.Parameters.AddIn("sus1", e.SusUpto3MEntries); cmd.Parameters.AddIn("sus2", e.SusUpto3MAmount);
        cmd.Parameters.AddIn("sus3", e.SusThree6MEntries); cmd.Parameters.AddIn("sus4", e.SusThree6MAmount);
        cmd.Parameters.AddIn("sus5", e.SusOver6MEntries); cmd.Parameters.AddIn("sus6", e.SusOver6MAmount);
        cmd.Parameters.AddIn("sus7", e.SusNewOver6MEntries); cmd.Parameters.AddIn("sus8", e.SusNewOver6MAmount);
        cmd.Parameters.AddIn("sus9", OracleDbType.Varchar2, e.SusReason); cmd.Parameters.AddIn("sus10", OracleDbType.Varchar2, e.SusEfforts);

        cmd.Parameters.AddIn("pob1", e.PobUpto3MEntries); cmd.Parameters.AddIn("pob2", e.PobUpto3MAmount);
        cmd.Parameters.AddIn("pob3", e.PobThree6MEntries); cmd.Parameters.AddIn("pob4", e.PobThree6MAmount);
        cmd.Parameters.AddIn("pob5", e.PobOver6MEntries); cmd.Parameters.AddIn("pob6", e.PobOver6MAmount);
        cmd.Parameters.AddIn("pob7", e.PobNewOver6MEntries); cmd.Parameters.AddIn("pob8", e.PobNewOver6MAmount);
        cmd.Parameters.AddIn("pob9", OracleDbType.Varchar2, e.PobReason); cmd.Parameters.AddIn("pob10", OracleDbType.Varchar2, e.PobEfforts);

        cmd.Parameters.AddIn("ibk1", e.IbkUpto3MEntries); cmd.Parameters.AddIn("ibk2", e.IbkUpto3MAmount);
        cmd.Parameters.AddIn("ibk3", e.IbkThree6MEntries); cmd.Parameters.AddIn("ibk4", e.IbkThree6MAmount);
        cmd.Parameters.AddIn("ibk5", e.IbkOver6MEntries); cmd.Parameters.AddIn("ibk6", e.IbkOver6MAmount);
        cmd.Parameters.AddIn("ibk7", e.IbkNewOver6MEntries); cmd.Parameters.AddIn("ibk8", e.IbkNewOver6MAmount);
        cmd.Parameters.AddIn("ibk9", OracleDbType.Varchar2, e.IbkReason); cmd.Parameters.AddIn("ibk10", OracleDbType.Varchar2, e.IbkEfforts);

        cmd.Parameters.AddIn("p_del", e.DelFlag); cmd.Parameters.AddIn("p_by", e.EntryBy);
        cmd.Parameters.AddIn("p_at", e.EntryDate ?? DateTime.Now); cmd.Parameters.AddIn("p_status", e.Status);

        await cmd.ExecAsync(ct);
    }
}
