using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Bestimmt den feinen Anschluss-Code (Bauart + offen/verschlossen) aus dem Bild ueber einen
/// fokussierten Qwen-Aufruf mit striktem 16-Codes-Enum-Schema. Reiner Zusatz: bei Fehler,
/// Zeitueberschreitung, "unsicher" oder unbekanntem Code werden leere Kandidaten geliefert.
/// Muster wie <see cref="EnhancedVisionAnalysisService"/>.
/// </summary>
public sealed class BcaFineCodeClassifier : IBcaFineCodeClassifier, IDisposable
{
    // Die 16 gueltigen BCA-Feincodes (Bauart A-G,Z je offen/verschlossen) plus "unsicher".
    private static readonly IReadOnlySet<string> ValidCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "BCAAA","BCAAB","BCABA","BCABB","BCACA","BCACB","BCADA","BCADB",
        "BCAEA","BCAEB","BCAFA","BCAFB","BCAGA","BCAGB","BCAZA","BCAZB",
    };

    private static readonly JsonElement Schema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "code": { "type": "string",
          "enum": ["BCAAA","BCAAB","BCABA","BCABB","BCACA","BCACB","BCADA","BCADB",
                   "BCAEA","BCAEB","BCAFA","BCAFB","BCAGA","BCAGB","BCAZA","BCAZB","unsicher"] },
        "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
        "is_uncertain": { "type": "boolean" }
      },
      "required": ["code", "is_uncertain"]
    }
    """).RootElement.Clone();

    private const string Prompt =
        "Du siehst das Bild eines seitlichen Rohranschlusses (VSA-Familie BCA). Bestimme die " +
        "Bauart und ob der Anschluss offen oder verschlossen ist. Antworte NUR mit einem der " +
        "Codes:\n" +
        "BCAAA Anschluss mit Formstueck; BCAAB dito verschlossen;\n" +
        "BCABA Sattelanschluss gebohrt; BCABB dito verschlossen;\n" +
        "BCACA Sattelanschluss eingespitzt; BCACB dito verschlossen;\n" +
        "BCADA Anschluss gebohrt; BCADB dito verschlossen;\n" +
        "BCAEA Anschluss eingespitzt; BCAEB dito verschlossen;\n" +
        "BCAFA Spezialanschluss; BCAFB dito verschlossen;\n" +
        "BCAGA Anschluss unbekannter Bauart; BCAGB dito verschlossen;\n" +
        "BCAZA andersartiger Anschluss; BCAZB dito verschlossen.\n" +
        "Wenn die Bauart nicht sicher erkennbar ist, setze code='unsicher' und is_uncertain=true.";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly OllamaClient _client;
    private readonly string _model;
    private readonly bool _ownsClient;

    public BcaFineCodeClassifier(OllamaClient client, string model)
        : this(client, model, ownsClient: false)
    {
    }

    /// <summary>
    /// Mit <paramref name="ownsClient"/> = true gibt <see cref="Dispose"/> den Qwen-Client frei
    /// (Pruefplatz-Verdrahtung baut einen eigenen). Bei false (Default) bleibt ein geteilter
    /// Client unberuehrt.
    /// </summary>
    public BcaFineCodeClassifier(OllamaClient client, string model, bool ownsClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _ownsClient = ownsClient;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    public async Task<BcaFineCodeSuggestion> SuggestAsync(
        string anschlussBildBase64, CancellationToken ct = default)
    {
        BcaFineCodeDto dto;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            dto = await _client.ChatStructuredWithOptionsAsync<BcaFineCodeDto>(
                model: _model,
                messages: [new OllamaClient.ChatMessage("user", Prompt, [anschlussBildBase64])],
                formatSchema: Schema,
                options: OllamaDeterministicOptions.Create(),
                ct: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return BcaFineCodeSuggestion.Uncertain;   // Zeitueberschreitung -> grober Code bleibt
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return BcaFineCodeSuggestion.Uncertain;   // Transport-/Modellfehler -> grober Code bleibt
        }

        var code = dto.Code?.Trim().ToUpperInvariant();
        if (dto.IsUncertain || code is null || !ValidCodes.Contains(code))
            return BcaFineCodeSuggestion.Uncertain;

        var confidence = Math.Clamp(dto.Confidence ?? 0.0, 0.0, 1.0);
        return new BcaFineCodeSuggestion(
            new[] { new BcaFineCodeCandidate(code, confidence) },
            IsUncertain: false);
    }

    private sealed record BcaFineCodeDto(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("confidence")] double? Confidence,
        [property: JsonPropertyName("is_uncertain")] bool IsUncertain);
}
