# Eigentümerdossier — Design

**Datum:** 2026-08-22
**Status:** freigegeben durch Pascal, Umsetzung läuft
**Vorbild:** `C:\Users\Besitzer\Documents\Eigentümerdossier.pdf` (Abwasser Uri, Erstfeld West, Parzellen 762+756)

## Zweck

Bei der Aufnahme eines ganzen Gebiets liegen mehrere Liegenschaften im selben
Projekt. Für jede Liegenschaft soll ein eigenes Dossier entstehen, das dem
Eigentümer übergeben wird: Deckblatt, Eigentumsverhältnisse, Informationen zur
Sanierung mit Unterschriftsfeld — und als Beilage die TV-Protokolle genau der
Haltungen, die zu dieser Liegenschaft gehören.

## Namensabgrenzung

`Dossier` ist im Bestand bereits belegt: `HaltungsDossierPdfBuilder` erzeugt ein
PDF **pro Haltung**. Das Neue heisst durchgängig **Eigentümerdossier** und liegt
in eigenen Namensräumen (`…Application.Dossiers`, `…Infrastructure.Dossiers`).
Der bestehende Haltungsdossier-Weg wird nicht angefasst.

## Entscheidungen (getroffen)

| Frage | Entscheidung |
|---|---|
| Datenhaltung | **Nur Auswahl** — Verweis auf die Original-Haltungen, keine Kopie |
| Ausgabe | **Word-Vorlage mit Platzhaltern** + PDF-Beilagen daneben; Knopf „Alles zu einem PDF" |
| Eingabeumfang | **Zweistufig**: Gebietsangaben einmal je Projekt, Liegenschaftsangaben je Dossier, jedes Feld überschreibbar |
| Oberfläche | **Zwei Ebenen**: Dossier-Liste + Dossier-Cockpit |
| Beilagen automatisch | Importierte **TV-Original-PDFs**; fehlt eines, **eigenes Protokoll als Rückfall** (inkl. Fotos) |
| Beilagen von Hand | Übersichtsplan (QGIS) und Offerte legt Pascal selbst in den Ordner |

## Datenmodell

Zwei Ebenen in einer Datei `<Projekt>\Dossiers\dossiers.json`:

```
DossierDocument
 ├─ SchemaVersion : int (1)
 ├─ Area : DossierAreaSettings      ← einmal je Projekt
 └─ Dossiers : List<DossierDefinition>
```

**`DossierAreaSettings`** (Gebiet): `AreaTitle`, `ContactPerson`, `Contractor`,
`SiteManagement`, `ExecutionDate`, `Obstructions`, `HouseConnectionText`,
`StormWaterText`, `ResponseDeadline`, `FooterLine`, `LogoPath`.

**`DossierDefinition`** (Liegenschaft): `Id` (Guid), `Name`, `ParcelNumbers`,
`HouseNumbers`, `Address`, `PostalCode`, `Town`, `OwnerName`, `OwnerAddress`,
`ContactName`, `ContactPhone`, `ContactMail`, `Occupancy`, `ConstructionProcess`,
`Remarks`, `Attachments`, `Revision`, `Status`, `HoldingIds : List<Guid>`,
`FolderName`, `CreatedAtUtc`, `ModifiedAtUtc` — dazu die überschreibbaren
Gebietsfelder als `string?` (leer = vom Gebiet erben).

**Regeln:**
- Verwiesen wird auf `HaltungRecord.Id` (Guid), nie auf den Haltungsnamen.
  Eine Umbenennung darf das Dossier nicht zerstören.
- Eine Haltung darf in mehreren Dossiers vorkommen (geteilte Leitung).
- Eine nicht mehr auffindbare Haltungs-Id wird **sichtbar als fehlend gemeldet**,
  nie stillschweigend übersprungen.

## Ordner

```
Projekt\
 └─ Dossiers\
     ├─ dossiers.json
     └─ <Liegenschaftsname>\
         ├─ Eigentuemerdossier.docx
         ├─ Beilagen\
         │   ├─ 01_TV_36080-36086.pdf
         │   └─ 02_TV_33850-7.25390.pdf
         └─ Eigentuemerdossier_komplett.pdf
```

Jeder Schreibpfad läuft über `ProjectWritePathGuard` (Junction-/Symlink-Schutz,
Projektgrenze). Der Ordnername wird über `ProjectPathResolver.SanitizePathSegment`
gebildet, Kollisionen mit `-2`, `-3` … aufgelöst — dasselbe Muster wie
`NewProjectFolderPlanner`.

## Bausteine

| Schicht | Typ | Aufgabe |
|---|---|---|
| Domain | `DossierDefinition`, `DossierAreaSettings`, `DossierDocument`, `DossierStatus` | reine Daten |
| Application | `IDossierStore` | `dossiers.json` laden/speichern |
| Application | `DossierFolderPlanner` | Ordnernamen planen (pure, kein Dateisystem) |
| Application | `DossierFieldResolver` | Gebiet → Dossier vererben (pure) |
| Application | `DossierSnapshotBuilder` | Kennzahlen der Auswahl (pure) |
| Application | `IDossierWordExportService` | Word aus Vorlage erzeugen |
| Application | `IDossierAttachmentService` | Beilagen sammeln |
| Application | `IDossierPdfAssemblyService` | Word→PDF + Beilagen zu einem PDF |
| Infrastructure | `DossierFileStore` | atomar mit `.bak`, Muster `TrainingCenterDocumentFileStore` |
| Infrastructure | `DossierWordTemplateExportService` | OpenXML-Platzhalter füllen |
| Infrastructure | `DossierAttachmentCollector` | Original-PDF suchen, sonst eigenes Protokoll |
| Infrastructure | `DossierPdfAssemblyService` | nutzt `IPdfMergeService` |
| UI | `DossiersPage` + `DossiersPageViewModel` | Liste + Cockpit |
| UI | `DossierHoldingPickerDialog` | Haltungsauswahl mit Gruppierung |

