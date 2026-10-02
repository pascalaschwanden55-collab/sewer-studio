using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Findet zu einem gemeinten Schachtfeld den Namen, unter dem ein Datensatz es
/// wirklich fuehrt.
///
/// Schachtfelder heissen nicht nach einem Katalog, sondern nach der Kopfzeile der
/// Excel-Vorlage: Die Tabelle bindet auf <c>Fields[&lt;Spaltentext&gt;]</c>. Dadurch
/// steht der Eigentuemer dort unter <c>Eigentümer</c> — mit Umlaut —, waehrend der
/// XTF-Import und das Nachfuellen aus QGIS <c>Eigentuemer</c> schreiben. Beides
/// nebeneinander heisst: Der sichtbare Wert bleibt leer, und der geschriebene ist
/// nirgends zu sehen.
///
/// <see cref="Feld"/> loest das, indem es zuerst im Datensatz nachsieht. Nur wenn er
/// das Feld noch gar nicht kennt, gilt der uebergebene Name.
///
/// Die Faltung ist bewusst schlicht und nur fuer den Vergleich: Umlaute aufgeloest,
/// Trennzeichen weg, klein. Der gefundene Name wird unveraendert zurueckgegeben.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class SchachtFeldnamen
{
    /// <summary>
    /// Der Feldname, den <paramref name="record"/> fuer das gemeinte Feld benutzt.
    /// Kennt er keinen passenden, kommt <paramref name="gemeint"/> zurueck.
    ///
    /// Bei mehreren Treffern gewinnt der zuerst gefundene mit Inhalt — sonst der
    /// erste ueberhaupt. So wird ein bereits gefuelltes Feld nicht durch eine leere
    /// Zweitschreibweise verdeckt.
    ///
    /// Bewusst nur ueber <see cref="Falte"/>, ohne Mojibake-Rueckrechnung: Schreiber holen
    /// sich hier ihr Ziel, und ein Wert soll nicht in einen kaputten Namen geschrieben werden,
    /// den die Tabelle nicht zeigt. Lesen ueber alle Schreibweisen: <see cref="Wert"/>.
    /// </summary>
    public static string Feld(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);

        var gesucht = Falte(gemeint);
        if (gesucht.Length == 0)
            return gemeint;

        string? ersterTreffer = null;

        foreach (var vorhanden in PassendeSchreibweisen(record, gesucht))
        {
            ersterTreffer ??= vorhanden;

            if (!string.IsNullOrWhiteSpace(record.Fields[vorhanden]))
                return vorhanden;
        }

        return ersterTreffer ?? gemeint;
    }

    /// <summary>
    /// Alle Namen, unter denen <paramref name="record"/> dasselbe Feld fuehrt — auch die
    /// bekannten Mojibake-Schreibweisen («PrimÃ¤re SchÃ¤den», siehe <see cref="Gruppenschluessel"/>).
    /// Bei einem sauberen Datensatz ist das genau einer.
    /// </summary>
    public static IReadOnlyList<string> Schreibweisen(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);

        var gesucht = Gruppenschluessel(gemeint);
        if (gesucht.Length == 0)
            return new List<string>();

        return record.Fields.Keys
            .Where(vorhanden => string.Equals(Gruppenschluessel(vorhanden), gesucht, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Der Wert des gemeinten Feldes ueber alle Schreibweisen nach <see cref="AktuellerWert"/>:
    /// Handwert (auch bewusst leer) vor Importwert, darin der juengste. Fuer Leser wie den
    /// XTF-Export; vorher lasen sie die erste Schreibweise mit Inhalt und zeigten so einen alten
    /// Importwert neben einer bewusst geleerten Handkorrektur (Review PR #80).
    /// </summary>
    public static string Wert(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);
        return AktuellerWert(record.Fields, record.FieldMeta, Schreibweisen(record, gemeint));
    }

    /// <summary>
    /// Vergleichsschluessel einer Schreibweisen-Gruppe: bekannte Mojibake-Schreibweisen
    /// (UTF-8 als CP1252 gelesen, auch doppelt) werden mit
    /// <see cref="SchachtFeldnamenReparatur.Entwirre"/> zurueckgerechnet, dann gefaltet. Ohne das
    /// erkannte die Handwert-Sperre einen Handwert unter «PrimÃ¤re SchÃ¤den» nicht, und SchachtPro,
    /// XTF oder KINS schrieben weiter unter «Primäre Schäden» (Review PR #80).
    /// </summary>
    public static string Gruppenschluessel(string? name)
        => Falte(SchachtFeldnamenReparatur.Entwirre(name));

    /// <summary>
    /// Der aktuelle Wert eines Feldes, das unter mehreren Schreibweisen steht (Vorlage
    /// «Ausführung», Import «Ausfuehrung»). Mit zaehlen nur Schreibweisen mit Inhalt und
    /// bewusst leere (von Hand geleert, E3) — eine leere Vorlagenspalte ist keine Aussage.
    /// 1. Handwerte (<see cref="FieldMetadata.UserEdited"/>, auch bewusst leer) gehen jeder
    ///    automatischen Quelle vor — die Projektregel «Handwerte ueberschreibt kein Import» gilt
    ///    auch ueber Schreibweisen hinweg. Der PDF-Import (SchachtProtocolApplier) schreibt alle
    ///    Schreibweisen; die handbearbeitete bleibt geschuetzt, eine andere bekommt den Importwert
    ///    mit neuerem Zeitstempel. Der darf die Handkorrektur nicht verdecken (Review PR #78).
    /// 2. Innerhalb derselben Klasse gewinnt die zuletzt geaenderte
    ///    (<see cref="FieldMetadata.LastUpdatedUtc"/>).
    /// 3. Ohne Zeitangabe oder bei gleicher Zeit gilt die Reihenfolge von
    ///    <paramref name="schreibweisen"/> (bisherige Regel: erste nicht-leere).
    ///
    /// Review PR #75: Mit «erste nicht-leere» ging eine bewusste Leer-Korrektur an einer
    /// Schreibweise verloren, solange eine andere noch den Altwert trug — das Formular sah keinen
    /// Konflikt und schrieb den Altwert-Zusatz in beide Schreibweisen.
    /// </summary>
    public static string AktuellerWert(
        IReadOnlyDictionary<string, string>? felder,
        IReadOnlyDictionary<string, FieldMetadata>? meta,
        IEnumerable<string> schreibweisen)
    {
        string? bester = null;
        var besterIstHandwert = false;
        var besteZeit = DateTime.MinValue;

        foreach (var name in schreibweisen ?? Enumerable.Empty<string>())
        {
            var wert = felder is not null && felder.TryGetValue(name, out var w) ? w ?? "" : "";
            FieldMetadata? herkunft = null;
            meta?.TryGetValue(name, out herkunft);
            if (string.IsNullOrWhiteSpace(wert) && herkunft is not { UserEdited: true })
                continue;

            var handwert = herkunft is { UserEdited: true };
            var zeit = herkunft?.LastUpdatedUtc ?? DateTime.MinValue;
            var gewinnt = bester is null
                          || (handwert && !besterIstHandwert)
                          || (handwert == besterIstHandwert && zeit > besteZeit);
            if (gewinnt)
            {
                bester = wert;
                besterIstHandwert = handwert;
                besteZeit = zeit;
            }
        }

        return bester ?? "";
    }

    /// <summary>
    /// Traegt irgendeine Schreibweise dieses Feldes einen Handwert (<see cref="FieldMetadata.UserEdited"/>,
    /// auch bewusst leer)? Dann darf keine automatische Quelle in irgendeine Schreibweise derselben
    /// Gruppe schreiben — die Projektregel «Handwerte, auch bewusst leer, ueberschreibt kein Import»
    /// (Entscheid E3, 02.10.2026) gilt fuer das Feld, nicht nur fuer den einen Namen. Vorher fuellte
    /// ein Import die ungeschuetzten Schreibweisen (Nachtrag Review PR #78).
    ///
    /// Die eine Stelle fuer diese Regel; der Schreibweg des Datensatzes
    /// (<see cref="SchachtRecord.SetFieldValue(string, string?, FieldSource, bool)"/>,
    /// <see cref="SchachtRecord.FuelleLeeresFeld"/>) fragt hier.
    /// </summary>
    public static bool HatHandwert(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);

        string? gesucht = null;
        foreach (var (name, meta) in record.FieldMeta)
        {
            if (meta is not { UserEdited: true })
                continue;
            if (string.Equals(name, gemeint, StringComparison.Ordinal))
                return true;

            gesucht ??= Gruppenschluessel(gemeint);
            if (gesucht.Length > 0 && string.Equals(Gruppenschluessel(name), gesucht, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Alle im Datensatz vorhandenen Feldnamen, deren gefaltete Form <paramref name="gesucht"/>
    /// entspricht. Suchlogik fuer <see cref="Feld"/> (ohne Mojibake-Rueckrechnung).
    /// </summary>
    private static IEnumerable<string> PassendeSchreibweisen(SchachtRecord record, string gesucht)
    {
        foreach (var vorhanden in record.Fields.Keys)
        {
            if (string.Equals(Falte(vorhanden), gesucht, StringComparison.Ordinal))
                yield return vorhanden;
        }
    }

    /// <summary>
    /// Bringt einen Feldnamen nur fuer den Vergleich auf eine schlichte Form.
    /// Der Rueckgabewert wird nie angezeigt und nie gespeichert.
    /// </summary>
    public static string Falte(string? name)
    {
        var text = name ?? "";
        var gefaltet = new StringBuilder(text.Length);

        foreach (var zeichen in text)
        {
            switch (char.ToLowerInvariant(zeichen))
            {
                case 'ä': gefaltet.Append("ae"); break;
                case 'ö': gefaltet.Append("oe"); break;
                case 'ü': gefaltet.Append("ue"); break;
                case 'ß': gefaltet.Append("ss"); break;

                // Trennzeichen aller Art fallen weg: Die Kopfzeile der Vorlage traegt
                // Zeilenumbrueche ("Status\noffen/abgeschlossen"), und dieselbe Spalte
                // steht in Altprojekten auch mit Leerzeichen statt Umbruch da.
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                case '_':
                case '-':
                case '.':
                case '/':
                    break;

                default:
                    gefaltet.Append(char.ToLowerInvariant(zeichen));
                    break;
            }
        }

        return gefaltet.ToString();
    }
}
