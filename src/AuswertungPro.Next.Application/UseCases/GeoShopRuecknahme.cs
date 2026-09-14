using System.Text.Json;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

/// <summary>Alle vom Abgleich beruehrbaren Werte vor der ersten Mutation sichern; Ruecknahme ohne Ereignisaufrufe.</summary>
internal sealed class GeoShopRuecknahme
{
    private readonly List<Action> _rueckwege = [];
    public GeoShopRuecknahme(GeoShopPlan plan)
    {
        foreach (var datensatz in plan.Positionen.Select(p => p.Ziel.Datensatz).Distinct())
        {
            if (datensatz is SchachtRecord s)
            {
                var alt = Kopie(s);
                _rueckwege.Add(() => { s.Fields = alt.Fields; s.FieldMeta = alt.FieldMeta; s.Geonis = alt.Geonis; s.ModifiedAtUtc = alt.ModifiedAtUtc; });
            }
            else if (datensatz is HaltungRecord h)
            {
                var alt = Kopie(h);
                _rueckwege.Add(() => { h.Fields = alt.Fields; h.FieldMeta = alt.FieldMeta; h.Geonis = alt.Geonis; h.ModifiedAtUtc = alt.ModifiedAtUtc; });
            }
        }
        foreach (var p in plan.Positionen.Select(z => z.Ziel.Projekt).OfType<Project>().Distinct())
        {
            var akten = p.Objektakten.ToArray();
            var kopien = akten.Select(Kopie).ToArray();
            var meta = new Dictionary<string, string>(p.Metadata); var dirty = p.Dirty;
            var version = p.Version; var zeit = p.ModifiedAtUtc;
            _rueckwege.Add(() =>
            {
                for (var i = 0; i < akten.Length; i++)
                {
                    var a = akten[i]; var alt = kopien[i];
                    a.Werte = alt.Werte; a.Quellen = alt.Quellen; a.Bezuege = alt.Bezuege;
                    a.HauptdeckelId = alt.HauptdeckelId; a.Unterlisten = alt.Unterlisten; a.Zusatzdaten = alt.Zusatzdaten;
                }
                p.Objektakten.Clear(); p.Objektakten.AddRange(akten);
                p.Metadata = meta; p.Dirty = dirty; p.Version = version; p.ModifiedAtUtc = zeit;
            });
        }
    }
    public void StelleWiederHer() { foreach (var weg in _rueckwege) weg(); }
    private static T Kopie<T>(T wert) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(wert))!;
}
