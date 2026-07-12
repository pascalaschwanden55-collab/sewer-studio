using System.Text;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using UglyToad.PdfPig;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfPhotoSectionTests
{
    private static readonly byte[] TransparentPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    [Fact]
    public void BuildItems_NutztBevorzugtenOrdner_BegrenztUndDedupliziertProjektweit()
    {
        var root = CreateTempDirectory();
        try
        {
            var preferred = Directory.CreateDirectory(Path.Combine(root, "Fotos", "Haltungen", "100-200")).FullName;
            foreach (var fileName in new[] { "a.png", "b.png", "c.png" })
                File.WriteAllBytes(Path.Combine(preferred, fileName), TransparentPng);

            var first = new ProtocolEntry { FotoPaths = ["alter-ort/a.png", "alter-ort/b.png", "alter-ort/c.png"] };
            var second = new ProtocolEntry { FotoPaths = ["anderer-ort/a.png", "anderer-ort/c.png"] };

            var items = ProtocolPdfPhotoSection.BuildItems(
                [first, second],
                root,
                maxPhotosPerEntry: 2,
                preferredFolder: preferred);

            Assert.Collection(
                items,
                item => AssertItem(item, first, Path.Combine(preferred, "a.png")),
                item => AssertItem(item, first, Path.Combine(preferred, "b.png")),
                item => AssertItem(item, second, Path.Combine(preferred, "c.png")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildNumberMap_BewahrtGlobaleFotoreihenfolge()
    {
        var first = new ProtocolEntry { FotoPaths = ["a.png", "c.png"] };
        var second = new ProtocolEntry { FotoPaths = ["b.png"] };
        var items = new[]
        {
            new ProtocolPdfPhotoSection.PhotoItem(first, "a.png"),
            new ProtocolPdfPhotoSection.PhotoItem(second, "b.png"),
            new ProtocolPdfPhotoSection.PhotoItem(first, "c.png")
        };

        var map = ProtocolPdfPhotoSection.BuildNumberMap(items);

        Assert.Equal("1,3", map[first]);
        Assert.Equal("2", map[second]);
        Assert.Equal("1,3", ProtocolPdfPhotoSection.ResolveNumberText(first, map));
        Assert.Equal("-", ProtocolPdfPhotoSection.ResolveNumberText(new ProtocolEntry(), map));
        Assert.Equal("2", ProtocolPdfPhotoSection.ResolveNumberText(first, photoNumbers: null));
    }

    [Fact]
    public void BuildHaltungsprotokollPdf_MitFoto_ErzeugtGueltigePdfDatei()
    {
        var root = CreateTempDirectory();
        try
        {
            var photoPath = Path.Combine(root, "foto.png");
            File.WriteAllBytes(photoPath, TransparentPng);

            var entry = new ProtocolEntry
            {
                Code = "BAB",
                Beschreibung = "Riss",
                MeterStart = 1.25,
                FotoPaths = ["foto.png"],
                Source = ProtocolEntrySource.Imported
            };
            var document = new ProtocolDocument
            {
                HaltungId = "100-200",
                Current = new ProtocolRevision { Entries = [entry] }
            };
            var record = new HaltungRecord { Protocol = document };
            record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: true);
            record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Manual, userEdited: true);

            var pdf = new ProtocolPdfExporter().BuildHaltungsprotokollPdf(
                new Project { Name = "Fototest" },
                record,
                document,
                root,
                new HaltungsprotokollPdfOptions
                {
                    IncludePhotos = true,
                    IncludeHaltungsgrafik = false,
                    IncludeObservationTable = false
                });

            Assert.True(pdf.Length > 1000);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
            using var parsed = PdfDocument.Open(pdf);
            Assert.Equal(2, parsed.NumberOfPages);
            Assert.DoesNotContain("Bild fehlt", parsed.GetPage(2).Text, StringComparison.Ordinal);
            Assert.Contains("BAB Riss", parsed.GetPage(2).Text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildHaltungsprotokollPdf_MitKaputtemFoto_ErzeugtTrotzdemGueltigePdfDatei()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "kaputt.png"), "Das ist keine Bilddatei.");

            var entry = new ProtocolEntry
            {
                Code = "BAB",
                Beschreibung = "Riss",
                MeterStart = 1.25,
                FotoPaths = ["kaputt.png"],
                Source = ProtocolEntrySource.Imported
            };
            var document = new ProtocolDocument
            {
                HaltungId = "100-200",
                Current = new ProtocolRevision { Entries = [entry] }
            };
            var record = new HaltungRecord { Protocol = document };
            record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: true);
            record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Manual, userEdited: true);

            var pdf = new ProtocolPdfExporter().BuildHaltungsprotokollPdf(
                new Project { Name = "Fototest" },
                record,
                document,
                root,
                new HaltungsprotokollPdfOptions
                {
                    IncludePhotos = true,
                    IncludeHaltungsgrafik = false,
                    IncludeObservationTable = false
                });

            Assert.True(pdf.Length > 1000);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
            using var parsed = PdfDocument.Open(pdf);
            Assert.Equal(2, parsed.NumberOfPages);
            Assert.Contains("Bild fehlt", parsed.GetPage(2).Text, StringComparison.Ordinal);
            Assert.Contains("BAB Riss", parsed.GetPage(2).Text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildHaltungsprotokollPdf_DreiFotos_VerwendetZweiFotoseitenInBefundreihenfolge()
    {
        var root = CreateTempDirectory();
        try
        {
            foreach (var fileName in new[] { "eins.png", "zwei.png", "drei.png" })
                File.WriteAllBytes(Path.Combine(root, fileName), TransparentPng);

            var entries = new[]
            {
                CreatePhotoEntry("drei.png", meter: 3, description: "Dritter Fotobefund"),
                CreatePhotoEntry("eins.png", meter: 1, description: "Erster Fotobefund"),
                CreatePhotoEntry("zwei.png", meter: 2, description: "Zweiter Fotobefund")
            };
            var document = new ProtocolDocument
            {
                HaltungId = "100-200",
                Current = new ProtocolRevision { Entries = entries.ToList() }
            };
            var record = new HaltungRecord { Protocol = document };
            record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: true);
            record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Manual, userEdited: true);

            var pdf = new ProtocolPdfExporter().BuildHaltungsprotokollPdf(
                new Project { Name = "Fototest" },
                record,
                document,
                root,
                new HaltungsprotokollPdfOptions
                {
                    IncludePhotos = true,
                    IncludeHaltungsgrafik = false,
                    IncludeObservationTable = false
                });

            using var parsed = PdfDocument.Open(pdf);
            Assert.Equal(3, parsed.NumberOfPages);
            var firstPhotoPage = parsed.GetPage(2).Text;
            var secondPhotoPage = parsed.GetPage(3).Text;
            Assert.Contains("Erster Fotobefund", firstPhotoPage, StringComparison.Ordinal);
            Assert.Contains("Zweiter Fotobefund", firstPhotoPage, StringComparison.Ordinal);
            Assert.DoesNotContain("Dritter Fotobefund", firstPhotoPage, StringComparison.Ordinal);
            Assert.Contains("Dritter Fotobefund", secondPhotoPage, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertItem(
        ProtocolPdfPhotoSection.PhotoItem item,
        ProtocolEntry expectedEntry,
        string expectedPath)
    {
        Assert.Same(expectedEntry, item.Entry);
        Assert.Equal(Path.GetFullPath(expectedPath), Path.GetFullPath(item.Path));
    }

    private static ProtocolEntry CreatePhotoEntry(string path, double meter, string description)
        => new()
        {
            Code = "BAB",
            Beschreibung = description,
            MeterStart = meter,
            FotoPaths = [path],
            Source = ProtocolEntrySource.Imported
        };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "sewer-pdf-photo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
