using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Erzeugt die bewegten Impulse und Knotenringe des Startbilds.</summary>
public partial class StartupSplashWindow
{
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
