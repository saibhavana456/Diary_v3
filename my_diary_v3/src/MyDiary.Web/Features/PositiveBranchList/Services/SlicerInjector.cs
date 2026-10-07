using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MyDiary.Web.Features.PositiveBranchList.Services;

/// <summary>
/// Post-processes a ClosedXML-written .xlsx to add real, native Excel Table Slicers —
/// ClosedXML itself has no slicer support (verified: no Slicer-related types in the library at
/// all), so this hand-writes the OOXML parts Excel needs (slicerCache/slicer/drawing parts,
/// plus the relationship and extLst wiring that ties them to the worksheet and workbook), based
/// on the exact structure of the user-supplied "Visakhapatnam.xlsx" reference file.
///
/// Key discovery from that reference file that makes this practical: a modern "table slicer
/// cache" (x15:tableSlicerCache) does NOT embed a static list of items — it just points at
/// (tableId, columnIndex) and Excel computes the distinct values live from the table each time
/// it opens. That's exactly why the same slicer definition works unmodified for every zone: the
/// RO list "varies for regions based on the zone" automatically, because each zone's report has
/// its own Base table with only that zone's RO values in it.
///
/// Everything here operates on the Base sheet only, adding three slicers (RO / Parameters /
/// Range) bound to the Base table's respective columns, plus the same purple
/// "Click on RO/Parameter/Range..." instruction box as the reference file.
/// </summary>
public static class SlicerInjector
{
    private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] AddBaseSheetSlicers(byte[] xlsxBytes, string baseSheetName, string tableName)
    {
        using var input = new MemoryStream(xlsxBytes);
        using var output = new MemoryStream();
        input.CopyTo(output);
        output.Position = 0;

        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            var workbookXml = ReadEntry(zip, "xl/workbook.xml");
            var workbookRelsXml = ReadEntry(zip, "xl/_rels/workbook.xml.rels");
            var contentTypesXml = ReadEntry(zip, "[Content_Types].xml");

            // 1. Find the Base sheet's r:id in workbook.xml, then its worksheet part path via workbook.xml.rels.
            var sheetRId = ExtractAttr(workbookXml, $@"<x:sheet name=""{Regex.Escape(baseSheetName)}""[^>]*r:id=""([^""]+)""");
            var sheetPartPath = ResolveRelTarget(workbookRelsXml, sheetRId, "xl/");
            var sheetRelsPartPath = ToRelsPath(sheetPartPath);

            var sheetXml = ReadEntry(zip, sheetPartPath);
            var sheetRelsXml = ReadEntry(zip, sheetRelsPartPath) ?? EmptyRelationships();

            // 2. Find the table part (via the worksheet's own rels' "table" relationship), then read its id/name/columns.
            var tablePartPath = ResolveRelTargetDirect(sheetRelsXml, "relationships/table");
            var tableXml = ReadEntry(zip, tablePartPath)
                ?? throw new InvalidOperationException("Could not locate the Base table part to attach slicers to.");

            var tableId = ExtractAttr(tableXml, @"<x:table[^>]*\sid=""([^""]+)""") ?? "1";
            var columnIndex = ExtractTableColumnIndices(tableXml);

            // A table with a slicer attached needs xr:uid (table) / xr3:uid (each column) revision
            // metadata — ClosedXML never writes these (it doesn't know about slicers), and the
            // reference file's own table1.xml carries them on every element. Omitting them is what
            // actually triggered "Repaired Records: Slicer Cache" — the cache/slicer parts
            // themselves were already schema-valid; Excel just couldn't correlate them to a table
            // lacking the revision identity it expects once a slicer references that table.
            var patchedTableXml = PatchTableWithUids(tableXml);
            WriteEntry(zip, tablePartPath, patchedTableXml);

            // 3. Build the three slicer-cache parts (RO / Parameters / Range), the slicer part, and the drawing part.
            var caches = new[]
            {
                ("RO", "PblSlicerCacheRO", columnIndex["RO"]),
                ("Parameters", "PblSlicerCacheParameters", columnIndex["Parameters"]),
                ("Range", "PblSlicerCacheRange", columnIndex["Range"])
            };

            for (int i = 0; i < caches.Length; i++)
            {
                var (sourceName, cacheName, col) = caches[i];
                WriteEntry(zip, $"xl/slicerCaches/pblSlicerCache{i + 1}.xml", BuildSlicerCacheXml(cacheName, sourceName, tableId, col));
            }

            WriteEntry(zip, "xl/slicers/pblSlicer1.xml", BuildSlicerPartXml(caches.Select((c, i) => (c.Item2, c.Item1)).ToArray()));
            WriteEntry(zip, "xl/drawings/pblSlicerDrawing.xml", BuildDrawingXml());

            // 4. Wire relationships: workbook -> 3 slicerCaches; worksheet -> slicer part + drawing part.
            var newWbRelIds = new List<string>();
            var (updatedWbRels, wbNextIds) = AppendRelationships(workbookRelsXml,
                Enumerable.Range(1, 3).Select(i => (
                    Type: "http://schemas.microsoft.com/office/2007/relationships/slicerCache",
                    Target: $"/xl/slicerCaches/pblSlicerCache{i}.xml")).ToList());
            newWbRelIds.AddRange(wbNextIds);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", updatedWbRels);

            var (updatedSheetRels, sheetNextIds) = AppendRelationships(sheetRelsXml, new List<(string Type, string Target)>
            {
                ("http://schemas.microsoft.com/office/2007/relationships/slicer", "/xl/slicers/pblSlicer1.xml"),
                ("http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing", "/xl/drawings/pblSlicerDrawing.xml")
            });
            WriteEntry(zip, sheetRelsPartPath, updatedSheetRels);
            var slicerPartRId = sheetNextIds[0];
            var drawingRId = sheetNextIds[1];

            // 5. Patch worksheet XML: xr/xr2/xr3 namespaces + xr:uid on the <x:worksheet> root itself
            //    (the reference file's Base sheet has these; a sheet carrying a slicer apparently
            //    needs the same revision-tracking identity the table does — this was still missing
            //    after the table-level fix, which is why that fix alone didn't clear the repair),
            //    then <x:drawing/> before <x:tableParts>, then <x:extLst> with the slicerList at the end.
            var patchedSheet = PatchWorksheetRootWithUid(sheetXml);
            if (!patchedSheet.Contains("<x:drawing "))
            {
                patchedSheet = patchedSheet.Contains("<x:tableParts")
                    ? patchedSheet.Replace("<x:tableParts", $@"<x:drawing r:id=""{drawingRId}""/><x:tableParts")
                    : patchedSheet.Replace("</x:worksheet>", $@"<x:drawing r:id=""{drawingRId}""/></x:worksheet>");
            }
            var slicerListExt = $@"<x:extLst><x:ext uri=""{{3A4CF648-6AED-40f4-86FF-DC5316D8AED3}}"" xmlns:x15=""http://schemas.microsoft.com/office/spreadsheetml/2010/11/main""><x14:slicerList xmlns:x14=""http://schemas.microsoft.com/office/spreadsheetml/2009/9/main""><x14:slicer r:id=""{slicerPartRId}""/></x14:slicerList></x:ext></x:extLst>";
            patchedSheet = patchedSheet.Replace("</x:worksheet>", slicerListExt + "</x:worksheet>");
            WriteEntry(zip, sheetPartPath, patchedSheet);

            // 6. Patch workbook.xml: <x:extLst> with x15:slicerCaches listing the 3 workbook-level rIds.
            var slicerCachesExt = $@"<x:extLst><x:ext uri=""{{46BE6895-7355-4a93-B00E-2C351335B9C9}}"" xmlns:x15=""http://schemas.microsoft.com/office/spreadsheetml/2010/11/main""><x15:slicerCaches xmlns:x14=""http://schemas.microsoft.com/office/spreadsheetml/2009/9/main"">{string.Join("", newWbRelIds.Select(id => $@"<x14:slicerCache r:id=""{id}""/>"))}</x15:slicerCaches></x:ext></x:extLst>";
            var patchedWorkbook = workbookXml.Replace("</x:workbook>", slicerCachesExt + "</x:workbook>");
            WriteEntry(zip, "xl/workbook.xml", patchedWorkbook);

            // 7. Patch [Content_Types].xml: explicit Overrides for the new parts (the blanket
            //    Default Extension="xml" rule maps to the WRONG content type — workbook.main — so
            //    every new xml part needs its own Override or Excel treats the file as corrupt).
            var overrides = new StringBuilder();
            for (int i = 1; i <= 3; i++)
                overrides.Append($@"<Override PartName=""/xl/slicerCaches/pblSlicerCache{i}.xml"" ContentType=""application/vnd.ms-excel.slicerCache+xml""/>");
            overrides.Append(@"<Override PartName=""/xl/slicers/pblSlicer1.xml"" ContentType=""application/vnd.ms-excel.slicer+xml""/>");
            overrides.Append(@"<Override PartName=""/xl/drawings/pblSlicerDrawing.xml"" ContentType=""application/vnd.openxmlformats-officedocument.drawing+xml""/>");
            var patchedContentTypes = contentTypesXml.Replace("</Types>", overrides + "</Types>");
            WriteEntry(zip, "[Content_Types].xml", patchedContentTypes);
        }

        return output.ToArray();
    }

    private static string PatchWorksheetRootWithUid(string sheetXml)
    {
        if (sheetXml.Contains("xmlns:xr=")) return sheetXml; // already has it — idempotent

        // ClosedXML writes <x:worksheet ... mc:Ignorable="x14ac" xmlns:x="...">. Extend that
        // Ignorable list and add the xr/xr2/xr3 namespaces + xr:uid, matching the reference file's
        // Base worksheet exactly.
        var patched = Regex.Replace(sheetXml, @"mc:Ignorable=""([^""]*)""", m =>
            $@"mc:Ignorable=""{m.Groups[1].Value} xr xr2 xr3""");
        patched = Regex.Replace(patched, @"(<x:worksheet\s)", $@"$1xmlns:xr=""http://schemas.microsoft.com/office/spreadsheetml/2014/revision"" xmlns:xr2=""http://schemas.microsoft.com/office/spreadsheetml/2015/revision2"" xmlns:xr3=""http://schemas.microsoft.com/office/spreadsheetml/2016/revision3"" xr:uid=""{NewUid()}"" ");
        return patched;
    }

    private static string PatchTableWithUids(string tableXml)
    {
        // Add the xr/xr3 namespace declarations + mc:Ignorable to the <x:table> root, and xr:uid on
        // the element itself, unless ClosedXML already wrote them (it doesn't today, but this keeps
        // the patch idempotent if that ever changes).
        var patched = tableXml;
        if (!patched.Contains("xmlns:xr=") && Regex.IsMatch(patched, @"<x:table\s"))
        {
            patched = Regex.Replace(patched, @"(<x:table\s)", $@"$1xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006"" mc:Ignorable=""xr xr3"" xmlns:xr=""http://schemas.microsoft.com/office/spreadsheetml/2014/revision"" xmlns:xr3=""http://schemas.microsoft.com/office/spreadsheetml/2016/revision3"" xr:uid=""{NewUid()}"" ");
        }

        // Add xr3:uid to every <x:tableColumn>.
        patched = Regex.Replace(patched, @"<x:tableColumn\s", m => $@"<x:tableColumn xr3:uid=""{NewUid()}"" ");

        return patched;
    }

    private static Dictionary<string, int> ExtractTableColumnIndices(string tableXml)
    {
        var map = new Dictionary<string, int>();
        var matches = Regex.Matches(tableXml, @"<x:tableColumn id=""(\d+)""[^>]*name=""([^""]+)""");
        foreach (Match m in matches)
            map[m.Groups[2].Value] = int.Parse(m.Groups[1].Value);
        return map;
    }

    // Excel's strict schema validator ("file level validation and repair") discards a slicerCache
    // or slicer element that lacks its xr10:uid attribute, even though mc:Ignorable="xr10" marks
    // that namespace as ignorable-if-unsupported — the attribute itself is still checked. Every
    // element needs its own unique uid.
    private static string NewUid() => "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";

    private static string BuildSlicerCacheXml(string cacheName, string sourceName, string tableId, int column) => $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<slicerCacheDefinition xmlns=""http://schemas.microsoft.com/office/spreadsheetml/2009/9/main"" xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006"" mc:Ignorable=""x xr10"" xmlns:x=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:xr10=""http://schemas.microsoft.com/office/spreadsheetml/2016/revision10"" name=""{cacheName}"" xr10:uid=""{NewUid()}"" sourceName=""{sourceName}""><extLst><x:ext uri=""{{2F2917AC-EB37-4324-AD4E-5DD8C200BD13}}"" xmlns:x15=""http://schemas.microsoft.com/office/spreadsheetml/2010/11/main""><x15:tableSlicerCache tableId=""{tableId}"" column=""{column}""/></x:ext></extLst></slicerCacheDefinition>";

    private static string BuildSlicerPartXml((string CacheName, string Caption)[] slicers)
    {
        var sb = new StringBuilder();
        sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<slicers xmlns=""http://schemas.microsoft.com/office/spreadsheetml/2009/9/main"" xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006"" mc:Ignorable=""x xr10"" xmlns:x=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:xr10=""http://schemas.microsoft.com/office/spreadsheetml/2016/revision10"">");
        foreach (var (cacheName, caption) in slicers)
            sb.Append($@"<slicer name=""{caption}"" xr10:uid=""{NewUid()}"" cache=""{cacheName}"" caption=""{caption}"" columnCount=""1"" style=""SlicerStyleLight2"" rowHeight=""241300""/>");
        sb.Append("</slicers>");
        return sb.ToString();
    }

    private static string BuildDrawingXml()
    {
        // Layout mirrors the reference file: three slicer widgets side by side (RO, Parameters,
        // Range) plus the purple "Click on ... to auto-filter" instruction box to their left,
        // all anchored above the table (rows 3-12, i.e. 0-indexed 2-11).
        string Frame(int id, string name, int fromCol, int toCol) => $@"
<xdr:twoCellAnchor editAs=""absolute"">
  <xdr:from><xdr:col>{fromCol}</xdr:col><xdr:colOff>50000</xdr:colOff><xdr:row>2</xdr:row><xdr:rowOff>50000</xdr:rowOff></xdr:from>
  <xdr:to><xdr:col>{toCol}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>11</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
  <mc:AlternateContent xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006"" xmlns:sle15=""http://schemas.microsoft.com/office/drawing/2012/slicer"">
    <mc:Choice Requires=""sle15"">
      <xdr:graphicFrame macro="""">
        <xdr:nvGraphicFramePr><xdr:cNvPr id=""{id}"" name=""{name}""/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr>
        <xdr:xfrm><a:off x=""0"" y=""0""/><a:ext cx=""0"" cy=""0""/></xdr:xfrm>
        <a:graphic><a:graphicData uri=""http://schemas.microsoft.com/office/drawing/2010/slicer""><sle:slicer xmlns:sle=""http://schemas.microsoft.com/office/drawing/2010/slicer"" name=""{name}""/></a:graphicData></a:graphic>
      </xdr:graphicFrame>
    </mc:Choice>
    <mc:Fallback>
      <xdr:sp macro="""" textlink="""">
        <xdr:nvSpPr><xdr:cNvPr id=""0"" name=""""/><xdr:cNvSpPr><a:spLocks noTextEdit=""1""/></xdr:cNvSpPr></xdr:nvSpPr>
        <xdr:spPr><a:xfrm><a:off x=""0"" y=""0""/><a:ext cx=""2000000"" cy=""1700000""/></a:xfrm><a:prstGeom prst=""rect""><a:avLst/></a:prstGeom><a:solidFill><a:prstClr val=""white""/></a:solidFill><a:ln w=""1""><a:solidFill><a:prstClr val=""green""/></a:solidFill></a:ln></xdr:spPr>
        <xdr:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=""en-IN"" sz=""1100""/><a:t>This shape represents a table slicer. Table slicers are supported in Excel 2013 or later.</a:t></a:r></a:p></xdr:txBody>
      </xdr:sp>
    </mc:Fallback>
  </mc:AlternateContent>
  <xdr:clientData/>
</xdr:twoCellAnchor>";

        var instructionBox = @"
<xdr:twoCellAnchor editAs=""absolute"">
  <xdr:from><xdr:col>0</xdr:col><xdr:colOff>50000</xdr:colOff><xdr:row>2</xdr:row><xdr:rowOff>50000</xdr:rowOff></xdr:from>
  <xdr:to><xdr:col>1</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>11</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
  <xdr:sp macro="""" textlink="""">
    <xdr:nvSpPr><xdr:cNvPr id=""99"" name=""PblInstruction""/><xdr:cNvSpPr/></xdr:nvSpPr>
    <xdr:spPr><a:xfrm><a:off x=""0"" y=""0""/><a:ext cx=""1671432"" cy=""1674352""/></a:xfrm><a:prstGeom prst=""roundRect""><a:avLst><a:gd name=""adj"" fmla=""val 6979""/></a:avLst></a:prstGeom><a:solidFill><a:srgbClr val=""7030A0""/></a:solidFill><a:ln><a:noFill/></a:ln></xdr:spPr>
    <xdr:txBody><a:bodyPr vertOverflow=""clip"" horzOverflow=""clip"" rtlCol=""0"" anchor=""ctr""/><a:lstStyle/><a:p><a:pPr algn=""ctr""/><a:r><a:rPr lang=""en-IN"" sz=""1100""><a:solidFill><a:srgbClr val=""FFFFFF""/></a:solidFill></a:rPr><a:t>Click on RO / Parameter / Range to filter the data below</a:t></a:r></a:p></xdr:txBody>
  </xdr:sp>
  <xdr:clientData/>
</xdr:twoCellAnchor>";

        return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<xdr:wsDr xmlns:xdr=""http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"" xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"">"
            + instructionBox
            + Frame(2, "RO", 1, 3)
            + Frame(3, "Parameters", 3, 5)
            + Frame(4, "Range", 5, 7)
            + "</xdr:wsDr>";
    }

    // ── small XML/rels helpers (regex-based on purpose: these are ClosedXML/Excel-generated,
    //    single-line, well-formed parts — a full XML DOM parser would be safer for arbitrary
    //    input but is unnecessary overhead for patching our own known-shape output) ──────────

    private static string? ReadEntry(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        zip.GetEntry(path)?.Delete();
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string EmptyRelationships() =>
        @"<?xml version=""1.0"" encoding=""utf-8""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""></Relationships>";

    private static string? ExtractAttr(string xml, string pattern)
    {
        var m = Regex.Match(xml, pattern);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Resolves an r:id to its Target path (normalized to a zip-entry path with no leading slash) via a rels file.</summary>
    private static string ResolveRelTarget(string relsXml, string? rId, string basePrefix)
    {
        if (rId is null) throw new InvalidOperationException("Relationship id not found.");
        var m = Regex.Match(relsXml, $@"<Relationship[^>]*Id=""{Regex.Escape(rId)}""[^>]*Target=""([^""]+)""")
                 .Success ? Regex.Match(relsXml, $@"<Relationship[^>]*Id=""{Regex.Escape(rId)}""[^>]*Target=""([^""]+)""")
                 : Regex.Match(relsXml, $@"<Relationship[^>]*Target=""([^""]+)""[^>]*Id=""{Regex.Escape(rId)}""");
        if (!m.Success) throw new InvalidOperationException($"Relationship {rId} not found.");
        var target = m.Groups[1].Value;
        return target.StartsWith('/') ? target.TrimStart('/') : basePrefix + target;
    }

    private static string ResolveRelTargetDirect(string relsXml, string typeContains)
    {
        var m = Regex.Match(relsXml, $@"<Relationship[^>]*Type=""[^""]*{Regex.Escape(typeContains)}""[^>]*Target=""([^""]+)""");
        if (!m.Success) m = Regex.Match(relsXml, $@"<Relationship[^>]*Target=""([^""]+)""[^>]*Type=""[^""]*{Regex.Escape(typeContains)}""");
        if (!m.Success) throw new InvalidOperationException($"No relationship of type containing '{typeContains}' found.");
        var target = m.Groups[1].Value;
        return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
    }

    private static string ToRelsPath(string partPath)
    {
        var idx = partPath.LastIndexOf('/');
        var dir = idx >= 0 ? partPath[..idx] : "";
        var file = idx >= 0 ? partPath[(idx + 1)..] : partPath;
        return $"{dir}/_rels/{file}.rels";
    }

    private static (string UpdatedXml, List<string> NewIds) AppendRelationships(string relsXml, List<(string Type, string Target)> newRels)
    {
        var existingIds = Regex.Matches(relsXml, @"Id=""rId(\d+)""").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        var next = (existingIds.Count > 0 ? existingIds.Max() : 0) + 1;
        var newIds = new List<string>();
        var sb = new StringBuilder();
        foreach (var (type, target) in newRels)
        {
            var id = $"rId{next++}";
            newIds.Add(id);
            sb.Append($@"<Relationship Id=""{id}"" Type=""{type}"" Target=""{target}""/>");
        }
        return (relsXml.Replace("</Relationships>", sb + "</Relationships>"), newIds);
    }
}
