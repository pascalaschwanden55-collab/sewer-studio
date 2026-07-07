# Kostenfelder & NPK folgen „Sanieren = Ja" (Design v4, final)

**Datum:** 2026-07-07 · **Branch:** `feature/gis-karte` · **Status:** Design v4 final, wartet auf User-Freigabe

> **Historie:** v1 naiv; v2/v3 bauten ein aufwändiges Herkunfts-Signal (DerivedFromStore + Guard-Änderung
> + Reconciliation), um handgetippte vs. berechnete Werte zu trennen. Die vom User gelieferte **Fachregel
> macht das überflüssig**: Maßgeblich ist das Feld `Sanieren_JaNein`. Damit wird der Umbau **schlank und
> additiv, ohne Kern-Eingriff**. v4 verwirft die Herkunfts-Variante.

## Fachregel (vom User bestätigt) — der Kern

Das Record-Feld **`Sanieren_JaNein`** (Combo „Sanieren Ja/Nein", `FieldCatalog.cs:118`) ist der
**Hauptschalter** für alles Kosten-Relevante:

- **`Sanieren = Ja`** → Haltung wird saniert → zählt. Abgeleitete Kostenfelder = aus den Massnahmen.
- **`Sanieren = Nein` / leer** → wird nicht saniert → abgeleitete Kostenfelder **leer**, zählt nirgends
  (weder Haltungen-Export noch NPK).
- **Umstellen Ja→Nein** → die berechneten Kostenfelder der Haltung werden **sofort geleert**.

## Auslöser (echter Fall, nachgezählt: Zone 1.15)

| Sanieren | Haltungen | Σ Anschlüsse | Kosten-Netto |
|---|---|---|---|
| **Ja** | 52 | **52** ✅ | **582'573 CHF** ✅ |
| Nein | 38 | 19 | 0 (7 leere Store-Einträge) |
| (leer) | 2 | 1 | 0 |

Haltungen-Export zeigte **72** (= 52 + 20 auf Nein/leer, viele aus PDF-Import `FieldSource.Pdf=7`), NPK war
um **29'132 CHF** zu hoch wegen einer Geister-Haltung (`80622-80874`: gelöschte Haltung, Store-Eintrag blieb
liegen). Korrekt nach Regel: **52 Anschlüsse, 582'573 CHF.**

## Ursachen (verifiziert)

1. **Haltungen-Export ignoriert die Regel.** `ExcelTemplateExportService.ExportToTemplate` schreibt die
   Record-Kostenfelder **1:1**, egal ob `Sanieren=Ja` — inkl. veralteter/importierter Werte auf
   Nein-Haltungen → 72 statt 52.
2. **NPK ignoriert die Regel.** `ProjectPositionAggregator` aggregiert **jeden** Kostenspeicher-Eintrag,
   ohne `Sanieren`-Filter und ohne Prüfung, ob die Haltung noch existiert → Geister + Nein zählen mit.
