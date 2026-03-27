using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Pipe-Graph-Timeline: Zeigt Defekt-Marker entlang der Haltungslänge
/// mit Playhead, Fortschrittsbalken und Klick-Navigation.
/// Wiederverwendbar in CodierModus und TrainingCenter.
/// </summary>
public partial class PipeGraphTimeline : UserControl
{
    // ═══════ Dependency Properties ═══════

    /// <summary>Gesamtlaenge der Haltung in Metern.</summary>
    public static readonly DependencyProperty TotalLengthProperty =
        DependencyProperty.Register(nameof(TotalLength), typeof(double), typeof(PipeGraphTimeline),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    /// <summary>Aktuelle Meter-Position (Playhead).</summary>
    public static readonly DependencyProperty CurrentMeterProperty =
        DependencyProperty.Register(nameof(CurrentMeter), typeof(double), typeof(PipeGraphTimeline),
            new PropertyMetadata(0.0, OnCurrentMeterChanged));

    /// <summary>
    /// Sammlung von Marker-Objekten. Jedes Objekt muss mindestens
    /// die Properties MeterAt (double), Code (string), Confidence (double) haben.
    /// </summary>
    public static readonly DependencyProperty MarkersProperty =
        DependencyProperty.Register(nameof(Markers), typeof(IEnumerable), typeof(PipeGraphTimeline),
            new PropertyMetadata(null, OnMarkersChanged));

    /// <summary>Callback wenn User auf eine Position in der Timeline klickt.</summary>
    public static readonly DependencyProperty NavigateToMeterCommandProperty =
        DependencyProperty.Register(nameof(NavigateToMeterCommand), typeof(ICommand), typeof(PipeGraphTimeline));

    /// <summary>Callback wenn User auf einen Marker klickt (Parameter = Marker-Objekt).</summary>
    public static readonly DependencyProperty MarkerClickedCommandProperty =
        DependencyProperty.Register(nameof(MarkerClickedCommand), typeof(ICommand), typeof(PipeGraphTimeline));

    /// <summary>Funktion zum Auslesen von MeterAt aus einem Marker-Objekt.</summary>
    public static readonly DependencyProperty MeterAccessorProperty =
        DependencyProperty.Register(nameof(MeterAccessor), typeof(Func<object, double>), typeof(PipeGraphTimeline),
            new PropertyMetadata(null, OnMarkersChanged));

    /// <summary>Funktion zum Auslesen des Codes aus einem Marker-Objekt.</summary>
    public static readonly DependencyProperty CodeAccessorProperty =
        DependencyProperty.Register(nameof(CodeAccessor), typeof(Func<object, string>), typeof(PipeGraphTimeline),
            new PropertyMetadata(null, OnMarkersChanged));

    /// <summary>Funktion zum Auslesen der Konfidenz aus einem Marker-Objekt.</summary>
    public static readonly DependencyProperty ConfidenceAccessorProperty =
        DependencyProperty.Register(nameof(ConfidenceAccessor), typeof(Func<object, double>), typeof(PipeGraphTimeline),
            new PropertyMetadata(null, OnMarkersChanged));

    /// <summary>Funktion zum Auslesen des Status (abgelehnt?) aus einem Marker-Objekt.</summary>
    public static readonly DependencyProperty IsRejectedAccessorProperty =
        DependencyProperty.Register(nameof(IsRejectedAccessor), typeof(Func<object, bool>), typeof(PipeGraphTimeline),
            new PropertyMetadata(null, OnMarkersChanged));

    public double TotalLength { get => (double)GetValue(TotalLengthProperty); set => SetValue(TotalLengthProperty, value); }
    public double CurrentMeter { get => (double)GetValue(CurrentMeterProperty); set => SetValue(CurrentMeterProperty, value); }
    public IEnumerable? Markers { get => (IEnumerable?)GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
    public ICommand? NavigateToMeterCommand { get => (ICommand?)GetValue(NavigateToMeterCommandProperty); set => SetValue(NavigateToMeterCommandProperty, value); }
    public ICommand? MarkerClickedCommand { get => (ICommand?)GetValue(MarkerClickedCommandProperty); set => SetValue(MarkerClickedCommandProperty, value); }
    public Func<object, double>? MeterAccessor { get => (Func<object, double>?)GetValue(MeterAccessorProperty); set => SetValue(MeterAccessorProperty, value); }
    public Func<object, string>? CodeAccessor { get => (Func<object, string>?)GetValue(CodeAccessorProperty); set => SetValue(CodeAccessorProperty, value); }
    public Func<object, double>? ConfidenceAccessor { get => (Func<object, double>?)GetValue(ConfidenceAccessorProperty); set => SetValue(ConfidenceAccessorProperty, value); }
    public Func<object, bool>? IsRejectedAccessor { get => (Func<object, bool>?)GetValue(IsRejectedAccessorProperty); set => SetValue(IsRejectedAccessorProperty, value); }

    // ═══════ Farben (QualityGate) ═══════

    private static readonly SolidColorBrush BrushGreen = new(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush BrushYellow = new(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush BrushRed = new(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly SolidColorBrush BrushRejected = new(Color.FromRgb(0x64, 0x74, 0x8B));
    private static readonly SolidColorBrush BrushManual = new(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Orange fuer manuelle Eintraege
    private static readonly SolidColorBrush BrushScaleText = new(Color.FromRgb(0x64, 0x74, 0x8B));

    static PipeGraphTimeline()
    {
        BrushGreen.Freeze();
        BrushYellow.Freeze();
        BrushRed.Freeze();
        BrushRejected.Freeze();
        BrushManual.Freeze();
        BrushScaleText.Freeze();
    }

    // ═══════ State ═══════

    private bool _isDragging;
    private INotifyCollectionChanged? _subscribedCollection;

    // ═══════ Konstruktor ═══════

    public PipeGraphTimeline()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Refresh();
    }

    // ═══════ Property-Change Callbacks ═══════

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PipeGraphTimeline tl) tl.Refresh();
    }

    private static void OnCurrentMeterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PipeGraphTimeline tl) tl.UpdatePlayhead();
    }

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PipeGraphTimeline tl) return;

