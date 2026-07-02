using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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

        Assert.DoesNotContain("private Task<byte[]?> TryExtractAnalyzedFrameBytesAsync", photos);
        Assert.DoesNotContain("private Task<byte[]?> TryExtractFrameAtSecondsAsync", photos);
        Assert.DoesNotContain("private TimeSpan? GetCurrentPlayerTimestamp", photos);
        Assert.DoesNotContain("private string? CodingCaptureSnapshot", photos);
        Assert.Contains("private Task<byte[]?> TryExtractAnalyzedFrameBytesAsync", capture);
        Assert.DoesNotContain("private byte[]? TryExtractAnalyzedFrameBytes", capture);
        Assert.Contains("private Task<byte[]?> TryExtractFrameAtSecondsAsync", capture);
        Assert.DoesNotContain("private byte[]? TryExtractFrameAtSeconds", capture);
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

    [Fact]
    public void PlayerWindow_photo_display_paths_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Viewer.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPhotoDisplayPathPolicy.cs");
        var loaderPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerImageSourceLoader.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerCommandWorkflow.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerDisplayWorkflow.cs");
        var viewerWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWorkflowService.cs");
        var viewerWorkflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWorkflowServiceFactory.cs");
        var viewerServicePath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWindowService.cs");
        var viewerServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWindowServiceFactory.cs");

        Assert.True(File.Exists(policyPath), "Fotoanzeige-Pfadauswahl muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(loaderPath), "Fotoanzeige-Bildquellen sollen ausserhalb der PlayerWindow-Partials geladen werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Fotoanzeige-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "Fotoanzeige-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerWorkflowPath), "Fotoanzeige-Workflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerWorkflowFactoryPath), "Fotoanzeige-Workflow soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(viewerServicePath), "Fotoanzeige-Fensteraufbau soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerServiceFactoryPath), "Fotoanzeige-Fensteraufbau soll ueber Factory verdrahtet werden.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);
        var loader = File.ReadAllText(loaderPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var viewerWorkflow = File.ReadAllText(viewerWorkflowPath);
        var viewerWorkflowFactory = File.ReadAllText(viewerWorkflowFactoryPath);
        var viewerService = File.ReadAllText(viewerServicePath);
        var viewerServiceFactory = File.ReadAllText(viewerServiceFactoryPath);

        Assert.Contains("CodingPhotoViewerCommandWorkflow.Execute", photos);
        Assert.Contains("CodingPhotoViewerDisplayWorkflow.Show", photos);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create", photos);
        Assert.DoesNotContain("new CodingPhotoViewerDisplayWorkflowActions", photos);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create().Show", photos);
        Assert.DoesNotContain("CodingPhotoViewerWindowServiceFactory.Create", photos);
        Assert.DoesNotContain("CodingPhotoViewerImageSourceLoader.Load", photos);
        Assert.DoesNotContain("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", photos);
        Assert.DoesNotContain("CodingPhotoDisplayPathPolicy.ResolveExistingPath", photos);
        Assert.DoesNotContain("File.Exists", photos);
        Assert.DoesNotContain("BitmapImage", photos);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveOrEmpty", photos);
        Assert.DoesNotContain("Path.GetDirectoryName(_serviceProvider!.Settings.LastProjectPath)", photos);
        Assert.DoesNotContain("var displayPhotoPaths = new List<string>", photos);
        Assert.DoesNotContain("displayPhotoPaths.Contains(fotoPath", photos);
        Assert.Contains("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", loader);
        Assert.Contains("CodingPhotoDisplayPathPolicy.ResolveExistingPath", loader);
        Assert.Contains("File.Exists", loader);
        Assert.Contains("BitmapImage", loader);
        Assert.Contains("request.SelectedItem is not CodingEvent", commandWorkflow);
        Assert.Contains("codingEvent.Entry.FotoPaths.Count == 0", commandWorkflow);
        Assert.Contains("actions.ShowNoPhotosOverlay()", commandWorkflow);
        Assert.Contains("actions.ShowViewer(codingEvent)", commandWorkflow);
        Assert.Contains("CodingPhotoViewerWorkflowServiceFactory.Create", displayWorkflow);
        Assert.Contains("new CodingPhotoViewerDisplayWorkflowActions", displayWorkflow);
        Assert.Contains("service.Show(owner, codingEvent, lastProjectPath)", displayWorkflow);
        Assert.Contains("CodingProjectFolderResolver.ResolveOrEmpty", viewerWorkflowFactory);
        Assert.Contains("CodingPhotoViewerWindowServiceFactory.Create", viewerWorkflowFactory);
        Assert.Contains("Show", viewerWorkflow);
        Assert.Contains("CodingPhotoViewerImageSourceLoader.Load", viewerService);
        Assert.Contains("WindowStateManager.Track", viewerService);
        Assert.Contains("new CodingPhotoViewerWindowService", viewerServiceFactory);
        Assert.Contains("public static IReadOnlyList<string> BuildDisplayPhotoPaths", policy);
        Assert.Contains("public static string? ResolveExistingPath", policy);
    }

    [Fact]
    public void PlayerWindow_photo_viewer_lives_in_viewer_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var photosPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.cs");
        var viewerPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.Viewer.cs");

        Assert.True(File.Exists(viewerPath), "Foto-Anzeigefenster soll aus dem Snapshot-Partial heraus.");

        var photos = File.ReadAllText(photosPath);
        var viewer = File.ReadAllText(viewerPath);

        Assert.DoesNotContain("private void CodingEventShowPhotos_Click", photos);
        Assert.Contains("private void CodingEventShowPhotos_Click", viewer);
        Assert.Contains("CodingPhotoViewerCommandWorkflow.Execute", viewer);
        Assert.Contains("CodingPhotoViewerDisplayWorkflow.Show", viewer);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create", viewer);
        Assert.DoesNotContain("new CodingPhotoViewerDisplayWorkflowActions", viewer);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create().Show", viewer);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is not CodingEvent", viewer);
        Assert.DoesNotContain("FotoPaths.Count == 0", viewer);
        Assert.DoesNotContain("new Window", viewer);
        Assert.DoesNotContain("new StackPanel", viewer);
        Assert.DoesNotContain("new Image", viewer);
        Assert.DoesNotContain("new ScrollViewer", viewer);
        Assert.DoesNotContain("WindowStateManager.Track", viewer);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveOrEmpty", viewer);
    }

    [Fact]
    public void PlayerWindow_manual_photo_slot_logic_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPhotoSlotPolicy.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingEventPhotoApplier.cs");
        var timestampScopePath = Path.Combine(uiRoot, "Ai", "CodingEventPhotoTimestampScope.cs");
        var pathAppenderPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryPhotoPathAppender.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTakePhotoCommandWorkflow.cs");
        var attachmentWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFramePhotoAttachmentWorkflow.cs");
        var framePhotoAttacherPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFramePhotoAttacher.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Foto-Slot-Regel muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Manuelle Foto-Slot-Anwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(timestampScopePath), "Manuelle Foto-Zeitsetzung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pathAppenderPath), "FotoPath-Anhaengen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Manueller Foto-Command soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(attachmentWorkflowPath), "Analysierter Frame vs. Snapshot-Fallback soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(framePhotoAttacherPath), "Konkreter KI-Frame-Foto-Service soll hinter einem kleinen Adapter liegen.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);
        var applier = File.ReadAllText(applierPath);
        var timestampScope = File.Exists(timestampScopePath) ? File.ReadAllText(timestampScopePath) : "";
        var pathAppender = File.Exists(pathAppenderPath) ? File.ReadAllText(pathAppenderPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var attachmentWorkflow = File.Exists(attachmentWorkflowPath) ? File.ReadAllText(attachmentWorkflowPath) : "";
        var framePhotoAttacher = File.Exists(framePhotoAttacherPath) ? File.ReadAllText(framePhotoAttacherPath) : "";

        Assert.Contains("CodingTakePhotoCommandWorkflow.Execute", photos);
        Assert.Contains("CodingEventPhotoApplier.Apply", photos);
        Assert.Contains("CodingEventPhotoTimestampScope.Apply", photos);
        Assert.Contains("CodingAnalyzedFramePhotoAttachmentWorkflow.Execute", photos);
        Assert.Contains("CodingAnalyzedFramePhotoAttacher.Attach", photos);
        Assert.DoesNotContain("CodingAiFramePhotoService.AttachAnalyzedFramePhoto", photos);
        Assert.DoesNotContain("TryExtractAnalyzedFrameBytes() ?? _detectionConfirmationBuffer.FrameBytes", photos);
        Assert.DoesNotContain("if (!string.IsNullOrWhiteSpace(path))", photos);
        Assert.DoesNotContain("var fallback = CodingCaptureSnapshot(entry)", photos);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank", photos);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is not CodingEvent", photos);
        Assert.DoesNotContain("if (fotoPath == null)", photos);
        Assert.DoesNotContain("Foto konnte nicht aufgenommen werden", photos);
        Assert.DoesNotContain("CodingPhotoSlotPolicy.Apply", photos);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", photos);
        Assert.DoesNotContain("codingEvent.VideoTimestamp = photoTime.Value", photos);
        Assert.DoesNotContain("FotoPaths.Add", photos);
        Assert.DoesNotContain("entry.FotoPaths[1] = fotoPath", photos);
        Assert.DoesNotContain("Foto 2 ersetzt", photos);
        Assert.Contains("public static CodingPhotoSlotUpdate Apply", policy);
        Assert.Contains("photoPaths.Count >= 2", policy);
        Assert.Contains("CodingPhotoSlotPolicy.Apply", applier);
        Assert.Contains("codingSessionService?.UpdateEvent", applier);
        Assert.Contains("RestoreOriginalTime", timestampScope);
        Assert.Contains("AddDistinctNonBlank", pathAppender);
        Assert.Contains("selectedItem is not CodingEvent codingEvent", commandWorkflow);
        Assert.Contains("actions.CaptureSnapshot(entry)", commandWorkflow);
        Assert.Contains("restoreOriginalTime()", commandWorkflow);
        Assert.Contains("actions.RefreshCodingEventsList()", commandWorkflow);
        Assert.Contains("actions.GetPreferredFrameBytes() ?? actions.GetBufferedFrameBytes()", attachmentWorkflow);
        Assert.Contains("actions.AttachAnalyzedFramePhoto(frameBytes)", attachmentWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank", attachmentWorkflow);
        Assert.Contains("CodingAiFramePhotoService.AttachAnalyzedFramePhoto", framePhotoAttacher);
    }

    [Fact]
    public void PlayerWindow_analyzed_frame_timestamp_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFrameTimestampPolicy.cs");

        Assert.True(File.Exists(policyPath), "Analysierter-Frame-Zeitpunkt muss ausserhalb der PlayerWindow-Partials entschieden werden.");

        var capture = File.ReadAllText(capturePath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingAnalyzedFrameTimestampPolicy.Resolve", capture);
        Assert.DoesNotContain("sec.Value < clean", capture);
        Assert.Contains("public static double? Resolve", policy);
        Assert.Contains("pendingTimestampSeconds.Value < firstCleanFrameSeconds.Value", policy);
    }
}
