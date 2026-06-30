using System.IO;
using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer <see cref="VideoFolderFallbackResolver.Resolve"/>.
/// Sichert die 3-stufige Fallback-Kette (LastVideoSourceFolder → LastVideoFolder →
/// Projektdatei-Verzeichnis) aus DataPageViewModel ab (verhaltensneutral).
/// </summary>
public sealed class DataPageVideoFolderFallbackResolverTests
{
    [Fact]
    public void Resolve_liefert_LastVideoSourceFolder_wenn_gesetzt()
    {
        var result = VideoFolderFallbackResolver.Resolve(
            lastVideoSourceFolder: @"C:\Videos\Quelle",
            lastVideoFolder: @"C:\Videos\Legacy",
            lastProjectPath: @"C:\Projekte\p.ssp");

        Assert.Equal(@"C:\Videos\Quelle", result);
    }

    [Fact]
    public void Resolve_faellt_auf_LastVideoFolder_zurueck_wenn_source_leer()
    {
        var result = VideoFolderFallbackResolver.Resolve(
            lastVideoSourceFolder: "",
            lastVideoFolder: @"C:\Videos\Legacy",
            lastProjectPath: @"C:\Projekte\p.ssp");

        Assert.Equal(@"C:\Videos\Legacy", result);
    }

    [Fact]
    public void Resolve_faellt_auf_projektdatei_verzeichnis_zurueck_wenn_beide_leer()
    {
        var result = VideoFolderFallbackResolver.Resolve(
            lastVideoSourceFolder: null,
            lastVideoFolder: null,
            lastProjectPath: @"C:\Projekte\p.ssp");

        Assert.Equal(@"C:\Projekte", result);
    }

    [Fact]
    public void Resolve_liefert_null_wenn_alle_quellen_leer()
    {
        var result = VideoFolderFallbackResolver.Resolve(
            lastVideoSourceFolder: null,
            lastVideoFolder: null,
            lastProjectPath: null);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ignoriert_whitespace_only_source_folder()
    {
        var result = VideoFolderFallbackResolver.Resolve(
            lastVideoSourceFolder: "  ",
            lastVideoFolder: @"C:\Videos\Legacy",
            lastProjectPath: null);

        Assert.Equal(@"C:\Videos\Legacy", result);
    }
}
