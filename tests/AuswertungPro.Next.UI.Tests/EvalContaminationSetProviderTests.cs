using System;
using System.IO;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class EvalContaminationSetProviderTests
{
    [Fact]
    public void Load_uses_eval_root_from_settings()
    {
        using var temp = new TempEvalSet();
        var settings = new AppSettings { EvalSetRoot = temp.Root };

        var sets = EvalContaminationSetProvider.Load(settings);

        Assert.Contains("abc123", sets.ImageHashes);
        Assert.Contains("287425-81162", sets.HaltungKeys);
    }

    [Fact]
    public void Load_missing_configured_root_fails_loud()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(
            () => EvalContaminationSetProvider.Load(missing));
    }

    [Fact]
    public void Load_empty_root_explicitly_disables_protection()
    {
        var sets = EvalContaminationSetProvider.Load(string.Empty);

        Assert.Empty(sets.ImageHashes);
        Assert.Empty(sets.HaltungKeys);
    }

    [Fact]
    public void Load_corrupt_manifest_fails_loud()
    {
        using var temp = new TempEvalSet();
        File.WriteAllText(Path.Combine(temp.Root, "_manifest.json"), "{ kaputt");

        var error = Assert.Throws<InvalidDataException>(
            () => EvalContaminationSetProvider.Load(temp.Root));

        Assert.Contains("_manifest.json", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempEvalSet : IDisposable
    {
        public TempEvalSet()
        {
            Root = Path.Combine(Path.GetTempPath(), "SewerStudioEvalSets", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "_manifest.json"), """
                {
                  "hashes": {
                    "images/frame.png": { "sha256": " abc123 " },
                    "notes/readme.txt": { "sha256": "ignored" }
                  }
                }
                """);
            File.WriteAllText(Path.Combine(Root, "_candidates.json"), """
                [
                  { "haltung_key": "287425-81162" }
                ]
                """);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
