using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed class PdfChunk
{
    public int Index { get; set; }
    public List<int> Pages { get; set; } = new();
    public string Text { get; set; } = "";
    public string? DetectedId { get; set; }
    public bool IsUncertain { get; set; }
    public string PageRange { get; set; } = "";
}

public static class PdfChunking
{
    private static readonly string[] MarkerRegexes =
    {
        @"(?im)^\s*(Haltungsname|Haltungsnahme|Haltungs?nummer|Haltung\s*Nr\.?|Haltung[\s\-]?ID|Haltungs[-\s]?ID|Leitung[\s\-]?ID|Leitung\s*Nr\.?|Leitungsnummer)\s*[:\-]?\s*(?<id>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\s*$",
        @"(?im)^\s*(?<id>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\s+\d{2}\.\d{2}\.\d{4}\b",
        @"(?im)^\s*(Haltung|Leitung)\s+(?<id>[\w\.\-\/]+)\s*$",
        @"(?im)Haltungsinspektion\s+-\s+\d{2}\.\d{2}\.\d{4}\s+-\s+(?<id>\S+)",
        @"(?im)^Leitung\s{2,}(?<id>\d+[\-\.]\d+[\.\d]*)",
    };

    public static List<PdfChunk> SplitIntoHaltungChunks(IReadOnlyList<string> pagesText, PdfParser parser)
    {
        var chunks = new List<PdfChunk>();
        if (pagesText is null || pagesText.Count == 0)
            return chunks;

        int chunkIndex = 0;
        PdfChunk? current = null;

        for (int p = 0; p < pagesText.Count; p++)
        {
            var pageNumber = p + 1;
            var pageText = (pagesText[p] ?? "").Replace("\r\n", "\n").Trim();
            if (string.IsNullOrWhiteSpace(pageText))
                continue;
            var pageParsed = HoldingFolderDistributor.ParsePdfPage(pageText);

            var lines = pageText.Split('\n');
            var markers = new List<(int LineIndex, string Id)>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                string? id = null;

                foreach (var rx in MarkerRegexes)
                {
                    var m = Regex.Match(line, rx);
                    if (m.Success)
                    {
                        id = m.Groups["id"].Value?.Trim();
                        if (!string.IsNullOrWhiteSpace(id))
                            break;
                    }
                }

                // Pattern: Leitungsgrafik / Leitungsbildbericht mit ID auf gleicher Zeile
                if (string.IsNullOrWhiteSpace(id) && Regex.IsMatch(line, @"(?im)^(Leitungsgrafik|Leitungsbildbericht)"))
                {
                    var m = Regex.Match(line, @"(?im)Leitung\s{2,}(?<id>\d+[\-\.]\d+[\.\d]*)");
                    if (m.Success)
                        id = m.Groups["id"].Value?.Trim();
                }

                if (!string.IsNullOrWhiteSpace(id))
                {
                    id = NormalizeMarkerId(id);
                    if (!IsLikelyHaltungId(id))
                        id = null;
                }

                if (!string.IsNullOrWhiteSpace(id))
                    markers.Add((i, id!));
            }

            // Segmente pro Seite
            var segments = new List<(string Text, string? Id, List<int> Pages)>();

            if (markers.Count <= 1)
            {
                string? segId = markers.Count == 1 ? markers[0].Id : null;
                if (string.IsNullOrWhiteSpace(segId) && pageParsed.Success && IsLikelyHaltungId(pageParsed.Haltung))
                    segId = NormalizeMarkerId(pageParsed.Haltung!);
                segments.Add((pageText, segId, new List<int> { pageNumber }));
            }
            else
            {
                var firstIndex = markers[0].LineIndex;
                for (int mi = 0; mi < markers.Count; mi++)
                {
                    var start = markers[mi].LineIndex;
                    var end = (mi < markers.Count - 1) ? markers[mi + 1].LineIndex - 1 : lines.Length - 1;

                    var segLines = new List<string>();
                    if (mi == 0 && firstIndex > 0)
                        segLines.AddRange(lines.Take(firstIndex));

                    if (start <= end)
                        segLines.AddRange(lines.Skip(start).Take(end - start + 1));

                    segments.Add((string.Join("\n", segLines), markers[mi].Id, new List<int> { pageNumber }));
                }
            }

            foreach (var seg in segments)
            {
                var segText = seg.Text;
                var segId = !string.IsNullOrWhiteSpace(seg.Id) ? seg.Id : GetHaltungKeyFromChunk(segText, parser);

                if (current is not null
                    && !string.IsNullOrWhiteSpace(segId)
                    && !string.IsNullOrWhiteSpace(current.DetectedId)
                    && string.Equals(segId, current.DetectedId, StringComparison.Ordinal))
                {
                    current.Text += "\n" + segText;
                    current.Pages.AddRange(seg.Pages);
                }
                else if (current is not null && string.IsNullOrWhiteSpace(segId))
                {
                    current.Text += "\n" + segText;
                    current.Pages.AddRange(seg.Pages);
                }
                else
                {
                    if (current is not null)
                        chunks.Add(current);

                    chunkIndex++;
                    current = new PdfChunk
                    {
                        Index = chunkIndex,
                        Pages = new List<int>(seg.Pages),
                        Text = segText,
                        DetectedId = segId,
                        IsUncertain = false
                    };
                }
            }
        }

        if (current is not null)
            chunks.Add(current);

        // Finalize
        foreach (var c in chunks)
        {
            if (string.IsNullOrWhiteSpace(c.DetectedId))
                c.IsUncertain = true;

            var pageList = c.Pages.Distinct().OrderBy(x => x).ToList();
            c.PageRange = pageList.Count switch
            {
                0 => "",
                1 => $"{pageList[0]}",
                _ => $"{pageList.First()}-{pageList.Last()}"
            };
        }

        return chunks;
    }

    public static string? GetHaltungKeyFromChunk(string textChunk, PdfParser parser)
    {
        if (string.IsNullOrWhiteSpace(textChunk))
            return null;

        try
        {
            var parsed = parser.ParseFields(textChunk);
            if (parsed.TryGetValue("Haltungsname", out var hn) && !string.IsNullOrWhiteSpace(hn))
            {
                var first = hn.Split('\n')[0].Trim();
                if (!string.IsNullOrWhiteSpace(first))
                    return NormalizeMarkerId(first);
            }
        }
        catch { /* ignore */ }

        foreach (var rx in MarkerRegexes)
        {
            var m = Regex.Match(textChunk, rx);
            if (m.Success)
            {
                var id = m.Groups["id"].Value?.Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    id = NormalizeMarkerId(id);
                    if (IsLikelyHaltungId(id))
                        return id;
                }
            }
        }

        return null;
    }

    private static string NormalizeMarkerId(string value)
    {
        var normalized = (value ?? "").Trim();
        normalized = Regex.Replace(normalized, @"\s+", "");
        normalized = normalized.Replace("/", "-");
        return normalized;
    }

    private static bool IsLikelyHaltungId(string? value)
        => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, @"^\d[\d\.]*-\d[\d\.]*$");
}
