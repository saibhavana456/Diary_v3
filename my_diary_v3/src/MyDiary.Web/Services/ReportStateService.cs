using MyDiary.Web.Features.Shared.Models;
using System.Security.Claims;

namespace MyDiary.Web.Services
{
    public class ReportStateService
    {
        public UserSession CurrentUser { get; private set; } = new();
        public bool IsInitialized { get; private set; }

        public void Initialize(ClaimsPrincipal principal)
        {
            if (IsInitialized) return;
            if (principal?.Identity?.IsAuthenticated != true) return;

            CurrentUser = new UserSession
            {
                EmpId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                Name = principal.Identity?.Name ?? string.Empty,
                Role = principal.FindFirst("privilege")?.Value ?? "BRANCH",

                // These claims are now provided by the provider
                BranchSolid = principal.FindFirst("branch_solid")?.Value ?? string.Empty,
                ZoneSolid = principal.FindFirst("zone_solid")?.Value ?? string.Empty
            };

            IsInitialized = true;
        }

        public void Clear()
        {
            CurrentUser = new UserSession();
            IsInitialized = false;
        }
    }
}