using System;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die zusaetzlichen Verzeichniszeilen fuer die Beilagen.
///
/// Die drei Kapitel bleiben Word ueberlassen. Was am Schluss dazukommt —
/// TV-Protokolle, Schachtprotokolle, Plaene — steht nicht im Word-Dokument
/// und kann deshalb weder von Word gezaehlt noch mit einer Seitenzahl
/// versehen werden.
/// </summary>
public sealed class DossierTocAttachmentsTests
{
    [Fact]
    public void Die_Nummerierung_laeuft_nach_den_Kapiteln_weiter()
    {
        var text = DossierTocAttachments.Build(
            new[] { Punkt("TV-Protokolle"), Punkt("Schachtprotokolle") },
            firstNumber: 4);

        Assert.Equal("4.\tTV-Protokolle\n5.\tSchachtprotokolle", text);
    }

    [Fact]
    public void Eigene_Seitenzahlen_stehen_rechts_in_einer_dritten_Spalte()
    {
        var text = DossierTocAttachments.Build(
            new[] { Punkt("TV-Protokolle", "8"), Punkt("Schachtprotokolle", "12") },
            firstNumber: 4,
            firstPageNumber: 5);

        Assert.Equal("4.\tTV-Protokolle\t8\n5.\tSchachtprotokolle\t12", text);
    }

    [Fact]
    public void Alte_Zeilen_ohne_Seitenangabe_erhalten_einen_Fortlaufenden_Vorschlag()
    {
        var text = DossierTocAttachments.Build(
            new[] { Punkt("TV-Protokolle"), Punkt("Schachtprotokolle") },
            firstNumber: 4,
            firstPageNumber: 5);

        Assert.Equal("4.\tTV-Protokolle\t5\n5.\tSchachtprotokolle\t6", text);
    }

    [Fact]
    public void Eine_bewusst_geloeschte_Seitenzahl_bleibt_leer()
    {
        var text = DossierTocAttachments.Build(
            new[] { Punkt("TV-Protokolle", "") },
            firstNumber: 4,
            firstPageNumber: 5);

        Assert.Equal("4.\tTV-Protokolle", text);
    }

    [Fact]
    public void Ohne_Zeilen_entsteht_nichts()
        => Assert.Equal(
            "",
            DossierTocAttachments.Build(Array.Empty<DossierTocAttachment>(), 4));

    [Fact]
    public void Leere_Zeilen_werden_uebersprungen_und_zaehlen_nicht_mit()
    {
        // Sonst entstuenden Luecken in der Nummerierung.
        var text = DossierTocAttachments.Build(
            new[] { Punkt("TV-Protokolle"), Punkt("   "), Punkt(""), Punkt("Pläne") },
            firstNumber: 4);

        Assert.Equal("4.\tTV-Protokolle\n5.\tPläne", text);
    }

    [Fact]
    public void Leerraum_am_Rand_faellt_weg()
    {
        var text = DossierTocAttachments.Build(new[] { Punkt("  TV-Protokolle  ") }, 4);

        Assert.Equal("4.\tTV-Protokolle", text);
    }

    [Fact]
    public void Eine_bereits_nummerierte_Zeile_wird_nicht_doppelt_nummeriert()
    {
        // Wer aus Gewohnheit „4. TV-Protokolle" tippt, soll nicht
        // „4.\t4. TV-Protokolle" im Dossier stehen haben.
        var text = DossierTocAttachments.Build(
            new[] { Punkt("4. TV-Protokolle"), Punkt("5.Pläne") },
            4);

        Assert.Equal("4.\tTV-Protokolle\n5.\tPläne", text);
    }

    [Fact]
    public void Eine_Zahl_im_Text_bleibt_erhalten()
    {
        // „3 Pläne" ist keine Nummerierung, sondern eine Menge.
        var text = DossierTocAttachments.Build(new[] { Punkt("3 Pläne Werkleitungen") }, 4);

        Assert.Equal("4.\t3 Pläne Werkleitungen", text);
    }

