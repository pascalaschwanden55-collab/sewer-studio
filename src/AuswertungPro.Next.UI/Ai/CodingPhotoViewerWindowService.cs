using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingPhotoViewerWindowService
{
    private readonly Func<CodingEvent, string, IReadOnlyList<ImageSource>> _loadSources;
    private readonly Action<Window> _trackWindow;
    private readonly Action<Window> _showWindow;

    public CodingPhotoViewerWindowService()
        : this(
            (codingEvent, projectFolder) => CodingPhotoViewerImageSourceLoader.Load(codingEvent, projectFolder),
            WindowStateManager.Track,
            window => window.Show())
    {
    }

    public CodingPhotoViewerWindowService(
        Func<CodingEvent, string, IReadOnlyList<ImageSource>> loadSources,
        Action<Window> trackWindow,
        Action<Window> showWindow)
    {
        _loadSources = loadSources ?? throw new ArgumentNullException(nameof(loadSources));
        _trackWindow = trackWindow ?? throw new ArgumentNullException(nameof(trackWindow));
        _showWindow = showWindow ?? throw new ArgumentNullException(nameof(showWindow));
    }

    public void Show(Window owner, CodingEvent codingEvent, string projectFolder)
    {
        var win = CreateWindow(owner, codingEvent, projectFolder);
        _trackWindow(win);
        _showWindow(win);
    }

    public static string BuildTitle(CodingEvent codingEvent)
        => $"Fotos - {codingEvent.Entry.Code} @ {codingEvent.MeterAtCapture:F2}m";

    private Window CreateWindow(Window owner, CodingEvent codingEvent, string projectFolder)
    {
        var win = new Window
        {
            Title = BuildTitle(codingEvent),
            Width = 640,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        foreach (var source in _loadSources(codingEvent, projectFolder))
        {
            var img = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(4),
                MaxHeight = 360
            };
            panel.Children.Add(img);
        }

        win.Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        return win;
    }
}
