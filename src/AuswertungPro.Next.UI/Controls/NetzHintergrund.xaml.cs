using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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

    // Formen-Pool statt Neuanlage je Bild: Knoten sind fix (Anzahl/Radius aendern sich nicht,
    // solange das Modell lebt), Linien wachsen bei Bedarf und werden nie entfernt; nur
    // ueberzaehlige verstecken sich (Visibility=Collapsed). Vermeidet GC-Druck bei ~30 Bildern/s.
    private readonly List<Ellipse> _knotenFormen = new();
    private readonly List<Line> _linienFormen = new();

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
    /// sichtbares, leicht bewegtes Standbild, bevor der Timer ueberhaupt entscheidet.
    /// Der Formen-Pool passt nur hier neu zur (moeglicherweise geaenderten) Knotenzahl;
    /// deshalb wird nur hier der Canvas geleert, nie in <see cref="Zeichne"/> selbst.</summary>
    private void NeuesModellUndStandbild()
    {
        var breite = (int)Math.Max(1, ActualWidth);
        var hoehe = (int)Math.Max(1, ActualHeight);
        _modell = new NetzHintergrundModell(breite, hoehe);
        _modell.Schritt();

        Flaeche.Children.Clear();
        _knotenFormen.Clear();
        _linienFormen.Clear();
        foreach (var k in _modell.Knoten)
        {
            var punkt = new Ellipse
            {
                Width = k.R * 2,
                Height = k.R * 2,
                IsHitTestVisible = false
            };
            punkt.SetResourceReference(Shape.FillProperty, "GlassBorderBrush");
            _knotenFormen.Add(punkt);
            Flaeche.Children.Add(punkt);
        }

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

    /// <summary>Schreibt nur Position/Deckkraft in den bestehenden Formen-Pool; legt nie neue
    /// Knoten an und leert den Canvas nicht. Der Linien-Pool waechst bei mehr Verbindungen als
    /// bisher gebraucht; ueberzaehlige Linien vom letzten Bild werden nur versteckt, nicht
    /// entfernt. So entsteht bei ~30 Bildern/s kein Neuanlage-/GC-Druck durch diese Methode.</summary>
    private void Zeichne()
    {
        if (_modell is null)
            return;

        for (var i = 0; i < _modell.Knoten.Count && i < _knotenFormen.Count; i++)
        {
            var k = _modell.Knoten[i];
            var punkt = _knotenFormen[i];
            Canvas.SetLeft(punkt, k.X - k.R);
            Canvas.SetTop(punkt, k.Y - k.R);
        }

        var verbindungen = _modell.Verbindungen();
        for (var i = 0; i < verbindungen.Count; i++)
        {
            if (i >= _linienFormen.Count)
            {
                var neu = new Line { StrokeThickness = 1, IsHitTestVisible = false };
                neu.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
                _linienFormen.Add(neu);
                Flaeche.Children.Add(neu);
            }

            var v = verbindungen[i];
            var a = _modell.Knoten[v.A];
            var b = _modell.Knoten[v.B];
            var linie = _linienFormen[i];
            linie.X1 = a.X;
            linie.Y1 = a.Y;
            linie.X2 = b.X;
            linie.Y2 = b.Y;
            linie.Opacity = LinienDeckkraft * v.Alpha;
            linie.Visibility = Visibility.Visible;
        }

        for (var i = verbindungen.Count; i < _linienFormen.Count; i++)
            _linienFormen[i].Visibility = Visibility.Collapsed;
    }
}
