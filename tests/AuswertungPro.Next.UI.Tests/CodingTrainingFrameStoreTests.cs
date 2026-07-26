using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Ai.Evidence;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingFrameStoreTests
{
    private const string GoldCodeFolder = "BBA - Wurzeln";

    [Fact]
    public async Task SaveGoldFrameAsync_writes_preferred_bytes_without_fallback()
    {
        using var temp = new TempDir();
        var ev = MakeEvent();
        var fallbackCalled = false;
        var bytes = new byte[] { 1, 2, 3 };
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var store = CreateStore(temp);

        var result = await store.SaveGoldFrameAsync(
            ev,
            bytes,
            () =>
            {
                fallbackCalled = true;
                return Task.FromResult<byte[]?>(new byte[] { 9 });
            });

        Assert.False(fallbackCalled);
        Assert.Null(result.Error);
        Assert.Equal(
            System.IO.Path.Combine(temp.Path, "gold_frames", GoldCodeFolder, $"gold_{hash}.png"),
            result.Path);
        Assert.Equal(bytes, File.ReadAllBytes(result.Path!));
    }

    [Fact]
    public async Task SaveGoldFrameAsync_uses_capture_fallback_when_preferred_bytes_are_missing()
    {
        using var temp = new TempDir();
        var store = CreateStore(temp);

        var result = await store.SaveGoldFrameAsync(
            MakeEvent(),
            preferredFrameBytes: null,
            () => Task.FromResult<byte[]?>(new byte[] { 4, 5, 6 }));

        Assert.Null(result.Error);
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(result.Path!));
    }

    [Fact]
    public async Task SaveGoldFrameAsync_returns_error_when_no_frame_is_available()
    {
        using var temp = new TempDir();
        var store = CreateStore(temp);

        var result = await store.SaveGoldFrameAsync(
            MakeEvent(),
            Array.Empty<byte>(),
            () => Task.FromResult<byte[]?>(Array.Empty<byte>()));

        Assert.Null(result.Path);
        Assert.Equal("kein Frame verfuegbar", result.Error);
        Assert.False(Directory.Exists(System.IO.Path.Combine(temp.Path, "gold_frames")));
    }

    [Fact]
    public async Task SaveGoldFrameAsync_copies_existing_photo_content_addressed()
    {
        using var temp = new TempDir();
        var source = System.IO.Path.Combine(temp.Path, "source.jpg");
        var bytes = new byte[] { 4, 2, 4, 2 };
        File.WriteAllBytes(source, bytes);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var store = CreateStore(temp);

        var result = await store.SaveGoldFrameAsync(
            MakeEvent(),
            source,
            preferredFrameBytes: null,
            () => Task.FromResult<byte[]?>(null));

        Assert.Null(result.Error);
        Assert.Equal(
            System.IO.Path.Combine(temp.Path, "gold_frames", GoldCodeFolder, $"gold_{hash}.jpg"),
            result.Path);
        Assert.Equal(bytes, File.ReadAllBytes(source));
        Assert.Equal(bytes, File.ReadAllBytes(result.Path!));
    }

    [Fact]
    public void SaveEvidenceFrame_returns_error_for_missing_raw_frame()
    {
        using var temp = new TempDir();
        var store = new CodingTrainingFrameStore(() => temp.Path);

        var result = store.SaveEvidenceFrame(MakeEvent(), rawFramePath: null);

        Assert.Null(result.Path);
        Assert.Equal("kein Rohbild fuer Beweisbild verfuegbar", result.Error);
    }

    [Fact]
    public void SaveEvidenceFrame_writes_expected_output_path_and_annotation()
    {
        using var temp = new TempDir();
        var raw = System.IO.Path.Combine(temp.Path, "raw.png");
        File.WriteAllBytes(raw, new byte[] { 1 });
        EvidenceFrameAnnotation? captured = null;
        string? capturedSource = null;
        string? capturedOutput = null;
        var ev = MakeEvent();
        var store = new CodingTrainingFrameStore(
            () => temp.Path,
            (source, output, annotation) =>
            {
                capturedSource = source;
                capturedOutput = output;
                captured = annotation;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
                File.WriteAllBytes(output, new byte[] { 7, 8 });
                return true;
            });

        var result = store.SaveEvidenceFrame(ev, raw);

        var expectedOutput = System.IO.Path.Combine(
            temp.Path,
            "gold_frames_annotated",
            $"{ev.EventId:N}_annotated.png");
        Assert.Null(result.Error);
        Assert.Equal(expectedOutput, result.Path);
        Assert.Equal(raw, capturedSource);
        Assert.Equal(expectedOutput, capturedOutput);
        Assert.Equal("BBA", captured!.Code);
        Assert.True(File.Exists(expectedOutput));
    }

    private static CodingEvent MakeEvent()
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = "BBA",
                Beschreibung = "Riss",
                Source = ProtocolEntrySource.Ai
            },
            AiContext = new CodingEventAiContext
            {
                Confidence = 0.8,
                Decision = CodingUserDecision.Accepted
            }
        };

    private static CodingTrainingFrameStore CreateStore(TempDir temp)
        => new(
            () => temp.Path,
            codeLabelLookup: code => code == "BBA" ? "Wurzeln" : null);

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sewer-frame-store-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
