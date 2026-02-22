// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Shared;

/// <summary>
/// Statischer VSA-Code-Katalog für Validierung (keine DB-Abhängigkeit).
/// Quelle: VSA-Richtlinie Kanalinspektion / EN 13508-2.
/// </summary>
public static class VsaCatalog
{
    public sealed record VsaCodeInfo(
        string Code,
        string Label,
        bool RequiresCharacterization,   // A–D
        bool RequiresQuantification,
        string? QuantUnit                // "%", "mm", "cm" – null wenn keine Quant
    );

    private static readonly FrozenDictionary<string, VsaCodeInfo> _codes =
        new Dictionary<string, VsaCodeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            // Risse / Brüche
            ["BAA"] = new("BAA", "Längsriss",              false, true,  "mm"),
            ["BAB"] = new("BAB", "Querriss",               false, true,  "mm"),
            ["BAC"] = new("BAC", "Spiralförmiger Riss",    false, true,  "mm"),
            ["BAD"] = new("BAD", "Mehrere Risse",          false, true,  "mm"),
            ["BAE"] = new("BAE", "Bruch",                  false, false, null),
            ["BAF"] = new("BAF", "Fehlstelle",             false, false, null),

            // Verformung
            ["BBA"] = new("BBA", "Verformung",             true,  true,  "%"),
            ["BBB"] = new("BBB", "Einsturz",               false, false, null),

            // Wandschäden
            ["BCA"] = new("BCA", "Oberflächenschaden",     true,  true,  "%"),
            ["BCB"] = new("BCB", "Korrosion",              true,  true,  "%"),
            ["BCC"] = new("BCC", "Inkrustation",           true,  true,  "%"),

            // Lageabweichungen
            ["BDA"] = new("BDA", "Versatz",                false, true,  "mm"),
            ["BDB"] = new("BDB", "Abweichung in der Achse",false, true,  "mm"),

            // Undichtheit
            ["BEA"] = new("BEA", "Infiltration",           false, false, null),
            ["BEB"] = new("BEB", "Exfiltration",           false, false, null),
            ["BEC"] = new("BEC", "Sichtbare Undichtheit",  false, false, null),

            // Hindernisse / Ablagerungen
            ["BFA"] = new("BFA", "Ablagerung",             true,  true,  "%"),
            ["BFB"] = new("BFB", "Einragender Stutzen",    false, true,  "%"),
            ["BFC"] = new("BFC", "Einragendes Rohr",       false, true,  "%"),
            ["BFD"] = new("BFD", "Fremdobjekt",            false, false, null),

            // Anschlüsse
            ["BGA"] = new("BGA", "Anschluss",              false, false, null),
            ["BGB"] = new("BGB", "Anschluss – fehlerhafter Einbau", false, false, null),

            // Bauteilzustand
            ["BHA"] = new("BHA", "Reparatur",              false, false, null),
            ["BHB"] = new("BHB", "Ausbesserung",           false, false, null),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>True wenn der Code im Katalog existiert.</summary>
    public static bool IsKnown(string code) =>
        !string.IsNullOrWhiteSpace(code) && _codes.ContainsKey(code);

    /// <summary>Gibt die Code-Informationen zurück, oder null wenn unbekannt.</summary>
    public static VsaCodeInfo? Get(string code) =>
        _codes.TryGetValue(code, out var info) ? info : null;

    /// <summary>Alle bekannten Codes.</summary>
    public static IEnumerable<VsaCodeInfo> All => _codes.Values;
}
