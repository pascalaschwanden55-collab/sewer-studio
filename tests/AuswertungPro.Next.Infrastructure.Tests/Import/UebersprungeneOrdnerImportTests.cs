using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// R1 (Deepscan 02.10.2026): Ein Ordner, den die sichere Dateisuche auslaesst, muss im
/// Importergebnis stehen und als Fehler zaehlen. Sonst meldet der Import «0 Fehler»,
/// obwohl Daten aus diesem Ordner fehlen.
/// </summary>
public sealed class UebersprungeneOrdnerImportTests
{
    [JunctionFact]
    public void PruefeBaum_nennt_die_Verknuepfung_und_betritt_sie_nicht()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-baustein");
        Directory.CreateDirectory(Path.Combine(baum.Quelle, "normal"));
        var link = baum.Verknuepfe(baum.Quelle);
        var innerer = Path.Combine(baum.Extern, "darunter");
        Directory.CreateDirectory(innerer);

        var zeilen = UebersprungeneOrdner.PruefeBaum(baum.Quelle);

        Assert.Equal(new[] { UebersprungeneOrdnerTestbaum.ErwarteteZeile(link) }, zeilen);
    }

    [Fact]
    public void PruefeBaum_ohne_Luecke_und_ohne_Wurzel_liefert_nichts()
    {
        var wurzel = Directory.CreateTempSubdirectory("r1-leer-");
        try
        {
            Directory.CreateDirectory(Path.Combine(wurzel.FullName, "a", "b"));

            Assert.Empty(UebersprungeneOrdner.PruefeBaum(wurzel.FullName));
            Assert.Empty(UebersprungeneOrdner.PruefeBaum(Path.Combine(wurzel.FullName, "fehlt")));
            Assert.Empty(UebersprungeneOrdner.PruefeBaum(null));
        }
        finally
        {
            wurzel.Delete(recursive: true);
        }
    }

    [Fact]
    public void Meldungen_nennt_unlesbare_Ordner_einmal_und_sortiert()
    {
        var a = Path.Combine(Path.GetTempPath(), $"r1-fehlt-a-{Guid.NewGuid():N}");
        var b = Path.Combine(Path.GetTempPath(), $"r1-fehlt-b-{Guid.NewGuid():N}");

        var zeilen = UebersprungeneOrdner.Meldungen(new[] { b, a, b.ToUpperInvariant(), " " });

        Assert.Equal(
            new[]
            {
                $"Ordner «{a}» übersprungen: nicht lesbar",
                $"Ordner «{b}» übersprungen: nicht lesbar"
            },
            zeilen);
    }

    [Fact]
    public void Ergaenze_zaehlt_jede_Zeile_als_Fehler_und_stellt_sie_voran()
    {
        var stats = new ImportStats(3, 1, 2, 1, 0, new[] { "Importquelle: Test" })
        {
            ErwarteteHaltungen = 7,
            BearbeiteteHaltungen = 5
        };

        var ergebnis = UebersprungeneOrdnerImport.Ergaenze(
            Result<ImportStats>.Success(stats),
            new[] { "Ordner «X» übersprungen: nicht lesbar" });

        Assert.True(ergebnis.Ok);
        var neu = ergebnis.Value!;
        Assert.Equal(2, neu.Errors);
        Assert.Equal(new[] { "Ordner «X» übersprungen: nicht lesbar", "Importquelle: Test" }, neu.Messages);
        Assert.Equal(3, neu.Found);
        Assert.Equal(7, neu.ErwarteteHaltungen);
        Assert.Equal(5, neu.BearbeiteteHaltungen);
    }

    [Fact]
    public void Ergaenze_zaehlt_eine_schon_gemeldete_Zeile_nicht_doppelt()
    {
        const string zeile = "Ordner «X» übersprungen: nicht lesbar";
        var stats = new ImportStats(1, 0, 1, 1, 0, new[] { "WinCan: " + zeile });

        var ergebnis = UebersprungeneOrdnerImport.Ergaenze(
            Result<ImportStats>.Success(stats),
            new[] { zeile });

        Assert.Same(stats, ergebnis.Value);
    }

    [Fact]
    public void Ergaenze_haengt_die_Zeile_an_einen_gescheiterten_Import_an()
    {
        var ergebnis = UebersprungeneOrdnerImport.Ergaenze(
            Result<ImportStats>.Fail("CODE", "Keine Datenbank gefunden."),
            new[] { "Ordner «X» übersprungen: nicht lesbar" });

        Assert.False(ergebnis.Ok);
        Assert.Equal("CODE", ergebnis.ErrorCode);
        Assert.Equal("Keine Datenbank gefunden. | Ordner «X» übersprungen: nicht lesbar", ergebnis.ErrorMessage);
    }

    [JunctionFact]
    public void Kins_meldet_den_uebersprungenen_Ordner_als_Fehler()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-kins");
        File.WriteAllText(Path.Combine(baum.Quelle, "Daten.txt"), "dummy");
        File.WriteAllText(Path.Combine(baum.Extern, "kiDVDaten.txt"), "fehlt im Lauf");
        var link = baum.Verknuepfe(baum.Quelle);
        var ibak = new FesterImport<IIbakImportService>(
            Result<ImportStats>.Success(new ImportStats(2, 1, 1, 0, 0, new[] { "ok" })));
        var sut = new KinsImportService(
            new FesterImport<IWinCanDbImportService>(Result<ImportStats>.Fail("X", "nicht erwartet")),
            ibak);

        var ergebnis = sut.ImportKinsExport(baum.Quelle, new Project());

        Assert.True(ergebnis.Ok, ergebnis.ErrorMessage);
        Assert.Equal(1, ergebnis.Value!.Errors);
        Assert.Contains(UebersprungeneOrdnerTestbaum.ErwarteteZeile(link), ergebnis.Value.Messages);
    }

    [JunctionFact]
    public void Kins_zaehlt_die_Meldung_des_WinCan_Teilimports_nicht_doppelt()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-kins-wincan");
        File.WriteAllText(Path.Combine(baum.Quelle, "export.db3"), "dummy");
        var link = baum.Verknuepfe(baum.Quelle);
        var zeile = UebersprungeneOrdnerTestbaum.ErwarteteZeile(link);
        var sut = new KinsImportService(
            new FesterImport<IWinCanDbImportService>(
                Result<ImportStats>.Success(new ImportStats(1, 0, 1, 1, 0, new[] { zeile }))),
            new FesterImport<IIbakImportService>(Result<ImportStats>.Fail("X", "nicht erwartet")));

        var ergebnis = sut.ImportKinsExport(baum.Quelle, new Project());

        Assert.True(ergebnis.Ok, ergebnis.ErrorMessage);
        Assert.Equal(1, ergebnis.Value!.Errors);
        Assert.Contains("WinCan: " + zeile, ergebnis.Value.Messages);
        Assert.DoesNotContain(zeile, ergebnis.Value.Messages);
    }

    [JunctionFact]
    public void WinCan_meldet_den_uebersprungenen_Ordner_im_Erfolg_und_im_Fehlschlag()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-wincan");
        File.WriteAllText(Path.Combine(baum.Extern, "projekt.db3"), "liegt hinter der Verknüpfung");
        var leer = Path.Combine(baum.Wurzel, "nur-verknuepfung");
        var leerLink = baum.Verknuepfe(leer);
        var mitXtf = Path.Combine(baum.Wurzel, "sdf-mit-xtf");
        Directory.CreateDirectory(Path.Combine(mitXtf, "DB"));
        File.WriteAllText(Path.Combine(mitXtf, "DB", "projekt.sdf"), "sdf");
        File.WriteAllText(Path.Combine(mitXtf, "export.xtf"), "xtf");
        var xtfLink = baum.Verknuepfe(mitXtf, "Video");

        try
        {
            var gescheitert = new WinCanDbImportService().ImportWinCanExport(leer, new Project());
            var erfolg = new WinCanDbImportService(new KeinM150(), new ErfolgreicherXtfImport())
                .ImportWinCanExport(mitXtf, new Project());

            Assert.False(gescheitert.Ok);
            Assert.Equal("WINCAN_DB_MISSING", gescheitert.ErrorCode);
            Assert.Contains(UebersprungeneOrdnerTestbaum.ErwarteteZeile(leerLink), gescheitert.ErrorMessage);
            Assert.True(erfolg.Ok, erfolg.ErrorMessage);
            Assert.Equal(1, erfolg.Value!.Errors);
            Assert.Equal(UebersprungeneOrdnerTestbaum.ErwarteteZeile(xtfLink), erfolg.Value.Messages[0]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    [JunctionFact]
    public void Ibak_nennt_den_uebersprungenen_Ordner_wenn_Daten_txt_fehlt()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-ibak");
        File.WriteAllText(Path.Combine(baum.Extern, "Daten.txt"), "liegt hinter der Verknüpfung");
        var link = baum.Verknuepfe(baum.Quelle);

        var ergebnis = new IbakExportImportService().ImportIbakExport(baum.Quelle, new Project());

        Assert.False(ergebnis.Ok);
        Assert.Equal("IBAK_DATEN_MISSING", ergebnis.ErrorCode);
        Assert.Contains(UebersprungeneOrdnerTestbaum.ErwarteteZeile(link), ergebnis.ErrorMessage);
    }

    private sealed class FesterImport<T> : IWinCanDbImportService, IIbakImportService
    {
        private readonly Result<ImportStats> _ergebnis;

        public FesterImport(Result<ImportStats> ergebnis) => _ergebnis = ergebnis;

        public Result<ImportStats> ImportWinCanExport(string exportRoot, Project project, ImportRunContext? ctx = null)
            => _ergebnis;

        public Result<ImportStats> ImportIbakExport(string exportRoot, Project project, ImportRunContext? ctx = null)
            => _ergebnis;
    }

    private sealed class ErfolgreicherXtfImport : IXtfImportService
    {
        public Result<ImportStats> ImportXtfFiles(
            IEnumerable<string> xtfPaths,
            Project project,
            ImportRunContext? ctx = null)
            => Result<ImportStats>.Success(new ImportStats(1, 0, 0, 0, 0, []));
    }

    private sealed class KeinM150 : IM150MdbRowReader
    {
        public bool TryReadRows(
            string mdbPath,
            out List<Dictionary<string, string>> rows,
            out string? error)
        {
            rows = [];
            error = "Nicht erwartet.";
            return false;
        }
    }
}
