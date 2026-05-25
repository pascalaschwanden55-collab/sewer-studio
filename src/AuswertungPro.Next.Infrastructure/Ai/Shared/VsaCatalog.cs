using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Ai.Shared;

/// <summary>
/// Statischer VSA-Code-Katalog fuer Validierung ohne Datenbank-Abhaengigkeit.
/// Quelle: VSA-Richtlinie Kanalinspektion / EN 13508-2.
/// </summary>
public static class VsaCatalog
{
    public sealed record VsaCodeInfo(
        string Code,
        string Label,
        bool RequiresCharacterization,
        bool RequiresQuantification,
        string? QuantUnit);

    private static readonly FrozenDictionary<string, VsaCodeInfo> _codes =
        new Dictionary<string, VsaCodeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["BAA"] = new("BAA", "Laengsriss", false, true, "mm"),
            ["BAB"] = new("BAB", "Querriss", false, true, "mm"),
            ["BAC"] = new("BAC", "Spiralfoermiger Riss", false, true, "mm"),
            ["BAD"] = new("BAD", "Mehrere Risse", false, true, "mm"),
            ["BAE"] = new("BAE", "Bruch", false, false, null),
            ["BAF"] = new("BAF", "Fehlstelle", false, false, null),

            ["BBA"] = new("BBA", "Verformung", true, true, "%"),
            ["BBB"] = new("BBB", "Einsturz", false, false, null),

            ["BCA"] = new("BCA", "Oberflaechenschaden", true, true, "%"),
            ["BCB"] = new("BCB", "Korrosion", true, true, "%"),
            ["BCC"] = new("BCC", "Inkrustation", true, true, "%"),

            ["BDA"] = new("BDA", "Versatz", false, true, "mm"),
            ["BDB"] = new("BDB", "Abweichung in der Achse", false, true, "mm"),

            ["BEA"] = new("BEA", "Infiltration", false, false, null),
            ["BEB"] = new("BEB", "Exfiltration", false, false, null),
            ["BEC"] = new("BEC", "Sichtbare Undichtheit", false, false, null),

            ["BFA"] = new("BFA", "Ablagerung", true, true, "%"),
            ["BFB"] = new("BFB", "Einragender Stutzen", false, true, "%"),
            ["BFC"] = new("BFC", "Einragendes Rohr", false, true, "%"),
            ["BFD"] = new("BFD", "Fremdobjekt", false, false, null),

            ["BGA"] = new("BGA", "Anschluss", false, false, null),
            ["BGB"] = new("BGB", "Anschluss - fehlerhafter Einbau", false, false, null),

            ["BHA"] = new("BHA", "Reparatur", false, false, null),
            ["BHB"] = new("BHB", "Ausbesserung", false, false, null),

            ["BCD"] = new("BCD", "Rohranfang", false, false, null),
            ["BCE"] = new("BCE", "Rohrende", false, false, null),
            ["BCG"] = new("BCG", "Bogen", false, true, "Grad"),
            ["BAI"] = new("BAI", "Einragender Stutzen", false, true, "%"),

            ["BDC"] = new("BDC", "Inspektionsabbruch", false, false, null),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string code) =>
        !string.IsNullOrWhiteSpace(code) && _codes.ContainsKey(code);

    public static VsaCodeInfo? Get(string code) =>
        _codes.TryGetValue(code, out var info) ? info : null;

    public static IEnumerable<VsaCodeInfo> All => _codes.Values;
}
