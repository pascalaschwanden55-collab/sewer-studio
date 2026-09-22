using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Was eine Zeile der Uebersicht aussagt — bestimmt Farbe und Symbol in der Anzeige.</summary>
public enum WebGisZeilenart
{
    /// <summary>Ein Feld wird geaendert (alt -&gt; neu).</summary>
    Aenderung,
    /// <summary>Eine Sanierungsmassnahme wird neu angelegt.</summary>
    NeueMassnahme,
    /// <summary>Etwas, das der Bearbeiter wissen soll; blockiert nicht.</summary>
    Hinweis,
    /// <summary>Hier geschieht nichts, bis der Grund behoben ist.</summary>
    Sperre,
    /// <summary>Bereits geschrieben (Ergebnisansicht).</summary>
    Erledigt,
    /// <summary>Beim Schreiben fehlgeschlagen (Ergebnisansicht).</summary>
    Fehler,
}

/// <summary>Eine Zeile unter einem Objekt: Feld, was bisher dort stand, was hinkommt.</summary>
public sealed record WebGisUebersichtZeile(WebGisZeilenart Art, string Feld, string? Alt, string Neu)
{
    public bool IstAenderung => Art is WebGisZeilenart.Aenderung or WebGisZeilenart.NeueMassnahme or WebGisZeilenart.Erledigt;
    public bool IstHinweis => Art == WebGisZeilenart.Hinweis;
    public bool IstSperre => Art is WebGisZeilenart.Sperre or WebGisZeilenart.Fehler;
    public bool HatAlt => !string.IsNullOrWhiteSpace(Alt);
}

/// <summary>Ein Objekt (Haltung oder Schacht) mit allem, was daran geschieht.</summary>
public sealed class WebGisUebersichtObjekt
{
    public required string Objekt { get; init; }
    /// <summary>Haltung oder Schacht — fuer den Sprung in die Bearbeitung.</summary>
    public WebGisObjektart Objektart { get; init; }
    /// <summary>Datensatz-Id im Projekt; <see cref="Guid.Empty"/>, wenn nicht bekannt.</summary>
    public Guid RecordId { get; init; }
    /// <summary>True, wenn die Karte einen Datensatz zum Oeffnen kennt.</summary>
    public bool KannOeffnen => RecordId != Guid.Empty;
    public List<WebGisUebersichtZeile> Zeilen { get; } = new();

    public int Aenderungen => Zeilen.Count(z => z.IstAenderung);
    public bool HatSperre => Zeilen.Exists(z => z.IstSperre);
    public bool NurHinweise => Aenderungen == 0 && !HatSperre;

    /// <summary>Eine Zeile Klartext fuer den Kopf der Karte.</summary>
    public string Kurz => HatSperre
        ? "gesperrt"
        : Aenderungen switch { 0 => "nur Hinweis", 1 => "1 Änderung", var n => $"{n} Änderungen" };
}

/// <summary>
/// Die Uebersicht fuer das Pruef-/Ergebnisfenster: nach Objekt gruppiert statt zwei lange
/// Listen, und gleichartige Meldungen zu einer Sammelzeile zusammengefasst. Reine Darstellung
/// aus dem Plan — kein Netz, keine Entscheidungen.
/// </summary>
public sealed class WebGisUebersicht
{
    public required string Titel { get; init; }
    public required string Kopfzeile { get; init; }
    public List<WebGisUebersichtObjekt> Objekte { get; } = new();
    /// <summary>Gleichartiges gebuendelt, z.B. "43 Sanierungsmassnahmen sind bereits vorhanden".</summary>
    public List<string> Sammelmeldungen { get; } = new();
    public required string Bericht { get; init; }

    public int ObjekteMitAenderung { get; private set; }
    /// <summary>Nur in der Ergebnisansicht: Objekte, die wirklich geschrieben wurden.</summary>
    public int Geschrieben { get; private set; }
    public bool IstErgebnis { get; private set; }
    public int NeueMassnahmen { get; private set; }
    public int Gesperrt { get; private set; }
    public int MitHinweis { get; private set; }
    public bool NichtsZuTun => ObjekteMitAenderung == 0 && NeueMassnahmen == 0;
    public bool HatSammelmeldungen => Sammelmeldungen.Count > 0;

