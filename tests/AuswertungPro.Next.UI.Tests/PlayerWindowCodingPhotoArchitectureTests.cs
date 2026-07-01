using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingPhotoArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_snapshot_target_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var captureServicePath = Path.Combine(uiRoot, "Ai", "CodingSnapshotFileCaptureService.cs");
        var captureServicesPath = Path.Combine(uiRoot, "Ai", "CodingPhotoCaptureServices.cs");
        var captureServicesOwnerPath = Path.Combine(uiRoot, "Player", "CodingPhotoCaptureServicesOwner.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotTargetPolicy.cs");

        Assert.True(File.Exists(policyPath), "Snapshot-Zielpfad fuer Coding-Fotos muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicePath), "Snapshot-Datei-Capture und Warten muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(captureServicesPath), "Snapshot-Service-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicesOwnerPath), "Snapshot-Service-Besitz soll ausserhalb der PlayerWindow-Partials liegen.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.Exists(capturePath) ? File.ReadAllText(capturePath) : string.Empty;
        var captureService = File.ReadAllText(captureServicePath);
        var captureServices = File.Exists(captureServicesPath) ? File.ReadAllText(captureServicesPath) : string.Empty;
        var captureServicesOwner = File.Exists(captureServicesOwnerPath) ? File.ReadAllText(captureServicesOwnerPath) : string.Empty;
        var policy = File.ReadAllText(policyPath);
        var photoText = photos + capture;

        Assert.Contains("CodingSnapshotTargetPolicy.Build", photoText);
        Assert.DoesNotContain("CodingSnapshotFileCaptureServiceFactory.Create", capture);
        Assert.Contains("CodingSnapshotFileCaptureServiceFactory.Create", captureServices);
        Assert.Contains("CodingPhotoCaptureServices", captureServicesOwner);
        Assert.Contains("_codingPhotoCaptureServicesOwner.SnapshotFileCaptureService", capture);
        Assert.DoesNotContain("new CodingPhotoCaptureServices()", capture);
        Assert.DoesNotContain("private CodingPhotoCaptureServices? _codingPhotoCaptureServices", capture);
        Assert.DoesNotContain("??= new CodingPhotoCaptureServices", capture);
        Assert.DoesNotContain("new CodingSnapshotFileCaptureService", capture);
        Assert.DoesNotContain("Path.GetDirectoryName(_videoPath)", photoText);
        Assert.DoesNotContain("DateTimeOffset.Now.ToString(\"HHmmss\")", photoText);
        Assert.DoesNotContain("Directory.CreateDirectory", capture);
        Assert.DoesNotContain("Thread.Sleep", capture);
        Assert.DoesNotContain("new FileInfo", capture);
        Assert.Contains("Directory.CreateDirectory", captureService);
        Assert.Contains("Thread.Sleep", captureService);
        Assert.Contains("public static CodingSnapshotTarget Build", policy);
        Assert.Contains("Path.Combine(videoDir, \"Fotos\")", policy);
    }

    [Fact]
    public void PlayerWindow_coding_photo_capture_lives_in_capture_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var photosPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.cs");
        var capturePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.Capture.cs");

        Assert.True(File.Exists(capturePath), "Foto-Capture und Frame-Extraktion sollen aus dem Foto-Orchestrator heraus.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.ReadAllText(capturePath);

        Assert.DoesNotContain("private byte[]? TryExtractAnalyzedFrameBytes", photos);
        Assert.DoesNotContain("private byte[]? TryExtractFrameAtSeconds", photos);
        Assert.DoesNotContain("private TimeSpan? GetCurrentPlayerTimestamp", photos);
        Assert.DoesNotContain("private string? CodingCaptureSnapshot", photos);
        Assert.Contains("private byte[]? TryExtractAnalyzedFrameBytes", capture);
        Assert.Contains("private byte[]? TryExtractFrameAtSeconds", capture);
        Assert.Contains("private TimeSpan? GetCurrentPlayerTimestamp", capture);
        Assert.Contains("private string? CodingCaptureSnapshot", capture);
        Assert.Contains("CodingFrameExtractionService", capture);
        Assert.Contains("CodingSnapshotTargetPolicy.Build", capture);
    }

    [Fact]
    public void PlayerWindow_frame_extraction_lives_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingFrameExtractionService.cs");
        var captureServicesPath = Path.Combine(uiRoot, "Ai", "CodingPhotoCaptureServices.cs");

        Assert.True(File.Exists(servicePath), "ffmpeg-Frame-Extraktion soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(captureServicesPath), "Frame-Extraction-Service-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");

        var capture = File.ReadAllText(capturePath);
        var service = File.ReadAllText(servicePath);
        var captureServices = File.Exists(captureServicesPath) ? File.ReadAllText(captureServicesPath) : string.Empty;

        Assert.DoesNotContain("CodingFrameExtractionServiceFactory.Create", capture);
        Assert.Contains("CodingFrameExtractionServiceFactory.Create", captureServices);
        Assert.DoesNotContain("new CodingFrameExtractionService", capture);
        Assert.DoesNotContain("FfmpegLocator.ResolveFfmpeg", capture);
        Assert.DoesNotContain("VideoFrameExtractor.TryExtractFramePngAsync", capture);
        Assert.DoesNotContain(".GetAwaiter().GetResult()", capture);
        Assert.Contains("FfmpegLocator.ResolveFfmpeg", service);
        Assert.Contains("VideoFrameExtractor.TryExtractFramePngAsync", service);
    }
}