    [Fact]
    public void Formatierung_folgt_dem_Titel_auch_wenn_eine_Nummer_entfernt_wird()
    {
        var attachment = new DossierTocAttachment
        {
            Title = "  4. TV-Protokolle  ",
            TitleStyles =
            [
                new DossierTextStyleRange
                {
                    Start = 5,
                    Length = 13,
                    ColorHex = "C00000",
                    Bold = true
                }
            ]
        };

        var entry = Assert.Single(DossierTocAttachments.BuildEntries([attachment], 4, 5));
        var style = Assert.Single(entry.TitleStyles);

        Assert.Equal("TV-Protokolle", entry.Title);
        Assert.Equal(0, style.Start);
        Assert.Equal(entry.Title.Length, style.Length);
        Assert.Equal("C00000", style.ColorHex);
        Assert.True(style.Bold);
    }

    [Fact]
    public void Fehlende_Liste_ist_kein_Absturz()
        => Assert.Equal("", DossierTocAttachments.Build(attachments: null, firstNumber: 4));

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Die_Anfangsnummer_wird_uebernommen(int start)
    {
        var text = DossierTocAttachments.Build(new[] { Punkt("Beilage") }, start);

        Assert.StartsWith($"{start}.\t", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ausgeblendete_Kapitel_werden_fuer_die_Vorschau_nicht_mitgezaehlt()
    {
        var start = DossierTocAttachments.StartAfter(
            new[]
            {
                new DossierPreviewTocEntry("1.", "Übersichtsplan Werkleitungen", "3"),
                new DossierPreviewTocEntry("2.", "Eigentumsverhältnisse", "4"),
                new DossierPreviewTocEntry("3.", "Informationen Sanierung", "4")
            },
            new[] { "Eigentumsverhältnisse" });

        Assert.Equal(3, start.FirstNumber);
        Assert.Equal(5, start.FirstPageNumber);
    }

    private static DossierTocAttachment Punkt(string title, string? pageNumber = null)
        => new() { Title = title, PageNumber = pageNumber };
}

/// <summary>
/// Die Vorlage selbst — was das Inhaltsverzeichnis betrifft.
/// </summary>
public sealed class DossierTocTemplateTests
{
    private static string VorlagenText()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var pfad = System.IO.Path.Combine(
            wurzel!, "Export_Vorlage",
            AuswertungPro.Next.Infrastructure.Dossiers.DossierWordTemplate.TemplateFileName);

        using var archiv = System.IO.Compression.ZipFile.OpenRead(pfad);
        using var strom = archiv.GetEntry("word/document.xml")!.Open();
        using var leser = new System.IO.StreamReader(strom);
        return leser.ReadToEnd();
    }

    [Fact]
    public void Die_dritte_Zeile_nennt_dasselbe_wie_die_Ueberschrift()
    {
        // Die Überschrift wurde umbenannt, das eingefrorene Verzeichnis zeigte
        // noch den alten Namen — und niemand erneuert es beim Öffnen.
        var xml = VorlagenText();

        Assert.DoesNotContain("Informationen Baustelle", xml, StringComparison.Ordinal);
        Assert.Contains("Informationen Sanierung", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_leere_Ueberschrift_speist_das_Verzeichnis_nicht_mehr()
    {
        // Sie bleibt stehen — sie trägt Abstand —, ist aber aus der Gliederung
        // genommen. Sonst entstünde beim Erneuern eine leere Verzeichniszeile.
        Assert.Contains("<w:outlineLvl w:val=\"9\" />", VorlagenText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Fuer_die_Beilagen_gibt_es_eine_Stelle_im_Verzeichnis()
    {
        Assert.Contains("{{Verzeichnis_Beilagen}}", VorlagenText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Verzeichnis_bleibt_ein_echtes_Word_Feld()
    {
        // Die drei Kapitel gehören weiterhin Word. Würde das Feld zu festem
        // Text, verlören sie ihre Seitenzahlen.
        Assert.Contains("TOC \\o", VorlagenText(), StringComparison.Ordinal);
    }
}

/// <summary>
/// Der Wechsel auf die aktuelle Formatversion.
/// </summary>
public sealed class DossierSchemaMigrationTests
{
    [Fact]
    public void Eine_Datei_der_Version_5_wird_uebernommen_und_verliert_nichts()
    {
        // Pascals Projekt steht auf 5. Die Umstellung darf nichts wegwerfen und
        // keine Zeile erfinden.
        var dokument = new AuswertungPro.Next.Domain.Models.Dossiers.DossierDocument
        {
            SchemaVersion = 5,
            Dossiers =
            {
                new AuswertungPro.Next.Domain.Models.Dossiers.DossierDefinition
                {
                    Name = "Liegenschaft Nr. 439 Dittli",
                    ShaftNumbers = { "33458", "36051" }
                }
            }
        };

        var umgestellt = DossierDocumentMigration.MigrateToCurrent(dokument);
        var dossier = umgestellt.Dossiers[0];

        Assert.Equal(9, umgestellt.SchemaVersion);
        Assert.Equal("Liegenschaft Nr. 439 Dittli", dossier.Name);
        Assert.Equal(new[] { "33458", "36051" }, dossier.ShaftNumbers);
        Assert.Empty(dossier.TocAttachments);
        Assert.Null(dossier.TocAttachmentLines);
        Assert.Null(dossier.TocAttachmentPageNumbers);
    }

    [Fact]
    public void Bestehende_Verzeichniszeilen_bleiben_erhalten()
    {
        var dokument = new AuswertungPro.Next.Domain.Models.Dossiers.DossierDocument
        {
            SchemaVersion = 6,
            Dossiers =
            {
                new AuswertungPro.Next.Domain.Models.Dossiers.DossierDefinition
                {
                    TocAttachmentLines = new List<string>
                    {
                        "TV-Protokolle",
                        "Schachtprotokolle"
                    }
                }
            }
        };

        var dossier = DossierDocumentMigration.MigrateToCurrent(dokument).Dossiers[0];

        Assert.Collection(
            dossier.TocAttachments,
            punkt =>
            {
                Assert.Equal("TV-Protokolle", punkt.Title);
                Assert.Null(punkt.PageNumber);
            },
            punkt =>
            {
                Assert.Equal("Schachtprotokolle", punkt.Title);
                Assert.Null(punkt.PageNumber);
            });
        Assert.Null(dossier.TocAttachmentLines);
        Assert.Null(dossier.TocAttachmentPageNumbers);
    }


    [Fact]
    public void Bestehende_eigene_Seitenzahlen_bleiben_erhalten()
    {
        var dokument = new AuswertungPro.Next.Domain.Models.Dossiers.DossierDocument
        {
            SchemaVersion = 7,
            Dossiers =
            {
                new AuswertungPro.Next.Domain.Models.Dossiers.DossierDefinition
                {
                    TocAttachmentLines = new List<string> { "TV-Protokolle" },
                    TocAttachmentPageNumbers = new List<string> { "8" }
                }
            }
        };

        var dossier = DossierDocumentMigration.MigrateToCurrent(dokument).Dossiers[0];

        var punkt = Assert.Single(dossier.TocAttachments);
        Assert.Equal("TV-Protokolle", punkt.Title);
        Assert.Equal("8", punkt.PageNumber);
        Assert.Null(dossier.TocAttachmentLines);
        Assert.Null(dossier.TocAttachmentPageNumbers);
    }

    [Fact]
    public void Eine_bewusst_leere_Seitenzahl_wird_von_Version_7_unveraendert_uebernommen()
    {
        var dokument = new DossierDocument
        {
            SchemaVersion = 7,
            Dossiers =
            {
                new DossierDefinition
                {
                    TocAttachmentLines = new List<string> { "TV-Protokolle" },
                    TocAttachmentPageNumbers = new List<string> { "" }
                }
            }
        };

        var punkt = Assert.Single(
            DossierDocumentMigration.MigrateToCurrent(dokument).Dossiers[0].TocAttachments);

        Assert.Equal(string.Empty, punkt.PageNumber);
    }
}
