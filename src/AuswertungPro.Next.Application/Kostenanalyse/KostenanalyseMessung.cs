using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Ergebnis der Rueckblick-Messung.
///
/// Abdeckung und Treffer stehen bewusst nebeneinander — und die Positionen sind in
/// Routine und entscheidende getrennt: Reinigung, TV-Vorkontrolle oder Abnahme kommen
/// in fast jeder Haltung vor. Sie zu treffen ist keine Kunst und wuerde jede Gesamtzahl
/// schoenfaerben.
/// </summary>
public sealed record KostenanalyseMessErgebnis
{
    public int Gesamt { get; init; }
    public int MitVorschlag { get; init; }
    public int Enthalten { get; init; }

    // Ueber alle Positionen — die geschenkte Zahl.
    public int PositionenRichtig { get; init; }
    public int PositionenZuviel { get; init; }
    public int PositionenFehlend { get; init; }

    /// <summary>Positionen, die in mindestens 90 % aller Faelle vorkommen.</summary>
    public IReadOnlyList<string> RoutinePositionen { get; init; } = [];

    /// <summary>Alle uebrigen — hier entscheidet sich, ob das Verfahren etwas kann.</summary>
    public IReadOnlyList<string> EntscheidendePositionen { get; init; } = [];

    // Nur die entscheidenden Positionen — die ehrliche Zahl.
    public int EntscheidendRichtig { get; init; }
    public int EntscheidendZuviel { get; init; }
    public int EntscheidendFehlend { get; init; }

    // Gegenprobe: immer dasselbe Standardpaket, ohne jede Aehnlichkeitssuche.
    public int BasisRichtig { get; init; }
    public int BasisZuviel { get; init; }
    public int BasisFehlend { get; init; }

    /// <summary>Anteil der Haltungen, die ueberhaupt einen Vorschlag bekamen.</summary>
    public double Abdeckung => Gesamt == 0 ? 0d : (double)MitVorschlag / Gesamt;

    public double EntscheidendGenauigkeit => Anteil(EntscheidendRichtig, EntscheidendZuviel);
    public double EntscheidendVollstaendigkeit => Anteil(EntscheidendRichtig, EntscheidendFehlend);
    public double BasisGenauigkeit => Anteil(BasisRichtig, BasisZuviel);
    public double BasisVollstaendigkeit => Anteil(BasisRichtig, BasisFehlend);

    private static double Anteil(int richtig, int rest)
        => richtig + rest == 0 ? 0d : (double)richtig / (richtig + rest);
}

/// <summary>
/// Misst die Vorhersageguete rueckblickend: Jeder Fall wird OHNE sich selbst vorhergesagt
/// und mit dem echten Paket verglichen (Leave-one-out).
///
/// Bewertet werden nur unbeeinflusste Faelle. Ein Fall, bei dem der Vorschlag vorher
/// sichtbar war, bleibt Lernmaterial — sonst misst sich das Verfahren an sich selbst.
///
/// Zwei Zahlen sind Pflicht, sonst taeuscht die Messung:
/// - Abdeckung: Ein Modell, das nur schweigt, hat null Fehler und null Nutzen.
/// - Gegenprobe: Wenn "immer dasselbe Standardpaket" genauso gut ist, bringt die
///   Aehnlichkeitssuche nichts.
/// </summary>
public static class KostenanalyseMessung
{
    /// <summary>Ab diesem Anteil gilt eine Position als Routine.</summary>
    public const double RoutineSchwelle = 0.9;

