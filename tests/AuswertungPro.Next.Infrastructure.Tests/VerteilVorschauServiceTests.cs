using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Vorschau im Fenster «Verteilen» sagt voraus, was das Verteilen tun wird — mit echten
/// kleinen PDFs und Filmen, und ohne selbst etwas zu schreiben.
/// </summary>
public sealed class VerteilVorschauServiceTests
{
    private static readonly DateTime Datum = new(2026, 7, 12);

    [Fact]
    public void Haltung_mit_passendem_film_wird_mit_film_abgelegt()
    {
        using var f = new Fixture();
        f.Haltungspdf("Haltung.pdf", "1000-2000");
        f.Film("20260712_1000-2000.mp4", "film-a");

        var ergebnis = f.PlaneHaltungen();

        var zeile = Assert.Single(ergebnis.Zeilen);
        Assert.Equal(VerteilVorschauStatus.FilmGefunden, zeile.Status);
        Assert.Equal("1000-2000", zeile.Objekt);
        Assert.Equal("20260712_1000-2000.mp4", zeile.Film);
        Assert.Equal("1000-2000", zeile.Zielordner);
        Assert.Equal(1, ergebnis.WerdenAbgelegt);
        Assert.Equal(0, ergebnis.BrauchenAufmerksamkeit);
    }

    [Fact]
    public void Haltung_ohne_film_meldet_film_fehlt()
    {
        using var f = new Fixture();
        f.Haltungspdf("Haltung.pdf", "1000-2000");

        var zeile = Assert.Single(f.PlaneHaltungen().Zeilen);

        Assert.Equal(VerteilVorschauStatus.FilmFehlt, zeile.Status);
        Assert.True(zeile.WirdAbgelegt);
        Assert.True(zeile.BrauchtAufmerksamkeit);
    }

