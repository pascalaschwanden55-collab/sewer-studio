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
    public void ImportKinsExport_FallbacksToBoth_WhenNoHintsExist()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "readme.txt"), "dummy");

        var winCan = new FakeWinCanImport(Result<ImportStats>.Success(new ImportStats(1, 0, 1, 0, 0, new[] { "ok-w" })));
        var ibak = new FakeIbakImport(Result<ImportStats>.Success(new ImportStats(2, 0, 2, 0, 0, new[] { "ok-i" })));
        var sut = new KinsImportService(winCan, ibak);

        var res = sut.ImportKinsExport(dir.Path, new Project());

        Assert.True(res.Ok, res.ErrorMessage);
        Assert.Equal(1, winCan.CallCount);
        Assert.Equal(1, ibak.CallCount);
        Assert.NotNull(res.Value);
        Assert.Equal(3, res.Value!.Found);
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

    private sealed class FakeWinCanImport : IWinCanDbImportService
    {
        private readonly Result<ImportStats> _result;
        public int CallCount { get; private set; }

        public FakeWinCanImport(Result<ImportStats> result) => _result = result;

        public Result<ImportStats> ImportWinCanExport(string exportRoot, Project project)
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

        public Result<ImportStats> ImportIbakExport(string exportRoot, Project project)
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
