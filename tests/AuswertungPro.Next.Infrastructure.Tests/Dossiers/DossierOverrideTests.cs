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

public sealed class DocxLiteralTextReplacerTests
{
    private static WordprocessingDocument Erzeuge(MemoryStream strom, params string[] zeilen)
    {
        var document = WordprocessingDocument.Create(strom, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        foreach (var zeile in zeilen)
        {
            body.AppendChild(new Paragraph())
                .Append(new Run(new Text(zeile) { Space = SpaceProcessingModeValues.Preserve }));
        }

        return document;
    }

    private static List<string> Lies(MemoryStream strom)
    {
        strom.Position = 0;
        using var document = WordprocessingDocument.Open(strom, false);

        return document.MainDocumentPart!.Document.Body!
            .Elements<Paragraph>()
            .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)))
            .Where(t => t.Length > 0)
            .ToList();
    }

    [Fact]
    public void Ein_fester_Text_wird_durch_die_eigene_Fassung_ersetzt()
    {
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "Eigentumsverhältnisse", "Haus Nr."))
        {
            DocxLiteralTextReplacer.Apply(document, new Dictionary<string, string>
            {
                ["Eigentumsverhältnisse"] = "Eigentümer der Liegenschaft"
            });

            document.MainDocumentPart!.Document.Save();
        }

        Assert.Equal(new[] { "Eigentümer der Liegenschaft", "Haus Nr." }, Lies(strom));
    }

    [Fact]
    public void Eine_geleerte_Zeile_wird_weggelassen()
    {
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "Beilagen", "Haus Nr."))
        {
            DocxLiteralTextReplacer.Apply(document, new Dictionary<string, string>
            {
                ["Beilagen"] = "   "
            });

            document.MainDocumentPart!.Document.Save();
        }

        Assert.Equal(new[] { "Haus Nr." }, Lies(strom));
    }

    [Fact]
    public void Eine_Zeile_mit_Platzhalter_gehoert_ihrem_Feld()
    {
        // Sonst wuerde eine eigene Fassung den Platzhalter zerstoeren, bevor
        // der Wert eingesetzt ist.
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "Datum: {{Datum}}"))
        {
            var geaendert = DocxLiteralTextReplacer.Apply(document, new Dictionary<string, string>
            {
                ["Datum: {{Datum}}"] = "Anderes"
            });

            Assert.Equal(0, geaendert);
            document.MainDocumentPart!.Document.Save();
        }

        Assert.Equal(new[] { "Datum: {{Datum}}" }, Lies(strom));
    }

    [Fact]
    public void Eine_eigene_Beschriftung_uebernimmt_Farbe_und_Schriftschnitt()
    {
        using var strom = new MemoryStream();
        using var document = Erzeuge(strom, "Informationen Sanierung");

        DocxLiteralTextReplacer.Apply(
            document,
            new Dictionary<string, string>
            {
                ["Informationen Sanierung"] = "Informationen Baustelle"
            },
            new Dictionary<string, List<DossierTextStyleRange>>
            {
                [DossierTopicTextFormatting.LiteralStyleKey("Informationen Sanierung")] =
                [
                    new DossierTextStyleRange
                    {
                        Start = 0,
                        Length = 13,
                        ColorHex = "C00000",
                        Bold = true,
                        Italic = true,
                        Underline = true
                    }
                ]
            });

        var run = document.MainDocumentPart!.Document.Body!
            .Descendants<Run>()
            .First(r => r.InnerText == "Informationen");

        Assert.Equal("Arial", run.RunProperties!.RunFonts!.Ascii!.Value);
        Assert.NotNull(run.RunProperties.Bold);
        Assert.NotNull(run.RunProperties.Italic);
        Assert.Equal(UnderlineValues.Single, run.RunProperties.Underline!.Val!.Value);
        Assert.Equal("C00000", run.RunProperties.Color!.Val!.Value);
    }

    [Fact]
    public void Platzhaltertext_in_einer_frei_bearbeiteten_Ueberschrift_bleibt_woertlich()
    {
        using var strom = new MemoryStream();
        using var document = Erzeuge(strom, "Informationen Sanierung", "{{Datum}}");

        var formatting = DocxLiteralTextReplacer.ApplyBeforePlaceholderFill(
            document,
            new Dictionary<string, string>
            {
                ["Informationen Sanierung"] = "Hinweis {{Datum}}"
            });

        DocxPlaceholderFiller.Fill(
            document,
            new Dictionary<string, string> { ["Datum"] = "24.08.2026" },
            formatting);

        var paragraphs = document.MainDocumentPart!.Document.Body!
            .Elements<Paragraph>()
            .Select(paragraph => paragraph.InnerText)
            .ToList();

        Assert.Equal("Hinweis {{Datum}}", paragraphs[0]);
        Assert.Equal("24.08.2026", paragraphs[1]);
    }

    [Fact]
    public void Ein_Text_den_es_nicht_gibt_aendert_nichts()
    {
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "Beilagen"))
        {
            Assert.Equal(0, DocxLiteralTextReplacer.Apply(document, new Dictionary<string, string>
            {
                ["Gibt es nicht"] = "Neu"
            }));

            document.MainDocumentPart!.Document.Save();
        }

        Assert.Equal(new[] { "Beilagen" }, Lies(strom));
    }

    [Fact]
    public void Ohne_eigene_Fassungen_geschieht_nichts()
    {
        using var strom = new MemoryStream();
        using var document = Erzeuge(strom, "Beilagen");

        Assert.Equal(0, DocxLiteralTextReplacer.Apply(document, null));
        Assert.Equal(0, DocxLiteralTextReplacer.Apply(
            document, new Dictionary<string, string>()));
    }
}

