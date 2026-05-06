using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Generiert ein eingefrorenes Eval-Set aus Operateur-Codierungen (DB3).
/// Die Frames sind menschlich codiert (Ground Truth), nicht von KI generiert.
///
/// Strategie: 120 diverse Frames aus DB3-Profilen:
/// - 60 Frames: Top-5 haeufigste VSA-Codes (je 12)
/// - 30 Frames: Bekannte Verwechslungspaare
/// - 30 Frames: Negativbeispiele (leere Segmente)
///
/// Jeder Frame kann manuell approved/rejected/corrected werden.
/// Nur approved Frames kommen ins finale Eval-Set.
/// Das Eval-Set wird EINGEFROREN und nie vom Auto-Training beruehrt.
/// </summary>
public static class EvalSetGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Ein Eval-Kandidat zur manuellen Pruefung.</summary>
    public sealed record EvalCandidate(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("frame_path")] string FramePath,
        [property: JsonPropertyName("haltung_key")] string HaltungKey,
        [property: JsonPropertyName("meter")] double Meter,
        [property: JsonPropertyName("zeit_sek")] double ZeitSek,
        [property: JsonPropertyName("code_main")] string CodeMain,
        [property: JsonPropertyName("code_full")] string CodeFull,
        [property: JsonPropertyName("kategorie")] string Kategorie,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("korrektur")] string? Korrektur,
        [property: JsonPropertyName("quelle")] string Quelle);

    /// <summary>
    /// Generiert Eval-Kandidaten aus InspectionProfiles.
    /// Waehlt 120 diverse Frames aus verschiedenen Haltungen und Code-Gruppen.
    /// </summary>
    public static List<EvalCandidate> GenerateCandidates(
        List<InspectionProfile> profiles,
        string framesDir,
        int targetCount = 120)
    {
        var candidates = new List<EvalCandidate>();
        var usedFrames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Alle Events mit Frame-Pfad sammeln
        var allEvents = new List<(InspectionProfile Profile, ProfileEvent Event, string FramePath)>();
        foreach (var p in profiles)
        {
            foreach (var evt in p.Ereignisse)
            {
                // Frame-Pfad konstruieren (wie InspectionFrameExtractor:52 — flach mit HaltungKey im Filename)
                var safeKey = SanitizeName(p.HaltungKey);
                var frameName = $"{safeKey}_{evt.ZeitSek:F1}s_{SanitizeName(evt.CodeFull)}_t+0.png";
                var framePath = Path.Combine(framesDir, frameName);

                if (File.Exists(framePath))
                    allEvents.Add((p, evt, framePath));
            }
        }

        if (allEvents.Count == 0) return candidates;

        var rng = new Random(42); // Reproduzierbar
        allEvents = allEvents.OrderBy(_ => rng.Next()).ToList(); // Mischen

        // ── Runde 1: Top-5 haeufigste Codes (je 12 = 60 Frames) ──
        var codeGroups = allEvents
            .GroupBy(e => e.Event.CodeMain)
            .OrderByDescending(g => g.Count())
            .Take(5);

        foreach (var group in codeGroups)
        {
            int added = 0;
            foreach (var (profile, evt, framePath) in group)
            {
                if (added >= 12) break;
                if (!usedFrames.Add(framePath)) continue;

                candidates.Add(CreateCandidate(profile, evt, framePath, "top5_code"));
                added++;
            }
        }

        // ── Runde 2: Verwechslungspaare (30 Frames) ──
        var conflictCodes = new[] { "BCA", "BCC", "BAI", "BAJ", "BBC", "BBB", "BAB", "BAC", "BCD", "BDB" };
        int conflictTarget = 30;
        int conflictAdded = 0;

        foreach (var code in conflictCodes)
        {
            if (conflictAdded >= conflictTarget) break;
            var matching = allEvents.Where(e =>
                e.Event.CodeMain.Equals(code, StringComparison.OrdinalIgnoreCase)
                && !usedFrames.Contains(e.FramePath));

            foreach (var (profile, evt, framePath) in matching.Take(3))
            {
                if (conflictAdded >= conflictTarget) break;
                if (!usedFrames.Add(framePath)) continue;

                candidates.Add(CreateCandidate(profile, evt, framePath, "verwechslungspaar"));
                conflictAdded++;
            }
        }

        // ── Runde 3: Negativbeispiele (30 Frames aus Leersegmenten) ──
        int negTarget = 30;
        int negAdded = 0;

        foreach (var p in profiles)
        {
            if (negAdded >= negTarget) break;
            foreach (var gap in p.Luecken)
            {
                if (negAdded >= negTarget) break;
                if ((gap.DistanzM ?? 0) < 3.0) continue; // Nur grosse Luecken

                var mitteZeit = (gap.VonZeit + gap.BisZeit) / 2.0;
                var safeKey = SanitizeName(p.HaltungKey);
                var frameName = $"{safeKey}_{mitteZeit:F1}s_kein_schaden.png";
                var framePath = Path.Combine(framesDir, frameName);

                if (File.Exists(framePath) && usedFrames.Add(framePath))
                {
                    candidates.Add(new EvalCandidate(
                        Id: Guid.NewGuid().ToString("N")[..12],
                        FramePath: framePath,
                        HaltungKey: p.HaltungKey,
                        Meter: (gap.VonMeter ?? 0 + (gap.BisMeter ?? 0)) / 2.0,
                        ZeitSek: mitteZeit,
                        CodeMain: "LEER",
                        CodeFull: "LEER",
                        Kategorie: "negativ",
                        Status: "pending",
                        Korrektur: null,
                        Quelle: "operateur_luecke"));
                    negAdded++;
                }
            }
        }

        // ── Runde 4: Auffuellen bis targetCount ──
        foreach (var (profile, evt, framePath) in allEvents)
        {
            if (candidates.Count >= targetCount) break;
            if (!usedFrames.Add(framePath)) continue;

            candidates.Add(CreateCandidate(profile, evt, framePath, "auffuellung"));
        }

        return candidates.Take(targetCount).ToList();
    }

    /// <summary>Speichert Eval-Kandidaten als JSON (fuer Review-UI).</summary>
    public static void SaveCandidates(List<EvalCandidate> candidates, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(candidates, JsonOpts));
    }

    /// <summary>Laedt Eval-Kandidaten (mit Status-Updates nach Review).</summary>
    public static List<EvalCandidate> LoadCandidates(string path)
    {
        if (!File.Exists(path)) return new();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<EvalCandidate>>(json) ?? new();
    }

    /// <summary>Exportiert nur approved Kandidaten als eingefrorenes Eval-Set.</summary>
    public static int ExportFrozenEvalSet(List<EvalCandidate> candidates, string evalSetDir)
    {
        Directory.CreateDirectory(evalSetDir);
        var approved = candidates.Where(c => c.Status == "approved").ToList();

        // Frames kopieren
        var imagesDir = Path.Combine(evalSetDir, "images");
        var labelsDir = Path.Combine(evalSetDir, "labels");
        Directory.CreateDirectory(imagesDir);
        Directory.CreateDirectory(labelsDir);

        int exported = 0;
        foreach (var c in approved)
        {
            if (!File.Exists(c.FramePath)) continue;

            var code = c.Korrektur ?? c.CodeFull; // Korrigierter Code hat Vorrang
            var dstImg = Path.Combine(imagesDir, Path.GetFileName(c.FramePath));
            var dstLbl = Path.Combine(labelsDir,
                Path.ChangeExtension(Path.GetFileName(c.FramePath), ".txt"));

            if (!File.Exists(dstImg))
                File.Copy(c.FramePath, dstImg);

            // YOLO-Label (ganzes Bild als Box — wird spaeter durch echte BBoxen ersetzt)
            var classId = YoloTrainingDataGenerator.GetYoloClassId(code);
            if (classId >= 0)
                File.WriteAllText(dstLbl, $"{classId} 0.15 0.15 0.85 0.15 0.85 0.85 0.15 0.85\n");
            else
                File.WriteAllText(dstLbl, ""); // Negativ

            exported++;
        }

        // Manifest speichern (eingefroren, nie aendern)
        var manifest = new
        {
            created_utc = DateTime.UtcNow.ToString("o"),
            total_candidates = candidates.Count,
            approved = approved.Count,
            exported = exported,
            frozen = true,
            warning = "DIESES EVAL-SET DARF NICHT VOM AUTO-TRAINING BERUEHRT WERDEN"
        };
        File.WriteAllText(
            Path.Combine(evalSetDir, "_manifest.json"),
            JsonSerializer.Serialize(manifest, JsonOpts));

        return exported;
    }

    private static EvalCandidate CreateCandidate(
        InspectionProfile profile, ProfileEvent evt, string framePath, string kategorie)
    {
        return new EvalCandidate(
            Id: Guid.NewGuid().ToString("N")[..12],
            FramePath: framePath,
            HaltungKey: profile.HaltungKey,
            Meter: evt.Meter ?? 0,
            ZeitSek: evt.ZeitSek,
            CodeMain: evt.CodeMain,
            CodeFull: evt.CodeFull,
            Kategorie: kategorie,
            Status: "pending", // pending → approved / rejected / corrected
            Korrektur: null,
            Quelle: "operateur_db3");
    }

    private static string SanitizeName(string name)
        => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
