using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Uebersetzt die festen Farbwerte unserer SVG-Bauer in Theme-Token. Die Bauer erzeugen
/// druckfertige Grafiken mit festen Hex-Werten (weisses Blatt, graue Linien, rote Schaeden);
/// im Programm muessen dieselben Grafiken im hellen und im dunklen Design lesbar bleiben.
///
/// Zwei Wege, weil WPF nur einen davon dynamisch halten kann:
/// <list type="bullet">
/// <item><see cref="Setze"/> haengt eine Form ueber <c>SetResourceReference</c> an den Token —
/// ein Themenwechsel wirkt sofort.</item>
/// <item><see cref="Pinsel"/> liest denselben Token einmalig aus, weil Verlaufsstufen und
/// Musterinhalte einen fertigen Pinsel brauchen (ein <see cref="Freezable"/> kennt keine
/// Ressourcensuche).</item>
/// </list>
/// Ausser dieser Tabelle steht im Zeichner keine Farbe.
/// </summary>
public static class SvgFarbZuordnung
{
    /// <summary>Token, wenn eine Farbe nicht bekannt ist: sichtbar, aber ohne falsche Aussage.</summary>
    public const string RueckfallToken = "TextBrush";

    private static readonly IReadOnlyDictionary<string, string> Token = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Blatt und Flaechen
        ["WHITE"] = "CardBrush",
        ["#FFFFFF"] = "CardBrush",
        ["#F5F5F5"] = "BgLightBrush",
        ["#E5E7EB"] = "BgLightBrush",
        // Linien und Raender
        ["#D1D5DB"] = "BorderBrush",
        ["#9CA3AF"] = "FaintBrush",
        ["#6B7280"] = "MutedBrush",
        ["#4B5563"] = "MutedBrush",
        ["#8B7355"] = "MutedBrush",       // Erdreich-Schraffur
        ["#4A5568"] = "TextSecondaryBrush",
        // Schrift
        ["#1F2937"] = "TextBrush",
        ["#111827"] = "TextBrush",
        // Marke und Wasser
        ["#006E9C"] = "AccentBrush",      // Standard-Marke der Bauer
        ["#1F6FEB"] = "AccentBrush",
        ["#2196F3"] = "AccentBrush",
        ["#1565C0"] = "AccentTextBrush",
        // Schadensfarben des DamageSymbolClassifier
        ["#D64541"] = "DangerBrush",
        ["#E67E22"] = "Severity4Brush",
        ["#27AE60"] = "SuccessBrush",
        ["#8B6914"] = "WarningBrush"
    };

    // Zustandsklassen Z0-Z4 sind eine eigene, unveraenderte Skala (Excel-Vorlage). Sie sind
    // in beiden Designs gleich und kommen deshalb als fertiger Pinsel, nicht als Token.
    private static readonly IReadOnlyDictionary<string, string> Zustandsklassen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["#FF0000"] = "0",
        ["#FF6600"] = "1",
        ["#FFFF00"] = "2",
        ["#AEB135"] = "3",
        ["#92D050"] = "4"
    };

    /// <summary>Alle bekannten Farbwerte (fuer Waechter und Fehlermeldungen).</summary>
    public static IReadOnlyCollection<string> BekannteFarben
    {
        get
        {
            var alle = new List<string>(Token.Keys);
            alle.AddRange(Zustandsklassen.Keys);
            return alle;
        }
    }

    /// <summary>Normalisiert einen SVG-Farbwert auf die Schreibweise der Tabelle.</summary>
    public static string Normalisiere(string? farbe)
        => (farbe ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>True, wenn die Farbe in der Tabelle steht (kein Rueckfall noetig).</summary>
    public static bool IstBekannt(string? farbe)
    {
        var key = Normalisiere(farbe);
        return Token.ContainsKey(key) || Zustandsklassen.ContainsKey(key);
    }

    /// <summary>Theme-Token einer Farbe; <c>null</c> bei den festen Zustandsklassen.</summary>
    public static string? TokenFuer(string? farbe)
    {
        var key = Normalisiere(farbe);
        if (Zustandsklassen.ContainsKey(key))
            return null;
        return Token.TryGetValue(key, out var token) ? token : RueckfallToken;
    }

    /// <summary>
    /// Bindet eine Formeigenschaft (Fill/Stroke) dynamisch an den Token der Farbe. Ein
    /// Themenwechsel wirkt dadurch ohne Neuzeichnen.
    /// </summary>
    public static void Setze(FrameworkElement ziel, DependencyProperty eigenschaft, string? farbe)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(eigenschaft);

        var key = Normalisiere(farbe);
        if (Zustandsklassen.TryGetValue(key, out var klasse)
            && ZustandsklasseColorPalette.HaltungenPalette.TryGetValue(klasse, out var fest))
        {
            ziel.SetValue(eigenschaft, fest);
            return;
        }

        ziel.SetResourceReference(eigenschaft, TokenFuer(key) ?? RueckfallToken);
    }

    /// <summary>
    /// Fertiger Pinsel fuer Verlaufsstufen und Musterinhalte. <paramref name="quelle"/> ist das
    /// Element, ueber das die Ressourcensuche laeuft (die Zeichenflaeche).
    /// </summary>
    public static Brush Pinsel(FrameworkElement quelle, string? farbe)
    {
        ArgumentNullException.ThrowIfNull(quelle);

        var key = Normalisiere(farbe);
        if (Zustandsklassen.TryGetValue(key, out var klasse)
            && ZustandsklasseColorPalette.HaltungenPalette.TryGetValue(klasse, out var fest))
        {
            return fest;
        }

        var token = TokenFuer(key) ?? RueckfallToken;
        if (quelle.TryFindResource(token) is Brush pinsel)
            return pinsel;
        if (quelle.TryFindResource(RueckfallToken) is Brush rueckfall)
            return rueckfall;

        // Ohne geladenes Theme (Rohtest ohne Ressourcen) bleibt die Grafik sichtbar.
        return SystemColors.ControlTextBrush;
    }
}
