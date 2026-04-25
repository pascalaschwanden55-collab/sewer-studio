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
    private readonly List<List<Ellipse>> _nodesByStage = new();
    private readonly List<Line> _connections = new();
    private readonly List<List<Line>> _connectionsByStage = new();
    private int _pulseStageIndex;

    // V4.2 Pipeline-Stages: Frame -> YOLO -> DINO -> SAM -> Qwen -> Code
    // Jede Stage hat Label, eigene Farbe, eigene Knoten-Anzahl — statt generischem Blob
    private sealed record StageDef(string Label, string SubLabel, int Count, double X, Color Color);

    // X-Positionen so gewaehlt, dass die Pipeline links vom Titel-Block sitzt
    // (Titel ab Canvas-x=420). 6 Stages × 72px Spacing auf x=40..400.
    private static readonly StageDef[] Stages =
    [
        new("FRAME",  "1920x1080",  1,  40, Color.FromRgb(0xBB, 0xBB, 0xBB)),
        new("YOLO",   "26m-seg",    4, 112, Color.FromRgb(0x22, 0xC5, 0x5E)), // gruen
        new("DINO",   "1.5",        4, 184, Color.FromRgb(0xA0, 0x78, 0xFF)), // violett
        new("SAM",    "2",          4, 256, Color.FromRgb(0xF5, 0x9E, 0x0B)), // orange
        new("QWEN",   "3-VL 8b+32b", 5, 328, Color.FromRgb(0x00, 0xBB, 0xFF)), // cyan
        new("CODE",   "VSA-KEK",    1, 400, Color.FromRgb(0xFF, 0xD7, 0x00)), // gold
    ];

    private static readonly string[] StatusMessages =
    [
        "Initialisiere Anwendung...",
        "YOLO26m-seg + DINO 1.5 + SAM 2 laden...",
        "Qwen3-VL aktivieren (8b-q8 + 32b-hybrid)...",
        "KnowledgeBase + nomic-embed verbinden...",
        "KI-Pipeline bereit"
    ];

    public StartupSplashWindow()
    {
        InitializeComponent();

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
        _statusTimer.Tick += OnStatusTick;

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _pulseTimer.Tick += OnPulseTick;

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            // Audit R-M2: Splash-Crash darf App-Startup nicht killen.
            try { _statusTimer.Stop(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Splash.Closed] StatusTimer: {ex.Message}"); }
            try { _pulseTimer.Stop(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Splash.Closed] PulseTimer: {ex.Message}"); }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Fenster einblenden
        var windowFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(1000))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, windowFade);

        // Neuronales Netz zeichnen (Pipeline-Stages)
        BuildNeuralNetwork();

        // Animationen starten — Timings doppelt so lang (10s minSplashDuration)
        AnimateNetworkFadeIn();
        RevealTitle(3200);
        FadeIn(SubText, 4000, 1200);
        FadeIn(VersionText, 4400, 800);
        FadeIn(StatusText, 1200, 700);
        FadeIn(StatusDot, 1200, 700);
        StartProgressBar();

        _statusTimer.Start();

        // Datenpulse mit Verzoegerung starten
        _pulseTimer.IsEnabled = false;
        var pulseDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
        pulseDelay.Tick += (_, _) =>
        {
            pulseDelay.Stop();
            _pulseTimer.Start();
        };
        pulseDelay.Start();
    }

    // ── Neuronales Netz aufbauen — Pipeline-Visualisierung ─────────────

    private void BuildNeuralNetwork()
    {
        // Vertikale Positionen pro Stage berechnen (gleichmaessig verteilt)
        // Canvas-Hoehe: ~520 (560 - 2*20 Margin). Y-Zentrum bei ~260.
        double topY = 80, bottomY = 440;
        var yPositions = Stages.Select(s => ComputeYPositions(s.Count, topY, bottomY)).ToArray();

        // 1. Verbindungen zwischen aufeinanderfolgenden Stages (hinter den Knoten)
        for (int s = 0; s < Stages.Length - 1; s++)
        {
            var stageConnections = new List<Line>();
            for (int i = 0; i < Stages[s].Count; i++)
            {
                for (int j = 0; j < Stages[s + 1].Count; j++)
                {
                    // Dichter vernetzt als vorher (~80%) — aber nur innerhalb benachbarter Stages
                    if (_rng.NextDouble() > 0.8) continue;

                    var line = new Line
                    {
                        X1 = Stages[s].X,
                        Y1 = yPositions[s][i],
                        X2 = Stages[s + 1].X,
                        Y2 = yPositions[s + 1][j],
                        Stroke = new SolidColorBrush(Color.FromArgb(22, 140, 200, 255)),
                        StrokeThickness = 1,
                        Opacity = 0
                    };
                    NeuralCanvas.Children.Add(line);
                    _connections.Add(line);
                    stageConnections.Add(line);
                }
            }
            _connectionsByStage.Add(stageConnections);
        }

        // 2. Knoten pro Stage zeichnen (gefaerbt nach Stage)
        for (int s = 0; s < Stages.Length; s++)
        {
            var stageNodes = new List<Ellipse>();
            var color = Stages[s].Color;
            bool isEdgeStage = s == 0 || s == Stages.Length - 1;
            double size = isEdgeStage ? 14.0 : 9.0;

            for (int i = 0; i < Stages[s].Count; i++)
            {
                var node = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Opacity = 0,
                    Fill = new RadialGradientBrush(
                        Color.FromArgb(230, color.R, color.G, color.B),
                        Color.FromArgb(50, color.R, color.G, color.B)),
                    Stroke = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
                    StrokeThickness = 1.2,
                    Effect = new DropShadowEffect
                    {
                        BlurRadius = 12,
                        ShadowDepth = 0,
                        Color = color,
                        Opacity = 0.6
                    }
                };
                Canvas.SetLeft(node, Stages[s].X - size / 2);
                Canvas.SetTop(node, yPositions[s][i] - size / 2);
                NeuralCanvas.Children.Add(node);
                _nodes.Add(node);
                stageNodes.Add(node);
            }
            _nodesByStage.Add(stageNodes);

            // 3. Label ueber/unter der Stage
            AddStageLabels(Stages[s], topY - 40, bottomY + 14);
        }
    }

    private static double[] ComputeYPositions(int count, double top, double bottom)
    {
        if (count <= 0) return Array.Empty<double>();
        if (count == 1) return [(top + bottom) / 2.0];
        double step = (bottom - top) / (count - 1);
        var result = new double[count];
        for (int i = 0; i < count; i++) result[i] = top + step * i;
        return result;
    }

    private void AddStageLabels(StageDef stage, double labelY, double subLabelY)
    {
        // Hauptlabel ueber der Stage (Modellname)
        var mainLabel = new TextBlock
        {
            Text = stage.Label,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromArgb(240, stage.Color.R, stage.Color.G, stage.Color.B)),
            Opacity = 0
        };
        mainLabel.Measure(new Size(200, 50));
        Canvas.SetLeft(mainLabel, stage.X - mainLabel.DesiredSize.Width / 2);
        Canvas.SetTop(mainLabel, labelY);
        NeuralCanvas.Children.Add(mainLabel);
        _labels.Add(mainLabel);

        // Sublabel unter der Stage (Variante/Detail)
        var subLabel = new TextBlock
        {
            Text = stage.SubLabel,
            FontSize = 9,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromArgb(140, 200, 210, 230)),
            Opacity = 0
        };
        subLabel.Measure(new Size(200, 50));
        Canvas.SetLeft(subLabel, stage.X - subLabel.DesiredSize.Width / 2);
        Canvas.SetTop(subLabel, subLabelY);
        NeuralCanvas.Children.Add(subLabel);
        _labels.Add(subLabel);
    }

    private readonly List<TextBlock> _labels = new();

    private void AnimateNetworkFadeIn()
    {
        // Stage-fuer-Stage einblenden: zuerst Label der Stage, dann Knoten,
        // dann die Verbindungen zur naechsten Stage — wie Pipeline-Bau
        // Timings verdoppelt fuer 10s-Splash.
        int baseDelay = 600;
        int stepMs = 560;

        for (int s = 0; s < _nodesByStage.Count; s++)
        {
            int stageDelay = baseDelay + s * stepMs;

            // Stage-Labels (je Stage 2: Haupt + Sub) einblenden
            // Index im _labels: s*2 = Haupt, s*2+1 = Sub
            if (s * 2 < _labels.Count) FadeIn(_labels[s * 2], stageDelay, 700);
            if (s * 2 + 1 < _labels.Count) FadeIn(_labels[s * 2 + 1], stageDelay + 160, 700);

            // Knoten der Stage
            foreach (var node in _nodesByStage[s])
                FadeIn(node, stageDelay + 100, 700);

            // Verbindungen von dieser Stage zur naechsten (falls vorhanden)
            if (s < _connectionsByStage.Count)
            {
                foreach (var line in _connectionsByStage[s])
                    FadeIn(line, stageDelay + 300, 800);
            }
        }
    }

    // ── Datenpulse durch das Netzwerk — sequentiell Stage fuer Stage ───

    private void OnPulseTick(object? sender, EventArgs e)
    {
        if (_connectionsByStage.Count == 0) return;

        // Pulse wandert Stage fuer Stage — wie echter Datenfluss Frame -> Code
        int stageIdx = _pulseStageIndex % _connectionsByStage.Count;
        _pulseStageIndex++;

        var stage = Stages[stageIdx];
        var nextStageColor = Stages[stageIdx + 1].Color;
        PulseStage(_connectionsByStage[stageIdx], _nodesByStage[stageIdx + 1], nextStageColor);
    }

    private void PulseStage(List<Line> connections, List<Ellipse> targetNodes, Color color)
    {
        // Alle Verbindungen dieser Stage gleichzeitig aufleuchten
        foreach (var line in connections)
        {
            var originalBrush = new SolidColorBrush(Color.FromArgb(22, 140, 200, 255));
            var pulseBrush = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B));

            line.Stroke = pulseBrush;
            line.StrokeThickness = 1.8;

            var fadeBack = new ColorAnimation(
                Color.FromArgb(200, color.R, color.G, color.B),
                Color.FromArgb(22, 140, 200, 255),
                TimeSpan.FromMilliseconds(700))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            fadeBack.Completed += (_, _) =>
            {
                line.Stroke = originalBrush;
                line.StrokeThickness = 1;
            };
            pulseBrush.BeginAnimation(SolidColorBrush.ColorProperty, fadeBack);
        }

        // Zielknoten der Stage aufleuchten lassen (Datenpaket kommt an)
        foreach (var node in targetNodes)
        {
            var bounce = new DoubleAnimation(1.0, 0.5, TimeSpan.FromMilliseconds(500))
            {
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            node.BeginAnimation(OpacityProperty, bounce);
        }
    }

    // ── Titel-Animation ───────────────────────────────────────────────

    private void RevealTitle(int startMs)
    {
        FadeIn(TitleText, startMs, 1200);

        // Slide von rechts (verdoppelt)
        var slide = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(1200))
        {
            BeginTime = TimeSpan.FromMilliseconds(startMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        TitleSlide.BeginAnimation(TranslateTransform.XProperty, slide);

        // Subtiler Glitch-Effekt (Rhythmus bleibt, nur spaeter ausgeloest)
        var glitch = new ThicknessAnimation
        {
            From = new Thickness(0),
            To = new Thickness(4, 0, 0, 0),
            Duration = TimeSpan.FromMilliseconds(35),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(4),
            BeginTime = TimeSpan.FromMilliseconds(startMs + 200)
        };
        TitleText.BeginAnimation(MarginProperty, glitch);
    }

    // ── Fortschrittsleiste ────────────────────────────────────────────

    private void StartProgressBar()
    {
        // Langsam wachsende Fortschrittsleiste (920px = volle Breite minus Margins)
        // Dauer verdoppelt fuer 10s-Splash.
        var grow = new DoubleAnimation(0, 920, TimeSpan.FromMilliseconds(11000))
        {
            BeginTime = TimeSpan.FromMilliseconds(1000),
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
