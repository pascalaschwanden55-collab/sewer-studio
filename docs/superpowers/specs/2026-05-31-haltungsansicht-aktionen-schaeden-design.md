# Design-Spec: Haltungsansicht anreichern — Rechtsklick-Aktionen + Schäden-Mini-Tabelle

**Datum:** 2026-05-31 · **Status:** Freigegeben (Brainstorming) · **Branch:** feature/gis-karte

## Ziel
Die neue Haltungsansicht (Liste links · Detail rechts) um zwei Dinge erweitern, die in der Tabelle schon existieren bzw. gewünscht sind:
1. **Rechtsklick-Aktionen** auf einer Haltung in der Liste — dieselben wie im DataGrid (Play, Beobachtungen, PDF/Drucken, Sanierung, …).
2. **Primäre Schäden als kleine, untereinander angeordnete Tabelle** im Detail, bei der ein **Doppelklick** auf eine Zeile (und ein **„+"**) den bestehenden Codier-Editor öffnet.

Alles über **Wiederverwendung** bestehender Logik — keine neue Codier-/Schreiblogik, keine zweite Datenhaltung.

## Kernprinzip: Wiederverwendung
- **Aktionen:** Die DataGrid-Handler existieren bereits in `DataPage.xaml.cs` (`PlayMenu_Click`, `BeobachtungenMenu_Click`, `PrintAwuHaltungsprotokollMenu_Click`, `OpenOriginalPdfMenu_Click`, `CostsMenu_Click`, `MoveRecordUpMenu_Click`, `MoveRecordDownMenu_Click`, `DeleteSelectedRows_Click`) und arbeiten über `ResolveActionRecord(sender, vm)` mit Fallback auf `vm.Selected`.
- **Schäden:** `DataPageViewModel.SelectedProtocolEntries` (`ObservableCollection<ProtocolEntry>`) ist bereits mit der gewählten Haltung synchronisiert. `OpenProtocolCommand` öffnet das bestehende `ProtocolObservationsWindow` (Codier-Editor) und ruft danach `RefreshSelectedProtocolEntries()` → die Tabelle aktualisiert sich von selbst.

## Teil 1 — Rechtsklick-Menü in der Liste (gespiegelt)
**Menüeinträge (1:1 wie DataGrid, `DataPage.xaml:547-562`):**
Position nach oben · Position nach unten · — · Beobachtungen… · — · Play · Haltungsprotokoll AWU drucken… · Haltungsprotokoll Original (PDF) öffnen… · Sanierungsmassnahmen… · — · Markierte Zeilen löschen.

**Verdrahtung (Layer-Disziplin):** Die Aktionslogik bleibt in der `DataPage` (dort liegt sie). `HaltungsansichtView` bleibt „dumm" und meldet nur, *welche* Aktion auf *welcher* Haltung gewünscht ist — über **eine Delegate-Property** analog zum bestehenden `DetailBuilder`:
```csharp
// In HaltungsansichtView
public Action<string, HaltungRecord>? ActionRequested { get; set; } // (actionKey, record)
```
- Rechtsklick auf eine Listenzeile **wählt zuerst die Zeile** (setzt `SelectedItem`/`vm.Selected`), dann öffnet das ContextMenu.
- Jeder MenuItem-Click ruft `ActionRequested?.Invoke("play"|"beobachtungen"|…, record)`.
- `DataPage` setzt `HaltungsansichtView.ActionRequested = RouteHaltungsansichtAction;` (analog zu `DetailBuilder`). `RouteHaltungsansichtAction(actionKey, record)` setzt `vm.Selected = record` und ruft den **bestehenden** Handler/Command je `actionKey`.
- „Markierte Zeilen löschen": die Liste ist Einfachauswahl → löscht die eine gewählte Haltung (Bestätigungsdialog wie bisher über den bestehenden Lösch-Pfad).

→ **Kein Funktions-Code dupliziert**; nur ContextMenu-XAML + Routing-Delegate.

## Teil 2 — Primäre Schäden als Mini-Tabelle (Doppelklick → codieren)
**Im Detail rechts**, unterhalb der Feld-Karten, eine Karte **„Primäre Schäden"** mit kleiner `DataGrid`/`ListView`-Tabelle, gebunden an `vm.SelectedProtocolEntries`:

| Spalte | Quelle |
|---|---|
| Meter | `MeterStart` (bei Streckenschaden `MeterStart–MeterEnd`) |
| Code | `ProtocolEntry.Code` |
| Stufe | `ProtocolEntry.CodeMeta?.Severity` |
| Klartext | `VsaCodeResolver.LookupLabel(Code)` (Fallback: Code) |

- **Doppelklick auf eine Zeile** → `vm.OpenProtocolCommand.Execute(vm.Selected)` (öffnet `ProtocolObservationsWindow`); nach Schließen aktualisiert sich `SelectedProtocolEntries` automatisch (bestehende Logik in `OpenProtocol`).
- **„+"-Knopf** in der Kartenüberschrift → derselbe `OpenProtocolCommand` (dort werden neue Beobachtungen codiert/hinzugefügt).
- **Read-only-Tabelle** (kein Inline-Editing) — die eigentliche Änderung macht der bestehende Editor.
- **Reine Projektion** `ProtocolEntry → SchadenZeile(Meter, Code, Stufe, Klartext)` wird in einen testbaren Helfer ausgelagert.

## Teil 3 — Harmonische Anordnung
Spalte 2 der `HaltungsansichtView` wird ein vertikaler Stapel (Grid, 2 Zeilen):
- **Oben (`*`):** `RecordDetailsView` (Feld-Karten, scrollbar) — wie bisher.
- **Unten (`Auto`, max. Höhe mit eigenem Scroll):** Karte „Primäre Schäden" (gleiche Karten-Optik wie die Feldgruppen: `CardBrush`/`BorderBrush`/CornerRadius, Überschrift + Mini-Tabelle).
- Das **Popup** (Doppelklick im DataGrid → `RecordDetailsWindow`) bleibt **unverändert** — die Schäden-Tabelle erscheint nur in der Haltungsansicht (das gemeinsame `RecordDetailsView` wird dafür nicht angefasst).

## File-Struktur
| Datei | Verantwortung | Status |
|---|---|---|
| `…/DataPage/SchadenZeileFormatter.cs` | reine Projektion `ProtocolEntry → SchadenZeile(Meter, Code, Stufe, Klartext)` | neu (testbar) |
| `tests/AuswertungPro.Next.UI.Tests/SchadenZeileFormatterTests.cs` | Test der Projektion | neu |
| `…/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml(.cs)` | ContextMenu an der Liste (→ `ActionRequested`); Schäden-Karte mit Mini-Tabelle (Doppelklick/„+" → Editor); Layout Spalte 2 | ändern |
| `…/Views/Pages/DataPage.xaml.cs` | `RouteHaltungsansichtAction` setzen + auf bestehende Handler/Commands mappen | ändern |

## Tests
- **Unit:** `SchadenZeileFormatter` — Punktschaden (nur MeterStart), Streckenschaden (MeterStart–MeterEnd), fehlende Severity (→ „–"), Klartext via Resolver mit Fallback auf Code, gelöschte/leere Einträge übersprungen.
- **Manuell (GUI):** Rechtsklick zeigt alle Aktionen und führt sie auf der richtigen Haltung aus; Doppelklick/„+" öffnet den Codier-Editor; nach dem Codieren aktualisiert sich die Mini-Tabelle; Layout harmonisch; Popup unverändert.

## Abgrenzung (bewusst NICHT)
- Keine neue Codier-/Protokoll-Schreiblogik — nur Anzeige + Aufruf des bestehenden Editors.
- Kein Inline-Editing der Schäden-Tabelle (bewusste Wahl: Mischform „Doppelklick → Editor").
- Keine zweite Datenhaltung (`SelectedProtocolEntries` ist die bestehende Quelle).
- Tabelle/Export/Popup bleiben unverändert; das `Primaere_Schaeden`-Textfeld in den Feldgruppen bleibt (synchronisierte Zusammenfassung derselben Daten).

## Offene Punkte / Risiken (im Plan zu klären)
- **Aktion „Markierte Zeilen löschen"** in der Einfachauswahl-Liste: auf den bestehenden Lösch-Pfad der gewählten Haltung abbilden (mit Bestätigung).
- **Right-click-selects-row:** in der `ListBox` per `PreviewMouseRightButtonDown` die Zeile selektieren, bevor das Menü öffnet (sonst greift `vm.Selected` evtl. auf die falsche Haltung).
- **Meter-Darstellung Streckenschaden:** Format „2.56–8.10 m" nur wenn `MeterEnd` sinnvoll > `MeterStart`, sonst Punkt.
- **`Severity`-Typ:** `ProtocolEntryCodeMeta.Severity` ist `string?` — leer/ungültig → „–".
