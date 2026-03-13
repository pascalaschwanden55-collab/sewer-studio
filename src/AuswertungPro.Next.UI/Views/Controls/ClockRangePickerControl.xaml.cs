using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Uhrzeiger-Bereichsauswahl (Von–Bis). Zeigt Rohrleitungsquerschnitt
/// mit Bogenmarkierung fuer den gewaehlten Bereich.
/// </summary>
public partial class ClockRangePickerControl : UserControl
{
    public static readonly DependencyProperty ValueFromProperty = DependencyProperty.Register(
        nameof(ValueFrom),
        typeof(string),
        typeof(ClockRangePickerControl),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    public static readonly DependencyProperty ValueToProperty = DependencyProperty.Register(
        nameof(ValueTo),
        typeof(string),
        typeof(ClockRangePickerControl),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    private readonly List<ClockItem> _items = new();
    private readonly List<Line> _ticks = new();
    private bool _isInitialized;
    private int? _vonHour;
    private int? _bisHour;
    private bool _nextClickSetsBis;

    // Farben (konsistent mit Theme)
    private static readonly SolidColorBrush VonBrush = new(Color.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly SolidColorBrush BisBrush = new(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush InRangeBrush = new(Color.FromRgb(0x4A, 0xB8, 0x6A));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0x3D, 0x4D, 0x63));
    private static readonly SolidColorBrush TickDefaultBrush = new(Color.FromRgb(0xCD, 0xD6, 0xE4));
    private static readonly SolidColorBrush TickActiveBrush = new(Color.FromRgb(0x16, 0xA3, 0x4A));

    public ClockRangePickerControl()
    {
        InitializeComponent();
        Loaded += (_, _) => EnsureItems();
        SizeChanged += (_, _) => RefreshLayout();
    }

    public string ValueFrom
    {
        get => (string)GetValue(ValueFromProperty);
        set => SetValue(ValueFromProperty, value);
    }

    public string ValueTo
    {
        get => (string)GetValue(ValueToProperty);
        set => SetValue(ValueToProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClockRangePickerControl control)
            control.ApplyValues();
    }

    private void ApplyValues()
    {
        _vonHour = ParseHour(ValueFrom);
        _bisHour = ParseHour(ValueTo);
        _nextClickSetsBis = _vonHour.HasValue && !_bisHour.HasValue;
        UpdateVisuals();
    }

    // ═══════════════════════════════════════════════════════════════
    // Initialisierung
    // ═══════════════════════════════════════════════════════════════

    private void EnsureItems()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        for (var hour = 1; hour <= 12; hour++)
        {
            // Tick-Markierung
            var tick = new Line
            {
                Stroke = TickDefaultBrush,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            FaceCanvas.Children.Add(tick);
            _ticks.Add(tick);

            // Stundenzahl
            var text = new TextBlock
            {
                Text = hour.ToString(CultureInfo.InvariantCulture),
                Foreground = DefaultBrush,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                IsHitTestVisible = false
            };

            // Unsichtbarer Klick-Button (22x22: bei 140px-Groesse ueberlappen
            // sich 28x28-Buttons, was Fehlklicks auf Nachbarstunden verursacht)
            // MinWidth/MinHeight=0: Theme-Style setzt MinWidth=100, MinHeight=36!
            var button = new Button
            {
                Tag = hour,
                Width = 22,
                Height = 22,
                MinWidth = 0,
                MinHeight = 0,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Opacity = 0.01
            };
            button.Click += Hour_Click;

            FaceCanvas.Children.Add(text);
            FaceCanvas.Children.Add(button);
            _items.Add(new ClockItem(hour, text, button));
        }

        RefreshLayout();
        ApplyValues();
    }

    // ═══════════════════════════════════════════════════════════════
    // Layout
    // ═══════════════════════════════════════════════════════════════

    private void RefreshLayout()
    {
        if (!_isInitialized)
            return;

        var w = FaceCanvas.ActualWidth > 0 ? FaceCanvas.ActualWidth : ActualWidth;
        var h = FaceCanvas.ActualHeight > 0 ? FaceCanvas.ActualHeight : ActualHeight;
        if (w < 40 || h < 40)
            return;

        var s = Math.Min(w, h);
        var cx = w / 2.0;
        var cy = h / 2.0;

        // Proportionen (konsistent mit ClockPickerControl)
        var rNum = s * 0.371;
        var rWall = s * 0.314;
        var wStroke = Math.Max(4, s * 0.071);
        var rInner = s * 0.271;

        // Rohrwand
        PipeWall.Width = rWall * 2;
        PipeWall.Height = rWall * 2;
        PipeWall.StrokeThickness = wStroke;
        Canvas.SetLeft(PipeWall, cx - rWall);
        Canvas.SetTop(PipeWall, cy - rWall);

        // Innenraum
        PipeInterior.Width = rInner * 2;
        PipeInterior.Height = rInner * 2;
        Canvas.SetLeft(PipeInterior, cx - rInner);
        Canvas.SetTop(PipeInterior, cy - rInner);

        // Mittelpunkt
        Canvas.SetLeft(CenterDot, cx - CenterDot.Width / 2);
        Canvas.SetTop(CenterDot, cy - CenterDot.Height / 2);

        // Tick-Markierungen und Stunden positionieren
        for (int i = 0; i < 12; i++)
        {
            var angle = GetAngle(i + 1);
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);

            // Tick (innerhalb der Rohrwand)
            _ticks[i].X1 = cx + cos * (rInner + 2);
            _ticks[i].Y1 = cy + sin * (rInner + 2);
            _ticks[i].X2 = cx + cos * (rInner + wStroke - 1);
            _ticks[i].Y2 = cy + sin * (rInner + wStroke - 1);

            // Zahl (aussen)
            Canvas.SetLeft(_items[i].Text, cx + cos * rNum - 7);
            Canvas.SetTop(_items[i].Text, cy + sin * rNum - 8);

            // Klick-Button (auf der Rohrwand, zentriert)
            Canvas.SetLeft(_items[i].Button, cx + cos * rWall - 11);
            Canvas.SetTop(_items[i].Button, cy + sin * rWall - 11);
        }

        UpdateVisuals();
    }

    // ═══════════════════════════════════════════════════════════════
    // Visuelle Aktualisierung
    // ═══════════════════════════════════════════════════════════════

    private void UpdateVisuals()
    {
        if (!_isInitialized)
            return;

        var w = FaceCanvas.ActualWidth > 0 ? FaceCanvas.ActualWidth : ActualWidth;
        var h = FaceCanvas.ActualHeight > 0 ? FaceCanvas.ActualHeight : ActualHeight;
        if (w < 40 || h < 40)
            return;

        var s = Math.Min(w, h);
        var cx = w / 2.0;
        var cy = h / 2.0;
        var rInner = s * 0.271;
        var rMarker = s * 0.20;

        UpdateArc(cx, cy, rInner);
        UpdateMarkers(cx, cy, rMarker);
        UpdateHighlights();
        UpdateStatusText();
    }

    /// <summary>Bereichs-Bogen als Strich (nicht gefuellt) zeichnen.</summary>
    private void UpdateArc(double cx, double cy, double rArc)
    {
        if (_vonHour is null || _bisHour is null)
        {
            RangeArc.Visibility = Visibility.Collapsed;
            return;
        }

        // Gesamtumfang: Von=12, Bis=12 → voller Kreis
        if (_vonHour.Value == 12 && _bisHour.Value == 12)
        {
            RangeArc.Data = new EllipseGeometry(new Point(cx, cy), rArc, rArc);
            RangeArc.Visibility = Visibility.Visible;
            return;
        }

        // Gleiche Stunde → kein Bogen
        if (_vonHour == _bisHour)
        {
            RangeArc.Visibility = Visibility.Collapsed;
            return;
        }

        var a1 = GetAngle(_vonHour.Value);
        var a2 = GetAngle(_bisHour.Value);
        var p1 = new Point(cx + Math.Cos(a1) * rArc, cy + Math.Sin(a1) * rArc);
        var p2 = new Point(cx + Math.Cos(a2) * rArc, cy + Math.Sin(a2) * rArc);

        // Winkelspanne im Uhrzeigersinn (Von → Bis)
        var sweep = a2 - a1;
        if (sweep <= 0)
            sweep += 2 * Math.PI;

        var figure = new PathFigure { StartPoint = p1, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(
            p2,
            new Size(rArc, rArc),
            0,
            sweep > Math.PI,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        RangeArc.Data = geometry;
        RangeArc.Visibility = Visibility.Visible;
    }

    /// <summary>Von/Bis-Markierungspunkte im Innenraum positionieren.</summary>
    private void UpdateMarkers(double cx, double cy, double rMarker)
    {
        if (_vonHour is not null)
        {
            PointVon.Visibility = Visibility.Visible;
            var a = GetAngle(_vonHour.Value);
            Canvas.SetLeft(PointVon, cx + Math.Cos(a) * rMarker - PointVon.Width / 2);
            Canvas.SetTop(PointVon, cy + Math.Sin(a) * rMarker - PointVon.Height / 2);
        }
        else
        {
            PointVon.Visibility = Visibility.Collapsed;
        }

        if (_bisHour is not null)
        {
            PointBis.Visibility = Visibility.Visible;
            var a = GetAngle(_bisHour.Value);
            Canvas.SetLeft(PointBis, cx + Math.Cos(a) * rMarker - PointBis.Width / 2);
            Canvas.SetTop(PointBis, cy + Math.Sin(a) * rMarker - PointBis.Height / 2);
        }
        else
        {
            PointBis.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Zahlen und Ticks je nach Selektion/Bereich hervorheben.</summary>
    private void UpdateHighlights()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var isVon = _vonHour.HasValue && item.Hour == _vonHour.Value;
            var isBis = _bisHour.HasValue && item.Hour == _bisHour.Value;
            var inRange = IsInRange(item.Hour);

            SolidColorBrush textBrush;
            FontWeight weight;
            double fontSize;

            if (isVon)
            {
                textBrush = VonBrush;
                weight = FontWeights.ExtraBold;
                fontSize = 13;
            }
            else if (isBis)
            {
                textBrush = BisBrush;
                weight = FontWeights.ExtraBold;
                fontSize = 13;
            }
            else if (inRange)
            {
                textBrush = InRangeBrush;
                weight = FontWeights.Bold;
                fontSize = 11;
            }
            else
            {
                textBrush = DefaultBrush;
                weight = FontWeights.SemiBold;
                fontSize = 11;
            }

            item.Text.Foreground = textBrush;
            item.Text.FontWeight = weight;
            item.Text.FontSize = fontSize;

            _ticks[i].Stroke = (inRange || isVon || isBis) ? TickActiveBrush : TickDefaultBrush;
            _ticks[i].StrokeThickness = (inRange || isVon || isBis) ? 2.5 : 1.5;
        }
    }

    /// <summary>Prueft ob eine Stunde im gewaehlten Bereich liegt.</summary>
    private bool IsInRange(int hour)
    {
        if (_vonHour is null || _bisHour is null)
            return false;

        var von = _vonHour.Value;
        var bis = _bisHour.Value;

        // Gesamtumfang (12→12)
        if (von == 12 && bis == 12)
            return true;

        if (von == bis)
            return hour == von;

        // Normalisierung fuer Wrap-Around-Vergleich
        var v = von % 12;
        var b = bis % 12;
        var h = hour % 12;

        return v <= b
            ? (h >= v && h <= b)
            : (h >= v || h <= b);
    }

    private void UpdateStatusText()
    {
        // Bei kleinen Groessen (z.B. 140x140 in VsaCodeExplorer) StatusText
        // ausblenden — ueberlappt sonst mit der 6-Uhr-Zahl
        var h = ActualHeight > 0 ? ActualHeight : Height;
        if (h < 200)
        {
            StatusText.Visibility = Visibility.Collapsed;
            return;
        }

        StatusText.Visibility = Visibility.Visible;
        if (_vonHour.HasValue && _bisHour.HasValue)
            StatusText.Text = $"{_vonHour} Uhr \u2192 {_bisHour} Uhr";
        else if (_vonHour.HasValue)
            StatusText.Text = $"Von: {_vonHour} Uhr  |  Bis: klicken...";
        else
            StatusText.Text = "Von: klicken...";
    }

    // ═══════════════════════════════════════════════════════════════
    // Klick-Verarbeitung
    // ═══════════════════════════════════════════════════════════════

    private void Hour_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int hour)
            return;

        hour = NormalizeHour(hour);

        if (!_vonHour.HasValue || (!_nextClickSetsBis && !_bisHour.HasValue))
        {
            // Setze Von
            ValueFrom = hour.ToString(CultureInfo.InvariantCulture);
            _nextClickSetsBis = true;
            return;
        }

        if (_nextClickSetsBis)
        {
            // Setze Bis
            ValueTo = hour.ToString(CultureInfo.InvariantCulture);
            _nextClickSetsBis = false;
            return;
        }

        // Beide gesetzt: naechstliegenden Wert aktualisieren
        var distVon = ClockDistance(hour, _vonHour.Value);
        var distBis = ClockDistance(hour, _bisHour!.Value);

        if (distVon <= distBis)
        {
            ValueFrom = hour.ToString(CultureInfo.InvariantCulture);
            _nextClickSetsBis = true;
        }
        else
        {
            ValueTo = hour.ToString(CultureInfo.InvariantCulture);
            _nextClickSetsBis = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════════

    private static int ClockDistance(int a, int b)
    {
        var diff = Math.Abs(a - b);
        return Math.Min(diff, 12 - diff);
    }

    private static int? ParseHour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            ? NormalizeHour(hour)
            : null;
    }

    private static int NormalizeHour(int hour)
    {
        if (hour <= 0)
            return 12;
        if (hour > 12)
            return ((hour - 1) % 12) + 1;
        return hour;
    }

    private static double GetAngle(int hour)
    {
        var h = NormalizeHour(hour);
        var degrees = (h % 12) * 30 - 90;
        return degrees * Math.PI / 180.0;
    }

    private sealed class ClockItem
    {
        public int Hour { get; }
        public TextBlock Text { get; }
        public Button Button { get; }

        public ClockItem(int hour, TextBlock text, Button button)
        {
            Hour = hour;
            Text = text;
            Button = button;
        }
    }
}
