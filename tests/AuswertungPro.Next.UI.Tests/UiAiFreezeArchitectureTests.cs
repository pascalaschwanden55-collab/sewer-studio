using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Friert den Bestand unter src/AuswertungPro.Next.UI/Ai ein: Neue Workflow- und
/// Orchestrierungsklassen (Request/Actions/Result-Muster) gehoeren seit Stufe 2 nach
/// src/AuswertungPro.Next.Application/UseCases/ (Namespace AuswertungPro.Next.Application.UseCases).
/// Der Vergleich laeuft per Dateiname, damit Umzuege zwischen den Ai-Unterordnern frei bleiben.
/// </summary>
public sealed class UiAiFreezeArchitectureTests
{
    private static readonly string[] AllowlistPath = ["tests", "AuswertungPro.Next.UI.Tests", "UiAiFreezeAllowlist.txt"];
    private static readonly string[] AiRootPath = ["src", "AuswertungPro.Next.UI", "Ai"];

    [Fact]
    public void UiAi_contains_only_frozen_files()
    {
        var allowed = LoadAllowlist();
        var unknown = Directory
            .EnumerateFiles(RepoFile(AiRootPath), "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !allowed.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            unknown.Length == 0,
            "Neue Dateien unter src/AuswertungPro.Next.UI/Ai sind nicht erlaubt. "
            + "Neue Workflow-/Orchestrierungsklassen gehoeren nach "
            + "src/AuswertungPro.Next.Application/UseCases/ (siehe AGENTS.md). "
            + "Bewusst UI-nahe Dateien muessen in tests/AuswertungPro.Next.UI.Tests/UiAiFreezeAllowlist.txt "
            + "eingetragen werden:\n" + string.Join("\n", unknown));
    }

    [Fact]
    public void UiAi_freeze_allowlist_has_no_stale_or_duplicate_entries()
    {
        var allowed = LoadAllowlist();
        var existing = Directory
            .EnumerateFiles(RepoFile(AiRootPath), "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = allowed.Where(name => !existing.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var duplicates = LoadAllowlistRaw()
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            stale.Length == 0 && duplicates.Length == 0,
            "UiAiFreezeAllowlist.txt bitte aufraeumen."
            + (stale.Length > 0 ? "\nVerwaiste Eintraege ohne Datei:\n" + string.Join("\n", stale) : string.Empty)
            + (duplicates.Length > 0 ? "\nDoppelte Eintraege:\n" + string.Join("\n", duplicates) : string.Empty));
    }

    private static HashSet<string> LoadAllowlist()
        => LoadAllowlistRaw().ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string[] LoadAllowlistRaw()
        => File.ReadAllLines(RepoFile(AllowlistPath))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
}
