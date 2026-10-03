using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Fallordner-Scan des Training Centers: Ordner unter der Wurzel durchsuchen, Videos/Protokolle pruefen und paaren,
/// Videoverweise als Rueckfall, Inspektionsdatum ermitteln (aus <see cref="TrainingCenterImportService"/> verschoben,
/// Paket A 03.10.2026, ohne Verhaltensaenderung). Laeuft synchron; die Fassade legt ihn in den Hintergrund.
/// </summary>
internal sealed class TrainingCenterFallScan
{
    internal static readonly string[] VideoExts = [..AuswertungPro.Next.Infrastructure.Media.MediaFileTypes.VideoExtensions, ".ts", ".m4v"];
    internal static readonly string[] ProtocolExts = [".json", ".xml", ".pdf"];

    private readonly Func<string, IEnumerable<string>> _dateienImOrdner;
    private readonly TrainingCenterFallDateien _fallDateien;

    public TrainingCenterFallScan(Func<string, IEnumerable<string>> dateienImOrdner, TrainingCenterFallDateien fallDateien)
    {
        _dateienImOrdner = dateienImOrdner;
        _fallDateien = fallDateien;
    }

    internal List<TrainingCaseInput> Scan(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner,
        ICollection<string>? hinweise,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return new List<TrainingCaseInput>();

        var folders = EnumerateFolders(rootFolder, uebersprungeneOrdner);

        var cases = new List<TrainingCaseInput>();

        foreach (var folder in folders)
        {
            // Ausserhalb des try: der Fang fuer Ordnerfehler darf den Abbruch nicht verschlucken.
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var files = _dateienImOrdner(folder).ToList();
                if (files.Count == 0)
                    continue;

                // PR #85: Videos und Protokolle, die selbst eine Verknuepfung oder nicht pruefbar sind (z. B.
                // Symlinks aelterer Verteillaeufe), werden nicht verwendet, sondern gemeldet.
                var direktVideos = files
                    .Where(_fallDateien.IstVideo)
                    .Where(f => _fallDateien.IstUnverknuepft(f, TrainingCenterFallDateien.Art.Video, hinweise))
                    .ToList();
                var protos = files
                    .Where(f => ProtocolExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Where(f => _fallDateien.IstUnverknuepft(f, TrainingCenterFallDateien.Art.Protokoll, hinweise))
                    .ToList();

                var caseId = SafeRelativeId(rootFolder, folder);
                var (bestVideo, bestProto) = TrainingCenterPaarung.ResolvePair(
                    direktVideos, protos, caseId, out var mehrdeutigeVideos, out var widerspruch);

                // Ein verwendbares Direktvideo hat Vorrang. Wird keines ausgewaehlt (keines da, nur ausgeschlossene
                // wie *_g.mpg, mehrdeutig), gelten als Rueckfall die Videoverweise der Verteilung (PR #85).
                var verweisVideos = new List<string>();
                TrainingCenterPaarung.Widerspruch? verweisWiderspruch = null;
                if (string.IsNullOrWhiteSpace(bestVideo))
                {
                    verweisVideos = _fallDateien.LoeseVideoverweiseAuf(files, hinweise);
                    if (verweisVideos.Count > 0)
                    {
                        var (verweisVideo, verweisProto) = TrainingCenterPaarung.ResolvePair(
                            verweisVideos, protos, caseId, out _, out verweisWiderspruch);
                        if (!string.IsNullOrWhiteSpace(verweisVideo))
                            (bestVideo, bestProto) = (verweisVideo, verweisProto);
                    }
                }

                // Ohne Video UND ohne Protokoll: ueberspringen. Nur ausgeschlossene Videos (Grafik, Uebersicht)
                // zaehlen dabei nicht als Video (Review PR #85); mehrdeutige echte Videos bleiben als Fall sichtbar.
                var nurAusgeschlosseneVideos = direktVideos.All(TrainingCenterPaarung.IstAusgeschlossenesVideo);
                if (nurAusgeschlosseneVideos && verweisVideos.All(TrainingCenterPaarung.IstAusgeschlossenesVideo) && protos.Count == 0)
                    continue;

                // Folgepaket 3: mehrere echte Videos ohne Haltungsschluessel nicht still verwerfen, sondern melden;
                // der Fall bleibt sichtbar (ohne Video), damit das passende Video benannt werden kann.
                if (mehrdeutigeVideos && string.IsNullOrWhiteSpace(bestVideo))
                {
                    var namen = direktVideos.Where(video => !TrainingCenterPaarung.IstAusgeschlossenesVideo(video)).Select(Path.GetFileName);
                    hinweise?.Add($"Fall «{caseId}»: mehrere Videos ohne eindeutigen Haltungsschlüssel ({string.Join(", ", namen)}) "
                                  + "– keines verwendet; bitte das passende Video nach der Haltung benennen.");
                }

                // Paket B: widersprechende Haltungsschluessel nicht still verwerfen, sondern begruenden.
                MeldeWiderspruch(caseId, widerspruch, hinweise);
                MeldeWiderspruch(caseId, verweisWiderspruch, hinweise);

                var inspectionDate = ResolveInspectionDate(folder, bestProto, bestVideo);

                cases.Add(new TrainingCaseInput(
                    CaseId: caseId,
                    FolderPath: folder,
                    VideoPath: bestVideo,
                    ProtocolPath: bestProto,
                    InspectionDate: inspectionDate));
            }
            catch (Exception ex)
            {
                // Deepscan R8: Ein nicht lesbarer Ordner liefert keinen Trainingsfall. Er fehlt nicht mehr
                // still, sondern wird gesammelt und vom Training Center im Protokoll benannt.
                uebersprungeneOrdner?.Add(folder);
                System.Diagnostics.Trace.WriteLine(
                    $"[TrainingCenterImport] Ordner uebersprungen: {folder}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Stable ordering for UI
        return cases.OrderBy(c => c.CaseId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Scannt nur nach Protokollen (PDF/JSON), Video ist nicht erforderlich.
    /// Fuer den reinen Protokoll-Import ohne Videoanalyse.
    /// </summary>
    internal List<TrainingCaseInput> ScanProtocolOnly(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return new List<TrainingCaseInput>();

        var folders = EnumerateFolders(rootFolder);
        var cases = new List<TrainingCaseInput>();

        foreach (var folder in folders)
        {
            try
            {
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).ToList();
                if (files.Count == 0) continue;

                var protos = files.Where(f => ProtocolExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                if (protos.Count == 0) continue;

                var videos = files.Where(f => VideoExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                var caseId = SafeRelativeId(rootFolder, folder);
                var (bestVideo, proto) = TrainingCenterPaarung.ResolveProtocolOnlyPair(videos, protos, caseId);
                var inspectionDate = ResolveInspectionDate(folder, proto, bestVideo);
                if (string.IsNullOrWhiteSpace(proto)) continue; // Nur Non-Protocol-Dateien -> ueberspringen

                cases.Add(new TrainingCaseInput(
                    CaseId: caseId,
                    FolderPath: folder,
                    VideoPath: bestVideo,
                    ProtocolPath: proto,
                    InspectionDate: inspectionDate));
            }
            catch (Exception ex)
            {
                // Frueher still verschluckt -> stiller Verlust von Trainingsfaellen. Jetzt sichtbar
                // (mit Ordnerpfad), damit nachvollziehbar ist, WELCHER Ordner und WARUM uebersprungen wurde.
                System.Diagnostics.Trace.WriteLine(
                    $"[TrainingCenterImport] Ordner uebersprungen: {folder}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        cases = cases.OrderBy(c => c.CaseId, StringComparer.OrdinalIgnoreCase).ToList();
        return cases;
    }

    /// <summary>
    /// Paket B: Dateihinweis zu einem Widerspruch der Haltungsschluessel (Video, Protokoll, beide Schluessel, verworfener
    /// Teil). Ohne Hinweisliste (Batch-Import, Selbsttraining) nur im Trace.
    /// </summary>
    private static void MeldeWiderspruch(string caseId, TrainingCenterPaarung.Widerspruch? widerspruch, ICollection<string>? hinweise)
    {
        if (widerspruch is null)
            return;

        var meldung = $"Fall «{caseId}»: Video «{widerspruch.Video}» (Haltungsschlüssel {widerspruch.VideoSchluessel}) und "
                      + $"Protokoll «{widerspruch.Protokoll}» (Haltungsschlüssel {widerspruch.ProtokollSchluessel}) widersprechen sich – "
                      + (widerspruch.VideoVerworfen ? "Video" : "Protokoll") + " nicht verwendet.";
        hinweise?.Add(meldung);
        System.Diagnostics.Trace.WriteLine($"[TrainingCenterImport] {meldung}");
    }

    private static DateTime? ResolveInspectionDate(string folder, string protocolPath, string videoPath)
    {
        foreach (var candidate in EnumerateDateCandidates(folder, protocolPath, videoPath))
        {
            var parsed = TrainingSampleEligibility.TryParseInspectionDate(candidate);
            if (parsed is not null)
                return parsed.Value;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateDateCandidates(string folder, string protocolPath, string videoPath)
    {
        yield return Path.GetFileName(folder);
        yield return folder;

        if (!string.IsNullOrWhiteSpace(protocolPath))
        {
            yield return Path.GetFileName(protocolPath);
            foreach (var line in ReadTextDateCandidates(protocolPath))
                yield return line;
        }

        if (!string.IsNullOrWhiteSpace(videoPath))
            yield return Path.GetFileName(videoPath);
    }

    private static IEnumerable<string> ReadTextDateCandidates(string path)
    {
        var ext = Path.GetExtension(path);
        if (!string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(path).Take(200))
        {
            if (line.Contains("datum", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("date", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("aufnahme", StringComparison.OrdinalIgnoreCase))
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<string> EnumerateFolders(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner = null)
    {
        // Root + alle erreichbaren Unterordner (gesperrte und verknuepfte werden uebersprungen statt zu
        // werfen und, falls der Aufrufer eine Liste mitgibt, dort eingetragen).
        foreach (var dir in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateDirectoriesSafe(
                     rootFolder,
                     uebersprungeneOrdner))
            yield return dir;
    }

    private static string SafeRelativeId(string root, string folder)
    {
        try
        {
            var rel = Path.GetRelativePath(root, folder);
            if (string.IsNullOrWhiteSpace(rel) || rel == ".")
                return new DirectoryInfo(folder).Name;

            // Normalize slashes
            rel = rel.Replace('\\', '/');
            return rel;
        }
        catch
        {
            return new DirectoryInfo(folder).Name;
        }
    }
}
