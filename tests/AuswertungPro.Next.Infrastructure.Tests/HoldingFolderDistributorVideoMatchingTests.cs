using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingFolderDistributorVideoMatchingTests
{
    [Fact]
    public void FindVideoByHaltungDate_DoesNotMatchHaltungOnlyFallback()
    {
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        var files = new List<string>
        {
            Path.Combine(Path.GetTempPath(), "20230101_06-001.mp4")
        };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "06-001", "20240630", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotFound, result!.Status);
        Assert.Contains("Haltung-only", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindVideoByHaltungDate_MatchesSingleDatelessHaltungVideo()
    {
        // User-Fall (Buerglen_Gosmergasse): Das Video traegt NUR die Haltungsnummer ohne
        // Datum (L_58875-10.1089399.mpg), das PDF hat keinen Filmnamen. Wenn es zu der
        // Haltung GENAU EIN datumsloses Video gibt, ist die Zuordnung eindeutig -> Matched.
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        var only = Path.Combine(Path.GetTempPath(), "L_58875-10.1089399.mpg");
        var files = new List<string> { only };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "58875-10.1089399", "20260618", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
        Assert.Equal(only, result.VideoPath);
    }

    [Fact]
    public void FindVideoByHaltungDate_MultipleDatelessVideos_PicksClosestByFileTimestamp()
    {
        // User-Regel: zu jedem Protokoll gibt es ein Video; die Haltungsnummer ist das Indiz.
        // Wenn dieselbe Haltung MEHRERE datumslose Videos hat (Nachinspektion), entscheidet
        // der Datei-Zeitstempel (im Namen steht kein Datum) - das dem Protokoll-Datum
        // naechstgelegene Video gewinnt.
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>), typeof(Func<string, DateTime?>) },
                modifiers: null);
        Assert.NotNull(method);

        var alt = Path.Combine(Path.GetTempPath(), "L_58875-10.1089399.mpg");        // 2025
        var neu = Path.Combine(Path.GetTempPath(), "L_58875-10.1089399_b.mpg");      // 2026 (passt)
        var files = new List<string> { alt, neu };
        Func<string, DateTime?> stamp = p =>
            p == neu ? new DateTime(2026, 6, 18) : new DateTime(2025, 1, 1);

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "58875-10.1089399", "20260618", files, stamp });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
        Assert.Equal(neu, result.VideoPath);
    }

    [Fact]
    public void FindVideoByHaltungDate_DoesNotMatchSingleHaltungVideoWithConflictingDate()
    {
        // Schutz bleibt: traegt das einzige Haltung-Video ein ANDERES Datum als gesucht,
        // wird NICHT automatisch zugeordnet (Verwechslungsgefahr bei mehreren Inspektionen).
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        var files = new List<string>
        {
            Path.Combine(Path.GetTempPath(), "20230101_06-001.mp4")
        };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "06-001", "20240630", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotFound, result!.Status);
    }

    [Fact]
    public void SidecarLinkLookup_UsesReversedHolding_WhenPdfDirectionIsOpposite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"video-match-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var videoName = "1_1_1_22042014_112151.mp2";
        var videoPath = Path.Combine(tempDir, videoName);
        File.WriteAllText(videoPath, "dummy");

        try
        {
            var index = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["23021-22369"] = new List<string> { videoName }
            };

            var method = typeof(HoldingFolderDistributor).GetMethod(
                "TryFindVideoFromSidecarLinks",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
                null,
                new object?[] { index, "22369-23021", tempDir, "20140422", true, null });

            Assert.NotNull(result);
            Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
            Assert.Equal(videoPath, result.VideoPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RecordLinkLookup_UsesReversedHolding_WhenPdfDirectionIsOpposite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"record-link-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var videoName = "1_1_1_22042014_112151.mp2";
        var videoPath = Path.Combine(tempDir, videoName);
        File.WriteAllText(videoPath, "dummy");

        try
        {
            var project = new Project();
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "23021-22369", FieldSource.Xtf, userEdited: false);
            record.SetFieldValue("Link", videoName, FieldSource.Xtf, userEdited: false);
            project.AddRecord(record);

            var method = typeof(HoldingFolderDistributor).GetMethod(
                "TryFindVideoFromRecordLink",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
                null,
                new object?[] { project, "22369-23021", tempDir, "20140422", true, null });

            Assert.NotNull(result);
            Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
            Assert.Equal(videoPath, result.VideoPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}

