using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Vsa;
using VsaFinding = AuswertungPro.Next.Domain.Models.VsaFinding;

namespace AuswertungPro.Next.Infrastructure.Vsa;

/// <summary>
/// Reine Berechnungslogik fuer die VSA-Zustandsbeurteilung gemaess VSA Richtlinie 2023.
/// Kein IO, kein State, keine Abhaengigkeiten ausser auf Domaenobjekte.
/// </summary>
internal static class VsaConditionScorer
{
    // ── Kernberechnung: Zustandsnote + Dringlichkeitszahl ────────────────

    /// <summary>
    /// Berechnet ZN und DZ fuer eine Anforderung gemaess VSA Richtlinie 2023.
    /// ZN = EZ_min + 0.4 - A  (Kap. 5.2, Formel 1)
    /// DZ = ZN x 100 x B1 x B2 x B3 x B4  (Kap. 5.3, Formel 2)
    /// </summary>
    internal static VsaConditionResult ComputeForRequirement(
        VsaRequirement requirement,
        IReadOnlyList<ClassifiedFinding> classified,
        double assessmentLength,
        double minLength,
        double randbedingungen)
    {
        // EZ-Werte mit Laengenfaktoren sammeln (inkl. Code-Herkunft fuer Rechnungsweg)
        var entries = new List<(int EZ, double LF, string OrigCode)>();
        var skippedCodes = new List<string>(); // Codes ohne EZ fuer diese Anforderung
        foreach (var c in classified)
        {
            var origCode = NormalizeCode(c.Finding.KanalSchadencode);
            int? ez = requirement switch
            {
                VsaRequirement.Dichtheit => c.Classification.EZD,
                VsaRequirement.Standsicherheit => c.Classification.EZS,
                _ => c.Classification.EZB
            };
            if (ez is null)
            {
                if (!c.IsUnknown) skippedCodes.Add(origCode);
                continue;
            }
            entries.Add((ez.Value, ComputeLengthFactor(c.Finding, minLength), origCode));
        }

        if (entries.Count == 0)
        {
            // Unterscheide: keine Findings vs. nur unbekannte Codes
            var hasUnknown = classified.Any(c => c.IsUnknown);
            if (hasUnknown)
            {
                var na = new VsaConditionResult
                {
                    Requirement = requirement,
                    Zustandsnote = null,
                    WorstEinzelzustand = null,
                    Abminderung = null,
                    Dringlichkeitszahl = null
                };
                na.Notes.Add("Nur unbekannte Schadenscodes – Bewertung nicht moeglich.");
                return na;
            }

            if (skippedCodes.Count > 0)
            {
                var na = new VsaConditionResult
                {
                    Requirement = requirement,
                    Zustandsnote = null,
                    WorstEinzelzustand = null,
                    Abminderung = null,
                    Dringlichkeitszahl = null
                };
                na.Notes.Add($"Keine bewertbaren EZ fuer diese Anforderung (Codes ohne EZ: {string.Join(", ", skippedCodes)}).");
                return na;
            }

            var dzOk = Math.Round(4.0 * 100.0 * randbedingungen, 2, MidpointRounding.AwayFromZero);
            var ok = new VsaConditionResult
            {
                Requirement = requirement,
                Zustandsnote = 4.00,
                WorstEinzelzustand = 4,
                Abminderung = 0,
                Dringlichkeitszahl = dzOk
            };
            ok.Notes.Add("Keine Schadenscodes vorhanden – Leitung i.O.");
            return ok;
        }

        // EZ_min = schlechtester Einzelzustand (0 = schlecht, 4 = gut)
        var ezMin = entries.Min(e => e.EZ);

        double zn;
        double abminderung = 0;

        if (ezMin == 4)
        {
            // Bestmoeglicher Zustand – keine Abminderung
            zn = 4.00;
        }
        else
        {
            // ZN_start = EZ_min + 0.4
            var znStart = ezMin + 0.4;

            // Abminderung A = 0.4 × Σ((4 - EZ_i) × LF_i) / ((4 - EZ_min) × LA)
            if (assessmentLength > 0)
            {
                var sumNumerator = entries.Sum(e => (4.0 - e.EZ) * e.LF);
                var denominator = (4.0 - ezMin) * assessmentLength;
                if (denominator > 0)
                {
                    abminderung = 0.4 * sumNumerator / denominator;
                    abminderung = Math.Min(abminderung, 0.8); // A ≤ 0.8
                }
            }

            zn = Math.Max(znStart - abminderung, 0); // ZN ≥ 0
        }

        zn = Math.Round(zn, 2, MidpointRounding.AwayFromZero);
        zn = Math.Min(zn, 4.00); // sicherheitshalber kappen
        abminderung = Math.Round(abminderung, 2, MidpointRounding.AwayFromZero);

        // DZ = ZN × 100 × Π(B_j)
        var dz = Math.Round(zn * 100.0 * randbedingungen, 2, MidpointRounding.AwayFromZero);

        var result = new VsaConditionResult
        {
            Requirement = requirement,
            Zustandsnote = zn,
            WorstEinzelzustand = ezMin,
            Abminderung = abminderung,
            Dringlichkeitszahl = dz
        };

        // Zusammenfassung
        result.Notes.Add($"Beitraege={entries.Count}; EZmin={ezMin}; A={abminderung:F2}; RB={randbedingungen:F4}");

        // Einzelbeitraege auflisten
        foreach (var e in entries)
            result.Notes.Add($"  {e.OrigCode}: EZ={e.EZ}, LF={e.LF:F1}m");

        // Codes ohne EZ-Beitrag fuer diese Anforderung
        if (skippedCodes.Count > 0)
            result.Notes.Add($"  (ohne EZ: {string.Join(", ", skippedCodes)})");

        return result;
    }

