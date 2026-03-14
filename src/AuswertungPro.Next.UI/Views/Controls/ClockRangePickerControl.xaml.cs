using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Controls;

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
    private bool _isInitialized;
    private int? _vonHour;
    private int? _bisHour;
    private bool _nextClickSetsBis;

    public ClockRangePickerControl()
    {
        InitializeComponent();
        Loaded += (_, _) => EnsureItems();
        SizeChanged += (_, _) => UpdateLayout();
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

    private void EnsureItems()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        for (var hour = 1; hour <= 12; hour++)
        {
            var text = new TextBlock
            {
                Text = hour.ToString(CultureInfo.InvariantCulture),
                Foreground = Brushes.Black,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                IsHitTestVisible = false
            };

            var button = new Button
            {
                Tag = hour,
                Width = 30,
                Height = 30,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Opacity = 0.01
            };
            button.Click += Hour_Click;

            FaceCanvas.Children.Add(text);
            FaceCanvas.Children.Add(button);

            _items.Add(new ClockItem(hour, text, button));
        }

        UpdateLayout();
        ApplyValues();
    }

    private new void UpdateLayout()
    {
        if (!_isInitialized)
            return;

        var w = FaceCanvas.ActualWidth;
        var h = FaceCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            w = ActualWidth;
            h = ActualHeight;
        }

        if (w < 40 || h < 40)
            return;

        var size = Math.Min(w, h);
        var centerX = w / 2.0;
        var centerY = h / 2.0;
        var radius = Math.Max(20, size / 2.0 - 8);
        var numberRadius = radius - 18;
        var buttonRadius = radius - 14;

        Canvas.SetLeft(Face, centerX - radius);
        Canvas.SetTop(Face, centerY - radius);
        Face.Width = radius * 2;
        Face.Height = radius * 2;

        Canvas.SetLeft(CenterDot, centerX - CenterDot.Width / 2);
        Canvas.SetTop(CenterDot, centerY - CenterDot.Height / 2);

        foreach (var item in _items)
        {
            var angle = GetAngle(item.Hour);
            var nx = centerX + Math.Cos(angle) * numberRadius;
            var ny = centerY + Math.Sin(angle) * numberRadius;
            Canvas.SetLeft(item.Text, nx - 7);
            Canvas.SetTop(item.Text, ny - 9);

            var bx = centerX + Math.Cos(angle) * buttonRadius;
            var by = centerY + Math.Sin(angle) * buttonRadius;
            Canvas.SetLeft(item.Button, bx - item.Button.Width / 2);
            Canvas.SetTop(item.Button, by - item.Button.Height / 2);
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (!_isInitialized)
            return;

        var w = FaceCanvas.ActualWidth;
        var h = FaceCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            w = ActualWidth;
            h = ActualHeight;
        }

        if (w < 40 || h < 40)
            return;

        var centerX = w / 2.0;
        var centerY = h / 2.0;
        var size = Math.Min(w, h);
        var radius = Math.Max(20, size / 2.0 - 8);
        var handRadius = radius - 30;

        UpdateHand(HandVon, _vonHour, centerX, centerY, handRadius);
        UpdateHand(HandBis, _bisHour, centerX, centerY, handRadius);
        UpdateArc(centerX, centerY, radius - 4);
        UpdateNumberHighlights();
        UpdateStatusText();
    }

    private static void UpdateHand(Line hand, int? hour, double cx, double cy, double radius)
    {
        hand.X1 = cx;
        hand.Y1 = cy;

        if (hour is null)
        {
            hand.Visibility = Visibility.Collapsed;
            return;
        }

        hand.Visibility = Visibility.Visible;
        var angle = GetAngle(hour.Value);
        hand.X2 = cx + Math.Cos(angle) * radius;
        hand.Y2 = cy + Math.Sin(angle) * radius;
    }

    private void UpdateArc(double cx, double cy, double radius)
    {
        if (_vonHour is null || _bisHour is null || _vonHour == _bisHour)
        {
            RangeArc.Visibility = Visibility.Collapsed;
            return;
        }

        var vonAngle = GetAngle(_vonHour.Value);
        var bisAngle = GetAngle(_bisHour.Value);

        var vonPoint = new Point(cx + Math.Cos(vonAngle) * radius, cy + Math.Sin(vonAngle) * radius);
        var bisPoint = new Point(cx + Math.Cos(bisAngle) * radius, cy + Math.Sin(bisAngle) * radius);

        // Winkelspanne im Uhrzeigersinn (Von → Bis)
        var sweep = bisAngle - vonAngle;
        if (sweep < 0)
            sweep += 2 * Math.PI;
        if (sweep == 0)
            sweep = 2 * Math.PI;

        var isLargeArc = sweep > Math.PI;

        var figure = new PathFigure
        {
            StartPoint = new Point(cx, cy),
            IsClosed = true,
            IsFilled = true
        };
        figure.Segments.Add(new LineSegment(vonPoint, true));
        figure.Segments.Add(new ArcSegment(
            bisPoint,
            new Size(radius, radius),
            0,
            isLargeArc,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        RangeArc.Data = geometry;
        RangeArc.Visibility = Visibility.Visible;
    }

    private void UpdateNumberHighlights()
    {
        foreach (var item in _items)
        {
            var isVon = _vonHour.HasValue && item.Hour == _vonHour.Value;
            var isBis = _bisHour.HasValue && item.Hour == _bisHour.Value;
            var inRange = IsInRange(item.Hour);

            if (isVon)
            {
                item.Text.Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0));
                item.Text.FontWeight = FontWeights.ExtraBold;
                item.Text.FontSize = 15;
            }
            else if (isBis)
            {
                item.Text.Foreground = new SolidColorBrush(Color.FromRgb(31, 78, 158));
                item.Text.FontWeight = FontWeights.ExtraBold;
                item.Text.FontSize = 15;
            }
            else if (inRange)
            {
                item.Text.Foreground = new SolidColorBrush(Color.FromRgb(180, 100, 0));
                item.Text.FontWeight = FontWeights.Bold;
                item.Text.FontSize = 13;
            }
            else
            {
                item.Text.Foreground = Brushes.Black;
                item.Text.FontWeight = FontWeights.SemiBold;
                item.Text.FontSize = 13;
            }
        }
    }

    private bool IsInRange(int hour)
    {
        if (_vonHour is null || _bisHour is null)
            return false;

        var von = _vonHour.Value;
        var bis = _bisHour.Value;

        if (von == bis)
            return hour == von;

        if (von < bis)
            return hour >= von && hour <= bis;

        // Ueber 12 hinaus (z.B. von 10 bis 2)
        return hour >= von || hour <= bis;
    }

    private void UpdateStatusText()
    {
        if (_vonHour.HasValue && _bisHour.HasValue)
            StatusText.Text = $"{_vonHour} Uhr \u2192 {_bisHour} Uhr";
        else if (_vonHour.HasValue)
            StatusText.Text = $"Von: {_vonHour} Uhr  |  Bis: klicken...";
        else
            StatusText.Text = "Von: klicken...";
    }

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

        // Beide gesetzt: aktualisiere den naechsten (Von oder Bis, je nach Naehe)
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

    private static int ClockDistance(int a, int b)
    {
        var diff = Math.Abs(a - b);
        return Math.Min(diff, 12 - diff);
    }

    private static int? ParseHour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return NormalizeHour(hour);

        return null;
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