    /// <summary>Baut die Uebersicht aus dem Plan. <paramref name="ergebnis"/>: nach dem Schreiben.</summary>
    public static WebGisUebersicht Aus(WebGisExportPlan plan, bool ergebnis = false)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var u = new WebGisUebersicht
        {
            Titel = ergebnis ? "WebGIS-Übertragung — Ergebnis" : "WebGIS-Übertragung prüfen",
            Kopfzeile = "",
            Bericht = WebGisExportBericht.Details(plan, ergebnis),
        };

        var nachObjekt = new Dictionary<string, WebGisUebersichtObjekt>(StringComparer.Ordinal);
        WebGisUebersichtObjekt Hole(WebGisObjektart art, string bezeichnung, Guid recordId = default)
        {
            var name = (art == WebGisObjektart.Haltung ? "Haltung " : "Schacht ") + bezeichnung;
            if (!nachObjekt.TryGetValue(name, out var o))
            {
                o = new WebGisUebersichtObjekt { Objekt = name, Objektart = art, RecordId = recordId };
                nachObjekt[name] = o;
                u.Objekte.Add(o);
            }
            return o;
        }

        foreach (var p in plan.Positionen)
        {
            if (p.Aenderungen.Count == 0 && p.Sperren.Count == 0 && p.Hinweise.Count == 0 && p.SchreibFehler is null)
                continue; // unveraendert und ohne Meldung -> gar nicht zeigen

            var o = Hole(p.Objektart, p.Bezeichnung, p.RecordId);
            var art = ergebnis && p.Geschrieben ? WebGisZeilenart.Erledigt : WebGisZeilenart.Aenderung;
            foreach (var a in p.Aenderungen)
                o.Zeilen.Add(new WebGisUebersichtZeile(art, a.Feld, a.AltText ?? a.Alt, a.NeuText ?? a.Neu));
            foreach (var s in p.Sperren)
                o.Zeilen.Add(new WebGisUebersichtZeile(WebGisZeilenart.Sperre, "Gesperrt", null, s));
            foreach (var h in p.Hinweise)
                o.Zeilen.Add(new WebGisUebersichtZeile(WebGisZeilenart.Hinweis, "Hinweis", null, h));
            if (p.SchreibFehler is not null)
                o.Zeilen.Add(new WebGisUebersichtZeile(WebGisZeilenart.Fehler, "Fehler", null, p.SchreibFehler));
        }

        var schonVorhanden = 0;
        foreach (var s in plan.Sanierungen)
        {
            // Der haeufigste Fall braucht keine eigene Karte: die Massnahme steht schon im WebGIS.
            if (s.Sperren.Count == 1 && s.Sperren[0].Contains("bereits vorhanden", StringComparison.OrdinalIgnoreCase))
            {
                schonVorhanden++;
                continue;
            }
            if (!s.Schreibbar && s.Sperren.Count == 0 && s.Hinweise.Count == 0 && s.SchreibFehler is null) continue;

            var o = Hole(s.Objektart, s.ElternBezeichnung, s.ElternRecordId);
            if (s.Schreibbar || s.Geschrieben)
            {
                var art = ergebnis && s.Geschrieben ? WebGisZeilenart.Erledigt : WebGisZeilenart.NeueMassnahme;
                o.Zeilen.Add(new WebGisUebersichtZeile(art, "Sanierungsmassnahme", null, string.Join(" · ", s.Anzeige)));
            }
            foreach (var sp in s.Sperren)
                o.Zeilen.Add(new WebGisUebersichtZeile(WebGisZeilenart.Sperre, "Sanierungsmassnahme", null, sp));
            foreach (var h in s.Hinweise)
                o.Zeilen.Add(new WebGisUebersichtZeile(WebGisZeilenart.Hinweis, "Sanierungsmassnahme", null, h));
            if (s.SchreibFehler is not null)
                o.Zeilen.Add(new WebGisUebersichtZeile(WebGisZeilenart.Fehler, "Sanierungsmassnahme", null, s.SchreibFehler));
        }

