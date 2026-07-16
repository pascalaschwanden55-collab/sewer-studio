using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Sichtbarer Host fuer Toasts (unten rechts). Haelt die UI-freie <see cref="ToastQueueLogic"/>,
/// spiegelt deren sichtbare Toasts in eine ObservableCollection und raeumt ueber einen 500-ms-Timer
/// ab. Der Timer laeuft nur, solange es Toasts gibt. Einblenden per Fade + Slide-up (P2-Token).
/// </summary>
public partial class ToastHost : UserControl
{
    private readonly ToastQueueLogic _logic = new();
    private readonly ObservableCollection<ToastItem> _items = new();
    private readonly DispatcherTimer _timer;

    public ToastHost()
    {
        InitializeComponent();
        ItemsHost.ItemsSource = _items;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += OnTimerTick;
    }

    // Monotone Uhr (kein DateTime) -> immun gegen Zeitumstellung.
    private static long NowMs() => Environment.TickCount64;

    /// <summary>Meldung anzeigen. Threadsicher — marshalt bei Bedarf auf den UI-Thread.</summary>
    public void Enqueue(string message, ToastSeverity severity)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Enqueue(message, severity)));
            return;
        }

        _logic.Show(message, severity, NowMs());
        Sync();
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _logic.Prune(NowMs());
        Sync();
        if (_logic.Visible.Count == 0 && _logic.PendingCount == 0)
            _timer.Stop();
    }

    // Sichtbare Toasts der Logik in die gebundene Collection spiegeln (Reihenfolge bleibt erhalten).
    private void Sync()
    {
        var visible = _logic.Visible;

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (visible.All(v => v.Id != _items[i].Id))
                _items.RemoveAt(i);
        }

        foreach (var item in visible)
        {
            if (_items.All(x => x.Id != item.Id))
                _items.Add(item);
        }
    }

    private void Toast_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border)
            return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(AnimationTokens.Normal);

        border.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, 1d, duration) { EasingFunction = ease });
        var translate = EnsureMutableTranslateTransform(border);
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12d, 0d, duration) { EasingFunction = ease });
    }

    /// <summary>
    /// Laesst die Lebenslinie ueber die Anzeigedauer des Toasts von voller Breite auf null
    /// schrumpfen — der Nutzer sieht, wie lange die Meldung noch bleibt. Linear, weil sie eine
    /// Uhr abbildet: eine Beschleunigung waere schlicht gelogen.
    /// </summary>
    private void LifeLine_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border line || line.DataContext is not ToastItem item)
            return;

        // Fehler bleiben bis zum Klick — ohne Ablauf gibt es nichts abzulaufen.
        if (item.DurationMs is not { } durationMs)
            return;

        var remainingMs = _logic.RemainingMs(item.Id, NowMs()) ?? durationMs;
        var scale = (ScaleTransform)line.RenderTransform;

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1d, 0d, new Duration(TimeSpan.FromMilliseconds(Math.Max(remainingMs, 1)))));
    }

    private static TranslateTransform EnsureMutableTranslateTransform(Border border)
    {
        if (border.RenderTransform is TranslateTransform { IsFrozen: false } translate)
            return translate;

        var mutable = border.RenderTransform is TranslateTransform frozen
            ? frozen.CloneCurrentValue()
            : new TranslateTransform { Y = 12d };

        border.RenderTransform = mutable;
        return mutable;
    }

    private void Toast_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ToastItem item })
        {
            _logic.Dismiss(item.Id, NowMs());
            Sync();
        }
    }
}
