using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Findet zu einer Haltung die aehnlichsten gelernten Faelle.
///
/// Zuerst harte Grenzen (Durchmesser hoechstens eine Katalogstufe entfernt, mindestens
/// eine gemeinsame Schadensart), danach Rangfolge nach Schadensaehnlichkeit. Der
/// Durchmesser ist bewusst ein Filter und kein Gewicht: Eine DN 150 und eine DN 600
/// sind fachlich nicht vergleichbar, egal wie gut die Schaeden passen.
/// </summary>
public static class KostenfallAehnlichkeit
{
    /// <summary>Uebliche Nennweiten in aufsteigender Reihenfolge.</summary>
    public static readonly IReadOnlyList<int> DnStufen =
        [100, 125, 150, 185, 200, 250, 300, 350, 400, 500, 600, 700, 800, 900, 1000];

    /// <summary>Abstand in Katalogstufen; null, wenn eine Weite nicht im Katalog steht.</summary>
    public static int? DnStufenAbstand(int a, int b)
    {
        var indexA = IndexVon(a);
        var indexB = IndexVon(b);
        if (indexA < 0 || indexB < 0)
            return null;

        return Math.Abs(indexA - indexB);
    }

    /// <summary>Gemeinsame Schadensarten geteilt durch alle vorkommenden.</summary>
    public static double SchadensAehnlichkeit(KostenfallMerkmale a, KostenfallMerkmale b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var mengeA = new HashSet<string>(a.Schadensarten, StringComparer.OrdinalIgnoreCase);
        var mengeB = new HashSet<string>(b.Schadensarten, StringComparer.OrdinalIgnoreCase);
        if (mengeA.Count == 0 || mengeB.Count == 0)
            return 0d;

        var gemeinsam = mengeA.Intersect(mengeB, StringComparer.OrdinalIgnoreCase).Count();
        var insgesamt = mengeA.Union(mengeB, StringComparer.OrdinalIgnoreCase).Count();
        return insgesamt == 0 ? 0d : (double)gemeinsam / insgesamt;
    }

    public static IReadOnlyList<Kostenfall> FindeNachbarn(
        KostenfallMerkmale ziel,
        IReadOnlyList<Kostenfall> faelle,
        int maximal)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(faelle);

        if (ziel.DnMm is not > 0)
            return [];

        var anzahlZiel = ziel.Schaeden.Sum(s => s.Anzahl);
        var kandidaten = new List<(Kostenfall Fall, double Aehnlichkeit, int DnAbstand, int AnzahlAbstand)>();

        foreach (var fall in faelle)
        {
            if (fall.Merkmale.DnMm is not > 0)
                continue;

            var abstand = DnStufenAbstand(fall.Merkmale.DnMm.Value, ziel.DnMm.Value);
            if (abstand is null || abstand > 1)
                continue;

            var aehnlich = SchadensAehnlichkeit(ziel, fall.Merkmale);
            if (aehnlich <= 0d)
                continue;

            var anzahlFall = fall.Merkmale.Schaeden.Sum(s => s.Anzahl);
            kandidaten.Add((fall, aehnlich, abstand.Value, Math.Abs(anzahlZiel - anzahlFall)));
        }

        return kandidaten
            .OrderByDescending(k => k.Aehnlichkeit)
            .ThenBy(k => k.AnzahlAbstand)
            .ThenBy(k => k.DnAbstand)
            .ThenBy(k => k.Fall.Haltung, StringComparer.Ordinal) // stabile Reihenfolge
            .Take(maximal)
            .Select(k => k.Fall)
            .ToList();
    }

    private static int IndexVon(int dn)
    {
        for (var i = 0; i < DnStufen.Count; i++)
        {
            if (DnStufen[i] == dn)
                return i;
        }

        return -1;
    }
}
