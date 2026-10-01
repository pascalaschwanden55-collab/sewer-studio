# Fehleranalyse: Klassenbreiten-Diagnose (Arbeitsstand 2026-09-02)

Geprüft wurde der nicht eingecheckte Arbeitsstand auf `feature/eval-pruefsatz-review`:

| Datei | Art |
| --- | --- |
| `training/scripts/detect_klassenbreite.py` | neu, 175 Zeilen: baut aus dem Plan-Datensatz einen Datensatz mit weniger Klassen |
| `training/scripts/detect_klassenbreite_messung.py` | neu, 105 Zeilen: trainiert eine Stufe oder misst ein Gewicht, je Klasse |
| `CLAUDE.md` | 20 neue Zeilen mit dem Befund der Diagnose |
| `docs/quality/DETECT-LERNKURVE-UND-KLASSENBREITE-2026-08-30.md` | neu, Ergebnisbericht (nur Text) |

Die Zeilenzahlen und Fundstellen dieses Berichts beziehen sich auf diesen geprüften
Ausgangsstand vor der unten dokumentierten Umsetzung.

Vorgehen: Acht getrennte Prüfläufe (Zeile für Zeile, Querbezüge, Wiederverwendung,
Vereinfachung, Aufwand, weggelassene Schutzregeln) lieferten Kandidaten. Jeder Fund
unten ist danach von Hand am Code, an der installierten Ultralytics-Version 8.4.56
und an den echten Dateien unter `C:\KI_BRAIN\training` nachgeprüft worden.
Was sich nicht belegen liess, steht nicht in dieser Liste.

## Ergebnis in einem Satz

Die Zahlen der Diagnose stimmen mit den Belegdateien überein. Beim Referenzlauf am
13.08. legte dasselbe unsichere Trainingsmuster jedoch `yolo26n.pt` in den
freigegebenen Plan-Datensatz; der Gold-Trainer wies ihn deshalb zum Prüfzeitpunkt ab.
Im damals neuen Messskript fehlten zudem mehrere Schutzregeln.

## Ausführungsstand 2026-09-02

Das störende `yolo26n.pt` wurde nicht gelöscht. Es wurde bytegleich und damit
wiederherstellbar nach
`C:\KI_BRAIN\training\diagnostics\quarantine\ea8e715f-yolo26n-9B09CC8B.pt`
verschoben (5'544'453 Bytes, SHA-256
`9B09CC8BF347F0FC8A5F7657480587F25DB09B34BF33B0652110FB03A8AD4FEF`). Im
Plan-Datensatz ist die Datei nicht mehr vorhanden. Der Gold-Validator akzeptierte
den Datensatz danach mit 852 Bildern und 894 Instanzen.

Die drei vorhandenen `best.pt` wurden danach ohne neues Training einheitlich mit
`half=False, batch=4` nachgemessen. Für `BCC_bogen` ergaben sich AP50 0,8272 /
0,8151 / 0,8448 bei 15 / 5 / 2 Klassen. Gegenüber den historischen gemischten
Prüfeinstellungen änderte sich AP50 je Klasse höchstens um 0,0039. Der
FP16-/Stapel-8-Unterschied erklärt die Stufenunterschiede damit nicht allein.
Die drei neuen JSON-Belege heissen
`{referenz_15,klassen_5,klassen_2}_einheitlich_fp32_b4_klassenwerte.json`.

## Übersicht

