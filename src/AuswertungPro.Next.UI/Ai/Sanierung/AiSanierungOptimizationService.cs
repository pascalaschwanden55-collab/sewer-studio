using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Sanierung;

namespace AuswertungPro.Next.UI.Ai.Sanierung;

public sealed class AiSanierungOptimizationService : IAiSanierungOptimizationService
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
        _client = cfg.CreateOllamaClient(http);
    }

    public async Task<SanierungOptimizationResult> OptimizeAsync(
        SanierungOptimizationRequest req,
        CancellationToken ct)
    {
        // 0. Input validation
        if (req.Findings.Count == 0)
        {
            var emptyConstraints = _validation.ExtractConstraints(
                req.Findings, "",
                req.Pipe.Groundwater ?? false,
                req.Pipe.DiameterMm);
            return BuildFallbackResult(req, emptyConstraints,
                "Keine Schadensbefunde vorhanden – regelbasierter Fallback aktiv");
        }

        // 1. Extract constraints (§8)
        // Hoechste Zustandsklasse verwenden (Z5 > Z4 > ... > Z1).
        // FirstOrDefault wuerde bei mehreren Findings den ersten nicht-leeren Wert
        // nehmen, der oft Z1/Z2 ist — damit greifen §8-Constraints nicht korrekt.
        var zustandsklasse = req.Findings
            .Select(f => f.SeverityClass)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderByDescending(s => s, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? "";

        var constraints = _validation.ExtractConstraints(
            req.Findings, zustandsklasse,
            req.Pipe.Groundwater ?? false,
            req.Pipe.DiameterMm);

        // 2. Rule recommendation summary
        var ruleSummary = BuildRuleSummary(req.Rule);
        var allowedMeasures = BuildAllowedMeasures(req.Rule, constraints, req.RulesFilter);

        // 3. Build prompt
        var systemPrompt = BuildSystemPrompt();
        var userPrompt   = BuildUserPrompt(req, ruleSummary, allowedMeasures, constraints);

        // 4. Call Ollama with retry
        SanierungAiDto? aiDto = null;
        string? firstError = null;
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex1)
        {
            firstError = ex1.Message;
            System.Diagnostics.Debug.WriteLine($"[Sanierung-KI] Erster Versuch fehlgeschlagen: {ex1.Message}");

            // Kurze Pause vor Retry (Backoff)
            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);

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
            catch (OperationCanceledException) { throw; }
            catch (Exception ex2)
            {
                System.Diagnostics.Debug.WriteLine($"[Sanierung-KI] Zweiter Versuch fehlgeschlagen: {ex2.Message}");
                return BuildFallbackResult(req, constraints,
                    $"KI nicht erreichbar (1: {firstError} | 2: {ex2.Message})");
            }
        }

        if (aiDto is null)
            return BuildFallbackResult(req, constraints, "KI-Antwort konnte nicht verarbeitet werden");

        // 5a. Hard-Constraint-Block-Check ZUERST (vor §8): Wenn KI ein per RulesFilter
        //     ausgeschlossenes Verfahren empfiehlt -> sofort verwerfen.
        //     Dies ist die deterministische Erzwingung der Hard-Constraints (RulesEngine).
        bool isFallback = false;
        string recommendedMeasure = aiDto.recommended_measure;
        var riskFlags = new List<string>();

        if (req.RulesFilter is { ExcludedProcedures.Count: > 0 } rfBlock)
        {
            var blockedProc = MatchExcludedProcedure(recommendedMeasure, rfBlock.ExcludedProcedures);
            if (blockedProc is not null)
            {
                // KI hat trotz Hinweis im Prompt ein ausgeschlossenes Verfahren empfohlen.
                // -> Auf erste Eligible aus RulesFilter wechseln, sonst erste rule.Measures.
                var safeFallback = PickSafeFallbackFromRulesFilter(req.RulesFilter, req.Rule)
                    ?? req.Rule?.Measures.FirstOrDefault();
                if (safeFallback is not null)
                {
                    recommendedMeasure = safeFallback;
                    isFallback = true;
                    riskFlags.Insert(0,
                        $"HARD-CONSTRAINT VERLETZT: KI schlug '{aiDto.recommended_measure}' vor "
                        + $"(=ausgeschlossen: {blockedProc.Name} - {blockedProc.Reason}). "
                        + $"Auf '{safeFallback}' umgestellt.");
                }
                else
                {
                    // Keine zulaessige Massnahme verfuegbar -> Fallback-Result mit klarer Diagnose
                    return BuildFallbackResult(req, constraints,
                        $"Alle Verfahren ausgeschlossen oder KI-Vorschlag '{aiDto.recommended_measure}' "
                        + $"verletzt Hard-Constraint '{blockedProc.Name}' ({blockedProc.Reason}). "
                        + "Manuelle Pruefung erforderlich.");
                }
            }
        }

        // 5b. §8 Validate AI measure (Constraints aus VsaFindings: Liner-Verbot bei Einsturz, etc.)
        var validation = _validation.Validate(recommendedMeasure, constraints);
        riskFlags.AddRange(validation.RiskFlags);

        if (!validation.Passed)
        {
            // Use first rule measure as fallback - aber RulesFilter weiterhin respektieren
            var safeFallback = PickSafeFallbackFromRulesFilter(req.RulesFilter, req.Rule)
                ?? req.Rule?.Measures.FirstOrDefault()
                ?? aiDto.recommended_measure;
            recommendedMeasure = safeFallback;
            isFallback         = true;
            riskFlags.Insert(0, "KI-Vorschlag wurde wegen §8-Verletzung verworfen und durch Regelempfehlung ersetzt");
        }

        // 5c. Pruefe ob die Maßnahme in der erlaubten Menge ist
        if (!isFallback && !allowedMeasures.Any(m =>
            recommendedMeasure.Contains(m, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(recommendedMeasure, StringComparison.OrdinalIgnoreCase)))
        {
            var safeFallback = PickSafeFallbackFromRulesFilter(req.RulesFilter, req.Rule)
                ?? req.Rule?.Measures.FirstOrDefault()
                ?? recommendedMeasure;
            recommendedMeasure = safeFallback;
            isFallback = true;
            riskFlags.Insert(0, $"KI schlug '{aiDto.recommended_measure}' vor, nicht in erlaubten Maßnahmen – Regelempfehlung verwendet");
        }

        if (aiDto.risk_flag && !string.IsNullOrWhiteSpace(aiDto.risk_message))
            riskFlags.Add(aiDto.risk_message);

        // 6. Cost calculation (with Groundwater + Inflation)
        var costBand = _costEngine.Calculate(new CostCalcInput
        {
            Measure           = recommendedMeasure,
            DiameterMm        = req.Pipe.DiameterMm,
            LengthMeter       = req.Pipe.LengthMeter,
            DepthM            = req.Pipe.DepthM,
            Access            = req.Pipe.Access,
            Material          = req.Pipe.Material,
            Groundwater       = req.Pipe.Groundwater ?? false,
            RegionFactor      = req.Cost.RegionFactor,
            InflationFactor   = req.Cost.InflationFactor,
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

    private string BuildSystemPrompt()
    {
        return """
            Du bist ein Experte für Kanalsanierung nach VSA-Standard (Schweiz).
            Analysiere die Schadensbefunde und empfehle die optimale Sanierungsmassnahme.

            ABSOLUT BINDENDE REGELN (keine Ausnahmen):
            1. Wenn der Abschnitt "HARTE TECHNISCHE EINSCHRAENKUNGEN" ausgeschlossene Verfahren auflistet,
               darfst du diese NIEMALS empfehlen - auch nicht mit Begruendung. Das sind Hard-Constraints
               aus Hersteller-Datenblaettern und Normen (DIBt, DWA, VSA QUIK).
            2. Wenn ZULAESSIGE Verfahren aufgelistet sind, waehlst du AUSSCHLIESSLICH aus dieser Liste.
            3. GFK-Liner (iMPREG GL16, Alphaliner, Brandenburger) sind NICHT bogengaengig (max ~15 Grad).
               Bei Bogenrohr nur Brawoliner (Textil, bis 90 Grad) oder Nadelfilz empfehlen.
            4. Asbestzement (AZ): KEIN Bersten, KEINE Roboterfraesung - Faserfreisetzung verboten.
            5. Bei Zustandsklasse 4-5 darf Erneuerung empfohlen werden, bei 1-3 nur Reparatur/Renovation.

            Weitere Regeln:
            - Liner-Verfahren sind bei Einsturz (BAC-Codes) nicht zulaessig (keine Tragstruktur)
            - confidence: 0.0 = unsicher, 1.0 = sehr sicher
            - cost_adjustment_factor: 1.0 = Standardkosten, >1.0 = teurer als ueblich, <1.0 = guenstiger
            - Bei Kosten orientierst du dich an der Marktreferenz (sofern angegeben)

            Antworte AUSSCHLIESSLICH im angegebenen JSON-Format.
            """;
    }

    private static string AccessToGerman(AccessDifficulty access) => access switch
    {
        AccessDifficulty.Easy => "Leicht",
        AccessDifficulty.Hard => "Schwer",
        _ => "Mittel"
    };

    private static string ConstraintToGerman(MeasureConstraint c) => c switch
    {
        MeasureConstraint.NoLinerAllowed => "Kein Liner-Verfahren zulässig (Einsturz erkannt)",
        MeasureConstraint.NoFullReplacement => "Keine Vollerneuerung nötig (Zustandsklasse < 4)",
        MeasureConstraint.RequiresStructuralCheck => "Struktureller Schaden – statische Prüfung erforderlich",
        MeasureConstraint.RequiresDewatering => "Grundwasser vorhanden – Wasserhaltung einplanen",
        MeasureConstraint.NoBerstlining => "Berstlining nicht geeignet (DN > 500)",
        _ => c.ToString()
    };

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

        // HARD-CONSTRAINTS aus RulesEngine - dominant vor allem anderen.
        // KI darf KEIN ausgeschlossenes Verfahren empfehlen, egal wie passend es scheint.
        if (req.RulesFilter is { } rf)
        {
            sb.AppendLine();
            sb.AppendLine("### HARTE TECHNISCHE EINSCHRAENKUNGEN (BINDEND - keine Ausnahmen):");
            if (rf.PromptHints.Count > 0)
            {
                foreach (var hint in rf.PromptHints)
                    sb.AppendLine($"  ! {hint}");
            }
            if (rf.ExcludedProcedures.Count > 0)
            {
                sb.AppendLine("  AUSGESCHLOSSENE Verfahren (KI darf diese NIEMALS empfehlen):");
                foreach (var ex in rf.ExcludedProcedures)
                    sb.AppendLine($"    - {ex.Name}: {ex.Reason}");
            }
            if (rf.EligibleProcedures.Count > 0 || rf.ConditionalProcedures.Count > 0)
            {
                sb.AppendLine("  ZULAESSIGE Verfahren (waehle ausschliesslich aus dieser Liste):");
                foreach (var p in rf.EligibleProcedures)
                    sb.AppendLine($"    + {p} (geeignet)");
                foreach (var p in rf.ConditionalProcedures)
                    sb.AppendLine($"    ~ {p} (bedingt geeignet)");
            }
        }

        sb.AppendLine();
        sb.AppendLine("### Rohrkontext:");
        sb.AppendLine($"  DN: {req.Pipe.DiameterMm?.ToString() ?? "unbekannt"} mm");
        sb.AppendLine($"  Material: {req.Pipe.Material ?? "unbekannt"}");
        sb.AppendLine($"  Länge: {req.Pipe.LengthMeter?.ToString("F1") ?? "unbekannt"} m");
        if (req.Pipe.DepthM.HasValue)
            sb.AppendLine($"  Tiefe: {req.Pipe.DepthM.Value:F1} m");
        sb.AppendLine($"  Zugang: {AccessToGerman(req.Pipe.Access)}");
        sb.AppendLine($"  Grundwasser: {(req.Pipe.Groundwater switch { true => "JA (Wasserhaltung erforderlich)", false => "Nein", _ => "nicht erfasst" })}");

        // Historical similar cases for cost calibration
        if (req.SimilarCases is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("### Historische Vergleichsfälle (tatsächliche Kosten):");
            foreach (var sc in req.SimilarCases.Take(5))
            {
                var dn = sc.PipeDn.HasValue ? $" DN{sc.PipeDn}" : "";
                var len = sc.LengthM.HasValue ? $" {sc.LengthM:F1}m" : "";
                sb.AppendLine($"  - {sc.Code}{dn}{len}: {sc.Measure} → {sc.ActualCost:0} CHF");
            }
            sb.AppendLine("Berücksichtige diese Erfahrungswerte bei deiner Kostenschätzung (cost_adjustment_factor).");
        }

        // Marktreferenz aus historischem Profil-Aggregat (Buerglen 2024-2026)
        if (req.MarktReferenz is { AnzahlFaelle: >= 3 } mr)
        {
            sb.AppendLine();
            sb.AppendLine("### Marktreferenz (n=" + mr.AnzahlFaelle + " ähnliche Sanierungen, Bürglen 2024-2026):");
            sb.AppendLine($"  Profil: {mr.ProfilLabel}");
            if (mr.KostenProMMedianChf.HasValue)
                sb.AppendLine($"  Median Kosten/Meter: {mr.KostenProMMedianChf.Value:F2} CHF/m");
            if (mr.KostenProHaltungMedianChf.HasValue)
                sb.AppendLine($"  Median Kosten/Haltung: {mr.KostenProHaltungMedianChf.Value:F0} CHF");
            if (mr.TypischeMassnahmen.Count > 0)
            {
                sb.AppendLine("  Typische Massnahmen aus diesen Fällen:");
                foreach (var m in mr.TypischeMassnahmen.Take(3))
                    sb.AppendLine($"    - {m}");
            }
            if (mr.EmpfohleneSubmissionsBlocks.Count > 0)
                sb.AppendLine($"  Submissions-Blocks: {string.Join(", ", mr.EmpfohleneSubmissionsBlocks)} (Schweizer Standard)");
            sb.AppendLine("  Diese Marktreferenz ist ein verlässlicher Kosten-Anker. cost_adjustment_factor sollte sich daran orientieren.");
        }

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
                sb.AppendLine($"  - {ConstraintToGerman(c)}");
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
        IReadOnlyList<MeasureConstraint> constraints,
        RulesFilterDto? rulesFilter = null)
    {
        if (rule is null || rule.Measures.Count == 0)
            return ["Schlauchliner", "Kurzliner", "Reparatur", "Erneuerung Neubau"];

        var allowed = rule.Measures.ToList();

        // §8-Constraints (aus VsaFindings)
        if (constraints.Contains(MeasureConstraint.NoLinerAllowed))
            allowed.RemoveAll(SanierungValidationService.IsLinerMeasure);

        if (constraints.Contains(MeasureConstraint.NoFullReplacement))
            allowed.RemoveAll(SanierungValidationService.IsReplacementMeasure);

        if (constraints.Contains(MeasureConstraint.NoBerstlining))
            allowed.RemoveAll(SanierungValidationService.IsBerstliningMeasure);

        // Hard-Constraints aus RulesEngine (z.B. GFK-Bogen, Asbest-Bersten).
        // ExcludedProcedures haben Vorrang - betroffene Massnahmen werden hart entfernt,
        // KEIN Fallback auf rule.Measures (sonst werden gerade die Excludes wieder zugelassen).
        if (rulesFilter?.ExcludedProcedures is { Count: > 0 } excluded)
        {
            allowed.RemoveAll(m => excluded.Any(ex => MeasureMatchesProcedure(m, ex.ProcedureId)));
        }

        // Wenn nach §8-Filter leer, aber RulesFilter NICHT alles ausschliesst -> Original.
        // Wenn nach RulesFilter leer -> bewusst leere Liste (Hard-Constraint hat absolute Prioritaet).
        if (allowed.Count == 0)
        {
            var rulesFilterActive = rulesFilter?.ExcludedProcedures is { Count: > 0 };
            return rulesFilterActive ? Array.Empty<string>() : rule.Measures;
        }
        return allowed;
    }

    /// <summary>Mappt Massnahmen-Klartext auf Procedure-IDs (cipp_inliner, berstlining, ...).</summary>
    private static bool MeasureMatchesProcedure(string measure, string procedureId)
    {
        var m = measure.ToLowerInvariant();
        return procedureId.ToLowerInvariant() switch
        {
            "cipp_inliner" => m.Contains("schlauchlin") || m.Contains("inliner") || m.Contains("relining") || m.Contains("cipp"),
            "short_liner"  => m.Contains("kurzliner") || m.Contains("partliner") || m.Contains("hutprofil"),
            "robotic_milling" => m.Contains("roboter") && (m.Contains("fräs") || m.Contains("fraes")),
            "robotic_repair"  => m.Contains("roboter") && (m.Contains("spachtel") || m.Contains("reparatur")),
            "mechanical_sleeve" => m.Contains("manschette") || m.Contains("quick-lock"),
            "berstlining" => m.Contains("berst") || m.Contains("pipe bursting"),
            "close_fit_lining" => m.Contains("close-fit") || m.Contains("close fit"),
            "injection_grouting" => m.Contains("injekt") || m.Contains("verpress"),
            "open_excavation" => m.Contains("offene bauweise") || m.Contains("erneuerung neubau") || m.Contains("aufgraben"),
            "shaft_rehabilitation" => m.Contains("schacht"),
            _ => m.Contains(procedureId.Replace("_", " "))
        };
    }

    /// <summary>Prueft ob die KI-Antwort auf eine ausgeschlossene Procedure faellt - liefert
    /// die zugehoerige ExcludedProcedure zurueck oder null.</summary>
    private static ExcludedProcedure? MatchExcludedProcedure(
        string aiMeasure, IReadOnlyList<ExcludedProcedure> excluded)
    {
        return excluded.FirstOrDefault(ex => MeasureMatchesProcedure(aiMeasure, ex.ProcedureId));
    }

    /// <summary>Waehlt eine sichere Fallback-Massnahme aus rule.Measures, die NICHT
    /// vom RulesFilter ausgeschlossen ist.</summary>
    private static string? PickSafeFallbackFromRulesFilter(RulesFilterDto? rulesFilter, RuleRecommendationDto? rule)
    {
        if (rule?.Measures is not { Count: > 0 }) return null;
        var excluded = rulesFilter?.ExcludedProcedures ?? Array.Empty<ExcludedProcedure>();

        foreach (var m in rule.Measures)
        {
            var hits = excluded.Any(ex => MeasureMatchesProcedure(m, ex.ProcedureId));
            if (!hits) return m;
        }
        return null;
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
            Measure         = fallbackMeasure,
            DiameterMm      = req.Pipe.DiameterMm,
            LengthMeter     = req.Pipe.LengthMeter,
            DepthM          = req.Pipe.DepthM,
            Access          = req.Pipe.Access,
            Material        = req.Pipe.Material,
            Groundwater     = req.Pipe.Groundwater ?? false,
            RegionFactor    = req.Cost.RegionFactor,
            InflationFactor = req.Cost.InflationFactor
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
