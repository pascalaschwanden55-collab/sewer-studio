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
    public void Load_returns_empty_sets_for_missing_root()
    {
        var sets = EvalContaminationSetProvider.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Empty(sets.ImageHashes);
        Assert.Empty(sets.HaltungKeys);
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
