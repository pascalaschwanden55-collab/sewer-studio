using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Eine im WebGIS bereits vorhandene Sanierungsmassnahme (Zeile der Liste in der Elternmaske).</summary>
public sealed class WebGisSanierungZeile
{
    public string? GlobalId { get; init; }
    /// <summary>
    /// Erste Spalte der Liste, roh wie geliefert. Live gelesen 28.09.2026: Die Spalte heisst «Zeitpunkt»
    /// (Feld <c>zeitpunkt</c>) und war bei beiden Bürglen-Massnahmen leer — sie ist NICHT das Sanierungsjahr.
    /// </summary>
    public string? Beginn { get; init; }
    /// <summary>
    /// Sanierungsjahr aus der Massnahme selbst (<see cref="WebGisSanierungFeldkarte.SanierungsjahrRef"/>),
    /// nachgelesen über <see cref="WebGisMassnahmenJahr"/>. Null, solange nicht gelesen.
    /// </summary>
    public string? Sanierungsjahr { get; set; }
    /// <summary>Jahr der Massnahme: zuerst das nachgelesene Sanierungsjahr, sonst die erste Listenspalte.</summary>
    public string? Jahr => WebGisSanierungFeldkarte.JahrAusDatum(Sanierungsjahr) ?? WebGisSanierungFeldkarte.JahrAusDatum(Beginn);
    public string? Art { get; init; }
    public string? Status { get; init; }
    public string? Verfahren { get; init; }
}

/// <summary>Plan fuer genau eine anzulegende Sanierungsmassnahme.</summary>
public sealed class WebGisSanierungPosition
{
    public required WebGisObjektart Objektart { get; init; }
    /// <summary>Bezeichnung des Elternobjekts (Haltung/Schacht).</summary>
    public required string ElternBezeichnung { get; init; }
    public string? ElternGlobalId { get; init; }
    /// <summary>Datensatz-Id des Elternobjekts im Projekt — damit die Uebersicht es oeffnen kann.</summary>
    public Guid ElternRecordId { get; init; }
    public Guid AkteId { get; init; }
    /// <summary>refId -> Wert wie er ans WebGIS geht (Combo-Schluessel als Text, Datum ISO).</summary>
    public Dictionary<string, string> Felder { get; } = new(StringComparer.Ordinal);
    /// <summary>"Feld: Klartext" fuer den Bericht.</summary>
    public List<string> Anzeige { get; } = new();
    /// <summary>Sperrgruende — bei mindestens einem wird NICHT angelegt.</summary>
    public List<string> Sperren { get; } = new();
    public List<string> Hinweise { get; } = new();

    public bool Schreibbar => Sperren.Count == 0 && Felder.Count > 0 && ElternGlobalId is not null;

    /// <summary>
    /// Der Server hat das Anlegen bestaetigt (<c>isFailure:false</c> und neue Kennung). Das allein belegt noch
    /// nicht, dass alle geplanten Werte stehen — dafuer <see cref="Nachgeprueft"/> (WG05, 28.09.2026).
    /// Ein bestaetigtes Anlegen wird nie nachtraeglich zu «nicht angelegt».
    /// </summary>
    public bool Geschrieben { get; set; }
    public string? SchreibFehler { get; set; }
    /// <summary>
    /// Vom WebGIS vergebene Kennung nach dem Anlegen. Live belegt 28.09.2026: das ist die OBJECTID, NICHT die
    /// GlobalID — die Massnahme laesst sich darueber nicht zuruecklesen.
    /// </summary>
    public string? NeueId { get; set; }
    /// <summary>GlobalID der neu angelegten Massnahme, ermittelt aus der Liste am Elternobjekt (vorher/nachher).</summary>
    public string? NeueGlobalId { get; set; }
    /// <summary>
    /// Gegenprobe bestanden: Genau eine neue Zeile steht in der Liste des Elternobjekts, und Art, Status,
    /// Verfahren, Sanierungsjahr (alle geplanten Felder) stehen so in der Massnahme, wie sie geplant waren.
    /// </summary>
    public bool Nachgeprueft { get; set; }
    /// <summary>
    /// Vom Server bestaetigt, aber die Gegenprobe ergab keinen sicheren Befund (keine oder mehrere neue Zeilen,
    /// Lesefehler, abweichender Wert). Der Grund steht hier. Nie automatisch wiederholen und nie als «nicht
    /// angelegt» ausgeben: Vor einem neuen Versuch im WebGIS nachsehen.
    /// </summary>
    public string? Ungeklaert { get; set; }
}
