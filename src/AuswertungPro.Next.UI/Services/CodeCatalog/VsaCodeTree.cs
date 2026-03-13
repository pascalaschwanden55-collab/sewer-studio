using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Services.CodeCatalog;

// ═══════════════════════════════════════════════════════════════
// Statischer VSA-Code-Baum (EN 13508-2 / VSA-KEK 2018)
// Hierarchie: Gruppe → Hauptcode → Char1 → Char2
// ═══════════════════════════════════════════════════════════════

public sealed record GroupDef(string Label, string Color, string Icon, Dictionary<string, VsaCodeDef> Codes);

public sealed record VsaCodeDef
{
    public string Label { get; init; } = "";
    public string? FinalCode { get; init; }
    public bool IsSteuer { get; init; }
    public string? Note { get; init; }
    public string? Warn { get; init; }
    public bool XPrefix { get; init; }
    /// <summary>Char1 → Label. Wenn null, ist der Code ein Endcode.</summary>
    public Dictionary<string, CharDef>? Char1 { get; init; }
    /// <summary>Globales Char2 (fuer alle Char1 gleich). Wird von CharDef.Char2 ueberschrieben.</summary>
    public Dictionary<string, string>? Char2 { get; init; }
    /// <summary>Char2 pro Char1 (nur bei BAI etc.).</summary>
    public Dictionary<string, Dictionary<string, string>>? Char2PerChar1 { get; init; }
    /// <summary>Ungueltige Char1→Char2 Kombinationen.</summary>
    public Dictionary<string, HashSet<string>>? Invalid { get; init; }
    /// <summary>Wenn true, alle Char1×Char2 Kombinationen gueltig (kein Invalid).</summary>
    public bool AllValid { get; init; }
}

