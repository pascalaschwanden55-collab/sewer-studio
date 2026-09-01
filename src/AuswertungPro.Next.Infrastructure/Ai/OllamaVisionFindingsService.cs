using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai;

public sealed record FrameFinding(
    double? Meter,
    IReadOnlyList<string> Findings,
    string Severity,
    string? Raw
);

public sealed class OllamaVisionFindingsService
{
    private static readonly JsonElement ResponseSchema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "meter": { "type": ["number", "null"] },
        "findings": {
          "type": "array",
          "items": { "type": "string" }
        },
        "severity": {
          "type": "string",
          "enum": ["low", "mid", "high"]
        }
      },
      "required": ["meter", "findings", "severity"]
    }
    """).RootElement.Clone();

    private static readonly IReadOnlySet<string> ValidSeverities =
        new HashSet<string>(StringComparer.Ordinal) { "low", "mid", "high" };

    private readonly OllamaClient _client;
    private readonly string _model;

    public OllamaVisionFindingsService(OllamaClient client, string model)
    {
        _client = client;
        _model = model;
    }

    public async Task<FrameFinding> AnalyzeAsync(string framePngBase64, CancellationToken ct)
    {
        var prompt =
            "Du analysierst ein Kanal-TV Frame (Kanalinspektion).\n" +
            "Erkenne nur sichtbare Schäden/Anomalien: Riss, Infiltration, Wurzeleinwuchs, Ablagerung, Versatz, Korrosion, Einragung, Fremdkörper, Scherben, Einbruch, Deformation, offene Stösse.\n" +
            "Lies den Meterstand aus dem Bild, falls sichtbar (z.B. 18.40 m).\n" +
            "Gib AUSSCHLIESSLICH gültiges JSON zurück (keine Erklärung):\n" +
            "{\n" +
            "  \"meter\": 18.4 | null,\n" +
            "  \"findings\": [\"Riss\", \"Infiltration\"],\n" +
            "  \"severity\": \"low\"|\"mid\"|\"high\"\n" +
            "}\n" +
            "Wenn nichts erkennbar: findings=[], severity=\"low\".";

        try
        {
            var dto = await _client.ChatStructuredWithOptionsAsync<FrameFindingDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: ResponseSchema,
                options: OllamaDeterministicOptions.Create(),
                ct: ct).ConfigureAwait(false);

            var raw = JsonSerializer.Serialize(dto);
            if (dto.Findings is null ||
                string.IsNullOrWhiteSpace(dto.Severity) ||
                !ValidSeverities.Contains(dto.Severity))
            {
                return Empty(raw);
            }

            return new FrameFinding(dto.Meter, dto.Findings, dto.Severity, raw);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException)
        {
            return Empty(raw: null);
        }
    }

    private static FrameFinding Empty(string? raw)
        => new(null, Array.Empty<string>(), "low", raw);

    private sealed record FrameFindingDto(
        [property: JsonPropertyName("meter")] double? Meter,
        [property: JsonPropertyName("findings")] IReadOnlyList<string>? Findings,
        [property: JsonPropertyName("severity")] string? Severity);

}
