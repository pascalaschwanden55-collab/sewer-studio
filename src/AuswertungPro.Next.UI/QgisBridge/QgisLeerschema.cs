using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Spaltenbestand einer Live-Ebene fuer den Fall, dass sie gerade nichts zu zeigen hat.
///
/// Eine GeoJSON-Datei traegt keine eigene Spaltenliste — QGIS liest die Spalten aus den
/// Objekten. Bei <c>"features":[]</c> hat der Layer deshalb KEINE Spalten, und jede
/// gespeicherte Abfrage darauf scheitert: "geometrie_quelle not recognised as an
/// available field" -> der Layer laesst sich nicht mehr oeffnen und bleibt in QGIS rot
/// ("unsicher verortet"), bis der Nutzer seine Abfrage von Hand entfernt. Das trifft
/// jedes Mal zu, wenn kein Projekt offen ist oder es keine Schaeden hat.
///
/// Statt der leeren Sammlung geht darum eine einzelne Schemazeile hinaus: alle Spalten,
/// alle Werte leer, KEINE Geometrie. Am echten QGIS 4.2 gemessen: Der Layer bleibt
/// gueltig und zeigt trotzdem null Objekte, weil sein Geometrietyp-Filter
/// (<c>|geometrytype=Point</c>) eine Zeile ohne Geometrie aussortiert. Nur ein Layer
/// ganz ohne diesen Filter zeigt die leere Zeile in der Attributtabelle — gezeichnet
/// wird sie nie.
///
/// Die Listen hier sind eine zweite Aufschreibung der Felder aus
/// <see cref="QgisBridgeSnapshotBuilder"/>. Damit sie nicht auseinanderlaufen, haelt
/// <c>QgisLeerschemaTests</c> sie gegen ein echtes Objekt derselben Ebene.
/// </summary>
internal static class QgisLeerschema
{
    private static readonly Dictionary<string, string[]> FelderJeEbene = new(StringComparer.Ordinal)
    {
        ["current"] =
        [
            "haltung", "haltung_kataster", "richtung", "geometrie_quelle", "current",
            "zustandsklasse", "zustand_farbe", "nutzungsart", "schaden_count", "source"
        ],
        ["current_schacht"] =
        [
            "schacht", "im_projekt", "sanieren", "pruefungsresultat", "current", "source"
        ],
        ["damages"] =
        [
            "haltung", "haltung_kataster", "richtung", "geometrie_quelle", "code",
            "beschreibung", "meter_start", "meter_end", "streckenschaden", "severity",
            "mpeg", "raw", "quantifizierung1", "quantifizierung2", "ezd", "ezs", "ezb",
            "zustandsklasse", "source"
        ],
        ["network"] = ["haltung", "zustandsklasse", "zustand_farbe", "source"],
        ["sanierungstyp"] = ["haltung", "nr", "ausgefuehrt_durch", "source"],
        ["schaechte"] = ["schacht", "im_projekt", "sanieren", "pruefungsresultat", "source"],
        ["schacht_sanierungstyp"] = ["schacht", "nr", "ausgefuehrt_durch", "source"]
    };

    /// <summary>Die Ebenen, fuer die ein Leerschema hinterlegt ist.</summary>
    public static IReadOnlyList<string> Ebenen { get; } = FelderJeEbene.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Die Schemazeile dieser Ebene. Eine unbekannte Ebene liefert bewusst die leere
    /// Sammlung: lieber ein Layer ohne Spalten als erfundene Spaltennamen.
    /// </summary>
    public static GeoJsonFeatureCollection Baue(string ebene)
    {
        if (!FelderJeEbene.TryGetValue(ebene, out var felder))
            return GeoJsonFeatureCollection.Empty;

        var properties = new Dictionary<string, object?>(felder.Length, StringComparer.Ordinal);
        foreach (var feld in felder)
            properties[feld] = null;

        return new GeoJsonFeatureCollection([new GeoJsonFeature(geometry: null, properties)]);
    }
}
