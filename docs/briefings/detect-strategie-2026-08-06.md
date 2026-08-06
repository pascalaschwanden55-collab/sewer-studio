# Detect-Strategie nach zwei Messtagen — Stand 2026-08-06

Übergabe an Kimi. Enthält, was belegt ist, was entschieden wurde und was zu
tun ist. Die Zahlen stammen aus fünf Läufen gegen denselben Holdout
`detect_release_holdout_45b66da2c778` (400 Bilder, 315 gewertet, 350 Sollboxen)
mit festem Protokoll `conf=0,25`, `imgsz=1280`, `IoU=0,5`.

## 1. Der wichtigste methodische Befund

**Die Streuung zwischen zwei Trainingsläufen ist grösser als die Effekte, die
wir messen wollen.** `BCC_bogen` hat sich in den Daten praktisch nicht
verändert (99 → 105 → 104 Haltungen) und schwankte trotzdem:

| Lauf | BCC-Treffer von 37 |
|---|---:|
| Ausgangsmodell `detect_gold_9eb020e30322` | 27 |
| Kurzlauf `detect_gold_3f45c1e945fe` | 29 |
| Langlauf `..._lang` (Batch 8, 205 Epochen) | 30 |
| `detect_gold_61370615b1c1` (Geduld 20, abgebrochen) | 24 |
| `..._geduld` (Geduld 80, 166 Epochen) | 22 |

59 % bis 81 % Recall bei unveränderten Daten, allein durch Zufall im Training.

**Konsequenz:** Einzelläufe können Datenwirkungen nicht nachweisen. Jede
künftige Aussage der Form „X hat geholfen" braucht entweder mehrere Seeds je
Bedingung oder einen Effekt, der grösser ist als diese Spanne. Alle bisherigen
Schlüsse aus Einzelläufen — auch meine — sind entsprechend zu behandeln.

## 2. Was belegt ist

### Klassenlage über alle fünf Läufe

| Klasse | Sollboxen | Treffer (min–max) | Urteil |
|---|---:|---|---|
| BCC_bogen | 37 | 22–30 | **funktioniert** |
| BCA_anschluss | 39 | 6–12 | funktioniert eingeschränkt |
| BAI_dichtung | 26 | 0–6 | unbestätigt, schwankt bis null |
| BAH_schadanschluss | 8 | 0–3 | unbestätigt, Stichprobe zu klein |
| BAF_oberflaeche | 89 | 0–7 | **funktioniert nicht** |
| BAB_riss | 40 | 0–2 | **funktioniert nicht** |
| alle übrigen | ≤ 21 | meist 0 | zu wenig Sollboxen für ein Urteil |

`BAF_oberflaeche` ist der belastbarste Negativbefund: 89 Sollboxen, die grösste
Stichprobe im Holdout, und das Modell findet über fünf Läufe zwischen 0 und 7.

### Trainingslänge und Stapelgrösse bringen nichts

- 40 → 99 Epochen: innerhalb eines Laufs echte Konvergenz, aber kein neues Niveau
- 99 → 205 Epochen (Batch 8): F1 21,5 % → 21,2 %, also unverändert
- Schwellenwert-Sweep über die Vorhersagen: kein Arbeitspunkt mit brauchbarer
  Precision und Recall gleichzeitig

### Mehr Daten für eine Klasse: kein messbarer Effekt

`BAB_riss` von 43 auf 132 Haltungen im Register (+177 Handlabels, rund drei
Stunden Arbeit). Ergebnis auf dem Holdout: 1 → 2 Treffer von 40. Innerhalb der
oben gezeigten Streuung, also kein Nachweis.

### Der Ein-Klassen-Versuch (entscheidend)

Reines `BAB_riss`-Modell, 212 Trainingsboxen aus 132 Haltungen, 15 % Hintergrund,
identische Parameter, 143 Epochen. **Diagnose, kein Kandidat** — liegt im
Scratchpad, nicht unter `KnowledgeRoot`.

| Schwelle | Treffer von 40 | Precision | Bilder mit Fehlalarm |
|---:|---:|---:|---:|
| 0,02 | 16 (40 %) | 0,7 % | 60 von 74 |
| 0,05 | 11 (27,5 %) | 1,7 % | 34 von 74 |
| 0,10 | 3 (7,5 %) | 2,2 % | 12 von 74 |
| 0,20 | 1 (2,5 %) | 100 % | 0 von 74 |

Höchste Konfidenz auf dem Holdout überhaupt: **0,130**. Das Modell überschreitet
die Produktionsschwelle nirgends.

Deutung: Es hat „rissähnliche Textur" gelernt — und rauer Beton ist voll davon.
Bei 788×576 und einem zwei Pixel breiten Riss fehlt die Information im Bild.
Das ist keine Frage der Datenmenge.

## 3. Entschieden

**BAB_riss und BAF_oberflaeche werden nicht weiter als Boxen gesammelt.**

Die Begründungen sind verschieden und beide sollen erhalten bleiben:

- **BAB**: Auflösungsgrenze. Belegt durch den Ein-Klassen-Versuch.
- **BAF**: Klassendefinition. Der Trainingsbestand boxt kleine lokale Flecken,
  der Holdout-Standard markiert rahmenfüllende Oberfläche — 62 % Musterdivergenz
  laut `artifacts/label-review-20260803/abweichungsliste.json`. Ein
  rahmenfüllender Oberflächenschaden ist eine Szeneneigenschaft, keine
  Objektklasse. Vor jedem weiteren BAF-Aufwand wäre die Definitionsfrage zu
  klären — die Antwort führt aber zum selben Ergebnis: keine BAF-Boxen mehr.

