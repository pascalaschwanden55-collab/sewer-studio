using System.Diagnostics;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VideoFrameExtractorTests
{
    [Fact]
    public async Task ErfolgreicherLauf_LiefertPngBytesUndLoeschtTempDatei()
    {
        var videoPath = Path.GetTempFileName();
        var fake = new FakeProcessOutputReader(exitCode: 0, outputBytes: [1, 2, 3, 4]);
        try
        {
            var service = new VideoFrameExtractionService(fake);

            var result = await service.TryExtractFramePngAsync(
                "fake-ffmpeg.exe",
                videoPath,
                TimeSpan.FromSeconds(2.5),
                CancellationToken.None);

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, result);
            Assert.Equal(1, fake.Calls);
            Assert.NotNull(fake.OutputPath);
            Assert.False(File.Exists(fake.OutputPath));
            Assert.Equal("fake-ffmpeg.exe", fake.StartInfo?.FileName);
            Assert.Contains("-ss 2.5", fake.StartInfo?.Arguments, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task Fehlercode_LiefertNullUndLoeschtTempDatei()
    {
        var videoPath = Path.GetTempFileName();
        var fake = new FakeProcessOutputReader(exitCode: 1, outputBytes: [9, 8, 7]);
        try
        {
            var service = new VideoFrameExtractionService(fake);

            var result = await service.TryExtractFramePngAsync(
                "fake-ffmpeg.exe",
                videoPath,
                TimeSpan.Zero,
                CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(fake.OutputPath);
            Assert.False(File.Exists(fake.OutputPath));
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task FehlendesVideo_StartetKeinenProzess()
    {
        var fake = new FakeProcessOutputReader(exitCode: 0, outputBytes: [1]);
        var service = new VideoFrameExtractionService(fake);

        var result = await service.TryExtractFramePngAsync(
            "fake-ffmpeg.exe",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "fehlt.mp4"),
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, fake.Calls);
    }

    private sealed class FakeProcessOutputReader : IProcessOutputReader
    {
        private static readonly Regex OutputPathRegex = new(
            "-y \\\"(?<path>[^\\\"]+)\\\"$",
            RegexOptions.Compiled);

        private readonly int _exitCode;
        private readonly byte[] _outputBytes;

        public FakeProcessOutputReader(int exitCode, byte[] outputBytes)
        {
            _exitCode = exitCode;
            _outputBytes = outputBytes;
        }

        public int Calls { get; private set; }
        public string? OutputPath { get; private set; }
        public ProcessStartInfo? StartInfo { get; private set; }

        public Task<ProcessOutputResult?> ReadToExitAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken,
            Action<int>? onStarted = null)
        {
            Calls++;
            StartInfo = startInfo;
            var match = OutputPathRegex.Match(startInfo.Arguments);
            Assert.True(match.Success, $"Ausgabepfad fehlt in Argumenten: {startInfo.Arguments}");
            OutputPath = match.Groups["path"].Value;
            File.WriteAllBytes(OutputPath, _outputBytes);
            return Task.FromResult<ProcessOutputResult?>(new ProcessOutputResult(_exitCode, string.Empty, string.Empty));
        }
    }
}
