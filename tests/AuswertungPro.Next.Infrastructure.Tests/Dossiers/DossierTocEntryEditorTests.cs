using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierTocEntryEditorTests
{
    [Fact]
    public void Nur_der_Titel_wird_ersetzt_und_die_Seitenzahl_bleibt_ein_Feld()
    {
        using var stream = new MemoryStream();
        using var document = ErzeugeVerzeichnis(stream);

        var changed = DocxTocEntryEditor.Apply(
            document,
            new Dictionary<string, string>
            {
                ["Übersichtsplan Werkleitungen"] = "Situationsplan Werkleitungen"
            });

        var paragraph = document.MainDocumentPart!.Document.Body!
            .Elements<Paragraph>()
            .Single();

        Assert.Equal(1, changed);
        Assert.Equal(
            "1.Situationsplan Werkleitungen3",
            string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)));
        Assert.Contains(
            paragraph.Descendants<FieldCode>(),
            code => code.Text.Contains("PAGEREF", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Formatierung_gilt_nur_fuer_den_bearbeiteten_Titel()
    {
        using var stream = new MemoryStream();
        using var document = ErzeugeVerzeichnis(stream);

        DocxTocEntryEditor.Apply(
            document,
            new Dictionary<string, string>
            {
                ["Übersichtsplan Werkleitungen"] = "Situationsplan Werkleitungen"
            },
            new Dictionary<string, List<DossierTextStyleRange>>
            {
                [DossierTopicTextFormatting.LiteralStyleKey("Übersichtsplan Werkleitungen")] =
                [
                    new DossierTextStyleRange
                    {
                        Start = 0,
                        Length = 15,
                        ColorHex = "C00000",
                        Bold = true
                    }
                ]
            });

        var titleRun = document.MainDocumentPart!.Document.Body!
            .Descendants<Run>()
            .First(run => run.InnerText.StartsWith(
                "Situationsplan", System.StringComparison.Ordinal));
        var pageRun = document.MainDocumentPart.Document.Body!
            .Descendants<Run>()
            .First(run => run.InnerText == "3");

        Assert.NotNull(titleRun.RunProperties?.Bold);
        Assert.Equal("C00000", titleRun.RunProperties?.Color?.Val?.Value);
        Assert.Null(pageRun.RunProperties?.Bold);
    }

    private static WordprocessingDocument ErzeugeVerzeichnis(MemoryStream stream)
    {
        var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new Document(new Body());

        var paragraph = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Verzeichnis1" }),
            new Run(new Text("1.")),
            new Run(new TabChar()),
            new Run(new Text("Übersichtsplan Werkleitungen")),
            new Run(new TabChar()),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" PAGEREF _Toc123 \\h ")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new Text("3")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }));

        main.Document.Body!.Append(paragraph);
        return document;
    }
}
