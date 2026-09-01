using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ImportSourcePathGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"ImportSourcePathGuardTests_{Guid.NewGuid():N}");

    public ImportSourcePathGuardTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryInspectFile_Lehnt_Fehlenden_Pfad_Ab(string? path)
    {
        var ok = ImportSourcePathGuard.TryInspectFile(path, out var safePath, out var exists, out var error);

        Assert.False(ok);
        Assert.Empty(safePath);
        Assert.False(exists);
        Assert.Equal("Quellenpfad fehlt.", error);
    }

    [Fact]
    public void TryInspectFile_Akzeptiert_Vorhandene_Lokale_Datei()
    {
        var path = Path.Combine(_root, "quelle.pdf");
        File.WriteAllText(path, "test");

        var ok = ImportSourcePathGuard.TryInspectFile(path, out var safePath, out var exists, out var error);

        Assert.True(ok);
        Assert.Equal(Path.GetFullPath(path), safePath);
        Assert.True(exists);
        Assert.Null(error);
    }

    [Fact]
    public void TryInspectFile_Meldet_Fehlende_Datei_Als_Sicheren_Nichtfund()
    {
        var path = Path.Combine(_root, "fehlt.pdf");

        var ok = ImportSourcePathGuard.TryInspectFile(path, out var safePath, out var exists, out var error);

        Assert.True(ok);
        Assert.Equal(Path.GetFullPath(path), safePath);
        Assert.False(exists);
        Assert.Null(error);
    }

    [Fact]
    public void TryInspectDirectory_Unterscheidet_Datei_Und_Ordner()
    {
        var file = Path.Combine(_root, "quelle.xtf");
        File.WriteAllText(file, "test");

        Assert.True(ImportSourcePathGuard.TryInspectDirectory(
            _root, out var directoryPath, out var directoryExists, out var directoryError));
        Assert.Equal(Path.GetFullPath(_root), directoryPath);
        Assert.True(directoryExists);
        Assert.Null(directoryError);

        Assert.False(ImportSourcePathGuard.TryInspectDirectory(
            file, out var rejectedPath, out var rejectedExists, out var rejectedError));
        Assert.Empty(rejectedPath);
        Assert.False(rejectedExists);
        Assert.Contains("kein Ordner", rejectedError, StringComparison.Ordinal);
    }

    [Fact]
    public void TryInspectFile_Lehnt_Ordner_Und_Datei_Als_Elternsegment_Ab()
    {
        Assert.False(ImportSourcePathGuard.TryInspectFile(
            _root, out var directoryPath, out var directoryExists, out var directoryError));
        Assert.Empty(directoryPath);
        Assert.False(directoryExists);
        Assert.Contains("keine Datei", directoryError, StringComparison.Ordinal);

        var file = Path.Combine(_root, "datei");
        File.WriteAllText(file, "test");
        var child = Path.Combine(file, "kind.pdf");
        Assert.False(ImportSourcePathGuard.TryInspectFile(
            child, out var childPath, out var childExists, out var childError));
        Assert.Empty(childPath);
        Assert.False(childExists);
        Assert.Contains("Datei statt eines Ordners", childError, StringComparison.Ordinal);
    }

    [Fact]
    public void TryInspectFile_Lehnt_Unc_Pfad_Ab()
    {
        var ok = ImportSourcePathGuard.TryInspectFile(
            @"\\server\freigabe\quelle.pdf", out var safePath, out var exists, out var error);

        Assert.False(ok);
        Assert.Empty(safePath);
        Assert.False(exists);
        Assert.Equal("UNC-Quellenpfad wird nicht gelesen.", error);
    }

    [Fact]
    public void TryInspectDirectory_Lehnt_Laufwerkswurzel_Ab()
    {
        var root = Path.GetPathRoot(_root)!;

        var ok = ImportSourcePathGuard.TryInspectDirectory(
            root, out var safePath, out var exists, out var error);

        Assert.False(ok);
        Assert.Equal(Path.GetFullPath(root), safePath);
        Assert.False(exists);
        Assert.Contains("nicht auf eine Datei oder einen Unterordner", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryInspectFile_Meldet_Ungueltigen_Pfad_Als_Prueffehler()
    {
        var ok = ImportSourcePathGuard.TryInspectFile(
            "ungueltig\0pfad", out var safePath, out var exists, out var error);

        Assert.False(ok);
        Assert.Empty(safePath);
        Assert.False(exists);
        Assert.StartsWith("Quellenpfad konnte nicht sicher geprueft werden:", error, StringComparison.Ordinal);
    }

    [JunctionFact]
    public void TryInspectFile_Lehnt_Verknuepftes_Elternsegment_Ab()
    {
        var external = Path.Combine(_root, "extern");
        var link = Path.Combine(_root, "link");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "quelle.pdf"), "test");
        JunctionTestSupport.CreateDirectoryLink(link, external);

        var ok = ImportSourcePathGuard.TryInspectFile(
            Path.Combine(link, "quelle.pdf"), out var safePath, out var exists, out var error);

        Assert.False(ok);
        Assert.Empty(safePath);
        Assert.False(exists);
        Assert.Contains("Verknuepfung", error, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            var link = Path.Combine(_root, "link");
            if (Directory.Exists(link))
                Directory.Delete(link);
        }
        catch
        {
            // Nur Test-Aufraeumen.
        }

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Nur Test-Aufraeumen.
        }
    }
}
