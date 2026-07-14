# Design: Export- & Verteil-Konfiguration mit Zielverzeichnis und Namensvorlagen

> Stand: 2026-07-14 · Status: genehmigt (Etappe 1) · Betrifft: Export-Seite (`ExportPage` / `ExportPageViewModel`), Verteil-Logik (`HoldingFolderDistributor` u.a.), `AppSettings`.

## 1. Ziel

Die Export-/Verteilseite hat aktuell nur Export-Buttons (xlsx) und ein „Verteilen"-Menü; darunter ist eine große leere Fläche. Die Verteilung fragt jedes Mal per Dialog nach Zielordner, und die Datei-Benennung ist fest auf `Datum_Haltung` verdrahtet.

Ziel: Für **Haltungen, Schächte und Dichtheitsprüfungen** je ein konfigurierbares Zielverzeichnis und eine konfigurierbare Benennung über **drei getrennte Ebenen** — plus eine automatisch angelegte Ordnerstruktur und eine Live-Vorschau. Konfiguration lebt auf der Export-Seite (bisher leere Fläche).

## 2. Umfang

- **Etappe 1 (dieses Dokument):** Standard-Zielordner + 3-Ebenen-Muster je Typ, Live-Vorschau, Ein-Klick-Verteilung mit den gemerkten Werten. Excel-Export bekommt Ziel-Ordner + Datei-Muster.
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
| `{Schachtnummer}` | Schachtnummer (nur Schächte/Dichtheit) | `KS 60191` |

**Regeln:**
- Unbekannte Platzhalter bleiben als Literal stehen bzw. werden als leer behandelt (Entscheidung: leer + einmalige Validierungswarnung in der Vorschau).
- Fehlender Wert (z.B. kein Datum) → Platzhalter wird zu leerem String; ergibt das ein leeres Segment, wird das Segment übersprungen (siehe leere Ebenen).
- **Sanitizing:** Jedes fertige Segment läuft durch `ProjectPathResolver.SanitizePathSegment` (ersetzt ungültige Zeichen `\ / : * ? " < > |`, fängt `.`/`..` ab). Der Dateiname behält die Endung.
- Die Engine erzeugt NUR relative Segmente; das Zusammensetzen mit der Ziel-Wurzel macht der Aufrufer über `Path.Combine`.

## 4. Drei Ebenen + Ziel-Wurzel (pro Typ)

Pro Typ (Haltungen / Schächte / Dichtheit) gibt es vier Einstellungen:

| Feld | Bedeutung | Beispiel |
|---|---|---|
| Ziel-Wurzel | physischer Basis-Pfad (per Ordner-Dialog) | `D:\Verteilt\Haltungen` |
| 1. Ordner | Muster für die 1. Ebene | `{Gemeinde}` |
| 2. Unterordner | Muster für die 2. Ebene | `{Haltung}` |
| 3. Datei | Muster für den Dateinamen (ohne Endung) | `{Datum}_{Haltung}` |

**Zusammensetzung:** `Ziel-Wurzel \ Ordner \ Unterordner \ Datei.<originalEndung>`
→ `D:\Verteilt\Haltungen\Altdorf\06.24341-35625\20260626_06.24341-35625.pdf`

**Leere Ebene = weglassen:** Ist „Ordner" und/oder „Unterordner" leer (oder ergibt nach Auflösung ein leeres Segment), wird diese Ebene übersprungen. So sind flach (nur Wurzel + Datei) und tief (Wurzel + Ordner + Unterordner + Datei) beide möglich.

## 5. Persistenz (`AppSettings`)

Neue Felder, gruppiert pro Typ. Um Wildwuchs zu vermeiden: ein kleines serialisierbares Record `DistributionTargetConfig { string? Root; string OrdnerPattern; string UnterordnerPattern; string DateiPattern; }` und je ein Feld:

- `HaltungDistribution` : `DistributionTargetConfig`
- `SchachtDistribution` : `DistributionTargetConfig`
- `DichtheitDistribution` : `DistributionTargetConfig`
- `HaltungExport` / `SchachtExport` : `{ string? Root; string DateiPattern; }` (Excel — nur Ordner + Dateiname)

