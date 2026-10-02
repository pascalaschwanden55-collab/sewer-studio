using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Sperrklinke zu Deepscan 02.10.2026, A5: Produktdateien mit eigener
/// <c>FileAttributes.ReparsePoint</c>-Pruefung neben dem gemeinsamen <c>VerknuepfungsSchutz</c>.
/// Die Zahl darf nur sinken; wer eine Kopie auf den Baustein umstellt, senkt den Wert.
/// </summary>
public sealed class VerknuepfungsKopienWaechterTests
{
    private const int EigeneReparsePointPruefungen = 26;

    [Fact]
    public void Eigene_Verknuepfungspruefungen_werden_nicht_mehr()
    {
        var root = TestRepoPaths.RepoRoot();
        var baustein = Path.Combine("src", "AuswertungPro.Next.Application", "Common", "VerknuepfungsSchutz.cs");
        var treffer = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !Path.GetRelativePath(root, p).Equals(baustein, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.ReadAllText(p).Contains("FileAttributes.ReparsePoint", StringComparison.Ordinal))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(treffer.Count <= EigeneReparsePointPruefungen,
            $"Neue eigene Verknuepfungspruefung ({treffer.Count} statt hoechstens {EigeneReparsePointPruefungen}). " +
            "Bitte VerknuepfungsSchutz verwenden: " + string.Join(", ", treffer));
        Assert.True(treffer.Count == EigeneReparsePointPruefungen,
            $"Nur noch {treffer.Count} eigene Pruefungen; bitte EigeneReparsePointPruefungen senken.");
    }
}