    public static KostenanalyseMessErgebnis Messe(IReadOnlyList<Kostenfall> faelle)
    {
        ArgumentNullException.ThrowIfNull(faelle);

        var messbar = faelle.Where(f => f.Herkunft == KostenfallHerkunft.Unbeeinflusst).ToList();
        var (routine, entscheidend, standardpaket) = TeileAuf(faelle);

        var mitVorschlag = 0;
        var enthalten = 0;
        var richtig = 0;
        var zuviel = 0;
        var fehlend = 0;
        var eRichtig = 0;
        var eZuviel = 0;
        var eFehlend = 0;
        var bRichtig = 0;
        var bZuviel = 0;
        var bFehlend = 0;

        foreach (var fall in messbar)
        {
            // Ohne sich selbst — sonst waere die Antwort im Bestand enthalten.
            var andere = faelle.Where(f => !ReferenceEquals(f, fall)).ToList();
            var vorschlag = KostenVorschlagPolicy.Schlage(fall.Merkmale, andere);

            if (vorschlag.IstEnthaltung)
            {
                enthalten++;
                continue;
            }

            mitVorschlag++;

            var vorhergesagt = Menge(vorschlag.Positionen.Select(p => p.ItemKey));
            var tatsaechlich = Menge(fall.Positionen.Select(p => p.ItemKey));

            richtig += Schnitt(vorhergesagt, tatsaechlich);
            zuviel += Differenz(vorhergesagt, tatsaechlich);
            fehlend += Differenz(tatsaechlich, vorhergesagt);

            // Nur die entscheidenden Positionen.
            var vEnt = Menge(vorhergesagt.Where(entscheidend.Contains));
            var tEnt = Menge(tatsaechlich.Where(entscheidend.Contains));

            eRichtig += Schnitt(vEnt, tEnt);
            eZuviel += Differenz(vEnt, tEnt);
            eFehlend += Differenz(tEnt, vEnt);

            // Gegenprobe auf demselben Ausschnitt.
            bRichtig += Schnitt(standardpaket, tEnt);
            bZuviel += Differenz(standardpaket, tEnt);
            bFehlend += Differenz(tEnt, standardpaket);
        }

        return new KostenanalyseMessErgebnis
        {
            Gesamt = messbar.Count,
            MitVorschlag = mitVorschlag,
            Enthalten = enthalten,
            PositionenRichtig = richtig,
            PositionenZuviel = zuviel,
            PositionenFehlend = fehlend,
            RoutinePositionen = routine.OrderBy(k => k, StringComparer.Ordinal).ToList(),
            EntscheidendePositionen = entscheidend.OrderBy(k => k, StringComparer.Ordinal).ToList(),
            EntscheidendRichtig = eRichtig,
            EntscheidendZuviel = eZuviel,
            EntscheidendFehlend = eFehlend,
            BasisRichtig = bRichtig,
            BasisZuviel = bZuviel,
            BasisFehlend = bFehlend
        };
    }

    /// <summary>
    /// Teilt die Positionen in Routine und entscheidende und bildet das Standardpaket
    /// der Gegenprobe (entscheidende Positionen, die in der Mehrheit aller Faelle stehen).
    /// </summary>
    private static (HashSet<string> Routine, HashSet<string> Entscheidend, HashSet<string> Standardpaket)
        TeileAuf(IReadOnlyList<Kostenfall> faelle)
    {
        var routine = Menge([]);
        var entscheidend = Menge([]);
        var standardpaket = Menge([]);

        if (faelle.Count == 0)
            return (routine, entscheidend, standardpaket);

        var zaehler = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in faelle.SelectMany(f => Menge(f.Positionen.Select(p => p.ItemKey))))
            zaehler[key] = zaehler.TryGetValue(key, out var n) ? n + 1 : 1;

        foreach (var (key, anzahl) in zaehler)
        {
            if ((double)anzahl / faelle.Count >= RoutineSchwelle)
            {
                routine.Add(key);
                continue;
            }

            entscheidend.Add(key);
            if (anzahl * 2 > faelle.Count)
                standardpaket.Add(key);
        }

        return (routine, entscheidend, standardpaket);
    }

    private static HashSet<string> Menge(IEnumerable<string> keys)
        => new(keys, StringComparer.OrdinalIgnoreCase);

    private static int Schnitt(HashSet<string> a, HashSet<string> b)
        => a.Count(b.Contains);

    private static int Differenz(HashSet<string> a, HashSet<string> b)
        => a.Count(k => !b.Contains(k));
}
