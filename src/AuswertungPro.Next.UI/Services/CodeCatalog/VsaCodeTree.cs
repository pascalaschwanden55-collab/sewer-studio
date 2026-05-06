using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Services.CodeCatalog;

// ═══════════════════════════════════════════════════════════════
// Statischer VSA-Code-Baum (EN 13508-2 / VSA-KEK 2023)
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
    /// <summary>Markiert das aktuell gewaehlte Element in der Multi-Column Ansicht.</summary>
    public bool IsSelected { get; init; }
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
                    ["H"] = new() { "A", "Z" },  // WinCan: nur B,C,D,E gueltig (kein A, kein Z)
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
            ["BCD"] = new() { Label = "Rohranfang", FinalCode = "BCD", IsSteuer = true },
            ["BCE"] = new() { Label = "Rohrende", FinalCode = "BCE", IsSteuer = true },
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
                // WinCan: Char1 A,C,D,E → Char2 {A,B,C}; Char1 Y → Char2 {A,B,Y}
                Char2PerChar1 = new()
                {
                    ["A"] = new() { ["A"] = "SM->RW", ["B"] = "RW->SM", ["C"] = "Zufluss" },
                    ["C"] = new() { ["A"] = "SM->RW", ["B"] = "RW->SM", ["C"] = "Zufluss" },
                    ["D"] = new() { ["A"] = "SM->RW", ["B"] = "RW->SM", ["C"] = "Zufluss" },
                    ["E"] = new() { ["A"] = "SM->RW", ["B"] = "RW->SM", ["C"] = "Zufluss" },
                    ["Y"] = new() { ["A"] = "SM->RW", ["B"] = "RW->SM", ["Y"] = "o.Details" }
                }
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
                Label = "Rohrprofilwechsel",
                XPrefix = true,
                // Langtexte laut offizieller VSA-KEK-Richtlinie
                Char1 = new()
                {
                    ["A"] = C("unbekannt"),
                    ["B"] = C("Eiprofil"),
                    ["C"] = C("Kreisprofil"),
                    ["D"] = C("Maulprofil"),
                    ["E"] = C("offenes Profil"),
                    ["F"] = C("Rechteckprofil"),
                    ["G"] = C("Spezialprofil"),
                    ["H"] = C("unbekanntes Profil")
                }
            },
            ["AED"] = new()
            {
                Label = "Rohrmaterialwechsel",
                XPrefix = true,
                // Langtexte laut offizieller VSA-KEK-Richtlinie (SchadencodierungDatentransfer 2018/2023)
                Char1 = new()
                {
                    ["A"] = C("unbekannt (Anmerkung Pflicht)"),
                    ["B"] = C("Asbestzement"),
                    ["C"] = C("Normalbeton"),
                    ["D"] = C("Ortsbeton"),
                    ["E"] = C("Pressrohrbeton"),
                    ["F"] = C("Spezialbeton"),
                    ["G"] = C("Beton"),
                    ["H"] = C("Faserzement"),
                    ["I"] = C("Gebrannte Steine"),
                    ["J"] = C("Duktiler Guss"),
                    ["K"] = C("Grauguss"),
                    ["L"] = C("Epoxidharz"),
                    ["M"] = C("Hartpolyethylen"),
                    ["N"] = C("Polyester GUP"),
                    ["O"] = C("Polyethylen"),
                    ["P"] = C("Polypropylen"),
                    ["Q"] = C("Polyvinylchlorid"),
                    ["R"] = C("Kunststoff unbekannt"),
                    ["S"] = C("Stahl"),
                    ["T"] = C("Rostfreier Stahl"),
                    ["U"] = C("Steinzeug"),
                    ["V"] = C("Ton"),
                    ["W"] = C("unbekanntes Material"),
                    ["X"] = C("Zement")
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

    /// <summary>
    /// Baut die offizielle Bezeichnung fuer einen VSA-Code auf.
    /// Beispiel: "BABA" → "Risse, Haarriss, laengs"
    ///           "BCD"  → "Rohranfang"
    ///           "???"  → null
    /// </summary>
    /// <summary>
    /// Liefert die Einheit ("mm", "%", "°", ...) fuer den Quantifizierungs-Slot
    /// (index=1 → Q1, index=2 → Q2) eines VSA-Codes.
    /// Grundlage ist der <see cref="QuantRules"/>-Katalog. Bei Q1PerChar1 wird das
    /// Char1-Zeichen aus dem vollen Code extrahiert (z.B. "BAB" + "A" → BABA).
    /// Gibt null zurueck wenn keine Quantifizierung definiert ist.
    /// </summary>
    public static string? GetQuantificationUnit(string code, int index = 1)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3) return null;
        if (index != 1 && index != 2) return null;

        var normalized = code.Replace(".", "", StringComparison.Ordinal).ToUpperInvariant();
        var main = normalized[..3];
        if (!QuantRules.TryGetValue(main, out var rule) || rule is null) return null;

        if (index == 2) return rule.Q2?.Einheit;

        // index == 1: entweder fixes Q1 oder per-Char1
        if (rule.Q1 is not null && rule.Q1.Einheit is not null)
            return rule.Q1.Einheit;

        if (rule.Q1PerChar1 is not null)
        {
            // Char1-Position im Code ermitteln (XPrefix = 'X' an Pos 4 → Char1 an 5)
            int c1Offset = 3;
            if (Groups.TryGetValue(main[..2], out var group)
                && group.Codes.TryGetValue(main, out var mainDef)
                && mainDef.XPrefix
                && normalized.Length > 4
                && normalized[3] == 'X')
            {
                c1Offset = 4;
            }
            if (normalized.Length > c1Offset)
            {
                var c1 = normalized[c1Offset].ToString();
                if (rule.Q1PerChar1.TryGetValue(c1, out var q) && q is not null)
                    return q.Einheit;
            }
        }

        return null;
    }

    public static string? LookupLabel(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 2) return null;

        var groupKey = code[..2]; // z.B. "BA", "BC", "BD"
        if (!Groups.TryGetValue(groupKey, out var group)) return null;

        // 2-Zeichen-Code = Gruppenname
        if (code.Length == 2) return group.Label;

        // Hauptcode (3 Zeichen): z.B. "BAB"
        var mainKey = code[..3];
        if (!group.Codes.TryGetValue(mainKey, out var mainDef)) return null;

        if (code.Length == 3) return mainDef.Label;

        // XPrefix-Codes (AEC, AED): offiziell AECXC, AEDXV etc.
        // Wenn XPrefix gesetzt, muss Zeichen 4 ein 'X' sein.
        // "AEDV" ist ungueltig → nur "AEDXV" ist korrekt.
        int c1Offset = 3;
        if (mainDef.XPrefix)
        {
            if (code.Length < 5 || code[3] != 'X')
                return null; // Ungueltig: XPrefix fehlt
            c1Offset = 4; // Char1 ist an Position 5 (Index 4)
        }

        // Char1
        var parts = new List<string> { mainDef.Label };
        var c1Key = code[c1Offset].ToString();
        string? c1Label = null;
        if (mainDef.Char1 != null && mainDef.Char1.TryGetValue(c1Key, out var c1Def))
            c1Label = c1Def.Label;

        if (c1Label != null) parts.Add(c1Label);

        // Char2
        int c2Offset = c1Offset + 1;
        if (code.Length > c2Offset)
        {
            var c2Key = code[c2Offset].ToString();
            string? c2Label = null;

            // Char2 pro Char1 zuerst pruefen
            if (mainDef.Char2PerChar1 != null
                && mainDef.Char2PerChar1.TryGetValue(c1Key, out var perC1)
                && perC1.TryGetValue(c2Key, out var label))
                c2Label = label;
            // Dann CharDef-eigenes Char2
            else if (c1Label != null && mainDef.Char1 != null
                && mainDef.Char1.TryGetValue(c1Key, out var charDef)
                && charDef.Char2 != null
                && charDef.Char2.TryGetValue(c2Key, out var cLabel))
                c2Label = cLabel;
            // Dann globales Char2
            else if (mainDef.Char2 != null && mainDef.Char2.TryGetValue(c2Key, out var gLabel))
                c2Label = gLabel;

            if (c2Label != null) parts.Add(c2Label);
        }

        return string.Join(", ", parts);
    }

    // ── Reverse-Lookup: Langtext → VSA-Code ──────────────────────────

    private static Dictionary<string, string>? _reverseLookup;
    private static readonly object _reverseLock = new();

    /// <summary>
    /// Sucht den VSA-Code zu einem Langtext aus dem Protokoll.
    /// Beispiel: "Rohranfang" → "BCD", "Bogen nach links" → "BCCAY",
    ///           "Riss laengs" → "BABBA", "Inkrustation (verkalkt)" → "BBBA"
    ///
    /// Unterstuetzt:
    ///   - Exakte Treffer (case-insensitive)
    ///   - Zusammengesetzte Langtexte aus Hauptcode + Char1 + Char2
    ///     z.B. "Bogen nach links" = BCC.Label("Bogen") + Char1.A.Label("links") + Char2.Y("ohne Hoehe") → BCCAY
    ///   - Haeufige Protokoll-Varianten (manuelles Mapping fuer Faelle die der Tree nicht direkt abdeckt)
    /// </summary>
    public static string? ReverseLookup(string langtext)
    {
        if (string.IsNullOrWhiteSpace(langtext)) return null;

        var lookup = GetReverseLookup();
        var key = NormalizeForLookup(langtext);

        // Exakter Treffer
        if (lookup.TryGetValue(key, out var code))
            return code;

        // Teilmatch: laengster passender Praefix
        // z.B. "Bogen nach links oben 45°" → "Bogen nach links oben" → BCCAA
        string? bestCode = null;
        int bestLen = 0;
        foreach (var (k, v) in lookup)
        {
            if (key.StartsWith(k) && k.Length > bestLen)
            {
                bestLen = k.Length;
                bestCode = v;
            }
        }

        return bestCode;
    }

    /// <summary>
    /// Baut die Reverse-Lookup-Tabelle aus dem VsaCodeTree.
    /// Wird einmalig generiert und gecacht.
    /// </summary>
    private static Dictionary<string, string> GetReverseLookup()
    {
        if (_reverseLookup is not null)
            return _reverseLookup;

        lock (_reverseLock)
        {
            if (_reverseLookup is not null)
                return _reverseLookup;

            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            // Haeufige Protokoll-Langtexte die nicht 1:1 aus dem Tree kommen
            AddManualMappings(map);

            // Automatisch aus Tree generieren
            foreach (var (groupKey, group) in Groups)
            {
                foreach (var (mainKey, mainDef) in group.Codes)
                {
                    // Hauptcode-Label: "Risse" → BAB, "Rohranfang" → BCD
                    Add(map, mainDef.Label, mainDef.FinalCode ?? mainKey);

                    if (mainDef.Char1 == null) continue;

                    foreach (var (c1Key, c1Def) in mainDef.Char1)
                    {
                        var code4 = mainKey + c1Key;
                        // "Risse, Haarriss" → BABA
                        Add(map, $"{mainDef.Label} {c1Def.Label}", code4);
                        // Nur Char1-Label: "Haarriss" → BABA (wenn eindeutig)
                        Add(map, c1Def.Label, code4);

                        // Char2 aus verschiedenen Quellen
                        var char2Sources = new List<Dictionary<string, string>?>();
                        if (c1Def.Char2 != null) char2Sources.Add(c1Def.Char2);
                        if (mainDef.Char2PerChar1 != null && mainDef.Char2PerChar1.TryGetValue(c1Key, out var perC1))
                            char2Sources.Add(perC1);
                        if (mainDef.Char2 != null) char2Sources.Add(mainDef.Char2);

                        foreach (var c2Dict in char2Sources)
                        {
                            if (c2Dict == null) continue;
                            foreach (var (c2Key, c2Label) in c2Dict)
                            {
                                var code5 = code4 + c2Key;
                                // Voller Langtext: "Risse, Haarriss, laengs" → BABAA
                                Add(map, $"{mainDef.Label} {c1Def.Label} {c2Label}", code5);
                                // Verkuerzt: "Haarriss laengs" → BABAA
                                Add(map, $"{c1Def.Label} {c2Label}", code5);
                                // Haupt + Char2: "Risse laengs" → BABA? (nur wenn eindeutig)
                                Add(map, $"{mainDef.Label} {c2Label}", code4 + c2Key);
                            }
                        }
                    }
                }
            }

            _reverseLookup = map;
            return map;
        }
    }

    /// <summary>Manuelle Mappings fuer haeufige Protokoll-Formulierungen.</summary>
    private static void AddManualMappings(Dictionary<string, string> map)
    {
        // Steuercodes
        Add(map, "Rohranfang", "BCD");
        Add(map, "Rohrende", "BCE");
        Add(map, "Beginn TV-Untersuchung", "BDB");
        Add(map, "Beginn TV-Untersuchung (Vorgabe)", "BDB");
        Add(map, "Beginn TV-Untersuch (Vorgabe)", "BDB");
        Add(map, "Beginn der Untersuchung", "BDB");
        Add(map, "Abbruch der Inspektion", "BDC");
        Add(map, "Distanzmessung Anfang", "BCDXP");
        Add(map, "Distanzmessung Ende", "BCEXP");

        // Bogen-Varianten (haeufig in Protokollen)
        Add(map, "Bogen nach links", "BCCAY");
        Add(map, "Bogen nach rechts", "BCCBY");
        Add(map, "Bogen nach links oben", "BCCAA");
        Add(map, "Bogen nach links unten", "BCCAB");
        Add(map, "Bogen nach rechts oben", "BCCBA");
        Add(map, "Bogen nach rechts unten", "BCCBB");
        Add(map, "Bogen nach oben", "BCCYA");
        Add(map, "Bogen nach unten", "BCCYB");
        Add(map, "Richtungsaenderung nach links", "BCCAY");
        Add(map, "Richtungsaenderung nach rechts", "BCCBY");
        Add(map, "Richtungsaenderung nach oben", "BCCYA");
        Add(map, "Richtungsaenderung nach unten", "BCCYB");

        // Anschluss-Varianten
        Add(map, "Anschluss mit Formstueck", "BCAAA");
        Add(map, "Anschluss mit Formstück", "BCAAA");
        Add(map, "Sattelanschluss gebohrt", "BCABA");
        Add(map, "Sattelanschluss eingespitzt", "BCACA");
        Add(map, "Anschluss gebohrt", "BCADA");
        Add(map, "Anschluss eingespitzt", "BCAEA");
        Add(map, "Spezialanschluss", "BCAFA");
        Add(map, "Anschluss unbekannter Bauart", "BCAGA");
        Add(map, "Andersartiger Anschluss", "BCAZA");

        // Allgemein-Codes
        Add(map, "Allgemeinzustand Fotobeispiel", "BDA");
        Add(map, "Pos. Allgemeinzustand Fotobeispiel", "BDA");
        Add(map, "Allgemeinzustand", "BDA");
        Add(map, "Wasserspiegel", "BDD");

        // Riss-Varianten
        Add(map, "Haarriss laengs", "BABAA");
        Add(map, "Haarriss radial", "BABAB");
        Add(map, "Riss laengs", "BABBA");
        Add(map, "Riss längs", "BABBA");
        Add(map, "Riss radial", "BABBB");
        Add(map, "Klaffender Riss laengs", "BABCA");
        Add(map, "Klaffender Riss radial", "BABCB");

        // Betriebliche Feststellungen
        Add(map, "Inkrustation (verkalkt)", "BBBA");
        Add(map, "Inkrustation", "BBB");
        Add(map, "Harte Ablagerungen", "BBCC");
        Add(map, "Lose Ablagerungen Sand", "BBCA");
        Add(map, "Lose Ablagerungen Kies", "BBCB");
        Add(map, "Wurzeleinwuchs", "BBA");
        Add(map, "Komplexes Wurzelwerk", "BBAC");
        Add(map, "Pfahlwurzel", "BBAA");

        // Material-Aenderungen
        Add(map, "Rohrmaterialwechsel: Polypropylen", "AEDXP");
        Add(map, "Rohrmaterialwechsel: Polyethylen", "AEDXO");
        Add(map, "Rohrmaterialwechsel: Polyvinylchlorid", "AEDXQ");
        Add(map, "Rohrmaterialwechsel: Beton", "AEDXG");
        Add(map, "Rohrmaterialwechsel: Steinzeug", "AEDXU");
        Add(map, "Rohrmaterialwechsel: Kunststoff unbekannt", "AEDXR");
        Add(map, "Neue Laenge einzelnes Rohr", "AEF");
        Add(map, "Neue Länge einzelnes Rohr", "AEF");
        Add(map, "Rohrprofilwechsel: Kreisprofil", "AECXC");
        Add(map, "Rohrprofilwechsel: Eiprofil", "AECXB");
    }

    private static void Add(Dictionary<string, string> map, string langtext, string code)
    {
        var key = NormalizeForLookup(langtext);
        map.TryAdd(key, code); // Erster Eintrag gewinnt (spezifischste Mappings zuerst)
    }

    private static string NormalizeForLookup(string text)
    {
        // Kleinbuchstaben, Umlaute normalisieren, Satzzeichen/Klammern entfernen, Mehrfach-Spaces
        var s = text.Trim().ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
            .Replace("ß", "ss")
            .Replace("(", "").Replace(")", "")
            .Replace(",", " ").Replace(".", " ").Replace("/", " ")
            .Replace("-", " ").Replace(":", " ");
        // Mehrfach-Spaces entfernen
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s.Trim();
    }

    /// <summary>
    /// Prueft ob ein VSA-Code typischerweise ein Streckenschaden ist (requiresRange laut Katalog).
    /// Typisch fuer: Risse laengs (BABA/BABAB), Korrosion (BAFA), Wurzeln (BBA),
    /// Anhaftungen/Inkrustation (BBB), Ablagerungen (BBC), Bodeneindringung (BBD), Infiltration (BBF) etc.
    /// </summary>
    private static readonly HashSet<string> StreckenschadenCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // BA: Bauliche Schaeden — laengs-Varianten
        "BABA",   // Risse - laengs (Haarriss)
        "BABAB",  // Oberflächenriss radial (laengs)
        "BABAC",  // Komplexe Rissbildung (laengs)
        "BABB",   // Risse - Riss (laengs)
        "BABBA",  // Risse - Riss laengs
        "BABBB",  // Risse - Riss radial (laengs)
        "BABBC",  // Risse - Riss, komplexe Rissbildung (laengs)
        "BABC",   // Risse - Bruch/Einsturz (laengs)
        "BABCA",  // Bruch/Einsturz laengs
        "BAFA",   // Oberflaechenschaden - Rauhigkeit
        "BAFAE",  // Oberflaechenschaden - Rauhigkeit erhoehte
        "BAFB",   // Oberflaechenschaden - Korrosion/Erosion
        "BAFC",   // Oberflaechenschaden - Sichtbare Bewehrung
        "BAFD",   // Oberflaechenschaden - Fehlstelle Beschichtung
        // BB: Betriebliche Schaeden
        "BBA",    // Wurzeln
        "BBB",    // Anhaftende Stoffe / Inkrustation
        "BBC",    // Ablagerungen
        "BBD",    // Eindringender Boden
        "BBF",    // Infiltration
    };

    public static bool IsStreckenschadenCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        // Exakter Match
        if (StreckenschadenCodes.Contains(code)) return true;
        // Prefix-Match: z.B. "BABBA" matched wenn "BABB" ein Streckenschaden ist
        for (int len = code.Length - 1; len >= 3; len--)
        {
            if (StreckenschadenCodes.Contains(code[..len])) return true;
        }
        return false;
    }
}
