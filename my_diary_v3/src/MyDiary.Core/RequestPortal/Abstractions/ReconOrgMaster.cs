namespace RequestPortal.Core.Abstractions;

public sealed class ReconBranch
{
    public string SolId { get; set; } = "";
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? ZoneCode { get; set; }
    public string? ZoneName { get; set; }
}

/// <summary>Exact port of the source app's OrganisationsDBContext queries against BRANCH_MASTER /
/// REGION_MASTER / ZONE_MASTER (see db/recon/003_org_master_tables.sql) — same tables, same filter
/// logic, no changes. Used by the RO/ZO consolidation actions to know the full branch roster (so "has
/// everyone submitted" can be answered), which Annexure4 alone can't answer since it only has rows for
/// branches that HAVE submitted.</summary>
public interface IReconOrgMasterRepo
{
    /// <summary>Exact port of Annexure4C.razor's allBranches query:
    /// b.REGION_CODE == regionCode AND b.BRANCH_CODE != regionCode AND b.BRANCH_CODE != b.ZONE_CODE.</summary>
    Task<IReadOnlyList<ReconBranch>> ListBranchesInRegionAsync(string regionCode, CancellationToken ct = default);

    /// <summary>Exact port of ReconRO.razor's allROs query:
    /// b.BRANCH_CODE == b.REGION_CODE AND b.BRANCH_CODE != b.ZONE_CODE AND b.BRANCH_CODE NOT IN ('999999','000000').</summary>
    Task<IReadOnlyList<(string Code, string Name)>> ListAllRegionsAsync(CancellationToken ct = default);

    Task<string?> GetZoneNameForRegionAsync(string regionCode, CancellationToken ct = default);

    /// <summary>Exact port of Annexure5B.razor's regionCodes query: REGION_MASTER.ZONE_CODE == zoneCode.</summary>
    Task<IReadOnlyList<string>> ListRegionCodesInZoneAsync(string zoneCode, CancellationToken ct = default);

    /// <summary>Exact port of Annexure5B.razor's pendingROSummary region-name lookup.</summary>
    Task<IReadOnlyList<(string Code, string Name)>> GetRegionNamesAsync(IReadOnlyCollection<string> regionCodes, CancellationToken ct = default);

    /// <summary>Exact port of Annexure5B.razor's allBranches query at ZO level:
    /// b.ZONE_CODE == zoneCode AND b.BRANCH_CODE != b.REGION_CODE.</summary>
    Task<IReadOnlyList<ReconBranch>> ListBranchesInZoneAsync(string zoneCode, CancellationToken ct = default);
}
