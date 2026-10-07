using System.Security.Claims;
using MyDiary.Web.Features.BcpDrill.Models;

namespace MyDiary.Web.Features.BcpDrill.Services;

/// <summary>
/// BCP / DR Drill Compliance. Every method derives the caller's scope from claims on the server; page
/// filters are intersected with that scope, and every write re-reads the target's real zone/region/branch.
/// </summary>
public interface IBcpDrillService
{
    BcpScope GetScope(ClaimsPrincipal user);

    Task<List<OrgOption>> GetZonesAsync(ClaimsPrincipal user);
    Task<List<OrgOption>> GetRegionsAsync(ClaimsPrincipal user, string? zoneCode);
    Task<List<OrgOption>> GetBranchesAsync(ClaimsPrincipal user, string? regionCode);

    // Branch map
    Task<List<BcpBranch>> ListBranchMapAsync(ClaimsPrincipal user, string? regionCode = null, bool includeInactive = false);
    Task<BcpBranch?> GetMyBranchAsync(ClaimsPrincipal user);
    /// <summary>Other active branches in the same region as a branch the caller may act for (for the alternate-site picker).</summary>
    Task<List<BcpBranch>> GetPeerBranchesAsync(ClaimsPrincipal user, string solId);
    Task SaveBranchAsync(ClaimsPrincipal user, BcpBranch branch);
    Task<int> LoadBranchesFromMasterAsync(ClaimsPrincipal user);

    // Annexure-I
    Task<List<BcpDrillEntry>> ListDrillsAsync(ClaimsPrincipal user, BcpDrillFilter filter);
    Task<BcpDrillEntry?> GetDrillAsync(ClaimsPrincipal user, long id);
    Task<long> SaveDrillAsync(ClaimsPrincipal user, BcpDrillEntry drill, byte[]? docBytes, string? docType, string? docName);
    Task ReviewDrillAsync(ClaimsPrincipal user, long id, bool acknowledge, string? remarks);
    Task DeleteDrillAsync(ClaimsPrincipal user, long id);
    Task<(byte[] Bytes, string ContentType, string FileName)?> GetDrillDocAsync(ClaimsPrincipal user, long id);

    // Annexure-II
    Task<List<BcpRoCert>> ListCertsAsync(ClaimsPrincipal user, string? regionCode = null);
    Task<BcpRoCert?> GetCertAsync(ClaimsPrincipal user, long id);
    Task<BcpRoCert> BuildCertDraftAsync(ClaimsPrincipal user, string regionCode);
    Task<long> SaveCertAsync(ClaimsPrincipal user, BcpRoCert cert);

    // MIS
    Task<List<BcpBranchMisRow>> GetBranchMisAsync(ClaimsPrincipal user, BcpDrillFilter filter);
    Task<List<BcpZoneMisRow>> GetRegionMisAsync(ClaimsPrincipal user, BcpDrillFilter filter);
    Task<List<BcpPeriodRow>> GetPeriodMisAsync(ClaimsPrincipal user, BcpDrillFilter filter);

    // Audit
    Task<List<BcpAuditRow>> ListAuditAsync(ClaimsPrincipal user, string? entity, string? entityId, int max = 300);
}
