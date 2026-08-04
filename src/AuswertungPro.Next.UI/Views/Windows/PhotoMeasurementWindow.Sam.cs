using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Asynchrone SAM-Vorschau des Foto-Assistenten. Die eigene Canvas-Ebene
/// verhindert, dass die Vorschau in das spaetere Trainings-Original einbrennt.
/// </summary>
public partial class PhotoMeasurementWindow
{
    private readonly IPhotoAnnotationUseCase? _photoAnnotationUseCase;
    private readonly PhotoAnnotationCaptureContext? _photoAnnotationContext;
    private CancellationTokenSource? _photoAnnotationCts;
    private WorkbenchSegmentation? _visiblePhotoSegmentation;
    private long _photoAnnotationGeneration;

    public PhotoAnnotationDraft? AnnotationDraft { get; private set; }

    private async Task SegmentPhotoMarkAsync(OverlayGeometry geometry)
    {
        ResetPhotoAnnotation();
        if (_photoAnnotationUseCase is null || _photoAnnotationContext is null)
        {
            // Allgemeine Foto-Dialoge ohne echten Codier-/Haltungskontext
            // behalten ihr bisheriges reines Markierungsverhalten.
            BtnOk.IsEnabled = true;
            return;
        }

        var generation = Interlocked.Increment(ref _photoAnnotationGeneration);
        var cts = new CancellationTokenSource();
        _photoAnnotationCts = cts;
        BtnOk.IsEnabled = false;
        TxtStatus.Text = "SAM segmentiert den markierten Bereich ...";

        PhotoAnnotationSegmentResult result;
        try
        {
            result = await _photoAnnotationUseCase.SegmentAsync(
                new PhotoAnnotationSegmentRequest(
                    _photoPath,
                    _photoAnnotationContext,
                    geometry),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested
            || generation != _photoAnnotationGeneration
            || !ReferenceEquals(_currentGeometry, geometry))
        {
            return;
        }

        if (!result.Success || result.Draft is null)
        {
            geometry.SamMask = null;
            TxtStatus.Text = result.Message;
            BtnOk.IsEnabled = false;
            return;
        }

        AnnotationDraft = result.Draft;
        _visiblePhotoSegmentation = result.Draft.Segmentation;
        var rendered = RenderPhotoSegmentation();
        if (!rendered.Rendered)
        {
            AnnotationDraft = null;
            _visiblePhotoSegmentation = null;
            geometry.SamMask = null;
            TxtStatus.Text = rendered.ErrorMessage
                             ?? "Die SAM-Maske kann nicht sichtbar geprueft werden.";
            BtnOk.IsEnabled = false;
            return;
        }

        BtnOk.IsEnabled = true;
        TxtStatus.Text =
            "SAM-Maske ist sichtbar. OK = Maske fuer diese Beobachtung uebernehmen.";
    }

    private bool CanCompletePhotoAnnotation()
    {
        if (_activeTool != PhotoTool.MarkRect
            || _photoAnnotationUseCase is null
            || _photoAnnotationContext is null)
        {
            return true;
        }

        if (AnnotationDraft is not null)
            return true;

        TxtStatus.Text =
            "Bitte warten, bis die SAM-Maske sichtbar ist, oder die Box neu ziehen.";
        return false;
    }

    private void ResetPhotoAnnotation()
    {
        Interlocked.Increment(ref _photoAnnotationGeneration);
        CancelPhotoAnnotationWork();

        AnnotationDraft = null;
        _visiblePhotoSegmentation = null;
        SamMaskCanvas.Children.Clear();
        if (_currentGeometry is not null)
            _currentGeometry.SamMask = null;
    }

    private void CancelPhotoAnnotationWork()
    {
        var cts = Interlocked.Exchange(ref _photoAnnotationCts, null);
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private TrainingStudioMaskOverlayRenderer.RenderResult RenderPhotoSegmentation()
    {
        SamMaskCanvas.Children.Clear();
        if (_visiblePhotoSegmentation is null)
            return new TrainingStudioMaskOverlayRenderer.RenderResult(false, null);

        return TrainingStudioMaskOverlayRenderer.Render(
            SamMaskCanvas,
            _visiblePhotoSegmentation,
            GetImageRenderedRect(PhotoImage));
    }

    private void RenderPhotoMarkRectangle(OverlayGeometry geometry)
    {
        ClearByTag(TagOverlay);
        if (geometry.Points.Count < 3)
            return;

        var p1 = NormToCanvas(geometry.Points[0].X, geometry.Points[0].Y);
        var p2 = NormToCanvas(geometry.Points[2].X, geometry.Points[2].Y);
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = Math.Max(0, p2.X - p1.X),
            Height = Math.Max(0, p2.Y - p1.Y),
            Stroke = Brushes.LimeGreen,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
            Tag = TagOverlay
        };
        Canvas.SetLeft(rect, p1.X);
        Canvas.SetTop(rect, p1.Y);
        OverlayCanvas.Children.Add(rect);
    }

    private void RenderPhotoAnnotationAfterResize()
    {
        if (_activeTool != PhotoTool.MarkRect || _currentGeometry is null)
            return;

        if (_visiblePhotoSegmentation is not null)
            _ = RenderPhotoSegmentation();
        RenderPhotoMarkRectangle(_currentGeometry);
    }
}
