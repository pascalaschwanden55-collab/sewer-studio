# ðŸš€ AuswertungPro v2.1.0 - SOFORTEINSATZ

> HINWEIS (historisch): Diese Startanleitung bezieht sich auf die fruehere PowerShell-Version.
> Fuer die aktuelle C#/.NET Umsetzung gelten primaer:
> - `README.md`
> - `docs/Programmpruefung_AuswertungPro_2026-02-20.md`


**Versionsdatum:** 25. Januar 2026  
**Status:** âœ… **PRODUKTIONSREIF**

---

## âš¡ 10-Sekunden Start

```powershell
cd f:\AuswertungPro
.\HaltungsAuswertungPro_v2.ps1
```

**Fertig.** Neue Excel-Ã¤hnliche Anwendung Ã¶ffnet sich.

---

## ðŸ“‹ Was ist neu? (Schnell zusammengefasst)

| Problem (v1) | LÃ¶sung (v2) |
|---|---|
| Monolithisch (2251 Zeilen) | **Services-Architektur (Module, testbar)** |
| Karten-Layout | **DataGrid mit 25 Spalten (Excel-Ã¤hnlich)** |
| Nutzer-Daten Ã¼berschrieben? | **Protection: UserEdited=true nie Ã¼berschrieben** |
| Keine Konflikterkennung | **Konflikt-Protokollierung statt Fehler** |
| PDF-Felder begrenzt | **Regex-Mapping (25+ Felder)** |
| Kein Crash-Recovery | **Auto-Backup, Crash-Marker, Recovery** |
| Keine Logs | **Strukturiertes Logging (app.log, errors.log)** |
| Ad-hoc Merge-Logik | **Zentral: Priority manual > pdf > xtf405 > xtf** |

---

## ðŸŽ¯ Alltags-Workflow (5 Schritte)

### 1ï¸âƒ£ Neues Projekt
```
[ðŸ†• Projekt] oder Ctrl+N
â†’ Leeres Projekt erstellt
```

### 2ï¸âƒ£ Haltungen erfassen
```
[âž• Haltung] oder Insert
â†’ Neue Zeile
â†’ DataGrid editieren (Doppelklick auf Zelle)
```

### 3ï¸âƒ£ XTF importieren
```
[ðŸ“¥ XTF] â†’ select .xtf file
â†’ "12 Haltungen gefunden?" â†’ Ja
â†’ Auto-Merge (neue Zeilen + Update existierende)
```

### 4ï¸âƒ£ PDF pro Haltung
```
[ðŸ“„ PDF] â†’ select .pdf
â†’ PDF geparst (Zustandsklasse, SchÃ¤den, Kosten, ...)
â†’ "8 Felder ergÃ¤nzt, 2 Konflikte"
```

### 5ï¸âƒ£ Speichern & Export
```
[ðŸ’¾ Speichern] Ctrl+S â†’ JSON atomar geschrieben + Backup
[ðŸ“Š Excel] Ctrl+E â†’ Excel-Datei mit 25 Spalten, Header, AutoFilter
```

---

## ðŸ›¡ï¸ Datenschutz (Das Wichtigste!)

**Regel:** Manuelle Eingaben werden **NIE** Ã¼berschrieben.

```
Szenario: Sie editieren Zustandsklasse = "3" manuell
â†’ XTF-Import versucht: "2" zu setzen
â†’ Ergebnis: "3" bleibt, "2" wird als "Konflikt" protokolliert
â†’ Message: "Konflikt: PDF hat '2', aber Sie haben '3' editiert"
â†’ Sie entscheiden spÃ¤ter, nicht das System
```

**PrioritÃ¤t bei Ãœberschreibung (nur wenn nicht manuell editiert):**
```
manual (Sie) > pdf > xtf405 > xtf
```

---

## ðŸ“ Dateien (Was ist neu?)

### Neu: Services/ Verzeichnis

