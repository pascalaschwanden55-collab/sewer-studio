# Design-Spec: Neue Projekt-Eröffnung (Start-Bildschirm + Auto-Projektordner)

**Datum:** 2026-06-26
**Branch:** feature/gis-karte
**Status:** Entwurf vom User freigegeben (mündlich), wartet auf Spec-Review

## 1. Problem / Motivation

Die Projekt-Eröffnung ist heute auf zwei konkurrierende Menüpunkte verteilt:

- **„Übersicht"** (`OverviewPage`/`OverviewPageViewModel`) = Projekt-Starter: Liste, Suche,
  Neu/Öffnen/Fortsetzen, Öffnen/Löschen.
- **„Projekt"** (`ProjectPage`/`ProjectPageViewModel`) = Projekt-Infoblatt: Name, Auftraggeber,
  Gemeinde, Zone, Datum, Firma …

Beide sind dauerhafte Menüeinträge, **beide** tragen „Neues Projekt" und „Öffnen". Das ist doppelt
und verwirrend. Zusätzlich verlangt „Neues Projekt" heute ein manuelles Wählen des Projektordners
(`ShellViewModel.NewProject` → `SelectFolder`), was umständlich ist.

## 2. Ziel

Eine klare, „befriedigende" Eröffnung:

1. Beim Programmstart **zuerst einen Start-Bildschirm** (Projekt wählen/anlegen), erst danach der
   Arbeitsbereich mit Menü.
2. Bei „Neues Projekt": Projektdaten ausfüllen → die App legt den Projektordner **selbständig** unter
   einem in den Einstellungen hinterlegten Projekte-Verzeichnis an (kein Ordner-Dialog mehr).

## 3. Designentscheidungen (vom User bestätigt)

- **D1 — Start-Bildschirm als Gateway**, kein zweites Fenster: an `IsProjectReady` gekoppelt.
- **D2 — „Projekt wechseln" als Kopf-Knopf** (nicht als Menüpunkt) bringt zurück zum Start-Bildschirm.
- **D3 — „Neues Projekt":** Infoblatt zuerst ausfüllen, dann anlegen. Folgt aus der User-Anforderung.
- **D4 — Projekte-Verzeichnis:** neue Einstellung. Startwert leer; beim ersten Anlegen wird einmalig
  danach gefragt (Vorschlag `D:\Projekt`) und gespeichert. Danach in den Einstellungen änderbar.
- **D5 — Ordnername = Projektname.** Kollision → automatisch `Name-2`, `Name-3` …; ungültige Zeichen
  werden via `ProjectPathResolver.SanitizePathSegment` entschärft (Application/Common, public, bereits
  vorhanden) — NICHT über das private `ShellViewModel.MakeSafeFileName`.
- **D6 — Medienverteilung bleibt UNVERÄNDERT (außerhalb des Scopes).** Korrektur zum Review: „wie heute"
  heißt NICHT verlinken. Der bestehende `MediaDistributionService` **kopiert** nach jedem Import die
  Medien in den Projektordner (`ImportPageViewModel.PostImportFolderAsync` → `DistributeMediaToProjectFolder`,
  Zeile 498) und ersetzt absolute durch relative Pfade. Das passt zur Idee „alles im Projektordner" und
  wird mit dieser Änderung **nicht angefasst**. (Vom User am 2026-06-26 bestätigt — siehe Abschnitt 12.)

## 4. Shell-Zustände

Statt nur `IsProjectReady` (an/aus) braucht die Shell drei klar getrennte Zustände. Vorschlag:
ein Enum `ShellMode` in `ShellViewModel`.

| ShellMode    | Auslöser                                  | Anzeige                                           |
|--------------|-------------------------------------------|--------------------------------------------------|
| `Launcher`   | Kein Projekt offen, kein Anlegen aktiv    | Start-Bildschirm formatfüllend, **kein** Menü     |
| `Draft`      | „Neues Projekt" geklickt                   | Projekt-Infoblatt formatfüllend, **kein** Menü    |
| `Workspace`  | Projekt geöffnet oder gerade angelegt      | Arbeitsbereich **mit** Menü                        |

Übergänge:

- `Launcher → Workspace`: Projekt aus Liste öffnen / „Öffnen…" / „Letztes fortsetzen".
- `Launcher → Draft`: „Neues Projekt".
- `Draft → Workspace`: „Projekt anlegen" erfolgreich.
- `Draft → Launcher`: „Abbrechen" im Infoblatt.
- `Workspace → Launcher`: Kopf-Knopf „Projekt wechseln" (mit Rückfrage bei ungespeicherten Änderungen).

`IsProjectReady` bleibt erhalten und ist genau dann `true`, wenn `ShellMode == Workspace`.

