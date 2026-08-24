using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models.Dossiers;

/// <summary>
/// Bearbeitungsstand eines Eigentuemerdossiers. Wird von Hand gesetzt und ist
/// nur eine Merkhilfe fuer das Gebiet, keine Automatik.
/// </summary>
public enum DossierStatus
{
    /// <summary>Angelegt, noch nichts erzeugt.</summary>
    Offen = 0,

    /// <summary>Word-Datei wurde erzeugt.</summary>
    WordErzeugt = 1,

    /// <summary>Dem Eigentuemer uebergeben oder versendet.</summary>
    Versendet = 2,

    /// <summary>Unterschrieben zurueck.</summary>
    Zurueck = 3
}

/// <summary>
/// Eine Zeile der Tabelle "Informationen". Welche Themen ein Dossier fuehrt,
/// entscheidet das Projekt: das eine Gebiet braucht "Hausanschluss Abwasser",
/// das andere "Ausgangslage" und "Sanierungskonzept". Deshalb eine Liste und
/// keine feste Feldreihe.
/// </summary>
public sealed class DossierTopicRow
{
    /// <summary>Beschriftung in der Spalte "Thema".</summary>
    public string Title { get; set; } = "";

    /// <summary>Text in der Spalte "Bemerkungen".</summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Schriftfarbe des Textes als sechsstelliger Hexwert ohne Raute, zum
    /// Beispiel "C00000". Leer heisst: die Farbe der Vorlage.
    /// </summary>
    public string ColorHex { get; set; } = "";

    /// <summary>
    /// Abweichende Formatierungen innerhalb des Textes. Start und Laenge beziehen sich
    /// auf <see cref="Text"/>. Bereiche ausserhalb des Textes werden beim Lesen
    /// sicher begrenzt. Eine leere Liste behaelt alte Dossiers mit einer Farbe
    /// fuer die ganze Zeile vollstaendig kompatibel.
    /// </summary>
    public List<DossierTextStyleRange> StyleRanges { get; set; } = new();
}

/// <summary>Ein formatierter Teil innerhalb eines Dossier-Thementextes.</summary>
public sealed class DossierTextStyleRange
{
    public int Start { get; set; }

    public int Length { get; set; }

    /// <summary>Sechsstelliger RGB-Hexwert ohne Raute.</summary>
    public string ColorHex { get; set; } = "";

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public bool Underline { get; set; }
}

/// <summary>Eine Zeile der Tabelle "Aenderungswesen".</summary>
public sealed class DossierChangeRow
{
    public string Version { get; set; } = "";
    public string Date { get; set; } = "";
    public string Visum { get; set; } = "";
    public string Change { get; set; } = "";

    /// <summary>Zeichenformatierung je Eingabefeld dieser Tabellenzeile.</summary>
    public Dictionary<string, List<DossierTextStyleRange>> FieldStyles { get; set; } = new();
}

/// <summary>
/// Gebietsweite Angaben. Sie gelten fuer alle Dossiers eines Projekts und
/// werden nur einmal erfasst. Ein Dossier darf jedes Feld einzeln ueberschreiben.
/// </summary>
public sealed class DossierAreaSettings
{
    /// <summary>Titel des Gesamtprojekts, z.B. "Sanierung Private Abwasserleitungen Erstfeld West".</summary>
    public string AreaTitle { get; set; } = "";

    /// <summary>Ansprechpartner der Bauherrschaft inkl. Adresse, Telefon, E-Mail.</summary>
    public string ContactPerson { get; set; } = "";

    /// <summary>Beauftragter Unternehmer.</summary>
    public string Contractor { get; set; } = "";

    /// <summary>Oertliche Bauleitung.</summary>
    public string SiteManagement { get; set; } = "";

    /// <summary>Geplanter Ausfuehrungstermin, z.B. "Herbst 2026/Fruehling 2027".</summary>
    public string ExecutionDate { get; set; } = "";

    /// <summary>Behinderungen, Zugaenge, Verkehrs- und Fussgaengerfuehrung.</summary>
    public string Obstructions { get; set; } = "";

    /// <summary>Erklaerungstext zum privaten Hausanschluss Abwasser.</summary>
    public string HouseConnectionText { get; set; } = "";

    /// <summary>Angaben zum Meteorwasser.</summary>
    public string StormWaterText { get; set; } = "";

    /// <summary>Frist fuer die Rueckmeldung des Eigentuemers.</summary>
    public string ResponseDeadline { get; set; } = "";

    /// <summary>Fusszeile der Word-Ausgabe.</summary>
    public string FooterLine { get; set; } = "";

    /// <summary>Pfad zum Logo (relativ zum Projekt oder absolut).</summary>
    public string LogoPath { get; set; } = "";

