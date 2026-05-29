// AuswertungPro - Aufnahmetechnik-Bewertung nach VSA/Furrer-Vorgaben
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Bewertet Aufnahmequalitaet eines Kanalbefahrungs-Frames.
/// WPF dekodiert nur das PNG; die eigentliche Bewertung liegt in Infrastructure.
/// </summary>
public sealed class TechniqueAssessmentService : ITechniqueAssessmentService
{
    private readonly OllamaClient _ollama;
    private readonly string _visionModel;

    public TechniqueAssessmentService(OllamaClient ollama, string visionModel = "qwen2.5vl:32b")
    {
        _ollama = ollama;
        _visionModel = visionModel;
    }

    public TechniqueAssessment AssessFrame(byte[] pngBytes, double? osdMeterReading, double protocolMeter)
    {
        var frame = LoadBgraFrame(pngBytes);
        return frame is null
            ? TechniqueFrameAnalyzer.AssessFrame(new BgraImageFrame(0, 0, []), osdMeterReading, protocolMeter)
            : TechniqueFrameAnalyzer.AssessFrame(frame, osdMeterReading, protocolMeter);
    }

    public async Task<TechniqueAssessment> AssessFrameWithVisionAsync(
        byte[] pngBytes,
        double? osdMeterReading,
        double protocolMeter,
        CancellationToken ct)
    {
        var basic = AssessFrame(pngBytes, osdMeterReading, protocolMeter);

        string? centering = null;
        try
        {
            centering = await QueryVisionCenteringAsync(pngBytes, ct);
        }
        catch
        {
            // Qwen nicht verfuegbar: Ergebnis ohne Zentrierung verwenden.
        }

        var grade = TechniqueFrameAnalyzer.ComputeGrade(
            basic.OsdReadable,
            basic.OsdDeltaMeters,
            basic.LightingQuality,
            basic.SharpnessQuality,
            centering);

        return basic with { CenteringQuality = centering, OverallGrade = grade };
    }

    private static BgraImageFrame? LoadBgraFrame(byte[] pngBytes)
    {
        try
        {
            using var ms = new MemoryStream(pngBytes);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                return null;

            BitmapSource source = decoder.Frames[0];
            if (source.Format != PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                source = converted;
            }

            var width = source.PixelWidth;
            var height = source.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            return new BgraImageFrame(width, height, pixels);
        }
        catch
        {
            return null;
        }
    }

    private sealed record VisionTechniqueResult
    {
        public string Zentrierung { get; init; } = "mittel";
        public string Beleuchtung { get; init; } = "mittel";
        public bool BewegungsUnschaerfe { get; init; }
        public bool SchwenkenWaehrendFahrt { get; init; }
    }

    private static readonly JsonElement TechniqueSchema = JsonDocument.Parse("""
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
        var b64 = Convert.ToBase64String(pngBytes);

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

        var result = await _ollama.ChatStructuredWithOptionsAsync<VisionTechniqueResult>(
            _visionModel, messages, TechniqueSchema,
            AuswertungPro.Next.Infrastructure.Ai.OllamaDeterministicOptions.Create(), ct);

        return result.Zentrierung.ToLowerInvariant() switch
        {
            "gut" => "Gut",
            "mittel" => "Mittel",
            "schlecht" => "Schlecht",
            _ => "Mittel"
        };
    }
}
