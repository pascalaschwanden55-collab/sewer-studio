# Codes in Klarschrift — Design

**Datum:** 2026-07-22
**Ziel:** Überall in der App, wo heute nur ein nackter VSA-Code steht (z.B. `BCAEA`),
soll zusätzlich die offizielle Katalog-Bedeutung erscheinen (z.B. „Anschluss eingespitzt").
Besonders beim Codieren.

## Entscheidung (vom Nutzer freigegeben)

- **Darstellung:** Code + Bedeutung **nebeneinander** (nicht als Tooltip).
- **Umfang:** alle Stellen mit nacktem Code **in einem Rutsch**.
- **Tabellen:** eigene schmale Spalte „Bedeutung" (nicht in dieselbe Zelle gequetscht).

## Baustein

- `VsaCodeResolver.LookupLabel(code)` liefert den Katalog-Klartext. Existiert bereits.
- `VsaCodeToTextConverter` (in `Views/Windows/TrainingCenterConverters.cs`) macht daraus
  `"CODE — Klartext"`. Bei unbekanntem/leerem Wert bleibt der Wert unverändert. Existiert bereits,
  ist aber nur lokal im TrainingCenter registriert.

## Zwei Anzeige-Situationen → zwei Converter

| Situation | Converter | Ausgabe | Verwendung |
|---|---|---|---|
| Panel/Chip/Liste (nur der Code steht da) | `VsaCodeToTextConverter` (**vorhanden**) | `BCAEA — Anschluss…` | inline, ersetzt den nackten Code-Text |
| Tabelle (Code-Spalte existiert schon) | `VsaCodeToLabelConverter` (**neu**) | `Anschluss…` (nur Klartext) | neue Spalte „Bedeutung" neben der Code-Spalte |

Der neue `VsaCodeToLabelConverter` gibt **nur** den Klartext zurück (kein Code-Präfix, sonst
stünde der Code doppelt in der Zeile). Unbekannter/leerer Code → leerer String.

## Zentrale Registrierung

Beide Converter einmal in `App.xaml` als Ressource registrieren (`{StaticResource VsaCodeToText}`
und `{StaticResource VsaCodeToLabel}`), damit alle Fenster sie ohne eigene Deklaration nutzen.
Die bisherige **lokale** Registrierung in `TrainingCenterWindow.xaml` entfällt (nutzt dann die zentrale).

## Betroffene Stellen (vollständig)

**Inspekteur-UI (Kern):**
- `Views/ProtocolObservationsWindow.xaml:111` — DataGrid-Code-Badge → neue Spalte „Bedeutung"
- `Views/Windows/BeobachtungenWindow.xaml:105` — DataGrid „OP Kürzel" → neue Spalte „Bedeutung"
- `Views/Windows/PlayerCodingSidePanel.xaml:202` — Listen-Item `Entry.Code` → inline
- `Views/Windows/PlayerCodingSidePanel.xaml:440` — zweite Liste `Entry.Code` → inline

**KI/Training-UI:**
- `Views/Windows/TrainingCenterWindow.xaml:214` — Samples-Grid → neue Spalte „Bedeutung"
- `TrainingCenterWindow.xaml:242` — Detail-`Run` `SelectedSample.Code` → inline
- `TrainingCenterWindow.xaml:410` — Statusleiste `CurrentEntryCode` → inline
- `TrainingCenterWindow.xaml:558` — Ergebnis-Verlauf `VsaCode` → inline
- `TrainingCenterWindow.xaml:722` — Review-Queue `SelfTrainingVsaCode` → inline
- `TrainingCenterWindow.xaml:1009` — Teacher-Thumbnail `VsaCode` → inline

**Sonderfälle:**
- `TrainingCenterWindow.xaml:607` — Chart-Balken-Label `Code`: Klartext als **Tooltip**
  (inline würde das Diagramm überladen). Label bleibt der Code.
- `Views/ProtocolEntryEditorDialog.xaml:57` — getippter Code (`CodeTextBox`): ein
  Klartext-`TextBlock` unter der Box, gebunden an `CodeTextBox.Text` via Converter, aktualisiert live.

**Kein Handlungsbedarf** (zeigen die Bedeutung bereits): Code-Auswahl-Dialog, Katalog-Editoren,
HaltungsansichtView, PlayerWindow, TrainingStudioWindow, VsaCodeExplorerWindow, TrainingCenter:778/786.

## Absicherung

- Fokussierter Test für `VsaCodeToLabelConverter`: bekannter Code → Klartext; unbekannt/leer/null → leer.
- Der bestehende `VsaCodeToTextConverter` bleibt unverändert (nur zentral registriert).
- Volle Suite muss grün bleiben (~10'277 Tests). QualityGate/VRAM unberührt (reine Anzeige).

## Architektur

Rein additive Anzeige-Schicht (WPF-Converter + XAML). Keine Geschäftslogik, kein Sidecar,
keine neuen Abhängigkeiten. Katalog-Zugriff nur lesend über den vorhandenen `VsaCodeResolver`.
