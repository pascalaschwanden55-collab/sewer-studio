# AuswertungPro - Professionelle Architektur

> HINWEIS (historisch): Dieses Dokument beschreibt vor allem die fruehere PowerShell-Architektur.
> Fuer die aktuelle C#/.NET Umsetzung gelten primaer:
> - `README.md`
> - `docs/Programmpruefung_AuswertungPro_2026-02-20.md`


## Ãœberblick

**AuswertungPro** ist ein tÃ¤gliches Excel-Ã¤hnliches Haltungs-Verwaltungs-Tool fÃ¼r die Schweizer Kanalinspektion und -sanierung mit:
- **25-Spalten DataGrid** Hauptansicht (kompakt, schnell, tastaturfreundlich)
- **Robuster Multi-Source Import**: XTF, XTF SIA405, PDF (mit Merge-Logik)
- **Datenschutz**: Nutzer-Eingaben werden NIE Ã¼berschrieben
- **Excel-Export** mit Formatierung
- **Autosave & Backups** fÃ¼r tÃ¤gliche StabilitÃ¤t

---

## Projektstruktur (aktuell)

```
AuswertungPro/
â”œâ”€â”€ HaltungsAuswertungPro.ps1        (WPF-Hauptanwendung, 2251 Zeilen monolithisch)
â”œâ”€â”€ AuswertungTool.ps1               (WinForms-Alternative, legacy)
â”œâ”€â”€ export_haltungen.ps1, pdf_auswertung.ps1  (CLI-Helfer)
â”œâ”€â”€ Projekte/                        (Projektdaten: *.haltproj JSON)
â”œâ”€â”€ Rohdaten/                        (XTF-Quellen)
â”œâ”€â”€ logs/                            (Fehler/Import-Logs)
â””â”€â”€ ...
```

**Problem aktuell**: `HaltungsAuswertungPro.ps1` ist monolithisch (2251 Zeilen):
- Karten-Layout (nicht DataGrid)
- Imports/Exports vermischt
- Merge-Logik unklar
- Fehlerbehandlung ad-hoc

---

## Neuarchitektur (MODULAR)

Ohne C#-Umschreiben, nur PowerShell-Module:

```
AuswertungPro/
â”œâ”€â”€ HaltungsAuswertungPro.ps1        (UI Entry Point, ~500 Zeilen)
â”œâ”€â”€ Services/
â”‚   â”œâ”€â”€ Models.ps1                   (HaltungRecord, FieldMeta, etc.)
â”‚   â”œâ”€â”€ ProjectStorageService.ps1    (JSON speichern/laden, atomar, backups)
â”‚   â”œâ”€â”€ MergeService.ps1             (Zentrale Merge-Logik: Regeln + Tracking)
â”‚   â”œâ”€â”€ XtfImportService.ps1         (XTF parsing + mapping)
â”‚   â”œâ”€â”€ Xtf405ImportService.ps1      (XTF SIA405, PrioritÃ¤t Ã¼ber XTF)
â”‚   â”œâ”€â”€ PdfImportService.ps1         (PDF parsing + Regex-Mapping)
â”‚   â”œâ”€â”€ ExcelExportService.ps1       (Excel COM, 25 Spalten, Formatierung)
â”‚   â”œâ”€â”€ LoggingService.ps1           (Zentrales Logging)
â”‚   â””â”€â”€ ValidationService.ps1        (Eingabe-Validierung)
â”œâ”€â”€ UI/
â”‚   â”œâ”€â”€ Views/
â”‚   â”‚   â”œâ”€â”€ MainWindow.xaml.ps1      (DataGrid-Layout)
â”‚   â”‚   â””â”€â”€ Dialogs.ps1              (Import-Dialoge, etc.)
â”‚   â””â”€â”€ ViewModels/ (optional, wenn ohne MVVM aber logisch getrennt)
â”‚       â””â”€â”€ GridViewModel.ps1        (Grid-Daten + Befehle)
â”œâ”€â”€ Projekte/
â”œâ”€â”€ Rohdaten/
â”œâ”€â”€ logs/
â””â”€â”€ README_ARCHITECTURE.md
```

