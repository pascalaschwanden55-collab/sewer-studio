using System.Globalization;
using SidecarE2eSmoke;

namespace NightlySoakRunner;

public sealed class NightlySoakOptions
{
    public bool ShowHelp { get; private init; }
    public IReadOnlyList<string> VideoPaths { get; private init; } = [];
    public TimeSpan Duration { get; private init; } = TimeSpan.FromHours(8);
    public TimeSpan Interval { get; private init; } = TimeSpan.FromSeconds(5);
    public int? MaxRounds { get; private init; }
    public int WarmupRounds { get; private init; } = 1;
    public string CsvPath { get; private init; } = DefaultCsvPath();
    public string SidecarUrl { get; private init; } = "http://127.0.0.1:8100";
    public string? Token { get; private init; }
    public bool StartSidecar { get; private init; }
    public string FfmpegPath { get; private init; } = "ffmpeg";
    public string? GoldenPath { get; private init; }
    public double VideoSecond { get; private init; }
    public int FrameCount { get; private init; } = 3;
    public double FrameStepSeconds { get; private init; } = 1;
    public int TimeoutSeconds { get; private init; } = 600;
    public int StartupTimeoutSeconds { get; private init; } = 300;
    public int? MonitorProcessId { get; private init; }
    public string NvidiaSmiPath { get; private init; } = "nvidia-smi";
    public bool RequireNvidiaSmi { get; private init; }
    public double MaxPrivateMemoryMb { get; private init; } = 16_384;
    public int MaxHandles { get; private init; } = 4_096;
    public double MaxP95Milliseconds { get; private init; } = 900_000;
    public double MaxVramMb { get; private init; } = 24_576;
    public double MaxMemoryGrowthMb { get; private init; } = 2_048;
    public int MaxHandleGrowth { get; private init; } = 512;

    public bool IsValid(out string error)
    {
        if (VideoPaths.Count == 0)
            return Fail("Mindestens ein echtes Testvideo mit --video angeben.", out error);
        if (Duration <= TimeSpan.Zero)
            return Fail("Die Laufzeit muss groesser als 0 sein.", out error);
        if (Interval <= TimeSpan.Zero)
            return Fail("Das Messintervall muss groesser als 0 sein.", out error);
        if (MaxRounds is <= 0)
            return Fail("--max-rounds muss groesser als 0 sein.", out error);
        if (WarmupRounds < 0)
            return Fail("--warmup-rounds darf nicht negativ sein.", out error);
        if (MonitorProcessId is <= 0)
            return Fail("--monitor-pid muss groesser als 0 sein.", out error);
        if (string.IsNullOrWhiteSpace(CsvPath))
            return Fail("Der CSV-Ausgabepfad darf nicht leer sein.", out error);
        if (MaxPrivateMemoryMb <= 0 || MaxHandles <= 0 || MaxP95Milliseconds <= 0
            || MaxVramMb <= 0 || MaxMemoryGrowthMb < 0 || MaxHandleGrowth < 0)
        {
            return Fail("Alle Obergrenzen muessen positiv sein; Wachstumsgrenzen duerfen 0 sein.", out error);
        }

        foreach (var video in VideoPaths)
        {
            var smoke = CreateSmokeOptions(video, StartSidecar);
            if (!smoke.IsValid(out error))
                return false;
        }

        error = string.Empty;
        return true;
    }

    public SidecarSmokeOptions CreateSmokeOptions(string videoPath, bool startSidecar)
    {
        var args = new List<string>
        {
            "--video", videoPath,
            "--full-pipeline",
            "--sidecar", SidecarUrl,
            "--ffmpeg", FfmpegPath,
            "--at", VideoSecond.ToString(CultureInfo.InvariantCulture),
            "--frames", FrameCount.ToString(CultureInfo.InvariantCulture),
            "--frame-step", FrameStepSeconds.ToString(CultureInfo.InvariantCulture),
            "--timeout-sec", TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            "--startup-timeout-sec", StartupTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(Token))
        {
            args.Add("--token");
            args.Add(Token);
        }

        if (!string.IsNullOrWhiteSpace(GoldenPath))
        {
            args.Add("--golden");
            args.Add(GoldenPath);
        }

        if (startSidecar)
            args.Add("--start-sidecar");

        return SidecarSmokeOptions.Parse([.. args]);
    }

