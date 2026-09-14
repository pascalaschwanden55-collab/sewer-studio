using System;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeitregeln der Startanimation 5.0 (Entscheid Pascal 14.09.2026, Variante D «Kugel,
/// deutlich neu»). Reine Rechnung ohne WPF: Der Splash fragt hier je Bild nach, wo ein
/// Knoten gerade ist, was sichtbar ist und ob eine Welle laeuft.
/// Alle Zeiten sind Sekunden seit Start der Animationsuhr.
/// </summary>
internal static class StartupSplashChoreografie
{
    /// <summary>Einflug: der erste Knoten startet bei 0,05 s, der letzte bei 1,05 s (Spiralreihenfolge).</summary>
    public const double EinflugStartSekunden = 0.05;
    public const double EinflugSpanneSekunden = 1.0;
    /// <summary>Jeder Knoten braucht 0,8 s von aussen bis zu seinem Platz; alle stehen bei 1,85 s.</summary>
    public const double EinflugDauerSekunden = 0.8;
    /// <summary>Die Kugel waechst waehrend des Einflugs von 72 auf 100 Prozent.</summary>
    public const double KugelStartMassstab = 0.72;
    public const double KugelWachstumSekunden = 2.0;
    /// <summary>Eine Verbindung erscheint in 0,25 s, sobald beide Enden angekommen sind, und blitzt dabei auf.</summary>
    public const double VerbindungEinblendungSekunden = 0.25;
    public const double VerbindungBlitzAbklingen = 5.0;
    /// <summary>Ringe zeichnen sich ab 0,3/0,5/0,7 s in 1,4 s als wachsender Bogen.</summary>
    public const double RingStartSekunden = 0.3;
    public const double RingStaffelungSekunden = 0.2;
    public const double RingBogenDauerSekunden = 1.4;
    public const double RingEinblendungSekunden = 0.4;
    public const double KernStartSekunden = 0.4;
    public const double KernDauerSekunden = 1.2;
    /// <summary>Impulse erst, wenn das Netz steht. Das Ende setzt allein der Bereit-Pfad.</summary>
    public const double ImpulseAbSekunden = 1.9;
    /// <summary>Scanwelle links nach rechts bei 3,0 s, Ringwelle aus dem Kern bei 5,0 s; beide wiederholen sich, solange das Programm laedt.</summary>
    public const double ScanwelleStartSekunden = 3.0;
    public const double ScanwelleDauerSekunden = 1.5;
    public const double RingwelleStartSekunden = 5.0;
    public const double RingwelleDauerSekunden = 1.2;
    public const double WellenAbstandSekunden = 5.4;
    /// <summary>Gruene Bereit-Welle: laeuft in 0,9 s vom Kern nach aussen und bleibt dann voll.</summary>
    public const double BereitwelleDauerSekunden = 0.9;

    private const double GoldenerWinkel = 2.39996322972865332;
    // Gleitkomma: 8,4 - 3,0 ist nicht exakt 5,4. Ohne Toleranz faellt ein Wellenstart
    // auf den Takt davor zurueck.
    private const double Toleranz = 1e-9;

    public static double EinflugStart(int index, int anzahl)
        => EinflugStartSekunden + EinflugSpanneSekunden * Anteil(index, anzahl);

    /// <summary>0 = noch am Startpunkt weit aussen, 1 = angekommen; dazwischen weich (ease-in-out).</summary>
    public static double EinflugFortschritt(int index, int anzahl, double sekunden)
        => EaseInOut((sekunden - EinflugStart(index, anzahl)) / EinflugDauerSekunden);

    /// <summary>Richtung des Startpunkts in rad (0..2π), deterministisch ueber alle Himmelsrichtungen verteilt.</summary>
    public static double EinflugRichtung(int index)
    {
        var winkel = (Math.Max(0, index) * GoldenerWinkel * 2.0 + 0.7) % (Math.PI * 2);
        return winkel < 0 ? winkel + Math.PI * 2 : winkel;
    }

    /// <summary>Abstand des Startpunkts als Vielfaches des Kugelradius (2,0 bis 3,3).</summary>
    public static double EinflugAbstand(int index)
    {
        var bruch = Math.Max(0, index) * 0.6180339887498949;
        return 2.0 + 1.3 * (bruch - Math.Floor(bruch));
    }

    public static double KugelMassstab(double sekunden)
        => KugelStartMassstab + (1 - KugelStartMassstab) * EaseOut(sekunden / KugelWachstumSekunden);

    public static double VerbindungAnkunft(int a, int b, int anzahl)
        => Math.Max(EinflugStart(a, anzahl), EinflugStart(b, anzahl)) + EinflugDauerSekunden;

    public static double VerbindungSichtbarkeit(int a, int b, int anzahl, double sekunden)
        => Clamp01((sekunden - VerbindungAnkunft(a, b, anzahl)) / VerbindungEinblendungSekunden);

    /// <summary>1 im Moment der Ankunft, danach exponentiell abklingend; 0 davor.</summary>
    public static double VerbindungBlitz(int a, int b, int anzahl, double sekunden)
    {
        var seit = sekunden - VerbindungAnkunft(a, b, anzahl);
        return seit < 0 ? 0 : Math.Exp(-VerbindungBlitzAbklingen * seit);
    }

    /// <summary>Anteil des gezeichneten Ringumfangs, 0..1.</summary>
    public static double RingBogen(int ringIndex, double sekunden)
        => EaseOut((sekunden - RingStart(ringIndex)) / RingBogenDauerSekunden);

    public static double RingSichtbarkeit(int ringIndex, double sekunden)
        => Clamp01((sekunden - RingStart(ringIndex)) / RingEinblendungSekunden);

    public static double KernGluehen(double sekunden)
        => EaseOut((sekunden - KernStartSekunden) / KernDauerSekunden);

    public static bool ImpulseErlaubt(double sekunden) => sekunden >= ImpulseAbSekunden;

    /// <summary>-1 ohne Welle, sonst 0..1 als Fortschritt des Sweeps von links nach rechts.</summary>
    public static double Wellenfortschritt(double sekunden)
        => Periodisch(sekunden, ScanwelleStartSekunden, ScanwelleDauerSekunden);

    /// <summary>-1 ohne Welle, sonst 0..1 als Radius der Ringwelle (Anteil des vollen Laufs).</summary>
    public static double Ringwellenfortschritt(double sekunden)
        => Periodisch(sekunden, RingwelleStartSekunden, RingwelleDauerSekunden);

    /// <summary>0 vor dem Bereit-Moment, danach linear bis 1 und dann voll: Radius der gruenen Welle.</summary>
    public static double Bereitwelle(double sekundenSeitBereit)
        => Clamp01(sekundenSeitBereit / BereitwelleDauerSekunden);

    private static double RingStart(int ringIndex)
        => RingStartSekunden + RingStaffelungSekunden * Math.Max(0, ringIndex);

    private static double Periodisch(double sekunden, double start, double dauer)
    {
        if (sekunden < start)
            return -1;
        var takt = Math.Floor((sekunden - start) / WellenAbstandSekunden + Toleranz);
        var fortschritt = (sekunden - (start + takt * WellenAbstandSekunden)) / dauer;
        return fortschritt < 1.0 - Toleranz ? fortschritt : -1;
    }

    private static double Anteil(double index, int anzahl)
        => anzahl > 1 ? Math.Clamp(index / (anzahl - 1), 0, 1) : 0;

    private static double Clamp01(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x;

    private static double EaseOut(double x)
    {
        x = Clamp01(x);
        var rest = 1 - x;
        return 1 - rest * rest * rest;
    }

    private static double EaseInOut(double x)
    {
        x = Clamp01(x);
        if (x < 0.5)
            return 4 * x * x * x;
        var rest = -2 * x + 2;
        return 1 - rest * rest * rest / 2;
    }
}
