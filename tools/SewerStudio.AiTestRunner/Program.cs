using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

return await AiTestRunner.RunAsync(args);

internal static class AiTestRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(string[] args)
    {
        RunnerOptions options;
        try
        {
            options = RunnerOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(RunnerOptions.Usage);
            return 64;
        }

        Directory.CreateDirectory(options.OutputDirectory);
        Directory.CreateDirectory(Path.Combine(options.OutputDirectory, "overlays"));

        var report = new AiAuditReport
        {
            CreatedUtc = DateTimeOffset.UtcNow,
            InputPath = options.InputPath,
            OutputDirectory = options.OutputDirectory,
            SidecarBaseUrl = options.SidecarBaseUrl,
            PipeDiameterMm = options.PipeDiameterMm,
            Limit = options.Limit
        };

        var cases = AiTestCaseDiscovery.Discover(options.InputPath, options.Limit).ToList();
        report.DiscoveredCases = cases
            .Select(c => new DiscoveredCaseReport(c.FramePath, c.ExpectedCode, c.ExpectedPrefix, c.Meter))
            .ToList();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds + 20) };
        var authToken = SidecarTokenResolver.Resolve();
        report.AuthTokenSource = authToken.Source;
        report.AuthTokenLoaded = !string.IsNullOrWhiteSpace(authToken.Token);
        var client = new VisionPipelineClient(new Uri(options.SidecarBaseUrl), http, authToken.Token);

        var health = await client.HealthCheckAsync().ConfigureAwait(false);
        report.SidecarReachable = health is not null;
        report.SidecarHealthStatus = health?.Status;
        report.SidecarHealthVersion = health?.Version;
        report.SidecarGpuModel = health?.Gpu?.CurrentModel;
        report.SidecarYoloModel = null;
        report.SidecarHealthError = health is null ? "Sidecar nicht erreichbar." : null;

        if (cases.Count == 0)
        {
            report.Summary = AiAuditSummary.Fail("Keine passenden PNG-Testframes gefunden.");
            await WriteReportsAsync(report, options.OutputDirectory).ConfigureAwait(false);
            Console.Error.WriteLine(report.Summary.Message);
            return 1;
        }

        if (health is null)
        {
            report.Summary = AiAuditSummary.Fail("Sidecar nicht erreichbar. Pipeline-Test wurde nicht gestartet.");
            await WriteReportsAsync(report, options.OutputDirectory).ConfigureAwait(false);
            Console.Error.WriteLine(report.Summary.Message);
            Console.Error.WriteLine("Health-Fehler: Sidecar nicht erreichbar.");
            return 2;
        }

        var service = new SingleFrameMultiModelService(client);

        foreach (var testCase in cases)
        {
            var frameReport = new FrameReport
            {
                FramePath = testCase.FramePath,
                ExpectedCode = testCase.ExpectedCode,
                ExpectedPrefix = testCase.ExpectedPrefix,
                Meter = testCase.Meter
            };
            report.Frames.Add(frameReport);

            try
            {
                using var frameCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
                var pngBytes = await File.ReadAllBytesAsync(testCase.FramePath, frameCts.Token).ConfigureAwait(false);
                var base64 = Convert.ToBase64String(pngBytes);
                var (imageWidth, imageHeight) = PngInfo.ReadDimensions(pngBytes);
                frameReport.ImageWidth = imageWidth;
                frameReport.ImageHeight = imageHeight;

                var result = await service.AnalyzeFrameAsync(
                    pngBytes,
                    options.PipeDiameterMm,
                    calibration: null,
                    frameCts.Token).ConfigureAwait(false);

                frameReport.IsRelevant = result.IsRelevant;
                frameReport.ViewType = null;
                frameReport.Error = result.Error;
                frameReport.YoloTimeMs = result.YoloTimeMs;
                frameReport.DinoTimeMs = result.DinoTimeMs;
                frameReport.SamTimeMs = result.SamTimeMs;
                frameReport.DinoDetectionCount = result.DinoDetections.Count;
                frameReport.MaskCount = result.SamResponse?.Masks.Count ?? 0;

                foreach (var detection in result.DinoDetections)
                {
                    var inferred = NormalizeCode(VsaCodeResolver.InferCodeFromLabel(detection.Label));
                    frameReport.Detections.Add(new DetectionReport
                    {
                        Label = detection.Label,
                        Phrase = detection.Phrase,
                        Confidence = detection.Confidence,
                        InferredCode = inferred,
                        Bbox = new[] { detection.X1, detection.Y1, detection.X2, detection.Y2 }
                    });
                }

                if (result.SamResponse is not null)
                {
                    for (var i = 0; i < result.SamResponse.Masks.Count; i++)
                    {
                        var mask = result.SamResponse.Masks[i];
                        var quant = i < result.QuantifiedMasks.Count ? result.QuantifiedMasks[i] : null;
                        var inferred = NormalizeCode(VsaCodeResolver.InferCodeFromLabel(mask.Label));
                        frameReport.Masks.Add(new MaskReport
                        {
                            Label = mask.Label,
                            Confidence = mask.Confidence,
                            InferredCode = inferred,
                            Bbox = mask.Bbox.ToArray(),
                            AreaPixels = mask.MaskAreaPixels,
                            WidthPixels = mask.WidthPixels,
                            HeightPixels = mask.HeightPixels,
                            CentroidX = mask.CentroidX,
                            CentroidY = mask.CentroidY,
                            ClockPosition = quant?.ClockPosition,
                            WidthMm = quant?.WidthMm,
                            HeightMm = quant?.HeightMm,
                            ExtentPercent = quant?.ExtentPercent,
                            CrossSectionReductionPercent = quant?.CrossSectionReductionPercent
                        });
                    }

                    var overlayPath = Path.Combine(
                        options.OutputDirectory,
                        "overlays",
                        $"{Path.GetFileNameWithoutExtension(testCase.FramePath)}.svg");

                    await SvgOverlayWriter.WriteAsync(
                        overlayPath,
                        pngBytes,
                        result.SamResponse,
                        scan: null,
                        frameReport.Masks,
                        frameCts.Token).ConfigureAwait(false);

                    frameReport.OverlaySvgPath = overlayPath;
                }

                frameReport.ObservedCodes = frameReport.Detections
                    .Select(d => d.InferredCode)
                    .Concat(frameReport.Masks.Select(m => m.InferredCode))
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToList();

                frameReport.HasExpectedPrefixHit = frameReport.ObservedCodes
                    .Any(code => code.StartsWith(testCase.ExpectedPrefix, StringComparison.OrdinalIgnoreCase));
            }
            catch (OperationCanceledException)
            {
                frameReport.Error = $"Timeout nach {options.TimeoutSeconds}s";
            }
            catch (Exception ex)
            {
                frameReport.Error = ex.ToString();
            }
        }

        report.Summary = AiAuditSummary.FromFrames(report.Frames);
        await WriteReportsAsync(report, options.OutputDirectory).ConfigureAwait(false);

        Console.WriteLine(report.Summary.Message);
        Console.WriteLine($"JSON: {Path.Combine(options.OutputDirectory, "report.json")}");
        Console.WriteLine($"HTML: {Path.Combine(options.OutputDirectory, "report.html")}");

        return report.Summary.Pass ? 0 : 1;
    }

    private static async Task WriteReportsAsync(AiAuditReport report, string outputDirectory)
    {
        var jsonPath = Path.Combine(outputDirectory, "report.json");
        var htmlPath = Path.Combine(outputDirectory, "report.html");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(report, JsonOptions),
            Encoding.UTF8).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            htmlPath,
            HtmlReportWriter.Render(report),
            Encoding.UTF8).ConfigureAwait(false);
    }

    private static string? NormalizeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }
}

