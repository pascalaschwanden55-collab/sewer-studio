using System.Globalization;
using AuswertungPro.Next.Infrastructure.Ai.Shared;

namespace SidecarE2eSmoke;

public sealed class SidecarSmokeOptions
{
    public bool ShowHelp { get; private init; }
    public string SidecarUrl { get; private init; } = "http://127.0.0.1:8100";
    public string? Token { get; private init; }
    public string? ImagePath { get; private init; }
    public string? VideoPath { get; private init; }
    public double VideoSecond { get; private init; }
    public int FrameCount { get; private init; } = 3;
    public double FrameStepSeconds { get; private init; } = 1.0;
    public string FfmpegPath { get; private init; } = "ffmpeg";
    public string? ReportPath { get; private init; }
    public string? GoldenPath { get; private init; }
    public int TimeoutSec { get; private init; } = 600;
    public int StartupTimeoutSec { get; private init; } = 300;
    public int TopK { get; private init; } = 5;
    public double YoloConfidence { get; private init; } = 0.25;
    public bool RunDino { get; private init; }
    public bool RunSam { get; private init; }
    public bool AllowSamFallbackBox { get; private init; }
    public bool FullPipeline { get; private init; }
    public bool StartSidecar { get; private init; }
    public bool KeepStartedSidecar { get; private init; }
    public string DinoPrompt { get; private init; } =
        "pipe crack, root intrusion, offset joint, water level, pipe damage";
    public double DinoBoxThreshold { get; private init; } = 0.25;
    public double DinoTextThreshold { get; private init; } = 0.20;
    public int? PipeDiameterMm { get; private init; } = 300;

    public bool ShouldRunDino => RunDino || FullPipeline;
    public bool ShouldRunSam => RunSam || FullPipeline;
    public bool ShouldUseSamFallbackBox => AllowSamFallbackBox || FullPipeline;

    public string SourceDescription => VideoPath is not null
        ? $"video={VideoPath}, start={VideoSecond:0.###}, frames={(FullPipeline ? FrameCount : 1)}"
        : $"image={ImagePath}";

    public bool IsValid(out string error)
    {
        if (ImagePath is null && VideoPath is null)
            return Fail("Bitte --image oder --video angeben.", out error);
        if (ImagePath is not null && VideoPath is not null)
            return Fail("--image und --video duerfen nicht zusammen gesetzt werden.", out error);
        if (ImagePath is not null && !File.Exists(ImagePath))
            return Fail($"Bild nicht gefunden: {ImagePath}", out error);
        if (VideoPath is not null && !File.Exists(VideoPath))
            return Fail($"Video nicht gefunden: {VideoPath}", out error);
        if (!Uri.TryCreate(SidecarUrl, UriKind.Absolute, out var sidecarUri) || !sidecarUri.IsLoopback)
            return Fail("--sidecar muss eine lokale Adresse wie http://127.0.0.1:8100 sein.", out error);
        if (TimeoutSec <= 0 || StartupTimeoutSec <= 0)
            return Fail("Zeitlimits muessen groesser als 0 sein.", out error);
        if (!double.IsFinite(VideoSecond) || VideoSecond < 0)
            return Fail("--at muss 0 oder groesser sein.", out error);
        if (FrameCount is < 1 or > 20)
            return Fail("--frames muss zwischen 1 und 20 liegen.", out error);
        if (!double.IsFinite(FrameStepSeconds) || FrameStepSeconds <= 0)
            return Fail("--frame-step muss groesser als 0 sein.", out error);
        if (TopK is < 1 or > 50)
            return Fail("--top-k muss zwischen 1 und 50 liegen.", out error);
        if (!IsProbability(YoloConfidence)
            || !IsProbability(DinoBoxThreshold)
            || !IsProbability(DinoTextThreshold))
        {
            return Fail("KI-Schwellenwerte muessen zwischen 0 und 1 liegen.", out error);
        }
        if (FullPipeline && VideoPath is null)
            return Fail("--full-pipeline braucht ein echtes Video (--video).", out error);
        if (FullPipeline && !File.Exists(ResolveGoldenPath()))
            return Fail($"Golden-Vertrag nicht gefunden: {ResolveGoldenPath()}", out error);
        if (PipeDiameterMm is <= 0 or > 10000)
            return Fail("--pipe-diameter-mm muss zwischen 1 und 10000 liegen.", out error);

        error = string.Empty;
        return true;
    }

