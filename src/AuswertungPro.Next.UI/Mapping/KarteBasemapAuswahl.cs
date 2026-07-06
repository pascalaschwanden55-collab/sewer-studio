namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Auswahl der Hintergrundkarte der App-Karte. Satellit (SWISSIMAGE, offline) und AV-Karte
/// farbig (Amtliche Vermessung/Grundbuch, offline) liegen lokal im Programmordner; OSM kommt
/// online. Umschaltbar per Knopf (reihum).
/// </summary>
public enum KarteBasemapAuswahl
{
    Satellit,
    AvKarte,
    OpenStreetMap,
}
