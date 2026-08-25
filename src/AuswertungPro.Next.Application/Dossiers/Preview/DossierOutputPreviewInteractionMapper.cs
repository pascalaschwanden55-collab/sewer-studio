using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>
/// Eine echte Ausgabeseite samt der Vorlagenseiten, die darauf stehen.
///
/// Es ist bewusst eine LISTE: Ist ein Kapitel kurz — etwa ohne gewählten
/// Übersichtsplan —, packt Word das nächste auf dasselbe Blatt. Mit nur einer
/// Editorseite waren die Felder des anderen Kapitels unerreichbar, und
/// ausgerechnet dort sitzt die Auswahl des Plans: Wer keinen hatte, kam nicht
/// an den Knopf, der ihn einfügen würde.
///
/// <see cref="EditorPage"/> bleibt als erste dieser Seiten erhalten.
/// </summary>
public sealed record DossierOutputPreviewNavigationItem(
    string ChapterTitle,
    string PageLabel,
    DossierOutputPreviewPage OutputPage,

    /// <summary>Alle Vorlagenseiten dieses Blattes, in Dokumentreihenfolge.</summary>
    IReadOnlyList<DossierPreviewPage> EditorPages,

    /// <summary>
    /// Die am staerksten belegte davon — sie benennt das Blatt. Getrennt von
    /// der Reihenfolge, weil die Felder in Leserichtung erscheinen sollen und
    /// nicht nach Belegstaerke.
    /// </summary>
    DossierPreviewPage? EditorPage);

/// <summary>
/// Ordnet echte PDF-Seiten den Vorlagen-Editoren und sichtbaren Texten ihren
/// semantischen Klickzielen zu. Reine Logik ohne WPF oder Dateizugriff.
/// </summary>
public static class DossierOutputPreviewInteractionMapper
{
    public static IReadOnlyList<DossierOutputPreviewNavigationItem> BuildNavigation(
        IReadOnlyList<DossierOutputPreviewPage> pages,
        IReadOnlyList<DossierPreviewNavigationItem> templates,
        DossierDefinition dossier,
        IReadOnlyDictionary<string, string> values,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(rowsFor);

        if (pages.Count == 0 || templates.Count == 0)
            return Array.Empty<DossierOutputPreviewNavigationItem>();

        var result = new List<DossierOutputPreviewNavigationItem>(pages.Count);
        var minimumTemplateIndex = 0;

        // Bis hierher sind die Vorlagenseiten bereits vergeben.
        var vergebenBis = -1;

        foreach (var page in pages)
        {
            if (page.IsAttachment)
            {
                result.Add(new DossierOutputPreviewNavigationItem(
                    "Beilagen",
                    $"Beilage — Seite {page.Number}",
                    page,
                    Array.Empty<DossierPreviewPage>(),
                    null));
                continue;
            }

            var pageText = Normalize(string.Join(" ", page.Words.Select(word => word.Text)));
            var bestIndex = minimumTemplateIndex;
            var bestScore = -1;

            for (var index = minimumTemplateIndex; index < templates.Count; index++)
            {
                var score = EvidenceScore(
                    pageText,
                    templates[index],
                    dossier,
                    values,
                    rowsFor);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }

            // Ein Blatt traegt oft mehr als ein Kapitel: Ist eines kurz — etwa
            // ohne gewaehlten Uebersichtsplan —, packt Word das naechste
            // dazu. Frueher gewann davon nur das am staerksten belegte, und die
            // Felder der uebrigen waren unerreichbar.
            //
            // Nach vorn: alles, was seit dem letzten Blatt noch nicht bedient
            // wurde — auch ein Kapitel ohne eigenen Textbeleg.
            // Nach hinten: jedes weitere Kapitel, das auf diesem Blatt
            // tatsaechlich zu lesen ist.
            var von = bestIndex == vergebenBis ? bestIndex : vergebenBis + 1;
            var bis = bestIndex;

            while (bis + 1 < templates.Count
                && EvidenceScore(pageText, templates[bis + 1], dossier, values, rowsFor) > 0)
            {
                bis++;
            }

            var seiten = templates
                .Skip(von)
                .Take(bis - von + 1)
                .Select(eintrag => eintrag.Page)
                .ToList();

            vergebenBis = bis;
            minimumTemplateIndex = bestIndex;
            var template = templates[bestIndex];
            var chapter = dossier.TextOverrides.TryGetValue(template.ChapterTitle, out var own)
                && !string.IsNullOrWhiteSpace(own)
                    ? own.Trim()
                    : template.ChapterTitle;

            if (!seiten.Contains(template.Page))
                seiten.Insert(0, template.Page);

            result.Add(new DossierOutputPreviewNavigationItem(
                chapter,
                $"Seite {page.Number}",
                page,
                seiten,
                template.Page));
        }

        return MitNachzueglern(result, templates, vergebenBis);
    }

