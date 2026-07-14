using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Pipeline;

public sealed class SidecarTokenFileResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SidecarTokenFileResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Aufloesung_beachtet_Einstellung_Umgebungsreihenfolge_und_Datei()
    {
        Directory.CreateDirectory(_root);
        var tokenFile = Path.Combine(_root, ".sidecar_token");
        File.WriteAllText(tokenFile, " datei-token ");
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        ISidecarTokenResolver resolver = new SidecarTokenFileResolver(
            name => environment.GetValueOrDefault(name),
            tokenFile);

        Assert.Equal("datei-token", resolver.Resolve());

        environment["SEWER_SIDECAR_TOKEN"] = " alt-token ";
        Assert.Equal("alt-token", resolver.Resolve());

        environment["SEWERSTUDIO_SIDECAR_TOKEN"] = " haupt-token ";
        Assert.Equal("haupt-token", resolver.Resolve());

        Assert.Equal("eingestellt", resolver.Resolve(" eingestellt "));
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
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }
}