    public string ResolveGoldenPath()
    {
        if (!string.IsNullOrWhiteSpace(GoldenPath))
            return Path.GetFullPath(GoldenPath);

        var outputCandidate = Path.Combine(AppContext.BaseDirectory, "golden", "pipeline-contract.v1.json");
        if (File.Exists(outputCandidate))
            return outputCandidate;

        return Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "tools",
            "SidecarE2eSmoke",
            "golden",
            "pipeline-contract.v1.json"));
    }

    public string? ResolveReportPath()
    {
        if (!string.IsNullOrWhiteSpace(ReportPath))
            return Path.GetFullPath(ReportPath);
        if (!FullPipeline)
            return null;

        return Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "artifacts",
            "sidecar-e2e",
            $"sidecar-e2e-{DateTime.Now:yyyyMMdd-HHmmss}.json"));
    }

    public static SidecarSmokeOptions Parse(string[] args)
    {
        var builder = new Builder { FfmpegPath = FfmpegLocator.ResolveFfmpeg() };
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h": builder.ShowHelp = true; break;
                case "--sidecar": builder.SidecarUrl = Next(args, ref i); break;
                case "--token": builder.Token = Next(args, ref i); break;
                case "--image": builder.ImagePath = Next(args, ref i); break;
                case "--video": builder.VideoPath = Next(args, ref i); break;
                case "--at": builder.VideoSecond = ParseDouble(Next(args, ref i), "--at"); break;
                case "--frames": builder.FrameCount = ParseInt(Next(args, ref i), "--frames"); break;
                case "--frame-step": builder.FrameStepSeconds = ParseDouble(Next(args, ref i), "--frame-step"); break;
                case "--ffmpeg": builder.FfmpegPath = Next(args, ref i); break;
                case "--report": builder.ReportPath = Next(args, ref i); break;
                case "--golden": builder.GoldenPath = Next(args, ref i); break;
                case "--timeout-sec": builder.TimeoutSec = ParseInt(Next(args, ref i), "--timeout-sec"); break;
                case "--startup-timeout-sec": builder.StartupTimeoutSec = ParseInt(Next(args, ref i), "--startup-timeout-sec"); break;
                case "--top-k": builder.TopK = ParseInt(Next(args, ref i), "--top-k"); break;
                case "--yolo-confidence": builder.YoloConfidence = ParseDouble(Next(args, ref i), "--yolo-confidence"); break;
                case "--dino-box-threshold": builder.DinoBoxThreshold = ParseDouble(Next(args, ref i), "--dino-box-threshold"); break;
                case "--dino-text-threshold": builder.DinoTextThreshold = ParseDouble(Next(args, ref i), "--dino-text-threshold"); break;
                case "--run-dino": builder.RunDino = true; break;
                case "--run-sam": builder.RunSam = true; break;
                case "--sam-fallback-box": builder.AllowSamFallbackBox = true; break;
                case "--full-pipeline": builder.FullPipeline = true; break;
                case "--start-sidecar": builder.StartSidecar = true; break;
                case "--keep-sidecar": builder.KeepStartedSidecar = true; break;
                case "--dino-prompt": builder.DinoPrompt = Next(args, ref i); break;
                case "--pipe-diameter-mm": builder.PipeDiameterMm = ParseInt(Next(args, ref i), "--pipe-diameter-mm"); break;
                default: throw new ArgumentException($"Unbekannte Option: {args[i]}");
            }
        }

        return builder.Build();
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
SidecarE2eSmoke - echter, opt-in KI-Test ohne Sewer-Studio-Oberflaeche.

