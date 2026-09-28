using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Was mit einem Feld beim Schreiben ins WebGIS passiert.</summary>
public enum WebGisVergleichsArt
{
    /// <summary>Der WebGIS-Wert wird durch den SewerStudio-Wert ersetzt.</summary>
    Aendern,
    /// <summary>Bemerkung: beide Texte bleiben, der SewerStudio-Text kommt dazu.</summary>
    Ergaenzen,
    /// <summary>Baujahr: nur ein leeres WebGIS-Feld wird gefüllt.</summary>
    Eintragen,
    /// <summary>Eine Sanierungsmassnahme wird neu angelegt.</summary>
    Anlegen,
    /// <summary>Beide Seiten stimmen überein.</summary>
    Gleich,
    /// <summary>Das WebGIS behält seinen Wert (Länge, Eigentümer, Betreiber, Baujahr, leerer SewerStudio-Wert).</summary>
    BleibtImWebGis,
    /// <summary>Wert der Kanalfirma, weicht ab — geht nur mit Haken hinaus.</summary>
    Vorschlag,
    /// <summary>Der Wert lässt sich nicht übertragen (z. B. kein WebGIS-Begriff) — mit Grund.</summary>
    NichtUebertragbar,
    /// <summary>Weicht ab, geht aber nicht hinaus (nicht von Hand gesetzt, aus dem Kataster).</summary>
    NichtGesendet,
}

/// <summary>
/// Eine Zeile der Vergleichsliste: Feld, Wert in SewerStudio, Wert im WebGIS jetzt und was beim Schreiben
/// passiert (Wunsch Pascal 28.09.2026: «alle Felder vergleichen, bevor ins WebGIS geschrieben wird»).
/// Reine Anzeige des Plans — entschieden wird im <see cref="WebGisExportPlanBuilder"/>, nicht hier.
/// </summary>
public sealed class WebGisFeldVergleich
{
    public required string Feld { get; init; }
    public required string SewerStudio { get; init; }
    public required string WebGis { get; init; }
    public required WebGisVergleichsArt Art { get; init; }
    /// <summary>Warum es so ist (leer, wenn selbsterklärend).</summary>
    public string Grund { get; init; } = string.Empty;
    /// <summary>Der Wert im WebGIS nach dem Schreiben (bei Ändern, Ergänzen, Eintragen, Vorschlag).</summary>
    public string? Nachher { get; init; }
    public string? RefId { get; init; }
    /// <summary>Der Vorschlag der Kanalfirma, dessen Haken diese Zeile steuert.</summary>
    public WebGisVorschlag? Vorschlag { get; init; }
    /// <summary>Die Massnahme bei einer Massnahmenzeile.</summary>
    public WebGisSanierungPosition? Massnahme { get; init; }

    /// <summary>Wird diese Zeile beim Schreiben wirksam (Vorschlag nur mit Haken)?</summary>
    public bool WirdGeschrieben => Art switch
    {
        WebGisVergleichsArt.Aendern or WebGisVergleichsArt.Ergaenzen or WebGisVergleichsArt.Eintragen => true,
        WebGisVergleichsArt.Anlegen => Massnahme?.Schreibbar ?? true,
        WebGisVergleichsArt.Vorschlag => Vorschlag?.Gewaehlt ?? false,
        _ => false,
    };

    /// <summary>Unterscheiden sich die beiden Seiten (für «Nur Unterschiede zeigen»)?</summary>
    public bool IstUnterschied => Art != WebGisVergleichsArt.Gleich;
}

/// <summary>
/// Baut die Vergleichszeilen einer Position aus Eingabe, gelesenem WebGIS-Stand und fertig geplanter Position.
/// Die Einordnung folgt ausschliesslich dem, was der Plan tatsächlich schreibt (<see cref="WebGisExportPosition.Aenderungen"/>,
/// <see cref="WebGisExportPosition.Vorschlaege"/>, Hinweise) — die Liste kann also nie etwas versprechen, das nicht geschrieben wird.
/// </summary>
public static class WebGisVergleichBuilder
{
    private const string Leer = "—";

    public static void Baue(WebGisObjektEingabe e, WebGisLesestand stand, WebGisExportPosition pos)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(pos);
        var art = e.Objektart;
        var z = pos.Vergleich;

