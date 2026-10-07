using System.Collections.Generic;
using System.Threading.Tasks;
using MyDiary.Web.Features.DeploymentPulse.Models;

namespace MyDiary.Web.Features.DeploymentPulse.Services
{
    public interface IDeploymentPulseService
    {
        /// <summary>
        /// Returns matching rows from DEPLOYMENT_PULSE_ITSM_VIEW.
        /// onlyPublished=true (the default) restricts results to PUBLISH_FLAG = 'Y' -
        /// use this for the anonymous/public route. Pass false only for an
        /// internal/authenticated view, if one is ever added.
        /// </summary>
        Task<List<DeploymentPulseItem>> GetDeploymentsAsync(DeploymentPulseFilter filter, bool onlyPublished = true);

        /// <summary>Distinct publish years, for the year filter dropdown.</summary>
        Task<List<string>> GetPublishYearsAsync();
    }
}
