# Abgeleitete Kostenfelder immer aktuell — Live-Sync (Design v3, final)

**Datum:** 2026-07-07 · **Branch:** `feature/gis-karte` · **Status:** Design v3, wartet auf User-Freigabe

> **Historie:** v1 naiv (überschrieb Handeingaben, konnte Daten leeren). v2 gehärtet, aber ein
> Kritiker-Pass fand **zwei Blocker**: (B1) die `SetFieldValue`-Sperre (`HaltungRecord.cs:52`) blockiert
> den Sync auf genau den einzufrierenden Feldern, weil `ApplyCosts` App-Werte historisch mit
> `userEdited:true` stempelt — nicht unterscheidbar von echter Handeingabe; (B2) eine Handeingabe *nach*
> einem Sync würde wieder überschrieben. **Beide Blocker haben eine Wurzel: die App kann Herkunft
> (App-berechnet vs. handgetippt) nicht zuverlässig trennen.** v3 führt darum ein echtes Herkunfts-Signal
> ein. **User-Entscheidung: „Gründlich — echtes Herkunfts-Gedächtnis."**

## Auslöser (echter Fall, nachgezählt)

Projekt „Zone 1.15": Haltungen-Vorlage-Export **72** Anschlüsse, NPK **51/52**, aktueller Live-Stand
**56**. Zusätzlich eine **echte Geister-Haltung** `ByHolding["80622-80874"]` ohne zugehörige Haltung —
kompletter GFK-Liner (94 m), 4×Einbinden, 4×Auffräsen, LEM 2, **Total 29'132.15 CHF** → NPK ~29'000 CHF
zu teuer + 4 Anschlüsse zu viel. Verifiziert: Store-Schlüssel == Feld `Haltungsname` (0 Abweichungen,
keine Duplikate, genau 1 Geist).

## User-Entscheidungen (verbindlich)

1. **Handeingabe gewinnt immer** — in JEDEM abgeleiteten Feld, auch bei einer Haltung MIT Massnahmen.
2. **Geister sicher aufräumen mit Bremse** — auch der 29k-Geist raus, aber mit Guards + Rückfrage bei
   Massenlöschung.
3. **Vorlage-Spalten ergänzen** — still weggelassene, befüllte Felder in den Export; `Renovierung Inliner m`
   reparieren.
4. **Gründlicher Ansatz** — echtes Herkunfts-Signal statt Heuristik.

## Prinzip

Der **Kostenspeicher (`costs/costs.json`) ist die Wahrheit** für massnahmen-abgeleitete Werte; die
Record-Kostenfelder sind ein **Abzug** davon. **Ausnahme: handgetippte Werte gewinnen immer.** Damit die
App das sicher trennen kann, bekommt jedes Feld ein **Herkunfts-Signal**.

## Nicht-Ziele

Kein globaler Store-Singleton · keine Änderung an Preis-/Rundungs-Engines · keine Änderung am
NPK-Aggregator (nur geisterfreier Input) · Schächte (`schacht_costs.json`) unverändert.

---

## Architektur

### 1. Schlüssel — genau eine Ableitung

```csharp
static string HoldingKey.FromRecord(HaltungRecord r) => (r.GetFieldValue("Haltungsname") ?? "").Trim();
```

Vergleich immer `OrdinalIgnoreCase`. Nie ein Schacht-Paar bauen, nie `NormalizeHoldingKey`. Geister-Set
aus der **vollständigen** `project.Data`.

### 2. Herkunfts-Signal (Kern-Änderung, additiv aber eng begrenzt)

Neues Feld `FieldMetadata.DerivedFromStore` (bool, Default `false`). Jeder Schreibweg klassifiziert einen
Feldwert eindeutig:

| Schreibweg | UserEdited | DerivedFromStore | Beispiel |
|---|---|---|---|
| Handeingabe (Grid/Dialog) | `true` | `false` | Nutzer tippt Kosten-Pauschale |
| App-berechnet (Sync, ApplyCosts, Recompute) | `false` | `true` | Anschlusszahl aus Massnahmen |
| KI/Import | `false` | `false` | PDF-Import, KI-Empfehlung |

**Änderungen am Kern (`HaltungRecord.SetFieldValue`):**
- Neuer optionaler Parameter `derivedFromStore = false`; jeder Write setzt `meta.DerivedFromStore`
  entsprechend (eine Handeingabe setzt es damit automatisch auf `false` zurück — löst B2).
- Bestehende Aufrufer bleiben quellcode-kompatibel (Default-Parameter). Alle **Handeingabe-Pfade**
  (`DataPage.xaml.cs:761,769,790`, `DataPageComboBoxCommitController.cs:39`) rufen weiterhin
  `userEdited:true` → `derivedFromStore` bleibt `false` (korrekt).
