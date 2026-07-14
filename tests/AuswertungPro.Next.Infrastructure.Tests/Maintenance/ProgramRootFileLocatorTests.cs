using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.Infrastructure.Maintenance;

namespace AuswertungPro.Next.Infrastructure.Tests.Maintenance;

public sealed class ProgramRootFileLocatorTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "ProgramRootFileLocatorTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindProgramRoot_verwendet_zuerst_den_uebergeordneten_App_Ordner()
    {
        var programRoot = Path.Combine(_testRoot, "programm");
        var appBase = Path.Combine(programRoot, "src", "UI", "bin", "Release");
        var currentDirectory = Path.Combine(_testRoot, "arbeitsordner");
        Directory.CreateDirectory(appBase);
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(Path.Combine(programRoot, "AuswertungPro.sln"), "test");

        IProgramRootLocator locator = new ProgramRootFileLocator();

        var result = locator.FindProgramRoot(appBase, currentDirectory);

        Assert.Equal(Path.GetFullPath(programRoot), result);
    }

    [Fact]
    public void FindProgramRoot_verwendet_den_Arbeitsordner_und_sonst_den_App_Ordner()
    {
        var appBase = Path.Combine(_testRoot, "app-ohne-solution");
        var programRoot = Path.Combine(_testRoot, "arbeitsbereich");
        var currentDirectory = Path.Combine(programRoot, "unterordner");
        Directory.CreateDirectory(appBase);
        Directory.CreateDirectory(currentDirectory);
        File.WriteAllText(Path.Combine(programRoot, "AuswertungPro.sln"), "test");

        IProgramRootLocator locator = new ProgramRootFileLocator();

        var found = locator.FindProgramRoot(appBase, currentDirectory);
        var fallback = locator.FindProgramRoot(appBase, Path.Combine(_testRoot, "ohne-solution"));

        Assert.Equal(Path.GetFullPath(programRoot), found);
        Assert.Equal(Path.GetFullPath(appBase), fallback);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }
}
