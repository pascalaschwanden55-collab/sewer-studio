using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierTopicHoldingInsertTests
{
    [Theory]
    [InlineData("Schäden", true)]
    [InlineData("Schäden Pz. 30", true)]
    [InlineData("schäden", true)]
    [InlineData("Sanierungskonzept", true)]
    [InlineData("Kostenschätzung Abwasser Uri", true)]
    [InlineData("Ansprechpartner", false)]
    [InlineData("Ausführungstermin", false)]
    [InlineData("Unternehmer", false)]
    [InlineData("Beilagen", false)]
    [InlineData("", false)]
    public void Nur_drei_Themen_brauchen_Leitungen_und_Schaechte(string titel, bool erwartet)
    {
        // Ein Ansprechpartner braucht keine Leitungsliste; die Knoepfe stuenden
        // dort nur im Weg.
        Assert.Equal(erwartet, DossierTopicEditing.SupportsHoldingInsert(titel));
    }
}

public sealed class DocxPlaceholderColorTests
{
    private static Run Run(string text)
        => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static WordprocessingDocument Erzeuge(MemoryStream strom, string text)
    {
        var document = WordprocessingDocument.Create(strom, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());
        body.AppendChild(new Paragraph()).Append(Run(text));
        return document;
    }

    private static (string Text, string? Farbe) Lies(MemoryStream strom)
    {
        strom.Position = 0;
        using var document = WordprocessingDocument.Open(strom, false);
        var body = document.MainDocumentPart!.Document.Body!;

        return (
            string.Concat(body.Descendants<Text>().Select(t => t.Text)),
            body.Descendants<Run>()
                .Select(r => r.RunProperties?.Color?.Val?.Value)
                .FirstOrDefault(f => f is not null));
    }

    [Fact]
    public void Eine_gesetzte_Farbe_faerbt_den_eingesetzten_Text()
    {
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "{{Text}}"))
        {
            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Text"] = "Leitung undicht",
                ["Text" + DocxPlaceholderFiller.FarbSuffix] = "C00000"
            });

            document.MainDocumentPart!.Document.Save();
        }

        var (text, farbe) = Lies(strom);

        Assert.Equal("Leitung undicht", text);
        Assert.Equal("C00000", farbe);
    }

    [Fact]
    public void Ohne_Farbe_bleibt_die_Vorlage_unangetastet()
    {
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "{{Text}}"))
        {
            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Text"] = "Leitung undicht",
                ["Text" + DocxPlaceholderFiller.FarbSuffix] = ""
            });

            document.MainDocumentPart!.Document.Save();
        }

        Assert.Null(Lies(strom).Farbe);
    }

    [Theory]
    [InlineData("rot")]
    [InlineData("#C00000")]
    [InlineData("C0000")]
    [InlineData("GGGGGG")]
    public void Ein_unbrauchbarer_Farbwert_wird_nicht_gesetzt(string wert)
    {
        // Lieber die Farbe der Vorlage als eine Angabe, die Word nicht versteht.
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "{{Text}}"))
        {
            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Text"] = "Leitung undicht",
                ["Text" + DocxPlaceholderFiller.FarbSuffix] = wert
            });

            document.MainDocumentPart!.Document.Save();
        }

        Assert.Null(Lies(strom).Farbe);
    }
}

public sealed class DossierTopicColorExportTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_farbe_" + Guid.NewGuid().ToString("N"));

    public DossierTopicColorExportTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Ein Aufraeumfehler darf den Testlauf nicht rot machen.
        }
    }

    [Fact]
    public void Die_Farbe_eines_Themas_reist_bis_in_die_Tabellenzeile()
    {
        var area = new DossierAreaSettings
        {
            Topics = { new DossierTopicRow { Title = "Schäden", Text = "Standard" } }
        };

        var dossier = new DossierDefinition
        {
            Topics =
            {
                new DossierTopicRow
                {
                    Title = "Schäden",
                    Text = "Leitung undicht",
                    ColorHex = "C00000"
                }
            }
        };

        var zeile = Assert.Single(
            DossierWordTemplateExportService.BuildTopicRows(area, dossier));

        Assert.Equal("Leitung undicht", zeile["Text"]);
        Assert.Equal("C00000", zeile["Text" + DocxPlaceholderFiller.FarbSuffix]);
    }
}
