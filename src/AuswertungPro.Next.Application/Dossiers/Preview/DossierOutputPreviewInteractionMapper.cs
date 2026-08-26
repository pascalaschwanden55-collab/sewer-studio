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
    /// <summary>
    /// Erkennt die Planstelle auch dann, wenn Word bei leerem Plan mehrere
    /// kurze Kapitel auf dasselbe Ausgabeblatt zieht und die Navigation das
    /// Blatt nach einem anderen Kapitel benennt.
    /// </summary>
    public static bool ContainsPlanLocation(
        DossierOutputPreviewPage outputPage,
        IReadOnlyList<DossierPreviewPage> editorPages)
    {
        ArgumentNullException.ThrowIfNull(outputPage);
        ArgumentNullException.ThrowIfNull(editorPages);

        if (editorPages.Any(page => page.FieldKeys.Contains(
                "Uebersichtsplan",
                StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Auch im Inhaltsverzeichnis steht "Uebersichtsplan". Dort darf das
        // Fotosymbol nicht erscheinen. Der Fallback gilt nur fuer ein Blatt,
        // dessen Kapitelzuordnung zwar verrutscht ist, das aber nicht die
        // Verzeichnisseite selbst enthaelt.
        var isTocPage = editorPages
            .SelectMany(DossierPreviewTextInventory.Literals)
            .Any(text => text.Contains(
                "Inhaltsverzeichnis",
                StringComparison.OrdinalIgnoreCase));

        return !isTocPage
            && outputPage.Words.Any(word => word.Text.Contains(
                "bersichtsplan",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Begrenzt die Klickziele auf die Vorlagenkapitel, die auf dem gerade
    /// sichtbaren PDF-Blatt liegen.
    ///
    /// Die Eingabeseite darf weiterhin alle Felder des Dossiers bereithalten.
    /// Fuer die Treffer im Blatt waere diese Gesamtmenge aber falsch: Ein Wert
    /// wie "439" steht auf Deckblatt und Eigentuemerseite. Wuerden beide Ziele
    /// gleichzeitig gesucht, koennte ein Klick auf dem Deckblatt in die
    /// Eigentuemertabelle springen.
    /// </summary>
    public static IReadOnlyList<DossierPreviewTarget> TargetsForPages(
        IEnumerable<DossierPreviewTarget> targets,
        IReadOnlyList<DossierPreviewPage> pages)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(pages);

        if (pages.Count == 0)
            return Array.Empty<DossierPreviewTarget>();

        var fieldKeys = new HashSet<string>(
            pages.SelectMany(page => page.FieldKeys),
            StringComparer.OrdinalIgnoreCase);
        var literalKeys = new HashSet<string>(
            pages.SelectMany(DossierPreviewTextInventory.Literals),
            StringComparer.Ordinal);
        var tocChapterTitles = new HashSet<string>(
            DossierTocChapterPageClickMapper.ChapterTitles(pages),
            StringComparer.Ordinal);

        return targets
            .Where(target => target.Kind switch
            {
                DossierPreviewTargetKind.Literal => literalKeys.Contains(target.Key),
                DossierPreviewTargetKind.RowCell
                    when DossierTocChapterPageClickMapper.IsPageTarget(target)
                    => DossierTocChapterPageClickMapper.OriginalTitle(target) is { } title
                        && tocChapterTitles.Contains(title),
                DossierPreviewTargetKind.Field
                    or DossierPreviewTargetKind.Row
                    or DossierPreviewTargetKind.RowCell => fieldKeys.Contains(target.Key),
                _ => false
            })
            .Distinct()
            .ToList();
    }

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

        // Erster Durchgang: Jedes Blatt beansprucht GENAU EIN Kapitel — das,
        // fuer das es den staerksten Textbeleg hat. Mehr darf ein Blatt hier
        // nicht an sich ziehen: Auf dem Verzeichnisblatt stehen alle
        // Kapitelnamen, es wuerde sonst den ganzen Rest verschlucken.
        var beansprucht = new int?[pages.Count];
        var minimumTemplateIndex = 0;

        var blatttexte = pages
            .Select(seite => Normalize(
                string.Join(" ", seite.Words.Select(word => word.Text))))
            .ToArray();

        for (var seite = 0; seite < pages.Count; seite++)
        {
            if (pages[seite].IsAttachment)
                continue;

            var pageText = blatttexte[seite];
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

            beansprucht[seite] = bestIndex;
            minimumTemplateIndex = bestIndex;
        }

        var seitenJeBlatt = VerteileUnbeanspruchte(
            beansprucht,
            templates,
            (blatt, kapitel) => EvidenceScore(
                blatttexte[blatt], templates[kapitel], dossier, values, rowsFor));
        var result = new List<DossierOutputPreviewNavigationItem>(pages.Count);

        for (var seite = 0; seite < pages.Count; seite++)
        {
            if (beansprucht[seite] is not { } eigenes)
            {
                result.Add(new DossierOutputPreviewNavigationItem(
                    "Beilagen",
                    $"Beilage — Seite {pages[seite].Number}",
                    pages[seite],
                    Array.Empty<DossierPreviewPage>(),
                    null));
                continue;
            }

            var template = templates[eigenes];
            var chapter = dossier.TextOverrides.TryGetValue(template.ChapterTitle, out var own)
                && !string.IsNullOrWhiteSpace(own)
                    ? own.Trim()
                    : template.ChapterTitle;

            result.Add(new DossierOutputPreviewNavigationItem(
                chapter,
                $"Seite {pages[seite].Number}",
                pages[seite],
                seitenJeBlatt[seite].Select(index => templates[index].Page).ToList(),
                template.Page));
        }

        return result;
    }

    /// <summary>
    /// Verteilt die Kapitel, die kein Blatt beansprucht hat.
    ///
    /// Ein Kapitel ohne eigenes Blatt steht trotzdem irgendwo: entweder mit
    /// seinem Text auf einem Blatt, das schon ein anderes Kapitel beansprucht
    /// hat, oder — wenn es leer ist wie der Uebersichtsplan ohne Plan — als
    /// blosse Ueberschrift ueber dem naechsten Kapitel.
    ///
    /// Gesucht wird deshalb zuerst das Blatt mit dem staerksten Beleg, und nur
    /// zwischen den Blaettern der beiden Nachbarkapitel. Diese Schranke ist
    /// nicht Kosmetik: Auf dem Verzeichnisblatt stehen ALLE Kapitelnamen, es
    /// wuerde sonst jedes belegarme Kapitel an sich ziehen. Ohne jeden Beleg
    /// gilt das Blatt des naechsten Kapitels, und ganz am Ende — hinten haengen
    /// die Protokolle als Beilagen — das letzte Dossierblatt.
    ///
    /// Ohne diese Verteilung waren die Felder solcher Kapitel unerreichbar,
    /// darunter ausgerechnet die Auswahl des Uebersichtsplans: Wer keinen Plan
    /// hat, hat ein leeres Kapitel 1 und kaeme nicht an den Knopf, der ihn
    /// einfuegen wuerde.
    /// </summary>
    private static List<int>[] VerteileUnbeanspruchte(
        int?[] beansprucht,
        IReadOnlyList<DossierPreviewNavigationItem> templates,
        Func<int, int, int> beleg)
    {
        var ergebnis = new List<int>[beansprucht.Length];
        for (var seite = 0; seite < ergebnis.Length; seite++)
            ergebnis[seite] = beansprucht[seite] is { } eigenes ? [eigenes] : [];

        var letztesDossierblatt = Array.FindLastIndex(
            beansprucht, eintrag => eintrag is not null);

        if (letztesDossierblatt < 0)
            return ergebnis;

        for (var kapitel = 0; kapitel < templates.Count; kapitel++)
        {
            if (Array.IndexOf(beansprucht, (int?)kapitel) >= 0)
                continue;

            ergebnis[Zielblatt(beansprucht, letztesDossierblatt, kapitel, beleg)].Add(kapitel);
        }

        foreach (var blatt in ergebnis)
            blatt.Sort();

        return ergebnis;
    }

    private static int Zielblatt(
        int?[] beansprucht,
        int letztesDossierblatt,
        int kapitel,
        Func<int, int, int> beleg)
    {
        // Zwischen den Blaettern der beiden Nachbarkapitel — weiter weg kann
        // ein Kapitel in einem fortlaufenden Dokument nicht stehen.
        var davor = Array.FindLastIndex(
            beansprucht, eintrag => eintrag is { } eigenes && eigenes < kapitel);

        var danach = Array.FindIndex(
            beansprucht, eintrag => eintrag is { } eigenes && eigenes > kapitel);

        var von = Math.Max(0, davor);
        var bis = danach >= 0 ? danach : letztesDossierblatt;

        var bestesBlatt = -1;
        var besterBeleg = 0;

        for (var blatt = von; blatt <= bis; blatt++)
        {
            if (beansprucht[blatt] is null)
                continue;

            var wert = beleg(blatt, kapitel);
            if (wert > besterBeleg)
            {
                besterBeleg = wert;
                bestesBlatt = blatt;
            }
        }

        if (bestesBlatt >= 0)
            return bestesBlatt;

        return danach >= 0 ? danach : letztesDossierblatt;
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
                    if (TryAddTocAttachmentCell(result, target, dossier))
                        break;

                    var row = Row(rowsFor, target.Key, target.RowIndex);
                    if (row is not null && row.TryGetValue(target.CellKey, out var cell))
                        Add(result, target, cell);
                    break;
            }
        }

        return result;
    }

    private static bool TryAddTocAttachmentCell(
        ICollection<DossierPreviewTextCandidate> result,
        DossierPreviewTarget target,
        DossierDefinition dossier)
    {
        if (!string.Equals(
                target.Key,
                "Verzeichnis_Beilagen",
                StringComparison.OrdinalIgnoreCase)
            || target.RowIndex < 0
            || target.RowIndex >= dossier.TocAttachments.Count)
        {
            return false;
        }

        var attachment = dossier.TocAttachments[target.RowIndex];
        if (string.Equals(target.CellKey, "Titel", StringComparison.OrdinalIgnoreCase))
            Add(result, target, attachment.Title);
        else if (string.Equals(target.CellKey, "Seite", StringComparison.OrdinalIgnoreCase))
            Add(result, target, attachment.PageNumber);

        return true;
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

                if (IstEchterText(text) && !result.Contains(text, StringComparer.Ordinal))
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

    /// <summary>
    /// Ein Text, den man bearbeiten kann — mindestens ein Buchstabe oder eine
    /// Ziffer.
    ///
    /// Die Vorlage enthaelt Punktlinien zum Ausfuellen von Hand. Sie standen
    /// bisher als bearbeitbare Beschriftung in der Liste, waren im Blatt aber
    /// nie anklickbar: Die Zuordnung sucht ueber WOERTER, und eine Reihe
    /// Punkte traegt keines. Ein Angebot, das der Klick nicht halten kann, ist
    /// schlechter als gar keines.
    /// </summary>
    public static bool IstEchterText(string? text)
        => text is not null && text.Any(char.IsLetterOrDigit);
}
