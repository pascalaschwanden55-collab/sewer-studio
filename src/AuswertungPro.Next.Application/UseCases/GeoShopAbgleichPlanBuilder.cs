using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

public sealed record GeoShopFeldAenderung(string Feld, string Vorher, string Nachher, bool IstKennung = false);
public sealed record GeoShopPosition(GeoShopZiel Ziel, string Vorher, GeoShopBauteil Quelle,
    bool Gedreht, bool KennungenAendern, IReadOnlyList<GeoShopFeldAenderung> Felder,
    string AlteKennungen, string NeueKennungen, string Aktenstand = "", bool NeueAktenwerte = false);
public sealed record GeoShopPlan(string Quelle, IReadOnlyList<GeoShopPosition> Positionen, IReadOnlyList<string> Hinweise,
    IReadOnlyList<object> Projektbestand);

/// <summary>Plant Kennungsersatz und Leerfelder gemeinsam. Namen allein reichen nur bei einem eindeutigen Treffer.</summary>
public static class GeoShopAbgleichPlanBuilder
{
    public static GeoShopPlan Baue(IReadOnlyList<GeoShopZiel> ziele, GeoShopBestand bestand)
    {
        var positionen = new List<GeoShopPosition>();
        var hinweise = new List<string>();
        var doppelt = ziele.GroupBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var jeName = bestand.Bauteile.ToLookup(b => b.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var ziel in ziele)
        {
            void Hinweis(string text) => hinweise.Add($"{ziel.Name}: {text}");
            if (ziel.Art != bestand.Art) throw new ArgumentException("Bauteilarten passen nicht zusammen.");
            if (ziel.Name.Length == 0 || doppelt.Contains(ziel.Name))
            { Hinweis("Name fehlt oder kommt im Projekt mehrfach vor – ausgelassen."); continue; }
            var teile = ziel.Name.Split('-');
            var gegenname = ziel.Art == BauteilArt.Haltung && teile.Length == 2
                ? $"{teile[1].Trim()}-{teile[0].Trim()}" : ziel.Name;
            var treffer = jeName[ziel.Name].Concat(jeName[gegenname]).Distinct().ToArray();
            if (treffer.Length != 1)
            { Hinweis(treffer.Length == 0 ? "Nicht in der XTF gefunden." : "Mehrdeutige Zuordnung – ausgelassen."); continue; }
            var quelle = treffer[0];
            if (quelle.Fehler is not null) { Hinweis(quelle.Fehler); continue; }
            if (quelle.Hinweis is not null) Hinweis(quelle.Hinweis);
            if (GeonisVerbund.Bestimme(quelle.Kennungen, bestand.Art) != GeonisVerbundStand.AbgleichMoeglich
                || !GueltigeKennungen(quelle.Kennungen, bestand.Art))
            { Hinweis("Unvollständige Kennungen – ausgelassen."); continue; }
            var gedreht = !string.Equals(quelle.Name, ziel.Name, StringComparison.OrdinalIgnoreCase);
            var werte = new Dictionary<string, string>(quelle.Felder, StringComparer.Ordinal);
            if (gedreht)
            {
                werte.Remove("Schacht_oben"); werte.Remove("Schacht_unten");
                if (!string.IsNullOrWhiteSpace(quelle.NachSchacht)) werte["Schacht_oben"] = quelle.NachSchacht;
                if (!string.IsNullOrWhiteSpace(quelle.VonSchacht)) werte["Schacht_unten"] = quelle.VonSchacht;
            }
            bool Widerspruch(string feld) => werte.TryGetValue(feld, out var wert)
                && !string.IsNullOrWhiteSpace(ziel.Wert(feld))
                && !string.Equals(ziel.Wert(feld).Trim(), wert.Trim(), StringComparison.OrdinalIgnoreCase);
            if (Widerspruch("Schacht_oben") || Widerspruch("Schacht_unten")
                || Widerspruch(FieldKeys.ShaftStructureType))
            { Hinweis("Schachtzuordnung oder Bauwerksart widerspricht der XTF – ausgelassen."); continue; }
            // Eine bereits korrigierte Hoehe darf nicht mit einer daraus abweichenden
            // Katasterbreite zu einem neuen, unbeabsichtigten Profil kombiniert werden.
            if ((Widerspruch(FieldKeys.NominalDiameterMm) || Widerspruch(FieldKeys.ProfileType))
                && string.IsNullOrWhiteSpace(ziel.Wert(FieldKeys.ClearWidthMm))
                && werte.Remove(FieldKeys.ClearWidthMm))
                Hinweis("Lichte Breite bleibt leer: vorhandene Höhe oder Profiltyp weicht von der XTF ab.");

            var neu = Kennungen(quelle.Kennungen, gedreht);
            var alt = ziel.Kennungen();
            var altText = Beschreibe(alt);
            var neuText = Beschreibe(neu);
            var aendern = altText != neuText || alt?.Quelle != $"GeoShop-XTF: {bestand.Quelle}";
            var felder = werte.Where(p => !string.IsNullOrWhiteSpace(p.Value)
                && string.IsNullOrWhiteSpace(ziel.Wert(p.Key)))
                .Select(p => new GeoShopFeldAenderung(p.Key, ziel.Wert(p.Key), p.Value)).ToList();
            var id = quelle.Kennungen.Hauptkennung!;
            var kennungsfelder = new[] { FieldKeys.GeonisId, FieldKeys.CadastreObjectId };
            if (kennungsfelder.Any(f => ziel.Handgesetzt(f) && !string.IsNullOrWhiteSpace(ziel.Wert(f)) && ziel.Wert(f) != id))
            { Hinweis("Eine abweichende Kennung ist von Hand geschützt – ausgelassen."); continue; }
            foreach (var feld in kennungsfelder)
                if (ziel.Wert(feld) != id) felder.Add(new GeoShopFeldAenderung(feld, ziel.Wert(feld), id, true));
            var neueAktenwerte = ziel.HatNeueAktenwerte(quelle);
            if (aendern || felder.Count > 0 || neueAktenwerte)
                positionen.Add(new GeoShopPosition(ziel, ziel.Stand(), quelle, gedreht, aendern, felder, altText, neuText, ziel.Aktenstand, neueAktenwerte));
            else Hinweis("Bereits abgeglichen; keine leeren Felder zu ergänzen.");
        }
        return new GeoShopPlan(bestand.Quelle, positionen, hinweise, ziele.Select(z => z.Datensatz).ToArray());
    }

