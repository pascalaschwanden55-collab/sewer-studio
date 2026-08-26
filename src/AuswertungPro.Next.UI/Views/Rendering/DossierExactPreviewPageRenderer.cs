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
        IReadOnlyDictionary<int, IReadOnlyList<DossierPreviewTarget>> hits,
        DossierPreviewTarget? planTarget = null)
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

        var areas = DossierOutputPreviewHitAreaBuilder.Build(page, hits);
        foreach (var group in areas
                     .GroupBy(area => new AreaKey(
                         area.Left,
                         area.Bottom,
                         area.Right,
                         area.Top))
                     // Grosse Zeilenflaechen zuerst, kleine Zellen danach. In
                     // WPF liegt das zuletzt eingefuegte Element oben.
                     .OrderByDescending(group =>
                         (group.Key.Right - group.Key.Left)
                         * (group.Key.Top - group.Key.Bottom)))
        {
            var bounds = group.Key;
            var border = new Border
            {
                Width = Math.Max(3, (bounds.Right - bounds.Left) * PointsToPixels),
                Height = Math.Max(3, (bounds.Top - bounds.Bottom) * PointsToPixels),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = new DossierPreviewFrameOrigin(Brushes.Transparent, new Thickness(0))
            };
            Canvas.SetLeft(border, bounds.Left * PointsToPixels);
            Canvas.SetTop(border, (page.Height - bounds.Top) * PointsToPixels);
            overlay.Children.Add(border);

            foreach (var target in group.Select(area => area.Target).Distinct())
                Remember(frames, target, border);
        }

        if (planTarget is { } plan && !page.IsAttachment)
            AddPlanButton(overlay, frames, page, plan);

        root.Children.Add(overlay);
        return new DossierPreviewRenderResult
        {
            Root = root,
            Overlay = overlay,
            Frames = frames.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<Border>)pair.Value)
        };
    }

    private static void AddPlanButton(
        Canvas overlay,
        IDictionary<DossierPreviewTarget, List<Border>> frames,
        DossierOutputPreviewPage page,
        DossierPreviewTarget target)
    {
        const double badgeWidth = 190;
        const double badgeHeight = 36;

        var heading = page.Words.FirstOrDefault(word =>
            word.Text.Contains("bersichtsplan", StringComparison.OrdinalIgnoreCase));
        var left = heading is null
            ? Math.Max(20, (overlay.Width - badgeWidth) / 2)
            : Math.Clamp(
                heading.Left * PointsToPixels,
                20,
                Math.Max(20, overlay.Width - badgeWidth - 20));
        var top = heading is null
            ? 56
            : Math.Clamp(
                (page.Height - heading.Bottom) * PointsToPixels + 14,
                20,
                Math.Max(20, overlay.Height - badgeHeight - 20));

        // Der aeussere Rahmen ist die Klick- und Blinkflaeche. Der sichtbare
        // Badge liegt darin als Kind und bleibt deshalb auch sichtbar, wenn
        // die allgemeine Hervorhebung den aeusseren Hintergrund zuruecksetzt.
        var clickArea = new Border
        {
            Width = badgeWidth,
            Height = badgeHeight,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "Werkleitungsplan wählen, drehen oder zuschneiden",
            Tag = new DossierPreviewFrameOrigin(Brushes.Transparent, new Thickness(0)),
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xED, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x73, 0xA5)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "📷",
                            FontFamily = new FontFamily("Segoe UI Emoji"),
                            FontSize = 16,
                            Margin = new Thickness(0, 0, 8, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Plan einfügen / bearbeiten",
                            FontFamily = new FontFamily("Arial"),
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x20, 0x3E, 0x64)),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            }
        };

        Canvas.SetLeft(clickArea, left);
        Canvas.SetTop(clickArea, top);
        overlay.Children.Add(clickArea);
        Remember(frames, target, clickArea);
    }

    private static void Remember(
        IDictionary<DossierPreviewTarget, List<Border>> frames,
        DossierPreviewTarget target,
        Border border)
    {
        if (!frames.TryGetValue(target, out var targetFrames))
            frames[target] = targetFrames = [];

        targetFrames.Add(border);
    }

    private readonly record struct AreaKey(
        double Left,
        double Bottom,
        double Right,
        double Top);
}