**Wiederverwendet statt neu gebaut:**
- `DashboardStatisticsBuilder.Build(...)` — dieselben Cockpit-Kennzahlen,
  angewandt auf die Teilmenge. Keine zweite Zahlenlogik.
- `IPdfMergeService` — Beilagen anhängen.
- `IInspectionProtocolFileLocator` — Original-TV-PDF finden.
- `IProtocolPdfExporter` — Rückfall-Protokoll erzeugen.
- `DocumentFormat.OpenXml` 3.1.1 — bereits über ClosedXML im Projekt, **kein
  neues NuGet-Paket**.

## Word-Vorlage

`Export_Vorlage\Eigentuemerdossier.docx`, erzeugt und reproduzierbar durch
`DossierWordTemplateBuilder`. Pascal kann die Vorlage jederzeit selbst in Word
bearbeiten (Layout, Logos, Standardtexte), solange die Platzhalter stehen
bleiben.

**Abweichung von der ursprünglichen Planung:** Statt eines eigenen Werkzeugs
`tools\WordVorlagenBauer` liegt der Vorlagenbauer als Klasse in Infrastructure,
und die Oberfläche bietet den Knopf „Word-Vorlage". Damit kann Pascal die
Standardvorlage jederzeit selbst wiederherstellen, wenn er sie verkorkst hat —
ohne Werkzeugprojekt, ohne Kommandozeile. Der `ExcelVorlagenBauer` bleibt als
Python-Werkzeug unberührt.

Platzhalter in der Form `{{Name}}`, ersetzt über den Textlauf hinweg (Word
zerlegt Text in `Run`-Elemente; ein naiver Ersatz je `Run` findet Platzhalter
nicht, die über mehrere Runs verteilt sind — deshalb wird je Absatz der
gesamte Text zusammengesetzt, ersetzt und zurückgeschrieben).

Wiederholtabellen (Haltungsliste) über eine mit `{{#Haltungen}}` markierte
Zeile, die je Datensatz geklont wird.

## Ablauf

1. Dossier anlegen, Haltungen wählen (Dialog mit Gruppierung nach `Strasse` /
   `Eigentuemer`), Stammdaten erfassen.
2. **Word erzeugen** → `Eigentuemerdossier.docx` im Dossier-Ordner. Bestehende
   Datei wird nie überschrieben; es entsteht ein freier Name.
3. Pascal ergänzt in Word, legt Übersichtsplan und Offerte in `Beilagen`.
4. **Beilagen sammeln** → TV-Originale bzw. Rückfallprotokolle nummeriert ablegen.
5. **Alles zu einem PDF** → Word→PDF (Word-Automation, sonst klare Meldung) +
   `IPdfMergeService`.

## Fehlerregeln (fail-closed)

- Fehlende Word-Vorlage: klare Meldung, kein Teilergebnis.
- Fehlende Haltung im Dossier: sichtbar in Liste und Cockpit, blockiert nicht.
- Fehlendes TV-Protokoll: Rückfallprotokoll, sonst ehrlich als „fehlt" gemeldet.
- Kein Word installiert: `Eigentuemerdossier.docx` bleibt, PDF-Zusammenführung
  meldet den Grund statt still ein unvollständiges PDF zu schreiben.
- Unlesbare `dossiers.json`: `.bak`-Rückfall, sonst Abbruch ohne Überschreiben.

## Was die Architektur-Wächter erzwungen haben

Vier bestehende Wächter haben beim ersten Durchlauf angeschlagen. Alle vier
waren berechtigt und wurden inhaltlich behoben, nicht durch Hochsetzen einer
Zahl:

| Wächter | Verstoss | Behebung |
|---|---|---|
| `ArchitectureDriftRatchetTests` | `DossiersPageViewModel` nahm den ganzen `ServiceProvider` im Konstruktor | Zwölf einzelne Verträge/Delegates statt des Sammelobjekts |
| `MaintainabilityFitnessTests` | `ServiceProvider.cs` überschritt mit 1003 Zeilen die Grenze | `DossierComposition` (Infrastructure) + `ServiceProvider.Dossiers.cs`, Muster `FullBackupComposition`; jetzt 993 Zeilen |
| `DarkModeFieldStyleArchitectureTests` | lokale `TextBox`-Stile ohne `BasedOn` — im Dunkelmodus unleserlich | `BasedOn="{StaticResource {x:Type TextBox}}"` |
| `DesignAuditThemeResourceTests` | Navigationszähler 15 → 16 | Zahl bewusst angehoben, Symbol `\uE8F1` (Library) auf Eindeutigkeit geprüft |

`ServiceProviderRegistrationTests` (146 → 150) ist ein bewusster Tripwire und
wurde mit Begründung angehoben; die vier Dienste stehen ordentlich in
`ServiceProviderRegistrationMap`.

## Tests

- `DossierFolderPlannerTests` — Sonderzeichen, Kollisionen
- `DossierFieldResolverTests` — Vererbung und Überschreiben
- `DossierSnapshotBuilderTests` — Teilmenge, fehlende Haltung, Kostenfilter
- `DossierFileStoreTests` — Laden/Speichern, korrupte Datei, `.bak`
- `DossierWordTemplateExportServiceTests` — Platzhalter über Run-Grenzen,
  Wiederholzeile, fehlende Vorlage
- `DossierAttachmentCollectorTests` — Original vor Rückfall, fehlende Datei
