using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Ersetzt feste Texte der Vorlage durch eigene Angaben des Dossiers —
/// Kapitelueberschriften, Spaltentitel, jede Zeile ohne Platzhalter.
///
/// Der Schluessel ist der urspruengliche Text selbst. Das hat einen Vorteil und
/// eine Grenze, und beide sind gewollt: es braucht keine kuenstliche Nummer,
/// die beim Umbau der Vorlage verrutscht — und wird der Text in Word geaendert,
/// greift die Ersetzung nicht mehr. Dann steht wieder der Text der Vorlage da,
/// nicht ein Rest von gestern.
///
/// Ein LEERER Ersatz entfernt den Absatz. Wer eine Zeile nicht braucht, soll
/// keine leere Zeile verschicken.
/// </summary>
public static class DocxLiteralTextReplacer
{
    public static int Apply(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, List<DossierTextStyleRange>>? fieldStyles = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (overrides is null || overrides.Count == 0)
            return 0;

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return 0;

        var karte = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (schluessel, wert) in overrides)
        {
            var sauber = (schluessel ?? string.Empty).Trim();
            if (sauber.Length > 0)
                karte[sauber] = wert ?? string.Empty;
        }

        var geaendert = 0;

        foreach (var absatz in body.Descendants<Paragraph>().ToList())
        {
            // Nur der innerste Absatz: eine Huelle um Textfelder traegt den
            // Text aller Felder zusammen und gehoert keinem davon.
            if (absatz.Descendants<Paragraph>().Any())
                continue;

            // Verzeichniszeilen gehoeren dem Verzeichnis-Editor. Ginge dieser
            // Weg darueber, schriebe er den ganzen Text in den ersten Lauf und
            // leerte die uebrigen — mitsamt Seitenzahl und Tabulatoren. Genau
            // das ist passiert, als der Schluessel noch die ganze Zeile war
            // ("1.Uebersichtsplan Werkleitungen3"): Die Zeile stand danach ohne
            // Seitenzahl und ohne Einzug da. Solche Eintraege liegen in alten
            // Dossiers weiterhin gespeichert und muessen wirkungslos bleiben.
            if (DossierTocStyle.IsEntry(
                    absatz.ParagraphProperties?.ParagraphStyleId?.Val?.Value))
            {
                continue;
            }

            var stuecke = absatz.Descendants<Text>().ToList();
            if (stuecke.Count == 0)
                continue;

            var text = string.Concat(stuecke.Select(t => t.Text)).Trim();

            // Eine Zeile mit Platzhalter gehoert dem Feld, nicht dem festen Text.
            if (text.Length == 0
                || text.Contains("{{", StringComparison.Ordinal)
                || !karte.TryGetValue(text, out var ersatz))
            {
                continue;
            }

            if (ersatz.Trim().Length == 0)
            {
                absatz.Remove();
                geaendert++;
                continue;
            }

            var styleKey = DossierTopicTextFormatting.LiteralStyleKey(text);
            var ranges = fieldStyles is not null
                && fieldStyles.TryGetValue(styleKey, out var stored)
                    ? DossierTopicTextFormatting.Normalize(ersatz, stored)
                    : new List<DossierTextStyleRange>();

            if (ranges.Count > 0)
            {
                DocxPlaceholderFiller.WriteBackFormatted(absatz, stuecke, ersatz, ranges);
            }
            else
            {
                stuecke[0].Text = ersatz;
                stuecke[0].Space = SpaceProcessingModeValues.Preserve;

                for (var i = 1; i < stuecke.Count; i++)
                    stuecke[i].Text = string.Empty;
            }

            geaendert++;
        }

        return geaendert;
    }
}
