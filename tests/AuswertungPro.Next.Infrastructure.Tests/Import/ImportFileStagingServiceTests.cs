using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ImportFileStagingServiceTests
{
    [Fact]
    public void Begin_meldet_den_eigenen_Guid_Arbeitsordner_neben_der_Projektdatei()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();

        using var session = Begin(projectPath);

        Assert.Equal(
            temp.StagingRoot,
            System.IO.Path.GetDirectoryName(session.StagingRoot),
            ignoreCase: true);
        Assert.True(Guid.TryParseExact(
            System.IO.Path.GetFileName(session.StagingRoot),
            "N",
            out _));
    }

    [Fact]
    public void StageCopy_bereitet_nur_vor_und_Accept_behaelt_die_veroeffentlichte_Datei()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/protokoll.pdf", "inhalt");
        var targetDirectory = Path.Combine(temp.ProjectRoot, "Imports", "PDF");
        var target = Path.Combine(targetDirectory, "protokoll.pdf");

        using (var session = Begin(projectPath))
        {
            var planned = session.StageCopy(source, targetDirectory);

            Assert.Equal(target, planned, ignoreCase: true);
            Assert.False(File.Exists(target));

            session.Publish();
            Assert.True(File.Exists(target));
            session.Accept();
        }

        Assert.Equal("inhalt", File.ReadAllText(target));
        Assert.False(Directory.Exists(temp.StagingRoot));
    }

    [Fact]
    public void StageCopy_meldet_das_geplante_Rollback_Ziel_bereits_vor_Publish()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/protokoll.pdf", "inhalt");
        var targetDirectory = Path.Combine(temp.ProjectRoot, "Imports", "PDF");

        using var session = Begin(projectPath);
        var target = session.StageCopy(source, targetDirectory);

        var prepared = Assert.Single(session.PreparedFiles);
        Assert.Equal(
            Path.GetRelativePath(temp.ProjectRoot, target),
            prepared.RelativePath,
            ignoreCase: true);
        Assert.NotEmpty(prepared.Sha256);
        Assert.Empty(session.PublishedFiles);
    }

    [Fact]
    public void Vorbereitete_Datei_ist_vor_Publish_ueber_den_Lesepfad_verfuegbar()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/protokoll.pdf", "inhalt");
        var targetDirectory = Path.Combine(temp.ProjectRoot, "Importdateien", "PDF");

        using var session = Begin(projectPath);
        var target = session.StageCopy(source, targetDirectory);

        var readable = Assert.Single(
            session.EnumerateReadableFiles(targetDirectory, "*.pdf", SearchOption.TopDirectoryOnly));
        Assert.Equal(target, readable.TargetPath, ignoreCase: true);
        Assert.False(target.Equals(readable.ReadPath, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("inhalt", File.ReadAllText(readable.ReadPath));
        Assert.Equal(readable.ReadPath, session.ResolveReadPath(target), ignoreCase: true);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void Lesesicht_vereint_vorhandene_und_vorbereitete_Dateien_ohne_Duplikate()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var targetDirectory = Path.Combine(temp.ProjectRoot, "Importdateien", "PDF");
        var existing = temp.CreateFile("Importdateien/PDF/alt.pdf", "alt");
        var source = temp.CreateFile("quelle/neu.pdf", "neu");

        using var session = Begin(projectPath);
        var target = session.StageCopy(source, targetDirectory);

        var files = session.EnumerateReadableFiles(
            targetDirectory,
            "*.pdf",
            SearchOption.TopDirectoryOnly);

        Assert.Equal(2, files.Count);
        Assert.Contains(files, file => file.TargetPath.Equals(existing, StringComparison.OrdinalIgnoreCase)
                                      && file.ReadPath.Equals(existing, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, file => file.TargetPath.Equals(target, StringComparison.OrdinalIgnoreCase)
                                      && !file.ReadPath.Equals(target, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StageGeneratedFile_schreibt_erst_in_den_Arbeitsordner_und_veroeffentlicht_spaeter()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var preferredTarget = Path.Combine(temp.ProjectRoot, "Haltungen_Verteilt", "H1", "H1.pdf");

        using var session = Begin(projectPath);
        var target = session.StageGeneratedFile(
            preferredTarget,
            stagePath => File.WriteAllText(stagePath, "erzeugt"));

        Assert.Equal(preferredTarget, target, ignoreCase: true);
        Assert.False(File.Exists(target));
        Assert.Equal("erzeugt", File.ReadAllText(session.ResolveReadPath(target)));

        session.Publish();
        Assert.Equal("erzeugt", File.ReadAllText(target));
        session.Accept();
    }

    [Fact]
    public void StageGeneratedFile_behaelt_vorhandene_Datei_und_waehlt_bei_anderem_Inhalt_neuen_Namen()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var existing = temp.CreateFile("Haltungen_Verteilt/H1/H1.pdf", "alt");

        using var session = Begin(projectPath);
        var target = session.StageGeneratedFile(
            existing,
            stagePath => File.WriteAllText(stagePath, "neu"));

        Assert.False(existing.Equals(target, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("alt", File.ReadAllText(existing));
        Assert.Equal("neu", File.ReadAllText(session.ResolveReadPath(target)));
    }

    [Fact]
    public void Dispose_vor_Publish_entfernt_nur_den_Arbeitsordner()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/daten.xtf", "xtf");
        var target = Path.Combine(temp.ProjectRoot, "Imports", "XTF", "daten.xtf");

        using (var session = Begin(projectPath))
            session.StageCopy(source, Path.GetDirectoryName(target)!);

        Assert.False(File.Exists(target));
        Assert.True(File.Exists(source));
        Assert.False(Directory.Exists(temp.StagingRoot));
    }

    [Fact]
    public void Dispose_nach_Publish_ohne_Accept_nimmt_neue_Datei_zurueck()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/foto.jpg", "foto");
        var target = Path.Combine(temp.ProjectRoot, "Fotos", "Haltungen", "H1", "foto.jpg");

        using (var session = Begin(projectPath))
        {
            session.StageCopy(source, Path.GetDirectoryName(target)!);
            session.Publish();
            Assert.True(File.Exists(target));
        }

        Assert.False(File.Exists(target));
        Assert.True(File.Exists(source));
        Assert.False(Directory.Exists(temp.StagingRoot));
    }

    [Fact]
    public void Wiederverwendete_Datei_wird_beim_Rollback_nie_geloescht()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/gleich.pdf", "identisch");
        var target = temp.CreateFile("Imports/PDF/gleich.pdf", "identisch");

        using (var session = Begin(projectPath))
        {
            Assert.Equal(
                target,
                session.StageCopy(source, Path.GetDirectoryName(target)!),
                ignoreCase: true);
            session.Publish();
        }

        Assert.Equal("identisch", File.ReadAllText(target));
    }

    [Fact]
    public void Gleich_grosse_aber_andere_Datei_bekommt_einen_eigenen_Zielnamen()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/foto.jpg", "NEU1");
        var existing = temp.CreateFile("Fotos/Haltungen/H1/foto.jpg", "ALT1");

        using var session = Begin(projectPath);
        var planned = session.StageCopy(
            source,
            Path.GetDirectoryName(existing)!,
            () => new DateTime(2026, 7, 17, 12, 30, 0));
        session.Publish();
        session.Accept();

        Assert.False(existing.Equals(planned, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("foto_20260717_123000", Path.GetFileName(planned), StringComparison.Ordinal);
        Assert.Equal("ALT1", File.ReadAllText(existing));
        Assert.Equal("NEU1", File.ReadAllText(planned));
    }

    [Fact]
    public void Publish_Konflikt_nimmt_bereits_veroeffentlichte_Dateien_zurueck()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var firstSource = temp.CreateFile("quelle/a.pdf", "a");
        var secondSource = temp.CreateFile("quelle/b.pdf", "b");
        var targetDirectory = Path.Combine(temp.ProjectRoot, "Imports", "PDF");
        var firstTarget = Path.Combine(targetDirectory, "a.pdf");
        var secondTarget = Path.Combine(targetDirectory, "b.pdf");

        using var session = Begin(projectPath);
        session.StageCopy(firstSource, targetDirectory);
        session.StageCopy(secondSource, targetDirectory);
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(secondTarget, "fremd");

        Assert.Throws<IOException>(session.Publish);

        Assert.False(File.Exists(firstTarget));
        Assert.Equal("fremd", File.ReadAllText(secondTarget));
    }

    [Fact]
    public void StageCopy_verweigert_Ziele_ausserhalb_des_Projektstamms()
    {
        using var temp = new TempDirectory();
        var projectPath = temp.CreateProjectFile();
        var source = temp.CreateFile("quelle/a.pdf", "a");
        var outside = Path.Combine(temp.Path, "ausserhalb");

        using var session = Begin(projectPath);

        var error = Assert.Throws<ArgumentException>(
            () => session.StageCopy(source, outside));
        Assert.Contains("Projektstamm", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IImportFileStagingSession Begin(string projectPath)
        => new ImportFileStagingService().Begin(projectPath)
           ?? throw new InvalidOperationException("Staging-Sitzung fehlt.");

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-import-stage-" + Guid.NewGuid().ToString("N"));
            ProjectRoot = System.IO.Path.Combine(Path, "Projekt");
            Directory.CreateDirectory(ProjectRoot);
        }

        public string Path { get; }
        public string ProjectRoot { get; }
        public string StagingRoot => System.IO.Path.Combine(ProjectRoot, "Projektdateien", ".import-staging");

        public string CreateProjectFile()
        {
            var path = System.IO.Path.Combine(ProjectRoot, "Projektdateien", "projekt.json");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{}");
            return path;
        }

        public string CreateFile(string relativePath, string content)
        {
            var path = System.IO.Path.Combine(
                relativePath.StartsWith("quelle/", StringComparison.Ordinal)
                    ? Path
                    : ProjectRoot,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
