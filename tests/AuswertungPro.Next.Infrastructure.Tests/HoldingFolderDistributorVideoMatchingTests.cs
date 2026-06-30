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
    public void FindVideoByHaltungDate_SingleHaltungVideoWithDivergingDate_NowMatchesWithWarning()
    {
        // Neue Politik (User 2026-06-30): Die Haltung ist das Indiz. Ein EINZIGES Haltung-Video
        // wird auch bei abweichendem Datum im Namen ZUGEORDNET - aber klar als "Datum weicht ab"
        // gekennzeichnet (Sicherheitsnetz = Import-Report), statt stillschweigend "missing".
        // (Frueher: NotFound = hartes Verwechslungs-Veto.)
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        var only = Path.Combine(Path.GetTempPath(), "20230101_06-001.mp4");
        var files = new List<string> { only };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "06-001", "20240630", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
        Assert.Equal(only, result.VideoPath);
        Assert.Contains("weicht ab", result.Message, StringComparison.OrdinalIgnoreCase);
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
    public void FindVideoByHaltungDate_MultipleDatedHaltungVideos_PicksClosestToProtocolDate()
    {
        // Modus 2 (mehrdeutig): Mehrere Haltung-Videos mit unterschiedlichem Datum im Namen ->
        // das dem Protokoll-Datum naechstgelegene gewinnt (statt Abbruch/missing).
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        var fern = Path.Combine(Path.GetTempPath(), "20240601_06-001.mp4");   // 28 Tage weg
        var nah = Path.Combine(Path.GetTempPath(), "20240630_06-001.mp4");    // 1 Tag weg
        var files = new List<string> { fern, nah };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "06-001", "20240629", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
        Assert.Equal(nah, result.VideoPath);
    }

    [Fact]
    public void FindVideoByHaltungDate_MultipleHaltungVideos_Indistinguishable_ReturnsAmbiguous()
    {
        // Kein klarer Sieger (gleiche Distanz zum Protokoll-Datum) -> bleibt Ambiguous; die
        // Kandidaten werden sichtbar gemacht, nicht geraten (Verwechslungsschutz bei Gleichstand).
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        // Beide tragen dasselbe (abweichende) Datum -> gleiche Distanz -> kein klarer Sieger.
        var a = Path.Combine(Path.GetTempPath(), "20240630_06-001.mp4");
        var b = Path.Combine(Path.GetTempPath(), "20240630_06-001_b.mp4");
        var files = new List<string> { a, b };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "06-001", "20240620", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, result!.Status);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void FindVideoByHaltungDate_StandardSearch_ExcludesGegeninspektionGSuffix()
    {
        // Reales Quell-Schema (D:\Videoprojekte\...\Film): Standard 'H__<Haltung>.mp4' (ohne
        // Datum) + Gegeninspektion 'H__<Haltung>_G.mp4'. Die Standard-Suche darf das _G-Video
        // NICHT als Kandidat ziehen (sonst mehrdeutig) und liefert eindeutig das Standard-Video.
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        var std = Path.Combine(Path.GetTempPath(), "H__07.1026779-10750.mp4");
        var geg = Path.Combine(Path.GetTempPath(), "H__07.1026779-10750_G.mp4");
        var files = new List<string> { std, geg };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "07.1026779-10750", "20250310", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
        Assert.Equal(std, result.VideoPath);
    }

    [Fact]
    public void FindVideoByHaltungDate_GegeninspektionSearch_MatchesGSuffixVideo()
    {
        // Symmetrisch: Die Gegeninspektions-Suche (haltung + "g") liefert genau das _G-Video,
        // nicht das Standard-Video.
        var method = Type.GetType("AuswertungPro.Next.Infrastructure.HoldingVideoMatching, AuswertungPro.Next.Infrastructure")!
            .GetMethod("FindVideoByHaltungDate", BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>) },
                modifiers: null);
        Assert.NotNull(method);

        var std = Path.Combine(Path.GetTempPath(), "H__07.1026779-10750.mp4");
        var geg = Path.Combine(Path.GetTempPath(), "H__07.1026779-10750_G.mp4");
        var files = new List<string> { std, geg };

        var result = (HoldingFolderDistributor.VideoFindResult?)method!.Invoke(
            null,
            new object?[] { "07.1026779-10750g", "20250310", files });

        Assert.NotNull(result);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result!.Status);
        Assert.Equal(geg, result.VideoPath);
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

