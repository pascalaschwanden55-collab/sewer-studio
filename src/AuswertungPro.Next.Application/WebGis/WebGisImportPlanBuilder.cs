using System;
using System.Collections.Generic;
using System.Globalization;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>Eingabe fuer den Importplan: was SewerStudio gerade fuehrt.</summary>
public sealed class WebGisImportEingabe
{
    public required WebGisObjektart Objektart { get; init; }
    public required string Bezeichnung { get; init; }
    public Guid RecordId { get; init; }
    /// <summary>Nur Haltung: aktuelle Haltungslaenge in SewerStudio (Text, Punkt als Dezimalzeichen).</summary>
    public string? Laenge { get; init; }
    public string? Baujahr { get; init; }
}

/// <summary>Eine geplante Uebernahme WebGIS -> SewerStudio.</summary>
public sealed class WebGisImportAenderung
{
    public required string Feld { get; init; }
    public string? Alt { get; init; }
    public required string Neu { get; init; }
    public required string Grund { get; init; }
}

public sealed class WebGisImportPosition
{
    public required WebGisObjektart Objektart { get; init; }
    public required string Bezeichnung { get; init; }
    public Guid RecordId { get; init; }
    public string? GlobalId { get; init; }
    public List<WebGisImportAenderung> Aenderungen { get; } = new();
    public List<string> Sperren { get; } = new();
    public List<string> Hinweise { get; } = new();
    public bool Uebernehmbar => Sperren.Count == 0 && Aenderungen.Count > 0;
    public bool Uebernommen { get; set; }
}

public sealed class WebGisImportPlan
{
    public List<WebGisImportPosition> Positionen { get; } = new();
    public List<string> Hinweise { get; } = new();
    public int Uebernehmbare => Positionen.FindAll(p => p.Uebernehmbar).Count;
    public int Gesperrte => Positionen.FindAll(p => p.Sperren.Count > 0).Count;
}

/// <summary>
/// Gegenrichtung: WebGIS -> SewerStudio. Reine Regel, kein Netz, kein Schreiben.
///
/// Regeln (Entscheid Pascal 21.09.2026):
/// - Die Haltungslaenge kommt IMMER aus dem WebGIS (Laenge geometrisch, e5b5b42c), auch
///   ueber einen Handwert hinweg — "egal was der Import der Kanalfernsehdaten bringt".
/// - Sonst wird nur gefuellt, was in SewerStudio leer ist (Baujahr Haltung/Schacht).
/// - Zustand, Bemerkung, Sanierung werden NICHT importiert: dort ist SewerStudio die Quelle.
/// </summary>
public static class WebGisImportPlanBuilder
{
    public const string FeldLaenge = "Haltungslaenge_m";
    public const string FeldBaujahr = "Baujahr";

    public static WebGisImportPosition Baue(WebGisImportEingabe e, WebGisLesestand? stand)
    {
        ArgumentNullException.ThrowIfNull(e);
        var pos = new WebGisImportPosition
        {
            Objektart = e.Objektart, Bezeichnung = e.Bezeichnung, RecordId = e.RecordId, GlobalId = stand?.GlobalId,
        };
        if (stand is null)
        {
            pos.Sperren.Add("Im WebGIS nicht eindeutig gefunden (kein oder mehrdeutiger Treffer).");
            return pos;
        }

        // 1) Laenge (nur Haltung), immer aus dem WebGIS.
        if (e.Objektart == WebGisObjektart.Haltung)
        {
            var webgis = LaengeNormiert(stand.Feld(WebGisFeldkarte.HaltungLaengeGeomRef));
            if (webgis is null)
                pos.Hinweise.Add("WebGIS fuehrt keine geometrische Laenge — Laenge unveraendert.");
            else
            {
                var alt = LaengeNormiert(e.Laenge);
                if (alt != webgis)
                    pos.Aenderungen.Add(new WebGisImportAenderung
                    {
                        Feld = FeldLaenge, Alt = e.Laenge, Neu = webgis,
                        Grund = "Laenge kommt immer aus dem WebGIS (Regel 1).",
                    });
            }
        }

        // 2) Baujahr nur wenn in SewerStudio leer.
        if (string.IsNullOrWhiteSpace(e.Baujahr))
        {
            var jahr = (stand.Feld(WebGisFeldkarte.BaujahrRef(e.Objektart)) ?? string.Empty).Trim();
            if (jahr.Length == 4 && int.TryParse(jahr, out _))
                pos.Aenderungen.Add(new WebGisImportAenderung
                {
                    Feld = FeldBaujahr, Alt = e.Baujahr, Neu = jahr, Grund = "In SewerStudio leer, im WebGIS vorhanden.",
                });
        }

        return pos;
    }

    /// <summary>Laenge auf zwei Stellen, Punkt als Dezimalzeichen ("11.44012297" -> "11.44"). Null bei leer/ungueltig.</summary>
    public static string? LaengeNormiert(string? text)
    {
        var t = (text ?? string.Empty).Trim().Replace(',', '.');
        if (t.Length == 0) return null;
        if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return null;
        return Math.Round(d, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
