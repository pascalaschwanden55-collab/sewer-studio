using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingPhotoArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_snapshot_target_lives_in_policy()
    {
        var photosPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var capturePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var captureServicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSnapshotFileCaptureService.cs");
        var captureServicesPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoCaptureServices.cs");
        var captureServicesOwnerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingPhotoCaptureServicesOwner.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSnapshotTargetPolicy.cs");

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
        Assert.Contains("CodingSnapshotFileCaptureServiceFactory.Create", captureServices);
        Assert.Contains("CodingPhotoCaptureServices", captureServicesOwner);
        Assert.Contains("_codingPhotoCaptureServicesOwner.SnapshotFileCaptureService", capture);
        Assert.Contains("Directory.CreateDirectory", captureService);
        Assert.Contains("Thread.Sleep", captureService);
        Assert.Contains("public static CodingSnapshotTarget Build", policy);
        Assert.Contains("Path.Combine(videoDir, \"Fotos\")", policy);
    }

    [Fact]
    public void PlayerWindow_coding_photo_capture_lives_in_capture_partial()
    {
        var photosPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var capturePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");

        Assert.True(File.Exists(capturePath), "Foto-Capture und Frame-Extraktion sollen aus dem Foto-Orchestrator heraus.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.ReadAllText(capturePath);

        Assert.Contains("private Task<byte[]?> TryExtractAnalyzedFrameBytesAsync", capture);
        Assert.Contains("private Task<byte[]?> TryExtractFrameAtSecondsAsync", capture);
        Assert.Contains("private TimeSpan? GetCurrentPlayerTimestamp", capture);
        Assert.Contains("private string? CodingCaptureSnapshot", capture);
        Assert.Contains("CodingFrameExtractionService", capture);
        Assert.Contains("CodingSnapshotTargetPolicy.Build", capture);
    }

    [Fact]
    public void PlayerWindow_frame_extraction_lives_in_service()
    {
        var capturePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var servicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingFrameExtractionService.cs");
        var captureServicesPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoCaptureServices.cs");

        Assert.True(File.Exists(servicePath), "ffmpeg-Frame-Extraktion soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(captureServicesPath), "Frame-Extraction-Service-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");

        var capture = File.ReadAllText(capturePath);
        var service = File.ReadAllText(servicePath);
        var captureServices = File.Exists(captureServicesPath) ? File.ReadAllText(captureServicesPath) : string.Empty;

        Assert.Contains("CodingFrameExtractionServiceFactory.Create", captureServices);
        Assert.Contains("FfmpegLocator.ResolveFfmpeg", service);
        Assert.Contains("VideoFrameExtractor.TryExtractFramePngAsync", service);

        var offenders = FindFileTokenOffenders(
            capturePath,
            ".GetAwaiter().GetResult()");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Coding-Foto-Capture soll Frame-Extraktion nicht synchron blockierend abwarten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_photo_display_paths_live_in_policy()
    {
        var photosPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.Viewer.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoDisplayPathPolicy.cs");
        var loaderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoViewerImageSourceLoader.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoViewerCommandWorkflow.cs");
        var displayWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoViewerDisplayWorkflow.cs");
        var viewerWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoViewerWorkflowService.cs");
        var viewerWorkflowFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoViewerWorkflowServiceFactory.cs");
        var viewerServicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoViewerWindowService.cs");
        var viewerServiceFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoViewerWindowServiceFactory.cs");

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
        var photosPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var viewerPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.Viewer.cs");

        Assert.True(File.Exists(viewerPath), "Foto-Anzeigefenster soll aus dem Snapshot-Partial heraus.");

        var photos = File.ReadAllText(photosPath);
        var viewer = File.ReadAllText(viewerPath);

        Assert.Contains("private void CodingEventShowPhotos_Click", viewer);
        Assert.Contains("CodingPhotoViewerCommandWorkflow.Execute", viewer);
        Assert.Contains("CodingPhotoViewerDisplayWorkflow.Show", viewer);
    }

    [Fact]
    public void PlayerWindow_manual_photo_slot_logic_lives_in_policy()
    {
        var photosPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPhotoSlotPolicy.cs");
        var applierPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingEventPhotoApplier.cs");
        var timestampScopePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingEventPhotoTimestampScope.cs");
        var pathAppenderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolEntryPhotoPathAppender.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTakePhotoCommandWorkflow.cs");
        var attachmentWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAnalyzedFramePhotoAttachmentWorkflow.cs");
        var framePhotoAttacherPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAnalyzedFramePhotoAttacher.cs");

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
        Assert.Contains("CodingAnalyzedFramePhotoAttachmentWorkflow.ExecuteAsync", photos);
        Assert.Contains("CodingAnalyzedFramePhotoAttacher.AttachWithStore", photos);
        Assert.Contains("_protocolContext.CodingFramePhotos", photos);
        Assert.DoesNotContain("CodingAiFramePhotoService.AttachAnalyzedFramePhoto", photos);
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
        Assert.Contains("actions.GetPreferredFrameBytesAsync()", attachmentWorkflow);
        Assert.Contains("actions.AttachAnalyzedFramePhoto(frameBytes)", attachmentWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank", attachmentWorkflow);
        Assert.Contains("ICodingFramePhotoStore framePhotoStore", framePhotoAttacher);
        Assert.Contains("framePhotoStore.AttachAnalyzedFramePhoto", framePhotoAttacher);
    }

}
