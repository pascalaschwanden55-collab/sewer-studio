using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests.Protocol;

public sealed class VsaCatalogFilePathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "VsaCatalogFilePathResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_verwendet_Umgebungsordner_und_Manifest_aus_dem_App_Datenordner()
    {
        var catalogRoot = Path.Combine(_root, "Catalogs");
        var baseDirectory = Path.Combine(_root, "App");
        Directory.CreateDirectory(catalogRoot);
        Directory.CreateDirectory(Path.Combine(baseDirectory, "Data"));

        var sectionPath = Path.Combine(catalogRoot, Vsa2019CatalogResolver.SectionCatalogFileName);
        var nodePath = Path.Combine(catalogRoot, Vsa2019CatalogResolver.NodeCatalogFileName);
        var manifestPath = Path.Combine(baseDirectory, "Data", VsaCatalogPathNames.KekManifestFileName);
        File.WriteAllText(sectionPath, "<sec />");
        File.WriteAllText(nodePath, "<nod />");
        File.WriteAllText(manifestPath, "{}");

        var environment = new Dictionary<string, string?>
        {
            [VsaCatalogPathNames.SectionCatalogRootEnvironmentVariable] = catalogRoot,
            [VsaCatalogPathNames.NodeCatalogRootEnvironmentVariable] = catalogRoot
        };
        IVsaCatalogPathResolver resolver = new VsaCatalogFilePathResolver();

        var result = resolver.Resolve(new VsaCatalogPathRequest(
            SectionCatalogPath: null,
            NodeCatalogPath: null,
            WinCanCatalogDirectory: null,
            LastProjectPath: null,
            BaseDirectory: baseDirectory,
            EnvironmentVariableReader: name => environment.GetValueOrDefault(name)));

        Assert.Equal(sectionPath, result.SectionCatalogPath);
        Assert.Equal(nodePath, result.NodeCatalogPath);
        Assert.Equal(manifestPath, result.KekManifestPath);
        Assert.Equal(new[] { manifestPath, sectionPath, nodePath }, result.SourcePaths);
    }

    /// <summary>Katalogart als Theory-Parameter: waehlt Dateiname, Umgebungsvariablen,
    /// welches Request-Feld den konfigurierten Pfad traegt und welches Result-Feld gelesen wird.</summary>
    public static IEnumerable<object[]> Katalogarten()
    {
        yield return new object[] { "Section" };
        yield return new object[] { "Node" };
    }

    private static string FileNameFor(string kind)
        => kind == "Section" ? Vsa2019CatalogResolver.SectionCatalogFileName : Vsa2019CatalogResolver.NodeCatalogFileName;

    private static string PathEnvVarFor(string kind)
        => kind == "Section"
            ? VsaCatalogPathNames.SectionCatalogPathEnvironmentVariable
            : VsaCatalogPathNames.NodeCatalogPathEnvironmentVariable;

    private static string RootEnvVarFor(string kind)
        => kind == "Section"
            ? VsaCatalogPathNames.SectionCatalogRootEnvironmentVariable
            : VsaCatalogPathNames.NodeCatalogRootEnvironmentVariable;

    private static string? SelectedPath(string kind, VsaCatalogPathResult result)
        => kind == "Section" ? result.SectionCatalogPath : result.NodeCatalogPath;

    /// <summary>
    /// Baut den Request fuer die getestete Katalogart <paramref name="kind"/>. Die jeweils
    /// ANDERE Katalogart bekommt einen garantiert gueltigen synthetischen konfigurierten Pfad
    /// unter <paramref name="baseDirectory"/>, damit ihre Suche auf der ersten Stufe endet und
    /// niemals GetDefaultCatalogRoots (Systemordner ausserhalb des Projekts) erreicht.
    /// </summary>
    private static VsaCatalogPathRequest BuildRequest(
        string kind,
        string? configuredPath,
        string? winCanDir,
        Dictionary<string, string?> environment,
        string baseDirectory)
    {
        var otherKind = kind == "Section" ? "Node" : "Section";
        var otherKindConfiguredPath = EnsureOtherKindCatalog(baseDirectory, otherKind);

        return new VsaCatalogPathRequest(
            SectionCatalogPath: kind == "Section" ? configuredPath : otherKindConfiguredPath,
            NodeCatalogPath: kind == "Node" ? configuredPath : otherKindConfiguredPath,
            WinCanCatalogDirectory: winCanDir,
            LastProjectPath: null,
            BaseDirectory: baseDirectory,
            EnvironmentVariableReader: name => environment.GetValueOrDefault(name));
    }

    private static string EnsureOtherKindCatalog(string baseDirectory, string kind)
    {
        var dir = Path.Combine(baseDirectory, "AndereKatalogart_" + kind);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, FileNameFor(kind));
        if (!File.Exists(path))
            File.WriteAllText(path, "<x />");
        return path;
    }

    [Theory]
    [MemberData(nameof(Katalogarten))]
    public void Resolve_konfigurierter_Pfad_hat_Vorrang_vor_Umgebung_und_WinCan(string kind)
    {
        var configuredDir = Path.Combine(_root, "Configured");
        var envFileDir = Path.Combine(_root, "EnvFile");
        var envRootDir = Path.Combine(_root, "EnvRoot");
        var winCanDir = Path.Combine(_root, "WinCan");
        Directory.CreateDirectory(configuredDir);
        Directory.CreateDirectory(envFileDir);
        Directory.CreateDirectory(envRootDir);
        Directory.CreateDirectory(winCanDir);

        var fileName = FileNameFor(kind);
        var configuredPath = Path.Combine(configuredDir, fileName);
        File.WriteAllText(configuredPath, "<x />");
        var envFilePath = Path.Combine(envFileDir, fileName);
        File.WriteAllText(envFilePath, "<x />");
        var envRootPath = Path.Combine(envRootDir, fileName);
        File.WriteAllText(envRootPath, "<x />");
        var winCanPath = Path.Combine(winCanDir, fileName);
        File.WriteAllText(winCanPath, "<x />");

        var environment = new Dictionary<string, string?>
        {
            [PathEnvVarFor(kind)] = envFilePath,
            [RootEnvVarFor(kind)] = envRootDir
        };
        IVsaCatalogPathResolver resolver = new VsaCatalogFilePathResolver();

        var result = resolver.Resolve(BuildRequest(kind, configuredPath, winCanDir, environment, _root));

        Assert.Equal(configuredPath, SelectedPath(kind, result));
    }

    [Theory]
    [MemberData(nameof(Katalogarten))]
    public void Resolve_Umgebungsdatei_hat_Vorrang_vor_Umgebungsordner_und_WinCan(string kind)
    {
        var envFileDir = Path.Combine(_root, "EnvFile");
        var envRootDir = Path.Combine(_root, "EnvRoot");
        var winCanDir = Path.Combine(_root, "WinCan");
        Directory.CreateDirectory(envFileDir);
        Directory.CreateDirectory(envRootDir);
        Directory.CreateDirectory(winCanDir);

        var fileName = FileNameFor(kind);
        var envFilePath = Path.Combine(envFileDir, fileName);
        File.WriteAllText(envFilePath, "<x />");
        var envRootPath = Path.Combine(envRootDir, fileName);
        File.WriteAllText(envRootPath, "<x />");
        var winCanPath = Path.Combine(winCanDir, fileName);
        File.WriteAllText(winCanPath, "<x />");

        var environment = new Dictionary<string, string?>
        {
            [PathEnvVarFor(kind)] = envFilePath,
            [RootEnvVarFor(kind)] = envRootDir
        };
        IVsaCatalogPathResolver resolver = new VsaCatalogFilePathResolver();

        var result = resolver.Resolve(BuildRequest(kind, null, winCanDir, environment, _root));

        Assert.Equal(envFilePath, SelectedPath(kind, result));
    }

    [Theory]
    [MemberData(nameof(Katalogarten))]
    public void Resolve_Umgebungsordner_hat_Vorrang_vor_WinCan_Ordner(string kind)
    {
        var envRootDir = Path.Combine(_root, "EnvRoot");
        var winCanDir = Path.Combine(_root, "WinCan");
        Directory.CreateDirectory(envRootDir);
        Directory.CreateDirectory(winCanDir);

        var fileName = FileNameFor(kind);
        var envRootPath = Path.Combine(envRootDir, fileName);
        File.WriteAllText(envRootPath, "<x />");
        var winCanPath = Path.Combine(winCanDir, fileName);
        File.WriteAllText(winCanPath, "<x />");

        var environment = new Dictionary<string, string?>
        {
            [RootEnvVarFor(kind)] = envRootDir
        };
        IVsaCatalogPathResolver resolver = new VsaCatalogFilePathResolver();

        var result = resolver.Resolve(BuildRequest(kind, null, winCanDir, environment, _root));

        Assert.Equal(envRootPath, SelectedPath(kind, result));
    }

    [Theory]
    [MemberData(nameof(Katalogarten))]
    public void Resolve_WinCan_Ordner_wird_verwendet_wenn_nichts_anderes_gesetzt_ist(string kind)
    {
        var winCanDir = Path.Combine(_root, "WinCan");
        Directory.CreateDirectory(winCanDir);
        var fileName = FileNameFor(kind);
        var winCanPath = Path.Combine(winCanDir, fileName);
        File.WriteAllText(winCanPath, "<x />");

        IVsaCatalogPathResolver resolver = new VsaCatalogFilePathResolver();

        var result = resolver.Resolve(BuildRequest(kind, null, winCanDir, new Dictionary<string, string?>(), _root));

        Assert.Equal(winCanPath, SelectedPath(kind, result));
    }

    [Theory]
    [MemberData(nameof(Katalogarten))]
    public void Resolve_ueberspringt_fehlenden_konfigurierten_Pfad_und_fehlende_Umgebungsdatei_und_faellt_auf_Umgebungsordner_zurueck(string kind)
    {
        // Konfigurierter Pfad UND Umgebungsdatei sind ausdruecklich gesetzt, zeigen aber auf
        // nicht vorhandene Dateien. Nur der nachrangige, projektlokale Umgebungsordner ist gueltig.
        var envRootDir = Path.Combine(_root, "EnvRoot");
        Directory.CreateDirectory(envRootDir);
        var fileName = FileNameFor(kind);
        var envRootPath = Path.Combine(envRootDir, fileName);
        File.WriteAllText(envRootPath, "<x />");

        var missingConfigured = Path.Combine(_root, "fehlt-konfiguriert", fileName);
        var missingEnvFile = Path.Combine(_root, "fehlt-umgebungsdatei", fileName);
        var environment = new Dictionary<string, string?>
        {
            [PathEnvVarFor(kind)] = missingEnvFile,
            [RootEnvVarFor(kind)] = envRootDir
        };
        IVsaCatalogPathResolver resolver = new VsaCatalogFilePathResolver();

        var result = resolver.Resolve(BuildRequest(kind, missingConfigured, null, environment, _root));

        Assert.Equal(envRootPath, SelectedPath(kind, result));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }
}