Defaults reproduzieren das heutige Verhalten: `Ordner`/`Unterordner` leer, `Datei = {Datum}_{Haltung}` bzw. `{Datum}_{Schachtnummer}`, `Root = LastDistributionTargetFolder`. Damit ändert sich ohne Konfiguration nichts. Persistenz über den bestehenden atomaren `SettingsStore`.

## 6. UI (bisher leere Fläche der Export-Seite)

Row 3 (`Height="*"`) bekommt einen scrollbaren Bereich mit einer Karte pro Typ (Haltungen, Schächte, Dichtheit, Excel-Export). Jede Karte: Ziel-Wurzel (TextBox + „…"-Ordnerdialog), drei Muster-TextBoxen (bzw. eine beim Excel), eine Platzhalter-Legende und eine **Live-Vorschau** des fertigen Pfads/Namens anhand eines Beispiel-Datensatzes (erster passender Datensatz im Projekt; sonst Dummy-Werte). Änderungen werden debounced gespeichert und die Vorschau sofort aktualisiert. Konsistent zum bestehenden Karten-/Theme-Stil der Seite.

## 7. Verdrahtung in die Verteilung

Die Verteil-Commands (`DistributeHoldingsAsync`, `DistributeShaftsAsync`, `DistributeDichtheitAsync`) nutzen die konfigurierte Ziel-Wurzel statt des Zielordner-Dialogs (Dialog/Projektordner nur, wenn keine Wurzel gesetzt).

**Entscheidung 2026-07-14 (nach Code-Analyse des Verteilers):** Bei der Kanal-Verteilung ist die Datei-Benennung tief mit der Video-Zuordnung verwoben — PDF, Standard-Video, Gegeninspektions-Video und die Info-Dateien teilen alle das Schema `<Datum>_<Haltung>`, und die Video-Suche benutzt genau diesen Namen als Schlüssel. Ein frei wählbares Datei-Muster dort wäre kein „optionaler Parameter an einer Stelle", sondern ein breiter Eingriff in mehrere Distributor-Klassen (`HoldingFolderDistributor`, `ParsedHoldingDistributionController`, `DistributionFileTransfer`).

Der Nutzer hat sich deshalb für **nur konfigurierbare Ziel-Wurzel** entschieden: Pro Typ ist der Ziel-Ordner einstellbar, die Benennung darunter bleibt beim bewährten, mit der Zuordnung verwobenen Schema. Damit ist die Verdrahtung **streng additiv** (`ResolveConfiguredDistributionRoot`: gesetzte Wurzel hat Vorrang, sonst exakt bisheriges Verhalten) und fasst den Distributor **nicht** an. Freie Datei-Muster gelten nur für den **Excel-Export** (keine Video-Kopplung), umgesetzt in `ExportPageViewModel.BuildConfiguredExcelPath` über den `DistributionPatternResolver`.

**Vorgemerkt (eigenes, geprüftes Paket):** Voll konfigurierbare Ordner-/Datei-Muster auch für die Kanal-Verteilung — nur mit Regressionstests, da die ~3000 Videos an der Zuordnung hängen.

## 8. Testkonzept

- **DistributionPatternResolver:** Unit-Tests (TDD) für jeden Platzhalter, Sonderzeichen-Sanitizing, fehlende Felder, leere Ebenen (flach vs. tief), Endungserhalt, Schacht- vs. Haltungs-Kontext.
- **AppSettings:** Roundtrip-Test (Serialisierung der neuen Felder, Defaults).
- **ExportPageViewModel:** Test der Vorschau-Property (Muster-Änderung → korrekte Vorschau) und der Ziel-Wurzel-Nutzung statt Dialog.
- **UI:** Build + manuelle Abnahme.

## 9. Umsetzungsreihenfolge (kleine, testgeschützte Pakete)

1. `DistributionPatternResolver` + Interface + Tests.
2. `DistributionTargetConfig` + `AppSettings`-Felder + Roundtrip-Test.
3. UI-Konfigfläche + Vorschau-Property im ViewModel + Test.
4. Verdrahtung in die Verteil-Commands (additiv) + Excel-Export.

Jedes Paket: bauen (0/0), Tests grün, eigener deutscher Commit.
