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
    /// <summary>
    /// VSA-Merkblatt 2018 (Vernehmlassung 1.0.8, 28.03.2018) — verbindliche Zuordnung.
    /// ACHTUNG: Die Code-Bedeutungen unterscheiden sich vom aelteren Standard!
    ///   BBA = Wurzeln (NICHT Inkrustation)
    ///   BBB = Anhaftende Stoffe/Inkrustation (NICHT Wurzeln)
    ///   BAG = Einragender Anschluss
    ///   BAH = Schadhafter Anschluss (NICHT Versatz)
    ///   BAI = Einragendes Dichtungsmaterial
    ///   BAJ = Verschobene Rohrverbindung (= Versatz!)
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

            // 3 = displacement (Verschobene Rohrverbindung / Versatz)
            ["BAJ"] = 3,

            // 4 = intrusion (Einragender Anschluss + Eindringender Boden)
            ["BAG"] = 4,   // Einragender Anschluss
            ["BBD"] = 4,   // Eindringen von Bodenmaterial

            // 5 = root (Wurzeln)
            ["BBA"] = 5,   // VSA 2018: BBA = Wurzeln

            // 6 = deposit (Ablagerung + Anhaftende Stoffe/Inkrustation)
            ["BBC"] = 6,   // Ablagerungen an der Rohrsohle
            ["BBB"] = 6,   // VSA 2018: BBB = Anhaftende Stoffe (Inkrustation)

            // 7 = infiltration (Undichtheit / Infiltration / Exfiltration)
            ["BBF"] = 7,
            ["BBG"] = 7,

            // 8 = connection (Seitlicher Anschluss)
            ["BCA"] = 8,

            // 9 = structural_other (Sammelklasse)
            ["BAD"] = 9,   // Defektes Mauerwerk
            ["BAE"] = 9,   // Fehlender Moertel
            ["BAH"] = 9,   // Schadhafter Anschluss
            ["BAI"] = 9,   // Einragendes Dichtungsmaterial
            ["BAK"] = 9,   // Feststellung Innenauskleidung
            ["BAL"] = 9,   // Schadhafte Reparatur
            ["BAM"] = 9,   // Schadhafte Schweissnaht
            ["BAN"] = 9,   // Poroese Leitung
            ["BAO"] = 9,   // Boden sichtbar
            ["BAP"] = 9,   // Hohlraum sichtbar
            ["BBE"] = 9,   // Andere Hindernisse
            ["BBH"] = 9,   // Ungeziefer
            ["BCB"] = 9,   // Punktuelle Reparatur
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