**„Kein Menü" gilt umfassend (Finding 2):** In `Launcher`/`Draft` ist nicht nur die linke Nav-Spalte
auszublenden, sondern auch das **obere WPF-`Menu`** (`MainWindow.xaml`, „Datei" mit Neues Projekt/Öffnen/
Speichern, heute nur in `IsFocusMode` collapsed) und die **Tastenkürzel** `Strg+N/O/S`
(`Window.InputBindings`). Regelung: `Strg+N` (Neues Projekt) darf im `Launcher` aktiv bleiben;
`Strg+S` (Speichern) und `Strg+O` (Öffnen-Dialog) werden außerhalb des `Workspace` deaktiviert bzw.
mode-bewusst gemacht, damit sie ohne offenes Projekt nicht ins Leere laufen.

## 5. Betroffene Komponenten

### 5.1 `ShellViewModel`
- Neues `ShellMode`-Property (+ `IsMenuVisible => ShellMode == Workspace`).
- `NavItems`: Eintrag **„Uebersicht" entfernen**. „Projekt" bleibt.
- `NewProject()` umbauen: **nicht mehr** sofort `SelectFolder`+Save. Stattdessen: leeres `Project`
  in-memory anlegen, `ShellMode = Draft`, auf das Infoblatt schalten.
- Neuer Befehl `SwitchProjectCommand` („Projekt wechseln"): `ConfirmDiscardUnsavedChanges()` →
  `ResetProjectReady()` → `ShellMode = Launcher`.
- Neuer Befehl/Methode `CreateProjectFromDraft(...)`: validiert Name, berechnet Zielordner (siehe 5.4),
  legt Ordner an, speichert `projekt.json`, setzt `LastProjectPath`/Recent, `MarkProjectReady`,
  `ShellMode = Workspace`, `NavigateTo("Import")`.
- **Landeseiten zentral definieren (Finding 4):** Die Shell setzt Modus + Landeseite selbst; das bisherige
  `NavigateTo("Projekt")` in `OverviewPageViewModel.OpenSelectedProject/OpenProject/OpenLastProject`
  **entfällt** (sonst bricht der Öffnen-Flow). Festlegung: **Öffnen/Fortsetzen → Landeseite „Haltungen"**
  (Arbeitsdaten); **„Projekt anlegen" (neu) → Landeseite „Import"**. (Beides bei Bedarf leicht änderbar.)

### 5.2 `MainWindow.xaml`
- Menü-Spalte (linkes Nav) nur sichtbar bei `ShellMode == Workspace`.
- **Oberes WPF-`Menu`** (Datei/Werkzeuge …, Zeile 16) ebenfalls nur sichtbar bei `ShellMode == Workspace`
  — heute nur bei `IsFocusMode` collapsed; Sichtbarkeitsbedingung um den Modus erweitern.
- **`Window.InputBindings`** (Zeile 11–13: `Strg+N/O/S`, F11): `Strg+S`/`Strg+O` außerhalb des `Workspace`
  deaktivieren bzw. mode-bewusst machen; `Strg+N` darf im `Launcher` „Neues Projekt" auslösen.
- Content-Bereich zeigt je nach `ShellMode`: Start-Bildschirm / Infoblatt / aktuelle Arbeitsseite.
- Kopf-Knopf „Projekt wechseln" nur im `Workspace` sichtbar, gebunden an `SwitchProjectCommand`.

### 5.3 `OverviewPage` / `OverviewPageViewModel` (= Start-Bildschirm)
- Inhaltlich nahezu unverändert (Liste, Suche, Neu/Öffnen/Fortsetzen, Öffnen/Löschen).
- `NewProject()` ruft künftig den Draft-Flow der Shell auf (statt direkt anzulegen).
- Doppelte Navigation `NavigateTo("Projekt")` entfällt; die Shell steuert den Moduswechsel.
- **Projektliste scannt `ProjectsRootDirectory` (Finding 3):** `LoadAllProjects()` scannt heute hart
  `D:\Projekt` und `C:\Projekt` (Zeile 144). Künftig zusätzlich den in den Einstellungen gesetzten
  `ProjectsRootDirectory` inkl. direkter Unterordner — sonst erscheinen Projekte aus einem frei
  gewählten Stammordner nur über „Recent".

### 5.4 `ProjectPage` / `ProjectPageViewModel` (= Infoblatt)
- Kopfknöpfe „Neues Projekt" und „Öffnen" **entfernen** (gibt's nur noch am Start-Bildschirm).
- Im `Draft`-Modus: primärer Knopf „**Projekt anlegen**" (statt „Projekt speichern"), aktiv nur wenn
  Name nicht leer. „Abbrechen" → zurück zum Launcher.
- Im `Workspace`-Modus: wie heute „Projekt speichern" / „Speichern unter".
- **Name-Wrapper mit Benachrichtigung (Finding 6):** `Project.Name` ist eine reine Property ohne
  `INotifyPropertyChanged`, und `ProjectPageViewModel` reicht nur `Project` durch. Damit „Projekt anlegen"
  zuverlässig aktiv/inaktiv schaltet, bekommt das VM ein eigenes `[ObservableProperty] DraftName`, an das
  das Name-Feld im `Draft`-Modus bindet; dessen Änderung ruft `AnlegenCommand.NotifyCanExecuteChanged()`.

### 5.5 `AppSettings`
- Neue Property `string? ProjectsRootDirectory` (Default-Logik: leer = beim ersten Anlegen nachfragen,
  Vorschlagswert `D:\Projekt`).

### 5.6 `SettingsPage` / `SettingsPageViewModel`
- Neues Feld „Projekte-Verzeichnis" mit Ordner-Auswahl-Knopf, bindet an `ProjectsRootDirectory`.

### 5.7 `ShellNavigationPolicy`
- „Uebersicht" aus `CanOpenWithoutProject` entfernen. „Projekt", „Export", „Einstellungen" bleiben.

## 6. Neue testbare Logik (pur, ohne UI)

`NewProjectFolderPlanner` (Application/Common, analog zu vorhandenem `ProjectPathResolver`):

```text
Plan(baseDir, projectName, Func<string,bool> dirExists)
  -> { FolderPath, ProjectFilePath }
```

- entschärft ungültige Zeichen via `ProjectPathResolver.SanitizePathSegment` (Finding 5),
- hängt bei Kollision `-2`, `-3` … an,
- liefert `…\{SafeName[-n]}\projekt.json`.

Reine Funktion → direkt unit-testbar, keine Dateisystem-Seiteneffekte (Existenz via Delegate).

## 7. Fehlerbehandlung

- **Projekte-Verzeichnis leer** beim ersten Anlegen → Ordner-Dialog „Projekte-Verzeichnis wählen",
  Auswahl in Settings speichern; Abbruch → zurück zum Infoblatt (kein Projekt angelegt).
- **Ordner nicht anlegbar / nicht schreibbar** → Fehlermeldung, bleibt im `Draft`-Modus.
- **Name leer** → „Projekt anlegen" deaktiviert.
- **Ungespeicherte Änderungen** bei „Projekt wechseln" / Programm schließen → vorhandenes
  `ConfirmDiscardUnsavedChanges()`.

## 8. Tests

- **Unit:** `NewProjectFolderPlanner` — Safe-Name, Kollisions-Suffix, Pfadbau.
- **Unit:** `AppSettings` Round-Trip für `ProjectsRootDirectory`.
- **VM:** `ShellViewModel` Modusübergänge (Launcher→Draft→Workspace, Workspace→Launcher).
- **VM:** „Projekt anlegen" legt Ordner + `projekt.json` am erwarteten Pfad an (mit Temp-baseDir).
- **Guard (UiArchitectureGuardTests-Stil):** „Uebersicht" nicht mehr in `NavItems`; `ProjectPage`
  ohne „Neues Projekt"/„Öffnen"-Knöpfe; `MainWindow` bindet Menü-Sichtbarkeit an den Workspace-Modus.

## 9. Bewusst NICHT im Scope

- Arbeitsseiten (Haltungen, Schächte, Import, Export, …) — unverändert.
- Öffnen-/Speichern-/Lade-Logik selbst — unverändert.
- Medien in den Projektordner kopieren (= „Verteilung") — läuft heute automatisch nach jedem echten
  Import und bleibt unverändert.
- Kein zweites Fenster für den Start-Bildschirm.

## 10. Hinweis zur Umsetzung

Codex zerlegt parallel aktiv das `PlayerWindow` auf demselben Branch. Diese Änderungen liegen in
anderen Dateien (Shell, MainWindow, Overview/Project-Page, Settings, AppSettings) und kollidieren
nicht mit dem PlayerWindow. Trotzdem: getrennt committen, Codex' Dateien nicht anfassen
(siehe Lehre „ein Worktree pro Agent").

## 11. Review-Befunde eingearbeitet (2026-06-26)

User-Review am Code verifiziert und eingearbeitet:

- **F1 (Hoch):** D6 korrigiert — Medien werden heute schon in den Projektordner **kopiert**
  (`MediaDistributionService`); „nur verlinken" war falsch. Verteilung bleibt unverändert/out-of-scope.
- **F2 (Hoch):** Oberes WPF-`Menu` + `Strg+N/O/S` in Abschnitt 4 und 5.2 mit aufgenommen.
- **F3 (Mittel):** `OverviewPageViewModel` scannt künftig `ProjectsRootDirectory` (5.3).
- **F4 (Mittel):** Landeseiten zentral definiert (Öffnen→Haltungen, Neu→Import); `NavigateTo("Projekt")`
  in der Übersicht entfällt (5.1).
- **F5 (Mittel):** `ProjectPathResolver.SanitizePathSegment` statt privatem `MakeSafeFileName` (D5, 6).
- **F6 (Mittel):** VM-`DraftName` mit `INotifyPropertyChanged` für „Projekt anlegen"-CanExecute (5.4).

## 12. Bestätigte Entscheidung (Medien)

Wegen der Korrektur in F1/D6: Heute werden importierte Medien in den Projektordner **kopiert**
(passt zu „alles im Projektordner"). **Vom User am 2026-06-26 bestätigt:** so lassen — die
Medienverteilung wird in dieser Änderung nicht angefasst. Ein späterer Wechsel auf „nur verlinken"
wäre ein eigener, separater Auftrag.
