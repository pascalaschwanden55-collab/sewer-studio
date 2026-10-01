using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>Wo nach Videos gesucht wird; fuer alle PDFs eines Verteillaufs gleich.</summary>
internal sealed record HoldingVideoSearchContext(
    string VideoSourceFolder,
    bool Recursive,
    IReadOnlyList<string>? VideoFilesCache = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? SidecarVideoLinksByHolding = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? SidecarHoldingsByVideoLink = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? CdIndexVideoLinksByPhoto = null);

/// <summary>Gefundenes Video und die Haltung, unter der abgelegt wird.</summary>
internal sealed record HoldingVideoSearchResult(
    Distributor.VideoFindResult Video,
    string HoldingRaw,
    string Holding,
    bool HoldingLabelAdjusted);

/// <summary>
/// Sucht das Standardvideo einer Haltung und entscheidet, ob das gefundene Video die Haltung
/// korrigiert. Reihenfolge der Suchwege (der erste Treffer gewinnt): Name aus dem Protokoll
/// bzw. Haltung+Datum; dieselbe Suche mit der unkorrigierten Protokoll-Haltung; der Link eines
/// importierten Datensatzes; Seitenwagen M150/MDB; Fotohinweise aus dem CD-Index. Ein
/// mehrdeutiges Ergebnis ersetzt nur ein «nicht gefunden». Danach darf ein eindeutiger
/// Seitenwagen-Eintrag oder der Videoname einer bekannten Haltung die Haltung umbiegen.
/// Schreibt nichts; Ablage und Verknuepfung macht der Aufrufer.
/// </summary>
internal static class HoldingVideoSearch
{
    internal static HoldingVideoSearchResult Find(
        HoldingVideoSearchContext search,
        Project? project,
        string? videoFileFromPdf,
        string holdingRaw,
        string holding,
        string originalHolding,
        string dateStamp,
        string pdfToStorePath)
    {
        var videoFind = FindByNameOrDate(search, videoFileFromPdf, holding, dateStamp);

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched
            && !string.Equals(originalHolding, holding, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = FindByNameOrDate(search, videoFileFromPdf, originalHolding, dateStamp);
            if (fallback.Status == Distributor.VideoMatchStatus.Matched
                || (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && fallback.Status == Distributor.VideoMatchStatus.Ambiguous))
            {
                videoFind = fallback;
            }
        }

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched)
        {
            var fromLink = Distributor.TryFindVideoFromRecordLink(
                project,
                holding,
                search.VideoSourceFolder,
                dateStamp,
                search.Recursive,
                search.VideoFilesCache);
            if (fromLink.Status == Distributor.VideoMatchStatus.Matched)
                videoFind = fromLink;
        }

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched)
        {
            var fromSidecar = Distributor.TryFindVideoFromSidecarLinks(
                search.SidecarVideoLinksByHolding,
                holding,
                search.VideoSourceFolder,
                dateStamp,
                search.Recursive,
                search.VideoFilesCache);
            if (fromSidecar.Status == Distributor.VideoMatchStatus.Matched
                || (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && fromSidecar.Status == Distributor.VideoMatchStatus.Ambiguous))
            {
                videoFind = fromSidecar;
            }
        }

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched)
        {
            var fromCdIndex = Distributor.TryFindVideoFromCdIndexPhotoHints(
                search.CdIndexVideoLinksByPhoto,
                pdfToStorePath,
                holding,
                search.VideoSourceFolder,
                dateStamp,
                search.Recursive,
                search.VideoFilesCache);
            if (fromCdIndex.Status == Distributor.VideoMatchStatus.Matched
                || (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && fromCdIndex.Status == Distributor.VideoMatchStatus.Ambiguous))
            {
                videoFind = fromCdIndex;
            }
        }

        var holdingLabelAdjusted = false;
        if (videoFind.Status == Distributor.VideoMatchStatus.Matched && videoFind.VideoPath is not null)
        {
            var mappedHolding = Distributor.TryResolveHoldingFromMatchedVideo(
                search.SidecarHoldingsByVideoLink,
                search.SidecarVideoLinksByHolding,
                videoFind.VideoPath,
                holding);
            if (!string.IsNullOrWhiteSpace(mappedHolding)
                && !string.Equals(mappedHolding, holding, StringComparison.OrdinalIgnoreCase))
            {
                holdingRaw = mappedHolding;
                holding = ProjectPathResolver.SanitizePathSegment(
                    HoldingIdNormalizer.NormalizeHaltungId(mappedHolding));
                holdingLabelAdjusted = true;
            }
        }

        if (!holdingLabelAdjusted
            && videoFind.Status == Distributor.VideoMatchStatus.Matched
            && videoFind.VideoPath is not null)
        {
            var mappedHolding = Distributor.TryResolveHoldingFromMatchedVideoName(
                project,
                videoFind.VideoPath,
                holding);
            if (!string.IsNullOrWhiteSpace(mappedHolding))
            {
                holdingRaw = mappedHolding;
                holding = ProjectPathResolver.SanitizePathSegment(
                    HoldingIdNormalizer.NormalizeHaltungId(mappedHolding));
                holdingLabelAdjusted = true;
            }
        }

        return new HoldingVideoSearchResult(videoFind, holdingRaw, holding, holdingLabelAdjusted);
    }

    /// <summary>Gegeninspektion: dieselbe Haltung mit angehaengtem «g».</summary>
    internal static Distributor.VideoFindResult FindCounterInspection(
        HoldingVideoSearchContext search, string holding, string dateStamp)
        => Distributor.FindVideoByHaltungDate(
            search.VideoSourceFolder,
            holding + "g",
            dateStamp,
            search.Recursive,
            search.VideoFilesCache);

    private static Distributor.VideoFindResult FindByNameOrDate(
        HoldingVideoSearchContext search, string? videoFileFromPdf, string holding, string dateStamp)
        => string.IsNullOrWhiteSpace(videoFileFromPdf)
            ? Distributor.FindVideoByHaltungDate(
                search.VideoSourceFolder,
                holding,
                dateStamp,
                search.Recursive,
                search.VideoFilesCache)
            : Distributor.FindVideo(
                videoFileFromPdf,
                search.VideoSourceFolder,
                holding,
                dateStamp,
                search.Recursive,
                search.VideoFilesCache);
}
