namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Konfiguration einer Ziel-Ablage: physische Ziel-Wurzel, zwei optionale Ueberordner
/// und ein Dateinamensmuster. Bei der Verteilung bleiben der letzte Objektordner und
/// der Dateiname aus Sicherheitsgruenden fest; dort werden nur die beiden Ueberordner
/// verwendet. Beim Excel-Export wird nur das getrennte Dateinamensmuster verwendet.
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

    /// <summary>
    /// Muster fuer den Dateinamen (ohne Endung). Frei nutzbar bei Excel; bei der
    /// Verteilung nur zur Anzeige/Kompatibilitaet, weil der sichere Name fest bleibt.
    /// </summary>
    public string DateiPattern { get; set; } = string.Empty;
}