```
Services/
â”œâ”€â”€ Bootstrap.ps1           (Initialisierung)
â”œâ”€â”€ Models.ps1              (Datenmodelle)
â”œâ”€â”€ MergeService.ps1        â­ (Merge-Logik - das Herz!)
â”œâ”€â”€ ProjectStorageService.ps1  (Speichern/Laden)
â”œâ”€â”€ LoggingService.ps1      (Logs)
â”œâ”€â”€ ValidationService.ps1   (Validierung)
â”œâ”€â”€ AutosaveService.ps1     (Autosave + Recovery)
â”œâ”€â”€ XtfImportService.ps1    (XTF)
â”œâ”€â”€ Xtf405ImportService.ps1 (XTF405 - hÃ¶here PrioritÃ¤t)
â”œâ”€â”€ PdfImportService.ps1    (PDF mit Regex)
â””â”€â”€ ExcelExportService.ps1  (Export)
```

### Neu: Dokumentation

```
â”œâ”€â”€ ARCHITECTURE.md         (Umfassende Doku, 400+ Zeilen)
â”œâ”€â”€ README_v2.md            (Quick-Start + FAQ)
â”œâ”€â”€ RELEASE_NOTES_v2.1.0.md (Ã„nderungen, Roadmap)
â””â”€â”€ DATEIEN_MANIFEST.md     (File-Ãœbersicht)
```

### Neu: Hauptapp

```
â”œâ”€â”€ HaltungsAuswertungPro_v2.ps1  (Neue DataGrid-App)
```

### UnverÃ¤ndert (KompatibilitÃ¤t)

```
â”œâ”€â”€ HaltungsAuswertungPro.ps1    (alte Karten-App, noch funktional)
â”œâ”€â”€ AuswertungTool.ps1          (WinForms-Alternative)
â”œâ”€â”€ README.md                    (alte Doku)
â””â”€â”€ ... (andere Dateien)
```

---

## ðŸŽ® Keyboard-Shortcuts

| Shortcut | Funktion |
|----------|----------|
| **Ctrl+N** | ðŸ†• Neues Projekt |
| **Ctrl+O** | ðŸ“‚ Projekt Ã¶ffnen |
| **Ctrl+S** | ðŸ’¾ Speichern |
| **Insert** | âž• Haltung hinzufÃ¼gen |
| **Delete** | ðŸ—‘ Zeile lÃ¶schen |
| **Ctrl+E** | ðŸ“Š Excel Export |
| **Ctrl+F** | ðŸ” Suche |

---

## ðŸ“Š DataGrid - 25 Spalten

Alle Standard-Felder der Schweizer Kanalinspektion:

| # | Feld | Quelle |
|---|------|--------|
| 1-8 | NR, ID, Strasse, Material, DN, Nutzung, LÃ¤nge, Fliessrichtung | XTF |
| 9-13 | PrimÃ¤re SchÃ¤den, Zustandsklasse, PrÃ¼fung, Sanieren J/N, Massnahmen | PDF |
| 14-17 | Kosten, EigentÃ¼mer, Bemerkungen, Link | PDF/XTF |
| 18-25 | Renovierung, Reparaturen, Datum | PDF |

**Sortierbar, filterbar, editierbar im DataGrid.**

---

## ðŸ’¾ Projektspeicherung

```
Projekte/
â”œâ”€â”€ {ProjectId}.haltproj              (JSON-Datei)
â”œâ”€â”€ backups/
â”‚   â”œâ”€â”€ {ProjectId}_20260125_144530.haltproj  (Backup 1)
â”‚   â””â”€â”€ {ProjectId}_20260125_150000.haltproj  (Backup 2)
```

**Atomare Writes:** Falls Crash beim Save â†’ `data.json.crash` erzeugt â†’ Beim Start: "Crash-Recovery?" Dialog.

**Auto-Backup:** Letzte 20 Backups behalten, Ã¤ltere lÃ¶schen.

---

## ðŸ“ Logging

```
logs/
â”œâ”€â”€ app.log        # [INFO] / [DEBUG] / [WARN]
â”œâ”€â”€ errors.log     # [ERROR] + Stack-Trace
â””â”€â”€ imports.log    # Import-Historie (timestamped)
```

**Beispiel:**
```
[2026-01-25 14:45:30] [INFO] [XtfImport] XTF geparst: 12 Haltungen
[2026-01-25 14:46:00] [WARN] [Merge] Konflikt: Zustandsklasse UserEdited=true
[2026-01-25 14:46:15] [ERROR] [PdfImport] Invalid regex match
2026-01-25 14:45:45 | XTF | file.xtf | Created: 3 | Updated: 8 | Conflicts: 2
```

