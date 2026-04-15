using System.Diagnostics;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Extrahiert Trainings-Frames aus Videos anhand von InspectionProfile-Zeitstempeln.
/// Kontextfenster: 5 Frames pro Event (t-2s, t-1s, t, t+1s, t+2s).
/// Negativ-Frames nur aus sicheren Leersegmenten.
/// </summary>
public static class InspectionFrameExtractor
{
    // Offset-Reihe fuer das Kontextfenster um jeden Event (t-2s, t, t+2s)
    private static readonly int[] KontextOffsets = [-2, 0, +2];

    /// <summary>
    /// Extrahiert alle Trainings-Frames fuer ein InspectionProfile.
    /// Pro Event 5 Kontext-Frames, pro Luecke 1 Negativ-Frame, plus Aufnahmetechnik-Frames.
    /// </summary>
    public static async Task<List<ExtractedFrame>> ExtractFramesAsync(
        InspectionProfile profile,
        string videoPath,
        string outputDir,
        CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);

        var frames = new List<ExtractedFrame>();
        var safeKey = SanitizeName(profile.HaltungKey);

        // --- 1. Event-Frames: 5 Frames pro Event ---
        foreach (var ev in profile.Ereignisse)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var offset in KontextOffsets)
            {
                var zeitSek = ev.ZeitSek + offset;
                if (zeitSek < 0) zeitSek = 0;

                var isRef = offset == 0;
                var frameTyp = isRef ? "event_reference" : "event_context";

                // Szenen-Klasse aus Code ableiten
                var szeneKlasse = BestimmeSzeneKlasse(ev.CodeFull);

                // Defekt-Klasse: null bei Aufnahme-Codes
                string? defektKlasse = IstAufnahmeCode(ev.CodeFull) ? null : ev.CodeFull;

                // Dateiname zusammensetzen
                var offsetStr = offset >= 0 ? $"+{offset}" : $"{offset}";
                var dateiname = $"{safeKey}_{zeitSek:F1}s_{SanitizeName(ev.CodeFull)}_t{offsetStr}.png";
                var ausgabePfad = Path.Combine(outputDir, dateiname);

                var erfolg = await ExtractSingleFrameAsync(videoPath, zeitSek, ausgabePfad, ct);
                if (!erfolg) continue;

                frames.Add(new ExtractedFrame(
                    PngPfad: ausgabePfad,
                    HaltungKey: profile.HaltungKey,
                    ZeitSek: zeitSek,
                    Meter: ev.Meter,
                    OffsetSek: offset,
                    IsReferenceFrame: isRef,
                    SzeneKlasse: szeneKlasse,
                    DefektKlasse: defektKlasse,
                    CodeMain: ev.CodeMain,
                    CodeFull: ev.CodeFull,
                    Uhr: ev.Uhr1,
                    LabelQualitaet: "direct_from_db",
                    FrameTyp: frameTyp,
                    Quelle: "codierung"));
            }
        }

        // --- 2. Negativ-Frames aus Luecken ---
        foreach (var luecke in profile.Luecken)
        {
            ct.ThrowIfCancellationRequested();

            // Pruefe ob Luecke gross genug ist (>3m und >30s)
            var distanzOk = luecke.DistanzM.HasValue && luecke.DistanzM.Value > 3.0;
            var dauerOk = luecke.DauerSek > 30.0;

            // Pruefe ob kein Event in +-2m Umkreis
            var mitteZeit = (luecke.VonZeit + luecke.BisZeit) / 2.0;
            var mitteMeter = luecke.VonMeter.HasValue && luecke.BisMeter.HasValue
                ? (luecke.VonMeter.Value + luecke.BisMeter.Value) / 2.0
                : (double?)null;

            var keinNaherEvent = !profile.Ereignisse.Any(e =>
                e.Meter.HasValue && mitteMeter.HasValue &&
                Math.Abs(e.Meter.Value - mitteMeter.Value) <= 2.0);

            // Luecke muss mindestens >3m und kein naher Event haben
            if (!distanzOk || !keinNaherEvent) continue;

            var istSicher = distanzOk && dauerOk;
            var labelQualitaet = istSicher ? "derived_from_gap" : "neutral_unlabeled";
            var frameTyp = istSicher ? "negativ_sicher" : "negativ_unsicher";

            var dateiname = $"{safeKey}_{mitteZeit:F1}s_kein_schaden.png";
            var ausgabePfad = Path.Combine(outputDir, dateiname);

            var erfolg = await ExtractSingleFrameAsync(videoPath, mitteZeit, ausgabePfad, ct);
            if (!erfolg) continue;

            frames.Add(new ExtractedFrame(
                PngPfad: ausgabePfad,
                HaltungKey: profile.HaltungKey,
                ZeitSek: mitteZeit,
                Meter: mitteMeter,
                OffsetSek: 0,
                IsReferenceFrame: true,
                SzeneKlasse: "axial",
                DefektKlasse: null,
                CodeMain: null,
                CodeFull: null,
                Uhr: null,
                LabelQualitaet: labelQualitaet,
                FrameTyp: frameTyp,
                Quelle: "luecke"));
        }

        // --- 3. Aufnahmetechnik-Frames: 3s vor/nach BCD ---
        var bcdEvents = profile.Ereignisse
            .Where(e => e.CodeFull.StartsWith("BCD", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var bcd in bcdEvents)
        {
            ct.ThrowIfCancellationRequested();

            // 3s VOR BCD → Schacht-Phase
            var zeitVor = Math.Max(0, bcd.ZeitSek - 3.0);
            var dateinameSchacht = $"{safeKey}_{zeitVor:F1}s_schacht.png";
            var pfadSchacht = Path.Combine(outputDir, dateinameSchacht);

            var erfolg = await ExtractSingleFrameAsync(videoPath, zeitVor, pfadSchacht, ct);
            if (erfolg)
            {
                frames.Add(new ExtractedFrame(
                    PngPfad: pfadSchacht,
                    HaltungKey: profile.HaltungKey,
                    ZeitSek: zeitVor,
                    Meter: null,
                    OffsetSek: -3,
                    IsReferenceFrame: false,
                    SzeneKlasse: "schacht",
                    DefektKlasse: null,
                    CodeMain: null,
                    CodeFull: null,
                    Uhr: null,
                    LabelQualitaet: "derived_from_gap",
                    FrameTyp: "aufnahmetechnik",
                    Quelle: "aufnahmetechnik"));
            }

            // 3s NACH BCD → Axial-Phase (Kamera im Rohr)
            var zeitNach = bcd.ZeitSek + 3.0;
            var dateinameAxial = $"{safeKey}_{zeitNach:F1}s_axial.png";
            var pfadAxial = Path.Combine(outputDir, dateinameAxial);

            erfolg = await ExtractSingleFrameAsync(videoPath, zeitNach, pfadAxial, ct);
            if (erfolg)
            {
                frames.Add(new ExtractedFrame(
                    PngPfad: pfadAxial,
                    HaltungKey: profile.HaltungKey,
                    ZeitSek: zeitNach,
                    Meter: null,
                    OffsetSek: +3,
                    IsReferenceFrame: false,
                    SzeneKlasse: "axial",
                    DefektKlasse: null,
                    CodeMain: null,
                    CodeFull: null,
                    Uhr: null,
                    LabelQualitaet: "derived_from_gap",
                    FrameTyp: "aufnahmetechnik",
                    Quelle: "aufnahmetechnik"));
            }
        }

        return frames;
    }

    /// <summary>
    /// Speichert den Frame-Index als JSON-Datei.
    /// </summary>
    public static void SaveFrameIndex(List<ExtractedFrame> frames, string outputPath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(frames, options);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Extrahiert einen einzelnen Frame aus dem Video via ffmpeg.
    /// Gibt true zurueck wenn der Frame bereits existiert oder erfolgreich extrahiert wurde.
    /// </summary>
    private static async Task<bool> ExtractSingleFrameAsync(
        string videoPath,
        double sekunden,
        string outputPng,
        CancellationToken ct)
    {
        // Frame bereits vorhanden → ueberspringen
        if (File.Exists(outputPng)) return true;

        // Ausgabe-Ordner sicherstellen
        var dir = Path.GetDirectoryName(outputPng);
        if (dir != null) Directory.CreateDirectory(dir);

        try
        {
            var args = $"-ss {sekunden.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} " +
                       $"-i \"{videoPath}\" " +
                       $"-frames:v 1 -q:v 2 \"{outputPng}\" -y";

            var psi = new ProcessStartInfo("ffmpeg", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var prozess = Process.Start(psi);
            if (prozess == null) return false;

            // stderr drainieren damit ffmpeg nicht blockiert
            _ = prozess.StandardError.ReadToEndAsync();
            _ = prozess.StandardOutput.ReadToEndAsync();

            // Timeout: 30s pro Frame
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            try
            {
                await prozess.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                try { prozess.Kill(); } catch { }
                return false;
            }

            return prozess.ExitCode == 0 && File.Exists(outputPng);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // ffmpeg nicht gefunden oder anderer Fehler → Frame ueberspringen
            return false;
        }
    }

    /// <summary>
    /// Ersetzt ungueltige Dateinamen-Zeichen durch Unterstrich.
    /// </summary>
    private static string SanitizeName(string name)
    {
        var ungueltig = Path.GetInvalidFileNameChars();
        var ergebnis = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            ergebnis.Append(ungueltig.Contains(c) ? '_' : c);
        }
        return ergebnis.ToString();
    }

    /// <summary>
    /// Leitet die Szenen-Klasse aus dem Code-String ab.
    /// </summary>
    private static string BestimmeSzeneKlasse(string codeFull)
    {
        if (codeFull.StartsWith("BCD", StringComparison.OrdinalIgnoreCase) ||
            codeFull.StartsWith("BCE", StringComparison.OrdinalIgnoreCase))
            return "uebergang";

        // BA, BB, BC und alle anderen → axial
        return "axial";
    }

    /// <summary>
    /// Prueft ob der Code ein reiner Aufnahme-Code ist (BCD, BCE, BDB).
    /// Fuer diese Codes wird keine Defekt-Klasse gesetzt.
    /// </summary>
    private static bool IstAufnahmeCode(string codeFull)
    {
        return codeFull.StartsWith("BCD", StringComparison.OrdinalIgnoreCase) ||
               codeFull.StartsWith("BCE", StringComparison.OrdinalIgnoreCase) ||
               codeFull.StartsWith("BDB", StringComparison.OrdinalIgnoreCase);
    }
}
