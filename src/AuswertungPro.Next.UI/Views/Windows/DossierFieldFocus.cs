using System.Collections.Generic;
using System.Linq;
using System.Windows;

using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeigt rechts nur die Stelle, die gerade im Blatt angeklickt wurde.
///
/// Pascals Modell: Klick in der Vorschau — rechts geht genau dieses Feld auf,
/// alle anderen werden ausgeblendet. Die Eingabeseite trägt für ein volles
/// Kapitel schnell zwei Dutzend Eingaben; sichtbar sein muss davon genau eine.
///
/// Sichtbar bleibt die angeklickte Stelle selbst, alles darin (Beschriftung,
/// Eingabe, Formatwerkzeuge) und der Weg dorthin (ihr Abschnitt). Alles andere
/// verschwindet — es wird nur ausgeblendet, nicht abgebaut, damit ein
/// geschriebener Text und der Cursor erhalten bleiben.
///
/// Der Rückweg über <see cref="ZeigeAlles"/> ist Pflicht und keine Zutat: Ein
/// leeres Feld hat in der Vorschau keinen Text zum Anklicken, und ein frisch
/// angelegtes Dossier ist vollständig leer. Ohne ihn käme man dort nie hinein.
/// </summary>
internal static class DossierFieldFocus
{
    public static void ZeigeNur(
        IEnumerable<FrameworkElement> alle,
        FrameworkElement fokus)
    {
        var kette = new HashSet<DependencyObject>(Vorfahren(fokus)) { fokus };

        foreach (var stelle in alle)
        {
            stelle.Visibility = kette.Contains(stelle) || IstDarin(stelle, fokus)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    public static void ZeigeAlles(IEnumerable<FrameworkElement> alle)
    {
        foreach (var stelle in alle)
            stelle.Visibility = Visibility.Visible;
    }

    /// <summary>Steckt <paramref name="stelle"/> innerhalb von <paramref name="fokus"/>?</summary>
    private static bool IstDarin(DependencyObject stelle, DependencyObject fokus)
        => Vorfahren(stelle).Any(vorfahr => ReferenceEquals(vorfahr, fokus));

    private static IEnumerable<DependencyObject> Vorfahren(DependencyObject start)
    {
        var aktuell = VisualTreeSafe.GetParentSafe(start);
        while (aktuell is not null)
        {
            yield return aktuell;
            aktuell = VisualTreeSafe.GetParentSafe(aktuell);
        }
    }
}
