using System.Diagnostics;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

var options = SidecarSmokeOptions.Parse(args);
if (options.ShowHelp)
{
    SidecarSmokeOptions.PrintHelp();
    return 0;
}

if (!options.IsValid(out var error))
{
    Console.Error.WriteLine(error);
    SidecarSmokeOptions.PrintHelp();
    return 2;
}

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSec));
var report = new SidecarSmokeReport
{
    CreatedUtc = DateTimeOffset.UtcNow,
    SidecarUrl = options.SidecarUrl,
    Source = options.SourceDescription,
};

try
{
    var imageBytes = options.VideoPath is not null
        ? await ExtractFrameAsync(options, cts.Token)
        : await File.ReadAllBytesAsync(options.ImagePath!, cts.Token);

    var imageBase64 = Convert.ToBase64String(imageBytes);
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(options.TimeoutSec) };
    var client = new VisionPipelineClient(new Uri(options.SidecarUrl), http, options.Token);

    Console.WriteLine("1/5 Health...");
    var health = await client.CheckHealthDetailedAsync(cts.Token);
    report.Health = new HealthReport(
        health.IsReachable,
        health.IsAuthorized,
        health.StatusCode,
        health.Error,
        health.Health?.Status,
        health.Health?.Gpu?.VramAllocatedGb,
        health.Health?.Gpu?.VramTotalGb,
        health.Health?.Gpu?.LoadedModels?.Keys.OrderBy(k => k).ToArray() ?? []);

    if (!health.IsReachable || !health.IsAuthorized)
        throw new InvalidOperationException($"Sidecar health failed: reachable={health.IsReachable}, authorized={health.IsAuthorized}, error={health.Error}");

    Console.WriteLine("2/5 YOLO classify...");
    var classify = await client.ClassifyYoloAsync(new YoloClassifyRequest(imageBase64, options.TopK), cts.Token);
    report.Classify = classify;

    Console.WriteLine("3/5 YOLO detect...");
    var yolo = await client.DetectYoloAsync(new YoloRequest(imageBase64, options.YoloConfidence), cts.Token);
    report.Yolo = yolo;

    if (options.RunDino)
    {
        Console.WriteLine("4/5 DINO...");
        report.Dino = await client.DetectDinoAsync(
            new DinoRequest(imageBase64, options.DinoPrompt, options.DinoBoxThreshold, options.DinoTextThreshold),
            cts.Token);
    }
    else
    {
        Console.WriteLine("4/5 DINO skipped");
    }

    if (options.RunSam)
    {
        Console.WriteLine("5/5 SAM...");
        var samBoxes = BuildSamBoxes(yolo, options.AllowSamFallbackBox);
        if (samBoxes.Count == 0)
        {
            report.SamSkippedReason = "Keine YOLO-Box vorhanden und --sam-fallback-box nicht gesetzt.";
        }
        else
        {
            report.Sam = await client.SegmentSamAsync(
                new SamRequest(imageBase64, samBoxes, options.PipeDiameterMm),
                cts.Token);
        }
    }
    else
    {
        Console.WriteLine("5/5 SAM skipped");
    }

    report.Success = true;
}
catch (Exception ex)
{
    report.Success = false;
    report.Error = ex.ToString();
    Console.Error.WriteLine(ex.Message);
}

if (options.ReportPath is not null)
{
    var dir = Path.GetDirectoryName(options.ReportPath);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
    await File.WriteAllTextAsync(options.ReportPath, JsonSerializer.Serialize(report, JsonOptions()));
    Console.WriteLine($"Report: {options.ReportPath}");
}

Console.WriteLine(report.Success ? "E2E smoke PASS" : "E2E smoke FAIL");
return report.Success ? 0 : 1;

static IReadOnlyList<SamBoundingBox> BuildSamBoxes(YoloResponse yolo, bool allowFallbackBox)
{
    var first = yolo.Detections.OrderByDescending(d => d.Confidence).FirstOrDefault();
    if (first is not null)
    {
        return
        [
            new SamBoundingBox(first.X1, first.Y1, first.X2, first.Y2, first.ClassName, first.Confidence)
        ];
    }

    return allowFallbackBox
        ? [new SamBoundingBox(0.2, 0.2, 0.8, 0.8, "manual_fallback", 0.0)]
        : [];
}

