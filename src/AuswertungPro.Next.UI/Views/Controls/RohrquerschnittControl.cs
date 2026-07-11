using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Theme;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Interaktiver Rohrquerschnitt fuer die Uhrlage-Eingabe — dieselbe Formsprache wie der
/// KI-Uhrlage-Ring im LiveFrameWindow (gestrichelte Doppelringe, 12 Ticks, Ring-Sektor):
/// Klick auf den Ring setzt UhrVon, Ziehen zieht den Sektor bis UhrBis auf.
/// Komplett theme-faehig (DynamicResource/StatusColors) — im Gegensatz zum alten
/// hartkodierten ClockPickerControl. Werteformat kompatibel: Stunden als Text ("10").
/// </summary>
public sealed class RohrquerschnittControl : Canvas
{
    public static readonly DependencyProperty UhrVonProperty =
        DependencyProperty.Register(
            nameof(UhrVon), typeof(string), typeof(RohrquerschnittControl),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((RohrquerschnittControl)d).Rebuild()));

    public static readonly DependencyProperty UhrBisProperty =
        DependencyProperty.Register(
            nameof(UhrBis), typeof(string), typeof(RohrquerschnittControl),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((RohrquerschnittControl)d).Rebuild()));

    public static readonly DependencyProperty SeverityLevelProperty =
        DependencyProperty.Register(
            nameof(SeverityLevel), typeof(int), typeof(RohrquerschnittControl),
            new FrameworkPropertyMetadata(0, (d, _) => ((RohrquerschnittControl)d).Rebuild()));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(RohrquerschnittControl),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty AllowRangeProperty =
        DependencyProperty.Register(
            nameof(AllowRange), typeof(bool), typeof(RohrquerschnittControl),
            new FrameworkPropertyMetadata(true, (d, _) => ((RohrquerschnittControl)d).Rebuild()));

    private bool _dragging;
    private int? _hoverHour;
    private Action<string>? _themeHandler;

    public RohrquerschnittControl()
    {
        MinWidth = 150;
        MinHeight = 150;
        Background = Brushes.Transparent; // Hit-Test auf der ganzen Flaeche
        SizeChanged += (_, _) => Rebuild();

        MouseLeftButtonDown += OnRingMouseDown;
        MouseMove += OnRingMouseMove;
        MouseLeftButtonUp += OnRingMouseUp;
        MouseLeave += (_, _) => { _hoverHour = null; if (!_dragging) Rebuild(); };

        // Theme-Wechsel neu zeichnen; statisches Event -> bei Unloaded wieder abbestellen.
        Loaded += (_, _) =>
        {
            _themeHandler ??= _ => Dispatcher.Invoke(Rebuild);
            ThemeManager.ThemeChanged += _themeHandler;
            Rebuild();
        };
        Unloaded += (_, _) =>
        {
            if (_themeHandler is not null)
                ThemeManager.ThemeChanged -= _themeHandler;
        };
    }

    public string UhrVon
    {
        get => (string)GetValue(UhrVonProperty);
        set => SetValue(UhrVonProperty, value);
    }

    public string UhrBis
    {
        get => (string)GetValue(UhrBisProperty);
        set => SetValue(UhrBisProperty, value);
    }

    /// <summary>1..5 faerbt den Sektor in der Severity-Rampe; 0 = Akzentfarbe.</summary>
    public int SeverityLevel
    {
        get => (int)GetValue(SeverityLevelProperty);
        set => SetValue(SeverityLevelProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>false = nur Einzelposition (UhrVon), Ziehen setzt kein UhrBis.</summary>
    public bool AllowRange
    {
        get => (bool)GetValue(AllowRangeProperty);
        set => SetValue(AllowRangeProperty, value);
    }

    // ═══════════════ Interaktion ═══════════════

    private void OnRingMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly) return;
        var stunde = HourAt(e.GetPosition(this));
        if (stunde is null) return;

        // Neuer Aufzug: Von setzen, Bis zuruecksetzen. SetCurrentValue erhaelt Bindings.
        SetCurrentValue(UhrVonProperty, ClockSectorMath.FormatHour(stunde.Value));
        SetCurrentValue(UhrBisProperty, string.Empty);
        _dragging = AllowRange;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnRingMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(this);
        if (_dragging)
        {
            var stunde = HourAt(position, unbegrenzt: true);
            if (stunde is null) return;

            var von = ClockSectorMath.ParseHour(UhrVon);
            var neuerText = von is not null && stunde.Value == von.Value
                ? string.Empty // zurueck auf Einzelposition
                : ClockSectorMath.FormatHour(stunde.Value);
            if (!string.Equals(UhrBis, neuerText, StringComparison.Ordinal))
                SetCurrentValue(UhrBisProperty, neuerText);
            return;
        }

        // Dezente Hover-Rueckmeldung auf dem Ring.
        var hover = IsReadOnly ? null : HourAt(position);
        if (hover != _hoverHour)
        {
            _hoverHour = hover;
            Cursor = hover is null ? Cursors.Arrow : Cursors.Hand;
            Rebuild();
        }
    }

    private void OnRingMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
    }

    /// <summary>Stunde unter dem Punkt, oder null wenn zu weit vom Ring entfernt.</summary>
    private int? HourAt(Point p, bool unbegrenzt = false)
    {
        var (center, outerR, innerR) = Layout();
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        var distanz = Math.Sqrt(dx * dx + dy * dy);
        if (!unbegrenzt && (distanz < innerR * 0.55 || distanz > outerR * 1.25))
            return null;

        var winkel = Math.Atan2(dy, dx) * 180d / Math.PI;
        return ClockSectorMath.AngleToHour(winkel);
    }

    // ═══════════════ Zeichnen ═══════════════

    private (Point Center, double OuterR, double InnerR) Layout()
    {
        var size = Math.Min(
            double.IsNaN(ActualWidth) || ActualWidth <= 0 ? MinWidth : ActualWidth,
            double.IsNaN(ActualHeight) || ActualHeight <= 0 ? MinHeight : ActualHeight);
        var center = new Point(size / 2d, size / 2d);
        var outerR = Math.Max(30d, size / 2d - 18d); // Platz fuer Stundenzahlen aussen
        var innerR = outerR * 0.62d;
        return (center, outerR, innerR);
    }

    private void Rebuild()
    {
        Children.Clear();
        var (center, outerR, innerR) = Layout();

        var borderBrush = ResolveBrush("BorderBrush", Color.FromRgb(0x9E, 0xAE, 0xC4));
        var mutedBrush = ResolveBrush("MutedBrush", Color.FromRgb(0x3D, 0x4D, 0x63));
        var textBrush = ResolveBrush("TextBrush", Color.FromRgb(0x0A, 0x0F, 0x1A));

        // Gestrichelte Doppelringe — die Formsprache des KI-Uhrlage-Rings.
        AddRing(center, outerR, borderBrush, 1.4, dashed: true);
        AddRing(center, innerR, borderBrush, 1.0, dashed: true);

        // 12 Stunden-Ticks + Zahlen (12/3/6/9 betont).
        for (var stunde = 1; stunde <= 12; stunde++)
        {
            var winkel = ClockSectorMath.HourToAngle(stunde);
            var rad = winkel * Math.PI / 180d;
            var richtungX = Math.Cos(rad);
            var richtungY = Math.Sin(rad);

            var haupt = stunde % 3 == 0;
            Children.Add(new Line
            {
                X1 = center.X + richtungX * (outerR - (haupt ? 7 : 4)),
                Y1 = center.Y + richtungY * (outerR - (haupt ? 7 : 4)),
                X2 = center.X + richtungX * (outerR + 3),
                Y2 = center.Y + richtungY * (outerR + 3),
                Stroke = haupt ? textBrush : mutedBrush,
                StrokeThickness = haupt ? 1.6 : 1.0
            });

            var zahl = new TextBlock
            {
                Text = ClockSectorMath.FormatHour(stunde),
                FontSize = haupt ? 12 : 10,
                FontWeight = haupt ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = haupt ? textBrush : mutedBrush
            };
            zahl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            SetLeft(zahl, center.X + richtungX * (outerR + 11) - zahl.DesiredSize.Width / 2d);
            SetTop(zahl, center.Y + richtungY * (outerR + 11) - zahl.DesiredSize.Height / 2d);
            Children.Add(zahl);
        }

        // Hover-Vorschau: zarter Punkt an der Stunde unterm Cursor.
        if (_hoverHour is int hover)
            AddMarker(center, (outerR + innerR) / 2d, hover, mutedBrush, 3.5);

        DrawSektor(center, outerR, innerR);
        DrawZentrumText(center, textBrush, mutedBrush);
    }

    private void DrawSektor(Point center, double outerR, double innerR)
    {
        var von = ClockSectorMath.ParseHour(UhrVon);
        if (von is null)
            return;

        var farbe = SeverityLevel is >= 1 and <= 5
            ? StatusColors.Current.Severity(SeverityLevel)
            : ResolveColor("AccentBrush", Color.FromRgb(0x25, 0x63, 0xEB));

        var fill = new SolidColorBrush(farbe) { Opacity = 0.32 };
        var stroke = new SolidColorBrush(farbe);

        var bis = AllowRange ? ClockSectorMath.ParseHour(UhrBis) : null;
        double startDeg, sweepDeg;
        if (bis is null || bis.Value == von.Value)
        {
            // Einzelposition: schmaler Keil, auf der Stunde zentriert.
            startDeg = ClockSectorMath.HourToAngle(von.Value) - 10d;
            sweepDeg = 20d;
        }
        else
        {
            startDeg = ClockSectorMath.HourToAngle(von.Value);
            sweepDeg = ClockSectorMath.SweepDegrees(von.Value, bis.Value);
        }

        Children.Add(new Path
        {
            Data = RingSectorGeometry.Build(center.X, center.Y, innerR + 2, outerR - 2, startDeg, sweepDeg),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 1.8,
            IsHitTestVisible = false
        });

        // Marker-Punkte an Von/Bis (mittlerer Radius) — wie die Befund-Dots im LiveFrame.
        var markerBrush = new SolidColorBrush(farbe);
        AddMarker(center, (outerR + innerR) / 2d, von.Value, markerBrush, 4.5);
        if (bis is not null && bis.Value != von.Value)
            AddMarker(center, (outerR + innerR) / 2d, bis.Value, markerBrush, 4.5);
    }

    private void AddMarker(Point center, double radius, int stunde, Brush brush, double groesse)
    {
        var rad = ClockSectorMath.HourToAngle(stunde) * Math.PI / 180d;
        var punkt = new Ellipse
        {
            Width = groesse * 2,
            Height = groesse * 2,
            Fill = brush,
            IsHitTestVisible = false
        };
        SetLeft(punkt, center.X + Math.Cos(rad) * radius - groesse);
        SetTop(punkt, center.Y + Math.Sin(rad) * radius - groesse);
        Children.Add(punkt);
    }

    private void DrawZentrumText(Point center, Brush textBrush, Brush mutedBrush)
    {
        var von = ClockSectorMath.ParseHour(UhrVon);
        var bis = AllowRange ? ClockSectorMath.ParseHour(UhrBis) : null;

        var wert = von is null
            ? "–"
            : bis is null || bis.Value == von.Value
                ? ClockSectorMath.FormatHour(von.Value)
                : $"{ClockSectorMath.FormatHour(von.Value)} – {ClockSectorMath.FormatHour(bis.Value)}";

        var panel = new StackPanel { IsHitTestVisible = false };
        panel.Children.Add(new TextBlock
        {
            Text = wert,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = von is null ? mutedBrush : textBrush,
            TextAlignment = TextAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Uhr",
            FontSize = 9,
            Foreground = mutedBrush,
            TextAlignment = TextAlignment.Center
        });

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(panel, center.X - panel.DesiredSize.Width / 2d);
        SetTop(panel, center.Y - panel.DesiredSize.Height / 2d);
        Children.Add(panel);
    }

    private void AddRing(Point center, double radius, Brush stroke, double dicke, bool dashed)
    {
        var ring = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = stroke,
            StrokeThickness = dicke,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        if (dashed)
            ring.StrokeDashArray = new DoubleCollection { 3d, 3d };
        SetLeft(ring, center.X - radius);
        SetTop(ring, center.Y - radius);
        Children.Add(ring);
    }

    private Brush ResolveBrush(string key, Color fallback)
        => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private Color ResolveColor(string brushKey, Color fallback)
        => TryFindResource(brushKey) is SolidColorBrush solid ? solid.Color : fallback;
}
