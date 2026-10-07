using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Keyless]
    [Table("REGION_MASTER")]
    public class RegionMaster
    {
        [Column("REGION_CODE")] public string? REGION_CODE { get; set; }
        [Column("REGION_NAME")] public string? REGION_NAME { get; set; }
        [Column("ZONE_CODE")] public string? ZONE_CODE { get; set; }
        [Column("STATUS")] public string? STATUS { get; set; }
    }
}