public sealed class DossierFieldOverrideTests
{
    [Fact]
    public void Eine_eigene_Angabe_sticht_den_berechneten_Wert()
    {
        var dossier = new DossierDefinition
        {
            ParcelNumbers = "30",
            FieldOverrides = { ["Datum_Lang"] = "im Frühjahr 2026" }
        };

        var request = Anfrage(dossier);

        Assert.Equal("im Frühjahr 2026",
            DossierWordTemplateExportService.BuildValues(request)["Datum_Lang"]);
    }

    [Fact]
    public void Eine_leere_eigene_Angabe_laesst_die_Stelle_bewusst_leer()
    {
        // Der Unterschied zaehlt: Eintrag mit leerem Text heisst "hier soll
        // nichts stehen", kein Eintrag heisst "rechne es aus".
        var dossier = new DossierDefinition
        {
            ParcelNumbers = "30",
            FieldOverrides = { ["Datum_Lang"] = "" }
        };

        Assert.Equal("", DossierWordTemplateExportService.BuildValues(Anfrage(dossier))["Datum_Lang"]);
    }

    [Fact]
    public void Ohne_eigene_Angabe_gilt_der_berechnete_Wert()
    {
        var werte = DossierWordTemplateExportService.BuildValues(
            Anfrage(new DossierDefinition { ParcelNumbers = "30" }));

        Assert.Equal(DateTime.Today.ToString("dd.MM.yyyy",
            System.Globalization.CultureInfo.GetCultureInfo("de-CH")), werte["Datum"]);
    }

    [Fact]
    public void Zusaetzliche_Verzeichnispunkte_erscheinen_auch_in_der_Vorschau()
    {
        var dossier = new DossierDefinition
        {
            TocAttachments =
            {
                new DossierTocAttachment { Title = "TV-Protokolle" },
                new DossierTocAttachment { Title = "Schachtprotokolle" }
            }
        };

        var value = DossierWordTemplateExportService.BuildValues(Anfrage(dossier))[
            "Verzeichnis_Beilagen"];

        Assert.Equal("4.\tTV-Protokolle\t5\n5.\tSchachtprotokolle\t6", value);
    }

    [Fact]
    public void Vorschau_uebernimmt_die_aus_sichtbaren_Kapiteln_berechnete_Anfangsnummer()
    {
        var dossier = new DossierDefinition
        {
            TocAttachments =
            {
                new DossierTocAttachment { Title = "TV-Protokolle" }
            }
        };

        var value = DossierWordTemplateExportService.BuildValues(
            Anfrage(dossier),
            new DossierTocAttachmentStart(3, 5))["Verzeichnis_Beilagen"];

        Assert.Equal("3.\tTV-Protokolle\t5", value);
    }

    private static AuswertungPro.Next.Application.Dossiers.DossierExportRequest Anfrage(
        DossierDefinition dossier)
    {
        var projekt = new AuswertungPro.Next.Domain.Models.Project();

        return new AuswertungPro.Next.Application.Dossiers.DossierExportRequest(
            projekt,
            Path.GetTempPath(),
            new DossierAreaSettings(),
            dossier,
            AuswertungPro.Next.Application.Dossiers.DossierSnapshotBuilder.Build(
                dossier, projekt, null),
            Path.GetTempPath());
    }
}
