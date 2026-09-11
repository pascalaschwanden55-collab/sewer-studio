using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Beschraenkt den Lieferplan auf handmarkierte Felder. Pflichtobjekte sind nur Kontext;
/// ausschliesslich Aenderungseintraege erlauben FME ein Update. Export ist keine Quittierung.
/// </summary>
public static class XtfAenderungsPlanBuilder
{
    public static XtfNeuPlan Build(XtfNeuPlan voll, Project projekt)
    {
        var hinweise = voll.Hinweise.Where(h => !h.Contains("Zusatzangaben in derselben XTF", StringComparison.Ordinal)).ToList();
        if (voll.Objekte.GroupBy(o => o.Tid, StringComparer.Ordinal).Any(g => g.Count() > 1))
            return new([], ["Doppelte Objektkennungen: keine eindeutige Aenderungslieferung moeglich."], 0, 0, true);
        var alle = voll.Objekte.ToDictionary(o => o.Tid, StringComparer.Ordinal);
        var auftraege = new Dictionary<(string Tid, string Feld), DateTime>();
        var hs = 0;
        var ss = 0;
        foreach (var s in projekt.SchaechteData)
        {
            var name = XtfSchachtPlanBuilder.Wert(s, "Schachtnummer")?.Trim();
            var klasse = AbwasserbauwerkVokabular.Klasse(XtfSchachtPlanBuilder.Wert(s, FieldKeys.ShaftStructureType), XtfSchachtPlanBuilder.Wert(s, "Funktion"));
            var ziel = Finde(name, klasse);
            if (ziel is null) continue;
            var vorher = auftraege.Count;
            foreach (var (norm, feld) in XtfSchachtPlanBuilder.Felder)
                Markiere(ziel, norm, Meta(s, feld));
            foreach (var (feld, norm) in GemeinsameFelder)
                Markiere(ziel, norm, Meta(s, feld));
            // Eine Abmessung kann beide exportierten Richtungen beeinflussen.
            var massdatum = Neueste(Meta(s, FieldKeys.ShaftDimension1Mm), Meta(s, FieldKeys.ShaftDimension2Mm), Meta(s, "Dimension"));
            Markiere(ziel, "Dimension1", massdatum);
            Markiere(ziel, "Dimension2", massdatum);
            Markiere(ziel, "Art", Neueste(Meta(s, FieldKeys.InfiltrationType), Meta(s, "Funktion")));
            var typdatum = Meta(s, FieldKeys.ShaftStructureType);
            if (typdatum is not null) auftraege[(ziel.Tid, "Bauwerksart")] = typdatum.Value;
            foreach (var feld in XtfZusatzangaben.Felder)
                MarkiereZusatz(ziel, feld, Meta(s, feld));
            if (auftraege.Count > vorher) ss++;
        }
        foreach (var h in projekt.Data)
        {
            var ziel = Finde(h.GetFieldValue(FieldKeys.HoldingName)?.Trim(), "Haltung");
            if (ziel is null) continue;
            var vorher = auftraege.Count;
            var kanalTid = ziel.Verweise.FirstOrDefault(v => v.Name == "AbwasserbauwerkRef")?.ZielTid;
            var kanal = kanalTid is not null ? alle.GetValueOrDefault(kanalTid) : null;
            foreach (var (norm, feld) in XtfStammdatenPlanBuilder.HaltungFelder)
                Markiere(ziel, norm, Handdatum(h.FieldMeta.GetValueOrDefault(feld)));
            if (kanal is not null)
            {
                foreach (var (norm, feld) in XtfStammdatenPlanBuilder.Felder)
                    Markiere(kanal, norm, Handdatum(h.FieldMeta.GetValueOrDefault(feld)));
                foreach (var (feld, norm) in GemeinsameFelder)
                    Markiere(kanal, norm, Handdatum(h.FieldMeta.GetValueOrDefault(feld)));
            }
            foreach (var feld in XtfZusatzangaben.Felder)
                MarkiereZusatz(ziel, feld, Handdatum(h.FieldMeta.GetValueOrDefault(feld)));
            if (auftraege.Count > vorher) hs++;
        }
        var gebraucht = auftraege.Keys.Select(k => k.Tid).ToHashSet(StringComparer.Ordinal);
        var offen = new Queue<string>(gebraucht);
        while (offen.TryDequeue(out var tid))
        {
            foreach (var referenz in alle[tid].Verweise)
                if (alle.ContainsKey(referenz.ZielTid) && gebraucht.Add(referenz.ZielTid)) offen.Enqueue(referenz.ZielTid);
        }
        var objekte = new List<XtfNeuObjekt>();
        foreach (var o in voll.Objekte.Where(o => gebraucht.Contains(o.Tid) && !o.ImTopicZusatz))
        {
            // Organisationen/Profilwerte dienen nur zur Aufloesung der Referenzen.
            var felder = o.Klasse is "Organisation" or "Rohrprofil" ? o.Felder
                : o.Felder.Where(f => f.Key == "Bezeichnung" || auftraege.ContainsKey((o.Tid, f.Key))).ToArray();
            objekte.Add(o with { Felder = felder, Geometrie = null });
        }
        var ids = new XtfNeuKennungen(projekt.Id.ToString("N"));
        foreach (var ((tid, feld), zeit) in auftraege.OrderBy(p => p.Key.Tid, StringComparer.Ordinal).ThenBy(p => p.Key.Feld, StringComparer.Ordinal))
        {
            if (feld.StartsWith("Zusatz:", StringComparison.Ordinal))
                objekte.Add(voll.Objekte.Single(o => o.ImTopicZusatz && o.Klasse == "Zusatzangabe"
                    && o.Felder.Any(f => f.Key == "ObjektTid" && f.Value == tid)
                    && o.Felder.Any(f => f.Key == "Feld" && f.Value == feld[7..])));
            objekte.Add(new("Aenderung", ids.Fuer("Aenderung", tid + "|" + feld),
                [new("ObjektTid", tid), new("Feld", feld), new("GeaendertAm", zeit.ToString("O", CultureInfo.InvariantCulture))], [], ImTopicZusatz: true));
        }
        hinweise.Add($"Aenderungslieferung: {hs} bearbeitete Haltungen, {ss} bearbeitete Bauwerke, {auftraege.Count} Feldauftraege. Andere Objekte sind nur Verknuepfungshilfen und duerfen nicht aktualisiert werden.");
        hinweise.Add($"{objekte.Count(o => o.Klasse == "Zusatzangabe")} Zusatzwerte und {auftraege.Count} Aenderungseintraege in derselben XTF; das Modell {XtfZusatzangaben.Modell}.ili gehoert zur Lieferung.");
        hinweise.Add("Auswahl anhand der Handmarkierung je Feld, ohne bestaetigten GEONIS-Vergleichsstand. Bereits frueher exportierte Handaenderungen bleiben bis zur geklaerten Uebernahme enthalten; der Export setzt keine Markierung zurueck.");
        return new(objekte, hinweise, hs, ss, true);

        XtfNeuObjekt? Finde(string? name, string? klasse)
        {
            var treffer = voll.Objekte.Where(o => o.Klasse == klasse && o.Felder.Any(f => f.Key == "Bezeichnung" && f.Value == name)).ToArray();
            return treffer.Length == 1 ? treffer[0] : null;
        }
        void Markiere(XtfNeuObjekt ziel, string feld, DateTime? zeit)
        {
            if (zeit is not null && (ziel.Felder.Any(f => f.Key == feld) || ziel.Verweise.Any(v => v.Name == feld)))
                auftraege[(ziel.Tid, feld)] = zeit.Value;
        }
        void MarkiereZusatz(XtfNeuObjekt ziel, string feld, DateTime? zeit)
        {
            if (zeit is not null && voll.Objekte.Any(o => o.ImTopicZusatz && o.Klasse == "Zusatzangabe"
                && o.Felder.Any(f => f.Key == "ObjektTid" && f.Value == ziel.Tid)
                && o.Felder.Any(f => f.Key == "Feld" && f.Value == feld)))
                auftraege[(ziel.Tid, "Zusatz:" + feld)] = zeit.Value;
        }
    }

    private static readonly (string Feld, string Norm)[] GemeinsameFelder =
    [
        (FieldKeys.Street, "Standortname"), (FieldKeys.GrossCost, "Bruttokosten"),
        (FieldKeys.InspectionYear, "Zustandserhebung_Jahr"), (FieldKeys.Owner, "EigentuemerRef"),
        (FieldKeys.DataOwner, "DatenherrRef"), (FieldKeys.DataSupplier, "DatenlieferantRef")
    ];

    private static DateTime? Meta(SchachtRecord s, string feld)
    {
        var name = XtfZusatzangaben.Schachtfeld(s, feld);
        return Neueste(SchachtFeldnamen.Schreibweisen(s, name)
            .Select(n => Handdatum(s.FieldMeta.GetValueOrDefault(n))).ToArray());
    }
    private static DateTime? Handdatum(FieldMetadata? meta)
        => meta is { UserEdited: true } && meta.LastUpdatedUtc != default
            ? DateTime.SpecifyKind(meta.LastUpdatedUtc, DateTimeKind.Utc) : null;
    private static DateTime? Neueste(params DateTime?[] daten) => daten.Where(d => d.HasValue).Max();
}
