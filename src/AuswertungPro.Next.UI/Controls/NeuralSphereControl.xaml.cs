using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Controls;

public partial class NeuralSphereControl : UserControl
{
    // ── Dependency Properties ──────────────────────────────────────────
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(NeuralSphereControl),
            new PropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(NeuralSphereControl),
            new PropertyMetadata(""));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    // ── Constants ──────────────────────────────────────────────────────
    private const int NodeCount = 50;
    private const int PulseCount = 6;
    private const double GoldenRatio = 1.6180339887498949;

    // Dauer des Rotations-Anlaufs in Sekunden (weich hochfahren statt sofort volle Drehzahl).
    private const double RotationRampSeconds = 2.0;

    // Rueckfallfarben, falls das Theme-Dictionary die Schluessel nicht fuehrt (z. B. im Test).
    private static readonly Color FallbackAccent = Color.FromRgb(0x25, 0x63, 0xEB);
    private static readonly Color FallbackAccentLight = Color.FromRgb(0x3B, 0x82, 0xF6);

    // Aus dem Theme gelesen, sobald die Visuals entstehen. Ein Theme-Wechsel zur Laufzeit faerbt
    // die Kugel bewusst nicht um: die Knotenfarben stecken in Dutzenden erzeugter Brushes, und
    // die Kugel lebt in Fenstern, die dabei ohnehin neu aufgebaut werden.
    private Color _accentColor = FallbackAccent;
    private Color _accentLight = FallbackAccentLight;

    // ── State ──────────────────────────────────────────────────────────
    private readonly double[] _theta = new double[NodeCount];
    private readonly double[] _phi = new double[NodeCount];

    // Projected 2D positions (reused each frame)
    private readonly double[] _px = new double[NodeCount];
    private readonly double[] _py = new double[NodeCount];
    private readonly double[] _pz = new double[NodeCount]; // depth for sizing

    private readonly Ellipse[] _nodeEllipses = new Ellipse[NodeCount];
    private readonly List<(int a, int b, Line line)> _connections = new();
    private readonly Ellipse[] _pulseEllipses = new Ellipse[PulseCount];
    private readonly double[] _pulseProgress = new double[PulseCount]; // 0..1 or -1 = inactive
    private readonly int[] _pulseConnIdx = new int[PulseCount]; // which connection
    private Ellipse? _glowEllipse;

    private double _rotY;
    private double _rotX;
    private double _glowPhase;
    private int _fireTick;
    private readonly Random _rng = new();

    private readonly DispatcherTimer _timer;
    private bool _visualsCreated;

    // Startzeitpunkt des aktuellen Animationslaufs; null, solange der Timer steht.
    // Dient dem weichen Rotations-Anlauf ueber die ersten Sekunden nach dem Start.
    private DateTime? _rampStartUtc;

    public NeuralSphereControl()
    {
        InitializeComponent();
        ComputeFibonacciPositions();
        for (int i = 0; i < PulseCount; i++)
            _pulseProgress[i] = -1;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30fps
        };
        _timer.Tick += OnTick;

        Loaded += (_, _) =>
        {
            // Tick-Abo nach einem Unload/Reload-Zyklus wiederherstellen (Unloaded meldet es ab).
            // Ohne dies liefe der Timer nach dem Wiedereinhaengen ins Leere -> Animation eingefroren.
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            EnsureVisuals();
            UpdateTimerState();
            RenderFrame();
        };

        // Unsichtbar heisst: nicht rechnen. Sonst dreht die Kugel in einem verdeckten Reiter weiter.
        IsVisibleChanged += (_, _) => UpdateTimerState();

        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        };
    }

    /// <summary>
    /// Die Kugel rechnet nur, wenn sie arbeitet, sichtbar ist und der Nutzer Dauer-Animationen
    /// nicht abgeschaltet hat. Sonst bleibt das Netz als stilles Standbild stehen.
    /// </summary>
    private void UpdateTimerState()
    {
        if (IsActive && IsVisible && !MotionSettings.ReduceMotion)
        {
            // Nur bei einem echten (Neu-)Start merken, damit der Anlauf bei jedem
            // Start von vorn beginnt und ein laufender Loop nicht zurueckspringt.
            if (!_timer.IsEnabled)
                _rampStartUtc = DateTime.UtcNow;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _rampStartUtc = null;
        }
    }

    /// <summary>Holt die Akzentfarben aus dem aktiven Theme; ohne Treffer bleiben die Rueckfallwerte.</summary>
    private void ResolveThemeColors()
    {
        if (TryFindResource("ColorAccent") is Color accent)
            _accentColor = accent;
        if (TryFindResource("ColorAccentLight") is Color accentLight)
            _accentLight = accentLight;
    }

    // ── Fibonacci sphere distribution ──────────────────────────────────
    private void ComputeFibonacciPositions()
    {
        for (int i = 0; i < NodeCount; i++)
        {
            _theta[i] = Math.Acos(1.0 - 2.0 * (i + 0.5) / NodeCount);
            _phi[i] = 2.0 * Math.PI * i / GoldenRatio;
        }
    }

    // ── Visual creation (once) ─────────────────────────────────────────
    private void EnsureVisuals()
    {
        if (_visualsCreated) return;
        _visualsCreated = true;

        ResolveThemeColors();
        var canvas = SphereCanvas;

        // Central glow
        _glowEllipse = new Ellipse
        {
            Width = 100,
            Height = 100,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(60, _accentColor.R, _accentColor.G, _accentColor.B), 0.0),
                    new GradientStop(Color.FromArgb(0, _accentColor.R, _accentColor.G, _accentColor.B), 1.0)
                }
            },
            Opacity = 0.15,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_glowEllipse, 20);
        Canvas.SetTop(_glowEllipse, 20);
        canvas.Children.Add(_glowEllipse);

        // Connections (find ~80 nearest-neighbor pairs)
        BuildConnections(canvas);

        // Nodes
        for (int i = 0; i < NodeCount; i++)
        {
            var e = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(_accentColor),
                IsHitTestVisible = false
            };
            _nodeEllipses[i] = e;
            canvas.Children.Add(e);
        }

        // Pulse ellipses
        for (int i = 0; i < PulseCount; i++)
        {
            var e = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(_accentLight),
                Opacity = 0,
                IsHitTestVisible = false
            };
            _pulseEllipses[i] = e;
            canvas.Children.Add(e);
        }
    }

    private void BuildConnections(Canvas canvas)
    {
        // Pre-compute 3D positions at rotation=0 to find neighbors
        var positions = new (double x, double y, double z)[NodeCount];
        for (int i = 0; i < NodeCount; i++)
        {
            positions[i] = (
                Math.Sin(_theta[i]) * Math.Cos(_phi[i]),
                Math.Sin(_theta[i]) * Math.Sin(_phi[i]),
                Math.Cos(_theta[i])
            );
        }

        // Connect nodes within distance threshold to get ~80 connections
        var threshold = 0.72;
        for (int i = 0; i < NodeCount; i++)
        {
            for (int j = i + 1; j < NodeCount; j++)
            {
                var dx = positions[i].x - positions[j].x;
                var dy = positions[i].y - positions[j].y;
                var dz = positions[i].z - positions[j].z;
                var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dist < threshold)
                {
                    var line = new Line
                    {
                        Stroke = new SolidColorBrush(Color.FromArgb(50, _accentColor.R, _accentColor.G, _accentColor.B)),
                        StrokeThickness = 0.8,
                        IsHitTestVisible = false
                    };
                    _connections.Add((i, j, line));
                    canvas.Children.Add(line);
                }
            }
        }
    }

    // ── IsActive changed ───────────────────────────────────────────────
    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NeuralSphereControl ctrl)
        {
            ctrl.UpdateTimerState();
            // Einmal neu zeichnen: der Ruhezustand ist gedimmt, der Arbeitszustand heller —
            // das gilt auch, wenn gar nicht animiert wird.
            ctrl.RenderFrame();
        }
    }

    // ── Animation tick ─────────────────────────────────────────────────
    private void OnTick(object? sender, EventArgs e)
    {
        var active = IsActive;

        // Rotations-Anlauf: die Drehzahl faehrt ueber die ersten Sekunden weich hoch
        // (kubisches Ease-in wie im Startsplash), statt sofort voll zu drehen.
        var ramp = _rampStartUtc is DateTime rampStart
            ? StartupSplashAnimationPolicy.EaseInCubic(
                (DateTime.UtcNow - rampStart).TotalSeconds / RotationRampSeconds)
            : 0.0;
        var rotSpeed = (active ? 1.0 : 0.25) * ramp;
        _rotY += 0.012 * rotSpeed;
        _rotX += 0.004 * rotSpeed;

        // Glow pulse
        if (active)
        {
            _glowPhase += 0.04;
            if (_glowEllipse is not null)
                _glowEllipse.Opacity = 0.15 + 0.40 * (0.5 + 0.5 * Math.Sin(_glowPhase));
        }

        // Fire pulses (~4/sec = every ~8 ticks at 30fps)
        if (active)
        {
            _fireTick++;
            if (_fireTick >= 8 && _connections.Count > 0)
            {
                _fireTick = 0;
                TryFirePulse();
            }
        }

        // Advance pulses
        for (int i = 0; i < PulseCount; i++)
        {
            if (_pulseProgress[i] < 0) continue;
            _pulseProgress[i] += 0.06;
            if (_pulseProgress[i] > 1.0)
                _pulseProgress[i] = -1;
        }

        RenderFrame();
    }

    private void TryFirePulse()
    {
        for (int i = 0; i < PulseCount; i++)
        {
            if (_pulseProgress[i] < 0)
            {
                _pulseProgress[i] = 0;
                _pulseConnIdx[i] = _rng.Next(_connections.Count);
                return;
            }
        }
    }

    // ── Render ──────────────────────────────────────────────────────────
    private void RenderFrame()
    {
        if (!_visualsCreated) return;

        var cx = 70.0;
        var cy = 70.0;
        var radius = 55.0;
        var cosY = Math.Cos(_rotY);
        var sinY = Math.Sin(_rotY);
        var cosX = Math.Cos(_rotX);
        var sinX = Math.Sin(_rotX);
        var active = IsActive;

        // Project all nodes
        for (int i = 0; i < NodeCount; i++)
        {
            var st = Math.Sin(_theta[i]);
            var ct = Math.Cos(_theta[i]);
            var sp = Math.Sin(_phi[i]);
            var cp = Math.Cos(_phi[i]);

            // 3D position on unit sphere
            var x0 = st * cp;
            var y0 = st * sp;
            var z0 = ct;

            // Rotate around Y axis
            var x1 = x0 * cosY + z0 * sinY;
            var y1 = y0;
            var z1 = -x0 * sinY + z0 * cosY;

            // Rotate around X axis
            var y2 = y1 * cosX - z1 * sinX;
            var z2 = y1 * sinX + z1 * cosX;

            _px[i] = cx + x1 * radius;
            _py[i] = cy + y2 * radius;
            _pz[i] = z2; // -1..1, front=positive
        }

        // Update nodes (depth-based sizing)
        for (int i = 0; i < NodeCount; i++)
        {
            var depth01 = (_pz[i] + 1.0) / 2.0; // 0=back, 1=front
            // Tiefennebel: die ferne Hemisphaere wird kleiner und dunkler,
            // die nahe bleibt unveraendert — dadurch wirkt die Kugel plastisch.
            var fog = StartupSplashAnimationPolicy.DepthFog(_pz[i]);
            var size = (2.0 + depth01 * 3.5) * (0.72 + fog * 0.28);
            var alpha = active
                ? (byte)((40 + depth01 * 180) * fog)
                : (byte)((20 + depth01 * 60) * fog);

            var e = _nodeEllipses[i];
            e.Width = size;
            e.Height = size;
            ((SolidColorBrush)e.Fill).Color = Color.FromArgb(alpha, _accentColor.R, _accentColor.G, _accentColor.B);
            Canvas.SetLeft(e, _px[i] - size / 2.0);
            Canvas.SetTop(e, _py[i] - size / 2.0);
        }

        // Update connections
        for (int c = 0; c < _connections.Count; c++)
        {
            var (a, b, line) = _connections[c];
            line.X1 = _px[a];
            line.Y1 = _py[a];
            line.X2 = _px[b];
            line.Y2 = _py[b];

            var avgDepth = (_pz[a] + _pz[b] + 2.0) / 4.0;
            // Gleicher Tiefennebel wie bei den Knoten: Linien nach hinten verblassen
            // und werden feiner, nach vorn bleiben sie satt.
            var fog = StartupSplashAnimationPolicy.DepthFog((_pz[a] + _pz[b]) / 2.0);
            var alpha = active
                ? (byte)((20 + avgDepth * 60) * fog)
                : (byte)((10 + avgDepth * 25) * fog);
            line.StrokeThickness = 0.8 * (0.75 + fog * 0.25);
            ((SolidColorBrush)line.Stroke).Color = Color.FromArgb(alpha, _accentColor.R, _accentColor.G, _accentColor.B);
        }

        // Update pulses
        for (int i = 0; i < PulseCount; i++)
        {
            var pe = _pulseEllipses[i];
            if (_pulseProgress[i] < 0 || !active)
            {
                pe.Opacity = 0;
                continue;
            }

            var t = _pulseProgress[i];
            var connIdx = _pulseConnIdx[i];
            if (connIdx < 0 || connIdx >= _connections.Count)
            {
                pe.Opacity = 0;
                continue;
            }

            var (a, b, _) = _connections[connIdx];
            var ppx = _px[a] + (_px[b] - _px[a]) * t;
            var ppy = _py[a] + (_py[b] - _py[a]) * t;

            pe.Opacity = 1.0 - Math.Abs(t - 0.5) * 2.0; // fade in/out
            Canvas.SetLeft(pe, ppx - 3);
            Canvas.SetTop(pe, ppy - 3);

            // Brighten target node near end of pulse
            if (t > 0.8)
            {
                var target = _nodeEllipses[b];
                target.Width = 7;
                target.Height = 7;
                Canvas.SetLeft(target, _px[b] - 3.5);
                Canvas.SetTop(target, _py[b] - 3.5);
                ((SolidColorBrush)target.Fill).Color = _accentLight;
            }
        }
    }
}
