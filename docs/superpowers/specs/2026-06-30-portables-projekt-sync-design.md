# Selbst-enthaltenes, portables Projekt mit Programm↔Filesystem-Sync — Design

> Stand 2026-06-30. Konsolidiert aus mehreren Klärungen mit dem User. Ziel: ein Projektordner, der überallhin kopierbar ist und beim Öffnen vollständig auflöst — und der bei Programm-Änderungen konsistent mitzieht.

## Vision (ein Satz)
**Der Projektordner ist die vollständige, portable, lebende Wahrheit:** Ordner auf einen beliebigen PC/Pfad kopieren → `.json` auswählen → alles ist da (Videos, Fotos, Protokolle, Verknüpfungen), und Änderungen im Programm ziehen die Ordner/Dateien nach.

## Leitprinzipien
1. **Alle Medien liegen im Projektordner** (Videos, Fotos, Protokoll-PDFs) — sind sie bereits.
2. **Alle Pfade sind relativ** zum Projektordner. Kein einziger absoluter Pfad darf übrigbleiben (ein einziger bricht die Portabilität).
3. **Filesystem = lebende Repräsentation:** Programm-Edits, die Benennung/Struktur betreffen, ziehen Ordner + Dateien nach.
4. **Korrekte Bindung:** Fotos sind pro Befund verlinkt und überall sichtbar (Beobachtungen, AWU-PDF, Original-Anzeige) — nicht nur abgelegt.

## Anforderungen

### A — Portabilität (relativ verlinken)
- Alle Medienpfade (`Link`, `PDF_Path`, `PDF_All`, `entry.FotoPaths`, `finding.FotoPath`) **relativ** zum Projektordner.
- Beim Öffnen löst `ProjectPathResolver` relativ gegen den `.json`-Ordner auf → funktioniert auf jedem PC/Laufwerk.
- **Wichtig (Korrektur):** Die Dateien sind schon im Projekt — es ist **kein Kopier-, sondern ein Link-Problem.** Heute zeigen Pfade absolut auf den Ursprung → brechen beim Verschieben und werden von der Anzeige (Inside-Projekt-Guard) abgelehnt.

