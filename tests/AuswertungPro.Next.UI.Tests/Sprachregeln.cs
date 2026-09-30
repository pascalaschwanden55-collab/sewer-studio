using System;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Q4b (Wartbarkeitsaudit 30.09.2026): EINE Quelle fuer die Listen der Sprachwaechter
/// (<see cref="DesignAuditLaufzeittexteTests"/>, <see cref="DesignAuditLaufzeittexteSchichtenTests"/>,
/// <see cref="DesignAuditFeinschliffTests"/>). Kein Wert steht in zwei Listen.
///
/// WICHTIG (oberflaeche.md, Aufgabe 10c1, Lehre 1): Die Listen enthalten die ALTE
/// ASCII-Ersatzschreibweise. Diese Datei darf von keinem Waechter als Pruefgegenstand gelesen
/// werden (alle Waechter durchsuchen nur <c>src/</c>), und kein Bereinigungsskript darf sie
/// anfassen — es wuerde sein eigenes Suchmuster wegkorrigieren.
///
/// Die Wortlisten sind je Pruefbereich getrennt geblieben (UI-Projekt / drei untere Schichten),
/// weil die Vereinigung fuer den jeweils anderen Bereich neue Treffer ergeben koennte; der
/// gemeinsame Teil steht nur einmal in <see cref="WortformenGemeinsam"/>.
/// </summary>
internal static class Sprachregeln
{
    /// <summary>Pruefbereich einer geschuetzten Zeichenkette.</summary>
    [Flags]
    internal enum Bereich
    {
        /// <summary>UI-Projekt (Aufgabe 10c1, <see cref="DesignAuditLaufzeittexteTests"/>).</summary>
        Ui = 1,

        /// <summary>Application/Infrastructure/Domain (Aufgabe 10c2).</summary>
        Schichten = 2,

        Beide = Ui | Schichten,
    }

    /// <summary>Eine ganze Zeichenkette, die bewusst in Ersatzschreibweise bleibt, samt Grund.</summary>
    internal sealed record GeschuetzteZeichenkette(string Text, string Grund, Bereich Bereich);

    /// <summary>
    /// Stammformen (Teilzeichenketten, gross/klein beachtet) fuer die vier festen Quelldateien
    /// des Waechters <c>Sichtbare_Laufzeittexte_tragen_echte_Umlaute</c>. Bewusst konkret: Ein
    /// blosses „ae/oe/ue irgendwo" traefe auch „Neu", „Quelle" oder „Muster".
    /// </summary>
    internal static readonly string[] LaufzeittextStaemme =
    [
        "Faell", "Schaetz", "aehnlich", "Gruen", "Schaed", "Pruef", "Verknuepf",
        "Loesch", "Oeffn", "Groess", "Naechst", "Ueber", "Zustaend", "Maengel", "Bemuehung",
    ];

    /// <summary>
    /// Regex-Bausteine fuer sichtbare XAML-Attribute (<see cref="DesignAuditFeinschliffTests"/>,
    /// ohne Gross/Klein). Bewusst NICHT enthalten: „ss" (Schweizer Schreibweise ist korrekt)
    /// und Woerter wie „neue", „Steuer", „Bauer", „Quelle", in denen ae/oe/ue echte
    /// Buchstabenfolgen sind. <c>\b</c> steht vor Woertern, die nur als Wortanfang gelten.
    /// </summary>
    internal static readonly string[] XamlUmlautErsatzMuster =
    [
        "oeffn", "pruef", "\\bfuer\\b", "\\bueber", "waehl", "uebernehm", "zurueck", "aender", "menue", "naechst", "drueck",
        "temporaer", "bestaetig", "rueckmeld", "zugehoerig", "\\bgruen", "verknuepf", "loesch", "laenge", "groesse", "hoehe",
        "gefaell", "schaecht", "spaet", "vorschlaeg", "zusaetzl", "verfuegbar", "gueltig", "moeglich", "noetig", "erfuellt",
        "waehrend", "schluessel", "ausfuehr", "ergaenz", "erklaer", "uebersicht", "ueberspring", "ausgewaehlt", "zaehl",
        "fuellen", "buendel", "rueckgaengig", "ueberschreib", "kuerzel", "laeuft", "staerke", "wuensch", "hoeher", "groesser",
        "\\bkuerz", "praefix", "gebaeud", "haeus", "kanaele", "strassenzuege", "uebertrag", "ueberpruef", "ausloes",
        "loeschen", "zuruecksetz", "waehle", "geoeffnet", "ueblich", "uebrig", "aehnlich", "erhoeh", "gefuehrt", "flaech", "dafuer", "wofuer",
    ];

