using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Controls;

public sealed class DonutChart : Canvas
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(DonutChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public static readonly DependencyProperty SegmentCommandProperty =
        DependencyProperty.Register(
            nameof(SegmentCommand),
            typeof(ICommand),
            typeof(DonutChart),
            new FrameworkPropertyMetadata(null, OnChartChanged));

    public static readonly DependencyProperty CenterTextProperty =
        DependencyProperty.Register(
            nameof(CenterText),
            typeof(string),
            typeof(DonutChart),
            new FrameworkPropertyMetadata(string.Empty, OnChartChanged));

    public static readonly DependencyProperty CenterLabelProperty =
        DependencyProperty.Register(
            nameof(CenterLabel),
            typeof(string),
            typeof(DonutChart),
            new FrameworkPropertyMetadata(string.Empty, OnChartChanged));

    public static readonly DependencyProperty AnimateBuildProperty =
        DependencyProperty.Register(
            nameof(AnimateBuild),
            typeof(bool),
            typeof(DonutChart),
            new FrameworkPropertyMetadata(true));

    // Interner Aufbau-Fortschritt 0..1: Segmente sweepen bis BuildProgress * 360 Grad.
    private static readonly DependencyProperty BuildProgressProperty =
        DependencyProperty.Register(
            "BuildProgress",
            typeof(double),
            typeof(DonutChart),
            new FrameworkPropertyMetadata(1d, (d, _) => ((DonutChart)d).Rebuild()));

    private INotifyCollectionChanged? _observableItems;

    public DonutChart()
    {
        SizeChanged += (_, _) => Rebuild();
        Loaded += (_, _) => StartBuildAnimation();
        MinWidth = 120;
        MinHeight = 120;
    }

    public bool AnimateBuild
    {
        get => (bool)GetValue(AnimateBuildProperty);
        set => SetValue(AnimateBuildProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? SegmentCommand
    {
        get => (ICommand?)GetValue(SegmentCommandProperty);
        set => SetValue(SegmentCommandProperty, value);
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string CenterLabel
    {
        get => (string)GetValue(CenterLabelProperty);
        set => SetValue(CenterLabelProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (DonutChart)d;
        if (chart._observableItems is not null)
            chart._observableItems.CollectionChanged -= chart.ItemsCollectionChanged;

        chart._observableItems = e.NewValue as INotifyCollectionChanged;
        if (chart._observableItems is not null)
            chart._observableItems.CollectionChanged += chart.ItemsCollectionChanged;

        chart.StartBuildAnimation();
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DonutChart)d).Rebuild();

    private void ItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => StartBuildAnimation();

    /// <summary>Sweep-Aufbau starten (neue Daten/erstes Anzeigen); ohne Animation direkt voll zeichnen.</summary>
    private void StartBuildAnimation()
    {
        if (!AnimateBuild || !IsLoaded)
        {
            BeginAnimation(BuildProgressProperty, null);
            SetValue(BuildProgressProperty, 1d);
            Rebuild(); // Explizit: SetValue feuert kein Rebuild, wenn der Wert schon 1.0 war.
            return;
        }

        var sweep = new System.Windows.Media.Animation.DoubleAnimation(
            0d, 1d, AnimationTokens.Slow)
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        };
        sweep.Completed += (_, _) =>
        {
            // Clock freigeben und Endzustand festschreiben.
            BeginAnimation(BuildProgressProperty, null);
            SetValue(BuildProgressProperty, 1d);
        };
        BeginAnimation(BuildProgressProperty, sweep);
    }

    private void Rebuild()
    {
        Children.Clear();

        var items = ReadItems(ItemsSource).Where(i => i.Value > 0d).ToList();
        var total = items.Sum(i => i.Value);
        if (total <= 0d)
        {
            var emptySize = DrawEmptyRing();
            DrawCenterText(emptySize);
            return;
        }

        var size = Math.Min(ActualWidth, ActualHeight);
        if (double.IsNaN(size) || size <= 0d)
            size = Math.Min(Width, Height);
        if (double.IsNaN(size) || size <= 0d)
            size = 140d;

        var center = new Point(size / 2d, size / 2d);
        var outerRadius = Math.Max(20d, size / 2d - 3d);
        var innerRadius = Math.Max(10d, outerRadius * 0.58d);
        var startAngle = -90d;

        // Sweep-Aufbau: Segmente nur bis zum animierten Winkel-Limit zeichnen.
        var progress = Math.Clamp((double)GetValue(BuildProgressProperty), 0d, 1d);
        var angleLimit = -90d + progress * 360d;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var sweep = item.Value / total * 360d;
            if (sweep >= 359.99d)
            {
                AddSegmentClipped(item, i, center, outerRadius, innerRadius, startAngle, 180d, angleLimit);
                AddSegmentClipped(item, i, center, outerRadius, innerRadius, startAngle + 180d, 180d, angleLimit);
            }
            else
            {
                AddSegmentClipped(item, i, center, outerRadius, innerRadius, startAngle, sweep, angleLimit);
            }

            startAngle += sweep;
        }

        DrawCenterText(size);
    }

    private double DrawEmptyRing()
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (double.IsNaN(size) || size <= 0d)
            size = 140d;

        var ellipse = new Ellipse
        {
            Width = Math.Max(20d, size - 6d),
            Height = Math.Max(20d, size - 6d),
            Stroke = ResolveThemeBrush("BorderLightBrush", Color.FromRgb(218, 223, 230)),
            StrokeThickness = Math.Max(8d, size * 0.16d),
            Fill = Brushes.Transparent
        };
        SetLeft(ellipse, 3d);
        SetTop(ellipse, 3d);
        Children.Add(ellipse);
        return size;
    }

    // Kappt den Sweep am Aufbau-Limit (Sweep-Animation); volle 1.0 zeichnet unveraendert.
    private void AddSegmentClipped(ChartItem item, int index, Point center, double outerRadius, double innerRadius, double startAngle, double sweepAngle, double angleLimit)
    {
        var allowed = angleLimit - startAngle;
        if (allowed <= 0d)
            return;

        AddSegment(item, index, center, outerRadius, innerRadius, startAngle, Math.Min(sweepAngle, allowed));
    }

    private void AddSegment(ChartItem item, int index, Point center, double outerRadius, double innerRadius, double startAngle, double sweepAngle)
    {
        if (sweepAngle <= 0d)
            return;

        var endAngle = startAngle + sweepAngle;
        var outerStart = PointOnCircle(center, outerRadius, startAngle);
        var outerEnd = PointOnCircle(center, outerRadius, endAngle);
        var innerEnd = PointOnCircle(center, innerRadius, endAngle);
        var innerStart = PointOnCircle(center, innerRadius, startAngle);
        var largeArc = sweepAngle > 180d;

        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerRadius, outerRadius), 0d, largeArc, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerEnd, true));
        figure.Segments.Add(new ArcSegment(innerStart, new Size(innerRadius, innerRadius), 0d, largeArc, SweepDirection.Counterclockwise, true));

        var path = new Path
        {
            Data = new PathGeometry([figure]),
            Fill = ResolveBrush(item.Key, index),
            Stroke = ResolveThemeBrush("CardBrush", Colors.White),
            StrokeThickness = 1.2d,
            Tag = item.Key,
            ToolTip = $"{item.Label}: {item.Count} ({item.Percent:N1}%)",
            Cursor = SegmentCommand is null ? Cursors.Arrow : Cursors.Hand
        };

        if (SegmentCommand is not null)
        {
            path.MouseEnter += (_, _) => path.Opacity = 0.8d;
            path.MouseLeave += (_, _) => path.Opacity = 1d;
            path.MouseLeftButtonUp += (_, e) =>
            {
                if (SegmentCommand.CanExecute(item.Key))
                    SegmentCommand.Execute(item.Key);
                e.Handled = true;
            };
        }

        Children.Add(path);
    }

    private void DrawCenterText(double size)
    {
        if (string.IsNullOrWhiteSpace(CenterText) && string.IsNullOrWhiteSpace(CenterLabel))
            return;

        var panel = new StackPanel
        {
            Width = size,
            IsHitTestVisible = false
        };

        if (!string.IsNullOrWhiteSpace(CenterText))
        {
            panel.Children.Add(new TextBlock
            {
                Text = CenterText,
                FontSize = Math.Max(18d, size * 0.17d),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = ResolveThemeBrush("TextBrush", Color.FromRgb(24, 31, 42))
            });
        }

        if (!string.IsNullOrWhiteSpace(CenterLabel))
        {
            panel.Children.Add(new TextBlock
            {
                Text = CenterLabel,
                FontSize = Math.Max(10d, size * 0.075d),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = ResolveThemeBrush("MutedBrush", Color.FromRgb(102, 112, 128))
            });
        }

        panel.Measure(new Size(size, size));
        SetLeft(panel, 0d);
        SetTop(panel, Math.Max(0d, (size - panel.DesiredSize.Height) / 2d));
        Panel.SetZIndex(panel, 10);
        Children.Add(panel);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(
            center.X + radius * Math.Cos(radians),
            center.Y + radius * Math.Sin(radians));
    }

    private static IReadOnlyList<ChartItem> ReadItems(IEnumerable? source)
    {
        if (source is null)
            return Array.Empty<ChartItem>();

        var result = new List<ChartItem>();
        foreach (var raw in source)
        {
            if (raw is null)
                continue;

            var key = ReadString(raw, "Key");
            var label = ReadString(raw, "Label");
            var count = ReadInt(raw, "Count");
            var percent = ReadDouble(raw, "Percent");
            var value = count > 0 ? count : percent;
            if (string.IsNullOrWhiteSpace(key))
                key = label;

            result.Add(new ChartItem(key, string.IsNullOrWhiteSpace(label) ? key : label, count, percent, value));
        }

        return result;
    }

    private static Brush ResolveBrush(string key, int index)
    {
        if (string.Equals(key, "ohne", StringComparison.OrdinalIgnoreCase))
            return FrozenBrush(142, 150, 162);

        var stateBrush = ZustandsklasseColorPalette.TryGetBackground(key);
        if (stateBrush is not null)
            return stateBrush;

        var palette = FallbackPalette;
        return palette[index % palette.Length];
    }

    private static SolidColorBrush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private Brush ResolveThemeBrush(string key, Color fallback)
        => TryFindResource(key) as Brush ?? FrozenBrush(fallback.R, fallback.G, fallback.B);

    private static readonly Brush[] FallbackPalette =
    [
        FrozenBrush(46, 134, 193),
        FrozenBrush(124, 179, 66),
        FrozenBrush(239, 108, 0),
        FrozenBrush(126, 87, 194),
        FrozenBrush(0, 137, 123),
        FrozenBrush(198, 40, 40)
    ];

    private static string ReadString(object source, string propertyName)
        => ReadProperty(source, propertyName)?.ToString() ?? string.Empty;

    private static int ReadInt(object source, string propertyName)
        => Convert.ToInt32(ReadDouble(source, propertyName), CultureInfo.InvariantCulture);

    private static double ReadDouble(object source, string propertyName)
    {
        var value = ReadProperty(source, propertyName);
        if (value is null)
            return 0d;

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0d;
        }
    }

    private static object? ReadProperty(object source, string propertyName)
        => source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(source);

    private sealed record ChartItem(string Key, string Label, int Count, double Percent, double Value);
}
