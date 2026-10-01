using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.UseCases.Xtf;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Macht aus dem WebGIS-Exportplan die Vorschau fuer das bestehende Vorschaufenster
/// (Alt/Neu-Tabelle, Warnungen, Details) und nach dem Schreiben den Ergebnisbericht.
/// Reine Darstellung, kein Netz.
/// </summary>
public static class WebGisExportBericht
{
    public static XtfExportVorschau Vorschau(WebGisExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var zeilen = new List<XtfVorschauZeile>();
        var warnungen = new List<string>();

        foreach (var p in plan.Positionen)
        {
            var objekt = Objekt(p.Objektart, p.Bezeichnung);
            foreach (var a in p.Aenderungen)
                zeilen.Add(new XtfVorschauZeile(objekt, a.Feld, Leer(a.AltText ?? a.Alt), a.NeuText ?? a.Neu));
            foreach (var s in p.Sperren) warnungen.Add($"{objekt}: GESPERRT — {s}");
            foreach (var h in p.Hinweise) warnungen.Add($"{objekt}: {h}");
            if (p.SchreibFehler is not null) warnungen.Add($"{objekt}: FEHLER — {p.SchreibFehler}");
        }
        foreach (var s in plan.Sanierungen)
        {
            var objekt = Objekt(s.Objektart, s.ElternBezeichnung);
            if (s.Schreibbar)
                zeilen.Add(new XtfVorschauZeile(objekt, "Sanierungsmassnahme (neu)", XtfExportVorschau.Leer, string.Join(" · ", s.Anzeige)));
            foreach (var sp in s.Sperren) warnungen.Add($"{objekt}, Sanierungsmassnahme: GESPERRT — {sp}");
            foreach (var h in s.Hinweise) warnungen.Add($"{objekt}, Sanierungsmassnahme: {h}");
            if (s.SchreibFehler is not null) warnungen.Add($"{objekt}, Sanierungsmassnahme: FEHLER — {s.SchreibFehler}");
            if (s.Ungeklaert is not null) warnungen.Add($"{objekt}, Sanierungsmassnahme: UNGEKLÄRT — {s.Ungeklaert}");
        }

        var zusammenfassung =
            $"{plan.Schreibbare} Objekte mit Änderungen, {plan.Gesperrte} gesperrt · "
            + $"{plan.SanierungenSchreibbar} Sanierungsmassnahmen anzulegen, {plan.SanierungenGesperrt} gesperrt. "
            + "Längen werden nie geschrieben. Gesperrte Objekte werden übersprungen.";

        return new XtfExportVorschau(
            "WebGIS-Übertragung prüfen",
            zusammenfassung,
            zeilen,
            warnungen,
            Details(plan, mitErgebnis: false),
            IstFehler: plan.Schreibbare == 0 && plan.SanierungenSchreibbar == 0);
    }

    /// <summary>Kurzer Ergebnistext nach <c>FuehreAusAsync</c> fuer Status/Toast.</summary>
    public static string Ergebnis(WebGisExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var objOk = plan.Positionen.Count(p => p.Ausgang is WebGisSchreibAusgang.Geschrieben
            or WebGisSchreibAusgang.VomServerBestaetigt or WebGisSchreibAusgang.Nachgeprueft);
        var objFehler = plan.Positionen.Count(p => p.Ausgang == WebGisSchreibAusgang.Fehler);
        // WG05: «angelegt» zaehlt nur nachgepruefte Massnahmen; bestaetigt ohne Gegenprobe und ungeklaert getrennt.
        var sanOk = plan.Sanierungen.Count(s => s.Ausgang == WebGisSchreibAusgang.Nachgeprueft);
        var sanBestaetigt = plan.Sanierungen.Count(s => s.Ausgang == WebGisSchreibAusgang.VomServerBestaetigt);
        var sanUngeklaert = plan.Sanierungen.Count(s => s.Ausgang == WebGisSchreibAusgang.Ungeklaert);
        var sanFehler = plan.Sanierungen.Count(s => s.Ausgang == WebGisSchreibAusgang.Fehler);
        return $"WebGIS: {objOk} Objekte geschrieben, {sanOk} Sanierungsmassnahmen angelegt und nachgeprüft"
             + (sanBestaetigt > 0 ? $", {sanBestaetigt} vom Server bestätigt (nicht nachgeprüft)" : "")
             + (sanUngeklaert > 0 ? $", {sanUngeklaert} mit ungeklärtem Ausgang (im WebGIS nachsehen, nicht erneut anlegen)" : "")
             + (objFehler + sanFehler > 0 ? $", {objFehler + sanFehler} fehlgeschlagen (siehe Bericht)." : ".");
    }

