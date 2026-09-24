using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.UseCases.Xtf;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Eine Zeile der Holen-Vorschau mit Bezug zum Objekt (Doppelklick oeffnet es).</summary>
public sealed record WebGisHolenZeile(string Objekt, string Feld, string Alt, string Neu, WebGisObjektart Objektart, Guid RecordId);

/// <summary>Kopf des Holen-Fensters: Warnung (orange) oder alles zugeordnet (gruen), dazu die Zahlen.</summary>
public sealed record WebGisHolenKopf(bool Warnung, string Warntext, string Zusammenfassung);

/// <summary>
/// Darstellung des Holens (WebGIS -> SewerStudio): Tabellenzeilen mit Objektbezug, Hinweise,
/// Zusammenfassung und der ganze Bericht. Reine Darstellung.
/// </summary>
public static class WebGisImportBericht
{
    /// <summary>Die Tabellenzeilen: jede geplante Feldaenderung und jede neue Sanierungsakte, mit Objektbezug.</summary>
    public static IReadOnlyList<WebGisHolenZeile> Zeilen(WebGisImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var zeilen = new List<WebGisHolenZeile>();
        foreach (var p in plan.Positionen)
            if (p.Uebernehmbar)
                foreach (var a in p.Aenderungen)
                    zeilen.Add(new WebGisHolenZeile(Objekt(p.Objektart, p.Bezeichnung), Anzeigename(a.Feld), Leer(a.Alt), a.Neu, p.Objektart, p.RecordId));
        foreach (var s in plan.Sanierungen)
            if (s.Uebernehmbar)
                zeilen.Add(new WebGisHolenZeile(Objekt(s.Objektart, s.ElternBezeichnung), "Sanierungsmassnahme (neu)",
                    XtfExportVorschau.Leer, s.Kurztext, s.Objektart, s.ElternRecordId));
        return zeilen;
    }

    public const string ArtObjektGesperrt = "Objekt gesperrt";
    public const string ArtMassnahmeGesperrt = "Massnahme gesperrt";
    public const string ArtWertNichtUebernommen = "Wert nicht übernommen";

    /// <summary>
    /// Alles, was nicht zugeordnet werden konnte und deshalb nicht uebernommen wird (Wunsch Pascal
    /// 23.09.2026: klar gekennzeichnet, nicht in einer zugeklappten Hinweisliste). Zuerst ganze
    /// Objekte und Massnahmen, danach einzelne Werte. Jeder Hinweis des Holens heisst «nicht uebernommen».
    /// </summary>
    public static IReadOnlyList<WebGisHolenZeile> NichtZugeordnet(WebGisImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var gesperrt = new List<WebGisHolenZeile>();
        var werte = new List<WebGisHolenZeile>();
        foreach (var p in plan.Positionen)
        {
            var objekt = Objekt(p.Objektart, p.Bezeichnung);
            foreach (var s in p.Sperren) gesperrt.Add(new WebGisHolenZeile(objekt, ArtObjektGesperrt, "", s, p.Objektart, p.RecordId));
            foreach (var h in p.Hinweise) werte.Add(new WebGisHolenZeile(objekt, ArtWertNichtUebernommen, "", h, p.Objektart, p.RecordId));
        }
        foreach (var s in plan.Sanierungen)
        {
            var objekt = Objekt(s.Objektart, s.ElternBezeichnung);
            foreach (var sp in s.Sperren) gesperrt.Add(new WebGisHolenZeile(objekt, ArtMassnahmeGesperrt, "", sp, s.Objektart, s.ElternRecordId));
            foreach (var h in s.Hinweise) werte.Add(new WebGisHolenZeile(objekt, ArtWertNichtUebernommen, "", "Sanierungsmassnahme: " + h, s.Objektart, s.ElternRecordId));
        }
        gesperrt.AddRange(werte);
        return gesperrt;
    }

    /// <summary>Kopf des Fensters: Warnung, sobald etwas gesperrt oder nicht zugeordnet ist.</summary>
    public static WebGisHolenKopf Kopf(WebGisImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var objekte = plan.Gesperrte;
        var massnahmen = plan.Sanierungen.Count(s => s.Sperren.Count > 0);
        var werte = plan.Positionen.Sum(p => p.Hinweise.Count) + plan.Sanierungen.Sum(s => s.Hinweise.Count);
        var teile = new List<string>();
        if (objekte > 0) teile.Add(objekte == 1 ? "1 Objekt gesperrt" : $"{objekte} Objekte gesperrt");
        if (massnahmen > 0) teile.Add(massnahmen == 1 ? "1 Massnahme gesperrt" : $"{massnahmen} Massnahmen gesperrt");
        if (werte > 0) teile.Add(werte == 1 ? "1 Wert nicht zugeordnet" : $"{werte} Werte nicht zugeordnet");
        var warntext = teile.Count == 0 ? "" : string.Join(" · ", teile) + " — wird nicht übernommen (rote Liste darunter).";
        return new WebGisHolenKopf(teile.Count > 0, warntext, Vorschau(plan).Zusammenfassung);
    }

