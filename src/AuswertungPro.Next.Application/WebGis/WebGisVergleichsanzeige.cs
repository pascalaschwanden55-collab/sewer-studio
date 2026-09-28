using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Farbton einer Anzeige (das Fenster übersetzt ihn in Farben).</summary>
public enum WebGisAnzeigeTon { Aenderung, Gleich, Vorschlag, Warnung, Gesperrt, Bestaetigt, Fehler }

/// <summary>Eine Zeile der Vergleichstabelle: Feld | SewerStudio | WebGIS jetzt | Was passiert.</summary>
public sealed class WebGisVergleichsZeile : INotifyPropertyChanged
{
    private readonly WebGisFeldVergleich _vergleich;
    private readonly Action? _geaendert;

    internal WebGisVergleichsZeile(WebGisFeldVergleich vergleich, WebGisExportPosition pos, Action? geaendert)
    {
        _vergleich = vergleich;
        _geaendert = geaendert;
        Feld = vergleich.Feld;
        SewerStudio = vergleich.SewerStudio;
        (WebGis, Aktion, Grund, Ton) = Einordnen(vergleich, pos);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Feld { get; }
    public string SewerStudio { get; }
    public string WebGis { get; }
    public string Aktion { get; private set; }
    public string Grund { get; }
    public WebGisAnzeigeTon Ton { get; private set; }
    public bool IstUnterschied => _vergleich.IstUnterschied;
    public bool WirdGeschrieben => _vergleich.WirdGeschrieben;
    /// <summary>Hellblau hinterlegt: diese Zeile ändert das WebGIS beim nächsten Schreiben.</summary>
    public bool Hervorheben => Ton == WebGisAnzeigeTon.Aenderung || (IstVorschlag && Mitschreiben);
    /// <summary>Haken «mitschreiben» (nur bei einem Vorschlag der Kanalfirma, vor dem Schreiben).</summary>
    public bool IstVorschlag => _vergleich.Vorschlag is not null && Ton == WebGisAnzeigeTon.Vorschlag;

    public bool Mitschreiben
    {
        get => _vergleich.Vorschlag?.Gewaehlt ?? false;
        set
        {
            if (_vergleich.Vorschlag is null || _vergleich.Vorschlag.Gewaehlt == value) return;
            _vergleich.Vorschlag.Gewaehlt = value;
            Aktion = value ? "wird geändert (angehakt)" : "Vorschlag – nicht angehakt";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mitschreiben)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Aktion)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hervorheben)));
            _geaendert?.Invoke();
        }
    }

    private static (string WebGis, string Aktion, string Grund, WebGisAnzeigeTon Ton) Einordnen(WebGisFeldVergleich v, WebGisExportPosition pos)
    {
        // Nach dem Schreiben: was der Server bestätigt hat, zeigt den neuen Wert.
        if (v.Massnahme is { } san)
        {
            // WG05: bestätigt / nachgeprüft / ungeklärt getrennt zeigen.
            if (san.Ungeklaert is not null)
                return (v.Nachher ?? v.WebGis, "Ausgang ungeklärt", san.Ungeklaert, WebGisAnzeigeTon.Warnung);
            if (san.Geschrieben && san.Nachgeprueft)
                return (v.Nachher ?? v.WebGis, "angelegt und nachgeprüft", "zurückgelesen (ID " + (san.NeueId ?? "?") + ")", WebGisAnzeigeTon.Bestaetigt);
            if (san.Geschrieben)
                return (v.Nachher ?? v.WebGis, "angelegt", "vom Server bestätigt (ID " + (san.NeueId ?? "?") + "), nicht nachgeprüft", WebGisAnzeigeTon.Warnung);
            if (san.SchreibFehler is not null)
                return (v.WebGis, "nicht angelegt", san.SchreibFehler, WebGisAnzeigeTon.Fehler);
        }
        else if (v.WirdGeschrieben || (v.Art == WebGisVergleichsArt.Vorschlag && pos.Geschrieben && (v.Vorschlag?.Gewaehlt ?? false)))
        {
            if (pos.Geschrieben && pos.VomServerBestaetigt)
                return (v.Nachher ?? v.SewerStudio, "im WebGIS bestätigt", "nach dem Schreiben zurückgelesen", WebGisAnzeigeTon.Bestaetigt);
            if (pos.Geschrieben)
                return (v.Nachher ?? v.SewerStudio, "geschrieben", "Kontrolle durch Zurücklesen offen", WebGisAnzeigeTon.Warnung);
            if (pos.SchreibFehler is not null)
                return (v.WebGis, "nicht geschrieben", pos.SchreibFehler, WebGisAnzeigeTon.Fehler);
        }

        var grund = v.Grund;
        if (v.Nachher is not null && v.Art is WebGisVergleichsArt.Aendern or WebGisVergleichsArt.Ergaenzen or WebGisVergleichsArt.Eintragen
            && string.IsNullOrWhiteSpace(grund))
            grund = "neu: " + v.Nachher;
        else if (v.Nachher is not null && v.Art is WebGisVergleichsArt.Ergaenzen)
            grund = "danach: " + v.Nachher;

        return v.Art switch
        {
            WebGisVergleichsArt.Aendern => (v.WebGis, "wird geändert", grund, WebGisAnzeigeTon.Aenderung),
            WebGisVergleichsArt.Ergaenzen => (v.WebGis, "wird ergänzt", grund, WebGisAnzeigeTon.Aenderung),
            WebGisVergleichsArt.Eintragen => (v.WebGis, "wird eingetragen", grund, WebGisAnzeigeTon.Aenderung),
            WebGisVergleichsArt.Anlegen => (v.WebGis, "wird angelegt", grund, WebGisAnzeigeTon.Aenderung),
            WebGisVergleichsArt.Gleich => (v.WebGis, "gleich", grund, WebGisAnzeigeTon.Gleich),
            WebGisVergleichsArt.BleibtImWebGis => (v.WebGis, "bleibt im WebGIS", grund, WebGisAnzeigeTon.Gleich),
            WebGisVergleichsArt.Vorschlag => (v.WebGis,
                (v.Vorschlag?.Gewaehlt ?? false) ? "wird geändert (angehakt)" : "Vorschlag – nicht angehakt", grund, WebGisAnzeigeTon.Vorschlag),
            WebGisVergleichsArt.NichtUebertragbar => (v.WebGis, "nicht übertragbar", grund, WebGisAnzeigeTon.Warnung),
            _ => (v.WebGis, "wird nicht gesendet", grund, WebGisAnzeigeTon.Gleich),
        };
    }
}