### B — Fotos korrekt gebunden + überall sichtbar
- `FotoPath` zeigt relativ auf die Projekt-Kopie → Beobachtungen-Fenster, AWU-PDF und Original-Anzeige zeigen das Bild.
- Die Pro-Befund-Zuordnung kommt aus dem Import (WinCan/XTF setzen `finding.FotoPath`/`entry.FotoPaths`); nur der **Pfad** wird relativ gemacht, die Bindung bleibt.
- Foto-Quellorte je Format konsistent behandeln: `…\Foto\` (XTF/Meien), `…\DISK1\Projects\…\Picture\` (WinCan/Altdorf) — Auflösung per **Dateiname-Suche im gesamten Projektordner** (formatunabhängig).

### C — Programm → Filesystem-Sync
- **Haltungsname ändern** → Ordner + Datei-Namen (Video, `_G`, PDFs, Fotos) + alle Pfad-Felder + Haltungsnummer im PDF-Text ziehen mit. *(Großteils bereits gebaut: `HoldingRenameService` + Datei-Rename-Fix + PDF-Text-Rewrite.)*
- Daten-Edits (Beobachtungen, Massnahmen …) → Projekt-`.json` (Auto-Save vorhanden).
- Regel: jede Änderung, die Benennung/Struktur betrifft, hält den Projektordner konsistent.

### D — Verteilung im Projekt
- Verteilung schreibt nach `<Projekt>\Verteilung\<Haltung>\` (bzw. `Haltungen\`) **innerhalb** des Projekts, nicht extern (`D:\Haltungen`) → sonst zeigt `Link` nach außen.

### E — Bestehende Projekte reparieren
- „Projekt portabel machen / Verknüpfungen reparieren"-Aktion: setzt alle Medienpfade relativ auf die vorhandenen Projekt-Kopien — **ohne Neu-Import** (Meien/Altdorf sofort reparierbar).

## Ist-Zustand (schon vorhanden / gebaut)
- `MediaDistributionService` (läuft beim Import): kopiert/relativiert Medien nach `Haltungen\<Haltung>\…`. Bones der Portabilität.
- `ProjectPathResolver`: relative Auflösung gegen Projektordner.
- `HoldingRenameService` + Datei-Rename-Fix (`-`/`_G`) + PDF-Text-Rewrite: Haltungsname-Sync. *(2026-06-30 gemergt.)*
- „Original (PDF) öffnen" → Haltung-spezifisches Verteilungs-PDF. *(gemergt.)*
- Foto-Exporter verlangt relativ + Inside-Projekt (`ProtocolPdfExporter.cs:91-96`).

## Zu bauen
1. **Relink-Pass (Kern):** alle Medienpfade → relativ auf die vorhandene Projekt-Kopie; Datei per Namen im **gesamten** Projektordner finden (nicht nur `Haltungen\`); absolute-außerhalb → Projekt-Kopie; absolute-innerhalb → relativ; relativ-kaputt → reparieren. Keine Duplikate, kein Neu-Kopieren wenn schon im Projekt.
2. **Trigger:** als Reparatur-Aktion (bestehende Projekte) **und** automatisch nach Import/Verteilung.
3. **Verteilung ins Projekt** verankern (D).
4. **Verifikation:** Beobachtungen + AWU zeigen Fotos; Kopier-Test (Projektordner an anderen Pfad → `.json` öffnen → alles da).

## Offene Entscheidungen
- **Reparatur-Aktion: wo auslösen?** Eigener Knopf (z.B. in Einstellungen/Projekt-Menü) vs. automatisch beim Öffnen eines Projekts mit absoluten Pfaden.
- **Verteilungs-Ordnername:** `Verteilung\` (User-Wortwahl) vs. bestehendes `Haltungen\`.

## Verifikation (Definition of Done)
- Projektordner an einen fremden Pfad kopieren, `.json` öffnen → Video-Play, Fotos in Beobachtungen, AWU-PDF mit Fotos, Original-PDF — alles funktioniert ohne manuelles Verlinken.
- Haltung im Programm umbenennen → Ordner/Dateien/PDF-Text ziehen mit, Verknüpfungen bleiben.

## Anschluss-Feature: XTF-Export (korrigierte Version vom Original)
Am Ende eines Projekts: **vollständigen Datensatz als XTF zurückexportieren** (für Lieferung an Werk/Kataster).
- **Ansatz (User 2026-06-30): „korrigierte Version vom Original".** Die **originale, schema-gültige XTF** (liegt im Projekt) als Basis nehmen und mit den Programm-Korrekturen + erfassten Befunden/Massnahmen zurückschreiben — NICHT das INTERLIS-Modell neu nachbauen. Garantiert Schema-Konformität; nur die **Änderungen/Ergänzungen** werden eingepflegt. (Analog zur PDF-Korrektur: Original + Patch.)
- **Ziel:** vollständiger, round-trip-fähiger Datensatz (Stammdaten + Schächte + Befunde + Quantifizierung + Massnahmen), „die richtigen Daten" korrekt gemappt.
- **Ist-Zustand:** Es gibt heute NUR XTF-Import (Stammdaten lesen), KEINEN Export (alter Writer entfernt).
- **Braucht zum Bau:** (1) erwartetes Modell — **SIA405** (User hatte `…_SIA405.xtf`) vs. VSA-DSS-Kanalisation; (2) eine echte **Beispiel-XTF der Gegenstelle** als Schema-Vorlage; (3) falls Befunde NICHT in der Original-XTF stehen (kamen aus PDF), muss der Untersuchungs-/Schadens-Teil als XTF-Elemente ergänzt werden → dafür eine XTF-Vorlage MIT Untersuchungen.
- **Reihenfolge:** NACH der Portabilität/Relink.
