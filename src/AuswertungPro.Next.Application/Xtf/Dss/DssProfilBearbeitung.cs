using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.Application.Xtf.Dss;

internal static class DssProfilBearbeitung
{
    public static void Uebernehme(Project projekt, HaltungRecord h, DssExportObjekt haltung, Dictionary<string, DssExportObjekt> objekte, XtfNeuKennungen ids)
    {
        bool Geaendert(string key) => GeoShopImportVergleich.BehaeltBestandswert(projekt, h.Id, key, h.GetFieldValue(key))
            || h.FieldMeta.TryGetValue(key, out var m) && (m.UserEdited || m.Source != FieldSource.Kataster && h.GetFieldValue(key).Length > 0);
        var typGeaendert = Geaendert(FieldKeys.ProfileType);
        var breiteGeaendert = Geaendert(FieldKeys.ClearWidthMm);
        if (!typGeaendert && !breiteGeaendert && !Geaendert(FieldKeys.NominalDiameterMm) && !Geaendert(FieldKeys.DataOwner) && !Geaendert(FieldKeys.DataSupplier)) return;
        var alt = objekte.GetValueOrDefault(haltung.Refs.GetValueOrDefault("RohrprofilRef", ""));
        var rohTyp = typGeaendert ? h.GetFieldValue(FieldKeys.ProfileType) : alt?.Werte.GetValueOrDefault("Profiltyp") ?? "";
        if (rohTyp.Length == 0)
        {
            if (typGeaendert) haltung.Refs.Remove("RohrprofilRef");
            return;
        }
        var typ = ProfiltypVokabular.NachNorm(rohTyp) ?? rohTyp;
        var ratio = alt?.Werte.GetValueOrDefault("HoehenBreitenverhaeltnis");
        var breite = h.GetFieldValue(FieldKeys.ClearWidthMm);
        var hoehe = h.GetFieldValue(FieldKeys.NominalDiameterMm);
        if (breiteGeaendert && breite.Length == 0) ratio = null;
        else if (breite.Length > 0)
        {
            ratio = XtfRohrprofilVerhaeltnis.Berechne(hoehe, breite);
            if (ratio is null && typ != "Kreisprofil") throw new InvalidOperationException("DSS: Rohrprofilbreite und lichte Höhe ergeben kein eindeutiges Verhältnis.");
        }
        if (typ == "Kreisprofil")
        {
            if (ratio is not null && ratio != "1" && ratio != "1.00") throw new InvalidOperationException("DSS: Kreisprofil mit unterschiedlicher Höhe und Breite.");
            ratio = "1.00";
        }
        // Ergibt sich dasselbe Profil wie das Original, bleibt der Originalverweis. Sonst
        // entstünde je Haltung eine identische Kopie unter neuer Kennung (Bürglen 14.09.2026:
        // 19 gleiche Kreisprofile, ausgelöst durch eine DN aus dem alten Import).
        if (alt is not null && alt.Werte.GetValueOrDefault("Profiltyp") == typ
            && GleichesVerhaeltnis(alt.Werte.GetValueOrDefault("HoehenBreitenverhaeltnis"), ratio)
            && alt.Refs.GetValueOrDefault("DatenherrRef") == haltung.Refs.GetValueOrDefault("DatenherrRef")
            && alt.Refs.GetValueOrDefault("DatenlieferantRef") == haltung.Refs.GetValueOrDefault("DatenlieferantRef")) return;
        // Gemeinsames Katasterprofil niemals unter seiner Original-TID verändern.
        var tid = ids.Fuer("Rohrprofil", h.Id.ToString("N") + "|" + typ + "|" + ratio
            + "|" + haltung.Refs["DatenherrRef"] + "|" + haltung.Refs["DatenlieferantRef"]);
        var profil = new DssExportObjekt("Rohrprofil", tid);
        if (alt is not null) foreach (var (k, v) in alt.Werte) profil.Werte[k] = v;
        foreach (var rolle in new[] { "DatenherrRef", "DatenlieferantRef" }) profil.Refs[rolle] = haltung.Refs[rolle];
        profil.Setze("Bezeichnung", "Profil_" + tid[5..]); profil.Setze("Profiltyp", typ);
        profil.Setze("HoehenBreitenverhaeltnis", ratio ?? "");
        objekte.TryAdd(tid, profil); haltung.Refs["RohrprofilRef"] = tid;
        haltung.Werte["Letzte_Aenderung"] = DateTime.Today.ToString("yyyyMMdd");
    }

    /// <summary>«1», «1.00» und «1.000» sind dasselbe Verhältnis; leer ist nur leer gleich.</summary>
    private static bool GleichesVerhaeltnis(string? original, string? neu)
    {
        if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(neu)) return string.IsNullOrWhiteSpace(original) && string.IsNullOrWhiteSpace(neu);
        return decimal.TryParse(original, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var a)
            && decimal.TryParse(neu, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var b)
            ? a == b : original == neu;
    }
}
