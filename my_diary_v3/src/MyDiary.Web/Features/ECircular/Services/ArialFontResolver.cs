using PdfSharpCore.Fonts;

namespace MyDiary.Web.Features.ECircular.Services;

/// <summary>
/// Custom PdfSharpCore font resolver that loads the Arial TTF files bundled
/// under <c>wwwroot/css/fonts</c> instead of relying on OS-installed fonts.
///
/// Why this is needed:
/// PdfSharpCore resolves fonts via <see cref="IFontResolver"/>. On Windows there's a
/// built-in mechanism that reads OS fonts, so <c>XFont("Arial", ...)</c> "just works".
/// On Linux/K8s containers Arial is not installed — the container image typically ships
/// with a minimal set of fonts (or none), causing a "font not found" error.
///
/// This resolver reads the font bytes from wwwroot/css/fonts at startup so the same code
/// works on any OS without needing to install system fonts in the container.
///
/// Register once, before any PDF is generated:
/// <code>PdfSharpCore.Fonts.GlobalFontSettings.FontResolver = new ArialFontResolver(fontsDir);</code>
/// </summary>
public sealed class ArialFontResolver : IFontResolver
{
    // Face keys returned by ResolveTypeface and looked up in GetFont.
    private const string FaceRegular    = "Arial#Regular";
    private const string FaceBold       = "Arial#Bold";
    private const string FaceItalic     = "Arial#Italic";
    private const string FaceBoldItalic = "Arial#BoldItalic";

    private readonly Dictionary<string, byte[]> _fontData;

    /// <summary>
    /// Required by <see cref="IFontResolver"/>. Returns the default font family name.
    /// </summary>
    public string DefaultFontName => "Arial";

    public ArialFontResolver(string fontsDirectory)
    {
        if (string.IsNullOrWhiteSpace(fontsDirectory))
            throw new ArgumentException("Fonts directory path is required.", nameof(fontsDirectory));

        _fontData = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            [FaceRegular]    = ReadFont(fontsDirectory, "arial.ttf"),
            [FaceBold]       = ReadFont(fontsDirectory, "arialbd.ttf"),
            [FaceItalic]     = ReadFont(fontsDirectory, "ariali.ttf"),
            [FaceBoldItalic] = ReadFont(fontsDirectory, "arialbi.ttf"),
        };
    }

    public byte[] GetFont(string faceName)
    {
        if (_fontData.TryGetValue(faceName, out var bytes))
            return bytes;

        // Fallback: if an unknown face is requested, return the regular variant
        // rather than throwing — PdfSharpCore may probe alternative face names.
        return _fontData[FaceRegular];
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Only intercept Arial. Return null for any other font so PdfSharpCore
        // falls back to its default platform font resolution (system fonts on Windows,
        // fontconfig on Linux if available). This avoids side effects on other
        // features that use different fonts (e.g. Times New Roman in Procurement PDFs).
        if (!string.Equals(familyName, "Arial", StringComparison.OrdinalIgnoreCase))
            return null;

        var faceName = (isBold, isItalic) switch
        {
            (true, true)   => FaceBoldItalic,
            (true, false)  => FaceBold,
            (false, true)  => FaceItalic,
            _              => FaceRegular
        };

        return new FontResolverInfo(faceName);
    }

    private static byte[] ReadFont(string directory, string fileName)
    {
        // Try exact filename first, then case-insensitive search (important on Linux
        // where file systems are case-sensitive).
        var path = Path.Combine(directory, fileName);
        if (File.Exists(path))
            return File.ReadAllBytes(path);

        // Case-insensitive fallback: scan the directory for a matching file name.
        if (Directory.Exists(directory))
        {
            var match = Directory.GetFiles(directory)
                .FirstOrDefault(f => string.Equals(
                    Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return File.ReadAllBytes(match);
        }

        throw new FileNotFoundException(
            $"Required Arial font file not found: {path}. " +
            "Ensure the TTF files exist under wwwroot/css/fonts and are included in the publish output.",
            path);
    }
}
