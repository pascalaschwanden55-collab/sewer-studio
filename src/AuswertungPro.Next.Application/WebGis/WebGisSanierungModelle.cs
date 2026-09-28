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

    public bool Geschrieben { get; set; }
    public string? SchreibFehler { get; set; }
    /// <summary>Vom WebGIS vergebene Objekt-ID nach dem Anlegen.</summary>
    public string? NeueId { get; set; }
}
