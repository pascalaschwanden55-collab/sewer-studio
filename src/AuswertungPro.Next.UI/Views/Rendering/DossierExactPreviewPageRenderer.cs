using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.UI.Views.Rendering;

/// <summary>Legt nur unsichtbare Klickflächen über das echte PDF-Seitenbild.</summary>
public static class DossierExactPreviewPageRenderer
{
    private const double PointsToPixels = 96d / 72d;

    public static Border CreateNotice(string text, bool pageSized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new Border
        {
            Width = pageSized ? 794 : double.NaN,
            Height = pageSized ? 1123 : double.NaN,
            Padding = pageSized ? new Thickness(50) : new Thickness(12),
            CornerRadius = pageSized ? new CornerRadius(0) : new CornerRadius(8),
            Background = Brushes.White,
            BorderBrush = pageSized
                ? Brushes.Transparent
                : new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
            BorderThickness = pageSized ? new Thickness(0) : new Thickness(1),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Arial"),
                FontSize = pageSized ? 15 : 11,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = pageSized
                    ? HorizontalAlignment.Center
                    : HorizontalAlignment.Stretch,
                VerticalAlignment = pageSized
                    ? VerticalAlignment.Center
                    : VerticalAlignment.Top
            }
        };
    }

    public static DossierPreviewRenderResult Render(
        BitmapSource bitmap,
        DossierOutputPreviewPage page,
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(hits);

        var width = page.Width * PointsToPixels;
        var height = page.Height * PointsToPixels;
        var root = new Grid
        {
            Width = width,
            Height = height,
            Background = Brushes.White
        };
        root.Children.Add(new Image
        {
            Source = bitmap,
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        });

        var overlay = new Canvas { Width = width, Height = height };
        var frames = new Dictionary<DossierPreviewTarget, List<Border>>();

        foreach (var (wordIndex, targets) in hits)
        {
            if (wordIndex < 0 || wordIndex >= page.Words.Count || targets.Count == 0)
                continue;

            var word = page.Words[wordIndex];
            var border = new Border
            {
                Width = Math.Max(3, (word.Right - word.Left) * PointsToPixels + 4),
                Height = Math.Max(3, (word.Top - word.Bottom) * PointsToPixels + 4),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = new DossierPreviewFrameOrigin(Brushes.Transparent, new Thickness(0))
            };
            Canvas.SetLeft(border, word.Left * PointsToPixels - 2);
            Canvas.SetTop(border, (page.Height - word.Top) * PointsToPixels - 2);
            overlay.Children.Add(border);

            foreach (var target in targets.Distinct())
            {
                if (!frames.TryGetValue(target, out var targetFrames))
                    frames[target] = targetFrames = new List<Border>();
                targetFrames.Add(border);
            }
        }

        root.Children.Add(overlay);
        return new DossierPreviewRenderResult
        {
            Root = root,
            Frames = frames.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<Border>)pair.Value)
        };
    }
}
