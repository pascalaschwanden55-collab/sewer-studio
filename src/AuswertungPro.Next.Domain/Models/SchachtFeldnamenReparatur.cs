using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>Eine Gruppe von Feldnamen, die dieselbe Angabe meinen.</summary>
/// <param name="Ziel">Der Name, der bleiben soll.</param>
/// <param name="Aufzuloesen">Die uebrigen Schreibweisen derselben Angabe.</param>
/// <param name="Wert">Der Wert, der danach im Zielfeld steht.</param>
/// <param name="Uneindeutig">
/// True, wenn die Schreibweisen VERSCHIEDENE nichtleere Werte tragen. Dann wird
/// nichts zusammengefuehrt — welcher gilt, weiss nur der Mensch.
/// </param>
public sealed record FeldnamenGruppe(
    string Ziel,
    IReadOnlyList<string> Aufzuloesen,
    string Wert,
    bool Uneindeutig)
{
    public bool IstSauber => Aufzuloesen.Count == 0;
}

/// <summary>
/// Fuehrt Feldnamen zusammen, die dieselbe Angabe unter verschiedener Schreibweise
/// tragen.
///
/// Schachtfelder heissen nach der Kopfzeile der Excel-Vorlage. Wurde die einmal mit
/// der falschen Zeichentabelle gelesen, entsteht ein zweites Feld mit kaputtem Namen
/// — und beim naechsten Mal ein drittes. Im Projekt Jagdmatt steht "Primäre Schäden"
/// viermal da (auch als "PrimÃ¤re SchÃ¤den" und "PrimÃƒÂ¤re SchÃƒÂ¤den"), die
/// Ausfuehrung sogar siebenmal. Die Werte sind jedesmal dieselben; nur der Name
/// wechselt.
///
/// <see cref="Entwirre"/> rechnet den Zeichensalat zurueck: Ein UTF-8-Text, der als
/// CP1252 gelesen wurde, laesst sich verlustfrei umkehren. Ein korrekter Name bleibt
/// dabei unveraendert — "Straße" und "Eigentümer" ueberstehen den Lauf unberuehrt.
///
/// Fail-closed: Tragen zwei Schreibweisen VERSCHIEDENE Werte, wird nichts
/// zusammengefuehrt. Welcher gilt, kann nur der Mensch entscheiden.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class SchachtFeldnamenReparatur
{
    private const int MaxRunden = 4;

    /// <summary>
    /// Rechnet einen falsch gelesenen Namen zurueck. Ein bereits korrekter Name
    /// kommt unveraendert heraus.
    ///
    /// Der Weg ist: als CP1252 (ersatzweise Latin-1) in Bytes zurueck, als UTF-8
    /// wieder heraus. Geht das nicht auf, war der Name schon richtig. Mehrfach
    /// wiederholt, weil ein Name auch zweimal falsch gelesen worden sein kann.
    /// </summary>
    public static string Entwirre(string? name)
    {
        var text = name ?? "";
        if (text.Length == 0 || text.All(z => z < 128))
            return text;

        for (var runde = 0; runde < MaxRunden; runde++)
        {
            var naechst = EineRunde(text);
            if (naechst is null)
                return text;

            text = naechst;
        }

        return text;
    }

    /// <summary>
    /// Die 27 Stellen, an denen CP1252 von Latin-1 abweicht (0x80 bis 0x9F).
    ///
    /// Bewusst als Tabelle statt ueber <c>Encoding.GetEncoding(1252)</c>: Die
    /// Codepage braucht dort einen Anbieter, den nur die Infrastructure-Schicht
    /// registriert. Genau diese 27 Zeichen entscheiden aber ueber den haeufigsten
    /// Fall — das <c>ƒ</c> aus "PrimÃƒÂ¤re" ist U+0192 und steht in Latin-1 nicht.
    /// </summary>
    private static readonly char[] Cp1252Oberhalb =
    [
        '€', '', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '', 'Ž', '',
        '', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '', 'ž', 'Ÿ'
    ];

    private static string? EineRunde(string text)
    {
        var bytes = AlsCp1252(text);
        if (bytes is null)
            return null;

        string zurueck;
        try
        {
            zurueck = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Die Bytes ergeben kein gueltiges UTF-8: Dann war der Name schon richtig.
            return null;
        }

        return string.Equals(zurueck, text, StringComparison.Ordinal) ? null : zurueck;
    }

    /// <summary>
    /// Der Text als CP1252-Bytes, oder <c>null</c>, wenn ein Zeichen dort nicht
    /// vorkommt. Dann stammt er nicht aus dieser Tabelle und es gibt nichts
    /// zurueckzurechnen.
    /// </summary>
    private static byte[]? AlsCp1252(string text)
    {
        var bytes = new byte[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var zeichen = text[i];

            if (zeichen < 0x80 || (zeichen >= 0xA0 && zeichen <= 0xFF))
            {
                bytes[i] = (byte)zeichen;
                continue;
            }

            var stelle = Array.IndexOf(Cp1252Oberhalb, zeichen);
            if (stelle < 0)
                return null;

            bytes[i] = (byte)(0x80 + stelle);
        }

        return bytes;
    }

    /// <summary>
    /// Plant die Zusammenfuehrung fuer einen Datensatz.
    ///
    /// <paramref name="bevorzugt"/> sind die Namen, die die Oberflaeche gerade
    /// verwendet — in der Regel die Spalten der Vorlage. Ein Name daraus gewinnt,
    /// damit der zusammengefuehrte Wert danach auch sichtbar ist. Sonst gewinnt die
    /// entwirrte Schreibweise.
    /// </summary>
    public static IReadOnlyList<FeldnamenGruppe> Plane(
        SchachtRecord record, IReadOnlyCollection<string>? bevorzugt = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        var gruppen = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var name in record.Fields.Keys)
        {
            var schluessel = SchachtFeldnamen.Falte(Entwirre(name));
            if (schluessel.Length == 0)
                continue;

            if (!gruppen.TryGetValue(schluessel, out var liste))
                gruppen[schluessel] = liste = [];

            liste.Add(name);
        }

        var bevorzugtGefaltet = (bevorzugt ?? Array.Empty<string>())
            .Select(b => SchachtFeldnamen.Falte(Entwirre(b)))
            .ToHashSet(StringComparer.Ordinal);

        var ergebnis = new List<FeldnamenGruppe>();
        foreach (var (_, namen) in gruppen)
        {
            if (namen.Count < 2)
                continue;

            var werte = namen
                .Select(n => (record.Fields[n] ?? "").Trim())
                .Where(w => w.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var ziel = WaehleZiel(namen, bevorzugt, bevorzugtGefaltet);
            ergebnis.Add(new FeldnamenGruppe(
                ziel,
                namen.Where(n => !string.Equals(n, ziel, StringComparison.Ordinal)).ToList(),
                werte.Count == 1 ? werte[0] : (record.Fields[ziel] ?? "").Trim(),
                Uneindeutig: werte.Count > 1));
        }

        return ergebnis;
    }

    /// <summary>
    /// Wendet die geplanten Gruppen an. Uneindeutige bleiben unberuehrt.
    /// Liefert die Zahl der entfernten Schreibweisen.
    /// </summary>
    public static int Wende(SchachtRecord record, IReadOnlyList<FeldnamenGruppe> gruppen)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(gruppen);

        var entfernt = 0;
        foreach (var gruppe in gruppen.Where(g => !g.Uneindeutig && !g.IstSauber))
        {
            record.Fields[gruppe.Ziel] = gruppe.Wert;

            foreach (var alt in gruppe.Aufzuloesen)
            {
                if (!record.Fields.Remove(alt))
                    continue;

                record.FieldMeta.Remove(alt);
                entfernt++;
            }
        }

        return entfernt;
    }

    /// <summary>
    /// Der Name, der bleibt: zuerst einer, den die Oberflaeche gerade verwendet,
    /// dann eine Schreibweise, die den Zeichensalat nicht mehr traegt, sonst die
    /// erste. Bei gleichem Rang gewinnt die kuerzere — sie ist die schlichtere.
    /// </summary>
    private static string WaehleZiel(
        List<string> namen, IReadOnlyCollection<string>? bevorzugt, HashSet<string> bevorzugtGefaltet)
    {
        var ausVorlage = namen.FirstOrDefault(n =>
            bevorzugt is not null
            && bevorzugt.Contains(n, StringComparer.Ordinal)
            && bevorzugtGefaltet.Contains(SchachtFeldnamen.Falte(Entwirre(n))));
        if (ausVorlage is not null)
            return ausVorlage;

        return namen
            .OrderBy(n => string.Equals(Entwirre(n), n, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(n => n.Length)
            .ThenBy(n => n, StringComparer.Ordinal)
            .First();
    }
}
