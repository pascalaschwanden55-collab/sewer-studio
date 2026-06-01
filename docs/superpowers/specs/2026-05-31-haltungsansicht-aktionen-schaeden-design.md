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
**Im Detail rechts**, unterhalb der Feld-Karten, eine Karte **„Primäre Schäden"** mit einer **kompakten Zeilen-Tabelle im Stil von Bild 2** (Referenz-Screenshot des Users), gebunden an `vm.SelectedProtocolEntries`. Jede Zeile als schmale Karten-Zeile:

| Spalte | Quelle | Optik (wie Bild 2) |
|---|---|---|
| Meter | `MeterStart` (bei Streckenschaden `MeterStart–MeterEnd`) | grün, linksbündig, `0.00 m` |
| Code | `ProtocolEntry.Code` | blaue, leicht abgerundete Chip |
| Klartext | `VsaCodeResolver.LookupLabel(Code)` (Fallback: Code) | fett |
| Kategorie | VSA-Gruppe aus dem Code abgeleitet (Bestandsaufnahme→„Bestand", Betrieb→„Betrieb", Zustand→„Zustand", …) | heller Tag, rechtsbündig |

- **Kompakt** („einfach in klein"): kleinere Schrift/Padding als das Vorbild, passend in die Detail-Spalte; eigener vertikaler Scroll bei vielen Einträgen.
- **Doppelklick auf eine Zeile** → `vm.OpenProtocolCommand.Execute(vm.Selected)` (öffnet `ProtocolObservationsWindow`); nach Schließen aktualisiert sich `SelectedProtocolEntries` automatisch (bestehende Logik in `OpenProtocol`).
- **„+"-Knopf** in der Kartenüberschrift → derselbe `OpenProtocolCommand` (dort werden neue Beobachtungen codiert/hinzugefügt).
- **Read-only-Tabelle** (kein Inline-Editing) — die eigentliche Änderung macht der bestehende Editor.
- **Reine Projektion** `ProtocolEntry → SchadenZeile(Meter, Code, Klartext, Kategorie)` in einem testbaren Helfer; falls im Code bereits eine VSA-Kategorisierung/ein Zeilen-Stil dieser Art existiert (z. B. in `ProtocolObservationsWindow`/`BeobachtungenWindow`), diese **wiederverwenden** statt neu bauen.

## Teil 3 — Harmonische Anordnung
Spalte 2 der `HaltungsansichtView` wird ein vertikaler Stapel (Grid, 2 Zeilen):
- **Oben (`*`):** `RecordDetailsView` (Feld-Karten, scrollbar) — wie bisher.
- **Unten (`Auto`, max. Höhe mit eigenem Scroll):** Karte „Primäre Schäden" (gleiche Karten-Optik wie die Feldgruppen: `CardBrush`/`BorderBrush`/CornerRadius, Überschrift + Mini-Tabelle).
- Das **Popup** (Doppelklick im DataGrid → `RecordDetailsWindow`) bleibt **unverändert** — die Schäden-Tabelle erscheint nur in der Haltungsansicht (das gemeinsame `RecordDetailsView` wird dafür nicht angefasst).

## File-Struktur
| Datei | Verantwortung | Status |
|---|---|---|
| `…/DataPage/SchadenZeileFormatter.cs` | reine Projektion `ProtocolEntry → SchadenZeile(Meter, Code, Klartext, Kategorie)` | neu (testbar) |
| `tests/AuswertungPro.Next.UI.Tests/SchadenZeileFormatterTests.cs` | Test der Projektion | neu |
| `…/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml(.cs)` | ContextMenu an der Liste (→ `ActionRequested`); Schäden-Karte mit Mini-Tabelle (Doppelklick/„+" → Editor); Layout Spalte 2 | ändern |
| `…/Views/Pages/DataPage.xaml.cs` | `RouteHaltungsansichtAction` setzen + auf bestehende Handler/Commands mappen | ändern |

## Tests
- **Unit:** `SchadenZeileFormatter` — Punktschaden (nur MeterStart, „0.00 m"), Streckenschaden (MeterStart–MeterEnd), Klartext via Resolver mit Fallback auf Code, Kategorie-Ableitung aus dem Code (BC*/BD*→Bestand, BB*→Betrieb, BA*→Zustand, unbekannt→leer), gelöschte/leere Einträge übersprungen.
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
