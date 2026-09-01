using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SilentCatchGuardTests
{
    private static readonly Regex EmptyCatchPattern = new(
        @"catch(?:\s*\([^)]*\))?\s*\{\s*\}",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Produktivcode_enthaelt_keine_vollstaendig_leeren_Catch_Bloecke()
    {
        var sourceRoot = Path.Combine(TestRepoPaths.FindRepoRoot(), "src");
        var findings = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(ContainsCodeMatch)
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            findings.Length == 0,
            "Vollstaendig leere catch-Bloecke verschlucken Fehler ohne jede Spur:\n" +
            string.Join("\n", findings));
    }

    private static bool ContainsCodeMatch(string path)
    {
        var source = File.ReadAllText(path);
        return EmptyCatchPattern.Matches(source).Any(match =>
        {
            var lineStart = source.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
            var prefix = source[lineStart..match.Index].TrimStart();
            return !prefix.StartsWith("//", StringComparison.Ordinal)
                && !prefix.StartsWith("*", StringComparison.Ordinal);
        });
    }
}
