using System;
using System.Collections.Generic;
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
    private const double CanvasCenterX = 210;
    private const double CanvasCenterY = 260;
    private const double ProjectionScale = 130;
    private const double CameraDistance = 4.3;

    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _pulseTimer;
    private readonly DispatcherTimer _frameTimer;
    private readonly Random _rng = new(43);
    private readonly List<NeuralNode> _nodes = new();
    private readonly List<List<int>> _layers = new();
    private readonly List<NeuralConnection> _connections = new();
    private readonly List<ActivePulse> _activePulses = new();
    private readonly double[] _screenX = new double[64];
    private readonly double[] _screenY = new double[64];
    private readonly double[] _screenDepth = new double[64];

    private Ellipse? _volumeGlow;
    private Ellipse? _volumeShell;
    private Ellipse? _backPlane;
    private int _statusIndex;
    private int _pulseCursor;
    private double _rotationY = -0.52;
    private double _rotationX = 0.18;
    private double _breathPhase;

    private static readonly Color PrimaryAccent = Color.FromRgb(0x7E, 0xA7, 0xC9);
    private static readonly Color SecondaryAccent = Color.FromRgb(0x8B, 0xC2, 0xB6);
    private static readonly Color LineAccent = Color.FromRgb(0x7A, 0x91, 0xAA);
    private static readonly Color NodeCore = Color.FromRgb(0xE5, 0xEC, 0xF3);
    private static readonly Color ReadyAccent = Color.FromRgb(0x8B, 0xC2, 0xB6);

    private sealed class NeuralNode
    {
        public NeuralNode(double x, double y, double z, int layer, Ellipse visual)
        {
            X = x;
            Y = y;
            Z = z;
            Layer = layer;
            Visual = visual;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public int Layer { get; }
        public Ellipse Visual { get; }
        public double Activation { get; set; }
    }

    private sealed class NeuralConnection
    {
        public NeuralConnection(int a, int b, Line visual)
        {
            A = a;
            B = b;
            Visual = visual;
        }

        public int A { get; }
        public int B { get; }
        public Line Visual { get; }
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

    private static readonly string[] StatusMessages =
    [
        "Initialisiere Anwendung...",
        "Lokale KI-Modelle vorbereiten...",
        "3D-Neuralnetz synchronisieren...",
        "VSA-Kataloge und Wissensbasis verbinden...",
        "SewerStudio v4.3 bereit"
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

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(620) };
        _pulseTimer.Tick += OnPulseTick;

        _frameTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _frameTimer.Tick += OnFrameTick;

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            try { _statusTimer.Stop(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Splash.Closed] StatusTimer: {ex.Message}"); }
            try { _pulseTimer.Stop(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Splash.Closed] PulseTimer: {ex.Message}"); }
            try { _frameTimer.Stop(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Splash.Closed] FrameTimer: {ex.Message}"); }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var windowFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, windowFade);

        BuildNeuralNetwork();
        RenderFrame();
        AnimateNetworkFadeIn();
        RevealTitle(2400);
        FadeIn(SubText, 3000, 900);
        FadeIn(VersionText, 3350, 700);
        FadeIn(StatusText, 1100, 650);
        FadeIn(StatusDot, 1100, 650);
        StartProgressBar();

        _frameTimer.Start();
        _statusTimer.Start();

        var pulseDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2100) };
        pulseDelay.Tick += (_, _) =>
        {
            pulseDelay.Stop();
            _pulseTimer.Start();
        };
        pulseDelay.Start();
    }

    private void BuildNeuralNetwork()
    {
        NeuralCanvas.Children.Clear();
        _nodes.Clear();
        _layers.Clear();
        _connections.Clear();
        _activePulses.Clear();

        AddVolumeShell();

        AddLayer(-1.45, 10, 0);
        AddLayer(-0.48, 15, 1);
        AddLayer(0.48, 15, 2);
        AddLayer(1.45, 9, 3);

        for (int i = 0; i < _layers.Count - 1; i++)
            ConnectAdjacentLayers(_layers[i], _layers[i + 1], 4);

        ConnectSkipLayer(_layers[0], _layers[2], 5);
        ConnectSkipLayer(_layers[1], _layers[3], 5);
    }

    private void AddVolumeShell()
    {
        _volumeGlow = new Ellipse
        {
            Width = 420,
            Height = 350,
            Opacity = 0,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.46, 0.42),
                RadiusX = 0.58,
                RadiusY = 0.58,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(34, PrimaryAccent.R, PrimaryAccent.G, PrimaryAccent.B), 0.0),
                    new GradientStop(Color.FromArgb(12, SecondaryAccent.R, SecondaryAccent.G, SecondaryAccent.B), 0.55),
                    new GradientStop(Color.FromArgb(0, PrimaryAccent.R, PrimaryAccent.G, PrimaryAccent.B), 1.0)
                }
            },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_volumeGlow, CanvasCenterX - 210);
        Canvas.SetTop(_volumeGlow, CanvasCenterY - 175);
        Panel.SetZIndex(_volumeGlow, 0);
        NeuralCanvas.Children.Add(_volumeGlow);

        _backPlane = new Ellipse
        {
            Width = 330,
            Height = 236,
            Opacity = 0,
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromArgb(34, PrimaryAccent.R, PrimaryAccent.G, PrimaryAccent.B)),
            StrokeThickness = 0.8,
            StrokeDashArray = [8, 14],
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new SkewTransform(-14, 0),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_backPlane, CanvasCenterX - 165);
        Canvas.SetTop(_backPlane, CanvasCenterY - 118);
        Panel.SetZIndex(_backPlane, 0);
        NeuralCanvas.Children.Add(_backPlane);

        _volumeShell = new Ellipse
        {
            Width = 368,
            Height = 294,
            Opacity = 0,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.42, 0.38),
                GradientOrigin = new Point(0.32, 0.28),
                RadiusX = 0.9,
                RadiusY = 0.85,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(18, NodeCore.R, NodeCore.G, NodeCore.B), 0.0),
                    new GradientStop(Color.FromArgb(7, PrimaryAccent.R, PrimaryAccent.G, PrimaryAccent.B), 0.55),
                    new GradientStop(Color.FromArgb(0, PrimaryAccent.R, PrimaryAccent.G, PrimaryAccent.B), 1.0)
                }
            },
            Stroke = new SolidColorBrush(Color.FromArgb(54, PrimaryAccent.R, PrimaryAccent.G, PrimaryAccent.B)),
            StrokeThickness = 0.9,
            Effect = new DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 0,
                Color = PrimaryAccent,
                Opacity = 0.18
            },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_volumeShell, CanvasCenterX - 184);
        Canvas.SetTop(_volumeShell, CanvasCenterY - 147);
        Panel.SetZIndex(_volumeShell, 0);
        NeuralCanvas.Children.Add(_volumeShell);
    }

    private void AddLayer(double x, int count, int layer)
    {
        var indices = new List<int>(count + 2);
        var angleOffset = -Math.PI / 2 + layer * 0.22;
        var yRadius = 0.98 - layer * 0.035;
        var zRadius = 0.56 + layer * 0.035;

        for (int i = 0; i < count; i++)
        {
            var angle = angleOffset + i * Math.PI * 2 / count;
            var y = Math.Sin(angle) * yRadius;
            var z = Math.Cos(angle) * zRadius;
            indices.Add(AddNode(x, y, z, layer, i % 5 == 0 ? 7.0 : 5.7));
        }

        indices.Add(AddNode(x, 0, 0, layer, 6.8));
        if (layer is 1 or 2)
        {
            indices.Add(AddNode(x, -0.36, 0.16, layer, 5.8));
            indices.Add(AddNode(x, 0.36, -0.16, layer, 5.8));
        }

        _layers.Add(indices);
    }

    private int AddNode(double x, double y, double z, int layer, double baseSize)
    {
        var accent = layer is 1 or 2 ? PrimaryAccent : SecondaryAccent;
        var node = new Ellipse
        {
            Width = baseSize,
            Height = baseSize,
            Opacity = 0,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.44, 0.42),
                GradientOrigin = new Point(0.34, 0.30),
                RadiusX = 0.7,
                RadiusY = 0.7,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(245, NodeCore.R, NodeCore.G, NodeCore.B), 0.0),
                    new GradientStop(Color.FromArgb(180, accent.R, accent.G, accent.B), 0.62),
                    new GradientStop(Color.FromArgb(52, accent.R, accent.G, accent.B), 1.0)
                }
            },
            Stroke = new SolidColorBrush(Color.FromArgb(160, accent.R, accent.G, accent.B)),
            StrokeThickness = 0.7,
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Color = accent,
                Opacity = 0.32
            },
            IsHitTestVisible = false
        };

        Panel.SetZIndex(node, 30);
        NeuralCanvas.Children.Add(node);
        _nodes.Add(new NeuralNode(x, y, z, layer, node));
        return _nodes.Count - 1;
    }

    private void ConnectAdjacentLayers(List<int> left, List<int> right, int nearestCount)
    {
        for (int i = 0; i < left.Count; i++)
        {
            var distances = new List<(double Distance, int Index)>(right.Count);
            var source = _nodes[left[i]];

            for (int j = 0; j < right.Count; j++)
            {
                var target = _nodes[right[j]];
                var dy = source.Y - target.Y;
                var dz = source.Z - target.Z;
                distances.Add((dy * dy + dz * dz, right[j]));
            }

            distances.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var links = Math.Min(nearestCount, distances.Count);
            for (int j = 0; j < links; j++)
                AddConnection(left[i], distances[j].Index, j == 0 ? (byte)44 : (byte)25, j == 0 ? 0.86 : 0.62);
        }
    }

    private void ConnectSkipLayer(List<int> left, List<int> right, int count)
    {
        if (left.Count == 0 || right.Count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var a = left[(i * 3 + 1) % left.Count];
            var b = right[(i * 5 + 2) % right.Count];
            AddConnection(a, b, 14, 0.52);
        }
    }

    private void AddConnection(int a, int b, byte alpha, double thickness)
    {
        var line = new Line
        {
            Stroke = new SolidColorBrush(Color.FromArgb(alpha, LineAccent.R, LineAccent.G, LineAccent.B)),
            StrokeThickness = thickness,
            Opacity = 0,
            IsHitTestVisible = false
        };

        Panel.SetZIndex(line, 10);
        NeuralCanvas.Children.Add(line);
        _connections.Add(new NeuralConnection(a, b, line));
    }

    private void AnimateNetworkFadeIn()
    {
        if (_volumeGlow is not null)
            FadeIn(_volumeGlow, 500, 1000);
        if (_backPlane is not null)
            FadeIn(_backPlane, 720, 1000);
        if (_volumeShell is not null)
            FadeIn(_volumeShell, 820, 1000);

        for (int i = 0; i < _connections.Count; i++)
            FadeIn(_connections[i].Visual, 900 + (i % 32) * 16, 900);

        for (int i = 0; i < _nodes.Count; i++)
            FadeIn(_nodes[i].Visual, 1100 + (i % 22) * 34, 760);
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        _rotationY += 0.008;
        _rotationX = 0.18 + Math.Sin(_rotationY * 0.7) * 0.045;
        _breathPhase += 0.035;

        foreach (var node in _nodes)
            node.Activation = Math.Max(0, node.Activation - 0.035);

        foreach (var connection in _connections)
            connection.Activation = Math.Max(0, connection.Activation - 0.045);

        for (int i = _activePulses.Count - 1; i >= 0; i--)
        {
            var pulse = _activePulses[i];
            pulse.T += pulse.Speed;
            if (pulse.T >= 1.0)
            {
                NeuralCanvas.Children.Remove(pulse.Visual);
                _activePulses.RemoveAt(i);
            }
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

        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            Project(node.X, node.Y, node.Z, cosY, sinY, cosX, sinX, out var px, out var py, out var depth, out var perspective);

            _screenX[i] = px;
            _screenY[i] = py;
            _screenDepth[i] = depth;

            var depth01 = Clamp01((depth + 1.95) / 3.9);
            var size = (4.6 + perspective * 4.2 + depth01 * 3.2) * (1.0 + node.Activation * 0.45);
            var alpha = (byte)Math.Clamp(72 + depth01 * 155 + node.Activation * 28, 0, 255);
            var color = Blend(PrimaryAccent, NodeCore, 0.32 + depth01 * 0.52 + node.Activation * 0.18);

            node.Visual.Width = size;
            node.Visual.Height = size;
            node.Visual.Opacity = 0.46 + depth01 * 0.46 + node.Activation * 0.14;
            node.Visual.Fill = CreateNodeBrush(color, alpha);
            node.Visual.Stroke = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            Canvas.SetLeft(node.Visual, px - size / 2);
            Canvas.SetTop(node.Visual, py - size / 2);
            Panel.SetZIndex(node.Visual, 30 + (int)(depth01 * 40));
        }

        foreach (var connection in _connections)
        {
            var a = connection.A;
            var b = connection.B;
            var depth01 = Clamp01((_screenDepth[a] + _screenDepth[b] + 3.9) / 7.8);
            var alpha = (byte)Math.Clamp(16 + depth01 * 70 + connection.Activation * 132, 0, 220);
            var color = Blend(LineAccent, SecondaryAccent, connection.Activation * 0.72 + depth01 * 0.18);

            connection.Visual.X1 = _screenX[a];
            connection.Visual.Y1 = _screenY[a];
            connection.Visual.X2 = _screenX[b];
            connection.Visual.Y2 = _screenY[b];
            connection.Visual.StrokeThickness = 0.45 + depth01 * 0.72 + connection.Activation * 1.35;
            connection.Visual.Stroke = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            Panel.SetZIndex(connection.Visual, 8 + (int)(depth01 * 12));
        }

        UpdateActivePulseVisuals();
        UpdateVolumeShell();
    }

    private static void Project(
        double x,
        double y,
        double z,
        double cosY,
        double sinY,
        double cosX,
        double sinX,
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
        px = CanvasCenterX + x1 * ProjectionScale * perspective;
        py = CanvasCenterY + y1 * ProjectionScale * perspective;
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
            var depth01 = Clamp01((depth + 1.95) / 3.9);
            var size = 3.6 + depth01 * 4.5;

            pulse.Visual.Width = size;
            pulse.Visual.Height = size;
            pulse.Visual.Opacity = Math.Max(0, 1.0 - Math.Abs(t - 0.5) * 1.65);
            Canvas.SetLeft(pulse.Visual, px - size / 2);
            Canvas.SetTop(pulse.Visual, py - size / 2);
            Panel.SetZIndex(pulse.Visual, 80 + (int)(depth01 * 30));
        }
    }

    private void UpdateVolumeShell()
    {
        var breath = 0.5 + 0.5 * Math.Sin(_breathPhase);

        if (_volumeShell is not null)
            _volumeShell.Opacity = 0.34 + breath * 0.08;

        if (_volumeGlow is not null)
            _volumeGlow.Opacity = 0.52 + breath * 0.13;

        if (_backPlane?.RenderTransform is SkewTransform skew)
            skew.AngleX = -14 + Math.Sin(_rotationY) * 5.5;
    }

    private static RadialGradientBrush CreateNodeBrush(Color color, byte alpha)
    {
        return new RadialGradientBrush
        {
            Center = new Point(0.44, 0.42),
            GradientOrigin = new Point(0.32, 0.28),
            RadiusX = 0.72,
            RadiusY = 0.72,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(245, NodeCore.R, NodeCore.G, NodeCore.B), 0.0),
                new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), 0.62),
                new GradientStop(Color.FromArgb(42, color.R, color.G, color.B), 1.0)
            }
        };
    }

    private void OnPulseTick(object? sender, EventArgs e)
    {
        if (_connections.Count == 0)
            return;

        for (int i = 0; i < 5; i++)
        {
            _pulseCursor = (_pulseCursor + 9 + _rng.Next(17)) % _connections.Count;
            FirePulse(_pulseCursor, i % 2 == 0 ? SecondaryAccent : PrimaryAccent);
        }
    }

    private void FirePulse(int connectionIndex, Color color)
    {
        var connection = _connections[connectionIndex];
        connection.Activation = 1.0;
        _nodes[connection.A].Activation = Math.Max(_nodes[connection.A].Activation, 0.72);
        _nodes[connection.B].Activation = 1.0;

        var particle = new Ellipse
        {
            Width = 5,
            Height = 5,
            Opacity = 0.96,
            Fill = new SolidColorBrush(Color.FromArgb(235, color.R, color.G, color.B)),
            Effect = new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.72
            },
            IsHitTestVisible = false
        };

        NeuralCanvas.Children.Add(particle);
        _activePulses.Add(new ActivePulse(connectionIndex, particle, color, 0.045 + _rng.NextDouble() * 0.018));
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
        var grow = new DoubleAnimation(0, 920, TimeSpan.FromMilliseconds(10500))
        {
            BeginTime = TimeSpan.FromMilliseconds(900),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        ProgressBar.BeginAnimation(WidthProperty, grow);
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
            _pulseTimer.Stop();
        }
        else if (_statusIndex >= 2)
        {
            StatusDot.Fill = new SolidColorBrush(SecondaryAccent);
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(PrimaryAccent);
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
        _pulseTimer.Stop();
        _frameTimer.Stop();

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
