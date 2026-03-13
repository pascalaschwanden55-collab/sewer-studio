using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Lokaler KI-Service (Ollama):
/// 1) optional Vision (qwen2.5vl:32b) -> Findings
/// 2) Text/Entscheider (qwen2.5vl:32b) -> VSA-Code Suggestion
/// </summary>
public sealed class OllamaProtocolAiService : IProtocolAiService
{
    private readonly AiRuntimeConfig _cfg;
    private readonly OllamaClient _client;
    private readonly OllamaVisionFindingsService _vision;
    private readonly IRetrievalService? _retrieval;
    private readonly IAiSuggestionPlausibilityService? _plausibility;

    public OllamaProtocolAiService(
        AiRuntimeConfig cfg,
        IRetrievalService? retrieval = null,
        IAiSuggestionPlausibilityService? plausibility = null)
    {
        _cfg = cfg;
        _client = cfg.CreateOllamaClient();
        _vision = new OllamaVisionFindingsService(_client, cfg.VisionModel);
        _retrieval = retrieval;
        _plausibility = plausibility;
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

        var meterForKb = meterFromVision ?? input.Meter;
        var kbSuggestion = await TrySuggestFromKnowledgeBaseAsync(input, findings, meterForKb, ct)
            .ConfigureAwait(false);

        // --- 2) Prompt für Entscheider bauen ---
        var trainingPool = ProtocolTrainingStore.LoadRecent(80);
        var training = FilterTrainingSamples(trainingPool, input, findings, meterFromVision ?? input.Meter).Take(5).ToList();
        var prompt = BuildPrompt(input, findings, meterFromVision, severity, training, kbSuggestion);

        // Structured output only. If this fails, prefer KB fallback over free-text parsing.
        string? llmCode = null;
        string? llmReason = null;
        string? llmError = null;
        try
        {
            var dto = await _client.ChatStructuredAsync<ProtocolSuggestionDto>(
                _cfg.TextModel,
                new[]
                {
                    new OllamaClient.ChatMessage("system",
                        "Du bist ein Kanalinspektion-Experte nach VSA-Standard. " +
                        "Antworte ausschließlich im vorgegebenen JSON-Format."),
                    new OllamaClient.ChatMessage("user", prompt)
                },
                ProtocolSuggestionSchema,
                ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(dto.suggestedCode) &&
                input.AllowedCodes.Any(c => string.Equals(c, dto.suggestedCode, StringComparison.OrdinalIgnoreCase)))
            {
                llmCode = input.AllowedCodes.First(c =>
                    string.Equals(c, dto.suggestedCode, StringComparison.OrdinalIgnoreCase));
                llmReason = dto.rationale;
            }
            else if (!string.IsNullOrWhiteSpace(dto.suggestedCode))
            {
                llmError = $"LLM schlug nicht erlaubten Code '{dto.suggestedCode}' vor.";
            }
        }
        catch (Exception ex)
        {
            llmError = ex.Message;
        }

        var selectedCode = llmCode;
        var flags = new List<string>();

        if (selectedCode is null && kbSuggestion?.SuggestedCode is not null)
        {
            selectedCode = kbSuggestion.SuggestedCode;
            flags.Add("kb_fallback");
        }

        if (selectedCode is not null
            && kbSuggestion?.SuggestedCode is not null
            && !string.Equals(selectedCode, kbSuggestion.SuggestedCode, StringComparison.OrdinalIgnoreCase))
        {
            flags.Add("kb_disagrees");
        }

        var confidence = selectedCode is null
            ? 0
            : llmCode is not null
                ? Math.Max(0.65, kbSuggestion?.Confidence ?? 0.65)
                : Math.Max(0.55, kbSuggestion?.Confidence ?? 0.55);

        if (llmError is not null)
            flags.Add("llm_structured_failed");

        var reason = BuildReason(llmReason ?? llmError ?? "", kbSuggestion);

        // Apply plausibility checks if service is available
        if (_plausibility is not null && selectedCode is not null)
        {
            var checkResult = _plausibility.ApplyChecks(
                new AiSuggestionResult(selectedCode, confidence, reason, null, null),
                new ObservationContext(input.ExistingText ?? ""));
            selectedCode = checkResult.SuggestedCode;
            confidence = checkResult.Confidence;
            if (checkResult.Warnings is { Length: > 0 })
                flags.AddRange(checkResult.Warnings);
        }

        return new AiSuggestion(
            SuggestedCode: selectedCode,
            Confidence: confidence,
            Reason: reason,
            Flags: flags);
    }

    private static string BuildPrompt(
        AiInput input,
        List<string> findings,
        double? meter,
        string severity,
        IReadOnlyList<ProtocolTrainingStore.ProtocolTrainingSample> training,
        KbCodeSuggestion? kbSuggestion)
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
        if (kbSuggestion is { Matches.Count: > 0 })
        {
            sb.AppendLine("Ähnliche Fälle (Wissensdatenbank):");
            foreach (var match in kbSuggestion.Matches.Take(4))
            {
                var meterText = $"{match.MeterStart:0.00}-{match.MeterEnd:0.00}m";
                sb.AppendLine($"- Code={match.VsaCode}; Meter={meterText}; Score={match.Score:0.000}; Text={match.Description}");
            }
            if (!string.IsNullOrWhiteSpace(kbSuggestion.SuggestedCode))
            {
                sb.AppendLine($"KB-Code-Hinweis: {kbSuggestion.SuggestedCode} (confidence {kbSuggestion.Confidence:0.00})");
            }
        }
        sb.AppendLine($"Erlaubte Codes: {string.Join(", ", input.AllowedCodes)}");

