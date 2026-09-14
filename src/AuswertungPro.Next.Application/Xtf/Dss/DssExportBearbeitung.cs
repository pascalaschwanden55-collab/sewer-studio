using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Aktuelle Felder auf dem kopierten Objektverbund, bewusstes Leeren eingeschlossen.</summary>
internal sealed class DssExportBearbeitung(Project projekt, Dictionary<string, DssExportObjekt> objekte,
    Dictionary<Guid, DssExportObjekt> roots, Dictionary<string, ObjektQuellbeleg> quellen, List<string> hinweise,
    bool mitZusatzangaben = false)
{
    private readonly XtfNeuKennungen _ids = new(projekt.Id.ToString("N"));
    private readonly Dictionary<(string Tid, string Feld), (string Wert, bool Hand)> _vorgaben = new();
    public void Uebernehme()
    {
        foreach (var (id, root) in roots)
        {
            var art = root.Klasse == "Haltung" ? "haltung" : "schacht";
            var akte = projekt.Objektakten.SingleOrDefault(a => a.Id == id) ?? new ObjektAkte { Id = id, Art = art };
            BestandsErgaenzungen(id, root);
            Felder(akte, root, id);
        }
        // Haltungspunkte existieren bereits im importierten Netz. Weder einen neuen Punkt
        // erfinden noch Verbindungen ändern, nur dessen aktuelle eigene Felder übernehmen.
        foreach (var akte in projekt.Objektakten.Where(a => a.Art == "haltungspunkt"))
        {
            var tids = akte.Quellen.Where(q => q.Klasse == "Haltungspunkt" && !q.IstLokaleKennung)
                .Select(q => q.Kennung).Distinct().ToArray();
            if (tids.Length != 1 || !objekte.TryGetValue(tids[0], out var punkt) || punkt.Klasse != "Haltungspunkt")
                throw new InvalidOperationException($"DSS: {akte}: zugehöriger Original-Haltungspunkt fehlt oder ist mehrdeutig.");
            Felder(akte, punkt, null);
        }
        foreach (var akte in projekt.Objektakten.Where(a => DssEinbautenZuordnung.IstAkte(a.Art)))
        {
            var tids = akte.Quellen.Where(q => DssEinbautenZuordnung.Art(q.Klasse) == akte.Art && !q.IstLokaleKennung)
                .Select(q => q.Kennung).Distinct().ToArray();
            if (tids.Length != 1 || !objekte.TryGetValue(tids[0], out var einbau))
                throw new InvalidOperationException($"DSS: {akte}: zugehöriger Original-Einbau fehlt oder ist mehrdeutig. Keine neue Kennung oder Zuordnung erfunden.");
            DssEinbautenZuordnung.PruefeKlasse(akte, einbau.Klasse);
            Felder(akte, einbau, null);
        }
        foreach (var akte in projekt.Objektakten.Where(a => a.Art is "deckel" or "sanierung" or "unterhalt"))
        {
            var eltern = akte.Bezuege.Where(roots.ContainsKey).Select(id => roots[id]).ToArray();
            if (eltern.Length == 0) continue;
            var klasse = akte.Art == "deckel" ? "Deckel" : "Unterhalt";
            var belege = akte.Quellen.Where(q => q.Klasse == klasse && !q.IstLokaleKennung).Select(q => q.Kennung).Distinct().ToArray();
            if (belege.Length > 1) throw new InvalidOperationException($"DSS: {akte} hat mehrere Originalkennungen.");
            var tid = belege.SingleOrDefault() ?? _ids.Fuer(klasse, akte.Id.ToString("N"));
            if (!objekte.TryGetValue(tid, out var o))
            {
                o = quellen.TryGetValue(tid, out var q) ? DssExportObjekt.Aus(q) : new DssExportObjekt(klasse, tid);
                if (belege.Length == 0)
                {
                    o.Setze("Bezeichnung", klasse + "_" + akte.Id.ToString("N")[..8]);
                    foreach (var rolle in new[] { "DatenherrRef", "DatenlieferantRef" })
                        if (eltern[0].Refs.TryGetValue(rolle, out var ziel)) o.Refs[rolle] = ziel;
                }
                objekte.Add(tid, o);
            }
            if (klasse == "Deckel")
            {
                if (eltern.Length != 1) throw new InvalidOperationException($"DSS: Deckel {akte} muss genau einem Bauwerk zugeordnet sein.");
                Verweise(o, "AbwasserbauwerkRef", eltern[0].Refs["AbwasserbauwerkRef"]);
            }
            else foreach (var parent in eltern) Assoziation("Erhaltungsereignis_AbwasserbauwerkAssoc", "AbwasserbauwerkRef", parent.Refs["AbwasserbauwerkRef"], "Erhaltungsereignis_AbwasserbauwerkAssocRef", o.Tid);
            Felder(akte, o, null);
            if (akte.Werte.TryGetValue("sanierung.firma", out var firma) && firma.VonHand)
            {
                foreach (var key in objekte.Where(p => p.Value.Klasse == "Erhaltungsereignis_Ausfuehrende_FirmaAssoc"
                    && p.Value.Refs.GetValueOrDefault("Erhaltungsereignis_Ausfuehrende_FirmaAssocRef") == o.Tid).Select(p => p.Key).ToArray()) objekte.Remove(key);
                Verweise(o, "Ausfuehrende_FirmaRef", string.IsNullOrWhiteSpace(firma.Text) ? "" : Organisation(firma.Text));
            }
        }
    }
    private void Felder(ObjektAkte akte, DssExportObjekt root, Guid? id)
    {
        var bekannteFelder = FieldCatalog.Objektfelder.Felder.Where(f => f.Art == akte.Art).Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, wert) in akte.Werte.Where(w => !bekannteFelder.Contains(w.Key) && (w.Value.Text.Length > 0 || w.Value.VonHand)))
            hinweise.Add($"{DssObjektarten.Bezeichnung(akte)}: {key} = „{wert.Text}“ fehlt in der XTF; Feldzuordnung ist unbekannt. Bleibt im Projekt erhalten.");
        foreach (var f in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == akte.Art && !f.NurLesen))
        {
            akte.Werte.TryGetValue(f.Id, out var eingabe);
            string? text = null;
            if (f.Speicherfeld is { } key && id is { } recordId)
            {
                var h = projekt.Data.SingleOrDefault(r => r.Id == recordId);
                var s = projekt.SchaechteData.SingleOrDefault(r => r.Id == recordId);
                var feld = s is null ? key : SchachtFeldnamen.Feld(s, key);
                var meta = h?.FieldMeta.GetValueOrDefault(feld) ?? s?.FieldMeta.GetValueOrDefault(feld);
                var wert = h?.GetFieldValue(feld) ?? s?.GetFieldValue(feld) ?? "";
                if (meta is { UserEdited: true } || GeoShopImportVergleich.BehaeltBestandswert(projekt, recordId, key, wert)
                    || wert.Length > 0 && (meta is null || meta.Source != FieldSource.Kataster))
                    text = eingabe is not null && eingabe.Bestandswert == wert ? eingabe.Text : wert;
            }
            else if (eingabe is not null) text = eingabe.Text;
            if (text is null) continue;
            if (mitZusatzangaben && f.Id == "sanierung.s_year" && text.Length == 4 && text.All(char.IsAsciiDigit))
            {
                // Ein Jahr ist kein vollständiges INTERLIS-Datum. Keinen 1. Januar erfinden.
                if (!akte.Werte.TryGetValue("sanierung.beginn", out var beginn) || beginn.Text.Length == 0)
                    Setze(root, "Zeitpunkt", "", eingabe?.VonHand == true);
                hinweise.Add($"Unterhalt {root.Tid}: Sanierungsjahr {text} in Erfasste_Angaben; ohne genauen Tag kein Zeitpunkt erfunden.");
                continue;
            }
            if (f.Id is "sanierung.firma" or "haltung.profile" or "haltung.width" or "schacht.rechtswert" or "schacht.hochwert" or "deckel.rechtswert" or "deckel.hochwert") continue;
            if (f.Id is "haltung.fromnode" or "haltung.tonode")
            {
                var punkt = Ziel(root, f.Id == "haltung.fromnode" ? "von" : "nach");
                if (punkt is null) throw new InvalidOperationException($"DSS: {f.Label}: Haltungspunkt fehlt.");
                if (text.Length == 0) Verweise(punkt, "AbwassernetzelementRef", "");
                else
                {
                    var kandidaten = objekte.Values.Where(o => o.Klasse == "Abwasserknoten" && o.Werte.GetValueOrDefault("Bezeichnung") == text).ToArray();
                    if (kandidaten.Length != 1) throw new InvalidOperationException($"DSS: Anschlussknoten „{text}“ fehlt oder ist mehrdeutig.");
                    Verweise(punkt, "AbwassernetzelementRef", kandidaten[0].Tid);
                }
                continue;
            }
            if (DssEinbautenZuordnung.Klassenanzeige(root.Klasse) is not null && f.Id is "bauwerksteil.art" or "ueberlauf.bauwerksart") continue;
            if (DssFeldZuordnung.Ziel(f, root.Klasse) is not { } ziel)
            {
                if (text.Length > 0) hinweise.Add($"{DssObjektarten.Bezeichnung(akte)}: {f.Label} = „{text}“ fehlt in der XTF, weil kein belegtes DSS-Zielfeld besteht; bleibt in Projekt/Objektakten-JSON.");
                continue;
            }
            var objekt = Ziel(root, ziel.Klasse);
            if (objekt is null) throw new InvalidOperationException($"DSS: {f.Label}: zugehöriges Objekt {ziel.Klasse} fehlt.");
            if (mitZusatzangaben && eingabe is { VonHand: false } && f.Speicherfeld is null
                && quellen.GetValueOrDefault(objekt.Tid)?.Werte.GetValueOrDefault(ziel.Attribut) == text
                && DssQuellabweichungen.IstAbweichung(objekt.Klasse, ziel.Attribut, text)) continue;
            if (ziel.Attribut.EndsWith("Ref", StringComparison.Ordinal))
            {
                Verweise(objekt, ziel.Attribut, text.Length > 0 && ziel.Attribut is "EigentuemerRef" or "BetreiberRef" ? Organisation(text) : text, eingabe?.VonHand == true || f.Speicherfeld is not null);
            }
            else if (mitZusatzangaben && ziel.Attribut == "Sanierungsbedarf" && text.Equals("Saniert", StringComparison.OrdinalIgnoreCase))
            {
                // Saniert ist eine erlaubte Eingabe in SewerStudio, aber KEIN DSS-Normwert.
                // Den überholten Bedarf entfernen und die Eingabe ausdrücklich separat liefern.
                Setze(objekt, ziel.Attribut, "", eingabe?.VonHand == true || f.Speicherfeld is not null);
                hinweise.Add($"{objekt.Klasse} {objekt.Tid}: Sanierungsbedarf «{text}» im Zusatzmodell Erfasste_Angaben; DSS kennt diesen Auswahlwert nicht. Kein Ersatzcode geraten.");
            }
            else Setze(objekt, ziel.Attribut, DssFeldZuordnung.Normwert(objekt.Klasse, ziel.Attribut, text), eingabe?.VonHand == true || f.Speicherfeld is not null);
            if (f.Id is "haltung.name" or "schacht.bezeichnung") Ziel(root, "Kanal")?.Setze("Bezeichnung", text);
        }
        if (id is { } rid && root.Klasse == "Haltung") DssProfilBearbeitung.Uebernehme(projekt, projekt.Data.Single(h => h.Id == rid), root, objekte, _ids);
        DssKoordinatenBearbeitung.Uebernehme(projekt, akte, root);
    }
    private DssExportObjekt? Ziel(DssExportObjekt root, string klasse)
    {
        if (root.Klasse == klasse) return root;
        var rolle = klasse switch
        {
            "Kanal" or "Normschacht" => "AbwasserbauwerkRef", "von" => "vonHaltungspunktRef", "nach" => "nachHaltungspunktRef", "Rohrprofil" => "RohrprofilRef", _ => ""
        };
        return objekte.GetValueOrDefault(root.Refs.GetValueOrDefault(rolle, ""));
    }
    private void Setze(DssExportObjekt o, string feld, string text, bool hand = true)
    {
        if (_vorgaben.TryGetValue((o.Tid, feld), out var vorhanden) && vorhanden.Hand && !hand) return;
        var norm = DssExportSchema.Normalisiere(o.Klasse, feld, text) ?? "";
        if (_vorgaben.TryGetValue((o.Tid, feld), out var vorher) && vorher.Wert != norm && (vorher.Hand || !hand))
            throw new InvalidOperationException($"DSS: widersprüchliche aktuelle Angaben für das gemeinsame Objekt {o.Tid}, Feld {feld}.");
        _vorgaben[(o.Tid, feld)] = (norm, hand); o.Setze(feld, norm);
    }
    private void BestandsErgaenzungen(Guid id, DssExportObjekt root)
    {
        var h = projekt.Data.SingleOrDefault(r => r.Id == id);
        var s = projekt.SchaechteData.SingleOrDefault(r => r.Id == id);
        string? Aktuell(string key)
        {
            var vergleichsfeld = key;
            key = s is null ? key : SchachtFeldnamen.Feld(s, key);
            var meta = h?.FieldMeta.GetValueOrDefault(key) ?? s?.FieldMeta.GetValueOrDefault(key);
            var text = h?.GetFieldValue(key) ?? s?.GetFieldValue(key) ?? "";
            return meta?.UserEdited == true || GeoShopImportVergleich.BehaeltBestandswert(projekt, id, vergleichsfeld, text)
                || text.Length > 0 && meta?.Source != FieldSource.Kataster ? text : null;
        }
        var bw = Ziel(root, "Kanal");
        foreach (var (key, rolle) in new[] { (FieldKeys.DataOwner, "DatenherrRef"), (FieldKeys.DataSupplier, "DatenlieferantRef") })
        {
            if (Aktuell(key) is not { } text) continue;
            if (text.Length == 0) throw new InvalidOperationException($"DSS: Pflichtangabe {key} wurde geleert.");
            var tid = Organisation(text);
            foreach (var o in new[] { root, bw, Ziel(root, "von"), Ziel(root, "nach") }.Where(o => o is not null)) Verweise(o!, rolle, tid);
        }
        if (bw is null) return;
        if (Aktuell(FieldKeys.InspectionYear) is { } datum)
        {
            var felder = new List<KeyValuePair<string, string>>();
            XtfBauwerkFelder.ErgaenzeGemeinsame(felder, key => key == FieldKeys.InspectionYear ? datum : null, root.Tid, hinweise);
            var jahr = felder.SingleOrDefault(f => f.Key == "Zustandserhebung_Jahr").Value;
            if (datum.Length > 0 && jahr is null) throw new InvalidOperationException("DSS: ungültiges Datum/Jahr der Zustandserhebung.");
            Setze(bw, "Zustandserhebung_Jahr", jahr ?? "");
        }
        if (s is not null && Aktuell(FieldKeys.ShaftStructureType) is { Length: > 0 } bauart)
        {
            var klasse = AbwasserbauwerkVokabular.Klasse(bauart, XtfSchachtPlanBuilder.Wert(s, "Funktion"));
            if (klasse != bw.Klasse) throw new InvalidOperationException($"DSS: Bauwerksart wurde von {bw.Klasse} auf {bauart} geändert. Die Originalkennung darf nicht unbemerkt die Objektklasse wechseln.");
        }
        if (bw.Klasse == "Versickerungsanlage" && Aktuell(FieldKeys.InfiltrationType) is { } art) Setze(bw, "Art", art);
        if (s is not null && Aktuell(FieldKeys.Street) is { } strasse) Setze(bw, "Standortname", strasse);
        if (s is not null && Aktuell(FieldKeys.GrossCost) is { } kosten) Setze(bw, "Bruttokosten", kosten);
    }
    private void Verweise(DssExportObjekt o, string rolle, string tid, bool hand = true)
    {
        if (_vorgaben.TryGetValue((o.Tid, rolle), out var vorher))
        {
            if (vorher.Hand && !hand) return;
            if (vorher.Wert != tid && (vorher.Hand || !hand)) throw new InvalidOperationException($"DSS: widersprüchliche Verweise für {o.Tid}, {rolle}.");
        }
        _vorgaben[(o.Tid, rolle)] = (tid, hand);
        if (o.Refs.GetValueOrDefault(rolle, "") == tid) return;
        if (tid.Length == 0) o.Refs.Remove(rolle); else o.Refs[rolle] = tid;
        o.Werte["Letzte_Aenderung"] = DateTime.Today.ToString("yyyyMMdd");
    }
    private string Organisation(string text)
    {
        if (SiaObjektkennung.IstGueltig(text)) return text;
        var treffer = projekt.Objektakten.SelectMany(a => a.Quellen).Where(q => q.Klasse == "Organisation" && q.Werte.GetValueOrDefault("Bezeichnung") == text)
            .Select(q => q.Kennung).Concat(objekte.Values.Where(o => o.Klasse == "Organisation" && o.Werte.GetValueOrDefault("Bezeichnung") == text).Select(o => o.Tid)).Distinct().ToArray();
        if (treffer.Length == 1)
        {
            if (quellen.TryGetValue(treffer[0], out var q)) objekte.TryAdd(q.Kennung, DssExportObjekt.Aus(q));
            return treffer[0];
        }
        if (treffer.Length > 1) throw new InvalidOperationException($"DSS: Organisationsname „{text}“ ist mehrdeutig. Bitte die Originalkennung wählen.");
        var typ = EigentumVokabular.NachOrganisationstyp(text);
        if (typ is null) throw new InvalidOperationException($"DSS: Für „{text}“ fehlen Organisationskennung und belegter Organisationstyp.");
        var tid = _ids.Fuer("Organisation", text);
        var org = new DssExportObjekt("Organisation", tid);
        org.Setze("Bezeichnung", text); org.Setze("Organisationstyp", typ); org.Setze("Status", "aktiv");
        objekte.TryAdd(tid, org); return tid;
    }
    private void Assoziation(string klasse, string rolle1, string ziel1, string rolle2, string ziel2)
    {
        if (objekte.Values.Any(o => o.Klasse == klasse && o.Refs.GetValueOrDefault(rolle1) == ziel1 && o.Refs.GetValueOrDefault(rolle2) == ziel2)) return;
        var id = _ids.Fuer(klasse, ziel1 + "|" + ziel2);
        var o = new DssExportObjekt(klasse, id, true); o.Refs[rolle1] = ziel1; o.Refs[rolle2] = ziel2; objekte.Add(id, o);
    }
}