- **App-berechnete Pfade** (`ApplyCosts` **und** der neue Sync) stempeln künftig
  `userEdited:false, derivedFromStore:true`. Das ist die nötige, bewusste Änderung an `ApplyCosts`
  (Zustimmung „gründlich"): App-Werte sind eben *nicht* Handeingaben.

**Überschreib-Regel (im Sync, nicht in der Guard):** Ein abgeleitetes Feld wird nur überschrieben/geleert,
wenn es **leer** ist ODER `DerivedFromStore == true`. Damit sind **beide** geschützt: echte Handeingaben
(`UserEdited=true`) **und** Import/KI-Werte (`DerivedFromStore=false`) — löst B2 vollständig und schützt
importierte Zählwerte.

Die `SetFieldValue`-Guard (`:52`) bleibt semantisch gleich (schützt `UserEdited`-Felder vor
niedrigprioren Writes); da App-Werte künftig `userEdited:false` tragen, blockiert sie den Sync **nicht**
mehr (löst B1 für neue Daten).

### 3. Einmalige Selbstheilung bestehender Projekte (Reconciliation)

Bestandsdaten sind mehrdeutig: `ApplyCosts` hat App-Werte als `userEdited:true` gestempelt — ununterscheidbar
von Handeingaben, und ein stale Wert (72) weicht vom Store (56) ab, eine „gleich→abgeleitet"-Heuristik
würde ihn fälschlich als Handeingabe schützen. Darum:

- **Einmalig beim ersten Öffnen** nach dem Update (idempotent, über ein `Metadata`-Flag
  `costFieldsProvenanceReconciled` gesichert):
  Für die 8 abgeleiteten Kostenfelder → `DerivedFromStore=true, UserEdited=false` setzen.
- Fachliche Begründung: Diese spezifischen Felder wurden in der Praxis **nur** von `ApplyCosts`
  (App-berechnet) geschrieben, nie über einen verlässlichen Hand-Pfad (der Kosten-Hand-Pfad war sogar
  kaputt, siehe §7). Ab jetzt trägt jede echte Handeingabe korrekt `UserEdited=true`, ist also geschützt.
- Effekt: Zone 1.15 korrigiert sich beim ersten Lauf (72 → aktueller Stand), ohne dass je eine *künftige*
  Handeingabe gefährdet ist.

### 4. Reine Rechenlogik — additive Mapper-Methoden

`SanierungCostFieldMapper` bekommt (bestehende `ApplyCosts`/`ClearCosts` bleiben als Fassade, intern auf
den neuen Stempel umgestellt):

```csharp
// Alle 8 Felder aus cost; schreibt/leert ein Feld NUR wenn (leer ODER meta.DerivedFromStore),
// mit userEdited:false, derivedFromStore:true. Kosten/Empfohlene nur bei Store-Eintrag MIT Massnahmen.
int SyncDerivedFields(HaltungRecord record, HoldingCost? cost);
```

Mengen über die vorhandenen Helfer (`MaxMeasureQty`, `SumSelectedQty`, `SumMeasureLengths`,
`HasSelectedLiner`). „Store-Eintrag leer" == keine `MeasureCost`-Zeile mit `Selected && Qty>0` (identisch
zu `ProjectPositionAggregator.cs:56`). Bei `cost==null` oder leer → die 6 Mengenfelder leeren (nur wenn
`DerivedFromStore`), Kosten/Empfohlene unangetastet (Pauschal-Schutz).

### 5. Dienst `IDerivedCostFieldSynchronizer` (rein, testbar)

```csharp
CostSyncResult Sync(Project project, ProjectCostStore store, ISet<string> removedHoldingKeys);
readonly record struct CostSyncResult(int RecordsUpdated, int GhostsRemoved, bool GhostSweepDeferred);
```

1. **Record-Sync** über alle `project.Data`: pro Record `SyncDerivedFields(record, store[key])`. Ein
   Record, dessen Store-Eintrag komplett fehlt, wird über die `DerivedFromStore`-Regel geleert (nur die
   app-abgeleiteten Mengenfelder) — **unabhängig** von `removedHoldingKeys` (löst K3).
2. **Geister** (§6).

### 6. Geister-Sweep mit Bremse

Kandidaten = `store.ByHolding`-Keys ohne passenden `HoldingKey` in `project.Data` (keine Roh-/Trim-/
case-Variante vorhanden). Sweep wird **komplett übersprungen** (nichts gelöscht), wenn:

- `project.Data` leer ODER `projectPath` ungültig,
- `costs.json` **fehlte** oder Ladefehler (§8),
- ein Import/Ladevorgang aktiv ist (bestehendes Flag),
- Geister-Anteil über Schwelle (> 25 % oder > 20 Einträge) → `GhostSweepDeferred=true` + Rückfrage-Toast
  „N verwaiste Kosteneinträge — entfernen?" (bewusste Bestätigung, keine stille Massenlöschung).

### 7. Zentraler Choke-Point — `CostPersistenceCoordinator`

Alle Store-Schreiber rufen ihn, damit Records + Store **im Ruhezustand konsistent** sind:

```csharp
CostSyncResult PersistAndSync(Project project, string projectPath, ProjectCostStore mergedStore, ISet<string> removedHoldingKeys);
```

**Vertrag (explizit):** Der Aufrufer übergibt einen **frisch geladenen, selektiv gemergten** Store (das
bestehende LWW-Muster bleibt Aufrufer-Pflicht; Matrix `:1195-1210`, CostCalc `:231-240`). Der Koordinator
speichert genau diesen Store atomar und synchronisiert dann die Records. Der Koordinator lädt **nicht**
selbst und bekommt nie den veralteten Seiten-Snapshot `_costStore` (`BuilderPageViewModel.cs:876`).

- **CostCalc-Save:** der bisherige `_applyTotal`-Callback (`CostCalculatorViewModel.cs:249` →
  `ApplyCostsToRecord`, `userEdited:true` + Lernen/Dirty) wird für die **Feld-Stempelung** durch den
  Koordinator-Sync ersetzt (einheitlich `userEdited:false/derived`). Das **Lernen** (falls gewünscht)
  bleibt als getrennter, expliziter Aufruf erhalten — es wird nicht mehr an die Feld-Stempelung gekoppelt.

### 8. Andockstellen (v3)

| # | Stelle | Aktion |
|---|---|---|
| 1 | Matrix-Save (`SanierungsMatrixPageViewModel.cs:1226`) | fresh-merge → `Coordinator.PersistAndSync` |
| 2 | CostCalc-Save (`CostCalculatorViewModel.cs:242`) | dito; `_applyTotal`-Feldstempel entfällt |
| 3 | „Kosten neu berechnen" (`BuilderPageViewModel.cs:921/937`) | über Koordinator → Records ziehen jetzt mit |
| 4 | Haltungslöschung — **ein** Controller-Pfad | nach `RemoveRecord`: `removedHoldingKeys += Key`; Koordinator |
| 5 | Umbenennung (`DataPage.xaml.cs:600`, nach `HoldingRenameService`) | VOR Sync `ByHolding[alt]→[neu]` umschlüsseln, dann Koordinator |
| 6 | Vor Haltungen-Export + NPK-Export | **frischer** 2-Arg-`Load` + `loadError`+`storeFileExisted`-Check unmittelbar vor Aggregation; Sync; bei Fehler abbrechen (nicht auf `_costStore` `:876`) |
| 7 | **Dirty-Gate vor Export** | offene Matrix/CostCalc mit ungespeicherten Änderungen (`_hasUnsavedChanges`, `:1235`) → „vor Export speichern?" erzwingen (löst W2: sonst zeigt der Export den alten Plattenstand) |

Löschung über **einen** Controller-Pfad kanalisieren (heute zwei: `DataPage.xaml.cs:414` +
`DataPageRecordCollectionController.cs:63`).

### 9. Repository-Härtung (additiv, Overloads)

Neues Overload — **bestehende 1-/2-Arg-`Load` bleiben** als dünne Wrapper (sonst brechen ~10 Aufrufer,
u. a. `SanierungsMatrixPageViewModel:673,1195`, `CostCalculatorViewModel:110,231`, `SchachtLvCostLoader:23`):

```csharp
ProjectCostStore Load(string? path, out string? loadError, out bool storeFileExisted);
```

Bei `!storeFileExisted` ODER `loadError != null`: **kein** Clearing, **kein** Geister-Delete, **kein** Save
(heute liefert `:43-44` leeren Store mit `loadError=null` → stiller Datenverlust bei fehlender/gesperrter
Datei).

### 10. Export-Fixes (`ExcelTemplateExportService` / Vorlage)

