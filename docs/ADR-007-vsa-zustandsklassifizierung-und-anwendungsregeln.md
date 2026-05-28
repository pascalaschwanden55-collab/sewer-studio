# ADR-007: VSA-Zustandsklassifizierung und Anwendungsregeln

**Status:** Accepted
**Datum:** 2026-05-27
**Accepted:** 2026-05-28
**Baut auf:** ADR-006 VSA-KEK-Code-Single-Source-of-Truth

## Quelle

Primaere fachliche Quelle:

`D:\Fachwissen\2_1_4_7_4 Zustandsbeurteilung\VSA_Rili_ Zustandsbeurteilung von Entwässerungsanlagen.pdf`

Dateistand:

- Groesse: 4'219'718 Bytes
- Letzte Aenderung: 2024-04-29 13:26:12
- Richtlinie: Zustandsbeurteilung von Entwaesserungsanlagen, Ausgabe 2023
- Relevant: Anhang C, Tabellen 7-32 fuer Kanaele; Anhang D, Tabellen 33-63 fuer Schaechte

Im gleichen Ordner existiert zusaetzlich `Modul_Zustandserfassung/`. Dieser
Ordner ist fuer spaetere Gegenpruefungen bekannt, aber nicht Quelle dieser ADR.

## Kontext

ADR-006 hat die VSA-KEK-Code-Wahrheit stabilisiert: Code-Bedeutungen kommen aus
dem Manifest. Das reicht fuer die Zustandsbeurteilung nicht aus.

Stand 2026-05-28: ADR-007 ist umgesetzt. `VsaEvaluationService` nutzt produktiv
die v2-Regeldateien aus der VSA-Richtlinie 2023. Legacy bleibt nur als
Rollback-Pfad ueber `VsaUseV2Engine=false` erhalten.

Smoke-Test 2026-05-28: echte Schaeden zeigen plausible, gestreute Noten;
Bestandsaufnahme-/Nicht-Bewertungscodes werden sauber als `n/a` angezeigt. D.2
ist damit fachlich und technisch abgeschlossen.

Die Richtlinie enthaelt eigene Klassifizierungstabellen. Diese bestimmen pro
Code, Charakterisierung, Anforderung und Quantifizierung den Einzelzustand fuer:

- D: Dichtheit
- S: Standsicherheit
- B: Betriebssicherheit

Damit gibt es zwei getrennte fachliche Wahrheiten:

- **Code-Wahrheit:** VSA-KEK-Manifest.
- **Klassifizierungs-Wahrheit:** VSA-Richtlinie Zustandsbeurteilung 2023.

## Befund

Die aktuelle Datei
`src/AuswertungPro.Next.UI/Data/classification_channels.json` widerspricht der
PDF-Quelle an kritischen Stellen.

| Code | PDF-Wahrheit | Aktuelle JSON-Bedeutung | Befund |
|---|---|---|---|
| `BAA` | Verformung | Riss, Q1 = Rissbreite mm | falsch |
| `BAB` | Risse | Bruch/Einsturz, Q1 = Ausmass % | falsch |
| `BAC` | Leitungsbruch/Einsturz | teils passend, aber quantifizierungsabhaengig modelliert | fraglich |
| `BAF` | Oberflaechenschaden | Q1 = Schadenausmass % | fraglich, PDF arbeitet zeilen-/charakterisierungsbezogen |
| `BBA` | Wurzeln | Deformation, Q1 = Verformung % | falsch |
| `BDD` | Wasserspiegel | Deformation, Q1 = Verformung % | falsch |

Die Tests in `VsaEvaluationServiceTests` verriegeln diesen falschen Zustand
teilweise, z. B. durch Annahmen wie `BAA` mit Rissbreite.

## Zusaetzlicher Befund: Modell zu grob

Das aktuelle Modell `VsaClassificationTable.VsaRule` kann nur:

- `Code`
- statische `EZD/EZS/EZB`
- einfache `QuantRules` pro `Q1/Q2`

Die PDF-Tabellen verlangen aber mehr:

- mehrere Zeilen pro Code
- `Ch1`
- `Ch2`
- Anforderung `D/S/B`
- Masseinheit
- Wertebereiche fuer Einzelzustand `0..4`
- Geltungsbereich, z. B. biegesteif/biegeweich oder DN-Grenzen
- Sonderfussnoten