**Messdisziplin unverändert:** `conf=0,25` bleibt das Produktionsprotokoll. Der
Schwellenlauf war Diagnose und liegt bewusst nicht unter `training/reports`.

## 4. Arbeitsliste in dieser Reihenfolge

### 4.1 Holdout-Abdeckung prüfen, bevor gesammelt wird

Der Holdout hat 8 Sollboxen für BAH und 26 für BAI. Die Projektregel für
`ready_for_detect_evaluation` verlangt mindestens 20 je Klasse.

**Das heisst: BAH lässt sich auf diesem Holdout nicht bestätigen, egal wie viel
gesammelt wird.** Erst Holdout-Ziele schaffen, dann sammeln — sonst entsteht
unbeweisbare Arbeit.

Zu klären: Reicht eine Erweiterung des bestehenden Holdouts (dann ist er endgültig
kein unabhängiger Abnahmebestand mehr), oder braucht es einen frischen?

### 4.2 Schlank-Detektor als Assistenten-Strang

Sicherer Kern: **BCC_bogen, BCA_anschluss**.
Kandidaten mitführen: **BAH, BAI, BAJ** (BAJ 0–2 von 18, unentschieden).

Zweck ist ein Vorschlags-Assistent mit menschlicher Bestätigung, nicht
Autonomie. Dafür reichen 60–80 % Recall bei tragbarer Fehlalarmquote.

### 4.3 Fehlalarmquote als Zielgrösse führen

Über alle Läufe schlägt das Modell auf **12 bis 24 %** der sauberen Bilder an.
Ein Assistent mit 20 % Fehlalarm auf sauberem Rohr erzeugt bei 3000 Videos mehr
Prüfarbeit, als er abnimmt.

Der Schlank-Detektor wird an Fehlalarmen scheitern, nicht am Recall. Die Quote
gehört als eigenes Ziel in jeden Bericht, nicht als Nebenbedingung.

### 4.4 Screening-Spur für BAB und BAF über den Qwen-Weg

**Wichtig für die Metrik:** Ein Bild-Screening lässt sich nicht gegen Boxen bei
IoU 0,5 werten. Der ehrliche Vergleich ist bildweise:

> „Enthält dieses Bild mindestens einen Riss: ja/nein" — gemessen an den
> Holdout-Bildern mit mindestens einer BAB-Box gegen die 74 Negativbilder.

Das ist eine andere Zahl als „2 von 40" und muss von Anfang an so benannt sein,
sonst entsteht später ein Scheinvergleich. Erwartung dämpfen: Wenn die
Information in zwei Pixeln fehlt, fehlt sie. Der mögliche Gewinn liegt in der
besseren Nutzung von Kontext (Meter, Umgebung), nicht in neuer Auflösung.

### 4.5 Bildquelle

Strategisch richtig, taktisch ohne Wirkung: Die rund 3000 Bestandsvideos bleiben
PAL-SD. Für künftige Aufnahmen vormerken, für den aktuellen Plan ändert es nichts.

## 5. Datenstand

| | |
|---|---|
| Register `DETECT_ALL` | 1214 Goldbilder (996 Train, 218 Validation) |
| Negative | 286, Satz `proto_hn_fefb59779b86`, Modus `streng_reviewte_saetze` |
| Datensatz | `61370615b1c1458382472678879015aa4974a419ea60df8a3e9b5efc744af07e` |
| Gold-Audit | `gold_stock_audit_20260806_154328_776.json`, release-fähig |

Heute ausgeführt:

- Inbox-Reparatur: 246 Haltungsnummern über Byte-Beleg gesetzt, 15 dekontaminiert
  (`training/repairs/inbox_holding_id_repair_20260806_113952`)
- 19 Samples ohne prüfbare Haltungsnummer auf `Draft` gesetzt, mit KB-Deindex und
  Teacher-Bereinigung (`training/repairs/draft_ohne_haltung_20260806_154316`)
- Elf neue Codeentscheidungen eingetragen, `personal_gold_approval` neu gebunden

## 6. Noch nicht committet

- `training/scripts/train_detect_gold.py`: neue Optionen `--batch`, `--workers`,
  `--cache`. Voreinstellungen unverändert (3 / 0 / off), 250 Tests grün.
  Wirkung gemessen: `workers=8` plus `cache=ram` bringen rund 40 % kürzere
  Epochen; `batch=8` verändert die Qualität nicht messbar.
- `training/class_maps/detect_class_migration_v3.candidate.json`: elf
  Codeentscheidungen und neue Freigabebindung. Sicherung liegt als
  `.vor_freigabe_20260806` daneben.
- Hilfsordner `C:\KI_BRAIN\training\negatives\_kein_legacy_pool` (leer). Er
  verhindert, dass der Audit den alten 14-Bilder-Pool mit dem strikten Satz
  mischt. Sauberer wäre ein Schalter `--no-legacy-negatives` in
  `gold_stock_audit.py`.

## 7. Was nicht getan werden soll

- **Keine weiteren BAB- oder BAF-Boxen sammeln.** Belegt, nicht vermutet.
- **Keine Einzellauf-Vergleiche mehr als Beweis führen.** Siehe Abschnitt 1.
- **Nicht am `gold_stock_audit.py` schrauben, um Samples ohne Haltung
  durchzulassen.** Ich habe das zweimal versucht; beide Male brachen Tests, die
  absichtlich festhalten, dass solche Samples behalten und gemeldet werden.
  Der richtige Ort ist der Sample-Zustand, nicht der Wächter.
- **`conf=0,25` nicht aufweichen.** Der Schwellenlauf war Diagnose.
