using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Ein Wert fuer ein Feld der Sanierungsakte (Klartext; bei Auswahlfeldern mit Katalogeintrag).</summary>
public sealed record WebGisAkteWert(string FeldId, string Text, ObjektAuswahl? Auswahl, string? KatalogId);

/// <summary>Eine aus dem WebGIS zu holende Sanierungsmassnahme.</summary>
public sealed class WebGisSanierungImport
{
    public required WebGisObjektart Objektart { get; init; }
    public Guid ElternRecordId { get; init; }
    public required string ElternBezeichnung { get; init; }
    public required string WebGisGlobalId { get; init; }
    /// <summary>Alle Maskenfelder der Massnahme beim Planen; weicht der Stand vor dem Uebernehmen ab, keine Akte.</summary>
    public IReadOnlyDictionary<string, string?>? GelesenerStand { get; init; }
    public List<WebGisAkteWert> Werte { get; } = new();
    public List<string> Hinweise { get; } = new();
    /// <summary>Sperrgruende — bei mindestens einem wird keine Akte angelegt.</summary>
    public List<string> Sperren { get; } = new();
    public bool Uebernehmbar => Sperren.Count == 0 && Werte.Count > 0;
    public bool Uebernommen { get; set; }

    /// <summary>"Art · Status · Verfahren · Jahr" fuer Vorschau und Bericht.</summary>
    public string Kurztext => string.Join(" · ", new[]
    {
        WebGisSanierungFeldkarte.AkteArt, WebGisSanierungFeldkarte.AkteStatus,
        WebGisSanierungFeldkarte.AkteVerfahren, WebGisSanierungFeldkarte.AkteJahr,
    }.Select(id => Werte.FirstOrDefault(w => w.FeldId == id)?.Text).Where(t => !string.IsNullOrEmpty(t)));
}

/// <summary>
/// WebGIS-Sanierungsmassnahme -> Sanierungsakte in SewerStudio (Holen, 23.09.2026). Reine Regel.
/// Auswahlwerte gehen NUR ueber den Klartext gegen den Katalog der Akte (LokalerEintrag und
/// WebGIS-Schluessel sind verschiedene Nummernkreise); das Verfahren gegen die Liste der Art.
/// Ohne bekannte Art keine Akte. Geschrieben wird wie ein Import: ohne Handmarke, mit Beleg
/// System «WebGIS» und der GlobalId der Massnahme — so kommt dieselbe Massnahme nie doppelt.
/// </summary>
public static class WebGisSanierungImportRegel
{
    public const string BelegSystem = "WebGIS";

    public static WebGisSanierungImport Baue(
        WebGisObjektart art, Guid elternRecordId, string elternBezeichnung, string globalId, WebGisLesestand massnahme)
    {
        ArgumentNullException.ThrowIfNull(massnahme);
        var imp = new WebGisSanierungImport
        {
            Objektart = art, ElternRecordId = elternRecordId, ElternBezeichnung = elternBezeichnung, WebGisGlobalId = globalId,
            GelesenerStand = new Dictionary<string, string?>(massnahme.Felder, StringComparer.Ordinal),
        };
        var katalog = FieldCatalog.Objektfelder;

        string? artCode = null;
        foreach (var (akteKey, refId, anzeige) in WebGisSanierungFeldkarte.ComboFelder)
        {
            var text = Klartext(massnahme, refId);
            if (text is null) continue;
            var feld = katalog.Feld(akteKey);

            IEnumerable<ObjektAuswahl> eintraege;
            string? katalogId;
            if (feld.KatalogIdJeEltern is { } jeEltern)
            {
                katalogId = jeEltern;
                eintraege = artCode is null ? [] : katalog.Auswahl(jeEltern)?.Eintraege.Where(e => e.Eltern == artCode) ?? [];
            }
            else
            {
                katalogId = feld.KatalogId;
                eintraege = katalog.Auswahl(feld.KatalogId)?.Eintraege ?? [];
            }

            var treffer = eintraege.Where(e => WebGisHandwertKarte.Falte(e.Label) == WebGisHandwertKarte.Falte(text)).Take(2).ToList();
            if (treffer.Count != 1)
            {
                var grund = $"{anzeige} «{text}» steht nicht in der SewerStudio-Liste" + (feld.KatalogIdJeEltern is null ? "" : " der Art");
                if (akteKey == WebGisSanierungFeldkarte.AkteArt) imp.Sperren.Add(grund + " — Massnahme nicht übernommen.");
                else imp.Hinweise.Add(grund + " — Feld leer gelassen.");
                continue;
            }
            if (akteKey == WebGisSanierungFeldkarte.AkteArt) artCode = treffer[0].OriginalCode;
            imp.Werte.Add(new WebGisAkteWert(akteKey, treffer[0].Label, treffer[0], katalogId));
        }
        if (!imp.Werte.Any(w => w.FeldId == WebGisSanierungFeldkarte.AkteArt) && imp.Sperren.Count == 0)
            imp.Sperren.Add("Art der Massnahme fehlt im WebGIS — Massnahme nicht übernommen.");

        var name = (massnahme.Feld(WebGisSanierungFeldkarte.BezeichnungRef) ?? string.Empty).Trim();
        if (name.Length > 0) imp.Werte.Add(new WebGisAkteWert(WebGisSanierungFeldkarte.AkteName, name, null, null));

        if (Jahr(massnahme.Feld(WebGisSanierungFeldkarte.SanierungsjahrRef)) is { } jahr)
            imp.Werte.Add(new WebGisAkteWert(WebGisSanierungFeldkarte.AkteJahr, jahr, null, null));

        return imp;
    }