internal sealed record RunnerOptions(
    string InputPath,
    string OutputDirectory,
    string SidecarBaseUrl,
    int PipeDiameterMm,
    int Limit,
    int TimeoutSeconds)
{
    public const string Usage =
        "Usage: dotnet run --project tools/SewerStudio.AiTestRunner -- " +
        "--input D:\\Haltungen\\07.717339-690761 --out .tmp\\ai-test-runner\\07.717339-690761 " +
        "--sidecar http://localhost:8100 --dn 300 --limit 8";

    public static RunnerOptions Parse(string[] args)
    {
        var input = @"D:\Haltungen\07.717339-690761";
        string? output = null;
        var sidecar = "http://localhost:8100";
        var dn = 300;
        var limit = 8;
        var timeout = 180;

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            string Next()
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Fehlender Wert fuer {name}");
                return args[++i];
            }

            switch (name)
            {
                case "--input":
                    input = Next();
                    break;
                case "--out":
                    output = Next();
                    break;
                case "--sidecar":
                    sidecar = Next().TrimEnd('/');
                    break;
                case "--dn":
                    dn = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--limit":
                    limit = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--timeout-sec":
                    timeout = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--help":
                case "-h":
                    throw new ArgumentException(Usage);
                default:
                    throw new ArgumentException($"Unbekannter Parameter: {name}");
            }
        }

        input = Path.GetFullPath(input);
        output ??= Path.Combine(".tmp", "ai-test-runner", Path.GetFileName(input.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

        return new RunnerOptions(
            input,
            Path.GetFullPath(output),
            sidecar,
            Math.Max(1, dn),
            Math.Clamp(limit, 1, 200),
            Math.Clamp(timeout, 10, 3600));
    }
}

