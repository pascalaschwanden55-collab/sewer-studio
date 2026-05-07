using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

// Audit 2026-04-23 ARCH-H5: MultiModelAnalysisService war 2185 LOC. Pure
// Helper, Plausibility-/Consensus-Filter und der ProtocolMerger sind hier
// extrahiert. Hauptdatei behaelt nur den GPU-State-Automaten (AnalyzeAsync,
// AnalyzeWithNvdecAsync, UpdateActive/FinalizeOrDiscard, GetVideoDuration).
public sealed partial class MultiModelAnalysisService
{
    // ── Conversion helper ──────────────────────────────────────────────

    /// <summary>
    /// Convert a MultiModelFrameResult to EnhancedFrameAnalysis
    /// (for compatibility with the existing pipeline).
    /// </summary>
    public static EnhancedFrameAnalysis ToEnhancedAnalysis(
        MultiModelFrameResult result,
        int pipeDiameterMm,
        Domain.Models.PipeCalibration? calibration = null)
    {
        if (!result.IsRelevant)
            return EnhancedFrameAnalysis.Empty();

        // K3: optionale Kalibrierung — falls uebergeben, nutzt QuantifyWithRatio statt 0.70.
        var quantified = new List<MaskQuantificationService.QuantifiedMask>();
        foreach (var mask in result.SamMasks)
        {
            quantified.Add(calibration != null
                ? MaskQuantificationService.Quantify(
                    mask, result.ImageWidth, result.ImageHeight, pipeDiameterMm, calibration)
                : MaskQuantificationService.Quantify(
                    mask, result.ImageWidth, result.ImageHeight, pipeDiameterMm));
        }

        var findings = new List<EnhancedFinding>(quantified.Count);
        for (var i = 0; i < quantified.Count; i++)
        {
            var q = quantified[i];
            if (string.IsNullOrWhiteSpace(q.Label))
                continue;

            var bbox = i < result.SamMasks.Count ? GetNormalizedBbox(result.SamMasks[i], result.ImageWidth, result.ImageHeight) : default;
            findings.Add(new EnhancedFinding(
                Label: q.Label,
                VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                Severity: EstimateSeverity(q),
                PositionClock: NormalizeClockPosition(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                HeightMm: q.HeightMm,
                WidthMm: q.WidthMm,
                IntrusionPercent: q.IntrusionPercent,
                CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1Norm: bbox.X1,
                BboxY1Norm: bbox.Y1,
                BboxX2Norm: bbox.X2,
                BboxY2Norm: bbox.Y2,
                Notes: null
            ));
        }

        return new EnhancedFrameAnalysis(
            Meter: result.Meter,
            PipeMaterial: "unbekannt",
            PipeDiameterMm: pipeDiameterMm,
            Findings: findings,
            ImageQuality: "gut",
            IsEmptyFrame: false,
            Error: null);
    }

    // ── Private helpers ────────────────────────────────────────────────

    private static int EstimateSeverity(MaskQuantificationService.QuantifiedMask q)
    {
        // Heuristic based on physical dimensions
        if (q.CrossSectionReductionPercent is > 50) return 5;
        if (q.CrossSectionReductionPercent is > 25) return 4;
        if (q.ExtentPercent is > 50) return 4;
        if (q.HeightMm is > 50) return 3;
        if (q.ExtentPercent is > 25) return 3;
        if (q.HeightMm is > 10) return 2;
        return 1;
    }

    private static (double? X1, double? Y1, double? X2, double? Y2) GetNormalizedBbox(
        SamMaskResult mask,
        int imageWidth,
        int imageHeight)
    {
        if (mask.Bbox == null || mask.Bbox.Count < 4 || imageWidth <= 0 || imageHeight <= 0)
            return default;

        return (
            Clamp01(mask.Bbox[0] / imageWidth),
            Clamp01(mask.Bbox[1] / imageHeight),
            Clamp01(mask.Bbox[2] / imageWidth),
            Clamp01(mask.Bbox[3] / imageHeight));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    private static bool ShouldSuppressByImageQuality(string? imageQuality, double dinoConf)
    {
        if (!string.Equals(imageQuality, "schlecht", StringComparison.OrdinalIgnoreCase))
            return false;

        // Niedrige DINO-Konfidenz + schlechte Bildqualitaet => likely false positive.
        // Bei starken DINO-Hinweisen behalten wir Findings fuer bessere Recall.
        return dinoConf < 0.35;
    }

    private static double EstimateQwenVisionConfidence(string? imageQuality, bool hasFindings)
    {
        var baseConf = imageQuality?.ToLowerInvariant() switch
        {
            "gut" => 0.85,
            "mittel" => 0.65,
            "schlecht" => 0.35,
            _ => 0.55
        };

        if (hasFindings)
            baseConf += 0.05;

        return Math.Clamp(baseConf, 0.0, 1.0);
    }

    /// <summary>Geschaetzte Haltungslaenge in Metern (wird durch OSD-Korrektur von Qwen ueberschrieben).</summary>
    public double EstimatedReachLengthM { get; set; } = 50.0; // Typisch 15-80m, Fallback 50m

    private double EstimateMeter(double t, double duration, ref double lastMeter)
    {
        // Lineare Schaetzung basierend auf geschaetzter Haltungslaenge (wird durch Qwen OSD korrigiert)
        var estimated = t / Math.Max(duration, 1.0) * EstimatedReachLengthM;
        lastMeter = Math.Max(lastMeter, estimated);
        return Math.Round(lastMeter, 2);
    }

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            path = new Uri(path).LocalPath;
        return Path.GetFullPath(path);
    }

    private void UpdateActive(
        Dictionary<string, ActiveFindingState> active,
        List<EnhancedFinding> current,
        double meter,
        List<RawVideoDetection> completed,
        AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? evidence = null)
    {
        var currentMap = new Dictionary<string, EnhancedFinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in current)
        {
            var key = BuildFindingKey(f);
            if (!currentMap.ContainsKey(key))
                currentMap[key] = f;
        }

        foreach (var key in active.Keys.ToList())
        {
            if (currentMap.TryGetValue(key, out var finding))
            {
                // BBox Y-Zentrum fuer Bestaetigungs-Tracking berechnen.
                // Klammern um die Null-Coalescing-Ausdruecke sind ZWINGEND noetig, da `??`
                // schwaecher bindet als `+` -> ohne Klammern wuerde Y2 ignoriert sobald Y1 gesetzt ist.
                double yCenter = ((finding.BboxY1Norm ?? 0) + (finding.BboxY2Norm ?? 1)) / 2.0;
                active[key].Update(meter, finding.Severity, finding.VsaCodeHint, finding.PositionClock,
                    finding.ExtentPercent, finding.HeightMm, finding.WidthMm,
                    finding.IntrusionPercent, finding.CrossSectionReductionPercent, finding.DiameterReductionMm,
                    evidence, yCenter);
            }
            else
            {
                active[key].MissedFrames++;
                if (active[key].MissedFrames >= DedupWindowFrames)
                {
                    FinalizeOrDiscard(active, key, completed);
                }
            }
        }

        foreach (var pair in currentMap)
        {
            if (!active.ContainsKey(pair.Key))
            {
                var f = pair.Value;
                double yCenter = (f.BboxY1Norm ?? 0 + (f.BboxY2Norm ?? 1)) / 2.0;
                active[pair.Key] = new ActiveFindingState(
                    f.Label.Trim(), meter, f.Severity, f.VsaCodeHint, f.PositionClock,
                    f.ExtentPercent, f.HeightMm, f.WidthMm,
                    f.IntrusionPercent, f.CrossSectionReductionPercent, f.DiameterReductionMm,
                    evidence, yCenter);
            }
        }
    }

