using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Workflow: Detections -> vollstaendiges ProtocolDocument.
/// GenerateFromDetectionsAsync verarbeitet bereits analysierte RawVideoDetections;
/// eine Video-Analyse wird dadurch nur einmal ausgefuehrt.
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
        // Die Instanz uebernimmt den beim Programmstart aktivierten Weight-Snapshot.
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
                Warnings: new[] { "Keine Schaeden erkannt." });
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
            $"Fertig – {protocolEntries.Count} Eintraege gemappt."));

        return new FullProtocolGenerationResult(
            Document: BuildDocument(request, protocolEntries),
            MappedEntries: mappedEntries,
            Error: null,
            Warnings: warnings);
    }

    private async Task<MappedProtocolEntry> MapDetectionAsync(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request,
        CancellationToken ct)
    {
        var kbExamples = await GetKnowledgeExamplesAsync(detection, request, ct).ConfigureAwait(false);

        // Wenn EnhancedVision bereits einen Code-Hinweis geliefert hat,
        // diesen im Prompt priorisieren -> spart LLM-Aufwand.
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Auch der KB-only-Fehlerpfad durchlaeuft Plausibilitaet, Quality Gate
            // und Unsicherheit. Sein Confidence-Deckel liegt bewusst unter jeder
            // Auto-Freigabegrenze.
            if (kbExamples.Count > 0)
            {
                return KbFallbackMappingPolicy.Build(
                    detection,
                    kbExamples[0],
                    ex.Message,
                    _plausibility,
                    _qualityGate);
            }

            var emptyEvidence = detection.Evidence ?? new EvidenceVector();
            var emptyGate = _qualityGate.Evaluate(emptyEvidence);
            return new MappedProtocolEntry(
                Detection: detection with { Evidence = emptyEvidence },
                SuggestedCode: null,
                Confidence: 0,
                Reason: ex.Message,
                Warnings: new[] { "Code-Mapping fehlgeschlagen: " + ex.Message },
                QualityGateResult: emptyGate,
                Uncertainty: UncertaintyEstimate.FromSinglePass(0));
        }

        var checkedSuggestion = _plausibility.ApplyChecks(
            dto.ToDomain(),
            new ObservationContext(detection.FindingLabel));

        var suggestedCode = checkedSuggestion.SuggestedCode;
        var confidence = checkedSuggestion.Confidence;
        var reason = checkedSuggestion.Rationale;
        var warnings = checkedSuggestion.Warnings?.ToList() ?? new List<string>();

        if (string.IsNullOrWhiteSpace(suggestedCode) && kbExamples.Count > 0)
        {
            var fallback = KbFallbackMappingPolicy.Build(
                detection,
                kbExamples[0],
                "LLM lieferte keinen gueltigen Code.",
                _plausibility,
                _qualityGate);

            warnings.AddRange(fallback.Warnings);
            return fallback with
            {
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? fallback.Reason
                    : $"{reason} | {fallback.Reason}",
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        // QualityGate: build EvidenceVector and evaluate.
        var kbTopScore = kbExamples.Count > 0 ? kbExamples[0].Score : (double?)null;
        var kbAgrees = kbExamples.Count > 0 && !string.IsNullOrWhiteSpace(suggestedCode) &&
            kbExamples[0].Code.Equals(suggestedCode, StringComparison.OrdinalIgnoreCase);

        var evidence = detection.Evidence ?? new EvidenceVector();
        evidence = evidence with
        {
            LlmCodeConf = confidence,
            KbSimilarity = kbTopScore,
            KbCodeAgreement = kbExamples.Count > 0 ? kbAgrees : null,
            PlausibilityScore = checkedSuggestion.Confidence,
            DamageCategory = suggestedCode
        };

        var qgResult = _qualityGate.Evaluate(evidence);
        var compositeConfidence = qgResult.CompositeConfidence;
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
        => ProtocolEntryFactory.BuildPrompt(detection, request, vsaHint, kbExamples);

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
                var meterWeight = KbMeterWeighting.Weight(meterCenter, sampleMeter);
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
        => ProtocolEntryFactory.BuildKnowledgeQuery(detection, request);

    private static string BuildSystemPrompt()
    {
        return "Du bist ein Kanalinspektion-Experte nach DIN EN 13508-2 / VSA-DSS. " +
               "Mappe einen erkannten Befund auf den korrekten Schadenskode. " +
               "Antworte nur mit gueltigem JSON.";
    }

    private static ProtocolEntry BuildProtocolEntry(MappedProtocolEntry mapped)
        => ProtocolEntryFactory.BuildProtocolEntry(mapped);

    private static ProtocolEntryCodeMeta? BuildCodeMeta(MappedProtocolEntry mapped)
        => ProtocolEntryFactory.BuildCodeMeta(mapped);

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
            Original = ProtocolRevisionCloner.CloneRevision(
                revision,
                "KI (FullProtocolGeneration)",
                "Original aus Video-Analyse"),
            Current = ProtocolRevisionCloner.CloneRevision(
                revision,
                "KI (FullProtocolGeneration)",
                "Automatisch generiert aus Video-Analyse")
        };
    }

    private static ProtocolDocument BuildEmptyDocument(FullProtocolGenerationRequest request)
    {
        var revision = new ProtocolRevision
        {
            RevisionId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "KI (FullProtocolGeneration)",
            Comment = "Keine Schaeden erkannt",
            Entries = new List<ProtocolEntry>()
        };
        return new ProtocolDocument
        {
            HaltungId = request.HaltungId,
            Original = ProtocolRevisionCloner.CloneRevision(
                revision,
                "KI (FullProtocolGeneration)",
                "Original aus Video-Analyse"),
            Current = ProtocolRevisionCloner.CloneRevision(
                revision,
                "KI (FullProtocolGeneration)",
                "Keine Schaeden erkannt")
        };
    }
}