Beispiel `BAA`: Die PDF unterscheidet Verformung nach Geltungsbereich
biegesteif/biegeweich fuer Standsicherheit und separat Betriebssicherheit. Das
ist mit dem aktuellen einfachen Code-Level-Rule-Modell nicht korrekt abbildbar.

Beispiel `BAB`: Risse haengen an `Ch1/Ch2` und teilweise an mm-Grenzen,
teilweise an fixer Klassifizierung ohne Quantifizierung. Ein einzelner
`BAB`-Block mit einer Q1-Regel ist fachlich nicht ausreichend.

## Weitere Risiken

`classification_channels.json` enthaelt Regeln fuer Codes, die in Anhang C der
PDF-Quelle nicht als Klassifizierungstabelle erscheinen:

- `BCA`
- `BCB`
- `BCC`
- `BCD`
- `BCE`
- `BDA`
- `BDB`
- `BDC`
- `BDF`
- `BDG`

Diese Regeln muessen entweder aus einer anderen offiziellen Quelle belegt oder
als nicht-verifizierte Erweiterung markiert werden.

`classification_manholes.json` ist ebenfalls nicht belastbar genug: Anhang D
enthaelt quantifizierungs- und bereichsabhaengige Schachtregeln, die aktuelle
JSON-Datei hat aber keine `QuantRules`.

## Entscheidungen

1. **JSON neu schreiben, nicht inkrementell reparieren.**  
   Die bestehende Struktur ist zu grob. Einzelne Werte in
   `classification_channels.json` zu flicken wuerde den Drift nur verschieben.

2. **Drift zuerst sichtbar machen.**  
   Phase A schreibt Tests, die den aktuellen Widerspruch beweisen. In dieser
   Phase wird noch keine Produktivlogik geaendert.

3. **Schachtregeln werden mit-auditiert.**  
   Nicht nur Kanaele sind betroffen. `classification_manholes.json` muss gegen
   Anhang D geprueft und spaeter in dieselbe Regelstruktur ueberfuehrt werden.

4. **Keine geschaetzten Schwellenwerte.**  
   Wenn eine Regel nicht sicher aus PDF/Quelle belegt ist, wird sie nicht
   erfunden. Stattdessen wird sie als fehlend markiert, mit Reason
   `missing-vsa-source`.

5. **`VsaEvaluationService` wird gegen Drift gehaertet.**  
   Der Service darf spaeter nicht still auf unvollstaendige oder widerspruechliche
   Legacy-Regeln fallen. Fallbacks muessen explizit und diagnostizierbar sein.

## Zielbild

Neue regelbasierte Struktur, zeilenorientiert statt codeorientiert:

```text
Code
Ch1Set
Ch2Set
Requirement: D/S/B
Unit
Ranges: Einzelzustand 0..4
Scope: DN, Material, biegesteif/biegeweich, Bereich
Footnote
Source: PDF page/table/row
```

Der Service waehlt dann nicht nur nach Code, sondern nach:

1. Code / Prefix
2. Ch1
3. Ch2
4. Anforderung D/S/B
5. Q1/Q2
6. Geltungsbereich

## Phasenplan

### Phase A: Drift-Tests rot

Ziel: Den aktuellen Widerspruch beweisen.

- Tests fuer `BAA`, `BAB`, `BBA`, `BDD`.
- Keine Produktivlogik aendern.
- Erwartung: Tests sind zunaechst rot.
- Ergebnis: klarer Beweis, dass Legacy-JSON nicht zur PDF-Quelle passt.

### Phase B: PDF lesen und Schwellen extrahieren

Ziel: Eine menschenlesbare Zwischentabelle als belastbare Quelle.

- Anhang C, Tabellen 7-32 vollstaendig erfassen.
- Anhang D, Tabellen 33-63 vollstaendig erfassen.
- Ausgabe: `docs/vsa-zustandsklassifizierung-2023-schwellen.md`.
- Jede Regel bekommt PDF-Seite, Tabelle und falls moeglich Tabellenzeile.
- Mehrdeutige Stellen werden nicht geraten, sondern mit `missing-vsa-source`
  markiert.

### Phase C: Neue JSON-Struktur einfuehren

Ziel: Neue Regeldateien parallel zu Legacy-JSON.

- `vsa_zustandsklassifizierung_2023_channels.json`
- `vsa_zustandsklassifizierung_2023_manholes.json`
- Alte Dateien bleiben zunaechst erhalten.
- Neue Struktur enthaelt Source-Referenzen.

