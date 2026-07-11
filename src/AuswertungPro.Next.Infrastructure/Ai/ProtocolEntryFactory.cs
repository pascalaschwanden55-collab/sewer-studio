using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Reine Bau-/Mapping-Helfer fuer FullProtocolGenerationService.
/// Keine IO-, Ollama-, KB- oder QualityGate-Abhaengigkeiten.
/// </summary>
internal static class ProtocolEntryFactory
{
    /// <summary>
    /// Baut den LLM-Nutzertext-Prompt fuer eine einzelne Detection.
    /// </summary>
    internal static string BuildPrompt(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request,
        string vsaHint,
        IReadOnlyList<KbExample> kbExamples)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Antworte ausschließlich im vorgegebenen JSON-Schema.");
        sb.AppendLine("Wähle nur EINEN Basiscode aus 'Erlaubte Codes'.");
        sb.AppendLine();
        sb.AppendLine($"Befund: {detection.FindingLabel}");
        sb.AppendLine($"Schweregrad: {detection.Severity}");
        sb.AppendLine($"Meterbereich: {detection.MeterStart:0.00}m – {detection.MeterEnd:0.00}m");
        if (!string.IsNullOrWhiteSpace(detection.PositionClock))
            sb.AppendLine($"Uhrlage: {detection.PositionClock}");
        if (detection.ExtentPercent is > 0)
            sb.AppendLine($"Ausdehnung: {detection.ExtentPercent}% Umfang");
        if (detection.HeightMm is > 0)
            sb.AppendLine($"Schadenshöhe: {detection.HeightMm}mm");
        if (detection.WidthMm is > 0)
            sb.AppendLine($"Schadensbreite: {detection.WidthMm}mm");
        if (detection.IntrusionPercent is > 0)
            sb.AppendLine($"Einragungsgrad: {detection.IntrusionPercent}%");
        if (detection.CrossSectionReductionPercent is > 0)
            sb.AppendLine($"Querschnittsverringerung: {detection.CrossSectionReductionPercent}%");
        if (detection.DiameterReductionMm is > 0)
            sb.AppendLine($"Durchmesserverringerung: {detection.DiameterReductionMm}mm");
        sb.AppendLine($"Haltung: {request.HaltungId}");
        if (!string.IsNullOrWhiteSpace(vsaHint))
            sb.AppendLine(vsaHint);
        if (kbExamples.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Ähnliche Fälle aus Wissensdatenbank:");
            foreach (var ex in kbExamples.Take(3))
            {
                sb.AppendLine(
                    $"- Code={ex.Code}; Score={ex.Score:0.000}; Meter={ex.MeterStart:0.00}-{ex.MeterEnd:0.00}m; Text={ex.Description}");
            }
        }
        sb.AppendLine();
        sb.AppendLine($"Erlaubte Codes: {string.Join(", ", request.AllowedCodes)}");
        sb.AppendLine("Erzeuge: suggestedCode, confidence (0.0-1.0), rationale, evidence, warnings.");
        return sb.ToString();
    }

    /// <summary>
    /// Baut den Suchtext fuer die Wissensdatenbank-Abfrage.
    /// </summary>
    internal static string BuildKnowledgeQuery(
        RawVideoDetection detection,
        FullProtocolGenerationRequest request)
    {
        var parts = new List<string>
        {
            detection.FindingLabel,
            $"Meter {detection.MeterStart:0.00}-{detection.MeterEnd:0.00}",
            $"Severity {detection.Severity}",
            $"Haltung {request.HaltungId}"
        };

        if (!string.IsNullOrWhiteSpace(detection.PositionClock))
            parts.Add($"Uhrlage {detection.PositionClock}");

        if (detection.ExtentPercent is > 0)
            parts.Add($"Ausdehnung {detection.ExtentPercent}%");

        if (!string.IsNullOrWhiteSpace(detection.VsaCodeHint))
            parts.Add($"VisionCode {detection.VsaCodeHint}");

        if (detection.HeightMm is > 0)
            parts.Add($"Höhe {detection.HeightMm}mm");
        if (detection.IntrusionPercent is > 0)
            parts.Add($"Einragung {detection.IntrusionPercent}%");
        if (detection.CrossSectionReductionPercent is > 0)
            parts.Add($"QV {detection.CrossSectionReductionPercent}%");

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>
    /// Baut einen ProtocolEntry aus einem gemappten Befund.
    /// </summary>
    internal static ProtocolEntry BuildProtocolEntry(MappedProtocolEntry mapped)
    {
        var det = mapped.Detection;
        return new ProtocolEntry
        {
            // Kennung aus dem Mapping uebernehmen: verbindet die Auswahl im
            // Vollanalyse-Fenster mit dem Dokument (Uebernahme-Filter, Stufe 1B).
            EntryId = mapped.EntryId != Guid.Empty ? mapped.EntryId : Guid.NewGuid(),
            Code = mapped.SuggestedCode ?? string.Empty,
            Beschreibung = det.FindingLabel,
            MeterStart = det.MeterStart,
            MeterEnd = det.MeterEnd > det.MeterStart ? det.MeterEnd : det.MeterStart,
            IsStreckenschaden = det.MeterEnd - det.MeterStart > 0.05,
            // KI-generierter Eintrag: als Ai kennzeichnen, nicht als Manual tarnen
            // (Herkunft/Filterung im Export bleibt nachvollziehbar).
            Source = ProtocolEntrySource.Ai,
            CodeMeta = BuildCodeMeta(mapped),
            Ai = new ProtocolEntryAiMeta
            {
                SuggestedCode = mapped.SuggestedCode,
                Confidence = mapped.Confidence,
                Reason = mapped.Reason,
                Flags = mapped.Warnings.ToList(),
                MeterSource = det.MeterSource,
                IsMeterEstimated = det.IsMeterEstimated,
                CentralDecision = mapped.Freigabe is null
                    ? null
                    : AiDecisionAuditMapper.Create(
                        mapped.Freigabe,
                        mapped.VisionModel,
                        mapped.TextModel,
                        mapped.QualityGateVersion,
                        mapped.QualityGateResult?.WeightsUsed,
                        mapped.QualityGateResult?.Explanation),
                SuggestedAt = DateTimeOffset.UtcNow
            }
        };
    }

    /// <summary>
    /// Baut die CodeMeta-Struktur mit Quantifizierungsparametern.
    /// </summary>
    internal static ProtocolEntryCodeMeta? BuildCodeMeta(MappedProtocolEntry mapped)
    {
        var det = mapped.Detection;
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddClockParameters(parameters, det.PositionClock);
        AddMm(parameters, "vsa.q1", det.HeightMm);
        AddMm(parameters, "vsa.q2", det.WidthMm);
        AddPercent(parameters, "vsa.umfang.prozent", det.ExtentPercent);
        AddPercent(parameters, "vsa.einragung.prozent", det.IntrusionPercent);
        AddPercent(parameters, "vsa.querschnitt.prozent", det.CrossSectionReductionPercent);
        AddMm(parameters, "vsa.dn.reduktion", det.DiameterReductionMm);

        if (parameters.Count == 0 && string.IsNullOrWhiteSpace(det.Severity))
            return null;

        return new ProtocolEntryCodeMeta
        {
            Code = mapped.SuggestedCode ?? det.VsaCodeHint ?? string.Empty,
            Parameters = parameters,
            Severity = string.IsNullOrWhiteSpace(det.Severity) ? null : det.Severity.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Fuegt Uhrlage-Parameter (vsa.uhr.von / vsa.uhr.bis) in das Dictionary ein.
    /// </summary>
    internal static void AddClockParameters(Dictionary<string, string> parameters, string? positionClock)
    {
        if (string.IsNullOrWhiteSpace(positionClock))
            return;

        var raw = positionClock.Trim();
        var parts = raw.Split(new[] { '-', '–' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            parameters["vsa.uhr.von"] = parts[0];
            parameters["vsa.uhr.bis"] = parts[1];
            return;
        }

        parameters["vsa.uhr.von"] = raw;
    }

    /// <summary>
    /// Fuegt einen mm-Wert in das Dictionary ein, sofern vorhanden und positiv.
    /// </summary>
    internal static void AddMm(Dictionary<string, string> parameters, string key, int? value)
    {
        if (value is > 0)
            parameters[key] = $"{value.Value} mm";
    }

    /// <summary>
    /// Fuegt einen Prozentwert in das Dictionary ein, sofern vorhanden und positiv.
    /// </summary>
    internal static void AddPercent(Dictionary<string, string> parameters, string key, int? value)
    {
        if (value is > 0)
            parameters[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Wissensdatenbank-Treffer fuer den Prompt-Bau (internes DTO).
/// </summary>
internal sealed record KbExample(
    string Code,
    string Description,
    double MeterStart,
    double MeterEnd,
    double Score);
