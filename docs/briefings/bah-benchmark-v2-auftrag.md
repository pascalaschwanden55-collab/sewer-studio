# Auftrag: BAH-Benchmarkdeckung aus dem PDF-Kanal

Stand 2026-08-07. Ersetzt den BAH-Teil der Extension v1 — nicht deren
BAJ-Reservierung und nicht die BCA-Boxen.

## 1. Der Referenzstand steht — und er ist die eigentliche Nachricht

Drei identische Läufe auf Datensatz `61370615b1c1…`, nur der Seed unterschiedlich
(42/43/44), je 300 Epochen mit Geduld 80, Batch 8, gemessen gegen
`detect_benchmark_v1`:

| Grösse | Seed 42 | Seed 43 | Seed 44 | Spanne |
|---|---:|---:|---:|---|
| Recall | 15,3 % | 19,5 % | 13,2 % | 13,2–19,5 |
| Precision | 38,4 % | 41,3 % | 48,1 % | 38,4–48,1 |
| F1 | 21,9 % | 26,5 % | 20,7 % | 20,7–26,5 |
| **Fehlalarm-Bilder** | 17/75 | 14/75 | **6/75** | **8–23 %** |
| BCC (37) | 24 | 28 | 23 | 23–28 |
| BCA (53) | 12 | 13 | 11 | 11–13 |
| BAB (40) | 2 | 2 | 1 | 1–2 |
| BAF (89) | 1 | 3 | 0 | 0–3 |

Interne Validation der drei Läufe: mAP50 0,331 / 0,311 / 0,311.

**Die Fehlalarmquote schwankt um fast das Dreifache bei identischen Daten.**
Ausgerechnet die Grösse, die als Hauptziel gilt, ist die instabilste.

Rückwirkend heisst das: „Die Negative haben die Fehlalarme verdoppelt", „BAH
erwacht", „mehr BAB-Daten haben nichts gebracht" — alles Aussagen innerhalb
dieser Streuung. Keine davon war ein Beleg.

Stabil sind nur **BCA** (11–13 von 53) und einigermassen **BCC** (23–28 von 37).

## 2. Warum die Extension v1 den BAH-Teil nicht löst

Die 13 BAH-Boxen der Extension stammen aus Pascals **Goldtestrolle**. Nach
Herkunft getrennt gemessen:

| Seed | BAH im Original-Holdout (8) | BAH in der Extension (13) |
|---|---:|---:|
| 42 | 2 | 11 |
| 43 | 3 | 11 |
| 44 | 2 | 9 |

25–37 % gegen 69–85 %. Die gemischte Zahl (52–67 %) beschreibt keines von beidem.

**Ursache ist Auswahlverzerrung, nicht Technik.** Auflösungen sind gleich, die
Extension ist sogar stärker komprimiert (0,223 gegen 0,392 Bytes je Pixel). Die
Extension-Bilder kamen in den Prüfplatz, *weil* der Schaden dort deutlich sichtbar
war. Die Holdout-BAHs musste Pascal in einer blinden Review erst finden.

Die Neu-Review hat den Vollständigkeitsvertrag repariert, die **Auswahl** aber
nicht. Das war mein Denkfehler; deine ursprüngliche Trennung nach Herkunft war
die wichtigere Hälfte.

Gegenprobe, die das stützt: Bei **BCA** ist es umgekehrt — 26–28 % im Holdout,
nur 7–14 % in der Extension. Die beiden Bestände sind schlicht verschiedene
Aufgaben.

## 3. Was zu bauen ist

**`detect_benchmark_v2`** mit BAH-Deckung aus **derselben Quelle wie der
Original-Holdout**: PDF-Kanal, keine Goldbilder.

### Quelle und Umfang

- Aus den 39 freien BAH-Haltungen laut
  `docs/quality/BAH-VERFUEGBARKEIT-PDF-KANAL-2026-08-06.md`
- **12 bis 15 Haltungen** für den Benchmark reservieren. Ziel: mindestens
  20 BAH-Sollboxen aus dem PDF-Kanal.
- Die restlichen rund 27 bleiben dem Training. Zusammen mit den 65 bereits in
  Gold reicht das für das Trainingsziel von 50–70 Haltungen.

### Weg

Derselbe wie beim Original-Holdout, damit die Verteilung stimmt:
`prepare_detect_release_pdf_extraction.py` und der getrennte
`tools/DetectReleaseHoldoutPdfExtractor`. **Nicht** aus `gold_frames`, nicht aus
den XTF-/db3-Kandidatenlisten.

### Review

Blind, mit dem Holdout-Prüfplatz, Vertrag unverändert: **alle sichtbaren
Objekte der 15 Klassen** einzeichnen. Pascal rechnet mit rund 35 Bildern,
knapp eine Stunde.

### Zusammenführung

- Neuer eingefrorener Bestand `detect_benchmark_v2`, Original-Holdout und
  Extension v1 bleiben unangetastet als Herkunftsbelege.
- **Herkunft je Bild bleibt Pflicht** und muss in jedem Bericht als eigene
  Spalte auftauchen: `holdout_v1`, `extension_v1_gold`, `extension_v2_pdf`.
- Die Extension-v1-BAH-Boxen bleiben im Bestand, werden aber **nie in die
  Kopfzahl gemischt** — sie sind ab jetzt ein getrennt ausgewiesener
  Vergleichsteil („Bilder aus Goldquelle").
- Die BAJ-Reservierung und die 14 BCA-Boxen aus Extension v1 bleiben gültig.

### Schutz

Die neuen Benchmark-Haltungen müssen wie bei v1 unter `eval_set/subsets` liegen
und `haltung_key` in `_candidates.json` tragen — dann greift der Audit-Schutz
automatisch. Bei v1 nachgewiesen: alle 13 Haltungen erkannt, Kosten null
Trainingsbilder.

## 4. Messregeln ab jetzt

1. **Drei Seeds je Bedingung.** Einzelläufe sind keine Belege mehr.
2. **Nach Herkunft getrennt berichten.** Eine Kopfzahl über gemischte Quellen ist
   irreführend, wie der BAH-Fall zeigt.
3. **Ein Unterschied muss grösser sein als die Spanne aus Abschnitt 1.** Bei der
   Fehlalarmquote heisst das: unter einem Faktor 3 ist nichts nachweisbar.
4. `conf=0,25` bleibt Produktionsprotokoll.

## 5. Was nicht zu tun ist

- **Keine BAH-Sammlung starten, bevor die Deckung steht.** Mit 8 verwertbaren
  Sollboxen wäre auch eine Verdopplung der Trainingsdaten nicht nachweisbar.
- **Keine Goldbilder mehr als Benchmarkmaterial.** Der Weg ist geprüft und
  liefert eine leichtere Aufgabe als die echte.
- **Extension v1 nicht zurückbauen.** Sie ist als getrennt ausgewiesener
  Vergleichsteil wertvoll — sie zeigt den Abstand zwischen Goldquelle und
  PDF-Quelle, und dieser Abstand ist selbst ein Befund.
