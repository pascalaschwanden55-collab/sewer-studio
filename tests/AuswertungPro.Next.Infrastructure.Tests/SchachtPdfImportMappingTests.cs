using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtPdfImportMappingTests
{
    [Fact]
    public void ParseSchachtFields_MapsTemplateRelevantFields()
    {
        var text = string.Join("\n", new[]
        {
            "GEP Aufnahmen Altdorf 2025",
            "Schachtprotokoll   Nr. 74467",
            "Schachttyp Kontrollschacht",
            "Schachtform Rund",
            "Dimension 1000 mm",
            "Schachttiefe 2,35 m",
            "Zustand der Bauteile      Maengelfrei",
            "Datum 02/10/2025",
            "Bemerkung ohne Auffaelligkeiten"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("74467", parsed.SchachtNummer);
        Assert.Equal("02.10.2025", parsed.Datum);
        Assert.Equal("Kontrollschacht", parsed.Funktion);
        Assert.Equal("Rund", parsed.Schachtform);
        Assert.Equal("1000 mm", parsed.Dimension);
        Assert.Equal("2.35", parsed.Schachttiefe);
        Assert.Equal("Maengelfrei", parsed.PrimaereSchaeden);
        Assert.Null(parsed.Bemerkungen);
    }

    [Theory]
    [InlineData("Schachtform rund\nDurchmesser 1,2 m\nTiefe 2350 mm", "Rund", "1200 mm", "2.35")]
    [InlineData("Form Oval\nAbmessung 1000 x 800 mm\nSchachttiefe 2.5", "Oval", "1000 x 800 mm", "2.5")]
    [InlineData("Schachtform quadratisch\nDimension 80 cm x 80 cm\nSchachttiefe 250 cm", "Quadratisch", "800 x 800 mm", "2.5")]
    [InlineData("Schachtform rechteckig\nDimension 1.2 x 0.8 m\nSchachttiefe 3 m", "Rechteckig", "1200 x 800 mm", "3")]
    [InlineData("Dimension mm 800\nTiefe (Abstich) m 1.79", "Rund", "800 mm", "1.79")]
    [InlineData("Dimension mm 1200 / 800\nTiefe (Abstich) m 2.4", "Rechteckig", "1200 x 800 mm", "2.4")]
    public void ParseSchachtFields_NormalisiertFormDimensionUndSchachttiefe(
        string text,
        string expectedForm,
        string expectedDimension,
        string expectedDepth)
    {
        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal(expectedForm, parsed.Schachtform);
        Assert.Equal(expectedDimension, parsed.Dimension);
        Assert.Equal(expectedDepth, parsed.Schachttiefe);
    }

    [Fact]
    public void ParseSchachtFields_UsesDeckelDnNotAsSchachtDimension()
    {
        var parsed = LegacyPdfImportService.ParseSchachtFields(
            "Schachtprotokoll Nr. 80638\nMaterial Deckel Vollguss Deckel DN m 0.72");

        Assert.Null(parsed.Schachtform);
        Assert.Null(parsed.Dimension);
    }

    [Fact]
    public void ParseSchachtFields_ExtractsMarkedPrimaryDamages_FromZustandDerBauteile()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 74467",
            "Zustand der Bauteile",
            "Deckelrahmen gerissen ● ausgebrochen lose",
            "Schachthals gerissen ausgebrochen ● korrodiert Fugen mangelhaft verputzt",
            "Datum 02/10/2025"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Contains("Deckelrahmen: ausgebrochen", parsed.PrimaereSchaeden);
        Assert.Contains("Schachthals: korrodiert", parsed.PrimaereSchaeden);
    }

    [Fact]
    public void ParseSchachtFields_ExtractsSchachtProFreeDamageRows()
    {
        var text = string.Join("\n", new[]
        {
            "Projekt: Fuerlauwi Meiental Datum: 18.06.2026",
            "Schachtprotokoll Schacht Nr. 22152",
            "Schachtfunktion            Kontrollschacht",
            "ZUSTAND DER SCHACHTBAUTEILE",
            "Konus                      In\uFB01ltration \u2022 Fugen mangelhaft verputzt",
            "Bankett                    Ausgebrochen \u2022 Riss",
            "Durchlaufrinne             Bemerkung: Ablagerung",
            "Leiter                     fehlt",
            "Tauchbogen                 nicht notwendig"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("22152", parsed.SchachtNummer);
        Assert.Equal("18.06.2026", parsed.Datum);
        Assert.Equal("Kontrollschacht", parsed.Funktion);
        Assert.NotNull(parsed.PrimaereSchaeden);
        var primaereSchaeden = parsed.PrimaereSchaeden;
        Assert.Equal(
            new[]
            {
                "Konus: Infiltration",
                "Konus: Fugen mangelhaft verputzt",
                "Bankett: Riss",
                "Bankett: Ausgebrochen",
                "Durchlaufrinne: Ablagerung",
                "Leiter/Steigeisen: fehlt"
            },
            primaereSchaeden.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void ParseSchachtFields_MovesSchachtBemerkungenIntoPrimaryDamages()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Schacht Nr. 1085605",
            "Schachtfunktion            Kontrollschacht",
            "Bemerkungen                überdeckt, 2 Einläufe",
            "ZUSTAND DER SCHACHTBAUTEILE",
            "Schacht                    Überdeckt"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Null(parsed.Bemerkungen);
        Assert.Contains("Schacht: Überdeckt", parsed.PrimaereSchaeden);
        Assert.Contains("Bemerkungen: überdeckt, 2 Einläufe", parsed.PrimaereSchaeden);
    }

    [Fact]
    public void ImportPdf_FillsVisibleTemplateAliases_AndKeepsRemarksOutOfBemerkungen()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"schacht-pdf-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var pdfPath = Path.Combine(tempRoot, "20260618_1085605.pdf");

        try
        {
            WritePdf(
                pdfPath,
                "Projekt: Fuerlauwi Meiental Datum: 18.06.2026",
                "Schachtprotokoll Schacht Nr. 1085605",
                "Schachtfunktion            Kontrollschacht",
                "Schachtform                Rechteckig",
                "Dimension                  1200 x 800 mm",
                "Schachttiefe               2,35 m",
                "Bemerkungen                ueberdeckt, 2 Einlaeufe",
                "ZUSTAND DER SCHACHTBAUTEILE",
                "Schacht                    Ueberdeckt");

            var project = new Project();
            var stats = new LegacyPdfImportService().ImportPdf(pdfPath, project);

            Assert.Equal(0, stats.Errors);
            var record = Assert.Single(project.SchaechteData);
            Assert.Equal("1085605", record.GetFieldValue("Schachtnummer"));
            Assert.Equal("Kontrollschacht", record.GetFieldValue("Funktion"));
            Assert.Equal("Rechteckig", record.GetFieldValue("Schachtform"));
            Assert.Equal("1200 x 800 mm", record.GetFieldValue("Dimension"));
            Assert.Equal("2.35", record.GetFieldValue("Schachttiefe"));
            Assert.Equal("18.06.2026", record.GetFieldValue("Ausführung\nDatum/Jahr"));
            Assert.Equal("offen", record.GetFieldValue("Status\noffen/abgeschlossen"));
            Assert.Contains("Schacht: Ueberdeckt", record.GetFieldValue("Primäre Schäden"));
            Assert.Contains("Bemerkungen: ueberdeckt, 2 Einlaeufe", record.GetFieldValue("Primäre Schäden"));
            Assert.Equal("", record.GetFieldValue("Bemerkungen"));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ImportPdf_FillMissingOnly_ErgaenztAberErsetztKeineSchachtwerteOderProtokolle()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"schacht-pdf-fill-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var pdfPath = Path.Combine(tempRoot, "20260618_1085605.pdf");

        try
        {
            WritePdf(
                pdfPath,
                "Schachtprotokoll Schacht Nr. 1085605",
                "Schachtfunktion            Kontrollschacht",
                "Dimension                  1000 mm",
                "ZUSTAND DER SCHACHTBAUTEILE",
                "Schacht                    Ueberdeckt");

            var project = new Project();
            var record = new SchachtRecord();
            record.SetFieldValue("Schachtnummer", "1085605");
            record.SetFieldValue("Funktion", "Von Hand geprueft");
            record.SetFieldValue("PDF_Path", "C:/bestand/alt.pdf");
            record.Protocol = new ProtocolDocument
            {
                HaltungId = "1085605",
                Original = new ProtocolRevision
                {
                    Entries = [new ProtocolEntry { Code = "Bestand", Beschreibung = "Original" }]
                },
                Current = new ProtocolRevision
                {
                    Entries = [new ProtocolEntry { Code = "Bestand", Beschreibung = "Arbeitsstand" }]
                }
            };
            project.SchaechteData.Add(record);

            var stats = new LegacyPdfImportService().ImportPdf(
                pdfPath,
                project,
                fillMissingOnly: true);

            Assert.Equal(0, stats.Errors);
            Assert.Equal("Von Hand geprueft", record.GetFieldValue("Funktion"));
            Assert.Equal("1000 mm", record.GetFieldValue("Dimension"));
            Assert.Equal("C:/bestand/alt.pdf", record.GetFieldValue("PDF_Path"));
            Assert.Equal("Bestand", Assert.Single(record.Protocol.Current.Entries).Code);
            Assert.Empty(record.Protocol.History);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ImportPdf_ImportsEverySchachtFromGesamtauszug()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"schacht-pdf-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var pdfPath = Path.Combine(tempRoot, "Gesamtauszug.pdf");

        try
        {
            WriteMultiPagePdf(
                pdfPath,
                new[]
                {
                    "Projekt: Fuerlauwi Meiental Datum: 18.06.2026",
                    "Schachtprotokoll Schacht Nr. 22149",
                    "Schachtfunktion            Kontrollschacht",
                    "ZUSTAND DER SCHACHTBAUTEILE",
                    "Schachtrohr                korrodiert"
                },
                new[]
                {
                    "Projekt: Fuerlauwi Meiental Datum: 18.06.2026",
                    "Schachtprotokoll Schacht Nr. 1061114",
                    "Schachtfunktion            Kontrollschacht",
                    "ZUSTAND DER SCHACHTBAUTEILE",
                    "Bankett                    Ablagerungen"
                },
                new[]
                {
                    "Projekt: Fuerlauwi Meiental Datum: 18.06.2026",
                    "Schachtprotokoll Schacht Nr. 3.01",
                    "Schachtfunktion            Kontrollschacht",
                    "ZUSTAND DER SCHACHTBAUTEILE",
                    "Schacht                    Ueberdeckt"
                });

            var project = new Project();
            var stats = new LegacyPdfImportService().ImportPdf(pdfPath, project);

            Assert.Equal(0, stats.Errors);
            Assert.Equal(3, stats.Found);
            Assert.Equal(3, stats.CreatedRecords);
            Assert.Equal(3, project.SchaechteData.Count);
            Assert.Contains(project.SchaechteData, r => r.GetFieldValue("Schachtnummer") == "22149");
            Assert.Contains(project.SchaechteData, r => r.GetFieldValue("Schachtnummer") == "1061114");
            Assert.Contains(project.SchaechteData, r => r.GetFieldValue("Schachtnummer") == "3.01");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ParseSchachtFields_ExtractsMarkedPrimaryDamages_WhenMarkerAfterDamage()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 70001",
            "Zustand der Bauteile",
            "Bankett gerissen ausgebrochen ● korrodiert Ablagerungen"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Contains("Bankett: ausgebrochen", parsed.PrimaereSchaeden);
    }

    [Fact]
    public void ParseSchachtFields_ExtractsMarkedPrimaryDamages_ForBracketAndCheckmarkMarkers()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 70002",
            "Zustand der Bauteile",
            "Leiter/Steigeisen [x] fehlt zu kurz verrostet",
            "Tauchbogen vorhanden ✓ defekt nicht notwendig"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Contains("Leiter/Steigeisen: fehlt", parsed.PrimaereSchaeden);
        Assert.Contains("Tauchbogen: defekt", parsed.PrimaereSchaeden);
    }

    [Fact]
    public void ParseSchachtFields_SetsStatusOffen_WhenMarkedDamagesExist()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 80001",
            "Zustand der Bauteile",
            "Deckelrahmen gerissen ● ausgebrochen lose"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("offen", parsed.Status);
    }

    [Fact]
    public void ParseSchachtFields_SetsStatusAbgeschlossen_WhenOnlyMaengelfrei()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 80002",
            "Zustand der Bauteile Maengelfrei"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("Maengelfrei", parsed.PrimaereSchaeden);
        Assert.Equal("abgeschlossen", parsed.Status);
    }

    [Fact]
    public void ParseSchachtFields_PrefersExplicitStatus_WhenStatusLineExists()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 80003",
            "Zustand der Bauteile",
            "Deckelrahmen gerissen ● ausgebrochen lose",
            "Status offen/abgeschlossen: abgeschlossen"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.Equal("abgeschlossen", parsed.Status);
    }

    [Fact]
    public void ParseSchachtFields_ListsPrimaryDamagesLineByLine_InComponentOrder()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll Nr. 90001",
            "Zustand der Bauteile",
            "Schachthals gerissen ausgebrochen â— korrodiert",
            "Deckelrahmen gerissen â— ausgebrochen lose"
        });

        var parsed = LegacyPdfImportService.ParseSchachtFields(text);

        Assert.NotNull(parsed.PrimaereSchaeden);
        Assert.Contains("\n", parsed.PrimaereSchaeden);

        var lines = parsed.PrimaereSchaeden.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.True(lines.Length >= 2);

        var firstSchachthals = Array.FindIndex(lines, l => l.StartsWith("Schachthals:", StringComparison.OrdinalIgnoreCase));
        var lastDeckelrahmen = Array.FindLastIndex(lines, l => l.StartsWith("Deckelrahmen:", StringComparison.OrdinalIgnoreCase));

        Assert.True(lastDeckelrahmen >= 0, "Deckelrahmen-Eintrag fehlt.");
        Assert.True(firstSchachthals >= 0, "Schachthals-Eintrag fehlt.");
        Assert.True(lastDeckelrahmen < firstSchachthals, "Deckelrahmen muss vor Schachthals gelistet sein.");
    }

    private static void WritePdf(string path, params string[] lines)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        var y = 780m;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(40, y), font);
            y -= 18;
        }

        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteMultiPagePdf(string path, params string[][] pages)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var lines in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 780m;
            foreach (var line in lines)
            {
                page.AddText(line, 12, new PdfPoint(40, y), font);
                y -= 18;
            }
        }

        File.WriteAllBytes(path, builder.Build());
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup for Windows file handles during failed test runs.
        }
    }
}
