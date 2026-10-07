using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDiary.Web.Features.ProjectMuskaan.Models;

namespace MyDiary.Web.Features.ProjectMuskaan.Services
{
    public interface IProjectMuskaanService
    {
        /// <summary>
        /// Returns every row of the Project Muskaan summary table. The table is small, so the
        /// page filters, sorts and pages in memory (same approach as the original WebForms page,
        /// which cached the full table in ViewState).
        /// </summary>
        Task<List<ProjectMuskaanTicket>> GetTicketsAsync(CancellationToken cancellationToken = default);
    }
}
