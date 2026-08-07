# Plan: schmal bauen auf dem, was messbar funktioniert

Stand 2026-08-07. Ersetzt die Prioritäten aus
[detect-strategie-2026-08-06.md](detect-strategie-2026-08-06.md); die dortigen
Messregeln und Klassenbefunde bleiben gültig.

## 1. Die Beweislage nach zwei Messtagen

Alles, was geprüft wurde, nebeneinander — gemessen an `detect_benchmark_v1`,
`conf=0,25`, drei Seeds wo angegeben:

| Ansatz | Ergebnis |
|---|---|
| 13-Klassen-Detektor, boxgenau | 13–20 % Recall |
| dasselbe, nur Lokalisierung ohne Klasse | 18–26 % |
| dasselbe, auf Bildebene als Filter | 34–49 % Bild-Recall, 77–92 % Spezifität |
| **nur BCC_bogen im Mehrklassenmodell** | **23–28 von 37 = 62–76 %** |
| BAB als eigenes Einzelklassenmodell | 0 Treffer bei `conf=0,25`, höchste Konfidenz 0,13 |
| BAB-Daten verdreifachen (43 → 132 Haltungen) | kein messbarer Effekt (1 → 2 von 40) |
| 99 → 205 Epochen | kein Effekt (F1 21,5 → 21,2 %) |
| Batch 3 → 8 | kein Effekt |
| Schwellenwert 0,25 → 0,60 | kein brauchbarer Arbeitspunkt |

**BCC ist um Faktor drei bis vier besser als alles andere und das Einzige auf
brauchbarem Niveau.**

Das Muster dahinter ist erkennbar: Das Modell kann, was **gross, geometrisch und
eindeutig** ist. Es scheitert an allem **Feinen, Flächigen, Kontextabhängigen**.
Ein Bogen füllt das Bild und hat eine Form. Ein Riss ist zwei Pixel breit.

## 2. Entscheidung

**Schmal und tief statt breit und dünn.** Der 15-Klassen-Detektor wird nicht
weiter ausgebaut. Er bleibt als Kandidat bestehen, `not_deployed`, ohne weitere
Investition.

Stattdessen wird die Klasse ausgebaut, die nachweislich trägt.

## 3. Arbeitspakete

### Paket 1 — BCC als Einzelklassenmodell messen (zuerst, billig)

Frage: Schlägt ein Modell, das nur Bögen lernt, die 23–28 von 37 aus dem
Mehrklassenmodell?

- Datensatz aus dem vorhandenen Export `61370615b1c1…` filtern: nur Klasse 14
  `BCC_bogen`, Klassen-ID auf 0 umschreiben, Geometrie unverändert.
  Vorlage: der BAB-Versuch, Skript liegt im Scratchpad-Muster vor.
- Hintergrundanteil wie dort: rund 15 % echte Negative aus dem Datensatz.
- **Drei Seeds**, 300 Epochen, Geduld 80, Batch 8, `workers 8`, `cache ram`.
- Messung gegen `detect_benchmark_v1`, dabei **BCC getrennt nach Herkunft**
  ausweisen (Holdout v1 gegen Extension v1) — die Herkunftstrennung gilt ab
  jetzt immer.
- Diagnose, kein Release-Kandidat. Ergebnis liegt ausserhalb der aktiven
  Kandidatenordner.

**Entscheidungsregel:** Erreicht der Einzelklassen-BCC im Mittel über drei Seeds
mehr als 30 von 37, ist das der Produktweg. Bleibt er bei 23–28, ist die
Mehrklassenkonkurrenz nicht das Problem und das Mehrklassenmodell reicht als
BCC-Lieferant.

### Paket 2 — Fehlalarme des BCC-Wegs bestimmen

Die Fehlalarmquote ist der praktische Blocker, nicht der Recall. Für den
BCC-Weg getrennt messen:

- Auf wie vielen der 75 Negativbilder feuert das BCC-Modell?
- Zusätzlich: Wie oft feuert es auf Positivbildern **ohne** BCC — also dort, wo
  ein anderer Schaden ist, aber kein Bogen? Das ist die eigentliche
  Verwechslungsgefahr (im Mehrklassenmodell war `BAJ_verbindung → BCC_bogen`
  die häufigste Verwechslung).