    /// <summary>
    /// Finalisiert oder verwirft einen Befund basierend auf Bestaetigung.
    /// Unbestaetigte Ferndetektionen werden still verworfen (Selbstkorrektur).
    /// </summary>
    private void FinalizeOrDiscard(
        Dictionary<string, ActiveFindingState> active,
        string key,
        List<RawVideoDetection> completed)
    {
        var state = active[key];
        if (state.ShouldFinalize)
        {
            completed.Add(state.ToDetection());
        }
        else
        {
            _logger.LogDebug(
                "Selbstkorrektur: '{Name}' verworfen — {Frames} Frames, MaxY={Y:F2}, bestaetigt={Confirmed}",
                state.Name, state.FrameCount, state.MaxYCenter, state.IsConfirmed);
        }
        active.Remove(key);
    }

    private static void AdvanceAll(
        Dictionary<string, ActiveFindingState> active,
        List<RawVideoDetection> completed,
        int dedupWindow)
    {
        foreach (var key in active.Keys.ToList())
        {
            active[key].MissedFrames++;
            if (active[key].MissedFrames >= dedupWindow)
            {
                // Nur bestaetigte Befunde finalisieren
                if (active[key].ShouldFinalize)
                    completed.Add(active[key].ToDetection());
                active.Remove(key);
            }
        }
    }

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        var probePath = DeriveFfprobePath(_ffmpegPath);
        var psi = new ProcessStartInfo
        {
            FileName = probePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("format=duration");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(videoPath);

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return 0;
            var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                return dur;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MultiModelAnalysis] ffprobe fehlgeschlagen: {ex.Message}");
        }
        return 0;
    }

    private static string DeriveFfprobePath(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) ||
            string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffprobe";
        var dir = Path.GetDirectoryName(ffmpegPath);
        var ext = Path.GetExtension(ffmpegPath);
        return string.IsNullOrWhiteSpace(dir) ? "ffprobe" + ext : Path.Combine(dir, "ffprobe" + ext);
    }

    /// <summary>
    /// Baut einen stabilen Dedup-Key fuer ein Finding.
    /// Normalisiert Labels gegen DINO-Phrasen-Drift (crack/fracture/break → gleicher Key)
    /// und Clock-Positionen (3:00/3/rechts → normalisierte Stunde).
    /// </summary>
    private static string BuildFindingKey(EnhancedFinding f)
    {
        var label = VsaCodeResolver.NormalizeFindingCode(f.VsaCodeHint)
            ?? VsaCodeResolver.InferCodeFromLabel(f.Label)
            ?? NormalizeFindingLabel(f.Label.Trim());
        var clock = NormalizeClockPosition(f.PositionClock);
        // Keine Dimensionen im Key — wachsende Schaeden (z.B. 5x3 → 8x5mm)
        // wuerden sonst neue Keys erzeugen und die Dedup brechen.
        // Maximalwerte werden stattdessen in UpdateActive() aktualisiert.
        if (string.IsNullOrEmpty(clock))
            return label;
        return $"{label}|{clock}";
    }

    /// <summary>
    /// Normalisiert DINO-Labels auf kanonische Gruppen.
    /// "crack", "fracture", "break" → "crack"
    /// "root intrusion", "roots" → "roots"
    /// Reduziert Label-Drift zwischen Frames.
    /// </summary>
    private static string NormalizeFindingLabel(string label)
    {
        var lower = label.ToLowerInvariant();

        // Risse/Brueche
        if (lower.Contains("crack") || lower.Contains("fracture") || lower.Contains("riss"))
            return "crack";
        if (lower.Contains("break") || lower.Contains("bruch") || lower.Contains("collapse") || lower.Contains("einsturz"))
            return "break";

        // Deformation
        if (lower.Contains("deform") || lower.Contains("verform") || lower.Contains("dent") || lower.Contains("oval"))
            return "deformation";

        // Wurzeln
        if (lower.Contains("root") || lower.Contains("wurzel"))
            return "roots";

        // Korrosion / Oberflaechenschaden
        if (lower.Contains("corros") || lower.Contains("erosion") || lower.Contains("surface damage") || lower.Contains("abplatz"))
            return "corrosion";

        // Ablagerung
        if (lower.Contains("deposit") || lower.Contains("sediment") || lower.Contains("buildup")
            || lower.Contains("ablagerung") || lower.Contains("inkrust"))
            return "deposit";

        // Infiltration
        if (lower.Contains("infiltrat") || lower.Contains("ingress") || lower.Contains("leak")
            || lower.Contains("undicht") || lower.Contains("fremdwasser"))
            return "infiltration";

        // Versatz
        if (lower.Contains("displace") || lower.Contains("offset") || lower.Contains("versatz") || lower.Contains("joint"))
            return "displacement";

        // Hindernis
        if (lower.Contains("obstacle") || lower.Contains("blockage") || lower.Contains("obstruct") || lower.Contains("hindernis"))
            return "obstacle";

        // Anschluss
        if (lower.Contains("connection") || lower.Contains("anschluss") || lower.Contains("intrud") || lower.Contains("protrud"))
            return "connection";

        return lower;
    }

    /// <summary>
    /// Normalisiert Clock-Positionen auf ganzzahlige Stunden.
    /// "3:00" → "3", "12" → "12", "Scheitel" → "12", "Sohle" → "6", "rechts" → "3", "links" → "9".
    /// </summary>
    private static string? NormalizeClockPosition(string? clock)
    {
        var normalized = VsaCodeResolver.NormalizeClock(clock);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized;
    }

    // ── Materialplausibilitaet ─────────────────────────────────────────────

    /// <summary>
    /// Filtert Detektionen die fuer das verbaute Rohrmaterial unplausibel sind.
    /// Kunststoffrohre (PE, PVC, PP, GFK) sind dicht — Infiltration (BBF) ist nur
    /// bei gleichzeitigem Strukturschaden (BA) oder defekter Verbindung (BAJ/BAH)
    /// moeglich. Ohne Begleitschaden wird BBF bei Kunststoff verworfen.
    /// </summary>
    private void ApplyMaterialPlausibilityFilter(List<RawVideoDetection> detections, string? materialOverride = null)
    {
        var material = materialOverride ?? _config.PipeMaterial;
        if (string.IsNullOrWhiteSpace(material)) return;

        bool isKunststoff = IsKunststoffMaterial(material);
        if (!isKunststoff) return;

        // Pruefen ob ein Strukturschaden in der Naehe ist (±2m)
        bool HasNearbyStructuralDamage(double meter)
        {
            return detections.Any(d =>
            {
                var code = d.VsaCodeHint;
                if (string.IsNullOrEmpty(code) || code.Length < 2) return false;
                var prefix = code[..2].ToUpperInvariant();
                // BA = Strukturell (Riss, Bruch, Versatz), oder BAJ/BAH (defekte Verbindung)
                return prefix == "BA" && Math.Abs(d.MeterStart - meter) < 2.0;
            });
        }

        var before = detections.Count;
        detections.RemoveAll(d =>
        {
            var code = d.VsaCodeHint?.ToUpperInvariant() ?? "";
            // BBF = Infiltration: bei Kunststoff nur mit Begleitschaden
            if (code.StartsWith("BBF") && !HasNearbyStructuralDamage(d.MeterStart))
            {
                _logger.LogInformation(
                    "Materialplausibilitaet: {Code} @ {Meter:F1}m verworfen — Kunststoffrohr ({Material}) ohne Begleitschaden",
                    code, d.MeterStart, material);
                return true;
            }
            // BBD = Eindringender Boden: bei intaktem Kunststoff ebenfalls nur mit Schaden
            if (code.StartsWith("BBD") && !HasNearbyStructuralDamage(d.MeterStart))
            {
                _logger.LogInformation(
                    "Materialplausibilitaet: {Code} @ {Meter:F1}m verworfen — Kunststoffrohr ({Material}) ohne Begleitschaden",
                    code, d.MeterStart, material);
                return true;
            }
            return false;
        });

        if (before != detections.Count)
        {
            _logger.LogInformation(
                "Materialplausibilitaet ({Material}): {Before} → {After} Detektionen",
                material, before, detections.Count);
        }
    }

    /// <summary>
    /// Mappt ein Qwen-Material auf den spezifischen AED-Untercode.
    /// VSA: AEDXO=PE, AEDXP=PP, AEDXQ=PVC, AEDXG=Beton, AEDXU=Steinzeug, AED=generisch.
    /// </summary>
    private static string MapMaterialToAedCode(string material)
    {
        var m = material.ToUpperInvariant();
        if (m.Contains("PE") || m.Contains("POLYETHYL")) return "AEDXO";
        if (m.Contains("PP") || m.Contains("POLYPROP")) return "AEDXP";
        if (m.Contains("PVC") || m.Contains("POLYVINYL")) return "AEDXQ";
        if (m.Contains("BETON") || m.Contains("CONCRETE")) return "AEDXG";
        if (m.Contains("STEINZEUG") || m.Contains("VITRIF")) return "AEDXU";
        if (m.Contains("GFK") || m.Contains("FIBERGLASS")) return "AEDXH";
        if (m.Contains("STAHL") || m.Contains("STEEL")) return "AEDXI";
        if (m.Contains("GUSS") || m.Contains("CAST")) return "AEDXJ";
        if (m.Contains("FASER") || m.Contains("ASBESTOS")) return "AEDXK";
        return "AED"; // Generisch
    }

    /// <summary>
    /// Prueft ob das Material ein Kunststoff ist (dichte Rohrwand).
    /// Erkennt: Polyethylen, PE, PVC, PP, GFK, Kunststoff, Plastik etc.
    /// </summary>
    private static bool IsKunststoffMaterial(string material)
    {
        var m = material.ToUpperInvariant();
        return m.Contains("PE") || m.Contains("PVC") || m.Contains("PP")
            || m.Contains("GFK") || m.Contains("KUNSTSTOFF") || m.Contains("PLASTIK")
            || m.Contains("POLYETHYL") || m.Contains("POLYPROP") || m.Contains("POLYVINYL")
            || m.Contains("HDPE") || m.Contains("FASERZ"); // Faserzement = auch dicht
    }

    // ── ActiveFindingState (mirrors VideoFullAnalysisService.ActiveFinding) ──

    /// <summary>
    /// Filtert Detektionen die nicht von mindestens 2 Modellen bestaetigt werden,
    /// entfernt Red-QualityGate-Ergebnisse und verwirft zu kleine/weit entfernte Objekte.
    /// </summary>
    private void ApplyConsensusAndQualityFilter(List<RawVideoDetection> detections)
    {
        const double YoloMin = 0.20;  // YOLO bestaetigt
        const double DinoMin = 0.25;  // DINO bestaetigt
        const double QwenMin = 0.55;  // Qwen bestaetigt (vorher 0.40 — zu nah an Zufall)

        // Minimale BBox-Flaeche (normiert): Objekte unter ~3% der Bildflaeche sind zu weit weg
        // (max ~20cm Entfernung bei typischen Kanalrohren DN100-DN600)
        const double MinBboxArea = 0.03;

        var qg = new AuswertungPro.Next.Application.Ai.QualityGate.QualityGateService();
        var before = detections.Count;

        detections.RemoveAll(d =>
        {
            // 0. Zu kleine/weit entfernte Objekte verwerfen
            if (d.BboxX1 is not null && d.BboxY1 is not null &&
                d.BboxX2 is not null && d.BboxY2 is not null)
            {
                var bboxW = Math.Abs(d.BboxX2.Value - d.BboxX1.Value);
                var bboxH = Math.Abs(d.BboxY2.Value - d.BboxY1.Value);
                var area = bboxW * bboxH;
                if (area < MinBboxArea)
                    return true;
            }

            if (d.Evidence is not { } ev) return false; // Kein Evidence → behalten (Legacy)

            // 1. QualityGate Red → raus
            var result = qg.Evaluate(ev);
            if (result.IsRed)
                return true;

            // 2. Multi-Model-Konsens: mindestens 2 Modelle muessen bestaetigen
            int confirmations = 0;
            if (ev.YoloConf is >= YoloMin) confirmations++;
            if (ev.DinoConf is >= DinoMin) confirmations++;
            if (ev.QwenVisionConf is >= QwenMin) confirmations++;

            return confirmations < 2;
        });

        if (before != detections.Count)
        {
            _logger.LogInformation(
                "Konsens+QualityGate-Filter: {Before} → {After} Detektionen ({Removed} entfernt)",
                before, detections.Count, before - detections.Count);
        }
    }

    private sealed class ActiveFindingState
    {
        public string Name { get; }
        public double MeterStart { get; private set; }
        public double MeterEnd { get; private set; }
        public int MaxSeverity { get; private set; }
        public string? VsaCodeHint { get; private set; }
        public string? PositionClock { get; private set; }
        public int? ExtentPercent { get; private set; }
        public int? HeightMm { get; private set; }
        public int? WidthMm { get; private set; }
        public int? IntrusionPercent { get; private set; }
        public int? CrossSectionReductionPercent { get; private set; }
        public int? DiameterReductionMm { get; private set; }
        public AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? Evidence { get; private set; }
        public int FrameCount { get; private set; } = 1;
        public int MissedFrames { get; set; }

        // ── Bestaetigungs-Tracking ──────────────────────────────────
        // Ein Befund gilt erst als bestaetigt wenn er mindestens einmal
        // auf Kamerahoehe (Y >= 0.30) gesehen wurde. Ferndetektionen
        // (oberes Bilddrittel) allein reichen nicht.

        /// <summary>True wenn die Detection mindestens einmal auf Kamerahoehe bestaetigt wurde.</summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>Naechste Y-Position (normiert) an der die Detection gesehen wurde. Hoeher = naeher.</summary>
        public double MaxYCenter { get; private set; }

        /// <summary>Meterstand bei Bestaetigung (naeher = genauer).</summary>
        public double? ConfirmedMeter { get; private set; }

        /// <summary>Mindestanzahl Frames bevor ein Befund finalisiert wird.</summary>
        public const int MinConfirmationFrames = 2;

        /// <summary>Y-Schwelle ab der eine Detection als "auf Kamerahoehe" gilt (normiert).</summary>
        private const double ConfirmationYThreshold = 0.30;

        public ActiveFindingState(
            string name, double start, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? evidence = null,
            double bboxYCenterNorm = 0.5)
        {
            Name = name; MeterStart = start; MeterEnd = start;
            MaxSeverity = severity; VsaCodeHint = hint; PositionClock = clock;
            ExtentPercent = extent; HeightMm = height; WidthMm = width;
            IntrusionPercent = intrusion; CrossSectionReductionPercent = crossSection;
            DiameterReductionMm = diameterReduction;
            Evidence = evidence;
            MaxYCenter = bboxYCenterNorm;
            if (bboxYCenterNorm >= ConfirmationYThreshold)
            {
                IsConfirmed = true;
                ConfirmedMeter = start;
            }
        }

        public void Update(double meter, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? evidence = null,
            double bboxYCenterNorm = 0.5)
        {
            MeterEnd = meter;
            MissedFrames = 0;
            FrameCount++;
            if (severity > MaxSeverity) MaxSeverity = severity;
            if (!string.IsNullOrWhiteSpace(hint)) VsaCodeHint = hint;
            if (!string.IsNullOrWhiteSpace(clock)) PositionClock = clock;
            if (extent is { } e) ExtentPercent = Math.Max(ExtentPercent ?? 0, Math.Clamp(e, 1, 100));
            if (height is { } h) HeightMm = Math.Max(HeightMm ?? 0, h);
            if (width is { } w) WidthMm = Math.Max(WidthMm ?? 0, w);
            if (intrusion is { } ip) IntrusionPercent = Math.Max(IntrusionPercent ?? 0, ip);
            if (crossSection is { } csr) CrossSectionReductionPercent = Math.Max(CrossSectionReductionPercent ?? 0, csr);
            if (diameterReduction is { } dr) DiameterReductionMm = Math.Max(DiameterReductionMm ?? 0, dr);
            if (evidence is not null)
            {
                Evidence = Evidence is null ? evidence : MergeEvidence(Evidence, evidence);
            }

            // Bestaetigungs-Tracking: wenn naeher gesehen → Meter korrigieren
            if (bboxYCenterNorm > MaxYCenter)
            {
                MaxYCenter = bboxYCenterNorm;
                if (bboxYCenterNorm >= ConfirmationYThreshold && !IsConfirmed)
                {
                    IsConfirmed = true;
                    ConfirmedMeter = meter;
                    // Meter korrigieren: Bestaetigung auf Kamerahoehe ist genauer
                    MeterStart = meter;
                }
            }
        }

        /// <summary>
        /// True wenn der Befund finalisiert werden soll (genug Frames + bestaetigt).
        /// Unbestaetigte Ferndetektionen werden still verworfen.
        /// </summary>
        public bool ShouldFinalize => IsConfirmed && FrameCount >= MinConfirmationFrames;

        public RawVideoDetection ToDetection() =>
            new(Name,
                ConfirmedMeter ?? MeterStart, // Bestaetigung-Meter hat Vorrang
                MeterEnd,
                SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock,
                ExtentPercent, HeightMm, WidthMm, IntrusionPercent, CrossSectionReductionPercent, DiameterReductionMm,
                Evidence: Evidence is not null ? Evidence with { FrameCount = FrameCount } : null);

        private static string SeverityLabel(int s) => s >= 4 ? "high" : s == 3 ? "mid" : "low";

        private static AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector MergeEvidence(AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector a, AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector b) =>
            new(
                YoloConf: Max(a.YoloConf, b.YoloConf),
                DinoConf: Max(a.DinoConf, b.DinoConf),
                SamMaskStability: Max(a.SamMaskStability, b.SamMaskStability),
                QwenVisionConf: Max(a.QwenVisionConf, b.QwenVisionConf),
                LlmCodeConf: Max(a.LlmCodeConf, b.LlmCodeConf),
                KbSimilarity: Max(a.KbSimilarity, b.KbSimilarity),
                KbCodeAgreement: a.KbCodeAgreement ?? b.KbCodeAgreement,
                PlausibilityScore: Max(a.PlausibilityScore, b.PlausibilityScore),
                DamageCategory: a.DamageCategory ?? b.DamageCategory,
                FrameCount: (a.FrameCount ?? 0) + (b.FrameCount ?? 0)
            );

        private static double? Max(double? a, double? b) =>
            a.HasValue && b.HasValue ? Math.Max(a.Value, b.Value)
            : a ?? b;
    }
}
