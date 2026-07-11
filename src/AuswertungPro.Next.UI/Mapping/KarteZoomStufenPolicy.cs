namespace AuswertungPro.Next.UI.Mapping;

/// <summary>Sichtbarkeits-Flags je Zoomstufe (eine Entscheidung, vier Layer).</summary>
public sealed record KarteZoomSicht(
    bool SchaechteSichtbar,
    bool LabelsSichtbar,
    bool SchaedenSichtbar,
    bool PfeileSichtbar);

/// <summary>
/// Zentralisiert die Zoom-Schwellen der Karten-Layer (m/px in WebMercator):
/// Schaechte ab 5 (kompatibel zur bestehenden SchachtSichtbarkeitPolicy),
/// Schadenspunkte ab 10 (nur die gewaehlte Haltung), Labels und
/// Fliessrichtungs-Pfeile erst im Detail-Zoom ab 2.5.
/// </summary>
public static class KarteZoomStufenPolicy
{
    public const double SchaechteMaxAufloesung = SchachtSichtbarkeitPolicy.MaxAufloesungMeterProPixel;
    public const double SchaedenMaxAufloesung = 10.0;
    public const double LabelsMaxAufloesung = 2.5;
    public const double PfeileMaxAufloesung = 2.5;

    public static KarteZoomSicht Fuer(double aufloesungMeterProPixel, bool schaechteEingeschaltet)
    {
        if (aufloesungMeterProPixel <= 0)
            return new KarteZoomSicht(false, false, false, false);

        return new KarteZoomSicht(
            SchaechteSichtbar: schaechteEingeschaltet && aufloesungMeterProPixel <= SchaechteMaxAufloesung,
            LabelsSichtbar: aufloesungMeterProPixel <= LabelsMaxAufloesung,
            SchaedenSichtbar: aufloesungMeterProPixel <= SchaedenMaxAufloesung,
            PfeileSichtbar: aufloesungMeterProPixel <= PfeileMaxAufloesung);
    }
}