3. **Abgeleitete Record-Felder werden nicht nachgezogen**, wenn Massnahmen/Sanieren sich ändern (nur an 3
   Stempel-Stellen; nicht bei „Kosten neu berechnen", nicht bei Ja→Nein, nicht bei Haltungslöschung).

## Nicht-Ziele

Kein Herkunfts-Signal / kein `SetFieldValue`-Guard-Umbau / keine Reconciliation (durch die Sanieren-Regel
unnötig) · kein globaler Store-Singleton · Preis-/Rundungs-Engines unverändert · Schächte unverändert.

---

## Architektur

### 1. Schlüssel + Schalter

```csharp
static string HoldingKey.FromRecord(HaltungRecord r) => (r.GetFieldValue("Haltungsname") ?? "").Trim();
static bool   IsToRenovate(HaltungRecord r) =>
    string.Equals((r.GetFieldValue("Sanieren_JaNein") ?? "").Trim(), "Ja", StringComparison.OrdinalIgnoreCase);
```

Schlüsselvergleich `OrdinalIgnoreCase`, nie Schacht-Paar bauen.

### 2. Reine Rechenlogik — additive Mapper-Methode

`SanierungCostFieldMapper` (bestehende `ApplyCosts`/`ClearCosts` bleiben; neu:)

```csharp
// Zieht die 8 abgeleiteten Felder eines Records nach der Sanieren-Regel nach.
// Rückgabe: true, wenn sich etwas geändert hat.
bool SyncRecord(HaltungRecord record, HoldingCost? cost);
```

Logik:
- **`Sanieren=Ja` und Store-Eintrag mit Massnahmen** → 8 Felder aus `cost` berechnen (bestehende Helfer
  `MaxMeasureQty`/`SumSelectedQty`/`SumMeasureLengths`/`HasSelectedLiner`), schreiben mit
  `userEdited:true` (wie `ApplyCosts` heute → die `SetFieldValue`-Guard `:52` blockiert nie, kein
  Reconciliation nötig).
- **`Sanieren=Ja` ohne Store-Eintrag (oder Eintrag ohne `Selected && Qty>0`)** → die 6 Mengenfelder leeren;
  `Kosten` + `Empfohlene_Sanierungsmassnahmen` **behalten** (schützt einen von Hand getippten
  Pauschalbetrag bei „Ja ohne Matrix-Massnahme").
- **`Sanieren=Nein` / leer** → **alle 8** Kostenfelder leeren (Haltung wird nicht saniert).
- Jedes Feld nur schreiben, wenn sich der Wert ändert (kein unnötiges Dirty).

### 3. Dienst `IDerivedCostFieldSynchronizer` (rein, testbar)

```csharp
int Sync(Project project, ProjectCostStore store);   // synct alle Records nach §2, gibt Anzahl geänderter zurück
```

### 4. NPK-Aggregation filtert auf `Sanieren=Ja`-Records

Der NPK-Aufrufer (`BuilderPageViewModel.PrepareLvPositions`) übergibt dem
`ProjectPositionAggregator` **nur** die `HoldingCost`-Einträge, deren Haltungsname zu einem Record mit
`Sanieren=Ja` gehört. Das schließt **automatisch** aus: Nein-Haltungen **und Geister** (kein Record → nie
„Ja"). `ProjectPositionAggregator` selbst bleibt unverändert (nur gefilterter Input).

### 5. Zentraler Choke-Point — `CostPersistenceCoordinator`

Alle Store-Schreiber rufen ihn, damit Records + Store im Ruhezustand konsistent sind:

```csharp
int PersistAndSync(Project project, string projectPath, ProjectCostStore mergedStore);
```

**Vertrag:** Aufrufer übergibt den frisch geladenen, selektiv gemergten Store (bestehendes
Load-fresh-merge-Muster bleibt Aufrufer-Pflicht). Der Koordinator speichert atomar und ruft dann
`synchronizer.Sync`. Der bisherige CostCalc-`_applyTotal`→`ApplyCostsToRecord`-Pfad
(`CostCalculatorViewModel.cs:249`) entfällt für die Feld-Stempelung (macht der Koordinator einheitlich);
optionales **Lernen** bleibt als getrennter Aufruf erhalten.

### 6. Guards (kein stiller Datenverlust)

`ProjectCostStoreRepository.Load` bekommt ein Overload `Load(path, out loadError, out bool storeFileExisted)`
(bestehende 1-/2-Arg-Overloads bleiben als Wrapper). Der Sync/Koordinator **überspringt** jede Änderung
(kein Leeren, kein Save), wenn `!storeFileExisted` ODER `loadError != null` ODER `project.Data` leer ist —
sonst würde eine fehlende/gesperrte `costs.json` fälschlich alle Ja-Haltungen leeren.

### 7. Andockstellen

| # | Stelle | Aktion |
|---|---|---|
| 1 | Matrix-Save (`SanierungsMatrixPageViewModel.cs:1226`) | fresh-merge → `Coordinator.PersistAndSync` |
| 2 | CostCalc-Save (`CostCalculatorViewModel.cs:242`) | dito; `_applyTotal`-Feldstempel entfällt |
| 3 | „Kosten neu berechnen" (`BuilderPageViewModel.cs:921`) | über Koordinator → Records ziehen mit |
| 4 | **Grid-Edit von `Sanieren_JaNein`** (Commit `DataPage.xaml.cs`) | Ja→Nein: Kostenfelder der Zeile **sofort** leeren (Sync auf dieser Haltung) |
| 5 | Haltungslöschung (**ein** Controller-Pfad) | nach `RemoveRecord`: Store-Eintrag der Haltung gezielt entfernen (`ByHolding.Remove`), dann Koordinator |
| 6 | Umbenennung (`DataPage.xaml.cs:600`, nach `HoldingRenameService`) | `ByHolding[alt]→[neu]` umschlüsseln, dann Koordinator |
| 7 | Vor Haltungen-Export + NPK-Export | frischer 2-Arg-`Load` (+ `storeFileExisted`), Sync, bei Fehler abbrechen; NPK filtert §4 |
| 8 | **Dirty-Gate vor Export** | offene Matrix/CostCalc mit ungespeicherten Änderungen (`_hasUnsavedChanges` `:1235`) → „vor Export speichern?" |

Geister-Store-Einträge werden gezielt bei Löschung/Rename entfernt (#5/#6); bereits verwaiste Einträge sind
für die Ausgabe **harmlos** (NPK filtert sie über §4 aus), können aber optional beim Speichern entfernt
werden, wenn `project.Data` vollständig geladen ist.

### 8. Export-Fixes (`ExcelTemplateExportService` / Vorlage)

- **`Renovierung Inliner m`** wird nie exportiert (`:88-89`, Header „m" matcht nichts): Header in
  `Export_Vorlage/Haltungen.xlsx` auf `Renovierung Inliner m` setzen.
- **Vorlage-Spalten ergänzen** (verifizierte Keys): `Linerendmanschette_LEM` (`FieldCatalog.cs:38,129`),
  `Ausgefuehrt_durch` (`:31,121`), `Referenzpruefung` (`:26,117`), `VSA_Zustandsnote_D/S/B` (`:24,43,44`).
- **NR-Autonummer** (`:86-108`): `NR` in der `ColumnOrder`-Schleife überspringen.
- **Stiller Feld-Drop** befüllter, ungemappter Felder im `Result` ausweisen (Transparenz).

---

## Testing (Pflicht, fokussiert)

Reine Logik (`DerivedCostFieldSynchronizer`, `SanierungCostFieldMapper.SyncRecord`):
1. Sanieren=Ja + Anschluss-Massnahme N → `Anschluesse_verpressen=N`; Massnahme auf M ändern, erneut → M.
2. Sanieren=Nein → alle 8 Kostenfelder leer, auch wenn Store-Eintrag vorhanden.
3. Ja→Nein-Umstellung → Felder der Haltung sofort geleert.
4. **Pauschal-Schutz:** Sanieren=Ja, keine Massnahme, `Kosten="1200.00"` von Hand → Kosten bleibt,
   Mengenfelder leer.
5. **PDF-Import-Wert auf Nein-Haltung** (`Anschluesse_verpressen=5, Source=Pdf`, Sanieren=Nein) → geleert.
6. **NPK-Filter:** Store mit Ja-, Nein- und Geister-Einträgen → Aggregat enthält nur Ja; Geist
   (`80622-80874`) fehlt; Summe = 52 Anschlüsse / 582'573 CHF.
7. **Fehlende `costs.json`** → keine Feldänderung, kein Save.
8. Rename A→B mit Store-Eintrag unter A → Eintrag unter B, Felder korrekt.
9. No-op bei bereits synchronem Stand (kein Dirty).
10. Export-Coverage: jedes `ColumnOrder`-Feld bildet auf eine Zielspalte ab; `Renovierung Inliner m` landet
    in der Spalte.

Bestehende Tests grün: `SanierungCostFieldMapperTests`, `ProjectPositionAggregatorTests`, `ExcelExportTests`.

## Vorher/Nachher (Zone 1.15)

Nach Persist/Export: Haltungen-Vorlage zeigt **52** Anschlüsse (Nein/leer-Haltungen leer), NPK zeigt
**582'573 CHF** (Geist + Nein raus), handgetippte Pauschalen auf Ja-Haltungen bleiben. Keine Kern-Änderung,
keine Herkunfts-Logik.

## Compliance (CLAUDE.md)

Eigener Dienst + Interface ✅ · Application-Logik testbar ✅ · im ServiceProvider registriert ✅ ·
fokussierte Tests ✅ · Engines/NPK-Aggregator unverändert (nur gefilterter Input) ✅ · additive
Repository-Overloads, kein Guard-/Metadaten-Umbau ✅ · VRAM/QualityGate unberührt ✅ · Kommentare deutsch ✅ ·
`CostPersistenceCoordinator` bündelt bestehende Store-Schreibpfade (mit User abgestimmt) ✅.
