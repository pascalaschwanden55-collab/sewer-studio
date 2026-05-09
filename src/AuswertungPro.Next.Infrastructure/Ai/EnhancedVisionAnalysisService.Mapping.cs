using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;

namespace AuswertungPro.Next.Infrastructure.Ai;

// EnhancedVisionAnalysisService Mapping-Layer: DTOs fuer JSON-Deserialisierung
// (EnhancedVisionDto, EnhancedFindingDto) plus MapToAnalysis (DTO -> Domain-
// Result mit Severity-Clamp, Findings-Filter, ViewType-Validierung).
// Aus dem Hauptdatei extrahiert (Slice 15a).
public sealed partial class EnhancedVisionAnalysisService
{
    private static EnhancedFrameAnalysis MapToAnalysis(EnhancedVisionDto dto)
    {
        var findings = (dto.Findings ?? Array.Empty<EnhancedFindingDto>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Label))
            .Select(f =>
            {
                var label = f.Label.Trim();
                var codeHint = f.VsaCodeHint?.Trim();

                // Code-Extraktion aus Label (3 Fallbacks):
                // 1. Label ist reiner VSA-Code (3-6 Grossbuchstaben): "BABBA" → codeHint
                // 2. Label beginnt mit Code + Doppelpunkt: "BBBA: Inkrustation..." → "BBBA"
                // 3. Label ist Freitext → InferCodeFromLabel/ReverseLookup
                if (string.IsNullOrEmpty(codeHint))
                {
                    if (label.Length >= 3 && label.Length <= 6
                        && label.All(c => c >= 'A' && c <= 'Z'))
                    {
                        // Reiner VSA-Code
                        codeHint = label;
                    }
                    else if (label.Length >= 4 && label[..3].All(c => c >= 'A' && c <= 'Z')
                             && (label[3] == ':' || label[3] == ' ' || (label.Length > 3 && label[3] >= 'A' && label[3] <= 'Z')))
                    {
                        // Code am Anfang: "BBBA: Inkrustation..." oder "BBBA Inkrustation"
                        var codePart = label.Split(new[] { ':', ' ', '-' }, 2)[0].Trim();
                        if (codePart.Length >= 3 && codePart.Length <= 6 && codePart.All(c => c >= 'A' && c <= 'Z'))
                            codeHint = codePart;
                    }

                    // Freitext → VSA-Code (deutsch + englisch)
                    if (string.IsNullOrEmpty(codeHint))
                    {
                        codeHint = VsaCodeResolver.InferCodeFromLabel(label)
                            ?? AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.ReverseLookup(label);
                    }
                }

                return new EnhancedFinding(
                Label: label,
                VsaCodeHint: codeHint,
                Severity: Math.Clamp(f.Severity, 1, 5),
                PositionClock: f.PositionClock?.Trim(),
                ExtentPercent: f.ExtentPercent,
                HeightMm: f.HeightMm,
                WidthMm: f.WidthMm,
                IntrusionPercent: f.IntrusionPercent,
                CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                DiameterReductionMm: f.DiameterReductionMm,
                Notes: f.Notes?.Trim());
            })
            .ToList();

        // Aufnahmetyp normalisieren
        var viewType = (dto.ViewType ?? "axial").ToLowerInvariant() switch
        {
            "nahaufnahme" => "nahaufnahme",
            "schwenk" => "schwenk",
            "schacht" => "schacht",
            _ => "axial"
        };

        // Soft-Filter: Bei Nahaufnahme/Schwenk Severity abstufen statt loeschen
        // Audit-Trail: Unterdrueckte Findings werden in SuppressedFindings gespeichert
        var suppressedFindings = new List<EnhancedFinding>();

        if (viewType is "nahaufnahme" or "schwenk")
        {
            // Soft-Filter: Severity hart auf 1 (= optisch/Beobachtung) abstufen
            // und Audit-Notes ergaenzen. Findings bleiben in der Liste, koennen aber
            // QualityGate nicht mehr triggern.
            suppressedFindings.AddRange(findings);
            findings = findings.Select(f => f with
            {
                Severity = 1,
                Notes = $"[Soft-Suppress: view_type={viewType} -> Severity 1] {f.Notes ?? ""}"
            }).ToList();
        }

        // Bei Schachtaufnahme: nur Steuercodes durchlassen, Rest als unterdrueckt markieren
        if (viewType is "schacht")
        {
            var kept = new List<EnhancedFinding>();
            foreach (var f in findings)
            {
                var code = (f.VsaCodeHint ?? f.Label).ToUpperInvariant();
                if (code.StartsWith("BCD") || code.StartsWith("BCE") || code.StartsWith("BDB"))
                    kept.Add(f);
                else
                    suppressedFindings.Add(f);
            }
            findings = kept;
        }

        // Audit-Logging fuer unterdrueckte Findings
        if (suppressedFindings.Count > 0)
        {
            var suppressedCodes = string.Join(", ", suppressedFindings.Select(f => f.VsaCodeHint ?? f.Label));
            LastSuppressedLog = $"[Suppressed] {suppressedFindings.Count} Findings unterdrueckt (view_type={viewType}): {suppressedCodes}";
            System.Diagnostics.Debug.WriteLine(LastSuppressedLog);
        }
        else
        {
            LastSuppressedLog = null;
        }

        return new EnhancedFrameAnalysis(
            Meter: dto.Meter,
            PipeMaterial: dto.PipeMaterial ?? "unbekannt",
            PipeDiameterMm: dto.PipeDiameterMm,
            Findings: findings,
            ImageQuality: dto.ImageQuality ?? "mittel",
            IsEmptyFrame: dto.IsEmptyFrame && findings.Count == 0,
            Error: null,
            ViewType: viewType);
    }

    private sealed record EnhancedVisionDto(
        [property: JsonPropertyName("meter")] double? Meter,
        [property: JsonPropertyName("time_in_video")] double? TimeInVideo,
        [property: JsonPropertyName("pipe_material")] string? PipeMaterial,
        [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm,
        [property: JsonPropertyName("findings")] IReadOnlyList<EnhancedFindingDto>? Findings,
        [property: JsonPropertyName("image_quality")] string? ImageQuality,
        [property: JsonPropertyName("is_empty_frame")] bool IsEmptyFrame,
        [property: JsonPropertyName("view_type")] string? ViewType);

    private sealed record EnhancedFindingDto(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("vsa_code_hint")] string? VsaCodeHint,
        [property: JsonPropertyName("severity")] int Severity,
        [property: JsonPropertyName("position_clock")] string? PositionClock,
        [property: JsonPropertyName("extent_percent")] int? ExtentPercent,
        [property: JsonPropertyName("height_mm")] int? HeightMm,
        [property: JsonPropertyName("width_mm")] int? WidthMm,
        [property: JsonPropertyName("intrusion_percent")] int? IntrusionPercent,
        [property: JsonPropertyName("cross_section_reduction_percent")] int? CrossSectionReductionPercent,
        [property: JsonPropertyName("diameter_reduction_mm")] int? DiameterReductionMm,
        [property: JsonPropertyName("notes")] string? Notes);
}