    /// <summary>Ganze Woerter (Wortgrenze), die in BEIDEN Pruefbereichen (UI und untere Schichten) bereinigt wurden.</summary>
    internal static readonly string[] WortformenGemeinsam =
    [
        "Anschluesse", "Aufraeumen", "Bestaetigung", "Eintraege", "Faelle", "Faellen", "Fuer",
        "Goldpruefung", "Goldpruefungs", "Kostenuebersicht", "Laenge", "Laeufe", "Loeschen", "Maskenflaeche",
        "Naechstes", "Oeffnen", "Pruefe", "Pruefen", "Pruefplatz", "Pruefung", "Uebernahme",
        "Uebernommen", "Ungueltige", "Vorschlaege", "Zeitueberschreitung", "aufgeloest", "aufloesbar", "ausdruecklich",
        "ausgewaehlt", "ausgewaehlte", "benoetigt", "beschaedigt", "bestaetigt", "bestaetigte", "duerfen",
        "enthaelt", "ergaenzen", "ergaenzt", "fuer", "geaendert", "gehoert", "geloescht",
        "geoeffnet", "geprueft", "gepruefte", "geschuetzt", "geschuetzten", "gewaehlten", "groesser",
        "gueltig", "gueltige", "gueltigen", "gueltiges", "klaeren", "koennen", "laesst",
        "laeuft", "moeglich", "muessen", "noetig", "oeffnen", "pruefe", "pruefen",
        "temporaere", "ueber", "uebergeben", "uebernommen", "uebernommene", "ueberschrieben", "uebersprungen",
        "uebrigen", "ungueltig", "ungueltige", "ungueltigen", "ungueltiges", "unterstuetzt", "unveraenderlich",
        "unveraendert", "unvollstaendig", "veraendert", "verfuegbar", "vollstaendig", "waehle", "waehlen",
        "waehrend", "wuerde", "zuruecknehmen", "zusaetzlichen",
    ];

    /// <summary>Ganze Woerter, die nur im UI-Projekt (Aufgabe 10c1) geprueft werden. Bewusst nicht aufgenommene Woerter samt Gruenden: siehe Kommentar an <see cref="DesignAuditLaufzeittexteTests"/>.</summary>
    internal static readonly string[] WortformenNurUi =
    [
        "Abhaengigkeitspaket", "Aendern", "Ausgewaehlte", "Beschaedigte", "Bestaetigen", "Bildflaeche", "Bildgroesse",
        "Dichtheitspruefung", "Eigentuemerdossiers", "Ergaenzt", "Flaeche", "Fuellung", "Geaendert", "Geraetesicherheit",
        "Goldfaelle", "Haltungslaenge", "Naeherung", "Nettobetraege", "Oeffner", "Persoenliche", "Preisaenderungen",
        "Protokolleintraege", "Pruefspur", "Pruefungsfortschritt", "Qualitaetspruefung", "Saetze", "Saetzen", "Schaerfe",
        "Schaetzung", "Uebernehmen", "Uebersprungen", "Uebersprungene", "Ungueltig", "Unvollstaendige", "Verfuegung",
        "Waehle", "Waehlen", "Waehrend", "Zugehoerige", "Zuruecksetzen", "Zusaetzliche", "aendern",
        "ausgefuellt", "auswaehlbar", "auswaehlen", "bestaetigen", "geaenderte", "gehaengt", "geoeffneten",
        "geschuetzte", "gewaehlt", "gezaehlt", "hashgeprueften", "hinzufuegen", "laedt", "moegliche",
        "nachgeruestet", "naechsten", "naeher", "naeherung", "persoenliches", "rueckgaengig", "schlaegt",
        "spaeter", "spaetere", "temporaeren", "trainingsfaehig", "uebernehmen", "ueberschreibt", "uebrige",
        "verknuepfen", "verknuepfte", "verstaendlich", "zugehoerige", "zugehoerigen", "zurueckgegeben", "zurueckgehaltene",
    ];