internal static class AiTestCaseDiscovery
{
    private static readonly Regex CodeFromFrameName = new(
        @"_(?<code>[A-Za-z]{3}[A-Za-z.]*)_(?<meter>[0-9]+(?:[\.,][0-9]+)?)m_",
        RegexOptions.Compiled);

    private static readonly string[] PreferredPrefixes = { "BCA", "BCC", "BCD", "BDB", "AED" };

    public static IEnumerable<AiFrameCase> Discover(string inputPath, int limit)
    {
        if (!Directory.Exists(inputPath))
            yield break;

        var framesDir = Path.Combine(inputPath, "self_training_frames");
        var searchRoot = Directory.Exists(framesDir) ? framesDir : inputPath;
        var option = Directory.Exists(framesDir) ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;

        var candidates = Directory.EnumerateFiles(searchRoot, "*.png", option)
            .Select(Parse)
            .Where(c => c is not null)
            .Cast<AiFrameCase>()
            .Where(c => PreferredPrefixes.Contains(c.ExpectedPrefix, StringComparer.OrdinalIgnoreCase))
            .OrderBy(c => PrefixRank(c.ExpectedPrefix))
            .ThenBy(c => c.Meter ?? double.MaxValue)
            .ThenBy(c => c.FramePath, StringComparer.OrdinalIgnoreCase)
            .Take(limit);

        foreach (var candidate in candidates)
            yield return candidate;
    }

    private static AiFrameCase? Parse(string path)
    {
        var match = CodeFromFrameName.Match(Path.GetFileName(path));
        if (!match.Success)
            return null;

        var code = NormalizeCode(match.Groups["code"].Value);
        if (code.Length < 3)
            return null;

        double? meter = null;
        var meterText = match.Groups["meter"].Value.Replace(',', '.');
        if (double.TryParse(meterText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMeter))
            meter = parsedMeter;

        return new AiFrameCase(path, code, code[..3], meter);
    }

    private static int PrefixRank(string prefix)
    {
        var index = Array.FindIndex(PreferredPrefixes, p => p.Equals(prefix, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 999 : index;
    }

    private static string NormalizeCode(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }
}

internal sealed record AiFrameCase(string FramePath, string ExpectedCode, string ExpectedPrefix, double? Meter);

internal sealed record ResolvedSidecarToken(string? Token, string Source);

internal static class SidecarTokenResolver
{
    public static ResolvedSidecarToken Resolve()
    {
        var authEnvToken = Environment.GetEnvironmentVariable("SEWER_SIDECAR_AUTH_TOKEN")?.Trim();
        if (!string.IsNullOrWhiteSpace(authEnvToken))
            return new ResolvedSidecarToken(authEnvToken, "SEWER_SIDECAR_AUTH_TOKEN");

        var envToken = Environment.GetEnvironmentVariable("SEWER_SIDECAR_TOKEN")?.Trim();
        if (!string.IsNullOrWhiteSpace(envToken))
            return new ResolvedSidecarToken(envToken, "SEWER_SIDECAR_TOKEN");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var tokenPath = Path.Combine(localAppData, "SewerStudio", ".sidecar_token");
            if (File.Exists(tokenPath))
            {
                var token = File.ReadAllText(tokenPath).Trim();
                if (!string.IsNullOrWhiteSpace(token))
                    return new ResolvedSidecarToken(token, tokenPath);
            }
        }

        var envLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(envLocalAppData))
        {
            var tokenPath = Path.Combine(envLocalAppData, "SewerStudio", ".sidecar_token");
            if (File.Exists(tokenPath))
            {
                var token = File.ReadAllText(tokenPath).Trim();
                if (!string.IsNullOrWhiteSpace(token))
                    return new ResolvedSidecarToken(token, tokenPath);
            }
        }

        return new ResolvedSidecarToken(null, "none");
    }
}

