using RequestPortal.Core.Abstractions;
using static RequestPortal.Data.OracleHelper;

namespace RequestPortal.Data.Repositories;

/// <summary>Raw-Oracle port of the source app's OrganisationsDBContext queries against
/// BRANCH_MASTER/REGION_MASTER/ZONE_MASTER — see db/recon/003_org_master_tables.sql and
/// IReconOrgMasterRepo's own doc comment for why this exists.</summary>
public sealed class ReconOrgMasterRepo : IReconOrgMasterRepo
{
    private readonly AdoUnitOfWork _uow;

    public ReconOrgMasterRepo(AdoUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<ReconBranch>> ListBranchesInRegionAsync(string regionCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT SOL_ID,BRANCH_CODE,BRANCH_NAME,REGION_CODE,REGION_NAME,ZONE_CODE,ZONE_NAME
            FROM BRANCH_MASTER
            WHERE REGION_CODE=:p_region AND BRANCH_CODE<>:p_region AND BRANCH_CODE<>ZONE_CODE",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_region", regionCode);
        return await cmd.QueryAsync(r => new ReconBranch
        {
            SolId = Str(r, "SOL_ID"), BranchCode = StrN(r, "BRANCH_CODE"), BranchName = StrN(r, "BRANCH_NAME"),
            RegionCode = StrN(r, "REGION_CODE"), RegionName = StrN(r, "REGION_NAME"),
            ZoneCode = StrN(r, "ZONE_CODE"), ZoneName = StrN(r, "ZONE_NAME")
        }, ct);
    }

    public async Task<IReadOnlyList<(string Code, string Name)>> ListAllRegionsAsync(CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT DISTINCT REGION_CODE, REGION_NAME FROM BRANCH_MASTER
            WHERE BRANCH_CODE=REGION_CODE AND BRANCH_CODE<>ZONE_CODE AND BRANCH_CODE NOT IN ('999999','000000')",
            _uow.OracleTx);
        var rows = await cmd.QueryAsync(r => (Str(r, "REGION_CODE"), Str(r, "REGION_NAME")), ct);
        return rows;
    }

    public async Task<string?> GetZoneNameForRegionAsync(string regionCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT ZONE_NAME FROM REGION_MASTER WHERE REGION_CODE=:p_region", _uow.OracleTx);
        cmd.Parameters.AddIn("p_region", regionCode);
        return await cmd.ScalarAsync<string>(ct);
    }

    public async Task<IReadOnlyList<ReconBranch>> ListBranchesInZoneAsync(string zoneCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(@"
            SELECT SOL_ID,BRANCH_CODE,BRANCH_NAME,REGION_CODE,REGION_NAME,ZONE_CODE,ZONE_NAME
            FROM BRANCH_MASTER
            WHERE ZONE_CODE=:p_zone AND BRANCH_CODE<>REGION_CODE",
            _uow.OracleTx);
        cmd.Parameters.AddIn("p_zone", zoneCode);
        return await cmd.QueryAsync(r => new ReconBranch
        {
            SolId = Str(r, "SOL_ID"), BranchCode = StrN(r, "BRANCH_CODE"), BranchName = StrN(r, "BRANCH_NAME"),
            RegionCode = StrN(r, "REGION_CODE"), RegionName = StrN(r, "REGION_NAME"),
            ZoneCode = StrN(r, "ZONE_CODE"), ZoneName = StrN(r, "ZONE_NAME")
        }, ct);
    }

    public async Task<IReadOnlyList<string>> ListRegionCodesInZoneAsync(string zoneCode, CancellationToken ct = default)
    {
        await _uow.EnsureConnectionAsync(ct);
        using var cmd = _uow.OracleConn.Cmd(
            "SELECT REGION_CODE FROM REGION_MASTER WHERE ZONE_CODE=:p_zone", _uow.OracleTx);
        cmd.Parameters.AddIn("p_zone", zoneCode);
        return await cmd.QueryAsync(r => Str(r, "REGION_CODE"), ct);
    }

    public async Task<IReadOnlyList<(string Code, string Name)>> GetRegionNamesAsync(IReadOnlyCollection<string> regionCodes, CancellationToken ct = default)
    {
        if (regionCodes.Count == 0) return Array.Empty<(string, string)>();
        await _uow.EnsureConnectionAsync(ct);
        var ps = string.Join(",", regionCodes.Select((_, i) => $":p_r{i}"));
        using var cmd = _uow.OracleConn.Cmd($"SELECT REGION_CODE, REGION_NAME FROM REGION_MASTER WHERE REGION_CODE IN ({ps})", _uow.OracleTx);
        int i = 0; foreach (var c in regionCodes) cmd.Parameters.AddIn($"p_r{i++}", c);
        return await cmd.QueryAsync(r => (Str(r, "REGION_CODE"), Str(r, "REGION_NAME")), ct);
    }
}
