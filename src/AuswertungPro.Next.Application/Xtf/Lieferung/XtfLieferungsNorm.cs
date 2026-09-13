using System.Globalization;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Xtf.Dss;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Lieferung;

/// <summary>Gemeinsamer DSS-Vertrag für die freie Lieferung; keine Ersatzeigentümer oder Namenszuordnungen.</summary>
public static class XtfLieferungsNorm
{
    public static IReadOnlyList<string> Rollen(string klasse) => DssExportPruefung.ErlaubteRefs(klasse).ToArray();
    public static bool Pflichtrolle(string klasse, string rolle) => DssExportPruefung.PflichtRefs(klasse).Contains(rolle);
    public static bool ExterneOrganisation(string rolle) => DssExportPruefung.Ziele.GetValueOrDefault(rolle)?.SequenceEqual(["Organisation"]) == true;
    public static bool PassendesZiel(string rolle, string klasse) => DssExportPruefung.Ziele.GetValueOrDefault(rolle)?.Contains(klasse) == true;
    public static bool OhneTid(string klasse) => DssExportPruefung.Associationen.ContainsKey(klasse);

    public static string? Problem(ObjektQuellbeleg q, Func<string, string?> zielklasse)
    {
        try
        {
            if (q.Modell != (q.Klasse == "Organisation" ? DssExportSchema.Basis : DssExportSchema.Modell))
                throw new InvalidOperationException($"Modell {q.Modell} ist noch nicht im freien DSS-Prüfvertrag enthalten.");
            if (!OhneTid(q.Klasse) && !SiaObjektkennung.IstGueltig(q.Kennung)) throw new InvalidOperationException("Originalkennung fehlt oder ist ungültig.");
            if (OhneTid(q.Klasse) && q.Kennung.Length > 0) throw new InvalidOperationException("Diese Beziehung darf keine eigene TID tragen.");
            var felder = DssExportSchema.Felder(q.Klasse);
            if (felder is null && !OhneTid(q.Klasse)) throw new InvalidOperationException($"Klasse {q.Klasse} ist noch nicht geprüft.");
            foreach (var (name, wert) in q.Werte) DssExportSchema.Normalisiere(q.Klasse, name, wert);
            foreach (var (name, xml) in q.Strukturen)
            {
                if (felder?.GetValueOrDefault(name)?.Kind != "Structure") throw new InvalidOperationException($"Nicht zugeordnete Struktur {name}.");
                DssExportPruefung.PruefeStruktur(DssExportObjekt.Aus(q), name, xml);
            }
            foreach (var f in felder?.Where(f => f.Value.Required) ?? [])
                if (string.IsNullOrEmpty(q.Werte.GetValueOrDefault(f.Key)) && !q.Strukturen.ContainsKey(f.Key))
                    throw new InvalidOperationException($"Pflichtfeld {f.Key} fehlt.");
            foreach (var rolle in DssExportPruefung.PflichtRefs(q.Klasse))
                if (string.IsNullOrEmpty(q.Referenzen.GetValueOrDefault(rolle))) throw new InvalidOperationException($"Pflichtverweis {rolle} fehlt.");
            foreach (var (rolle, tid) in q.Referenzen)
            {
                if (!Rollen(q.Klasse).Contains(rolle)) throw new InvalidOperationException($"Beziehung {rolle} ist noch nicht geprüft.");
                if (!SiaObjektkennung.IstGueltig(tid)) throw new InvalidOperationException($"Ungültige Kennung in {rolle}.");
                var klasse = zielklasse(tid);
                if (klasse is null && ExterneOrganisation(rolle)) continue;
                if (klasse is null) throw new InvalidOperationException($"Bezugsobjekt {rolle} ({tid}) fehlt.");
                if (!PassendesZiel(rolle, klasse)) throw new InvalidOperationException($"{rolle} zeigt auf {klasse} statt auf die passende Objektart.");
            }
            return null;
        }
        catch (InvalidOperationException e) { return e.Message; }
        catch (System.Xml.XmlException e) { return "Ungültige Geometrie: " + e.Message; }
    }

    public static string? Namensschluessel(ObjektQuellbeleg q)
    {
        if (OhneTid(q.Klasse) || q.Klasse is "Organisation" or "Haltung_Text" or "Abwasserbauwerk_Text") return null;
        var name = q.Werte.GetValueOrDefault("Bezeichnung");
        if (string.IsNullOrEmpty(name)) return null;
        var gruppe = DssExportPruefung.Bauwerke.Contains(q.Klasse) ? "Abwasserbauwerk"
            : q.Klasse is "Haltung" or "Abwasserknoten" ? "Abwassernetzelement"
            : q.Klasse is "Deckel" or "Einstiegshilfe" or "Trockenwetterfallrohr" ? "BauwerksTeil"
            : q.Klasse is "FoerderAggregat" or "Leapingwehr" or "Streichwehr" ? "Ueberlauf" : q.Klasse;
        var bezug = q.Klasse == "Unterhalt" ? q.Werte.GetValueOrDefault("Zeitpunkt") : q.Referenzen.GetValueOrDefault("DatenherrRef");
        if (q.Klasse == "Unterhalt" && bezug is not null)
        {
            try { bezug = DssExportSchema.Normalisiere(q.Klasse, "Zeitpunkt", bezug); }
            catch (InvalidOperationException) { /* Der Feldprüfer meldet das ungültige Datum. */ }
        }
        return System.Text.Json.JsonSerializer.Serialize(new[] { gruppe, name, bezug });
    }

    public static string Koordinate(string achse, string text)
    {
        if (!decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
            || decimal.Round(n, 3) != n || (achse == "C1" ? n < 2480000 || n > 2840000 : n < 1070000 || n > 1300000))
            throw new InvalidOperationException("Koordinate ausserhalb LV95 oder mehr als drei Nachkommastellen.");
        return n.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