internal static class PngInfo
{
    public static (int Width, int Height) ReadDimensions(byte[] pngBytes)
    {
        if (pngBytes.Length < 24)
            return (0, 0);

        try
        {
            var width = (pngBytes[16] << 24) | (pngBytes[17] << 16) | (pngBytes[18] << 8) | pngBytes[19];
            var height = (pngBytes[20] << 24) | (pngBytes[21] << 16) | (pngBytes[22] << 8) | pngBytes[23];
            return width > 0 && height > 0 ? (width, height) : (0, 0);
        }
        catch
        {
            return (0, 0);
        }
    }
}

internal sealed class AiAuditReport
{
    public string RunnerVersion { get; set; } = "2026-05-14-headless-v1";
    public DateTimeOffset CreatedUtc { get; set; }
    public string InputPath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public string SidecarBaseUrl { get; set; } = "";
    public int PipeDiameterMm { get; set; }
    public int Limit { get; set; }
    public bool SidecarReachable { get; set; }
    public string? SidecarHealthStatus { get; set; }
    public string? SidecarHealthVersion { get; set; }
    public string? SidecarGpuModel { get; set; }
    public string? SidecarYoloModel { get; set; }
    public string? SidecarHealthError { get; set; }
    public bool AuthTokenLoaded { get; set; }
    public string AuthTokenSource { get; set; } = "none";
    public List<DiscoveredCaseReport> DiscoveredCases { get; set; } = [];
    public List<FrameReport> Frames { get; set; } = [];
    public AiAuditSummary Summary { get; set; } = AiAuditSummary.Fail("Nicht ausgefuehrt.");
}

internal sealed record DiscoveredCaseReport(string FramePath, string ExpectedCode, string ExpectedPrefix, double? Meter);