        // Zustand
        var zustandSs = WebGisFeldkarte.ZustandCode(e.Zustandsklasse) is int code
            ? WebGisFeldkarte.ZustandText(code)
            : Text(e.Zustandsklasse);
        z.Add(Klassifiziere(pos, stand, "Zustand", WebGisFeldkarte.ZustandRef(art),
            e.Zustandsklasse is null ? Roh(e, "Zustandsklasse") : zustandSs,
            gesendetWert: e.Zustandsklasse, hinweisSchluessel: "Zustandsklasse"));

        // Sanierungsbedarf: nur «Saniert» mit ausgeführter Akte geht hinaus
        z.Add(Sanierungsbedarf(e, stand, pos));

        // Bemerkung: zusammenführen
        z.Add(Klassifiziere(pos, stand, "Bemerkung", WebGisFeldkarte.BemerkungRef(art),
            e.Bemerkung ?? Roh(e, "Bemerkungen", "Bemerkung"), gesendetWert: e.Bemerkung, hinweisSchluessel: null,
            aenderungsArt: WebGisVergleichsArt.Ergaenzen));

        // Baujahr: nur ein leeres Feld füllen, nie überschreiben
        z.Add(Baujahr(e, stand, pos));

        // Felder der Handwertkarte (eine Zeile je WebGIS-Feld; DN und lichte Breite teilen eines)
        foreach (var gruppe in WebGisHandwertKarte.Felder.Where(f => f.Objektart == art).GroupBy(f => f.RefId))
        {
            var karte = gruppe.FirstOrDefault(f => !string.IsNullOrWhiteSpace(Roh(e, f.SewerStudioFeld))) ?? gruppe.First();
            z.Add(Klassifiziere(pos, stand, karte.Anzeige, karte.RefId, Roh(e, karte.SewerStudioFeld),
                gesendetWert: e.Handwerte.TryGetValue(karte.SewerStudioFeld, out var hw) ? hw : null,
                hinweisSchluessel: karte.Anzeige, zweiterHinweisSchluessel: karte.SewerStudioFeld));
        }

