using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>Wie der Import mit dem eigenen Ergebnis weiterfaehrt.</summary>
public enum PlausibilitaetsStufe
{
    /// <summary>Ergebnis passt zu den Quellen — weiter ohne Rueckfrage.</summary>
    Gruen = 0,

    /// <summary>
    /// Mengenabweichung. Darf uebersteuert werden, Vorbelegung ist aber Abbrechen.
    /// </summary>
    Rueckfrage = 1,

    /// <summary>
    /// Keine einzige lesbare Quelle. Kein Uebersteuern — es gibt nichts zu uebernehmen.
    /// </summary>
    HartAbbruch = 2
}

/// <summary>Urteil des Stopptors samt Begruendung fuer den Benutzer und den Bericht.</summary>
public sealed record PlausibilitaetsUrteil(
    PlausibilitaetsStufe Stufe,
    string Begruendung,
    IReadOnlyList<string> Quellenzeilen,
    string Fingerabdruck)
{
    public static PlausibilitaetsUrteil Gruen { get; } =
        new(PlausibilitaetsStufe.Gruen, "", Array.Empty<string>(), "");

    public bool BrauchtRueckfrage => Stufe == PlausibilitaetsStufe.Rueckfrage;

    /// <summary>
    /// Ehrlicher Abbruchtext. Bewusst NICHT "nichts veraendert": Vor dem Tor koennen
    /// Wiederherstellungspunkt, Arbeitsdateien im Staging und der Importbericht bereits
    /// entstanden sein.
    /// </summary>
    public const string AbbruchHinweis =
        "Keine Projektdaten und keine Importdateien uebernommen.";

    public string VollerText()
    {
        var sb = new StringBuilder();
        sb.Append(Begruendung);
        if (Quellenzeilen.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Gepruefte Quellen:");
            foreach (var zeile in Quellenzeilen)
                sb.AppendLine("  " + zeile);
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Prueft vor der Uebernahme, ob das Importergebnis zu den gefundenen Quellen passt.
///
/// Anlass: Am 2026-08-21 meldete der Import "erfolgreich" bei null Haltungen, obwohl
/// fuenf lesbare WinCan-Datenbanken im Ordner lagen. Genau dieser Widerspruch muss
/// auffallen, BEVOR Dateien veroeffentlicht werden.
///
/// Reines Urteil: kein Datei-, Datenbank- oder Netzzugriff, keine KI. Dadurch laeuft der
/// Weg auch bei ausgeschaltetem Ollama und auf schwacher Hardware.
/// </summary>
public static class ImportPlausibilitaetsTor
{
    /// <param name="quellen">Protokoll der Quellenwahl. Leer = dieses Tor urteilt nicht.</param>
    /// <param name="bearbeiteteHaltungen">
    /// Wirklich verarbeitete Haltungen — ausdruecklich NICHT <c>ImportStats.Found</c>:
    /// dieser Wert zaehlt bei WinCan auch Schaechte mit (gemessen 44 bei 15 Haltungen
    /// und 26 Schaechten) und ist als Pruefgroesse unbrauchbar.
    /// </param>
    public static PlausibilitaetsUrteil Beurteile(
        QuellenwahlErgebnis? quellen,
        int bearbeiteteHaltungen)
    {
        if (quellen is null || quellen.AlleVersuche.Count == 0)
            return PlausibilitaetsUrteil.Gruen;

        var zeilen = quellen.AlleVersuche.Select(v => v.Berichtszeile(Dateiname)).ToList();
        var tauglich = quellen.Anzahl(QuellenTauglichkeit.Tauglich);
        var leer = quellen.Anzahl(QuellenTauglichkeit.Leer);
        var untauglich = quellen.Anzahl(QuellenTauglichkeit.Untauglich);
        var erwartet = quellen.ErwarteteMenge;
        var fingerabdruck = BerechneFingerabdruck(quellen, bearbeiteteHaltungen);

        // Keine einzige lesbare Quelle: es gibt nichts zu uebernehmen.
        if (tauglich == 0 && leer == 0)
        {
            return new PlausibilitaetsUrteil(
                PlausibilitaetsStufe.HartAbbruch,
                $"{untauglich} Quelle(n) gefunden, aber keine davon ist lesbar oder enthaelt "
                + "Haltungsdaten. " + PlausibilitaetsUrteil.AbbruchHinweis,
                zeilen,
                fingerabdruck);
        }

        // Alles lesbar, aber ohne Datensaetze: gueltiger leerer Projektstand.
        if (erwartet == 0)
            return PlausibilitaetsUrteil.Gruen with { Fingerabdruck = fingerabdruck };

        if (bearbeiteteHaltungen <= 0)
        {
            return new PlausibilitaetsUrteil(
                PlausibilitaetsStufe.Rueckfrage,
                $"{tauglich} Quelle(n) mit zusammen {erwartet} Haltung(en) gelesen, "
                + "aber keine einzige Haltung uebernommen. Das kann nicht stimmen.",
                zeilen,
                fingerabdruck);
        }

        if (bearbeiteteHaltungen < erwartet)
        {
            var fehlend = erwartet - bearbeiteteHaltungen;
            return new PlausibilitaetsUrteil(
                PlausibilitaetsStufe.Rueckfrage,
                $"{erwartet} Haltung(en) in den Quellen, aber nur {bearbeiteteHaltungen} "
                + $"uebernommen — {fehlend} fehlen.",
                zeilen,
                fingerabdruck);
        }

        return PlausibilitaetsUrteil.Gruen with { Fingerabdruck = fingerabdruck };
    }

    /// <summary>
    /// Bindet eine Zustimmung an die geprueften Quellen und die Zahlen.
    ///
    /// Vorschau und Echtlauf sind zwei getrennte Laeufe. Ohne diesen Abgleich wuerde der
    /// Benutzer entweder zweimal gefragt, oder eine Zustimmung aus der Vorschau wuerde
    /// auch dann gelten, wenn der Echtlauf inzwischen etwas voellig anderes vorfindet.
    /// </summary>
    public static bool ZustimmungGiltNoch(string? zugestimmterFingerabdruck, PlausibilitaetsUrteil urteil)
    {
        ArgumentNullException.ThrowIfNull(urteil);
        return !string.IsNullOrEmpty(zugestimmterFingerabdruck)
               && string.Equals(zugestimmterFingerabdruck, urteil.Fingerabdruck, StringComparison.Ordinal);
    }

    private static string BerechneFingerabdruck(QuellenwahlErgebnis quellen, int bearbeiteteHaltungen)
    {
        var sb = new StringBuilder();
        sb.Append(bearbeiteteHaltungen.ToString(CultureInfo.InvariantCulture)).Append('|');

        // Nach Pfad sortiert, damit die Reihenfolge der Pruefung den Wert nicht veraendert.
        foreach (var v in quellen.AlleVersuche.OrderBy(v => v.Pfad, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(v.Pfad.ToUpperInvariant()).Append('#')
              .Append((int)v.Befund.Tauglichkeit).Append('#')
              .Append(v.Befund.Menge.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }

    private static string Dateiname(string pfad)
    {
        // Bewusst ohne System.IO: reine Zeichenkette, damit dieser Weg keine Dateisystem-
        // Abhaengigkeit bekommt.
        var trenner = pfad.LastIndexOfAny(new[] { '\\', '/' });
        return trenner >= 0 && trenner < pfad.Length - 1 ? pfad[(trenner + 1)..] : pfad;
    }
}