    internal static GeonisKennungen Kennungen(KatasterKennung k, bool gedreht) => new()
    {
        Haltung = k.Haltung, Kanal = k.Kanal, Knoten = k.Knoten, Bauwerk = k.Bauwerk,
        VonPunkt = gedreht ? k.NachPunkt : k.VonPunkt, NachPunkt = gedreht ? k.VonPunkt : k.NachPunkt,
        VonPunktBezeichnung = gedreht ? k.NachPunktBezeichnung : k.VonPunktBezeichnung,
        NachPunktBezeichnung = gedreht ? k.VonPunktBezeichnung : k.NachPunktBezeichnung,
        Rohrprofil = k.Rohrprofil, RohrprofilTyp = k.RohrprofilTyp, RichtungGedreht = gedreht
    };

    private static bool GueltigeKennungen(KatasterKennung k, BauteilArt art)
        => (art == BauteilArt.Haltung
            ? new[] { k.Haltung, k.Kanal, k.VonPunkt, k.NachPunkt, k.Rohrprofil }
            : new[] { k.Knoten, k.Bauwerk }).All(SiaObjektkennung.IstGueltig);

    private static string Beschreibe(GeonisKennungen? k) => k is null ? "Keine Kennungen" :
        string.Join(Environment.NewLine, new[] {
            ("Haltung", k.Haltung), ("Kanal", k.Kanal), ("Haltungspunkt oben", k.VonPunkt),
            ("Punktbezeichnung oben", k.VonPunktBezeichnung), ("Haltungspunkt unten", k.NachPunkt),
            ("Punktbezeichnung unten", k.NachPunktBezeichnung), ("Rohrprofil", k.Rohrprofil),
            ("Profiltyp", k.RohrprofilTyp), ("Knoten", k.Knoten), ("Bauwerk", k.Bauwerk),
            ("Gegenrichtung", k.RichtungGedreht ? "Ja" : "Nein") }
            .Where(p => !string.IsNullOrWhiteSpace(p.Item2)).Select(p => $"  {p.Item1}: {p.Item2}"));
}

public static class GeoShopAbgleichAnwender
{
    /// <summary>Vor jeglichem Schreiben den ganzen Plan auf unveraenderten Projektstand pruefen.</summary>
    public static int WendeAn(GeoShopPlan plan, IReadOnlyList<GeoShopZiel> aktuelleZiele)
    {
        if (plan.Projektbestand.Count != aktuelleZiele.Count
            || !plan.Projektbestand.Zip(aktuelleZiele).All(p => ReferenceEquals(p.First, p.Second.Datensatz))
            || plan.Positionen.Any(p => !aktuelleZiele.Any(z => ReferenceEquals(z.Datensatz, p.Ziel.Datensatz))
            || p.Ziel.Stand() != p.Vorher || p.Ziel.Aktenstand != p.Aktenstand))
            throw new InvalidOperationException("Das Projekt wurde während der Vorschau geändert. Bitte den GeoShop-Abgleich erneut starten.");
        foreach (var position in plan.Positionen) position.Ziel.Uebernehme(position, plan.Quelle);
        return plan.Positionen.Count;
    }
}
