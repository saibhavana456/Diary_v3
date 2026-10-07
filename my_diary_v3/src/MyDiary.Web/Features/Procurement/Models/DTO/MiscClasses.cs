namespace MyDiary.Web.Features.Procurement.Models.DTO
{
    public class MiscClasses
    {
        public class DeleteRequestDto
        {
            public string RequestCode { get; set; }
            public string DeletedBy { get; set; }
        }
        public class AtmMasteDTO
        {
            public string? AtmCode { get; set; }
            public string? TerminalId8Digit { get; set; }
            public string? Project { get; set; }
            public string? AtmMake { get; set; }
            public DateTime? InstallationDate { get; set; }
            public string? AtmCrm { get; set; }
            public string? CapexOpex { get; set; }
            public string? CapexOpexOnly { get; set; }
            public decimal? AvgUptime { get; set; }
            public decimal? AvgHits { get; set; }
        }


    }
}
