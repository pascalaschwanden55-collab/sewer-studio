using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.UseCases.Xtf;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Eine Zeile der Holen-Vorschau mit Bezug zum Objekt (Doppelklick oeffnet es).</summary>
public sealed record WebGisHolenZeile(string Objekt, string Feld, string Alt, string Neu, WebGisObjektart Objektart, Guid RecordId);

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

    /// <summary>Alle Hinweise und Sperren als Zeilen (fuer die volle Liste im Fenster).</summary>
    public static IReadOnlyList<WebGisHolenZeile> Hinweise(WebGisImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var zeilen = new List<WebGisHolenZeile>();
        foreach (var p in plan.Positionen)
        {
            foreach (var s in p.Sperren) zeilen.Add(new WebGisHolenZeile(Objekt(p.Objektart, p.Bezeichnung), "nicht gelesen", "", s, p.Objektart, p.RecordId));
            foreach (var h in p.Hinweise) zeilen.Add(new WebGisHolenZeile(Objekt(p.Objektart, p.Bezeichnung), "Hinweis", "", h, p.Objektart, p.RecordId));
        }
        foreach (var s in plan.Sanierungen)
        {
            foreach (var sp in s.Sperren) zeilen.Add(new WebGisHolenZeile(Objekt(s.Objektart, s.ElternBezeichnung), "Sanierungsmassnahme", "", sp, s.Objektart, s.ElternRecordId));
            foreach (var h in s.Hinweise) zeilen.Add(new WebGisHolenZeile(Objekt(s.Objektart, s.ElternBezeichnung), "Sanierungsmassnahme", "", h, s.Objektart, s.ElternRecordId));
        }
        return zeilen;
    }

    /// <summary>Lesbarer Name fuer interne Feldschluessel (Akte).</summary>
    private static string Anzeigename(string feld) => feld switch
    {
        WebGisImportPlanBuilder.FeldWebGisGlobalId => "GlobalID (WebGIS)",
        "haltung.pipegroup" or "schacht.materialgruppe" => "Materialgruppe",
        "Haltungslaenge_m" => "Haltungslänge [m]",
        _ => feld,
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

    private static string Objekt(WebGisObjektart art, string name)
        => (art == WebGisObjektart.Haltung ? "Haltung " : "Schacht ") + name;

    private static string Leer(string? s) => string.IsNullOrWhiteSpace(s) ? XtfExportVorschau.Leer : s;
}
