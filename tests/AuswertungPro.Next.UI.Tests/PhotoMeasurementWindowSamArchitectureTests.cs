using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoMeasurementWindowSamArchitectureTests
{
    [Fact]
    public void Photo_assistant_shows_true_sam_mask_without_burning_it_into_export_photo()
    {
        var root = FindRepositoryRoot();
        var windowRoot = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows");
        var xaml = File.ReadAllText(
            Path.Combine(windowRoot, "PhotoMeasurementWindow.xaml"));
        var rootCode = File.ReadAllText(
            Path.Combine(windowRoot, "PhotoMeasurementWindow.xaml.cs"));
        var samCode = File.ReadAllText(
            Path.Combine(windowRoot, "PhotoMeasurementWindow.Sam.cs"));

        var maskCanvas = xaml.IndexOf(
            "x:Name=\"SamMaskCanvas\"",
            StringComparison.Ordinal);
        var toolCanvas = xaml.IndexOf(
            "x:Name=\"OverlayCanvas\"",
            StringComparison.Ordinal);

        Assert.True(maskCanvas >= 0);
        Assert.True(toolCanvas > maskCanvas);
        Assert.Contains("IsHitTestVisible=\"False\"", xaml);
        Assert.Contains("SegmentPhotoMarkAsync(geometry)", rootCode);
        Assert.Contains("CanCompletePhotoAnnotation()", rootCode);
        Assert.Contains("new PhotoAnnotationSegmentRequest(", samCode);
        Assert.Contains("_photoPath", samCode);
        Assert.Contains("TrainingStudioMaskOverlayRenderer.Render(", samCode);
        Assert.Contains("BtnOk.IsEnabled = false", samCode);
        Assert.Contains("AnnotationDraft = result.Draft", samCode);

        // Der bestehende Export bekommt weiterhin nur Werkzeug-Canvas und Originalfoto.
        Assert.Contains("_overlayExporter.Export(", rootCode);
        Assert.Contains("OverlayCanvas,", rootCode);
        Assert.DoesNotContain("SamMaskCanvas,", rootCode);
    }

    [Fact]
    public void Vsa_confirmation_saves_pending_original_annotation_through_guarded_workbench()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var window = File.ReadAllText(Path.Combine(
            uiRoot,
            "Views",
            "Windows",
            "VsaCodeExplorerWindow.xaml.cs"));
        var dialogFactory = File.ReadAllText(Path.Combine(
            uiRoot,
            "Services",
            "VsaCodeExplorerDialogServiceFactory.cs"));

        Assert.Contains("_vm.OriginalFotoPaths", window);
        Assert.DoesNotContain("pending.Item.FramePath", window);
        Assert.Contains("win.AnnotationDraft", window);
        Assert.Contains("_pendingPhotoAnnotations[photoIndex]", window);
        Assert.Contains("DialogHost.Current.Confirm(", window);
        Assert.Contains("new PhotoAnnotationBatchSaveRequest(", window);
        Assert.Contains("await PhotoAnnotationBatchSaveUseCase.ExecuteAsync(", window);
        Assert.Contains("_vm.BuildProtocolEntryPreview()", window);
        Assert.Contains("Environment.UserName", window);
        Assert.Contains("if (_photoAnnotationSaveInProgress)", window);
        Assert.Contains("RootContent.IsEnabled = false", window);
        Assert.Contains("BtnCancel.IsEnabled = false", window);
        Assert.Contains("MarkPhotoAnnotationHandled(", window);
        Assert.Contains("TrainingStudioWindowDependencyFactory.Create(services)", dialogFactory);
        Assert.Contains("(workbench as IDisposable)?.Dispose()", dialogFactory);
    }
}
