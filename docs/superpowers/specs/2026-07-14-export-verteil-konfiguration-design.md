# Design: Export- & Verteil-Konfiguration mit Zielverzeichnis und Namensvorlagen

> Stand: 2026-07-14 · Status: Etappe 1 umgesetzt · Betrifft: Export-Seite (`ExportPage` / `ExportPageViewModel`), Verteil-Logik (`HoldingFolderDistributor` u.a.), `AppSettings`.

## 1. Ziel

Die Export-/Verteilseite hat aktuell nur Export-Buttons (xlsx) und ein „Verteilen"-Menü; darunter ist eine große leere Fläche. Die Verteilung fragt jedes Mal per Dialog nach Zielordner, und die Datei-Benennung ist fest auf `Datum_Haltung` verdrahtet.

Ziel: Für **Haltungen, Schächte und Dichtheitsprüfungen** je ein konfigurierbares Zielverzeichnis und eine konfigurierbare Benennung über **drei getrennte Ebenen** — plus eine automatisch angelegte Ordnerstruktur und eine Live-Vorschau. Konfiguration lebt auf der Export-Seite (bisher leere Fläche).

## 2. Umfang

- **Etappe 1 (dieses Dokument):** Standard-Zielordner + sicherer Verzeichnisbaum je Verteiltyp, Live-Vorschau und Ein-Klick-Verteilung mit den gemerkten Werten. Excel bekommt nur einen gemeinsamen Zielordner; die Dateinamen bleiben fest.
- **Etappe 2 (später, eigene Spec):** benannte Profile (mehrere gespeicherte Konfigurationen, wählbar per Dropdown).

Nicht in Etappe 1: Profile, Migration bestehender Verteilungen, Umbenennen bereits verteilter Dateien.

## 3. Kernkomponente: Platzhalter-Engine (`DistributionPatternResolver`)

Eigenständige, reine Klasse mit Interface (`IDistributionPatternResolver`) in `AuswertungPro.Next.Application` oder `Infrastructure`. Nimmt ein Muster + einen Kontext (Felder eines Datensatzes) und liefert ein **sicheres** Pfad-/Namenssegment. Voll per TDD getestet.

**Platzhalter (case-insensitiv, Form `{Name}`):**

| Platzhalter | Quelle | Beispiel |
|---|---|---|
| `{Datum}` | Inspektionsdatum, `yyyyMMdd` | `20260626` |
| `{Jahr}` | Jahr des Inspektionsdatums | `2026` |
| `{Monat}` | Monat, 2-stellig | `06` |
| `{Gemeinde}` | Gemeinde | `Altdorf` |
| `{Haltung}` | Haltungsname (nur Haltungen) | `06.24341-35625` |
| `{Schachtnummer}` | Schachtnummer (nur Schächte) | `KS 60191` |

**Regeln:**
- Unbekannte Platzhalter bleiben als Literal stehen bzw. werden als leer behandelt (Entscheidung: leer + einmalige Validierungswarnung in der Vorschau).
- Fehlender Wert (z.B. kein Datum) → Platzhalter wird zu leerem String; ergibt das ein leeres Segment, wird das Segment übersprungen (siehe leere Ebenen).
- **Sanitizing:** Jedes fertige Segment läuft durch `ProjectPathResolver.SanitizePathSegment` (ersetzt ungültige Zeichen `\ / : * ? " < > |`, fängt `.`/`..` ab). Der Dateiname behält die Endung.
- Die Engine erzeugt NUR relative Segmente; das Zusammensetzen mit der Ziel-Wurzel macht der Aufrufer über `Path.Combine`.

## 4. Sicherer Verzeichnisbaum + Ziel-Wurzel (pro Verteiltyp)

Pro Typ (Haltungen / Schächte / Dichtheit) gibt es vier Einstellungen:

| Feld | Bedeutung | Beispiel |
|---|---|---|
| Ziel-Wurzel | physischer Basis-Pfad (per Ordner-Dialog) | `D:\Verteilt\Haltungen` |
| 1. Ordner | Muster für die 1. Ebene | `{Gemeinde}` |
| 2. Unterordner | Muster für die 2. optionale Ebene | `{Jahr}` |
| Fester Objektordner | wird immer vom Programm angehängt | `{Haltung}` bzw. `{Schachtnummer}` |
| Feste Datei | sicherheitsrelevantes, nicht editierbares Schema | `{Datum}_{Haltung}` |

**Zusammensetzung:** `Ziel-Wurzel \ Ordner \ Unterordner \ fester Objektordner \ feste Datei.<originalEndung>`
→ `D:\Verteilt\Haltungen\Altdorf\2026\06.24341-35625\20260626_06.24341-35625.pdf`

**Leere optionale Ebene = weglassen:** Ist „Ordner" und/oder „Unterordner" leer, wird diese Ebene übersprungen. Der letzte Haltungs-/Schachtordner bleibt immer erhalten. Das schützt Video-Erkennung, Konfliktbearbeitung und bestehende Projektpfade.

## 5. Persistenz (`AppSettings`)

Neue Felder, gruppiert pro Typ. Um Wildwuchs zu vermeiden: ein kleines serialisierbares Record `DistributionTargetConfig { string? Root; string OrdnerPattern; string UnterordnerPattern; string DateiPattern; }` und je ein Feld:

- `HaltungDistribution` : `DistributionTargetConfig`
- `SchachtDistribution` : `DistributionTargetConfig`
- `DichtheitDistribution` : `DistributionTargetConfig`
- `ExcelExportRoot`: gemeinsamer Zielordner für beide Excel-Dateien
- `HaltungExport` / `SchachtExport`: alte Felder bleiben für Rückwärtskompatibilität lesbar, werden von der neuen Excel-Oberfläche aber nicht mehr bearbeitet

