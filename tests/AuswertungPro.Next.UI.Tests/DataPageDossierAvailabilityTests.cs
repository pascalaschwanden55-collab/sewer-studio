using System;
using System.IO;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die aus DataPageViewModel extrahierte Dossier-Verfuegbarkeitspruefung ab.
/// Reine Logik: pruefen ob druckbare Foto-Pfade existieren, ohne PDF-Erzeugung/Dialoge.
/// </summary>
public sealed class DataPageDossierAvailabilityTests
{
    // --- ResolveDossierPhotoPath ---

    [Fact]
    public void ResolveDossierPhotoPath_liefert_absoluten_pfad_unveraendert()
    {
        using var temp = new TempDir();
        var abs = temp.CreateFile("foto.jpg");

        Assert.Equal(abs, DataPageDossierAvailability.ResolveDossierPhotoPath(abs, temp.Path));
    }

    [Fact]
    public void ResolveDossierPhotoPath_kombiniert_relativen_pfad_mit_projektordner()
    {
        using var temp = new TempDir();

        var resolved = DataPageDossierAvailability.ResolveDossierPhotoPath("fotos/a.jpg", temp.Path);

        Assert.Equal(Path.GetFullPath(Path.Combine(temp.Path, "fotos", "a.jpg")), resolved, ignoreCase: true);
    }

    [Fact]
    public void ResolveDossierPhotoPath_liefert_null_bei_leer()
    {
        Assert.Null(DataPageDossierAvailability.ResolveDossierPhotoPath("  ", "C:\\projekt"));
    }

    [Fact]
    public void ResolveDossierPhotoPath_liefert_null_bei_relativ_ohne_projektordner()
    {
        Assert.Null(DataPageDossierAvailability.ResolveDossierPhotoPath("fotos/a.jpg", ""));
    }

    // --- HasPrintablePhotos ---

    [Fact]
    public void HasPrintablePhotos_true_wenn_existierendes_foto_verlinkt()
    {
        using var temp = new TempDir();
        temp.CreateFile("foto.jpg");
        var record = RecordWithFotos(deleted: false, "foto.jpg");

        Assert.True(DataPageDossierAvailability.HasPrintablePhotos(record, temp.Path));
    }

    [Fact]
    public void HasPrintablePhotos_false_wenn_foto_fehlt()
    {
        using var temp = new TempDir();
        var record = RecordWithFotos(deleted: false, "fehlt.jpg");

        Assert.False(DataPageDossierAvailability.HasPrintablePhotos(record, temp.Path));
    }

    [Fact]
    public void HasPrintablePhotos_false_wenn_eintrag_geloescht()
    {
        using var temp = new TempDir();
        temp.CreateFile("foto.jpg");
        var record = RecordWithFotos(deleted: true, "foto.jpg");

        Assert.False(DataPageDossierAvailability.HasPrintablePhotos(record, temp.Path));
    }

    [Fact]
    public void HasPrintablePhotos_false_ohne_protokoll()
    {
        using var temp = new TempDir();
        var record = new HaltungRecord();

        Assert.False(DataPageDossierAvailability.HasPrintablePhotos(record, temp.Path));
    }

    // --- EvaluatePrintableSections ---

    [Theory]
    [InlineData("deckblatt")]
    [InlineData("haltungsprotokoll")]
    [InlineData("fotos")]
    [InlineData("schachtVon")]
    [InlineData("schachtBis")]
    [InlineData("hydraulik")]
    [InlineData("kosten")]
    public void EvaluatePrintableSections_erkennt_druckbare_basisabschnitte(string section)
    {
        using var temp = new TempDir();
        temp.CreateFile("foto.jpg");

        var options = OptionsFor(section);
        var record = RecordWithFotos(deleted: false, "foto.jpg");

        var state = DataPageDossierAvailability.EvaluatePrintableSections(
            options,
            record,
            temp.Path,
            hasSchachtVon: section == "schachtVon",
            hasSchachtBis: section == "schachtBis",
            hasHydraulikResult: section == "hydraulik",
            kostenAvailable: section == "kosten",
            originalPdfCount: 0);

        Assert.True(state.HasDossierBaseSection);
        Assert.False(state.HasOriginalPdfSection);
        Assert.True(state.HasAnySection);
    }

    [Fact]
    public void EvaluatePrintableSections_erkennt_original_pdfs_ohne_basisabschnitt()
    {
        var state = DataPageDossierAvailability.EvaluatePrintableSections(
            EmptyOptions() with { IncludeOriginalProtokolle = true },
            new HaltungRecord(),
            projectFolder: "",
            hasSchachtVon: false,
            hasSchachtBis: false,
            hasHydraulikResult: false,
            kostenAvailable: false,
            originalPdfCount: 2);

        Assert.False(state.HasDossierBaseSection);
        Assert.True(state.HasOriginalPdfSection);
        Assert.True(state.HasAnySection);
    }

    [Fact]
    public void EvaluatePrintableSections_false_wenn_auswahl_keinen_druckbaren_inhalt_liefert()
    {
        using var temp = new TempDir();
        var record = RecordWithFotos(deleted: false, "fehlt.jpg");

        var state = DataPageDossierAvailability.EvaluatePrintableSections(
            EmptyOptions() with
            {
                IncludeFotos = true,
                IncludeOriginalProtokolle = true
            },
            record,
            temp.Path,
            hasSchachtVon: false,
            hasSchachtBis: false,
            hasHydraulikResult: false,
            kostenAvailable: false,
            originalPdfCount: 0);

        Assert.False(state.HasDossierBaseSection);
        Assert.False(state.HasOriginalPdfSection);
        Assert.False(state.HasAnySection);
    }

    private static HaltungRecord RecordWithFotos(bool deleted, params string[] fotoPaths)
    {
        var entry = new ProtocolEntry { IsDeleted = deleted };
        foreach (var foto in fotoPaths)
            entry.FotoPaths.Add(foto);

        return new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision { Entries = { entry } },
            },
        };
    }

    private static DossierPrintOptions OptionsFor(string section)
        => section switch
        {
            "deckblatt" => EmptyOptions() with { IncludeDeckblatt = true },
            "haltungsprotokoll" => EmptyOptions() with { IncludeHaltungsprotokoll = true },
            "fotos" => EmptyOptions() with { IncludeFotos = true },
            "schachtVon" => EmptyOptions() with { IncludeSchachtVon = true },
            "schachtBis" => EmptyOptions() with { IncludeSchachtBis = true },
            "hydraulik" => EmptyOptions() with { IncludeHydraulik = true },
            "kosten" => EmptyOptions() with { IncludeKostenschaetzung = true },
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
        };

    private static DossierPrintOptions EmptyOptions()
        => new()
        {
            IncludeDeckblatt = false,
            IncludeHaltungsprotokoll = false,
            IncludeFotos = false,
            IncludeSchachtVon = false,
            IncludeSchachtBis = false,
            IncludeHydraulik = false,
            IncludeKostenschaetzung = false,
            IncludeOriginalProtokolle = false
        };

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssd_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string CreateFile(string name)
        {
            var full = System.IO.Path.Combine(Path, name);
            File.WriteAllText(full, "x");
            return full;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
