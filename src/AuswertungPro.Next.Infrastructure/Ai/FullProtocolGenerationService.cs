using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Workflow: Detections -> vollstÃ¤ndiges ProtocolDocument
///
/// BUG 1.3 FIX: GenerateAsync analysiert das Video NICHT mehr selbst.
/// Stattdessen akzeptiert GenerateFromDetectionsAsync bereits analysierte
/// RawVideoDetections. VideoAnalysisPipelineService Ã¼bergibt diese.
/// Damit wird jede Video-Analyse nur noch einmal durchgefÃ¼hrt.
/// </summary>
public sealed class FullProtocolGenerationService : IDisposable
{
    private readonly OllamaClient _client;
    private readonly IAiSuggestionPlausibilityService _plausibility;
    private readonly AiRuntimeSettings _cfg;
    private readonly IRetrievalService? _retrieval;
    private readonly KnowledgeBaseContext? _ownedKbContext;
    private readonly QualityGateService _qualityGate;
    private readonly Dictionary<string, IReadOnlyList<RetrievalResult>> _retrievalCache =
        new(StringComparer.OrdinalIgnoreCase);

    public FullProtocolGenerationService(
        AiRuntimeSettings cfg,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient,
        IRetrievalService? retrieval = null,
        QualityGateService? qualityGate = null)
    {
        _cfg = cfg;
        _plausibility = plausibility;
        _client = new OllamaClient(
            cfg.OllamaBaseUri,
            httpClient,
            cfg.OllamaRequestTimeout,
            keepAlive: cfg.OllamaKeepAlive,
            numCtx: cfg.OllamaNumCtx);
        _retrieval = retrieval;
        _qualityGate = qualityGate ?? new QualityGateService();

        // Only create own KB when none provided and AI is active
        if (_retrieval is null && cfg.Enabled)
        {
            try
            {
                var ollamaConfig = new OllamaConfig(
                    cfg.OllamaBaseUri,
                    cfg.VisionModel,
                    cfg.TextModel,
                    cfg.EmbedModel ?? OllamaConfig.DefaultEmbedModel,
                    cfg.OllamaRequestTimeout,
                    cfg.OllamaKeepAlive,
                    cfg.OllamaNumCtx);
                _ownedKbContext = new KnowledgeBaseContext();
                var embedder = new EmbeddingService(httpClient, ollamaConfig);
                _retrieval = new RetrievalService(_ownedKbContext, embedder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FullProtocolGen] KB-Init fehlgeschlagen: {ex.Message}");
                _retrieval = null;
            }
        }
    }

    public void Dispose() => _ownedKbContext?.Dispose();

    // â”€â”€ BUG 1.3 FIX: Nimmt bereits analysierte Detections entgegen â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Mappt bereits erkannte Detections auf VSA-Codes und baut ein ProtocolDocument.
    /// Das Video wird NICHT erneut analysiert.
    /// </summary>
    public async Task<FullProtocolGenerationResult> GenerateFromDetectionsAsync(
        IReadOnlyList<RawVideoDetection> detections,
        FullProtocolGenerationRequest request,
        IProgress<CodeMappingProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled)
            return FullProtocolGenerationResult.Failed("KI ist deaktiviert.");

        if (request.AllowedCodes is not { Count: > 0 })
            return FullProtocolGenerationResult.Failed("Kein Code-Katalog vorhanden.");

        if (detections.Count == 0)
        {
            return new FullProtocolGenerationResult(
                Document: BuildEmptyDocument(request),
                MappedEntries: Array.Empty<MappedProtocolEntry>(),
                Error: null,
                Warnings: new[] { "Keine SchÃ¤den erkannt." });
        }

        var mappedEntries = new List<MappedProtocolEntry>();
        var total = detections.Count;

        for (var i = 0; i < detections.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var detection = detections[i];

            progress?.Report(new CodeMappingProgress(i + 1, total,
                $"Code-Mapping {i + 1}/{total}: {detection.FindingLabel}"));

            var entry = await MapDetectionAsync(detection, request, ct).ConfigureAwait(false);
            mappedEntries.Add(entry);
        }