static async Task<byte[]> ExtractFrameAsync(SidecarSmokeOptions options, CancellationToken ct)
{
    var tempPath = Path.Combine(Path.GetTempPath(), $"sewer-sidecar-e2e-{Guid.NewGuid():N}.jpg");
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = options.FfmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(options.VideoSecond.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(options.VideoPath!);
        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-q:v");
        psi.ArgumentList.Add("2");
        psi.ArgumentList.Add(tempPath);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg ExitCode {process.ExitCode}: {stderr}");

        if (!File.Exists(tempPath))
            throw new FileNotFoundException("ffmpeg hat kein Frame erzeugt.", tempPath);

        return await File.ReadAllBytesAsync(tempPath, ct);
    }
    finally
    {
        try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
    }
}

static JsonSerializerOptions JsonOptions() => new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
};

public sealed record HealthReport(
    bool IsReachable,
    bool IsAuthorized,
    int? StatusCode,
    string? Error,
    string? Status,
    double? VramAllocatedGb,
    double? VramTotalGb,
    IReadOnlyList<string> LoadedModels);

public sealed class SidecarSmokeReport
{
    public DateTimeOffset CreatedUtc { get; init; }
    public string SidecarUrl { get; init; } = "";
    public string Source { get; init; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public HealthReport? Health { get; set; }
    public YoloClassifyResponse? Classify { get; set; }
    public YoloResponse? Yolo { get; set; }
    public DinoResponse? Dino { get; set; }
    public SamResponse? Sam { get; set; }
    public string? SamSkippedReason { get; set; }
}

public sealed class SidecarSmokeOptions
{
    public bool ShowHelp { get; private init; }
    public string SidecarUrl { get; private init; } = "http://127.0.0.1:8100";
    public string? Token { get; private init; }
    public string? ImagePath { get; private init; }
    public string? VideoPath { get; private init; }
    public double VideoSecond { get; private init; }
    public string FfmpegPath { get; private init; } = "ffmpeg";
    public string? ReportPath { get; private init; }
    public int TimeoutSec { get; private init; } = 120;
    public int TopK { get; private init; } = 5;
    public double YoloConfidence { get; private init; } = 0.25;
    public bool RunDino { get; private init; }
    public bool RunSam { get; private init; }
    public bool AllowSamFallbackBox { get; private init; }
    public string DinoPrompt { get; private init; } = "pipe crack, root intrusion, offset joint, water level, pipe damage";
    public double DinoBoxThreshold { get; private init; } = 0.25;
    public double DinoTextThreshold { get; private init; } = 0.20;
    public int? PipeDiameterMm { get; private init; }

    public string SourceDescription => VideoPath is not null
        ? $"video={VideoPath}, second={VideoSecond:0.###}"
        : $"image={ImagePath}";

    public bool IsValid(out string error)
    {
        if (ImagePath is null && VideoPath is null)
        {
            error = "Bitte --image oder --video angeben.";
            return false;
        }
        if (ImagePath is not null && VideoPath is not null)
        {
            error = "--image und --video duerfen nicht zusammen gesetzt werden.";
            return false;
        }
        if (ImagePath is not null && !File.Exists(ImagePath))
        {
            error = $"Bild nicht gefunden: {ImagePath}";
            return false;
        }
        if (VideoPath is not null && !File.Exists(VideoPath))
        {
            error = $"Video nicht gefunden: {VideoPath}";
            return false;
        }
        error = "";
        return true;
    }