    /// <summary>
    /// Autoren fuer die Zeile "Autoren:" auf Seite 2. Bleibt das Feld leer,
    /// nimmt die Ausgabe den Windows-Benutzernamen — der heisst aber je nach
    /// Rechner "Besitzer" und gehoert nicht in ein Dokument fuer den Eigentuemer.
    /// </summary>
    public string Authors { get; set; } = "";

    /// <summary>
    /// Zweite Deckblattzeile unter dem Gebietstitel, z.B. "6472 Erstfeld".
    /// </summary>
    public string AreaLocation { get; set; } = "";

    /// <summary>Projektnummer fuer die Deckblattzeile "Proj. Nr. AWU".</summary>
    public string ProjectNumber { get; set; } = "";

    /// <summary>Kuerzel fuer die Deckblattzeile "Gez.".</summary>
    public string DrawnBy { get; set; } = "";

    /// <summary>
    /// Die Standardthemen der Tabelle "Informationen" fuer alle Dossiers des
    /// Gebiets. Ein Dossier darf einzelne Themen ueberschreiben und eigene
    /// ergaenzen — siehe <see cref="DossierDefinition.Topics"/>.
    /// </summary>
    public List<DossierTopicRow> Topics { get; set; } = new();
}

/// <summary>
/// Eine Zeile der Tabelle "Eigentumsverhaeltnisse". Eine Liegenschaft kann
/// mehrere haben — Stockwerkeigentum, Doppelhaus, mehrere Hausnummern.
/// </summary>
public sealed class DossierOwnerRow
{
    public string HouseNumber { get; set; } = "";
    public string ParcelNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Mail { get; set; } = "";

    /// <summary>Objektbewohner, z.B. "Mehrfamilienhaus".</summary>
    public string Occupancy { get; set; } = "";

    /// <summary>Zeichenformatierung je Eingabefeld dieser Tabellenzeile.</summary>
    public Dictionary<string, List<DossierTextStyleRange>> FieldStyles { get; set; } = new();

    /// <summary>
    /// Wahr, sobald mindestens eines der sechs Felder etwas anderes als
    /// Leerraum enthaelt. Zentrale Regel fuer "ist diese Zeile leer" —
    /// Editor-Fenster und Migration verwenden dieselbe Eigenschaft, statt die
    /// Regel je einzeln nachzubauen.
    /// </summary>
    /// <remarks>
    /// Abgeleitet, nicht gespeichert: In der Datei haette der Wert nichts
    /// verloren — niemand pflegt ihn, und aendert sich die Regel, stuende dort
    /// ein falscher Altwert.
    /// </remarks>
    [JsonIgnore]
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(HouseNumber)
        || !string.IsNullOrWhiteSpace(ParcelNumber)
        || !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(Phone)
        || !string.IsNullOrWhiteSpace(Mail)
        || !string.IsNullOrWhiteSpace(Occupancy);
}

