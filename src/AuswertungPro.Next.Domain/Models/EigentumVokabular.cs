using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Der Eigentuemer einer Haltung oder eines Schachts — an einer Stelle.
///
/// Massgebend sind hier ausnahmsweise nicht die Norm, sondern die beiden ausgelieferten
/// Excel-Vorlagen. Sie faerben die Eigentuemerspalte ueber eine bedingte Formatierung mit
/// EXAKTEM Vergleich: <c>Haltungen.xlsx</c> Spalte O und <c>Schaechte.xlsx</c> Spalte J
/// pruefen je <c>="AWU"</c>, <c>="Kanton"</c>, <c>="Bund"</c>, <c>="Gemeinde"</c> und
/// <c>="Privat"</c>. Ein Wert daneben ist zwar sichtbar, bleibt aber farblos.
///
/// Die XTF schreibt andere Begriffe: Im QGIS-Export Zone 1.17 stehen an den 295
/// Normschaechten "Privat" 204x, "Abwasser Uri" 68x und "Kanton Uri" 17x. Ohne diese
/// Uebersetzung waere die Spalte gefuellt und zwei Drittel der Zeilen ohne Farbe — also
/// genau das, wofuer die Angabe gebraucht wird, kaputt.
///
/// <see cref="Costs.OwnershipAwuFilter"/> bleibt davon unberuehrt: Er erkennt sowohl
/// "AWU" als auch jeden Freitext mit "Abwasser Uri" und arbeitet nach der Uebersetzung
/// genauso weiter.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class EigentumVokabular
{
    /// <summary>
    /// Ein Eigentumsverhaeltnis mit allen Schreibweisen, die dafuer gelesen werden, und
    /// dem Begriff, den die Excel-Vorlage faerbt.
    /// </summary>
    private sealed record Konzept(string[] Gelesen, string App);

    private static readonly Konzept[] Konzepte =
    [
        // Abwasser Uri fuehrt sich in der XTF als "Abwasser Uri", Pascal im Projekt als
        // "AWU" — im Bestand 74 Haltungen und 5 Schaechte. Die Vorlage kennt nur "AWU".
        new(["awu", "abwasser uri", "abwasser uri (awu)", "abwasseruri"], "AWU"),
        new(["kanton", "kanton uri", "kanton_uri"], "Kanton"),
        new(["bund", "eidgenossenschaft"], "Bund"),
        new(["gemeinde", "einwohnergemeinde", "korporationsgemeinde"], "Gemeinde"),
        new(["privat", "private", "privatperson"], "Privat")
    ];

    /// <summary>
    /// Die Auswahl im Programm — leer plus genau die fuenf Werte, welche die Excel-Vorlage
    /// faerbt. Ein sechster Eintrag hier waere eine farblose Zelle dort.
    /// </summary>
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
        new[] { "" }
            .Concat(Konzepte.Select(k => k.App))
            .ToList());

    /// <summary>
    /// Bringt eine beliebige gelesene Schreibweise auf den Begriff des Programms.
    ///
    /// Ein unbekannter Wert bleibt unveraendert stehen. Eine Korporation oder
    /// Genossenschaft ist eine echte Angabe — sie zu loeschen waere schlimmer, als sie
    /// ohne Farbe stehen zu lassen.
    /// </summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return "";

        var klein = text.ToLowerInvariant();
        var treffer = Konzepte.FirstOrDefault(k =>
            k.Gelesen.Contains(klein)
            || string.Equals(k.App, text, StringComparison.OrdinalIgnoreCase));

        return treffer?.App ?? text;
    }
}
