using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Erzeugt die Standardvorlage "Eigentuemerdossier.docx" nach dem Vorbild des
/// bestehenden Eigentuemerdossiers (Abwasser Uri).
///
/// Die Vorlage ist bewusst eine ganz normale Word-Datei: Pascal darf sie
/// jederzeit selbst oeffnen und aendern — Logo austauschen, Standardtexte
/// umschreiben, Zeilen ergaenzen. Nur die Platzhalter <c>{{Name}}</c> und die
/// Wiederholzeile <c>{{#Haltungen}}</c> muessen stehen bleiben.
///
/// Jeder Platzhalter steht hier in EINEM Textstueck. Sobald Word die Datei
/// speichert, kann es ihn zerlegen; der Fueller kommt damit zurecht
/// (siehe <see cref="DocxPlaceholderFiller"/>).
/// </summary>
public static class DossierWordTemplateBuilder
{
    /// <summary>Dateiname der Vorlage im Ordner "Export_Vorlage".</summary>
    public const string TemplateFileName = "Eigentuemerdossier.docx";

    private const string HintColor = "808080";
    private const string HeaderFill = "DCE6F1";
    private const string BorderColor = "000000";

    /// <summary>Erzeugt die Vorlage als Bytes.</summary>
    public static byte[] Build()
    {
        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(
                   stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            AddStyles(mainPart);
            var footerId = AddFooter(mainPart);

            BuildCoverPage(body);
            BuildChangeLogPage(body);
            BuildOverviewPlanPage(body);
            BuildContentPages(body);

            body.AppendChild(BuildSectionProperties(footerId));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    /// <summary>Schreibt die Vorlage atomar an den Zielpfad.</summary>
    public static void WriteTo(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var folder = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        var temp = targetPath + ".tmp";
        File.WriteAllBytes(temp, Build());

        if (File.Exists(targetPath))
            File.Replace(temp, targetPath, destinationBackupFileName: null);
        else
            File.Move(temp, targetPath);
    }

    // ── Seite 1: Deckblatt ────────────────────────────────────────────────

    private static void BuildCoverPage(Body body)
    {
        // Der Rahmen um das Deckblatt ist im Vorbild eine einzelne umrandete
        // Zelle. Dasselbe Mittel hier: eine Tabelle mit genau einer Zelle.
        var frame = new Table();
        frame.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            BuildBorders(size: 12)));
        frame.AppendChild(new TableGrid(new GridColumn { Width = "9000" }));

        var cell = new TableCell();
        cell.AppendChild(new TableCellProperties(
            new TableCellWidth { Width = "5000", Type = TableWidthUnitValues.Pct }));

        cell.AppendChild(Paragraph("{{Logo_Hinweis}}", size: 18, color: HintColor, italic: true));
        cell.AppendChild(EmptyParagraph());
        cell.AppendChild(EmptyParagraph());

        cell.AppendChild(Paragraph(
            "{{Gebietstitel}}", size: 32, bold: true, alignment: JustificationValues.Center));
        cell.AppendChild(EmptyParagraph());
        cell.AppendChild(EmptyParagraph());

        // "Eigentuemerdossier" steht im Vorbild in einem eigenen Kasten.
        var titleBox = new Table();
        titleBox.AppendChild(new TableProperties(
            new TableWidth { Width = "2600", Type = TableWidthUnitValues.Pct },
            new TableJustification { Val = TableRowAlignmentValues.Center },
            BuildBorders(size: 8)));
        titleBox.AppendChild(new TableGrid(new GridColumn { Width = "4600" }));
        var titleCell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Width = "5000", Type = TableWidthUnitValues.Pct }),
            Paragraph("Eigentümerdossier", size: 32, bold: true,
                alignment: JustificationValues.Center));
        titleBox.AppendChild(new TableRow(titleCell));
        cell.AppendChild(titleBox);

        cell.AppendChild(EmptyParagraph());
        cell.AppendChild(EmptyParagraph());
        cell.AppendChild(EmptyParagraph());

        cell.AppendChild(Paragraph(
            "{{Parzellen_Zeile}}", size: 28, bold: true, alignment: JustificationValues.Center));
        cell.AppendChild(Paragraph(
            "{{Adresse_Zeile}}", size: 26, bold: true, alignment: JustificationValues.Center));
        cell.AppendChild(Paragraph(
            "{{Eigentuemer_Block}}", size: 26, bold: true, alignment: JustificationValues.Center));

        for (var i = 0; i < 4; i++)
            cell.AppendChild(EmptyParagraph());

        var footerRow = new Table();
        footerRow.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            NoBorders()));
        footerRow.AppendChild(new TableGrid(
            new GridColumn { Width = "4500" }, new GridColumn { Width = "4500" }));
        footerRow.AppendChild(new TableRow(
            BorderlessCell(Paragraph("Datum: {{Datum}}", size: 16)),
            BorderlessCell(Paragraph("Revision: {{Revision}}", size: 16, bold: true,
                alignment: JustificationValues.Right))));
        cell.AppendChild(footerRow);

        frame.AppendChild(new TableRow(cell));
        body.AppendChild(frame);
        body.AppendChild(PageBreak());
    }

    // ── Seite 2: Aenderungswesen und Inhalt ───────────────────────────────

    private static void BuildChangeLogPage(Body body)
    {
        body.AppendChild(Paragraph("Änderungswesen:", size: 20));
        body.AppendChild(EmptyParagraph());

        var table = NewTable(1100, 1800, 1100, 5000);
        table.AppendChild(HeaderRow("Version", "Datum", "Visum", "Art der Änderung"));
        table.AppendChild(BodyRow("{{Revision}}", "{{Datum}}", "", "Ersterstellung"));
        table.AppendChild(BodyRow("", "", "", ""));
        table.AppendChild(BodyRow("", "", "", ""));
        body.AppendChild(table);

        body.AppendChild(EmptyParagraph());
        body.AppendChild(EmptyParagraph());

        var meta = NewTable(3000, 6000, borders: false);
        meta.AppendChild(BorderlessRow("Erstellungsdatum:", "{{Datum_Lang}}"));
        meta.AppendChild(BorderlessRow("Autoren:", "{{Autor}}"));
        body.AppendChild(meta);

        body.AppendChild(EmptyParagraph());
        body.AppendChild(EmptyParagraph());
        body.AppendChild(Paragraph("Inhaltsverzeichnis", size: 24, bold: true));
        body.AppendChild(EmptyParagraph());

        var toc = NewTable(600, 8400, borders: false);
        toc.AppendChild(BorderlessRow("1.", "Übersichtsplan Werkleitungen"));
        toc.AppendChild(BorderlessRow("2.", "Eigentumsverhältnisse"));
        toc.AppendChild(BorderlessRow("3.", "Betroffene Abwasserleitungen"));
        toc.AppendChild(BorderlessRow("4.", "Informationen Sanierung"));
        toc.AppendChild(BorderlessRow("5.", "Rückmeldung / Einverständnis"));
        body.AppendChild(toc);

        body.AppendChild(PageBreak());
    }

    // ── Seite 3: Uebersichtsplan ──────────────────────────────────────────

    private static void BuildOverviewPlanPage(Body body)
    {
        body.AppendChild(Paragraph("1.  Übersichtsplan Werkleitungen", size: 24, bold: true));
        body.AppendChild(EmptyParagraph());
        body.AppendChild(Paragraph(
            "[Hier den Übersichtsplan einfügen: Register „Einfügen\" → „Bilder\". "
            + "Diesen Hinweis danach löschen.]",
            size: 20, color: HintColor, italic: true));
        body.AppendChild(PageBreak());
    }

    // ── Seite 4 ff.: Inhalt ───────────────────────────────────────────────

    private static void BuildContentPages(Body body)
    {
        // 2. Eigentumsverhaeltnisse
        body.AppendChild(Paragraph("2.  Eigentumsverhältnisse", size: 24, bold: true));
        body.AppendChild(EmptyParagraph());

        var owner = NewTable(1400, 1400, 6200);
        owner.AppendChild(HeaderRow("Haus Nr.", "Pz. Nr.", "Eigentümer"));
        owner.AppendChild(BodyRow(
            "{{Hausnummern}}",
            "{{Parzellen}}",
            "{{Eigentuemer_Detail}}"));
        body.AppendChild(owner);

        body.AppendChild(EmptyParagraph());
        body.AppendChild(EmptyParagraph());

        // 3. Betroffene Abwasserleitungen — der Mehrwert gegenueber dem Vorbild:
        // die Leitungen kommen direkt aus der Auswertung.
        body.AppendChild(Paragraph("3.  Betroffene Abwasserleitungen", size: 24, bold: true));
        body.AppendChild(EmptyParagraph());

        var holdings = NewTable(2600, 1200, 1200, 2800, 1400);
        holdings.AppendChild(HeaderRow(
            "Leitung", "Länge", "Zustand", "Empfohlene Massnahme", "Kosten CHF"));
        holdings.AppendChild(BodyRow(
            "{{#Haltungen}}{{Haltung}}", "{{Laenge}}", "{{Zustand}}", "{{Massnahme}}", "{{Kosten}}"));
        holdings.AppendChild(SummaryRow(
            "Total", "{{Laenge_Total}}", "", "", "{{Kosten_Total}}"));
        body.AppendChild(holdings);

        body.AppendChild(EmptyParagraph());
        body.AppendChild(Paragraph(
            "{{Kosten_Hinweis}}", size: 16, italic: true));

        body.AppendChild(PageBreak());

        // 4. Informationen Sanierung
        body.AppendChild(Paragraph("4.  Informationen Sanierung", size: 24, bold: true));
        body.AppendChild(EmptyParagraph());

        var info = NewTable(2600, 6600);
        info.AppendChild(HeaderRow("Thema", "Bemerkungen"));
        info.AppendChild(BodyRow("Ausführungstermin", "{{Ausfuehrungstermin}}"));
        info.AppendChild(BodyRow("Ansprechpartner", "{{Ansprechpartner}}"));
        info.AppendChild(BodyRow("Unternehmer", "{{Unternehmer}}"));
        info.AppendChild(BodyRow("Örtliche Bauleitung", "{{Bauleitung}}"));
        info.AppendChild(BodyRow(
            "Behinderungen, Zugänge, Verkehrsführung, Fussgängerführung", "{{Behinderungen}}"));
        info.AppendChild(BodyRow("Bauvorgang", "{{Bauvorgang}}"));
        info.AppendChild(BodyRow("Hausanschluss Abwasser", "{{Hausanschluss}}"));
        info.AppendChild(BodyRow("Meteorwasser", "{{Meteorwasser}}"));
        info.AppendChild(BodyRow("Bemerkungen", "{{Bemerkungen}}"));
        info.AppendChild(BodyRow("Beilagen", "{{Beilagen}}"));
        body.AppendChild(info);

        body.AppendChild(EmptyParagraph());
        body.AppendChild(EmptyParagraph());

        // 5. Rueckmeldung
        body.AppendChild(Paragraph("5.  Rückmeldung / Einverständnis Eigentümer", size: 24, bold: true));
        body.AppendChild(EmptyParagraph());

        var response = NewTable(9200);
        response.AppendChild(BodyRow("{{Rueckmeldung}}"));
        var signature = new TableCell();
        signature.AppendChild(new TableCellProperties(
            new TableCellWidth { Width = "5000", Type = TableWidthUnitValues.Pct }));
        signature.AppendChild(EmptyParagraph());
        signature.AppendChild(EmptyParagraph());

        var signatureLines = NewTable(4400, 4400, borders: false);
        signatureLines.AppendChild(BorderlessRow(
            "..............................................",
            ".............................................."));
        signatureLines.AppendChild(BorderlessRow("Ort/Datum", "Unterschrift(en)"));
        signature.AppendChild(signatureLines);
        signature.AppendChild(EmptyParagraph());
        response.AppendChild(new TableRow(signature));
        body.AppendChild(response);
    }

    // ── Bausteine ─────────────────────────────────────────────────────────

    private static Paragraph Paragraph(
        string text,
        int size = 20,
        bool bold = false,
        bool italic = false,
        string? color = null,
        JustificationValues? alignment = null)
    {
        var runProperties = new RunProperties();
        if (bold)
            runProperties.AppendChild(new Bold());
        if (italic)
            runProperties.AppendChild(new Italic());
        if (!string.IsNullOrWhiteSpace(color))
            runProperties.AppendChild(new Color { Val = color });
        runProperties.AppendChild(new FontSize { Val = size.ToString() });
        runProperties.AppendChild(new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });

        var run = new Run(runProperties,
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var paragraph = new Paragraph(run);
        var properties = new ParagraphProperties(
            new SpacingBetweenLines { After = "60", Line = "240", LineRule = LineSpacingRuleValues.Auto });
        if (alignment is not null)
            properties.AppendChild(new Justification { Val = alignment });
        paragraph.PrependChild(properties);

        return paragraph;
    }

    private static Paragraph EmptyParagraph() => Paragraph(string.Empty);

    private static Paragraph PageBreak()
        => new(new Run(new Break { Type = BreakValues.Page }));

    private static Table NewTable(params int[] columnWidths) => NewTable(true, columnWidths);

    private static Table NewTable(int width1, int width2, bool borders)
        => NewTable(borders, width1, width2);

    private static Table NewTable(bool borders, params int[] columnWidths)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            borders ? BuildBorders(size: 6) : NoBorders()));

        var grid = new TableGrid();
        foreach (var width in columnWidths)
            grid.AppendChild(new GridColumn { Width = width.ToString() });
        table.AppendChild(grid);

        return table;
    }

    private static TableBorders BuildBorders(uint size) => new(
        new TopBorder { Val = BorderValues.Single, Size = size, Color = BorderColor },
        new LeftBorder { Val = BorderValues.Single, Size = size, Color = BorderColor },
        new BottomBorder { Val = BorderValues.Single, Size = size, Color = BorderColor },
        new RightBorder { Val = BorderValues.Single, Size = size, Color = BorderColor },
        new InsideHorizontalBorder { Val = BorderValues.Single, Size = size, Color = BorderColor },
        new InsideVerticalBorder { Val = BorderValues.Single, Size = size, Color = BorderColor });

    private static TableBorders NoBorders() => new(
        new TopBorder { Val = BorderValues.None },
        new LeftBorder { Val = BorderValues.None },
        new BottomBorder { Val = BorderValues.None },
        new RightBorder { Val = BorderValues.None },
        new InsideHorizontalBorder { Val = BorderValues.None },
        new InsideVerticalBorder { Val = BorderValues.None });

    private static TableRow HeaderRow(params string[] values)
    {
        var row = new TableRow();
        row.AppendChild(new TableRowProperties(new TableRowHeight { Val = 340 }));

        foreach (var value in values)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderFill },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            cell.AppendChild(Paragraph(value, size: 18, bold: true));
            row.AppendChild(cell);
        }

        return row;
    }

    private static TableRow BodyRow(params string[] values)
    {
        var row = new TableRow();
        foreach (var value in values)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            cell.AppendChild(Paragraph(value, size: 18));
            row.AppendChild(cell);
        }

        return row;
    }

    private static TableRow SummaryRow(params string[] values)
    {
        var row = new TableRow();
        foreach (var value in values)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(
                new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }));
            cell.AppendChild(Paragraph(value, size: 18, bold: true));
            row.AppendChild(cell);
        }

        return row;
    }

    private static TableRow BorderlessRow(params string[] values)
    {
        var row = new TableRow();
        foreach (var value in values)
            row.AppendChild(BorderlessCell(Paragraph(value, size: 20)));

        return row;
    }

    private static TableCell BorderlessCell(Paragraph content)
    {
        var cell = new TableCell();
        cell.AppendChild(new TableCellProperties(new TableCellBorders(
            new TopBorder { Val = BorderValues.None },
            new LeftBorder { Val = BorderValues.None },
            new BottomBorder { Val = BorderValues.None },
            new RightBorder { Val = BorderValues.None })));
        cell.AppendChild(content);
        return cell;
    }

    private static void AddStyles(MainDocumentPart mainPart)
    {
        var stylePart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylePart.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(
                    new RunPropertiesBaseStyle(
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
                        new FontSize { Val = "20" }))));
        stylePart.Styles.Save();
    }

    private static string AddFooter(MainDocumentPart mainPart)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var footerId = mainPart.GetIdOfPart(footerPart);

        var line = new Table();
        line.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6, Color = BorderColor },
                new LeftBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None })));
        line.AppendChild(new TableGrid(
            new GridColumn { Width = "6500" }, new GridColumn { Width = "2500" }));

        var pageNumber = new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
                    new FontSize { Val = "16" }),
                new Text("Seite ") { Space = SpaceProcessingModeValues.Preserve }),
            new SimpleField { Instruction = "PAGE" },
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
                    new FontSize { Val = "16" }),
                new Text(" von ") { Space = SpaceProcessingModeValues.Preserve }),
            new SimpleField { Instruction = "NUMPAGES" });

        line.AppendChild(new TableRow(
            BorderlessCell(Paragraph("{{Fusszeile}}", size: 16)),
            BorderlessCell(pageNumber)));

        footerPart.Footer = new Footer(line);
        footerPart.Footer.Save();

        return footerId;
    }

    private static SectionProperties BuildSectionProperties(string footerId) => new(
        new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
        new PageSize { Width = 11906, Height = 16838 },
        new PageMargin
        {
            Top = 1134,
            Right = 1134,
            Bottom = 1134,
            Left = 1134,
            Header = 567,
            Footer = 567,
            Gutter = 0
        });
}
