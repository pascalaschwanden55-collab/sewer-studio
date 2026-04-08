// AuswertungPro – KI Videoanalyse Modul
// VSA-Richtlinie Schadencodierung 2018 / EN 13508-2
// NUR Leitungscodes (B-Gruppe + AE), KEINE Schachtcodes (D-Gruppe)
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Shared;

/// <summary>
/// Statischer VSA-Code-Katalog fuer Plausibilitaetspruefung (keine DB-Abhaengigkeit).
/// Quelle: VSA-Richtlinie Schadencodierung und Datentransfer 2018, EN 13508-2.
/// Nur Leitungscodes — Schachtcodes werden nicht von der KI verarbeitet.
/// </summary>
public static class VsaCatalog
{
    public sealed record VsaCodeInfo(
        string Code,
        string Label,
        bool RequiresCharacterization,   // Char1 (A-Z) noetig
        bool RequiresQuantification,
        string? QuantUnit                // "%", "mm", "°" – null wenn keine Quant
    );

    private static readonly FrozenDictionary<string, VsaCodeInfo> _codes =
        new Dictionary<string, VsaCodeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            // ══════════════════════════════════════════════════════════
            // BA: Bauliche Schaeden
            // ══════════════════════════════════════════════════════════
            ["BAA"] = new("BAA", "Verformung",                  true,  true,  "%"),
            ["BAB"] = new("BAB", "Risse",                       true,  true,  "mm"),   // A=Haarriss ohne Q1
            ["BAC"] = new("BAC", "Bruch/Einsturz",              true,  true,  "mm"),
            ["BAD"] = new("BAD", "Defektes Mauerwerk",          true,  true,  "mm"),   // C=Sohle abgesackt
            ["BAE"] = new("BAE", "Fehlender Moertel",           false, true,  "mm"),
            ["BAF"] = new("BAF", "Oberflaechenschaden",         true,  false, null),
            ["BAG"] = new("BAG", "Einragender Anschluss",       false, true,  "%"),
            ["BAH"] = new("BAH", "Schadhafter Anschluss",       true,  false, null),
            ["BAI"] = new("BAI", "Einragendes Dichtungsmaterial", true, true, "%"),
            ["BAJ"] = new("BAJ", "Verschobene Rohrverbindung",  true,  false, null),
            ["BAK"] = new("BAK", "Schadhafte Innenauskleidung", true,  false, null),
            ["BAL"] = new("BAL", "Schadhafte Reparatur",        true,  false, null),
            ["BAM"] = new("BAM", "Schadhafte Schweissnaht",     true,  false, null),
            ["BAN"] = new("BAN", "Poroese Leitung",             false, false, null),
            ["BAO"] = new("BAO", "Boden sichtbar",              false, false, null),
            ["BAP"] = new("BAP", "Hohlraum sichtbar",           false, false, null),

            // ══════════════════════════════════════════════════════════
            // BB: Betriebliche Stoerungen
            // ══════════════════════════════════════════════════════════
            ["BBA"] = new("BBA", "Wurzeln",                     true,  true,  "%"),
            ["BBB"] = new("BBB", "Anhaftende Stoffe",           true,  true,  "%"),    // A=Inkrustation
            ["BBC"] = new("BBC", "Ablagerungen",                true,  true,  "%"),
            ["BBD"] = new("BBD", "Eindringender Boden",         true,  true,  "%"),
            ["BBE"] = new("BBE", "Andere Hindernisse",          true,  true,  "%"),
            ["BBF"] = new("BBF", "Infiltration",                true,  false, null),
            ["BBG"] = new("BBG", "Exfiltration",                false, false, null),
            ["BBH"] = new("BBH", "Ungeziefer",                  true,  true,  "Stk"),

            // ══════════════════════════════════════════════════════════
            // BC: Bestandsaufnahme
            // ══════════════════════════════════════════════════════════
            ["BCA"] = new("BCA", "Seitlicher Anschluss",        true,  true,  "mm"),   // Q1=Hoehe, Q2=Breite
            ["BCB"] = new("BCB", "Punktuelle Reparatur",        true,  false, null),
            ["BCC"] = new("BCC", "Bogen",                       true,  true,  "°"),
            ["BCD"] = new("BCD", "Rohranfang",                  false, false, null),
            ["BCE"] = new("BCE", "Rohrende",                    false, false, null),

            // ══════════════════════════════════════════════════════════
            // BD: Weitere Codes / Steuercodes
            // ══════════════════════════════════════════════════════════
            ["BDA"] = new("BDA", "Allgemeinzustand Foto",       false, false, null),
            ["BDB"] = new("BDB", "Beginn TV-Untersuchung",      false, false, null),
            ["BDC"] = new("BDC", "Inspektionsabbruch",          true,  false, null),
            ["BDD"] = new("BDD", "Wasserspiegel",               true,  true,  "%"),
            ["BDE"] = new("BDE", "Zufluss/Fehlanschluss",       true,  true,  "%"),
            ["BDF"] = new("BDF", "Gefaehrliche Atmosphaere",    true,  false, null),
            ["BDG"] = new("BDG", "Keine Sicht",                 true,  false, null),

            // ══════════════════════════════════════════════════════════
            // AE: Aenderungen waehrend Inspektion
            // ══════════════════════════════════════════════════════════
            ["AEC"] = new("AEC", "Rohrprofilwechsel",           true,  true,  "mm"),
            ["AED"] = new("AED", "Rohrmaterialwechsel",         true,  false, null),
            ["AEF"] = new("AEF", "Neue Baulaenge",              false, true,  "mm"),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>True wenn der Code im Katalog existiert.</summary>
    public static bool IsKnown(string code) =>
        !string.IsNullOrWhiteSpace(code) && _codes.ContainsKey(code);

    /// <summary>Gibt die Code-Informationen zurueck, oder null wenn unbekannt.</summary>
    public static VsaCodeInfo? Get(string code) =>
        _codes.TryGetValue(code, out var info) ? info : null;

    /// <summary>Alle bekannten Codes.</summary>
    public static IEnumerable<VsaCodeInfo> All => _codes.Values;
}
