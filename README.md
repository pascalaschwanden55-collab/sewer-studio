# AuswertungPro.Next (Skeleton)

Modernisierte, klarer strukturierte Windows-App (WPF) für:
- Projektverwaltung (Neu/Öffnen/Speichern)
- Import: PDF (via pdftotext.exe) und XTF (SIA405 / VSA_KEK Erkennung)
- Export: Excel (Platzhalter-Interface, zum Anbinden eurer Template-Logik)
- VSA-Zustandsklassifizierung & -Bewertung: Engine (Formeln + Randbedingungen), Mapping datengetrieben (JSON)
- Diagnostik: Fehlercode-Generator + Log-Datei (per Schalter deaktivierbar)

## Quickstart
1. Öffne den Ordner in VS Code.
2. `dotnet restore` (Windows)
3. Starte `src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`

## pdftotext
Lege `pdftotext.exe` unter `src/AuswertungPro.Next.UI/tools/pdftotext.exe` ab
oder konfiguriere einen Pfad in den Einstellungen.

## PDF-Export (Offerte)
Der PDF-Export rendert ein HTML-Template und druckt es via Playwright/Chromium als PDF.

**Einmalig Chromium installieren (nach Build/Restore):**
```powershell
pwsh "src/AuswertungPro.Next.UI/bin/Debug/net10.0-windows/playwright.ps1" install chromium
```

**Optional: Logo im PDF**
- Lege eine PNG-Datei unter `src/AuswertungPro.Next.UI/Assets/Brand/abwasser-uri-logo.png` ab.
- Die Datei wird nach `bin/...` kopiert und automatisch eingebettet.

## VSA Klassifizierungstabellen (Anhang C/D)
Die App erwartet `classification_channels.json` und `classification_manholes.json`.
Im Skeleton sind nur *Beispiel-Einträge* enthalten.
Du kannst später ein Dev-Tool bauen, das aus deiner VSA-Rili-PDF die Tabellen extrahiert
und daraus diese JSON-Dateien generiert, oder du pflegst die Tabellen direkt.

## Diagnose / Fehlercodes
- In **Einstellungen** kann `EnableDiagnostics` deaktiviert werden.
- Wenn aktiv: bei Exceptions wird ein Fehlercode angezeigt + in Log gespeichert.