| Nr | Schwere | Stelle | Befund | Beleg |
| --- | --- | --- | --- | --- |
| 1 | hoch | `detect_klassenbreite_messung.py:66` | `os.chdir` in den Datensatz lässt Ultralytics `yolo26n.pt` und Cache-Dateien dorthin schreiben. Allein `yolo26n.pt` führt zur Abweisung des Plan-Datensatzes `ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2`; die Cache-Dateien sind erlaubt und werden vom Gold-Trainer entfernt. | live nachgestellt |
| 2 | hoch | `detect_klassenbreite_messung.py:77` | Training startet ohne Prüfung des Prozesses `SewerStudio`, des Sidecars und des freien VRAM. | Code |
| 3 | mittel | `detect_klassenbreite_messung.py:69` | Referenz und Stufen werden mit verschiedenen Prüfeinstellungen gemessen (FP32/Stapel 4 gegen FP16/Stapel 8). | Ultralytics-Quelle |
| 4 | mittel | `detect_klassenbreite_messung.py:32` | `exist_ok=True` ohne Schutz des Laufordners: ein bestehender Lauf wird still überschrieben. | Code + Ultralytics-Quelle |
| 5 | mittel | `CLAUDE.md:529`, Doc | Die Diagnose lief mit `fliplr=0.5`, `hsv_h=0.015`, `hsv_s=0.7`, `hsv_v=0.4`, `mosaic=1.0`; der produktive Trainer mit `fliplr=0.0`, `hsv_h=0.01`, `hsv_s=0.3`, `hsv_v=0.3`. Der Unterschied war nicht erwähnt. | `args.yaml` der drei Läufe + Code |
| 6 | mittel | `detect_klassenbreite_messung.py:70` | Im Messmodus stammen die Klassennamen vom Gewicht, nicht vom Datensatz. Kein Abgleich. | Ultralytics-Quelle |
| 7 | niedrig | `detect_klassenbreite_messung.py:70` | Relative Pfade werden nach dem `chdir` falsch aufgelöst; ein blosser Name wie `yolo26m.pt` lädt still das offizielle COCO-Modell. | Code |
| 8 | niedrig | `detect_klassenbreite_messung.py:40` | Klassen ohne Soll-Box in der Validierung fehlen im Bericht ohne Hinweis. | Ultralytics-Quelle |
| 9 | niedrig | `detect_klassenbreite.py:101`, `:123` | Alles im Bildordner gilt als Bild; unbekannte Klassen-ID endet als nackter `KeyError`; Arbeitsordner bleibt bei Abbruch liegen. | Code |
| 10 | niedrig | `detect_klassenbreite_messung.py:24` | Absolute Pfade fest verdrahtet; Geschwister nutzen `REPOSITORY_ROOT` und `SEWERSTUDIO_KNOWLEDGE_ROOT`. | Code |
| 11 | niedrig | `detect_klassenbreite.py:44`, `:93` | Dritter eigener `data.yaml`-Leser und kopiertes Arbeitsordner-Ritual aus `detect_lernkurve.py`. | Code |
| 12 | niedrig | beide Skripte | Kein Test, obwohl die Checkliste in CLAUDE.md einen verlangt. | Ordner `training/scripts/tests` |
| 13 | niedrig | `CLAUDE.md:530` | Pfadangabe liest sich als Repo-Pfad, gemeint ist `C:\KI_BRAIN`. Zahlen doppelt zur Doc. | Code + Doc |
| 14 | niedrig | `tools/ExcelVorlagenBauer/ausgabe/` | Erzeugte Vorlagenkopien liegen ungeschützt vor einem `git add -A`; `.gitignore` kennt nur `output/`, nicht `ausgabe/`. | `git check-ignore`, SHA-256 |

## Die Funde im Einzelnen

### 1. Der freigegebene Plan-Datensatz war zum Prüfzeitpunkt für den Gold-Trainer unbrauchbar

Was passiert: `detect_klassenbreite_messung.py` wechselt vor dem Training mit
`os.chdir(args.dataset)` in den Datensatzordner. Ultralytics macht beim Trainingsstart
einen Selbsttest der halben Genauigkeit und lädt dafür `YOLO("yolo26n.pt")`. Fehlt die
Datei, wird sie in den aktuellen Ordner geladen, also in den Datensatz. Zusätzlich
entstehen `labels/train.cache` und `labels/val.cache`. Die Diagnoseläufe räumen diese
Artefakte nicht auf.

Beleg auf der Platte:

| Ordner | Fremddatei | Zeit |
| --- | --- | --- |
| `diagnostics/klassen_5` | `yolo26n.pt` | 30.08. 20:57 (Start dieser Stufe) |
| `diagnostics/klassen_2` | `yolo26n.pt` | 30.08. 21:25 (Start dieser Stufe) |
| `datasets/ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2` (Plan-Datensatz) | `yolo26n.pt`, `labels/train.cache`, `labels/val.cache` | 13.08. 16:41 (Referenzlauf `lernkurve_100`, gleiches Muster) |

Wirkung: `train_detect_gold.py::_collect_dataset_files` verlangt genau sechs
Wurzeleinträge. Live nachgestellt:

```
ABGEWIESEN: ValueError Der Datensatz enthaelt unerwartete oder fehlende Haupteintraege.
Extra-Eintraege: ['yolo26n.pt']
```

Damit konnte der produktive Gold-Trainer den freigegebenen Exportplan zum
Prüfzeitpunkt nicht mehr laden. Das verstösst gegen die Regel in CLAUDE.md, dass der plan-gesteuerte
Datensatz unverändert bleibt. Die Bilder, Labels, das Manifest und der Exportbeleg
selbst sind unberührt; nur die fremde Datei stört. Die beiden Cache-Dateien duldet
der Trainer ausdrücklich (`train_detect_gold.py:296`) und entfernt sie selbst; sie
blockieren nichts, gehören aber trotzdem nicht in den Datensatz.

Zur Herkunft: Das neue Skript lief gegen den Plan-Datensatz bisher nur im Messmodus
(`--gewicht`, 30.08.), und der lädt kein `yolo26n.pt`. Die Datei stammt vom
Referenztraining `lernkurve_100` am 13.08., das nach demselben Muster aus dem
Datensatzordner heraus gestartet wurde. Im Trainingsmodus würde das neue Skript
genau dasselbe tun; belegt durch `klassen_5` und `klassen_2`.

Vorschlag: Kein `chdir`. Stattdessen wie `train_detect_gold.py::_write_runtime_yaml`
eine kleine Laufzeit-`data.yaml` mit absolutem `path:` in einen Temp-Ordner schreiben
und diese an Ultralytics geben. Danach die Cache-Dateien entfernen, wie es
`_remove_ultralytics_label_caches` tut. Die 5,5 MB grosse `yolo26n.pt` im
Plan-Datensatz ist eine öffentliche Ultralytics-Datei. Sie wurde nach Abschluss der
Analyse nicht gelöscht, sondern wie im Ausführungsstand beschrieben in Quarantäne
verschoben.

### 2. Kein Sidecar- und VRAM-Schutz vor dem Training

`train_detect_gold.py:831` und `train_bcc_pilot.py` verweigern den Start, wenn der
Sidecar auf Port 8100 antwortet, der freie VRAM nicht sicher messbar ist oder weniger
als 28'000 MB frei sind. Das Messskript ruft `YOLO(BASIS).train(...)` ohne diese
Prüfung auf. Läuft SewerStudio mit geladenen KI-Modellen, konkurrieren Training,
DINO, SAM und Ollama um dieselbe Grafikkarte. Das kann den Trainingslauf oder die
nächste Analyse wegen Speichermangel scheitern lassen.

Vorschlag: `import train_detect_gold as t` und `t.ensure_training_resources()` vor dem
Trainingszweig. `detect_gold_holdout_provenance.py:19` importiert das Modul bereits so.

`ensure_training_resources()` prüft nur den Sidecar über HTTP und den freien VRAM.
Ein geöffnetes `SewerStudio.exe` ohne laufenden Sidecar wird nicht erkannt, kann den
Sidecar aber während des Trainings nachstarten. Der Self-Training-Harness prüft dafür
mit `Process.GetProcessesByName("SewerStudio")` zusätzlich den Prozess. Das
Messskript muss vor jedem Trainingsstart ebenfalls fail-closed prüfen: Ein laufender
Prozess oder ein nicht sicher bestimmbarer Prozessstatus sperrt den Start. Diese
Startprüfung verhindert nicht, dass SewerStudio erst nach Trainingsbeginn geöffnet
wird; das Programm wird niemals automatisch beendet.

