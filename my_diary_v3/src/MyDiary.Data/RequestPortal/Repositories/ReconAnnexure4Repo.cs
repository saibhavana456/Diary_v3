using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>ADO/Oracle repo for Recon Certificates' Annexure 4 (RP_RECON_ANNEXURE4 + 6 child breakup
/// tables — see db/recon/002_recon_annexure_schema.sql). SaveAsync mirrors the source app's ProcessSave
/// exactly: on edit, ALL child rows for the REPORT_ID are deleted and re-inserted from scratch (not
/// diffed) — same strategy, same risk profile, kept identical rather than "improved" per instruction.</summary>
public sealed class ReconAnnexure4Repo : IReconAnnexure4Repo
{
    private const string SelectMainSql = @"
        SELECT ID,REPORT_ID,REPORT_MONTH,REPORT_YEAR,BRANCH_CODE,BRANCH_HEAD,BRANCH_NAME,MOBILE_NO,IP_NO,
               BR_MAINTAIN_IB_ACC,BR_MAINTAIN_IB_ACC_NOT_RECON,
               OUR_DEBIT_ENTRIES_1M,OUR_DEBIT_AMOUNT_1M,OUR_CREDIT_ENTRIES_1M,OUR_CREDIT_AMOUNT_1M,
               THEIR_DEBIT_ENTRIES_1M,THEIR_DEBIT_AMOUNT_1M,THEIR_CREDIT_ENTRIES_1M,THEIR_CREDIT_AMOUNT_1M,
               OUR_DEBIT_ENTRIES_3M,OUR_DEBIT_AMOUNT_3M,OUR_CREDIT_ENTRIES_3M,OUR_CREDIT_AMOUNT_3M,
               THEIR_DEBIT_ENTRIES_3M,THEIR_DEBIT_AMOUNT_3M,THEIR_CREDIT_ENTRIES_3M,THEIR_CREDIT_AMOUNT_3M,
               OUR_DEBIT_ENTRIES_6M,OUR_DEBIT_AMOUNT_6M,OUR_CREDIT_ENTRIES_6M,OUR_CREDIT_AMOUNT_6M,
               THEIR_DEBIT_ENTRIES_6M,THEIR_DEBIT_AMOUNT_6M,THEIR_CREDIT_ENTRIES_6M,THEIR_CREDIT_AMOUNT_6M,
               OUR_DEBIT_ENTRIES_1Y,OUR_DEBIT_AMOUNT_1Y,OUR_CREDIT_ENTRIES_1Y,OUR_CREDIT_AMOUNT_1Y,
               THEIR_DEBIT_ENTRIES_1Y,THEIR_DEBIT_AMOUNT_1Y,THEIR_CREDIT_ENTRIES_1Y,THEIR_CREDIT_AMOUNT_1Y,
               OUR_DEBIT_ENTRIES_ABOVE_1Y,OUR_DEBIT_AMOUNT_ABOVE_1Y,OUR_CREDIT_ENTRIES_ABOVE_1Y,OUR_CREDIT_AMOUNT_ABOVE_1Y,
               THEIR_DEBIT_ENTRIES_ABOVE_1Y,THEIR_DEBIT_AMOUNT_ABOVE_1Y,THEIR_CREDIT_ENTRIES_ABOVE_1Y,THEIR_CREDIT_AMOUNT_ABOVE_1Y,
               SUN_UPTO3M_ENTRIES,SUN_UPTO3M_AMOUNT,SUN_THREE6M_ENTRIES,SUN_THREE6M_AMOUNT,SUN_OVER6M_ENTRIES,SUN_OVER6M_AMOUNT,
               SUN_NEWOVER6M_ENTRIES,SUN_NEWOVER6M_AMOUNT,SUN_REASON,SUN_EFFORTS,
               SUS_UPTO3M_ENTRIES,SUS_UPTO3M_AMOUNT,SUS_THREE6M_ENTRIES,SUS_THREE6M_AMOUNT,SUS_OVER6M_ENTRIES,SUS_OVER6M_AMOUNT,
               SUS_NEWOVER6M_ENTRIES,SUS_NEWOVER6M_AMOUNT,SUS_REASON,SUS_EFFORTS,
               POB_UPTO3M_ENTRIES,POB_UPTO3M_AMOUNT,POB_THREE6M_ENTRIES,POB_THREE6M_AMOUNT,POB_OVER6M_ENTRIES,POB_OVER6M_AMOUNT,
               POB_NEWOVER6M_ENTRIES,POB_NEWOVER6M_AMOUNT,POB_REASON,POB_EFFORTS,
               IBK_UPTO3M_ENTRIES,IBK_UPTO3M_AMOUNT,IBK_THREE6M_ENTRIES,IBK_THREE6M_AMOUNT,IBK_OVER6M_ENTRIES,IBK_OVER6M_AMOUNT,
               IBK_NEWOVER6M_ENTRIES,IBK_NEWOVER6M_AMOUNT,IBK_REASON,IBK_EFFORTS,
               DEL_FLAG,ENTRY_BY,ENTRY_DATE,MODIFY_BY,MODIFY_DATE,DEL_BY,DEL_DATE,STATUS,
               RO_USERID,RO_ACTIONDATE,RO_OPERATIONS_USERID,RO_OPERATIONS_ACTIONDATE,RO_CHECKER_USERID,RO_CHECKER_ACTIONDATE,
               ZO_OPERATIONS_USERID,ZO_OPERATIONS_ACTIONDATE,ZO_CHECKER_USERID,ZO_CHECKER_ACTIONDATE,RECON_USERID,RECON_ACTIONDATE,
               REPORT_TYPE
        FROM RP_RECON_ANNEXURE4";

    private readonly AdoUnitOfWork _uow;

    public ReconAnnexure4Repo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<ReconAnnexure4?> GetBranchEntryAsync(string branchCode, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectMainSql +
            " WHERE BRANCH_CODE=:p_br AND REPORT_MONTH=:p_m AND REPORT_YEAR=:p_y AND REPORT_TYPE='BRANCH' AND DEL_FLAG='N'",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_br", branchCode);
        cmd.Parameters.AddIn("p_m", reportMonth);
        cmd.Parameters.AddIn("p_y", reportYear);
        var entry = await cmd.QueryOneAsync(MapMain, ct);
        if (entry is not null) await LoadChildrenAsync(entry, ct);
        return entry;
    }

