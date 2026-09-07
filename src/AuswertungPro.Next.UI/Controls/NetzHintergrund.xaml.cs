using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Nova-Etappe 2: leises Leitungsnetz im Fensterhintergrund (reine Optik, keine Eingabe).
///
/// Die Bewegung selbst steckt WPF-frei in <see cref="NetzHintergrundModell"/>; dieses Control
/// bildet nur ab: es zeichnet Knoten (<c>GlassBorderBrush</c>) und Verbindungen
/// (<c>AccentBrush</c>, mit dem Abstand abnehmende Deckkraft) auf einen <see cref="Canvas"/>.
///
/// Der Zeittakt (33 ms) laeuft nur, wenn wirklich etwas zu sehen sein soll: die Engine ist
/// eingeschaltet (<see cref="IsEngineEnabled"/>), der Nutzer hat Dauer-Animationen nicht
/// abgeschaltet (<see cref="MotionSettings.ReduceMotion"/>), das Control ist sichtbar und das
/// Fenster ist aktiv (kein Dialog offen). Sonst bleibt ein einzelnes Standbild stehen, das beim
/// Laden einmal berechnet wird; kein leerer Hintergrund, aber auch keine unsichtbare Arbeit.
/// </summary>
public partial class NetzHintergrund : UserControl
{
    private const double IntervallMs = 33;
    private const double LinienDeckkraft = 0.09;

    public static readonly DependencyProperty IsEngineEnabledProperty =
        DependencyProperty.Register(nameof(IsEngineEnabled), typeof(bool), typeof(NetzHintergrund),
            new PropertyMetadata(true, static (d, _) => ((NetzHintergrund)d).AktualisiereZustand()));

    /// <summary>Von aussen gesetzt (MainWindow bindet an <c>ShellViewModel.HintergrundEngine</c>).
    /// Dieses Control kennt keinen Service-Locator und liest <c>AppSettings</c> nicht selbst.</summary>
    public bool IsEngineEnabled
    {
        get => (bool)GetValue(IsEngineEnabledProperty);
        set => SetValue(IsEngineEnabledProperty, value);
    }

    private NetzHintergrundModell? _modell;
    private DispatcherTimer? _timer;
    private Window? _fenster;

    public NetzHintergrund()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => AktualisiereZustand();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _fenster = Window.GetWindow(this);
        if (_fenster is not null)
        {
            _fenster.Activated += OnFensterAktivitaetGeaendert;
            _fenster.Deactivated += OnFensterAktivitaetGeaendert;
        }

        SizeChanged += OnSizeChanged;
        NeuesModellUndStandbild();
        AktualisiereZustand();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopTimer();
        SizeChanged -= OnSizeChanged;
        if (_fenster is not null)
        {
            _fenster.Activated -= OnFensterAktivitaetGeaendert;
            _fenster.Deactivated -= OnFensterAktivitaetGeaendert;
            _fenster = null;
        }
    }

    private void OnFensterAktivitaetGeaendert(object? sender, EventArgs e) => AktualisiereZustand();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        NeuesModellUndStandbild();
        AktualisiereZustand();
    }

    /// <summary>Neues, zur aktuellen Groesse passendes Modell; ein Schritt fuer ein sofort
    /// sichtbares, leicht bewegtes Standbild, bevor der Timer ueberhaupt entscheidet.</summary>
    private void NeuesModellUndStandbild()
    {
        var breite = (int)Math.Max(1, ActualWidth);
        var hoehe = (int)Math.Max(1, ActualHeight);
        _modell = new NetzHintergrundModell(breite, hoehe);
        _modell.Schritt();
        Zeichne();
    }

    private void AktualisiereZustand()
    {
        var fensterAktiv = _fenster?.IsActive ?? true;
        var sollLaufen = IsEngineEnabled && !MotionSettings.ReduceMotion && IsVisible && fensterAktiv;
        if (sollLaufen)
            StartTimer();
        else
            StopTimer();
    }

    private void StartTimer()
    {
        if (_timer is not null)
            return;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(IntervallMs)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _modell?.Schritt();
        Zeichne();
    }

    private void Zeichne()
    {
        Flaeche.Children.Clear();
        if (_modell is null)
            return;

        foreach (var v in _modell.Verbindungen())
        {
            var a = _modell.Knoten[v.A];
            var b = _modell.Knoten[v.B];
            var linie = new Line
            {
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y,
                StrokeThickness = 1,
                Opacity = LinienDeckkraft * v.Alpha,
                IsHitTestVisible = false
            };
            linie.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
            Flaeche.Children.Add(linie);
        }

        foreach (var k in _modell.Knoten)
        {
            var punkt = new Ellipse
            {
                Width = k.R * 2,
                Height = k.R * 2,
                IsHitTestVisible = false
            };
            punkt.SetResourceReference(Shape.FillProperty, "GlassBorderBrush");
            Canvas.SetLeft(punkt, k.X - k.R);
            Canvas.SetTop(punkt, k.Y - k.R);
            Flaeche.Children.Add(punkt);
        }
    }
}