---

## Datenmodell

### HaltungRecord (PSCustomObject)

```powershell
@{
    Id           = [guid]          # Eindeutig pro Projekt
    NR           = [int]           # Sortierbar, editierbar
    Haltungsname = [string]        # ID aus XTF
    Strasse      = [string]
    # ... (25 Spalten gesamt)
    Datum_Jahr   = [string]        # dd.mm.yyyy oder yyyy
}
```

### FieldMeta (pro Feld pro Haltung)

```powershell
@{
    FieldName   = "Zustandsklasse"
    Source      = "manual" | "xtf" | "xtf405" | "pdf"  # Quelle
    UserEdited  = $true | $false   # Wurde manuell geÃ¤ndert?
    LastUpdated = [datetime]       # Wann?
    Conflict    = @{               # Falls Importwert != aktuell + UserEdited=true
        Source = "pdf"
        Value  = "2"
        Reason = "UserEdited, nicht Ã¼berschrieben"
    }
}
```

### Projekt (JSON)

```json
{
  "Version": 2,
  "Name": "BÃ¼rglen Klausenstrasse",
  "Created": "2026-01-25T10:15:00Z",
  "Modified": "2026-01-25T14:45:00Z",
  "AppVersion": "2.1.0",
  "Metadata": {
    "Zone": "6.19",
    "Firm": "XYZ AG",
    "Contact": "max@example.ch"
  },
  "Data": [
    {
      "Id": "guid-1",
      "Fields": {
        "NR": 1,
        "Haltungsname": "H001",
        ...
      },
      "FieldMeta": {
        "Haltungsname": { "Source": "xtf", "UserEdited": false, "LastUpdated": "..." },
        "Zustandsklasse": { "Source": "pdf", "UserEdited": true, "LastUpdated": "..." }
      }
    }
  ],
  "ImportHistory": [
    { "Time": "...", "Type": "XTF", "File": "...", "Count": 12, "Errors": 0 }
  ]
}
```

---

## Kernregeln (MergeService)

### 1. **Niemals UserEdited Ã¼berschreiben**
```
if (FieldMeta.UserEdited == true) {
    â†’ NO-OVERWRITE
    â†’ markiere als "Konflikt" statt fehler
    â†’ logge: "Nutzer hat Wert XYZ, Import hat ABC"
}
```

### 2. **Leere Felder dÃ¼rfen importiert werden**
```
if (CurrentValue == "" && Source < Priority) {
    â†’ SET Value
    â†’ Source = new source
    â†’ UserEdited = false
}
```

### 3. **PrioritÃ¤t bei Ãœberschreibung**
```
Priority: manual > pdf > xtf405 > xtf

if (!UserEdited && CurrentSource < NewSource) {
    â†’ UPDATE
    â†’ Source = NewSource
    â†’ LastUpdated = now
}
```

### 4. **Event-Suppression wÃ¤hrend Import**
```powershell
# Beim setzen von Werten via Import:
$script:SuppressFieldEvents = $true
$textBox.Text = $newValue
# Handler ignoriert diese Ã„nderung
$script:SuppressFieldEvents = $false
```

---

## 25 Spalten (Final, nach Anforderung)