    private async Task LoadChildrenAsync(ReconAnnexure4 e, CancellationToken ct)
    {
        e.BreakupEntries = (await ListInterbankAsync(e.ReportId, ct)).ToList();
        e.SundryEntries = (await ListOsGt3MonAsync("RP_RECON_A4_SUNDRY_OS_GT_3M", e.ReportId, ct)).ToList();
        e.SuspenseEntries = (await ListOsGt3MonAsync("RP_RECON_A4_SUSPENSE_OS_GT_3M", e.ReportId, ct)).ToList();
        e.PobEntries = (await ListOsGt3MonAsync("RP_RECON_A4_POB_OS_GT_3M", e.ReportId, ct)).ToList();
        e.BooksNotTallied = (await ListBooksNotTalliedAsync(e.ReportId, ct)).ToList();
        var positions = (await ListPositionsAsync(e.ReportId, ct)).ToList();
        e.BrPositionReconciled = positions.Where(p => p.ReconciliationStatus == "Reconciled").ToList();
        e.BrPositionNotReconciled = positions.Where(p => p.ReconciliationStatus == "Not Reconciled").ToList();
    }

    private async Task<IReadOnlyList<ReconA4InterbankBreakup>> ListInterbankAsync(string reportId, CancellationToken ct)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,REPORT_ID,DATE_OF_ENTRY,BRANCH_NAME,INTER_BANK_NAME,OUR_DEBIT_AMOUNT,OUR_CREDIT_AMOUNT,THEIR_DEBIT_AMOUNT,THEIR_CREDIT_AMOUNT,TRANSACTION_NATURE_DETAILS FROM RP_RECON_A4_INTERBANK_BREAKUP WHERE REPORT_ID=:p_rid",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new ReconA4InterbankBreakup
        {
            Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), DateOfEntry = DtN(r, "DATE_OF_ENTRY"),
            BranchName = StrN(r, "BRANCH_NAME"), InterBankName = StrN(r, "INTER_BANK_NAME"),
            OurDebitAmount = Dec(r, "OUR_DEBIT_AMOUNT"), OurCreditAmount = Dec(r, "OUR_CREDIT_AMOUNT"),
            TheirDebitAmount = Dec(r, "THEIR_DEBIT_AMOUNT"), TheirCreditAmount = Dec(r, "THEIR_CREDIT_AMOUNT"),
            TransactionNatureDetails = StrN(r, "TRANSACTION_NATURE_DETAILS")
        }, ct);
    }

    private async Task<IReadOnlyList<ReconA4OsGt3MonBreakup>> ListOsGt3MonAsync(string table, string reportId, CancellationToken ct)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            $"SELECT ID,REPORT_ID,NATURE_OF_ENTRY,ORIGINATED_DATE,OUTSTANDING_AMOUNT_RUPEES,STATUS_REASON_PENDENCY,EXPECTED_REVERSAL_DATE FROM {table} WHERE REPORT_ID=:p_rid",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new ReconA4OsGt3MonBreakup
        {
            Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), NatureOfEntry = StrN(r, "NATURE_OF_ENTRY"),
            OriginatedDate = DtN(r, "ORIGINATED_DATE"), OutstandingAmountRupees = Dec(r, "OUTSTANDING_AMOUNT_RUPEES"),
            StatusReasonPendency = StrN(r, "STATUS_REASON_PENDENCY"), ExpectedReversalDate = DtN(r, "EXPECTED_REVERSAL_DATE")
        }, ct);
    }

    private async Task<IReadOnlyList<ReconA4BooksNotTallied>> ListBooksNotTalliedAsync(string reportId, CancellationToken ct)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,REPORT_ID,BRANCH_NAME,HEADS_NOT_TALLIED,NOT_TALLIED_SINCE_DATE,DIFFERENCE_AMOUNT_RUPEES,ACTION_INITIATED_RECON_EXPDATE,REASON FROM RP_RECON_A4_BOOKS_NOT_TALLIED WHERE REPORT_ID=:p_rid",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new ReconA4BooksNotTallied
        {
            Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), BranchName = StrN(r, "BRANCH_NAME"),
            HeadsNotTallied = StrN(r, "HEADS_NOT_TALLIED"), NotTalliedSinceDate = DtN(r, "NOT_TALLIED_SINCE_DATE"),
            DifferenceAmountRupees = Dec(r, "DIFFERENCE_AMOUNT_RUPEES"),
            ActionInitiatedReconExpDate = StrN(r, "ACTION_INITIATED_RECON_EXPDATE"), Reason = StrN(r, "REASON")
        }, ct);
    }

    private async Task<IReadOnlyList<ReconA4IbReconBrPosition>> ListPositionsAsync(string reportId, CancellationToken ct)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,REPORT_ID,IBR_ACCOUNT_BANK,IBR_ACCOUNT_BANK_BRANCH,RECONCILIATION_STATUS FROM RP_RECON_A4_IB_RECON_BR_POSITION WHERE REPORT_ID=:p_rid",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new ReconA4IbReconBrPosition
        {
            Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), IbrAccountBank = StrN(r, "IBR_ACCOUNT_BANK"),
            IbrAccountBankBranch = StrN(r, "IBR_ACCOUNT_BANK_BRANCH"), ReconciliationStatus = Str(r, "RECONCILIATION_STATUS")
        }, ct);
    }

    /// <summary>Exact same shape as the source app's ProcessSave: insert-or-update the main row, then
    /// unconditionally delete and re-insert every child row for that REPORT_ID, all in one transaction.</summary>
    public async Task SaveAsync(ReconAnnexure4 e, bool isEditMode, CancellationToken ct = default)
    {
        await _uow.BeginAsync(ct);
        var tx = _uow.OracleTx;
        try
        {
            if (!isEditMode)
            {
                using var ins = _uow.OracleConn.Cmd(@"
                    INSERT INTO RP_RECON_ANNEXURE4(
                        ID,REPORT_ID,REPORT_MONTH,REPORT_YEAR,BRANCH_CODE,BRANCH_HEAD,BRANCH_NAME,MOBILE_NO,IP_NO,
                        DEL_FLAG,ENTRY_BY,ENTRY_DATE,STATUS,REPORT_TYPE)
                    VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_m,:p_y,:p_br,:p_bh,:p_bn,:p_mob,:p_ip,
                        'N',:p_by,:p_at,:p_status,'BRANCH')", tx);
                ins.Parameters.AddIn("p_rid", e.ReportId); ins.Parameters.AddIn("p_m", e.ReportMonth);
                ins.Parameters.AddIn("p_y", e.ReportYear); ins.Parameters.AddIn("p_br", e.BranchCode);
                ins.Parameters.AddIn("p_bh", OracleDbType.Varchar2, e.BranchHead); ins.Parameters.AddIn("p_bn", e.BranchName);
                ins.Parameters.AddIn("p_mob", OracleDbType.Varchar2, e.MobileNo); ins.Parameters.AddIn("p_ip", OracleDbType.Varchar2, e.IpNo);
                ins.Parameters.AddIn("p_by", e.EntryBy); ins.Parameters.AddIn("p_at", e.EntryDate ?? DateTime.Now);
                ins.Parameters.AddIn("p_status", e.Status);
                await ins.ExecAsync(ct);
            }
            else
            {
                using var upd = _uow.OracleConn.Cmd(@"
                    UPDATE RP_RECON_ANNEXURE4 SET
                        BRANCH_HEAD=:p_bh, MOBILE_NO=:p_mob, IP_NO=:p_ip, STATUS=:p_status,
                        MODIFY_BY=:p_mby, MODIFY_DATE=:p_mat
                    WHERE REPORT_ID=:p_rid", tx);
                upd.Parameters.AddIn("p_bh", OracleDbType.Varchar2, e.BranchHead);
                upd.Parameters.AddIn("p_mob", OracleDbType.Varchar2, e.MobileNo);
                upd.Parameters.AddIn("p_ip", OracleDbType.Varchar2, e.IpNo);
                upd.Parameters.AddIn("p_status", e.Status);
                upd.Parameters.AddIn("p_mby", e.ModifyBy); upd.Parameters.AddIn("p_mat", e.ModifyDate ?? DateTime.Now);
                upd.Parameters.AddIn("p_rid", e.ReportId);
                await upd.ExecAsync(ct);

                // Delete-then-reinsert every child table for this REPORT_ID — same strategy as source.
                foreach (var table in new[]
                {
                    "RP_RECON_A4_INTERBANK_BREAKUP", "RP_RECON_A4_SUNDRY_OS_GT_3M", "RP_RECON_A4_SUSPENSE_OS_GT_3M",
                    "RP_RECON_A4_POB_OS_GT_3M", "RP_RECON_A4_BOOKS_NOT_TALLIED", "RP_RECON_A4_IB_RECON_BR_POSITION"
                })
                {
                    using var del = _uow.OracleConn.Cmd($"DELETE FROM {table} WHERE REPORT_ID=:p_rid", tx);
                    del.Parameters.AddIn("p_rid", e.ReportId);
                    await del.ExecAsync(ct);
                }
            }

            foreach (var item in e.BreakupEntries)
            {
                using var cmd = _uow.OracleConn.Cmd(@"
                    INSERT INTO RP_RECON_A4_INTERBANK_BREAKUP(ID,REPORT_ID,DATE_OF_ENTRY,BRANCH_NAME,INTER_BANK_NAME,OUR_DEBIT_AMOUNT,OUR_CREDIT_AMOUNT,THEIR_DEBIT_AMOUNT,THEIR_CREDIT_AMOUNT,TRANSACTION_NATURE_DETAILS)
                    VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_de,:p_bn,:p_ibn,:p_oda,:p_oca,:p_tda,:p_tca,:p_tnd)", tx);
                cmd.Parameters.AddIn("p_rid", e.ReportId); cmd.Parameters.AddIn("p_de", item.DateOfEntry ?? DateTime.Now);
                cmd.Parameters.AddIn("p_bn", item.BranchName ?? "N/A"); cmd.Parameters.AddIn("p_ibn", item.InterBankName ?? "N/A");
                cmd.Parameters.AddIn("p_oda", item.OurDebitAmount ?? 0); cmd.Parameters.AddIn("p_oca", item.OurCreditAmount ?? 0);
                cmd.Parameters.AddIn("p_tda", item.TheirDebitAmount ?? 0); cmd.Parameters.AddIn("p_tca", item.TheirCreditAmount ?? 0);
                cmd.Parameters.AddIn("p_tnd", item.TransactionNatureDetails ?? "N/A");
                await cmd.ExecAsync(ct);
            }

            await InsertOsGt3MonRangeAsync(tx, "RP_RECON_A4_SUNDRY_OS_GT_3M", e.ReportId, e.SundryEntries, ct);
            await InsertOsGt3MonRangeAsync(tx, "RP_RECON_A4_SUSPENSE_OS_GT_3M", e.ReportId, e.SuspenseEntries, ct);
            await InsertOsGt3MonRangeAsync(tx, "RP_RECON_A4_POB_OS_GT_3M", e.ReportId, e.PobEntries, ct);

            foreach (var item in e.BooksNotTallied)
            {
                using var cmd = _uow.OracleConn.Cmd(@"
                    INSERT INTO RP_RECON_A4_BOOKS_NOT_TALLIED(ID,REPORT_ID,BRANCH_NAME,HEADS_NOT_TALLIED,NOT_TALLIED_SINCE_DATE,DIFFERENCE_AMOUNT_RUPEES,ACTION_INITIATED_RECON_EXPDATE,REASON)
                    VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_bn,:p_hnt,:p_ntsd,:p_dar,:p_aire,:p_reason)", tx);
                cmd.Parameters.AddIn("p_rid", e.ReportId); cmd.Parameters.AddIn("p_bn", item.BranchName ?? "N/A");
                cmd.Parameters.AddIn("p_hnt", item.HeadsNotTallied ?? "N/A"); cmd.Parameters.AddIn("p_ntsd", item.NotTalliedSinceDate ?? DateTime.Now);
                cmd.Parameters.AddIn("p_dar", item.DifferenceAmountRupees ?? 0); cmd.Parameters.AddIn("p_aire", item.ActionInitiatedReconExpDate ?? "N/A");
                cmd.Parameters.AddIn("p_reason", item.Reason ?? "N/A");
                await cmd.ExecAsync(ct);
            }

            foreach (var item in e.BrPositionReconciled.Concat(e.BrPositionNotReconciled))
            {
                using var cmd = _uow.OracleConn.Cmd(@"
                    INSERT INTO RP_RECON_A4_IB_RECON_BR_POSITION(ID,REPORT_ID,IBR_ACCOUNT_BANK,IBR_ACCOUNT_BANK_BRANCH,RECONCILIATION_STATUS)
                    VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_bank,:p_branch,:p_status)", tx);
                cmd.Parameters.AddIn("p_rid", e.ReportId); cmd.Parameters.AddIn("p_bank", item.IbrAccountBank ?? "N/A");
                cmd.Parameters.AddIn("p_branch", item.IbrAccountBankBranch ?? "N/A"); cmd.Parameters.AddIn("p_status", item.ReconciliationStatus);
                await cmd.ExecAsync(ct);
            }

            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    private async Task InsertOsGt3MonRangeAsync(OracleTransaction? tx, string table, string reportId, List<ReconA4OsGt3MonBreakup> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            using var cmd = _uow.OracleConn.Cmd($@"
                INSERT INTO {table}(ID,REPORT_ID,NATURE_OF_ENTRY,ORIGINATED_DATE,OUTSTANDING_AMOUNT_RUPEES,STATUS_REASON_PENDENCY,EXPECTED_REVERSAL_DATE)
                VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_noe,:p_od,:p_amt,:p_srp,:p_erd)", tx);
            cmd.Parameters.AddIn("p_rid", reportId); cmd.Parameters.AddIn("p_noe", item.NatureOfEntry ?? "N/A");
            cmd.Parameters.AddIn("p_od", item.OriginatedDate ?? DateTime.Now); cmd.Parameters.AddIn("p_amt", item.OutstandingAmountRupees ?? 0);
            cmd.Parameters.AddIn("p_srp", item.StatusReasonPendency ?? "N/A"); cmd.Parameters.AddIn("p_erd", item.ExpectedReversalDate ?? DateTime.Now.AddMonths(1));
            await cmd.ExecAsync(ct);
        }
    }

    public async Task AppendActionHistoryAsync(ReconA4ActionHistory a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_RECON_A4_ACTION_HISTORY(ID,REPORT_ID,ACTION_LEVEL,ACTION_TYPE,USER_ID,ACTION_DATE,STATUS_AFTER,REMARKS,DEL_FLAG)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_level,:p_type,:p_uid,:p_at,:p_status,:p_remarks,'N')", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", a.ReportId); cmd.Parameters.AddIn("p_level", a.ActionLevel);
        cmd.Parameters.AddIn("p_type", a.ActionType); cmd.Parameters.AddIn("p_uid", a.UserId);
        cmd.Parameters.AddIn("p_at", a.ActionDate); cmd.Parameters.AddIn("p_status", OracleDbType.Varchar2, a.StatusAfter);
        cmd.Parameters.AddIn("p_remarks", OracleDbType.Varchar2, a.Remarks);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<ReconA4ActionHistory>> GetActionHistoryAsync(string reportId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,REPORT_ID,ACTION_LEVEL,ACTION_TYPE,USER_ID,ACTION_DATE,STATUS_AFTER,REMARKS FROM RP_RECON_A4_ACTION_HISTORY WHERE REPORT_ID=:p_rid AND DEL_FLAG='N' ORDER BY ACTION_DATE DESC",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_rid", reportId);
        return await cmd.QueryAsync(r => new ReconA4ActionHistory
        {
            Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), ActionLevel = Str(r, "ACTION_LEVEL"),
            ActionType = Str(r, "ACTION_TYPE"), UserId = Str(r, "USER_ID"), ActionDate = Dt(r, "ACTION_DATE"),
            StatusAfter = StrN(r, "STATUS_AFTER"), Remarks = StrN(r, "REMARKS")
        }, ct);
    }

    public async Task<IReadOnlyList<ReconAnnexure4>> ListSubmissionsAsync(string reportMonth, string reportYear, IReadOnlyCollection<string> branchCodes, IReadOnlyCollection<string> statuses, CancellationToken ct = default)
    {
        if (branchCodes.Count == 0 || statuses.Count == 0) return Array.Empty<ReconAnnexure4>();
        await _uow.EnsureConnectionAsync(ct);

        var branchParams = string.Join(",", branchCodes.Select((_, i) => $":p_br{i}"));
        var statusParams = string.Join(",", statuses.Select((_, i) => $":p_st{i}"));
        using var cmd = _uow.OracleConn.Cmd(SelectMainSql +
            $" WHERE REPORT_MONTH=:p_m AND REPORT_YEAR=:p_y AND REPORT_TYPE='BRANCH' AND DEL_FLAG='N'" +
            $" AND BRANCH_CODE IN ({branchParams}) AND STATUS IN ({statusParams})", _uow.OracleTx);
        cmd.Parameters.AddIn("p_m", reportMonth); cmd.Parameters.AddIn("p_y", reportYear);
        int bi = 0; foreach (var b in branchCodes) cmd.Parameters.AddIn($"p_br{bi++}", b);
        int si = 0; foreach (var s in statuses) cmd.Parameters.AddIn($"p_st{si++}", s);
        return await cmd.QueryAsync(MapMain, ct);
    }

    public async Task<ReconAnnexure4?> GetConsolidatedAsync(string reportType, string ownerCode, string reportMonth, string reportYear, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectMainSql +
            " WHERE REPORT_TYPE=:p_type AND BRANCH_CODE=:p_owner AND REPORT_MONTH=:p_m AND REPORT_YEAR=:p_y AND DEL_FLAG='N'",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_type", reportType); cmd.Parameters.AddIn("p_owner", ownerCode);
        cmd.Parameters.AddIn("p_m", reportMonth); cmd.Parameters.AddIn("p_y", reportYear);
        return await cmd.QueryOneAsync(MapMain, ct);
    }

    public async Task UpsertConsolidatedAsync(ReconAnnexure4 e, bool exists, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        if (!exists)
        {
            using var cmd = _uow.OracleConn.Cmd(@"
                INSERT INTO RP_RECON_ANNEXURE4(
                    ID,REPORT_ID,REPORT_TYPE,REPORT_MONTH,REPORT_YEAR,BRANCH_CODE,BRANCH_NAME,DEL_FLAG,ENTRY_BY,ENTRY_DATE,STATUS,
                    RO_OPERATIONS_USERID,RO_OPERATIONS_ACTIONDATE,ZO_OPERATIONS_USERID,ZO_OPERATIONS_ACTIONDATE,
                    BR_MAINTAIN_IB_ACC,BR_MAINTAIN_IB_ACC_NOT_RECON,
                    OUR_DEBIT_ENTRIES_1M,OUR_DEBIT_AMOUNT_1M,OUR_CREDIT_ENTRIES_1M,OUR_CREDIT_AMOUNT_1M,
                    THEIR_DEBIT_ENTRIES_1M,THEIR_DEBIT_AMOUNT_1M,THEIR_CREDIT_ENTRIES_1M,THEIR_CREDIT_AMOUNT_1M,
                    OUR_DEBIT_ENTRIES_3M,OUR_DEBIT_AMOUNT_3M,OUR_CREDIT_ENTRIES_3M,OUR_CREDIT_AMOUNT_3M,
                    THEIR_DEBIT_ENTRIES_3M,THEIR_DEBIT_AMOUNT_3M,THEIR_CREDIT_ENTRIES_3M,THEIR_CREDIT_AMOUNT_3M,
                    OUR_DEBIT_ENTRIES_6M,OUR_DEBIT_AMOUNT_6M,OUR_CREDIT_ENTRIES_6M,OUR_CREDIT_AMOUNT_6M,
                    THEIR_DEBIT_ENTRIES_6M,THEIR_DEBIT_AMOUNT_6M,THEIR_CREDIT_ENTRIES_6M,THEIR_CREDIT_AMOUNT_6M,
                    OUR_DEBIT_ENTRIES_1Y,OUR_DEBIT_AMOUNT_1Y,OUR_CREDIT_ENTRIES_1Y,OUR_CREDIT_AMOUNT_1Y,
                    THEIR_DEBIT_ENTRIES_1Y,THEIR_DEBIT_AMOUNT_1Y,THEIR_CREDIT_ENTRIES_1Y,THEIR_CREDIT_AMOUNT_1Y,
                    OUR_DEBIT_ENTRIES_ABOVE_1Y,OUR_DEBIT_AMOUNT_ABOVE_1Y,OUR_CREDIT_ENTRIES_ABOVE_1Y,OUR_CREDIT_AMOUNT_ABOVE_1Y,
                    THEIR_DEBIT_ENTRIES_ABOVE_1Y,THEIR_DEBIT_AMOUNT_ABOVE_1Y,THEIR_CREDIT_ENTRIES_ABOVE_1Y,THEIR_CREDIT_AMOUNT_ABOVE_1Y,
                    SUN_UPTO3M_ENTRIES,SUN_UPTO3M_AMOUNT,SUN_THREE6M_ENTRIES,SUN_THREE6M_AMOUNT,SUN_OVER6M_ENTRIES,SUN_OVER6M_AMOUNT,
                    SUN_NEWOVER6M_ENTRIES,SUN_NEWOVER6M_AMOUNT,SUN_REASON,SUN_EFFORTS,
                    SUS_UPTO3M_ENTRIES,SUS_UPTO3M_AMOUNT,SUS_THREE6M_ENTRIES,SUS_THREE6M_AMOUNT,SUS_OVER6M_ENTRIES,SUS_OVER6M_AMOUNT,
                    SUS_NEWOVER6M_ENTRIES,SUS_NEWOVER6M_AMOUNT,SUS_REASON,SUS_EFFORTS,
                    POB_UPTO3M_ENTRIES,POB_UPTO3M_AMOUNT,POB_THREE6M_ENTRIES,POB_THREE6M_AMOUNT,POB_OVER6M_ENTRIES,POB_OVER6M_AMOUNT,
                    POB_NEWOVER6M_ENTRIES,POB_NEWOVER6M_AMOUNT,POB_REASON,POB_EFFORTS,
                    IBK_UPTO3M_ENTRIES,IBK_UPTO3M_AMOUNT,IBK_THREE6M_ENTRIES,IBK_THREE6M_AMOUNT,IBK_OVER6M_ENTRIES,IBK_OVER6M_AMOUNT,
                    IBK_NEWOVER6M_ENTRIES,IBK_NEWOVER6M_AMOUNT,IBK_REASON,IBK_EFFORTS)
                VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_rid,:p_type,:p_m,:p_y,:p_owner,:p_bn,'N',:p_by,:p_at,:p_status,
                    :p_rouid,:p_roat,:p_zouid,:p_zoat,
                    :br1,:br2,
                    :od1m,:oda1m,:oc1m,:oca1m,:td1m,:tda1m,:tc1m,:tca1m,
                    :od3m,:oda3m,:oc3m,:oca3m,:td3m,:tda3m,:tc3m,:tca3m,
                    :od6m,:oda6m,:oc6m,:oca6m,:td6m,:tda6m,:tc6m,:tca6m,
                    :od1y,:oda1y,:oc1y,:oca1y,:td1y,:tda1y,:tc1y,:tca1y,
                    :oda1yy,:odaa1yy,:oca1yy,:ocaa1yy,:tda1yy,:tdaa1yy,:tca1yy,:tcaa1yy,
                    :sun1,:sun2,:sun3,:sun4,:sun5,:sun6,:sun7,:sun8,:sun9,:sun10,
                    :sus1,:sus2,:sus3,:sus4,:sus5,:sus6,:sus7,:sus8,:sus9,:sus10,
                    :pob1,:pob2,:pob3,:pob4,:pob5,:pob6,:pob7,:pob8,:pob9,:pob10,
                    :ibk1,:ibk2,:ibk3,:ibk4,:ibk5,:ibk6,:ibk7,:ibk8,:ibk9,:ibk10)", _uow.OracleTx);
            BindConsolidated(cmd, e);
            cmd.Parameters.AddIn("p_rid", e.ReportId); cmd.Parameters.AddIn("p_type", e.ReportType);
            cmd.Parameters.AddIn("p_m", e.ReportMonth); cmd.Parameters.AddIn("p_y", e.ReportYear);
            cmd.Parameters.AddIn("p_owner", e.BranchCode); cmd.Parameters.AddIn("p_bn", e.BranchName);
            cmd.Parameters.AddIn("p_by", e.EntryBy); cmd.Parameters.AddIn("p_at", e.EntryDate ?? DateTime.Now);
            cmd.Parameters.AddIn("p_status", e.Status);
            cmd.Parameters.AddIn("p_rouid", OracleDbType.Varchar2, e.RoOperationsUserId);
            cmd.Parameters.AddIn("p_roat", e.RoOperationsActionDate);
            cmd.Parameters.AddIn("p_zouid", OracleDbType.Varchar2, e.ZoOperationsUserId);
            cmd.Parameters.AddIn("p_zoat", e.ZoOperationsActionDate);
            await cmd.ExecAsync(ct);
        }
        else
        {
            using var cmd = _uow.OracleConn.Cmd(@"
                UPDATE RP_RECON_ANNEXURE4 SET
                    STATUS=:p_status, RO_OPERATIONS_USERID=:p_rouid, RO_OPERATIONS_ACTIONDATE=:p_roat,
                    ZO_OPERATIONS_USERID=:p_zouid, ZO_OPERATIONS_ACTIONDATE=:p_zoat,
                    BR_MAINTAIN_IB_ACC=:br1, BR_MAINTAIN_IB_ACC_NOT_RECON=:br2,
                    OUR_DEBIT_ENTRIES_1M=:od1m, OUR_DEBIT_AMOUNT_1M=:oda1m, OUR_CREDIT_ENTRIES_1M=:oc1m, OUR_CREDIT_AMOUNT_1M=:oca1m,
                    THEIR_DEBIT_ENTRIES_1M=:td1m, THEIR_DEBIT_AMOUNT_1M=:tda1m, THEIR_CREDIT_ENTRIES_1M=:tc1m, THEIR_CREDIT_AMOUNT_1M=:tca1m,
                    OUR_DEBIT_ENTRIES_3M=:od3m, OUR_DEBIT_AMOUNT_3M=:oda3m, OUR_CREDIT_ENTRIES_3M=:oc3m, OUR_CREDIT_AMOUNT_3M=:oca3m,
                    THEIR_DEBIT_ENTRIES_3M=:td3m, THEIR_DEBIT_AMOUNT_3M=:tda3m, THEIR_CREDIT_ENTRIES_3M=:tc3m, THEIR_CREDIT_AMOUNT_3M=:tca3m,
                    OUR_DEBIT_ENTRIES_6M=:od6m, OUR_DEBIT_AMOUNT_6M=:oda6m, OUR_CREDIT_ENTRIES_6M=:oc6m, OUR_CREDIT_AMOUNT_6M=:oca6m,
                    THEIR_DEBIT_ENTRIES_6M=:td6m, THEIR_DEBIT_AMOUNT_6M=:tda6m, THEIR_CREDIT_ENTRIES_6M=:tc6m, THEIR_CREDIT_AMOUNT_6M=:tca6m,
                    OUR_DEBIT_ENTRIES_1Y=:od1y, OUR_DEBIT_AMOUNT_1Y=:oda1y, OUR_CREDIT_ENTRIES_1Y=:oc1y, OUR_CREDIT_AMOUNT_1Y=:oca1y,
                    THEIR_DEBIT_ENTRIES_1Y=:td1y, THEIR_DEBIT_AMOUNT_1Y=:tda1y, THEIR_CREDIT_ENTRIES_1Y=:tc1y, THEIR_CREDIT_AMOUNT_1Y=:tca1y,
                    OUR_DEBIT_ENTRIES_ABOVE_1Y=:oda1yy, OUR_DEBIT_AMOUNT_ABOVE_1Y=:odaa1yy, OUR_CREDIT_ENTRIES_ABOVE_1Y=:oca1yy, OUR_CREDIT_AMOUNT_ABOVE_1Y=:ocaa1yy,
                    THEIR_DEBIT_ENTRIES_ABOVE_1Y=:tda1yy, THEIR_DEBIT_AMOUNT_ABOVE_1Y=:tdaa1yy, THEIR_CREDIT_ENTRIES_ABOVE_1Y=:tca1yy, THEIR_CREDIT_AMOUNT_ABOVE_1Y=:tcaa1yy,
                    SUN_UPTO3M_ENTRIES=:sun1, SUN_UPTO3M_AMOUNT=:sun2, SUN_THREE6M_ENTRIES=:sun3, SUN_THREE6M_AMOUNT=:sun4,
                    SUN_OVER6M_ENTRIES=:sun5, SUN_OVER6M_AMOUNT=:sun6, SUN_NEWOVER6M_ENTRIES=:sun7, SUN_NEWOVER6M_AMOUNT=:sun8,
                    SUN_REASON=:sun9, SUN_EFFORTS=:sun10,
                    SUS_UPTO3M_ENTRIES=:sus1, SUS_UPTO3M_AMOUNT=:sus2, SUS_THREE6M_ENTRIES=:sus3, SUS_THREE6M_AMOUNT=:sus4,
                    SUS_OVER6M_ENTRIES=:sus5, SUS_OVER6M_AMOUNT=:sus6, SUS_NEWOVER6M_ENTRIES=:sus7, SUS_NEWOVER6M_AMOUNT=:sus8,
                    SUS_REASON=:sus9, SUS_EFFORTS=:sus10,
                    POB_UPTO3M_ENTRIES=:pob1, POB_UPTO3M_AMOUNT=:pob2, POB_THREE6M_ENTRIES=:pob3, POB_THREE6M_AMOUNT=:pob4,
                    POB_OVER6M_ENTRIES=:pob5, POB_OVER6M_AMOUNT=:pob6, POB_NEWOVER6M_ENTRIES=:pob7, POB_NEWOVER6M_AMOUNT=:pob8,
                    POB_REASON=:pob9, POB_EFFORTS=:pob10,
                    IBK_UPTO3M_ENTRIES=:ibk1, IBK_UPTO3M_AMOUNT=:ibk2, IBK_THREE6M_ENTRIES=:ibk3, IBK_THREE6M_AMOUNT=:ibk4,
                    IBK_OVER6M_ENTRIES=:ibk5, IBK_OVER6M_AMOUNT=:ibk6, IBK_NEWOVER6M_ENTRIES=:ibk7, IBK_NEWOVER6M_AMOUNT=:ibk8,
                    IBK_REASON=:ibk9, IBK_EFFORTS=:ibk10
                WHERE REPORT_ID=:p_rid", _uow.OracleTx);
            BindConsolidated(cmd, e);
            cmd.Parameters.AddIn("p_status", e.Status);
            cmd.Parameters.AddIn("p_rouid", OracleDbType.Varchar2, e.RoOperationsUserId);
            cmd.Parameters.AddIn("p_roat", e.RoOperationsActionDate);
            cmd.Parameters.AddIn("p_zouid", OracleDbType.Varchar2, e.ZoOperationsUserId);
            cmd.Parameters.AddIn("p_zoat", e.ZoOperationsActionDate);
            cmd.Parameters.AddIn("p_rid", e.ReportId);
            await cmd.ExecAsync(ct);
        }
    }

    private static void BindConsolidated(OracleCommand cmd, ReconAnnexure4 e)
    {
        cmd.Parameters.AddIn("br1", e.BrMaintainIbAcc); cmd.Parameters.AddIn("br2", e.BrMaintainIbAccNotRecon);
        cmd.Parameters.AddIn("od1m", e.OurDebitEntries1M); cmd.Parameters.AddIn("oda1m", e.OurDebitAmount1M);
        cmd.Parameters.AddIn("oc1m", e.OurCreditEntries1M); cmd.Parameters.AddIn("oca1m", e.OurCreditAmount1M);
        cmd.Parameters.AddIn("td1m", e.TheirDebitEntries1M); cmd.Parameters.AddIn("tda1m", e.TheirDebitAmount1M);
        cmd.Parameters.AddIn("tc1m", e.TheirCreditEntries1M); cmd.Parameters.AddIn("tca1m", e.TheirCreditAmount1M);
        cmd.Parameters.AddIn("od3m", e.OurDebitEntries3M); cmd.Parameters.AddIn("oda3m", e.OurDebitAmount3M);
        cmd.Parameters.AddIn("oc3m", e.OurCreditEntries3M); cmd.Parameters.AddIn("oca3m", e.OurCreditAmount3M);
        cmd.Parameters.AddIn("td3m", e.TheirDebitEntries3M); cmd.Parameters.AddIn("tda3m", e.TheirDebitAmount3M);
        cmd.Parameters.AddIn("tc3m", e.TheirCreditEntries3M); cmd.Parameters.AddIn("tca3m", e.TheirCreditAmount3M);
        cmd.Parameters.AddIn("od6m", e.OurDebitEntries6M); cmd.Parameters.AddIn("oda6m", e.OurDebitAmount6M);
        cmd.Parameters.AddIn("oc6m", e.OurCreditEntries6M); cmd.Parameters.AddIn("oca6m", e.OurCreditAmount6M);
        cmd.Parameters.AddIn("td6m", e.TheirDebitEntries6M); cmd.Parameters.AddIn("tda6m", e.TheirDebitAmount6M);
        cmd.Parameters.AddIn("tc6m", e.TheirCreditEntries6M); cmd.Parameters.AddIn("tca6m", e.TheirCreditAmount6M);
        cmd.Parameters.AddIn("od1y", e.OurDebitEntries1Y); cmd.Parameters.AddIn("oda1y", e.OurDebitAmount1Y);
        cmd.Parameters.AddIn("oc1y", e.OurCreditEntries1Y); cmd.Parameters.AddIn("oca1y", e.OurCreditAmount1Y);
        cmd.Parameters.AddIn("td1y", e.TheirDebitEntries1Y); cmd.Parameters.AddIn("tda1y", e.TheirDebitAmount1Y);
        cmd.Parameters.AddIn("tc1y", e.TheirCreditEntries1Y); cmd.Parameters.AddIn("tca1y", e.TheirCreditAmount1Y);
        cmd.Parameters.AddIn("oda1yy", e.OurDebitEntriesAbove1Y); cmd.Parameters.AddIn("odaa1yy", e.OurDebitAmountAbove1Y);
        cmd.Parameters.AddIn("oca1yy", e.OurCreditEntriesAbove1Y); cmd.Parameters.AddIn("ocaa1yy", e.OurCreditAmountAbove1Y);
        cmd.Parameters.AddIn("tda1yy", e.TheirDebitEntriesAbove1Y); cmd.Parameters.AddIn("tdaa1yy", e.TheirDebitAmountAbove1Y);
        cmd.Parameters.AddIn("tca1yy", e.TheirCreditEntriesAbove1Y); cmd.Parameters.AddIn("tcaa1yy", e.TheirCreditAmountAbove1Y);
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
    }

    public async Task MarkBranchesForwardedToRoAsync(IReadOnlyCollection<string> reportIds, string actorEmpCode, CancellationToken ct = default)
    {
        if (reportIds.Count == 0) return;
        await _uow.EnsureConnectionAsync(ct);
        var now = DateTime.Now;
        foreach (var rid in reportIds)
        {
            using var cmd = _uow.OracleConn.Cmd(
                "UPDATE RP_RECON_ANNEXURE4 SET STATUS='102', RO_OPERATIONS_USERID=:p_uid, RO_OPERATIONS_ACTIONDATE=:p_at WHERE REPORT_ID=:p_rid",
                _uow.OracleTx);
            cmd.Parameters.AddIn("p_uid", actorEmpCode); cmd.Parameters.AddIn("p_at", now); cmd.Parameters.AddIn("p_rid", rid);
            await cmd.ExecAsync(ct);
        }
    }

    public async Task MarkConsolidatedForwardedToZoAsync(string reportId, string actorEmpCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_RECON_ANNEXURE4 SET STATUS='103', RO_CHECKER_USERID=:p_uid, RO_CHECKER_ACTIONDATE=:p_at WHERE REPORT_ID=:p_rid",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_uid", actorEmpCode); cmd.Parameters.AddIn("p_at", DateTime.Now); cmd.Parameters.AddIn("p_rid", reportId);
        await cmd.ExecAsync(ct);
    }

    public async Task<IReadOnlyList<ReconAnnexure4>> ListConsolidatedAsync(string reportType, IReadOnlyCollection<string> ownerCodes, string reportMonth, string reportYear, IReadOnlyCollection<string> statuses, CancellationToken ct = default)
    {
        if (ownerCodes.Count == 0 || statuses.Count == 0) return Array.Empty<ReconAnnexure4>();
        await _uow.EnsureConnectionAsync(ct);

        var ownerParams = string.Join(",", ownerCodes.Select((_, i) => $":p_o{i}"));
        var statusParams = string.Join(",", statuses.Select((_, i) => $":p_s{i}"));
        using var cmd = _uow.OracleConn.Cmd(SelectMainSql +
            $" WHERE REPORT_TYPE=:p_type AND REPORT_MONTH=:p_m AND REPORT_YEAR=:p_y AND DEL_FLAG='N'" +
            $" AND BRANCH_CODE IN ({ownerParams}) AND STATUS IN ({statusParams})", _uow.OracleTx);
        cmd.Parameters.AddIn("p_type", reportType); cmd.Parameters.AddIn("p_m", reportMonth); cmd.Parameters.AddIn("p_y", reportYear);
        int oi = 0; foreach (var o in ownerCodes) cmd.Parameters.AddIn($"p_o{oi++}", o);
        int si = 0; foreach (var s in statuses) cmd.Parameters.AddIn($"p_s{si++}", s);
        return await cmd.QueryAsync(MapMain, ct);
    }

    public async Task MarkRoRecordsForwardedToZoAsync(IReadOnlyCollection<string> reportIds, string actorEmpCode, CancellationToken ct = default)
    {
        if (reportIds.Count == 0) return;
        await _uow.EnsureConnectionAsync(ct);
        var now = DateTime.Now;
        foreach (var rid in reportIds)
        {
            using var cmd = _uow.OracleConn.Cmd(
                "UPDATE RP_RECON_ANNEXURE4 SET STATUS='104', ZO_OPERATIONS_USERID=:p_uid, ZO_OPERATIONS_ACTIONDATE=:p_at WHERE REPORT_ID=:p_rid",
                _uow.OracleTx);
            cmd.Parameters.AddIn("p_uid", actorEmpCode); cmd.Parameters.AddIn("p_at", now); cmd.Parameters.AddIn("p_rid", rid);
            await cmd.ExecAsync(ct);
        }
    }

    public async Task MarkConsolidatedForwardedToReconAsync(string reportId, string actorEmpCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "UPDATE RP_RECON_ANNEXURE4 SET STATUS='100', ZO_CHECKER_USERID=:p_uid, ZO_CHECKER_ACTIONDATE=:p_at WHERE REPORT_ID=:p_rid",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_uid", actorEmpCode); cmd.Parameters.AddIn("p_at", DateTime.Now); cmd.Parameters.AddIn("p_rid", reportId);
        await cmd.ExecAsync(ct);
    }

    public async Task<int> CountUnbalancedBranchesAsync(string reportMonth, string reportYear, IReadOnlyCollection<string> branchCodes, CancellationToken ct = default)
    {
        if (branchCodes.Count == 0) return 0;
        await _uow.EnsureConnectionAsync(ct);
        var ps = string.Join(",", branchCodes.Select((_, i) => $":p_b{i}"));
        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT COUNT(DISTINCT b.REPORT_ID) FROM RP_RECON_A4_BOOKS_NOT_TALLIED b
            JOIN RP_RECON_ANNEXURE4 a ON a.REPORT_ID = b.REPORT_ID
            WHERE a.REPORT_MONTH=:p_m AND a.REPORT_YEAR=:p_y AND a.DEL_FLAG='N' AND a.BRANCH_CODE IN ({ps})",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_m", reportMonth); cmd.Parameters.AddIn("p_y", reportYear);
        int i = 0; foreach (var b in branchCodes) cmd.Parameters.AddIn($"p_b{i++}", b);
        return await cmd.ScalarAsync<int>(ct);
    }

    public async Task<IReadOnlyList<ReconA4InterbankBreakup>> ListInterbankBreakupForBranchesAsync(string reportMonth, string reportYear, IReadOnlyCollection<string> branchCodes, CancellationToken ct = default)
    {
        if (branchCodes.Count == 0) return Array.Empty<ReconA4InterbankBreakup>();
        await _uow.EnsureConnectionAsync(ct);
        var ps = string.Join(",", branchCodes.Select((_, i) => $":p_b{i}"));
        using var cmd = _uow.OracleConn.Cmd($@"
            SELECT ib.ID,ib.REPORT_ID,ib.DATE_OF_ENTRY,ib.BRANCH_NAME,ib.INTER_BANK_NAME,ib.OUR_DEBIT_AMOUNT,ib.OUR_CREDIT_AMOUNT,ib.THEIR_DEBIT_AMOUNT,ib.THEIR_CREDIT_AMOUNT,ib.TRANSACTION_NATURE_DETAILS
            FROM RP_RECON_A4_INTERBANK_BREAKUP ib
            JOIN RP_RECON_ANNEXURE4 a ON a.REPORT_ID = ib.REPORT_ID
            WHERE a.REPORT_MONTH=:p_m AND a.REPORT_YEAR=:p_y AND a.DEL_FLAG='N' AND a.REPORT_TYPE='BRANCH' AND a.BRANCH_CODE IN ({ps})
            ORDER BY ib.DATE_OF_ENTRY", _uow.OracleTx);
        cmd.Parameters.AddIn("p_m", reportMonth); cmd.Parameters.AddIn("p_y", reportYear);
        int i = 0; foreach (var b in branchCodes) cmd.Parameters.AddIn($"p_b{i++}", b);
        return await cmd.QueryAsync(r => new ReconA4InterbankBreakup
        {
            Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), DateOfEntry = DtN(r, "DATE_OF_ENTRY"),
            BranchName = StrN(r, "BRANCH_NAME"), InterBankName = StrN(r, "INTER_BANK_NAME"),
            OurDebitAmount = Dec(r, "OUR_DEBIT_AMOUNT"), OurCreditAmount = Dec(r, "OUR_CREDIT_AMOUNT"),
            TheirDebitAmount = Dec(r, "THEIR_DEBIT_AMOUNT"), TheirCreditAmount = Dec(r, "THEIR_CREDIT_AMOUNT"),
            TransactionNatureDetails = StrN(r, "TRANSACTION_NATURE_DETAILS")
        }, ct);
    }

    private static ReconAnnexure4 MapMain(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), ReportId = Str(r, "REPORT_ID"), ReportMonth = StrN(r, "REPORT_MONTH"), ReportYear = StrN(r, "REPORT_YEAR"),
        BranchCode = StrN(r, "BRANCH_CODE"), BranchHead = StrN(r, "BRANCH_HEAD"), BranchName = StrN(r, "BRANCH_NAME"),
        MobileNo = StrN(r, "MOBILE_NO"), IpNo = StrN(r, "IP_NO"),
        BrMaintainIbAcc = LongN(r, "BR_MAINTAIN_IB_ACC"), BrMaintainIbAccNotRecon = LongN(r, "BR_MAINTAIN_IB_ACC_NOT_RECON"),
        OurDebitEntries1M = LongN(r, "OUR_DEBIT_ENTRIES_1M"), OurDebitAmount1M = Dec(r, "OUR_DEBIT_AMOUNT_1M"),
        OurCreditEntries1M = LongN(r, "OUR_CREDIT_ENTRIES_1M"), OurCreditAmount1M = Dec(r, "OUR_CREDIT_AMOUNT_1M"),
        TheirDebitEntries1M = LongN(r, "THEIR_DEBIT_ENTRIES_1M"), TheirDebitAmount1M = Dec(r, "THEIR_DEBIT_AMOUNT_1M"),
        TheirCreditEntries1M = LongN(r, "THEIR_CREDIT_ENTRIES_1M"), TheirCreditAmount1M = Dec(r, "THEIR_CREDIT_AMOUNT_1M"),
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
        DelFlag = Str(r, "DEL_FLAG"), EntryBy = StrN(r, "ENTRY_BY"), EntryDate = DtN(r, "ENTRY_DATE"),
        ModifyBy = StrN(r, "MODIFY_BY"), ModifyDate = DtN(r, "MODIFY_DATE"), Status = StrN(r, "STATUS"),
        RoUserId = StrN(r, "RO_USERID"), RoActionDate = DtN(r, "RO_ACTIONDATE"),
        RoOperationsUserId = StrN(r, "RO_OPERATIONS_USERID"), RoOperationsActionDate = DtN(r, "RO_OPERATIONS_ACTIONDATE"),
        RoCheckerUserId = StrN(r, "RO_CHECKER_USERID"), RoCheckerActionDate = DtN(r, "RO_CHECKER_ACTIONDATE"),
        ZoOperationsUserId = StrN(r, "ZO_OPERATIONS_USERID"), ZoOperationsActionDate = DtN(r, "ZO_OPERATIONS_ACTIONDATE"),
        ZoCheckerUserId = StrN(r, "ZO_CHECKER_USERID"), ZoCheckerActionDate = DtN(r, "ZO_CHECKER_ACTIONDATE"),
        ReconUserId = StrN(r, "RECON_USERID"), ReconActionDate = DtN(r, "RECON_ACTIONDATE"),
        ReportType = Str(r, "REPORT_TYPE")
    };
}