    [Fact]
    public void Zwei_verschiedene_filme_sind_mehrdeutig()
    {
        using var f = new Fixture();
        f.Haltungspdf("Haltung.pdf", "1000-2000");
        f.Film("20260712_1000-2000_a.mp4", "film-a");
        f.Film("20260712_1000-2000_b.mp4", "film-b");

        var zeile = Assert.Single(f.PlaneHaltungen().Zeilen);

        Assert.Equal(VerteilVorschauStatus.FilmMehrdeutig, zeile.Status);
        Assert.Contains("2 Filme", zeile.Film, StringComparison.Ordinal);
        Assert.Contains("20260712_1000-2000_a.mp4", zeile.Hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public void Nach_dem_verteilen_liegt_alles_schon_vorhanden()
    {
        using var f = new Fixture();
        var pdf = f.Haltungspdf("Haltung.pdf", "1000-2000");
        f.Film("20260712_1000-2000.mp4", "film-a");

        // Der echte Verteilweg legt Protokoll und Film ab …
        var verteilt = HoldingFolderDistributor.DistributeFiles([pdf], f.Filme, f.Ziel);
        Assert.All(verteilt, r => Assert.True(r.Success, r.Message));

        // … und die Vorschau erkennt danach, dass es nichts Neues mehr gibt.
        var ergebnis = f.PlaneHaltungen();
        var zeile = Assert.Single(ergebnis.Zeilen);
        Assert.Equal(VerteilVorschauStatus.SchonVorhanden, zeile.Status);
        Assert.Equal(0, ergebnis.WerdenAbgelegt);
    }

    [Fact]
    public void Nicht_zuordenbare_pdf_wird_nicht_abgelegt()
    {
        using var f = new Fixture();
        f.Textpdf("Rechnung.pdf", "Rechnung Nr. 4711 – Betrag CHF 120.00");

        var zeile = Assert.Single(f.PlaneHaltungen().Zeilen);

        Assert.Equal(VerteilVorschauStatus.NichtZugeordnet, zeile.Status);
        Assert.False(zeile.WirdAbgelegt);
    }

    [Fact]
    public void Vorschau_nutzt_verzeichnisbaum_und_sanierungsebene()
    {
        using var f = new Fixture();
        f.Haltungspdf("Haltung.pdf", "1000-2000");
        var projekt = new Project();
        projekt.Metadata["Gemeinde"] = "Altdorf";

        var ergebnis = f.Plane(VerteilArt.Haltungen, DistributionVariant.Sanierung, projekt,
            new DistributionTargetConfig { OrdnerPattern = "{Gemeinde}", UnterordnerPattern = "{Jahr}" });

        var zeile = Assert.Single(ergebnis.Zeilen);
        Assert.StartsWith(Path.Combine("Altdorf", "2026", "1000-2000"), zeile.Zielordner, StringComparison.Ordinal);
        Assert.Contains("Saniert 2026", zeile.Zielordner, StringComparison.Ordinal);
    }

    [Fact]
    public void Schacht_sammelbericht_ergibt_eine_zeile_je_schacht()
    {
        using var f = new Fixture();
        f.Schachtsammelpdf("Schaechte.pdf", "74467", "74468");

        var ergebnis = f.Plane(VerteilArt.Schaechte);

        Assert.Equal(2, ergebnis.Zeilen.Count);
        Assert.Equal(new[] { "74467", "74468" }, ergebnis.Zeilen.Select(z => z.Objekt).ToArray());
        Assert.All(ergebnis.Zeilen, z => Assert.Equal(VerteilVorschauStatus.Bereit, z.Status));
        Assert.Equal(2, ergebnis.WerdenAbgelegt);
    }

    [Fact]
    public void Schacht_teil_eines_sammelberichts_ist_nach_dem_verteilen_schon_vorhanden()
    {
        using var f = new Fixture();
        var pdf = f.Schachtsammelpdf("Schaechte.pdf", "74467", "74468");
        var verteilt = HoldingFolderDistributor.DistributeShaftFiles([pdf], f.Ziel);
        Assert.All(verteilt, r => Assert.True(r.Success, r.Message));

        var ergebnis = f.Plane(VerteilArt.Schaechte);

        Assert.All(ergebnis.Zeilen, z => Assert.Equal(VerteilVorschauStatus.SchonVorhanden, z.Status));
    }

    [Fact]
    public void Dichtheit_nennt_haltung_und_dp_ordner()
    {
        using var f = new Fixture();
        f.Textpdf("Dichtheit.pdf", "Prufgegenstand / Haltung 6928 -> 6927", "Datum 2026/07/12");
        var projekt = new Project();
        var haltung = new HaltungRecord();
        haltung.SetFieldValue("Haltungsname", "6927-6928", FieldSource.Manual, userEdited: false);
        projekt.AddRecord(haltung);

        var ergebnis = f.Plane(VerteilArt.Dichtheit, projekt: projekt);

        var zeile = Assert.Single(ergebnis.Zeilen);
        Assert.Equal("6927-6928", zeile.Objekt);
        Assert.Equal(VerteilVorschauStatus.Bereit, zeile.Status);
        Assert.Contains(ergebnis.Hinweise, h => h.Contains("Kataster", StringComparison.Ordinal));
    }

    [Fact]
    public void Vorschau_schreibt_nichts()
    {
        using var f = new Fixture();
        f.Haltungspdf("Haltung.pdf", "1000-2000");
        f.Haltungspdf("Sammel.pdf", "3000-4000", "5000-6000");
        f.Film("20260712_1000-2000.mp4", "film-a");
        f.Schachtsammelpdf("Schaechte.pdf", "74467", "74468");
        f.Textpdf("Dichtheit.pdf", "Prufgegenstand / Haltung 6928 -> 6927", "Datum 2026/07/12");
        var vorher = f.Schnappschuss();

        f.PlaneHaltungen();
        f.Plane(VerteilArt.Schaechte);
        f.Plane(VerteilArt.Dichtheit);
        f.Plane(VerteilArt.Haltungen, DistributionVariant.Sanierung);

        Assert.Empty(Directory.EnumerateFileSystemEntries(f.Ziel));
        Assert.Equal(vorher, f.Schnappschuss());
    }

    [Fact]
    public void Abbruch_wird_weitergereicht()
    {
        using var f = new Fixture();
        f.Haltungspdf("Haltung.pdf", "1000-2000");
        using var abbruch = new CancellationTokenSource();
        abbruch.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => new VerteilVorschauService().Plane(
            f.Anfrage(VerteilArt.Haltungen, DistributionVariant.Normal, null, null), null, abbruch.Token));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _wurzel = Path.Combine(Path.GetTempPath(), "sewerstudio-verteilvorschau-" + Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Quelle = Directory.CreateDirectory(Path.Combine(_wurzel, "Quelle")).FullName;
            Filme = Directory.CreateDirectory(Path.Combine(_wurzel, "Filme")).FullName;
            Ziel = Directory.CreateDirectory(Path.Combine(_wurzel, "Ziel")).FullName;
        }

        public string Quelle { get; }
        public string Filme { get; }
        public string Ziel { get; }

        public VerteilVorschauErgebnis PlaneHaltungen() => Plane(VerteilArt.Haltungen);

        public VerteilVorschauErgebnis Plane(
            VerteilArt art,
            DistributionVariant ablage = DistributionVariant.Normal,
            Project? projekt = null,
            DistributionTargetConfig? baum = null)
            => new VerteilVorschauService().Plane(Anfrage(art, ablage, projekt, baum), null, CancellationToken.None);

        public VerteilVorschauAnfrage Anfrage(
            VerteilArt art, DistributionVariant ablage, Project? projekt, DistributionTargetConfig? baum)
            => new(art, ablage, VerteilQuelle.PdfOrdner(Quelle),
                art == VerteilArt.Haltungen ? Filme : null, Ziel, baum, projekt);

        public string Haltungspdf(string name, params string[] haltungen)
        {
            var pfad = Path.Combine(Quelle, name);
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            foreach (var haltung in haltungen)
            {
                var page = builder.AddPage(PageSize.A4);
                page.AddText($"Haltungsinspektion - {Datum:dd.MM.yyyy} - {haltung}", 14, new PdfPoint(40, 780), font);
                page.AddText("Leitungsbericht", 12, new PdfPoint(40, 740), font);
            }

            File.WriteAllBytes(pfad, builder.Build());
            return pfad;
        }

        public string Schachtsammelpdf(string name, params string[] schaechte)
        {
            var pfad = Path.Combine(Quelle, name);
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            foreach (var schacht in schaechte)
            {
                var page = builder.AddPage(PageSize.A4);
                page.AddText($"Projekt: Test Datum: {Datum:dd.MM.yyyy}", 12, new PdfPoint(40, 780), font);
                page.AddText($"Schachtprotokoll Schacht Nr. {schacht}", 18, new PdfPoint(40, 740), font);
                page.AddText("STAMMDATEN & SKIZZE", 12, new PdfPoint(40, 700), font);
            }

            File.WriteAllBytes(pfad, builder.Build());
            return pfad;
        }

        public string Textpdf(string name, params string[] zeilen)
        {
            var pfad = Path.Combine(Quelle, name);
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            var y = 780;
            foreach (var zeile in zeilen)
            {
                page.AddText(zeile, 12, new PdfPoint(40, y), font);
                y -= 40;
            }

            File.WriteAllBytes(pfad, builder.Build());
            return pfad;
        }

        public void Film(string name, string inhalt)
            => File.WriteAllText(Path.Combine(Filme, name), inhalt);

        /// <summary>Alle Dateien samt Groesse und Aenderungszeit unter der Testwurzel.</summary>
        public string Schnappschuss()
            => string.Join("\n", Directory.EnumerateFileSystemEntries(_wurzel, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(p => File.Exists(p)
                    ? $"{p}|{new FileInfo(p).Length}|{File.GetLastWriteTimeUtc(p).Ticks}"
                    : p + "|dir"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_wurzel))
                    Directory.Delete(_wurzel, recursive: true);
            }
            catch
            {
                // Aufraeumfehler duerfen das Testergebnis nicht verdecken.
            }
        }
    }
}
