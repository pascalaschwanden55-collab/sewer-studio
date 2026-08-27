using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Ohne gewaehlten Plan wurde der ganze Planabsatz entfernt - samt seinem Platz.
/// An der echten Word/PDF-Ausgabe gemessen: 4 Blaetter statt 5, mit Uebersichtsplan,
/// Eigentumsverhaeltnissen UND Informationen Sanierung gemeinsam auf Blatt 3.
///
/// Der Absatz bleibt jetzt stehen und behaelt die Hoehe der Planflaeche. Entfernt
/// werden nur seine schwebenden Formen - die lagen sonst in Word ueber den
/// Folgekapiteln, und genau dagegen war das Loeschen urspruenglich gedacht.
///
/// Geprueft wird der echte Exportweg gegen die ausgelieferte Vorlage.
/// </summary>
public sealed class DossierPlanseiteTests : IDisposable
{
    private const int MinTwips = 10000;
    private const int MaxTwips = 13000;

    private readonly string _ordner = Path.Combine(
        Path.GetTempPath(), "planseite", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Ohne_Plan_bleibt_die_Planflaeche_als_Platz_stehen()
    {
        var pfad = await ExportiereAsync(mitPlan: false);

        using var doc = WordprocessingDocument.Open(pfad, false);

        var reserviert = ReservierteAbsaetze(doc);
        Assert.True(
            reserviert.Count == 1,
            $"Erwartet genau eine reservierte Planflaeche, gefunden {reserviert.Count}. "
            + "Ohne sie rutschen Eigentumsverhaeltnisse und Sanierung auf das Planblatt.");
    }

    [Fact]
    public async Task Ohne_Plan_bleibt_in_der_Planflaeche_kein_schwebender_Rahmen()
    {
        // Ein stehengebliebener schwebender Rahmen liegt in Word ueber Kapitel 2 und 3.
        var pfad = await ExportiereAsync(mitPlan: false);

        using var doc = WordprocessingDocument.Open(pfad, false);
        var absatz = Assert.Single(ReservierteAbsaetze(doc));

        Assert.Empty(absatz.Descendants<DW.Anchor>());
        Assert.Empty(absatz.Descendants<Picture>());
        Assert.Empty(absatz.Descendants<DocumentFormat.OpenXml.AlternateContent>());
    }

    [Fact]
    public async Task Mit_Plan_wird_das_Bild_gesetzt_und_kein_Platz_reserviert()
    {
        var pfad = await ExportiereAsync(mitPlan: true);

        using var doc = WordprocessingDocument.Open(pfad, false);

        var inlineBilder = doc.MainDocumentPart!.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>()
            .Where(zeichnung => zeichnung.Descendants<DW.Inline>().Any())
            .ToList();

        Assert.NotEmpty(inlineBilder);
        Assert.Empty(ReservierteAbsaetze(doc));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Der_Platzhaltertext_verschwindet_immer(bool mitPlan)
    {
        // Eine geschweifte Klammer darf der Eigentuemer nie zu sehen bekommen.
        var pfad = await ExportiereAsync(mitPlan);

        using var doc = WordprocessingDocument.Open(pfad, false);

        Assert.DoesNotContain(
            "{{@",
            doc.MainDocumentPart!.Document.Body!.InnerText,
            StringComparison.Ordinal);
    }

    /// <summary>Absaetze mit fest reservierter Hoehe in der Groessenordnung der Planflaeche.</summary>
    private static IReadOnlyList<Paragraph> ReservierteAbsaetze(WordprocessingDocument doc)
        => doc.MainDocumentPart!.Document.Body!
            .Descendants<Paragraph>()
            .Where(absatz =>
            {
                var spacing = absatz.ParagraphProperties?.SpacingBetweenLines;
                if (spacing?.LineRule?.Value != LineSpacingRuleValues.Exact)
                    return false;

                return int.TryParse(spacing.Line?.Value, out var twips)
                    && twips >= MinTwips
                    && twips <= MaxTwips;
            })
            .ToList();

    private async Task<string> ExportiereAsync(bool mitPlan)
    {
        Directory.CreateDirectory(_ordner);

        string? planPfad = null;
        if (mitPlan)
        {
            planPfad = Path.Combine(_ordner, "plan.png");
            File.WriteAllBytes(planPfad, HochformatPng());
        }

        var document = new DossierDocument
        {
            Area = new DossierAreaSettings { AreaTitle = "Testgebiet", AreaLocation = "Altdorf" }
        };
        var dossier = new DossierDefinition
        {
            Name = "Liegenschaft 1",
            Owners = [new DossierOwnerRow { HouseNumber = "20", ParcelNumber = "844", Name = "Meier" }],
            OverviewPlanPath = planPfad ?? string.Empty
        };
        document.Dossiers.Add(dossier);
        DossierDocumentMigration.MigrateToCurrent(document);

        var verteilung = new ZustandVerteilung(Array.Empty<ZustandBucket>());
        var statistik = new DashboardStatistics(
            0, 0, 0, 0, verteilung, verteilung,
            Array.Empty<DashboardBucket>(), Array.Empty<DashboardCostBucket>(), 0, 0, 0, 0, 0);
        var snapshot = new DossierSnapshot(dossier.Id, dossier.Name, [], [], statistik);

        var vorlage = Path.Combine(
            TestRepoPaths.RepoRoot(), "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        var service = new DossierWordTemplateExportService(() => vorlage);

        var result = await service.ExportAsync(new DossierExportRequest(
            new Project(), _ordner, document.Area!, dossier, snapshot, _ordner));

        Assert.True(result.Success, result.Message);
        return result.FilePath!;
    }

    /// <summary>Ein schlichtes graues Hochformat-PNG in Planproportionen.</summary>
    private static byte[] HochformatPng()
    {
        const int breite = 600;
        const int hoehe = 850;

        var roh = new List<byte>();
        for (var zeile = 0; zeile < hoehe; zeile++)
        {
            roh.Add(0);
            roh.AddRange(Enumerable.Repeat((byte)0xC0, breite));
        }

        using var speicher = new MemoryStream();
        speicher.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var kopf = new List<byte>();
        kopf.AddRange(BigEndian(breite));
        kopf.AddRange(BigEndian(hoehe));
        kopf.AddRange(new byte[] { 8, 0, 0, 0, 0 });

        SchreibeBlock(speicher, "IHDR", kopf.ToArray());
        SchreibeBlock(speicher, "IDAT", Deflate(roh.ToArray()));
        SchreibeBlock(speicher, "IEND", Array.Empty<byte>());
        return speicher.ToArray();
    }

    private static byte[] BigEndian(int wert)
        => new[] { (byte)(wert >> 24), (byte)(wert >> 16), (byte)(wert >> 8), (byte)wert };

    private static byte[] Deflate(byte[] daten)
    {
        using var ziel = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(
                   ziel, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(daten);
        }

        return ziel.ToArray();
    }

    private static void SchreibeBlock(Stream ziel, string typ, byte[] daten)
    {
        var inhalt = new List<byte>(System.Text.Encoding.ASCII.GetBytes(typ));
        inhalt.AddRange(daten);

        ziel.Write(BigEndian(daten.Length));
        ziel.Write(inhalt.ToArray());
        ziel.Write(BigEndian(unchecked((int)Crc32(inhalt.ToArray()))));
    }

    private static uint Crc32(byte[] daten)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var wert in daten)
        {
            crc ^= wert;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch { }
    }
}