        var protocolEntries = mappedEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.SuggestedCode))
            .Select(BuildProtocolEntry)
            .ToList();

        var warnings = mappedEntries
            .SelectMany(e => e.Warnings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        progress?.Report(new CodeMappingProgress(total, total,
            $"Fertig â€“ {protocolEntries.Count} EintrÃ¤ge gemappt."));

        return new FullProtocolGenerationResult(
            Document: BuildDocument(request, protocolEntries),
            MappedEntries: mappedEntries,
            Error: null,
            Warnings: warnings);
    }

    // â”€â”€ Private â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<MappedProtocolEntry> MapDetectionAsync(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request,
        CancellationToken ct)
    {
        var kbExamples = await GetKnowledgeExamplesAsync(detection, request, ct).ConfigureAwait(false);

        // Wenn EnhancedVision bereits einen Code-Hinweis geliefert hat,
        // diesen im Prompt priorisieren â†’ spart LLM-Aufwand
        var vsaHint = !string.IsNullOrWhiteSpace(detection.VsaCodeHint)
            ? $"\nVision-Code-Hinweis (priorisiere falls plausibel): {detection.VsaCodeHint}"
            : string.Empty;

        var prompt = BuildPrompt(detection, request, vsaHint, kbExamples);

        AiSuggestionResultDto dto;
        try
        {
            dto = await _client.ChatStructuredWithOptionsAsync<AiSuggestionResultDto>(
                model: _cfg.TextModel,
                messages: new[]
                {
                    new OllamaClient.ChatMessage("system", BuildSystemPrompt()),
                    new OllamaClient.ChatMessage("user", prompt)
                },
                formatSchema: AiSuggestionSchemas.AiSuggestionResultSchema,
                options: OllamaDeterministicOptions.Create(),
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            if (kbExamples.Count > 0)
            {
                var fallback = kbExamples[0];
                return new MappedProtocolEntry(
                    Detection: detection,
                    SuggestedCode: fallback.Code,
                    Confidence: Math.Clamp(fallback.Score, 0.35, 0.85),
                    Reason: "LLM-Fehler, KB-Fallback verwendet: " + ex.Message,
                    Warnings: new[] { "Code-Mapping fehlgeschlagen, KB-Fallback verwendet." });
            }

            return new MappedProtocolEntry(
                Detection: detection,
                SuggestedCode: null,
                Confidence: 0,
                Reason: ex.Message,
                Warnings: new[] { "Code-Mapping fehlgeschlagen: " + ex.Message });
        }

        var checked_ = _plausibility.ApplyChecks(
            dto.ToDomain(),
            new ObservationContext(detection.FindingLabel));

        var suggestedCode = checked_.SuggestedCode;
        var confidence = checked_.Confidence;
        var reason = checked_.Rationale;
        var warnings = checked_.Warnings?.ToList() ?? new List<string>();

        if (string.IsNullOrWhiteSpace(suggestedCode) && kbExamples.Count > 0)
        {
            var fallback = kbExamples[0];
            suggestedCode = fallback.Code;
            confidence = Math.Max(confidence, Math.Clamp(fallback.Score, 0.35, 0.85));
            reason = string.IsNullOrWhiteSpace(reason)
                ? $"KB-Fallback: {fallback.Code}"
                : $"{reason} | KB-Fallback: {fallback.Code}";
            warnings.Add("LLM lieferte keinen gÃ¼ltigen Code, KB-Fallback verwendet.");
        }

        // â”€â”€ QualityGate: build EvidenceVector and evaluate â”€â”€
        var kbTopScore = kbExamples.Count > 0 ? kbExamples[0].Score : (double?)null;
        var kbAgrees = kbExamples.Count > 0 && !string.IsNullOrWhiteSpace(suggestedCode)
            && kbExamples[0].Code.Equals(suggestedCode, StringComparison.OrdinalIgnoreCase);

        var evidence = detection.Evidence ?? new EvidenceVector();
        evidence = evidence with
        {
            LlmCodeConf = confidence,
            KbSimilarity = kbTopScore,
            KbCodeAgreement = kbExamples.Count > 0 ? kbAgrees : null,
            PlausibilityScore = checked_.Confidence,
            DamageCategory = suggestedCode
        };

        var qgResult = _qualityGate.Evaluate(evidence);
        var compositeConfidence = qgResult.CompositeConfidence;

        // Update detection with enriched evidence
        var enrichedDetection = detection with { Evidence = evidence };

        return new MappedProtocolEntry(
            Detection: enrichedDetection,
            SuggestedCode: suggestedCode,
            Confidence: compositeConfidence,
            Reason: reason,
            Warnings: warnings,
            QualityGateResult: qgResult,
            Uncertainty: UncertaintyEstimate.FromSinglePass(compositeConfidence));
    }

    private static string BuildPrompt(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request,
        string vsaHint,
        IReadOnlyList<KbExample> kbExamples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Antworte ausschlieÃŸlich im vorgegebenen JSON-Schema.");
        sb.AppendLine("WÃ¤hle nur EINEN Basiscode aus 'Erlaubte Codes'.");
        sb.AppendLine();
        sb.AppendLine($"Befund: {detection.FindingLabel}");
        sb.AppendLine($"Schweregrad: {detection.Severity}");
        sb.AppendLine($"Meterbereich: {detection.MeterStart:0.00}m â€“ {detection.MeterEnd:0.00}m");
        if (!string.IsNullOrWhiteSpace(detection.PositionClock))
            sb.AppendLine($"Uhrlage: {detection.PositionClock}");
        if (detection.ExtentPercent is > 0)
            sb.AppendLine($"Ausdehnung: {detection.ExtentPercent}% Umfang");
        if (detection.HeightMm is > 0)
            sb.AppendLine($"SchadenshÃ¶he: {detection.HeightMm}mm");
        if (detection.WidthMm is > 0)
            sb.AppendLine($"Schadensbreite: {detection.WidthMm}mm");
        if (detection.IntrusionPercent is > 0)
            sb.AppendLine($"Einragungsgrad: {detection.IntrusionPercent}%");
        if (detection.CrossSectionReductionPercent is > 0)
            sb.AppendLine($"Querschnittsverringerung: {detection.CrossSectionReductionPercent}%");
        if (detection.DiameterReductionMm is > 0)
            sb.AppendLine($"Durchmesserverringerung: {detection.DiameterReductionMm}mm");
        sb.AppendLine($"Haltung: {request.HaltungId}");
        if (!string.IsNullOrWhiteSpace(vsaHint))
            sb.AppendLine(vsaHint);
        if (kbExamples.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Ã„hnliche FÃ¤lle aus Wissensdatenbank:");
            foreach (var ex in kbExamples.Take(3))
            {
                sb.AppendLine(
                    $"- Code={ex.Code}; Score={ex.Score:0.000}; Meter={ex.MeterStart:0.00}-{ex.MeterEnd:0.00}m; Text={ex.Description}");
            }
        }
        sb.AppendLine();
        sb.AppendLine($"Erlaubte Codes: {string.Join(", ", request.AllowedCodes)}");
        sb.AppendLine("Erzeuge: suggestedCode, confidence (0.0-1.0), rationale, evidence, warnings.");
        return sb.ToString();
    }

    private async Task<IReadOnlyList<KbExample>> GetKnowledgeExamplesAsync(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request,
        CancellationToken ct)
    {
        if (_retrieval is null)
            return Array.Empty<KbExample>();

        var query = BuildKnowledgeQuery(detection, request);
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<KbExample>();

        if (!_retrievalCache.TryGetValue(query, out var retrieved))
        {
            try
            {
                retrieved = await _retrieval.RetrieveAsync(query, topK: 8, ct).ConfigureAwait(false);
                _retrievalCache[query] = retrieved;
            }
            catch
            {
                return Array.Empty<KbExample>();
            }
        }

        var allowed = new HashSet<string>(request.AllowedCodes, StringComparer.OrdinalIgnoreCase);
        var meterCenter = (detection.MeterStart + detection.MeterEnd) / 2.0;

        return retrieved
            .Where(r => !string.IsNullOrWhiteSpace(r.Sample.VsaCode) && allowed.Contains(r.Sample.VsaCode))
            .Select(r =>
            {
                var sampleMeter = (r.Sample.MeterStart + r.Sample.MeterEnd) / 2.0;
                var meterWeight = Math.Max(0.35, 1.0 - Math.Min(1.0, Math.Abs(sampleMeter - meterCenter) / 12.0));
                return new KbExample(
                    Code: r.Sample.VsaCode,
                    Description: r.Sample.Beschreibung,
                    MeterStart: r.Sample.MeterStart,
                    MeterEnd: r.Sample.MeterEnd,
                    Score: r.Score * meterWeight);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static string BuildKnowledgeQuery(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request)
    {
        var parts = new List<string>
        {
            detection.FindingLabel,
            $"Meter {detection.MeterStart:0.00}-{detection.MeterEnd:0.00}",
            $"Severity {detection.Severity}",
            $"Haltung {request.HaltungId}"
        };

        if (!string.IsNullOrWhiteSpace(detection.PositionClock))
            parts.Add($"Uhrlage {detection.PositionClock}");

        if (detection.ExtentPercent is > 0)
            parts.Add($"Ausdehnung {detection.ExtentPercent}%");

        if (!string.IsNullOrWhiteSpace(detection.VsaCodeHint))
            parts.Add($"VisionCode {detection.VsaCodeHint}");

        if (detection.HeightMm is > 0)
            parts.Add($"HÃ¶he {detection.HeightMm}mm");
        if (detection.IntrusionPercent is > 0)
            parts.Add($"Einragung {detection.IntrusionPercent}%");
        if (detection.CrossSectionReductionPercent is > 0)
            parts.Add($"QV {detection.CrossSectionReductionPercent}%");

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private string BuildSystemPrompt()
    {
        var basePrompt = "Du bist ein Kanalinspektion-Experte nach DIN EN 13508-2 / VSA-DSS. " +
            "Mappe einen erkannten Befund auf den korrekten Schadenskode. " +
            "Antworte nur mit gÃ¼ltigem JSON.";

        return basePrompt;
    }

    private static ProtocolEntry BuildProtocolEntry(MappedProtocolEntry mapped)
    {
        var det = mapped.Detection;
        return new ProtocolEntry
        {
            EntryId = Guid.NewGuid(),
            Code = mapped.SuggestedCode ?? string.Empty,
            Beschreibung = det.FindingLabel,
            MeterStart = det.MeterStart,
            MeterEnd = det.MeterEnd > det.MeterStart ? det.MeterEnd : det.MeterStart,
            IsStreckenschaden = det.MeterEnd - det.MeterStart > 0.05,
            // KI-generierter Eintrag: als Ai kennzeichnen, nicht als Manual tarnen
            // (Herkunft/Filterung im Export bleibt nachvollziehbar).
            Source = ProtocolEntrySource.Ai,
            Ai = new ProtocolEntryAiMeta
            {
                SuggestedCode = mapped.SuggestedCode,
                Confidence = mapped.Confidence,
                Reason = mapped.Reason,
                Flags = mapped.Warnings.ToList(),
                SuggestedAt = DateTimeOffset.UtcNow
            }
        };
    }

    private sealed record KbExample(
        string Code,
        string Description,
        double MeterStart,
        double MeterEnd,
        double Score);

    private static ProtocolDocument BuildDocument(
        FullProtocolGenerationRequest request,
        List<ProtocolEntry> entries)
    {
        var revision = new ProtocolRevision
        {
            RevisionId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "KI (FullProtocolGeneration)",
            Comment = "Automatisch generiert aus Video-Analyse",
            Entries = entries
        };
        return new ProtocolDocument
        {
            HaltungId = request.HaltungId,
            Original = revision,
            Current = revision
        };
    }

    private static ProtocolDocument BuildEmptyDocument(FullProtocolGenerationRequest request)
    {
        var revision = new ProtocolRevision
        {
            RevisionId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "KI (FullProtocolGeneration)",
            Comment = "Keine SchÃ¤den erkannt",
            Entries = new List<ProtocolEntry>()
        };
        return new ProtocolDocument
        {
            HaltungId = request.HaltungId,
            Original = revision,
            Current = revision
        };
    }
}

// â”€â”€ DTOs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