    // ── Laengenfaktor ────────────────────────────────────────────────────

    /// <summary>
    /// LF_i: Laengenfaktor pro Feststellung.
    /// Punktfeststellungen: minLength (3.0m Kanaele, 0.5m Schaechte).
    /// Streckenfeststellungen: tatsaechliche Laenge wenn > minLength.
    /// </summary>
    internal static double ComputeLengthFactor(VsaFinding finding, double minLength)
    {
        double? actualLength = null;
        if (finding.MeterStart.HasValue && finding.MeterEnd.HasValue)
            actualLength = Math.Abs(finding.MeterEnd.Value - finding.MeterStart.Value);

        return actualLength.HasValue && actualLength.Value > minLength
            ? actualLength.Value
            : minLength;
    }

    // ── Randbedingungen (VSA Richtlinie 2023, Kap. 5.3, Tabellen 3-6) ──

    /// <summary>Berechnet Π(B_j) = B1 × B2 × B3 × B4.</summary>
    internal static double ComputeRandbedingungen(HaltungRecord record)
    {
        var b1 = ComputeB1(record.GetFieldValue("Gewaesserschutz"));
        var b2 = ComputeB2(record.GetFieldValue("Nutzungsart"));
        var b3 = ComputeB3(record.GetFieldValue("Grundwasserspiegel"));
        var b4 = ComputeB4(record.GetFieldValue("FunktionHierarchisch"));
        return b1 * b2 * b3 * b4;
    }

    // Tabelle 3: Gewaesser-/Grundwasserschutz
    private static double ComputeB1(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "S"  => 0.90,
        "AU" => 0.95,
        "ZU" => 0.95,
        "AO" => 0.95,
        _    => 1.00
    };

    // Tabelle 4: Nutzungsart. Zuerst auf den Normbegriff bringen, damit auch alte
    // Schreibweisen aus Bestandsprojekten denselben Faktor ergeben wie heute erfasste.
    private static double ComputeB2(string? value) => NutzungsartVokabular.Normalisieren(value) switch
    {
        "Bachwasser"             => 1.10,
        "Industrieabwasser"      => 0.90,
        "Schmutzabwasser"        => 0.95,
        "Mischabwasser"          => 1.00,
        "Niederschlagsabwasser"  => 1.05,
        _                        => 1.00
    };

