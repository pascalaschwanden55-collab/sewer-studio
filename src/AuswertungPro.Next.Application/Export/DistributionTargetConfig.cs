namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Konfiguration einer Ziel-Ablage: physische Ziel-Wurzel plus drei Namens-/Ordner-Ebenen
/// (Ordner / Unterordner / Datei) als Platzhalter-Muster. Leere Ordner-Ebenen entfallen
/// (flache vs. tiefe Ablage). Wird pro Typ (Haltungen / Schaechte / Dichtheit) und fuer
/// den Excel-Export in <c>AppSettings</c> gehalten und von <see cref="IDistributionPatternResolver"/>
/// zu einem relativen Zielpfad aufgeloest.
/// </summary>
/// <remarks>
/// Bewusst eine veraenderliche Klasse (kein record): Die UI bindet zweiseitig direkt an die
/// Muster-Felder, und die Werte werden ueber den bestehenden JSON-Settings-Store persistiert.
/// </remarks>
public sealed class DistributionTargetConfig
{
    /// <summary>Physische Ziel-Wurzel (Basis-Pfad). Leer = beim Verteilen per Ordner-Dialog erfragen.</summary>
    public string? Root { get; set; }

    /// <summary>Muster fuer die 1. Ordnerebene; leer = Ebene entfaellt.</summary>
    public string OrdnerPattern { get; set; } = string.Empty;

    /// <summary>Muster fuer die 2. Ordnerebene; leer = Ebene entfaellt.</summary>
    public string UnterordnerPattern { get; set; } = string.Empty;

    /// <summary>Muster fuer den Dateinamen (ohne Endung).</summary>
    public string DateiPattern { get; set; } = string.Empty;
}
