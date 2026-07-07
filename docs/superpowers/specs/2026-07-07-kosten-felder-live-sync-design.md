# Abgeleitete Kostenfelder immer aktuell — Live-Sync-Dienst (Design)

**Datum:** 2026-07-07 · **Branch:** `feature/gis-karte` · **Status:** Design, wartet auf User-Freigabe

## Auslöser (echter Fall)

Projekt „Zone 1.15" (`D:\Projekte\Zone 1.15`): Der Haltungen-Vorlage-Export zeigt **72** Anschlüsse, das NPK-Leistungsverzeichnis **51/52**. Nachgezählt aus den echten Daten:

| Quelle | Stand | Anschlüsse |
|---|---|---|
| Record-Feld `Anschluesse_verpressen` (in `Altdorf_Zone_1.15.json` gespeichert) | eingefroren, alt | **72** (41 Haltungen) |
| NPK aus `costs.json.bak_altmodell` (06.07. 15:20), Position „Anschluss einbinden" | älter | **52** |
| NPK aus `costs.json` (07.07. 10:02) | aktuell | **56** einbinden + 56 auffräsen (28 Haltungen) |

Keine der beiden vom User gesehenen Zahlen ist der aktuelle Stand (56).

## Ursachenanalyse (verifiziert im Code)

Es gibt **zwei** getrennte Fehlerquellen:

1. **Eingefrorene Record-Felder.** Die aus den Massnahmen abgeleiteten Felder in `HaltungRecord`
   (`Kosten`, `Empfohlene_Sanierungsmassnahmen`, `Renovierung_Inliner_m`, `Renovierung_Inliner_Stk`,
   `Anschluesse_verpressen`, `Reparatur_Manschette`, `Linerendmanschette_LEM`, `Reparatur_Kurzliner`)
   werden nur an drei Stellen gestempelt: Einzel-Kostenfenster-Save
   (`CostCalculatorViewModel.cs:242/249`), Matrix-Save (`SanierungsMatrixPageViewModel.cs:1219-1226`),
   Restore (`DataPageViewModel.cs:210`). Sie werden **nicht** nachgezogen bei
   „Kosten mit aktuellem Katalog neu berechnen" im Druckcenter (`BuilderPageViewModel.cs:921` schreibt
   nur den Store, nicht die Record-Felder). Ändert man danach Massnahmen ohne Neu-Stempeln, bleibt das
   Feld veraltet. → Der Haltungen-Vorlage-Export (`ExportPageViewModel.cs:89` →
   `ExcelTemplateExportService.ExportToTemplate`) liest **ausschließlich** diese Record-Felder und
   exportiert damit veraltet.

2. **Geister-Haltungen im Kostenspeicher.** Löscht man eine ganze Haltung
   (`DataPageViewModel.Remove` / `DataPage.xaml.cs:404` → `Project.RemoveRecord` → `Data.RemoveAt`),
   bleibt der zugehörige Eintrag in `ProjectCostStore.ByHolding` (`costs/costs.json`) **liegen**.
   Das NPK-Leistungsverzeichnis (store-basiert, `ProjectPositionAggregator`) zählt diese Geister-Haltung
   weiter mit. Die Schacht-Matrix entfernt den Eintrag korrekt
   (`SchachtSanierungsMatrixPageViewModel.cs:187/214`); bei den Haltungen fehlt das Äquivalent.

## Ziel

Eine Wahrheit: der Kostenspeicher (`costs/costs.json`). Alle abgeleiteten Record-Felder und die
NPK-Aggregation zeigen **immer den aktuellen Stand** — auch nach Entfernen eines Anschlusses, einer
Massnahme oder einer ganzen Haltung. Der User hat den „Auto-Sync-Dienst"-Ansatz gewählt und
„Handbetrag schützen" für das `Kosten`-Feld.

## Nicht-Ziele (bewusst außen vor)

- Kein Umbau auf einen globalen In-Memory-Store-Singleton (heute lädt jedes ViewModel frisch; dieses
  Muster bleibt).
