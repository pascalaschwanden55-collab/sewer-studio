using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Export;

/// <summary>Werte eines Datensatzes fuer die Platzhalter-Aufloesung bei Verteilung/Export.</summary>
public sealed record DistributionPatternContext(
    DateTime? Datum = null,
    string? Gemeinde = null,
    string? Haltung = null,
    string? Schachtnummer = null);

/// <summary>
/// Loest konfigurierbare Namens-/Ordnermuster mit Platzhaltern (z.B. {Datum}_{Haltung}) auf
/// und setzt daraus einen dateisystemsicheren relativen Pfad (Ordner/Unterordner/Datei) zusammen.
/// Leere Ebenen werden weggelassen (flach vs. tief).
/// </summary>
public interface IDistributionPatternResolver
{
    /// <summary>Loest ein Muster auf (roher Text, unsanitisiert; leer wenn nichts uebrig bleibt).</summary>
    string ResolveSegment(string? pattern, DistributionPatternContext context);

    /// <summary>
    /// Baut den relativen Zielpfad: &lt;Ordner&gt;/&lt;Unterordner&gt;/&lt;Datei&gt;&lt;Endung&gt;.
    /// Leere Ordner-/Unterordner-Ebenen entfallen; ein leerer Dateiname faellt auf "unbenannt" zurueck.
    /// </summary>
    string ResolveRelativePath(
        string? ordnerPattern,
        string? unterordnerPattern,
        string? dateiPattern,
        DistributionPatternContext context,
        string extension);
}

public sealed class DistributionPatternResolver : IDistributionPatternResolver
{
    private static readonly Regex PlatzhalterRegex = new(@"\{([A-Za-z]+)\}", RegexOptions.Compiled);

    public string ResolveSegment(string? pattern, DistributionPatternContext context)
    {
        if (string.IsNullOrEmpty(pattern))
            return string.Empty;

        var werte = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Datum"] = context.Datum?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? string.Empty,
            ["Jahr"] = context.Datum?.ToString("yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
            ["Monat"] = context.Datum?.ToString("MM", CultureInfo.InvariantCulture) ?? string.Empty,
            ["Gemeinde"] = context.Gemeinde ?? string.Empty,
            ["Haltung"] = context.Haltung ?? string.Empty,
            ["Schachtnummer"] = context.Schachtnummer ?? string.Empty,
        };

        // Unbekannte Platzhalter werden zu leer (nicht als Literal stehen gelassen).
        return PlatzhalterRegex.Replace(
            pattern,
            m => werte.TryGetValue(m.Groups[1].Value, out var v) ? v : string.Empty);
    }

    public string ResolveRelativePath(
        string? ordnerPattern,
        string? unterordnerPattern,
        string? dateiPattern,
        DistributionPatternContext context,
        string extension)
    {
        var segmente = new List<string>();

        var ordner = SanitizeOptional(ResolveSegment(ordnerPattern, context));
        if (ordner is not null)
            segmente.Add(ordner);

        var unterordner = SanitizeOptional(ResolveSegment(unterordnerPattern, context));
        if (unterordner is not null)
            segmente.Add(unterordner);

        var dateiRoh = ResolveSegment(dateiPattern, context);
        var dateiSegment = string.IsNullOrWhiteSpace(dateiRoh)
            ? "unbenannt"
            : ProjectPathResolver.SanitizePathSegment(dateiRoh);

        segmente.Add(dateiSegment + NormalizeExtension(extension));

        return Path.Combine(segmente.ToArray());
    }

    /// <summary>Sanitisiert ein Ordner-Segment; null, wenn nach Aufloesung leer (Ebene entfaellt).</summary>
    private static string? SanitizeOptional(string aufgeloest)
        => string.IsNullOrWhiteSpace(aufgeloest)
            ? null
            : ProjectPathResolver.SanitizePathSegment(aufgeloest);

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return string.Empty;
        return extension.StartsWith('.') ? extension : "." + extension;
    }
}
