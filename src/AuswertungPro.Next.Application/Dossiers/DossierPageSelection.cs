using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Welche Blätter des fertigen Dossiers ins Gesamt-PDF kommen.
///
/// Standard ist: alle. Wer nichts anfasst, bekommt das vollständige Dossier —
/// eine Auswahl, die man erst treffen müsste, wäre eine Falle.
///
/// Reine Zustandsführung ohne Oberfläche: Das Fenster zeigt nur, was hier
/// entschieden wird, und die Ausgabe fragt nur <see cref="Ausgeschlossen"/>.
/// Als Pflichtblatt markierte Seiten bleiben auch bei „Keine" gewählt.
/// </summary>
public sealed class DossierPageSelection
{
    private readonly HashSet<int> _ausgeschlossen = new();
    private readonly HashSet<int> _pflichtblaetter;

    public DossierPageSelection(int blaetter)
        : this(blaetter, pflichtblaetter: null)
    {
    }

    public DossierPageSelection(int blaetter, IEnumerable<int>? pflichtblaetter)
    {
        if (blaetter < 0)
            throw new ArgumentOutOfRangeException(nameof(blaetter));

        Blaetter = blaetter;
        _pflichtblaetter = new HashSet<int>(
            (pflichtblaetter ?? Enumerable.Empty<int>())
                .Where(seite => seite >= 1 && seite <= blaetter));
    }

    /// <summary>Wie viele Blätter das Dossier insgesamt hat.</summary>
    public int Blaetter { get; }

    /// <summary>Die Seitennummern (1-basiert), die NICHT in die Datei sollen.</summary>
    public IReadOnlySet<int> Ausgeschlossen => _ausgeschlossen;

    public int GewaehlteAnzahl => Blaetter - _ausgeschlossen.Count;

    /// <summary>Ein PDF ohne Seiten wäre kaputt — dann bleibt der Knopf gesperrt.</summary>
    public bool DarfErzeugen => GewaehlteAnzahl > 0;

    public bool IstGewaehlt(int seite) => !_ausgeschlossen.Contains(seite);

    public bool IstPflichtblatt(int seite) => _pflichtblaetter.Contains(seite);

    public void Setze(int seite, bool gewaehlt)
    {
        if (seite < 1 || seite > Blaetter)
            return;

        if (gewaehlt)
            _ausgeschlossen.Remove(seite);
        else if (IstPflichtblatt(seite))
            _ausgeschlossen.Remove(seite);
        else
            _ausgeschlossen.Add(seite);
    }

    public void Alle() => _ausgeschlossen.Clear();

    public void Keine()
    {
        foreach (var seite in Enumerable.Range(1, Blaetter))
        {
            if (!IstPflichtblatt(seite))
                _ausgeschlossen.Add(seite);
        }
    }

    /// <summary>Was gerade erzeugt würde — in einem Satz.</summary>
    public string Beschreibung => GewaehlteAnzahl switch
    {
        0 => "Kein Blatt gewählt",
        var gewaehlt when gewaehlt == Blaetter => string.Format(
            CultureInfo.CurrentCulture, "Alle {0} Blätter", Blaetter),
        var gewaehlt => string.Format(
            CultureInfo.CurrentCulture, "{0} von {1} Blättern", gewaehlt, Blaetter)
    };
}
