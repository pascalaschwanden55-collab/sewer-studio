using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Xtf.Dss;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Vorhandene Anschlusspunkte als eigene Akten; gemeinsame Original-TID bleibt gemeinsam.</summary>
internal static class GeoShopHaltungspunktImport
{
    internal static bool Fehlen(Project projekt, Guid bezug, IEnumerable<ObjektQuellbeleg> quellen)
        => quellen.Any(q => q.Klasse == "Haltungspunkt" && !projekt.Objektakten.Any(a => a.Art == "haltungspunkt"
            && a.Bezuege.Contains(bezug) && a.Quellen.Any(s => s.Modell == q.Modell && s.Klasse == q.Klasse && s.Kennung == q.Kennung)));

    internal static void Uebernehme(Project projekt, Guid bezug, IEnumerable<ObjektQuellbeleg> quellen)
    {
        foreach (var q in quellen.Where(q => q.Klasse == "Haltungspunkt"))
        {
            var akte = projekt.Objektakten.SingleOrDefault(a => a.Art == "haltungspunkt"
                && a.Quellen.Any(s => s.Modell == q.Modell && s.Klasse == q.Klasse && s.Kennung == q.Kennung));
            if (akte is null)
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(q.Modell + ":" + q.Klasse + ":" + q.Kennung));
                akte = new() { Id = new Guid(hash.AsSpan(0, 16)), Art = "haltungspunkt", Quellen = [new()
                {
                    System = q.System, Datei = q.Datei, Modell = q.Modell, Klasse = q.Klasse, Kennung = q.Kennung,
                    IstLokaleKennung = q.IstLokaleKennung, ImportiertUtc = q.ImportiertUtc,
                    Werte = new(q.Werte), Referenzen = new(q.Referenzen), Strukturen = new(q.Strukturen)
                }] };
                projekt.Objektakten.Add(akte);
            }
            if (!akte.Bezuege.Contains(bezug)) akte.Bezuege.Add(bezug);
            foreach (var f in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == "haltungspunkt"))
            {
                if (f.Exportziel is not { } ziel || !ziel.StartsWith("Haltungspunkt.", StringComparison.Ordinal)) continue;
                if (akte.Werte.TryGetValue(f.Id, out var alt) && (alt.VonHand || alt.Text.Length > 0)) continue;
                if (q.Werte.TryGetValue(ziel["Haltungspunkt.".Length..], out var text))
                    akte.Werte[f.Id] = new() { Text = DssFeldZuordnung.KatalogAnzeige(f, text) ?? text, GeaendertUtc = DateTime.UtcNow };
            }
        }
    }
}
