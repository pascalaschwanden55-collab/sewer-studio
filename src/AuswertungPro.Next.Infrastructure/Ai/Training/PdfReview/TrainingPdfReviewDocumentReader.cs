using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

internal sealed record TrainingPdfEmbeddedPhoto(
    int PageNumber,
    int PhotoIndex,
    byte[] ImageBytes,
    string Extension,
    string ContextText)
{
    /// <summary>
    /// Explizite Haltung des aktuellen PDF-Abschnitts. Bei Sammel-PDFs darf
    /// dieser Wert vom Datei-/Ordnernamen abweichen.
    /// </summary>
    public string? SectionHaltungId { get; init; }

    /// <summary>
    /// True, wenn der aktuelle Abschnitt mehrere gleich starke Haltungs-IDs
    /// enthaelt. Solche Fotos duerfen nicht automatisch uebernommen werden.
    /// </summary>
    public bool HasAmbiguousSectionHaltung { get; init; }

    /// <summary>
    /// Ausschliesslich der Text des sicher zugeordneten Haltungsabschnitts.
    /// Bei Sammel-PDFs duerfen Befunddetails nur daraus ergaenzt werden.
    /// </summary>
    public string? SectionText { get; init; }
}

internal sealed record TrainingPdfReviewDocument(
    int PageCount,
    string DocumentText,
    IReadOnlyList<TrainingPdfEmbeddedPhoto> Photos,
    IReadOnlyList<string> Issues);

internal interface ITrainingPdfReviewDocumentReader
{
    TrainingPdfReviewDocument Read(string pdfPath, CancellationToken cancellationToken);
}

/// <summary>
/// Liest nur grosse, wirklich platzierte PDF-Bilder. Kleine Logos und ganzseitige
/// Scanbilder werden nicht als Kanal-Foto behandelt. Text wird dem geometrisch
/// naechsten Foto zugeordnet, damit Ein- und Zweispaltenberichte funktionieren.
/// Kumulative Byte- und Pixelgrenzen verhindern, dass viele gueltige Einzelbilder
/// den Arbeitsspeicher erschoepfen; eine Ueberschreitung stoppt den Import vollstaendig.
/// </summary>
internal sealed class TrainingPdfReviewDocumentReader : ITrainingPdfReviewDocumentReader
{
    private const double MinimumDisplayedWidthRatio = 0.18;
    private const double MinimumDisplayedHeightRatio = 0.10;
    private const double MinimumDisplayedAreaRatio = 0.025;
    private const double MaximumDisplayedAreaRatio = 0.70;
    private const double MaximumTextDistanceRatio = 0.12;
    private const long DefaultMaximumTotalPhotoBytes = 256L * 1024 * 1024;
    private const long DefaultMaximumTotalPhotoPixels = 250_000_000;

    private readonly long _maximumTotalPhotoBytes;
    private readonly long _maximumTotalPhotoPixels;
    private readonly TrainingPdfEmbeddedImageReader _embeddedImageReader;

    public TrainingPdfReviewDocumentReader()
        : this(
            DefaultMaximumTotalPhotoBytes,
            DefaultMaximumTotalPhotoPixels,
            jpegColorNormalizer: null)
    {
    }

    internal TrainingPdfReviewDocumentReader(
        long maximumTotalPhotoBytes,
        long maximumTotalPhotoPixels)
        : this(
            maximumTotalPhotoBytes,
            maximumTotalPhotoPixels,
            jpegColorNormalizer: null)
    {
    }

    internal TrainingPdfReviewDocumentReader(
        ITrainingPdfJpegColorNormalizer jpegColorNormalizer)
        : this(
            DefaultMaximumTotalPhotoBytes,
            DefaultMaximumTotalPhotoPixels,
            jpegColorNormalizer)
    {
    }

