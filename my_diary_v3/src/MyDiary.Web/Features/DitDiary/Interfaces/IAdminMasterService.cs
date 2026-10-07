using MyDiary.Web.Features.DitDiary.Models.Entities;

namespace MyDiary.Web.Features.DitDiary.Interfaces
{
    public interface IAdminMasterService
    {
        Task<List<Department>> GetDepartmentsAsync(bool includeInactive = true);
        Task<List<Section>> GetSectionsAsync(bool includeInactive = true);
        Task SaveDepartmentAsync(Department dept, bool isNew);
        Task SaveSectionAsync(Section section, bool isNew);
        Task ToggleDepartmentStatusAsync(long deptId);
        Task ToggleSectionStatusAsync(long sectionId);
        Task<List<ZoneMaster>> GetZonesAsync();
        Task<List<RegionMaster>> GetRegionsByZoneAsync(string zoneCode);
        Task<List<BranchMaster>> GetBranchesByRegionAsync(string regionCode);
        Task<DitStaffDetail?> GetStaffByPfAsync(string pf);
        Task<(string? CM, string? AGM, string? DGM, string? GM, string? CGM)> GetLeadersAsync(long sectionId);
        Task SaveLeadersAsync(long sectionId, string? cm, string? agm, string? dgm, string? gm, string? cgm);
        Task<List<SectionTeamMember>> GetMembersAsync(long sectionId);
        Task AddMemberAsync(long sectionId, string pf, string name, string? role);
        Task RemoveMemberAsync(long sectionId, string pf);
        Task<List<DitStaffDetail>> GetStaffByPfListAsync(List<string> pfs);
        Task<(List<long> DeptIds, List<long> SectionIds)> GetUserScopeAsync(string pf);
        Task<int> CountSubmissionsByPfAsync(string pf);
        Task UpdateMemberRoleAsync(long sectionId, string pf, string? role);

    }
}