        // Karten sortieren: erst was geschrieben wird, dann Sperren, dann reine Hinweise.
        u.Objekte.Sort((a, b) =>
        {
            int Rang(WebGisUebersichtObjekt o) => o.Aenderungen > 0 ? 0 : o.HatSperre ? 1 : 2;
            var r = Rang(a).CompareTo(Rang(b));
            return r != 0 ? r : string.Compare(a.Objekt, b.Objekt, StringComparison.Ordinal);
        });

        u.ObjekteMitAenderung = u.Objekte.Count(o => o.Aenderungen > 0);
        u.IstErgebnis = ergebnis;
        u.Geschrieben = plan.Positionen.Count(p => p.Geschrieben);
        u.NeueMassnahmen = plan.Sanierungen.Count(s => ergebnis ? s.Geschrieben : s.Schreibbar);
        u.Gesperrt = u.Objekte.Count(o => o.HatSperre);
        u.MitHinweis = u.Objekte.Count(o => o.NurHinweise);

        if (schonVorhanden > 0)
            u.Sammelmeldungen.Add($"{schonVorhanden} Sanierungsmassnahmen sind im WebGIS bereits vorhanden — nicht doppelt angelegt.");
        var ohneAkte = plan.Positionen.Count(p => p.Hinweise.Exists(h => h.Contains("keine ausgefuehrte Sanierungs-Akte", StringComparison.OrdinalIgnoreCase)));
        if (ohneAkte > 0)
            u.Sammelmeldungen.Add($"{ohneAkte} Objekte melden «saniert» in der Bemerkung, haben aber keine Sanierungs-Akte — dort wird kein Sanierungsbedarf gesetzt.");

        return u.With(Kopf(u, ergebnis));
    }

    private static string Kopf(WebGisUebersicht u, bool ergebnis)
    {
        var teile = new List<string>();
        if (ergebnis)
        {
            // Nur wirklich Geschriebenes zaehlen — ein fehlgeschlagenes Objekt traegt seine
            // Aenderungszeilen weiterhin (21.09.2026 15:57: «13 geschrieben», es waren 5).
            teile.Add($"{u.Geschrieben} Objekte geschrieben");
            teile.Add($"{u.NeueMassnahmen} Massnahmen angelegt");
            if (u.Gesperrt > 0) teile.Add($"{u.Gesperrt} nicht geschrieben");
        }
        else
        {
            teile.Add(u.ObjekteMitAenderung == 1 ? "1 Objekt wird geändert" : $"{u.ObjekteMitAenderung} Objekte werden geändert");
            teile.Add(u.NeueMassnahmen == 1 ? "1 Massnahme wird angelegt" : $"{u.NeueMassnahmen} Massnahmen werden angelegt");
            if (u.Gesperrt > 0) teile.Add($"{u.Gesperrt} gesperrt");
            if (u.MitHinweis > 0) teile.Add($"{u.MitHinweis} nur mit Hinweis");
        }
        return string.Join(" · ", teile);
    }

    /// <summary>Kopie mit anderer Kopfzeile (Init-Eigenschaften bleiben sonst unveraendert).</summary>
    private WebGisUebersicht With(string kopfzeile)
    {
        var neu = new WebGisUebersicht { Titel = Titel, Kopfzeile = kopfzeile, Bericht = Bericht };
        neu.Objekte.AddRange(Objekte);
        neu.Sammelmeldungen.AddRange(Sammelmeldungen);
        neu.ObjekteMitAenderung = ObjekteMitAenderung;
        neu.Geschrieben = Geschrieben;
        neu.IstErgebnis = IstErgebnis;
        neu.NeueMassnahmen = NeueMassnahmen;
        neu.Gesperrt = Gesperrt;
        neu.MitHinweis = MitHinweis;
        return neu;
    }
}
