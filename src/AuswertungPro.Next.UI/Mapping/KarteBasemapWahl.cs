using System;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Reine Umschalt-Logik fuer die Hintergrundkarte: bestimmt die naechste verfuegbare Auswahl
/// (reihum) und ob eine Auswahl ueberhaupt verfuegbar ist. Fehlende Offline-Ordner werden
/// uebersprungen; OSM (online) ist immer verfuegbar. Bewusst pure + testbar (keine God-Class).
/// </summary>
public static class KarteBasemapWahl
{
    private static readonly KarteBasemapAuswahl[] Reihenfolge =
    {
        KarteBasemapAuswahl.Satellit,
        KarteBasemapAuswahl.AvKarte,
        KarteBasemapAuswahl.OpenStreetMap,
    };

    /// <summary>Naechste verfuegbare Hintergrundkarte reihum; fehlende Offline-Karten uebersprungen.</summary>
    public static KarteBasemapAuswahl Naechste(KarteBasemapAuswahl aktuell, bool hatSatellit, bool hatAv)
    {
        var start = Array.IndexOf(Reihenfolge, aktuell);
        for (var i = 1; i <= Reihenfolge.Length; i++)
        {
            var kandidat = Reihenfolge[((start + i) % Reihenfolge.Length + Reihenfolge.Length) % Reihenfolge.Length];
            if (IstVerfuegbar(kandidat, hatSatellit, hatAv))
                return kandidat;
        }
        return KarteBasemapAuswahl.OpenStreetMap; // OSM ist immer verfuegbar (Fallback)
    }

    public static bool IstVerfuegbar(KarteBasemapAuswahl wahl, bool hatSatellit, bool hatAv) => wahl switch
    {
        KarteBasemapAuswahl.Satellit => hatSatellit,
        KarteBasemapAuswahl.AvKarte => hatAv,
        _ => true, // OpenStreetMap (online)
    };
}
