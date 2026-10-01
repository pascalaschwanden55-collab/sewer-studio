using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Gemeinsame Hilfen fuer WPF-Tests: Baumsuche und der Ruhepunkt nach <c>Show()</c>.
/// </summary>
internal static class WpfTestHilfe
{
    /// <summary>
    /// Alle Nachfahren des Typs <typeparamref name="T"/> im VISUELLEN Baum (Tiefensuche, Reihenfolge
    /// wie im Baum). Sieht nur, was schon eine Vorlage aufgebaut hat - das Element muss geladen sein.
    /// </summary>
    public static IEnumerable<T> Nachfahren<T>(DependencyObject wurzel) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
        {
            var kind = VisualTreeHelper.GetChild(wurzel, i);
            if (kind is T treffer)
                yield return treffer;
            foreach (var tiefer in Nachfahren<T>(kind))
                yield return tiefer;
        }
    }

    /// <summary>
    /// Alle Nachfahren im LOGISCHEN Baum (Tiefensuche, jedes Kind vor seinen eigenen Nachfahren).
    /// Findet auch nicht gerenderte Elemente, aber keine Teile aus Steuerelementvorlagen.
    /// </summary>
    public static IEnumerable<DependencyObject> LogischeNachfahren(DependencyObject wurzel)
    {
        foreach (var kind in LogicalTreeHelper.GetChildren(wurzel).OfType<DependencyObject>())
        {
            yield return kind;
            foreach (var nachfahr in LogischeNachfahren(kind))
                yield return nachfahr;
        }
    }

    /// <summary>
    /// Ruhepunkt nach <c>Show()</c> in einem <c>[IsolatedWpfFact]</c>-Kindprozess: Layout erzwingen und
    /// pruefen, dass das Fenster geladen ist. Bewusst KEIN <c>Dispatcher.Invoke(..., ApplicationIdle)</c>
    /// (kann dort haengen bleiben, siehe docs/architektur/oberflaeche.md, Aufgabe 5).
    /// </summary>
    public static void WarteAufLayout(Window fenster)
    {
        fenster.UpdateLayout();
        Assert.True(fenster.IsLoaded);
    }
}
