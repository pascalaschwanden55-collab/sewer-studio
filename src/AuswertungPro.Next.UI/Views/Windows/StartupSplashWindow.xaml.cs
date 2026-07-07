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
    private const int MaxActivePulses = 42;
    private const int MaxActiveFlares = 5;
    private const int MaxCascadeGeneration = 4;
    private const double PulseIntervalSeconds = 0.09;
    private const double FlareIntervalSeconds = 0.5;
    private const double WaveIntervalSeconds = 2.7;
    private const double WaveDurationSeconds = 1.5;
    private const double WaveBandWidth = 48;
    private const double GoldenAngle = 2.39996322972865332;
    private const double CanvasCenterX = 220;
    private const double CanvasCenterY = 260;
    private const double ProjectionScale = 170;
    private const double CameraDistance = 4.6;
    private const double ProgressFullWidth = 920;
    private const double ProgressReadyWidth = ProgressFullWidth * 0.9;
    private static readonly TimeSpan MinimumDisplayTime = TimeSpan.FromMilliseconds(8000);
    private static readonly TimeSpan ReadyProgressDuration = TimeSpan.FromMilliseconds(260);

    private readonly DispatcherTimer _statusTimer;
    private readonly Stopwatch _animationClock = new();
    private readonly Random _rng = new(7);
    private readonly List<NeuralNode> _nodes = new();
    private readonly List<NeuralConnection> _connections = new();
    private readonly List<ActivePulse> _activePulses = new();
    private readonly List<NodeFlare> _flares = new();
    private readonly List<RingSatellite> _satellites = new();
    private List<int>[] _adjacency = Array.Empty<List<int>>();

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
    private Rectangle? _scanLine;

    private int _statusIndex;
    private int _pulseColorCursor;
    private double _rotationY;
    private double _rotationX = 0.22;
    private double _rotationZ;
    private double _breathPhase;
    private double _lastFrameSeconds;
    private double _pulseAccumulator;
    private double _flareAccumulator;
    // Inferenz-Welle: -1 = inaktiv, 0..1 = Fortschritt des Sweeps durch das Netz.
    private double _waveT = -1;
    private double _waveCooldown = 1.6;
    private bool _renderLoopActive;
    private bool _emitPulses;
    private bool _skipRequested;
    private bool _readySignaled;
    private Task? _finishTask;

    // Basisnetz (Knoten, Linien, Ringe) bleibt bewusst gedaempft/dunkel als Slate,
    // damit die farbigen und goldenen Impulse davor klar hervortreten.
    private static readonly Color AccentDeep = Color.FromRgb(0x33, 0x3B, 0x4B);
    private static readonly Color AccentBlue = Color.FromRgb(0x48, 0x57, 0x70);
    private static readonly Color AccentCyan = Color.FromRgb(0x5C, 0x77, 0x88);
    private static readonly Color NodeCore = Color.FromRgb(0xE6, 0xEB, 0xF0);
    private static readonly Color LineAccent = Color.FromRgb(0x74, 0x82, 0x96);
    private static readonly Color ReadyAccent = Color.FromRgb(0x86, 0xCB, 0x92);

    // Lebendige Impuls-Palette: kraeftige Farben mit Gold als wiederkehrendem Blickfang.
    private static readonly Color PulseGold = Color.FromRgb(0xFF, 0xC6, 0x4B);
    private static readonly Color PulseAmber = Color.FromRgb(0xFF, 0x9D, 0x3A);
    private static readonly Color PulseCyan = Color.FromRgb(0x3F, 0xC6, 0xF0);
    private static readonly Color PulseBlue = Color.FromRgb(0x5B, 0x8B, 0xFF);
    private static readonly Color PulseTeal = Color.FromRgb(0x33, 0xD6, 0xB8);
    private static readonly Color PulseViolet = Color.FromRgb(0xA9, 0x84, 0xFF);

    // Gold taucht mehrfach auf, damit goldene Impulse das Bild praegen, es aber bunt bleibt.
    private static readonly Color[] PulsePalette =
    {
        PulseGold, PulseAmber, PulseCyan, PulseGold, PulseBlue, PulseTeal, PulseGold, PulseViolet
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
        public ActivePulse(int connectionIndex, Ellipse visual, Color color, double speed, bool reverse, int generation)
        {
            ConnectionIndex = connectionIndex;
            Visual = visual;
            Color = color;
            Speed = speed;
            Reverse = reverse;
            Generation = generation;
        }

        public int ConnectionIndex { get; }
        public Ellipse Visual { get; }
        public Color Color { get; }
        public double T { get; set; }
        public double Speed { get; }
        // Impulse laufen entlang der Verbindung in beide Richtungen (A->B oder B->A).
        public bool Reverse { get; }
        // Kaskaden-Tiefe: ankommende Impulse feuern begrenzt weiter (Signalausbreitung).
        public int Generation { get; }
    }

    private sealed class RingSatellite
    {
        public RingSatellite(Ellipse visual, double radius, double speed, double angle)
        {
            Visual = visual;
            Radius = radius;
            Speed = speed;
            Angle = angle;
        }

        public Ellipse Visual { get; }
        public double Radius { get; }
        public double Speed { get; }
        public double Angle { get; set; }
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
        "Neural Core hochfahren...",
        "Lokale KI-Modelle vorbereiten...",
        "Synapsen kalibrieren...",
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

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1250) };
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
        RevealTitle(2600);
        FadeIn(SubText, 3400, 900);
        FadeIn(VersionText, 3900, 700);
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
        _satellites.Clear();

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
                    new GradientStop(Color.FromArgb(44, AccentBlue.R, AccentBlue.G, AccentBlue.B), 0.0),
                    new GradientStop(Color.FromArgb(16, AccentCyan.R, AccentCyan.G, AccentCyan.B), 0.45),
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

        // Scanline der Inferenz-Welle: laeuft synchron zum Aktivierungs-Sweep durchs Netz.
        _scanLine = new Rectangle
        {
            Width = 110,
            Height = 520,
            Opacity = 0,
            IsHitTestVisible = false,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, AccentCyan.R, AccentCyan.G, AccentCyan.B), 0.0),
                    new GradientStop(Color.FromArgb(46, AccentCyan.R, AccentCyan.G, AccentCyan.B), 0.5),
                    new GradientStop(Color.FromArgb(0, AccentCyan.R, AccentCyan.G, AccentCyan.B), 1.0)
                }
            }
        };
        Canvas.SetTop(_scanLine, 0);
        Canvas.SetLeft(_scanLine, CanvasCenterX - 230);
        Panel.SetZIndex(_scanLine, 3);
        NeuralCanvas.Children.Add(_scanLine);

        // Orbitierende Satelliten auf den Ringen (Radius = halber Ringdurchmesser).
        AddSatellite(210, 0.35, 0.0, AccentDeep, 5);
        AddSatellite(210, 0.35, Math.PI, AccentBlue, 4);
        AddSatellite(170, -0.55, 1.1, AccentCyan, 4);
        AddSatellite(130, 0.85, 2.4, AccentCyan, 3.5);
    }

    private void AddSatellite(double radius, double speed, double startAngle, Color color, double size)
    {
        var dot = new Ellipse
        {
            Width = size,
            Height = size,
            Opacity = 0,
            Fill = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            Effect = new DropShadowEffect
            {
                BlurRadius = 9,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.45
            },
            IsHitTestVisible = false
        };
        Panel.SetZIndex(dot, 6);
        NeuralCanvas.Children.Add(dot);
        _satellites.Add(new RingSatellite(dot, radius, speed, startAngle));
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

        // Adjazenzliste fuer die Signalausbreitung: Knoten -> abgehende Verbindungen.
        _adjacency = new List<int>[_nodes.Count];
        for (int i = 0; i < _nodes.Count; i++)
            _adjacency[i] = new List<int>();
        for (int c = 0; c < _connections.Count; c++)
        {
            _adjacency[_connections[c].A].Add(c);
            _adjacency[_connections[c].B].Add(c);
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
                OnPulseArrived(pulse);
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

        // Inferenz-Welle: periodischer Sweep, der wie ein Forward-Pass durchs Netz laeuft.
        if (_waveT >= 0)
        {
            _waveT += dt / WaveDurationSeconds;
            if (_waveT >= 1.0)
            {
                _waveT = -1;
                _waveCooldown = WaveIntervalSeconds;
                if (_scanLine is not null)
                    _scanLine.Opacity = 0;
            }
        }
        else if (_emitPulses)
        {
            _waveCooldown -= dt;
            if (_waveCooldown <= 0)
                _waveT = 0;
        }

        foreach (var satellite in _satellites)
            satellite.Angle += satellite.Speed * dt;

        RenderFrame();
    }

    private void OnPulseArrived(ActivePulse pulse)
    {
        if (pulse.ConnectionIndex < 0 || pulse.ConnectionIndex >= _connections.Count)
            return;

        var connection = _connections[pulse.ConnectionIndex];
        var nodeIndex = pulse.Reverse ? connection.A : connection.B;
        _nodes[nodeIndex].Activation = 1.0;

        // Signalausbreitung wie im echten Netz: ankommende Impulse feuern
        // mit abnehmender Wahrscheinlichkeit entlang der Synapsen weiter.
        if (!_emitPulses || pulse.Generation >= MaxCascadeGeneration)
            return;
        if (nodeIndex >= _adjacency.Length || _adjacency[nodeIndex].Count == 0)
            return;

        var outgoing = _adjacency[nodeIndex];
        var chance = 0.75 / (pulse.Generation + 1);
        var branches = _rng.NextDouble() < 0.35 ? 2 : 1;
        for (int b = 0; b < branches; b++)
        {
            if (_activePulses.Count >= MaxActivePulses)
                return;
            if (_rng.NextDouble() > chance)
                continue;

            var connIdx = outgoing[_rng.Next(outgoing.Count)];
            if (connIdx == pulse.ConnectionIndex)
                continue;

            var next = _connections[connIdx];
            var reverse = next.B == nodeIndex;
            // Kaskaden behalten ueberwiegend ihre Farbe (nur leicht Richtung Cyan), damit Gold golden bleibt.
            var color = Blend(pulse.Color, AccentCyan, 0.18);
            FirePulse(connIdx, color, reverse, pulse.Generation + 1);
        }
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

        // Position und Staerke der Inferenz-Welle (Sweep von links nach rechts).
        var waveActive = _waveT >= 0 && _waveT <= 1.0;
        var waveX = CanvasCenterX - 230 + _waveT * 460;
        var waveStrength = waveActive ? Math.Sin(Math.PI * _waveT) : 0;

        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            Project(node.X, node.Y, node.Z,
                cosY, sinY, cosX, sinX, cosZ, sinZ,
                out var px, out var py, out var depth, out var perspective);

            _screenX[i] = px;
            _screenY[i] = py;
            _screenDepth[i] = depth;

            if (waveActive)
            {
                var waveDist = Math.Abs(px - waveX);
                if (waveDist < WaveBandWidth)
                {
                    var boost = (1.0 - waveDist / WaveBandWidth) * 0.95 * waveStrength;
                    if (boost > node.Activation)
                        node.Activation = boost;
                }
            }

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

            if (waveActive)
            {
                var midX = (_screenX[a] + _screenX[b]) / 2.0;
                var waveDist = Math.Abs(midX - waveX);
                if (waveDist < WaveBandWidth)
                {
                    var boost = (1.0 - waveDist / WaveBandWidth) * 0.45 * waveStrength;
                    if (boost > connection.Activation)
                        connection.Activation = boost;
                }
            }

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
        UpdateScanLine(waveActive, waveX, waveStrength);
        UpdateSatellites();
    }

    private void UpdateScanLine(bool waveActive, double waveX, double waveStrength)
    {
        if (_scanLine is null)
            return;

        if (!waveActive)
        {
            _scanLine.Opacity = 0;
            return;
        }

        Canvas.SetLeft(_scanLine, waveX - _scanLine.Width / 2.0);
        _scanLine.Opacity = 0.9 * waveStrength;
    }

    private void UpdateSatellites()
    {
        var breath = 0.5 + 0.5 * Math.Sin(_breathPhase);
        // Weich einblenden, synchron zum Fade-in der Ringe.
        var fadeIn = Clamp01((_animationClock.Elapsed.TotalSeconds - 0.6) / 1.2);
        foreach (var satellite in _satellites)
        {
            var px = CanvasCenterX + Math.Cos(satellite.Angle) * satellite.Radius;
            var py = CanvasCenterY + Math.Sin(satellite.Angle) * satellite.Radius;
            var size = satellite.Visual.Width;
            Canvas.SetLeft(satellite.Visual, px - size / 2.0);
            Canvas.SetTop(satellite.Visual, py - size / 2.0);
            satellite.Visual.Opacity = (0.45 + breath * 0.35) * fadeIn;
        }
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
            var t = pulse.Reverse ? 1.0 - pulse.T : pulse.T;
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

    private void FirePulse(int connectionIndex, Color color, bool reverse = false, int generation = 0)
    {
        var connection = _connections[connectionIndex];
        connection.Activation = 1.0;
        var sourceIndex = reverse ? connection.B : connection.A;
        _nodes[sourceIndex].Activation = Math.Max(_nodes[sourceIndex].Activation, 0.85);

        var particle = new Ellipse
        {
            Width = 6,
            Height = 6,
            Opacity = 0.95,
            Fill = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)),
            Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.75
            },
            IsHitTestVisible = false
        };
        Panel.SetZIndex(particle, 90);
        NeuralCanvas.Children.Add(particle);

        // Kaskaden-Impulse laufen etwas schneller, damit die Ausbreitung als Kette lesbar bleibt.
        var speed = (0.038 + _rng.NextDouble() * 0.026) * (1.0 + generation * 0.15);
        _activePulses.Add(new ActivePulse(connectionIndex, particle, color, speed, reverse, generation));
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
                BlurRadius = 14,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.5
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
        // Die letzte Meldung ("bereit") setzt ausschliesslich CompleteProgressCoreAsync,
        // damit der Text nie "bereit" zeigt, bevor die Anwendung es wirklich ist.
        if (_statusIndex >= StatusMessages.Length - 2)
        {
            _statusTimer.Stop();
            return;
        }

        _statusIndex++;
        StatusText.Text = StatusMessages[_statusIndex];
        StatusDot.Fill = new SolidColorBrush(_statusIndex >= 3 ? AccentCyan : AccentBlue);
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
