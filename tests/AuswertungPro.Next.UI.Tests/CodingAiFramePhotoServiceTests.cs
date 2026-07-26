using System;
using System.IO;
using System.Reflection;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiFramePhotoServiceTests
{
    [Fact]
    public void AttachAnalyzedFramePhoto_saves_frame_bytes_and_links_photo_to_ai_entry()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewerstudio-ai-frame-photo-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = "BDDC",
                Beschreibung = "Wasserstand",
                MeterStart = 5.70,
                Zeit = TimeSpan.FromSeconds(42)
            };

            var savedPath = InvokeAttachAnalyzedFramePhoto(entry, ValidPngBytes(), null, root);

            Assert.NotNull(savedPath);
            Assert.True(File.Exists(savedPath));
            Assert.Equal(savedPath, Assert.Single(entry.FotoPaths));
            Assert.Contains("BDDC", Path.GetFileName(savedPath), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(ValidPngBytes(), File.ReadAllBytes(savedPath!));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AttachAnalyzedFramePhoto_keeps_existing_real_photo()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewerstudio-ai-frame-photo-existing-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var existing = Path.Combine(root, "manual.png");
            File.WriteAllBytes(existing, ValidPngBytes());
            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = "BDDC",
                MeterStart = 5.70
            };
            entry.FotoPaths.Add(existing);

            var savedPath = InvokeAttachAnalyzedFramePhoto(entry, ValidPngBytes(), null, root);

            Assert.Equal(existing, savedPath);
            Assert.Equal(new[] { existing }, entry.FotoPaths);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AttachAnalyzedFramePhoto_uses_a_new_name_when_the_target_already_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewerstudio-ai-frame-photo-collision-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = "BDDC",
                MeterStart = 5.70,
                Zeit = TimeSpan.FromSeconds(42),
                EntryId = Guid.Parse("11111111-1111-1111-1111-111111111111")
            };
            var expectedName = "BDDC_5.70m_00-00-42-000_11111111111111111111111111111111_ai.png";
            var occupiedPath = Path.Combine(root, expectedName);
            File.WriteAllBytes(occupiedPath, [9, 8, 7]);

            var savedPath = InvokeAttachAnalyzedFramePhoto(entry, ValidPngBytes(), null, root);

            Assert.Equal(Path.Combine(root, Path.GetFileNameWithoutExtension(expectedName) + "_1.png"), savedPath);
            Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(occupiedPath));
            Assert.Equal(ValidPngBytes(), File.ReadAllBytes(savedPath!));
            Assert.Equal(savedPath, Assert.Single(entry.FotoPaths));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static string? InvokeAttachAnalyzedFramePhoto(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath,
        string? photoRoot)
    {
        var serviceType = typeof(CodingDefectPreviewService).Assembly.GetType("AuswertungPro.Next.UI.Ai.Coding.CodingAiFramePhotoService");
        Assert.NotNull(serviceType);

        var method = serviceType.GetMethod(
            "AttachAnalyzedFramePhoto",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(ProtocolEntry), typeof(byte[]), typeof(string), typeof(string)],
            modifiers: null);
        Assert.NotNull(method);

        return (string?)method.Invoke(null, [entry, frameBytes, videoPath, photoRoot]);
    }

    private static byte[] ValidPngBytes()
        => Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
