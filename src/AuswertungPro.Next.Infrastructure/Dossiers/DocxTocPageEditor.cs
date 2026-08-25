using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Setzt eine eigene Seitenzahl in eine Kapitelzeile des Inhaltsverzeichnisses.
///
/// Die drei Kapitelzeilen holen ihre Seitenzahl aus einem Word-Feld (PAGEREF)
/// und waren deshalb als einzige nicht aenderbar — waehrend die Beilagenzeilen
/// darunter eine freie Seitenzahl haben. Diese Ungleichheit behebt diese Klasse.
///
/// Angetastet wird nur, wofuer wirklich eine eigene Angabe vorliegt: Dort
/// ersetzt Text das Feld. Alle uebrigen Zeilen behalten ihr Feld und damit
/// Words Rechnung — das ist der Unterschied zu einem pauschalen Ersetzen des
/// ganzen Verzeichnisses, bei dem auch unberuehrte Zahlen fuer immer
/// feststuenden.
///
/// Nummer, Titel und die beiden Tabulatoren bleiben unberuehrt; die neue Zahl
/// uebernimmt die Zeichenformatierung der alten, damit die Zeile aussieht wie
/// ihre Nachbarn.
/// </summary>
public static class DocxTocPageEditor
{
    /// <summary>
    /// Liefert die Zahl der geaenderten Zeilen. Der Schluessel ist der Titel
    /// der Kapitelzeile — derselbe wie beim Aendern des Titels.
    /// </summary>
    public static int Apply(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string>? pages)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (pages is null || pages.Count == 0)
            return 0;

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return 0;

        var geaendert = 0;

        foreach (var absatz in body.Descendants<Paragraph>().ToList())
        {
            var eintrag = DocxTocEntryReader.Read(absatz);
            if (eintrag is null || !pages.TryGetValue(eintrag.Title, out var seite))
                continue;

            if (ErsetzeFeld(absatz, seite ?? string.Empty))
                geaendert++;
        }

        return geaendert;
    }

    /// <summary>
    /// Ersetzt das PAGEREF-Feld durch einen Textlauf.
    ///
    /// Gesucht wird ueber ALLE Nachfahren, nicht nur die direkten Kinder: Das
    /// Verzeichnisfeld traegt den Schalter "\h", deshalb steckt jede Zeile in
    /// einem Hyperlink und ihre Laeufe liegen eine Ebene tiefer.
    ///
    /// Der erste Feldbeginn im Absatz ist nicht zwingend der richtige — bei der
    /// ersten Zeile steht davor der Beginn des Verzeichnisfeldes selbst.
    /// Genommen wird deshalb der Beginn, auf den ein PAGEREF-Befehl folgt.
    /// </summary>
    private static bool ErsetzeFeld(Paragraph absatz, string seite)
    {
        var laeufe = absatz.Descendants<Run>().ToList();

        var beginn = -1;
        for (var i = 0; i < laeufe.Count && beginn < 0; i++)
        {
            if (IstSeitenfeldBeginn(laeufe, i))
                beginn = i;
        }

        if (beginn < 0)
            return false;

        var ende = laeufe.FindIndex(beginn + 1, lauf => lauf
            .Elements<FieldChar>()
            .Any(feld => feld.FieldCharType?.Value == FieldCharValues.End));

        if (ende < 0)
            return false;

        // Die Formatierung des bisherigen Feldergebnisses weiterverwenden:
        // sonst stuende die neue Zahl fett oder in anderer Groesse da.
        var vorlage = laeufe
            .Skip(beginn)
            .Take(ende - beginn + 1)
            .LastOrDefault(lauf => lauf.Elements<Text>().Any())
            ?.RunProperties;

        var neu = new Run();
        if (vorlage is not null)
            neu.Append(vorlage.CloneNode(deep: true));

        if (seite.Length > 0)
            neu.Append(new Text(seite) { Space = SpaceProcessingModeValues.Preserve });

        laeufe[beginn].InsertBeforeSelf(neu);

        for (var i = ende; i >= beginn; i--)
            laeufe[i].Remove();

        return true;
    }

    /// <summary>
    /// Wahr, wenn dieser Lauf einen Feldbeginn traegt, auf den ein
    /// PAGEREF-Befehl folgt — und kein anderes Feld dazwischenliegt.
    /// </summary>
    private static bool IstSeitenfeldBeginn(IReadOnlyList<Run> laeufe, int stelle)
    {
        if (!laeufe[stelle].Elements<FieldChar>()
                .Any(feld => feld.FieldCharType?.Value == FieldCharValues.Begin))
        {
            return false;
        }

        for (var i = stelle + 1; i < laeufe.Count; i++)
        {
            if (laeufe[i].Elements<FieldChar>().Any())
                return false;

            if (laeufe[i].Elements<FieldCode>()
                .Any(code => code.Text.Contains("PAGEREF", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