    /// <summary>
    /// Gibt es am Objekt schon eine Akte fuer diese Massnahme? Gleiche WebGIS-Kennung im Beleg, oder
    /// gleiche Art + Status + Verfahren (dieselbe Regel wie beim Senden, gegen Doppel in beide Richtungen).
    /// </summary>
    public static bool SchonVorhanden(IEnumerable<ObjektAkte> akten, Guid recordId, WebGisSanierungZeile zeile)
    {
        foreach (var a in akten)
        {
            if (a.Art != WebGisSaniertKriterium.ArtSanierung || !a.Bezuege.Contains(recordId)) continue;
            if (zeile.GlobalId is { Length: > 0 } gid
                && a.Quellen.Any(q => q.System == BelegSystem && string.Equals(q.Kennung, gid, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (zeile.Art is null) continue;
            if (Gleich(a, WebGisSanierungFeldkarte.AkteArt, zeile.Art)
                && Gleich(a, WebGisSanierungFeldkarte.AkteStatus, zeile.Status)
                && Gleich(a, WebGisSanierungFeldkarte.AkteVerfahren, zeile.Verfahren))
                return true;
        }
        return false;
    }

    /// <summary>Legt die Akte an. False, wenn nicht uebernehmbar, das Objekt fehlt oder sie schon da ist.</summary>
    public static bool LegeAn(Project projekt, WebGisSanierungImport imp)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        ArgumentNullException.ThrowIfNull(imp);
        if (!imp.Uebernehmbar) return false;
        var gibtEs = imp.Objektart == WebGisObjektart.Haltung
            ? projekt.Data.Any(r => r.Id == imp.ElternRecordId)
            : projekt.SchaechteData.Any(r => r.Id == imp.ElternRecordId);
        if (!gibtEs) return false;

        var zeile = new WebGisSanierungZeile
        {
            GlobalId = imp.WebGisGlobalId,
            Art = Text(imp, WebGisSanierungFeldkarte.AkteArt),
            Status = Text(imp, WebGisSanierungFeldkarte.AkteStatus),
            Verfahren = Text(imp, WebGisSanierungFeldkarte.AkteVerfahren),
        };
        if (SchonVorhanden(projekt.Objektakten, imp.ElternRecordId, zeile)) return false;

        var jetzt = DateTime.UtcNow;
        var akte = new ObjektAkte { Art = WebGisSaniertKriterium.ArtSanierung, Bezuege = [imp.ElternRecordId] };
        foreach (var w in imp.Werte)
            akte.Werte[w.FeldId] = new ObjektFeldWert
            {
                Text = w.Text, KatalogId = w.KatalogId, Originalcode = w.Auswahl?.OriginalCode,
                LokalerEintrag = w.Auswahl?.Index, VonHand = false, GeaendertUtc = jetzt,
            };
        akte.Quellen.Add(new ObjektQuellbeleg
        {
            System = BelegSystem, Klasse = WebGisSanierungFeldkarte.Tabelle, Kennung = imp.WebGisGlobalId, ImportiertUtc = jetzt,
        });
        projekt.Objektakten.Add(akte);
        imp.Uebernommen = true;
        return true;
    }

    private static string? Text(WebGisSanierungImport imp, string feldId) => imp.Werte.FirstOrDefault(w => w.FeldId == feldId)?.Text;

    private static bool Gleich(ObjektAkte a, string feldId, string? text)
        => WebGisHandwertKarte.Falte(a.Werte.GetValueOrDefault(feldId)?.Text) == WebGisHandwertKarte.Falte(text);

    private static string? Klartext(WebGisLesestand s, string refId)
    {
        var key = s.Feld(refId);
        if (string.IsNullOrWhiteSpace(key) || !s.Kataloge.TryGetValue(refId, out var liste)) return null;
        foreach (var (k, t) in liste)
            if (k == key)
                return string.IsNullOrWhiteSpace(t) || WebGisHandwertKarte.Falte(t) == "unbekannt" ? null : t.Trim();
        return null;
    }

    /// <summary>Jahr aus dem Datum der Maske: «01.01.2026», ISO oder Millisekunden seit 1970.</summary>
    private static string? Jahr(string? wert)
    {
        var t = (wert ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        if (t.Length >= 11 && long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.Year.ToString(CultureInfo.InvariantCulture);
        var m = Regex.Match(t, @"(?<!\d)(19\d\d|20\d\d|2100)(?!\d)");
        return m.Success ? m.Value : null;
    }
}
