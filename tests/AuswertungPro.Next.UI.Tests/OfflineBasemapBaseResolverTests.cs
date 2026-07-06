using System.IO;
using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OfflineBasemapBaseResolverTests
{
    [Fact]
    public void Resolve_gibt_pfad_zurueck_wenn_er_satellit_enthaelt()
    {
        using var t = new TempDir();
        Directory.CreateDirectory(Path.Combine(t.Path, "satellit"));
        Assert.Equal(t.Path, OfflineBasemapBaseResolver.Resolve(t.Path));
    }

    [Fact]
    public void Resolve_faellt_auf_elternordner_zurueck_bei_veraltetem_unterpfad()
    {
        // Realer Fall: settings.json haelt noch "...\basemap_tiles\uri", die Karten liegen aber
        // unter "...\basemap_tiles\{satellit,av}". Dann muss der Elternordner genommen werden.
        using var t = new TempDir();
        Directory.CreateDirectory(Path.Combine(t.Path, "av"));
        var stale = Path.Combine(t.Path, "uri");
        Directory.CreateDirectory(stale);
        Assert.Equal(t.Path, OfflineBasemapBaseResolver.Resolve(stale));
    }

    [Fact]
    public void Resolve_laesst_pfad_unveraendert_wenn_nichts_gefunden()
    {
        using var t = new TempDir();
        var p = Path.Combine(t.Path, "leer");
        Directory.CreateDirectory(p);
        Assert.Equal(p, OfflineBasemapBaseResolver.Resolve(p));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_null_oder_leer_bleibt(string? input)
        => Assert.Equal(input, OfflineBasemapBaseResolver.Resolve(input));

    private sealed class TempDir : System.IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
