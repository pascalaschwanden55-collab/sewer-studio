using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Paarung von Video und Protokoll eines Training-Center-Falls (aus <see cref="TrainingCenterImportService"/>
/// verschoben, Paket A 03.10.2026, ohne Verhaltensaenderung): bestes Video (Grafik-/Uebersichtsvideos
/// ausgeschlossen), bestes Protokoll, Haltungsschluessel-Abgleich und Widerspruchsregel.
/// </summary>
internal static class TrainingCenterPaarung
{
    private static readonly Regex HaltungIdInFilename = new(
        @"(?<id>\d[\d\.]*[-/]\d[\d\.]*)", RegexOptions.Compiled);

    /// <summary>Grafikvideo oder Uebersicht (Muster auf dem vollen Dateinamen mit Endung)?</summary>
    internal static bool IstAusgeschlossenesVideo(string pfad)
    {
        var name = Path.GetFileName(pfad).ToLowerInvariant();
        return VideoExcludePatterns.Any(name.Contains);
    }

    /// <summary>
    /// Waehlt das beste Video aus mehreren Kandidaten.
    /// Prio: 1. CaseId im Namen, 2. Groesstes (laengstes) Video, 3. Grafik-Videos ausschliessen.
    /// </summary>
    private static string PickBestVideo(List<string> videos, string caseId)
    {
        var nameNoExt = (string p) => Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
        var caseIdLower = caseId.ToLowerInvariant().Replace("/", "").Replace("\\", "");

        // Grafik-Videos und Uebersichten ausschliessen (Matching auf voller Dateiname MIT Extension).
        // Review PR #85: auch ein EINZELNES Video; vorher kam der Einzelfall-Ruecksprung vor dem Filter.
        var filtered = videos
            .Where(v => !IstAusgeschlossenesVideo(v))
            .ToList();
        // Kein Fallback auf ausgeschlossene Videos — leere Liste wird vom Aufrufer behandelt
        if (filtered.Count == 0) return "";
        if (filtered.Count == 1) return filtered[0];

        // 1. Prio: Video dessen Name die CaseId enthaelt
        var caseMatch = filtered.FirstOrDefault(v => nameNoExt(v).Contains(caseIdLower));
        if (caseMatch is not null) return caseMatch;

        // 2. Prio: Groesstes Video (korreliert mit Laenge, da Bitrate aehnlich)
        return filtered.Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.Length)
            .First().FullName;
    }

    // Dateinamen-Muster fuer Protokoll-PDFs (bevorzugt)
    private static readonly string[] ProtocolKeywords =
        ["protokoll", "haltung", "inspektion", "zustandsbericht", "bericht"];

    // Dateinamen-Muster die KEINE Inspektionsprotokolle sind
    private static readonly string[] NonProtocolKeywords =
        ["plan", "situationsplan", "_dp", "lageplan", "uebersicht", "übersicht"];

    // Video-Dateinamen die ausgeschlossen werden (Grafik-Videos, Uebersichten)
    // Hinweis: Matching auf Dateiname MIT Extension (ToLowerInvariant)
    private static readonly string[] VideoExcludePatterns = ["_g.mp", "_g.avi", "_g.ts", "_g.mkv", "_g.m4v", "uebersicht", "übersicht"];

    /// <summary>
    /// Waehlt das beste Protokoll. Gibt null zurueck wenn nur Non-Protocol-Dateien vorhanden.
    /// </summary>
    private static string? PickBestProtocol(List<string> protos)
    {
        // JSON hat hoechste Prio (strukturiert)
        var json = protos.FirstOrDefault(p => Path.GetExtension(p).Equals(".json", StringComparison.OrdinalIgnoreCase));
        if (json is not null) return json;

        // PDFs: Protokoll-Keywords bevorzugen, Non-Protocol ausschliessen
        var pdfs = protos.Where(p => Path.GetExtension(p).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (pdfs.Count > 0)
        {
            var name = (string p) => Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

            // 1. Prio: Dateiname enthaelt Protokoll-Keyword
            var protocolPdf = pdfs.FirstOrDefault(p => ProtocolKeywords.Any(k => name(p).Contains(k)));
            if (protocolPdf is not null) return protocolPdf;

            // 2. Prio: Dateiname ist KEIN bekanntes Non-Protocol
            var nonExcluded = pdfs.Where(p => !NonProtocolKeywords.Any(k => name(p).Contains(k))).ToList();
            if (nonExcluded.Count > 0)
            {
                // Groesstes PDF unter den verbleibenden (wahrscheinlich das Protokoll)
                return nonExcluded.OrderByDescending(p => new FileInfo(p).Length).First();
            }

            // Nur Non-Protocol-PDFs vorhanden → kein echtes Protokoll
            return null;
        }

        // XML als letzter Fallback
        return protos.FirstOrDefault(p => Path.GetExtension(p).Equals(".xml", StringComparison.OrdinalIgnoreCase));
    }

    internal static (string VideoPath, string ProtocolPath) ResolvePair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId)
        => ResolvePair(videos, protos, caseId, out _);

    /// <summary>
    /// Wie oben; <paramref name="mehrdeutigeVideos"/> sagt, ob mehrere echte (nicht ausgeschlossene) Videos ohne
    /// eindeutigen Haltungsschluessel vorlagen und deshalb keines gewaehlt wurde (Folgepaket 3).
    /// </summary>
    internal static (string VideoPath, string ProtocolPath) ResolvePair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId,
        out bool mehrdeutigeVideos)
        => ResolvePairCore(videos, protos, caseId, preserveProtocolOnConflict: false, out mehrdeutigeVideos, out _);

    /// <summary>
    /// Widerspruch der Haltungsschluessel von Video und Protokoll; <see cref="VideoVerworfen"/> sagt, welcher
    /// Teil deshalb nicht verwendet wurde (Paket B: der Grund wird als Dateihinweis gemeldet).
    /// </summary>
    internal sealed record Widerspruch(
        string Video,
        string VideoSchluessel,
        string Protokoll,
        string ProtokollSchluessel,
        bool VideoVerworfen);

    /// <summary>Wie oben; <paramref name="widerspruch"/> beschreibt einen verworfenen Widerspruch (Paket B).</summary>
    internal static (string VideoPath, string ProtocolPath) ResolvePair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId,
        out bool mehrdeutigeVideos,
        out Widerspruch? widerspruch)
        => ResolvePairCore(videos, protos, caseId, preserveProtocolOnConflict: false, out mehrdeutigeVideos, out widerspruch);

    internal static (string VideoPath, string ProtocolPath) ResolveProtocolOnlyPair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId)
    {
        return ResolvePairCore(videos, protos, caseId, preserveProtocolOnConflict: true, out _, out _);
    }

    private static (string VideoPath, string ProtocolPath) ResolvePairCore(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId,
        bool preserveProtocolOnConflict,
        out bool mehrdeutigeVideos,
        out Widerspruch? widerspruch)
    {
        mehrdeutigeVideos = false;
        widerspruch = null;
        var videoList = videos.ToList();
        var protoList = protos.ToList();

        var bestVideo = videoList.Count > 0 ? PickBestVideo(videoList, caseId) : "";
        var bestProto = protoList.Count > 0 ? PickBestProtocol(protoList) ?? "" : "";

        if (videoList.Count <= 1 && protoList.Count <= 1)
        {
            return preserveProtocolOnConflict
                ? DropContradiction(bestVideo, bestProto, caseId, preserveProtocolOnConflict: true, out widerspruch)
                : (bestVideo, bestProto);
        }

        var caseKey = EvalContaminationGuard.NormalizeHaltungKey(caseId);
        var matchingVideo = PickVideoByHaltungKey(videoList, caseKey, caseId);
        if (!string.IsNullOrWhiteSpace(matchingVideo))
        {
            bestVideo = matchingVideo;
        }
        else if (videoList.Count(video => !IstAusgeschlossenesVideo(video)) > 1)
        {
            // Mehrere echte Videos ohne eindeutigen Haltungs-Treffer sind unsicher.
            // Lieber kein Video verwenden als das groesste falsche Video koppeln. Ausgeschlossene Videos
            // (Grafik, Uebersicht) zaehlen nicht mit: ein echtes Video daneben bleibt eindeutig (Folgepaket 3).
            bestVideo = "";
            mehrdeutigeVideos = true;
        }

        var matchingProto = PickProtocolByHaltungKey(protoList, caseKey);
        if (!string.IsNullOrWhiteSpace(matchingProto))
            bestProto = matchingProto;

        return DropContradiction(bestVideo, bestProto, caseId, preserveProtocolOnConflict, out widerspruch);
    }

    private static string PickVideoByHaltungKey(List<string> videos, string? caseKey, string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseKey))
            return "";

        var matches = videos
            .Where(v => string.Equals(NormalizeFileHaltungKey(v), caseKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 0 ? "" : PickBestVideo(matches, caseId);
    }

    private static string PickProtocolByHaltungKey(List<string> protos, string? caseKey)
    {
        if (string.IsNullOrWhiteSpace(caseKey))
            return "";

        var matches = protos
            .Where(p => string.Equals(NormalizeFileHaltungKey(p), caseKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 0 ? "" : PickBestProtocol(matches) ?? "";
    }

    private static (string VideoPath, string ProtocolPath) DropContradiction(
        string videoPath,
        string protocolPath,
        string caseId,
        bool preserveProtocolOnConflict,
        out Widerspruch? widerspruch)
    {
        widerspruch = null;
        if (string.IsNullOrWhiteSpace(videoPath) || string.IsNullOrWhiteSpace(protocolPath))
            return (videoPath, protocolPath);

        var videoKey = NormalizeFileHaltungKey(videoPath);
        var protocolKey = NormalizeFileHaltungKey(protocolPath);
        if (videoKey is null || protocolKey is null)
            return (videoPath, protocolPath);

        if (string.Equals(videoKey, protocolKey, StringComparison.OrdinalIgnoreCase))
            return (videoPath, protocolPath);

        // Paket B: Die Regel bleibt; der verworfene Teil und beide Schluessel gehen an den Aufrufer zur Meldung.
        var ergebnis = VerwirfBeiWiderspruch(videoPath, protocolPath, videoKey, protocolKey, caseId, preserveProtocolOnConflict);
        // Der Normalisierer liefert ohne Schachtpaar auch blosse Namen zurueck. Diese sind kein Haltungsbeleg.
        if (HaltungIdInFilename.IsMatch(Path.GetFileNameWithoutExtension(videoPath))
            && HaltungIdInFilename.IsMatch(Path.GetFileNameWithoutExtension(protocolPath)))
            widerspruch = new Widerspruch(videoPath, videoKey, protocolPath, protocolKey, VideoVerworfen: ergebnis.VideoPath.Length == 0);
        return ergebnis;
    }

    private static (string VideoPath, string ProtocolPath) VerwirfBeiWiderspruch(
        string videoPath,
        string protocolPath,
        string videoKey,
        string protocolKey,
        string caseId,
        bool preserveProtocolOnConflict)
    {
        if (preserveProtocolOnConflict)
            return ("", protocolPath);

        var caseKey = EvalContaminationGuard.NormalizeHaltungKey(caseId);
        var videoMatchesCase = caseKey is not null
            && string.Equals(videoKey, caseKey, StringComparison.OrdinalIgnoreCase);
        var protocolMatchesCase = caseKey is not null
            && string.Equals(protocolKey, caseKey, StringComparison.OrdinalIgnoreCase);

        if (videoMatchesCase && !protocolMatchesCase)
            return (videoPath, "");
        if (protocolMatchesCase && !videoMatchesCase)
            return ("", protocolPath);

        return (videoPath, "");
    }

    private static string? NormalizeFileHaltungKey(string path)
    {
        return EvalContaminationGuard.NormalizeHaltungKey(Path.GetFileNameWithoutExtension(path));
    }
}
