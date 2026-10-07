using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using System.Data;

namespace MyDiary.Web.Features.Shared.Services
{
    // ── Models ──────────────────────────────────────────────────────────────
    public class LeftNavLink
    {
        public string Label { get; set; } = "";
        public string? Label_HI { get; set; }
        public string Url { get; set; } = "";
        public bool IsExternal { get; set; } = true;
        public int SortOrder { get; set; }


        public string DisplayLabel(string lang)
                => lang == "hi" && !string.IsNullOrWhiteSpace(Label_HI)
                   ? Label_HI!
                   : Label;

    }

    public class LeftNavGroup
    {
        public string Label { get; set; } = "";
        public string? Label_HI { get; set; }

        public List<LeftNavLink> Links { get; set; } = new();
        public string? MoreUrl { get; set; }
        public bool MoreExternal { get; set; } = true;
        public int SortOrder { get; set; }


        public string DisplayLabel(string lang)
                => lang == "hi" && !string.IsNullOrWhiteSpace(Label_HI)
                   ? Label_HI!
                   : Label;

    }

    // ── Interface ────────────────────────────────────────────────────────────
    public interface ILeftNavService
    {
        Task<List<LeftNavGroup>> GetLeftNavGroupsAsync();
    }

    // ── Service ──────────────────────────────────────────────────────────────
    public class LeftNavService : ILeftNavService
    {
        private readonly SqlDbProvider _db;

        public LeftNavService(SqlDbProvider db) => _db = db;

        public async Task<List<LeftNavGroup>> GetLeftNavGroupsAsync()
        {
            try
            {
                // Groups
                const string groupSql = @"
                    
SELECT GroupId,
       GroupLabel,
       GroupLabel_HI,
       MoreUrl,
       MoreExternal,
       SortOrder
FROM dbo.LandingNavGroups
WHERE IsActive = 1
ORDER BY SortOrder
";

                var groupDt = await _db.ExecuteQueryAsync(groupSql);
                if (groupDt.Rows.Count == 0) return GetFallbackGroups();

                // Links
                const string linkSql = @"
                    
SELECT GroupId,
       Label,
       Label_HI,
       Url,
       IsExternal,
       SortOrder
FROM dbo.LandingNavLinks
WHERE IsActive = 1
ORDER BY GroupId, SortOrder
";

                var linkDt = await _db.ExecuteQueryAsync(linkSql);

                // Build lookup: GroupId → links
                var linkMap = new Dictionary<int, List<LeftNavLink>>();
                foreach (DataRow lr in linkDt.Rows)
                {
                    int gid = Convert.ToInt32(lr["GroupId"]);
                    if (!linkMap.ContainsKey(gid)) linkMap[gid] = new();
                    linkMap[gid].Add(new LeftNavLink
                    {
                        Label = lr["Label"]?.ToString() ?? "",
                        Label_HI = lr["Label_HI"]?.ToString(),   // ✅ MISSING LINE
                        Url = lr["Url"]?.ToString() ?? "",
                        IsExternal = Convert.ToBoolean(lr["IsExternal"]),
                        SortOrder = Convert.ToInt32(lr["SortOrder"])
                    });
                }

                var groups = new List<LeftNavGroup>();
                foreach (DataRow gr in groupDt.Rows)
                {
                    int gid = Convert.ToInt32(gr["GroupId"]);
                    groups.Add(new LeftNavGroup
                    {
                        Label = gr["GroupLabel"]?.ToString() ?? "",
                        Label_HI = gr["GroupLabel_HI"]?.ToString(),
                        MoreUrl = gr["MoreUrl"] == DBNull.Value ? null : gr["MoreUrl"]?.ToString(),
                        MoreExternal = gr["MoreExternal"] != DBNull.Value && Convert.ToBoolean(gr["MoreExternal"]),
                        SortOrder = Convert.ToInt32(gr["SortOrder"]),
                        Links = linkMap.TryGetValue(gid, out var lnks) ? lnks : new()
                    });
                }

                return groups.Count > 0 ? groups : GetFallbackGroups();
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[LeftNavService] Unhandled error: " + ex.Message);
                // DB unavailable — return mock data so the page still loads
                return GetFallbackGroups();
            }
        }