- Keine Änderung an der Rundungs-/Preis-Architektur (`MeasurePricingEngine`, `CostCalculatorLogicService`).
- Keine Änderung am NPK-Aggregator selbst — er ist bereits store-basiert und korrekt; er profitiert nur
  vom Geister-Entfernen.
- Schächte (`schacht_costs.json`) bleiben unverändert (dort ist die Logik schon sauber).

## Architektur

### Neuer Dienst `IDerivedCostFieldSynchronizer` (Application-Schicht)

Rein rechnend, keine Datei-I/O → voll unit-testbar. Namespace
`AuswertungPro.Next.Application.DataPage` (bei `SanierungCostFieldMapper`).

```csharp
public interface IDerivedCostFieldSynchronizer
{
    /// <summary>
    /// Zieht die abgeleiteten Kostenfelder aller HaltungRecords aus dem Live-Kostenspeicher
    /// nach und entfernt Geister-Einträge (Haltungen, die es im Projekt nicht mehr gibt).
    /// Reine Mutation an übergebenen Objekten, keine I/O.
    /// </summary>
    CostSyncResult Sync(Project project, ProjectCostStore store);
}

public readonly record struct CostSyncResult(int RecordsChanged, int GhostsRemoved)
{
    public bool StoreChanged => GhostsRemoved > 0;
    public bool ProjectChanged => RecordsChanged > 0;
}
```

### Sync-Logik pro Record

Holding-Schlüssel = `HaltungRecord`-Feld `Haltungsname` (case-insensitiv, wie
`ProjectCostStore.ByHolding`).

1. **Store-Eintrag mit selektierten Massnahmen vorhanden** → Zielwerte aller Felder wie
   `SanierungCostFieldMapper.ApplyCosts(record, cost, includeCosts:true)` berechnen. Feld nur schreiben,
   wenn sich der Wert tatsächlich ändert (kein unnötiges Dirty).
