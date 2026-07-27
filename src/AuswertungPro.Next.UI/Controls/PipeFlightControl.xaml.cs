using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Pseudo-3D-Hero: stilisierte Fahrt durch ein Kanalrohr. Konzentrische Ringe
/// laufen perspektivisch vom Fluchtpunkt auf den Betrachter zu (Technik wie die
/// Startanimation: eigene Projektion auf 2D-Canvas, kein Viewport3D, kein Paket).
/// Dauer-Animation — unterliegt <see cref="MotionSettings.ReduceMotion"/>:
/// bei reduzierter Bewegung steht ein ruhiger, statischer Tunnel.
/// Rendert nur, solange das Control sichtbar ist.
/// </summary>
public partial class PipeFlightControl : UserControl
{
    private const int RingCount = 14;
    private const int GoldEvery = 5;
    private const int SeamCount = 6;
    /// <summary>Naehe/Ferne der virtuellen Kamera — siehe <see cref="DepthToRadiusFactor"/>.</summary>
    private const double NearZ = 0.12;
    private const double FarZ = 1.30;
    /// <summary>Tiefenstrecke pro Sekunde: ein Ring braucht ~7 s vom Fluchtpunkt zum Rand.</summary>
    private const double DepthPerSecond = 0.14;
    /// <summary>Rohrquerschnitt in Perspektive: Ellipse statt Kreis.</summary>
    private const double VerticalSquash = 0.66;

    // Palette der Startanimation (Hero gehoert zur selben Bildsprache, dunkel gehalten).
    private static readonly Color RingSlate = Color.FromRgb(0x5B, 0x6B, 0x85);
    private static readonly Color RingCyan = Color.FromRgb(0x5C, 0x77, 0x88);
    private static readonly Color RingGold = Color.FromRgb(0xFF, 0xC6, 0x4B);

    private readonly List<Ellipse> _rings = new();
    private readonly List<SolidColorBrush> _ringBrushes = new();
    private readonly double[] _depths = new double[RingCount];
    private readonly List<Line> _seams = new();
    private Ellipse? _glow;

    private readonly Stopwatch _clock = new();
    private double _lastFrameSeconds;
    private double _elapsed;
    private bool _loopActive;
    private bool _built;

    public PipeFlightControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        SizeChanged += (_, _) => RenderFrame();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildTunnel();
        UpdateLoopState();
        RenderFrame();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopLoop();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        => UpdateLoopState();

    private void UpdateLoopState()
    {
        // Bei reduzierter Bewegung bleibt der statische Frame stehen (Muster NeuralSphereControl:
        // Endzustand sichtbar, keine Dauer-Animation).
        if (IsLoaded && IsVisible && !MotionSettings.ReduceMotion)
            StartLoop();
        else
            StopLoop();
    }

    private void StartLoop()
    {
        if (_loopActive)
            return;
        _lastFrameSeconds = 0;
        _clock.Restart();
        CompositionTarget.Rendering += OnRendering;
        _loopActive = true;
    }

    private void StopLoop()
    {
        if (!_loopActive)
            return;
        CompositionTarget.Rendering -= OnRendering;
        _clock.Stop();
        _loopActive = false;
    }

