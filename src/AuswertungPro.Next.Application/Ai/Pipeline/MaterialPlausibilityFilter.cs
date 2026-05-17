using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Vision;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Rohrmaterial-bezogene Plausibilitaetsregeln fuer Detektionen.
///
/// Aus <c>MultiModelAnalysisService.Helpers.cs</c> migriert (Audit 2026-05-13 M1).
/// Pure-Logic-Klasse: keine HTTP-, DB- oder UI-Abhaengigkeiten.
/// </summary>
public static class MaterialPlausibilityFilter
{
    /// <summary>
    /// Prueft ob das Material ein Kunststoff ist (dichte Rohrwand).
    /// Erkennt: Polyethylen, PE, PVC, PP, GFK, Kunststoff, Plastik etc.
    /// </summary>
    public static bool IsKunststoff(string material)
    {
        if (string.IsNullOrEmpty(material)) return false;
        var m = material.ToUpperInvariant();
        return m.Contains("PE") || m.Contains("PVC") || m.Contains("PP")
            || m.Contains("GFK") || m.Contains("KUNSTSTOFF") || m.Contains("PLASTIK")
            || m.Contains("POLYETHYL") || m.Contains("POLYPROP") || m.Contains("POLYVINYL")
            || m.Contains("HDPE") || m.Contains("FASERZ"); // Faserzement = auch dicht
    }

    /// <summary>
    /// Mappt ein Qwen-Material auf den spezifischen AED-Untercode.
    /// VSA: AEDXO=PE, AEDXP=PP, AEDXQ=PVC, AEDXG=Beton, AEDXU=Steinzeug, AED=generisch.
    /// </summary>
    public static string MapMaterialToAedCode(string material)
    {
        if (string.IsNullOrEmpty(material)) return "AED";
        var m = material.ToUpperInvariant();
        if (m.Contains("PE") || m.Contains("POLYETHYL")) return "AEDXO";
        if (m.Contains("PP") || m.Contains("POLYPROP")) return "AEDXP";
        if (m.Contains("PVC") || m.Contains("POLYVINYL")) return "AEDXQ";
        if (m.Contains("BETON") || m.Contains("CONCRETE")) return "AEDXG";
        if (m.Contains("STEINZEUG") || m.Contains("VITRIF")) return "AEDXU";
        if (m.Contains("GFK") || m.Contains("FIBERGLASS")) return "AEDXH";
        if (m.Contains("STAHL") || m.Contains("STEEL")) return "AEDXI";
        if (m.Contains("GUSS") || m.Contains("CAST")) return "AEDXJ";
        if (m.Contains("FASER") || m.Contains("ASBESTOS")) return "AEDXK";
        return "AED"; // Generisch
    }

    /// <summary>
    /// Filtert Detektionen die fuer das verbaute Rohrmaterial unplausibel sind.
    /// Kunststoffrohre (PE, PVC, PP, GFK) sind dicht — Infiltration (BBF) ist nur
    /// bei gleichzeitigem Strukturschaden (BA) oder defekter Verbindung (BAJ/BAH)
    /// moeglich. Ohne Begleitschaden wird BBF/BBD bei Kunststoff verworfen.
    /// </summary>
    /// <param name="detections">Liste wird in-place gefiltert.</param>
    /// <param name="material">Rohrmaterial (z.B. "PE", "Beton"). Leer/null → no-op.</param>
    /// <param name="logger">Optional fuer Protokollierung der Filter-Entscheidungen.</param>
    /// <returns>Anzahl entfernter Detektionen.</returns>
    public static int Apply(List<RawVideoDetection> detections, string? material, ILogger? logger = null)
    {
        if (detections == null || detections.Count == 0) return 0;
        if (string.IsNullOrWhiteSpace(material)) return 0;
        if (!IsKunststoff(material!)) return 0;

        // Pruefen ob ein Strukturschaden in der Naehe ist (±2m)
        bool HasNearbyStructuralDamage(double meter)
        {
            foreach (var d in detections)
            {
                var code = d.VsaCodeHint;
                if (string.IsNullOrEmpty(code) || code.Length < 2) continue;
                var prefix = code[..2].ToUpperInvariant();
                // BA = Strukturell (Riss, Bruch, Versatz), oder BAJ/BAH (defekte Verbindung)
                if (prefix == "BA" && Math.Abs(d.MeterStart - meter) < 2.0)
                    return true;
            }
            return false;
        }

        var before = detections.Count;
        detections.RemoveAll(d =>
        {
            var code = d.VsaCodeHint?.ToUpperInvariant() ?? "";
            // BBF = Infiltration: bei Kunststoff nur mit Begleitschaden
            if (code.StartsWith("BBF") && !HasNearbyStructuralDamage(d.MeterStart))
            {
                logger?.LogInformation(
                    "Materialplausibilitaet: {Code} @ {Meter:F1}m verworfen — Kunststoffrohr ({Material}) ohne Begleitschaden",
                    code, d.MeterStart, material);
                return true;
            }
            // BBD = Eindringender Boden: bei intaktem Kunststoff ebenfalls nur mit Schaden
            if (code.StartsWith("BBD") && !HasNearbyStructuralDamage(d.MeterStart))
            {
                logger?.LogInformation(
                    "Materialplausibilitaet: {Code} @ {Meter:F1}m verworfen — Kunststoffrohr ({Material}) ohne Begleitschaden",
                    code, d.MeterStart, material);
                return true;
            }
            return false;
        });

        var removed = before - detections.Count;
        if (removed > 0)
        {
            logger?.LogInformation(
                "Materialplausibilitaet ({Material}): {Before} → {After} Detektionen",
                material, before, detections.Count);
        }
        return removed;
    }
}
