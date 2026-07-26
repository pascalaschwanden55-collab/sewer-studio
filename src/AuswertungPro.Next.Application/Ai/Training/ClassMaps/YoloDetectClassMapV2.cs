using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Application.Ai.Training.ClassMaps;

/// <summary>
/// Verbindliche Klassen-IDs fuer YOLO-Detect v2. Eingefroren: neue Klassen nur
/// als neue Kartenversion, nie in-place (siehe v3 mit BCC_bogen).
/// </summary>
public static class YoloDetectClassMapV2
{
    public const int Version = 2;

    public static IReadOnlyDictionary<string, int> Classes { get; } =
        new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["BCA_anschluss"] = 0,
                ["BAB_riss"] = 1,
                ["BAC_bruch"] = 2,
                ["BAA_verformung"] = 3,
                ["BAF_oberflaeche"] = 4,
                ["BAH_schadanschluss"] = 5,
                ["BAI_dichtung"] = 6,
                ["BAJ_verbindung"] = 7,
                ["BBA_wurzeln"] = 8,
                ["BBB_anhaftung"] = 9,
                ["BBC_ablagerung"] = 10,
                ["BBD_boden"] = 11,
                ["BBF_infiltration"] = 12,
                ["SONST_schaden"] = 13
            });
}