    private TrainingPdfReviewDocumentReader(
        long maximumTotalPhotoBytes,
        long maximumTotalPhotoPixels,
        ITrainingPdfJpegColorNormalizer? jpegColorNormalizer)
    {
        if (maximumTotalPhotoBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTotalPhotoBytes),
                "Das gesamte PDF-Fotobudget muss positiv sein.");
        }

        if (maximumTotalPhotoPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTotalPhotoPixels),
                "Das gesamte PDF-Pixelbudget muss positiv sein.");
        }

        _maximumTotalPhotoBytes = maximumTotalPhotoBytes;
        _maximumTotalPhotoPixels = maximumTotalPhotoPixels;
        _embeddedImageReader = new TrainingPdfEmbeddedImageReader(
            jpegColorNormalizer);
    }

    public TrainingPdfReviewDocument Read(
        string pdfPath,
        CancellationToken cancellationToken)
    {
        PdfImportSafetyPolicy.ThrowIfFileTooLarge(pdfPath);
        using var document = PdfDocument.Open(pdfPath);
        PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);

        var photos = new List<TrainingPdfEmbeddedPhoto>();
        var issues = new List<string>();
        var documentText = new System.Text.StringBuilder();
        long totalPhotoBytes = 0;
        long totalPhotoPixels = 0;
        string? currentSectionHaltungId = null;
        var currentSectionHaltungAmbiguous = false;
        var sectionAliases = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var conflictingSectionAliases = new List<string>();
        var knownCanonicalSections = new List<string>();
        var sectionTexts = new Dictionary<string, System.Text.StringBuilder>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rawPageText = BuildText(page.Letters);
            var pageEncodingShift = PdfFontEncodingDecoder.DetectShift(rawPageText);
            var pageText = pageEncodingShift > 0
                ? PdfFontEncodingDecoder.ShiftAllChars(
                    rawPageText,
                    pageEncodingShift)
                : rawPageText;
            documentText.AppendLine($"--- Seite {page.Number} ---");
            documentText.AppendLine(pageText);
            var pageHaltung = TrainingPdfProtocolMetadataParser
                .ResolvePageHaltung(pageText);
            if (pageHaltung.IsAmbiguous)
            {
                currentSectionHaltungId = null;
                currentSectionHaltungAmbiguous = true;
            }
            else if (pageHaltung.HaltungId is not null)
            {
                switch (pageHaltung.Source)
                {
                    case TrainingPdfPageHaltungSource.InspectionTitle:
                        RegisterCanonicalSection(
                            knownCanonicalSections,
                            sectionAliases,
                            conflictingSectionAliases,
                            pageHaltung.HaltungId);
                        RegisterTrustedSectionAliases(
                            sectionAliases,
                            conflictingSectionAliases,
                            knownCanonicalSections,
                            pageHaltung.HaltungId,
                            pageHaltung.AlternateHaltungIds);
                        currentSectionHaltungId = pageHaltung.HaltungId;
                        currentSectionHaltungAmbiguous = false;
                        break;
                    case TrainingPdfPageHaltungSource.PhotoTitle:
                        if (TryResolveCanonicalSection(
                                knownCanonicalSections,
                                pageHaltung.HaltungId,
                                out var canonicalSection))
                        {
                            currentSectionHaltungId = canonicalSection;
                            currentSectionHaltungAmbiguous = false;
                        }
                        else if (IsConflictingSectionAlias(
                                conflictingSectionAliases,
                                pageHaltung.HaltungId))
                        {
                            currentSectionHaltungId = null;
                            currentSectionHaltungAmbiguous = true;
                        }
                        else
                        {
                            currentSectionHaltungId = TryResolveSectionAlias(
                                sectionAliases,
                                pageHaltung.HaltungId,
                                out var knownCanonical)
                                ? knownCanonical
                                : pageHaltung.HaltungId;
                            currentSectionHaltungAmbiguous = false;
                            if (!TryResolveSectionAlias(
                                    sectionAliases,
                                    pageHaltung.HaltungId,
                                    out _))
                            {
                                RegisterCanonicalSection(
                                    knownCanonicalSections,
                                    sectionAliases,
                                    conflictingSectionAliases,
                                    pageHaltung.HaltungId);
                            }
                        }

                        break;
                    default:
                        RegisterCanonicalSection(
                            knownCanonicalSections,
                            sectionAliases,
                            conflictingSectionAliases,
                            pageHaltung.HaltungId);
                        currentSectionHaltungId = pageHaltung.HaltungId;
                        currentSectionHaltungAmbiguous = false;
                        break;
                }
            }

            if (!currentSectionHaltungAmbiguous
                && currentSectionHaltungId is not null)
            {
                var sectionText = ResolveSectionTextBuilder(
                    sectionTexts,
                    currentSectionHaltungId);
                sectionText.AppendLine($"--- Seite {page.Number} ---");
                sectionText.AppendLine(pageText);
            }

            IReadOnlyList<PlacedImage> pagePhotos;
            try
            {
                pagePhotos = SelectPhotoImages(page);
            }
            catch (Exception ex)
            {
                issues.Add($"Seite {page.Number}: Bilder konnten nicht gelesen werden ({ex.Message}).");
                continue;
            }

            for (var index = 0; index < pagePhotos.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var placed = pagePhotos[index];
                var declaredPhotoPixels =
                    (long)placed.Image.WidthInSamples * placed.Image.HeightInSamples;
                if (declaredPhotoPixels is > 0
                    and <= TrainingPdfEmbeddedImageReader.MaximumPhotoPixels)
                {
                    // Vor einer moeglichen PNG-Dekodierung stoppen. So wird nicht erst
                    // eine weitere grosse Bildkopie erzeugt und danach verworfen.
                    EnsureWithinBudget(
                        totalPhotoPixels,
                        declaredPhotoPixels,
                        _maximumTotalPhotoPixels,
                        "Pixel-Gesamtlimit",
                        page.Number,
                        index + 1);
                    EnsureWithinBudget(
                        totalPhotoBytes,
                        placed.Image.RawBytes.Count,
                        _maximumTotalPhotoBytes,
                        "Byte-Gesamtlimit",
                        page.Number,
                        index + 1);
                }

                if (!_embeddedImageReader.TryRead(
                        placed.Image,
                        out var bytes,
                        out var extension,
                        out var photoPixels))
                {
                    issues.Add($"Seite {page.Number}, Foto {index + 1}: Bildformat nicht lesbar.");
                    continue;
                }

                EnsureWithinBudget(
                    totalPhotoPixels,
                    photoPixels,
                    _maximumTotalPhotoPixels,
                    "Pixel-Gesamtlimit",
                    page.Number,
                    index + 1);
                EnsureWithinBudget(
                    totalPhotoBytes,
                    bytes.LongLength,
                    _maximumTotalPhotoBytes,
                    "Byte-Gesamtlimit",
                    page.Number,
                    index + 1);
                totalPhotoPixels += photoPixels;
                totalPhotoBytes += bytes.LongLength;

                var rawContext = BuildNearestPhotoText(
                    page.Letters,
                    pagePhotos,
                    index,
                    page.Width,
                    page.Height);
                var context = pageEncodingShift > 0
                    ? PdfFontEncodingDecoder.ShiftAllChars(
                        rawContext,
                        pageEncodingShift)
                    : rawContext;
                photos.Add(new TrainingPdfEmbeddedPhoto(
                    page.Number,
                    index + 1,
                    bytes,
                    extension,
                    context)
                {
                    SectionHaltungId = currentSectionHaltungId,
                    HasAmbiguousSectionHaltung =
                        currentSectionHaltungAmbiguous,
                });
            }
        }

        var materializedSectionTexts = sectionTexts.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        var photosWithSectionText = photos
            .Select(photo =>
            {
                if (photo.SectionHaltungId is null
                    || !TryResolveSectionText(
                        materializedSectionTexts,
                        photo.SectionHaltungId,
                        out var sectionText))
                {
                    return photo;
                }

                return photo with { SectionText = sectionText };
            })
            .ToArray();
        return new TrainingPdfReviewDocument(
            document.NumberOfPages,
            documentText.ToString(),
            photosWithSectionText,
            issues);
    }

    private static System.Text.StringBuilder ResolveSectionTextBuilder(
        IDictionary<string, System.Text.StringBuilder> sectionTexts,
        string sectionHaltungId)
    {
        foreach (var pair in sectionTexts)
        {
            if (TrainingPdfHaltungId.AreEquivalent(
                    pair.Key,
                    sectionHaltungId))
            {
                return pair.Value;
            }
        }

        var normalized = TrainingPdfHaltungId.NormalizeForStorage(
            sectionHaltungId) ?? sectionHaltungId;
        var builder = new System.Text.StringBuilder();
        sectionTexts[normalized] = builder;
        return builder;
    }

    private static bool TryResolveSectionText(
        IEnumerable<KeyValuePair<string, string>> sectionTexts,
        string sectionHaltungId,
        out string sectionText)
    {
        foreach (var pair in sectionTexts)
        {
            if (!TrainingPdfHaltungId.AreEquivalent(
                    pair.Key,
                    sectionHaltungId))
            {
                continue;
            }

            sectionText = pair.Value;
            return true;
        }

        sectionText = string.Empty;
        return false;
    }

    private static void RegisterTrustedSectionAliases(
        IDictionary<string, string> aliases,
        IList<string> conflictingAliases,
        IReadOnlyList<string> knownCanonicalSections,
        string canonical,
        IReadOnlyList<string> candidates)
    {
        var normalizedCandidates = new List<string>();
        foreach (var candidate in candidates)
        {
            var normalized = TrainingPdfHaltungId.NormalizeForStorage(candidate);
            if (normalized is null
                || TrainingPdfHaltungId.AreEquivalent(normalized, canonical)
                || normalizedCandidates.Any(existing =>
                    TrainingPdfHaltungId.AreEquivalent(existing, normalized)))
            {
                continue;
            }

            normalizedCandidates.Add(normalized);
        }

        var hasConflict = normalizedCandidates.Any(candidate =>
            IsConflictingSectionAlias(conflictingAliases, candidate)
            || (TryResolveCanonicalSection(
                    knownCanonicalSections,
                    candidate,
                    out var knownCanonical)
                && !TrainingPdfHaltungId.AreEquivalent(
                    knownCanonical,
                    canonical))
            || (TryResolveSectionAlias(
                    aliases,
                    candidate,
                    out var existingCanonical)
                && !TrainingPdfHaltungId.AreEquivalent(
                    existingCanonical,
                    canonical)));
        if (hasConflict)
        {
            foreach (var candidate in normalizedCandidates)
            {
                RemoveEquivalentSectionAlias(aliases, candidate);
                if (!IsConflictingSectionAlias(
                        conflictingAliases,
                        candidate))
                {
                    conflictingAliases.Add(candidate);
                }
            }

            return;
        }

        foreach (var candidate in normalizedCandidates)
        {
            RemoveEquivalentSectionAlias(aliases, candidate);
            aliases[candidate] = canonical;
        }
    }

    private static void RegisterCanonicalSection(
        IList<string> knownCanonicalSections,
        IDictionary<string, string> aliases,
        IList<string> conflictingAliases,
        string candidate)
    {
        RemoveEquivalentSectionAlias(aliases, candidate);
        for (var index = conflictingAliases.Count - 1; index >= 0; index--)
        {
            if (TrainingPdfHaltungId.AreEquivalent(
                    conflictingAliases[index],
                    candidate))
            {
                conflictingAliases.RemoveAt(index);
            }
        }

        if (TryResolveCanonicalSection(
                knownCanonicalSections,
                candidate,
                out _))
        {
            return;
        }

        var normalized = TrainingPdfHaltungId.NormalizeForStorage(candidate);
        if (normalized is not null)
            knownCanonicalSections.Add(normalized);
    }

    private static bool TryResolveCanonicalSection(
        IEnumerable<string> knownCanonicalSections,
        string candidate,
        out string canonical)
    {
        foreach (var known in knownCanonicalSections)
        {
            if (!TrainingPdfHaltungId.AreEquivalent(known, candidate))
                continue;

            canonical = TrainingPdfHaltungId.PreferCleanAlias(
                known,
                candidate)!;
            return true;
        }

        canonical = string.Empty;
        return false;
    }

    private static bool TryResolveSectionAlias(
        IEnumerable<KeyValuePair<string, string>> aliases,
        string candidate,
        out string canonical)
    {
        foreach (var pair in aliases)
        {
            if (!TrainingPdfHaltungId.AreEquivalent(pair.Key, candidate))
                continue;

            canonical = pair.Value;
            return true;
        }

        canonical = string.Empty;
        return false;
    }

    private static bool IsConflictingSectionAlias(
        IEnumerable<string> conflictingAliases,
        string candidate)
        => conflictingAliases.Any(alias =>
            TrainingPdfHaltungId.AreEquivalent(alias, candidate));

    private static void RemoveEquivalentSectionAlias(
        IDictionary<string, string> aliases,
        string candidate)
    {
        var key = aliases.Keys.FirstOrDefault(alias =>
            TrainingPdfHaltungId.AreEquivalent(alias, candidate));
        if (key is not null)
            aliases.Remove(key);
    }

    private static IReadOnlyList<PlacedImage> SelectPhotoImages(Page page)
    {
        var pageArea = Math.Max(1.0, page.Width * page.Height);
        var selected = new List<PlacedImage>();
        foreach (var image in page.GetImages())
        {
            var bounds = image.Bounds;
            var width = Math.Abs(bounds.Width);
            var height = Math.Abs(bounds.Height);
            var areaRatio = width * height / pageArea;
            if (width < page.Width * MinimumDisplayedWidthRatio
                || height < page.Height * MinimumDisplayedHeightRatio
                || areaRatio < MinimumDisplayedAreaRatio
                || areaRatio > MaximumDisplayedAreaRatio)
            {
                continue;
            }

            // Identische Platzierung ist meist Bild + Maske. Nur den groessten
            // eigentlichen Bildstrom behalten.
            var sameBounds = selected.FindIndex(candidate =>
                NearlyEqual(candidate.Bounds.Left, bounds.Left)
                && NearlyEqual(candidate.Bounds.Bottom, bounds.Bottom)
                && NearlyEqual(candidate.Bounds.Right, bounds.Right)
                && NearlyEqual(candidate.Bounds.Top, bounds.Top));
            var placed = new PlacedImage(image, bounds);
            if (sameBounds < 0)
            {
                selected.Add(placed);
                continue;
            }

            var oldSamples = (long)selected[sameBounds].Image.WidthInSamples
                             * selected[sameBounds].Image.HeightInSamples;
            var newSamples = (long)image.WidthInSamples * image.HeightInSamples;
            if (newSamples > oldSamples)
                selected[sameBounds] = placed;
        }

        return selected
            .OrderByDescending(item => item.Bounds.Top)
            .ThenBy(item => item.Bounds.Left)
            .ToArray();
    }

    private static void EnsureWithinBudget(
        long current,
        long additional,
        long maximum,
        string budgetName,
        int pageNumber,
        int photoNumber)
    {
        if (additional <= maximum - current)
            return;

        throw new InvalidDataException(
            $"Seite {pageNumber}, Foto {photoNumber}: {budgetName} des PDF-Fotolesers " +
            $"ueberschritten ({current} + {additional} > {maximum}). Import abgebrochen.");
    }

    private static string BuildNearestPhotoText(
        IReadOnlyList<Letter> letters,
        IReadOnlyList<PlacedImage> photos,
        int photoIndex,
        double pageWidth,
        double pageHeight)
    {
        var assigned = new List<Letter>();
        foreach (var line in SplitIntoTextLines(letters, pageWidth))
        {
            var assignments = line
                .Select(segment => FindNearestPhoto(
                    segment,
                    photos,
                    pageWidth,
                    pageHeight))
                .ToArray();
            if (!assignments.Any(item =>
                    item.PhotoIndex == photoIndex
                    && item.Distance <= MaximumTextDistanceRatio))
            {
                continue;
            }

            // Tabellenbeschriftungen wie "Zustand | BDA" bestehen oft aus
            // getrennten Textsegmenten derselben Zeile. Sobald ein Segment
            // sicher am Foto liegt, gehoeren die Segmente derselben Zeile mit
            // demselben naechsten Foto ebenfalls zu diesem Fotoblock.
            for (var index = 0; index < line.Count; index++)
            {
                if (assignments[index].PhotoIndex == photoIndex
                    && assignments[index].VerticalDistance
                    <= MaximumTextDistanceRatio)
                {
                    assigned.AddRange(line[index]);
                }
            }
        }

        return BuildText(assigned);
    }

    private static TextSegmentAssignment FindNearestPhoto(
        IReadOnlyList<Letter> segment,
        IReadOnlyList<PlacedImage> photos,
        double pageWidth,
        double pageHeight)
    {
        var left = segment.Min(letter => letter.StartBaseLine.X);
        var right = segment.Max(letter =>
            letter.StartBaseLine.X + Math.Max(0, letter.Width));
        var y = segment.Average(letter => letter.StartBaseLine.Y);
        var nearest = -1;
        var nearestDistance = double.MaxValue;
        var nearestVerticalDistance = double.MaxValue;
        for (var candidateIndex = 0; candidateIndex < photos.Count; candidateIndex++)
        {
            var bounds = photos[candidateIndex].Bounds;
            var distance = DistanceToRectangle(
                left,
                right,
                y,
                bounds,
                pageWidth,
                pageHeight);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestVerticalDistance = VerticalDistanceRatio(
                y,
                bounds,
                pageHeight);
            nearest = candidateIndex;
        }

        return new TextSegmentAssignment(
            nearest,
            nearestDistance,
            nearestVerticalDistance);
    }

    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<Letter>>> SplitIntoTextLines(
        IReadOnlyList<Letter> letters,
        double pageWidth)
    {
        var averageWidth = letters
            .Where(letter => letter.Width > 0
                             && letter.Value?.Length == 1
                             && !char.IsWhiteSpace(letter.Value[0]))
            .Select(letter => letter.Width)
            .DefaultIfEmpty(5.5)
            .Average();
        if (averageWidth < 0.5)
            averageWidth = 5.5;
        // Ein kleinerer Zeilenschnitt trennt auch zweispaltige Fototabellen,
        // deren linke und rechte Spalte nur wenige Punkte Abstand haben.
        // Segmente derselben Tabellenzeile werden weiter unten anhand ihres
        // naechsten Fotos wieder sicher zusammengefuehrt.
        var splitGap = Math.Max(pageWidth * 0.01, averageWidth * 2.0);
        var result = new List<IReadOnlyList<IReadOnlyList<Letter>>>();

        foreach (var line in letters
                     .GroupBy(letter => Math.Round(letter.StartBaseLine.Y / 2.0) * 2.0))
        {
            var segments = new List<IReadOnlyList<Letter>>();
            var current = new List<Letter>();
            var previousEnd = double.NaN;
            foreach (var letter in line.OrderBy(item => item.StartBaseLine.X))
            {
                var gap = double.IsNaN(previousEnd)
                    ? 0
                    : letter.StartBaseLine.X - previousEnd;
                if (current.Count > 0 && gap > splitGap)
                {
                    segments.Add(current.ToArray());
                    current.Clear();
                }

                current.Add(letter);
                previousEnd = letter.StartBaseLine.X
                              + (letter.Width > 0 ? letter.Width : averageWidth);
            }

            if (current.Count > 0)
                segments.Add(current.ToArray());
            if (segments.Count > 0)
                result.Add(segments);
        }

        return result;
    }

    private static double DistanceToRectangle(
        double textLeft,
        double textRight,
        double y,
        PdfRectangle bounds,
        double pageWidth,
        double pageHeight)
    {
        var dx = textRight < bounds.Left
            ? bounds.Left - textRight
            : textLeft > bounds.Right
                ? textLeft - bounds.Right
                : 0;
        var dy = y < bounds.Bottom
            ? bounds.Bottom - y
            : y > bounds.Top
                ? y - bounds.Top
                : 0;
        var nx = dx / Math.Max(1, pageWidth);
        var ny = dy / Math.Max(1, pageHeight);
        return Math.Sqrt(nx * nx + ny * ny);
    }

    private static double VerticalDistanceRatio(
        double y,
        PdfRectangle bounds,
        double pageHeight)
    {
        var dy = y < bounds.Bottom
            ? bounds.Bottom - y
            : y > bounds.Top
                ? y - bounds.Top
                : 0;
        return dy / Math.Max(1, pageHeight);
    }

    private static string BuildText(IReadOnlyList<Letter> letters)
    {
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

        var result = new System.Text.StringBuilder();
        foreach (var line in letters
                     .GroupBy(letter => Math.Round(letter.StartBaseLine.Y / 2.0) * 2.0)
                     .OrderByDescending(group => group.Key))
        {
            var sorted = line.OrderBy(letter => letter.StartBaseLine.X).ToArray();
            if (sorted.Length == 0)
                continue;

            var previousEnd = sorted[0].StartBaseLine.X;
            foreach (var letter in sorted)
            {
                var gap = letter.StartBaseLine.X - previousEnd;
                if (gap > averageWidth * 0.5)
                {
                    var spaces = Math.Clamp(
                        (int)Math.Round(gap / averageWidth),
                        1,
                        40);
                    result.Append(' ', spaces);
                }

                result.Append(letter.Value);
                previousEnd = letter.StartBaseLine.X
                              + (letter.Width > 0 ? letter.Width : averageWidth);
            }

            result.AppendLine();
        }

        return result.ToString();
    }

    private static bool NearlyEqual(double left, double right)
        => Math.Abs(left - right) <= 0.5;

    private sealed record PlacedImage(IPdfImage Image, PdfRectangle Bounds);

    private sealed record TextSegmentAssignment(
        int PhotoIndex,
        double Distance,
        double VerticalDistance);
}
