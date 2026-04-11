using System.Collections.Frozen;

namespace AuswertungPro.Next.UI.Ai.Training.Models;

/// <summary>
/// Statisches Mapping zwischen VSA-Schadencodes (EN 13508-2) und 10 YOLO-Defektklassen.
/// Steuercodes (BCD, BCE, BCC, BDB-BDF) werden ausgeschlossen — sie ergeben sich
/// aus dem Meterstand, nicht aus visueller Erkennung.
/// </summary>
public static class YoloDefectTaxonomy
{
    /// <summary>
    /// YOLO-Defektklasse mit numerischer ID und englischem Klassennamen.
    /// </summary>
    public readonly record struct DefectClass(int ClassId, string ClassName);

    /// <summary>
    /// Alle 10 YOLO-Klassen, sortiert nach ClassId (0-9).
    /// </summary>
    public static DefectClass[] AllClasses { get; } =
    [
        new(0, "crack"),
        new(1, "fracture"),
        new(2, "deformation"),
        new(3, "displacement"),
        new(4, "intrusion"),
        new(5, "root"),
        new(6, "deposit"),
        new(7, "infiltration"),
        new(8, "connection"),
        new(9, "structural_other"),
    ];

    /// <summary>
    /// VSA-3-Zeichen-Praefix → YOLO-Klassen-Index.
    /// Steuercodes sind NICHT enthalten — FromVsaCode gibt fuer sie null zurueck.
    /// </summary>
    private static readonly FrozenDictionary<string, int> VsaToClassId =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // 0 = crack (Risse)
            ["BAB"] = 0,

            // 1 = fracture (Bruch/Einsturz)
            ["BAC"] = 1,

            // 2 = deformation (Verformung + Oberflaechenschaden)
            ["BAA"] = 2,
            ["BAF"] = 2,

            // 3 = displacement (Versatz)
            ["BAH"] = 3,

            // 4 = intrusion (Einragender Stutzen + Eindringender Boden)
            ["BAI"] = 4,
            ["BBD"] = 4,

            // 5 = root (Wurzeleinwuchs)
            ["BBB"] = 5,

            // 6 = deposit (Ablagerung + Inkrustation)
            ["BBC"] = 6,
            ["BBA"] = 6,

            // 7 = infiltration (Undichtheit / Infiltration)
            ["BBF"] = 7,
            ["BBG"] = 7,

            // 8 = connection (Seitlicher Anschluss)
            ["BCA"] = 8,

            // 9 = structural_other (Sammelklasse fuer seltene Schaeden)
            ["BAD"] = 9,
            ["BAE"] = 9,
            ["BAG"] = 9,
            ["BAJ"] = 9,
            ["BAK"] = 9,
            ["BBE"] = 9,
            ["BBH"] = 9,
            ["BCB"] = 9,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Steuercodes — werden aus dem Meterstand abgeleitet, nicht visuell erkannt.
    /// </summary>
    private static readonly FrozenSet<string> Steuercodes =
        FrozenSet.ToFrozenSet(
            ["BCD", "BCE", "BCC", "BDB", "BDC", "BDD", "BDE", "BDF"],
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Mappt einen VSA-Code (z.B. "BAB.B.A") auf die zugehoerige YOLO-Klasse.
    /// Extrahiert die ersten 3 Zeichen vor dem ersten Punkt als Praefix.
    /// Gibt null zurueck fuer Steuercodes, unbekannte Codes oder ungueltige Eingaben.
    /// </summary>
    public static DefectClass? FromVsaCode(string? vsaCode)
    {
        if (string.IsNullOrWhiteSpace(vsaCode))
            return null;

        // 3-Zeichen-Praefix extrahieren (vor erstem Punkt oder die ersten 3 Zeichen)
        var trimmed = vsaCode.Trim();
        var dotIndex = trimmed.IndexOf('.');
        var prefix = dotIndex >= 0
            ? trimmed[..dotIndex]
            : (trimmed.Length >= 3 ? trimmed[..3] : trimmed);

        if (prefix.Length < 3)
            return null;

        // Steuercodes explizit ausschliessen
        if (Steuercodes.Contains(prefix))
            return null;

        // Lookup im Mapping
        if (VsaToClassId.TryGetValue(prefix, out var classId))
            return AllClasses[classId];

        return null;
    }

    /// <summary>
    /// Erzeugt den Inhalt einer YOLO data.yaml fuer das Training.
    /// Enthaelt nc (Anzahl Klassen) und alle Klassennamen.
    /// </summary>
    public static string GenerateDataYaml(string datasetPath)
    {
        var names = string.Join(", ", AllClasses.Select(c => $"'{c.ClassName}'"));

        return $"""
                path: {datasetPath}
                train: images/train
                val: images/val

                nc: {AllClasses.Length}
                names: [{names}]
                """;
    }
}