    /// <summary>Ganze Woerter, die nur in Application/Infrastructure/Domain (Aufgabe 10c2) geprueft werden. „Qüllengrösse" ist ein Altlast-Eintrag aus 10c2 (unschaedlich, unveraendert uebernommen).</summary>
    internal static readonly string[] WortformenNurSchichten =
    [
        "Aehnlichkeitssuche", "Aelterer", "Aenderungsauftrag", "Aenderungsdatum", "Aufloesung", "Aufloesungsdaten", "Ausfuehrung",
        "Ausfuehrungsmodus", "Begruendung", "Beitraege", "Benoetigt", "Bestaetiger", "Bestaetigt", "Bildqualitaeten",
        "Bogenfaelle", "Dateigroesse", "Dateipfadaenderungen", "Dateiuebertragungs", "Dateiveroeffentlichung", "Datensaetze", "Eigentuemermanifest",
        "Einschraenkungen", "Eintraegen", "Einzellaeufe", "Einzelpruefung", "Ergaenzende", "Ergaenzung", "Exportbestaetigung",
        "Feldauftraege", "Feldauftraegen", "Frueherer", "Gefuellte", "Geprueft", "Gepruefte", "Geschuetzter",
        "Goldpruefungen", "Groesse", "Groessenlimit", "Gueltigkeit", "Handaenderung", "Handaenderungen", "Haupteintraege",
        "Hausanschluesse", "Inhaltspruefung", "Kanalschaeden", "Klassenschluessel", "Klassenzaehlung", "Kompatibilitaets", "Kuenstliche",
        "Kuerzel", "Kuerzlich", "Laengen", "Laengenangabe", "Loeschversuch", "Luecke", "Luecken",
        "Maskenqualitaet", "Moegliche", "Moeglicher", "Nachpruefung", "Negativsaetze", "Oberflaeche", "Persoenlich",
        "Plausibilitaet", "Positionspruefung", "Praefix", "Projektpfadpruefung", "Protokolleintraegen", "Pruefablage", "Pruefbeleg",
        "Pruefdatei", "Pruefergebnis", "Prueffall", "Prueffoto", "Prueflauf", "Pruefliste", "Pruefpfad",
        "Pruefsumme", "Pruefsummenalgorithmus", "Pruefsummendatei", "Pruefsummennachweis", "Qualitaetsbericht", "Quarantaene", "Quellengroesse",
        "Qüllengrösse", "Rueckfall", "Rueckgabewert", "Ruecknahme", "Ruecksetz", "Ruecksetzung", "Schluessel",
        "Sicherheitsgruenden", "Sicherheitspruefung", "Sicherungspruefung", "Temporaeres", "Temporaerpfad", "Trainingsfaelle", "Uebergeordneter",
        "Ueberlagerung", "Uebersicht", "Unabhaengiger", "Ungueltiger", "Ungueltiges", "Urspruengliche", "Verknuepfte",
        "Verknuepfter", "Verknuepfung", "Verknuepfungshilfen", "Verlaeufe", "Veroeffentlichung", "Versionsstaende", "Vollstaendige",
        "Vollstaendiger", "Vollstaendigkeit", "Vorpruefung", "Zaehler", "Zellgroesse", "Zusammenfuegedienst", "aehnliche",
        "aehnlichen", "aelter", "aelteren", "aendert", "aufgefuehrten", "aufgeraeumt", "aufraeumen",
        "ausdrueckliche", "ausfuehrbar", "ausfuehren", "ausgefuehrt", "ausgewaehlten", "ausgewaehlter", "behaelt",
        "benoetigen", "benoetigte", "beschaedigte", "beschaedigten", "bestaetigten", "bestaetigter", "bestaetigtes",
        "darueber", "eingeschraenkt", "erfuellt", "ergaenzend", "erhaelt", "frueher", "frueheren",
        "fruehes", "fuehren", "fuehrt", "fuellt", "fuenf", "gefuehrt", "gefuellt",
        "gefuellte", "gefuellten", "gehoeren", "geklaerten", "gekuerzt", "geschuetzter", "gewaehlte",
        "gewuenscht", "glaubwuerdiges", "groesseren", "gruene", "gueltiger", "haengend", "haengt",
        "hinzugefuegt", "hoechstens", "laenger", "laengster", "loeschen", "lueckenlos", "mitgezaehlt",
        "nachgeprueft", "oeffne", "persoenlich", "persoenliche", "persoenlichen", "pruefbar", "pruefbarer",
        "pruefbares", "pruefenden", "prueft", "raeumliche", "regulaeres", "ruecklaufende", "schreibgeschuetzt",
        "schreibgeschuetzten", "schwaecheren", "staerkerer", "traegt", "ueberein", "ueberlappen", "ueberlappt",
        "ueberschreiben", "ueberschreiten", "ueberschreitet", "ueberschritten", "unabhaengige", "ungeklaert", "ungepruefter",
        "ungeschuetzter", "ungueltiger", "unnoetiger", "unterstuetzte", "unterstuetzter", "unterstuetztes", "unvollstaendige",
        "unvollstaendigem", "unvollstaendigen", "unvollstaendiger", "unzulaessiges", "urspruengliche", "verknuepft", "verlaessliches",
        "verlaesst", "veroeffentlicht", "vollstaendige", "vollstaendiger", "vollstaendiges", "waere", "widerspruechlich",
        "widerspruechliche", "widerspruechlichen", "wuerden", "zaehlen", "zurueck", "zurueckgenommen", "zurueckgenommene",
        "zurueckgerollt", "zurueckgesetzt", "zuruecklegen", "zurueckverschoben", "zusaetzlich", "zusammengefuehrt", "zusammengefuehrte",
        "zusammenzufuehren",
    ];

