# VSA-Code-Konsistenz: Härtung vom 2026-07-10

Ziel: 100 % robuste, programmweit konsistente Schadenscodes (gemäss ADR-006: Manifest = alleinige Code-Wahrheit).

## Ausgangsbefund (programmweiter Scan)

122 distinct VSA-Code-Literale in C#/Sidecar/Configs; 49 davon fehlten im Manifest:

- 37 Hauptcode-Familien ohne Gruppeneintrag (AEC, AED, BDC, BDG, DAK, DAL, DAF, … DDG) — Services, KI-Befundliste und IBAK/KIAS-Importe referenzieren nackte Hauptcodes, Klartext-Auflösung lief ins Leere.
- BDGZ/DDGZ (EN-13508-2-Z-Codes) fehlten — WinCan-Kataloge führen sie.
- Rest: bewusstes Präfix-Matching (BABA/BAFA in StartsWith-Checks) und Doku-Beispiel BCDXY — keine Manifest-Einträge nötig.
- BBD bleibt absichtlich ohne Basiscode (CLAUDE.md + bestehender Truth-Test).

## Änderungen (3 Dateien, rein additiv)

1. **`src/AuswertungPro.Next.UI/Data/vsa_kek_2020_catalog_manifest.json`** — 680 → **719 Codes** (+819 Zeilen, 0 gelöscht):
   - 37 Basisgruppen (source `VSA-KEK-2020-Heading`, selektierbar, Muster BCC/BAA), Titel fachlich nach EN 13508-2/VSA, Schacht-Spiegel mit Suffix „(Schacht)".
   - BDGZ/DDGZ „Keine Sicht, andere" (source `WinCan-Fallback`, **nicht** selektierbar — reiner Import-/Anzeige-Anker, kein Neu-Erfassen ausserhalb der ILI-Enum).
2. **`src/AuswertungPro.Next.Application/Protocol/VsaKekCatalogBuilder.cs`** — Generator synchron erweitert (`OfficialChannelHeadings` +8, neue `OfficialManholeHeadings` 29, neue `WinCanCompatibilityZCodes`, `AddWinCanCompatibilityZCodes` vor `AddObservedXtfCodes`). Eine Regenerierung aus der ILI (Bin.7z des Erstfeld-Jagdmatt-Exports) erzeugt jetzt dieselben Einträge. Builder↔JSON per Skript verifiziert: synchron.
3. **`tests/AuswertungPro.Next.Pipeline.Tests/VsaKekManifestIntegrityTests.cs`** — neuer Riegel (6 Tests):
   Codes eindeutig · jede Familie hat Gruppeneintrag (Ausnahme BBD) · canonicalCode zeigt auf existierenden Code · selektierbare Codes haben Klartext ≠ Code · Import-Hauptcodes (AEC…DDG) lösen auf Klartext auf · BDGZ/DDGZ vorhanden & nicht selektierbar.

## Verifikation

- JSON parst, 719 eindeutige Codes, Diff rein additiv.
- Alle Assertions der bestehenden `VsaKekManifestTruthTests` in Python nachgestellt: bestanden (inkl. „BBD existiert nicht").
- Programmweite Literale erneut geprüft: 0 unaufgelöste (BCDXY = dokumentiertes Beispiel im VsaCodeValidator-Kommentar).
- Builder-Tabellen ↔ JSON-Einträge: identisch (Titel, Source, Selektierbarkeit).

## Offen (auf dem Windows-Rechner ausführen)

```bash
dotnet test AuswertungPro.sln
```
(Sandbox hat kein dotnet; Pipeline.Tests targeten net10.0 und laufen lokal.)
