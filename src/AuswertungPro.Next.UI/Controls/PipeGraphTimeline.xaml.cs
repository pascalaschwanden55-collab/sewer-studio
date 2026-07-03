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

    /// <summary>Optional: End-Meter fuer Streckenschaeden — Marker wird als Balken von Meter bis Ende gezeichnet.</summary>
    public static readonly DependencyProperty EndMeterAccessorProperty =
        DependencyProperty.Register(nameof(EndMeterAccessor), typeof(Func<object, double?>), typeof(PipeGraphTimeline),
            new PropertyMetadata(null, OnMarkersChanged));

    /// <summary>Optional: explizite Markerfarbe — ueberschreibt die QualityGate-Klassifizierung (Schadensband).</summary>
    public static readonly DependencyProperty ColorKindAccessorProperty =
        DependencyProperty.Register(nameof(ColorKindAccessor), typeof(Func<object, MarkerColorKind?>), typeof(PipeGraphTimeline),
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
    public Func<object, double?>? EndMeterAccessor { get => (Func<object, double?>?)GetValue(EndMeterAccessorProperty); set => SetValue(EndMeterAccessorProperty, value); }
    public Func<object, MarkerColorKind?>? ColorKindAccessor { get => (Func<object, MarkerColorKind?>?)GetValue(ColorKindAccessorProperty); set => SetValue(ColorKindAccessorProperty, value); }

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

    private static Brush BrushForMarkerColor(MarkerColorKind color)
        => color switch
        {
            MarkerColorKind.Green => BrushGreen,
            MarkerColorKind.Yellow => BrushYellow,
            MarkerColorKind.Red => BrushRed,
            MarkerColorKind.Rejected => BrushRejected,
            MarkerColorKind.Manual => BrushManual,
            _ => BrushRed
        };

    private bool _isDragging;
    private INotifyCollectionChanged? _subscribedCollection;

    // ═══════ Konstruktor ═══════

    public PipeGraphTimeline()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Refresh();
        // CollectionChanged-Abo an den Lebenszyklus binden: beim Entladen loesen (sonst haelt die
        // externe Markers-Collection das Control am Leben -> Leak), beim Laden neu setzen. (Audit)
        Loaded += (_, _) => SubscribeMarkers();
        Unloaded += (_, _) => UnsubscribeMarkers();
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
        tl.SubscribeMarkers();
        tl.Refresh();
    }

    private void SubscribeMarkers()
    {
        UnsubscribeMarkers();
        if (Markers is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += OnMarkersCollectionChanged;
            _subscribedCollection = ncc;
        }
    }

    private void UnsubscribeMarkers()
    {
        if (_subscribedCollection != null)
        {
            _subscribedCollection.CollectionChanged -= OnMarkersCollectionChanged;
            _subscribedCollection = null;
        }
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

        double canvasW = TimelineScaleCalculator.EffectiveCanvasWidth(TimelineBar.ActualWidth);

        foreach (var item in Markers)
        {
            double meter = MeterAccessor?.Invoke(item) ?? 0;
            string code = CodeAccessor?.Invoke(item) ?? "?";
            double conf = ConfidenceAccessor?.Invoke(item) ?? -1;
            bool rejected = IsRejectedAccessor?.Invoke(item) ?? false;
            double? endMeter = EndMeterAccessor?.Invoke(item);

            double x = TimelineScaleCalculator.MeterToX(meter, TotalLength, canvasW);

            // Farbe: explizit (Schadensband) oder QualityGate-Zone
            var color = ColorKindAccessor?.Invoke(item)
                        ?? MarkerColorClassifier.Classify(rejected, conf);
            Brush fill = BrushForMarkerColor(color);

            // Streckenschaden: Balken von Meter bis Ende; sonst schmaler Punkt-Marker
            var istStrecke = endMeter is double em && em > meter;
            double breite = 6;
            if (istStrecke)
            {
                double x2 = TimelineScaleCalculator.MeterToX(endMeter!.Value, TotalLength, canvasW);
                breite = Math.Max(6, x2 - x);
            }

            var normalOpacity = rejected ? 0.4 : (istStrecke ? 0.55 : 0.9);
            var bar = new Border
            {
                Width = breite,
                Height = 28,
                CornerRadius = new CornerRadius(3),
                Background = fill,
                ToolTip = istStrecke
                    ? $"{code}  {meter:F2}–{endMeter!.Value:F2}m"
                    : $"{code}  {meter:F2}m" + (conf >= 0 ? $"  ({conf * 100:F0}%)" : ""),
                Cursor = Cursors.Hand,
                Opacity = normalOpacity
            };

            // Hover-Effekt
            bar.MouseEnter += (s, _) => { if (s is Border b) { b.Opacity = 1.0; b.Height = 34; } };
            bar.MouseLeave += (s, _) => { if (s is Border b) { b.Opacity = normalOpacity; b.Height = 28; } };

            // Klick auf Marker
            var capturedItem = item;
            bar.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true; // Kein Timeline-Klick
                MarkerClickedCommand?.Execute(capturedItem);
            };

            Canvas.SetLeft(bar, istStrecke ? x : x - 3);
            Canvas.SetTop(bar, 4);
            MarkerCanvas.Children.Add(bar);

            // Code-Label unter dem Balken (nur wenn genug Platz)
            if (canvasW > 200)
            {
                var labelX = istStrecke ? x + breite / 2 : x;
                var label = new TextBlock
                {
                    Text = code,
                    FontSize = 9,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = fill,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(label, labelX - 14);
                Canvas.SetTop(label, 34);
                MarkerCanvas.Children.Add(label);
            }
        }
    }

    /// <summary>Playhead (weisse Linie + Punkt) aktualisieren.</summary>
    private void UpdatePlayhead()
    {
        if (TotalLength <= 0) return;

        double barH = 36;
        double x = TimelineScaleCalculator.MeterToX(CurrentMeter, TotalLength, TimelineBar.ActualWidth);

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

        double canvasW = TimelineScaleCalculator.EffectiveCanvasWidth(TimelineBar.ActualWidth);

        foreach (var tick in TimelineScaleCalculator.BuildTicks(TotalLength, canvasW))
        {
            var tb = new TextBlock
            {
                Text = tick.Text,
                FontSize = 10,
                Foreground = BrushScaleText,
                FontFamily = new FontFamily("Consolas")
            };

            // Letzte Beschriftung rechtsbuendig
            if (tick.AlignRight)
            {
                Canvas.SetRight(tb, 0);
            }
            else
            {
                Canvas.SetLeft(tb, tick.Left);
            }

            Canvas.SetTop(tb, 0);
            ScaleCanvas.Children.Add(tb);
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

        double x = e.GetPosition(TimelineBar).X;
        var meter = TimelineScaleCalculator.XToMeter(x, TotalLength, TimelineBar.ActualWidth);
        if (meter is null) return;

        NavigateToMeterCommand?.Execute(meter.Value);
    }
}