    /// <summary>Wortformen fuer das UI-Projekt (gemeinsamer Teil plus UI-eigener Teil).</summary>
    internal static string[] WortformenFuerUi
        => WortformenGemeinsam.Concat(WortformenNurUi).Distinct().ToArray();

    /// <summary>Wortformen fuer die drei unteren Schichten (gemeinsamer Teil plus schichteigener Teil).</summary>
    internal static string[] WortformenFuerSchichten
        => WortformenGemeinsam.Concat(WortformenNurSchichten).Distinct().ToArray();

    /// <summary>
    /// Ganze Literale, die exakt so verglichen, zurueckgelesen oder gespeichert werden und deshalb
    /// in Ersatzschreibweise bleiben — mit Grund und Pruefbereich.
    /// </summary>
    internal static readonly GeschuetzteZeichenkette[] GeschuetzteGanzeZeichenketten =
    [
        new("Pruefung bestanden", "Excel-Farbregel-Wert: ExcelReportStyle.Farbregeln vergleicht exakt (seit 10b)", Bereich.Beide),
        new("Pruefung knapp nicht bestanden", "Excel-Farbregel-Wert: ExcelReportStyle.Farbregeln vergleicht exakt (seit 10b)", Bereich.Beide),
        new("Pruefung nicht bestanden (grob undicht)", "Excel-Farbregel-Wert: ExcelReportStyle.Farbregeln vergleicht exakt (seit 10b)", Bereich.Beide),
        new("Ausfuehrung Datum/Jahr", "Feldschluessel (Schacht-Vorlage, SchachtPro, Namensverteilung)", Bereich.Schichten),
        new("SewerStudio-Datensicherung. Diese Datei markiert den Spiegel-Ordner \\u2014 nicht loeschen.", "Inhalt der Sicherungs-Markerdatei; bestehende Sicherungen werden daran erkannt", Bereich.Schichten),
        new("2 Vollstaendig", "Ordnername im Kataster-Paket (Dateinamen bleiben ASCII)", Bereich.Schichten),
        new("ausmass ergaenzen", "Platzhaltererkennung der Goldbeschreibung (Python-Gegenstueck gold_stock_audit.py)", Bereich.Schichten),
        new("ausmaß ergaenzen", "Platzhaltererkennung der Goldbeschreibung (Variante mit scharfem S)", Bereich.Schichten),
        new("{normalizedCode} - persoenlich bestaetigt", "Standardbeschreibung eines Goldsamples: Datenwert in training_samples.json und KB", Bereich.Schichten),
        new("Automatisch ergaenzte Rohrgrenze", "SkipReason im gespeicherten Protokolleintrag (Projektdatei)", Bereich.Schichten),
        new("Aenderungslieferung aus SewerStudio; nur Aenderung-Eintraege erlauben Updates: ", "Kommentar im Kopf der XTF-Datei (XTF-Ausgabe bleibt unveraendert)", Bereich.Schichten),
        new("Vollstaendiger Neu-Export aus SewerStudio: ", "Kommentar im Kopf der XTF-Datei (XTF-Ausgabe bleibt unveraendert)", Bereich.Schichten),
    ];

    internal static string[] GeschuetzteFuer(Bereich bereich)
        => GeschuetzteGanzeZeichenketten
            .Where(g => (g.Bereich & bereich) != 0)
            .Select(g => g.Text)
            .ToArray();

    /// <summary>
    /// Dateien der unteren Schichten, deren Zeichenketten bewusst unveraendert bleiben
    /// (Grund je Eintrag). Nur der Schichten-Waechter kennt diese Ausnahme.
    /// </summary>
    internal static readonly (string Datei, string Grund)[] AusgenommeneSchichtDateien =
    [
        ("EnhancedVisionPromptBuilder.cs", "KI-Prompt an Qwen: Der Wortlaut steuert das Modell, kein Anzeigetext"),
        ("GuidedVerificationService.cs", "KI-Prompt samt JSON-Schluesseln (bestaetigung, erklaerung), die zurueckgelesen werden"),
        ("PdfKiSchiedsrichter.cs", "KI-Prompt des PDF-Schiedsrichters"),
        ("PdfProjectMetadataParser.cs", "PDF-Parsermuster (Zustaendige Person neben der Umlautform)"),
        ("VsaCodeTree.cs", "VSA-Codebaum: Katalog-/Vokabulartabelle (Merkmalsbezeichnungen), nicht Teil von 10c2"),
        ("SchachtProFieldNames.cs", "Feldschluessel Ausfuehrung Datum/Jahr (Alias der Excel-Vorlage)"),
        ("ExcelSchachtFeldzuordnung.cs", "Feldschluessel Ausfuehrung Datum/Jahr (Alias der Excel-Vorlage)"),
    ];
}
