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

    private void CmbEingabemarker_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelEingabemarker();
            ClearDetectionOverlays();
            return;
        }

        if (e.Key != System.Windows.Input.Key.Enter) return;
        SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker");
    }

    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EingabemarkerPopup.Visibility != Visibility.Visible) return;
        if (CmbEingabemarker.SelectedItem is ComboBoxItem item && item.Content is string text && !string.IsNullOrEmpty(text))
        {
            TxtEingabemarker.Text = text;
            SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker");
        }
    }

    private static string? ResolveEingabemarkerCodeHint(string? keyword)
        => AuswertungPro.Next.UI.Player.PlayerVsaCodeHintResolver.ResolveKeyword(keyword);

    private async Task SubmitEingabemarker()
    {
        var keyword = TxtEingabemarker.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(keyword)) return;

        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        _eingabemarkerPhase = EingabemarkerPhase.Analyzing;

        var codeHint = ResolveEingabemarkerCodeHint(keyword);

        try
        {
            if (_codingVm != null && codeHint != null)
            {
                var checkMeter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                var existingDup = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
                    _codingVm.Events,
                    codeHint,
                    checkMeter);
                if (existingDup != null)
                {
                    SetCodingAiState(
                        $"{codeHint} bereits vorhanden bei {existingDup.MeterAtCapture:F2}m - Duplikat",
                        PlayerStatusColors.Warning, "");
                    return;
                }
            }

            if (codeHint != null && _codingVm != null && _codingSessionService != null)
            {
                var meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
                var label = LookupVsaLabel(codeHint) ?? keyword;

                var draft = CodingEingabemarkerEventFactory.CreateAccepted(
                    codeHint,
                    label,
                    keyword,
                    meter,
                    videoTime);

                var fotoPath = CodingCaptureSnapshot(draft.Entry);
                if (fotoPath != null) draft.Entry.FotoPaths.Add(fotoPath);

                var ev = _codingSessionService.AddEvent(draft.Entry, _codingVm.CurrentOverlay);
                ev.AiContext = draft.AiContext;
                RefreshCodingEventsList();
                UpdateToolBadge();
                PersistSingleEventAsTrainingSample(ev).SafeFireAndForget("TrainingSaveSingle");
                SetCodingAiState($"{codeHint} {label} bei {meter:F2}m eingetragen",
                    PlayerStatusColors.Success, "");
            }
            else
            {
                SetCodingAiState($"KI analysiert: \"{keyword}\" ...",
                    PlayerStatusColors.Warning, "Qwen analysiert");
                await RunCodingAnalysisAsync(
                    $"Eingabemarker: {keyword}",
                    disableAnalyzeButton: true,
                    keywordHint: keyword,
                    codeHint: null);
            }
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", PlayerStatusColors.Error, "");
        }
        finally
        {
            CancelEingabemarker();
        }
    }
}
