using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Reine Klassifikations-Logik fuer Placeholder-Eintraege und Fingerprints.
/// Liest nur aus HaltungRecord und Dictionary, mutiert nichts.
/// </summary>
internal static class PdfPlaceholderClassifier
{
    /// <summary>
    /// Erstellt einen stabilen Fingerprint aus den Inspektion-Feldern eines Datensatzes.
    /// Gibt null zurueck wenn keine Primaer-Schaeden vorhanden.
    /// </summary>
    internal static string? BuildRepairFingerprint(HaltungRecord r)
    {
        var damages = NormalizeForFingerprint(r.GetFieldValue("Primaere_Schaeden"));
        if (string.IsNullOrWhiteSpace(damages))
            return null;

        var dir = NormalizeForFingerprint(r.GetFieldValue("Inspektionsrichtung"));
        var use = NormalizeForFingerprint(r.GetFieldValue("Nutzungsart"));
        var dn  = NormalizeForFingerprint(r.GetFieldValue("DN_mm"));
        var len = NormalizeForFingerprint(r.GetFieldValue("Haltungslaenge_m"));
        var mat = NormalizeForFingerprint(r.GetFieldValue("Rohrmaterial"));

        return $"{damages}|{dir}|{use}|{dn}|{len}|{mat}";
    }

    /// <summary>
    /// Normalisiert einen Feldwert fuer den Fingerprint-Vergleich (Whitespace komprimiert, getrimmt).
    /// </summary>
    internal static string NormalizeForFingerprint(string? value)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0)
            return "";

        v = Regex.Replace(v, @"\s+", " ");
        return v;
    }

    /// <summary>
    /// Prueft ob ein Feldwert sinnvollen Text enthaelt (Buchstaben oder Ziffern).
    /// </summary>
    internal static bool HasMeaningfulText(string? value)
    {
        var v = NormalizeForFingerprint(value);
        if (string.IsNullOrWhiteSpace(v))
            return false;

        return Regex.IsMatch(v, @"[\p{L}\p{N}]");
    }

    /// <summary>
    /// Prueft ob ein Haltungsname ein bekannter Placeholder-Schluessel ist.
    /// </summary>
    internal static bool IsKnownPlaceholderKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        if (key.StartsWith("UNBEKANNT_", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsHeaderPlaceholderKey(key))
            return true;

        return key.Equals("Datum :", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Datum", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Haltungsname :", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Haltungsname", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Prueft ob ein Haltungsname ein Header-Placeholder-Schluessel ist
    /// (z.B. "Datum : Wetter : Operator :").
    /// </summary>
    internal static bool IsHeaderPlaceholderKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!Regex.IsMatch(key, @"(?i)^\s*(?:Haltungsname\s*:)?\s*Datum\s*:"))
            return false;

        return Regex.IsMatch(key, @"(?i)\bWetter\s*:") ||
               Regex.IsMatch(key, @"(?i)\bOperator\s*:") ||
               Regex.IsMatch(key, @"(?i)\bAuftrag\s*Nr\.?\s*:");
    }

    /// <summary>
    /// Prueft ob ein Chunk ohne bekannte Haltungs-ID uebersprungen werden soll
    /// (keine verwertbaren Inspektion-Felder und keine Haltungs-Zeile im Text).
    /// </summary>
    internal static bool ShouldSkipUnknownChunk(Dictionary<string, string> fields, PdfChunk chunk)
    {
        // Chunks mit nutzbaren Inspektionsdaten behalten.
        bool hasUsefulPayload =
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Primaere_Schaeden")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Inspektionsrichtung")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Nutzungsart")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("DN_mm")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Haltungslaenge_m"));

        if (hasUsefulPayload)
            return false;

        var text = chunk.Text ?? "";
        if (Regex.IsMatch(text, @"(?im)^\s*\d[\d\.]*\s*[-/]\s*\d[\d\.]*\s+\d{2}\.\d{2}\.\d{4}\b"))
            return false;

        return true;
    }
}