/// <summary>
/// Ein Eigentuemerdossier: die benannte Auswahl der Haltungen einer Liegenschaft
/// plus deren Stammdaten. Die Haltungsdaten selbst bleiben im Projekt; hier
/// stehen nur Verweise ueber <see cref="HaltungRecord.Id"/>.
/// </summary>
public sealed class DossierDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Anzeigename, meist Strasse und Hausnummern.</summary>
    public string Name { get; set; } = "";

    /// <summary>Ordnername unterhalb von "Dossiers". Einmal vergeben, bleibt er stabil.</summary>
    public string FolderName { get; set; } = "";

    // ── Liegenschaft ──────────────────────────────────────────────────────
    public string ParcelNumbers { get; set; } = "";
    public string HouseNumbers { get; set; } = "";
    public string Address { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Town { get; set; } = "";

    /// <summary>Politische Gemeinde. Nicht dasselbe wie der Ort der Adresse.</summary>
    public string Municipality { get; set; } = "";

    /// <summary>BFS-Nummer der Gemeinde. Ueber sie laeuft die Parzellensuche.</summary>
    public int? MunicipalityBfsNr { get; set; }

    // ── Eigentuemer ───────────────────────────────────────────────────────
    public string OwnerName { get; set; } = "";
    public string OwnerAddress { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string ContactMail { get; set; } = "";

    /// <summary>Objektbewohner, z.B. "Mehrfamilienhaus".</summary>
    public string Occupancy { get; set; } = "";

    /// <summary>
    /// Die Zeilen der Tabelle "Eigentumsverhaeltnisse". Die Einzelfelder oben
    /// bleiben bestehen: sie speisen weiterhin das Deckblatt.
    /// </summary>
    public List<DossierOwnerRow> Owners { get; set; } = new();

    /// <summary>Bilddatei des Uebersichtsplans fuer Kapitel 1.</summary>
    public string OverviewPlanPath { get; set; } = "";

    /// <summary>
    /// Breite des Uebersichtsplans im Dokument in Zentimetern. Leer heisst:
    /// die Breite der Vorlage (15 cm wie im Originaldossier).
    /// </summary>
    public double? OverviewPlanWidthCm { get; set; }

    // ── Sanierung ─────────────────────────────────────────────────────────
    /// <summary>Beschreibung des Bauvorgangs fuer genau diese Liegenschaft.</summary>
    public string ConstructionProcess { get; set; } = "";

    public string Remarks { get; set; } = "";

    /// <summary>Aufzaehlung der Beilagen fuer die Dokumentseite.</summary>
    public string Attachments { get; set; } = "";

    public string Revision { get; set; } = "A";

    /// <summary>
    /// Abweichende Themen dieses Dossiers. Ein Eintrag mit demselben Titel wie
    /// ein Gebietsthema ersetzt dessen Text; alle uebrigen werden angehaengt.
    /// Bleibt die Liste leer, gelten die Gebietsthemen unveraendert.
    /// </summary>
    public List<DossierTopicRow> Topics { get; set; } = new();

    /// <summary>Zeilen der Tabelle "Aenderungswesen".</summary>
    public List<DossierChangeRow> Changes { get; set; } = new();

    /// <summary>Text der Zeile "Fuer die Aktennotiz".</summary>
    public string FileNote { get; set; } = "";

    /// <summary>
    /// Eigene Werte fuer einzelne Stellen des Dossiers, je Platzhaltername.
    ///
    /// Damit ist auch eine sonst berechnete Angabe von Hand zu setzen — das
    /// Erstellungsdatum, der Eigentuemerblock, die Leitungsliste. Ein LEERER
    /// Eintrag ist eine Angabe: die Stelle bleibt dann bewusst leer. Fehlt der
    /// Eintrag ganz, rechnet das Programm den Wert wie bisher aus.
    /// </summary>
    public Dictionary<string, string> FieldOverrides { get; set; } = new();

    /// <summary>
    /// Formatierte Teile einzelner Vorlagenfelder. Der Schluessel entspricht
    /// dem Platzhalter beziehungsweise dem eindeutigen Eingabefeld. Alte
    /// Dossiers ohne diese additive Angabe bleiben unveraendert lesbar.
    /// </summary>
    public Dictionary<string, List<DossierTextStyleRange>> FieldStyles { get; set; } = new();

    /// <summary>
    /// Kapitel, die dieses Dossier nicht fuehrt — benannt nach ihrer
    /// Ueberschrift. Wer keinen Uebersichtsplan hat, laesst das Kapitel weg,
    /// statt eine leere Seite zu verschicken.
    /// </summary>
    public List<string> HiddenChapters { get; set; } = new();

    /// <summary>
    /// Eigene Fassungen fester Texte der Vorlage, je urspruenglichem Text.
    /// Ein LEERER Ersatz laesst die Zeile weg.
    ///
    /// Der Text selbst ist der Schluessel: das braucht keine kuenstliche
    /// Nummer, die beim Umbau der Vorlage verrutscht. Wird der Text in Word
    /// geaendert, greift die eigene Fassung nicht mehr — dann steht wieder der
    /// Text der Vorlage da und nicht ein Rest von gestern.
    /// </summary>
    public Dictionary<string, string> TextOverrides { get; set; } = new();

    public DossierStatus Status { get; set; } = DossierStatus.Offen;

    /// <summary>Verweise auf die Haltungen. Bewusst die Guid, nicht der Name.</summary>
    public List<Guid> HoldingIds { get; set; } = new();

    /// <summary>
    /// Die Schaechte der Liegenschaft, als Schachtnummern wie im Projekt.
    /// Nummern statt Kennungen, weil ein Schacht auch dann noch gemeint ist,
    /// wenn sein Datensatz spaeter neu eingelesen wurde.
    /// </summary>
    public List<string> ShaftNumbers { get; set; } = new();

    // ── Ueberschreibbare Gebietsfelder (null/leer = vom Gebiet erben) ─────
    public string? ExecutionDateOverride { get; set; }
    public string? ContactPersonOverride { get; set; }
    public string? ContractorOverride { get; set; }
    public string? SiteManagementOverride { get; set; }
    public string? ObstructionsOverride { get; set; }
    public string? HouseConnectionTextOverride { get; set; }
    public string? StormWaterTextOverride { get; set; }
    public string? ResponseDeadlineOverride { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Inhalt von "dossiers.json": die Gebietsangaben und alle Dossiers des Projekts.
/// </summary>
public sealed class DossierDocument
{
    /// <summary>Formatversion, die diese Programmversion schreibt.</summary>
    public const int CurrentSchemaVersion = 5;

    /// <summary>Formatversion. Unbekannt hoehere Versionen werden nicht ueberschrieben.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DossierAreaSettings Area { get; set; } = new();

    public List<DossierDefinition> Dossiers { get; set; } = new();
}
