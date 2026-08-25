using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
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

    [Fact]
    public void Markierter_Text_erhaelt_Farbe_Fett_Kursiv_Unterstrichen_und_Arial()
    {
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "{{Text}}"))
        {
            var format = DossierTopicTextFormatting.Encode(new[]
            {
                new DossierTextStyleRange
                {
                    Start = 0,
                    Length = 8,
                    ColorHex = "C00000",
                    Bold = true,
                    Italic = true,
                    Underline = true
                }
            });

            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Text"] = "rot fett normal",
                ["Text" + DossierTopicTextFormatting.StyleRangesSuffix] = format
            });
            document.MainDocumentPart!.Document.Save();
        }

        strom.Position = 0;
        using var gelesen = WordprocessingDocument.Open(strom, false);
        var run = gelesen.MainDocumentPart!.Document.Body!
            .Descendants<Run>()
            .First(r => r.InnerText == "rot fett");

        Assert.Equal("C00000", run.RunProperties!.Color!.Val!.Value);
        Assert.NotNull(run.RunProperties.Bold);
        Assert.NotNull(run.RunProperties.Italic);
        Assert.Equal(UnderlineValues.Single, run.RunProperties.Underline!.Val!.Value);
        Assert.Equal("Arial", run.RunProperties.RunFonts!.Ascii!.Value);
    }

    [Fact]
    public void Markiertes_Feld_in_einer_beschrifteten_Zeile_bleibt_im_Word_rot()
    {
        using var strom = new MemoryStream();
        using (var document = Erzeuge(strom, "Datum: {{Datum}}"))
        {
            var format = DossierTopicTextFormatting.Encode(new[]
            {
                new DossierTextStyleRange
                {
                    Start = 0,
                    Length = 10,
                    ColorHex = "C00000"
                }
            });

            DocxPlaceholderFiller.Fill(document, new Dictionary<string, string>
            {
                ["Datum"] = "25.08.2026",
                ["Datum" + DossierTopicTextFormatting.StyleRangesSuffix] = format
            });
            document.MainDocumentPart!.Document.Save();
        }

        strom.Position = 0;
        using var gelesen = WordprocessingDocument.Open(strom, false);
        var runs = gelesen.MainDocumentPart!.Document.Body!
            .Descendants<Run>()
            .Where(run => run.InnerText.Length > 0)
            .ToList();

        Assert.Contains(runs, run => run.InnerText == "Datum: "
            && run.RunProperties?.Color?.Val?.Value == "000000");
        Assert.Contains(runs, run => run.InnerText == "25.08.2026"
            && run.RunProperties?.Color?.Val?.Value == "C00000");
    }
}

public sealed class DossierTopicTextFormattingTests
{
    [Fact]
    public void Platzhalter_behaelt_das_Format_der_Marke()
    {
        var text = "Leitung: {{Haltungen_Text}}";
        var start = text.IndexOf("{{", StringComparison.Ordinal);
        var format = new[]
        {
            new DossierTextStyleRange
            {
                Start = start,
                Length = "{{Haltungen_Text}}".Length,
                ColorHex = "0070C0",
                Bold = true
            }
        };

        var result = DossierTopicTextFormatting.ReplacePlaceholders(
            text,
            new Dictionary<string, string> { ["Haltungen_Text"] = "KS 30" },
            format);

        Assert.Equal("Leitung: KS 30", result.Text);
        var range = Assert.Single(result.StyleRanges);
        Assert.Equal("KS 30", result.Text.Substring(range.Start, range.Length));
        Assert.Equal("0070C0", range.ColorHex);
        Assert.True(range.Bold);
    }

    [Fact]
    public void Ungueltige_oder_zu_lange_Bereiche_werden_sicher_begrenzt()
    {
        var result = DossierTopicTextFormatting.Normalize("abc", new[]
        {
            new DossierTextStyleRange { Start = -4, Length = 20, ColorHex = "C00000" },
            new DossierTextStyleRange { Start = 1, Length = 1, ColorHex = "kein Hex" }
        });

        var range = Assert.Single(result);
        Assert.Equal(0, range.Start);
        Assert.Equal(3, range.Length);
    }

    [Fact]
    public void Beschaedigte_Schalter_werden_beim_Lesen_ignoriert()
    {
        Assert.Empty(DossierTopicTextFormatting.Decode("0,3,C00000,ja,0,0"));
    }

    [Fact]
    public void Formatierungen_bleiben_im_gespeicherten_Dossier_erhalten()
    {
        var original = new DossierDefinition
        {
            Topics =
            {
                new DossierTopicRow
                {
                    Title = "Schäden",
                    Text = "Leitung undicht",
                    StyleRanges =
                    {
                        new DossierTextStyleRange
                        {
                            Start = 0,
                            Length = 7,
                            ColorHex = "C00000",
                            Bold = true,
                            Italic = true,
                            Underline = true
                        }
                    }
                }
            }
        };
        original.FieldStyles["Parzellen_Zeile"] = new List<DossierTextStyleRange>
        {
            new() { Start = 0, Length = 2, ColorHex = "000000", Bold = true }
        };
        original.Owners.Add(new DossierOwnerRow
        {
            Name = "Kurt Beispiel",
            FieldStyles =
            {
                ["Name"] = new List<DossierTextStyleRange>
                {
                    new() { Start = 0, Length = 4, ColorHex = "0070C0", Italic = true }
                }
            }
        });

        var json = JsonSerializer.Serialize(original);
        var gelesen = JsonSerializer.Deserialize<DossierDefinition>(json)!;

        var thema = Assert.Single(gelesen.Topics);
        var themaFormat = Assert.Single(thema.StyleRanges);
        Assert.Equal("C00000", themaFormat.ColorHex);
        Assert.True(themaFormat.Bold);
        Assert.True(themaFormat.Italic);
        Assert.True(themaFormat.Underline);
        Assert.True(Assert.Single(gelesen.FieldStyles["Parzellen_Zeile"]).Bold);
        Assert.True(Assert.Single(Assert.Single(gelesen.Owners).FieldStyles["Name"]).Italic);
    }
}

