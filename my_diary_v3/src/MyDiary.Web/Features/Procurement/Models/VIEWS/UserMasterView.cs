using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MyDiary.Web.Features.Procurement.Models.VIEWS
{
    [Table("UV_USER_MASTER")]
    [Keyless]
    public class UserMasterView
    {
        public string PF_NO { get; set; } = default!;
        public string? NAME { get; set; }
        public string? JOB_CODE { get; set; }
        public string? JOB_DESC { get; set; }
        public string? LOCATION { get; set; }
        public string? LOCATION_DESC { get; set; }
        public string? REGION_CODE { get; set; }
        public string? REGION_NAME { get; set; }
        public string? ZONE_CODE { get; set; }
        public string? ZONE_NAME { get; set; }
        public string? DEPT_ID { get; set; }
        public string? DEPT_DESC { get; set; }
        public string? FINACLE_ID { get; set; }
        public string? CONTACT_NO { get; set; }
        public string? EMAIL_ID { get; set; }
        public string? OFFICE_TYPE { get; set; }
        public string? ROLE { get; set; }
        public string? ACCESS_LEVEL { get; set; }
        public DateTime? EFFECTIVE_DATE { get; set; }
        public string? REPORTING_OFFICER_PF_NO { get; set; }
        public string? REPORTING_OFFICER_NAME { get; set; }
        public string? REMARKS { get; set; }
        public string? BRANCH_CODE { get; set; }
        public string? BRANCH_NAME { get; set; }
        public string? BR_ADD1 { get; set; }
        public string? BR_ADD2 { get; set; }
        public string? BR_ADD3 { get; set; }
        public string? DISTRICT_NAME_ENG { get; set; }
        public string? STATE_NAME_ENG { get; set; }
        public string? BM_MOB { get; set; }
        public string? BR_EMAIL { get; set; }

    }
}
