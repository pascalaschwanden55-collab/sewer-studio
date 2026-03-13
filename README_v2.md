# AuswertungPro v2.1.0 - Professional Edition

## Quick Start

```powershell
cd f:\AuswertungPro
.\HaltungsAuswertungPro_v2.ps1
```

Neue Anwendung mit neuer Architektur. Die alte `HaltungsAuswertungPro.ps1` (Karten-Layout) bleibt erhalten.

---

## Architektur-Übersicht

### Alte vs. Neue Version

| Aspekt | v1 (alt) | v2 (neu) |
|--------|----------|----------|
| UI | Karten-Layout (StackPanel) | **DataGrid (25 Spalten, Excel-ähnlich)** |
| Struktur | Monolithisch (2251 Zeilen) | **Modular (Services)** |
| Import | Ad-hoc | **Zentrale Merge-Logik, Regex-Mapping** |
| Tracking | Fragmented | **FieldMetadata pro Feld pro Haltung** |
| Exports | Excel COM | **Excel + CSV (robust)** |
| Fehler | Fehlgeleitete Dialoge | **Zentrales Logging, Best-Effort Import** |

### Service-Struktur

```
Services/
├── Models.ps1              # HaltungRecord, Project, FieldMetadata (Classes)
├── LoggingService.ps1      # Zentrale Log-Infrastruktur
├── ValidationService.ps1   # Input-Validierung + Normalisierung
├── MergeService.ps1        # KERNSTÜCK: Merge-Logik + Konflikt-Handling
├── ProjectStorageService.ps1  # JSON speichern/laden, atomar, Backups
├── AutosaveService.ps1     # Autosave + Crash-Recovery
├── XtfImportService.ps1    # XTF parsing (VSA_KEK standard)
├── Xtf405ImportService.ps1 # XTF SIA405 parsing (höhere Priorität)
├── PdfImportService.ps1    # PDF + Regex-Mapping (25+ Felder)
├── ExcelExportService.ps1  # Excel COM export
└── Bootstrap.ps1           # Service-Initialisierung
```

---

## Projektstruktur (Disk)

```
Projekte/
├── {ProjectId}.haltproj             # JSON Hauptdatei
├── backups/
│   ├── {ProjectId}_20260125_144530.haltproj
│   └── ...
└── ...

logs/
├── app.log                          # Info + Debug
├── errors.log                       # Nur Fehler
└── imports.log                      # Import-Historie

Rohdaten/
├── *.xtf                            # Quell-XTF Dateien
└── ...
```

---

## Kernkonzept: Datenintegrität

### Regel 1: UserEdited = true → NIEMALS Überschreiben

```powershell
# Wenn Nutzer manuell einen Wert editiert hat:
if ($FieldMeta.UserEdited -eq $true) {
    → Import darf NICHT überschreiben
    → Markiere als Konflikt statt Fehler
    → Protokolliere: "Importwert XYZ verfügbar, aber nicht überschrieben"
}
```

### Regel 2: Leere Felder dürfen gefüllt werden

```powershell
if ($CurrentValue -eq "" -and $UserEdited -eq $false) {
    → Import darf wert setzen
    → FieldMeta.Source = new source
    → FieldMeta.UserEdited = false
}
```

### Regel 3: Priorität bei Überschreibung

```
manual (10) > pdf (7) > xtf405 (5) > xtf (3)

Wenn zwei Quellen unterschiedliche Werte haben:
→ Höhere Priorität gewinnt
→ Nur wenn UserEdited = false
```

---

## Nutzung: Täglicher Workflow

### 1. Neues Projekt starten

```
Toolbar: [🆕 Projekt] oder Ctrl+N
→ Leeres Projekt erstellt
→ Automatische Projekt-ID generiert
```

### 2. Haltungen hinzufügen

```
Toolbar: [➕ Haltung] oder Insert
→ Neue Zeile mit Auto-NR
→ Bearbeitbar im DataGrid
```

### 3. XTF Importieren

```
Toolbar: [📥 XTF]
→ Datei-Dialog → select .xtf
→ "X Haltungen gefunden" Dialog
→ Ja = Alle importieren
→ Neue Haltungen + Merge existierende
```

**Merge-Verhalten bei XTF:**
- Neue Zeilen werden erzeugt
- Existierende Haltungen werden gefunden (by Haltungsname oder Strasse+DN+Länge)
- Nur leere Felder oder nicht-editierte Felder werden gefüllt
- UserEdited-Felder: Konflikte protokolliert, nicht überschrieben

### 4. PDF Batch-Import (mehrere Haltungen)

```
Toolbar: [📄 PDF]
→ Datei-Dialog → select .pdf
→ PDF wird automatisch in Haltungs-Chunks gesplittet
→ Alle Haltungen werden importiert (neu oder update)
→ Bei unsicherer Zuordnung: Vorschau-Liste + Bestätigung
→ Felder gefüllt: Zustandsklasse, Schäden, Sanieren, Kosten, Datum, etc.
→ Summary: Gefunden / Neu / Aktualisiert / Konflikte / Fehler
```