    /// <summary>Vollstaendiger Bericht (Vorschau oder Ergebnis) als Text fuer die Projektablage.</summary>
    public static string Details(WebGisExportPlan plan, bool mitErgebnis)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var sb = new StringBuilder();
        sb.AppendLine(mitErgebnis ? "WEBGIS-ÜBERTRAGUNG — ERGEBNIS" : "WEBGIS-ÜBERTRAGUNG — VORSCHAU (nichts geschrieben)");
        sb.AppendLine(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
        foreach (var h in plan.Hinweise) sb.AppendLine(h);
        sb.AppendLine();

        foreach (var p in plan.Positionen)
        {
            var status = p.Sperren.Count > 0 ? "GESPERRT"
                : mitErgebnis ? p.Ausgang switch
                {
                    WebGisSchreibAusgang.Fehler => "FEHLER",
                    WebGisSchreibAusgang.Offen => p.Aenderungen.Count == 0 ? "UNVERÄNDERT" : "OFFEN",
                    _ => "GESCHRIEBEN",
                }
                : p.Aenderungen.Count == 0 ? "UNVERÄNDERT" : "ÄNDERN";
            sb.AppendLine($"[{status}] {Objekt(p.Objektart, p.Bezeichnung)}" + (p.GlobalId is null ? "" : $"  (GlobalID {p.GlobalId})"));
            foreach (var a in p.Aenderungen) sb.AppendLine($"    {a.Feld}: {Leer(a.AltText ?? a.Alt)} → {a.NeuText ?? a.Neu}");
            // Nur angehakte Vorschlaege werden geschrieben (sie stehen dann auch oben als Aenderung).
            foreach (var v in p.Vorschlaege)
                sb.AppendLine($"    [{(v.Gewaehlt ? "x" : " ")}] Kanalfirma weicht ab — {v.Anzeige}: {Leer(v.AltText)} → {v.NeuText}");
            foreach (var s in p.Sperren) sb.AppendLine($"    !! {s}");
            foreach (var h in p.Hinweise) sb.AppendLine($"    ({h})");
            if (p.SchreibFehler is not null) sb.AppendLine($"    !! {p.SchreibFehler}");
        }

        if (plan.Sanierungen.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("SANIERUNGSMASSNAHMEN");
            foreach (var s in plan.Sanierungen)
            {
                var status = s.Sperren.Count > 0 ? "GESPERRT"
                    : mitErgebnis ? s.Ausgang switch
                    {
                        WebGisSchreibAusgang.Ungeklaert => $"UNGEKLÄRT (vom Server bestätigt, ID {s.NeueId ?? "?"})",
                        WebGisSchreibAusgang.Nachgeprueft => $"ANGELEGT UND NACHGEPRÜFT (ID {s.NeueId ?? "?"}, GlobalID {s.NeueGlobalId ?? "?"})",
                        WebGisSchreibAusgang.VomServerBestaetigt => $"ANGELEGT, VOM SERVER BESTÄTIGT (ID {s.NeueId ?? "?"}, nicht nachgeprüft)",
                        WebGisSchreibAusgang.Fehler => "FEHLER",
                        _ => "OFFEN",
                    }
                    : "ANLEGEN";
                sb.AppendLine($"[{status}] {Objekt(s.Objektart, s.ElternBezeichnung)}  (Akte {s.AkteId.ToString("N")[..8]})");
                foreach (var z in s.Anzeige) sb.AppendLine("    " + z);
                foreach (var sp in s.Sperren) sb.AppendLine($"    !! {sp}");
                foreach (var h in s.Hinweise) sb.AppendLine($"    ({h})");
                if (s.SchreibFehler is not null) sb.AppendLine($"    !! {s.SchreibFehler}");
                if (s.Ungeklaert is not null) sb.AppendLine($"    !! {s.Ungeklaert}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Aenderungslog nach dem Schreiben: eine Zeile je geschriebenem Feld bzw. je angelegter
    /// Massnahme mit Zeit, Objekt, Feld, alt -&gt; neu und Ergebnis. Zum Anhaengen an ein
    /// fortlaufendes Logfile in der Projektablage.
    /// </summary>
    public static string Log(WebGisExportPlan plan, DateTime zeitpunkt, string benutzer)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var sb = new StringBuilder();
        sb.AppendLine($"===== {Z(zeitpunkt)} | WebGIS-Übertragung | {benutzer} | {Ergebnis(plan)}");
        foreach (var p in plan.Positionen)
        {
            var zeile = LogZeile(p, zeitpunkt);
            if (zeile.Length > 0) sb.AppendLine(zeile);
        }
        foreach (var s in plan.Sanierungen)
        {
            var zeile = LogZeile(s, zeitpunkt);
            if (zeile.Length > 0) sb.AppendLine(zeile);
        }
        return sb.ToString();
    }

    // ---- Log je Schritt: Der Ablauf ruft diese Bausteine SOFORT nach jedem Schreibversuch. ----
    // Nach einem Abbruch (Sitzung, Netz, Absturz) steht so im Log, welche Objekte schon
    // geschrieben sind; vorher entstand das Log erst am Ende und bei einem Abbruch gar nicht.

    /// <summary>Kopfzeile VOR dem ersten Schreiben.</summary>
    public static string LogStart(DateTime zeit, string benutzer, WebGisExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return $"===== {Z(zeit)} | WebGIS-Übertragung | {benutzer} | gestartet: {plan.Schreibbare} Objekte, {plan.SanierungenSchreibbar} Sanierungsmassnahmen";
    }

    /// <summary>Logzeile(n) eines Objekts nach seinem Schreibversuch; leer, wenn nichts zu melden ist.</summary>
    public static string LogZeile(WebGisExportPosition p, DateTime zeit)
    {
        ArgumentNullException.ThrowIfNull(p);
        var z = Z(zeit);
        var objekt = Objekt(p.Objektart, p.Bezeichnung);
        return p.Ausgang switch
        {
            WebGisSchreibAusgang.Fehler =>
                $"{z} | {objekt} | {string.Join(", ", p.Aenderungen.ConvertAll(a => a.Feld))} | nicht geschrieben | FEHLER: {p.SchreibFehler}",
            WebGisSchreibAusgang.Offen => p.Sperren.Count > 0
                ? $"{z} | {objekt} | – | übersprungen | GESPERRT: {string.Join("; ", p.Sperren)}"
                : string.Empty,
            _ => string.Join(Environment.NewLine,
                p.Aenderungen.ConvertAll(a => $"{z} | {objekt} | {a.Feld} | {AltLog(a)} → {NeuLog(a)} | OK")),
        };
    }

    /// <summary>Logzeile einer Sanierungsmassnahme nach ihrem Anlegeversuch; leer, wenn nichts zu melden ist.</summary>
    public static string LogZeile(WebGisSanierungPosition s, DateTime zeit)
    {
        ArgumentNullException.ThrowIfNull(s);
        var z = Z(zeit);
        var objekt = Objekt(s.Objektart, s.ElternBezeichnung);
        var werte = string.Join(", ", s.Anzeige.ConvertAll(a => a.Split(" (")[0]));
        return s.Ausgang switch
        {
            WebGisSchreibAusgang.Ungeklaert =>
                $"{z} | {objekt} | Sanierungsmassnahme vom Server bestätigt (ID {s.NeueId ?? "?"}) | – → {werte} | UNGEKLÄRT: {s.Ungeklaert}",
            WebGisSchreibAusgang.Nachgeprueft =>
                $"{z} | {objekt} | Sanierungsmassnahme angelegt (ID {s.NeueId ?? "?"}) | – → {werte} | OK, nachgeprüft (GlobalID {s.NeueGlobalId ?? "?"})",
            WebGisSchreibAusgang.VomServerBestaetigt =>
                $"{z} | {objekt} | Sanierungsmassnahme angelegt (ID {s.NeueId ?? "?"}) | – → {werte} | BESTÄTIGT, nicht nachgeprüft",
            WebGisSchreibAusgang.Fehler =>
                $"{z} | {objekt} | Sanierungsmassnahme | nicht angelegt ({werte}) | FEHLER: {s.SchreibFehler}",
            _ => s.Sperren.Count > 0
                ? $"{z} | {objekt} | Sanierungsmassnahme | übersprungen | GESPERRT: {string.Join("; ", s.Sperren)}"
                : string.Empty,
        };
    }

    /// <summary>Zeile VOR dem Schreibversuch eines Objekts: steht sie nicht im Log, wird nicht gesendet.</summary>
    public static string LogGeplant(WebGisExportPosition p, DateTime zeit)
    {
        ArgumentNullException.ThrowIfNull(p);
        return $"{Z(zeit)} | {Objekt(p.Objektart, p.Bezeichnung)} | {string.Join(", ", p.Aenderungen.ConvertAll(a => a.Feld))} | wird gesendet | GEPLANT";
    }

    /// <summary>Zeile VOR dem Anlegeversuch einer Massnahme.</summary>
    public static string LogGeplant(WebGisSanierungPosition s, DateTime zeit)
    {
        ArgumentNullException.ThrowIfNull(s);
        var werte = string.Join(", ", s.Anzeige.ConvertAll(a => a.Split(" (")[0]));
        return $"{Z(zeit)} | {Objekt(s.Objektart, s.ElternBezeichnung)} | Sanierungsmassnahme | wird angelegt ({werte}) | GEPLANT";
    }

    /// <summary>Schlusszeile nach vollstaendigem Lauf.</summary>
    public static string LogAbschluss(DateTime zeit, WebGisExportPlan plan)
        => $"===== {Z(zeit)} | abgeschlossen | {Ergebnis(plan)}";

    /// <summary>Schlusszeile nach Abbruch — mit dem Stand bis dahin.</summary>
    public static string LogAbbruch(DateTime zeit, string grund, WebGisExportPlan plan)
        => $"===== {Z(zeit)} | ABGEBROCHEN | {grund} | bis dahin: {Ergebnis(plan)}";

    private static string Z(DateTime zeit) => zeit.ToString("dd.MM.yyyy HH:mm:ss");

    private static string AltLog(WebGisFeldAenderung a)
        => Leer(a.Alt) + (a.AltText is not null && a.AltText != a.Alt ? " (" + a.AltText + ")" : "");

    private static string NeuLog(WebGisFeldAenderung a)
        => a.Neu + (a.NeuText is not null && a.NeuText != a.Neu ? " (" + a.NeuText + ")" : "");

    private static string Objekt(WebGisObjektart art, string bezeichnung)
        => (art == WebGisObjektart.Haltung ? "Haltung " : "Schacht ") + bezeichnung;

    private static string Leer(string? s) => string.IsNullOrWhiteSpace(s) ? XtfExportVorschau.Leer : s;
}
