using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Hält die Abschnitte der Dossier-Eingabeseite so, dass immer nur einer offen
/// ist.
///
/// Vorher standen alle Abschnitte offen, die der Aufbau als wichtig ansah — auf
/// der Verzeichnisseite waren das drei. Die gesuchte Eingabe lag dann irgendwo
/// in einer sehr langen Liste, und man musste sie suchen statt sie zu sehen.
///
/// Eigene Klasse, weil das eine Regel für sich ist: Sie lässt sich ohne Fenster,
/// Vorlage und Dossier prüfen, und die Eingabeseite hatte ihre zulässige Grösse
/// bereits einmal erreicht.
/// </summary>
internal sealed class DossierFieldSectionAccordion
{
    private readonly List<Expander> _abschnitte = new();

    /// <summary>Verhindert, dass das Zuklappen der anderen sich selbst auslöst.</summary>
    private bool _ordnet;

    /// <summary>Die Abschnitte der gerade gezeigten Seite, in ihrer Reihenfolge.</summary>
    public IReadOnlyList<Expander> Abschnitte => _abschnitte;

    /// <summary>Vergisst die Abschnitte der vorigen Seite.</summary>
    public void Leere() => _abschnitte.Clear();

    /// <summary>
    /// Nimmt einen Abschnitt auf. Wer ihn später aufklappt, klappt damit die
    /// übrigen zu.
    /// </summary>
    public void Merke(Expander abschnitt)
    {
        _abschnitte.Add(abschnitt);
        abschnitt.Expanded += (_, _) => OeffneNur(abschnitt);
    }

    /// <summary>Öffnet genau diesen Abschnitt und schliesst alle anderen.</summary>
    public void OeffneNur(Expander offen)
    {
        if (_ordnet || !_abschnitte.Contains(offen))
            return;

        _ordnet = true;
        try
        {
            offen.IsExpanded = true;

            foreach (var anderer in _abschnitte.Where(a => !ReferenceEquals(a, offen)))
                anderer.IsExpanded = false;
        }
        finally
        {
            _ordnet = false;
        }
    }

    /// <summary>
    /// Nach dem Neuaufbau steht genau ein Abschnitt offen: der erste, den der
    /// Aufbau als wichtig gekennzeichnet hat — sonst schlicht der erste.
    /// </summary>
    public void OeffneNurDenErsten()
    {
        var erster = _abschnitte.FirstOrDefault(abschnitt => abschnitt.IsExpanded)
            ?? _abschnitte.FirstOrDefault();

        if (erster is not null)
            OeffneNur(erster);
    }
}