        sb.Append("Antworte ausschließlich im JSON-Format: {\"suggestedCode\": \"<CODE>\", \"rationale\": \"<Begründung>\"}. Wähle nur einen Code aus 'Erlaubte Codes'.");
        return sb.ToString();
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

    private async Task<KbCodeSuggestion?> TrySuggestFromKnowledgeBaseAsync(
        AiInput input,
        IReadOnlyList<string> findings,
        double? meter,
        CancellationToken ct)
    {
        if (_retrieval is null)
            return null;

        var query = BuildKnowledgeQuery(input, findings, meter);
        if (string.IsNullOrWhiteSpace(query))
            return null;

        IReadOnlyList<RetrievalResult> retrieved;
        try
        {
            retrieved = await _retrieval.RetrieveAsync(query, topK: 10, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }

        if (retrieved.Count == 0)
            return null;

        var allowed = new HashSet<string>(input.AllowedCodes, StringComparer.OrdinalIgnoreCase);
        var matches = new List<KbMatch>();
        var aggregated = new Dictionary<string, (double Score, int Hits)>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in retrieved)
        {
            var code = item.Sample.VsaCode?.Trim();
            if (string.IsNullOrWhiteSpace(code) || !allowed.Contains(code))
                continue;

            var meterMid = (item.Sample.MeterStart + item.Sample.MeterEnd) / 2.0;
            var meterWeight = meter.HasValue
                ? Math.Max(0.35, 1.0 - Math.Min(1.0, Math.Abs(meter.Value - meterMid) / 12.0))
                : 1.0;
            var score = item.Score * meterWeight;

            matches.Add(new KbMatch(
                VsaCode: code,
                Description: item.Sample.Beschreibung,
                MeterStart: item.Sample.MeterStart,
                MeterEnd: item.Sample.MeterEnd,
                Score: score));

            if (aggregated.TryGetValue(code, out var current))
                aggregated[code] = (current.Score + score, current.Hits + 1);
            else
                aggregated[code] = (score, 1);
        }

        if (aggregated.Count == 0)
            return null;

        var rankedCodes = aggregated
            .OrderByDescending(x => x.Value.Score)
            .ThenByDescending(x => x.Value.Hits)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var best = rankedCodes[0];
        var second = rankedCodes.Count > 1 ? rankedCodes[1] : default;
        var denom = best.Value.Score + second.Value.Score;
        var confidence = denom > 0
            ? Math.Clamp(best.Value.Score / denom, 0.5, 0.98)
            : 0.5;

        var topMatches = matches
            .OrderByDescending(m => m.Score)
            .Take(5)
            .ToList();

        return new KbCodeSuggestion(
            SuggestedCode: best.Key,
            Confidence: confidence,
            Matches: topMatches);
    }

    private static string BuildKnowledgeQuery(AiInput input, IReadOnlyList<string> findings, double? meter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.ExistingText))
            parts.Add(input.ExistingText);
        if (!string.IsNullOrWhiteSpace(input.ExistingCode))
            parts.Add($"Code {input.ExistingCode}");
        if (findings.Count > 0)
            parts.Add(string.Join(", ", findings.Where(f => !string.IsNullOrWhiteSpace(f))));
        if (meter.HasValue)
            parts.Add($"Meter {meter.Value:0.00}");
        if (!string.IsNullOrWhiteSpace(input.HaltungId))
            parts.Add($"Haltung {input.HaltungId}");

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string BuildReason(string llmResponse, KbCodeSuggestion? kbSuggestion)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(llmResponse))
            sb.AppendLine(llmResponse.Trim());

        if (kbSuggestion is { Matches.Count: > 0 })
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.AppendLine("KB-Kontext:");
            foreach (var m in kbSuggestion.Matches.Take(3))
                sb.AppendLine($"- {m.VsaCode} ({m.Score:0.000}) {m.Description}");
        }

        return sb.ToString().Trim();
    }

    private sealed record KbMatch(
        string VsaCode,
        string Description,
        double MeterStart,
        double MeterEnd,
        double Score);

    private sealed record KbCodeSuggestion(
        string SuggestedCode,
        double Confidence,
        IReadOnlyList<KbMatch> Matches);

    private sealed class ProtocolSuggestionDto
    {
        public string? suggestedCode { get; set; }
        public string? rationale { get; set; }
    }

    private static readonly JsonElement ProtocolSuggestionSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "suggestedCode": { "type": ["string","null"], "description": "VSA/EN Code Vorschlag" },
            "rationale": { "type": ["string","null"], "description": "Begründung" }
          },
          "required": ["suggestedCode"]
        }
        """).RootElement.Clone();

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
