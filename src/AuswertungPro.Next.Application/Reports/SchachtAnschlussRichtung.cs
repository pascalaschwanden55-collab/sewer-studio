using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Rechnung: die Richtung, unter der eine Leitung einen Schacht verlaesst.
///
/// Der Linienzug einer Haltung beginnt oder endet am Schacht. Genommen wird das Ende, das
/// naeher am Schachtpunkt liegt — aber nur, wenn es wirklich dort liegt
/// (<see cref="StandardToleranzM"/>): Eine Leitung, die einen Meter neben dem Schacht endet,
/// gehoert zu einem anderen Knoten. Die Richtung ist die zum naechsten Stuetzpunkt, damit
/// ein gebogener Verlauf die Lage AM Schacht zeigt und nicht die zum fernen Ende.
///
/// Azimut: 0° = Nord, 90° = Ost, im Uhrzeigersinn — die Konvention der Vermessung. Fuer den
/// Grundriss mit Auslauf oben rechnet <see cref="Relativ"/> auf den Auslauf um.
/// </summary>
public static class SchachtAnschlussRichtung
{
    public const double StandardToleranzM = 1.0;

    /// <summary>Azimut in Grad, oder <c>null</c>, wenn kein Ende am Schacht liegt.</summary>
    public static double? Azimut(XtfPunkt schacht, IReadOnlyList<XtfPunkt>? verlauf, double toleranzM = StandardToleranzM)
    {
        ArgumentNullException.ThrowIfNull(schacht);
        if (verlauf is null || verlauf.Count < 2)
            return null;

        var anfang = verlauf[0];
        var ende = verlauf[^1];
        var abstandAnfang = Abstand(schacht, anfang);
        var abstandEnde = Abstand(schacht, ende);

        XtfPunkt nah;
        XtfPunkt? naechster;
        if (abstandAnfang <= abstandEnde)
        {
            if (abstandAnfang > toleranzM)
                return null;
            nah = anfang;
            naechster = ErsterAnderer(verlauf, 0, +1);
        }
        else
        {
            if (abstandEnde > toleranzM)
                return null;
            nah = ende;
            naechster = ErsterAnderer(verlauf, verlauf.Count - 1, -1);
        }

        if (naechster is null)
            return null;

        var dx = naechster.Ost - nah.Ost;
        var dy = naechster.Nord - nah.Nord;
        var grad = Math.Atan2(dx, dy) * 180d / Math.PI;
        return grad < 0 ? grad + 360d : grad;
    }

    /// <summary>
    /// True, wenn das ENDE des Linienzugs am Schacht liegt (die Leitung fuehrt hinein), false,
    /// wenn der Anfang dort liegt; <c>null</c>, wenn keines der beiden Enden innerhalb der
    /// Toleranz liegt. Fuer Katasterleitungen ohne Projektnamen ist das die einzige Auskunft
    /// ueber Einlauf oder Auslauf.
    /// </summary>
    public static bool? EndetImSchacht(XtfPunkt schacht, IReadOnlyList<XtfPunkt>? verlauf, double toleranzM = StandardToleranzM)
    {
        ArgumentNullException.ThrowIfNull(schacht);
        if (verlauf is null || verlauf.Count < 2)
            return null;

        var abstandAnfang = Abstand(schacht, verlauf[0]);
        var abstandEnde = Abstand(schacht, verlauf[^1]);
        if (abstandAnfang <= abstandEnde)
            return abstandAnfang <= toleranzM ? false : null;
        return abstandEnde <= toleranzM ? true : null;
    }

    /// <summary>Winkel im Uhrzeigersinn ab <paramref name="bezugAzimut"/> (z.B. Auslauf = 12 Uhr), 0 bis 360.</summary>
    public static double Relativ(double azimut, double bezugAzimut)
    {
        var rest = (azimut - bezugAzimut) % 360d;
        return rest < 0 ? rest + 360d : rest;
    }

    /// <summary>Uhrlage 1 bis 12 aus einem relativen Winkel (0° = 12 Uhr).</summary>
    public static int Uhr(double relativGrad)
    {
        var stunde = (int)Math.Round(Relativ(relativGrad, 0d) / 30d) % 12;
        return stunde == 0 ? 12 : stunde;
    }

    private static double Abstand(XtfPunkt a, XtfPunkt b)
        => Math.Sqrt((a.Ost - b.Ost) * (a.Ost - b.Ost) + (a.Nord - b.Nord) * (a.Nord - b.Nord));

    /// <summary>Der naechste Stuetzpunkt, der nicht auf dem Endpunkt liegt (doppelte Punkte ueberspringen).</summary>
    private static XtfPunkt? ErsterAnderer(IReadOnlyList<XtfPunkt> verlauf, int start, int schritt)
    {
        var bezug = verlauf[start];
        for (var i = start + schritt; i >= 0 && i < verlauf.Count; i += schritt)
        {
            if (Abstand(bezug, verlauf[i]) > 0.001)
                return verlauf[i];
        }

        return null;
    }
}
