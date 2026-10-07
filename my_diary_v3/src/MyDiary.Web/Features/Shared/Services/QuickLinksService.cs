using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Data;
using System.Data;

namespace MyDiary.Web.Features.Shared.Services
{
    public class QuickLinkItem
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public string Url { get; set; } = "";
        public string IconName { get; set; } = "";
        public string IconColor { get; set; } = "";
        public bool IsExternal { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public interface IQuickLinksService
    {
        Task<List<QuickLinkItem>> GetQuickLinksAsync();
    }

    public class QuickLinksService : IQuickLinksService
    {
        private readonly SqlDbProvider _db;

        public QuickLinksService(SqlDbProvider db) => _db = db;

        public async Task<List<QuickLinkItem>> GetQuickLinksAsync()
        {
            try
            {
                const string sql = @"
                    SELECT Id, Label, Url, IconName, IconColor, IsExternal, SortOrder
                    FROM dbo.QuickLinks
                    WHERE IsActive = 1
                    ORDER BY SortOrder";

                var dt = await _db.ExecuteQueryAsync(sql);
                var list = new List<QuickLinkItem>();
                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new QuickLinkItem
                    {
                        Id         = Convert.ToInt32(row["Id"]),
                        Label      = row["Label"]?.ToString() ?? "",
                        Url        = row["Url"]?.ToString() ?? "",
                        IconName   = row["IconName"]?.ToString() ?? "",
                        IconColor  = row["IconColor"]?.ToString() ?? "Primary",
                        IsExternal = Convert.ToBoolean(row["IsExternal"]),
                        SortOrder  = Convert.ToInt32(row["SortOrder"])
                    });
                }
                if (list.Count > 0) return list.OrderBy(x => x.Label).ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "[QuickLinksService] Error loading quick links: " + ex.Message);
            }
            return GetFallbackLinks();
        }

        private static List<QuickLinkItem> GetFallbackLinks() => new()
        {
            new() { Id=1,  Label="FINACLE 10x",           Url="https://finacle.ubi.com/SSO/ui/SSOLogin.jsp",                                    IconName="AccountBalance",       IconColor="Error",     IsExternal=true,  SortOrder=1  },
            new() { Id=2,  Label="FINACLE Simulation",    Url="https://finacle10.ubi.com/fininfra/ui/SSOLogin.jsp",                             IconName="AccountBalance",       IconColor="Warning",   IsExternal=true,  SortOrder=2  },
            new() { Id=3,  Label="FINACLE Reporting",     Url="https://finrptdc.ubi.com:9443/SSO/ui/SSOLogin.jsp",                              IconName="Analytics",            IconColor="Primary",   IsExternal=true,  SortOrder=3  },
            new() { Id=4,  Label="FINACLE Reporting 2",   Url="https://drrptweb.ubi.com:9443/SSO/ui/SSOLogin.jsp",                              IconName="Analytics",            IconColor="Secondary", IsExternal=true,  SortOrder=4  },
            new() { Id=5,  Label="FINASTRA",              Url="https://finastra.tradeubi.unionbankofindia.co.in/tiplus2-global/login",           IconName="AccountBalanceWallet", IconColor="Info",      IsExternal=true,  SortOrder=5  },
            new() { Id=6,  Label="FINASTRA DC",           Url="https://#/tiplus2-global/login",                                                 IconName="AccountBalanceWallet", IconColor="Dark",      IsExternal=true,  SortOrder=6  },
            new() { Id=7,  Label="FINASTRA Report",       Url="https://finastrarpt.tradeubi.unionbankofindia.co.in/tiplus2-global/login",        IconName="Description",          IconColor="Primary",   IsExternal=true,  SortOrder=7  },
            new() { Id=8,  Label="Union Parivar",         Url="https://unionparivar.unionbankofindia.co.in/",                                   IconName="Groups",               IconColor="Warning",   IsExternal=true,  SortOrder=8  },
            new() { Id=9,  Label="e-KYC",                 Url="https://ekyc.unionbankofindia.co.in:447/iASKWeb/uiInd",                          IconName="VerifiedUser",         IconColor="Success",   IsExternal=true,  SortOrder=9  },
            new() { Id=10, Label="eTHIC",                 Url="https://ethicprod.unionbankofindia.co.in/eTHIC/login.htm",                       IconName="Gavel",                IconColor="Error",     IsExternal=true,  SortOrder=10 },
            new() { Id=11, Label="eTHIC Report",          Url="https://ethicreport.unionbankofindia.co.in/eTHIC_Report/login.htm",              IconName="Assessment",           IconColor="Warning",   IsExternal=true,  SortOrder=11 },
            new() { Id=12, Label="CKYC",                  Url="https://ckyc.unionbankofindia.co.in/ckyc/",                                     IconName="Badge",                IconColor="Primary",   IsExternal=true,  SortOrder=12 },
            new() { Id=13, Label="Branch CCR",            Url="https://iaml.unionbankofindia.co.in:9136/branchop-web/",                         IconName="AccountBalance",       IconColor="Secondary", IsExternal=true,  SortOrder=13 },
            new() { Id=14, Label="Branch STR",            Url="https://iaml.unionbankofindia.co.in:9135/aml/loginPage.aml",                     IconName="Security",             IconColor="Error",     IsExternal=true,  SortOrder=14 },
            new() { Id=15, Label="DMS Login",             Url="https://dms.unionbankofindia.co.in/omniapp/",                                    IconName="FolderOpen",           IconColor="Info",      IsExternal=true,  SortOrder=15 },
            new() { Id=16, Label="CRM Edge",              Url="https://ucrm.unionbankofindia.co.in",                                            IconName="SupportAgent",         IconColor="Success",   IsExternal=true,  SortOrder=16 },
            new() { Id=17, Label="IT Konnect",            Url="https://ubiitsm-dwp.unionbankofindia.co.in",                                     IconName="Public",               IconColor="Primary",   IsExternal=true,  SortOrder=17 },
        };
    }
}