/// <summary>Ein Eintrag der Objektliste links: Haltung oder Schacht mit Abzeichen.</summary>
public sealed class WebGisVergleichsObjekt : INotifyPropertyChanged
{
    private readonly WebGisExportPosition _pos;

    internal WebGisVergleichsObjekt(WebGisExportPosition pos, IReadOnlyList<WebGisSanierungPosition> massnahmen)
    {
        _pos = pos;
        Objektart = pos.Objektart;
        RecordId = pos.RecordId;
        Name = pos.Bezeichnung;
        ArtText = pos.Objektart == WebGisObjektart.Haltung ? "Haltung" : "Schacht";
        Massnahmen = massnahmen;
        Zeilen = pos.Vergleich.Select(v => new WebGisVergleichsZeile(v, pos, Neuzaehlen)).ToList();
        Neuzaehlen();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WebGisObjektart Objektart { get; }
    public Guid RecordId { get; }
    public string Name { get; }
    public string ArtText { get; }
    public IReadOnlyList<WebGisVergleichsZeile> Zeilen { get; }
    public IReadOnlyList<WebGisSanierungPosition> Massnahmen { get; }
    public string Chip { get; private set; } = string.Empty;
    public WebGisAnzeigeTon ChipTon { get; private set; }
    /// <summary>Gleich wie <see cref="ChipTon"/> — damit Liste und Zeilen dieselbe Abzeichen-Vorlage nutzen.</summary>
    public WebGisAnzeigeTon Ton => ChipTon;
    public string Untertitel { get; private set; } = string.Empty;
    public int AnzahlAenderungen { get; private set; }
    public bool Gesperrt => _pos.Sperren.Count > 0;
    /// <summary>Gibt es etwas zu schreiben (Felder oder neue Massnahme) und ist das Objekt frei?</summary>
    public bool Schreibbar => !Gesperrt && AnzahlAenderungen > 0 && !IstGeschrieben;
    public bool IstGeschrieben => _pos.Geschrieben || (Massnahmen.Count > 0 && Massnahmen.All(m => m.Geschrieben || !m.Schreibbar) && Massnahmen.Any(m => m.Geschrieben));

    private void Neuzaehlen()
    {
        AnzahlAenderungen = Zeilen.Count(z => z.WirdGeschrieben);
        if (Gesperrt)
        {
            Chip = "gesperrt"; ChipTon = WebGisAnzeigeTon.Gesperrt;
            Untertitel = string.Join(" ", _pos.Sperren);
        }
        else if (Massnahmen.Any(m => m.Ungeklaert is not null))
        {
            Chip = "ungeklärt"; ChipTon = WebGisAnzeigeTon.Warnung;
            Untertitel = Massnahmen.First(m => m.Ungeklaert is not null).Ungeklaert!;
        }
        else if (_pos.SchreibFehler is not null || Massnahmen.Any(m => m.SchreibFehler is not null))
        {
            Chip = "Fehler"; ChipTon = WebGisAnzeigeTon.Fehler;
            Untertitel = _pos.SchreibFehler ?? Massnahmen.First(m => m.SchreibFehler is not null).SchreibFehler!;
        }
        else if (IstGeschrieben)
        {
            Chip = "geschrieben"; ChipTon = WebGisAnzeigeTon.Bestaetigt;
            Untertitel = _pos.VomServerBestaetigt || !_pos.Geschrieben
                ? "Geschrieben – vom WebGIS bestätigt."
                : "Geschrieben – die Kontrolle durch Zurücklesen steht noch aus.";
        }
        else
        {
            Chip = AnzahlAenderungen == 0 ? "keine Änderung" : AnzahlAenderungen + (AnzahlAenderungen == 1 ? " Änderung" : " Änderungen");
            ChipTon = AnzahlAenderungen == 0 ? WebGisAnzeigeTon.Gleich : WebGisAnzeigeTon.Aenderung;
            Untertitel = AnzahlAenderungen == 0
                ? "Nichts zu schreiben."
                : AnzahlAenderungen + (AnzahlAenderungen == 1 ? " Änderung geplant." : " Änderungen geplant.");
        }
        foreach (var name in new[] { nameof(AnzahlAenderungen), nameof(Chip), nameof(ChipTon), nameof(Ton), nameof(Untertitel), nameof(Schreibbar) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Eine Zeile im Bestätigungsfenster: genau das, was geschrieben wird.</summary>
public sealed record WebGisSchreibZeile(
    string Objekt, string Feld, string Vorher, string Nachher, string Ergebnis, WebGisAnzeigeTon Ton);

/// <summary>Baut die Anzeige der Vergleichsliste aus einem Plan. Reine Darstellung, entscheidet nichts.</summary>
public static class WebGisVergleichsanzeige
{
    public static List<WebGisVergleichsObjekt> Objekte(WebGisExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Positionen
            .Select(p => new WebGisVergleichsObjekt(p,
                plan.Sanierungen.FindAll(s => s.Objektart == p.Objektart && s.ElternRecordId == p.RecordId)))
            .ToList();
    }

    /// <summary>
    /// Genau die Felder und Massnahmen, die ein Schreiblauf mit diesem Plan ins WebGIS bringt — für das
    /// Bestätigungsfenster «vorher → nachher». Gesperrte Objekte fehlen, weil sie nicht geschrieben werden.
    /// </summary>
    /// <param name="nachDemSchreiben">false: Ergebnis «geplant»; true: was der Server bestätigt hat.</param>
    public static List<WebGisSchreibZeile> Schreibliste(WebGisExportPlan plan, bool nachDemSchreiben = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var liste = new List<WebGisSchreibZeile>();
        foreach (var pos in plan.Positionen)
        {
            if (pos.Sperren.Count > 0) continue;
            var objekt = (pos.Objektart == WebGisObjektart.Haltung ? "Haltung " : "Schacht ") + pos.Bezeichnung;
            foreach (var v in pos.Vergleich.Where(v => v.WirdGeschrieben))
            {
                if (v.Massnahme is { } san && !san.Schreibbar && !san.Geschrieben) continue;
                var (ergebnis, ton) = nachDemSchreiben ? Ergebnis(pos, v) : ("geplant", WebGisAnzeigeTon.Aenderung);
                liste.Add(new WebGisSchreibZeile(objekt, v.Feld, v.WebGis, v.Nachher ?? v.SewerStudio, ergebnis, ton));
            }
        }
        return liste;
    }

    private static (string Text, WebGisAnzeigeTon Ton) Ergebnis(WebGisExportPosition pos, WebGisFeldVergleich v)
    {
        if (v.Massnahme is { } san)
        {
            if (san.Ungeklaert is not null) return ("Ausgang ungeklärt: " + san.Ungeklaert, WebGisAnzeigeTon.Warnung);
            if (san.Geschrieben && san.Nachgeprueft) return ("angelegt und nachgeprüft (ID " + (san.NeueId ?? "?") + ")", WebGisAnzeigeTon.Bestaetigt);
            if (san.Geschrieben) return ("angelegt, vom Server bestätigt (ID " + (san.NeueId ?? "?") + "), nicht nachgeprüft", WebGisAnzeigeTon.Warnung);
            if (san.SchreibFehler is not null) return ("nicht angelegt: " + san.SchreibFehler, WebGisAnzeigeTon.Fehler);
            return ("nicht versucht", WebGisAnzeigeTon.Warnung);
        }
        if (pos.Geschrieben && pos.VomServerBestaetigt) return ("bestätigt", WebGisAnzeigeTon.Bestaetigt);
        if (pos.Geschrieben) return ("geschrieben, Kontrolle offen", WebGisAnzeigeTon.Warnung);
        if (pos.SchreibFehler is not null) return ("nicht geschrieben: " + pos.SchreibFehler, WebGisAnzeigeTon.Fehler);
        return ("nicht versucht", WebGisAnzeigeTon.Warnung);
    }
}