public sealed record CharDef
{
    public string Label { get; init; } = "";
    /// <summary>Eigenes Char2 fuer diesen Char1 (z.B. BAK.D hat eigene Char2).</summary>
    public Dictionary<string, string>? Char2 { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// Quantifizierungs-Regeln
// ═══════════════════════════════════════════════════════════════

public sealed record QuantRule
{
    public QuantField? Q1 { get; init; }
    /// <summary>Variabel: Char1-abhaengige Q1-Regel.</summary>
    public Dictionary<string, QuantField?>? Q1PerChar1 { get; init; }
    public QuantField? Q2 { get; init; }
}

public sealed record QuantField
{
    /// <summary>P=Pflicht, V=Variabel, O=Optional.</summary>
    public string Pflicht { get; init; } = "O";
    public string? Einheit { get; init; }
    public string? Label { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public string? Hint { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// Uhrzeiger-Regeln
// ═══════════════════════════════════════════════════════════════

public sealed record ClockRule
{
    /// <summary>none, single, range.</summary>
    public string Mode { get; init; } = "range";
    public string? Hint { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// Kachel-Element (fuer ViewModel)
// ═══════════════════════════════════════════════════════════════

public sealed record TileItem
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Description { get; init; }
    public string? BadgeText { get; init; }
    public string? BadgeColor { get; init; }
    public bool IsInvalid { get; init; }
    public bool IsFinal { get; init; }
    public bool IsSteuer { get; init; }
    public string? GroupColor { get; init; }
    public string? Icon { get; init; }
}

// ═══════════════════════════════════════════════════════════════
// Statische Daten
// ═══════════════════════════════════════════════════════════════

public static class VsaCodeTree
{
    // Hilfsfunktion: Einfache Char1-Definition (nur Label, kein eigenes Char2)
    private static CharDef C(string label) => new() { Label = label };
    private static CharDef C(string label, Dictionary<string, string> char2) => new() { Label = label, Char2 = char2 };

    public static readonly Dictionary<string, GroupDef> Groups = new()
    {
        ["BA"] = new("Baulicher Zustand", "#DC2626", "\U0001F527", new()
        {
            ["BAA"] = new()
            {
                Label = "Verformung",
                Char1 = new() { ["A"] = C("vertikal deformiert"), ["B"] = C("horizontal deformiert") }
            },
            ["BAB"] = new()
            {
                Label = "Risse",
                Char1 = new() { ["A"] = C("Haarriss"), ["B"] = C("Riss"), ["C"] = C("Klaffender Riss (>=5mm)") },
                Char2 = new() { ["A"] = "laengs", ["B"] = "radial", ["C"] = "komplex/Scherben", ["D"] = "spiralfoermig", ["E"] = "sternfoermig" },
                AllValid = true
            },
            ["BAC"] = new()
            {
                Label = "Leitungsbruch/Einsturz",
                Char1 = new() { ["A"] = C("Verschobene Scherbe"), ["B"] = C("Fehlende Scherbe"), ["C"] = C("Bruch/Einsturz") }
            },
            ["BAD"] = new()
            {
                Label = "Defektes Mauerwerk",
                Char1 = new() { ["A"] = C("Steine verschoben"), ["B"] = C("Steine fehlen"), ["C"] = C("Sohle abgesackt"), ["D"] = C("Einsturz") }
            },
            ["BAE"] = new() { Label = "Fehlender Moertel", FinalCode = "BAE" },
            ["BAF"] = new()
            {
                Label = "Oberflaechenschaden",
                Note = "J nur metallisch!",
                Char1 = new()
                {
                    ["A"] = C("Rauhe Wand"), ["B"] = C("Abplatzung"), ["C"] = C("Zuschlag sichtbar"),
                    ["D"] = C("Zuschlag einragend"), ["E"] = C("Zuschlag fehlen"), ["F"] = C("Armierung sichtbar"),
                    ["G"] = C("Armierung einragend"), ["H"] = C("Armierung korrodiert"), ["I"] = C("Wand fehlt"),
                    ["J"] = C("Wand korrodiert"), ["K"] = C("Beule"), ["Z"] = C("Andersartig")
                },
                Char2 = new() { ["A"] = "mechanisch", ["B"] = "chemisch", ["C"] = "chem. oben", ["D"] = "chem. unten", ["E"] = "Ursache unklar", ["Z"] = "andere Ursache" },
                Invalid = new()
                {
                    ["B"] = new() { "B", "C", "D" },
                    ["H"] = new() { "A" },
                    ["J"] = new() { "A" },
                    ["K"] = new() { "B", "C", "D" }
                }
            },
            ["BAG"] = new() { Label = "Einragender Anschluss", FinalCode = "BAG", Warn = "Transfer: BAGA" },
            ["BAH"] = new()
            {
                Label = "Schadhafter Anschluss",
                Char1 = new()
                {
                    ["A"] = C("falsch eingefuehrt"), ["B"] = C("zurueckliegend"), ["C"] = C("unvollstaendig"),
                    ["D"] = C("beschaedigt"), ["E"] = C("verstopft"), ["Z"] = C("andersartig")
                }
            },
            ["BAI"] = new()
            {
                Label = "Einrag. Dichtungsmaterial",
                Char1 = new() { ["A"] = C("Dichtring"), ["Z"] = C("Anderes Material") },
                Char2PerChar1 = new()
                {
                    ["A"] = new() { ["A"] = "verschoben", ["B"] = "einragend oben Mitte", ["C"] = "einragend unten Mitte", ["D"] = "gebrochen" }
                }
            },
            ["BAJ"] = new()
            {
                Label = "Verschob. Rohrverbindung",
                Char1 = new() { ["A"] = C("Breite Verbindung"), ["B"] = C("versetzt"), ["C"] = C("Knick") }
            },
            ["BAK"] = new()
            {
                Label = "Innenauskleidung",
                Char1 = new()
                {
                    ["A"] = C("abgeloest"), ["B"] = C("verfaerbt"), ["C"] = C("Endstelle schadhaft"),
                    ["D"] = C("Faltenbildung", new() { ["A"] = "laengs", ["B"] = "radial", ["C"] = "komplex", ["D"] = "spiralfoermig" }),
                    ["E"] = C("Blasen/Beulen"), ["F"] = C("Beule nach aussen"), ["G"] = C("Innenhaut loest"),
                    ["H"] = C("Naht loest"), ["I"] = C("Riss/Spalt"), ["J"] = C("Loch"),
                    ["K"] = C("Verb. defekt"), ["L"] = C("Werkstoff weich"), ["M"] = C("Harz fehlt"),
                    ["N"] = C("Ende nicht dicht"), ["Z"] = C("andersartig")
                }
            },
            ["BAL"] = new()
            {
                Label = "Schadhafte Reparatur",
                Char1 = new()
                {
                    ["A"] = C("Wand fehlt"), ["B"] = C("Loch mangelhaft"), ["C"] = C("loest sich"),
                    ["D"] = C("fehlt Kontaktfl."), ["E"] = C("Ueberschuss"), ["F"] = C("Loch Werkstoff"),
                    ["G"] = C("Riss Werkstoff", new() { ["A"] = "laengs", ["B"] = "radial", ["C"] = "komplex", ["D"] = "spiralfoermig" }),
                    ["Z"] = C("andersartig")
                }
            },
            ["BAM"] = new()
            {
                Label = "Schweissnaht",
                Char1 = new() { ["A"] = C("laengs"), ["B"] = C("radial"), ["C"] = C("spiralfoermig") }
            },
            ["BAN"] = new() { Label = "Poroese Leitung", FinalCode = "BAN" },
            ["BAO"] = new() { Label = "Boden sichtbar", FinalCode = "BAO" },
            ["BAP"] = new() { Label = "Hohlraum sichtbar", FinalCode = "BAP" },
        }),

        ["BB"] = new("Betrieblicher Zustand", "#F59E0B", "\u2699\uFE0F", new()
        {
            ["BBA"] = new()
            {
                Label = "Wurzeln",
                Char1 = new() { ["A"] = C("Pfahlwurzel"), ["B"] = C("feiner Einwuchs"), ["C"] = C("komplexes Wurzelwerk") }
            },
            ["BBB"] = new()
            {
                Label = "Anhaftende Stoffe",
                Char1 = new() { ["A"] = C("Inkrustation"), ["B"] = C("Fett"), ["C"] = C("Faeulnis"), ["Z"] = C("andersartig") }
            },
            ["BBC"] = new()
            {
                Label = "Ablagerungen Sohle",
                Char1 = new() { ["A"] = C("Sand"), ["B"] = C("Kies"), ["C"] = C("Hart"), ["Z"] = C("andersartig") }
            },
            ["BBD"] = new()
            {
                Label = "Eindringen Boden",
                Char1 = new() { ["A"] = C("Sand"), ["B"] = C("Humus"), ["C"] = C("Fein"), ["D"] = C("Grob"), ["Z"] = C("andersartig") }
            },
            ["BBE"] = new()
            {
                Label = "Andere Hindernisse",
                Char1 = new()
                {
                    ["A"] = C("Backsteine"), ["B"] = C("Leitungsstueck"), ["C"] = C("Gegenst. Sohle"),
                    ["D"] = C("durch Wand"), ["E"] = C("in Verb."), ["F"] = C("aus Anschluss"),
                    ["G"] = C("Werkltg."), ["H"] = C("eingebaut"), ["Z"] = C("andersartig")
                }
            },
            ["BBF"] = new()
            {
                Label = "Infiltration",
                Char1 = new() { ["A"] = C("Schwitzen/Kalk"), ["B"] = C("tropft"), ["C"] = C("fliesst"), ["D"] = C("spritzt") }
            },
            ["BBG"] = new() { Label = "Exfiltration", FinalCode = "BBG" },
            ["BBH"] = new()
            {
                Label = "Ungeziefer",
                Char1 = new() { ["A"] = C("Ratte"), ["B"] = C("Kakerlake"), ["Z"] = C("Tiere") },
                Char2 = new() { ["A"] = "in Leitung", ["B"] = "im Anschluss", ["C"] = "in Verbindung", ["Z"] = "andere" },
                AllValid = true
            },
        }),

        ["BC"] = new("Bestandsaufnahme", "#2563EB", "\U0001F4CB", new()
        {
            ["BCA"] = new()
            {
                Label = "Seitl. Anschluss",
                Char1 = new()
                {
                    ["A"] = C("Formstueck"), ["B"] = C("Sattel geb."), ["C"] = C("Sattel eingesp."),
                    ["D"] = C("gebohrt"), ["E"] = C("eingespitzt"), ["F"] = C("Spezial"),
                    ["G"] = C("unbekannt"), ["Z"] = C("andersartig")
                },
                Char2 = new() { ["A"] = "Offen", ["B"] = "Verschlossen" },
                AllValid = true
            },
            ["BCB"] = new()
            {
                Label = "Punkt. Reparatur",
                Char1 = new()
                {
                    ["A"] = C("Rohr ausget."), ["B"] = C("Innenauskleid."), ["C"] = C("Moertelinjiz."),
                    ["D"] = C("Injizierung"), ["E"] = C("Loch rep."), ["F"] = C("Auskl. Anschl."),
                    ["G"] = C("andere Rep."), ["Z"] = C("andersartig")
                }
            },
            ["BCC"] = new()
            {
                Label = "Bogen",
                Char1 = new() { ["A"] = C("links"), ["B"] = C("rechts"), ["Y"] = C("vertikal") },
                Char2 = new() { ["A"] = "oben", ["B"] = "unten", ["Y"] = "ohne Hoehe" },
                Invalid = new() { ["Y"] = new() { "Y" } }
            },
            ["BCD"] = new() { Label = "Anfangsknoten", FinalCode = "BCD", IsSteuer = true },
            ["BCE"] = new() { Label = "Endknoten", FinalCode = "BCE", IsSteuer = true },
        }),

        ["BD"] = new("Weitere Codes", "#7C3AED", "\U0001F4CC", new()
        {
            ["BDA"] = new() { Label = "Allg. Foto", FinalCode = "BDA" },
            ["BDB"] = new() { Label = "Allg. Anmerkung", FinalCode = "BDB", IsSteuer = true },
            ["BDC"] = new()
            {
                Label = "Abbruch",
                Char1 = new() { ["A"] = C("Hindernis"), ["B"] = C("Wasserstand"), ["C"] = C("Ausruestung"), ["Z"] = C("anderer Grund") },
                Char2 = new() { ["A"] = "Ziel erreicht", ["B"] = "AG verzichtet", ["C"] = "Gegens. err.", ["D"] = "Gegens. nicht", ["E"] = "unklar", ["Z"] = "Anmerkung" },
                AllValid = true,
                IsSteuer = true
            },
            ["BDD"] = new()
            {
                Label = "Wasserspiegel",
                Char1 = new() { ["A"] = C("klar"), ["C"] = C("trueb"), ["D"] = C("gefaerbt"), ["E"] = C("trueb+gefaerbt") }
            },
            ["BDE"] = new()
            {
                Label = "Zufluss/Fehlanschl.",
                Char1 = new() { ["A"] = C("klar"), ["C"] = C("trueb"), ["D"] = C("gefaerbt"), ["E"] = C("trueb+gef."), ["Y"] = C("o.Angabe") },
                Char2 = new() { ["A"] = "SM->RW", ["B"] = "RW->SM", ["C"] = "Zufluss", ["Y"] = "o.Details" }
            },
            ["BDF"] = new()
            {
                Label = "Gefaehrl. Atmosphaere",
                Char1 = new() { ["A"] = C("O2-Mangel"), ["B"] = C("H2S"), ["C"] = C("Methan"), ["Z"] = C("andersartig") }
            },
            ["BDG"] = new()
            {
                Label = "Keine Sicht",
                Char1 = new() { ["A"] = C("unter Wasser"), ["B"] = C("Verschlammung"), ["C"] = C("Dampf"), ["Z"] = C("anderer Grund") }
            },
        }),

        ["AE"] = new("Aenderungen", "#16A34A", "\U0001F504", new()
        {
            ["AEC"] = new()
            {
                Label = "Rohrprofil/DN",
                XPrefix = true,
                Char1 = new()
                {
                    ["A"] = C("unbek."), ["B"] = C("Ei"), ["C"] = C("Kreis"), ["D"] = C("Maul"),
                    ["E"] = C("offen"), ["F"] = C("Rechteck"), ["G"] = C("Spezial"), ["H"] = C("unbekannt")
                }
            },
            ["AED"] = new()
            {
                Label = "Rohrmaterial",
                XPrefix = true,
                Char1 = new()
                {
                    ["A"] = C("unbek."), ["B"] = C("AZ"), ["C"] = C("NB"), ["D"] = C("OB"),
                    ["E"] = C("PR"), ["F"] = C("SB"), ["G"] = C("BE"), ["H"] = C("FZ"),
                    ["I"] = C("GS"), ["J"] = C("DG"), ["K"] = C("GG"), ["L"] = C("EP"),
                    ["M"] = C("HDPE"), ["N"] = C("GUP"), ["O"] = C("PE"), ["P"] = C("PP"),
                    ["Q"] = C("PVC"), ["R"] = C("KS"), ["S"] = C("ST"), ["T"] = C("RS"),
                    ["U"] = C("SZ"), ["V"] = C("TO"), ["W"] = C("?"), ["X"] = C("ZE")
                }
            },
            ["AEF"] = new() { Label = "Baulaenge", FinalCode = "AEF" },
        }),
    };

    // ═══════════════════════════════════════════════════════════════
    // QUANT_RULES
    // ═══════════════════════════════════════════════════════════════

    public static readonly Dictionary<string, QuantRule> QuantRules = new()
    {
        ["BAA"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Reduzierung", Min = 0, Max = 100 } },
        ["BAB"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = null,
                ["B"] = new() { Pflicht = "P", Einheit = "mm", Label = "Rissbreite" },
                ["C"] = new() { Pflicht = "P", Einheit = "mm", Label = "Rissbreite", Hint = ">=5mm" }
            }
        },
        ["BAC"] = new() { Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "Bruchlaenge", Hint = ">1000 = Strecke" } },
        ["BAD"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = null, ["B"] = null, ["D"] = null,
                ["C"] = new() { Pflicht = "P", Einheit = "mm", Label = "Absackung", Min = 20, Hint = ">=20mm" }
            }
        },
        ["BAE"] = new() { Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "Tiefe Moertel", Min = 5, Hint = ">=5mm" } },
        ["BAF"] = new() { Q1 = null },
        ["BAG"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Einragung (%DN)", Min = 0, Max = 100 } },
        ["BAH"] = new() { Q1 = null },
        ["BAI"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = null,
                ["Z"] = new() { Pflicht = "P", Einheit = "%", Label = "QS-Minderung", Min = 0, Max = 100 }
            }
        },
        ["BAJ"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = new() { Pflicht = "P", Einheit = "mm", Label = "Abstand", Min = 15, Hint = ">=15mm" },
                ["B"] = new() { Pflicht = "P", Einheit = "mm", Label = "Versatz" },
                ["C"] = new() { Pflicht = "P", Einheit = "°", Label = "Achsabweichung", Min = 0, Max = 360 }
            }
        },
        ["BAK"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = new() { Pflicht = "P", Einheit = "%", Label = "QS-Verringerung", Max = 100 },
                ["D"] = new() { Pflicht = "P", Einheit = "%", Label = "QS-Verringerung", Max = 100 },
                ["E"] = new() { Pflicht = "P", Einheit = "%", Label = "QS-Verringerung", Max = 100 },
                ["F"] = new() { Pflicht = "P", Einheit = "mm", Label = "Tiefe Beule" },
                ["I"] = new() { Pflicht = "P", Einheit = "mm", Label = "Rissbreite" },
                ["J"] = new() { Pflicht = "P", Einheit = "mm", Label = "Lochlaenge" }
            }
        },
        ["BAL"] = new() { Q1 = null }, ["BAM"] = new() { Q1 = null },
        ["BAN"] = new() { Q1 = null }, ["BAO"] = new() { Q1 = null }, ["BAP"] = new() { Q1 = null },
        ["BBA"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "QS-Verminderung", Max = 100 } },
        ["BBB"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "QS-Verminderung", Max = 100 } },
        ["BBC"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Hoehe (%LH)", Max = 100 } },
        ["BBD"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "QS-Minderung", Max = 100 } },
        ["BBE"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "QS-Minderung", Max = 100 } },
        ["BBF"] = new() { Q1 = null }, ["BBG"] = new() { Q1 = null },
        ["BBH"] = new() { Q1 = new() { Pflicht = "P", Einheit = "Stk", Label = "Anzahl Tiere", Min = 1 } },
        ["BCA"] = new()
        {
            Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "Hoehe Anschluss" },
            Q2 = new() { Pflicht = "O", Einheit = "mm", Label = "Breite (!=Hoehe)" }
        },
        ["BCB"] = new() { Q1 = null },
        ["BCC"] = new() { Q1 = new() { Pflicht = "P", Einheit = "°", Label = "Richtungsaenderung", Min = 0, Max = 360 } },
        ["BCD"] = new() { Q1 = null }, ["BCE"] = new() { Q1 = null },
        ["BDA"] = new() { Q1 = null }, ["BDB"] = new() { Q1 = null }, ["BDC"] = new() { Q1 = null },
        ["BDD"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Wasserspiegel (%lH)", Max = 100 } },
        ["BDE"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "WS Anschl. (%)", Max = 100 } },
        ["BDF"] = new() { Q1 = null }, ["BDG"] = new() { Q1 = null },
        ["AEC"] = new()
        {
            Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "Hoehe neues Profil" },
            Q2 = new() { Pflicht = "O", Einheit = "mm", Label = "Breite (!=Hoehe)" }
        },
        ["AED"] = new() { Q1 = null },
        ["AEF"] = new() { Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "Neue Baulaenge" } },
    };

    // ═══════════════════════════════════════════════════════════════
    // CLOCK_RULES
    // ═══════════════════════════════════════════════════════════════

    public static readonly Dictionary<string, ClockRule> ClockRules = new()
    {
        ["BAJ"] = new() { Mode = "single", Hint = "Richtung des Versatzes" },
        ["BCA"] = new() { Mode = "single", Hint = "Anschlussmitte" },
        ["BAG"] = new() { Mode = "single", Hint = "Anschlussmitte" },
        ["BAH"] = new() { Mode = "single", Hint = "Anschlussmitte" },
        ["BCD"] = new() { Mode = "none" }, ["BCE"] = new() { Mode = "none" },
        ["BDA"] = new() { Mode = "none" }, ["BDB"] = new() { Mode = "none" },
        ["BDC"] = new() { Mode = "none" }, ["BDF"] = new() { Mode = "none" },
        ["AEC"] = new() { Mode = "none" }, ["AED"] = new() { Mode = "none" }, ["AEF"] = new() { Mode = "none" },
    };

    public static readonly ClockRule DefaultClockRule = new() { Mode = "range", Hint = "Lage am Umfang (Uhrzeiger)" };

    // ═══════════════════════════════════════════════════════════════
    // Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Ermittelt die effektive Q1-Regel fuer einen Code + Char1.</summary>
    public static (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? c1Key)
    {
        if (!QuantRules.TryGetValue(codeKey, out var rule))
            return (null, null);

        var q1 = rule.Q1;
        if (q1 is { Pflicht: "V" } && rule.Q1PerChar1 is not null && c1Key is not null)
        {
            q1 = rule.Q1PerChar1.TryGetValue(c1Key, out var perChar) ? perChar : null;
        }

        return (q1, rule.Q2);
    }

    /// <summary>Ermittelt die Uhrzeiger-Regel fuer einen Code.</summary>
    public static ClockRule GetClockRule(string codeKey)
    {
        return ClockRules.TryGetValue(codeKey, out var rule) ? rule : DefaultClockRule;
    }

    /// <summary>Ermittelt die Char2-Optionen fuer einen Code + Char1.</summary>
    public static Dictionary<string, string>? GetChar2Options(VsaCodeDef cd, string c1)
    {
        if (cd.Char2PerChar1 is not null)
            return cd.Char2PerChar1.TryGetValue(c1, out var c2) ? c2 : null;

        if (cd.Char2 is not null)
            return cd.Char2;

        if (cd.Char1 is not null && cd.Char1.TryGetValue(c1, out var charDef) && charDef.Char2 is not null)
            return charDef.Char2;

        return null;
    }

    /// <summary>Prueft ob eine Char1×Char2 Kombination ungueltig ist.</summary>
    public static bool IsInvalidCombo(VsaCodeDef cd, string c1, string c2)
    {
        if (cd.AllValid) return false;
        return cd.Invalid is not null
            && cd.Invalid.TryGetValue(c1, out var set)
            && set.Contains(c2);
    }
}