    public static NightlySoakOptions Parse(string[] args)
    {
        var builder = new Builder();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h": builder.ShowHelp = true; break;
                case "--video": builder.VideoPaths.Add(Next(args, ref i)); break;
                case "--duration-hours": builder.Duration = TimeSpan.FromHours(ParseDouble(Next(args, ref i), "--duration-hours")); break;
                case "--duration-minutes": builder.Duration = TimeSpan.FromMinutes(ParseDouble(Next(args, ref i), "--duration-minutes")); break;
                case "--interval-sec": builder.Interval = TimeSpan.FromSeconds(ParseDouble(Next(args, ref i), "--interval-sec")); break;
                case "--max-rounds": builder.MaxRounds = ParseInt(Next(args, ref i), "--max-rounds"); break;
                case "--warmup-rounds": builder.WarmupRounds = ParseInt(Next(args, ref i), "--warmup-rounds"); break;
                case "--csv": builder.CsvPath = Path.GetFullPath(Next(args, ref i)); break;
                case "--sidecar": builder.SidecarUrl = Next(args, ref i); break;
                case "--token": builder.Token = Next(args, ref i); break;
                case "--start-sidecar": builder.StartSidecar = true; break;
                case "--ffmpeg": builder.FfmpegPath = Next(args, ref i); break;
                case "--golden": builder.GoldenPath = Next(args, ref i); break;
                case "--at": builder.VideoSecond = ParseDouble(Next(args, ref i), "--at"); break;
                case "--frames": builder.FrameCount = ParseInt(Next(args, ref i), "--frames"); break;
                case "--frame-step": builder.FrameStepSeconds = ParseDouble(Next(args, ref i), "--frame-step"); break;
                case "--timeout-sec": builder.TimeoutSeconds = ParseInt(Next(args, ref i), "--timeout-sec"); break;
                case "--startup-timeout-sec": builder.StartupTimeoutSeconds = ParseInt(Next(args, ref i), "--startup-timeout-sec"); break;
                case "--monitor-pid": builder.MonitorProcessId = ParseInt(Next(args, ref i), "--monitor-pid"); break;
                case "--nvidia-smi": builder.NvidiaSmiPath = Next(args, ref i); break;
                case "--require-nvidia-smi": builder.RequireNvidiaSmi = true; break;
                case "--max-private-mb": builder.MaxPrivateMemoryMb = ParseDouble(Next(args, ref i), "--max-private-mb"); break;
                case "--max-handles": builder.MaxHandles = ParseInt(Next(args, ref i), "--max-handles"); break;
                case "--max-p95-ms": builder.MaxP95Milliseconds = ParseDouble(Next(args, ref i), "--max-p95-ms"); break;
                case "--max-vram-mb": builder.MaxVramMb = ParseDouble(Next(args, ref i), "--max-vram-mb"); break;
                case "--max-memory-growth-mb": builder.MaxMemoryGrowthMb = ParseDouble(Next(args, ref i), "--max-memory-growth-mb"); break;
                case "--max-handle-growth": builder.MaxHandleGrowth = ParseInt(Next(args, ref i), "--max-handle-growth"); break;
                default: throw new ArgumentException($"Unbekannte Option: {args[i]}");
            }
        }

        return builder.Build();
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
NightlySoakRunner - wiederholter, echter KI-Nachtlauf mit Ressourcenmessung.

Schnellpruefung mit zwei Runden:
  dotnet run --project tools/NightlySoakRunner -- --video D:\Test\kurz.mp4 --max-rounds 2 --start-sidecar

Acht Stunden mit mehreren Videos:
  dotnet run --project tools/NightlySoakRunner -- --video D:\Test\a.mp4 --video D:\Test\b.mp4 --duration-hours 8 --start-sidecar