---

## ðŸ”§ PDF-Feldmapping (Erweiterbar)

Neue PDF-Felder einfach hinzufÃ¼gen:

```powershell
# Datei: Services/PdfImportService.ps1

$script:PdfFieldMapping = @{
    'Zustandsklasse' = @{
        Regexes = @(
            '(?im)^\s*(Zustandsklasse|ZK)\s*[:\-]?\s*([0-9])\b'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    # ... weitere Felder
}
```

**Regex-Pattern:**
- `(?im)` = Case-insensitive + Multiline
- `[:\-]?` = Optionaler Trenner
- `(.+?)` = Capture Group (Nicht-greedy)

---

## â“ FAQ

**F: Wird mein editiertes Feld beim Import Ã¼berschrieben?**  
**A:** Nein! UserEdited=true ist geschÃ¼tzt. Import als Konflikt protokolliert.

**F: Wie viele Backups behÃ¤lst du?**  
**A:** Letzte 20, dann Auto-Cleanup.

**F: Kann ich Ã„nderungen rÃ¼ckgÃ¤ngig machen?**  
**A:** Aktuell: Undo pro Zelle (bei Doppelklick). Global: Projekt ohne Speichern schlieÃŸen + neu Ã¶ffnen.

**F: Was wenn die App beim Save crasht?**  
**A:** Crash-Marker (`data.json.crash`) wird erzeugt. Beim Start: Recovery-Dialog. Alte Datei gespeichert als `.broken`.

**F: Kann ich mehrere PDFs auf einmal importieren?**  
**A:** Aktuell: Pro Zeile eine PDF. Batch-Import geplant (v2.2).

---

## ðŸ“š WeiterfÃ¼hrende Docs

| Datei | FÃ¼r wen? | LÃ¤nge |
|-------|----------|--------|
| **README_v2.md** | Nutzer & Devs | 300+ Zeilen |
| **ARCHITECTURE.md** | Architekten & Devs | 400+ Zeilen |
| **RELEASE_NOTES_v2.1.0.md** | Release-Manager | 350+ Zeilen |
| **DATEIEN_MANIFEST.md** | IT-Admin | 150+ Zeilen |

---

## ðŸŽ¬ Demo (2 Minuten)

1. **App starten:** `.\HaltungsAuswertungPro_v2.ps1` âœ…
2. **Projekt:** [ðŸ†• Projekt] â†’ "Neues Projekt erstellt"
3. **Haltung:** [âž• Haltung] â†’ Neue Zeile im DataGrid
4. **Edit:** Doppelklick auf Zelle â†’ "H001" eingeben
5. **Speichern:** [ðŸ’¾ Speichern] â†’ "Projekt gespeichert"
6. **Export:** [ðŸ“Š Excel] â†’ Excel Ã¶ffnet sich
7. **Logs:** `logs/app.log` anschauen

---

## âœ… Checkliste vor Go-Live

- [x] Services/ Verzeichnis mit 11 Modulen
- [x] HaltungsAuswertungPro_v2.ps1 vorhanden
- [x] Dokumentation vollstÃ¤ndig (ARCHITECTURE.md + README_v2.md)
- [x] Logging funktional
- [x] Autosave + Crash-Recovery
- [x] Merge-Logik getestet
- [x] PDF-Regex-Mapping funktional
- [x] Excel-Export funktional
- [x] Tastatur-Shortcuts funktional
- [x] Alte App bleibt kompatibel

---

## ðŸš€ Go-Live Befehl

```powershell
# Starte neue Produktionsapp:
cd f:\AuswertungPro
.\HaltungsAuswertungPro_v2.ps1
```

**GlÃ¼ckwunsch!** Sie verwenden jetzt **AuswertungPro v2.1.0 Professional Edition.**

---

**Fragen?**  
â†’ README_v2.md (Quick-Start)  
â†’ ARCHITECTURE.md (Details)  
â†’ logs/ (Debugging)

**Bugs melden?**  
â†’ logs/errors.log anschauen  
â†’ Kontextwerte erfassen  
â†’ RELEASE_NOTES.md konsultieren

---

**Version 2.1.0 | 2026-01-25 | READY FOR PRODUCTION** âœ…

