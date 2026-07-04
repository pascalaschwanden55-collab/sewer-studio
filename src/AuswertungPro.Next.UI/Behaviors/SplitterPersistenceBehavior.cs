using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Merkt sich die Groesse eines per <see cref="GridSplitter"/> verstellbaren Panels pro Ansicht
/// (Dim2 Layout). Generalisiert das Muster aus HaltungsansichtView (DragCompleted -&gt; clamp -&gt; Save).
/// Aktivierung rein per XAML am GridSplitter:
///   behaviors:SplitterPersistenceBehavior.IsEnabled="True"
///   behaviors:SplitterPersistenceBehavior.SplitterKey="Stats"
///   behaviors:SplitterPersistenceBehavior.TargetColumnIndex="2"
/// Der ViewKey kommt vererbt vom Seiten-/Fenster-Root.
/// </summary>
public static class SplitterPersistenceBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(SplitterPersistenceBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty SplitterKeyProperty =
        DependencyProperty.RegisterAttached("SplitterKey", typeof(string), typeof(SplitterPersistenceBehavior), new PropertyMetadata("Splitter"));
    public static void SetSplitterKey(DependencyObject e, string v) => e.SetValue(SplitterKeyProperty, v);
    public static string GetSplitterKey(DependencyObject e) => (string?)e.GetValue(SplitterKeyProperty) ?? "Splitter";

    /// <summary>Index der zu persistierenden Spalte im Eltern-Grid (-1 = keine).</summary>
    public static readonly DependencyProperty TargetColumnIndexProperty =
        DependencyProperty.RegisterAttached("TargetColumnIndex", typeof(int), typeof(SplitterPersistenceBehavior), new PropertyMetadata(-1));
    public static void SetTargetColumnIndex(DependencyObject e, int v) => e.SetValue(TargetColumnIndexProperty, v);
    public static int GetTargetColumnIndex(DependencyObject e) => (int)e.GetValue(TargetColumnIndexProperty);

    /// <summary>Index der zu persistierenden Zeile im Eltern-Grid (-1 = keine).</summary>
    public static readonly DependencyProperty TargetRowIndexProperty =
        DependencyProperty.RegisterAttached("TargetRowIndex", typeof(int), typeof(SplitterPersistenceBehavior), new PropertyMetadata(-1));
    public static void SetTargetRowIndex(DependencyObject e, int v) => e.SetValue(TargetRowIndexProperty, v);
    public static int GetTargetRowIndex(DependencyObject e) => (int)e.GetValue(TargetRowIndexProperty);

    public static readonly DependencyProperty MinSizeProperty =
        DependencyProperty.RegisterAttached("MinSize", typeof(double), typeof(SplitterPersistenceBehavior), new PropertyMetadata(120d));
    public static void SetMinSize(DependencyObject e, double v) => e.SetValue(MinSizeProperty, v);
    public static double GetMinSize(DependencyObject e) => (double)e.GetValue(MinSizeProperty);

    public static readonly DependencyProperty MaxSizeProperty =
        DependencyProperty.RegisterAttached("MaxSize", typeof(double), typeof(SplitterPersistenceBehavior), new PropertyMetadata(1200d));
    public static void SetMaxSize(DependencyObject e, double v) => e.SetValue(MaxSizeProperty, v);
    public static double GetMaxSize(DependencyObject e) => (double)e.GetValue(MaxSizeProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GridSplitter splitter)
            return;

        if (e.NewValue is true)
        {
            splitter.Loaded += OnLoaded;
            splitter.DragCompleted += OnDragCompleted;
        }
        else
        {
            splitter.Loaded -= OnLoaded;
            splitter.DragCompleted -= OnDragCompleted;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not GridSplitter splitter || splitter.Parent is not Grid grid)
            return;

        var viewKey = ViewPersonalization.GetViewKey(splitter);
        if (string.IsNullOrWhiteSpace(viewKey))
            return;

        if (!SplitterPersistenceCore.TryGetStored(viewKey!, GetSplitterKey(splitter), out var size))
            return;

        var clamped = Math.Clamp(size, GetMinSize(splitter), GetMaxSize(splitter));
        ApplyToTarget(grid, splitter, clamped);
    }

    private static void OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is not GridSplitter splitter || splitter.Parent is not Grid grid)
            return;

        var viewKey = ViewPersonalization.GetViewKey(splitter);
        if (string.IsNullOrWhiteSpace(viewKey))
            return;

        var actual = ReadTargetActual(grid, splitter);
        if (actual <= 0)
            return;

        SplitterPersistenceCore.Persist(viewKey!, GetSplitterKey(splitter), actual, GetMinSize(splitter), GetMaxSize(splitter));
    }

    private static double ReadTargetActual(Grid grid, GridSplitter splitter)
    {
        var col = GetTargetColumnIndex(splitter);
        if (col >= 0 && col < grid.ColumnDefinitions.Count)
            return grid.ColumnDefinitions[col].ActualWidth;

        var row = GetTargetRowIndex(splitter);
        if (row >= 0 && row < grid.RowDefinitions.Count)
            return grid.RowDefinitions[row].ActualHeight;

        return 0;
    }

    private static void ApplyToTarget(Grid grid, GridSplitter splitter, double size)
    {
        var col = GetTargetColumnIndex(splitter);
        if (col >= 0 && col < grid.ColumnDefinitions.Count)
        {
            grid.ColumnDefinitions[col].Width = new GridLength(size, GridUnitType.Pixel);
            return;
        }

        var row = GetTargetRowIndex(splitter);
        if (row >= 0 && row < grid.RowDefinitions.Count)
            grid.RowDefinitions[row].Height = new GridLength(size, GridUnitType.Pixel);
    }
}

/// <summary>Reine, testbare Kernlogik der Splitter-Persistenz.</summary>
internal static class SplitterPersistenceCore
{
    public static void Persist(string viewKey, string splitterKey, double actualSize, double min, double max)
    {
        var view = ViewCustomizationStore.GetOrCreate(viewKey);
        view.SplitterSizes ??= new();
        view.SplitterSizes[splitterKey] = Math.Clamp(actualSize, min, max);
        ViewCustomizationStore.Save();
    }

    public static bool TryGetStored(string viewKey, string splitterKey, out double size)
    {
        size = 0;
        var view = ViewCustomizationStore.GetOrCreate(viewKey);
        if (view.SplitterSizes is null || !view.SplitterSizes.TryGetValue(splitterKey, out var stored) || stored <= 0)
            return false;

        size = stored;
        return true;
    }
}