Wichtige Optionen:
  --csv <pfad>                   CSV-Ziel; Standard unter artifacts/nightly-soak
  --interval-sec <sekunden>     Pause zwischen Runden; Standard 5
  --warmup-rounds <anzahl>      Runden vor der Leak-Basis; Standard 1
  --max-private-mb <MB>         RAM-Obergrenze; Standard 16384
  --max-memory-growth-mb <MB>   RAM-Wachstum ab Basis; Standard 2048
  --max-handles <anzahl>        Handle-Obergrenze; Standard 4096
  --max-handle-growth <anzahl>  Handle-Wachstum ab Basis; Standard 512
  --max-p95-ms <ms>             95%-Laufzeitgrenze; Standard 900000
  --max-vram-mb <MB>            GPU-Speichergrenze; Standard 24576
  --require-nvidia-smi          Abbrechen, wenn echte GPU-Messung fehlt
  --monitor-pid <pid>           Messprozess ausnahmsweise fest vorgeben

Der Sidecar darf nur lokal erreichbar sein. Strg+C beendet den Lauf sauber.
""");
    }

    private static string DefaultCsvPath() => Path.GetFullPath(Path.Combine(
        Environment.CurrentDirectory,
        "artifacts",
        "nightly-soak",
        $"nightly-soak-{DateTime.Now:yyyyMMdd-HHmmss}.csv"));

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private static string Next(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Wert fehlt fuer {args[index]}");
        return args[++index];
    }

    private static int ParseInt(string value, string option)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"Ungueltige Zahl fuer {option}: {value}");

    private static double ParseDouble(string value, string option)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"Ungueltige Zahl fuer {option}: {value}");

    private sealed class Builder
    {
        public bool ShowHelp { get; set; }
        public List<string> VideoPaths { get; } = [];
        public TimeSpan Duration { get; set; } = TimeSpan.FromHours(8);
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
        public int? MaxRounds { get; set; }
        public int WarmupRounds { get; set; } = 1;
        public string CsvPath { get; set; } = DefaultCsvPath();
        public string SidecarUrl { get; set; } = "http://127.0.0.1:8100";
        public string? Token { get; set; }
        public bool StartSidecar { get; set; }
        public string FfmpegPath { get; set; } = "ffmpeg";
        public string? GoldenPath { get; set; }
        public double VideoSecond { get; set; }
        public int FrameCount { get; set; } = 3;
        public double FrameStepSeconds { get; set; } = 1;
        public int TimeoutSeconds { get; set; } = 600;
        public int StartupTimeoutSeconds { get; set; } = 300;
        public int? MonitorProcessId { get; set; }
        public string NvidiaSmiPath { get; set; } = "nvidia-smi";
        public bool RequireNvidiaSmi { get; set; }
        public double MaxPrivateMemoryMb { get; set; } = 16_384;
        public int MaxHandles { get; set; } = 4_096;
        public double MaxP95Milliseconds { get; set; } = 900_000;
        public double MaxVramMb { get; set; } = 24_576;
        public double MaxMemoryGrowthMb { get; set; } = 2_048;
        public int MaxHandleGrowth { get; set; } = 512;

        public NightlySoakOptions Build() => new()
        {
            ShowHelp = ShowHelp,
            VideoPaths = VideoPaths.Select(Path.GetFullPath).ToArray(),
            Duration = Duration,
            Interval = Interval,
            MaxRounds = MaxRounds,
            WarmupRounds = WarmupRounds,
            CsvPath = CsvPath,
            SidecarUrl = SidecarUrl,
            Token = Token,
            StartSidecar = StartSidecar,
            FfmpegPath = FfmpegPath,
            GoldenPath = GoldenPath,
            VideoSecond = VideoSecond,
            FrameCount = FrameCount,
            FrameStepSeconds = FrameStepSeconds,
            TimeoutSeconds = TimeoutSeconds,
            StartupTimeoutSeconds = StartupTimeoutSeconds,
            MonitorProcessId = MonitorProcessId,
            NvidiaSmiPath = NvidiaSmiPath,
            RequireNvidiaSmi = RequireNvidiaSmi,
            MaxPrivateMemoryMb = MaxPrivateMemoryMb,
            MaxHandles = MaxHandles,
            MaxP95Milliseconds = MaxP95Milliseconds,
            MaxVramMb = MaxVramMb,
            MaxMemoryGrowthMb = MaxMemoryGrowthMb,
            MaxHandleGrowth = MaxHandleGrowth,
        };
    }
}