Vollstaendiger Video-Vertragstest:
  dotnet run --project tools/SidecarE2eSmoke -- --video D:\Test\kurz.mp4 --at 2 --full-pipeline --start-sidecar

Einzelbild-Schnelltest:
  dotnet run --project tools/SidecarE2eSmoke -- --image C:\tmp\frame.png --run-dino --run-sam --sam-fallback-box

Wichtige Optionen:
  --sidecar <url>           Standard: http://127.0.0.1:8100 (nur lokal erlaubt)
  --token <token>           Sonst automatische Aufloesung wie in Sewer Studio
  --video <pfad> --at <sec> Startposition im echten Video
  --frames <anzahl>         Standard im Volltest: 3
  --frame-step <sekunden>   Abstand der Videobilder, Standard: 1
  --full-pipeline           Video + YOLO/DINO/SAM + Quantifizierung + Golden-Vertrag
  --start-sidecar           Sidecar bei Bedarf automatisch starten
  --keep-sidecar            Automatisch gestarteten Sidecar danach weiterlaufen lassen
  --golden <pfad>           Anderen Golden-Vertrag verwenden
  --report <pfad>           JSON-Ergebnis schreiben (Volltest: automatisch unter artifacts)
  --timeout-sec <sekunden>  Gesamtlauf, Standard: 600
""");
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private static bool IsProbability(double value)
        => double.IsFinite(value) && value is >= 0 and <= 1;

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
        public string SidecarUrl { get; set; } = "http://127.0.0.1:8100";
        public string? Token { get; set; }
        public string? ImagePath { get; set; }
        public string? VideoPath { get; set; }
        public double VideoSecond { get; set; }
        public int FrameCount { get; set; } = 3;
        public double FrameStepSeconds { get; set; } = 1.0;
        public string FfmpegPath { get; set; } = "ffmpeg";
        public string? ReportPath { get; set; }
        public string? GoldenPath { get; set; }
        public int TimeoutSec { get; set; } = 600;
        public int StartupTimeoutSec { get; set; } = 300;
        public int TopK { get; set; } = 5;
        public double YoloConfidence { get; set; } = 0.25;
        public bool RunDino { get; set; }
        public bool RunSam { get; set; }
        public bool AllowSamFallbackBox { get; set; }
        public bool FullPipeline { get; set; }
        public bool StartSidecar { get; set; }
        public bool KeepStartedSidecar { get; set; }
        public string DinoPrompt { get; set; } = "pipe crack, root intrusion, offset joint, water level, pipe damage";
        public double DinoBoxThreshold { get; set; } = 0.25;
        public double DinoTextThreshold { get; set; } = 0.20;
        public int? PipeDiameterMm { get; set; } = 300;

        public SidecarSmokeOptions Build() => new()
        {
            ShowHelp = ShowHelp,
            SidecarUrl = SidecarUrl,
            Token = Token,
            ImagePath = ImagePath,
            VideoPath = VideoPath,
            VideoSecond = VideoSecond,
            FrameCount = FrameCount,
            FrameStepSeconds = FrameStepSeconds,
            FfmpegPath = FfmpegPath,
            ReportPath = ReportPath,
            GoldenPath = GoldenPath,
            TimeoutSec = TimeoutSec,
            StartupTimeoutSec = StartupTimeoutSec,
            TopK = TopK,
            YoloConfidence = YoloConfidence,
            RunDino = RunDino,
            RunSam = RunSam,
            AllowSamFallbackBox = AllowSamFallbackBox,
            FullPipeline = FullPipeline,
            StartSidecar = StartSidecar,
            KeepStartedSidecar = KeepStartedSidecar,
            DinoPrompt = DinoPrompt,
            DinoBoxThreshold = DinoBoxThreshold,
            DinoTextThreshold = DinoTextThreshold,
            PipeDiameterMm = PipeDiameterMm,
        };
    }
}
