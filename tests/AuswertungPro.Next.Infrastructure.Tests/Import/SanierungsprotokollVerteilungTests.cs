using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Buerglen 2026-09-09: Alle zehn Dichtheitspruefungen und alle neun Aushaerte-
/// protokolle der Quelle landeten im Schachtordner statt bei ihrer Haltung — eines
/// davon unter der Chargen-Nr. des Liners (<c>1009336029</c>), die es als Schachtnummer
/// las. Diese Protokolle nennen ihre Haltung nur als WinCan-Laufnummer, und eine
/// Dichtheitspruefung deckt oft eine ganze Pruefstrecke ueber mehrere Haltungen ab.
/// </summary>
public sealed class SanierungsprotokollVerteilungTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"sanierungsprotokoll-{Guid.NewGuid():N}");

    [Fact]
    public void Dichtheitspruefung_LandetBeiIhrerHaltung()
    {
        var (quelle, projekt, projektOrdner) = Aufbau();
        Haltung(projekt, "60248-60247", "H66");
        SchreibePdf(quelle, "DP H66.pdf",
            "Druckpruefprotokoll", "Von Schacht: 60248", "Bis Schacht: 60247",
            "Haltung: H66", "Pruefdatum: 17.08.2026", "Norm: SIA 190");

        var ergebnis = Verteile(projekt, projektOrdner, quelle);

        Assert.Equal(1, ergebnis.Verteilt);
        Assert.Equal(0, ergebnis.NichtZugeordnet);
        var datei = Assert.Single(Dateien(projektOrdner, "60248-60247"));
        Assert.Equal("20260817_60248-60247_DP.pdf", Path.GetFileName(datei));
    }

    [Fact]
    public void SammelPruefung_LandetInBeidenHaltungen()
    {
        // Eine Pruefung, eine Pruefstrecke von 44.75 m, zwei Haltungen. Das Schachtpaar
        // 59435/60191 ist KEINE Haltung des Projekts — der alte Weg haette daraus einen
        // neuen Ordner "59435-60191" gebaut.
        var (quelle, projekt, projektOrdner) = Aufbau();
        Haltung(projekt, "59435-60284", "H12");
        Haltung(projekt, "60284-60191", "H13");
        SchreibePdf(quelle, "DP H12_H13.pdf",
            "Druckpruefprotokoll", "Von Schacht: 59435", "Bis Schacht: 60191",
            "Haltung: H12 H13", "Pruefdatum: 18.08.2026", "Norm: SIA 190");

        var ergebnis = Verteile(projekt, projektOrdner, quelle);

        Assert.Equal(2, ergebnis.Verteilt);
        Assert.Equal("20260818_59435-60284_DP.pdf", Path.GetFileName(Assert.Single(Dateien(projektOrdner, "59435-60284"))));
        Assert.Equal("20260818_60284-60191_DP.pdf", Path.GetFileName(Assert.Single(Dateien(projektOrdner, "60284-60191"))));
        Assert.False(Directory.Exists(Path.Combine(projektOrdner, "Haltungen_Verteilt", "59435-60191")));
    }

    [Fact]
    public void Aushaerteprotokoll_LandetBeiSeinerHaltung()
    {
        var (quelle, projekt, projektOrdner) = Aufbau();
        Haltung(projekt, "60248-60247", "H66");
        SchreibePdf(quelle, "Aushaertungsprotokoll H66.pdf",
            "Aushaerteprotokoll", "Haltung: H66", "Linertyp: S+ Standard",
            "Chargen-Nr.: 1009336029", "Einbaudatum: 17.08.2026");

        var ergebnis = Verteile(projekt, projektOrdner, quelle);

        Assert.Equal(1, ergebnis.Verteilt);
        var datei = Assert.Single(Dateien(projektOrdner, "60248-60247"));
        Assert.Equal("20260817_60248-60247_AH.pdf", Path.GetFileName(datei));
    }

    [Fact]
    public void ChargenNummer_WirdNieZuEinemOrdner()
    {
        // Der Kern des Buerglen-Fehlers: Die einzige lange Zahl im Aushaerteprotokoll ist
        // die Chargen-Nr. des Liners. Ohne bekannte Haltung entsteht daraus gar nichts.
        var (quelle, projekt, projektOrdner) = Aufbau();
        Haltung(projekt, "60248-60247", "H66");
        SchreibePdf(quelle, "Aushaertungsprotokoll H99.pdf",
            "Aushaerteprotokoll", "Haltung: H99", "Chargen-Nr.: 1009336029");

        var ergebnis = Verteile(projekt, projektOrdner, quelle);

        Assert.Equal(0, ergebnis.Verteilt);
        Assert.Equal(1, ergebnis.NichtZugeordnet);
        Assert.False(Directory.Exists(Path.Combine(projektOrdner, "Haltungen_Verteilt", "1009336029")));
        Assert.Contains(ergebnis.Messages, m => m.Contains("Aushaertungsprotokoll H99.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public void ZweiterLauf_KopiertNichtsEinZweitesMal()
    {
        var (quelle, projekt, projektOrdner) = Aufbau();
        Haltung(projekt, "59435-60284", "H12");
        Haltung(projekt, "60284-60191", "H13");
        SchreibePdf(quelle, "DP H12_H13.pdf",
            "Druckpruefprotokoll", "Haltung: H12 H13", "Pruefdatum: 18.08.2026", "Norm: SIA 190");

        Verteile(projekt, projektOrdner, quelle);
        var nachErstem = AllePdf(projektOrdner).Count;
        var zweiter = Verteile(projekt, projektOrdner, quelle);

        Assert.Equal(2, nachErstem);
        Assert.Equal(nachErstem, AllePdf(projektOrdner).Count);
        Assert.Equal(0, zweiter.Verteilt);
    }

    [Fact]
    public void HaltungOhneWinCanBezeichnung_LaesstDenSchachtwegUnveraendert()
    {
        // Ohne Bezeichnung greift der neue Weg nicht; der bestehende Weg ueber das
        // Schachtpaar im Inhalt muss unveraendert weiterlaufen.
        var (quelle, projekt, projektOrdner) = Aufbau();
        SchreibePdf(quelle, "pruefung.pdf",
            "Dichtheitspruefung nach SIA 190", "oberer Schacht: 100",
            "unterer Schacht: 200", "14.08.2026");

        var ergebnis = Verteile(projekt, projektOrdner, quelle);

        Assert.Equal(1, ergebnis.Verteilt);
        Assert.NotEmpty(AllePdf(projektOrdner));
    }

    // ---------------------------------------------------------------------

    private static DichtheitImportDistributor.Result Verteile(
        Project projekt, string projektOrdner, string quelle)
        => new DichtheitImportDistributionService().Distribute(projekt, projektOrdner, quelle);

    private (string Quelle, Project Projekt, string ProjektOrdner) Aufbau()
    {
        var quelle = Path.Combine(_root, "Quelle");
        var projektOrdner = Path.Combine(_root, "Projekt");
        Directory.CreateDirectory(quelle);
        Directory.CreateDirectory(projektOrdner);
        return (quelle, new Project(), projektOrdner);
    }

    private static void Haltung(Project projekt, string name, string? bezeichnung)
    {
        var record = new HaltungRecord { ImportBezeichnung = bezeichnung };
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Legacy, userEdited: false);
        projekt.Data.Add(record);
    }

    private static void SchreibePdf(string ordner, string name, params string[] zeilen)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780;
        foreach (var zeile in zeilen)
        {
            page.AddText(zeile, 12, new PdfPoint(40, y), font);
            y -= 30;
        }

        File.WriteAllBytes(Path.Combine(ordner, name), builder.Build());
    }

    private static IReadOnlyList<string> Dateien(string projektOrdner, string haltung)
    {
        var dir = Path.Combine(projektOrdner, "Haltungen_Verteilt", haltung);
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.pdf") : [];
    }

    private static IReadOnlyList<string> AllePdf(string projektOrdner)
    {
        var dir = Path.Combine(projektOrdner, "Haltungen_Verteilt");
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.pdf", SearchOption.AllDirectories)
            : [];
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { }
    }
}