public sealed class DossierFieldFormattingTests
{
    [Fact]
    public void Format_eines_normalen_Feldes_reist_in_den_Wertevorrat()
    {
        var dossier = new DossierDefinition { ParcelNumbers = "30" };
        dossier.FieldStyles["Parzellen_Zeile"] = new List<DossierTextStyleRange>
        {
            new() { Start = 0, Length = 2, Bold = true, ColorHex = "C00000" }
        };

        var emptyDistribution = new ZustandVerteilung(Array.Empty<ZustandBucket>());
        var statistics = new DashboardStatistics(
            0, 0, 0, 0,
            emptyDistribution,
            emptyDistribution,
            Array.Empty<DashboardBucket>(),
            Array.Empty<DashboardCostBucket>(),
            0, 0, 0, 0, 0);
        var snapshot = new DossierSnapshot(
            dossier.Id,
            dossier.Name,
            Array.Empty<DossierHoldingLine>(),
            Array.Empty<Guid>(),
            statistics);
        var request = new DossierExportRequest(
            new Project(), "", new DossierAreaSettings(), dossier, snapshot, "");

        var values = DossierWordTemplateExportService.BuildValues(request);
        var styles = DossierTopicTextFormatting.Decode(
            values["Parzellen_Zeile" + DossierTopicTextFormatting.StyleRangesSuffix]);

        var style = Assert.Single(styles);
        Assert.True(style.Bold);
        Assert.Equal("C00000", style.ColorHex);
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

    [Fact]
    public void Gemischte_Formatierung_reist_bis_in_die_Tabellenzeile()
    {
        var area = new DossierAreaSettings();
        var dossier = new DossierDefinition
        {
            Topics =
            {
                new DossierTopicRow
                {
                    Title = "Schäden",
                    Text = "rot und fett",
                    StyleRanges =
                    {
                        new DossierTextStyleRange
                        {
                            Start = 0,
                            Length = 3,
                            ColorHex = "C00000",
                            Bold = true
                        }
                    }
                }
            }
        };

        var zeile = Assert.Single(DossierWordTemplateExportService.BuildTopicRows(area, dossier));
        var format = DossierTopicTextFormatting.Decode(
            zeile["Text" + DossierTopicTextFormatting.StyleRangesSuffix]);

        var range = Assert.Single(format);
        Assert.Equal("C00000", range.ColorHex);
        Assert.True(range.Bold);
    }

    [Fact]
    public void Eigentuemerdaten_behalten_ihr_Feldformat_in_der_Word_Zeile()
    {
        var owner = new DossierOwnerRow
        {
            HouseNumber = "51",
            Name = "Kurt Beispiel",
            Phone = "041 000 00 00"
        };
        owner.FieldStyles["HouseNumber"] = new List<DossierTextStyleRange>
        {
            new() { Start = 0, Length = 2, Bold = true }
        };
        owner.FieldStyles["Phone"] = new List<DossierTextStyleRange>
        {
            new() { Start = 0, Length = 3, ColorHex = "C00000", Underline = true }
        };

        var row = Assert.Single(DossierWordTemplateExportService.BuildOwnerRows(
            new DossierDefinition { Owners = { owner } }));

        Assert.True(Assert.Single(DossierTopicTextFormatting.Decode(
            row["Haus_Nr" + DossierTopicTextFormatting.StyleRangesSuffix])).Bold);

        var cellStyles = DossierTopicTextFormatting.Decode(
            row["Eigentuemer_Zelle" + DossierTopicTextFormatting.StyleRangesSuffix]);
        var phone = Assert.Single(cellStyles);
        Assert.Equal("041", row["Eigentuemer_Zelle"].Substring(phone.Start, phone.Length));
        Assert.Equal("C00000", phone.ColorHex);
        Assert.True(phone.Underline);
    }

    [Fact]
    public void Aenderungszeile_behaelt_ihr_Feldformat_im_Export()
    {
        var change = new DossierChangeRow { Version = " 1 " };
        change.FieldStyles["Version"] = new List<DossierTextStyleRange>
        {
            new() { Start = 1, Length = 1, Italic = true }
        };

        var row = Assert.Single(DossierWordTemplateExportService.BuildChangeRows(
            new DossierDefinition { Changes = { change } }));

        Assert.Equal("1", row["Version"]);
        Assert.True(Assert.Single(DossierTopicTextFormatting.Decode(
            row["Version" + DossierTopicTextFormatting.StyleRangesSuffix])).Italic);
    }
}
