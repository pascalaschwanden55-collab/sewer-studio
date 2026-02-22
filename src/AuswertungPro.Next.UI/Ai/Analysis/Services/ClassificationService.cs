// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Analysis.Models;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Shared;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Analysis.Services;

/// <summary>
/// Stufe 2 der Analyse-Pipeline: Beschreibung + few-shot → VSA-Code + AnalysisObservation.
/// Verwendet qwen2.5:14b (TextModel) mit Retrieval-Augmentation aus der KnowledgeBase.
/// </summary>
public sealed class ClassificationService(
    OllamaClient client,
    OllamaConfig config,
    RetrievalService retrieval)
{
    private const int FewShotK = 3;
    private static readonly JsonElement Schema = BuildSchema();

    private const string SystemPrompt =
        "Du bist ein Experte für Kanalinspektion nach VSA-Standard (EN 13508-2). " +
        "Ordne die Schadenbeschreibung einem VSA-Code zu und erstelle den normativen Protokolltext. " +
        "Antworte AUSSCHLIESSLICH mit dem angegebenen JSON-Format. " +
        "Gültige VSA-Codes: BAA BAB BAC BAD BAE BAF BBA BBB BCA BCB BCC " +
        "BDA BDB BEA BEB BEC BFA BFB BFC BFD BGA BGB BHA BHB.";

    /// <summary>
    /// Klassifiziert eine rohe Schadenbeschreibung und gibt eine AnalysisObservation zurück.
    /// Gibt null zurück wenn kein gültiger VSA-Code ermittelt werden kann.
    /// </summary>
    public async Task<AnalysisObservation?> ClassifyAsync(
        string rawDescription,
        double meterPosition,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawDescription))
            return null;

        try
        {
            var examples = await retrieval
                .RetrieveAsync(rawDescription, FewShotK, ct)
                .ConfigureAwait(false);

            var userPrompt = BuildUserPrompt(rawDescription, meterPosition, examples);

            OllamaClient.ChatMessage[] messages =
            [
                new("system", SystemPrompt),
                new("user",   userPrompt)
            ];

            var dto = await client.ChatStructuredAsync<ClassificationDto>(
                config.TextModel, messages, Schema, ct).ConfigureAwait(false);

            return MapToObservation(dto, meterPosition);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    // ── Intern ────────────────────────────────────────────────────────────

    private static string BuildUserPrompt(
        string description,
        double meterPos,
        IReadOnlyList<RetrievalResult> examples)
    {
        var sb = new StringBuilder();

        if (examples.Count > 0)
        {
            sb.AppendLine("--- Ähnliche Beispiele ---");
            foreach (var ex in examples)
            {
                sb.AppendLine($"Beschreibung: {ex.Sample.Beschreibung}");
                sb.AppendLine($"VSA-Code: {ex.Sample.VsaCode}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("--- Neue Beobachtung ---");
        sb.AppendLine($"Meter-Position: {meterPos:F2} m");
        sb.AppendLine($"Beschreibung: {description}");
        return sb.ToString();
    }

    private static AnalysisObservation? MapToObservation(ClassificationDto dto, double meterPos)
    {
        if (string.IsNullOrWhiteSpace(dto.VsaCode))
            return null;

        if (!VsaCatalog.IsKnown(dto.VsaCode))
            return null;

        var info = VsaCatalog.Get(dto.VsaCode)!;

        QuantificationDetail? quant = null;
        if (dto.QuantValue.HasValue && !string.IsNullOrWhiteSpace(dto.QuantUnit))
        {
            quant = new QuantificationDetail
            {
                Value         = dto.QuantValue.Value,
                Unit          = dto.QuantUnit!,
                Type          = string.IsNullOrWhiteSpace(dto.QuantType) ? info.Label : dto.QuantType!,
                ClockPosition = string.IsNullOrWhiteSpace(dto.ClockPosition) ? null : dto.ClockPosition
            };
        }

        var meterStart = dto.MeterStart > 0 ? dto.MeterStart : meterPos;
        var meterEnd   = dto.MeterEnd > meterStart ? dto.MeterEnd : meterStart;

        return new AnalysisObservation
        {
            VsaCode           = dto.VsaCode.ToUpperInvariant(),
            Characterization  = string.IsNullOrWhiteSpace(dto.Characterization) ? null : dto.Characterization,
            Label             = string.IsNullOrWhiteSpace(dto.Label) ? info.Label : dto.Label,
            Text              = string.IsNullOrWhiteSpace(dto.Text)  ? info.Label : dto.Text,
            Quantification    = quant,
            Confidence        = new ObservationConfidence
            {
                Detection      = Math.Clamp(dto.DetectionConf,      0, 1),
                Classification = Math.Clamp(dto.ClassificationConf, 0, 1),
                Quantification = Math.Clamp(dto.QuantificationConf, 0, 1)
            },
            Evidence          = dto.Evidence ?? "",
            IsStreckenschaden = dto.IsStreckenschaden,
            MeterStart        = meterStart,
            MeterEnd          = meterEnd
        };
    }

    private static JsonElement BuildSchema()
    {
        const string json = """
            {
              "type": "object",
              "required": ["vsaCode", "label", "text", "meterStart", "meterEnd",
                           "detectionConf", "classificationConf", "quantificationConf"],
              "properties": {
                "vsaCode":            { "type": "string"  },
                "characterization":   { "type": "string"  },
                "label":              { "type": "string"  },
                "text":               { "type": "string"  },
                "meterStart":         { "type": "number"  },
                "meterEnd":           { "type": "number"  },
                "isStreckenschaden":  { "type": "boolean" },
                "quantValue":         { "type": "number"  },
                "quantUnit":          { "type": "string"  },
                "quantType":          { "type": "string"  },
                "clockPosition":      { "type": "string"  },
                "detectionConf":      { "type": "number"  },
                "classificationConf": { "type": "number"  },
                "quantificationConf": { "type": "number"  },
                "evidence":           { "type": "string"  }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

// ── Interne DTOs ─────────────────────────────────────────────────────────

internal sealed class ClassificationDto
{
    public string  VsaCode            { get; init; } = "";
    public string? Characterization   { get; init; }
    public string  Label              { get; init; } = "";
    public string  Text               { get; init; } = "";
    public double  MeterStart         { get; init; }
    public double  MeterEnd           { get; init; }
    public bool    IsStreckenschaden  { get; init; }
    public double? QuantValue         { get; init; }
    public string? QuantUnit          { get; init; }
    public string? QuantType          { get; init; }
    public string? ClockPosition      { get; init; }
    public double  DetectionConf      { get; init; }
    public double  ClassificationConf { get; init; }
    public double  QuantificationConf { get; init; }
    public string? Evidence           { get; init; }
}
