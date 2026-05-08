using System;
using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Slice 1: VsaYoloClassMap.TryGetClassId — Lookup ohne Auto-Create.
/// Negative-Tests muessen den statischen _map-Cache zuruecksetzen,
/// sonst koennen frueher gelaufene Tests (oder Produktiv-Pfad) "XYZ" o.ae.
/// schon einbetoniert haben.
/// </summary>
public sealed class VsaYoloClassMapTryGetTests
{
    [Fact]
    public void TryGetClassId_KnownCategory_ReturnsTrue()
    {
        // BAB ist in der Default-Map (Index 4) — keine Isolation noetig.
        var ok = VsaYoloClassMap.TryGetClassId("BAB B", out var id);
        Assert.True(ok);
        Assert.True(id >= 0);
    }

    [Fact]
    public void TryGetClassId_KnownCategory_ReturnsExpectedDefaultId()
    {
        // BAB ist in den Defaults explizit auf 4 gesetzt — solange das
        // yolo_class_map.json im aktuellen Root das nicht ueberschrieben hat,
        // muss BAB hier auch genau 4 ergeben.
        VsaYoloClassMap.TryGetClassId("BAB B", out var babId);
        VsaYoloClassMap.TryGetClassId("BCD", out var bcdId);

        Assert.NotEqual(babId, bcdId);
    }

    [Fact]
    public void TryGetClassId_UnknownCategory_ReturnsFalse_NoSideEffect()
    {
        // Isolierter Lauf gegen einen frischen, leeren KnowledgeRoot:
        using var iso = ClassMapIsolation.FreshEmptyRoot();

        // ZZZ ist nicht in den Defaults und kann durch die Isolation auch
        // nicht aus einer anderen Map nachgeladen werden.
        var ok = VsaYoloClassMap.TryGetClassId("ZZZ Q", out var id);

        Assert.False(ok);
        Assert.Equal(-1, id);

        // Kontrolle: Der Cache darf "ZZZ" nicht aufgenommen haben (Auto-Create
        // ist ausschliesslich Sache von GetClassId, nicht TryGetClassId).
        var snapshot = VsaYoloClassMap.GetFullMap();
        Assert.False(snapshot.ContainsKey("ZZZ"));
    }

    [Fact]
    public void TryGetClassId_EmptyOrNull_ReturnsFalse()
    {
        Assert.False(VsaYoloClassMap.TryGetClassId("", out var a));
        Assert.Equal(-1, a);

        Assert.False(VsaYoloClassMap.TryGetClassId(null!, out var b));
        Assert.Equal(-1, b);

        Assert.False(VsaYoloClassMap.TryGetClassId("   ", out var c));
        Assert.Equal(-1, c);
    }

    /// <summary>
    /// Setzt KnowledgeRootProvider auf einen frischen Temp-Pfad und kippt
    /// den statischen _map-Cache via Reflection, damit der naechste Aufruf
    /// gegen einen leeren Default-Datenstand laeuft. Stellt nach Dispose den
    /// alten Zustand wieder her, damit andere Tests nicht beeintraechtigt
    /// werden.
    /// </summary>
    private sealed class ClassMapIsolation : IDisposable
    {
        private readonly string _tempDir;
        private readonly bool _hadResolverBefore;

        private ClassMapIsolation(string tempDir, bool hadResolverBefore)
        {
            _tempDir = tempDir;
            _hadResolverBefore = hadResolverBefore;
        }

        public static ClassMapIsolation FreshEmptyRoot()
        {
            var hadResolverBefore = KnowledgeRootProvider.HasResolver;
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "VsaYoloClassMapTryGetTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            KnowledgeRootProvider.SetResolver(() => tempDir);
            ResetStaticMapCache();

            return new ClassMapIsolation(tempDir, hadResolverBefore);
        }

        public void Dispose()
        {
            // Cache leeren, damit der naechste Test EnsureLoaded() neu durchlaeuft
            ResetStaticMapCache();

            // Resolver auf den Vor-Zustand zuruecksetzen. Wenn vorher keiner
            // gesetzt war, _resolver via Reflection auf null kippen.
            if (!_hadResolverBefore)
            {
                ResetStaticResolver();
            }

            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Best-Effort — kein Test-Failure, falls Files noch gelockt sind
            }
        }

        private static void ResetStaticMapCache()
        {
            var mapField = typeof(VsaYoloClassMap).GetField(
                "_map",
                BindingFlags.NonPublic | BindingFlags.Static);
            mapField?.SetValue(null, null);
        }

        private static void ResetStaticResolver()
        {
            var resolverField = typeof(KnowledgeRootProvider).GetField(
                "_resolver",
                BindingFlags.NonPublic | BindingFlags.Static);
            resolverField?.SetValue(null, null);
        }
    }
}
