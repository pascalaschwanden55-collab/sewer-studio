using System;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeitregeln der Startanimation 5.0 (Entscheid Pascal 14.09.2026, Variante «Kugel, straffer»).
/// Reine Rechnung ohne WPF: Der Splash fragt hier je Bild nach, was wann sichtbar ist.
/// Alle Zeiten sind Sekunden seit Start der Animationsuhr.
/// </summary>
internal static class StartupSplashChoreografie
{
    /// <summary>Der erste Knoten beginnt bei 0,15 s, der letzte bei 1,15 s (Spiralreihenfolge).</summary>
    public const double AufbauStartSekunden = 0.15;
    public const double AufbauSpanneSekunden = 1.0;
    /// <summary>Jeder Knoten und jede Verbindung blendet in 0,35 s ein; alle Knoten stehen bei 1,5 s.</summary>
    public const double EinblendDauerSekunden = 0.35;
    public const double VerbindungStartSekunden = 0.7;
    /// <summary>Impulse erst, wenn das Netz steht. Das Ende setzt allein der Bereit-Pfad.</summary>
    public const double ImpulseAbSekunden = 1.5;
    public const double ErsteWelleSekunden = 3.0;
    public const double ZweiteWelleSekunden = 5.4;
    public const double WellenDauerSekunden = 1.5;
    /// <summary>Laedt das Programm laenger als acht Sekunden, folgen weitere Wellen im alten Takt.</summary>
    public const double WellenAbstandSekunden = 2.7;
    /// <summary>Gruenes Aufleuchten des ganzen Netzes im Bereit-Moment.</summary>
    public const double BereitDauerSekunden = 0.56;
    public const int RingDauerMillisekunden = 1200;
    public const int RingStaffelungMillisekunden = 150;

    // Gleitkomma: 8,1 - 5,4 ist nicht exakt 2,7. Ohne Toleranz faellt ein Wellenstart
    // auf den Takt davor zurueck.
    private const double Toleranz = 1e-9;

    public static double KnotenSichtbarkeit(int index, int anzahl, double sekunden)
        => Einblendung(AufbauStartSekunden + AufbauSpanneSekunden * Anteil(index, anzahl), sekunden);

    public static double VerbindungSichtbarkeit(int a, int b, int anzahl, double sekunden)
        => Einblendung(VerbindungStartSekunden + AufbauSpanneSekunden * Anteil((a + b) / 2.0, anzahl), sekunden);

    public static bool ImpulseErlaubt(double sekunden) => sekunden >= ImpulseAbSekunden;

    /// <summary>-1 ohne Welle, sonst 0..1 als Fortschritt des Sweeps von links nach rechts.</summary>
    public static double Wellenfortschritt(double sekunden)
    {
        if (sekunden < ErsteWelleSekunden)
            return -1;

        double start;
        if (sekunden < ZweiteWelleSekunden)
        {
            start = ErsteWelleSekunden;
        }
        else
        {
            var takt = Math.Floor((sekunden - ZweiteWelleSekunden) / WellenAbstandSekunden + Toleranz);
            start = ZweiteWelleSekunden + takt * WellenAbstandSekunden;
        }

        var fortschritt = (sekunden - start) / WellenDauerSekunden;
        return fortschritt < 1.0 - Toleranz ? fortschritt : -1;
    }

    public static int RingStartMillisekunden(int ringIndex) => Math.Max(0, ringIndex) * RingStaffelungMillisekunden;

    /// <summary>0 ausserhalb, dazwischen ein weicher Halbbogen mit Spitze 1 in der Mitte.</summary>
    public static double Bereitanteil(double sekundenSeitBereit)
    {
        if (sekundenSeitBereit <= 0 || sekundenSeitBereit >= BereitDauerSekunden)
            return 0;
        return Math.Sin(Math.PI * sekundenSeitBereit / BereitDauerSekunden);
    }

    private static double Anteil(double index, int anzahl)
        => anzahl > 1 ? Math.Clamp(index / (anzahl - 1), 0, 1) : 0;

    private static double Einblendung(double startSekunden, double sekunden)
    {
        var x = (sekunden - startSekunden) / EinblendDauerSekunden;
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        var rest = 1 - x;
        return 1 - rest * rest * rest;
    }
}