| # | Spalte | Typ | Quelle | Notes |
|---|--------|-----|--------|-------|
| 1 | NR. | int | manuell | Autogenerierung optional |
| 2 | Haltungsname (ID) | text | XTF | Matching-Key |
| 3 | Strasse | text | XTF |  |
| 4 | Rohrmaterial | combo | XTF | PVC/PE/PP/GFK/Beton/... |
| 5 | DN mm | int | XTF | 20..3000 |
| 6 | Nutzungsart | combo | XTF | Schmutzwasser/Regenwasser/Misc |
| 7 | HaltungslÃ¤nge m | decimal | XTF |  |
| 8 | Fliessrichtung | combo | XTF/PDF |  |
| 9 | PrimÃ¤re SchÃ¤den | text (multi) | PDF | + Schadencodes |
| 10 | Zustandsklasse | combo | PDF | 0..5 oder 1..5 |
| 11 | PrÃ¼fungsresultat | text | PDF |  |
| 12 | Sanieren Ja/Nein | combo | PDF | Ja/Nein |
| 13 | Empfohlene Sanierungsmassnahmen | text (multi) | PDF |  |
| 14 | Kosten | decimal | PDF | CHF, normalisieren |
| 15 | EigentÃ¼mer | text | XTF/PDF |  |
| 16 | Bemerkungen | text (multi) | PDF |  |
| 17 | Link | text | PDF | Regex URL |
| 18 | Renovierung Inliner Stk. | int | PDF |  |
| 19 | Renovierung Inliner m | decimal | PDF |  |
| 20 | AnschlÃ¼sse verpressen | int | PDF |  |
| 21 | Reparatur Manschette | int | PDF |  |
| 22 | Reparatur Kurzliner | int | PDF |  |
| 23 | Erneuerung Neubau m | decimal | PDF |  |
| 24 | offen/abgeschlossen | combo | manuell | offen/abgeschlossen |
| 25 | Datum/Jahr | text/date | PDF | dd.mm.yyyy oder yyyy |

---

## Services Detail

### ProjectStorageService

**Funktionen:**
- `New-Project()`: Leeres Projekt mit Metadaten
- `Load-Project($path)`: LÃ¤dt JSON, validiert
- `Save-Project($project, $path)`: Atomar schreiben (temp â†’ replace)
- `New-Backup($project)`: Zeitgestempel `data_YYYYMMDD_HHMMSS.json`
- `Auto-Save($project)`: Alle 3 Min wenn Dirty=true

**Atomare Writes:**
```powershell
# Verhindert kaputtes JSON bei Crash:
$temp = "$path.tmp"
$json | Out-File $temp
Move-Item $temp $path -Force
```

### MergeService

**Funktionen:**
- `Merge-Field($currentValue, $newValue, $fieldMeta, $newSource)`: Zentrale Logik
  - PrÃ¼ft UserEdited + Priority
  - Setzt FieldMeta.Source/LastUpdated
  - Markiert Konflikt statt zu Ã¼berschreiben
- `Get-MergeConflicts($project)`: Konflikt-Ãœbersicht
- `Resolve-Conflicts($conflicts, $action)`: "Accept" / "Reject" / "ReviewPerField"

### XtfImportService & Xtf405ImportService

**Funktionen:**
- `Parse-XtfFile($path)`: XML -> Array von HaltungRecords
- `Match-RowByHaltungsname($haltungen, $newRecords)`: Findet Zeilen zum Update
- `Import-Xtf($project, $path)`: Full Import mit Merge

**Matching:**
1. PrimÃ¤r: Haltungsname (ID)
2. Fallback: (Strasse + DN + LÃ¤nge) nur wenn eindeutig
3. Neu: Erzeugt neue Zeile

### PdfImportService

**Funktionen:**
- `Extract-PdfText($pdfPath)`: PDF -> Text mit Texttract/PdfSharp
- `Parse-PdfToKeyValue($text)`: Regex-basiert (siehe PDF-Mapping)
- `Import-PdfForRow($project, $rowId, $pdfPath)`: Single-Row-Import

**Regex-Mapping-Tabelle:**
```powershell
$PdfFieldRegexes = @{
    "Haltungsname" = @(
        '(?im)^\s*(Haltungsname|Haltungsnahme|Haltung\s*ID|Leitung\s*ID)\s*[:\-]?\s*(.+?)\s*$'
    )
    "Zustandsklasse" = @(
        '(?im)^\s*(Zustandsklasse|ZK)\s*[:\-]?\s*([0-9])\b'
    )
    # ...
}
```

### ExcelExportService

**Funktionen:**
- `Export-ToExcel($project, $outputPath)`: 25 Spalten, Formatierung
- AutoFilter, FreezePane Header, Spaltenbreiten

### LoggingService

