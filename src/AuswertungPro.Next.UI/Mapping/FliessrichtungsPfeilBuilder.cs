using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Fliessrichtungs-Pfeil einer Haltung als "V" aus zwei kurzen Linien (robust in jeder
/// Mapsui-Version, kein Bitmap noetig): Spitze bei halber Strecke, Fluegel je 30 Grad
/// von der Rueckrichtung, Laenge = groesse (Mercator-Einheiten, an die Aufloesung gekoppelt).
/// Fliessrichtung = Digitalisierungsrichtung der Haltung (von-Schacht -> bis-Schacht).
/// </summary>
public static class FliessrichtungsPfeilBuilder
{
    private const double FluegelWinkelGrad = 30d;

    public static IReadOnlyList<((double X, double Y) Spitze, (double X, double Y) Ende)> BauePfeilLinien(
        IReadOnlyList<(double X, double Y)> linie,
        double groesse)
    {
        var spitze = PolylineMath.PunktBeiAnteil(linie, 0.5);
        var richtung = PolylineMath.RichtungGradBeiAnteil(linie, 0.5);
        if (spitze is null || richtung is null || groesse <= 0d)
            return [];

        var rueck = richtung.Value + 180d;
        return
        [
            (spitze.Value, Versetzt(spitze.Value, rueck - FluegelWinkelGrad, groesse)),
            (spitze.Value, Versetzt(spitze.Value, rueck + FluegelWinkelGrad, groesse))
        ];
    }

    private static (double X, double Y) Versetzt((double X, double Y) von, double winkelGrad, double laenge)
    {
        var rad = winkelGrad * Math.PI / 180d;
        return (von.X + Math.Cos(rad) * laenge, von.Y + Math.Sin(rad) * laenge);
    }
}
