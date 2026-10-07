using System;

namespace MyDiary.Web.Features.DeploymentPulse.Models
{
    /// <summary>
    /// Mirrors a row of DEPLOYMENT_PULSE_ITSM_VIEW (Oracle). Column mapping is done
    /// manually in DeploymentPulseService rather than via an ORM, consistent with how
    /// the original WebForms page read this view.
    /// </summary>
    public class DeploymentPulseItem
    {
        public string CrqNumber { get; set; }
        public string Vertical { get; set; }
        public string Description { get; set; }
        public string Impact { get; set; }
        public DateTime? TentativeGoLiveDate { get; set; }
        public DateTime? ActualGoLiveDate { get; set; }

        // NOTE: contains "<Name> - <Email>" of the SPOC. Public/anonymous route should
        // decide whether to display this as-is or mask/omit it - see caller.
        public string SpocDetails { get; set; }

        public string PublishFlag { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// Search criteria for the report grid - same fields the WebForms page exposed
    /// (CR No., Vertical, Status, Publish Year, Tentative Go-Live date range).
    /// </summary>
    public class DeploymentPulseFilter
    {
        public string CrqNumber { get; set; }
        public string Vertical { get; set; }
        public string Status { get; set; } = "ALL";
        public string PublishYear { get; set; } = "ALL";
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