**Funktionen:**
- `Write-Log($level, $message, $context)`: Info/Warn/Error
- Datei: `logs/app.log` (optional rotate)
- Format: `[2026-01-25 14:45:30] [INFO] [XtfImport] XTF loaded: 12 Haltungen`

### ValidationService

**Funktionen:**
- `Validate-Integer($value, $min, $max)`: DN, NR, etc.
- `Validate-Decimal($value)`: LÃ¤nge, Kosten
- `Normalize-CostValue($value)`: 12'500 â†’ 12500
- `Normalize-DateValue($value)`: "25.01.2026" â†’ parsebar

---

## UI (WPF DataGrid, nicht Karten)

### MainWindow Layout

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ Toolbar (Proj | +Zeile | Import XTF/PDF | Export Excel) â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚ [DataGrid mit 25 Spalten, 1 Zeile = 1 Haltung]         â”‚
â”‚ - Row Height: kompakt (25-30 px)                        â”‚
â”‚ - TextWrapping: Off                                     â”‚
â”‚ - Frozen: Spalten 1-2 (NR + ID)                         â”‚
â”‚ - HorizontalScrollBar: visible                          â”‚
â”‚ - VerticalScrollBar: visible                            â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚ Statusbar: "Projekt: ...", "Letzter Import: XTF ...", â”‚
â”‚            "Fehler: 3" (klickbar â†’ Details)             â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### Toolbar Buttons + Shortcuts

| Aktion | Shortcut | Button |
|--------|----------|--------|
| Neues Projekt | Ctrl+N | ðŸ†• Projekt |
| Projekt Ã¶ffnen | Ctrl+O | ðŸ“‚ Ã–ffnen |
| Speichern | Ctrl+S | ðŸ’¾ Speichern |
| + Haltung | Insert / Ctrl++ | âž• Haltung |
| Zeile lÃ¶schen | Del | ðŸ—‘ |
| Import XTF | - | ðŸ“¥ XTF |
| Import XTF405 | - | ðŸ“¥ XTF405 |
| Import PDF | - | ðŸ“„ PDF |
| Export Excel | Ctrl+E | ðŸ“Š Excel |
| Suchen | Ctrl+F | ðŸ” |
| Filter zurÃ¼cksetzen | Ctrl+Alt+R | ðŸ”„ |

### Shortcuts Handling

```powershell
$mainWindow.Add_PreviewKeyDown({
    param($sender, $e)
    if ($e.KeyboardDevice.IsKeyDown([System.Windows.Input.Key]::LeftCtrl)) {
        switch ($e.Key) {
            'N' { New-Project; $e.Handled = $true }
            'O' { Open-Project; $e.Handled = $true }
            'S' { Save-Project; $e.Handled = $true }
            'E' { Export-ToExcel; $e.Handled = $true }
            'F' { Show-SearchDialog; $e.Handled = $true }
            'Add' { Add-HaltungRow; $e.Handled = $true }
        }
    }
    if ($e.Key -eq 'Insert') { Add-HaltungRow; $e.Handled = $true }
    if ($e.Key -eq 'Delete') { Delete-CurrentRow; $e.Handled = $true }
})
```

### KontextmenÃ¼ (Rechtsklick auf Zeile)

- Zeile duplizieren
- PDF zuordnen
- Link kopieren (+ Ã¶ffnen im Browser)
- Zeile lÃ¶schen
- Details/Metadaten anzeigen

---

## Fehlerbehandlung & Logging

### Import-Flow

```
1. Import starten â†’ Set-TextSafe lblStatus "Importiere..."
2. Parse-File â†’ Fehler? â†’ Log + continue
3. Pro Datensatz: Merge-Field
   - Konflikt? â†’ $project.Conflicts[] hinzufÃ¼gen
   - OK? â†’ $project.Data update + FieldMeta
4. Fertig â†’ "$X erfolgreich, $Y Fehler, $Z Konflikte"
5. UI aktualisieren
```

### Fehler-UI

- Nicht spammen (kein Dialog pro Zeile)
- Statusbar: "Import abgeschlossen mit 3 Fehlern" (klickbar)
- Dialog: "Details anzeigen" â†’ List mit Fehlern + Kontexten

