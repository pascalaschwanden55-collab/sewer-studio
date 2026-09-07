using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3): Die vier nur lesenden Statusspalten der Haltungstabelle.
///
/// Sie sind KEINE Felder: Sie stehen nicht im <c>FieldCatalog</c>, gehen in keinen Export und
/// werden nie als Feld im gespeicherten Spaltenlayout abgelegt. Ihre Schluessel dienen allein
/// der Wiedererkennung in der Tabelle (Spalten-<c>Tag</c>), damit der
/// <see cref="DataPageColumnViewController"/> sie wie jede andere Spalte ein- und ausblenden
/// kann. Ein Schluessel traegt deshalb bewusst das Praefix <c>Nova_</c> und kann nicht mit
/// einem echten Feldnamen kollidieren.
/// </summary>
public static class NovaStatusSpalten
{
    public const string Ki = "Nova_KI";
    public const string Pruefung = "Nova_Pruefung";
    public const string Video = "Nova_Video";
    public const string Protokoll = "Nova_Protokoll";

    /// <summary>Die vier Schluessel in Anzeigereihenfolge (KI, Pruefung, Video, Protokoll).</summary>
    public static IReadOnlyList<string> Alle { get; } = [Ki, Pruefung, Video, Protokoll];

    /// <summary>
    /// Ist <paramref name="feld"/> eine virtuelle Statusspalte? Layout-Speicherung und
    /// -Wiederherstellung muessen solche Spalten ueberspringen, sonst landete eine Spalte ohne
    /// Feld als vermeintliches Feld in <c>settings.json</c>.
    /// </summary>
    public static bool IstVirtuell(string? feld)
        => feld is not null && feld.StartsWith("Nova_", StringComparison.Ordinal);
}
