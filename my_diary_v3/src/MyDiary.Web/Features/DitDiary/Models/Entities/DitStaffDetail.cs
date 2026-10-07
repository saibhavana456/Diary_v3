using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    /// <summary>Read-only view of the HRMS snapshot ORGANISATION.STAFF_DETAILS. Property names are kept from the
    /// original DIT Diary app; only the column mapping is changed to My Diary's snapshot layout.</summary>
    [Keyless]
    [Table("STAFF_DETAILS", Schema = "ORGANISATION")]
    public class DitStaffDetail
    {
        [Column("PF_NO")] public string? EMPLID { get; set; }             // PF number
        [Column("NAME")] public string? NAME { get; set; }
        [Column("LOCATION")] public string? LOCATION { get; set; }
        [Column("LOCATION_DESC")] public string? DESCR { get; set; }      // location desc
        [Column("DEPT_ID")] public string? DEPTID { get; set; }
        [Column("DEPT_DESC")] public string? DESCR1 { get; set; }         // dept desc
        [Column("JOB_CODE")] public string? EMP_DESGN { get; set; }
        [Column("JOB_DESC")] public string? EMP_DESGN_DESC { get; set; }
        [Column("SCALE_CODE")] public string? EMP_SCALE_CODE { get; set; }
        [Column("SCALE_DESC")] public string? EMP_SCALE_DESCR { get; set; }
        [Column("CONTACT_NO")] public string? PHONE { get; set; }
        [Column("EMAIL_ID")] public string? EMAIL { get; set; }
        [Column("SOLE_ID")] public string? BRSOLID { get; set; }          // SOL ID
        [Column("REGION_CODE")] public string? REGION_CODE { get; set; }
        [Column("REGION_NAME")] public string? REGION_NAME { get; set; }
        [Column("ZONE_NAME")] public string? DIVISION_NAME { get; set; }
    }
}
