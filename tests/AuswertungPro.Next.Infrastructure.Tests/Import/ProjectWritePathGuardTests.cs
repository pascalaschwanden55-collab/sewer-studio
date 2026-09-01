using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ProjectWritePathGuardTests
{
    [Fact]
    public void EnsureDirectChild_Akzeptiert_Nur_Unmittelbares_Kind()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"ImportParent_{Guid.NewGuid():N}");
        var child = Path.Combine(parent, "Kind");
        var nested = Path.Combine(child, "Enkel");

        ImportFileStagingPathGuard.EnsureDirectChild(child, parent);
        var error = Assert.Throws<IOException>(() =>
            ImportFileStagingPathGuard.EnsureDirectChild(nested, parent));

        Assert.Contains("Unsicherer Import-Arbeitsordner", error.Message, StringComparison.Ordinal);
    }

    [JunctionFact]
    public void EnsureSafeFileTarget_LehntVerknuepftenProjektrootAb()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"ProjectWritePathGuardTests_{Guid.NewGuid():N}");
        var external = Path.Combine(root, "Fremdziel");
        var projectLink = Path.Combine(root, "Projekt");
        Directory.CreateDirectory(external);
        JunctionTestSupport.CreateDirectoryLink(projectLink, external);

        try
        {
            var guard = new ProjectWritePathGuard(projectLink);
            var target = Path.Combine(projectLink, "Imports", "PDF", "quelle.pdf");

            var error = Assert.Throws<IOException>(() => guard.EnsureSafeFileTarget(target));

            Assert.Contains("Verknuepfung", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFiles(external, "*", SearchOption.AllDirectories));
        }
        finally
        {
            try
            {
                if (Directory.Exists(projectLink))
                    Directory.Delete(projectLink);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }
    }
}
