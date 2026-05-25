# KI-Classifier vs. Eval-Set Gap 2026-05-24

## Kurzfazit

Der neue YOLO-Klassifikator `manual1286_fixedval_round3train_v8n_320_dropout02`
ist **nicht kaputt**, aber fuer das aktuelle 120er Eval-Set **nicht passend**.

Er wurde auf 8 grobe Schadensgruppen trainiert. Das Eval-Set prueft dagegen viele
konkrete VSA-Codes und 30 Leerbilder. Darum kann der Klassifikator viele Bilder
gar nicht korrekt loesen.

**Entscheidung:** Dieses Modell bleibt Kandidat. Es darf nicht nach
`sidecar/models/yolo_cls_best.pt` kopiert werden.

## Gepruefter Kandidat

| Feld | Wert |
|---|---|
| Modell | `YOLOv8n-cls` |
| Kandidat | `manual1286_fixedval_round3train_v8n_320_dropout02` |
| Pfad | `sidecar/models/candidates/classification/manual1286_fixedval_round3train_v8n_320_dropout02/best.pt` |
| Training | 1159 Train-Bilder, 127 Val-Bilder |
| Training-Val Top-1 | 61.417 % |
| Training-Val Top-5 | 95.276 % |
| Status | `keep_as_candidate` |

## Trainingsklassen

Der Kandidat kennt genau diese 8 Gruppen:

| Klasse | Train | Val |
|---|---:|---:|
| `riss_bruch` | 249 | 17 |
| `oberflaeche` | 197 | 14 |
| `versatz` | 161 | 14 |
| `ablagerung` | 153 | 5 |
| `anschluss` | 129 | 33 |
| `deformation` | 103 | 22 |
| `dichtung` | 91 | 12 |
| `infiltration` | 76 | 10 |

Das ist fuer eine grobe Schadensgruppen-Erkennung brauchbar.
Es ist aber kein VSA-Code-Modell.

## Eval-Set-Verteilung

Das aktuelle Eval-Set unter `C:/KI_BRAIN/eval_set/images` hat 120 Bilder.
Die wichtigsten Klassen:

| Code | Bilder |
|---|---:|
| `LEER` | 30 |
| `BCD` | 15 |
| `BDA` | 12 |
| `BAIZ` | 12 |
| `BCE` | 12 |
| `BDDC` | 12 |

Schon diese sechs Gruppen ergeben 93 von 120 Bildern.
Davon sind viele nicht in den 8 Trainingsklassen enthalten.

## Warum die Benchmarks schlecht ausfallen

Die schlechten Werte sind erwartbar:

| Benchmark-Modus | Ergebnis 10 Bilder | Bewertung |
|---|---:|---|
| Qwen allein | nicht belastbar historisch | alter kaputter Lauf mit vielen Leerantworten |
| `--yolo-context` | 0/10 Exact | zu harte falsche VSA-Kandidaten |
| `--yolo-presence-context` | 1/10 Exact | technisch sauberer, fachlich trotzdem falsche Klassenbasis |

Das Problem ist nicht nur der Prompt.
Der Klassifikator hat schlicht keine Klasse fuer viele Eval-Set-Ziele:

- `LEER`
- `BCD` / Rohranfang
- `BCE` / Rohrende
- `BDA`
- `BDDC`
- viele konkrete `BA*`, `BB*`, `BC*`, `BD*`-Codes

Wenn das Modell ein Leerbild sieht, muss es trotzdem eine der 8 Schadensgruppen
waehlen. Dadurch entsteht falscher Kontext fuer Qwen.

## Reproduzierbare Vorpruefung

Der Gap kann ohne Qwen, ohne Ollama und ohne Sidecar gemessen werden:

```powershell
dotnet run --project tools\EvalSetBenchmark -- `
  --coverage-only `
  --classifier-dataset "D:\sewer_pdf_manual_classification_round1_round2_fixedval_round3train_plus_teacher"
```

Ergebnis vom 2026-05-24:

```text
Eval-Abdeckung: 29/120 (24.2 %)
Fehlende Eval-Codes (Top):
  LEER      30 Bilder
  BCD       15 Bilder
  BCE       12 Bilder
  BDA       12 Bilder
  BDDC      12 Bilder
```

Diese Zahl ist wichtiger als die 61.417 % Training-Val-Accuracy.
Sie sagt: Das Modell passt nur zu rund einem Viertel des aktuellen Eval-Sets.

## Technische Schlussfolgerung

Der YOLO-Kandidat darf aktuell nur als **unsicherer Bildhinweis** verwendet werden.
Er darf nicht als VSA-Code-Vorschlag oder harter Import-Kontext gelten.

Der Modus `--yolo-presence-context` ist deshalb der richtige Schutz:

- YOLO-Hinweise werden nur als Beobachtung an Qwen gegeben.
- Qwen wird angewiesen, diese Hinweise nicht als VSA-Code zu uebernehmen.
- Leere Bilder duerfen trotz Hinweis weiterhin leer bleiben.

Das verbessert die Sicherheit, loest aber nicht die fachliche Luecke im Modell.