    /// <summary>Lesbarer Name fuer interne Feldschluessel (Akte).</summary>
    private static string Anzeigename(string feld) => feld switch
    {
        WebGisImportPlanBuilder.FeldWebGisGlobalId => "GlobalID (WebGIS)",
        "haltung.pipegroup" or "schacht.materialgruppe" => "Materialgruppe",
        "haltung.operator" or "schacht.betreiber" => "Betreiber",
        "Haltungslaenge_m" => "Haltungslänge [m]",
        _ => WebGisImportAktenfelder.Anzeige(feld) ?? feld,
    };

    public static XtfExportVorschau Vorschau(WebGisImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var zeilen = new List<XtfVorschauZeile>();
        var warnungen = new List<string>();
        foreach (var p in plan.Positionen)
        {
            var objekt = Objekt(p.Objektart, p.Bezeichnung);
            if (p.Uebernehmbar)
                foreach (var a in p.Aenderungen)
                    zeilen.Add(new XtfVorschauZeile(objekt, Anzeigename(a.Feld), Leer(a.Alt), a.Neu));
            foreach (var s in p.Sperren) warnungen.Add($"{objekt}: nicht gelesen — {s}");
            foreach (var h in p.Hinweise) warnungen.Add($"{objekt}: {h}");
        }
        foreach (var s in plan.Sanierungen)
        {
            var objekt = Objekt(s.Objektart, s.ElternBezeichnung);
            if (s.Uebernehmbar)
                zeilen.Add(new XtfVorschauZeile(objekt, "Sanierungsmassnahme (neu)", XtfExportVorschau.Leer, s.Kurztext));
            foreach (var sp in s.Sperren) warnungen.Add($"{objekt}, Sanierungsmassnahme: {sp}");
            foreach (var h in s.Hinweise) warnungen.Add($"{objekt}, Sanierungsmassnahme: {h}");
        }

        var felder = plan.Positionen.Where(p => p.Uebernehmbar).Sum(p => p.Aenderungen.Count);
        var massnahmen = plan.SanierungenUebernehmbar;
        var zusammenfassung =
            $"{felder} Felder in {plan.Uebernehmbare} Objekten · {massnahmen} Sanierungsmassnahme{(massnahmen == 1 ? "" : "n")} neu · "
            + $"{plan.Gesperrte} nicht gelesen. Handwerte bleiben stehen; das WebGIS ersetzt nur Werte aus GeoShop/QGIS. "
            + "Ins WebGIS wird nichts geschrieben.";

        return new XtfExportVorschau(
            "Vom WebGIS holen",
            zusammenfassung,
            zeilen,
            warnungen,
            Details(plan),
            IstFehler: zeilen.Count == 0);
    }

    /// <summary>Vollstaendiger Bericht als Text (Details und Projektablage).</summary>
    public static string Details(WebGisImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var sb = new StringBuilder();
        foreach (var h in plan.Hinweise) sb.AppendLine(h);

        // Zuoberst, nicht zwischen 172 anderen Objekten (Wunsch Pascal 23.09.2026, Lauf 19:26).
        var nichtZugeordnet = NichtZugeordnet(plan);
        if (nichtZugeordnet.Count > 0)
        {
            var kopf = $"=== NICHT ZUGEORDNET — WIRD NICHT ÜBERNOMMEN ({nichtZugeordnet.Count}) ===";
            sb.AppendLine().AppendLine(kopf);
            foreach (var z in nichtZugeordnet) sb.AppendLine($"{Kennung(z.Feld)}{z.Objekt} — {z.Neu}");
            sb.AppendLine(new string('=', kopf.Length));
        }

        foreach (var p in plan.Positionen)
        {
            if (p.Aenderungen.Count == 0 && p.Sperren.Count == 0 && p.Hinweise.Count == 0) continue;
            sb.AppendLine().AppendLine(Objekt(p.Objektart, p.Bezeichnung));
            foreach (var a in p.Aenderungen) sb.AppendLine($"  {a.Feld}: {Leer(a.Alt)} -> {a.Neu}  ({a.Grund})");
            foreach (var s in p.Sperren) sb.AppendLine($"  NICHT GELESEN: {s}");
            foreach (var h in p.Hinweise) sb.AppendLine($"  Hinweis: {h}");
        }
        foreach (var s in plan.Sanierungen)
        {
            sb.AppendLine().AppendLine($"{Objekt(s.Objektart, s.ElternBezeichnung)} — Sanierungsmassnahme {s.WebGisGlobalId}");
            foreach (var w in s.Werte) sb.AppendLine($"  {w.FeldId}: {w.Text}");
            foreach (var sp in s.Sperren) sb.AppendLine($"  NICHT ÜBERNOMMEN: {sp}");
            foreach (var h in s.Hinweise) sb.AppendLine($"  Hinweis: {h}");
        }
        return sb.ToString();
    }

    /// <summary>Gleich breite Kennung am Zeilenanfang des roten Blocks im Textbericht.</summary>
    private static string Kennung(string art) => (art switch
    {
        ArtObjektGesperrt => "GESPERRT",
        ArtMassnahmeGesperrt => "MASSNAHME",
        _ => "WERT",
    }).PadRight(11);

    private static string Objekt(WebGisObjektart art, string name)
        => (art == WebGisObjektart.Haltung ? "Haltung " : "Schacht ") + name;

    private static string Leer(string? s) => string.IsNullOrWhiteSpace(s) ? XtfExportVorschau.Leer : s;
}
