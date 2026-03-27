using System;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Minimale AI-Typen im UI-Projekt, damit die Video-Pipeline unabhängig von anderen Layern kompiliert.
/// (Falls du später echte Domain/Application-Typen hast, kannst du diese Datei ersetzen.)
/// </summary>
public sealed record ObservationContext(
    string Observation,
    IReadOnlySet<string>? AlreadyConfirmedCodes = null);

public sealed record AiSuggestionResult(
    string? SuggestedCode,
    double Confidence,
    string? Rationale,
    string? Evidence,
    string[]? Warnings);

public interface IAiSuggestionPlausibilityService
{
    AiSuggestionResult ApplyChecks(AiSuggestionResult suggestion, ObservationContext context);
}


/// <summary>
/// DTO für structured output aus dem LLM.
/// </summary>
public sealed class AiSuggestionResultDto
{
    public string? suggestedCode { get; set; }
    public double confidence { get; set; }
    public string? rationale { get; set; }
    public string? evidence { get; set; }
    public string[]? warnings { get; set; }

    public AiSuggestionResult ToDomain()
        => new(
            SuggestedCode: suggestedCode,
            Confidence: confidence,
            Rationale: rationale,
            Evidence: evidence,
            Warnings: warnings);
}

public static class AiSuggestionSchemas
{
    // JSON Schema für AiSuggestionResultDto (Ollama /api/chat format)
    public static readonly JsonElement AiSuggestionResultSchema = JsonDocument.Parse("""
    {
      "type":"object",
      "additionalProperties": false,
      "properties": {
        "suggestedCode": { "type": ["string","null"], "description": "VSA/EN Code Vorschlag" },
        "confidence": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "rationale": { "type": ["string","null"] },
        "evidence": { "type": ["string","null"] },
        "warnings": { "type": ["array","null"], "items": { "type":"string" } }
      },
      "required": ["suggestedCode","confidence","rationale"]
    }
    """).RootElement.Clone();
}
