using System.Globalization;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Der Parzellendienst sucht raeumlich mit Linien — er wurde fuer Haltungen
/// gebaut. Ein Schacht ist dagegen ein Punkt.
///
/// Statt den bewaehrten WFS-Weg zu aendern, wird aus dem Punkt eine sehr kurze
/// Linie gebaut. Der Dossier-Weg bleibt dadurch vollstaendig unberuehrt.
/// </summary>
public static class PunktAlsKurzeLinie
{
    /// <summary>
    /// Baut eine waagrechte Linie von <paramref name="halbeLaenge"/> Metern in
    /// beide Richtungen um den Punkt. Ein Meter genuegt: Er trifft die Parzelle
    /// unter dem Schacht und bleibt kurz genug, um nicht ohne Not in die
    /// Nachbarparzelle zu ragen.
    ///
    /// Das Praefix "LINESTRING" ist Pflicht, nicht Schmuck: Der Parzellendienst
    /// verwirft jeden Eintrag ohne dieses Praefix stillschweigend — ohne
    /// Fehlermeldung, mit leerem Ergebnis. Ein Test haelt diesen Vertrag fest.
    /// </summary>
    public static string Baue(double ost, double nord, double halbeLaenge = 0.5)
    {
        var links = (ost - halbeLaenge).ToString("0.###", CultureInfo.InvariantCulture);
        var rechts = (ost + halbeLaenge).ToString("0.###", CultureInfo.InvariantCulture);
        var hoehe = nord.ToString("0.###", CultureInfo.InvariantCulture);

        return $"LINESTRING({links} {hoehe}, {rechts} {hoehe})";
    }
}
