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