Defaults reproduzieren das bisherige Verhalten: optionale Überordner leer, fester Objektordner direkt unter der Ziel-Wurzel. Alte getrennte Excel-Zielordner werden einmalig übernommen (Haltungen vor Schächten) und anschließend gemeinsam geführt. Waren die alten Zielordner verschieden, bleibt der frühere Schacht-Zielordner zusätzlich in `LegacySchachtExportRoot` dokumentiert und geht bei der Umstellung nicht still verloren. Leere oder gleiche Excel-Dateinamen werden automatisch auf die getrennten sicheren Vorgaben `Haltungen.xlsx` und `Schaechte.xlsx` zurückgesetzt, damit sich die Exporte nicht gegenseitig überschreiben. Persistenz über den bestehenden atomaren `SettingsStore`.

## 6. UI (bisher leere Fläche der Export-Seite)

Row 3 (`Height="*"`) bekommt einen scrollbaren Bereich. Oben steht eine einfache Excel-Karte mit genau einem gemeinsamen Zielordner. Darunter folgen drei Verteilkarten für Haltungen, Schächte und Dichtheit. In jeder Verteilkarte werden die beiden optionalen Ordnerebenen mit anklickbaren Bausteinen zusammengestellt. Der feste Objektordner und der feste Dateiname sind sichtbar, aber nicht änderbar. Eine **Live-Vorschau** zeigt sofort den fertigen Zielpfad. Änderungen werden debounced gespeichert.

## 7. Verdrahtung in die Verteilung

Die Verteil-Commands (`DistributeHoldingsAsync`, `DistributeShaftsAsync`, `DistributeDichtheitAsync`) nutzen die konfigurierte Ziel-Wurzel statt des Zielordner-Dialogs (Dialog/Projektordner nur, wenn keine Wurzel gesetzt).

**Endgültige Entscheidung 2026-07-14:** Die zwei konfigurierbaren Ebenen werden nur als **Überordner** angewendet. Der letzte Objektordner (`<Haltung>` oder `<Schacht>`) und alle Dateinamen bleiben unverändert. Damit können Haltungen, Schächte und DP frei nach Gemeinde/Jahr/Monat gegliedert werden, ohne die Video-Zuordnung oder bestehende Projektpfade zu gefährden.

Die drei manuellen Verteilbefehle übergeben eine eingefrorene Kopie der Baumkonfiguration. Sind beide Baumebenen leer, wird weiterhin gar keine Baumkonfiguration übergeben; damit bleiben auch die bisherigen DP-Datumsregeln unverändert. Automatische Importe ohne diesen optionalen Parameter behalten exakt die alte Struktur. `DistributionDirectoryTreeResolver` kapselt Auflösung und Bereinigung der Segmente; `HoldingFolderDistributor` verwendet ihn erst nach der endgültigen Haltungs-/Schachtkorrektur. DP wird fachlich nach Haltung abgelegt und verwendet das feste Schema `{Datum}_{Haltung}_DP`. Der Befehl „Dichtheitsprüfung (PDF) öffnen“ sucht sowohl im Projektordner als auch in einer extern konfigurierten Ziel-Wurzel rekursiv nach dem festen Haltungsordner.

Excel bleibt davon getrennt: Beide Excel-Exporte verwenden nur `ExcelExportRoot`. Die Dateien heißen fest `Haltungen.xlsx` und `Schaechte.xlsx`.

### Ergänzung: grafischer Baustein-Editor

Die drei Verteilkarten erhalten anklickbare Bausteine für Gemeinde, Datum, Jahr,
Monat und Trennzeichen. Erste und zweite Ordnerebene besitzen jeweils eigene Bausteinkette,
Zurück, Leeren und eine optionale Text-Feinbearbeitung. Der letzte Haltungs-/Schachtordner
und der Dateiname werden ebenfalls als Bausteinkette angezeigt, bleiben aber nicht editierbar.
Excel besitzt keinen Baustein-Editor. Damit bleibt die Video-Zuordnung unverändert.

## 8. Testkonzept

- **DistributionPatternResolver:** Unit-Tests (TDD) für jeden Platzhalter, Sonderzeichen-Sanitizing, fehlende Felder, leere Ebenen (flach vs. tief), Endungserhalt, Schacht- vs. Haltungs-Kontext.
- **DistributionDirectoryTreeResolver:** fester letzter Objektordner, optionale Überordner, Pfadbereinigung und unverändertes Altschema ohne Muster.
- **AppSettings:** Roundtrip-Test (Serialisierung der neuen Felder, Defaults).
- **ExportPageViewModel:** kompletter Vorschaupfad, grafische Bausteine je Ordnerebene sowie gemeinsamer Excel-Zielordner mit festen Dateinamen.
- **Distributor:** Integrationstest, dass die Baumkonfiguration tatsächlich bei der Dateiablage verwendet wird und Dateinamen unverändert bleiben.
- **UI:** Build + manuelle Abnahme.

## 9. Umsetzungsreihenfolge (kleine, testgeschützte Pakete)

1. `DistributionPatternResolver` + Interface + Tests.
2. `DistributionTargetConfig` + `AppSettings`-Felder + Roundtrip-Test.
3. UI-Konfigfläche + Vorschau-Property im ViewModel + Test.
4. Verdrahtung in die Verteil-Commands (additiv) + Excel-Export.

Die vier Schritte sind umgesetzt. Abschlussbedingung: Release-Build 0/0 und alle vier vorgeschriebenen Testprojekte grün.
