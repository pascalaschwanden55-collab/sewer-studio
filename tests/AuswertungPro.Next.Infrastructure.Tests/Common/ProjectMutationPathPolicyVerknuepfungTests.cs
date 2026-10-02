using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Projekt-Schreibgrenze auf dem gemeinsamen VerknuepfungsSchutz (Deepscan 02.10.2026, A5),
/// Regel ProjektSchreibgrenze: Projektroot und alle Vorfahren bis zum Laufwerk, fehlende
/// Eintraege erlaubt, Lesefehler des eingespielten Lesers werden unveraendert weitergegeben.
/// </summary>
public sealed class ProjectMutationPathPolicyVerknuepfungTests
{
    private const string Root = @"C:\pm\projekt";
    private const string Ziel = @"C:\pm\projekt\Haltungen\H1\bild.jpg";

    [Theory]
    [InlineData(@"C:\pm\projekt\Haltungen")]
    [InlineData(@"C:\pm\projekt")]
    [InlineData(@"C:\pm")]
    public void EnsureSafePath_sperrt_Verknuepfung_im_Projekt_an_der_Wurzel_und_darueber(string verknuepft)
    {
        var fehler = Assert.Throws<IOException>(() => ProjectMutationPathPolicy.EnsureSafePath(
            Root, Ziel, p => string.Equals(p, verknuepft, StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : FileAttributes.Directory));

        Assert.Contains("Verknüpfung oder Junction", fehler.Message, StringComparison.Ordinal);
        Assert.Contains(verknuepft, fehler.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureSafePath_laesst_fehlende_Eintraege_zu()
        => Assert.Equal(Ziel, ProjectMutationPathPolicy.EnsureSafePath(
            Root, Ziel, p => p.StartsWith(@"C:\pm\projekt\Haltungen", StringComparison.OrdinalIgnoreCase) ? null : FileAttributes.Directory));

    [Fact]
    public void EnsureSafePath_gibt_einen_Lesefehler_unveraendert_weiter()
        => Assert.Throws<UnauthorizedAccessException>(() => ProjectMutationPathPolicy.EnsureSafePath(
            Root, Ziel, p => p.EndsWith("H1", StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("gesperrt")
                : FileAttributes.Directory));

    [JunctionFact]
    public void EnsureWritableProjectPath_sperrt_eine_echte_Junction_im_Projekt()
    {
        var root = Path.Combine(Path.GetTempPath(), "projektgrenze_" + Guid.NewGuid().ToString("N"));
        var fremd = Path.Combine(Path.GetTempPath(), "projektgrenze_fremd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(fremd);
        var link = Path.Combine(root, "Haltungen");
        try
        {
            File.WriteAllText(Path.Combine(root, "projekt.json"), "{}");
            JunctionTestSupport.CreateDirectoryLink(link, fremd);

            var fehler = Assert.Throws<IOException>(() => ProjectPathResolver.EnsureWritableProjectPath(
                Path.Combine("Haltungen", "bild.jpg"), Path.Combine(root, "projekt.json")));

            Assert.Contains("Verknüpfung oder Junction", fehler.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
            Directory.Delete(root, recursive: true);
            Directory.Delete(fremd, recursive: true);
        }
    }
}