### 3. Referenz und Stufen mit verschiedenen Prüfeinstellungen gemessen

Beide Zweige messen `best.pt`, das ist in Ordnung. Aber sie messen es nicht gleich:

| Zweig | Genauigkeit | Stapel | Grund |
| --- | --- | --- | --- |
| trainiert (`klassen_5`, `klassen_2`) | FP16 | 8 | Der Trainer prüft mit `half = trainer.amp` und einem Prüf-Loader mit `batch_size * 2` |
| nur gemessen (`referenz_15`) | FP32 | 4 | `model.val(batch=4)`, `half` bleibt aus |

Nachgeprüft in Ultralytics 8.4.56: `trainer.py` Zeile 210 (`batch_size * 2`) und
`validator.py` (`self.args.half = ... and trainer.amp`). Mit `rect=True` ändert der
Stapel auch die Letterbox-Geometrie. Belegt ist damit nur, dass eine zweite,
unkontrollierte Grösse im ursprünglichen Vergleich steckte. Vor der Nachmessung
war ihr Anteil an der BCC-Schwankung 0,827 / 0,811 / 0,844 nur eine Vermutung.
Die spätere einheitliche Messung ergab 0,827 / 0,815 / 0,845 und änderte AP50
höchstens um 0,004. Die gemischten Prüfeinstellungen waren also nicht die
alleinige Erklärung.

Vorschlag: Nach jedem Training eine ausdrückliche Messung von `weights/best.pt` mit
`model.val(..., batch=4, half=False)` fahren und nur daraus `werte_je_klasse` bilden.
166 Bilder dauern wenige Sekunden. Referenz und Stufen laufen dann durch denselben Weg.

### 4. Bestehende Läufe werden still überschrieben

`PARAMETER` enthält `exist_ok=True`. Der einzige Schutz prüft die JSON-Datei unter
`diagnostics`, nicht den Laufordner unter `cls_runs`. Ultralytics löscht beim Start
eine vorhandene `results.csv` (`trainer.py`, `self.csv.unlink()` ohne `resume`),
schreibt sie neu und ersetzt `args.yaml`, `weights/best.pt` und `weights/last.pt`.

Szenario: Die JSON wird umbenannt oder `--name lernkurve_100` versehentlich gewählt.
Der Schutz greift nicht; Ergebnisdatei, Argumente und Referenzgewicht sind danach
ersetzt, der alte Lauf ist weg. Die in CLAUDE.md zitierten Zahlen wären dann nicht
mehr aus den Belegdateien nachrechenbar.

Vorschlag: `exist_ok=False` und vorab `(LAUFORDNER / args.name).exists()` mit
`SystemExit`.

### 5. Diagnose mit anderem Trainingsregime als der Kandidat

`args.yaml` der Läufe `lernkurve_100`, `klassen_5` und `klassen_2`:

```
flipud: 0.0  fliplr: 0.5  hsv_h: 0.015  hsv_s: 0.7  hsv_v: 0.4  mosaic: 1.0
```

Produktiver Trainer (`train_detect_gold.py:961`) und BCC-Pilot:

```
flipud=0.0  fliplr=0.0 (Uhrlage!)  hsv_h=0.01  hsv_s=0.3  hsv_v=0.3
```

Die Trainings-Augmentierung ist zwischen den drei Diagnosestufen konsistent. Der
Klassenversuch verändert durch entfallene Boxen aber zugleich den Hintergrunddruck;
in den veröffentlichten Klassenwerten unterscheiden sich zudem die Prüfeinstellungen
von Referenz und engen Stufen. Die Klassenmenge ist damit nicht die einzige geänderte
Grösse des berichteten Vergleichs. Ausserdem übertrug CLAUDE.md „nicht verengen" und
„mehr Epochen bringen nichts" zu direkt auf die produktive Linie ohne Spiegelung.
Der Docstring „Alles Uebrige bleibt Standard" verdeckt diese Abweichung.

