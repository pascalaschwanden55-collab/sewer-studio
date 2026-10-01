using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.VsaCatalog;

// ═══════════════════════════════════════════════════════════════
// Statischer VSA-Code-Baum (EN 13508-2 / VSA-KEK 2018)
// Hierarchie: Gruppe → Hauptcode → Char1 → Char2
// ═══════════════════════════════════════════════════════════════

public static class VsaCodeTree
{
    // Hilfsfunktion: Einfache Char1-Definition (nur Label, kein eigenes Char2)
    private static CharDef C(string label) => new() { Label = label };
    private static CharDef C(string label, Dictionary<string, string> char2) => new() { Label = label, Char2 = char2 };

    public static readonly Dictionary<string, GroupDef> Groups = new()
    {
        ["BA"] = new("Struktur der Rohrleitungen", "#DC2626", "\U0001F527", new()
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
            ["BAG"] = new() { Label = "Einragender Anschluss", FinalCode = "BAGA" },
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

        ["BB"] = new("Betrieb der Rohrleitungen", "#F59E0B", "\u2699\uFE0F", new()
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

        ["BC"] = new("Bestandsaufnahme der Rohrleitungen", "#2563EB", "\U0001F4CB", new()
        {
            ["BCA"] = new()
            {
                Label = "Seitl. Anschluss",
                Char1 = new()
                {
                    ["A"] = C("Anschluss mit Formst\u00fcck"),
                    ["B"] = C("Sattelanschluss gebohrt"),
                    ["C"] = C("Sattelanschluss eingespitzt"),
                    ["D"] = C("Anschluss gebohrt"),
                    ["E"] = C("Anschluss eingespitzt"),
                    ["F"] = C("Spezialanschluss"),
                    ["G"] = C("Anschluss unbekannter Bauart"),
                    ["Z"] = C("Andersartiger Anschluss")
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
                Char1 = new()
                {
                    ["A"] = C("Bogen nach links"),
                    ["B"] = C("Bogen nach rechts"),
                    ["Y"] = C("Bogen vertikal")
                },
                Char2 = new() { ["A"] = "oben", ["B"] = "unten", ["Y"] = "ohne H\u00f6he" },
                Invalid = new() { ["Y"] = new() { "Y" } }
            },
            ["BCD"] = new() { Label = "Rohranfang", FinalCode = "BCD", IsSteuer = true },
            ["BCE"] = new() { Label = "Rohrende", FinalCode = "BCE", IsSteuer = true },
        }),

        ["BD"] = new("Sonstiges Rohrleitungen", "#7C3AED", "\U0001F4CC", new()
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

        ["AE"] = new("Änderungen der Grundlageninformationen", "#16A34A", "\U0001F504", new()
        {
            ["AEC"] = new()
            {
                Label = "Rohrprofil/DN",
                XPrefix = true,
                Char1 = new()
                {
                    ["A"] = C("unbek."), ["B"] = C("Ei"), ["C"] = C("Kreis"), ["D"] = C("Maul"),
                    ["E"] = C("offen"), ["F"] = C("Rechteck"), ["G"] = C("Spezial"), ["H"] = C("Trapez")
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

    // Kanal-Picker-Matrix: Einheiten und Eingabebereiche sind gegen den lokal
    // installierten WinCan-Katalog EN13508_VSA-2019_CH_DEU_SEC.xml abgeglichen.
    // Die Manifest-Whitelist entscheidet weiterhin, welche Endcodes auswählbar sind.
    public static readonly Dictionary<string, QuantRule> QuantRules = new()
    {
        ["BAA"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Verformung" } },
        ["BAB"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = null,
                ["B"] = new() { Pflicht = "P", Einheit = "mm", Label = "Rissbreite", Min = 1, Max = 200 },
                ["C"] = new() { Pflicht = "P", Einheit = "mm", Label = "Rissbreite", Min = 1, Max = 200 }
            }
        },
        ["BAC"] = new() { Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "Bruchl\u00e4nge", Min = 10, Max = 1000 } },
        ["BAD"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = null, ["B"] = null, ["D"] = null,
                ["C"] = new() { Pflicht = "P", Einheit = "mm", Label = "Absackung", Min = 20, Max = 1000 }
            }
        },
        ["BAE"] = new() { Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "M\u00f6rteltiefe", Min = 5, Max = 500 } },
        ["BAF"] = new() { Q1 = null },
        ["BAG"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Einragende L\u00e4nge bezogen auf DN/H\u00f6he", Min = 1, Max = 100 } },
        ["BAH"] = new() { Q1 = null },
        ["BAI"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = null,
                ["Z"] = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsminderung", Min = 1, Max = 100 }
            }
        },
        ["BAJ"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = new() { Pflicht = "P", Einheit = "mm", Label = "Verbindungsabstand", Min = 0, Max = 9999 },
                ["B"] = new() { Pflicht = "P", Einheit = "mm", Label = "Versatz", Min = 0, Max = 9999 },
                ["C"] = new() { Pflicht = "P", Einheit = "\u00b0", Label = "Achsabweichung", Min = 1, Max = 359 }
            }
        },
        ["BAK"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["A"] = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverringerung", Min = 0, Max = 100 },
                ["B"] = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverringerung", Min = 0, Max = 100 },
                ["C"] = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverringerung", Min = 0, Max = 100 },
                ["D"] = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverringerung", Min = 0, Max = 100 },
                ["E"] = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverringerung", Min = 0, Max = 100 },
                ["F"] = new() { Pflicht = "P", Einheit = "mm", Label = "Beulentiefe", Min = 0 },
                ["I"] = new() { Pflicht = "P", Einheit = "mm", Label = "Rissbreite", Min = 0 },
                ["J"] = new() { Pflicht = "P", Einheit = "mm", Label = "Lochl\u00e4nge", Min = 0 },
                ["Z"] = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverringerung", Min = 0, Max = 100 }
            }
        },
        ["BAL"] = new()
        {
            Q1 = new() { Pflicht = "V" },
            Q1PerChar1 = new()
            {
                ["G"] = new() { Pflicht = "P", Einheit = "mm", Label = "Rissbreite", Min = 0 }
            }
        },
        ["BAM"] = new() { Q1 = null },
        ["BAN"] = new() { Q1 = null }, ["BAO"] = new() { Q1 = null }, ["BAP"] = new() { Q1 = null },
        ["BBA"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverminderung", Min = 1, Max = 100 } },
        ["BBB"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverminderung", Min = 1, Max = 100 } },
        ["BBC"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Ablagerungsh\u00f6he", Min = 0, Max = 100 } },
        ["BBD"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverminderung", Min = 1, Max = 100 } },
        ["BBE"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Querschnittsverminderung", Min = 1, Max = 100 } },
        ["BBF"] = new() { Q1 = null }, ["BBG"] = new() { Q1 = null },
        ["BBH"] = new() { Q1 = new() { Pflicht = "P", Einheit = "Stk.", Label = "Anzahl Tiere", Min = 0, Max = 10000 } },
        ["BCA"] = new()
        {
            Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "Anschlussh\u00f6he", Min = 0, Max = 10000 },
            Q2 = new() { Pflicht = "O", Einheit = "mm", Label = "Anschlussbreite (falls abweichend)", Min = 0, Max = 10000 }
        },
        ["BCB"] = new() { Q1 = null },
        ["BCC"] = new() { Q1 = new() { Pflicht = "P", Einheit = "\u00b0", Label = "Richtungs\u00e4nderung", Min = 1, Max = 359 } },
        ["BCD"] = new() { Q1 = null }, ["BCE"] = new() { Q1 = null },
        ["BDA"] = new() { Q1 = null }, ["BDB"] = new() { Q1 = null }, ["BDC"] = new() { Q1 = null },
        ["BDD"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Wasserh\u00f6he", Min = 0, Max = 100 } },
        ["BDE"] = new() { Q1 = new() { Pflicht = "P", Einheit = "%", Label = "Wasserh\u00f6he am Anschluss", Min = 0, Max = 100 } },
        ["BDF"] = new() { Q1 = null }, ["BDG"] = new() { Q1 = null },
        ["AEC"] = new()
        {
            Q1 = new() { Pflicht = "P", Einheit = "mm", Label = "H\u00f6he des neuen Profils", Min = 0, Max = 4500 },
            Q2 = new() { Pflicht = "V" },
            Q2PerChar1 = new()
            {
                ["A"] = new() { Pflicht = "O", Einheit = "mm", Label = "Breite des neuen Profils", Min = 0, Max = 4500 },
                ["B"] = new() { Pflicht = "O", Einheit = "mm", Label = "Breite des neuen Profils", Min = 0, Max = 4500 },
                ["C"] = null,
                ["D"] = new() { Pflicht = "O", Einheit = "mm", Label = "Breite des neuen Profils", Min = 0, Max = 4500 },
                ["E"] = new() { Pflicht = "O", Einheit = "mm", Label = "Breite des neuen Profils", Min = 0, Max = 4500 },
                ["F"] = new() { Pflicht = "O", Einheit = "mm", Label = "Breite des neuen Profils", Min = 0, Max = 4500 },
                ["G"] = new() { Pflicht = "O", Einheit = "mm", Label = "Breite des neuen Profils", Min = 0, Max = 4500 },
                ["H"] = new() { Pflicht = "O", Einheit = "mm", Label = "Breite des neuen Profils", Min = 0, Max = 4500 }
            }
        },
        ["AED"] = new() { Q1 = null },
        ["AEF"] = new() { Q1 = null },
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
        ["BCC"] = new() { Mode = "none" },
        ["BCD"] = new() { Mode = "none" }, ["BCE"] = new() { Mode = "none" },
        ["BDA"] = new() { Mode = "none" }, ["BDB"] = new() { Mode = "none" },
        ["BDC"] = new() { Mode = "none" }, ["BDF"] = new() { Mode = "none" },
        ["AEC"] = new() { Mode = "none" }, ["AED"] = new() { Mode = "none" }, ["AEF"] = new() { Mode = "none" },
    };

    public static readonly ClockRule DefaultClockRule = new() { Mode = "range", Hint = "Lage am Umfang (Uhrzeiger)" };

    // ═══════════════════════════════════════════════════════════════
    // Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Ermittelt die effektive Q1-Regel fuer einen Code + Char1. Delegiert an <see cref="VsaCodeRuleResolver"/>.</summary>
    public static (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? c1Key)
        => VsaCodeRuleResolver.GetQuantRule(codeKey, c1Key);

    /// <summary>Ermittelt die Uhrzeiger-Regel fuer einen Code. Delegiert an <see cref="VsaCodeRuleResolver"/>.</summary>
    public static ClockRule GetClockRule(string codeKey)
        => VsaCodeRuleResolver.GetClockRule(codeKey);

    /// <summary>Ermittelt die Char2-Optionen fuer einen Code + Char1. Delegiert an <see cref="VsaCodeRuleResolver"/>.</summary>
    public static Dictionary<string, string>? GetChar2Options(VsaCodeDef cd, string c1)
        => VsaCodeRuleResolver.GetChar2Options(cd, c1);

    /// <summary>Prueft ob eine Char1×Char2 Kombination ungueltig ist. Delegiert an <see cref="VsaCodeRuleResolver"/>.</summary>
    public static bool IsInvalidCombo(VsaCodeDef cd, string c1, string c2)
        => VsaCodeRuleResolver.IsInvalidCombo(cd, c1, c2);

    /// <summary>
    /// Baut die offizielle Bezeichnung fuer einen VSA-Code auf.
    /// Delegiert an <see cref="VsaLabelBuilder"/>.
    /// </summary>
    public static string? LookupLabel(string code)
        => VsaLabelBuilder.LookupLabel(code);

    /// <summary>
    /// Prueft ob ein VSA-Code typischerweise ein Streckenschaden ist.
    /// Delegiert an <see cref="StreckenschadenCodeClassifier"/>.
    /// </summary>
    public static bool IsStreckenschadenCode(string code)
        => StreckenschadenCodeClassifier.IsStreckenschadenCode(code);
}