        // Nie ins WebGIS: Länge (nur Haltung), Eigentümer, Betreiber
        if (art == WebGisObjektart.Haltung)
            z.Add(NieGeschrieben(stand, "Länge", WebGisFeldkarte.HaltungLaengeGeomRef,
                Roh(e, "Haltungslaenge_m", "Laenge_m"), "Länge geht nie ins WebGIS"));
        z.Add(NieGeschrieben(stand, "Eigentümer", WebGisFeldkarte.EigentuemerRef(art),
            Roh(e, "Eigentuemer", "Eigentümer"), "führt das WebGIS"));
        z.Add(NieGeschrieben(stand, "Betreiber", WebGisFeldkarte.BetreiberRef(art),
            Roh(e, "Betreiber"), "führt das WebGIS"));
    }

    /// <summary>Zeile einer geplanten Sanierungsmassnahme.</summary>
    public static WebGisFeldVergleich Massnahme(WebGisSanierungPosition san)
    {
        ArgumentNullException.ThrowIfNull(san);
        var werte = san.Anzeige.Count == 0 ? Leer : string.Join(" · ", san.Anzeige);
        var vorhanden = san.Sperren.Exists(s => s.Contains("bereits vorhanden", StringComparison.OrdinalIgnoreCase));
        if (vorhanden)
            return new WebGisFeldVergleich
            {
                Feld = "Sanierungsmassnahme", SewerStudio = werte, WebGis = "bereits vorhanden",
                Art = WebGisVergleichsArt.Gleich, Grund = "bereits im WebGIS", Massnahme = san,
            };
        if (san.Schreibbar)
            return new WebGisFeldVergleich
            {
                Feld = "Sanierungsmassnahme", SewerStudio = werte, WebGis = Leer, Art = WebGisVergleichsArt.Anlegen,
                Grund = string.Join(" ", san.Hinweise), Nachher = werte, Massnahme = san,
            };
        return new WebGisFeldVergleich
        {
            Feld = "Sanierungsmassnahme", SewerStudio = werte, WebGis = Leer, Art = WebGisVergleichsArt.NichtUebertragbar,
            Grund = string.Join(" ", san.Sperren), Massnahme = san,
        };
    }

    private static WebGisFeldVergleich Klassifiziere(
        WebGisExportPosition pos, WebGisLesestand stand, string feld, string refId, string? sewerStudio,
        string? gesendetWert, string? hinweisSchluessel, string? zweiterHinweisSchluessel = null,
        WebGisVergleichsArt aenderungsArt = WebGisVergleichsArt.Aendern)
    {
        var webgis = Text(stand.FeldText(refId) ?? stand.Feld(refId));
        var ss = Text(sewerStudio);

        var aenderung = pos.Aenderungen.Find(a => string.Equals(a.RefId, refId, StringComparison.Ordinal));
        if (aenderung is not null)
            return new WebGisFeldVergleich
            {
                Feld = feld, SewerStudio = ss, WebGis = Text(aenderung.AltText ?? aenderung.Alt), Art = aenderungsArt,
                Nachher = aenderung.NeuText ?? aenderung.Neu, RefId = refId,
                Grund = aenderungsArt == WebGisVergleichsArt.Ergaenzen ? "beide Texte bleiben" : string.Empty,
            };

        var vorschlag = pos.Vorschlaege.Find(v => v.Aenderungen.Exists(a => string.Equals(a.RefId, refId, StringComparison.Ordinal)));
        if (vorschlag is not null)
            return new WebGisFeldVergleich
            {
                Feld = feld, SewerStudio = ss, WebGis = webgis, Art = WebGisVergleichsArt.Vorschlag,
                Grund = "Wert der Kanalfirma — nur mit Haken", Nachher = vorschlag.NeuText, RefId = refId, Vorschlag = vorschlag,
            };

        if (ss == Leer)
            return new WebGisFeldVergleich
            {
                Feld = feld, SewerStudio = ss, WebGis = webgis, RefId = refId,
                Art = webgis == Leer ? WebGisVergleichsArt.Gleich : WebGisVergleichsArt.BleibtImWebGis,
                Grund = webgis == Leer ? string.Empty : "in SewerStudio leer — bleibt, wie es ist",
            };

        if (webgis != Leer && WebGisExportPlanBuilder.GleicherWert(webgis, ss))
            return new WebGisFeldVergleich { Feld = feld, SewerStudio = ss, WebGis = webgis, Art = WebGisVergleichsArt.Gleich, RefId = refId };

        var hinweis = Hinweis(pos, hinweisSchluessel) ?? Hinweis(pos, zweiterHinweisSchluessel);
        if (hinweis is not null)
            return new WebGisFeldVergleich
            {
                Feld = feld, SewerStudio = ss, WebGis = webgis, Art = WebGisVergleichsArt.NichtUebertragbar, Grund = hinweis, RefId = refId,
            };

        return new WebGisFeldVergleich
        {
            Feld = feld, SewerStudio = ss, WebGis = webgis, Art = WebGisVergleichsArt.NichtGesendet, RefId = refId,
            Grund = gesendetWert is null
                ? "nicht von Hand gesetzt — geht nur als Handwert oder angehakter Vorschlag hinaus"
                : "wird nicht gesendet",
        };
    }

    private static WebGisFeldVergleich Sanierungsbedarf(WebGisObjektEingabe e, WebGisLesestand stand, WebGisExportPosition pos)
    {
        var refId = WebGisFeldkarte.SanierungsbedarfRef(e.Objektart);
        var webgis = Text(stand.FeldText(refId) ?? stand.Feld(refId));
        var aenderung = pos.Aenderungen.Find(a => string.Equals(a.RefId, refId, StringComparison.Ordinal));
        var ss = e.Saniert ? "Saniert (ausgeführte Sanierungsakte)" : Text(Roh(e, "Sanierungsbedarf"));
        if (aenderung is not null)
            return new WebGisFeldVergleich
            {
                Feld = "Sanierungsbedarf", SewerStudio = ss, WebGis = Text(aenderung.AltText ?? aenderung.Alt),
                Art = WebGisVergleichsArt.Aendern, Nachher = aenderung.NeuText ?? aenderung.Neu, RefId = refId,
            };
        if (e.Saniert || ss == Leer || (webgis != Leer && WebGisExportPlanBuilder.GleicherWert(webgis, ss)))
            return new WebGisFeldVergleich
            {
                Feld = "Sanierungsbedarf", SewerStudio = ss, WebGis = webgis, RefId = refId,
                Art = ss == Leer && webgis != Leer ? WebGisVergleichsArt.BleibtImWebGis : WebGisVergleichsArt.Gleich,
                Grund = ss == Leer && webgis != Leer ? "in SewerStudio leer — bleibt, wie es ist" : string.Empty,
            };
        return new WebGisFeldVergleich
        {
            Feld = "Sanierungsbedarf", SewerStudio = ss, WebGis = webgis, Art = WebGisVergleichsArt.NichtGesendet, RefId = refId,
            Grund = "nur «Saniert» mit ausgeführter Sanierungsakte geht hinaus",
        };
    }

    private static WebGisFeldVergleich Baujahr(WebGisObjektEingabe e, WebGisLesestand stand, WebGisExportPosition pos)
    {
        var refId = WebGisFeldkarte.BaujahrRef(e.Objektart);
        var webgis = Text(stand.Feld(refId));
        var ss = Text(e.Baujahr ?? Roh(e, "Baujahr"));
        var aenderung = pos.Aenderungen.Find(a => string.Equals(a.RefId, refId, StringComparison.Ordinal));
        if (aenderung is not null)
            return new WebGisFeldVergleich
            {
                Feld = "Baujahr", SewerStudio = ss, WebGis = Leer, Art = WebGisVergleichsArt.Eintragen,
                Nachher = aenderung.Neu, RefId = refId, Grund = "im WebGIS leer — wird eingetragen",
            };
        if (ss == Leer || webgis == ss)
            return new WebGisFeldVergleich
            {
                Feld = "Baujahr", SewerStudio = ss, WebGis = webgis, RefId = refId,
                Art = ss == webgis ? WebGisVergleichsArt.Gleich : WebGisVergleichsArt.BleibtImWebGis,
                Grund = ss == webgis ? string.Empty : "in SewerStudio leer — bleibt, wie es ist",
            };
        return new WebGisFeldVergleich
        {
            Feld = "Baujahr", SewerStudio = ss, WebGis = webgis, Art = WebGisVergleichsArt.BleibtImWebGis, RefId = refId,
            Grund = e.Baujahr is null ? "Wert aus dem Kataster — geht nicht zurück" : "Baujahr wird nie überschrieben",
        };
    }

    private static WebGisFeldVergleich NieGeschrieben(WebGisLesestand stand, string feld, string refId, string? sewerStudio, string grund)
    {
        var webgis = Text(stand.FeldText(refId) ?? stand.Feld(refId));
        var ss = Text(sewerStudio);
        var gleich = ss == webgis || (ss != Leer && webgis != Leer && WebGisExportPlanBuilder.GleicherWert(webgis, ss));
        return new WebGisFeldVergleich
        {
            Feld = feld, SewerStudio = ss, WebGis = webgis, RefId = refId,
            Art = gleich ? WebGisVergleichsArt.Gleich : WebGisVergleichsArt.BleibtImWebGis,
            Grund = gleich ? string.Empty : grund,
        };
    }

    private static string? Hinweis(WebGisExportPosition pos, string? schluessel)
        => string.IsNullOrWhiteSpace(schluessel)
            ? null
            : pos.Hinweise.Find(h => h.Contains(schluessel, StringComparison.OrdinalIgnoreCase));

    /// <summary>Wert eines SewerStudio-Felds aus der Momentaufnahme; Schachtfelder auch unter der Vorlagenschreibweise.</summary>
    private static string? Roh(WebGisObjektEingabe e, params string[] namen)
    {
        foreach (var name in namen)
        {
            if (e.Werte.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            var gefaltet = WebGisHandwertKarte.Falte(name);
            foreach (var (k, wert) in e.Werte)
                if (WebGisHandwertKarte.Falte(k) == gefaltet && !string.IsNullOrWhiteSpace(wert)) return wert;
        }
        return null;
    }

    private static string Text(string? wert) => string.IsNullOrWhiteSpace(wert) ? Leer : wert.Trim();
}
