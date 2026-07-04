using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class StartupSplashWindow : Window
{
    private const int NodeCount = 112;
    private const int ConnectionsPerNode = 3;
    private const int MaxActivePulses = 14;
    private const int MaxActiveFlares = 3;
    private const double PulseIntervalSeconds = 0.11;
    private const double FlareIntervalSeconds = 0.78;
    private const double GoldenAngle = 2.39996322972865332;
    private const double CanvasCenterX = 220;
    private const double CanvasCenterY = 260;
    private const double ProjectionScale = 170;
    private const double CameraDistance = 4.6;
    private const double ProgressFullWidth = 920;
    private const double ProgressReadyWidth = ProgressFullWidth * 0.9;
    private static readonly TimeSpan MinimumDisplayTime = TimeSpan.FromMilliseconds(3500);
    private static readonly TimeSpan ReadyProgressDuration = TimeSpan.FromMilliseconds(260);

    private readonly DispatcherTimer _statusTimer;
    private readonly Stopwatch _animationClock = new();
    private readonly Random _rng = new(7);
    private readonly List<NeuralNode> _nodes = new();
    private readonly List<NeuralConnection> _connections = new();
    private readonly List<ActivePulse> _activePulses = new();
    private readonly List<NodeFlare> _flares = new();

    // Wird gesetzt, sobald der Fortschrittsbalken durchgelaufen ist (oder das Fenster schliesst).
    // App.xaml.cs wartet darauf, damit die Startanimation nicht abgeschnitten wird.
    private readonly TaskCompletionSource<bool> _progressDone =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _minimumDisplayDone =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private double[] _screenX = Array.Empty<double>();
    private double[] _screenY = Array.Empty<double>();
    private double[] _screenDepth = Array.Empty<double>();

    private Ellipse? _coreGlow;
    private Ellipse? _ringInner;
    private Ellipse? _ringMiddle;
    private Ellipse? _ringOuter;
    private RotateTransform? _ringInnerRotate;
    private RotateTransform? _ringMiddleRotate;
    private RotateTransform? _ringOuterRotate;
    private ScaleTransform? _coreGlowScale;

    private int _statusIndex;
    private int _pulseColorCursor;
    private double _rotationY;
    private double _rotationX = 0.22;
    private double _rotationZ;
    private double _breathPhase;
    private double _lastFrameSeconds;
    private double _pulseAccumulator;
    private double _flareAccumulator;
    private bool _renderLoopActive;
    private bool _emitPulses;
    private bool _skipRequested;
    private bool _readySignaled;
    private Task? _finishTask;

    private static readonly Color AccentDeep = Color.FromRgb(0x25, 0x63, 0xEB);
    private static readonly Color AccentBlue = Color.FromRgb(0x3B, 0x82, 0xF6);
    private static readonly Color AccentCyan = Color.FromRgb(0x06, 0xB6, 0xD4);
    private static readonly Color AccentTeal = Color.FromRgb(0x14, 0xB8, 0xA6);
    private static readonly Color AccentViolet = Color.FromRgb(0x8B, 0x5C, 0xF6);
    private static readonly Color NodeCore = Color.FromRgb(0xF5, 0xFA, 0xFF);
    private static readonly Color LineAccent = Color.FromRgb(0x60, 0xA5, 0xFA);
    private static readonly Color ReadyAccent = Color.FromRgb(0x14, 0xB8, 0xA6);

    private static readonly Color[] PulsePalette =
    {
        AccentBlue, AccentCyan, AccentTeal, AccentViolet, AccentDeep
    };

    private sealed class NeuralNode
    {
        public NeuralNode(double x, double y, double z, Ellipse visual, SolidColorBrush fillBrush, SolidColorBrush strokeBrush)
        {
            X = x;
            Y = y;
            Z = z;
            Visual = visual;
            FillBrush = fillBrush;
            StrokeBrush = strokeBrush;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public Ellipse Visual { get; }
        public SolidColorBrush FillBrush { get; }
        public SolidColorBrush StrokeBrush { get; }
        public double Activation { get; set; }
    }

    private sealed class NeuralConnection
    {
        public NeuralConnection(int a, int b, Line visual, SolidColorBrush strokeBrush)
        {
            A = a;
            B = b;
            Visual = visual;
            StrokeBrush = strokeBrush;
        }

        public int A { get; }
        public int B { get; }
        public Line Visual { get; }
        public SolidColorBrush StrokeBrush { get; }
        public double Activation { get; set; }
    }

    private sealed class ActivePulse
    {
        public ActivePulse(int connectionIndex, Ellipse visual, Color color, double speed)
        {
            ConnectionIndex = connectionIndex;
            Visual = visual;
            Color = color;
            Speed = speed;
        }

        public int ConnectionIndex { get; }
        public Ellipse Visual { get; }
        public Color Color { get; }
        public double T { get; set; }
        public double Speed { get; }
    }

    private sealed class NodeFlare
    {
        public NodeFlare(int nodeIndex, Ellipse visual, Color color)
        {
            NodeIndex = nodeIndex;
            Visual = visual;
            Color = color;
        }

        public int NodeIndex { get; }
        public Ellipse Visual { get; }
        public Color Color { get; }
        public double T { get; set; }
    }

    private static readonly string[] StatusMessages =
    [
        "Initialisiere Anwendung...",
        "Lokale KI-Modelle vorbereiten...",
        "3D-Neuralnetz synchronisieren...",
        "VSA-Kataloge und Wissensbasis verbinden...",
        "SewerStudio " + AppIdentity.DisplayVersion + " bereit"
    ];

    public StartupSplashWindow()
    {
        InitializeComponent();

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1900) };
        _statusTimer.Tick += OnStatusTick;

        Loaded += OnLoaded;
        MouseLeftButtonDown += (_, _) => RequestSkip();
        PreviewKeyDown += (_, _) => RequestSkip();
        Closed += (_, _) =>
        {
            _progressDone.TrySetResult(true);
            _minimumDisplayDone.TrySetResult(true);
            try { _statusTimer.Stop(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Splash.Closed] StatusTimer: {ex.Message}"); }
            StopRenderLoop();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var windowFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, windowFade);
        Focus();

        // Versionszeile aus der zentralen Versionsnummer aufbauen.
        VersionText.Text = AppIdentity.DisplayVersion + "  |  Neural Network Core  |  VSA-KEK 2020  |  Local AI";

        BuildNeuralNetwork();
        RenderFrame();
        AnimateNetworkFadeIn();
        RevealTitle(2400);
        FadeIn(SubText, 3000, 900);
        FadeIn(VersionText, 3350, 700);
        FadeIn(StatusText, 1100, 650);
        FadeIn(StatusDot, 1100, 650);
        StartProgressBar();

        StartRenderLoop();
        _statusTimer.Start();
    }

    private void StartRenderLoop()
    {
        if (_renderLoopActive)
            return;

        _emitPulses = true;
        _pulseAccumulator = 0;
        _flareAccumulator = 0;
        _lastFrameSeconds = 0;
        _animationClock.Restart();
        CompositionTarget.Rendering += OnRendering;
        _renderLoopActive = true;
    }

    private void StopRenderLoop()
    {
        if (!_renderLoopActive)
            return;

        CompositionTarget.Rendering -= OnRendering;
        _animationClock.Stop();
        _renderLoopActive = false;
        _emitPulses = false;
    }

    private void BuildNeuralNetwork()
    {
        NeuralCanvas.Children.Clear();
        _nodes.Clear();
        _connections.Clear();
        _activePulses.Clear();
        _flares.Clear();

        BuildBackdrop();
        BuildSphere();
        BuildConnections();

        _screenX = new double[_nodes.Count];
        _screenY = new double[_nodes.Count];
        _screenDepth = new double[_nodes.Count];
    }

    private void BuildBackdrop()
    {
        _coreGlowScale = new ScaleTransform(1, 1, 230, 230);
        _coreGlow = new Ellipse
        {
            Width = 460,
            Height = 460,
            Opacity = 0,
            CacheMode = new BitmapCache(),
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(72, AccentBlue.R, AccentBlue.G, AccentBlue.B), 0.0),
                    new GradientStop(Color.FromArgb(28, AccentCyan.R, AccentCyan.G, AccentCyan.B), 0.45),
                    new GradientStop(Color.FromArgb(0, AccentDeep.R, AccentDeep.G, AccentDeep.B), 1.0)
                }
            },
            RenderTransform = _coreGlowScale,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_coreGlow, CanvasCenterX - 230);
        Canvas.SetTop(_coreGlow, CanvasCenterY - 230);
        Panel.SetZIndex(_coreGlow, 0);
        NeuralCanvas.Children.Add(_coreGlow);

        _ringOuter = CreateRing(420, 18, AccentDeep, out _ringOuterRotate);
        _ringMiddle = CreateRing(340, 14, AccentBlue, out _ringMiddleRotate);
        _ringInner = CreateRing(260, 10, AccentCyan, out _ringInnerRotate);
    }

    private Ellipse CreateRing(double diameter, byte alpha, Color color, out RotateTransform rotate)
    {
        rotate = new RotateTransform(0, diameter / 2, diameter / 2);
        var ring = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Opacity = 0,
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)),
            StrokeThickness = 0.9,
            StrokeDashArray = { 2, 6, 1, 9 },
            RenderTransform = rotate,
            CacheMode = new BitmapCache(),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(ring, CanvasCenterX - diameter / 2);
        Canvas.SetTop(ring, CanvasCenterY - diameter / 2);
        Panel.SetZIndex(ring, 1);
        NeuralCanvas.Children.Add(ring);
        return ring;
    }

    private void BuildSphere()
    {
        for (int i = 0; i < NodeCount; i++)
        {
            var y = 1.0 - (i / (double)(NodeCount - 1)) * 2.0;
            var radius = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
            var theta = GoldenAngle * i;
            var x = Math.Cos(theta) * radius;
            var z = Math.Sin(theta) * radius;

            var visual = CreateNodeVisual(out var fillBrush, out var strokeBrush);
            Panel.SetZIndex(visual, 30);
            NeuralCanvas.Children.Add(visual);
            _nodes.Add(new NeuralNode(x, y, z, visual, fillBrush, strokeBrush));
        }
    }

    private static Ellipse CreateNodeVisual(out SolidColorBrush fillBrush, out SolidColorBrush strokeBrush)
    {
        fillBrush = new SolidColorBrush(Color.FromArgb(190, AccentBlue.R, AccentBlue.G, AccentBlue.B));
        strokeBrush = new SolidColorBrush(Color.FromArgb(180, AccentBlue.R, AccentBlue.G, AccentBlue.B));

        return new Ellipse
        {
            Width = 6,
            Height = 6,
            Opacity = 0,
            Fill = fillBrush,
            Stroke = strokeBrush,
            StrokeThickness = 0.6,
            IsHitTestVisible = false
        };
    }

    private void BuildConnections()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            var source = _nodes[i];
            var distances = new List<(double Distance, int Index)>(_nodes.Count - 1);
            for (int j = 0; j < _nodes.Count; j++)
            {
                if (i == j)
                    continue;
                var target = _nodes[j];
                var dx = source.X - target.X;
                var dy = source.Y - target.Y;
                var dz = source.Z - target.Z;
                distances.Add((dx * dx + dy * dy + dz * dz, j));
            }

            distances.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var links = Math.Min(ConnectionsPerNode, distances.Count);
            for (int k = 0; k < links; k++)
            {
                var b = distances[k].Index;
                if (b > i)
                    AddConnection(i, b);
            }
        }
    }

    private void AddConnection(int a, int b)
    {
        var strokeBrush = new SolidColorBrush(Color.FromArgb(40, LineAccent.R, LineAccent.G, LineAccent.B));
        var line = new Line
        {
            Stroke = strokeBrush,
            StrokeThickness = 0.6,
            Opacity = 0,
            IsHitTestVisible = false
        };

        Panel.SetZIndex(line, 10);
        NeuralCanvas.Children.Add(line);
        _connections.Add(new NeuralConnection(a, b, line, strokeBrush));
    }

    private void AnimateNetworkFadeIn()
    {
        if (_coreGlow is not null)
            FadeIn(_coreGlow, 400, 1200);
        if (_ringOuter is not null)
            FadeIn(_ringOuter, 600, 1200);
        if (_ringMiddle is not null)
            FadeIn(_ringMiddle, 720, 1200);
        if (_ringInner is not null)
            FadeIn(_ringInner, 840, 1200);

        for (int i = 0; i < _connections.Count; i++)
            FadeIn(_connections[i].Visual, 900 + (i % 40) * 14, 800);

        for (int i = 0; i < _nodes.Count; i++)
            FadeIn(_nodes[i].Visual, 1100 + (i % 28) * 26, 700);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _animationClock.Elapsed.TotalSeconds;
        var dt = _lastFrameSeconds <= 0 ? 1.0 / 60.0 : now - _lastFrameSeconds;
        _lastFrameSeconds = now;

        // Startup can briefly block while services initialize. Cap the delta so the
        // network continues smoothly instead of jumping after a delayed UI frame.
        dt = Math.Clamp(dt, 1.0 / 120.0, 1.0 / 24.0);
        AdvanceFrame(dt);
    }

    private void AdvanceFrame(double dt)
    {
        _rotationY += 0.42 * dt;
        _rotationX = 0.22 + Math.Sin(_rotationY * 0.45) * 0.08;
        _rotationZ = Math.Sin(_rotationY * 0.31) * 0.10;
        _breathPhase += 1.35 * dt;

        if (_ringOuterRotate is not null)
            _ringOuterRotate.Angle += 5.4 * dt;
        if (_ringMiddleRotate is not null)
            _ringMiddleRotate.Angle -= 8.1 * dt;
        if (_ringInnerRotate is not null)
            _ringInnerRotate.Angle += 12.6 * dt;

        foreach (var node in _nodes)
            node.Activation = Math.Max(0, node.Activation - 1.2 * dt);

        foreach (var connection in _connections)
            connection.Activation = Math.Max(0, connection.Activation - 1.5 * dt);

        if (_emitPulses && _connections.Count > 0)
        {
            _pulseAccumulator += dt;
            while (_pulseAccumulator >= PulseIntervalSeconds && _activePulses.Count < MaxActivePulses)
            {
                _pulseAccumulator -= PulseIntervalSeconds;
                var idx = _rng.Next(_connections.Count);
                var color = PulsePalette[_pulseColorCursor % PulsePalette.Length];
                _pulseColorCursor++;
                FirePulse(idx, color);
            }
        }

        for (int i = _activePulses.Count - 1; i >= 0; i--)
        {
            var pulse = _activePulses[i];
            pulse.T += pulse.Speed * dt * 30.0;
            if (pulse.T >= 1.0)
            {
                NeuralCanvas.Children.Remove(pulse.Visual);
                _activePulses.RemoveAt(i);
            }
        }

        for (int i = _flares.Count - 1; i >= 0; i--)
        {
            var flare = _flares[i];
            flare.T += 0.9 * dt;
            if (flare.T >= 1.0)
            {
                NeuralCanvas.Children.Remove(flare.Visual);
                _flares.RemoveAt(i);
            }
        }

        _flareAccumulator += dt;
        if (_emitPulses && _flareAccumulator >= FlareIntervalSeconds && _nodes.Count > 0 && _flares.Count < MaxActiveFlares)
        {
            _flareAccumulator -= FlareIntervalSeconds;
            SpawnFlare(_rng.Next(_nodes.Count));
        }

        RenderFrame();
    }

    private void RenderFrame()
    {
        if (_nodes.Count == 0)
            return;

        var cosY = Math.Cos(_rotationY);
        var sinY = Math.Sin(_rotationY);
        var cosX = Math.Cos(_rotationX);
        var sinX = Math.Sin(_rotationX);
        var cosZ = Math.Cos(_rotationZ);
        var sinZ = Math.Sin(_rotationZ);

        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            Project(node.X, node.Y, node.Z,
                cosY, sinY, cosX, sinX, cosZ, sinZ,
                out var px, out var py, out var depth, out var perspective);

            _screenX[i] = px;
            _screenY[i] = py;
            _screenDepth[i] = depth;

            var depth01 = Clamp01((depth + 1.0) / 2.0);
            var size = (4.0 + perspective * 3.4 + depth01 * 3.0) * (1.0 + node.Activation * 0.7);
            var alpha = (byte)Math.Clamp(80 + depth01 * 150 + node.Activation * 40, 0, 255);
            var color = Blend(AccentBlue, NodeCore, 0.22 + depth01 * 0.55 + node.Activation * 0.30);

            node.Visual.Width = size;
            node.Visual.Height = size;
            node.Visual.Opacity = 0.40 + depth01 * 0.50 + node.Activation * 0.15;
            node.FillBrush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
            node.StrokeBrush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
            Canvas.SetLeft(node.Visual, px - size / 2);
            Canvas.SetTop(node.Visual, py - size / 2);
            Panel.SetZIndex(node.Visual, 30 + (int)(depth01 * 40));
        }

        foreach (var connection in _connections)
        {
            var a = connection.A;
            var b = connection.B;
            var depth01 = Clamp01((_screenDepth[a] + _screenDepth[b] + 2.0) / 4.0);
            var alpha = (byte)Math.Clamp(18 + depth01 * 70 + connection.Activation * 160, 0, 235);
            var color = Blend(LineAccent, AccentCyan, connection.Activation * 0.85 + depth01 * 0.20);

            connection.Visual.X1 = _screenX[a];
            connection.Visual.Y1 = _screenY[a];
            connection.Visual.X2 = _screenX[b];
            connection.Visual.Y2 = _screenY[b];
            connection.Visual.StrokeThickness = 0.45 + depth01 * 0.6 + connection.Activation * 1.7;
            connection.StrokeBrush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
            Panel.SetZIndex(connection.Visual, 8 + (int)(depth01 * 12));
        }

        UpdateActivePulseVisuals();
        UpdateFlareVisuals();
        UpdateBackdrop();
    }

    private static void Project(
        double x,
        double y,
        double z,
        double cosY,
        double sinY,
        double cosX,
        double sinX,
        double cosZ,
        double sinZ,
        out double px,
        out double py,
        out double depth,
        out double perspective)
    {
        var x1 = x * cosY + z * sinY;
        var z1 = -x * sinY + z * cosY;
        var y1 = y * cosX - z1 * sinX;
        var z2 = y * sinX + z1 * cosX;

        perspective = CameraDistance / (CameraDistance - z2);
        var sx = x1 * ProjectionScale * perspective;
        var sy = y1 * ProjectionScale * perspective;

        px = CanvasCenterX + sx * cosZ - sy * sinZ;
        py = CanvasCenterY + sx * sinZ + sy * cosZ;
        depth = z2;
    }

    private void UpdateActivePulseVisuals()
    {
        foreach (var pulse in _activePulses)
        {
            if (pulse.ConnectionIndex < 0 || pulse.ConnectionIndex >= _connections.Count)
                continue;

            var connection = _connections[pulse.ConnectionIndex];
            var a = connection.A;
            var b = connection.B;
            var t = pulse.T;
            var px = _screenX[a] + (_screenX[b] - _screenX[a]) * t;
            var py = _screenY[a] + (_screenY[b] - _screenY[a]) * t;
            var depth = _screenDepth[a] + (_screenDepth[b] - _screenDepth[a]) * t;
            var depth01 = Clamp01((depth + 1.0) / 2.0);
            var size = 4.5 + depth01 * 5.0;

            pulse.Visual.Width = size;
            pulse.Visual.Height = size;
            pulse.Visual.Opacity = Math.Max(0, 1.0 - Math.Abs(t - 0.5) * 1.7);
            Canvas.SetLeft(pulse.Visual, px - size / 2);
            Canvas.SetTop(pulse.Visual, py - size / 2);
            Panel.SetZIndex(pulse.Visual, 80 + (int)(depth01 * 30));
        }
    }

    private void UpdateFlareVisuals()
    {
        foreach (var flare in _flares)
        {
            if (flare.NodeIndex < 0 || flare.NodeIndex >= _nodes.Count)
                continue;

            var px = _screenX[flare.NodeIndex];
            var py = _screenY[flare.NodeIndex];
            var t = flare.T;
            var size = 8.0 + t * 60.0;
            var opacity = Math.Max(0, 0.85 * (1.0 - t));

            flare.Visual.Width = size;
            flare.Visual.Height = size;
            flare.Visual.Opacity = opacity;
            flare.Visual.StrokeThickness = Math.Max(0.4, 2.4 * (1.0 - t));
            Canvas.SetLeft(flare.Visual, px - size / 2);
            Canvas.SetTop(flare.Visual, py - size / 2);
        }
    }

    private void UpdateBackdrop()
    {
        var breath = 0.5 + 0.5 * Math.Sin(_breathPhase);

        if (_coreGlow is not null)
        {
            _coreGlow.Opacity = 0.55 + breath * 0.30;
            var scale = 0.92 + breath * 0.10;
            if (_coreGlowScale is not null)
            {
                _coreGlowScale.ScaleX = scale;
                _coreGlowScale.ScaleY = scale;
            }
        }

        if (_ringOuter is not null)
            _ringOuter.Opacity = 0.30 + breath * 0.18;
        if (_ringMiddle is not null)
            _ringMiddle.Opacity = 0.42 + breath * 0.20;
        if (_ringInner is not null)
            _ringInner.Opacity = 0.55 + breath * 0.22;
    }

    private void FirePulse(int connectionIndex, Color color)
    {
        var connection = _connections[connectionIndex];
        connection.Activation = 1.0;
        _nodes[connection.A].Activation = Math.Max(_nodes[connection.A].Activation, 0.85);
        _nodes[connection.B].Activation = 1.0;

        var particle = new Ellipse
        {
            Width = 6,
            Height = 6,
            Opacity = 0.95,
            Fill = new SolidColorBrush(Color.FromArgb(245, color.R, color.G, color.B)),
            Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.85
            },
            IsHitTestVisible = false
        };
        Panel.SetZIndex(particle, 90);
        NeuralCanvas.Children.Add(particle);

        var speed = 0.038 + _rng.NextDouble() * 0.026;
        _activePulses.Add(new ActivePulse(connectionIndex, particle, color, speed));
    }

    private void SpawnFlare(int nodeIndex)
    {
        var color = PulsePalette[_rng.Next(PulsePalette.Length)];
        _nodes[nodeIndex].Activation = 1.0;

        var ring = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            StrokeThickness = 2.4,
            Opacity = 0.85,
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.7
            },
            IsHitTestVisible = false
        };
        Panel.SetZIndex(ring, 70);
        NeuralCanvas.Children.Add(ring);
        _flares.Add(new NodeFlare(nodeIndex, ring, color));
    }

    private void RevealTitle(int startMs)
    {
        FadeIn(TitleText, startMs, 1000);

        var slide = new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(1000))
        {
            BeginTime = TimeSpan.FromMilliseconds(startMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        TitleSlide.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    private void StartProgressBar()
    {
        var grow = new DoubleAnimation(0, ProgressReadyWidth, MinimumDisplayTime)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // Der Balken bleibt bei 90 %, bis die Anwendung wirklich bereit ist.
        grow.Completed += (_, _) =>
        {
            _minimumDisplayDone.TrySetResult(true);
            if (_readySignaled)
                _ = CompleteProgressAsync();
        };
        ProgressBar.BeginAnimation(WidthProperty, grow);
    }

    /// <summary>
    /// Meldet dem Splash, dass der ServiceProvider steht. Danach laeuft der Balken
    /// nach der Mindestanzeigezeit auf 100 % oder endet sofort, wenn der Nutzer ueberspringt.
    /// </summary>
    public async Task SignalReadyAsync()
    {
        _readySignaled = true;

        if (_skipRequested)
        {
            ProgressBar.BeginAnimation(WidthProperty, null);
            ProgressBar.Width = ProgressFullWidth;
            _progressDone.TrySetResult(true);
            return;
        }

        await _minimumDisplayDone.Task.ConfigureAwait(true);
        await CompleteProgressAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Task, der abgeschlossen ist, sobald die Startanimation bereit zum Ausblenden ist.
    /// </summary>
    public Task WaitForProgressAsync() => _progressDone.Task;

    private void RequestSkip()
    {
        if (_progressDone.Task.IsCompleted)
            return;

        _skipRequested = true;
        StatusText.Text = "Startanimation uebersprungen...";
        ProgressBar.BeginAnimation(WidthProperty, null);
        ProgressBar.Width = ProgressFullWidth;
        _progressDone.TrySetResult(true);
    }

    private Task CompleteProgressAsync()
    {
        if (_finishTask is not null)
            return _finishTask;

        _finishTask = CompleteProgressCoreAsync();
        return _finishTask;
    }

    private async Task CompleteProgressCoreAsync()
    {
        if (_progressDone.Task.IsCompleted)
            return;

        _statusTimer.Stop();
        StatusText.Text = StatusMessages[^1];
        StatusText.Foreground = new SolidColorBrush(ReadyAccent);
        StatusDot.Fill = new SolidColorBrush(ReadyAccent);
        _emitPulses = false;
        TriggerReadyBurst();
        await AnimateProgressToAsync(ProgressFullWidth, ReadyProgressDuration).ConfigureAwait(true);
        _progressDone.TrySetResult(true);
    }

    private Task AnimateProgressToAsync(double width, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            ProgressBar.Width = width;
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation
        {
            To = width,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) => tcs.TrySetResult(null);
        ProgressBar.BeginAnimation(WidthProperty, animation, HandoffBehavior.SnapshotAndReplace);
        return tcs.Task;
    }

    private void TriggerReadyBurst()
    {
        if (_connections.Count > 0)
        {
            for (var i = 0; i < 8; i++)
                FirePulse(_rng.Next(_connections.Count), ReadyAccent);
        }

        if (_nodes.Count > 0)
        {
            for (var i = 0; i < 3; i++)
                SpawnFlare(_rng.Next(_nodes.Count));
        }

        if (_coreGlowScale is not null)
        {
            var pulse = new DoubleAnimation(1.0, 1.16, TimeSpan.FromMilliseconds(280))
            {
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _coreGlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            _coreGlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        var titleGlow = new DropShadowEffect
        {
            Color = ReadyAccent,
            BlurRadius = 0,
            ShadowDepth = 0,
            Opacity = 0
        };
        TitleText.Effect = titleGlow;
        var blur = new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(240))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var opacity = new DoubleAnimation(0, 0.65, TimeSpan.FromMilliseconds(240))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        titleGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blur);
        titleGlow.BeginAnimation(DropShadowEffect.OpacityProperty, opacity);
    }

    private void OnStatusTick(object? sender, EventArgs e)
    {
        _statusIndex++;
        if (_statusIndex >= StatusMessages.Length)
        {
            _statusTimer.Stop();
            return;
        }

        StatusText.Text = StatusMessages[_statusIndex];

        if (_statusIndex == StatusMessages.Length - 1)
        {
            StatusText.Foreground = new SolidColorBrush(ReadyAccent);
            StatusDot.Fill = new SolidColorBrush(ReadyAccent);
            _statusTimer.Stop();
            _emitPulses = false;
        }
        else if (_statusIndex >= 2)
        {
            StatusDot.Fill = new SolidColorBrush(AccentCyan);
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(AccentBlue);
        }
    }

    private static void FadeIn(UIElement element, int startMs, int durMs)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(startMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Clamp01(amount);
        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }

    private static double Clamp01(double value)
    {
        if (value < 0)
            return 0;
        if (value > 1)
            return 1;
        return value;
    }

    public Task WaitAsync(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return Task.CompletedTask;
        return Task.Delay(duration);
    }

    public Task FadeOutAndCloseAsync(TimeSpan duration)
    {
        _statusTimer.Stop();
        StopRenderLoop();

        var tcs = new TaskCompletionSource<object?>();

        if (duration <= TimeSpan.Zero)
        {
            Close();
            tcs.TrySetResult(null);
            return tcs.Task;
        }

        var opacityAnim = new DoubleAnimation
        {
            To = 0,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        opacityAnim.Completed += (_, _) =>
        {
            Close();
            tcs.TrySetResult(null);
        };

        BeginAnimation(OpacityProperty, opacityAnim);
        return tcs.Task;
    }
}