## Empfohlene naechste Schritte

### Schritt 1: Kandidat nicht befoerdern

Kein Kopieren nach:

```text
sidecar/models/yolo_cls_best.pt
```

Kein dauerhafter Start mit:

```text
SEWER_SIDECAR_YOLO_CLS_MODEL_PATH=...manual1286...best.pt
```

Nur fuer Tests verwenden.

### Schritt 2: Router-Modell statt 8-Schadensgruppen-Modell bauen

Ein neues kleines Modell sollte zuerst grob unterscheiden:

| Router-Klasse | Zweck |
|---|---|
| `leer` | kein relevanter Schaden sichtbar |
| `beginn_ende` | BCD/BCE, Meta-Frames |
| `wasserstand` | BDA/BDDC |
| `anschluss` | Anschluss sichtbar |
| `oberflaeche` | Oberflaechenschaden |
| `riss_bruch` | Riss / Bruch |
| `versatz` | Versatz / Lageabweichung |
| `ablagerung` | Ablagerung / Inkrustation |
| `wurzeln` | Wurzeleinwuchs |
| `dichtung` | Dichtung / Fuge |
| `infiltration` | Wasserzutritt |
| `sonstiges` | nicht sicher einordenbar |

Das passt besser zur echten App-Entscheidung:
erst Bildtyp erkennen, dann VSA-Code bestimmen.

Der Router-Plan kann direkt aus dem Eval-Set angezeigt werden:

```powershell
dotnet run --project tools\EvalSetBenchmark -- --router-plan-only
```

Aktueller Plan vom 2026-05-24:

| Router-Klasse | Eval-Bilder | Codes |
|---|---:|---|
| `leer` | 30 | LEER |
| `beginn_ende` | 27 | BCD, BCE |
| `wasserstand` | 26 | BDA, BDB, BDCZC, BDDC |
| `dichtung` | 12 | BAIZ |
| `oberflaeche` | 7 | BAFCE, BAJA, BAJB |
| `anschluss` | 5 | BCADA, BCAEA, BCCYA |
| `ablagerung` | 3 | BBBZ, BBCC, BBCZ |
| `riss_bruch` | 3 | BABAC, BABBA |
| `versatz` | 3 | BAHC |
| `deformation` | 2 | BAAA |
| `wurzeln` | 2 | BBAA, BBAB |

### Schritt 3: Eval-Set nicht als Trainingsdaten verwenden

Das 120er Eval-Set bleibt Testdatenbestand.
Es darf nicht ins Training kopiert werden.

Stattdessen:

- aehnliche Bilder aus anderen Projekten sammeln
- gleiche Klassenstruktur wie oben verwenden
- danach gegen das unveraenderte Eval-Set pruefen

Zum Bauen eines Router-Datasets gibt es jetzt einen sicheren Builder.
Er vergleicht Bild-Hashes mit dem Eval-Set und ueberspringt Treffer automatisch:

```powershell
dotnet run --project tools\EvalSetBenchmark -- `
  --build-router-dataset `
  --source-dataset "D:\sewer_pdf_manual_classification_round1_round2_fixedval_round3train_plus_teacher" `
  --source-file-list "C:\Users\Besitzer\Downloads\router_missing_candidates.txt" `
  --router-output "D:\sewer_router_dataset_candidate" `
  --source-file-list-val-ratio 0.15 `
  --max-per-class-split 800 `
  --dry-run
