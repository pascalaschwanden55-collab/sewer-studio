using NightlySoakRunner;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class NightlySoakRunnerTests
{
    [Fact]
    public void Parse_MehrereVideosUndSchnelllauf_WerdenUebernommen()
    {
        using var fixture = new SoakFixture();

        var options = NightlySoakOptions.Parse([
            "--video", fixture.Video1,
            "--video", fixture.Video2,
            "--golden", fixture.Golden,
            "--duration-minutes", "2",
            "--max-rounds", "3",
            "--interval-sec", "1",
            "--csv", fixture.Csv,
        ]);

        Assert.True(options.IsValid(out var error), error);
        Assert.Equal(2, options.VideoPaths.Count);
        Assert.Equal(TimeSpan.FromMinutes(2), options.Duration);
        Assert.Equal(3, options.MaxRounds);
        Assert.Equal(Path.GetFullPath(fixture.Csv), options.CsvPath);
    }

    [Fact]
    public void IsValid_NichtLokalerSidecar_WirdAbgewiesen()
    {
        using var fixture = new SoakFixture();
        var options = NightlySoakOptions.Parse([
            "--video", fixture.Video1,
            "--golden", fixture.Golden,
            "--sidecar", "http://192.168.1.20:8100",
        ]);

        Assert.False(options.IsValid(out var error));
        Assert.Contains("lokale Adresse", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_SchreibtJedeRundeUndWechseltVideos()
    {
        using var fixture = new SoakFixture();
        var options = fixture.CreateOptions("--max-rounds", "2", "--warmup-rounds", "0");
        var probe = new FakeProbe([
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(100), 42, 200),
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(120), 42, 210),
        ]);
        var resources = new FakeResourceSampler([
            new ResourceSnapshot(42, 500, 100, 200, 190),
            new ResourceSnapshot(42, 510, 101, 210, 200),
        ]);

        var result = await new NightlySoakRunService(probe, resources, new NoDelay())
            .RunAsync(options);

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.CompletedRounds);
        Assert.Equal([fixture.Video1, fixture.Video2], probe.Videos);
        var lines = await File.ReadAllLinesAsync(fixture.Csv);
        Assert.Equal(3, lines.Length);
        Assert.Contains("private_memory_mb", lines[0]);
        Assert.Contains(";true;120;42;510;101;210;200;", lines[2]);
    }

    [Fact]
    public async Task RunAsync_StopptBeiRamWachstumUndDokumentiertFehler()
    {
        using var fixture = new SoakFixture();
        var options = fixture.CreateOptions(
            "--max-rounds", "3",
            "--warmup-rounds", "0",
            "--max-memory-growth-mb", "5");
        var probe = new FakeProbe([
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(100), 7, null),
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(110), 7, null),
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(120), 7, null),
        ]);
        var resources = new FakeResourceSampler([
            new ResourceSnapshot(7, 100, 10, null, null),
            new ResourceSnapshot(7, 106, 10, null, null),
            new ResourceSnapshot(7, 107, 10, null, null),
        ]);

        var result = await new NightlySoakRunService(probe, resources, new NoDelay())
            .RunAsync(options);

        Assert.False(result.Success);
        Assert.Equal(2, result.CompletedRounds);
        Assert.Contains("RAM-Wachstum", result.Message);
        var csv = await File.ReadAllTextAsync(fixture.Csv);
        Assert.Contains(";false;110;7;106;10;;;RAM-Wachstum", csv);
    }

    [Fact]
    public async Task RunAsync_StopptAbFuenfterRundeBeiZuHohemP95()
    {
        using var fixture = new SoakFixture();
        var options = fixture.CreateOptions(
            "--max-rounds", "6",
            "--warmup-rounds", "0",
            "--max-p95-ms", "105");
        var probe = new FakeProbe([
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(100), 9, null),
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(100), 9, null),
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(100), 9, null),
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(100), 9, null),
            new SoakProbeResult(true, TimeSpan.FromMilliseconds(110), 9, null),
        ]);
        var resources = new FakeResourceSampler(Enumerable.Repeat(
            new ResourceSnapshot(9, 100, 10, null, null),
            5));

        var result = await new NightlySoakRunService(probe, resources, new NoDelay())
            .RunAsync(options);

        Assert.False(result.Success);
        Assert.Equal(5, result.CompletedRounds);
        Assert.Contains("95%-Laufzeit", result.Message);
    }

    [Fact]
    public void Percentile95_VerwendetDenNaechstHoeherenMesswert()
    {
        var values = Enumerable.Range(1, 20).Select(value => (double)value);

        Assert.Equal(19, NightlySoakRunService.Percentile95(values));
    }

    private sealed class SoakFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"soak-{Guid.NewGuid():N}");

        public SoakFixture()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllBytes(Video1, [0x00]);
            File.WriteAllBytes(Video2, [0x01]);
            File.WriteAllText(Golden, """
                {
                  "contract_version": 1,
                  "expected_sidecar_version": "1.2.0",
                  "minimum_decoded_frames": 1,
                  "minimum_sam_masks": 0,
                  "required_checks": []
                }
                """);
        }

        public string Video1 => Path.Combine(_root, "a.mp4");
        public string Video2 => Path.Combine(_root, "b.mp4");
        public string Golden => Path.Combine(_root, "golden.json");
        public string Csv => Path.Combine(_root, "result.csv");

        public NightlySoakOptions CreateOptions(params string[] extra)
        {
            var args = new List<string>
            {
                "--video", Video1,
                "--video", Video2,
                "--golden", Golden,
                "--interval-sec", "0.001",
                "--csv", Csv,
            };
            args.AddRange(extra);
            var options = NightlySoakOptions.Parse([.. args]);
            Assert.True(options.IsValid(out var error), error);
            return options;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeProbe(IEnumerable<SoakProbeResult> results) : ISoakProbe
    {
        private readonly Queue<SoakProbeResult> _results = new(results);
        public List<string> Videos { get; } = [];

        public Task<SoakProbeResult> RunAsync(string videoPath, int round, CancellationToken ct)
        {
            Videos.Add(videoPath);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class FakeResourceSampler(IEnumerable<ResourceSnapshot> results) : IResourceSampler
    {
        private readonly Queue<ResourceSnapshot> _results = new(results);

        public Task<ResourceSnapshot> CaptureAsync(
            int processId,
            double? healthVramMb,
            NightlySoakOptions options,
            CancellationToken ct) => Task.FromResult(_results.Dequeue());
    }

    private sealed class NoDelay : ISoakDelay
    {
        public Task WaitAsync(TimeSpan duration, CancellationToken ct) => Task.CompletedTask;
    }
}
