using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Fehlende Angaben stehen im fertigen Dossier als „unbekannt" statt leer.
/// Eine leere Zelle laesst den Eigentuemer im Unklaren, ob die Angabe fehlt oder
/// ob sie vergessen wurde; die Wordvorlage zeigt an diesen Stellen ebenfalls
/// „unbekannt".
///
/// Die Menge ist bewusst eng und enthaelt NUR Zellen von Tabellen. Zwei Gruende:
/// <list type="bullet">
/// <item>Ein leeres Datum, eine leere Fusszeile oder eine leere Thementitelzeile
/// sind keine fehlenden Angaben, sondern schlicht nichts.</item>
/// <item>Die Klickzuordnung der Vorschau erkennt ein freies Feld an seinem TEXT.
/// Tragen mehrere Felder denselben Text, bleibt die Stelle bewusst ohne Treffer -
/// sonst wuerde geraten. „unbekannt" in einem freien Feld macht es also
/// unanklickbar. In einer Tabelle ist es dagegen sicher: dort loest der
/// Tabellenmapper die Zelle ueber Spalte und Zeilenband auf, nicht ueber den Text.</item>
/// </list>
/// Deshalb bleiben die Deckblattfelder leer - dort stand sonst zweimal „unbekannt"
/// in 40 Punkt als Titel eines Briefs an den Eigentuemer. Die schmalen
/// Zahlenspalten der Eigentuemertabelle bleiben ebenfalls leer, weil „unbekannt"
/// dort die Spalte sprengt.
/// </summary>
public static class DossierUnbekanntText
{
    /// <summary>Der Text, der eine fehlende Angabe sichtbar macht.</summary>
    public const string Unbekannt = "unbekannt";

    /// <summary>Die Felder, deren fehlender Wert als „unbekannt" erscheint.</summary>
    public static readonly IReadOnlyList<string> Felder =
    [
        "Text",              // Bemerkung eines Themas
        "Eigentuemer_Zelle", // Name in der Tabelle „Eigentumsverhaeltnisse"
        "Aktennotiz"
    ];

    /// <summary>
    /// Der sichtbare Wert eines einzelnen Feldes. Gehoert das Feld nicht zur Menge
    /// oben, bleibt der Wert unveraendert - auch wenn er leer ist.
    /// </summary>
    public static string OderUnbekannt(string feld, string? wert)
    {
        ArgumentNullException.ThrowIfNull(feld);

        if (!string.IsNullOrWhiteSpace(wert))
            return wert;

        foreach (var bekannt in Felder)
        {
            if (string.Equals(bekannt, feld, StringComparison.OrdinalIgnoreCase))
                return Unbekannt;
        }

        return wert ?? string.Empty;
    }

    /// <summary>
    /// Ersetzt leere Werte der oben genannten Felder durch „unbekannt". Alle
    /// uebrigen Eintraege bleiben unveraendert; fehlende Schluessel werden nicht
    /// erfunden.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Anwenden(
        IReadOnlyDictionary<string, string> werte)
    {
        ArgumentNullException.ThrowIfNull(werte);

        var ergebnis = new Dictionary<string, string>(werte, StringComparer.OrdinalIgnoreCase);
        foreach (var feld in Felder)
        {
            if (ergebnis.TryGetValue(feld, out var wert) && string.IsNullOrWhiteSpace(wert))
                ergebnis[feld] = Unbekannt;
        }

        return ergebnis;
    }
}