        // ── Mock / fallback data ─────────────────────────────────────────────
        private static List<LeftNavGroup> GetFallbackGroups() => new()
        {
            new LeftNavGroup
            {
                Label   = "Resources",
                MoreUrl = null,
                Links   = new()
                {
                    new() { Label = "Step by Step Process Flow",    Url = "https://mydiary.unionbankofindia.co.in/Artifacts/Activities?DeptID=52" },
                    new() { Label = "Financial Results",            Url = "https://www.unionbankofindia.co.in/english/financial-results.aspx" },
                    new() { Label = "Useful Links",                 Url = "https://softwareuat.unionbankofindia.co.in/MyDiary.Web/applications#Useful_links" },
                    new() { Label = "Circulars & Policies",         Url = "https://dms.unionbankofindia.co.in/webdesktop/CustomJobs/eCircular/Circular.jsp" },
                    new() { Label = "Code of Ethics",               Url = "https://ubinet.unionbankofindia.co.in/UBINET/CodeofEthics.jsp" },
                    new() { Label = "E-Manual Content",             Url = "https://ubinet.unionbankofindia.co.in/UBINET/emanuelHome.jsp" },
                    new() { Label = "Whistle Blower Portal",        Url = "https://whistleblower.unionbankofindia.bank.in/ubi_wb/" },
                    new() { Label = "FAQs/SOPs",                    Url = "https://mydiary.unionbankofindia.co.in/Artifacts/Activities?DeptID=53" },
                    new() { Label = "Union Connect – हम वही हैं",   Url = "https://mydiary.unionbankofindia.co.in/Artifacts/Documents/Upload/?ActivityID=299" },
                    new() { Label = "Deployment Pulse",      Url = "reports/deployment-pulse-itsm",            IsExternal = false },
                }
            },

            new LeftNavGroup
            {
                Label   = "STP Journeys",
                MoreUrl = null,
                Links   = new()
                {
                    new() { Label = "Account Statement Portal",         Url = "https://app5.unionbankofindia.co.in/StatementScheduler/" },
                    new() { Label = "Agri Debt Swap STP",               Url = "https://instaloan.unionbankofindia.bank.in/debtswap/lendperfect/debtSwapbankerlogin" },
                    new() { Label = "Agri Kisan Tatkal STP",            Url = "https://instaloan.unionbankofindia.bank.in/agristp/lendperfect/agribankerlogin" },
                    new() { Label = "Agri KCC STP",                     Url = "https://kccstp.unionbankofindia.bank.in/login-admin" },
                    new() { Label = "Agri SHG STP",                     Url = "https://instaloan.unionbankofindia.bank.in/shgstp/lendperfect/bankerlogin" },
                    new() { Label = "Credit Card for Staff STP",        Url = "https://ccms.unionbankofindia.co.in/staff-stp/" },
                    new() { Label = "MSME STP",                         Url = "https://msmeint.unionbankofindia.bank.in/#/banker-signin" },
                    new() { Label = "STP Journey for Staff Loans – Branch", Url = "https://instaloan.unionbankofindia.co.in/staffstp/lendperfect/bankerlogin" },
                    new() { Label = "STP Journey for Staff Loans",      Url = "https://instaloan.unionbankofindia.co.in/staffstp/lendperfect/customerLogin" },
                }
            },

            new LeftNavGroup
            {
                Label = "Rates & Charges",
                Links = new()
                {
                    new() { Label = "Deposit Rates",      Url = "rates-charges?tab=Deposits",            IsExternal = false },
                    new() { Label = "Lending Rate",       Url = "rates-charges?tab=MCLR",                IsExternal = false },
                    new() { Label = "Service Charges",    Url = "rates-charges?tab=Service Charges",     IsExternal = false },
                    new() { Label = "Treasury",           Url = "rates-charges?tab=Treasury",            IsExternal = false },
                    new() { Label = "Retail Loans",       Url = "rates-charges?tab=Retail Loans",        IsExternal = false },
                    new() { Label = "Gold Loan Vertical", Url = "rates-charges?tab=Gold Loan Vertical",  IsExternal = false },
                }
            },

            new LeftNavGroup
            {
                Label = "Branch Locator",
                Links = new()
                {
                    new() { Label = "Branch Search",      Url = "https://app2.unionbankofindia.co.in/branchsearch/Br_Keyword.aspx" },
                    new() { Label = "ATM Locator",        Url = "https://app2.unionbankofindia.co.in/branchsearch/ATMSearch.aspx" },
                    new() { Label = "Region Search",      Url = "https://app2.unionbankofindia.co.in/branchsearch/intermediateubinet.aspx?id=2" },
                    new() { Label = "Zone Search",        Url = "https://app2.unionbankofindia.co.in/branchsearch/intermediateubinet.aspx?id=1" },
                    new() { Label = "Specialized Branch", Url = "https://app2.unionbankofindia.co.in/branchsearch/SearchMain.aspx" },
                }
            },
        };
    }
}