internal sealed class FrameReport
{
    public string FramePath { get; set; } = "";
    public string ExpectedCode { get; set; } = "";
    public string ExpectedPrefix { get; set; } = "";
    public double? Meter { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public bool IsRelevant { get; set; }
    public string? ViewType { get; set; }
    public string? Error { get; set; }
    public double? PipeAxisConfidence { get; set; }
    public double? PipeAxisCenterX { get; set; }
    public double? PipeAxisCenterY { get; set; }
    public double? PipeAxisRadiusX { get; set; }
    public double? PipeAxisRadiusY { get; set; }
    public string? PipeAxisError { get; set; }
    public double YoloTimeMs { get; set; }
    public double DinoTimeMs { get; set; }
    public double SamTimeMs { get; set; }
    public int DinoDetectionCount { get; set; }
    public int MaskCount { get; set; }
    public bool HasExpectedPrefixHit { get; set; }
    public List<string> ObservedCodes { get; set; } = [];
    public List<DetectionReport> Detections { get; set; } = [];
    public List<MaskReport> Masks { get; set; } = [];
    public string? OverlaySvgPath { get; set; }
}

internal sealed class DetectionReport
{
    public string Label { get; set; } = "";
    public string? Phrase { get; set; }
    public double Confidence { get; set; }
    public string? InferredCode { get; set; }
    public double[] Bbox { get; set; } = [];
}

internal sealed class MaskReport
{
    public string Label { get; set; } = "";
    public double Confidence { get; set; }
    public string? InferredCode { get; set; }
    public double[] Bbox { get; set; } = [];
    public int AreaPixels { get; set; }
    public int WidthPixels { get; set; }
    public int HeightPixels { get; set; }
    public double CentroidX { get; set; }
    public double CentroidY { get; set; }
    public string? ClockPosition { get; set; }
    public int? WidthMm { get; set; }
    public int? HeightMm { get; set; }
    public int? ExtentPercent { get; set; }
    public int? CrossSectionReductionPercent { get; set; }
}

internal sealed class AiAuditSummary
{
    public bool Pass { get; set; }
    public string Message { get; set; } = "";
    public int FramesAnalyzed { get; set; }
    public int FramesWithMasks { get; set; }
    public int FramesWithCodeHits { get; set; }
    public Dictionary<string, bool> RequiredPrefixHits { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static AiAuditSummary Fail(string message) => new() { Pass = false, Message = message };

    public static AiAuditSummary FromFrames(IReadOnlyList<FrameReport> frames)
    {
        var required = new[] { "BCA", "BCC" };
        var requiredHits = required.ToDictionary(
            prefix => prefix,
            prefix => frames.Any(f =>
                f.ExpectedPrefix.Equals(prefix, StringComparison.OrdinalIgnoreCase) &&
                f.HasExpectedPrefixHit),
            StringComparer.OrdinalIgnoreCase);

        var framesWithMasks = frames.Count(f => f.MaskCount > 0);
        var framesWithCodeHits = frames.Count(f => f.HasExpectedPrefixHit);
        var pass = frames.Count > 0 && framesWithMasks > 0 && requiredHits.Values.All(v => v);

        var missing = requiredHits.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList();
        var message = pass
            ? $"PASS: {frames.Count} Frames analysiert, {framesWithMasks} mit SAM-Masken, BCA und BCC getroffen."
            : $"FAIL: {frames.Count} Frames analysiert, {framesWithMasks} mit SAM-Masken, fehlende Code-Treffer: {string.Join(", ", missing)}.";

        return new AiAuditSummary
        {
            Pass = pass,
            Message = message,
            FramesAnalyzed = frames.Count,
            FramesWithMasks = framesWithMasks,
            FramesWithCodeHits = framesWithCodeHits,
            RequiredPrefixHits = requiredHits
        };
    }
}

internal sealed record RingScanParams(
    double CenterX,
    double CenterY,
    double InnerRadius,
    double OuterRadius,
    int NumAngles,
    int NumRadii);

internal static class SvgOverlayWriter
{
    private static readonly string[] Colors =
    [
        "#00e676", "#00b0ff", "#ffea00", "#ff4081", "#7c4dff", "#ff9100"
    ];

