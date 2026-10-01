using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Abbild der Sanierungsakten fuer WebGIS-Laeufe (Pruefung 22.09.2026, C3): Senden und Holen warten zwischen
/// den Objekten auf das Netz und laufen danach auf einem anderen Thread weiter, waehrend im nicht-modalen
/// Fenster weiter bearbeitet wird. Wer dann die lebenden Akten liest, kollidiert mit der Oberflaeche. Das Abbild
/// entsteht VOR dem ersten Netzaufruf auf dem Thread des Aufrufers — Listen und Feldwerte sind Kopien.
/// </summary>
internal static class WebGisAktenAbbild
{
    public static IReadOnlyList<ObjektAkte> Sanierungen(IEnumerable<ObjektAkte> akten)
    {
        ArgumentNullException.ThrowIfNull(akten);
        return akten.Where(a => string.Equals(a.Art, WebGisSaniertKriterium.ArtSanierung, StringComparison.Ordinal))
            .Select(Kopie)
            .ToList();
    }

    private static ObjektAkte Kopie(ObjektAkte akte) => new()
    {
        Id = akte.Id,
        Art = akte.Art,
        Bezuege = new List<Guid>(akte.Bezuege),
        Werte = akte.Werte.ToDictionary(kv => kv.Key, kv => Kopie(kv.Value), StringComparer.Ordinal),
        Quellen = new List<ObjektQuellbeleg>(akte.Quellen),
        HauptdeckelId = akte.HauptdeckelId,
    };

    private static ObjektFeldWert Kopie(ObjektFeldWert wert) => new()
    {
        Text = wert.Text,
        KatalogId = wert.KatalogId,
        Originalcode = wert.Originalcode,
        LokalerEintrag = wert.LokalerEintrag,
        Bestandswert = wert.Bestandswert,
        VonHand = wert.VonHand,
        GeaendertUtc = wert.GeaendertUtc,
    };
}
