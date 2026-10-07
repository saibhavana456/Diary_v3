using System;

namespace MyDiary.Web.Features.ProjectMuskaan.Models
{
    /// <summary>
    /// One row of [dbo].[MDSummary ] (note: the table name really does have a trailing space).
    /// All text columns are normalised to "" instead of null so sorting/searching never has
    /// to null-check.
    /// </summary>
    public class ProjectMuskaanTicket
    {
        public string SNo { get; set; } = "";
        public string ProblemDescription { get; set; } = "";
        public string ActionRequired { get; set; } = "";
        public string BenefitToBranches { get; set; } = "";

        // These four/five columns are not shown in the grid (they were commented out in the
        // original ASPX) but the original search box still matched against them, so they are
        // loaded and included in SearchText to keep the search behaviour identical.
        public string BenefitEaseOfDoingBusiness { get; set; } = "";
        public string BenefitCostCutting { get; set; } = "";
        public string BenefitCustomerService { get; set; } = "";
        public string BenefitNewBusiness { get; set; } = "";
        public string BenefitRiskMitigation { get; set; } = "";

        public string RequestedBy { get; set; } = "";
        public string ActualStatus { get; set; } = "";
        public DateTime? CompletionDate { get; set; }

        /// <summary>Numeric SNo for sorting (falls back to the end of the list if SNo isn't numeric).</summary>
        public int SNoNumber => int.TryParse(SNo, out var n) ? n : int.MaxValue;

        private string? _searchText;

        /// <summary>Everything the search box matches against, built once per row.</summary>
        public string SearchText => _searchText ??= string.Join("\n",
            SNo,
            ProblemDescription,
            ActionRequired,
            BenefitToBranches,
            BenefitEaseOfDoingBusiness,
            BenefitCostCutting,
            BenefitCustomerService,
            BenefitNewBusiness,
            BenefitRiskMitigation,
            RequestedBy,
            ActualStatus);
    }
}
