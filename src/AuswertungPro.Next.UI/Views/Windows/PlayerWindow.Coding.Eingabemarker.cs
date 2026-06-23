using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void Eingabemarker_Click(object sender, RoutedEventArgs e)
    {
        if (BtnEingabemarker.IsChecked == true)
        {
            _player.SetPause(true);
            _eingabemarkerPhase = EingabemarkerPhase.Drawing;
            EnsureMarkOverlayReady();
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Cross;
            SetCodingAiState("Eingabemarker: Rechteck um die Beobachtung ziehen",
                PlayerStatusColors.Info, "Klicken + Ziehen = Bereich markieren");
        }
        else
        {
            CancelEingabemarker();
        }
    }

    private void CancelEingabemarker()
    {
        _eingabemarkerPhase = EingabemarkerPhase.Inactive;
        BtnEingabemarker.IsChecked = false;
        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        if (_eingabemarkerPreviewRect != null)
        {
            CodingOverlayCanvas.Children.Remove(_eingabemarkerPreviewRect);
            _eingabemarkerPreviewRect = null;
        }
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
    }

    private void EingabemarkerCanvas_MouseDown(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;

        _eingabemarkerDragStart = canvasPos;
        CodingOverlayCanvas.CaptureMouse();

        _eingabemarkerPreviewRect = new System.Windows.Shapes.Rectangle
        {
            Stroke = Brushes.Lime,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0))
        };
        Canvas.SetLeft(_eingabemarkerPreviewRect, canvasPos.X);
        Canvas.SetTop(_eingabemarkerPreviewRect, canvasPos.Y);
        _eingabemarkerPreviewRect.Width = 0;
        _eingabemarkerPreviewRect.Height = 0;
        CodingOverlayCanvas.Children.Add(_eingabemarkerPreviewRect);
    }

    private void EingabemarkerCanvas_MouseMove(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing || _eingabemarkerPreviewRect == null) return;

        var previewRect = CodingEingabemarkerGeometryPolicy.BuildPreviewRect(
            _eingabemarkerDragStart,
            canvasPos);

        Canvas.SetLeft(_eingabemarkerPreviewRect, previewRect.X);
        Canvas.SetTop(_eingabemarkerPreviewRect, previewRect.Y);
        _eingabemarkerPreviewRect.Width = previewRect.Width;
        _eingabemarkerPreviewRect.Height = previewRect.Height;
    }

    private void EingabemarkerCanvas_MouseUp(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;
        CodingOverlayCanvas.ReleaseMouseCapture();

        var normalizedRect = CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection(
            _eingabemarkerDragStart,
            canvasPos,
            new Size(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight));
        if (normalizedRect is null) { CancelEingabemarker(); return; }

        _eingabemarkerRectNorm = normalizedRect.Value;
        _eingabemarkerPhase = EingabemarkerPhase.Input;
        CodingOverlayCanvas.IsHitTestVisible = false;
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;

        EingabemarkerPopup.Visibility = Visibility.Visible;
        TxtEingabemarker.Text = "";
        CmbEingabemarker.SelectedIndex = -1;
        Dispatcher.BeginInvoke(new Action(() => TxtEingabemarker.Focus()),
            System.Windows.Threading.DispatcherPriority.Input);

        SetCodingAiState("Beschreibung eingeben oder Stichwort wählen, dann Enter",
            PlayerStatusColors.Info, "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"");
    }

}
