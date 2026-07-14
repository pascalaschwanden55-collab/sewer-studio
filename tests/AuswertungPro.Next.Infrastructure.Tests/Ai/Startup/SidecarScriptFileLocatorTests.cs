using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Startup;

public sealed class SidecarScriptFileLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SidecarScriptFileLocatorTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Sucht_Startskript_aufwaerts_und_beachtet_Startreihenfolge()
    {
        var firstRoot = Path.Combine(_root, "erste");
        var firstStart = Path.Combine(firstRoot, "bin", "Release");
        var firstScript = Path.Combine(firstRoot, "sidecar", "start_sidecar.ps1");
        var secondRoot = Path.Combine(_root, "zweite");
        var secondStart = Path.Combine(secondRoot, "src");
        var secondScript = Path.Combine(secondRoot, "sidecar", "start_sidecar.ps1");
        Directory.CreateDirectory(firstStart);
        Directory.CreateDirectory(Path.GetDirectoryName(firstScript)!);
        Directory.CreateDirectory(secondStart);
        Directory.CreateDirectory(Path.GetDirectoryName(secondScript)!);
        File.WriteAllText(firstScript, "erste");
        File.WriteAllText(secondScript, "zweite");
        ISidecarScriptLocator locator = new SidecarScriptFileLocator(
            [firstStart, secondStart],
            windowsDirectory: Path.Combine(_root, "Windows"));

        var found = locator.FindDefaultSidecarScript();

        Assert.Equal(firstScript, found);
    }

    [Fact]
    public void Powershell_Pfad_nutzt_Systemdatei_und_sonst_Path_Fallback()
    {
        var windowsDirectory = Path.Combine(_root, "Windows");
        var powerShell = Path.Combine(
            windowsDirectory,
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(powerShell)!);
        File.WriteAllText(powerShell, string.Empty);

        ISidecarScriptLocator existing = new SidecarScriptFileLocator([], windowsDirectory);
        ISidecarScriptLocator missing = new SidecarScriptFileLocator(
            [],
            Path.Combine(_root, "NichtVorhanden"));

        Assert.Equal(powerShell, existing.ResolvePowerShellExe());
        Assert.Equal("powershell.exe", missing.ResolvePowerShellExe());
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
