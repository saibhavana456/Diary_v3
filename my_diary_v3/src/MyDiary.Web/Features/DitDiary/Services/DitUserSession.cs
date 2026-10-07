using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using MyDiary.Web.Features.DitDiary.Data;
using MyDiary.Web.Features.DitDiary.Interfaces;
using MyDiary.Web.Features.DitDiary.Models.DTO;

namespace MyDiary.Web.Features.DitDiary.Services
{
    /// <summary>
    /// Replaces the standalone app's own login. The signed-in My Diary user is the DIT user: PF = emplid claim,
    /// name/designation/scale from the HRMS snapshot, role from the section leader / team-member tables, and
    /// admin = My Diary Super/App Admin or a row in RP_DIT_APP_ADMIN. Scope (departments/sections) is unchanged.
    /// </summary>
    public class DitUserSession
    {
        private readonly AuthenticationStateProvider _auth;
        private readonly IAdminMasterService _admin;
        private readonly ProjectBudgetService _budget;
        private readonly DitDbContext _db;

        public DitUserSession(AuthenticationStateProvider auth, IAdminMasterService admin, ProjectBudgetService budget, DitDbContext db)
        { _auth = auth; _admin = admin; _budget = budget; _db = db; }

        public AuthUser? User { get; private set; }
        public bool IsLoggedIn => User != null;
        public event Action? OnChange;

        public Task SetAsync(AuthUser user) { User = user; OnChange?.Invoke(); return Task.CompletedTask; }
        public Task ClearAsync() { User = null; OnChange?.Invoke(); return Task.CompletedTask; }

        private Task<AuthUser?>? _inflight;

        // Concurrent callers (layout + page) share one load instead of racing on the same context.
        public Task<AuthUser?> LoadAsync() => User != null ? Task.FromResult<AuthUser?>(User) : (_inflight ??= LoadCoreAsync());

        private async Task<AuthUser?> LoadCoreAsync()
        {
            var cp = (await _auth.GetAuthenticationStateAsync()).User;
            if (cp.Identity?.IsAuthenticated != true) return null;

            var pf = (cp.FindFirst("emplid")?.Value ?? "").Trim();
            if (pf.Length == 0) return null;

            var s = await _admin.GetStaffByPfAsync(pf);
            var user = new AuthUser
            {
                PfNumber = pf,
                Name = s?.NAME ?? cp.Identity?.Name ?? pf,
                Designation = s?.EMP_DESGN_DESC ?? "",
                ScaleText = s?.EMP_SCALE_DESCR ?? "",
                Scale = ParseScale(s?.EMP_SCALE_DESCR, s?.EMP_SCALE_CODE),
                BranchName = s?.DESCR ?? "",
                RegionName = s?.REGION_NAME ?? "",
                ZoneName = s?.DIVISION_NAME ?? "",
              //  Role = await ResolveRole(pf),
            };

            var (deptIds, sectionIds) = await _admin.GetUserScopeAsync(pf);
            user.DeptIds = deptIds;
            user.SectionIds = sectionIds;

            user.IsAdmin = IsTrue(cp.FindFirst("rp_super_admin")?.Value) || IsTrue(cp.FindFirst("rp_app_admin")?.Value)
                        || await _budget.IsAppAdminAsync(pf);

            User = user;
            OnChange?.Invoke();
            return User;
        }

        // Re-computes scope if missing (stale session, or newly mapped by admin).
        public async Task EnsureScopeAsync(IAdminMasterService admin)
        {
            if (User == null || User.IsAdmin || User.SectionIds.Count > 0) return;
            var (deptIds, sectionIds) = await admin.GetUserScopeAsync(User.PfNumber);
            User.DeptIds = deptIds;
            User.SectionIds = sectionIds;
        }

        private static bool IsTrue(string? v) => v is not null && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");

        // Leader role wins; otherwise the role given in Team Members.
        private async Task<string> ResolveRole(string pf)
        {
            var sec = await _db.Sections.AsNoTracking().FirstOrDefaultAsync(x =>
                x.CM_PF == pf || x.AGM_PF == pf || x.DGM_PF == pf || x.GM_PF == pf || x.CGM_PF == pf);
            if (sec != null)
            {
                if (sec.CM_PF == pf) return "CM";
                if (sec.AGM_PF == pf) return "AGM";
                if (sec.DGM_PF == pf) return "DGM";
                if (sec.GM_PF == pf) return "GM";
                if (sec.CGM_PF == pf) return "CGM";
            }
            var m = await _db.SectionTeamMembers.AsNoTracking().FirstOrDefaultAsync(x => x.PF_NUMBER == pf);
            return string.IsNullOrWhiteSpace(m?.MEMBER_ROLE) ? "User" : m!.MEMBER_ROLE!;
        }

        private static int ParseScale(string? descr, string? code)
        {
            var m = Regex.Match(descr ?? "", @"\d+");
            if (m.Success && int.TryParse(m.Value, out var n)) return n;
            if (int.TryParse((code ?? "").Trim(), out var c)) return c;
            return 0;   // CLERK / non-officer
        }
    }
}
