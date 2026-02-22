using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Controls;

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
    private bool _isInitialized;
    private int? _selectedHour;

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
            UpdateHand();
            UpdateSelectionVisuals();
            return;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
        {
            hour = NormalizeHour(hour);
            _selectedHour = hour;
            UpdateHand();
            UpdateSelectionVisuals();
        }
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
                FontWeight = FontWeights.SemiBold
            };

            var button = new Button
            {
                Tag = hour,
                Width = 28,
                Height = 28,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            button.Click += Hour_Click;

            FaceCanvas.Children.Add(text);
            FaceCanvas.Children.Add(button);

            _items.Add(new ClockItem(hour, text, button));
        }

        UpdateLayoutPositions();
        ApplyValue(Value);
    }

    private void UpdateLayoutPositions()
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

        var size = Math.Min(w, h);
        var centerX = w / 2.0;
        var centerY = h / 2.0;
        var radius = size / 2.0 - 6;
        var numberRadius = radius - 16;
        var buttonRadius = radius - 12;

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
            Canvas.SetLeft(item.Text, nx - 6);
            Canvas.SetTop(item.Text, ny - 8);

            var bx = centerX + Math.Cos(angle) * buttonRadius;
            var by = centerY + Math.Sin(angle) * buttonRadius;
            Canvas.SetLeft(item.Button, bx - item.Button.Width / 2);
            Canvas.SetTop(item.Button, by - item.Button.Height / 2);
        }

        UpdateHand();
    }

    private void UpdateHand()
    {
        var w = FaceCanvas.ActualWidth;
        var h = FaceCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            w = ActualWidth;
            h = ActualHeight;
        }

        var centerX = w / 2.0;
        var centerY = h / 2.0;
        Hand.X1 = centerX;
        Hand.Y1 = centerY;

        if (_selectedHour is null)
        {
            Hand.Visibility = Visibility.Collapsed;
            return;
        }

        Hand.Visibility = Visibility.Visible;
        var radius = Math.Min(w, h) / 2.0 - 26;
        var angle = GetAngle(_selectedHour.Value);
        Hand.X2 = centerX + Math.Cos(angle) * radius;
        Hand.Y2 = centerY + Math.Sin(angle) * radius;
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var item in _items)
        {
            if (_selectedHour.HasValue && item.Hour == _selectedHour.Value)
            {
                item.Button.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 180, 0));
                item.Button.BorderThickness = new Thickness(1);
            }
            else
            {
                item.Button.BorderBrush = Brushes.Transparent;
                item.Button.BorderThickness = new Thickness(0);
            }
        }
    }

    private void Hour_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int hour)
            return;

        hour = NormalizeHour(hour);
        Value = hour.ToString(CultureInfo.InvariantCulture);
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
