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
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _pulseTimer;
    private int _statusIndex;
    private readonly Random _rng = new(42);
    private readonly List<Ellipse> _nodes = new();
    private readonly List<Line> _connections = new();

    // Neurale Netzwerk-Knoten: 4 Schichten (Input, Hidden1, Hidden2, Output)
    private static readonly double[][] LayerX = [[120, 120, 120, 120, 120],
                                                  [240, 240, 240, 240, 240, 240, 240],
                                                  [360, 360, 360, 360, 360, 360],
                                                  [480, 480, 480, 480]];
    private static readonly double[][] LayerY = [[100, 180, 260, 340, 420],
                                                  [70, 130, 190, 250, 310, 370, 430],
                                                  [100, 170, 240, 310, 380, 450],
                                                  [150, 230, 310, 390]];

    private static readonly string[] StatusMessages =
    [
        "Initialisiere Anwendung...",
        "Neuronale Netze laden...",
        "YOLO-Modell vorbereiten...",
        "Qwen Vision aktivieren...",
        "KI-Pipeline bereit"
    ];

    public StartupSplashWindow()
    {
        InitializeComponent();

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        _statusTimer.Tick += OnStatusTick;

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _pulseTimer.Tick += OnPulseTick;

        Loaded += OnLoaded;
        Closed += (_, _) => { _statusTimer.Stop(); _pulseTimer.Stop(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Fenster einblenden
        var windowFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, windowFade);

        // Neuronales Netz zeichnen
        BuildNeuralNetwork();

        // Animationen starten
        StartRingIntro();
        AnimateNetworkFadeIn();
        RevealTitle(1600);
        FadeIn(SubText, 2000, 600);
        FadeIn(VersionText, 2200, 400);
        FadeIn(StatusText, 600, 350);
        FadeIn(StatusDot, 600, 350);
        StartProgressBar();

        // Ring-Rotation (langsam, endlos)
        var ringRotate = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(20))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        RingRotation.BeginAnimation(RotateTransform.AngleProperty, ringRotate);

        // Innerer Ring (entgegengesetzt)
        FadeIn(InnerRing, 900, 500);
        var innerRotate = new DoubleAnimation(360, 0, TimeSpan.FromSeconds(15))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        InnerRotation.BeginAnimation(RotateTransform.AngleProperty, innerRotate);

        // Kern-Puls
        FadeIn(CoreDot, 700, 400);
        var corePulse = new DoubleAnimation(0.5, 1.0, TimeSpan.FromMilliseconds(1200))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        CoreDot.BeginAnimation(OpacityProperty, corePulse);

        // Ring-Glow Puls
        var ringPulse = new DoubleAnimation(0.5, 0.9, TimeSpan.FromMilliseconds(1500))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        GlowRing.BeginAnimation(OpacityProperty, ringPulse);

        _statusTimer.Start();

        // Datenpulse mit Verzoegerung starten
        _pulseTimer.IsEnabled = false;
        var pulseDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        pulseDelay.Tick += (_, _) =>
        {
            pulseDelay.Stop();
            _pulseTimer.Start();
        };
        pulseDelay.Start();
    }

    // ── Neuronales Netz aufbauen ──────────────────────────────────────

    private void BuildNeuralNetwork()
    {
        // Verbindungen zuerst (hinter den Knoten)
        for (int layer = 0; layer < LayerX.Length - 1; layer++)
        {
            for (int i = 0; i < LayerX[layer].Length; i++)
            {
                for (int j = 0; j < LayerX[layer + 1].Length; j++)
                {
                    // Nicht alle Verbindungen zeichnen — nur ~60% fuer sauberen Look
                    if (_rng.NextDouble() > 0.6) continue;

                    var line = new Line
                    {
                        X1 = LayerX[layer][i],
                        Y1 = LayerY[layer][i],
                        X2 = LayerX[layer + 1][j],
                        Y2 = LayerY[layer + 1][j],
                        Stroke = new SolidColorBrush(Color.FromArgb(18, 0, 160, 255)),
                        StrokeThickness = 1,
                        Opacity = 0
                    };
                    NeuralCanvas.Children.Add(line);
                    _connections.Add(line);
                }
            }
        }

        // Knoten zeichnen
        for (int layer = 0; layer < LayerX.Length; layer++)
        {
            for (int i = 0; i < LayerX[layer].Length; i++)
            {
                var size = layer == 0 || layer == LayerX.Length - 1 ? 8.0 : 6.0;
                var node = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Opacity = 0,
                    Fill = new RadialGradientBrush(
                        Color.FromArgb(200, 0, 180, 255),
                        Color.FromArgb(60, 0, 100, 200))
                };
                Canvas.SetLeft(node, LayerX[layer][i] - size / 2);
                Canvas.SetTop(node, LayerY[layer][i] - size / 2);
                NeuralCanvas.Children.Add(node);
                _nodes.Add(node);
            }
        }
    }

    private void AnimateNetworkFadeIn()
    {
        // Verbindungen nacheinander einblenden
        for (int i = 0; i < _connections.Count; i++)
        {
            var delay = 400 + i * 15; // Gestaffelt
            FadeIn(_connections[i], delay, 600);
        }

        // Knoten einblenden
        for (int i = 0; i < _nodes.Count; i++)
        {
            var delay = 300 + i * 40;
            FadeIn(_nodes[i], delay, 400);
        }
    }

    // ── Datenpulse durch das Netzwerk ─────────────────────────────────

    private void OnPulseTick(object? sender, EventArgs e)
    {
        if (_connections.Count == 0) return;

        // Zufaellige Verbindung auswaehlen und aufleuchten lassen
        var idx = _rng.Next(_connections.Count);
        var line = _connections[idx];

        // Puls: hell aufblitzen → zurueck zum Normalzustand
        var originalBrush = new SolidColorBrush(Color.FromArgb(18, 0, 160, 255));
        var pulseBrush = new SolidColorBrush(Color.FromArgb(120, 0, 200, 255));

        line.Stroke = pulseBrush;
        line.StrokeThickness = 2;

        var pulseAnim = new ColorAnimation(
            Color.FromArgb(120, 0, 200, 255),
            Color.FromArgb(18, 0, 160, 255),
            TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        pulseAnim.Completed += (_, _) =>
        {
            line.Stroke = originalBrush;
            line.StrokeThickness = 1;
        };
        pulseBrush.BeginAnimation(SolidColorBrush.ColorProperty, pulseAnim);

        // Zufaelligen Knoten aufblinken lassen
        if (_nodes.Count > 0)
        {
            var nodeIdx = _rng.Next(_nodes.Count);
            var node = _nodes[nodeIdx];
            var nodePulse = new DoubleAnimation(1.0, 0.4, TimeSpan.FromMilliseconds(500))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            node.BeginAnimation(OpacityProperty, nodePulse);
        }
    }

    // ── Ring-Animation ────────────────────────────────────────────────

    private void StartRingIntro()
    {
        var ringIn = new DoubleAnimation(0.1, 1.0, TimeSpan.FromMilliseconds(1000))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, ringIn);
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, ringIn);
    }

    // ── Titel-Animation ───────────────────────────────────────────────

    private void RevealTitle(int startMs)
    {
        FadeIn(TitleText, startMs, 600);

        // Slide von rechts
        var slide = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(600))
        {
            BeginTime = TimeSpan.FromMilliseconds(startMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        TitleSlide.BeginAnimation(TranslateTransform.XProperty, slide);

        // Subtiler Glitch-Effekt
        var glitch = new ThicknessAnimation
        {
            From = new Thickness(0),
            To = new Thickness(4, 0, 0, 0),
            Duration = TimeSpan.FromMilliseconds(35),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(4),
            BeginTime = TimeSpan.FromMilliseconds(startMs + 100)
        };
        TitleText.BeginAnimation(MarginProperty, glitch);
    }

    // ── Fortschrittsleiste ────────────────────────────────────────────

    private void StartProgressBar()
    {
        // Langsam wachsende Fortschrittsleiste (920px = volle Breite minus Margins)
        var grow = new DoubleAnimation(0, 920, TimeSpan.FromMilliseconds(5500))
        {
            BeginTime = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        ProgressBar.BeginAnimation(WidthProperty, grow);
    }

    // ── Status-Timer ──────────────────────────────────────────────────

    private void OnStatusTick(object? sender, EventArgs e)
    {
        _statusIndex++;
        if (_statusIndex >= StatusMessages.Length)
        {
            _statusTimer.Stop();
            return;
        }

        StatusText.Text = StatusMessages[_statusIndex];

        // StatusDot Farbe je nach Phase
        if (_statusIndex <= 2)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0, 136, 255)); // Blau
        }
        else if (_statusIndex == StatusMessages.Length - 1)
        {
            // Bereit → Gruen
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 160));
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0, 210, 160));
            _statusTimer.Stop();
            _pulseTimer.Stop();
        }
    }

    // ── Hilfs-Animationen ─────────────────────────────────────────────

    private static void FadeIn(UIElement element, int startMs, int durMs)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(startMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    // ── Oeffentliche API (unveraendert) ───────────────────────────────

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
