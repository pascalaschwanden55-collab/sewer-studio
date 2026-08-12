using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

internal static class PdfCodeScanEvidenceReader
{
    private static readonly HashSet<string> VideoExtensions = new(
        new[] { ".mpg", ".mpeg", ".mp4", ".avi", ".mov", ".mkv", ".wmv" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly Regex MalformedBccPattern = new(
        @"(?<![A-Z0-9.])BCC\s*\.\s*YB(?![A-Z0-9.])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IEnumerable<string> EnumeratePdfs(string root)
        => EnumerateFiles(root, new HashSet<string>(new[] { ".pdf" }, StringComparer.OrdinalIgnoreCase));

    public static IEnumerable<string> EnumerateVideos(string root)
        => EnumerateFiles(root, VideoExtensions);

    public static VideoMatch MatchVideo(string pdf, IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
            return new VideoMatch(null, "missing");

        var pdfStem = Path.GetFileNameWithoutExtension(pdf);
        var pdfDirectory = Path.GetDirectoryName(pdf) ?? string.Empty;
        var exactInDirectory = candidates
            .Where(video => string.Equals(Path.GetDirectoryName(video), pdfDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(video => string.Equals(Path.GetFileNameWithoutExtension(video), pdfStem, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (exactInDirectory.Length == 1)
            return new VideoMatch(exactInDirectory[0], "exact_stem_same_folder");
        if (exactInDirectory.Length > 1)
            return new VideoMatch(null, "ambiguous_exact_stem_same_folder");

        var exact = candidates
            .Where(video => string.Equals(Path.GetFileNameWithoutExtension(video), pdfStem, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (exact.Length == 1)
            return new VideoMatch(exact[0], "exact_stem");
        if (exact.Length > 1)
            return new VideoMatch(null, "ambiguous_exact_stem");

        var sameDirectory = candidates
            .Where(video => string.Equals(Path.GetDirectoryName(video), pdfDirectory, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (sameDirectory.Length == 1)
            return new VideoMatch(sameDirectory[0], "single_in_pdf_folder");
        if (sameDirectory.Length > 1)
            return new VideoMatch(null, "ambiguous_pdf_folder");
        return candidates.Count == 1
            ? new VideoMatch(candidates[0], "single_in_holding")
            : new VideoMatch(null, "ambiguous_holding");
    }

    public static PdfEvidence ReadPdfEvidence(string pdf)
    {
        var photoCount = 0;
        var containsMalformedBcc = false;
        try
        {
            using var document = PdfDocument.Open(pdf);
            foreach (var page in document.GetPages())
            {
                var layoutText = ExtractPageLayoutText(page);
                containsMalformedBcc |= MalformedBccPattern.IsMatch(layoutText)
                                        || MalformedBccPattern.IsMatch(page.Text ?? string.Empty);
                try
                {
                    photoCount += page.GetImages().Count(image => image.WidthInSamples >= 200 && image.HeightInSamples >= 150);
                }
                catch
                {
                    // Eine einzelne Bildliste ist fuer den Codescan unkritisch.
                }
            }

            return new PdfEvidence(true, photoCount, containsMalformedBcc);
        }
        catch
        {
            return new PdfEvidence(false, photoCount, containsMalformedBcc);
        }
    }

    private static string ExtractPageLayoutText(UglyToad.PdfPig.Content.Page page)
    {
        var letters = page.Letters;
        if (letters.Count == 0)
            return string.Empty;

        var averageWidth = letters
            .Where(letter => letter.Width > 0
                             && letter.Value?.Length == 1
                             && !char.IsWhiteSpace(letter.Value[0]))
            .Select(letter => letter.Width)
            .DefaultIfEmpty(5.5)
            .Average();
        if (averageWidth < 0.5)
            averageWidth = 5.5;

        var text = new StringBuilder();
        foreach (var lineGroup in letters
                     .GroupBy(letter => Math.Round(letter.StartBaseLine.Y / 2.0) * 2.0)
                     .OrderByDescending(group => group.Key))
        {
            var sorted = lineGroup.OrderBy(letter => letter.StartBaseLine.X).ToArray();
            if (sorted.Length == 0)
                continue;

            var line = new StringBuilder();
            var previousEnd = sorted[0].StartBaseLine.X;
            foreach (var letter in sorted)
            {
                var gap = letter.StartBaseLine.X - previousEnd;
                if (gap > averageWidth * 0.5)
                {
                    var spaces = Math.Max(1, (int)Math.Round(gap / averageWidth));
                    line.Append(' ', Math.Min(spaces, 80));
                }

                line.Append(letter.Value ?? string.Empty);
                previousEnd = letter.StartBaseLine.X + (letter.Width > 0 ? letter.Width : averageWidth);
            }

            if (line.Length > 0)
                text.AppendLine(line.ToString().TrimEnd());
        }

        return text.ToString();
    }

    private static IEnumerable<string> EnumerateFiles(string root, IReadOnlySet<string> extensions)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        return Directory.EnumerateFiles(root, "*", options)
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }
}
