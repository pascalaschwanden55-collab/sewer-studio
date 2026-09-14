using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

public static class GeoShopEigentuemerErgaenzung
{
    public static GeoShopBestand Ergaenze(GeoShopBestand bestand, IReadOnlyDictionary<string, string> namen, string datei)
        => bestand with { Bauteile = bestand.Bauteile.Select(b =>
        {
            var bw = b.Quellen?.SingleOrDefault(q => q.Kennung == (bestand.Art == BauteilArt.Haltung ? b.Kennungen.Kanal : b.Kennungen.Bauwerk));
            var referenz = bw?.Referenzen.GetValueOrDefault("EigentuemerRef");
            if (referenz is null || !namen.TryGetValue(referenz, out var name) || string.IsNullOrWhiteSpace(name)) return b;
            // Diese Datei belegt nur Eigentümer. Keine Betreiber- oder Firmenrolle daraus ableiten.
            var felder = new Dictionary<string, string>(b.Felder);
            if (felder.TryGetValue(FieldKeys.Owner, out var alt) && alt.Length > 0 && alt != referenz && alt != name)
                return b with { Hinweis = (b.Hinweis + " Eigentümerdatei widerspricht dem XTF-Namen; XTF bleibt bestehen.").Trim() };
            felder[FieldKeys.Owner] = name;
            var beleg = new ObjektQuellbeleg { System = "Eigentümer-Zuordnung", Datei = datei,
                Klasse = "Organisation", Kennung = referenz, Werte = new() { ["Bezeichnung"] = name } };
            var quellen = b.Quellen ?? [];
            return b with { Felder = felder, Quellen = quellen.Any(q => q.System == beleg.System
                    && q.Kennung == referenz && q.Werte.GetValueOrDefault("Bezeichnung") == name)
                    ? quellen : quellen.Append(beleg).ToArray(),
                Hinweis = $"Eigentümername aus der Zuordnungsdatei: {datei}" };
        }).ToArray() };
}
