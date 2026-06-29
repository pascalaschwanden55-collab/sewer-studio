using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Mathematik fuer die Haltungsgrafik-Skala: Maßstab-Verhaeltnis und Tick-Schritte.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral), damit unit-testbar.
/// </summary>
public static class HaltungsgrafikScaleCalculator
{
    /// <summary>
    /// Maßstab 1:N aus Haltungslaenge (m) und nutzbarer Plot-Hoehe (in PDF-Punkten).
    /// Null, wenn nicht berechenbar (Laenge oder Hoehe nicht positiv).
    /// </summary>
    public static int? ComputeScaleRatio(double length, int plotHeightPx)
    {
        if (length <= 0)
            return null;

        var plotCm = plotHeightPx * 2.54 / 72.0;
        if (plotCm <= 0.01)
            return null;

        var mPerCm = length / plotCm;
        if (mPerCm <= 0)
            return null;

        return (int)Math.Round(mPerCm * 100.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Tick-Positionen (m) von 0 bis <paramref name="length"/> in <paramref name="step"/>-Schritten,
    /// inklusive Endpunkt. Leer bei nicht-positiver Eingabe.
    /// </summary>
    public static List<double> BuildTicks(double length, double step)
    {
        var list = new List<double>();
        if (length <= 0 || step <= 0)
            return list;

        var m = 0d;
        while (m <= length + 1e-6)
        {
            list.Add(m);
            m += step;
        }

        if (list.Count == 0 || Math.Abs(list[^1] - length) > 1e-6)
            list.Add(length);

        return list.Distinct().OrderBy(x => x).ToList();
    }

    /// <summary>Waehlt einen Tick-Schritt, sodass 4..8 Ticks entstehen; sonst groesster Kandidat.</summary>
    public static double ChooseTickStep(double length)
    {
        var candidates = new[] { 0.2, 0.5, 1d, 2d, 5d, 10d, 20d, 50d };
        if (length <= 0)
            return 1;

        foreach (var step in candidates)
        {
            var count = length / step;
            if (count >= 4 && count <= 8)
                return step;
        }

        return candidates.Last();
    }
}
