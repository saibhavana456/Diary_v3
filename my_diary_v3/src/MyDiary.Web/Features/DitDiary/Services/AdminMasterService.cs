using MyDiary.Web.Features.DitDiary.Data;
using MyDiary.Web.Features.DitDiary.Interfaces;
using MyDiary.Web.Features.DitDiary.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyDiary.Web.Features.DitDiary.Services
{
    public class AdminMasterService : IAdminMasterService
    {
        private readonly DitDbContext _context;
        public AdminMasterService(DitDbContext context)
        {
            _context = context;
        }

        public async Task<List<Department>> GetDepartmentsAsync(bool includeInactive = true)
        {
            var q = _context.Departments.AsNoTracking().AsQueryable();
            if (!includeInactive) q = q.Where(d => d.STATUS == "Active");
            return await q.OrderBy(d => d.SORT_ORDER).ThenBy(d => d.DEPT_NAME).ToListAsync();
        }

        public async Task<List<Section>> GetSectionsAsync(bool includeInactive = true)
        {
            var q = _context.Sections.Include(s => s.Department).AsNoTracking().AsQueryable();
            if (!includeInactive) q = q.Where(s => s.STATUS == "Active");
            return await q.OrderBy(s => s.SORT_ORDER).ThenBy(s => s.NAME).ToListAsync();
        }

        public async Task SaveDepartmentAsync(Department dept, bool isNew)
        {
            // Validation 1: unique department name
            bool nameExists = await _context.Departments
                .AnyAsync(d => d.DEPT_NAME.ToUpper() == dept.DEPT_NAME.ToUpper() && d.DEPT_ID != dept.DEPT_ID);
            if (nameExists)
                throw new InvalidOperationException($"A department named '{dept.DEPT_NAME}' already exists.");
            // Office Type is now mandatory for a department
            if (string.IsNullOrWhiteSpace(dept.OFFICE_TYPE))
                throw new InvalidOperationException("Please select an Office Type (Central / Zone / Region / Branch).");

            // Validation 2: cannot turn Revenue OFF while it still has Revenue sections
            if (!isNew && dept.HAS_REVENUE != "Y")
            {
                bool hasRevenueSections = await _context.Sections
                    .AnyAsync(s => s.DEPT_ID == dept.DEPT_ID && s.APPLIES_TO == "REVENUE");
                if (hasRevenueSections)
                    throw new InvalidOperationException(
                        "This department has Revenue sections, so 'Has Revenue Budget' cannot be set to No. Change those sections first.");
            }

            if (isNew)
            {
                long max = await _context.Departments.MaxAsync(d => (long?)d.DEPT_ID) ?? 0;
                dept.DEPT_ID = max + 1;
                dept.CREATED_AT = DateTime.Now;
                if (string.IsNullOrWhiteSpace(dept.STATUS)) dept.STATUS = "Active";
                if (string.IsNullOrWhiteSpace(dept.HAS_REVENUE)) dept.HAS_REVENUE = "N";
                _context.Departments.Add(dept);
            }
            else
            {
                dept.UPDATED_AT = DateTime.Now;
                _context.Departments.Update(dept);
            }
            await _context.SaveChangesAsync();
        }

        public async Task SaveSectionAsync(Section section, bool isNew)
        {
            section.Department = null; // don't let EF touch the parent Department

            var dept = await _context.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DEPT_ID == section.DEPT_ID);
            if (dept == null)
                throw new InvalidOperationException("Please select a valid department.");

            // Rule 1: Revenue sections are only allowed under revenue-enabled departments (e.g. DIT)
            if (section.APPLIES_TO == "REVENUE" && dept.HAS_REVENUE != "Y")
                throw new InvalidOperationException(
                    $"'{dept.DEPT_NAME}' has no Revenue budget, so this section can only be Capital.");

            // Rule 2: unique section name within the same department
            bool nameExists = await _context.Sections.AnyAsync(s =>
                s.NAME.ToUpper() == section.NAME.ToUpper()
                && s.DEPT_ID == section.DEPT_ID
                && s.SECTION_ID != section.SECTION_ID);
            if (nameExists)
                throw new InvalidOperationException(
                    $"A section named '{section.NAME}' already exists in this department.");

            if (isNew)
            {
                long max = await _context.Sections.MaxAsync(s => (long?)s.SECTION_ID) ?? 0;
                section.SECTION_ID = max + 1;
                section.CREATED_AT = DateTime.Now;
                if (string.IsNullOrWhiteSpace(section.STATUS)) section.STATUS = "Active";
                if (string.IsNullOrWhiteSpace(section.APPLIES_TO)) section.APPLIES_TO = "CAPITAL";
                _context.Sections.Add(section);
            }
            else
            {
                // Rule 3: cannot change Capital/Revenue type once budget data exists for this section
                var original = await _context.Sections.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SECTION_ID == section.SECTION_ID);
                if (original != null && original.APPLIES_TO != section.APPLIES_TO)
                {
                    bool hasBudgetData =
                        await _context.RevenueUtilizations.AnyAsync(r => r.SectionId == section.SECTION_ID)
                        || await _context.BudgetAllocations.AnyAsync(a => a.SectionId == section.SECTION_ID);
                    if (hasBudgetData)
                        throw new InvalidOperationException(
                            "Budget data already exists for this section, so its type (Capital/Revenue) cannot be changed.");
                }

                section.UPDATED_AT = DateTime.Now;
                _context.Sections.Update(section);
            }
            await _context.SaveChangesAsync();
        }

        public async Task ToggleDepartmentStatusAsync(long deptId)
        {
            var d = await _context.Departments.FirstOrDefaultAsync(x => x.DEPT_ID == deptId);
            if (d == null) return;

            bool deactivating = d.STATUS == "Active";
            d.STATUS = deactivating ? "Inactive" : "Active";
            d.UPDATED_AT = DateTime.Now;

            if (deactivating)
            {
                // Turn OFF only the currently-active sections, and remember we did it.
                var sections = await _context.Sections
                    .Where(s => s.DEPT_ID == deptId && s.STATUS == "Active").ToListAsync();
                foreach (var s in sections)
                {
                    s.STATUS = "Inactive";
                    s.DEACTIVATED_BY_PARENT = "Y";   // marked so we can restore later
                    s.UPDATED_AT = DateTime.Now;
                }
            }
            else
            {
                // Turn back ON only the sections WE turned off (not ones disabled manually earlier).
                var sections = await _context.Sections
                    .Where(s => s.DEPT_ID == deptId && s.DEACTIVATED_BY_PARENT == "Y").ToListAsync();
                foreach (var s in sections)
                {
                    s.STATUS = "Active";
                    s.DEACTIVATED_BY_PARENT = "N";
                    s.UPDATED_AT = DateTime.Now;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task ToggleSectionStatusAsync(long sectionId)
        {
            var s = await _context.Sections.FirstOrDefaultAsync(x => x.SECTION_ID == sectionId);
            if (s == null) return;

            bool activating = s.STATUS != "Active";
            if (activating)
            {
                var dept = await _context.Departments.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DEPT_ID == s.DEPT_ID);
                if (dept != null && dept.STATUS != "Active")
                    throw new InvalidOperationException(
                        $"Cannot activate this section because its department '{dept.DEPT_NAME}' is inactive. Activate the department first.");
            }

            s.STATUS = activating ? "Active" : "Inactive";
            s.DEACTIVATED_BY_PARENT = "N";  // a manual action clears the parent-cascade flag
            s.UPDATED_AT = DateTime.Now;
            await _context.SaveChangesAsync();
        }
        public async Task<List<ZoneMaster>> GetZonesAsync() =>
            await _context.ZoneMasters.AsNoTracking().OrderBy(z => z.ZONE_NAME_ENG).ToListAsync();

        public async Task<List<RegionMaster>> GetRegionsByZoneAsync(string zoneCode) =>
            await _context.RegionMasters.AsNoTracking()
                .Where(r => r.ZONE_CODE == zoneCode)
                .OrderBy(r => r.REGION_NAME).ToListAsync();

        public async Task<List<BranchMaster>> GetBranchesByRegionAsync(string regionCode) =>
            await _context.BranchMasters.AsNoTracking()
                .Where(b => b.REGION_CODE == regionCode)
                .OrderBy(b => b.BRANCH_NAME).ToListAsync();
        private static string GroupCode(long sectionId) => "SEC" + sectionId;

        public async Task<DitStaffDetail?> GetStaffByPfAsync(string pf) =>
            await _context.StaffDetails.AsNoTracking().FirstOrDefaultAsync(s => s.EMPLID == pf);

        public async Task<(string?, string?, string?, string?, string?)> GetLeadersAsync(long sectionId)
        {
            var s = await _context.Sections.AsNoTracking().FirstOrDefaultAsync(x => x.SECTION_ID == sectionId);
            return (s?.CM_PF, s?.AGM_PF, s?.DGM_PF, s?.GM_PF, s?.CGM_PF);
        }

        public async Task SaveLeadersAsync(long sectionId, string? cm, string? agm, string? dgm, string? gm, string? cgm)
        {
            var pdf = new[] { cm, agm, dgm, gm, cgm }
                            .Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
            var dup = pdf.GroupBy(p => p).FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
                throw new InvalidOperationException($"PF {dup.Key} is entered in more than one leader role. One Person can hold only one role in a section.");
            var s = await _context.Sections.FirstOrDefaultAsync(x => x.SECTION_ID == sectionId);
            if (s == null) return;
            s.CM_PF = cm; s.AGM_PF = agm; s.DGM_PF = dgm; s.GM_PF = gm; s.CGM_PF = cgm;
            s.UPDATED_AT = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task<List<SectionTeamMember>> GetMembersAsync(long sectionId) =>
     await _context.SectionTeamMembers.AsNoTracking()
         .Where(m => m.SECTION_ID == sectionId)
         .OrderByDescending(m => m.MEMBER_ID).ToListAsync();

        public async Task AddMemberAsync(long sectionId, string pf, string name, string? role)
        {
            bool exists = await _context.SectionTeamMembers
                .CountAsync(m => m.SECTION_ID == sectionId && m.PF_NUMBER == pf) > 0;
            if (exists) throw new InvalidOperationException($"PF {pf} is already a member of this team.");

            _context.SectionTeamMembers.Add(new SectionTeamMember
            {
                SECTION_ID = sectionId,
                PF_NUMBER = pf,
                MEMBER_NAME = name,
                MEMBER_ROLE = role,
                STATUS = "Active",
                CREATED_BY = "ADMIN",
                CREATED_AT = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(long sectionId, string pf)
        {
            var m = await _context.SectionTeamMembers
                .FirstOrDefaultAsync(x => x.SECTION_ID == sectionId && x.PF_NUMBER == pf);
            if (m != null) { _context.SectionTeamMembers.Remove(m); await _context.SaveChangesAsync(); }
            //if (m != null) { m.STATUS = "Inactive"; await _context.SaveChangesAsync(); }
        }
        public async Task<List<DitStaffDetail>> GetStaffByPfListAsync(List<string> pfs)
        {
            if (pfs == null || pfs.Count == 0) return new List<DitStaffDetail>();
            return await _context.StaffDetails.AsNoTracking().Where(s => pfs.Contains(s.EMPLID!)).ToListAsync();
        }
        // Leader first; if not a leader anywhere, fall back to team member.
        public async Task<(List<long>, List<long>)> GetUserScopeAsync(string pf)
        {
            pf = (pf ?? "").Trim();
            if (string.IsNullOrEmpty(pf)) return (new List<long>(), new List<long>());

            var sections = await _context.Sections.AsNoTracking()
                .Where(s => s.STATUS == "Active" &&
                            (s.CM_PF == pf || s.AGM_PF == pf || s.DGM_PF == pf ||
                             s.GM_PF == pf || s.CGM_PF == pf))
                .ToListAsync();

            if (sections.Count == 0)
            {
                var ids = await _context.SectionTeamMembers.AsNoTracking()
                    .Where(m => m.PF_NUMBER == pf && m.STATUS == "Active")
                    .Select(m => m.SECTION_ID).Distinct().ToListAsync();
                if (ids.Count > 0)
                    sections = await _context.Sections.AsNoTracking()
                        .Where(s => s.STATUS == "Active" && ids.Contains(s.SECTION_ID)).ToListAsync();
            }

            return (sections.Where(s => s.DEPT_ID != null).Select(s => s.DEPT_ID!.Value).Distinct().ToList(),
                    sections.Select(s => s.SECTION_ID).ToList());
        }

        public async Task<int> CountSubmissionsByPfAsync(string pf)
        {
            var c = await _context.CapitalUtilizations.AsNoTracking().CountAsync(x => x.SubmittedBy == pf);
            var r = await _context.RevenueUtilizations.AsNoTracking().CountAsync(x => x.SubmittedBy == pf);
            return c + r;
        }

        public async Task UpdateMemberRoleAsync(long sectionId, string pf, string? role)
        {
            var m = await _context.SectionTeamMembers
                .FirstOrDefaultAsync(x => x.SECTION_ID == sectionId && x.PF_NUMBER == pf);
            if (m != null) { m.MEMBER_ROLE = role; await _context.SaveChangesAsync(); }
        }

    }
}