    public static async Task WriteAsync(
        string path,
        byte[] pngBytes,
        SamResponse response,
        RingScanParams? scan,
        IReadOnlyList<MaskReport> masks,
        CancellationToken ct)
    {
        var width = response.ImageWidth > 0 ? response.ImageWidth : PngInfo.ReadDimensions(pngBytes).Width;
        var height = response.ImageHeight > 0 ? response.ImageHeight : PngInfo.ReadDimensions(pngBytes).Height;
        var b64 = Convert.ToBase64String(pngBytes);
        var sb = new StringBuilder();

        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        sb.AppendLine($"""  <image href="data:image/png;base64,{b64}" x="0" y="0" width="{width}" height="{height}" preserveAspectRatio="none" />""");
        sb.AppendLine("""  <rect x="0" y="0" width="100%" height="100%" fill="none" stroke="#111" stroke-width="2" />""");

        if (scan is not null)
            AppendScanOverlay(sb, scan);

        for (var i = 0; i < response.Masks.Count; i++)
        {
            var mask = response.Masks[i];
            var color = Colors[i % Colors.Length];
            AppendMaskCells(sb, mask, width, height, color);
            AppendMaskBox(sb, mask, masks.ElementAtOrDefault(i), color, width, height);
        }

        sb.AppendLine("</svg>");
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static void AppendScanOverlay(StringBuilder sb, RingScanParams scan)
    {
        sb.AppendLine($"""  <circle cx="{F(scan.CenterX)}" cy="{F(scan.CenterY)}" r="{F(scan.OuterRadius)}" fill="none" stroke="#ffd166" stroke-width="3" stroke-dasharray="10 7" opacity="0.9" />""");
        sb.AppendLine($"""  <circle cx="{F(scan.CenterX)}" cy="{F(scan.CenterY)}" r="{F(scan.InnerRadius)}" fill="none" stroke="#ffd166" stroke-width="2" stroke-dasharray="6 6" opacity="0.75" />""");
        sb.AppendLine($"""  <circle cx="{F(scan.CenterX)}" cy="{F(scan.CenterY)}" r="5" fill="#ffd166" opacity="0.9" />""");

        for (var a = 0; a < scan.NumAngles; a++)
        {
            var angle = (Math.PI * 2.0 * a) / scan.NumAngles;
            for (var r = 0; r < scan.NumRadii; r++)
            {
                var ratio = scan.NumRadii <= 1 ? 0.5 : (double)r / (scan.NumRadii - 1);
                var radius = scan.InnerRadius + (scan.OuterRadius - scan.InnerRadius) * ratio;
                var x = scan.CenterX + Math.Cos(angle) * radius;
                var y = scan.CenterY + Math.Sin(angle) * radius;
                sb.AppendLine($"""  <circle cx="{F(x)}" cy="{F(y)}" r="3" fill="#ffd166" opacity="0.75" />""");
            }
        }
    }

    private static void AppendMaskCells(StringBuilder sb, SamMaskResult mask, int width, int height, string color)
    {
        var decoded = RleMaskDecoder.Decode(mask.MaskRle, width, height);
        var step = Math.Max(6, Math.Min(width, height) / 96);
        for (var y = 0; y < height; y += step)
        {
            for (var x = 0; x < width; x += step)
            {
                if (!BlockHasMask(decoded, x, y, Math.Min(step, width - x), Math.Min(step, height - y)))
                    continue;

                sb.AppendLine($"""  <rect x="{x}" y="{y}" width="{Math.Min(step, width - x)}" height="{Math.Min(step, height - y)}" fill="{color}" opacity="0.26" />""");
            }
        }
    }

    private static bool BlockHasMask(bool[,] mask, int x, int y, int w, int h)
    {
        var height = mask.GetLength(0);
        var width = mask.GetLength(1);
        var sample = Math.Max(1, Math.Min(w, h) / 3);

        for (var yy = y; yy < y + h && yy < height; yy += sample)
        {
            for (var xx = x; xx < x + w && xx < width; xx += sample)
            {
                if (mask[yy, xx])
                    return true;
            }
        }

        return false;
    }

    private static void AppendMaskBox(StringBuilder sb, SamMaskResult mask, MaskReport? report, string color, int imageWidth, int imageHeight)
    {
        if (mask.Bbox.Count < 4)
            return;

        var x1 = ToPixel(mask.Bbox[0], imageWidth);
        var y1 = ToPixel(mask.Bbox[1], imageHeight);
        var x2 = ToPixel(mask.Bbox[2], imageWidth);
        var y2 = ToPixel(mask.Bbox[3], imageHeight);
        var label = WebUtility.HtmlEncode($"{report?.InferredCode ?? "?"} {mask.Label} {mask.Confidence:P0}");
        var textY = Math.Max(18, y1 - 6);

        sb.AppendLine($"""  <rect x="{F(x1)}" y="{F(y1)}" width="{F(Math.Max(1, x2 - x1))}" height="{F(Math.Max(1, y2 - y1))}" fill="none" stroke="{color}" stroke-width="4" />""");
        sb.AppendLine($"""  <rect x="{F(x1)}" y="{F(textY - 16)}" width="{Math.Max(110, label.Length * 8)}" height="20" fill="#101418" opacity="0.82" />""");
        sb.AppendLine($"""  <text x="{F(x1 + 4)}" y="{F(textY)}" fill="#fff" font-family="Segoe UI, Arial, sans-serif" font-size="14">{label}</text>""");
    }

    private static double ToPixel(double value, int size)
        => value is >= 0 and <= 1.5 ? value * size : value;

    private static string F(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal static class RleMaskDecoder
{
    public static bool[,] Decode(string rle, int width, int height)
    {
        var mask = new bool[height, width];
        if (string.IsNullOrWhiteSpace(rle) || width <= 0 || height <= 0)
            return mask;

        var parts = rle.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var startValue))
            return mask;

        var current = startValue != 0;
        var pos = 0;
        var total = width * height;

        for (var i = 1; i < parts.Length && pos < total; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var runLength) || runLength <= 0)
            {
                current = !current;
                continue;
            }

            var end = Math.Min(pos + runLength, total);
            if (current)
            {
                for (var p = pos; p < end; p++)
                    mask[p / width, p % width] = true;
            }

            pos = end;
            current = !current;
        }

