using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
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
    // Blinder KB-Validierungsabruf (unabhaengiger Zweitbeleg); null ohne Retrieval.
    private readonly KbBlindValidationService? _kbValidation;
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
        // Produktiv bewusst mit validierten Default-Gewichten starten. Experimentell
        // gelernte Gewichte bleiben bis zu einem getrennten Eval im Schattenbetrieb.
        _qualityGate = qualityGate ?? LearnedWeightsGateFactory.Create();

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
                BestEffort.ReportWarning($"[FullProtocolGen] KB-Init fehlgeschlagen: {ex.Message}");
                _retrieval = null;
            }
        }

        // Blinde KB-Validierung des LLM-Vorschlags (Fehlerpruefung 11.07., Kritisch 1):
        // eigener Abruf OHNE Code-Hinweis/Haltungs-ID, damit sich der Beleg nicht selbst
        // bestaetigt. Nur mit verfuegbarem Retrieval moeglich.
        _kbValidation = _retrieval is not null ? new KbBlindValidationService(_retrieval) : null;
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
                Warnings: new[] { "Keine Schäden erkannt." });
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
            $"Fertig – {protocolEntries.Count} Einträge gemappt."));

        return new FullProtocolGenerationResult(
            Document: BuildDocument(request, protocolEntries),
            MappedEntries: mappedEntries,
            Error: null,
            Warnings: warnings);
    }

    // â”€â”€ Private â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Zentrale Freigabe-Regel als ANZEIGE fuer jeden gemappten Befund (Review 11.07.,
    // Empfehlung 2): Urteil + Grund landen in den Warnings (-> Ai.Flags im Protokoll),
    // damit die Review sie sieht. Es wird weiterhin nichts automatisch uebernommen
    // oder verworfen — die Entscheidung bleibt beim Menschen.
    private readonly SelfImproving.AutoApprovalService _autoApproval = new();

    private MappedProtocolEntry MitZentralerFreigabe(MappedProtocolEntry entry)
    {
        var urteil = _autoApproval.Decide(entry);
        var hinweis = SelfImproving.AutoApprovalService.AlsHinweis(urteil);
        return entry with
        {
            Freigabe = urteil,
            EntryId = entry.EntryId == Guid.Empty ? Guid.NewGuid() : entry.EntryId,
            VisionModel = entry.VisionModel ?? _cfg.VisionModel,
            TextModel = entry.TextModel ?? _cfg.TextModel,
            QualityGateVersion = entry.QualityGateVersion ?? QualityGateService.PolicyVersion,
            Warnings = entry.Warnings.Append(hinweis).ToArray()
        };
    }

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
            // QualityGate MUSS auch im Fehler-/Fallback-Pfad laufen — sonst bleibt
            // QualityGateResult null und die UI zeigt stilles "Gelb" fuer einen nie
            // bewerteten Befund. Ohne LLM-Evidenz liefert der Gate korrekt Rot. (Audit R3)
            if (kbExamples.Count > 0)
            {
                var fallback = kbExamples[0];
                var fbEvidence = (detection.Evidence ?? new EvidenceVector()) with
                {
                    KbSimilarity = fallback.Score,
                    DamageCategory = fallback.Code
                };
                return MitZentralerFreigabe(new MappedProtocolEntry(
                    Detection: detection with { Evidence = fbEvidence },
                    SuggestedCode: fallback.Code,
                    Confidence: Math.Clamp(fallback.Score, 0.35, 0.85),
                    Reason: "LLM-Fehler, KB-Fallback verwendet: " + ex.Message,
                    Warnings: new[] { "Code-Mapping fehlgeschlagen, KB-Fallback verwendet." },
                    QualityGateResult: _qualityGate.Evaluate(fbEvidence)));
            }

            var emptyEvidence = detection.Evidence ?? new EvidenceVector();
            return MitZentralerFreigabe(new MappedProtocolEntry(
                Detection: detection,
                SuggestedCode: null,
                Confidence: 0,
                Reason: ex.Message,
                Warnings: new[] { "Code-Mapping fehlgeschlagen: " + ex.Message },
                QualityGateResult: _qualityGate.Evaluate(emptyEvidence)));
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
            warnings.Add("LLM lieferte keinen gültigen Code, KB-Fallback verwendet.");
        }

        // â”€â”€ QualityGate: build EvidenceVector and evaluate â”€â”€
        var kbTopScore = kbExamples.Count > 0 ? kbExamples[0].Score : (double?)null;

        // BLINDE Validierung statt Vergleich mit den Prompt-Beispielen (Kritisch 1):
        // Die kbExamples wurden mit Code-Hinweis+Haltungs-ID gesucht und haben das LLM
        // beeinflusst — ein Treffer dort ist KEIN unabhaengiger Beleg. Der blinde Abruf
        // sucht ohne Code/Haltung und zaehlt nur fremde, menschlich bestaetigte Gold-Faelle.
        bool? kbAgreement = null;
        if (_kbValidation is not null && !string.IsNullOrWhiteSpace(suggestedCode))
        {
            try
            {
                var validation = await _kbValidation
                    .ValidateAsync(detection, request.HaltungId, suggestedCode!, ct)
                    .ConfigureAwait(false);
                kbAgreement = validation.Agrees;
                if (!validation.Agrees)
                    warnings.Add($"KB-Validierung: {validation.Reason}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                warnings.Add($"KB-Validierung fehlgeschlagen: {ex.Message}");
            }
        }

        var evidence = detection.Evidence ?? new EvidenceVector();
        evidence = evidence with
        {
            LlmCodeConf = confidence,
            KbSimilarity = kbTopScore,
            KbCodeAgreement = kbAgreement,
            // Achtung: derselbe Ursprung wie LlmCodeConf (dieselbe Pruefung derselben
            // Sprachmodell-Antwort) und KbSimilarity (die Prompt-Beispiele). Alle drei
            // gelten daher als EINE Belegquelle — siehe EvidenceSourceGrouping.
            // Fuer die Ampel zaehlen sie zusammen nur einmal; im Zahlenwert behalten sie
            // bewusst ihr bisheriges Gewicht.
            PlausibilityScore = checked_.Confidence,
            DamageCategory = suggestedCode
        };

        var qgResult = _qualityGate.Evaluate(evidence);
        var compositeConfidence = qgResult.CompositeConfidence;

        // Update detection with enriched evidence
        var enrichedDetection = detection with { Evidence = evidence };

        return MitZentralerFreigabe(new MappedProtocolEntry(
            Detection: enrichedDetection,
            SuggestedCode: suggestedCode,
            Confidence: compositeConfidence,
            Reason: reason,
            Warnings: warnings,
            QualityGateResult: qgResult));
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

    private string BuildSystemPrompt()
    {
        var basePrompt = "Du bist ein Kanalinspektion-Experte nach DIN EN 13508-2 / VSA-DSS. " +
            "Mappe einen erkannten Befund auf den korrekten Schadenskode. " +
            "Antworte nur mit gültigem JSON.";

        return basePrompt;
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
            Original = ProtocolRevisionCloner.CloneRevision(revision, "KI (FullProtocolGeneration)", "Original aus Video-Analyse"),
            Current = ProtocolRevisionCloner.CloneRevision(revision, "KI (FullProtocolGeneration)", "Automatisch generiert aus Video-Analyse")
        };
    }

    private static ProtocolDocument BuildEmptyDocument(FullProtocolGenerationRequest request)
    {
        var revision = new ProtocolRevision
        {
            RevisionId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "KI (FullProtocolGeneration)",
            Comment = "Keine Schäden erkannt",
            Entries = new List<ProtocolEntry>()
        };
        return new ProtocolDocument
        {
            HaltungId = request.HaltungId,
            Original = ProtocolRevisionCloner.CloneRevision(revision, "KI (FullProtocolGeneration)", "Original aus Video-Analyse"),
            Current = ProtocolRevisionCloner.CloneRevision(revision, "KI (FullProtocolGeneration)", "Keine Schaeden erkannt")
        };
    }
}

// â”€â”€ DTOs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

