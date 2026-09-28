using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Objektklasse im WebGIS-Kataster (bestimmt Tabelle und Feldkarte).</summary>
public enum WebGisObjektart
{
    Haltung,
    Schacht
}

/// <summary>
/// Ein aus dem WebGIS frisch gelesener Objektstand. <see cref="Felder"/> ist der
/// aktuelle Wert je refId (Combo als Ganzzahl-Schluessel als Text, EditBox als Text).
/// </summary>
public sealed class WebGisLesestand
{
    public required string GlobalId { get; init; }
    public required string Bezeichnung { get; init; }
    public Dictionary<string, string?> Felder { get; init; } = new(StringComparer.Ordinal);

    public string? Feld(string refId) => Felder.TryGetValue(refId, out var v) ? v : null;

    /// <summary>Bereits vorhandene Sanierungsmassnahmen (Liste in der Elternmaske).</summary>
    public List<WebGisSanierungZeile> Sanierungen { get; init; } = new();

    /// <summary>Combo-Kataloge der Maske je refId (Schluessel/Klartext), fuer Klartext-Aufloesung der Handwerte.</summary>
    public Dictionary<string, List<(string Key, string Text)>> Kataloge { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Nachgeladene Listen je Zielgruppe (Schluessel <see cref="GruppenSchluessel"/>): Die Maske
    /// liefert fuer ein abhaengiges Detail (Material-Detail) nur die Liste der GERADE gesetzten
    /// Gruppe; steht in SewerStudio «Beton, Fertigteil», die Maske aber auf Kunststoff, fehlt
    /// der Wert dort (Schacht 59723, 22.09.2026). Der Ablauf laedt die Liste der Zielgruppe
    /// vor dem Planbau nach.
    /// </summary>
    public Dictionary<string, List<(string Key, string Text)>> KatalogeNachGruppe { get; init; } = new(StringComparer.Ordinal);

    public static string GruppenSchluessel(string refId, string hauptKey) => refId + "|" + hauptKey;

    /// <summary>
    /// Subtyp der Maske als «name:wert» (Sanierungsmaske «art:4», Schachtmaske die Bauwerksart),
    /// wie ihn getLayoutDataCombined im Datenobjekt liefert. Ohne ihn kann der Server fuer
    /// abhaengige Listen kein Formular waehlen («Could not load form's default form.», 22.09.2026).
    /// </summary>
    public string? Subtyp { get; set; }

    /// <summary>Katalogtext zum aktuellen Schluessel eines Combo-Felds (fuer den Bericht); sonst der Rohwert.</summary>
    public string? FeldText(string refId)
    {
        var v = Feld(refId);
        if (v is null || !Kataloge.TryGetValue(refId, out var l)) return v;
        foreach (var (k, t) in l) if (k == v) return t;
        return v;
    }
}

/// <summary>Eine geplante Feldaenderung fuer den Bericht und die Ausfuehrung.</summary>
public sealed class WebGisFeldAenderung
{
    public required string RefId { get; init; }
    public required string Feld { get; init; }
    /// <summary>
    /// Alter Wert GENAU so, wie <see cref="WebGisLesestand.Feld"/> ihn liefert (Combo: der
    /// Schluessel). Der Konfliktschutz vergleicht damit — nie mit dem Klartext (Buerglen 21.09.:
    /// acht Objekte faelschlich gesperrt, weil hier der Klartext stand).
    /// </summary>
    public string? Alt { get; init; }
    /// <summary>Klartext des alten Werts fuer Bericht und Anzeige (Combo); sonst null.</summary>
    public string? AltText { get; init; }
    public required string Neu { get; init; }
    /// <summary>Anzeigetext neu (Klartext statt Code) fuer den Bericht.</summary>
    public string? NeuText { get; init; }
}

/// <summary>Plan fuer genau ein Objekt.</summary>
public sealed class WebGisExportPosition
{
    public required WebGisObjektart Objektart { get; init; }
    public required string Bezeichnung { get; init; }
    public string? GlobalId { get; init; }
    /// <summary>
    /// Im Projekt gespeicherte GlobalID. Gesetzt heisst: jedes erneute Lesen (vor dem Schreiben,
    /// Nachkontrolle, Massnahmen) laeuft direkt ueber sie, nie ueber den Namen.
    /// </summary>
    public string? GespeicherteGlobalId { get; init; }
    public Guid RecordId { get; init; }
    /// <summary>
    /// Alle Felder des Objekts, wie sie beim Planen im WebGIS standen (samt Aenderungsdatum).
    /// Vor dem Schreiben muss der frische Stand gleich sein, sonst wird neu geprueft.
    /// Null nur bei einer Position ohne gelesenen Stand (gesperrt).
    /// </summary>
    public IReadOnlyDictionary<string, string?>? GelesenerStand { get; init; }
    public List<WebGisFeldAenderung> Aenderungen { get; } = new();
    /// <summary>Vergleichsliste für die Anzeige: jedes Feld mit SewerStudio- und WebGIS-Wert (28.09.2026).</summary>
    public List<WebGisFeldVergleich> Vergleich { get; } = new();
    /// <summary>
    /// Werte der Kanalfirma, die vom WebGIS abweichen — nur zur Auswahl, nie automatisch geschrieben
    /// (<see cref="WebGisVorschlagAuswahl"/>).
    /// </summary>
    public List<WebGisVorschlag> Vorschlaege { get; } = new();
    /// <summary>Sperrgruende — bei mindestens einem wird NICHT geschrieben.</summary>
    public List<string> Sperren { get; } = new();
    /// <summary>Nicht sperrende Hinweise fuer den Bericht.</summary>
    public List<string> Hinweise { get; } = new();

