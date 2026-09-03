using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Aufbau und laufende Darstellung des neuronalen Startbilds.</summary>
public partial class StartupSplashWindow
{
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
        _dust.Clear();

        BuildBackdrop();
        BuildSphere();
        BuildConnections();

        _screenX = new double[_nodes.Count];
        _screenY = new double[_nodes.Count];
        _screenDepth = new double[_nodes.Count];
    }

    /// <summary>Entwurfsmass (bei Faktor 1) auf die aktuelle Kugelgroesse umrechnen.</summary>
    private double S(double designValue) => designValue * _sphereScale;

    private void BuildBackdrop()
    {
        var glowDiameter = S(460);
        _coreGlowScale = new ScaleTransform(1, 1, glowDiameter / 2, glowDiameter / 2);
        _coreGlow = new Ellipse
        {
            Width = glowDiameter,
            Height = glowDiameter,
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
                    new GradientStop(Color.FromArgb(46, AccentBlue.R, AccentBlue.G, AccentBlue.B), 0.0),
                    new GradientStop(Color.FromArgb(18, AccentCyan.R, AccentCyan.G, AccentCyan.B), 0.45),
                    new GradientStop(Color.FromArgb(0, AccentDeep.R, AccentDeep.G, AccentDeep.B), 1.0)
                }
            },
            RenderTransform = _coreGlowScale,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_coreGlow, _centerX - glowDiameter / 2);
        Canvas.SetTop(_coreGlow, _centerY - glowDiameter / 2);
        Panel.SetZIndex(_coreGlow, 0);
        NeuralCanvas.Children.Add(_coreGlow);

        _ringOuter = CreateRing(S(420), 110, AccentDeep, out _ringOuterRotate);
        _ringMiddle = CreateRing(S(340), 90, AccentBlue, out _ringMiddleRotate);
        _ringInner = CreateRing(S(260), 80, AccentCyan, out _ringInnerRotate);

        // Scanline der Inferenz-Welle: laeuft synchron zum Aktivierungs-Sweep durchs Netz.
        // Hoehe auf die Kugel begrenzt: Auf hellem Grund darf das Band nicht als Streifen
        // ueber die ganze Flaeche laufen.
        var scanHeight = S(500);
        _scanLine = new Rectangle
        {
            Width = S(110),
            Height = scanHeight,
            Opacity = 0,
            IsHitTestVisible = false,
            OpacityMask = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Colors.Transparent, 0.0),
                    new GradientStop(Colors.Black, 0.3),
                    new GradientStop(Colors.Black, 0.7),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, AccentCyan.R, AccentCyan.G, AccentCyan.B), 0.0),
                    new GradientStop(Color.FromArgb(30, AccentCyan.R, AccentCyan.G, AccentCyan.B), 0.5),
                    new GradientStop(Color.FromArgb(0, AccentCyan.R, AccentCyan.G, AccentCyan.B), 1.0)
                }
            }
        };
        Canvas.SetTop(_scanLine, _centerY - scanHeight / 2);
        Canvas.SetLeft(_scanLine, _centerX - _waveHalfSpan);
        Panel.SetZIndex(_scanLine, 3);
        NeuralCanvas.Children.Add(_scanLine);

        // Orbitierende Satelliten auf den Ringen (Radius = halber Ringdurchmesser).
        AddSatellite(S(210), 0.35, 0.0, AccentDeep, S(5));
        AddSatellite(S(210), 0.35, Math.PI, AccentBlue, S(4));
        AddSatellite(S(170), -0.55, 1.1, AccentCyan, S(4));
        AddSatellite(S(130), 0.85, 2.4, AccentCyan, S(3.5));

        BuildAccentArc();
        BuildDustField();
    }

    /// <summary>
    /// Kurzes blaues Leuchtsegment auf dem Aussenring: laeuft etwas schneller
    /// als der Ring selbst und gibt dem Bild einen wandernden Akzent.
    /// </summary>
    private void BuildAccentArc()
    {
        _accentArcRotate = new RotateTransform(0, _centerX, _centerY);
        _accentArc = new Path
        {
            Stroke = new SolidColorBrush(Color.FromArgb(200, PulseBlue.R, PulseBlue.G, PulseBlue.B)),
            StrokeThickness = S(2.4),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0,
            Data = BuildArcGeometry(_centerX, _centerY, S(210), 188, 52),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Color = PulseBlue,
                Opacity = 0.35
            },
            RenderTransform = _accentArcRotate,
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_accentArc, 2);
        NeuralCanvas.Children.Add(_accentArc);
    }

    private static PathGeometry BuildArcGeometry(
        double centerX, double centerY, double radius, double startDegrees, double sweepDegrees)
    {
        var figure = new PathFigure
        {
            StartPoint = ArcPoint(centerX, centerY, radius, startDegrees),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = ArcPoint(centerX, centerY, radius, startDegrees + sweepDegrees),
            Size = new Size(radius, radius),
            IsLargeArc = sweepDegrees > 180,
            SweepDirection = SweepDirection.Clockwise,
            IsStroked = true
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static Point ArcPoint(double centerX, double centerY, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new Point(
            centerX + radius * Math.Cos(radians),
            centerY + radius * Math.Sin(radians));
    }

    /// <summary>
    /// Feines Punktfeld hinter dem Netz (deterministischer Seed): Die Koerner bleiben
    /// ortsfest und pulsieren nur leicht in der Deckkraft. Auf hellem Grund sind sie
    /// blau-grau statt weiss.
    /// </summary>
    private void BuildDustField()
    {
        var rng = new Random(23);
        for (var i = 0; i < DustCount; i++)
        {
            var size = (1.2 + rng.NextDouble() * 1.8) * Math.Sqrt(_sphereScale);
            var tint = rng.NextDouble();
            var color = tint < 0.70 ? LineAccent : tint < 0.92 ? AccentCyan : PulseBlue;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Opacity = 0,
                Fill = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, rng.NextDouble() * _canvasWidth);
            Canvas.SetTop(dot, rng.NextDouble() * _canvasHeight);
            Panel.SetZIndex(dot, 2);
            NeuralCanvas.Children.Add(dot);
            _dust.Add(new BackgroundDust(
                dot,
                baseOpacity: 0.12 + rng.NextDouble() * 0.22,
                phase: rng.NextDouble() * Math.PI * 2,
                speed: 0.3 + rng.NextDouble() * 0.9));
        }
    }

    private void AddSatellite(double radius, double speed, double startAngle, Color color, double size)
    {
        var dot = new Ellipse
        {
            Width = size,
            Height = size,
            Opacity = 0,
            Fill = new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B)),
            Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.35
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
            StrokeThickness = 1.0,
            StrokeDashArray = { 2, 6, 1, 9 },
            RenderTransform = rotate,
            CacheMode = new BitmapCache(),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(ring, _centerX - diameter / 2);
        Canvas.SetTop(ring, _centerY - diameter / 2);
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
        fillBrush = new SolidColorBrush(Color.FromArgb(200, AccentBlue.R, AccentBlue.G, AccentBlue.B));
        strokeBrush = new SolidColorBrush(Color.FromArgb(190, AccentBlue.R, AccentBlue.G, AccentBlue.B));

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
        var strokeBrush = new SolidColorBrush(Color.FromArgb(60, LineAccent.R, LineAccent.G, LineAccent.B));
        var line = new Line
        {
            Stroke = strokeBrush,
            StrokeThickness = 0.7,
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
        // Rotation faehrt ueber die ersten Sekunden weich hoch statt sofort
        // voll zu drehen — der Aufbau wirkt ruhiger und edler.
        var ramp = StartupSplashAnimationPolicy.EaseInCubic(
            _animationClock.Elapsed.TotalSeconds / RotationRampSeconds);
        _rotationY += 0.42 * ramp * dt;
        _rotationX = 0.22 + Math.Sin(_rotationY * 0.45) * 0.08;
        _rotationZ = Math.Sin(_rotationY * 0.31) * 0.10;
        _breathPhase += 1.35 * dt;

        if (_ringOuterRotate is not null)
            _ringOuterRotate.Angle += 5.4 * dt;
        if (_ringMiddleRotate is not null)
            _ringMiddleRotate.Angle -= 8.1 * dt;
        if (_ringInnerRotate is not null)
            _ringInnerRotate.Angle += 12.6 * dt;
        if (_accentArcRotate is not null)
            _accentArcRotate.Angle += 7.4 * dt;

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
            // Kaskaden behalten ueberwiegend ihre Farbe (nur leicht Richtung Cyan).
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
        var waveX = _centerX - _waveHalfSpan + _waveT * _waveHalfSpan * 2;
        var waveStrength = waveActive ? Math.Sin(Math.PI * _waveT) : 0;
        var nodeScale = Math.Sqrt(_sphereScale);

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
                if (waveDist < _waveBandWidth)
                {
                    var boost = (1.0 - waveDist / _waveBandWidth) * 0.95 * waveStrength;
                    if (boost > node.Activation)
                        node.Activation = boost;
                }
            }

            var depth01 = Clamp01((depth + 1.0) / 2.0);
            // Tiefennebel: die ferne Hemisphaere wird kleiner und blasser,
            // die nahe bleibt unveraendert — dadurch wirkt die Kugel plastisch.
            var fog = StartupSplashAnimationPolicy.DepthFog(depth);
            var size = (4.0 + perspective * 3.4 + depth01 * 3.0)
                * (1.0 + node.Activation * 0.7)
                * (0.72 + fog * 0.28)
                * nodeScale;
            var alpha = (byte)Math.Clamp((90 + depth01 * 150 + node.Activation * 40) * fog, 0, 255);
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
                if (waveDist < _waveBandWidth)
                {
                    var boost = (1.0 - waveDist / _waveBandWidth) * 0.45 * waveStrength;
                    if (boost > connection.Activation)
                        connection.Activation = boost;
                }
            }

            var depth01 = Clamp01((_screenDepth[a] + _screenDepth[b] + 2.0) / 4.0);
            var fog = StartupSplashAnimationPolicy.DepthFog((_screenDepth[a] + _screenDepth[b]) / 2.0);
            var alpha = (byte)Math.Clamp((30 + depth01 * 90 + connection.Activation * 140) * fog, 0, 235);
            var color = Blend(LineAccent, AccentCyan, connection.Activation * 0.85 + depth01 * 0.20);

            connection.Visual.X1 = _screenX[a];
            connection.Visual.Y1 = _screenY[a];
            connection.Visual.X2 = _screenX[b];
            connection.Visual.Y2 = _screenY[b];
            connection.Visual.StrokeThickness =
                (0.5 + depth01 * 0.7 + connection.Activation * 1.7) * (0.75 + fog * 0.25) * nodeScale;
            connection.StrokeBrush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
            Panel.SetZIndex(connection.Visual, 8 + (int)(depth01 * 12));
        }

        UpdateActivePulseVisuals();
        UpdateFlareVisuals();
        UpdateBackdrop();
        UpdateScanLine(waveActive, waveX, waveStrength);
        UpdateSatellites();
        UpdateDust();
        UpdateProgressSheen();
    }

    private void UpdateDust()
    {
        var elapsed = _animationClock.Elapsed.TotalSeconds;
        var fadeIn = Clamp01((elapsed - 0.8) / 1.6);
        if (fadeIn <= 0)
            return;

        foreach (var mote in _dust)
        {
            var twinkle = 0.55 + 0.45 * Math.Sin(mote.Phase + elapsed * mote.Speed);
            mote.Visual.Opacity = mote.BaseOpacity * twinkle * fadeIn;
        }
    }

    /// <summary>
    /// Wandernder Glanzstreifen auf dem Fortschrittsbalken. Laueft nur waehrend
    /// des Ladens; beim Ready-Uebergang (EmitPulses aus) blendet er sich aus.
    /// </summary>
    private void UpdateProgressSheen()
    {
        if (!_emitPulses)
        {
            ProgressSheen.Opacity = 0;
            return;
        }

        var elapsed = _animationClock.Elapsed.TotalSeconds;
        var fadeIn = Clamp01((elapsed - 1.0) / 1.0);
        var cycle = elapsed % SheenCycleSeconds / SheenCycleSeconds;
        ProgressSheenSlide.X = -100 + cycle * (_progressFullWidth + 200);
        ProgressSheen.Opacity = 0.7 * fadeIn;
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
            var px = _centerX + Math.Cos(satellite.Angle) * satellite.Radius;
            var py = _centerY + Math.Sin(satellite.Angle) * satellite.Radius;
            var size = satellite.Visual.Width;
            Canvas.SetLeft(satellite.Visual, px - size / 2.0);
            Canvas.SetTop(satellite.Visual, py - size / 2.0);
            satellite.Visual.Opacity = (0.45 + breath * 0.35) * fadeIn;
        }
    }

    private void Project(
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
        var projection = StartupSplashAnimationPolicy.Project(
            x, y, z,
            cosY, sinY, cosX, sinX, cosZ, sinZ,
            CameraDistance, _projectionScale, _centerX, _centerY);
        px = projection.X;
        py = projection.Y;
        depth = projection.Depth;
        perspective = projection.Perspective;
    }

    private void UpdateActivePulseVisuals()
    {
        var nodeScale = Math.Sqrt(_sphereScale);
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
            var size = (4.5 + depth01 * 5.0) * nodeScale;

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
            var size = (8.0 + t * 60.0) * _sphereScale;
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

        if (_accentArc is not null)
        {
            var fadeIn = Clamp01((_animationClock.Elapsed.TotalSeconds - 0.7) / 1.4);
            _accentArc.Opacity = (0.45 + breath * 0.30) * fadeIn;
        }
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
                BlurRadius = 12,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.55
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
                BlurRadius = 12,
                ShadowDepth = 0,
                Color = color,
                Opacity = 0.4
            },
            IsHitTestVisible = false
        };
        Panel.SetZIndex(ring, 70);
        NeuralCanvas.Children.Add(ring);
        _flares.Add(new NodeFlare(nodeIndex, ring, color));
    }
}
