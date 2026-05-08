using System;
using System.IO;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer den KnowledgeMirror-Health-Check (Audit Punkt 3.8).
/// Liefert Green/Yellow/Red basierend auf Brain-Mirror-Zustand.
/// </summary>
[Trait("Category", "Integration")]
public class KnowledgeMirrorHealthTests
{
    private static (string knowledgeRoot, string brainRoot) MakeRoots()
    {
        var stem = Guid.NewGuid().ToString("N");
        var knowledgeRoot = Path.Combine(Path.GetTempPath(), $"sewerstudio_test_kr_{stem}");
        var brainRoot = Path.Combine(Path.GetTempPath(), $"sewerstudio_test_br_{stem}");
        Directory.CreateDirectory(knowledgeRoot);
        return (knowledgeRoot, brainRoot);
    }

    private static void Cleanup(params string[] dirs)
    {
        foreach (var d in dirs)
        {
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); }
            catch { }
        }
    }

    [Fact]
    public void GetHealth_BrainRootMissing_Red()
    {
        var (knowledgeRoot, brainRoot) = MakeRoots();
        try
        {
            // brainRoot wird nicht angelegt
            using var svc = new KnowledgeMirrorService(knowledgeRoot, brainRoot);
            // Der Konstruktor ruft Directory.CreateDirectory(brainRoot) — also
            // muessen wir den loeschen damit "missing" greift.
            try { Directory.Delete(brainRoot, recursive: true); } catch { }

            var health = svc.GetHealth();
            Assert.Equal(KnowledgeMirrorStatus.Red, health.Status);
            Assert.Contains("existiert nicht", health.Message);
        }
        finally
        {
            Cleanup(knowledgeRoot, brainRoot);
        }
    }

    [Fact]
    public void GetHealth_NoLocalDb_Yellow()
    {
        var (knowledgeRoot, brainRoot) = MakeRoots();
        try
        {
            using var svc = new KnowledgeMirrorService(knowledgeRoot, brainRoot);
            // Weder lokale noch Brain-DB
            var health = svc.GetHealth();
            Assert.Equal(KnowledgeMirrorStatus.Yellow, health.Status);
            Assert.Contains("Mirror nicht relevant", health.Message);
        }
        finally
        {
            Cleanup(knowledgeRoot, brainRoot);
        }
    }

    [Fact]
    public void GetHealth_LocalButNoMirror_Red()
    {
        var (knowledgeRoot, brainRoot) = MakeRoots();
        try
        {
            // Lokale DB anlegen, Brain-DB fehlt
            File.WriteAllBytes(Path.Combine(knowledgeRoot, "KnowledgeBase.db"), new byte[100]);

            using var svc = new KnowledgeMirrorService(knowledgeRoot, brainRoot);
            var health = svc.GetHealth();
            Assert.Equal(KnowledgeMirrorStatus.Red, health.Status);
            Assert.Contains("kein Mirror", health.Message);
        }
        finally
        {
            Cleanup(knowledgeRoot, brainRoot);
        }
    }

    [Fact]
    public void GetHealth_BrainDbZeroBytes_Red()
    {
        var (knowledgeRoot, brainRoot) = MakeRoots();
        try
        {
            File.WriteAllBytes(Path.Combine(knowledgeRoot, "KnowledgeBase.db"), new byte[100]);

            using var svc = new KnowledgeMirrorService(knowledgeRoot, brainRoot);
            // Konstruktor erstellt brainRoot — leere DB-Datei dort anlegen
            File.WriteAllBytes(Path.Combine(brainRoot, "KnowledgeBase.db"), Array.Empty<byte>());

            var health = svc.GetHealth();
            Assert.Equal(KnowledgeMirrorStatus.Red, health.Status);
            Assert.Contains("0 Byte", health.Message);
        }
        finally
        {
            Cleanup(knowledgeRoot, brainRoot);
        }
    }

    [Fact]
    public void GetHealth_BrainSignificantlySmaller_Yellow()
    {
        var (knowledgeRoot, brainRoot) = MakeRoots();
        try
        {
            // Lokale DB 10 MB, Brain DB 5 MB → 50% kleiner → Yellow
            var localPath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
            File.WriteAllBytes(localPath, new byte[10 * 1024 * 1024]);

            using var svc = new KnowledgeMirrorService(knowledgeRoot, brainRoot);
            var brainPath = Path.Combine(brainRoot, "KnowledgeBase.db");
            File.WriteAllBytes(brainPath, new byte[5 * 1024 * 1024]);

            var health = svc.GetHealth();
            Assert.Equal(KnowledgeMirrorStatus.Yellow, health.Status);
            Assert.Contains("hinkt zurueck", health.Message);
        }
        finally
        {
            Cleanup(knowledgeRoot, brainRoot);
        }
    }

    [Fact]
    public void GetHealth_BrainEqualSize_Green()
    {
        var (knowledgeRoot, brainRoot) = MakeRoots();
        try
        {
            var localPath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
            File.WriteAllBytes(localPath, new byte[1024 * 1024]); // 1 MB

            using var svc = new KnowledgeMirrorService(knowledgeRoot, brainRoot);
            var brainPath = Path.Combine(brainRoot, "KnowledgeBase.db");
            File.WriteAllBytes(brainPath, new byte[1024 * 1024]); // 1 MB

            var health = svc.GetHealth();
            Assert.Equal(KnowledgeMirrorStatus.Green, health.Status);
            Assert.NotNull(health.BrainDbAgeHours);
            Assert.True(health.BrainDbBytes > 0);
        }
        finally
        {
            Cleanup(knowledgeRoot, brainRoot);
        }
    }
}
