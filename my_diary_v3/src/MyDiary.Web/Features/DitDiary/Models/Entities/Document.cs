using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyDiary.Web.Features.DitDiary.Models.Entities
{
    [Table("RP_DIT_DOCUMENT")]
    public class Document
    {
        [Key]
        [Column("DOC_ID")]
        public decimal DocId { get; set; }

        [Column("DOC_CODE")]
        public string? DocCode { get; set; }

        [Column("PROJECT_CODE")]
        public string? ProjectCode { get; set; }

        [Column("DOC_TYPE")]
        public string? DocType { get; set; }

        [Column("DOC_NAME")]
        public string? DocName { get; set; }

        [Column("REMARK")]
        public string? Remark { get; set; }

        [Column("FIELD1")]
        public string? Field1 { get; set; }

        [NotMapped]
        public byte[]? FileContent { get; set; }
        [NotMapped]
        public string? ContentType { get; set; }
        [NotMapped]
        public string? FileName { get; set; }

    }
}   