```

Dry-Run vom 2026-05-24:

| Klasse | Train | Val |
|---|---:|---:|
| `riss_bruch` | 249 | 17 |
| `oberflaeche` | 197 | 14 |
| `versatz` | 161 | 14 |
| `ablagerung` | 153 | 5 |
| `anschluss` | 129 | 33 |
| `deformation` | 103 | 22 |
| `dichtung` | 91 | 12 |
| `infiltration` | 76 | 10 |

Der Builder ist also bereit, aber diese Quelle deckt noch nicht alle Router-Klassen ab.
Es fehlen vor allem `leer`, `beginn_ende`, `wasserstand` und `wurzeln`.

Nach Hinzunahme von `router_missing_candidates.txt` wurde ein erster Router-Datensatz
gebaut:

```text
D:\sewer_router_dataset_candidate
```

Groesse:

| Split | Bilder |
|---|---:|
| train | 4'437 |
| val | 1'843 |
| total | 6'280 |

Trainingslauf:

```powershell
sidecar\.venv\Scripts\python.exe -c "from ultralytics import YOLO; YOLO('yolov8n-cls.pt').train(data=r'D:\sewer_router_dataset_candidate', imgsz=320, epochs=60, batch=32, dropout=0.2, patience=10, project=r'D:\sewer_cls_runs', name='router6280_11classes_v8n_320_dropout02')"
```

Ergebnis:

| Messung | Wert |
|---|---:|
| Dataset-Val Top-1 | 47.8 % |
| Dataset-Val Top-5 | 81.5 % |
| Eval-Set Router-Accuracy | 35 / 120 = 29.2 % |

Bewertung: **nicht aktivieren**.
Das Modell ist noch zu schwach. Hauptproblem: die Klasse `leer` fehlt im Trainingsdatensatz.
Auf dem Eval-Set werden viele Leerbilder als `wasserstand`, `oberflaeche` oder
andere Schadensklassen erkannt.

### Nachtrag 2026-05-25: Router mit echten `leer`-Bildern

Es wurden 888 echte `kein_schaden`-Bilder aus
`C:\KI_BRAIN\training_frames` gefunden. Diese Bilder sind als Quelle fuer `leer`
besser geeignet als `manual_decision=nein` aus den Review-CSV-Dateien.
Grund: `nein` bedeutet dort nicht automatisch "kein Schaden sichtbar".

Neuer Router-Datensatz:

```text
D:\sewer_router_dataset_candidate_with_leer
```

Quelle:

- `D:\sewer_pdf_manual_classification_round1_round2_fixedval_round3train_plus_teacher`
- `C:\Users\Besitzer\Downloads\router_missing_candidates.txt`
- `C:\Sewer-Studio_KI_4.3\.tmp\router_leer_candidates.txt`

Groesse:

| Split | Bilder |
|---|---:|
| train | 5'144 |
| val | 1'994 |
| total | 7'138 |

Wichtige Klassen:

| Klasse | Train | Val |
|---|---:|---:|
| `beginn_ende` | 800 | 800 |
| `wasserstand` | 800 | 588 |
| `leer` | 707 | 151 |
| `ablagerung` | 769 | 131 |
| `riss_bruch` | 681 | 98 |

Trainingslaeufe:

| Modell | Dataset-Val Top-1 | Eval-Set Router-Accuracy | Bewertung |
|---|---:|---:|---|
| `router7138_12classes_v8n_320_dropout02` | 49.7 % | 53 / 120 = 44.2 % | besser, aber nicht aktivieren |
| `router7138_12classes_v8s_320_dropout02` | 50.4 % | 51 / 120 = 42.5 % | schlechter als v8n |

`leer` wurde dadurch deutlich besser:

| Modell | `leer` korrekt im Eval-Set |
|---|---:|
| Router ohne `leer` | nicht sinnvoll messbar |
| `router7138_12classes_v8n_320_dropout02` | 22 / 30 = 73.3 % |
| `router7138_12classes_v8s_320_dropout02` | 22 / 30 = 73.3 % |

Aber: Viele echte Befund-Bilder werden weiterhin als `leer` erkannt.
Beim v8n-Router waren die groessten Fehler:

```text
wasserstand -> leer        14
beginn_ende -> leer         6
anschluss -> leer           5
dichtung -> leer            5
```

Zusaetzlich wurde ein einfacher `leer`-gegen-`befund`-Waechter getestet:

```text
D:\sewer_router_binary_empty_guard
D:\sewer_cls_runs\empty_guard1414_v8n_320_dropout02
```

Ergebnis auf dem Eval-Set:

| Klasse | Treffer |
|---|---:|
| `leer` | 28 / 30 = 93.3 % |
| `befund` | 42 / 90 = 46.7 % |
| Gesamt | 70 / 120 = 58.3 % |

Bewertung: **nicht aktivieren**. Der Waechter erkennt zwar Leerbilder gut,
uebersieht aber zu viele echte Befunde.

Stichproben der Fehler zeigen ein zweites Problem:
Einige Eval-Bilder sind zwar als Befund beschriftet, zeigen visuell aber kaum
erkennbaren Schaden. Beispiel: mehrere Bilder aus `81030-80945` sehen fuer einen
Bildklassifikator wie normale Rohrbilder aus, obwohl der Dateiname einen Code
enthaelt. Das ist kein Softwarefehler, sondern eine Datenfrage.

Konkrete Schlussfolgerung:

1. Der Router ist mit echten `leer`-Bildern messbar besser.
2. Er ist aber noch nicht robust genug fuer die App.
3. Der naechste Datenblock muss visuell gepruefte Befundbilder enthalten.
4. Das Eval-Set sollte in zwei Gruppen getrennt werden:
   - `sichtbar`: Schaden im Bild wirklich sichtbar
   - `protokolliert`: Code steht im Protokoll, Schaden im Einzelbild eventuell kaum sichtbar

Erst danach ist eine faire Modellentscheidung moeglich.

### Schritt 4: Benchmark-Ziel klar trennen

Es braucht zwei verschiedene Messungen:

| Messung | Zweck |
|---|---|
| Schadensgruppen-Accuracy | prueft das 8-Gruppen-Modell |
| VSA-Code-Eval | prueft die ganze Pipeline mit Qwen |

Diese zwei Zahlen duerfen nicht vermischt werden.

## Praktische Entscheidung fuer SewerStudio

Fuer den naechsten Entwicklungsstand gilt:

1. `manual1286...` bleibt Kandidat.
2. `--yolo-presence-context` bleibt als Testmodus erhalten.
3. Die App darf keine hohe KI-Genauigkeit aus diesem Kandidaten ableiten.
4. Der naechste Trainingsblock sollte ein Router-Datensatz sein, nicht ein weiterer
   Lauf mit denselben 8 Gruppen.