    private void BuildTunnel()
    {
        if (_built)
            return;
        _built = true;

        _glow = new Ellipse
        {
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(30, RingCyan.R, RingCyan.G, RingCyan.B), 0.0),
                    new GradientStop(Color.FromArgb(0, RingCyan.R, RingCyan.G, RingCyan.B), 1.0)
                }
            },
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_glow, 0);
        FlightCanvas.Children.Add(_glow);

        // Laengsnaete: statische Tunnelwand-Linien vom Fluchtpunkt nach aussen.
        for (var i = 0; i < SeamCount; i++)
        {
            var seam = new Line
            {
                Stroke = new SolidColorBrush(Color.FromArgb(12, RingSlate.R, RingSlate.G, RingSlate.B)),
                StrokeThickness = 1.0,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(seam, 1);
            FlightCanvas.Children.Add(seam);
            _seams.Add(seam);
        }

        for (var i = 0; i < RingCount; i++)
        {
            var isGold = i % GoldEvery == GoldEvery - 1;
            var brush = new SolidColorBrush(Color.FromArgb(0, RingSlate.R, RingSlate.G, RingSlate.B));
            var ring = new Ellipse
            {
                Stroke = brush,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(ring, 2 + i);
            FlightCanvas.Children.Add(ring);
            _rings.Add(ring);
            _ringBrushes.Add(brush);
            _depths[i] = (i + 1.0) / RingCount;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = _lastFrameSeconds <= 0 ? 1.0 / 60.0 : now - _lastFrameSeconds;
        _lastFrameSeconds = now;
        // Genauso wie die Startanimation: nach UI-Blockaden kein Sprung.
        dt = Math.Clamp(dt, 1.0 / 120.0, 1.0 / 24.0);
        _elapsed += dt;

        for (var i = 0; i < RingCount; i++)
        {
            _depths[i] -= DepthPerSecond * dt;
            if (_depths[i] <= 0.0)
                _depths[i] += 1.0;
        }

        RenderFrame();
    }

    private void RenderFrame()
    {
        if (!_built || _glow is null)
            return;

        var width = FlightCanvas.ActualWidth;
        var height = FlightCanvas.ActualHeight;
        if (width < 2 || height < 2)
            return;

        var maxRadius = Math.Max(width, height) * 0.62;
        // Fluchtpunkt driftet minimal — wirkt organisch statt starr zentriert.
        var vanishX = width * 0.5 + Math.Sin(_elapsed * 0.23) * width * 0.02;
        var vanishY = height * 0.44 + Math.Sin(_elapsed * 0.17 + 1.3) * height * 0.02;
        var breath = 0.5 + 0.5 * Math.Sin(_elapsed * 1.1);

        var glowSize = maxRadius * 1.4;
        _glow.Width = glowSize;
        _glow.Height = glowSize;
        Canvas.SetLeft(_glow, vanishX - glowSize / 2);
        Canvas.SetTop(_glow, vanishY - glowSize / 2);
        _glow.Opacity = 0.10 + breath * 0.06;

        for (var i = 0; i < SeamCount; i++)
        {
            var angle = (i + 0.5) * Math.PI * 2.0 / SeamCount;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle) * VerticalSquash;
            var seam = _seams[i];
            seam.X1 = vanishX + cos * maxRadius * 0.10;
            seam.Y1 = vanishY + sin * maxRadius * 0.10;
            seam.X2 = vanishX + cos * maxRadius * 1.05;
            seam.Y2 = vanishY + sin * maxRadius * 1.05;
        }

        for (var i = 0; i < RingCount; i++)
        {
            var depth = _depths[i];
            var radius = DepthToRadiusFactor(depth, NearZ, FarZ) * maxRadius;
            var isGold = i % GoldEvery == GoldEvery - 1;
            var alpha = DepthToAlpha(depth) * (isGold ? 150.0 : 90.0);

            var color = isGold ? RingGold : i % 2 == 0 ? RingSlate : RingCyan;
            _ringBrushes[i].Color = Color.FromArgb(
                (byte)Math.Clamp(alpha, 0, 255), color.R, color.G, color.B);

            var ring = _rings[i];
            ring.Width = radius * 2;
            ring.Height = radius * 2 * VerticalSquash;
            ring.StrokeThickness = 0.8 + (1.0 - depth) * 0.9;
            Canvas.SetLeft(ring, vanishX - radius);
            Canvas.SetTop(ring, vanishY - radius * VerticalSquash);
        }
    }

    /// <summary>
    /// Perspektivischer Radiusfaktor 0..1: Tiefe 0 (Kamera) = voller Radius,
    /// Tiefe 1 (Fluchtpunkt) = kleiner Rest — Verteilung wie 1/z im echten Tunnel.
    /// </summary>
    internal static double DepthToRadiusFactor(double depth, double nearZ, double farZ)
    {
        depth = Math.Clamp(depth, 0.0, 1.0);
        return nearZ / (nearZ + depth * (farZ - nearZ));
    }

    /// <summary>
    /// Sichtbarkeitskurve 0..1: Ringe blenden am Fluchtpunkt ein und kurz vor
    /// der Kamera wieder aus — kein hartes Aufpoppen an beiden Enden.
    /// </summary>
    internal static double DepthToAlpha(double depth)
    {
        depth = Math.Clamp(depth, 0.0, 1.0);
        return Math.Pow(Math.Sin(Math.PI * (1.0 - depth)), 0.7);
    }
}
