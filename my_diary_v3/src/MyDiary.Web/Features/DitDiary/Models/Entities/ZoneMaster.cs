using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Keyless]
    [Table("ZONE_MASTER")]
    public class ZoneMaster
    {
        [Column("ZONE_CODE")] public string? ZONE_CODE { get; set; }
        [Column("ZONE_NAME_ENG")] public string? ZONE_NAME_ENG { get; set; }
        [Column("STATUS")] public string? STATUS { get; set; }
    }
}