    public static SidecarSmokeOptions Parse(string[] args)
    {
        var b = new Builder();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    b.ShowHelp = true;
                    break;
                case "--sidecar":
                    b.SidecarUrl = Next(args, ref i);
                    break;
                case "--token":
                    b.Token = Next(args, ref i);
                    break;
                case "--image":
                    b.ImagePath = Next(args, ref i);
                    break;
                case "--video":
                    b.VideoPath = Next(args, ref i);
                    break;
                case "--at":
                    b.VideoSecond = double.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--ffmpeg":
                    b.FfmpegPath = Next(args, ref i);
                    break;
                case "--report":
                    b.ReportPath = Next(args, ref i);
                    break;
                case "--timeout-sec":
                    b.TimeoutSec = int.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--top-k":
                    b.TopK = int.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--yolo-confidence":
                    b.YoloConfidence = double.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--run-dino":
                    b.RunDino = true;
                    break;
                case "--run-sam":
                    b.RunSam = true;
                    break;
                case "--sam-fallback-box":
                    b.AllowSamFallbackBox = true;
                    break;
                case "--dino-prompt":
                    b.DinoPrompt = Next(args, ref i);
                    break;
                case "--pipe-diameter-mm":
                    b.PipeDiameterMm = int.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ArgumentException($"Unbekannte Option: {args[i]}");
            }
        }

        return b.Build();
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
SidecarE2eSmoke - opt-in Smoke-Test fuer echten Sidecar/GPU-Lauf.

Beispiele:
  dotnet run --project tools/SidecarE2eSmoke -- --image C:\tmp\frame.png --report C:\tmp\sidecar-e2e.json
  dotnet run --project tools/SidecarE2eSmoke -- --video D:\Haltungen\35723-35734\20230831_35723-35734.mp4 --at 12.5 --run-dino --run-sam --sam-fallback-box

Optionen:
  --sidecar <url>           Standard: http://127.0.0.1:8100
  --token <token>           Sidecar-Token, falls nicht aus AppData geladen werden soll
  --image <pfad>            Einzelbild testen
  --video <pfad> --at <sec> Frame aus Video per ffmpeg extrahieren
  --report <pfad>           JSON-Report schreiben
  --run-dino                DINO-Endpunkt zusaetzlich testen
  --run-sam                 SAM-Endpunkt zusaetzlich testen
  --sam-fallback-box        SAM mit Mittelbox testen, falls YOLO keine Box liefert
""");
    }

    private static string Next(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Wert fehlt fuer {args[index]}");
        index++;
        return args[index];
    }

    private sealed class Builder
    {
        public bool ShowHelp { get; set; }
        public string SidecarUrl { get; set; } = "http://127.0.0.1:8100";
        public string? Token { get; set; }
        public string? ImagePath { get; set; }
        public string? VideoPath { get; set; }
        public double VideoSecond { get; set; }
        public string FfmpegPath { get; set; } = "ffmpeg";
        public string? ReportPath { get; set; }
        public int TimeoutSec { get; set; } = 120;
        public int TopK { get; set; } = 5;
        public double YoloConfidence { get; set; } = 0.25;
        public bool RunDino { get; set; }
        public bool RunSam { get; set; }
        public bool AllowSamFallbackBox { get; set; }
        public string DinoPrompt { get; set; } = "pipe crack, root intrusion, offset joint, water level, pipe damage";
        public double DinoBoxThreshold { get; set; } = 0.25;
        public double DinoTextThreshold { get; set; } = 0.20;
        public int? PipeDiameterMm { get; set; }

        public SidecarSmokeOptions Build() => new()
        {
            ShowHelp = ShowHelp,
            SidecarUrl = SidecarUrl,
            Token = Token,
            ImagePath = ImagePath,
            VideoPath = VideoPath,
            VideoSecond = VideoSecond,
            FfmpegPath = FfmpegPath,
            ReportPath = ReportPath,
            TimeoutSec = TimeoutSec,
            TopK = TopK,
            YoloConfidence = YoloConfidence,
            RunDino = RunDino,
            RunSam = RunSam,
            AllowSamFallbackBox = AllowSamFallbackBox,
            DinoPrompt = DinoPrompt,
            DinoBoxThreshold = DinoBoxThreshold,
            DinoTextThreshold = DinoTextThreshold,
            PipeDiameterMm = PipeDiameterMm,
        };
    }
}
