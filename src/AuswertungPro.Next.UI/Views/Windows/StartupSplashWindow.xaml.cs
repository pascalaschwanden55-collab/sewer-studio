using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class StartupSplashWindow : Window
{
    private readonly DispatcherTimer _statusTimer;
    private int _statusIndex;

    private static readonly string[] StatusMessages =
    [
        "Lade Einstellungen...",
        "Pruefe Kataloge...",
        "Bereite Arbeitsflaeche vor...",
        "Bereit"
    ];

    public StartupSplashWindow()
    {
        InitializeComponent();

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _statusTimer.Tick += OnStatusTick;

        Loaded += OnLoaded;
        Closed += (_, _) => _statusTimer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BeginAnimation(OpacityProperty, Ease(0, 1, 180));

        Shell.BeginAnimation(OpacityProperty, Ease(0, 1, 260, 80));
        LogoMark.BeginAnimation(OpacityProperty, Ease(0, 1, 260, 170));
        LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, Ease(0.92, 1.0, 420, 170));
        LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, Ease(0.92, 1.0, 420, 170));

        TitleText.BeginAnimation(OpacityProperty, Ease(0, 1, 260, 260));
        SubText.BeginAnimation(OpacityProperty, Ease(0, 1, 260, 360));
        StatusText.BeginAnimation(OpacityProperty, Ease(0, 1, 220, 420));

        var progress = new DoubleAnimation(-170, 780, TimeSpan.FromMilliseconds(1450))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        ProgressTranslate.BeginAnimation(TranslateTransform.XProperty, progress);

        _statusTimer.Start();
    }

    private void OnStatusTick(object? sender, EventArgs e)
    {
        if (_statusIndex >= StatusMessages.Length)
        {
            _statusTimer.Stop();
            return;
        }

        StatusText.Text = StatusMessages[_statusIndex];
        _statusIndex++;

        if (_statusIndex == StatusMessages.Length)
        {
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            _statusTimer.Stop();
        }
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
        ProgressTranslate.BeginAnimation(TranslateTransform.XProperty, null);

        var tcs = new TaskCompletionSource<object?>();

        if (duration <= TimeSpan.Zero)
        {
            Close();
            tcs.TrySetResult(null);
            return tcs.Task;
        }

        var opacityAnim = Ease(Opacity, 0, (int)duration.TotalMilliseconds);
        opacityAnim.Completed += (_, _) =>
        {
            Close();
            tcs.TrySetResult(null);
        };

        BeginAnimation(OpacityProperty, opacityAnim);
        return tcs.Task;
    }

    private static DoubleAnimation Ease(double from, double to, int durationMs, int delayMs = 0)
        => new(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
}