2. **Kein Store-Eintrag (oder leer)** → **nur die Mengen-Zählfelder leeren**
   (`Renovierung_Inliner_m/Stk`, `Anschluesse_verpressen`, `Reparatur_Manschette`,
   `Linerendmanschette_LEM`, `Reparatur_Kurzliner`). `Kosten` und `Empfohlene_Sanierungsmassnahmen`
   **bleiben unangetastet** (Schutz für von Hand getippte Pauschalbeträge — User-Entscheidung
   „Handbetrag schützen").

Damit Fall 1 den bestehenden „Kosten-Pauschale"-Fall aus dem Konsistenz-Plan (K2) nicht bricht: Fall 1
greift nur, wenn ein echter Store-Eintrag existiert; eine reine Tabellen-Pauschale hat keinen
Store-Eintrag und fällt in Fall 2 (geschützt).

Die Zählfelder werden über die bereits öffentlichen, getesteten Helfer aus `SanierungCostFieldMapper`
berechnet (`MaxMeasureQty`, `SumSelectedQty`, `SumMeasureLengths`, `HasSelectedLiner`) — keine
Doppel-Logik. Um „nur Mengenfelder leeren" ohne `Kosten` zu ermöglichen, bekommt `SanierungCostFieldMapper`
eine schmale, additive Ergänzung:
`ClearQuantityFields(HaltungRecord)` (leert nur die 5 Mengenfelder; `ClearCosts` bleibt für den bestehenden
Matrix-Pfad unverändert).

### Geister entfernen

Nach dem Record-Sync: jeden `store.ByHolding`-Schlüssel entfernen, für den es im Projekt keine Haltung
mit passendem `Haltungsname` gibt. Zähler in `GhostsRemoved`.

### Orchestrierung (kein I/O im Dienst)

Da es keinen gemeinsamen In-Memory-Store gibt, laden/speichern die Aufrufer nach dem bestehenden Muster
„load-fresh → sync → save-if-changed". Ein dünner UI-Helfer kapselt das, damit es nicht an vier Stellen
kopiert wird:

```csharp
// UI-Schicht, z.B. ServiceProvider-Property oder kleiner Helfer
void SyncAndPersist(Project project, string projectPath)
{
    var repo = new ProjectCostStoreRepository();
    var store = repo.Load(projectPath, out var loadError);
    if (loadError != null) return;              // Store gesperrt/kaputt → NIE speichern (Audit K3)
    var result = _synchronizer.Sync(project, store);
    if (result.StoreChanged)
        repo.Save(projectPath, store, out _);   // nur wenn Geister entfernt wurden
    // result.ProjectChanged → Projekt als dirty markieren / Grid refresh (call-site-abhängig)
}
```

`loadError != null` → **nicht** speichern (bestehende Schutzregel gegen Überschreiben echter Daten mit
leerem Store).

### Andockstellen (4)

| # | Stelle | Aktion |
|---|---|---|
| 1 | `ExportPageViewModel` vor `ExcelExport.ExportToTemplate` (`:89`) | `SyncAndPersist` → Haltungen-Vorlage exportiert aktuelle Felder |
| 2 | `BuilderPageViewModel` vor NPK-Export (`PrepareLvPositions`/`:371,415`) | Sync auf `_costStore` (Geister raus) vor der Aggregation; store-save wenn geändert |
| 3 | Nach Haltungslöschung (`DataPageViewModel.Remove:554` und `DataPage.xaml.cs:404`) | `SyncAndPersist` → Geister-Store-Eintrag entfernt |
| 4 | Nach „Kosten neu berechnen" (`BuilderPageViewModel.cs:921`) | Record-Felder nachziehen |

Bei #2 hält `BuilderPageViewModel` den Store bereits als Feld (`_costStore`); dort wird direkt darauf
synchronisiert statt neu zu laden, um dem bestehenden Ladefluss zu folgen.

## DI-Registrierung

`src\AuswertungPro.Next.UI\ServiceProvider.cs` (handgeschriebener Container, kein MS.DI):
- Property `public IDerivedCostFieldSynchronizer CostFieldSync { get; }`
- Konstruktor: `CostFieldSync = new DerivedCostFieldSynchronizer();`
- Optional `GetService`-Switch-Eintrag analog `IExcelExportService`.

## Testing (fokussiert, Pflicht)

Neue Unit-Tests für `DerivedCostFieldSynchronizer` (reine Logik, kein WPF):

1. **Anschlüsse aktuell:** Store mit N selektierten `ANSCHLUSS_EINBINDEN`-Zeilen → `Anschluesse_verpressen`
   = N; ändert man den Store auf M und synct erneut → Feld = M (nicht eingefroren).
2. **Massnahme entfernt:** Record hatte `Anschluesse_verpressen=5`, Store-Eintrag jetzt ohne Anschluss →
   Feld geleert.
3. **Handbetrag geschützt:** Record mit `Kosten="1200.00"` und **keinem** Store-Eintrag → nach Sync
   bleibt `Kosten="1200.00"`, Mengenfelder leer.
4. **Geister-Haltung:** Store hat Eintrag „X-Y", Projekt hat keine Haltung „X-Y" → Eintrag entfernt,
   `GhostsRemoved=1`.
5. **No-op:** Bereits synchroner Zustand → `RecordsChanged=0`, `GhostsRemoved=0` (kein unnötiges Dirty).
6. **Kein Store-Eintrag, kein Handbetrag:** Mengenfelder leer, `Kosten` leer bleibt leer.

Bestehende Tests grün halten: `SanierungCostFieldMapperTests`, `ProjectPositionAggregatorTests`,
`ExcelExportTests`.

## Verhaltensänderung — Vorher/Nachher am realen Fall

Nach Umsetzung, Projekt „Zone 1.15", frisch exportiert: Haltungen-Vorlage **und** NPK zeigen beide **56**
Anschlüsse (aktueller Stand), Geister-Haltungen zählen nicht mehr mit.

## Compliance mit Architektur-Prinzipien (CLAUDE.md)

- Eigener Dienst mit Interface ✅ · Geschäftslogik in C#/Application, nicht in UI/Sidecar ✅ ·
  im ServiceProvider registriert ✅ · fokussierter Test ✅ · additiv, kein Bestands-Refactoring ✅ ·
  VRAM/QualityGate unberührt ✅ · Kommentare deutsch ✅.
