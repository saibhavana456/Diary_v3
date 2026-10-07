namespace MyDiary.Web.Features.ECircular.Models;

/// <summary>
/// Response from the ECircular search API.
/// </summary>
public sealed class ECircularSearchResponse
{
    public bool Status { get; set; } = true;
    public int TotalPages { get; set; }
    public List<ECircularItem> Items { get; set; } = [];
}

/// <summary>
/// Request model for PDF watermark generation.
/// </summary>
public sealed record ECircularPdfRequest(string EmployeeNumber, string EcircularLink);
