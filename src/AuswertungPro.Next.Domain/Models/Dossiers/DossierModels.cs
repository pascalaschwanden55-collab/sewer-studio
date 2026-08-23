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

    // ── Sanierung ─────────────────────────────────────────────────────────
    /// <summary>Beschreibung des Bauvorgangs fuer genau diese Liegenschaft.</summary>
    public string ConstructionProcess { get; set; } = "";

    public string Remarks { get; set; } = "";

    /// <summary>Aufzaehlung der Beilagen fuer die Dokumentseite.</summary>
    public string Attachments { get; set; } = "";

    public string Revision { get; set; } = "A";

    public DossierStatus Status { get; set; } = DossierStatus.Offen;

    /// <summary>Verweise auf die Haltungen. Bewusst die Guid, nicht der Name.</summary>
    public List<Guid> HoldingIds { get; set; } = new();

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
    public const int CurrentSchemaVersion = 3;

    /// <summary>Formatversion. Unbekannt hoehere Versionen werden nicht ueberschrieben.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DossierAreaSettings Area { get; set; } = new();

    public List<DossierDefinition> Dossiers { get; set; } = new();
}
