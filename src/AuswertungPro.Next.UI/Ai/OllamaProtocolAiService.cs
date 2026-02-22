using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Lokaler KI-Service (Ollama):
/// 1) optional Vision (qwen2.5vl) -> Findings
/// 2) Text/Entscheider (gpt-oss) -> VSA-Code Suggestion
/// </summary>
public sealed class OllamaProtocolAiService : IProtocolAiService
{
    private readonly AiRuntimeConfig _cfg;
    private readonly OllamaClient _client;
    private readonly OllamaVisionFindingsService _vision;

    public OllamaProtocolAiService(AiRuntimeConfig cfg)
    {
        _cfg = cfg;
        _client = new OllamaClient(cfg.OllamaBaseUri);
        _vision = new OllamaVisionFindingsService(_client, cfg.VisionModel);
    }

    public async Task<AiSuggestion?> SuggestAsync(AiInput input, CancellationToken ct = default)
    {
        if (!_cfg.Enabled)
            return null;

        if (input.AllowedCodes is not { Count: > 0 })
        {
            return new AiSuggestion(
                SuggestedCode: null,
                Confidence: 0,
                Reason: "Kein Code-Katalog vorhanden (AllowedCodes leer).",
                Flags: new[] { "no_catalog" });
        }

        // --- 1) Bild holen (Video->Frame oder Foto) ---
        string? frameBase64 = null;
        double? meterFromVision = null;
        List<string> findings = new();
        string severity = "low";

        var imagePaths = input.ImagePathsAbs?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();

        if (!string.IsNullOrWhiteSpace(input.VideoPathAbs) && input.Zeit is not null)
        {
            var bytes = await VideoFrameExtractor.TryExtractFramePngAsync(_cfg.FfmpegPath ?? "ffmpeg", input.VideoPathAbs!, input.Zeit.Value, ct);
            if (bytes is { Length: > 0 })
                frameBase64 = Convert.ToBase64String(bytes);
        }

        if (frameBase64 == null && imagePaths.Count > 0)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(imagePaths[0], ct);
                frameBase64 = Convert.ToBase64String(bytes);
            }
            catch { /* ignore */ }
        }

        if (frameBase64 != null)
        {
            var vision = await _vision.AnalyzeAsync(frameBase64, ct);
            meterFromVision = vision.Meter;
            findings = vision.Findings.ToList();
            severity = vision.Severity;
        }

        // --- 2) Prompt für Entscheider bauen ---
        var trainingPool = ProtocolTrainingStore.LoadRecent(80);
        var training = FilterTrainingSamples(trainingPool, input, findings, meterFromVision ?? input.Meter).Take(5).ToList();
        var prompt = BuildPrompt(input, findings, meterFromVision, severity, training);
        var response = await _client.GenerateAsync(_cfg.TextModel, prompt, null, ct);
        // ...hier ggf. Parsing/Scoring/Flags...
        return new AiSuggestion(
            SuggestedCode: ExtractCode(response, input.AllowedCodes),
            Confidence: 1.0,
            Reason: response,
            Flags: Array.Empty<string>());
    }

    private static string BuildPrompt(
        AiInput input,
        List<string> findings,
        double? meter,
        string severity,
        IReadOnlyList<ProtocolTrainingStore.ProtocolTrainingSample> training)
    {
        // Prompt-Template für KI-Entscheider
        var sb = new StringBuilder();
        sb.AppendLine($"Projekt: {input.ProjectFolderAbs}");
        sb.AppendLine($"Haltung: {input.HaltungId}");
        sb.AppendLine($"Meter: {(meter?.ToString("0.00") ?? input.Meter?.ToString("0.00") ?? "unbekannt")}");
        sb.AppendLine($"Vorbefund: {input.ExistingCode} {input.ExistingText}");
        sb.AppendLine($"Erkannte Schäden: {string.Join(", ", findings)}");
        sb.AppendLine($"Schweregrad: {severity}");
        if (training.Count > 0)
        {
            sb.AppendLine("Beispiele (Training):");
            foreach (var t in training)
            {
                var range = t.IsStreckenschaden ? "Strecke" : "Meter";
                sb.AppendLine($"- Code={t.Code}; {range}={t.MeterStart:0.00}-{t.MeterEnd:0.00}; Text={t.Beschreibung}");
            }
        }
        sb.AppendLine($"Erlaubte Codes: {string.Join(", ", input.AllowedCodes)}");
        sb.Append("Bitte schlage den zutreffendsten VSA-Code vor (nur Code, keine Erklärung).");
        return sb.ToString();
    }

    private static string? ExtractCode(string response, IReadOnlyList<string> allowedCodes)
    {
        // Extrahiere den ersten erlaubten Code aus der Antwort
        foreach (var code in allowedCodes)
        {
            if (response.Contains(code, StringComparison.OrdinalIgnoreCase))
                return code;
        }
        return null;
    }

    private static IEnumerable<ProtocolTrainingStore.ProtocolTrainingSample> FilterTrainingSamples(
        IReadOnlyList<ProtocolTrainingStore.ProtocolTrainingSample> pool,
        AiInput input,
        IReadOnlyList<string> findings,
        double? meter)
    {
        if (pool.Count == 0)
            return Array.Empty<ProtocolTrainingStore.ProtocolTrainingSample>();

        var code = (input.ExistingCode ?? "").Trim();

        var scored = pool
            .Select(p => new
            {
                Sample = p,
                Score = ScoreSample(p, code, findings, meter)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Sample.AtUtc)
            .Select(x => x.Sample);

        return scored;
    }

    private static int ScoreSample(
        ProtocolTrainingStore.ProtocolTrainingSample p,
        string code,
        IReadOnlyList<string> findings,
        double? meter)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(code) && string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase))
            score += 5;

        if (findings.Count > 0)
        {
            foreach (var f in findings)
            {
                if (!string.IsNullOrWhiteSpace(f) &&
                    p.Beschreibung.Contains(f, StringComparison.OrdinalIgnoreCase))
                    score += 2;
            }
        }

        if (meter is not null && p.MeterStart is not null && p.MeterEnd is not null)
        {
            var mid = (p.MeterStart.Value + p.MeterEnd.Value) / 2.0;
            var diff = Math.Abs(mid - meter.Value);
            if (diff <= 1.0) score += 2;
            else if (diff <= 3.0) score += 1;
        }

        if (p.Parameters is { Count: > 0 })
            score += 1;

        return score;
    }
}
