using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>ADO/Oracle repo for the Back-Dated Deposit Approval workflow (RP_BACKDATED_DEPOSIT_* tables —
/// see db/deposits/001_backdated_deposit_schema.sql). UpdateProposalAsync is the optimistic-concurrency
/// gate: it fails (returns false) if ROW_VERSION has moved since the caller read the row, which
/// BackDatedDepositWorkflowService.TransitionAsync turns into DepositStaleStateException.</summary>
public sealed class BackDatedDepositRepo : IBackDatedDepositRepo
{
    private readonly AdoUnitOfWork _uow;

    public BackDatedDepositRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<long> InsertProposalAsync(BackDatedDepositProposal p, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_BACKDATED_DEPOSIT_PROPOSAL(
                ID,REF_NO,DEPOSIT_TYPE,PURPOSE,BRANCH_SOL_ID,BRANCH_NAME,RO_SOL_ID,RO_NAME,ZO_SOL_ID,ZO_NAME,
                CIF_ID,CUSTOMER_NAME,SCHEME,OPENING_OR_LAST_RENEWAL_DT,TENURE,MATURITY_DATE,CUSTOMER_REQUEST_DATE,
                REASON,STATUS,PENDING_LEVEL,SPOC_NAME,SPOC_EMAIL,SPOC_MOBILE,SPOC_IP,CREATED_BY_EMP,CREATED_AT,
                ROW_VERSION,MODIFIED_AT)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_ref,:p_dtype,:p_purpose,:p_brsol,:p_brname,:p_rosol,:p_roname,:p_zosol,:p_zoname,
                :p_cif,:p_cust,:p_scheme,:p_open,:p_tenure,:p_mat,:p_creq,
                :p_reason,:p_status,:p_pending,:p_sname,:p_semail,:p_smobile,:p_sip,:p_by,:p_at,
                0,:p_modat)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_ref", p.RefNo);
        cmd.Parameters.AddIn("p_dtype", p.DepositType.ToString());
        cmd.Parameters.AddIn("p_purpose", p.Purpose.ToString());
        cmd.Parameters.AddIn("p_brsol", p.BranchSolId);
        cmd.Parameters.AddIn("p_brname", p.BranchName);
        cmd.Parameters.AddIn("p_rosol", OracleDbType.Varchar2, p.RoSolId);
        cmd.Parameters.AddIn("p_roname", OracleDbType.Varchar2, p.RoName);
        cmd.Parameters.AddIn("p_zosol", OracleDbType.Varchar2, p.ZoSolId);
        cmd.Parameters.AddIn("p_zoname", OracleDbType.Varchar2, p.ZoName);
        cmd.Parameters.AddIn("p_cif", p.CifId);
        cmd.Parameters.AddIn("p_cust", p.CustomerName);
        cmd.Parameters.AddIn("p_scheme", p.Scheme);
        cmd.Parameters.AddIn("p_open", p.OpeningOrLastRenewalDate);
        cmd.Parameters.AddIn("p_tenure", p.Tenure);
        cmd.Parameters.AddIn("p_mat", p.MaturityDate);
        cmd.Parameters.AddIn("p_creq", p.CustomerRequestDate);
        cmd.Parameters.AddIn("p_reason", p.Reason);
        cmd.Parameters.AddIn("p_status", p.Status.ToString());
        cmd.Parameters.AddIn("p_pending", p.PendingLevel.ToString());
        cmd.Parameters.AddIn("p_sname", p.SpocName);
        cmd.Parameters.AddIn("p_semail", p.SpocEmail);
        cmd.Parameters.AddIn("p_smobile", p.SpocMobile);
        cmd.Parameters.AddIn("p_sip", p.SpocIp);
        cmd.Parameters.AddIn("p_by", p.CreatedByEmp);
        cmd.Parameters.AddIn("p_at", p.CreatedAt);
        cmd.Parameters.AddIn("p_modat", p.CreatedAt);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<BackDatedDepositProposal?> GetAsync(long id, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectProposalSql + " WHERE ID=:p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", id);
        var p = await cmd.QueryOneAsync(MapProposal, ct);
        if (p is not null) p.Accounts = (await ListAccountsAsync(id, ct)).ToList();
        return p;
    }

    public async Task<BackDatedDepositProposal?> GetByRefNoAsync(string refNo, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectProposalSql + " WHERE REF_NO=:p_ref", _uow.OracleTx);
        cmd.Parameters.AddIn("p_ref", refNo);
        var p = await cmd.QueryOneAsync(MapProposal, ct);
        if (p is not null) p.Accounts = (await ListAccountsAsync(p.Id, ct)).ToList();
        return p;
    }

    public async Task<bool> UpdateProposalAsync(BackDatedDepositProposal p, long expectedRowVersion, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            UPDATE RP_BACKDATED_DEPOSIT_PROPOSAL SET
                RO_SOL_ID=:p_rosol, RO_NAME=:p_roname, ZO_SOL_ID=:p_zosol, ZO_NAME=:p_zoname,
                REASON=:p_reason, STATUS=:p_status, PENDING_LEVEL=:p_pending,
                ROW_VERSION=ROW_VERSION+1, MODIFIED_AT=:p_modat
            WHERE ID=:p_id AND ROW_VERSION=:p_rv", _uow.OracleTx);
        cmd.Parameters.AddIn("p_rosol", OracleDbType.Varchar2, p.RoSolId);
        cmd.Parameters.AddIn("p_roname", OracleDbType.Varchar2, p.RoName);
        cmd.Parameters.AddIn("p_zosol", OracleDbType.Varchar2, p.ZoSolId);
        cmd.Parameters.AddIn("p_zoname", OracleDbType.Varchar2, p.ZoName);
        cmd.Parameters.AddIn("p_reason", p.Reason);
        cmd.Parameters.AddIn("p_status", p.Status.ToString());
        cmd.Parameters.AddIn("p_pending", p.PendingLevel.ToString());
        cmd.Parameters.AddIn("p_modat", DateTime.Now);
        cmd.Parameters.AddIn("p_id", p.Id);
        cmd.Parameters.AddIn("p_rv", expectedRowVersion);
        var rows = await cmd.ExecAsync(ct);
        if (rows > 0) p.RowVersion = expectedRowVersion + 1;
        return rows > 0;
    }

    public async Task<IReadOnlyList<BackDatedDepositAccount>> ListAccountsAsync(long proposalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,PROPOSAL_ID,ACCOUNT_NUMBER,AMOUNT FROM RP_BACKDATED_DEPOSIT_ACCOUNT WHERE PROPOSAL_ID=:p_id ORDER BY ID",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", proposalId);
        return await cmd.QueryAsync(r => new BackDatedDepositAccount
        {
            Id = Long(r, "ID"), ProposalId = Long(r, "PROPOSAL_ID"),
            AccountNumber = Str(r, "ACCOUNT_NUMBER"), Amount = Dec(r, "AMOUNT")
        }, ct);
    }

    public async Task ReplaceAccountsAsync(long proposalId, IReadOnlyList<BackDatedDepositAccount> accounts, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using (var del = _uow.OracleConn.Cmd("DELETE FROM RP_BACKDATED_DEPOSIT_ACCOUNT WHERE PROPOSAL_ID=:p_id", _uow.OracleTx))
        {
            del.Parameters.AddIn("p_id", proposalId);
            await del.ExecAsync(ct);
        }
        foreach (var a in accounts)
        {
            using var ins = _uow.OracleConn.Cmd(@"
                INSERT INTO RP_BACKDATED_DEPOSIT_ACCOUNT(ID,PROPOSAL_ID,ACCOUNT_NUMBER,AMOUNT)
                VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_pid,:p_acc,:p_amt)", _uow.OracleTx);
            ins.Parameters.AddIn("p_pid", proposalId);
            ins.Parameters.AddIn("p_acc", a.AccountNumber);
            ins.Parameters.AddIn("p_amt", a.Amount);
            await ins.ExecAsync(ct);
        }
    }

    public async Task<long> InsertActionAsync(BackDatedDepositAction a, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_BACKDATED_DEPOSIT_ACTION(
                ID,PROPOSAL_ID,ACTOR_EMP_CODE,ACTOR_ROLE,LEVEL_CODE,ACTION_CODE,REMARKS,
                SPOC_NAME,SPOC_EMAIL,SPOC_MOBILE,SPOC_IP,FROM_STATUS,TO_STATUS,ACTED_AT)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_pid,:p_emp,:p_role,:p_level,:p_action,:p_remarks,
                :p_sname,:p_semail,:p_smobile,:p_sip,:p_from,:p_to,:p_at)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_pid", a.ProposalId);
        cmd.Parameters.AddIn("p_emp", a.ActorEmpCode);
        cmd.Parameters.AddIn("p_role", a.ActorRole);
        cmd.Parameters.AddIn("p_level", a.Level);
        cmd.Parameters.AddIn("p_action", a.Action);
        cmd.Parameters.AddIn("p_remarks", OracleDbType.Varchar2, a.Remarks);
        cmd.Parameters.AddIn("p_sname", OracleDbType.Varchar2, a.SpocName);
        cmd.Parameters.AddIn("p_semail", OracleDbType.Varchar2, a.SpocEmail);
        cmd.Parameters.AddIn("p_smobile", OracleDbType.Varchar2, a.SpocMobile);
        cmd.Parameters.AddIn("p_sip", OracleDbType.Varchar2, a.SpocIp);
        cmd.Parameters.AddIn("p_from", a.FromStatus);
        cmd.Parameters.AddIn("p_to", a.ToStatus);
        cmd.Parameters.AddIn("p_at", a.ActedAt);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<IReadOnlyList<BackDatedDepositAction>> ListActionsAsync(long proposalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            SelectActionSql + " WHERE PROPOSAL_ID=:p_id ORDER BY ACTED_AT", _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", proposalId);
        return await cmd.QueryAsync(MapAction, ct);
    }

    public async Task<IReadOnlyList<BackDatedDepositAction>> ListAllActionsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectActionSql + " ORDER BY ACTED_AT DESC", _uow.OracleTx);
        return await cmd.QueryAsync(MapAction, ct);
    }

    public async Task<long> InsertDocumentAsync(BackDatedDepositDocument d, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            INSERT INTO RP_BACKDATED_DEPOSIT_DOCUMENT(ID,PROPOSAL_ID,LEVEL_CODE,FILE_NAME,STORAGE_KEY,UPLOADED_BY_EMP,UPLOADED_AT)
            VALUES(RP_SEQ_GLOBAL.NEXTVAL,:p_pid,:p_level,:p_fname,:p_key,:p_by,:p_at)
            RETURNING ID INTO :p_id", _uow.OracleTx);
        cmd.Parameters.AddIn("p_pid", d.ProposalId);
        cmd.Parameters.AddIn("p_level", d.Level);
        cmd.Parameters.AddIn("p_fname", d.FileName);
        cmd.Parameters.AddIn("p_key", d.StorageKey);
        cmd.Parameters.AddIn("p_by", d.UploadedByEmp);
        cmd.Parameters.AddIn("p_at", d.UploadedAt);
        var outId = cmd.Parameters.AddOut("p_id", OracleDbType.Int64);
        await cmd.ExecAsync(ct);
        return outId.OutId();
    }

    public async Task<IReadOnlyList<BackDatedDepositDocument>> ListDocumentsAsync(long proposalId, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ID,PROPOSAL_ID,LEVEL_CODE,FILE_NAME,STORAGE_KEY,UPLOADED_BY_EMP,UPLOADED_AT FROM RP_BACKDATED_DEPOSIT_DOCUMENT WHERE PROPOSAL_ID=:p_id ORDER BY UPLOADED_AT",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_id", proposalId);
        return await cmd.QueryAsync(r => new BackDatedDepositDocument
        {
            Id = Long(r, "ID"), ProposalId = Long(r, "PROPOSAL_ID"), Level = Str(r, "LEVEL_CODE"),
            FileName = Str(r, "FILE_NAME"), StorageKey = Str(r, "STORAGE_KEY"),
            UploadedByEmp = Str(r, "UPLOADED_BY_EMP"), UploadedAt = Dt(r, "UPLOADED_AT")
        }, ct);
    }

    public Task<IReadOnlyList<BackDatedDepositProposal>> ListByBranchAsync(string branchSolId, CancellationToken ct = default) =>
        QueryProposals("WHERE BRANCH_SOL_ID=:p_v ORDER BY CREATED_AT DESC", "p_v", branchSolId, ct);

    public Task<IReadOnlyList<BackDatedDepositProposal>> ListPendingAtRoAsync(string roSolId, CancellationToken ct = default) =>
        QueryProposals("WHERE RO_SOL_ID=:p_v AND PENDING_LEVEL='Ro' ORDER BY CREATED_AT", "p_v", roSolId, ct);

    public Task<IReadOnlyList<BackDatedDepositProposal>> ListPendingAtZoAsync(string zoSolId, CancellationToken ct = default) =>
        QueryProposals("WHERE ZO_SOL_ID=:p_v AND PENDING_LEVEL='Zo' ORDER BY CREATED_AT", "p_v", zoSolId, ct);

    public async Task<IReadOnlyList<BackDatedDepositProposal>> ListPendingAtDmdAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectProposalSql + " WHERE PENDING_LEVEL='Dmd' ORDER BY CREATED_AT", _uow.OracleTx);
        return await cmd.QueryAsync(MapProposal, ct);
    }

    public async Task<IReadOnlyList<BackDatedDepositProposal>> ListAllAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectProposalSql + " ORDER BY CREATED_AT DESC", _uow.OracleTx);
        return await cmd.QueryAsync(MapProposal, ct);
    }

    private async Task<IReadOnlyList<BackDatedDepositProposal>> QueryProposals(string whereClause, string paramName, string paramValue, CancellationToken ct)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(SelectProposalSql + " " + whereClause, _uow.OracleTx);
        cmd.Parameters.AddIn(paramName, paramValue);
        return await cmd.QueryAsync(MapProposal, ct);
    }

    private const string SelectProposalSql = @"
        SELECT ID,REF_NO,DEPOSIT_TYPE,PURPOSE,BRANCH_SOL_ID,BRANCH_NAME,RO_SOL_ID,RO_NAME,ZO_SOL_ID,ZO_NAME,
               CIF_ID,CUSTOMER_NAME,SCHEME,OPENING_OR_LAST_RENEWAL_DT,TENURE,MATURITY_DATE,CUSTOMER_REQUEST_DATE,
               REASON,STATUS,PENDING_LEVEL,SPOC_NAME,SPOC_EMAIL,SPOC_MOBILE,SPOC_IP,CREATED_BY_EMP,CREATED_AT,
               ROW_VERSION,MODIFIED_AT
        FROM RP_BACKDATED_DEPOSIT_PROPOSAL";

    private const string SelectActionSql = @"
        SELECT ID,PROPOSAL_ID,ACTOR_EMP_CODE,ACTOR_ROLE,LEVEL_CODE,ACTION_CODE,REMARKS,
               SPOC_NAME,SPOC_EMAIL,SPOC_MOBILE,SPOC_IP,FROM_STATUS,TO_STATUS,ACTED_AT
        FROM RP_BACKDATED_DEPOSIT_ACTION";

    private static BackDatedDepositProposal MapProposal(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), RefNo = Str(r, "REF_NO"),
        DepositType = Enum.Parse<DepositType>(Str(r, "DEPOSIT_TYPE")),
        Purpose = Enum.Parse<DepositPurpose>(Str(r, "PURPOSE")),
        BranchSolId = Str(r, "BRANCH_SOL_ID"), BranchName = Str(r, "BRANCH_NAME"),
        RoSolId = StrN(r, "RO_SOL_ID"), RoName = StrN(r, "RO_NAME"),
        ZoSolId = StrN(r, "ZO_SOL_ID"), ZoName = StrN(r, "ZO_NAME"),
        CifId = Str(r, "CIF_ID"), CustomerName = Str(r, "CUSTOMER_NAME"), Scheme = Str(r, "SCHEME"),
        OpeningOrLastRenewalDate = Dt(r, "OPENING_OR_LAST_RENEWAL_DT"), Tenure = Str(r, "TENURE"),
        MaturityDate = Dt(r, "MATURITY_DATE"), CustomerRequestDate = Dt(r, "CUSTOMER_REQUEST_DATE"),
        Reason = Str(r, "REASON"),
        Status = Enum.Parse<DepositProposalStatus>(Str(r, "STATUS")),
        PendingLevel = Enum.Parse<DepositPendingLevel>(Str(r, "PENDING_LEVEL")),
        SpocName = Str(r, "SPOC_NAME"), SpocEmail = Str(r, "SPOC_EMAIL"),
        SpocMobile = Str(r, "SPOC_MOBILE"), SpocIp = Str(r, "SPOC_IP"),
        CreatedByEmp = Str(r, "CREATED_BY_EMP"), CreatedAt = Dt(r, "CREATED_AT"),
        RowVersion = Long(r, "ROW_VERSION"), ModifiedAt = Dt(r, "MODIFIED_AT")
    };

    private static BackDatedDepositAction MapAction(OracleDataReader r) => new()
    {
        Id = Long(r, "ID"), ProposalId = Long(r, "PROPOSAL_ID"),
        ActorEmpCode = Str(r, "ACTOR_EMP_CODE"), ActorRole = Str(r, "ACTOR_ROLE"), Level = Str(r, "LEVEL_CODE"),
        Action = Str(r, "ACTION_CODE"), Remarks = StrN(r, "REMARKS"),
        SpocName = StrN(r, "SPOC_NAME"), SpocEmail = StrN(r, "SPOC_EMAIL"),
        SpocMobile = StrN(r, "SPOC_MOBILE"), SpocIp = StrN(r, "SPOC_IP"),
        FromStatus = Str(r, "FROM_STATUS"), ToStatus = Str(r, "TO_STATUS"), ActedAt = Dt(r, "ACTED_AT")
    };
}
