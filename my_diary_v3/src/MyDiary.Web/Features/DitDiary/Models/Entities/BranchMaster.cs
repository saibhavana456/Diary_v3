using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Keyless]
    [Table("BRANCH_MASTER")]
    public class BranchMaster
    {
        [Column("SOL_ID")] public string? BRANCH_UCD1 { get; set; }   // SOL ID
        [Column("BRANCH_CODE")] public string? BRANCH_CODE { get; set; }
        [Column("BRANCH_NAME")] public string? BRANCH_NAME { get; set; }
        [Column("REGION_CODE")] public string? REGION_CODE { get; set; }
        [Column("ZONE_CODE")] public string? ZONE_CODE { get; set; }
        [Column("STATUS")] public string? STATUS { get; set; }
    }
}