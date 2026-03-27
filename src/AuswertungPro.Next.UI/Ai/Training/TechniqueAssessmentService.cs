// AuswertungPro – Aufnahmetechnik-Bewertung nach VSA/Furrer-Vorgaben
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Bewertet Aufnahmequalitaet eines Kanalbefahrungs-Frames.
/// Deterministische C#-Pruefungen + optionaler Qwen-Call fuer Zentrierung/Beleuchtung.
/// Universell einsetzbar: Selbsttraining, Live-Codierung, Pipeline.
/// </summary>
public interface ITechniqueAssessmentService
{
    /// <summary>
    /// Bewertet einen einzelnen Frame deterministisch (ohne LLM).
    /// </summary>
    /// <param name="pngBytes">Frame als PNG.</param>
    /// <param name="osdMeterReading">Vom OSD gelesener Meter-Wert (null wenn nicht lesbar).</param>
    /// <param name="protocolMeter">Meter-Wert laut Protokoll.</param>
    TechniqueAssessment AssessFrame(byte[] pngBytes, double? osdMeterReading, double protocolMeter);

    /// <summary>
    /// Bewertet einen Frame mit optionalem Qwen-Call fuer Zentrierung/Beleuchtungs-Detailanalyse.
    /// Nur 1x pro Haltung aufrufen (nicht pro Frame).
    /// </summary>
    Task<TechniqueAssessment> AssessFrameWithVisionAsync(
        byte[] pngBytes,
        double? osdMeterReading,
        double protocolMeter,
        CancellationToken ct);
}

public sealed class TechniqueAssessmentService : ITechniqueAssessmentService
{
    private readonly OllamaClient _ollama;
    private readonly string _visionModel;

    // Schwellenwerte nach Furrer/VSA Vorgaben
    private const double OsdGoodDelta = 0.5;     // < 0.5m = Gut
    private const double OsdMediumDelta = 1.0;    // < 1.0m = Mittel, >= 1.0m = Schlecht
    private const double LuminanceDark = 40.0;    // < 40 = Dunkel
    private const double LuminanceBright = 200.0; // > 200 = Ueberbelichtet
    private const double SharpnessThreshold = 50.0; // < 50 = Unscharf

    public TechniqueAssessmentService(OllamaClient ollama, string visionModel = "qwen2.5vl:32b")
    {
        _ollama = ollama;
        _visionModel = visionModel;
    }

    public TechniqueAssessment AssessFrame(byte[] pngBytes, double? osdMeterReading, double protocolMeter)
    {
        bool osdReadable = osdMeterReading.HasValue;
        double? osdDelta = osdReadable ? Math.Abs(osdMeterReading!.Value - protocolMeter) : null;

        // Bildanalyse
        double meanLum = ComputeMeanLuminance(pngBytes);
        double lapVar = ComputeLaplacianVariance(pngBytes);

        string lighting = EvaluateLighting(meanLum);
        string sharpness = EvaluateSharpness(lapVar);
        string grade = ComputeGrade(osdReadable, osdDelta, lighting, sharpness, centering: null);

        return new TechniqueAssessment(
            OsdReadable: osdReadable,
            OsdDeltaMeters: osdDelta,
            LightingQuality: lighting,
            SharpnessQuality: sharpness,
            CenteringQuality: null,
            OverallGrade: grade,
            MeanLuminance: meanLum,
            LaplacianVariance: lapVar);
    }

    public async Task<TechniqueAssessment> AssessFrameWithVisionAsync(
        byte[] pngBytes,
        double? osdMeterReading,
        double protocolMeter,
        CancellationToken ct)
    {
        // Deterministische Basis
        var basic = AssessFrame(pngBytes, osdMeterReading, protocolMeter);

        // Qwen-Call fuer Zentrierung + Beleuchtungsdetails
        string? centering = null;
        try
        {
            centering = await QueryVisionCenteringAsync(pngBytes, ct);
        }
        catch
        {
            // Qwen nicht verfuegbar → Ergebnis ohne Zentrierung
        }

        // Gesamtnote mit Zentrierung neu berechnen
        string grade = ComputeGrade(
            basic.OsdReadable, basic.OsdDeltaMeters,
            basic.LightingQuality, basic.SharpnessQuality, centering);

        return basic with { CenteringQuality = centering, OverallGrade = grade };
    }