Vorschlag: Einen Satz in Doc und CLAUDE.md ergänzen. Künftige Diagnoseläufe mit den
Augmentierungen des Kandidaten fahren.

### 6. Klassennamen im Messmodus vom Gewicht statt vom Datensatz

`DetectionValidator.init_metrics` setzt `metrics.names = model.names` (nachgeprüft).
Die Label-IDs kommen aus dem Datensatz. `werte_je_klasse` beschriftet die Zeilen also
mit den Namen des Gewichts. Ein 15-Klassen-Gewicht auf dem 2er-Satz liefert eine
plausible Tabelle mit falschen Namen. Beim aktuellen Referenzlauf passte es zufällig,
weil Gewicht und Datensatz dieselben 15 Klassen in derselben Reihenfolge tragen.

Vorschlag: `modell.names` gegen `lies_klassenkarte(data.yaml)` vergleichen, bei
Abweichung abbrechen.

### 7. Relative Pfade nach dem Ordnerwechsel

`--gewicht` und `--dataset` werden nicht aufgelöst, bevor `os.chdir` läuft. Ein
relativer Gewichtspfad zeigt danach ins Leere. Ein blosser Name wie `yolo26m.pt` wird
von Ultralytics als GitHub-Datei verstanden und still heruntergeladen; gemessen wird
dann das offizielle COCO-Modell, und der Bericht sieht korrekt aus. Der Bericht speichert
ausserdem nur die Pfadtexte, keine SHA-256 von Gewicht oder Datensatz.

Vorschlag: `.resolve()` vor jeder Verwendung; entfällt grösstenteils mit Fund 1.
SHA-256 des Gewichts und des `_export_receipt.json` in den Bericht aufnehmen.

### 8. Klassen ohne Soll-Box verschwinden still

`ap_class_index` enthält nur Klassen mit mindestens einer Soll-Box in der Validierung.
`BBA_wurzeln`, `BBD_boden` und `SONST_schaden` fehlen deshalb im 15er-Bericht ohne
Kennzeichnung. Eine Leserin kann „nicht messbar" nicht von „nicht gelaufen"
unterscheiden.

Vorschlag: Alle Klassen aus `data.yaml` ausgeben, fehlende mit `null` und dem Grund
„0 Soll-Boxen in val".

### 9. Robustheit von `detect_klassenbreite.py`

- `glob("*")` kopiert jeden Eintrag, auch `Thumbs.db` oder Unterordner, und zählt ihn
  als Bild. `train_detect_gold.py::_list_regular_files` filtert das bereits.
- `alt[alt_id]` wirft bei einer unbekannten Klassen-ID einen nackten `KeyError`,
  nachdem schon hunderte Bilder kopiert wurden.
- Kein `try/finally`: Der Ordner `.<ziel>.arbeit` bleibt bei jedem Abbruch liegen.
  `rmtree(..., ignore_errors=True)` löscht beim nächsten Lauf ohne Besitzmarker und ohne
  Verknüpfungsprüfung. `osd_datensatz.py:357` nutzt dafür `.staging-<uuid>` mit `finally`.

Der aktuelle Plan-Datensatz ist sauber (686/686 nur jpg/png), deshalb ist nichts davon
ausgelöst worden.

### 10. Fest verdrahtete absolute Pfade

`BASIS`, `LAUFORDNER` und `BERICHTE` sind Literale. `train_detect_gold.py:52` leitet
`REPOSITORY_ROOT` aus der Dateilage ab; über zehn Skripte lesen
`SEWERSTUDIO_KNOWLEDGE_ROOT`. In einer Arbeitskopie oder der abgeschotteten Testwelt
bricht das Skript beim Laden des Basisgewichts.

### 11. Doppelter Code

