using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Pallon-ähnlicher Workflow: Detections -> vollständiges ProtocolDocument
///
/// BUG 1.3 FIX: GenerateAsync analysiert das Video NICHT mehr selbst.
/// Stattdessen akzeptiert GenerateFromDetectionsAsync bereits analysierte
/// RawVideoDetections. VideoAnalysisPipelineService übergibt diese.
/// Damit wird jede Video-Analyse nur noch einmal durchgeführt.
/// </summary>
public sealed class FullProtocolGenerationService
{
    private readonly OllamaClient _client;
    private readonly IAiSuggestionPlausibilityService _plausibility;
    private readonly AiRuntimeConfig _cfg;

    public FullProtocolGenerationService(
        AiRuntimeConfig cfg,
        IAiSuggestionPlausibilityService plausibility,
        HttpClient httpClient)
    {
        _cfg = cfg;
        _plausibility = plausibility;
        _client = new OllamaClient(cfg.OllamaBaseUri, httpClient);
    }

    // ── BUG 1.3 FIX: Nimmt bereits analysierte Detections entgegen ───────────

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

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task<MappedProtocolEntry> MapDetectionAsync(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request,
        CancellationToken ct)
    {
        // Wenn EnhancedVision bereits einen Code-Hinweis geliefert hat,
        // diesen im Prompt priorisieren → spart LLM-Aufwand
        var vsaHint = !string.IsNullOrWhiteSpace(detection.VsaCodeHint)
            ? $"\nVision-Code-Hinweis (priorisiere falls plausibel): {detection.VsaCodeHint}"
            : string.Empty;

        var prompt = BuildPrompt(detection, request, vsaHint);

        AiSuggestionResultDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<AiSuggestionResultDto>(
                model: _cfg.TextModel,
                messages: new[]
                {
                    new OllamaClient.ChatMessage("system", BuildSystemPrompt()),
                    new OllamaClient.ChatMessage("user", prompt)
                },
                formatSchema: AiSuggestionSchemas.AiSuggestionResultSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
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

        return new MappedProtocolEntry(
            Detection: detection,
            SuggestedCode: checked_.SuggestedCode,
            Confidence: checked_.Confidence,
            Reason: checked_.Rationale,
            Warnings: checked_.Warnings ?? Array.Empty<string>());
    }

    private static string BuildPrompt(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request,
        string vsaHint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Antworte ausschließlich im vorgegebenen JSON-Schema.");
        sb.AppendLine("Wähle nur EINEN Basiscode aus 'Erlaubte Codes'.");
        sb.AppendLine();
        sb.AppendLine($"Befund: {detection.FindingLabel}");
        sb.AppendLine($"Schweregrad: {detection.Severity}");
        sb.AppendLine($"Meterbereich: {detection.MeterStart:0.00}m – {detection.MeterEnd:0.00}m");
        sb.AppendLine($"Haltung: {request.HaltungId}");
        if (!string.IsNullOrWhiteSpace(vsaHint))
            sb.AppendLine(vsaHint);
        sb.AppendLine();
        sb.AppendLine($"Erlaubte Codes: {string.Join(", ", request.AllowedCodes)}");
        sb.AppendLine("Erzeuge: suggestedCode, confidence (0.0-1.0), rationale, evidence, warnings.");
        return sb.ToString();
    }

    private static string BuildSystemPrompt() =>
        "Du bist ein Kanalinspektion-Experte nach DIN EN 13508-2 / VSA-DSS. " +
        "Mappe einen erkannten Befund auf den korrekten Schadenskode. " +
        "Antworte nur mit gültigem JSON.";

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
            Source = ProtocolEntrySource.Manual,
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
            Comment = "Keine Schäden erkannt",
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

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record FullProtocolGenerationRequest(
    string HaltungId,
    string VideoPath,
    IReadOnlyList<string> AllowedCodes,
    string? ProjectFolderAbs = null,
    string? RequestedBy = null
);

public sealed record FullProtocolGenerationResult(
    ProtocolDocument? Document,
    IReadOnlyList<MappedProtocolEntry> MappedEntries,
    string? Error,
    IReadOnlyList<string> Warnings)
{
    public bool IsSuccess => Error is null;

    public static FullProtocolGenerationResult Failed(string error) =>
        new(null, Array.Empty<MappedProtocolEntry>(), error, Array.Empty<string>());
}

public sealed record MappedProtocolEntry(
    RawVideoDetection Detection,
    string? SuggestedCode,
    double Confidence,
    string? Reason,
    IReadOnlyList<string> Warnings
);

public sealed record CodeMappingProgress(int Done, int Total, string Status)
{
    public double Percent => Total > 0 ? (double)Done / Total * 100.0 : 0;
}