---

## Import/Export Flows (Detailliert)

### XTF Import

1. **Datei Ã¶ffnen**: OpenFileDialog â†’ `Rohdaten/`
2. **Parse**: `ConvertFrom-XtfFile` â†’ Array[HaltungRecord]
3. **BestÃ¤tigung**: "12 Haltungen gefunden, importieren?"
4. **Merge**: FÃ¼r jede HaltungRecord:
   - Match by Haltungsname oder Fallback
   - Pro Feld: `Merge-Field(..., Source="xtf")`
   - Konflikte protokollieren
5. **UI Update**: DataGrid refresh
6. **Log**: `[INFO] [XtfImport] Datei: ..., Neue: X, Updated: Y, Konflikte: Z`
7. **Auto-Backup**: `data_YYYYMMDD_HHMMSS.json`

### PDF Import (Pro Zeile)

1. **PDF Ã¶ffnen**: OpenFileDialog
2. **Extract Text**: Texttract / PdfSharp
3. **Parse Regex**: Map Text â†’ KeyValue (Haltungsname, Zustandsklasse, etc.)
4. **Merge**: Pro Feld: `Merge-Field(..., Source="pdf")`
5. **BestÃ¤tigung**: "X Felder gefÃ¼llt, Y Ã¼bersprungen (UserEdited), Z Konflikte"
6. **Log + Auto-Backup**

---

## Autosave & Backups

**Autosave-Timer:**
```powershell
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [System.TimeSpan]::FromSeconds(180)  # 3 Minuten
$timer.Add_Tick({
    if ($script:Project.Dirty) {
        Save-Project -Path $script:ProjectPath
        New-Backup -Project $script:Project
        $script:Project.Dirty = $false
        Write-Log -Level Info -Message "Autosave durchgefÃ¼hrt"
    }
})
$timer.Start()
```

**Backup-Datei:**
```
Projekte/MeinProjekt/backups/
  data_20260125_144530.json
  data_20260125_150000.json
  ... (Keep last 20)
```

**Crash-Recovery:**
- Beim Start: PrÃ¼fe `data.json.tmp` (letzter fehlgeschlagener Save)
- Optional: "Letztes Autosave wiederherstellen?" Dialog

---

## Testing & Demo

**Smoke Test Daten:**
```powershell
# Neues Projekt mit 3 Dummy-Haltungen
$proj = New-Project
$proj.Data += @{
    Id = [guid]::NewGuid()
    Fields = @{ NR = 1; Haltungsname = "H001"; Strasse = "Klausenstrasse" }
    FieldMeta = @{ Haltungsname = @{ Source = "xtf"; UserEdited = $false } }
}
# ...
Save-Project $proj "test_project.haltproj"
```

**Verifikation:**
- âœ… Import setzt UserEdited = $false
- âœ… Manuelle Edit setzt UserEdited = $true
- âœ… Zweiter Import Ã¼berschreibt nicht (wenn UserEdited = $true)
- âœ… Konflikt wird protokolliert

---

## Umbau-Plan (Sequenz)

1. âœ… Repo-Analyse + Doku (dieses Dokument)
2. ðŸ”„ Modular Services erstellen (Models, MergeService, etc.)
3. ðŸ”„ DataGrid statt Karten-UI + 25 Spalten
4. ðŸ”„ ProjectStorageService (JSON + atomar + backups)
5. ðŸ”„ Tracking & SuppressFieldEvents (Bugfix)
6. ðŸ”„ XTF/XTF405 Import stabilisieren
7. ðŸ”„ PDF Import + Regex-Mapping
8. ðŸ”„ Export Excel
9. ðŸ”„ Toolbar + Shortcuts
10. ðŸ”„ Suche/Filter + KontextmenÃ¼
11. ðŸ”„ Finale Tests + README

---

## Referenzen

- Referenz-Excel: `Haltungen.xlsx` (25 Spalten, Spaltenbreiten als Richtwert)
- XTF SIA405 Standard: Interlis, XML-basiert
- PDF: Texttract oder System.Drawing.Printing


