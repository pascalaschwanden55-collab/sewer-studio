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
using AuswertungPro.Next.Application.Common;

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
            catch (Exception ex)
            {
                BestEffort.ReportWarning($"[Splash.Closed] StatusTimer konnte nicht gestoppt werden: {ex.Message}");
            }
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
        => StartupSplashAnimationPolicy.Blend(from, to, amount);

    private static double Clamp01(double value)
        => StartupSplashAnimationPolicy.Clamp01(value);

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
