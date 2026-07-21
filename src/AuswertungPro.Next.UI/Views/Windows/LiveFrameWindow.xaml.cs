using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class LiveFrameWindow : Window
{
    private readonly List<LiveFrameFinding> _findings = new();

    public LiveFrameWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        OverlayCanvas.SizeChanged += (_, _) => RenderOverlay();
    }

    public void UpdateFrame(ImageSource? image, IReadOnlyList<LiveFrameFinding>? findings,
        string? status, string? info, string? quantSummary)
    {
        LiveImage.Source = image;
        PlaceholderText.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;

        _findings.Clear();
        if (findings is not null)
            _findings.AddRange(findings.Take(8));

        StatusText.Text = status ?? "";
        if (DataContext is VideoAnalysisPipelineViewModel vm)
        {
            // use binding fallback
        }

        QuantText.Text = quantSummary ?? "";
        RenderOverlay();
    }

    public void UpdateInfo(string? info)
    {
        // Update via header binding if DataContext is set
    }

    private void RenderOverlay()
    {
        var width = OverlayCanvas.ActualWidth;
        var height = OverlayCanvas.ActualHeight;
        if (width < 60 || height < 60)
            return;

        OverlayCanvas.Children.Clear();
        if (LiveImage.Source is null)
            return;

        LiveFrameRingOverlayRenderer.Draw(
            OverlayCanvas,
            _findings,
            LiveFrameRingOverlayMode.Detail,
            width,
            height);
    }
}
