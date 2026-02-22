using System.Net.Http;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.UI.Ai.Sanierung.Dto;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

public sealed class AiSanierungOptimizationService
{
    private readonly AiRuntimeConfig _cfg;
    private readonly OllamaClient _client;
    private readonly SanierungValidationService _validation = new();
    private readonly CostOptimizationEngine _costEngine = new();

    private static readonly JsonElement AiSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "required": ["recommended_measure","confidence","cost_adjustment_factor","reasoning","risk_flag","risk_message"],
          "properties": {
            "recommended_measure":    { "type": "string" },
            "confidence":             { "type": "number" },
            "cost_adjustment_factor": { "type": "number" },
            "reasoning":              { "type": "string" },
            "risk_flag":              { "type": "boolean" },
            "risk_message":           { "type": "string" }
          }
        }
        """).RootElement;

    public AiSanierungOptimizationService(AiRuntimeConfig cfg, HttpClient? http = null)
    {
        _cfg    = cfg;
        _client = new OllamaClient(cfg.OllamaBaseUri, http);
    }

    public async Task<SanierungOptimizationResult> OptimizeAsync(
        SanierungOptimizationRequest req,
        CancellationToken ct)
    {
        // 1. Extract constraints (§8)
        var zustandsklasse = "";  // populated by caller via Findings.SeverityClass if available
        if (req.Findings.Count > 0)
            zustandsklasse = req.Findings.FirstOrDefault(f => f.SeverityClass != null)?.SeverityClass ?? "";

        var constraints = _validation.ExtractConstraints(req.Findings, zustandsklasse);

        // 2. Rule recommendation summary
        var ruleSummary = BuildRuleSummary(req.Rule);
        var allowedMeasures = BuildAllowedMeasures(req.Rule, constraints);

        // 3. Build prompt
        var systemPrompt = BuildSystemPrompt();
        var userPrompt   = BuildUserPrompt(req, ruleSummary, allowedMeasures, constraints);

        // 4. Call Ollama with retry
        SanierungAiDto? aiDto = null;
        try
        {
            aiDto = await _client.ChatStructuredAsync<SanierungAiDto>(
                _cfg.TextModel,
                [
                    new OllamaClient.ChatMessage("system", systemPrompt),
                    new OllamaClient.ChatMessage("user",   userPrompt)
                ],
                AiSchema,
                ct).ConfigureAwait(false);
        }
        catch
        {
            // Retry once with repair hint
            try
            {
                var repairPrompt = userPrompt + "\n\nANTWORTE AUSSCHLIESSLICH im angegebenen JSON-Format. Kein Text ausserhalb.";
                aiDto = await _client.ChatStructuredAsync<SanierungAiDto>(
                    _cfg.TextModel,
                    [
                        new OllamaClient.ChatMessage("system", systemPrompt),
                        new OllamaClient.ChatMessage("user",   repairPrompt)
                    ],
                    AiSchema,
                    ct).ConfigureAwait(false);
            }
            catch (Exception ex2)
            {
                // Fallback to rule recommendation
                return BuildFallbackResult(req, constraints, "KI nicht erreichbar: " + ex2.Message);
            }
        }

        if (aiDto is null)
            return BuildFallbackResult(req, constraints, "KI-Antwort konnte nicht verarbeitet werden");

        // 5. §8 Validate AI measure
        var validation = _validation.Validate(aiDto.recommended_measure, constraints);
        bool isFallback = false;
        string recommendedMeasure = aiDto.recommended_measure;
        var riskFlags = new List<string>(validation.RiskFlags);

        if (!validation.Passed)
        {
            // Use first rule measure as fallback
            recommendedMeasure = req.Rule?.Measures.FirstOrDefault() ?? aiDto.recommended_measure;
            isFallback         = true;
            riskFlags.Insert(0, "KI-Vorschlag wurde wegen §8-Verletzung verworfen und durch Regelempfehlung ersetzt");
        }

        if (aiDto.risk_flag && !string.IsNullOrWhiteSpace(aiDto.risk_message))
            riskFlags.Add(aiDto.risk_message);

        // 6. Cost calculation
        var costBand = _costEngine.Calculate(new CostCalcInput
        {
            Measure           = recommendedMeasure,
            DiameterMm        = req.Pipe.DiameterMm,
            LengthMeter       = req.Pipe.LengthMeter,
            DepthM            = req.Pipe.DepthM,
            Access            = req.Pipe.Access,
            Material          = req.Pipe.Material,
            RegionFactor      = req.Cost.RegionFactor,
            AiCorrectionFactor = Math.Clamp(aiDto.cost_adjustment_factor, 0.5, 2.0)
        });

        // 7. Build result
        var signals = req.Rule is not null ? "rules+ai" : "ai";
        if (req.SimilarCases is { Count: > 0 }) signals += "+similarity";

        return new SanierungOptimizationResult
        {
            RecommendedMeasure = recommendedMeasure,
            Confidence         = Math.Clamp(aiDto.confidence, 0.0, 1.0),
            CostEstimate       = costBand,
            Reasoning          = aiDto.reasoning ?? "",
            RiskFlags          = riskFlags,
            UsedSignals        = signals,
            IsFallback         = isFallback
        };
    }

    private static string BuildSystemPrompt() =>
        "Du bist ein Experte für Kanalsanierung nach VSA-Standard (Schweiz). " +
        "Analysiere die Schadensbefunde und empfehle die optimale Sanierungsmassnahme. " +
        "Berücksichtige die Rohrkennwerte, Einschränkungen und Regelempfehlungen. " +
        "Antworte AUSSCHLIESSLICH im angegebenen JSON-Format.";

    private static string BuildUserPrompt(
        SanierungOptimizationRequest req,
        string ruleSummary,
        IReadOnlyList<string> allowedMeasures,
        IReadOnlyList<MeasureConstraint> constraints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Sanierungsoptimierung");
        sb.AppendLine($"Haltung: {req.HaltungId}");
        sb.AppendLine();

        sb.AppendLine("### Schadensbefunde:");
        foreach (var f in req.Findings)
        {
            var pos = f.PositionMeter.HasValue ? $" @{f.PositionMeter:F1}m" : "";
            var qty = f.LengthMeter.HasValue ? $" L={f.LengthMeter:F1}m" : "";
            var sv  = f.SeverityClass is not null ? $" [{f.SeverityClass}]" : "";
            sb.AppendLine($"  - {f.Code}{pos}{qty}{sv}{(f.Comment is not null ? " – " + f.Comment : "")}");
        }

        sb.AppendLine();
        sb.AppendLine("### Rohrkontext:");
        sb.AppendLine($"  DN: {req.Pipe.DiameterMm?.ToString() ?? "unbekannt"} mm");
        sb.AppendLine($"  Material: {req.Pipe.Material ?? "unbekannt"}");
        sb.AppendLine($"  Länge: {req.Pipe.LengthMeter?.ToString("F1") ?? "unbekannt"} m");
        sb.AppendLine($"  Tiefe: {req.Pipe.DepthM?.ToString("F1") ?? "unbekannt"} m");
        sb.AppendLine($"  Zugang: {req.Pipe.Access}");

        if (!string.IsNullOrWhiteSpace(ruleSummary))
        {
            sb.AppendLine();
            sb.AppendLine("### Regelbasierte Empfehlung:");
            sb.AppendLine(ruleSummary);
        }

        if (allowedMeasures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Zulässige Massnahmen (beachte Einschränkungen):");
            foreach (var m in allowedMeasures)
                sb.AppendLine($"  - {m}");
        }

        if (constraints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Harte Einschränkungen (§8 – dürfen NICHT verletzt werden):");
            foreach (var c in constraints)
                sb.AppendLine($"  - {c}");
        }

        sb.AppendLine();
        sb.AppendLine("Gib die optimale Massnahme an. Wähle aus den zulässigen Massnahmen oder begründe eine Alternative.");

        return sb.ToString();
    }

    private static string BuildRuleSummary(RuleRecommendationDto? rule)
    {
        if (rule is null || rule.Measures.Count == 0)
            return "";
        var measures = string.Join(", ", rule.Measures);
        var cost     = rule.EstimatedCost.HasValue ? $" (geschätzte Kosten: {rule.EstimatedCost.Value:0} CHF)" : "";
        return $"Massnahmen: {measures}{cost}";
    }

    private static IReadOnlyList<string> BuildAllowedMeasures(
        RuleRecommendationDto? rule,
        IReadOnlyList<MeasureConstraint> constraints)
    {
        if (rule is null || rule.Measures.Count == 0)
            return ["Schlauchliner", "Kurzliner", "Reparatur", "Erneuerung Neubau"];

        var allowed = rule.Measures.ToList();

        if (constraints.Contains(MeasureConstraint.NoLinerAllowed))
            allowed.RemoveAll(m =>
                m.Contains("Liner",   StringComparison.OrdinalIgnoreCase) ||
                m.Contains("Inliner", StringComparison.OrdinalIgnoreCase));

        if (constraints.Contains(MeasureConstraint.NoFullReplacement))
            allowed.RemoveAll(m =>
                m.Contains("Erneuerung", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("Neubau",     StringComparison.OrdinalIgnoreCase));

        return allowed.Count > 0 ? allowed : rule.Measures;
    }

    private SanierungOptimizationResult BuildFallbackResult(
        SanierungOptimizationRequest req,
        IReadOnlyList<MeasureConstraint> constraints,
        string error)
    {
        var fallbackMeasure = req.Rule?.Measures.FirstOrDefault() ?? "Reparatur";

        var validation = _validation.Validate(fallbackMeasure, constraints);
        var costBand   = _costEngine.Calculate(new CostCalcInput
        {
            Measure     = fallbackMeasure,
            DiameterMm  = req.Pipe.DiameterMm,
            LengthMeter = req.Pipe.LengthMeter,
            DepthM      = req.Pipe.DepthM,
            Access      = req.Pipe.Access,
            Material    = req.Pipe.Material,
            RegionFactor = req.Cost.RegionFactor
        });

        return new SanierungOptimizationResult
        {
            RecommendedMeasure = fallbackMeasure,
            Confidence         = 0.0,
            CostEstimate       = costBand,
            Reasoning          = "Regelbasierter Fallback (KI nicht verfügbar)",
            RiskFlags          = validation.RiskFlags,
            UsedSignals        = "rules",
            IsFallback         = true,
            Error              = error
        };
    }
}

// Internal AI response DTO
internal sealed class SanierungAiDto
{
    public string recommended_measure    { get; init; } = "";
    public double confidence             { get; init; }
    public double cost_adjustment_factor { get; init; } = 1.0;
    public string reasoning              { get; init; } = "";
    public bool   risk_flag              { get; init; }
    public string risk_message           { get; init; } = "";
}
