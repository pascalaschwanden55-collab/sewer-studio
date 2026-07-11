using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Schatten;

/// <summary>
/// Baut den LLM-Optimierungs-Request fuer die Schattenauswertung — rein lesend.
///
/// Bewusste Abweichung von der Vorlage (SanierungOptimizationViewModel.BuildRequest):
/// Die Zustandsklasse kommt aus dem SCHATTEN-Ergebnis, NICHT aus dem Mensch-Feld
/// "Zustandsklasse" — sonst wuerde die "eigenstaendige" KI-Empfehlung von der
/// menschlichen Auswertung abschreiben.
/// </summary>
public static class SchattenLlmRequestBuilder
{
    public static SanierungOptimizationRequest Build(
        HaltungRecord record,
        string? schattenZustandsklasse,
        RuleRecommendationDto? rule)
    {
        ArgumentNullException.ThrowIfNull(record);

        var findings = new List<DamageFindingDto>();

        if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
        {
            foreach (var e in entries.Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)))
            {
                findings.Add(new DamageFindingDto
                {
                    Code = e.Code,
                    PositionMeter = e.MeterStart,
                    LengthMeter = e.MeterEnd.HasValue && e.MeterStart.HasValue
                        ? e.MeterEnd.Value - e.MeterStart.Value
                        : null,
                    Comment = e.Beschreibung
                });
            }
        }
        else if (record.VsaFindings is { Count: > 0 } vsaFindings)
        {
            foreach (var f in vsaFindings)
            {
                findings.Add(new DamageFindingDto
                {
                    Code = f.KanalSchadencode?.Trim() ?? "",
                    PositionMeter = f.MeterStart ?? f.SchadenlageAnfang,
                    Quantification = f.Quantifizierung1
                });
            }
        }

        var dnRaw = record.GetFieldValue("DN_mm");
        int.TryParse(dnRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn);

        var lengthRaw = record.GetFieldValue("Haltungslaenge_m");
        double.TryParse(lengthRaw?.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var lengthM);

        var gwRaw = record.GetFieldValue("Grundwasserspiegel");
        bool? groundwater = gwRaw?.Trim().ToLowerInvariant() switch
        {
            "oberhalb" => true,
            "unterhalb" => false,
            _ => null
        };

        var pipe = new PipeContextDto
        {
            DiameterMm = dn > 0 ? dn : null,
            Material = record.GetFieldValue("Rohrmaterial"),
            LengthMeter = lengthM > 0 ? lengthM : null,
            Groundwater = groundwater,
            Region = record.GetFieldValue("Strasse"),
            ProjectYear = DateTime.UtcNow.Year
        };

        // Schatten-Klasse auf alle Findings verteilen (Eigenstaendigkeit, s. Klassen-Kommentar).
        if (!string.IsNullOrWhiteSpace(schattenZustandsklasse))
        {
            for (var i = 0; i < findings.Count; i++)
                findings[i] = findings[i] with { SeverityClass = findings[i].SeverityClass ?? schattenZustandsklasse };
        }

        return new SanierungOptimizationRequest
        {
            HaltungId = record.GetFieldValue("Haltungsname") is { Length: > 0 } name ? name : record.Id.ToString(),
            Findings = findings,
            Pipe = pipe,
            Rule = rule
        };
    }
}