`lies_klassenkarte` ist der dritte handgeschriebene Leser des `names:`-Blocks neben
`train_detect_gold.py::_validate_data_yaml` und `train_bcc_pilot.py`. Er entfernt keine
Anführungszeichen, prüft `nc` nicht gegen die Namensliste und akzeptiert doppelte IDs.
Einfacher: `classes.txt` lesen, die der Exporter verbindlich neben `data.yaml` schreibt
und die der Gold-Trainer gleichwertig prüft. Das Arbeitsordner-Ritual
(`.arbeit`, `rmtree`, `mkdir`, `rename`) ist wörtlich aus `detect_lernkurve.py:79-118`
übernommen; beide Kopien driften bereits (`write_bytes` gegen `write_text`).

### 12. Kein Test

Für beide Skripte gibt es keinen Test. Die Kernlogik (Leser, Umnummerierung,
Negativdatei-Regel, `werte_je_klasse`) ist rein und in wenigen Zeilen prüfbar. Eine
spätere Änderung an der Umnummerierung würde still den Sinn von `klassen_5` verändern,
ohne dass etwas rot wird.

Umgesetzt: Für beide Skripte liegen fokussierte Unit-Tests unter
`training/scripts/tests/`. Sie prüfen unter anderem Klassenkarten, Label-IDs,
Staging-Besitz, Prozess-/VRAM-Schutz, FP32/Stapel 4, Laufordner-Schutz,
Klassenabgleich, Nullwerte und Cache-Bereinigung.

### 13. CLAUDE.md-Abschnitt

- „unter `training/diagnostics` und `training/cls_runs`" liest sich als Repo-Pfad. Die
  Läufe liegen unter `C:\KI_BRAIN\training\...` (KnowledgeRoot). Der Doc-Bericht sagt es
  richtig; nur dieser Satz nicht.
- Die 20 Zeilen wiederholen Zahlen, die vollständig in der Doc stehen. Für künftige
  Sitzungen genügt: reine Diagnose, nie aktivieren; die Lernkurve spricht für weitere
  Goldboxen, beweist aber keinen alleinigen Materialengpass; die konkrete Verengung
  zeigte keinen Vorteil. Dazu gehören die Grenzen aus Fund 3 und 5.
- Alle Zahlen des Abschnitts wurden geprüft und stimmen mit `args.yaml`, `results.csv`,
  `lernkurve.json`, `klassenbreite.json` und `*_klassenwerte.json` überein.
- Nebenbefund ausserhalb des Diffs: Der Docstring von `detect_lernkurve.py` behauptet
  eine Ziehung je physischer Haltung, der Code gruppiert je Bild. Die neue Doc sagt es
  richtig, das Skript nicht.

### 14. Ausgabeordner des Vorlagenbauers nicht ignoriert

`vorlage.py` baut nach `tools/ExcelVorlagenBauer/ausgabe/`; nur `--uebernehmen` kopiert
nach `Export_Vorlage/`. `.gitignore` Zeile 130 ignoriert `tools/**/output/`, der Ordner
heisst aber `ausgabe`. `git check-ignore` meldet nichts. Beide Dateien sind heute
bytegleich mit `Export_Vorlage/` (SHA-256 `b9e94458…` und `cae09855…`). Ein
`git add -A` würde zwei Kopien der Vorlagen einchecken, die beim nächsten Bau ohne
`--uebernehmen` von den ausgelieferten abweichen.

Vorschlag: `tools/ExcelVorlagenBauer/ausgabe/` in `.gitignore` aufnehmen.

## Was in Ordnung ist

- Die Zahlen in CLAUDE.md und Doc stimmen mit den Belegdateien überein.
- `data.yaml` wird von allen drei Erzeugern in genau der Form geschrieben, die
  `lies_klassenkarte` liest.
- Leere Labeldateien werden von Ultralytics als Negativbilder akzeptiert.
- `train()` und `val()` messen beide `best.pt`; der Vertrag `ap_class_index` mit
  `p`, `r`, `ap50`, `ap` hält in 8.4.56.
- Der atomare Rename des fertigen Arbeitsordners ist korrekt.

## Umsetzung der empfohlenen Reihenfolge

