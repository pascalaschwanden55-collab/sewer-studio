using System;
using System.IO;
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
