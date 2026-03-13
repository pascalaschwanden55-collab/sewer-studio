using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        "Initialisiere Anwendung...",
        "Lade Konfiguration...",
        "Module werden vorbereitet...",
        "Oberflaeche wird aufgebaut...",
        "Bereit"
    ];

    public StartupSplashWindow()
    {
        InitializeComponent();

        // Cover entire primary screen including taskbar
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1100)
        };
        _statusTimer.Tick += OnStatusTick;

        Loaded += OnLoaded;
        Closed += (_, _) => _statusTimer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var windowFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, windowFade);

        StartRingIntro();
        RevealTitle(1800);
        FadeIn(SubText, 2200, 700);
        FadeIn(StatusText, 500, 350);
        StartDataBlocks();

        var ringPulse = new DoubleAnimation(0.65, 1.0, TimeSpan.FromMilliseconds(900))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        GlowRing.BeginAnimation(OpacityProperty, ringPulse);

        _statusTimer.Start();
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
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(102, 255, 178));
            var brighten = new DoubleAnimation(1, TimeSpan.FromMilliseconds(250));
            StatusText.BeginAnimation(OpacityProperty, brighten);
            _statusTimer.Stop();
        }
    }

    private void StartRingIntro()
    {
        var ringIn = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, ringIn);
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, ringIn);
    }

    private void RevealTitle(int startMs)
    {
        FadeIn(TitleText, startMs, 500);

        var settle = new DoubleAnimation(1.12, 1.0, TimeSpan.FromMilliseconds(600))
        {
            BeginTime = TimeSpan.FromMilliseconds(startMs),
            EasingFunction = new BackEase
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 0.4
            }
        };
        TitleScale.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
        TitleScale.BeginAnimation(ScaleTransform.ScaleYProperty, settle);

        var glitch = new ThicknessAnimation
        {
            From = new Thickness(0),
            To = new Thickness(6, 0, 0, 0),
            Duration = TimeSpan.FromMilliseconds(40),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(6),
            BeginTime = TimeSpan.FromMilliseconds(startMs)
        };
        TitleText.BeginAnimation(MarginProperty, glitch);
    }

    private void FadeIn(UIElement element, int startMs, int durMs)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(startMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(OpacityProperty, animation);
    }

    private void StartDataBlocks()
    {
        Canvas.SetLeft(B1, 220); Canvas.SetTop(B1, 180);
        Canvas.SetLeft(B2, 260); Canvas.SetTop(B2, 250);
        Canvas.SetLeft(B3, 180); Canvas.SetTop(B3, 320);
        Canvas.SetLeft(B4, 300); Canvas.SetTop(B4, 120);

        AnimateBlock(B1, 4000, 0);
        AnimateBlock(B2, 3800, 200);
        AnimateBlock(B3, 4200, 350);
        AnimateBlock(B4, 3600, 500);
    }

    private void AnimateBlock(FrameworkElement rect, int travelMs, int delayMs)
    {
        var beginMs = 600 + delayMs;
        var endMs = beginMs + travelMs;

        var opacityFrames = new DoubleAnimationUsingKeyFrames
        {
            FillBehavior = FillBehavior.Stop
        };
        opacityFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        opacityFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(beginMs))));
        opacityFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(beginMs + 250))));
        opacityFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(endMs - 350))));
        opacityFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(endMs))));
        rect.BeginAnimation(OpacityProperty, opacityFrames);

        var x0 = Canvas.GetLeft(rect);
        var y0 = Canvas.GetTop(rect);

        var xAnimation = new DoubleAnimation(x0, x0 + 520, TimeSpan.FromMilliseconds(travelMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        var yAnimation = new DoubleAnimation(y0, y0 - 40, TimeSpan.FromMilliseconds(travelMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        rect.BeginAnimation(Canvas.LeftProperty, xAnimation);
        rect.BeginAnimation(Canvas.TopProperty, yAnimation);
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
