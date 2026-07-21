using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Kins;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KinsImportServiceTests
{
    [Fact]
    public void ImportKinsExport_Fails_WhenRootMissing()
    {
        var sut = new KinsImportService(
            new FakeWinCanImport(Result<ImportStats>.Fail("X", "should not run")),
            new FakeIbakImport(Result<ImportStats>.Fail("X", "should not run")));

        var res = sut.ImportKinsExport(@"Z:\not_existing_kins_path", new Project());

        Assert.False(res.Ok);
        Assert.Equal("KINS_ROOT_MISSING", res.ErrorCode);
    }

    [Fact]
    public void ImportKinsExport_UsesIbak_WhenDatenTxtExists()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "Daten.txt"), "dummy");

        var winCan = new FakeWinCanImport(Result<ImportStats>.Success(new ImportStats(1, 0, 1, 0, 0, Array.Empty<string>())));
        var ibak = new FakeIbakImport(Result<ImportStats>.Success(new ImportStats(2, 1, 1, 0, 0, Array.Empty<string>())));
        var sut = new KinsImportService(winCan, ibak);

        var res = sut.ImportKinsExport(dir.Path, new Project());

        Assert.True(res.Ok, res.ErrorMessage);
        Assert.Equal(0, winCan.CallCount);
        Assert.Equal(1, ibak.CallCount);
        Assert.NotNull(res.Value);
        Assert.Equal(2, res.Value!.Found);
    }

    [Fact]
    public void ImportKinsExport_UsesWinCan_WhenDb3Exists()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "export.db3"), "dummy");

        var winCan = new FakeWinCanImport(Result<ImportStats>.Success(new ImportStats(3, 1, 2, 0, 0, Array.Empty<string>())));
        var ibak = new FakeIbakImport(Result<ImportStats>.Success(new ImportStats(4, 0, 4, 0, 0, Array.Empty<string>())));
        var sut = new KinsImportService(winCan, ibak);

        var res = sut.ImportKinsExport(dir.Path, new Project());

        Assert.True(res.Ok, res.ErrorMessage);
        Assert.Equal(1, winCan.CallCount);
        Assert.Equal(0, ibak.CallCount);
        Assert.NotNull(res.Value);
        Assert.Equal(3, res.Value!.Found);
    }

    [Fact]
    public void ImportKinsExport_FallbacksToWinCan_WhenNoHintsExist()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "readme.txt"), "dummy");

        var winCan = new FakeWinCanImport(Result<ImportStats>.Success(new ImportStats(1, 0, 1, 0, 0, new[] { "ok-w" })));
        var ibak = new FakeIbakImport(Result<ImportStats>.Success(new ImportStats(2, 0, 2, 0, 0, new[] { "ok-i" })));
        var sut = new KinsImportService(winCan, ibak);

        var res = sut.ImportKinsExport(dir.Path, new Project());

        Assert.True(res.Ok, res.ErrorMessage);
        Assert.Equal(1, winCan.CallCount);
        Assert.Equal(1, ibak.CallCount); // Beide Importer als Fallback bei unbekannter Struktur
        Assert.NotNull(res.Value);
        Assert.Equal(3, res.Value!.Found); // WinCan(1) + IBAK(2) = 3
    }

    [Fact]
    public void ImportKinsExport_ParsesKiDvDatenTxt_AndCreatesRecords()
    {
        using var dir = new TempDir();
        var videoFile = Path.Combine(dir.Path, "A001.MPG");
        File.WriteAllText(videoFile, "dummy-video");

        var content = string.Join(Environment.NewLine, new[]
        {
            "Schmutzwasser 23654 -> 23038 UV 450 @Datei=A001.MPG",
            "   0.0m Rohranfang  @Pos=0:00:00",
            "  18.3m Rohrende  @Pos=0:02:23"
        });
        File.WriteAllText(Path.Combine(dir.Path, "kiDVDaten.txt"), content);
        File.WriteAllText(Path.Combine(dir.Path, "kiDVinfo.txt"), "Aufnahmen: 04.12.14 - 05.12.14");

        var winCan = new FakeWinCanImport(Result<ImportStats>.Fail("X", "should not run"));
        var ibak = new FakeIbakImport(Result<ImportStats>.Fail("X", "should not run"));
        var sut = new KinsImportService(winCan, ibak);
        var project = new Project();

        var res = sut.ImportKinsExport(dir.Path, project);

        Assert.True(res.Ok, res.ErrorMessage);
        Assert.Equal(0, winCan.CallCount);
        Assert.Equal(0, ibak.CallCount);
        Assert.Single(project.Data);

        var rec = project.Data[0];
        Assert.Equal("23654-23038", rec.GetFieldValue("Haltungsname"));
        Assert.Equal("A001.MPG", Path.GetFileName(rec.GetFieldValue("Link")));
        Assert.NotNull(rec.Protocol);
        Assert.True(rec.Protocol!.Current.Entries.Count >= 2);
        Assert.Equal("2014", rec.GetFieldValue("Datum_Jahr"));
    }

    [Fact]
    public void ImportKinsExport_LeererImportwert_ueberschreibt_gefuelltes_Feld_nicht()
    {
        using var dir = new TempDir();
        // KINS-Header ohne Material und ohne DN (nur Nutzungsart + Von/Nach).
        var content = string.Join(Environment.NewLine, new[]
        {
            "Schmutzwasser 23654 -> 23038 @Datei=A001.MPG",
            "   0.0m Rohranfang  @Pos=0:00:00"
        });
        File.WriteAllText(Path.Combine(dir.Path, "kiDVDaten.txt"), content);

        // Bestehende Haltung mit gefuelltem Rohrmaterial (z. B. aus XTF-Import).
        var project = new Project();
        var existing = project.CreateNewRecord();
        existing.SetFieldValue("Haltungsname", "23654-23038", FieldSource.Xtf, userEdited: false);
        existing.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Xtf, userEdited: false);
        project.AddRecord(existing);

        var sut = new KinsImportService(
            new FakeWinCanImport(Result<ImportStats>.Fail("X", "should not run")),
            new FakeIbakImport(Result<ImportStats>.Fail("X", "should not run")));

        var res = sut.ImportKinsExport(dir.Path, project);

        Assert.True(res.Ok, res.ErrorMessage);
        var rec = Assert.Single(project.Data);
        // Der leere KINS-Materialwert darf "Beton" nicht leer wischen.
        Assert.Equal("Beton", rec.GetFieldValue("Rohrmaterial"));
    }

    [Fact]
    public void ImportKinsExport_BenenntBestehendeHaltungNichtUm()
    {
        using var dir = new TempDir();
        var content = string.Join(Environment.NewLine, new[]
        {
            "Schmutzwasser 23654 -> 23038 @Datei=A001.MPG",
            "   0.0m Rohranfang  @Pos=0:00:00"
        });
        File.WriteAllText(Path.Combine(dir.Path, "kiDVDaten.txt"), content);

        // Bestehende Haltung mit breiterem Namen; wird per Grenz-Praefix gefunden.
        var project = new Project();
        var existing = project.CreateNewRecord();
        existing.SetFieldValue("Haltungsname", "23654-23038-1", FieldSource.Xtf, userEdited: false);
        project.AddRecord(existing);

        var sut = new KinsImportService(
            new FakeWinCanImport(Result<ImportStats>.Fail("X", "should not run")),
            new FakeIbakImport(Result<ImportStats>.Fail("X", "should not run")));

        var res = sut.ImportKinsExport(dir.Path, project);

        Assert.True(res.Ok, res.ErrorMessage);
        var rec = Assert.Single(project.Data);
        // Der Schluessel darf nicht auf den kuerzeren KINS-Namen "23654-23038" verkuerzt werden.
        Assert.Equal("23654-23038-1", rec.GetFieldValue("Haltungsname"));
    }

    [Fact]
    public void ImportKinsExport_DoesNotLinkVideo_WhenFileNameIsAmbiguous()
    {
        using var dir = new TempDir();
        var videoA = Path.Combine(dir.Path, "A", "A001.MPG");
        var videoB = Path.Combine(dir.Path, "B", "A001.MPG");
        Directory.CreateDirectory(Path.GetDirectoryName(videoA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(videoB)!);
        File.WriteAllText(videoA, "dummy-video-a");
        File.WriteAllText(videoB, "dummy-video-b");

        var content = string.Join(Environment.NewLine, new[]
        {
            "Schmutzwasser 23654 -> 23038 UV 450 @Datei=A001.MPG",
            "   0.0m Rohranfang  @Pos=0:00:00"
        });
        File.WriteAllText(Path.Combine(dir.Path, "kiDVDaten.txt"), content);

        var sut = new KinsImportService(
            new FakeWinCanImport(Result<ImportStats>.Fail("X", "should not run")),
            new FakeIbakImport(Result<ImportStats>.Fail("X", "should not run")));
        var project = new Project();

        var res = sut.ImportKinsExport(dir.Path, project);

        Assert.True(res.Ok, res.ErrorMessage);
        var rec = Assert.Single(project.Data);
        Assert.True(string.IsNullOrWhiteSpace(rec.GetFieldValue("Link")));
        Assert.Contains(res.Value!.Messages, m => m.Contains("mehrdeutig", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeWinCanImport : IWinCanDbImportService
    {
        private readonly Result<ImportStats> _result;
        public int CallCount { get; private set; }

        public FakeWinCanImport(Result<ImportStats> result) => _result = result;

        public Result<ImportStats> ImportWinCanExport(string exportRoot, Project project, ImportRunContext? ctx = null)
        {
            CallCount++;
            return _result;
        }
    }

    private sealed class FakeIbakImport : IIbakImportService
    {
        private readonly Result<ImportStats> _result;
        public int CallCount { get; private set; }

        public FakeIbakImport(Result<ImportStats> result) => _result = result;

        public Result<ImportStats> ImportIbakExport(string exportRoot, Project project, ImportRunContext? ctx = null)
        {
            CallCount++;
            return _result;
        }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kins_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures on CI/local file locks
            }
        }
    }
}
