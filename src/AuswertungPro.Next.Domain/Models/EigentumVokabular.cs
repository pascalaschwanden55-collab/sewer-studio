using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Der Eigentuemer einer Haltung oder eines Schachts — an einer Stelle.
///
/// Massgebend sind die amtlichen Begriffe des Kantons Uri. Im QGIS-Export Zone 1.17
/// stehen an den 295 Normschaechten "Privat" 204x, "Abwasser Uri" 68x und
/// "Kanton Uri" 17x; das Abwassernetz des Kantons liefert dieselben Begriffe.
///
/// Bis 2026-08-31 uebersetzte diese Klasse sie in die Kurzformen "AWU" und "Kanton",
/// weil die Excel-Vorlagen ihre Eigentuemerspalte ueber einen EXAKTEN Vergleich
/// faerben und nur diese fuenf Woerter kannten. Beide Vorlagen tragen jetzt je eine
/// Regel fuer den amtlichen Begriff UND fuer die Kurzform, und die Kennzahlen zaehlen
/// ueber beide. Damit gibt es keinen Grund mehr, einen eingelesenen Wert umzuschreiben.
/// Gespeicherte Kurzformen aus Altprojekten bleiben gueltig, gefaerbt und gezaehlt.
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
        // Der amtliche Begriff gewinnt. Frueher stand hier die Kurzform "AWU",
        // weil die Excel-Vorlage nur sie faerbte — seit 2026-08-31 faerbt und
        // zaehlt sie beide Schreibweisen, also darf der eingelesene Wert stehen
        // bleiben. Die Kurzformen werden weiter gelesen; Altprojekte behalten
        // ihren gespeicherten Wert unveraendert.
        new(["awu", "abwasser uri", "abwasser uri (awu)", "abwasseruri"], "Abwasser Uri"),
        new(["kanton", "kanton uri", "kanton_uri"], "Kanton Uri"),
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
