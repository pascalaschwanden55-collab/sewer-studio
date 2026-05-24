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
| `dichtung` | Dichtung / Fuge |
| `infiltration` | Wasserzutritt |
| `sonstiges` | nicht sicher einordenbar |

Das passt besser zur echten App-Entscheidung:
erst Bildtyp erkennen, dann VSA-Code bestimmen.

### Schritt 3: Eval-Set nicht als Trainingsdaten verwenden

Das 120er Eval-Set bleibt Testdatenbestand.
Es darf nicht ins Training kopiert werden.

Stattdessen:

- aehnliche Bilder aus anderen Projekten sammeln
- gleiche Klassenstruktur wie oben verwenden
- danach gegen das unveraenderte Eval-Set pruefen

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