**Merge-Verhalten bei PDF:**
- UserEdited-Felder werden nie überschrieben
- Leere Felder dürfen gefüllt werden
- Konflikte werden protokolliert (Details-Dialog)

**Typische Marker (für Chunking):**
- `Haltungsname`, `Haltungsnahme`, `Haltung ID`, `Leitung ID`
- `Haltung <ID>` / `Leitung <ID>`
- `Haltungsinspektion - DD.MM.YYYY - <ID>`

**Troubleshooting:**
- Keine Texte erkannt → Poppler/pdftotext installieren
- Fehlende IDs → Import erzeugt `UNBEKANNT_*` + Bemerkung "Zu pruefen"
- Falsche Zuordnung → Import-Preview prüfen + Regex erweitern (PdfImportService)

### 5. Speichern & Export

```
Speichern:
Toolbar: [💾 Speichern] oder Ctrl+S
→ JSON atomar geschrieben (temp → replace)
→ Backup auto-erstellt (timestamped)
→ Autosave läuft parallel (3 Minuten Intervall)

Export:
Toolbar: [📊 Excel] oder Ctrl+E
→ Save-Dialog
→ 25 Spalten, Header fett, AutoFilter, FreezePane
→ Formatierung + Hyperlinks
```

### 6. Suche & Filter

```
Suche:
Toolbar: [🔍 Suche] oder Ctrl+F
→ Dialog: "Haltungsname, Strasse, Bemerkungen durchsuchen"
→ Filter DataGrid

Filter zurücksetzen:
Toolbar: [🔄 Zurücksetzen]
```

---

## Tastatur-Shortcuts

| Shortcut | Aktion |
|----------|--------|
| Ctrl+N | 🆕 Neues Projekt |
| Ctrl+O | 📂 Projekt öffnen |
| Ctrl+S | 💾 Speichern |
| Insert | ➕ Haltung hinzufügen |
| Delete | 🗑 Zeile löschen |
| Ctrl+E | 📊 Excel Export |
| Ctrl+F | 🔍 Suche |
| Ctrl+Alt+R | 🔄 Filter zurücksetzen |

---

## PDF-Feld-Mapping (Regex)

Die Datei `Services\PdfImportService.ps1` enthält die zentrale Mapping-Tabelle.

### Beispiel: Zustandsklasse

```powershell
'Zustandsklasse' = @{
    Regexes = @(
        '(?im)^\s*(Zustandsklasse|Zustand|ZK)\s*[:\-]?\s*([0-9])\b'
    )
    Multiline = $false
    PostProcessor = { param($v) $v.Trim() }
}
```

**Pattern:**
- `(?im)` = Case-insensitive, Multiline
- `^\s*` = Zeilanfang + optionales Whitespace
- `(Zustandsklasse|Zustand|ZK)` = Label-Varianten
- `[:\-]?` = optionaler Trenner (: oder -)
- `([0-9])` = Digit (Capture Group)

### Neue Felder hinzufügen

1. Öffne `Services\PdfImportService.ps1`
2. Finde `$script:PdfFieldMapping`
3. Ergänze neuen Eintrag:

```powershell
'NeuFeld' = @{
    Regexes = @(
        '(?im)^\s*(Neu.*Feld|Label.*Variante)\s*[:\-]?\s*(.+?)\s*$'
    )
    Multiline = $true          # Wenn mehrzeilig (bis 5 Zeilen)
    MaxLines = 5               # Max. Zeilen sammeln
    PostProcessor = { 
        param($v)
        $v -replace "unwanted_text", ""  # Bereinigung
        $v.Trim()
    }
}
```

4. Optional: Teste mit `Parse-PdfText -Text $testPdfContent`

---

## Logging & Debugging

### Log-Dateien

```
logs/
├── app.log         # Alle Events (Info, Debug, Warn)
├── errors.log      # Nur Fehler + Stack-Traces
└── imports.log     # Import-Historie (zeitgestempelt)
```

### Logs ansehen

```powershell
# Letzte 50 Zeilen app.log:
Get-LogContent -LogType App -LastLines 50

# Alle Fehler:
Get-LogContent -LogType Errors

# Import-Historie:
Get-LogContent -LogType Imports
```

### Crash-Recovery

Falls die App beim Save crasht:
- Datei `data.json.crash` wird erstellt
- Beim Neustart: "Crash-Recovery erkannt?" Dialog
- Ja = Aus Crash-Recovery wiederherstellen
- Nein = Verwerfen + normal laden

---

## Entwickler-Guide

### Service laden / testen

