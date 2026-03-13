using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Uhrzeiger-Auswahl (Einzelpunkt). Zeigt Rohrleitungsquerschnitt
/// mit Punkt-Markierung an der gewaehlten Stunde.
/// </summary>
public partial class ClockPickerControl : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(ClockPickerControl),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    private readonly List<ClockItem> _items = new();
    private readonly List<Line> _ticks = new();
    private bool _isInitialized;
    private int? _selectedHour;

    // Farben (konsistent mit Theme)
    private static readonly SolidColorBrush SelectedBrush = new(Color.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly SolidColorBrush DefaultNumberBrush = new(Color.FromRgb(0x3D, 0x4D, 0x63));
    private static readonly SolidColorBrush TickDefaultBrush = new(Color.FromRgb(0xCD, 0xD6, 0xE4));

    public ClockPickerControl()
    {
        InitializeComponent();
        Loaded += (_, _) => EnsureItems();
        SizeChanged += (_, _) => UpdateLayoutPositions();
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClockPickerControl control)
            control.ApplyValue(e.NewValue as string);
    }

    private void ApplyValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _selectedHour = null;
        }
        else if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
        {
            _selectedHour = NormalizeHour(hour);
        }

        UpdatePointMarker();
        UpdateHighlights();
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
                Foreground = DefaultNumberBrush,
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
                Cursor = System.Windows.Input.Cursors.Hand,
                Opacity = 0.01
            };
            button.Click += Hour_Click;

            FaceCanvas.Children.Add(text);
            FaceCanvas.Children.Add(button);
            _items.Add(new ClockItem(hour, text, button));
        }

        UpdateLayoutPositions();
        ApplyValue(Value);
    }

    // ═══════════════════════════════════════════════════════════════
    // Layout
    // ═══════════════════════════════════════════════════════════════

    private void UpdateLayoutPositions()
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

        // Proportionen (basierend auf JSX-Referenz 140px)
        var rNum = s * 0.371;           // Zahlen aussen
        var rWall = s * 0.314;          // Mitte Rohrwand-Strich
        var wStroke = Math.Max(4, s * 0.071); // Rohrwand-Dicke
        var rInner = s * 0.271;         // Innenradius

        // Rohrwand positionieren
        PipeWall.Width = rWall * 2;
        PipeWall.Height = rWall * 2;
        PipeWall.StrokeThickness = wStroke;
        Canvas.SetLeft(PipeWall, cx - rWall);
        Canvas.SetTop(PipeWall, cy - rWall);

        // Innenraum positionieren
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

        UpdatePointMarker();
    }

    // ═══════════════════════════════════════════════════════════════
    // Visuelle Aktualisierung
    // ═══════════════════════════════════════════════════════════════

    private void UpdatePointMarker()
    {
        if (_selectedHour is null || !_isInitialized)
        {
            PointMarker.Visibility = Visibility.Collapsed;
            return;
        }

        var w = FaceCanvas.ActualWidth > 0 ? FaceCanvas.ActualWidth : ActualWidth;
        var h = FaceCanvas.ActualHeight > 0 ? FaceCanvas.ActualHeight : ActualHeight;
        if (w < 40 || h < 40)
            return;

        PointMarker.Visibility = Visibility.Visible;
        var s = Math.Min(w, h);
        var cx = w / 2.0;
        var cy = h / 2.0;
        var rMarker = s * 0.20; // Markierung im Innenraum

        var angle = GetAngle(_selectedHour.Value);
        Canvas.SetLeft(PointMarker, cx + Math.Cos(angle) * rMarker - PointMarker.Width / 2);
        Canvas.SetTop(PointMarker, cy + Math.Sin(angle) * rMarker - PointMarker.Height / 2);
    }

    private void UpdateHighlights()
    {
        if (!_isInitialized)
            return;

        for (int i = 0; i < _items.Count; i++)
        {
            var isSelected = _selectedHour.HasValue && _items[i].Hour == _selectedHour.Value;

            _items[i].Text.Foreground = isSelected ? SelectedBrush : DefaultNumberBrush;
            _items[i].Text.FontWeight = isSelected ? FontWeights.ExtraBold : FontWeights.SemiBold;
            _items[i].Text.FontSize = isSelected ? 13 : 11;

            _ticks[i].Stroke = isSelected ? SelectedBrush : TickDefaultBrush;
            _ticks[i].StrokeThickness = isSelected ? 2.5 : 1.5;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Klick-Verarbeitung
    // ═══════════════════════════════════════════════════════════════

    private void Hour_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int hour)
            return;

        hour = NormalizeHour(hour);

        // Toggle: gleiche Stunde erneut klicken = Auswahl aufheben
        Value = _selectedHour == hour
            ? string.Empty
            : hour.ToString(CultureInfo.InvariantCulture);
    }

    // ═══════════════════════════════════════════════════════════════
    // Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════════

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