Zielgrösse benennen, nicht nur berichten: Ein Assistent mit über 20 % Fehlalarm
auf sauberem Rohr erzeugt bei 3000 Videos mehr Arbeit, als er abnimmt.

### Paket 3 — BCC-Daten gezielt erweitern (nur wenn Paket 1 trägt)

BCC steht bei 104 Haltungen im Register; gemessen verfügbar sind rund 374.
**Das ist die einzige Klasse, bei der die Datenfrage überhaupt sinnvoll gestellt
werden kann**, weil ein Signal da ist, das wachsen könnte.

- Kandidatenliste über `collect_class_candidates.py --classes BCC`,
  danach **Byte-Hash-Prüfung** gegen Gold, Eval, Negative (die Haltungsprüfung
  allein hat zweimal Bilder durchgelassen).
- Ziel: 104 → rund 200 Haltungen.
- Danach wieder drei Seeds, Vergleich gegen Paket 1.

**Vorsicht bei der Erwartung:** Die Spanne bei BCC ist 23–28 von 37, also ±13 %.
Ein Zuwachs unter etwa 8 Boxen ist nicht nachweisbar. Wenn die Verdopplung der
Daten nur 2–3 Boxen bringt, sieht man es nicht — und dann ist die Antwort
„nicht nachweisbar", nicht „hat nichts gebracht".

### Paket 4 — Produktiver Einsatz als Vorschlags-Assistent

Erst wenn Paket 1 und 2 stehen. Der BCC-Weg wird im Programm als reiner
Vorschlag mit menschlicher Bestätigung geführt — kein autonomes Codieren, keine
Aktivierung als Standardmodell. Architektur wie beim bestehenden BCC-Piloten:
ID- und SHA-gepinnter Kandidat, eigener GPU-Slot, produktiver Artefaktzeiger
unberührt.

## 4. Was zurückgestellt wird

- **BAH-Benchmark v2** — die 15 gewählten Haltungen und die vorbereitete
  Extraktion bleiben liegen. BAH ist mit 25–37 % (Holdout-Anteil allein) die
  drittbeste Klasse, aber nicht die erste. Wiederaufnehmen, wenn der BCC-Weg
  steht.
- **BAH-Sammlung** (39 freie Haltungen) — pausiert. Ohne Benchmarkdeckung wäre
  der Erfolg ohnehin nicht nachweisbar.
- **BAB und BAF** — endgültig, siehe Strategiedokument.
- **Qwen-Screening-Spur** — bleibt als eigener Strang für die feinen und
  flächigen Klassen, aber nach dem BCC-Weg.

## 5. Messregeln (unverändert gültig)

1. **Drei Seeds je Bedingung.** Einzelläufe sind keine Belege.
2. **Nach Herkunft getrennt berichten** — Holdout v1, Extension v1 (Goldquelle),
   künftige PDF-Erweiterungen. Eine gemischte Kopfzahl war beim BAH-Fall
   irreführend.
3. **Ein Effekt muss grösser sein als die gemessene Spanne.** Referenz vom
   2026-08-07: Recall 13,2–19,5 %, Precision 38,4–48,1 %, F1 20,7–26,5 %,
   Fehlalarmbilder 6–17 von 75.
4. `conf=0,25` bleibt Produktionsprotokoll. Schwellenläufe sind Diagnose.
5. **Byte-Hash-Prüfung** bei jeder Kandidatenauswahl. Die Haltungsprüfung allein
   hat zweimal Eval-Bilder durchgelassen (67 beim Negativsatz, 2 bei BAB).

## 6. Der zweite Ertrag dieser zwei Tage

Der wertvollste Fund war keine Modellverbesserung, sondern eine Zahl: Drei
identische Läufe streuen die Fehlalarmquote zwischen 8 und 23 Prozent.

Daran sind fünf Aussagen zerbrochen, die vorher wie Befunde aussahen — „die
Negative haben die Fehlalarme verdoppelt", „BAH erwacht", „längeres Training
hilft", „mehr Daten haben nichts gebracht", „der Langlauf ist präziser". Keine
davon war belegt.

Das ist kein Rückschritt. Es ist der Unterschied zwischen Messen und Raten.