### Phase D: Tests und Service umstellen

Ziel: `VsaEvaluationService` nutzt die neue strukturierte Regelengine.

- Erledigt 2026-05-28.
- Tests fuer Kerncodes aus der PDF sind aktiv.
- Alte falsche `BAA`-als-Riss-Tests wurden ersetzt.
- Fallbacks sind explizit: v2 produktiv, Legacy per `VsaUseV2Engine=false`.
- Diagnostics fuer fehlende oder nicht belegte Regeln bleiben im Shadow- und
  Report-Tool nachvollziehbar.

### Phase E: Alles gruen und Legacy klar markieren

Ziel: Reviewfaehiger Abschluss.

- Erledigt 2026-05-28.
- Shadow-Cutover fachlich freigegeben: bekannte Nicht-Bewertungen und
  freigegebene Norm-Korrekturen blockieren nicht mehr.
- Legacy-Dateien bleiben fuer Rollback/Shadow erhalten, sind aber nicht mehr
  produktive Quelle.
- ADR-007 Status ist `Accepted`.

## Aufwand

Grobe Schaetzung: 9-10 Stunden.

- Phase A: 0.5-1h
- Phase B: 3-4h
- Phase C: 2h
- Phase D: 2-3h
- Phase E: 0.5h

## Nicht-Ziele

- Keine automatische Korrektur bestehender Zustandsnoten.
- Keine geratenen Schwellenwerte.
- Keine LLM-generierten Klassifizierungswerte.
- Keine Vermischung mit ADR-006: Code-Bedeutung und Zustandsklassifizierung
  bleiben getrennte Wahrheiten.
- Kein Manifest-Change fuer Code-Bedeutungen.
- Kein Trainingsdaten-Relabeling.
- Keine automatische Umschreibung historischer Bewertungen; neue Bewertungen
  laufen ueber die v2-Regelengine.

## Offene Fragen

1. **EZ-Skala 0-4 vs. bestehende 1-5-Interpretation:**  
   Die PDF-Tabellen arbeiten mit Einzelzustand `0..4`. Bestehende UI- und
   Service-Texte muessen daraufhin geprueft werden.

2. **Q1/Q2-Konsistenz:**  
   Die Richtlinie verweist fuer Masseinheiten auf DIN EN 13508-2 und das
   VSA-Merkblatt Schadencodierung und Datentransfer. Wir muessen sicherstellen,
   dass `Quantifizierung1/2` im Import denselben Parametern entspricht.

3. **Geltungsbereich im Datenmodell:**  
   BAA unterscheidet z. B. biegesteif/biegeweich. Es muss geklaert werden, aus
   welchen Stammdaten dieser Scope im Programm sicher bestimmt wird.

4. **Cache-Invalidierung:**  
   Wenn Klassifizierungstabellen geladen/gecached werden, muss ein Update der
   JSON-Dateien sicher wirksam werden.

5. **Nicht belegte Erweiterungen:**  
   Fuer Codes wie `BCA/BCB/BCC/BCD/BCE/BDA/BDB/BDC/BDF/BDG` muss geklaert
   werden, ob sie aus einer anderen offiziellen Quelle stammen oder nicht in
   diese Zustandsklassifizierung gehoeren.

## Konkreter Codex-Auftrag Phase A

Schreibe Drift-Tests, die belegen, dass die aktuelle
`classification_channels.json` nicht zur VSA-Richtlinie 2023 passt.

Anforderungen:

- Nur Tests, kein Fix.
- Testdatei in `tests/AuswertungPro.Next.Infrastructure.Tests`.
- Pruefe mindestens:
  - `BAA` darf nicht als Riss/Rissbreite modelliert sein.
  - `BAB` darf nicht als Bruch/Einsturz modelliert sein.
  - `BBA` darf nicht als Deformation modelliert sein.
  - `BDD` darf nicht als Deformation modelliert sein.
- Die Tests duerfen lokal rot sein, muessen aber klar als Phase-A-Drift-Nachweis
  benannt werden.
- Nicht in einen PR mergen, solange Phase C/D nicht umgesetzt sind.

## Aktueller Status

Der Code-Katalog ist inzwischen robust. Die Zustandsbeurteilung ist es noch
nicht. Die naechste fachliche Arbeit muss die Klassifizierungsregeln betreffen,
nicht weitere Symbol- oder UI-Kosmetik.
