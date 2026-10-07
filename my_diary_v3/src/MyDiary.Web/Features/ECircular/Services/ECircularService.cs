using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MyDiary.Core.Services;
using MyDiary.Web.Core.Extensions;
using MyDiary.Web.Features.ECircular.Models;
using Oracle.ManagedDataAccess.Client;
using System.Text.RegularExpressions;
using System.Web;

namespace MyDiary.Web.Features.ECircular.Services;

/// <summary>
/// ECircular service: fetches filter lookups from Oracle DB,
/// searches circulars via DMS HTTP, parses HTML results,
/// downloads PDFs and applies watermarks.
/// </summary>
public sealed class ECircularService : IECircularService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ECircularOptions _options;
    private readonly ILogger<ECircularService> _logger;

    public ECircularService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        IOptions<ECircularOptions> options,
        ILogger<ECircularService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _env = env;
        _options = options.Value;
        _logger = logger;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("MyDiaryDBConnection")
        ?? throw new InvalidOperationException("OracleDb connection string is missing.");

    #region Filter Lookup

    public async Task<ECircularFilterLookup> GetFilterDataAsync(CancellationToken ct = default)
    {
        var lookup = new ECircularFilterLookup();

        try
        {
            await using var connection = new OracleConnection(ConnectionString);
            await connection.OpenAsync(ct);

            // Load departments from E_CIRCULAR_DEPARTMENT_MASTER
            lookup.Departments = await ReadLookupAsync(
                connection,
                @"SELECT DEPARTMENT_NAME, DEPARTMENT_NAME AS DISPLAY_NAME
                  FROM E_CIRCULAR_DEPARTMENT_MASTER
                  ORDER BY DEPARTMENT_NAME",
                ct);

            lookup.CircularTypes = await ReadLookupAsync(
                connection,
                "SELECT CIRCULAR_TYPE FROM E_CIRCULAR_TYPE_MASTER ORDER BY CIRCULAR_TYPE",
                ct);

            for (int year = _options.StartYear; year <= AppTime.Now.Year; year++)
                lookup.Years.Add(year);

            // Load special listings from database
            lookup.SpecialListings = await ReadSpecialListingsAsync(connection, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ECircularService] Failed to load filter lookup data.");
        }

        return lookup;
    }

    private static async Task<List<ECircularLookupItem>> ReadLookupAsync(
        OracleConnection connection,
        string sql,
        CancellationToken ct)
    {
        var items = new List<ECircularLookupItem>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = Convert.ToString(reader.GetValue(0)) ?? "";
            var name = reader.FieldCount > 1
                ? (Convert.ToString(reader.GetValue(1)) ?? id)
                : id;
            items.Add(new ECircularLookupItem(id, name));
        }

        return items;
    }

    private static async Task<List<SpecialListingOption>> ReadSpecialListingsAsync(
        OracleConnection connection,
        CancellationToken ct)
    {
        var items = new List<SpecialListingOption>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT LISTING_KEY, DISPLAY_NAME, URL_TEMPLATE, OPEN_IN_NEW_TAB
                            FROM E_CIRCULAR_SPECIAL_LISTING
                            WHERE IS_ACTIVE = 'Y'
                            ORDER BY DISPLAY_ORDER";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new SpecialListingOption(
                Key: reader.GetString(0),
                DisplayName: reader.GetString(1),
                UrlTemplate: reader.GetString(2),
                OpenInNewTab: reader.GetString(3) == "Y"
            ));
        }

        return items;
    }

    #endregion

    #region Search

    public async Task<ECircularSearchResponse> SearchCircularsAsync(
        ECircularSearchRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var searchUrl = BuildSearchUrl(request);
            _logger.LogInformation("[ECircularService] Searching: {Url}", searchUrl);

            var client = _httpClientFactory.CreateClient("ECircularDms");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            using var response = await client.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(ct);
            return ParseSearchHtml(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ECircularService] Search failed.");
            return new ECircularSearchResponse();
        }
    }

    private string BuildSearchUrl(ECircularSearchRequest request)
    {
        var baseUri = GetBaseDmsUri();

        if (request.IsEmpty())
        {
            return new Uri(baseUri, $"Process.jsp?PageNo={request.PageNo}").ToString();
        }

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["PageNo"] = request.PageNo.ToString();
        query["IDCType"] = request.CircularType ?? string.Empty;
        query["IdCircularNo"] = request.CircularNo ?? string.Empty;
        query["IdDescription"] = request.Subject ?? string.Empty;
        query["IdDepName"] = request.DepartmentName ?? string.Empty;
        query["IdSecName"] = request.SectionName ?? string.Empty;
        query["publish-year"] = request.PublishYear?.ToString() ?? string.Empty;
        query["IdNameofAcct"] = string.Empty;
        query["IdRegAddress"] = string.Empty;
        query["IdNameofDPP"] = string.Empty;
        query["IdAddresses"] = string.Empty;
        query["IdUnitAddress"] = string.Empty;
        query["IdAssocConcern"] = string.Empty;
        query["IdNameofDirAssocConc"] = string.Empty;
        query["UIndex"] = "null";

        return new Uri(baseUri, "ProcessCircular.jsp?" + query).ToString();
    }

    private ECircularSearchResponse ParseSearchHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var response = new ECircularSearchResponse();

        var rows = doc.DocumentNode.SelectNodes(
            "//table[@id='searchResult1']//tr[position()>1]");

        if (rows != null)
        {
            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                if (cells == null || cells.Count < 8) continue;

                var href = cells[1].SelectSingleNode(".//a")
                    ?.GetAttributeValue("href", "") ?? "";
                var publishDate = CleanText(cells[3].InnerText);

                response.Items.Add(new ECircularItem
                {
                    Sno = CleanText(cells[0].InnerText),
                    CircularNo = CleanText(cells[1].InnerText),
                    CircularType = CleanText(cells[2].InnerText),
                    PublishDate = publishDate,
                    Language = CleanText(cells[4].InnerText),
                    Department = CleanText(cells[5].InnerText),
                    Section = CleanText(cells[6].InnerText),
                    Subject = CleanText(cells[7].InnerText),
                    CircularLink = href + "///" + publishDate
                });
            }
        }

        // Parse total pages
        var pageNode = doc.DocumentNode.SelectSingleNode(
            "(//table)[2]//td[@align='left']//font[contains(normalize-space(.), 'Page') and contains(., 'of')]");
        var match = Regex.Match(pageNode?.InnerText ?? "", @"\bof\s+(\d+)\b", RegexOptions.IgnoreCase);
        if (match.Success)
            response.TotalPages = int.Parse(match.Groups[1].Value);

        return response;
    }

    private static string CleanText(string? value) =>
        HtmlEntity.DeEntitize(value ?? "").Replace("\u00A0", " ").Trim();

    #endregion

    #region PDF Watermark (In-Memory)

    public async Task<byte[]> GetWatermarkedPdfBytesAsync(
        ECircularPdfRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeNumber))
            throw new ArgumentException("Employee PF Number is required.");

        if (string.IsNullOrWhiteSpace(request.EcircularLink))
            throw new ArgumentException("Circular PDF link is missing.");

        try
        {
            var (documentLink, _) = SplitCircularLink(request.EcircularLink);

            _logger.LogInformation("[ECircularService] PF={PfNo} requested PDF.", request.EmployeeNumber);

            var fullDmsUrl = ResolveDmsDocumentUrl(documentLink);

            _logger.LogInformation("[ECircularService] Downloading DMS document: {Url}", fullDmsUrl);

            // Download PDF bytes from DMS
            var pdfBytes = await DownloadPdfAsync(fullDmsUrl, ct);
            if (pdfBytes.Length == 0)
                throw new InvalidOperationException("Downloaded PDF is empty.");

            // Apply watermark entirely in memory and return bytes
            return ApplyWatermarkInMemory(pdfBytes, request.EmployeeNumber.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ECircularService] PDF generation failed. PF={PfNo}, Link={Link}", request.EmployeeNumber, request.EcircularLink);
            throw;
        }
    }

    private async Task<byte[]> DownloadPdfAsync(string url, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ECircularDms");

        _logger.LogInformation("[ECircularService] Download request started. Url={Url}", url);

        using var response = await client.GetAsync(url, ct);

        _logger.LogInformation("[ECircularService] DMS response Status={StatusCode}", response.StatusCode);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);

        _logger.LogInformation("[ECircularService] Download successful. Size={Size} bytes", bytes.Length);

        return bytes;
    }

    /// <summary>
    /// Applies watermark to PDF entirely in memory — no temp files written to disk.
    /// </summary>
    private static byte[] ApplyWatermarkInMemory(byte[] pdfBytes, string pfNo)
    {
        var generatedOn = AppTime.Now;

        using var inputStream = new MemoryStream(pdfBytes);
        using var document = PdfSharpCore.Pdf.IO.PdfReader.Open(
            inputStream, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Modify);

        if (document.PageCount == 0)
            throw new InvalidOperationException("Input PDF has no pages.");

        for (var i = 0; i < document.PageCount; i++)
        {
            var page = document.Pages[i];
            using var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(
                page, PdfSharpCore.Drawing.XGraphicsPdfPageOptions.Append);

            DrawWatermark(gfx, page, pfNo, generatedOn);
        }

        using var outputStream = new MemoryStream();
        document.Save(outputStream, false);
        return outputStream.ToArray();
    }

    private static void DrawWatermark(
        PdfSharpCore.Drawing.XGraphics gfx,
        PdfSharpCore.Pdf.PdfPage page,
        string pfNo,
        DateTime generatedOn)
    {
        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;

        var centerLine1 = $"PF No {pfNo}";
        var centerLine2 = generatedOn.ToString("dd-MM-yyyy HH:mm:ss");
        var bottomLine1 = $"{pfNo} || {generatedOn:dd-MM-yyyy HH:mm:ss}";
        var bottomLine2 = "Confidential do not share";

        var grayBrush = new PdfSharpCore.Drawing.XSolidBrush(
            PdfSharpCore.Drawing.XColor.FromArgb(105, 119, 119, 119));
        var redBrush = new PdfSharpCore.Drawing.XSolidBrush(
            PdfSharpCore.Drawing.XColor.FromArgb(130, 200, 30, 30));

        var centerFont1 = new PdfSharpCore.Drawing.XFont("Arial", 34, PdfSharpCore.Drawing.XFontStyle.Bold);
        var centerFont2 = new PdfSharpCore.Drawing.XFont("Arial", 24, PdfSharpCore.Drawing.XFontStyle.Bold);
        var bottomFont1 = new PdfSharpCore.Drawing.XFont("Arial", 14, PdfSharpCore.Drawing.XFontStyle.Bold);
        var bottomFont2 = new PdfSharpCore.Drawing.XFont("Arial", 13, PdfSharpCore.Drawing.XFontStyle.Bold);

        // Center rotated watermark
        var state = gfx.Save();
        gfx.TranslateTransform(pageWidth / 2, pageHeight / 2);
        gfx.RotateTransform(-45);
        gfx.DrawString(centerLine1, centerFont1, grayBrush,
            new PdfSharpCore.Drawing.XRect(-250, -30, 500, 45),
            PdfSharpCore.Drawing.XStringFormats.Center);
        gfx.DrawString(centerLine2, centerFont2, grayBrush,
            new PdfSharpCore.Drawing.XRect(-250, 15, 500, 35),
            PdfSharpCore.Drawing.XStringFormats.Center);
        gfx.Restore(state);

        // Bottom horizontal watermark
        gfx.DrawString(bottomLine1, bottomFont1, grayBrush,
            new PdfSharpCore.Drawing.XRect(0, pageHeight - 55, pageWidth, 22),
            PdfSharpCore.Drawing.XStringFormats.Center);
        gfx.DrawString(bottomLine2, bottomFont2, redBrush,
            new PdfSharpCore.Drawing.XRect(0, pageHeight - 35, pageWidth, 20),
            PdfSharpCore.Drawing.XStringFormats.Center);
    }

    #endregion

    #region Special Listings

    public async Task<ECircularSearchResponse> GetSpecialListingAsync(string key, int pageNo, CancellationToken ct = default)
    {
        var option = await GetSpecialListingOptionAsync(key, ct);

        if (option is null)
            throw new InvalidOperationException($"Special listing '{key}' is not configured.");

        if (option.OpenInNewTab)
            throw new InvalidOperationException($"Special listing '{key}' is an external URL and cannot be loaded inline.");

        try
        {
            var resolvedUrl = ResolveSpecialListingUrl(option.UrlTemplate, pageNo);
            _logger.LogInformation("[ECircularService] Loading special listing '{Key}': {Url}", key, resolvedUrl);

            var client = _httpClientFactory.CreateClient("ECircularDms");
            using var response = await client.GetAsync(resolvedUrl, ct);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(ct);
            return ParseSearchHtml(html);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "[ECircularService] Special listing '{Key}' failed.", key);
            return new ECircularSearchResponse();
        }
    }

    public async Task<string> GetSpecialListingUrl(string key, CancellationToken ct = default)
    {
        var option = await GetSpecialListingOptionAsync(key, ct);

        if (option is null)
            throw new InvalidOperationException($"Special listing '{key}' is not configured.");

        var url = option.UrlTemplate.Replace("{pageNo}", "0");
        return ResolveDmsDocumentUrl(url);
    }

    private async Task<SpecialListingOption?> GetSpecialListingOptionAsync(string key, CancellationToken ct)
    {
        try
        {
            await using var connection = new OracleConnection(ConnectionString);
            await connection.OpenAsync(ct);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT LISTING_KEY, DISPLAY_NAME, URL_TEMPLATE, OPEN_IN_NEW_TAB
                                FROM E_CIRCULAR_SPECIAL_LISTING
                                WHERE IS_ACTIVE = 'Y' AND UPPER(LISTING_KEY) = UPPER(:key)";
            cmd.Parameters.Add(new OracleParameter("key", key));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new SpecialListingOption(
                    Key: reader.GetString(0),
                    DisplayName: reader.GetString(1),
                    UrlTemplate: reader.GetString(2),
                    OpenInNewTab: reader.GetString(3) == "Y"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ECircularService] Failed to load special listing option '{Key}'.", key);
        }

        return null;
    }

    private string ResolveSpecialListingUrl(string urlTemplate, int pageNo)
    {
        var url = urlTemplate.Replace("{pageNo}", pageNo.ToString());

        // If already absolute, return as-is
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        // Otherwise resolve relative to DMS base URL
        return new Uri(GetBaseDmsUri(), url).ToString();
    }

    #endregion

    #region Helpers

    private static (string DocumentLink, string PublishDate) SplitCircularLink(string link)
    {
        var parts = link.Split("///", StringSplitOptions.None);
        var documentLink = HttpUtility.HtmlDecode(parts[0].Trim());
        var publishDate = parts.Length > 1
            ? parts[1].Trim().Replace("\u00A0", " ")
            : string.Empty;
        return (documentLink, publishDate);
    }

    private string ResolveDmsDocumentUrl(string documentLink)
    {
        if (string.IsNullOrWhiteSpace(documentLink))
            throw new ArgumentException("Circular document link is blank.");

        var decoded = HttpUtility.HtmlDecode(documentLink.Trim());
        if (Uri.TryCreate(decoded, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        return new Uri(GetBaseDmsUri(), decoded.TrimStart('/')).ToString();
    }

    private Uri GetBaseDmsUri()
    {
        var baseUrl = _options.BaseDmsUrl.Trim();
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return new Uri(baseUrl);
    }

    #endregion


}