    /// <summary>
    /// Haengt Kapitel, die kein Blatt mehr abbekommen haben, an das letzte
    /// Dossierblatt.
    ///
    /// Das ist der Regelfall und keine Randerscheinung: Im echten Dossier
    /// folgen hinten die Protokolle als Beilagen. Beilagenblaetter bekommen
    /// keine Vorlagenseite — die Zuordnung endete damit, bevor die letzten
    /// Kapitel an der Reihe waren, und ihre Felder waren unerreichbar.
    /// Gemessen fehlten so drei von fuenf Kapiteln.
    /// </summary>
    private static IReadOnlyList<DossierOutputPreviewNavigationItem> MitNachzueglern(
        List<DossierOutputPreviewNavigationItem> result,
        IReadOnlyList<DossierPreviewNavigationItem> templates,
        int vergebenBis)
    {
        if (vergebenBis + 1 >= templates.Count)
            return result;

        var letztes = result.FindLastIndex(eintrag => eintrag.EditorPage is not null);
        if (letztes < 0)
            return result;

        var seiten = result[letztes].EditorPages
            .Concat(templates.Skip(vergebenBis + 1).Select(eintrag => eintrag.Page))
            .ToList();

        result[letztes] = result[letztes] with { EditorPages = seiten };
        return result;
    }

    public static IReadOnlyList<DossierPreviewTextCandidate> BuildCandidates(
        IEnumerable<DossierPreviewTarget> targets,
        IReadOnlyList<DossierPreviewField> fields,
        IReadOnlyDictionary<string, string> values,
        DossierDefinition dossier,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(rowsFor);

        var result = new List<DossierPreviewTextCandidate>();
        foreach (var target in targets)
        {
            switch (target.Kind)
            {
                case DossierPreviewTargetKind.Field:
                    foreach (var field in fields.Where(field => string.Equals(
                                 field.Key,
                                 target.Key,
                                 StringComparison.OrdinalIgnoreCase)))
                    {
                        Add(result, target, field.Read());
                    }

                    if (values.TryGetValue(target.Key, out var value))
                        Add(result, target, value);
                    break;

                case DossierPreviewTargetKind.Literal:
                    Add(
                        result,
                        target,
                        dossier.TextOverrides.TryGetValue(target.Key, out var own)
                            ? own
                            : target.Key);
                    break;

                case DossierPreviewTargetKind.Row:
                    foreach (var text in RowTexts(target, dossier, rowsFor))
                        Add(result, target, text);
                    break;

                case DossierPreviewTargetKind.RowCell:
                    var row = Row(rowsFor, target.Key, target.RowIndex);
                    if (row is not null && row.TryGetValue(target.CellKey, out var cell))
                        Add(result, target, cell);
                    break;
            }
        }

        return result;
    }

    private static int EvidenceScore(
        string pageText,
        DossierPreviewNavigationItem template,
        DossierDefinition dossier,
        IReadOnlyDictionary<string, string> values,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor)
    {
        if (pageText.Length == 0)
            return 0;

        var score = 0;
        var chapter = dossier.TextOverrides.TryGetValue(template.ChapterTitle, out var own)
            ? own
            : template.ChapterTitle;
        var normalizedChapter = Normalize(chapter);
        if (normalizedChapter.Length >= 4
            && pageText.Contains(normalizedChapter, StringComparison.Ordinal))
        {
            score += 50;
        }

        foreach (var text in EvidenceTexts(template.Page, dossier, values, rowsFor)
                     .Select(Normalize)
                     .Where(text => text.Length >= 4)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!pageText.Contains(text, StringComparison.Ordinal))
                continue;

            var wordCount = text.Count(character => character == ' ') + 1;
            score += Math.Min(24, wordCount * 3 + text.Length / 14);
        }