        return mask;
    }
}

internal static class HtmlReportWriter
{
    public static string Render(AiAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine("<title>SewerStudio KI TestRunner Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;background:#f5f7f8;color:#172026}h1{font-size:22px;margin:0 0 12px}.summary{padding:12px 14px;border-radius:6px;background:#fff;border-left:6px solid #d33;margin-bottom:18px}.summary.pass{border-color:#16833a}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:16px}.card{background:#fff;border:1px solid #d9e1e5;border-radius:6px;padding:12px}.meta{font-size:13px;color:#52616b;line-height:1.5}.bad{color:#b00020}.ok{color:#146c2e}img{width:100%;height:auto;border:1px solid #ccd6dd;background:#000}.codes{font-family:Consolas,monospace;font-size:13px}.small{font-size:12px;color:#65737d}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>SewerStudio KI TestRunner Report</h1>");
        sb.AppendLine($"""<div class="summary {(report.Summary.Pass ? "pass" : "")}"><strong>{Esc(report.Summary.Message)}</strong><br><span class="meta">Sidecar: {(report.SidecarReachable ? "erreichbar" : "nicht erreichbar")} {Esc(report.SidecarHealthStatus ?? report.SidecarHealthError ?? "")}</span></div>""");
        sb.AppendLine($"""<p class="meta">Input: {Esc(report.InputPath)}<br>Output: {Esc(report.OutputDirectory)}<br>GPU: {Esc(report.SidecarGpuModel ?? "-")}<br>YOLO: {Esc(report.SidecarYoloModel ?? "-")}</p>""");
        sb.AppendLine("<div class=\"grid\">");

        foreach (var frame in report.Frames)
        {
            sb.AppendLine("<section class=\"card\">");
            sb.AppendLine($"""<h2 style="font-size:16px;margin:0 0 8px">{Esc(Path.GetFileName(frame.FramePath))}</h2>""");
            sb.AppendLine($"""<div class="meta">Soll: <strong>{Esc(frame.ExpectedCode)}</strong> bei {frame.Meter?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-"} m<br>Treffer: <strong class="{(frame.HasExpectedPrefixHit ? "ok" : "bad")}">{(frame.HasExpectedPrefixHit ? "ja" : "nein")}</strong> | Masken: {frame.MaskCount} | DINO: {frame.DinoDetectionCount} | View: {Esc(frame.ViewType ?? "-")}</div>""");
            sb.AppendLine($"""<div class="codes">Codes: {Esc(frame.ObservedCodes.Count == 0 ? "-" : string.Join(", ", frame.ObservedCodes))}</div>""");
            if (!string.IsNullOrWhiteSpace(frame.Error))
                sb.AppendLine($"""<p class="bad small">{Esc(frame.Error)}</p>""");

            if (!string.IsNullOrWhiteSpace(frame.OverlaySvgPath))
            {
                var rel = Path.GetRelativePath(report.OutputDirectory, frame.OverlaySvgPath).Replace('\\', '/');
                sb.AppendLine($"""<img src="{Esc(rel)}" alt="Overlay fuer {Esc(Path.GetFileName(frame.FramePath))}">""");
            }

            if (frame.Masks.Count > 0)
            {
                sb.AppendLine("<p class=\"small\">Masken: ");
                sb.AppendLine(Esc(string.Join(" | ", frame.Masks.Select(m =>
                    $"{m.InferredCode ?? "?"} {m.Label} conf={m.Confidence:0.00} area={m.AreaPixels} clock={m.ClockPosition ?? "-"}"))));
                sb.AppendLine("</p>");
            }

            sb.AppendLine("</section>");
        }

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string Esc(string value) => WebUtility.HtmlEncode(value);
}
