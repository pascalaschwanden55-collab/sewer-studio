using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.Import.Quellen;

/// <summary>
/// Wie brauchbar eine gepruefte Importquelle ist.
///
/// Die Trennung von <see cref="Leer"/> und <see cref="Untauglich"/> ist wesentlich:
/// Ein lesbarer, aber noch leerer Projektstand ist ein gueltiger Zustand und darf nicht
/// wie ein Defekt behandelt werden.
/// </summary>
public enum QuellenTauglichkeit
{
    /// <summary>Nicht lesbar oder ohne die erwartete Datenstruktur (z. B. eine Metadatenbank).</summary>
    Untauglich = 0,

    /// <summary>Lesbar und strukturell richtig, enthaelt aber keine Datensaetze.</summary>
    Leer = 1,

    /// <summary>Lesbar, strukturell richtig und mit mindestens einem Datensatz.</summary>
    Tauglich = 2
}

/// <summary>Ergebnis der kurzen Pruefung einer einzelnen Quelle.</summary>
/// <param name="Tauglichkeit">Einstufung der Quelle.</param>
/// <param name="Grund">Kurzer Klartext fuer den Importbericht.</param>
/// <param name="Menge">Gefundene Datensaetze (bei WinCan: Haltungen). Nie negativ.</param>
/// <param name="ErkanntAlsQuelle">
/// Ist das ueberhaupt eine Quelle dieser Art — unabhaengig davon, ob sie gerade nutzbar ist?
///
/// Die Unterscheidung ist noetig, weil Erkennung und Auswahl zwei verschiedene Fragen
/// beantworten. Eine WinCan-Datenbank, die gerade im LightViewer geoeffnet und damit
/// gesperrt ist, BEWEIST weiterhin einen WinCan-Export (true) — auch wenn daraus nichts
/// gelesen werden kann. Eine "*_Meta.db3" ist dagegen nie eine Datenquelle (false).
/// </param>
public sealed record QuellenBefund(
    QuellenTauglichkeit Tauglichkeit,
    string Grund,
    int Menge = 0,
    bool ErkanntAlsQuelle = true)
{
    public static QuellenBefund Tauglich(int menge, string grund)
        => new(QuellenTauglichkeit.Tauglich, grund, Math.Max(0, menge));

    public static QuellenBefund Leer(string grund)
        => new(QuellenTauglichkeit.Leer, grund);

    /// <summary>Gehoert nicht zu dieser Quellenart (z. B. eine Metadatenbank).</summary>
    public static QuellenBefund Untauglich(string grund)
        => new(QuellenTauglichkeit.Untauglich, grund, 0, ErkanntAlsQuelle: false);

    /// <summary>Richtige Quellenart, aber gerade nicht lesbar (gesperrt, defekt).</summary>
    public static QuellenBefund NichtLesbar(string grund)
        => new(QuellenTauglichkeit.Untauglich, grund, 0, ErkanntAlsQuelle: true);
}

/// <summary>Eine gepruefte Quelle samt Befund — eine Zeile des Importberichts.</summary>
public sealed record QuellenVersuch(string Pfad, QuellenBefund Befund)
{
    /// <summary>Zeile fuer den Importbericht, z. B. "projekt_Meta.db3 — keine Haltungstabelle".</summary>
    public string Berichtszeile(Func<string, string>? kurzname = null)
        => $"{(kurzname is null ? Pfad : kurzname(Pfad))} — {Befund.Grund}";
}