```powershell
# In PowerShell ISE:
. .\Services\Bootstrap.ps1

# Jetzt verfügbar:
# - New-Project, Load-Project, Save-Project
# - Merge-Field, Merge-Record
# - Parse-XtfFile, Import-XtfRecordsToProject
# - Parse-PdfText, Import-PdfToRecord
# - Export-ProjectToExcel
# - Write-Log, Log-Info, Log-Warn, Log-Error
```

### Test-Projekt erstellen

```powershell
$proj = New-Project
$proj.Name = "Test Project"
$proj.Metadata.Zone = "6.19"

$rec = $proj.CreateNewRecord()
$rec.SetFieldValue('Haltungsname', 'H001', 'manual', $false)
$rec.SetFieldValue('Strasse', 'Hauptstrasse', 'manual', $false)
$rec.SetFieldValue('DN_mm', '200', 'manual', $false)
$proj.AddRecord($rec)

Save-Project $proj "test.haltproj"

# Wieder laden:
$loaded = Load-Project "test.haltproj"
$loaded.Data.Count  # → 1
```

### Merge-Logik testen

```powershell
$proj = Load-Project "test.haltproj"
$rec = $proj.Data[0]

# User editiert Feld:
$rec.SetFieldValue('Zustandsklasse', '3', 'manual', $true)

# Jetzt beim Import:
$importRec = New-Object HaltungRecord
$importRec.SetFieldValue('Zustandsklasse', '2', 'pdf', $false)

$mergeResult = Merge-Field `
    -FieldName 'Zustandsklasse' `
    -CurrentValue '3' `
    -NewValue '2' `
    -FieldMeta $rec.FieldMeta['Zustandsklasse'] `
    -NewSource 'pdf'

# → $mergeResult.Merged = $false (nicht überschrieben!)
# → $mergeResult.Conflict = [Konflikt-Details]
# → Message = "UserEdited=true, nicht überschrieben"
```

### Validierung & Normalisierung

```powershell
# Dezimalzahl normalisieren:
$val = Normalize-DecimalValue -Value "1'234,56"
# → "1234.56"

# Kostenwert:
$cost = Normalize-CostValue -Value "CHF 12'500.-"
# → "12500.00"

# Datum:
$date = Normalize-DateValue -Value "25.01.2026"
# → "25.01.2026"  oder parsebar DateTime
```

---

## Häufig gestellte Fragen (FAQ)

### F: Warum wird mein editiertes Feld beim Import nicht überschrieben?
**A:** Das ist absichtlich! Wenn Sie einen Wert manuell geändert haben (UserEdited=true), schützt das System Ihren Wert. Der Importwert wird als "Konflikt" protokolliert, nicht verloren, aber nicht automatisch aktualisiert.

### F: Wie sehe ich Konflikte?
**A:** Nach Import: "Import: 12 Felder gefüllt, **3 Konflikte**" → Dialog öffnen zeigt Details (welches Feld, alter Wert, neuer Wert, Grund).

### F: Kann ich Änderungen rückgängig machen?
**A:** Aktuell: Undo/Redo pro Zelle (bei Doppelklick). Kompletter Undo: Projekt ohne Speichern schließen + wiedereröffnen.

### F: Wie lange laufen die Backups?
**A:** Letzte 20 Backups werden behalten. Ältere werden gelöscht.

### F: Kann ich ein Projekt umbenennen?
**A:** Ja: Projekt öffnen → In `project.json` den Namen ändern oder über "Edit Metadata" Dialog (noch nicht implementiert).

### F: Kann ich mehrere PDFs auf einmal importieren?
**A:** Ja. Mehrfachauswahl im Dateidialog möglich. Bei unsicheren Chunks erscheint eine Vorschau zur Bestätigung.

---

## Bekannte Einschränkungen (v2.1.0)

- ⚠️ DataGrid ist noch basic (kein Drag-Drop, Kontextmenü partial)
- ⚠️ PDF-Extraktion braucht externe Tools (texttract, PdfSharp)
- ⚠️ XTF/XTF405 Parsing ist simpel (nur basic VSA_KEK)
- ⚠️ Suche/Filter UI noch Stubs
- ⚠️ Undo/Redo pro Zelle nur

---

## Roadmap (v2.2+)

- [ ] DataGrid Kontextmenü: Duplizieren, Link öffnen
- [ ] Erweiterte Suche/Filter
- [ ] Undo/Redo global
- [ ] Projekt-Metadaten-Editor (Zone, Firma, Bearbeiter)
- [ ] Conflict-Resolution-UI (Pro Konflikt: Accept / Reject / Manual)
- [ ] Export als PDF-Report
- [ ] Daten-Validierungs-Report beim Speichern

---

## Support & Kontakt

Fehler/Logs: `logs/`
Feature-Anfragen: `ARCHITECTURE.md`

---

**Version:** 2.1.0  
**Datum:** 2026-01-25  
**Lizenz:** Internal Use
