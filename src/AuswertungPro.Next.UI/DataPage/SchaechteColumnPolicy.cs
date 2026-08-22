using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Ordnet frei benannte Schachtspalten den bekannten Anzeige- und Eingabetypen zu.
/// Die WPF-Seite baut damit nur noch Spalten und Dialoge auf.
/// </summary>
internal static class SchaechteColumnPolicy
{
    public static bool TryResolveDropdownColumnSpec(string columnName, out GridDropdownFieldSpec spec)
    {
        var optionField = ResolveOptionField(columnName);
        if (optionField is not null && GridDropdownFieldPolicy.TryResolve(optionField, out spec))
            return true;

        spec = null!;
        return false;
    }

    public static string? ResolveOptionField(string columnName)
    {
        var normalized = Normalize(columnName);

        if (normalized.Contains("schachtform", StringComparison.Ordinal)
            || string.Equals(normalized, "form", StringComparison.Ordinal))
            return "Schachtform";

        // Vor der Zustandsklasse pruefen: beide Namen enden auf "klasse".
        // Excel-Kopfzeilen tragen oft einen Umbruch oder Trennstrich
        // ("Belastungs-\nklasse"); fuer den Vergleich faellt beides weg.
        var ohneTrenner = normalized
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        if (ohneTrenner.Contains("belastungsklasse", StringComparison.Ordinal))
            return FieldKeys.LoadClass;

        if ((normalized.Contains("ausgefuehrt", StringComparison.Ordinal)
             || normalized.Contains("ausgefuhrt", StringComparison.Ordinal))
            && normalized.Contains("durch", StringComparison.Ordinal))
            return "Ausgefuehrt_durch";

        if (normalized.Contains("eigentuemer", StringComparison.Ordinal)
            || normalized.Contains("eigentumer", StringComparison.Ordinal)
            || normalized.Contains("eigentum", StringComparison.Ordinal))
            return "Eigentuemer";

        if (normalized.Contains("referenz", StringComparison.Ordinal)
            && normalized.Contains("pruefung", StringComparison.Ordinal))
            return "Referenzpruefung";

        var compact = normalized
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal)
            .Trim();
        while (compact.Contains("  ", StringComparison.Ordinal))
            compact = compact.Replace("  ", " ", StringComparison.Ordinal);
        if (compact.Equals("ja nein", StringComparison.Ordinal))
            return "Sanieren_JaNein";

        if (normalized.Contains("sanieren", StringComparison.Ordinal)
            || (normalized.Contains("sanierung", StringComparison.Ordinal)
                && normalized.Contains("ja", StringComparison.Ordinal)))
            return "Sanieren_JaNein";

        if (normalized.Contains("pruefung", StringComparison.Ordinal)
            || normalized.Contains("dichtheit", StringComparison.Ordinal)
            || normalized.Contains("dichtigkeit", StringComparison.Ordinal))
            return "Pruefungsresultat";

        return null;
    }

    public static string GetDisplayHeader(string columnName)
        => string.Equals(ResolveOptionField(columnName), "Sanieren_JaNein", StringComparison.Ordinal)
            ? "Sanieren Ja/Nein"
            : columnName;

    public static bool IsCostColumn(string columnName)
        => Normalize(columnName).Contains("kosten", StringComparison.Ordinal);

    public static bool IsZustandsklasseColumn(string columnName)
        => Normalize(columnName).Contains("zustandsklasse", StringComparison.Ordinal);

    public static bool IsPrimaryDamagesColumn(string header)
    {
        var normalized = Normalize(header);
        return normalized.Contains("primaere", StringComparison.Ordinal)
               && normalized.Contains("schaeden", StringComparison.Ordinal);
    }

    public static bool IsDetailsNameColumn(string header)
    {
        var normalized = Normalize(header);
        return normalized.Contains("schacht", StringComparison.Ordinal)
               && (normalized.Contains("name", StringComparison.Ordinal)
                   || normalized.Contains("nummer", StringComparison.Ordinal));
    }

    public static string GetSchachtNumber(SchachtRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var byName = record.GetFieldValue("Schachtnummer");
        if (!string.IsNullOrWhiteSpace(byName))
            return byName.Trim();

        var byNr = record.GetFieldValue("Nr.");
        if (!string.IsNullOrWhiteSpace(byNr))
            return byNr.Trim();

        return record.GetFieldValue("NR.").Trim();
    }

    public static string ResolveSchachtDetailGroup(string columnName)
    {
        var normalized = Normalize(columnName);

        if (ContainsAny(normalized, "kosten", "sanier", "renovierung", "reparatur", "erneuerung", "anschluss"))
            return "Sanierung und Kosten";

        if (ContainsAny(normalized, "pdf", "link", "video", "film", "datei"))
            return "Dokumente und Medien";

        if (ContainsAny(normalized, "zustand", "schaden", "pruefung", "dicht", "referenz", "gewaesser", "grundwasser"))
            return "Zustand und Inspektion";

        if (ContainsAny(
                normalized,
                "schacht", "nummer", "name", "nr", "funktion", "strasse", "lage", "ort",
                "material", "dn", "durchmesser", "dimension", "tiefe", "form",
                "eigentuem", "eigentum"))
            return "Stammdaten";

        return "Weitere Angaben";
    }

    public static string Normalize(string value)
        => SchaechteFieldLogic.NormalizeKey(value);

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.Ordinal));
}