    public bool Schreibbar => Sperren.Count == 0 && Aenderungen.Count > 0 && GlobalId is not null;

    /// <summary>Wird vom Ablauf gesetzt: true nach erfolgreichem Schreiben.</summary>
    public bool Geschrieben { get; set; }
    /// <summary>
    /// Der Server hat das Schreiben bestaetigt — unabhaengig davon, ob die Nachkontrolle danach ein
    /// verworfenes Feld findet oder selbst scheitert (Audit A05/A06, 23.09.2026).
    /// </summary>
    public bool VomServerBestaetigt { get; set; }
    /// <summary>Wird vom Ablauf gesetzt: Fehlertext bei fehlgeschlagenem Schreiben.</summary>
    public string? SchreibFehler { get; set; }
}

/// <summary>Gesamter Exportplan eines Projekts.</summary>
public sealed class WebGisExportPlan
{
    public List<WebGisExportPosition> Positionen { get; } = new();
    /// <summary>Anzulegende Sanierungsmassnahmen (Stufe 2), je ausgefuehrter Akte eine.</summary>
    public List<WebGisSanierungPosition> Sanierungen { get; } = new();
    public List<string> Hinweise { get; } = new();

    public int Schreibbare => Positionen.FindAll(p => p.Schreibbar).Count;
    public int Gesperrte => Positionen.FindAll(p => p.Sperren.Count > 0).Count;

    /// <summary>Nichts zu schreiben: keine Aenderung, keine Massnahme und kein angehakter Vorschlag der Kanalfirma.</summary>
    public bool NichtsZuSchreiben => Schreibbare == 0 && SanierungenSchreibbar == 0
        && !Positionen.Exists(p => p.Sperren.Count == 0 && p.GlobalId is not null && p.Vorschlaege.Exists(v => v.Gewaehlt));
    public int SanierungenSchreibbar => Sanierungen.FindAll(p => p.Schreibbar).Count;
    public int SanierungenGesperrt => Sanierungen.FindAll(p => p.Sperren.Count > 0).Count;
}