        return score;
    }

    private static IEnumerable<string> EvidenceTexts(
        DossierPreviewPage page,
        DossierDefinition dossier,
        IReadOnlyDictionary<string, string> values,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor)
    {
        foreach (var text in DossierPreviewTextInventory.Literals(page))
            yield return dossier.TextOverrides.TryGetValue(text, out var own) ? own : text;

        foreach (var key in page.FieldKeys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                yield return value;

            foreach (var row in rowsFor(key))
            {
                foreach (var (cellKey, cellValue) in row)
                {
                    if (!cellKey.EndsWith(
                            DossierTopicTextFormatting.StyleRangesSuffix,
                            StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(cellValue))
                    {
                        yield return cellValue;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> RowTexts(
        DossierPreviewTarget target,
        DossierDefinition dossier,
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor)
    {
        if (string.Equals(target.Key, "Verzeichnis_Beilagen", StringComparison.OrdinalIgnoreCase)
            && target.RowIndex >= 0
            && target.RowIndex < dossier.TocAttachments.Count)
        {
            var attachment = dossier.TocAttachments[target.RowIndex];
            yield return attachment.Title ?? string.Empty;
            yield return attachment.PageNumber ?? string.Empty;
            yield break;
        }

        var row = Row(rowsFor, target.Key, target.RowIndex);
        if (row is null)
            yield break;

        foreach (var (key, value) in row)
        {
            if (!key.EndsWith(
                    DossierTopicTextFormatting.StyleRangesSuffix,
                    StringComparison.Ordinal))
            {
                yield return value;
            }
        }
    }

    private static IReadOnlyDictionary<string, string>? Row(
        Func<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> rowsFor,
        string key,
        int index)
    {
        var rows = rowsFor(key);
        return index >= 0 && index < rows.Count ? rows[index] : null;
    }

    private static void Add(
        ICollection<DossierPreviewTextCandidate> target,
        DossierPreviewTarget address,
        string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            target.Add(new DossierPreviewTextCandidate(address, text));
    }

    private static string Normalize(string? text)
    {
        var characters = (text ?? string.Empty)
            .Select(character => char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ' ')
            .ToArray();
        return string.Join(
            " ",
            new string(characters).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>Sammelt die festen, bearbeitbaren Texte einer Vorlagenseite.</summary>
public static class DossierPreviewTextInventory
{
    public static IReadOnlyList<string> Literals(DossierPreviewPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var result = new List<string>();

        void Collect(IEnumerable<DossierPreviewParagraph> paragraphs)
        {
            foreach (var paragraph in paragraphs)
            {
                var text = paragraph.TocEntry?.Title
                    ?? string.Concat(paragraph.Runs.Select(run => run.Text)).Trim();

                // Ein Absatz mit Feldlauf gehoert dem Feld — seine BESCHRIFTUNG
                // aber nicht. In der Vorlage steht „Datum: {{Datum}}" als ein
                // einziger Lauf; frueher fiel damit auch „Datum:" heraus und
                // war als einziger sichtbarer Text nicht aenderbar.
                //
                // Gefragt wird auf der Wortform des Absatzes, nicht auf dem
                // gelesenen Text: Ein Feldlauf traegt hier keinen Text, sondern
                // seinen Schluessel. Nur so ergibt sich derselbe Schluessel wie
                // beim Schreiben ins Word-Dokument.
                if (paragraph.Runs.Any(run => run.IsField))
                    text = DossierMixedParagraphLiteral.Schluessel(Wortform(paragraph))
                        ?? string.Empty;

                if (text.Length > 0 && !result.Contains(text, StringComparer.Ordinal))
                    result.Add(text);
            }
        }

        foreach (var block in page.Blocks)
        {
            switch (block)
            {
                case DossierPreviewParagraph paragraph:
                    Collect([paragraph]);
                    Collect(paragraph.Floating
                        .SelectMany(floating => floating.Blocks)
                        .OfType<DossierPreviewParagraph>());
                    break;

                case DossierPreviewTable table:
                    Collect(table.Rows
                        .SelectMany(row => row.Cells)
                        .SelectMany(cell => cell.Paragraphs));
                    break;
            }
        }

        return result;
    }

    /// <summary>Der Absatz so, wie er in der Word-Vorlage steht.</summary>
    private static string Wortform(DossierPreviewParagraph paragraph)
        => string.Concat(paragraph.Runs.Select(run
            => run.IsField ? "{{" + run.FieldKey + "}}" : run.Text)).Trim();
}