    // ── Bildanalyse-Methoden ──

    /// <summary>Durchschnittliche Luminanz (0-255) aus dem PNG-Bild.</summary>
    private static double ComputeMeanLuminance(byte[] pngBytes)
    {
        try
        {
            var bmp = LoadBitmap(pngBytes);
            if (bmp == null) return 100; // Fallback Mittelwert

            int stride = bmp.PixelWidth * 4;
            var pixels = new byte[stride * bmp.PixelHeight];
            bmp.CopyPixels(pixels, stride, 0);

            // Sampling: jeden 8. Pixel fuer Performance
            long sum = 0;
            int count = 0;
            for (int i = 0; i < pixels.Length - 3; i += 32) // 8 Pixel * 4 Bytes
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                // ITU-R BT.601 Luminanz
                sum += (int)(0.299 * r + 0.587 * g + 0.114 * b);
                count++;
            }

            return count > 0 ? (double)sum / count : 100;
        }
        catch
        {
            return 100; // Sicherer Fallback
        }
    }

    /// <summary>
    /// Laplace-Varianz als Schaerfe-Mass.
    /// Hoher Wert = scharfes Bild, niedriger Wert = unscharf/Bewegungsunschaerfe.
    /// </summary>
    private static double ComputeLaplacianVariance(byte[] pngBytes)
    {
        try
        {
            var bmp = LoadBitmap(pngBytes);
            if (bmp == null) return 100;

            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[stride * h];
            bmp.CopyPixels(pixels, stride, 0);

            // Graustufenbild erstellen (Sampling fuer Performance)
            int sampleStep = Math.Max(1, Math.Min(w, h) / 200); // ~200x200 Samples
            int sw = (w - 2) / sampleStep;
            int sh = (h - 2) / sampleStep;
            if (sw < 3 || sh < 3) return 100;

            // Laplace-Kernel: [0,1,0; 1,-4,1; 0,1,0]
            double sum = 0;
            double sumSq = 0;
            int count = 0;

            for (int y = 1; y < h - 1; y += sampleStep)
            {
                for (int x = 1; x < w - 1; x += sampleStep)
                {
                    double center = GetLuminance(pixels, x, y, stride);
                    double top = GetLuminance(pixels, x, y - 1, stride);
                    double bottom = GetLuminance(pixels, x, y + 1, stride);
                    double left = GetLuminance(pixels, x - 1, y, stride);
                    double right = GetLuminance(pixels, x + 1, y, stride);

                    double lap = top + bottom + left + right - 4 * center;
                    sum += lap;
                    sumSq += lap * lap;
                    count++;
                }
            }

            if (count == 0) return 100;
            double mean = sum / count;
            return (sumSq / count) - (mean * mean); // Varianz
        }
        catch
        {
            return 100;
        }
    }

    private static double GetLuminance(byte[] pixels, int x, int y, int stride)
    {
        int idx = y * stride + x * 4;
        return 0.299 * pixels[idx + 2] + 0.587 * pixels[idx + 1] + 0.114 * pixels[idx];
    }

    private static BitmapSource? LoadBitmap(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) return null;
        var frame = decoder.Frames[0];
        // Sicherstellen dass Bgra32
        if (frame.Format != System.Windows.Media.PixelFormats.Bgra32)
        {
            return new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        }
        return frame;
    }

    // ── Bewertungs-Logik ──

    private static string EvaluateLighting(double meanLuminance)
    {
        if (meanLuminance < LuminanceDark) return "Schlecht";
        if (meanLuminance > LuminanceBright) return "Schlecht";
        if (meanLuminance < 60 || meanLuminance > 180) return "Mittel";
        return "Gut";
    }

    private static string EvaluateSharpness(double laplacianVariance)
    {
        if (laplacianVariance < 30) return "Schlecht";
        if (laplacianVariance < SharpnessThreshold) return "Mittel";
        return "Gut";
    }

    /// <summary>Gesamtnote A/B/C basierend auf Einzelbewertungen.</summary>
    private static string ComputeGrade(
        bool osdReadable, double? osdDelta,
        string lighting, string sharpness, string? centering)
    {
        int score = 0;
        int maxScore = 0;

        // OSD (Gewicht 2)
        maxScore += 2;
        if (osdReadable && osdDelta.HasValue)
        {
            if (osdDelta.Value < OsdGoodDelta) score += 2;
            else if (osdDelta.Value < OsdMediumDelta) score += 1;
        }

        // Beleuchtung (Gewicht 2)
        maxScore += 2;
        score += lighting switch { "Gut" => 2, "Mittel" => 1, _ => 0 };

        // Schaerfe (Gewicht 2)
        maxScore += 2;
        score += sharpness switch { "Gut" => 2, "Mittel" => 1, _ => 0 };

        // Zentrierung (Gewicht 2, optional)
        if (centering != null)
        {
            maxScore += 2;
            score += centering switch { "Gut" => 2, "Mittel" => 1, _ => 0 };
        }

        double pct = maxScore > 0 ? (double)score / maxScore : 0;
        if (pct >= 0.75) return "A";
        if (pct >= 0.45) return "B";
        return "C";
    }

    // ── Qwen Vision-Call (optional, 1x pro Haltung) ──

    private sealed record VisionTechniqueResult
    {
        public string Zentrierung { get; init; } = "mittel";
        public string Beleuchtung { get; init; } = "mittel";
        public bool BewegungsUnschaerfe { get; init; }
        public bool SchwenkenWaehrendFahrt { get; init; }
    }

    private static readonly JsonElement _techniqueSchema = JsonDocument.Parse("""
    {
      "type": "object",
      "properties": {
        "zentrierung":  { "type": "string", "enum": ["gut", "mittel", "schlecht"] },
        "beleuchtung":  { "type": "string", "enum": ["gut", "mittel", "schlecht"] },
        "bewegungsUnschaerfe": { "type": "boolean" },
        "schwenkenWaehrendFahrt": { "type": "boolean" }
      },
      "required": ["zentrierung", "beleuchtung", "bewegungsUnschaerfe", "schwenkenWaehrendFahrt"]
    }
    """).RootElement;

    private async Task<string?> QueryVisionCenteringAsync(byte[] pngBytes, CancellationToken ct)
    {
        string b64 = Convert.ToBase64String(pngBytes);

        const string prompt = """
            Analysiere dieses Bild einer Kanalbefahrung bezueglich Aufnahmetechnik.
            Bewerte:
            1. Zentrierung: Ist die Kamera zentrisch in der Rohrachse positioniert?
            2. Beleuchtung: Ist die Ausleuchtung gleichmaessig ohne dunkle Bereiche oder Ueberbelichtung?
            3. Bewegungsunschaerfe: Zeigt das Bild Anzeichen von Bewegungsunschaerfe?
            4. Schwenken waehrend Fahrt: Gibt es Anzeichen, dass die Kamera waehrend der Fahrt geschwenkt wurde?
            Antworte NUR im JSON-Format.
            """;

        var messages = new[]
        {
            new OllamaClient.ChatMessage("user", prompt, new[] { b64 })
        };

        var result = await _ollama.ChatStructuredAsync<VisionTechniqueResult>(
            _visionModel, messages, _techniqueSchema, ct);

        // Zentrierung normalisieren auf "Gut"/"Mittel"/"Schlecht"
        return result.Zentrierung.ToLowerInvariant() switch
        {
            "gut" => "Gut",
            "mittel" => "Mittel",
            "schlecht" => "Schlecht",
            _ => "Mittel"
        };
    }
}