1. Erledigt: `yolo26n.pt` aus dem Plan-Datensatz in Quarantäne verschoben und den
   Gold-Validator erfolgreich erneut ausgeführt.
2. Erledigt: Datensatz-`chdir` durch eine absolute Laufzeit-`data.yaml` in einem
   Temp-Ordner ersetzt; Cache-Dateien werden vor und nach dem Lauf sicher entfernt.
3. Erledigt: Neues Training prüft `SewerStudio.exe`, Sidecar und mindestens
   28'000 MB freien VRAM, beendet aber keinen Prozess.
4. Erledigt: Alle drei `best.pt` einheitlich mit `half=False, batch=4` nachgemessen.
5. Erledigt: `exist_ok=False`, sichere Namen und doppelte Laufordner-Prüfung.
6. Erledigt: Exakter Abgleich der Gewichtsklassen gegen den Datensatz.
7. Erledigt: Doc und `CLAUDE.md` nennen Augmentierung, `C:\KI_BRAIN` und Grenzen.
8. Erledigt: Fokussierte Tests sowie sichere Pfade, Labels, Staging, Nullwerte und
   SHA-256-Belege; der Builder nutzt die gemeinsame Gold-YAML-Prüfung.
9. Erledigt: `tools/ExcelVorlagenBauer/ausgabe/` ist in `.gitignore` eingetragen.

Die historischen `yolo26n.pt` in `diagnostics/klassen_5` und `klassen_2` wurden
nicht verändert. Der gezielte Messpfad liest sie nicht und legt keine neue Datei
im Datensatz ab.

## Nicht nachgeprüfter Hinweis

Ein Prüflauf meldete, dass `publish_bcc_copilot_candidate.py --lauf` jeden Laufordner
annimmt, nur die 15er-Klassenkarte prüft und keinen Datensatzbeleg bindet. Ein
`lernkurve_*`-Lauf (15 Klassen) könnte so als `not_deployed`-Kandidat veröffentlicht
werden, obwohl er mit Spiegelung trainiert wurde. Die Laufordner unter `cls_runs`
tragen keine maschinenlesbare Diagnose-Marke. Sidecar und Holdout-Werkzeuge suchen
nur unter `training/models/candidates` und verlangen ein Manifest; das ist belegt.
Der Publisher-Weg wurde aus Budgetgründen nicht mehr selbst nachvollzogen.

## Änderungsvermerk 2026-09-02

Nach einer Gegenprüfung durch Codex wurden vier Stellen korrigiert, alle am Code
nachgeprüft:

| Stelle | vorher | jetzt |
| --- | --- | --- |
| Fund 1 | Cache-Dateien als Teil der Abweisung lesbar | nur `yolo26n.pt` blockiert; Caches sind geduldet (`train_detect_gold.py:296`) |
| Fund 1 | „das Messskript hat verschmutzt" | Verschmutzung stammt vom Referenzlauf am 13.08. nach demselben Muster; das Skript lief bisher nur im Messmodus gegen den Plan-Datensatz |
| Fund 3 | Messunterschied als Erklärung der BCC-Schwankung | nur als Vermutung, Beleg erst durch Nachmessung |
| Fund 4 | `results.csv` werde angehängt | Ultralytics löscht sie beim Start und schreibt sie neu (`trainer.py:134`) |

Ergänzt: Prozessprüfung auf `SewerStudio` in Fund 2.

## Grenzen dieser Analyse

- `output/imagegen/` (Logo-Entwürfe, rund 6 MB) wurde nicht geprüft; keine Regel in
  CLAUDE.md betrifft den Ordner.
- Die Doc selbst wurde nur auf Zahlen und Pfade gegengelesen, nicht fachlich bewertet.
- Diese Grenzen beschreiben die ursprüngliche, rein lesende Analyse. Änderungen
  erfolgten erst danach auf den ausdrücklichen Umsetzungsauftrag hin und sind im
  Ausführungsstand oben einzeln dokumentiert.
