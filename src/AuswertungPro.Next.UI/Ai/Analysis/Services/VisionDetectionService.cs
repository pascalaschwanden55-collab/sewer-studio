// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai.Analysis.Services;

/// <summary>
/// Stufe 1 der Analyse-Pipeline: Frame-PNG → Ollama Vision → rohe Schadenbeschreibungen.
/// Verwendet qwen2.5vl (VisionModel aus OllamaConfig).
/// </summary>
public sealed class VisionDetectionService(OllamaClient client, OllamaConfig config)
{
    private static readonly JsonElement Schema = BuildSchema();

    private const string SystemPrompt =
        "Du bist ein Experte für Kanalinspektion nach VSA-Standard und EN 13508-2. " +
        "Analysiere den Kanalinspektions-Frame und beschreibe alle sichtbaren Schäden oder Anomalien " +
        "präzise auf Deutsch. Falls kein Schaden sichtbar ist, setze hasDefects=false.";

    /// <summary>
    /// Analysiert einen Frame und gibt rohe Schadenbeschreibungen zurück.
    /// Bei Fehler oder keinen Schäden → leere Liste.
    /// </summary>
    public async Task<IReadOnlyList<string>> DetectAsync(
        string framePath,
        double meterPosition,
        CancellationToken ct = default)
    {
        if (!File.Exists(framePath))
            return [];

        try
        {
            var base64 = await LoadBase64Async(framePath, ct).ConfigureAwait(false);
            if (base64 is null) return [];

            var userPrompt = $"Meter-Position: {meterPosition:F2} m. Analysiere diesen Frame.";
            OllamaClient.ChatMessage[] messages =
            [
                new("system", SystemPrompt),
                new("user",   userPrompt,  [base64])
            ];

            var dto = await client.ChatStructuredAsync<VisionDetectionDto>(
                config.VisionModel, messages, Schema, ct).ConfigureAwait(false);

            if (!dto.HasDefects || dto.Defects is null or { Count: 0 })
                return [];

            var results = new List<string>(dto.Defects.Count);
            foreach (var d in dto.Defects)
            {
                if (!string.IsNullOrWhiteSpace(d.Description))
                    results.Add(d.Description.Trim());
            }
            return results;
        }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    // ── Intern ────────────────────────────────────────────────────────────

    private static async Task<string?> LoadBase64Async(string path, CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            return Convert.ToBase64String(bytes);
        }
        catch { return null; }
    }

    private static JsonElement BuildSchema()
    {
        const string json = """
            {
              "type": "object",
              "required": ["hasDefects", "defects"],
              "properties": {
                "hasDefects": { "type": "boolean" },
                "defects": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["description"],
                    "properties": {
                      "description": { "type": "string" },
                      "severity":    { "type": "string" },
                      "location":    { "type": "string" }
                    }
                  }
                }
              }
            }
            """;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

// ── Interne DTOs ─────────────────────────────────────────────────────────

internal sealed class VisionDetectionDto
{
    public bool             HasDefects { get; init; }
    public List<VisionDefect>? Defects { get; init; }
}

internal sealed class VisionDefect
{
    public string Description { get; init; } = "";
    public string Severity    { get; init; } = "medium";
    public string Location    { get; init; } = "";
}
