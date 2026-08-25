using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Wie eine Eingabestelle auf sich aufmerksam macht — und wann die
/// Formatwerkzeuge sichtbar sind.
///
/// Beides gehört zum selben Befund: Ein Sprung aus dem Blatt setzte zwar den
/// Schreibfokus, sichtbar geblinkt hat aber nur das Blatt. Man landete
/// irgendwo und musste erst suchen, wo. Und unter jedem Textfeld stand
/// dauerhaft eine Werkzeugleiste, die jede Karte doppelt so hoch machte.
/// </summary>
internal static class DossierFieldHighlight
{
    private static readonly Color Blinkfarbe = Color.FromRgb(0xC0, 0x50, 0x50);

    private static readonly TimeSpan Blinkdauer = TimeSpan.FromMilliseconds(900);

    /// <summary>
    /// Die Werkzeugleiste ist genau dann sichtbar, wenn in der Karte
    /// gearbeitet wird.
    /// </summary>
    public static Visibility SichtbarkeitFuer(bool fokusInDerKarte)
        => fokusInDerKarte ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Zeigt die Formatwerkzeuge nur, solange in dieser Karte gearbeitet wird.
    ///
    /// Massgeblich ist der Fokus in der GANZEN Karte, nicht nur im Textfeld —
    /// sonst verschwände die Leiste in dem Moment, in dem man einen ihrer
    /// Knöpfe anklickt.
    /// </summary>
    public static void ZeigeWerkzeugeNurAmAktivenFeld(
        FrameworkElement karte,
        UIElement werkzeuge)
    {
        ArgumentNullException.ThrowIfNull(karte);
        ArgumentNullException.ThrowIfNull(werkzeuge);

        werkzeuge.Visibility = SichtbarkeitFuer(karte.IsKeyboardFocusWithin);

        karte.IsKeyboardFocusWithinChanged += (_, args)
            => werkzeuge.Visibility = SichtbarkeitFuer(args.NewValue is true);
    }

    /// <summary>
    /// Lässt eine Stelle rot aufblinken. Gegenstück zum Blinken im Blatt: Nach
    /// einem Sprung sieht man auf beiden Seiten, wo man gelandet ist.
    ///
    /// Der bisherige Hintergrund wird gemerkt und danach zurückgesetzt — sonst
    /// bliebe die Karte eingefärbt.
    /// </summary>
    public static void LasseAufblinken(FrameworkElement stelle)
    {
        var ziel = NaechsteFlaeche(stelle);
        if (ziel is null)
            return;

        var (setze, vorher) = ziel.Value;
        var pinsel = new SolidColorBrush(Blinkfarbe);
        setze(pinsel);

        pinsel.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
        {
            From = Blinkfarbe,
            To = Colors.Transparent,
            Duration = Blinkdauer
        });

        // Das Zuruecksetzen haengt bewusst an einer eigenen Uhr und nicht am
        // Ende der Animation: Ohne laufende Darstellung — etwa in einem Test
        // oder auf einem noch nicht gezeichneten Fenster — tickt die
        // Animationsuhr nie, und die Flaeche bliebe dauerhaft rot.
        var uhr = new DispatcherTimer { Interval = Blinkdauer };
        uhr.Tick += (_, _) =>
        {
            uhr.Stop();
            setze(vorher);
        };

        uhr.Start();
    }

    /// <summary>
    /// Die nächste Fläche, die sich einfärben lässt — die Stelle selbst oder
    /// ein Vorfahr. Ein reines Textfeld hat keinen eigenen Hintergrund, den man
    /// gefahrlos überschreiben könnte.
    /// </summary>
    private static (Action<Brush?> Setze, Brush? Vorher)? NaechsteFlaeche(DependencyObject? start)
    {
        for (var aktuell = start; aktuell is not null;
             aktuell = VisualTreeSafe.GetParentSafe(aktuell))
        {
            switch (aktuell)
            {
                case Border rahmen:
                    var alterRand = rahmen.Background;
                    return (pinsel => rahmen.Background = pinsel, alterRand);

                case Panel flaeche:
                    var altePlatte = flaeche.Background;
                    return (pinsel => flaeche.Background = pinsel, altePlatte);
            }
        }

        return null;
    }
}