    // Tabelle 5: Grundwasserspiegel
    private static double ComputeB3(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "unterhalb" => 0.90,
        "oberhalb"  => 1.10,
        _           => 1.00 // unbekannt
    };

    // Tabelle 6: Funktionale Hierarchie (PAA gemaess VSA-DSS)
    private static double ComputeB4(string? value) => value?.Trim() switch
    {
        "PAA.Hauptsammelkanal"          => 0.95,
        "PAA.Hauptsammelkanal_regional" => 0.90,
        "PAA.Liegenschaftsentwaesserung" or "PAA.Liegenschaftsentwässerung" => 1.10,
        "PAA.Sammelkanal"               => 1.00,
        "PAA.Sanierungsleitung"         => 1.00,
        "PAA.Strassenentwaesserung" or "PAA.Strassenentwässerung" => 1.00,
        "PAA.Gewaesser" or "PAA.Gewässer" => 1.00,
        _                               => 1.00
    };

    // ── Mappings ─────────────────────────────────────────────────────────

    /// <summary>ZN (0=schlecht, 4=gut) → Pruefungsresultat.</summary>
    internal static string BuildPruefungsresultat(double? note)
    {
        if (note is null)
            return "n/a";

        // ZN 0 = schlechtester Zustand, ZN 4 = bester Zustand
        if (note.Value >= 3.0)
            return "i.O.";
        if (note.Value >= 1.5)
            return "beobachten";
        return "Sanierungsbedarf";
    }

    /// <summary>DZ → Dringlichkeitsstufe (VSA Richtlinie, Tabelle 7).</summary>
    internal static string MapDringlichkeit(double? dz)
    {
        if (dz is null) return "n/a";
        return dz.Value switch
        {
            < 50  => "Sofort",
            < 150 => "Kurzfristig (3J)",
            < 250 => "Mittelfristig (8J)",
            < 350 => "Langfristig",
            _     => "Keine"
        };
    }

    internal static string MapZustandsklasse(double? note)
    {
        if (note is null)
            return "n/a";

        var value = (int)Math.Clamp(
            Math.Round(note.Value, MidpointRounding.AwayFromZero),
            min: 0,
            max: 4);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // ── Erklaerungsabschnitt ─────────────────────────────────────────────

    internal static void AppendRequirementSection(StringBuilder sb, VsaConditionResult result)
    {
        sb.AppendLine($"Anforderung {result.Requirement}:");
        sb.AppendLine($"  EZmin: {FmtEz(result.WorstEinzelzustand)}");
        sb.AppendLine($"  Abminderung A: {FmtNote(result.Abminderung)}");
        sb.AppendLine($"  Zustandsnote: {FmtNote(result.Zustandsnote)}");
        sb.AppendLine($"  Dringlichkeitszahl: {FmtNote(result.Dringlichkeitszahl)}");
        sb.AppendLine($"  Dringlichkeit: {MapDringlichkeit(result.Dringlichkeitszahl)}");
        if (result.Notes.Count > 0)
            sb.AppendLine($"  Hinweise: {string.Join("; ", result.Notes)}");
    }

    // ── Format-Hilfen (intern, kein IO) ──────────────────────────────────

    private static string FmtEz(int? ez)
        => ez is null ? "n/a" : ez.Value.ToString(CultureInfo.InvariantCulture);

    private static string FmtNote(double? value)
        => value is null ? "n/a" : value.Value.ToString("0.00", CultureInfo.InvariantCulture);

    // ── Code-Normalisierung ───────────────────────────────────────────────

    /// <summary>
    /// Normalisiert einen VSA-Schadencode: Whitespace, Sonderzeichen und Unterklassen entfernen,
    /// Grossbuchstaben. Beispiel: "BAJ.C @0.10m" → "BAJC".
    /// </summary>
    internal static string NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        var cutChars = new[] { ' ', '@', '(' };
        var idx = s.IndexOfAny(cutChars);
        if (idx >= 0)
            s = s.Substring(0, idx);

        s = Regex.Replace(s, @"[^A-Za-z0-9]+", string.Empty);
        return s.ToUpperInvariant();
    }
}
