using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Xtf.Dss;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Gemeinsame Einbauten einmalig anhand ihrer Originalkennung in die vorhandenen Masken übernehmen.</summary>
internal static class GeoShopEinbautenImport
{
    internal static bool Fehlen(Project projekt, Guid bezug, IEnumerable<ObjektQuellbeleg> quellen)
        => quellen.Any(q => DssEinbautenZuordnung.Art(q.Klasse) is { } art && !projekt.Objektakten.Any(a => a.Art == art
            && a.Bezuege.Contains(bezug) && a.Quellen.Any(s => s.Modell == q.Modell && s.Klasse == q.Klasse && s.Kennung == q.Kennung)));

    internal static void Uebernehme(Project projekt, Guid bezug, IEnumerable<ObjektQuellbeleg> quellen)
    {
        var bearbeitung = new ObjektaktenBearbeitung(projekt, bezug, projekt.Data.Any(h => h.Id == bezug) ? "haltung" : "schacht");
        foreach (var q in quellen)
        {
            if (DssEinbautenZuordnung.Art(q.Klasse) is not { } art) continue;
            var akte = projekt.Objektakten.SingleOrDefault(a => a.Art == art
                && a.Quellen.Any(s => s.Modell == q.Modell && s.Klasse == q.Klasse && s.Kennung == q.Kennung));
            if (akte is null)
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(q.Modell + ":" + q.Klasse + ":" + q.Kennung));
                akte = new() { Id = new Guid(hash.AsSpan(0, 16)), Art = art };
                projekt.Objektakten.Add(akte);
            }
            if (!akte.Bezuege.Contains(bezug)) akte.Bezuege.Add(bezug);
            if (!akte.Quellen.Any(s => GeoShopObjektaktenImport.Gleich(s, q))) akte.Quellen.Add(GeoShopObjektaktenImport.Kopie(q));
            foreach (var f in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == art))
            {
                if (akte.Werte.TryGetValue(f.Id, out var alt) && (alt.VonHand || alt.Text.Length > 0)) continue;
                var wert = f.Id is "bauwerksteil.art" or "ueberlauf.bauwerksart" ? DssEinbautenZuordnung.Klassenanzeige(q.Klasse) : null;
                if (DssEinbautenZuordnung.Ziel(f, q.Klasse) is { } ziel)
                    wert = DssFeldZuordnung.KatalogAnzeige(f, q.Werte.GetValueOrDefault(ziel.Attribut)
                        ?? q.Referenzen.GetValueOrDefault(ziel.Attribut), q.Klasse, bearbeitung.ErlaubteEintraege(akte, f));
                if (wert is not null) akte.Werte[f.Id] = new() { Text = wert, GeaendertUtc = DateTime.UtcNow };
            }
        }
    }
}
