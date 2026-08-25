using System;

using AuswertungPro.Next.Application.Dossiers;

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
            new[] { "TV-Protokolle", "Schachtprotokolle" }, firstNumber: 4);

        Assert.Equal("4.\tTV-Protokolle\n5.\tSchachtprotokolle", text);
    }

    [Fact]
    public void Eigene_Seitenzahlen_stehen_rechts_in_einer_dritten_Spalte()
    {
        var text = DossierTocAttachments.Build(
            new[] { "TV-Protokolle", "Schachtprotokolle" },
            new[] { "8", "12" },
            firstNumber: 4,
            firstPageNumber: 5);

        Assert.Equal("4.\tTV-Protokolle\t8\n5.\tSchachtprotokolle\t12", text);
    }

    [Fact]
    public void Alte_Zeilen_ohne_Seitenangabe_erhalten_einen_Fortlaufenden_Vorschlag()
    {
        var text = DossierTocAttachments.Build(
            new[] { "TV-Protokolle", "Schachtprotokolle" },
            Array.Empty<string>(),
            firstNumber: 4,
            firstPageNumber: 5);

        Assert.Equal("4.\tTV-Protokolle\t5\n5.\tSchachtprotokolle\t6", text);
    }

    [Fact]
    public void Eine_bewusst_geloeschte_Seitenzahl_bleibt_leer()
    {
        var text = DossierTocAttachments.Build(
            new[] { "TV-Protokolle" },
            new[] { "" },
            firstNumber: 4,
            firstPageNumber: 5);

        Assert.Equal("4.\tTV-Protokolle", text);
    }

    [Fact]
    public void Ohne_Zeilen_entsteht_nichts()
        => Assert.Equal("", DossierTocAttachments.Build(Array.Empty<string>(), 4));

    [Fact]
    public void Leere_Zeilen_werden_uebersprungen_und_zaehlen_nicht_mit()
    {
        // Sonst entstuenden Luecken in der Nummerierung.
        var text = DossierTocAttachments.Build(
            new[] { "TV-Protokolle", "   ", "", "Pläne" }, firstNumber: 4);

        Assert.Equal("4.\tTV-Protokolle\n5.\tPläne", text);
    }

    [Fact]
    public void Leerraum_am_Rand_faellt_weg()
    {
        var text = DossierTocAttachments.Build(new[] { "  TV-Protokolle  " }, 4);

        Assert.Equal("4.\tTV-Protokolle", text);
    }

    [Fact]
    public void Eine_bereits_nummerierte_Zeile_wird_nicht_doppelt_nummeriert()
    {
        // Wer aus Gewohnheit „4. TV-Protokolle" tippt, soll nicht
        // „4.\t4. TV-Protokolle" im Dossier stehen haben.
        var text = DossierTocAttachments.Build(new[] { "4. TV-Protokolle", "5.Pläne" }, 4);

        Assert.Equal("4.\tTV-Protokolle\n5.\tPläne", text);
    }

    [Fact]
    public void Eine_Zahl_im_Text_bleibt_erhalten()
    {
        // „3 Pläne" ist keine Nummerierung, sondern eine Menge.
        var text = DossierTocAttachments.Build(new[] { "3 Pläne Werkleitungen" }, 4);

        Assert.Equal("4.\t3 Pläne Werkleitungen", text);
    }

    [Fact]
    public void Fehlende_Liste_ist_kein_Absturz()
        => Assert.Equal("", DossierTocAttachments.Build(null, 4));

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Die_Anfangsnummer_wird_uebernommen(int start)
    {
        var text = DossierTocAttachments.Build(new[] { "Beilage" }, start);

        Assert.StartsWith($"{start}.\t", text, StringComparison.Ordinal);
    }
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
public sealed class DossierSchema7Tests
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

        Assert.Equal(7, umgestellt.SchemaVersion);
        Assert.Equal("Liegenschaft Nr. 439 Dittli", dossier.Name);
        Assert.Equal(new[] { "33458", "36051" }, dossier.ShaftNumbers);
        Assert.Empty(dossier.TocAttachmentLines);
        Assert.Empty(dossier.TocAttachmentPageNumbers);
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
                    TocAttachmentLines = { "TV-Protokolle", "Schachtprotokolle" }
                }
            }
        };

        var dossier = DossierDocumentMigration.MigrateToCurrent(dokument).Dossiers[0];

        Assert.Equal(new[] { "TV-Protokolle", "Schachtprotokolle" }, dossier.TocAttachmentLines);
        Assert.Empty(dossier.TocAttachmentPageNumbers);
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
                    TocAttachmentLines = { "TV-Protokolle" },
                    TocAttachmentPageNumbers = { "8" }
                }
            }
        };

        var dossier = DossierDocumentMigration.MigrateToCurrent(dokument).Dossiers[0];

        Assert.Equal(new[] { "TV-Protokolle" }, dossier.TocAttachmentLines);
        Assert.Equal(new[] { "8" }, dossier.TocAttachmentPageNumbers);
    }
}