- **`Renovierung Inliner m`** wird nie exportiert (`:88-89`, Header „m" matcht nichts): Header in
  `Export_Vorlage/Haltungen.xlsx` auf `Renovierung Inliner m` setzen.
- **Vorlage-Spalten ergänzen** (verifizierte Keys): `Linerendmanschette_LEM` (`FieldCatalog.cs:38,129`),
  `Ausgefuehrt_durch` (`:31,121`), `Referenzpruefung` (`:26,117`), VSA-Zustandsnoten
  `VSA_Zustandsnote_D/S/B` (`:24,43,44`).
- **NR-Autonummer** (`:86-108`): `NR` in der `ColumnOrder`-Schleife überspringen (Fallback-Laufnummer nicht
  mit Leerwert überschreiben).
- **Stiller Feld-Drop** befüllter, ungemappter Felder im `Result` ausweisen (Transparenz).
- **Kosten-Hand-Pfad reparieren** (Voraussetzung für den Kosten-Schutz): der `UserEdited`-Resolver findet
  beim `DataGridTemplateColumn` „Kosten" die getippte `TextBox` im Panel (heute `null` → Hand-Kosten wird
  fälschlich `userEdited:false`). Ohne diesen Fix ist ein handgetippter Kosten-Wert nicht als Handeingabe
  erkennbar.

---

## Testing (Pflicht, fokussiert)

Reine Logik (`DerivedCostFieldSynchronizer`, `SanierungCostFieldMapper`, `HaltungRecord`-Provenance):
1. Anschlüsse aktuell: Store N → Feld N; Store M, erneut → Feld M (nicht eingefroren).
2. Massnahme entfernt (Store-Eintrag weg) → Mengenfeld geleert, **unabhängig** von `removedHoldingKeys`.
3. **Handeingabe geschützt:** Feld `UserEdited=true, DerivedFromStore=false` → Sync lässt es unverändert,
   auch bei abweichendem Store (auch für Kosten bei einer Haltung MIT Massnahmen).
4. **Handeingabe nach Sync:** Sync stempelt (derived) → Nutzer tippt (`userEdited:true, derived:false`) →
   nächster Sync überschreibt NICHT (B2).
5. **Import-Wert geschützt:** `DerivedFromStore=false, UserEdited=false`, nicht leer → Sync überschreibt
   NICHT.
6. **Reconciliation:** Bestandsrecord mit `Anschluesse_verpressen=72 (userEdited:true)` + Store=56 →
   nach einmaliger Reconciliation `derived:true` → Sync setzt 56; Flag verhindert zweiten Lauf.
7. Geister `80622-80874` entfernt (`GhostsRemoved=1`); Namensgleichheit → NIE Geist.
8. **Sweep-Bremse:** Geister-Anteil über Schwelle → nichts gelöscht, `GhostSweepDeferred=true`.
9. **Fehlende `costs.json`** → keine Feldänderung, kein Save, kein Delete.
10. Rename A→B mit Store-Eintrag unter A → Eintrag unter B, kein Geist, keine Leerung.
11. `SetFieldValue`: App-Write (`derived:true`) überschreibt einen `userEdited:true`-Altwert nicht per
    Guard, aber nach Reconciliation schon (Guard-/Provenance-Zusammenspiel).
12. Export-Coverage: jedes `ColumnOrder`-Feld bildet auf eine Zielspalte ab oder ist bewusst gelistet;
    `Renovierung Inliner m` landet in der Spalte.

Bestehende Tests grün: `SanierungCostFieldMapperTests`, `ProjectPositionAggregatorTests`,
`ExcelExportTests`, Matrix-/CostCalc-Tests, `HaltungRecord`-Tests.

## Vorher/Nachher (Zone 1.15)

Erstes Öffnen: Reconciliation markiert die Kostenfelder als abgeleitet. Nächster Persist/Export:
Haltungen-Vorlage **und** NPK zeigen denselben aktuellen Stand; Geist `80622-80874` entfernt →
**–29'132 CHF, –4 Anschlüsse**; handgetippte Pauschalen/Texte bleiben.

## Scope & Compliance (CLAUDE.md)

Eigener Dienst + Interface ✅ · Application-Logik testbar ✅ · im ServiceProvider registriert ✅ ·
fokussierte Tests ✅ · Engines/NPK-Aggregator unverändert ✅ · VRAM/QualityGate unberührt ✅ ·
Kommentare deutsch ✅.
**Bewusste, mit User abgestimmte Kern-Eingriffe** (kein stilles Refactoring): (a) `FieldMetadata`
+ `SetFieldValue` um `DerivedFromStore` erweitern; (b) `ApplyCosts` stempelt App-Werte als abgeleitet;
(c) einmalige Reconciliation; (d) `CostPersistenceCoordinator` bündelt die Store-Schreibpfade. Diese vier
sind die Voraussetzung für „1:1 + zieht überall nach ohne Datenverlust" und ohne sie nicht erfüllbar.