        // Collection-Change Subscription verwalten
        if (tl._subscribedCollection != null)
        {
            tl._subscribedCollection.CollectionChanged -= tl.OnMarkersCollectionChanged;
            tl._subscribedCollection = null;
        }

        if (tl.Markers is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += tl.OnMarkersCollectionChanged;
            tl._subscribedCollection = ncc;
        }

        tl.Refresh();
    }

    private void OnMarkersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(Refresh);
    }

    // ═══════ Rendering ═══════

    /// <summary>Komplettes Neuzeichnen: Marker + Playhead + Skala.</summary>
    private void Refresh()
    {
        TxtTotalLength.Text = TotalLength > 0 ? $"{TotalLength:F1} m" : "";
        DrawMarkers();
        DrawScale();
        UpdatePlayhead();
    }

    /// <summary>Defekt-Marker auf die Timeline zeichnen.</summary>
    private void DrawMarkers()
    {
        MarkerCanvas.Children.Clear();
        if (TotalLength <= 0 || Markers == null) return;

        double canvasW = TimelineBar.ActualWidth;
        if (canvasW <= 0) canvasW = 400;

        foreach (var item in Markers)
        {
            double meter = MeterAccessor?.Invoke(item) ?? 0;
            string code = CodeAccessor?.Invoke(item) ?? "?";
            double conf = ConfidenceAccessor?.Invoke(item) ?? -1;
            bool rejected = IsRejectedAccessor?.Invoke(item) ?? false;

            double x = Math.Clamp(meter / TotalLength, 0, 1) * canvasW;

            // Farbe nach QualityGate-Zone
            Brush fill;
            if (rejected)
                fill = BrushRejected;
            else if (conf < 0)
                fill = BrushManual; // Kein AI-Kontext → manuell
            else if (conf >= 0.85)
                fill = BrushGreen;
            else if (conf >= 0.60)
                fill = BrushYellow;
            else
                fill = BrushRed;

            // Vertikaler Balken (wie im Mockup)
            var bar = new Border
            {
                Width = 6,
                Height = 28,
                CornerRadius = new CornerRadius(3),
                Background = fill,
                ToolTip = $"{code}  {meter:F2}m" + (conf >= 0 ? $"  ({conf * 100:F0}%)" : ""),
                Cursor = Cursors.Hand,
                Opacity = rejected ? 0.4 : 0.9
            };

            // Hover-Effekt
            bar.MouseEnter += (s, _) => { if (s is Border b) { b.Opacity = 1.0; b.Height = 34; } };
            bar.MouseLeave += (s, _) => { if (s is Border b) { b.Opacity = rejected ? 0.4 : 0.9; b.Height = 28; } };

            // Klick auf Marker
            var capturedItem = item;
            bar.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true; // Kein Timeline-Klick
                MarkerClickedCommand?.Execute(capturedItem);
            };

            Canvas.SetLeft(bar, x - 3);
            Canvas.SetTop(bar, 4);
            MarkerCanvas.Children.Add(bar);

            // Code-Label unter dem Balken (nur wenn genug Platz)
            if (canvasW > 200)
            {
                var label = new TextBlock
                {
                    Text = code,
                    FontSize = 9,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = fill,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(label, x - 14);
                Canvas.SetTop(label, 34);
                MarkerCanvas.Children.Add(label);
            }
        }
    }

    /// <summary>Playhead (weisse Linie + Punkt) aktualisieren.</summary>
    private void UpdatePlayhead()
    {
        if (TotalLength <= 0) return;

        double canvasW = TimelineBar.ActualWidth;
        if (canvasW <= 0) canvasW = 400;

        double barH = 36;
        double x = Math.Clamp(CurrentMeter / TotalLength, 0, 1) * canvasW;

        PlayheadLine.Height = barH;
        Canvas.SetLeft(PlayheadLine, x - 1);
        Canvas.SetTop(PlayheadLine, 0);

        Canvas.SetLeft(PlayheadDot, x - 5);
        Canvas.SetTop(PlayheadDot, -3);

        // Fortschrittsbalken
        ProgressFill.Width = Math.Max(0, x);
    }

    /// <summary>Meter-Skala unterhalb der Timeline.</summary>
    private void DrawScale()
    {
        ScaleCanvas.Children.Clear();
        if (TotalLength <= 0) return;

        double canvasW = TimelineBar.ActualWidth;
        if (canvasW <= 0) canvasW = 400;

        // Sinnvolle Intervalle wählen
        double interval = TotalLength switch
        {
            <= 10 => 2,
            <= 25 => 5,
            <= 50 => 10,
            <= 100 => 20,
            <= 250 => 50,
            _ => 100
        };

        for (double m = 0; m <= TotalLength; m += interval)
        {
            double x = (m / TotalLength) * canvasW;
            var tb = new TextBlock
            {
                Text = $"{m:F0}m",
                FontSize = 10,
                Foreground = BrushScaleText,
                FontFamily = new FontFamily("Consolas")
            };

            // Letzte Beschriftung rechtsbuendig
            if (Math.Abs(m - TotalLength) < 0.01 || m + interval > TotalLength)
            {
                tb.Text = $"{TotalLength:F1}m";
                Canvas.SetRight(tb, 0);
            }
            else
            {
                Canvas.SetLeft(tb, Math.Max(0, x - 8));
            }

            Canvas.SetTop(tb, 0);
            ScaleCanvas.Children.Add(tb);
        }

        // Erste (0m) immer anzeigen, wenn nicht schon da
        if (interval > 0)
        {
            var first = new TextBlock
            {
                Text = "0m",
                FontSize = 10,
                Foreground = BrushScaleText,
                FontFamily = new FontFamily("Consolas")
            };
            Canvas.SetLeft(first, 0);
            Canvas.SetTop(first, 0);
            ScaleCanvas.Children.Insert(0, first);
        }
    }

    // ═══════ Maus-Interaktion (Klick + Drag auf Timeline) ═══════

    private void TimelineBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        TimelineBar.CaptureMouse();
        NavigateToClickPosition(e);
    }

    private void TimelineBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        NavigateToClickPosition(e);
    }

    private void TimelineBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        TimelineBar.ReleaseMouseCapture();
    }

    private void NavigateToClickPosition(MouseEventArgs e)
    {
        if (TotalLength <= 0) return;

        double canvasW = TimelineBar.ActualWidth;
        if (canvasW <= 0) return;

        double x = e.GetPosition(TimelineBar).X;
        double meter = Math.Clamp((x / canvasW) * TotalLength, 0, TotalLength);

        NavigateToMeterCommand?.Execute(meter);
    }
}
