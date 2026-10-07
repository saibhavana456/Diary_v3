using System.Text.Json.Serialization;

namespace MyDiary.Web.Features.Business.Models;

public class AmenityItem
{
    [JsonPropertyName("sno")]
    public int Sno { get; set; }

    [JsonPropertyName("agenda_header")]
    public string AgendaHeader { get; set; } = string.Empty;

    [JsonPropertyName("agenda_name")]
    public string AgendaName { get; set; } = string.Empty;

    [JsonPropertyName("agenda_desc")]
    public string AgendaDescription { get; set; } = string.Empty;

    [JsonPropertyName("agendaStatus")]
    public string AgendaStatus { get; set; } = string.Empty;

    [JsonPropertyName("agenda_remarks")]
    public string? AgendaRemarks { get; set; }

    [JsonPropertyName("preview")]
    public string? PreviewBase64 { get; set; }

    [JsonPropertyName("agenda_fileupload")]
    public string AgendaFileUpload { get; set; } = string.Empty;

    [JsonPropertyName("meeting_id")]
    public int MeetingId { get; set; }
}
