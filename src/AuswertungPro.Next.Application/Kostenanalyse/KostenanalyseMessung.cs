using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>Ergebnis der Rueckblick-Messung. Abdeckung und Treffer stehen bewusst nebeneinander.</summary>
public sealed record KostenanalyseMessErgebnis
{
    public int Gesamt { get; init; }
    public int MitVorschlag { get; init; }
    public int Enthalten { get; init; }
    public int PositionenRichtig { get; init; }
    public int PositionenZuviel { get; init; }
    public int PositionenFehlend { get; init; }

    /// <summary>Anteil der Haltungen, die ueberhaupt einen Vorschlag bekamen.</summary>
    public double Abdeckung => Gesamt == 0 ? 0d : (double)MitVorschlag / Gesamt;
}

/// <summary>
/// Misst die Vorhersageguete rueckblickend: Jeder Fall wird OHNE sich selbst vorhergesagt
/// und mit dem echten Paket verglichen (Leave-one-out).
///
/// Bewertet werden nur unbeeinflusste Faelle. Ein Fall, bei dem der Vorschlag vorher
/// sichtbar war, bleibt Lernmaterial — sonst misst sich das Verfahren an sich selbst.
///
/// Abdeckung wird immer mitberichtet: Ein Modell, das nur schweigt, hat null Fehler
/// und null Nutzen.
/// </summary>
public static class KostenanalyseMessung
{
    public static KostenanalyseMessErgebnis Messe(IReadOnlyList<Kostenfall> faelle)
    {
        ArgumentNullException.ThrowIfNull(faelle);

        var messbar = faelle.Where(f => f.Herkunft == KostenfallHerkunft.Unbeeinflusst).ToList();

        var mitVorschlag = 0;
        var enthalten = 0;
        var richtig = 0;
        var zuviel = 0;
        var fehlend = 0;

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

            var vorhergesagt = new HashSet<string>(
                vorschlag.Positionen.Select(p => p.ItemKey), StringComparer.OrdinalIgnoreCase);
            var tatsaechlich = new HashSet<string>(
                fall.Positionen.Select(p => p.ItemKey), StringComparer.OrdinalIgnoreCase);

            richtig += vorhergesagt.Intersect(tatsaechlich, StringComparer.OrdinalIgnoreCase).Count();
            zuviel += vorhergesagt.Except(tatsaechlich, StringComparer.OrdinalIgnoreCase).Count();
            fehlend += tatsaechlich.Except(vorhergesagt, StringComparer.OrdinalIgnoreCase).Count();
        }

        return new KostenanalyseMessErgebnis
        {
            Gesamt = messbar.Count,
            MitVorschlag = mitVorschlag,
            Enthalten = enthalten,
            PositionenRichtig = richtig,
            PositionenZuviel = zuviel,
            PositionenFehlend = fehlend
        };
    }
}
