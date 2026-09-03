using System;
using System.Collections.Generic;
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
    /// </summary>
    public static string Feld(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);

        var gesucht = Falte(gemeint);
        if (gesucht.Length == 0)
            return gemeint;

        string? ersterTreffer = null;

        foreach (var vorhanden in record.Fields.Keys)
        {
            if (!string.Equals(Falte(vorhanden), gesucht, StringComparison.Ordinal))
                continue;

            ersterTreffer ??= vorhanden;

            if (!string.IsNullOrWhiteSpace(record.Fields[vorhanden]))
                return vorhanden;
        }

        return ersterTreffer ?? gemeint;
    }

    /// <summary>
    /// Alle Namen, unter denen <paramref name="record"/> dasselbe Feld fuehrt.
    /// Bei einem sauberen Datensatz ist das genau einer.
    /// </summary>
    public static IReadOnlyList<string> Schreibweisen(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);

        var gesucht = Falte(gemeint);
        var treffer = new List<string>();
        if (gesucht.Length == 0)
            return treffer;

        foreach (var vorhanden in record.Fields.Keys)
        {
            if (string.Equals(Falte(vorhanden), gesucht, StringComparison.Ordinal))
                treffer.Add(vorhanden);
        }

        return treffer;
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