/// <summary>Ergebnis der Quellenwahl: der Gewinner und das Protokoll aller Versuche.</summary>
public sealed record QuellenwahlErgebnis(
    QuellenVersuch? Gewinner,
    IReadOnlyList<QuellenVersuch> AlleVersuche)
{
    public static QuellenwahlErgebnis Leer { get; } = new(null, Array.Empty<QuellenVersuch>());

    /// <summary>Summe der Datensaetze aller tauglichen Quellen — die erwartete Menge.</summary>
    public int ErwarteteMenge
        => AlleVersuche
            .Where(v => v.Befund.Tauglichkeit == QuellenTauglichkeit.Tauglich)
            .Sum(v => v.Befund.Menge);

    public int Anzahl(QuellenTauglichkeit stufe)
        => AlleVersuche.Count(v => v.Befund.Tauglichkeit == stufe);

    /// <summary>
    /// Fuer die FORMATERKENNUNG: der beste Kandidat, auch wenn er gerade nicht lesbar ist.
    ///
    /// Der Import benutzt weiterhin <see cref="Gewinner"/> und arbeitet nur mit einer
    /// nachweislich brauchbaren Datei. Gibt es einen Gewinner, liefern beide dieselbe
    /// Datei — sie koennen also nicht auseinanderlaufen. Nur wenn ueberhaupt nichts
    /// lesbar ist, erkennt das Format trotzdem noch die Quellenart, waehrend der Import
    /// ehrlich meldet, dass er nichts lesen konnte.
    /// </summary>
    public QuellenVersuch? BesterErkannter
        => Gewinner ?? AlleVersuche
            .Where(v => v.Befund.ErkanntAlsQuelle)
            .OrderBy(v => v.Pfad, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
}

/// <summary>
/// Waehlt aus mehreren Kandidaten die tatsaechlich brauchbare Importquelle.
///
/// Der Kern ist bewusst schlicht: <b>alle</b> Kandidaten kurz anfassen, dann den besten
/// nehmen — statt einen nach Bauchgefuehl zu raten. Die Auswahl erfolgt ausdruecklich
/// NICHT nach Dateigroesse: Genau diese Regel liess den WinCan-Import am 2026-08-21
/// immer die 6,8 MB grosse "*_Meta.db3" oeffnen statt der 1,2 MB grossen Datendatei und
/// null Haltungen importieren.
///
/// Diese Klasse fasst selbst keine Datei an. Die Pruefung wird als Delegate
/// hereingereicht und lebt in Infrastructure — dieselbe Trennung wie beim
/// PdfKiSchiedsrichter. Dadurch braucht dieser Weg weder KI noch GPU noch Sidecar und
/// laeuft auch auf schwacher Hardware.
/// </summary>
public static class Quellenwahl
{
    /// <param name="kandidaten">Alle in Frage kommenden Pfade.</param>
    /// <param name="pruefe">
    /// Kurzer Griff auf einen Kandidaten. Muss guenstig sein (bei SQLite: ein Blick ins
    /// Inhaltsverzeichnis), darf keine Ausnahme werfen — ein Fehler wird als
    /// <see cref="QuellenTauglichkeit.Untauglich"/> behandelt und stoppt die uebrigen nicht.
    /// </param>
    public static QuellenwahlErgebnis Waehle(
        IEnumerable<string> kandidaten,
        Func<string, QuellenBefund> pruefe)
    {
        ArgumentNullException.ThrowIfNull(pruefe);
        if (kandidaten is null)
            return QuellenwahlErgebnis.Leer;

        var versuche = new List<QuellenVersuch>();
        foreach (var pfad in kandidaten)
        {
            if (string.IsNullOrWhiteSpace(pfad))
                continue;

            QuellenBefund befund;
            try
            {
                befund = pruefe(pfad) ?? QuellenBefund.NichtLesbar("Pruefung ohne Ergebnis.");
            }
            catch (Exception ex)
            {
                // Ein einzelner kaputter Kandidat darf die uebrigen nie blockieren.
                befund = QuellenBefund.NichtLesbar($"Pruefung fehlgeschlagen: {ex.Message}");
            }

            versuche.Add(new QuellenVersuch(pfad, befund));
        }

        if (versuche.Count == 0)
            return QuellenwahlErgebnis.Leer;

        // Stabile Reihenfolge: erst Tauglichkeit, dann Menge, zuletzt der Pfad.
        // Der Pfad als letztes Kriterium sorgt dafuer, dass derselbe Ordner zweimal
        // dasselbe Ergebnis liefert — ein Import muss reproduzierbar sein.
        var gewinner = versuche
            .OrderByDescending(v => v.Befund.Tauglichkeit)
            .ThenByDescending(v => v.Befund.Menge)
            .ThenBy(v => v.Pfad, StringComparer.OrdinalIgnoreCase)
            .First();

        return new QuellenwahlErgebnis(
            gewinner.Befund.Tauglichkeit == QuellenTauglichkeit.Untauglich ? null : gewinner,
            versuche);
    }
}
