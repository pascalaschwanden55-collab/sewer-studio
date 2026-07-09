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

public sealed class CategoryBars : Grid
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(CategoryBars),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public static readonly DependencyProperty BarCommandProperty =
        DependencyProperty.Register(
            nameof(BarCommand),
            typeof(ICommand),
            typeof(CategoryBars),
            new FrameworkPropertyMetadata(null, OnChartChanged));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(CategoryBars),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, OnChartChanged));

    public static readonly DependencyProperty ValuePathProperty =
        DependencyProperty.Register(
            nameof(ValuePath),
            typeof(string),
            typeof(CategoryBars),
            new FrameworkPropertyMetadata("Percent", FrameworkPropertyMetadataOptions.AffectsMeasure, OnChartChanged));

    private INotifyCollectionChanged? _observableItems;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? BarCommand
    {
        get => (ICommand?)GetValue(BarCommandProperty);
        set => SetValue(BarCommandProperty, value);
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public string ValuePath
    {
        get => (string)GetValue(ValuePathProperty);
        set => SetValue(ValuePathProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bars = (CategoryBars)d;
        if (bars._observableItems is not null)
            bars._observableItems.CollectionChanged -= bars.ItemsCollectionChanged;

        bars._observableItems = e.NewValue as INotifyCollectionChanged;
        if (bars._observableItems is not null)
            bars._observableItems.CollectionChanged += bars.ItemsCollectionChanged;

        bars.Rebuild();
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CategoryBars)d).Rebuild();

    private void ItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Rebuild();

    private void Rebuild()
    {
        Children.Clear();
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();

        var items = ReadItems(ItemsSource, ValuePath).ToList();
        if (items.Count == 0)
            return;

        if (Orientation == Orientation.Vertical)
            BuildVertical(items);
        else
            BuildHorizontal(items);
    }

    private void BuildHorizontal(IReadOnlyList<BarItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = CreateHorizontalRow(items[i], i);
            SetRow(row, i);
            Children.Add(row);
        }
    }

    private FrameworkElement CreateHorizontalRow(BarItem item, int index)
    {
        var row = new Grid
        {
            Margin = new Thickness(0, 3, 0, 3),
            MinHeight = 24,
            Background = Brushes.Transparent,
            Cursor = BarCommand is null ? Cursors.Arrow : Cursors.Hand,
            ToolTip = BuildToolTip(item)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

        var label = new TextBlock
        {
            Text = item.Label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12
        };
        row.Children.Add(label);

        var track = new Grid { Height = 14, VerticalAlignment = VerticalAlignment.Center };
        var valueStar = Math.Max(0.001d, item.NormalizedValue);
        var restStar = Math.Max(0.001d, 100d - item.NormalizedValue);
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(valueStar, GridUnitType.Star) });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(restStar, GridUnitType.Star) });
        track.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(230, 234, 239)),
            CornerRadius = new CornerRadius(3)
        });
        Grid.SetColumnSpan(track.Children[0], 2);

        var bar = new Rectangle
        {
            Fill = ResolveBrush(item.Key, index),
            RadiusX = 3,
            RadiusY = 3,
            MinWidth = item.NormalizedValue > 0d ? 2d : 0d
        };
        track.Children.Add(bar);
        Grid.SetColumn(track, 1);
        row.Children.Add(track);

        var value = new TextBlock
        {
            Text = item.ValueText,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            FontSize = 12
        };
        Grid.SetColumn(value, 2);
        row.Children.Add(value);

        AttachCommand(row, item);
        return row;
    }

    private void BuildVertical(IReadOnlyList<BarItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 34 });
            var column = CreateVerticalColumn(items[i], i);
            SetColumn(column, i);
            Children.Add(column);
        }
    }

    private FrameworkElement CreateVerticalColumn(BarItem item, int index)
    {
        var column = new Grid
        {
            Margin = new Thickness(4, 0, 4, 0),
            MinHeight = 120,
            Background = Brushes.Transparent,
            Cursor = BarCommand is null ? Cursors.Arrow : Cursors.Hand,
            ToolTip = BuildToolTip(item)
        };
        column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Max(0.001d, 100d - item.NormalizedValue), GridUnitType.Star) });
        column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Max(0.001d, item.NormalizedValue), GridUnitType.Star) });
        column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bar = new Rectangle
        {
            Fill = ResolveBrush(item.Key, index),
            RadiusX = 3,
            RadiusY = 3,
            MinHeight = item.NormalizedValue > 0d ? 2d : 0d,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        SetRow(bar, 1);
        column.Children.Add(bar);

        var label = new TextBlock
        {
            Text = item.Label,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 11,
            Margin = new Thickness(0, 5, 0, 0)
        };
        SetRow(label, 2);
        column.Children.Add(label);

        AttachCommand(column, item);
        return column;
    }

    private void AttachCommand(FrameworkElement element, BarItem item)
    {
        if (BarCommand is null)
            return;

        element.MouseLeftButtonUp += (_, e) =>
        {
            if (BarCommand.CanExecute(item.Key))
                BarCommand.Execute(item.Key);
            e.Handled = true;
        };
    }

    private static string BuildToolTip(BarItem item)
        => $"{item.Label}: {item.ValueText} ({item.Count})";

    private static IReadOnlyList<BarItem> ReadItems(IEnumerable? source, string? valuePath)
    {
        if (source is null)
            return Array.Empty<BarItem>();

        var rawItems = new List<(object Source, string Key, string Label, int Count, double Percent, double Value)>();
        foreach (var raw in source)
        {
            if (raw is null)
                continue;

            var key = ReadString(raw, "Key");
            var label = ReadString(raw, "Label");
            var count = ReadInt(raw, "Count");
            var percent = ReadDouble(raw, "Percent");
            var value = ReadDouble(raw, string.IsNullOrWhiteSpace(valuePath) ? "Percent" : valuePath);
            if (string.IsNullOrWhiteSpace(key))
                key = label;
            rawItems.Add((raw, key, string.IsNullOrWhiteSpace(label) ? key : label, count, percent, value));
        }

        var max = rawItems.Count == 0 ? 0d : rawItems.Max(i => i.Value);
        var usesPercent = string.Equals(valuePath, "Percent", StringComparison.OrdinalIgnoreCase);
        return rawItems
            .Select(i =>
            {
                var normalized = usesPercent
                    ? Math.Clamp(i.Value, 0d, 100d)
                    : max <= 0d ? 0d : Math.Clamp(i.Value * 100d / max, 0d, 100d);
                return new BarItem(
                    i.Key,
                    i.Label,
                    i.Count,
                    i.Percent,
                    i.Value,
                    normalized,
                    FormatValue(i.Source, valuePath, i.Value, usesPercent));
            })
            .ToList();
    }

    private static string FormatValue(object source, string? valuePath, double value, bool usesPercent)
    {
        if (usesPercent)
            return $"{value:N1}%";

        var raw = ReadProperty(source, valuePath ?? string.Empty);
        return raw switch
        {
            decimal decimalValue => $"{decimalValue:N0}",
            double doubleValue => $"{doubleValue:N0}",
            float floatValue => $"{floatValue:N0}",
            int intValue => intValue.ToString("N0", CultureInfo.CurrentCulture),
            long longValue => longValue.ToString("N0", CultureInfo.CurrentCulture),
            _ => value.ToString("N0", CultureInfo.CurrentCulture)
        };
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
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return null;

        return source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(source);
    }

    private sealed record BarItem(
        string Key,
        string Label,
        int Count,
        double Percent,
        double Value,
        double NormalizedValue,
        string ValueText);
}
