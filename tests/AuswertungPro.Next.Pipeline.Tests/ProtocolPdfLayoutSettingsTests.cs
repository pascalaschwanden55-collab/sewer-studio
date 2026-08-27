using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using UglyToad.PdfPig;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Die Einstellung "Fotos pro Seite" erreicht den PDF-Erzeuger einmal beim Aufbau.
/// Dadurch wirkt sie auf allen Aufrufwegen - auch dort, wo gar keine Optionen
/// uebergeben werden (zum Beispiel bei den Dossier-Beilagen).
/// </summary>
public sealed class ProtocolPdfLayoutSettingsTests : IDisposable
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "protokoll-fotoeinstellung",
        Guid.NewGuid().ToString("N"));

    private sealed record FesteEinstellung(int PhotosPerPage) : IProtocolPdfLayoutSettings;

    [Fact]
    public void Ohne_uebergebene_Optionen_gilt_die_Einstellung()
    {
        var pdf = Build(new FesteEinstellung(4), options: null);

        // Ohne Optionen stehen Haltungsgrafik und Befundtabelle davor - deshalb wird die
        // erste Seite mit Fotos gesucht statt fest Seite 2 angenommen.
        using var parsed = PdfDocument.Open(pdf);
        var ersteFotoseite = parsed.GetPages().First(p => p.GetImages().Any());
        Assert.Collection(ersteFotoseite.GetImages(), _ => { }, _ => { }, _ => { }, _ => { });
    }

    [Fact]
    public void Bei_offener_Anzahl_in_den_Optionen_gilt_die_Einstellung()
    {
        var pdf = Build(new FesteEinstellung(6), PhotoOptions(photosPerPage: null));

        using var parsed = PdfDocument.Open(pdf);
        Assert.Collection(
            parsed.GetPage(2).GetImages(),
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
    }

    [Fact]
    public void Eine_ausdrueckliche_Anzahl_in_den_Optionen_schlaegt_die_Einstellung()
    {
        var pdf = Build(new FesteEinstellung(6), PhotoOptions(photosPerPage: 1));

        using var parsed = PdfDocument.Open(pdf);
        Assert.Single(parsed.GetPage(2).GetImages());
    }

    [Fact]
    public void Eine_ausdrueckliche_Zwei_schlaegt_die_Einstellung()
    {
        var pdf = Build(new FesteEinstellung(6), PhotoOptions(photosPerPage: 2));

        using var parsed = PdfDocument.Open(pdf);
        Assert.Collection(parsed.GetPage(2).GetImages(), _ => { }, _ => { });
    }

    [Fact]
    public void Ohne_hinterlegte_Einstellung_bleiben_es_zwei_Fotos_je_Seite()
    {
        var pdf = Build(layoutSettings: null, PhotoOptions(photosPerPage: null));

        using var parsed = PdfDocument.Open(pdf);
        Assert.Collection(parsed.GetPage(2).GetImages(), _ => { }, _ => { });
    }

    private static HaltungsprotokollPdfOptions PhotoOptions(int? photosPerPage)
    {
        var options = new HaltungsprotokollPdfOptions
        {
            IncludePhotos = true,
            IncludeHaltungsgrafik = false,
            IncludeObservationTable = false
        };

        return photosPerPage is int value
            ? options with { PhotosPerPage = value }
            : options;
    }

    private byte[] Build(IProtocolPdfLayoutSettings? layoutSettings, HaltungsprotokollPdfOptions? options)
    {
        Directory.CreateDirectory(_root);

        var entries = new List<ProtocolEntry>();
        for (var i = 0; i < 6; i++)
        {
            var name = $"foto{i}.png";
            File.WriteAllBytes(Path.Combine(_root, name), PngBytes);
            entries.Add(new ProtocolEntry
            {
                Code = "BAB",
                Beschreibung = $"Riss {i}",
                MeterStart = i + 1,
                FotoPaths = [name],
                Source = ProtocolEntrySource.Imported
            });
        }

        var document = new ProtocolDocument
        {
            HaltungId = "100-200",
            Current = new ProtocolRevision { Entries = entries }
        };
        var record = new HaltungRecord { Protocol = document };
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Manual, userEdited: true);

        var exporter = new ProtocolPdfExporter(layoutSettings);
        return exporter.BuildHaltungsprotokollPdf(
            new Project { Name = "Fototest" },
            record,
            document,
            _root,
            options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
