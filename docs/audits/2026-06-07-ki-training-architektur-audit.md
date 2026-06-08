# KI-/Trainingsarchitektur-Audit SewerStudio / AuswertungPro

Datum: 2026-06-07  
Scope: KI-Pipeline, VSA-Klassifikator, Trainingsdaten, Eval/Benchmark, Sidecar-Integration  
Arbeitsweise: Code gelesen, Reports abgeglichen, keine Produktionslogik geaendert

## Kurzfazit

Der eingeschlagene Weg ist richtig: weg von "Qwen soll den Schaden allein erkennen", hin zu einem eigenen, kleinen VSA-Klassifikator auf echten, beschrifteten Kanalbildern.

Der harte Beleg dafuer ist eindeutig:

- Qwen3-VL 8B erreicht auf dem sauberen 57er-Eval zwar 16/57, aber alle Treffer sind LEER. Befundcodes: 0/41.
- Der eigene YOLO-Klassifikator v1 erreicht auf denselben 57 Bildern 28/41 Befundtreffer, aber LEER faellt stark ab: 2/16.
- Der neue Datensatzbau ist fachlich viel sauberer als vorher: Eval-Bilder werden per Name und SHA-256 ausgeschlossen; dabei wurden zusaetzlich 35 umbenannte Kopien gefunden.

Die groesste Architektur-Luecke ist jetzt nicht mehr "haben wir Daten?", sondern: Der neue Klassifikator ist noch nicht sauber als erster Produktionspfad verdrahtet. Die laufende MultiModel-Pipeline ist weiter stark auf YOLO/DINO/SAM/Qwen ausgerichtet; der Klassifikator wird dort vor allem als Skip-/Filter-Signal genutzt, nicht als fuehrender VSA-Code-Entscheider.

## Aktueller Stand

| Bereich | Urteil | Beleg |
|---|---|---|
| Trainingsdaten | Stark verbessert | `ClassifierDatasetBuilder`, `ClassifierDatasetPlan`, Hash-Ausschluss, Haltungssplit |
| Eval-Set | Gut als Start, aber klein | 57 sichtbare Frames, 41 Befund/Meta, 16 LEER |
| VLM/Qwen | Nicht geeignet als primaerer Code-Erkenner | 0 % Befundcodes im sauberen Eval |
| Eigener Klassifikator | Richtiger Hebel, noch unausgereift | 68 % Befundtreffer v1, aber LEER nur 12,5 % |
| Sidecar | Endpoint vorhanden, Betriebsluecken | `/classify/yolo` existiert, aber Pfad/Geraet/imgsz/Metadaten sind hart bzw. unvollstaendig |
| Produktionspipeline | Noch nicht angepasst genug | `MultiModelAnalysisService` nutzt Klassifikation nicht als zentrale Code-Entscheidung |
| Reporting | Zu manuell | Python-Eval schreibt hauptsaechlich Konsole, keine stabile Experiment-JSON |

## Wichtigste Befunde

### 1. Klassifikator ist noch kein First-Class-Coder

Schwere: Hoch

Der Sidecar kann klassifizieren (`VisionPipelineClient.ClassifyYoloAsync`, `/classify/yolo`). In der echten Pipeline wird das Ergebnis aber vor allem genutzt, um Frames bei OTHER/NORMAL zu ueberspringen. Die eigentliche Befundlogik bleibt bei YOLO-Detection, DINO/SAM und Qwen-Anreicherung.

Es gibt mit `VsaCodeResolver.ResolveFromClassifier(...)` bereits einen guten zentralen Ansatz fuer die Code-Entscheidung. Der ist aber nicht klar als Hauptpfad in `MultiModelAnalysisService` verdrahtet.

Empfehlung:

- Einen klaren `ClassifierDecision`-Pfad einfuehren: Top-k, Konfidenz, Klasse, gemappter VSA-Code, Modellversion, Preprocessing-Profil.
- `VsaCodeResolver.ResolveFromClassifier(...)` als offiziellen Code-Resolver nutzen.
- Qwen nicht mehr als primaeren Schadenscoder behandeln, sondern nur fuer OSD/Meter, Erklaerung oder unsichere Faelle.
- Jede Entscheidung im Trace speichern: Modell, weights, imgsz, no-crop ja/nein, top-k, Schwellenwert, Grund fuer Annahme/Ablehnung.

### 2. LEER-Problem ist der wichtigste fachliche Fehler

Schwere: Hoch

Der Klassifikator erkennt Befunde deutlich besser als Qwen, uebermeldet aber Schaden auf echten Leerbildern. Das ist fuer den Betrieb gefaehrlich: Aus "alles leer" darf nicht "alles Schaden" werden.

Beleg aus v1:

- Gesamt: 30/57 = 52,6 %
- Befundcodes: 28/41 = 68,3 %
- LEER: 2/16 = 12,5 %

Empfehlung:

- Zweistufig arbeiten: erst "Befund ja/nein", dann Schadenscode.
- LEER/Normal als eigenes hartes Gate behandeln, nicht nur als normale Klasse.
- Mehr harte Negativbeispiele sammeln: saubere Rohre, Schachtkontext, OSD, schlechte Sicht, Wasser, Lichtreflexe.
- Per-Class-Thresholds kalibrieren. Ein globaler Top1-Schwellenwert reicht nicht.
- Unsichere Bilder in "Review" statt automatisch in LEER oder Schaden schieben.

### 3. Training/Eval sind noch nicht reproduzierbar genug

Schwere: Hoch

`train_cls.py`, `eval_cls.py`, `eval_threshold.py` und die Enhancement-Skripte sind praktisch nuetzlich, aber noch keine stabile Experiment-Infrastruktur.

Aktuelle Luecken:

- `eval_cls.py` gibt hauptsaechlich Konsolentext aus.
- Keine verpflichtende JSON/CSV-Ausgabe mit Confusion-Matrix, Per-Class-Metriken und Modell-Metadaten.
- Enhancement-Datensaetze haben keine starke Manifest-/Hash-Dokumentation.
- Training, Eval und Sidecar koennen unterschiedliche Preprocessing-Pfade verwenden.

Empfehlung:

- `ClassifierExperimentReport.json` einfuehren.
- Pro Lauf speichern: dataset hash, excluded eval hashes, train/val counts, class counts, weights path, model name, imgsz, batch, seed, no-crop, preprocessing, metrics, confusion matrix.
- Abnahmekriterien automatisch pruefen: Befund >= Baseline, LEER nicht schlechter, BAB/BAI/BAJ/BDD nicht schlechter.
- Berichte unter `docs/benchmarks/` oder `C:\KI_BRAIN\runs\...` eindeutig versionieren.

### 4. Split-Logik ist gut, aber nicht wirklich stratifiziert

Schwere: Mittel/Hoch

`ClassifierDatasetPlan.SplitByHaltung(...)` haelt Haltungen sauber zusammen. Das verhindert Leakage ueber nahe Frames derselben Haltung. Das ist richtig.

Aber: Die aktuelle Auswahl der Val-Haltungen ist deterministisch, nicht wirklich klassen-stratifiziert. Der Plan spricht von stratifiziertem Split; die Implementierung waehlt stabile Haltungskeys. Bei schwachen Klassen wie BBA kann das zufaellig zu wenig Val-Material erzeugen.

Empfehlung:

- `SelectValHaltungenStratified(...)` bauen.
- Ziele: jede Klasse in Train und Val, Mindestanzahl Frames je Klasse in Val, schwache Klassen priorisiert absichern.
- Tests fuer BBA/BAI/BAB und kleine Klassen ergaenzen.

### 5. Sidecar-Klassifikator ist noch zu hart verdrahtet

Schwere: Mittel/Hoch

Im Sidecar sucht `yolo_wrapper.py` den Klassifikator ueber feste Kandidatenpfade und verschiebt das Modell auf CPU. Ausserdem wird beim Predict kein klares `imgsz`/Preprocessing-Profil als Betriebsvertrag sichtbar.

Risiken:

- Falsches Modell kann geladen werden.
- v3/v4/v5 lassen sich nicht sauber im Betrieb unterscheiden.
- RTX 5090 wird fuer Klassifikation nicht ausgenutzt.
- Train/Eval/Produktions-Inferenz koennen auseinanderlaufen.

Empfehlung:

- Classifier-Optionen in Konfiguration: model path, device, imgsz, top-k, thresholds, preprocessing profile.
- Sidecar `/classify/yolo` soll Metadaten zurueckgeben: model_path, model_name/version, imgsz, device, preprocessing.
- Kein stiller Fallback auf alte Klassifikatorgewichte.
- GPU/TensorRT spaeter pruefen, sobald fachliche Qualitaet stabil ist.

### 6. No-Crop ist wichtig, aber nur als Monkeypatch vorhanden

Schwere: Mittel

`nocrop_patch.py` ersetzt Ultralytics-Crop durch Letterbox. Das ist fachlich plausibel, weil Schaeden am Rohrwandrand sonst abgeschnitten werden koennen.

Problem: Es ist ein optionaler Monkeypatch im Trainings-/Evalskript. Die Produktionsinferenz im Sidecar nutzt diesen Vertrag nicht sichtbar.

Empfehlung:

- No-Crop/Letterbox als offizielles Trainingsprofil behandeln.
- Profilnamen verwenden, z.B. `raw_1024_letterbox`, `bilateral_1024_letterbox`.
- Sidecar-Inferenz muss dasselbe Profil nutzen oder der Lauf ist nicht vergleichbar.
- Eval-Bericht muss no-crop ja/nein sichtbar speichern.

### 7. Konfiguration passt nicht zu den neuen 11 Klassen

Schwere: Mittel

`AiSettingsFactory` enthaelt class thresholds fuer klassische Detection-Codes, aber nicht sauber fuer den neuen 11-Klassen-Klassifikator: BCD, BCE, BDA, BDD, BAJ, BAF, BAB, BAI, BBB, BBA, LEER.

Empfehlung:

- Detection-Thresholds und Classification-Thresholds trennen.
- Alle 11 Klassifikator-Klassen explizit konfigurieren.
- Schwellen nicht raten, sondern aus `eval_threshold.py`/Kalibrierung ableiten.

### 8. Bildverbesserung ist nur dann sinnvoll, wenn sie messbar gewinnt

Schwere: Mittel

Die Bilateral-Aufbereitung ist methodisch sauberer als KI-Super-Resolution, weil sie keine neuen Details erfinden soll. Der aktuelle Test ist auch richtig gedacht: gleiche Aufbereitung auf Train und Eval, danach neu trainieren.

Aber: Der Nutzen ist noch nicht bewiesen. Er darf erst in die Pipeline, wenn v4 gegen v3 wirklich besser ist und LEER nicht leidet.

Empfehlung:

- Bilateral nur uebernehmen, wenn die Abnahmekriterien automatisch bestanden sind.
- Preprocessing-Parameter fest in den Experiment-Report schreiben.
- Generative Super-Resolution und Frame Generation nicht als Befund- oder Trainingsgrundlage verwenden.
- Multi-Frame-Verfahren spaeter pruefen, weil sie echte Frames kombinieren statt Details zu erfinden.

### 9. Qwen sollte architektonisch zurueckgestuft werden

Schwere: Mittel

Qwen ist fuer den aktuellen VSA-Code-Recall nicht tauglich. Auch mit YOLO-Hinweisen blieb der Befundcode-Recall auf dem sauberen Eval bei 0 %. Prompt-Haertung ist deshalb nicht der Haupthebel.

Empfehlung:

- Qwen nicht weiter als Primaer-Erkenner optimieren.
- Qwen nur fuer Aufgaben einsetzen, in denen es nachweislich hilft: OSD/Meter lesen, Plausibilisierung, Text-/Erklaerungsassistenz, unsichere Review-Faelle.
- Jede Qwen-Rolle separat benchmarken.

### 10. Temporal Voting fehlt als sauberer Betriebshebel

Schwere: Mittel

Kanalbefunde erscheinen ueber mehrere Frames. Eine Einzelbildentscheidung ist deshalb schwach. Die Architektur hat schon Dedup/Tracking-Ansaetze, aber der neue Klassifikator wird noch nicht als Zeitreihe ausgewertet.

Empfehlung:

- Pro Haltung ein Fenster ueber mehrere Frames bilden.
- Top-k pro Meterbereich sammeln.
- Code erst bestaetigen, wenn mehrere nahe Frames konsistent sind.
- LEER nur dann setzen, wenn ein ganzer relevanter Abschnitt leer bleibt.

### 11. Dataset Builder braucht noch Betriebs-Haerte

Schwere: Mittel

Der Builder ist fachlich gut, aber fuer dauerhafte Nutzung fehlen ein paar Sicherungen.

Empfehlung:

- Nach dem Kopieren nochmal Hash-Kontaminationscheck direkt auf dem Output ausfuehren.
- Output-Manifest mit jeder Datei, Klasse, Haltung, Hash schreiben.
- Dateinamen-Kollisionen erkennen. `File.Copy(... overwrite:true)` ist in einem neuen Ordner meist ok, kann aber stille Ueberschreibungen verstecken.
- Report-Text korrigieren: Name+Hash-Ausschluss kann groesser als die Eval-Bildzahl sein, weil Hash-Kopien gefunden werden.

### 12. Eval-Set ist fuer harte Entscheidungen noch zu klein

Schwere: Mittel

Das 57er-Eval ist wertvoll, weil sauber und eingefroren. Fuer Architekturentscheidungen reicht es. Fuer Produktion ist es zu klein.

Empfehlung:

- Pro Zielklasse mindestens 30-50 saubere Evalbilder aufbauen.
- Nach Kamera, Aufloesung, Rohrmaterial, Sichtqualitaet und Haltung trennen.
- Hidden-Eval behalten, damit man nicht auf das sichtbare 57er ueberoptimiert.

## Empfohlene Roadmap

### Sofort

1. `eval_cls.py` so erweitern, dass immer ein JSON-Report geschrieben wird.
2. Sidecar-Klassifikator konfigurierbar machen: model path, device, imgsz, preprocessing, thresholds.
3. `VsaCodeResolver.ResolveFromClassifier(...)` als kontrollierten experimentellen Hauptpfad verdrahten.
4. v3/v4/v5 nur noch mit identischen Reports vergleichen, nicht per Konsolentext.

### Naechste Woche

1. Stratifizierten Haltungssplit bauen.
2. LEER-Gate und per-class thresholds kalibrieren.
3. Hard-negative Mining fuer LEER/Normal/Schacht/Reflexe starten.
4. No-Crop/Letterbox als offizielles Profil in Train, Eval und Sidecar vereinheitlichen.
5. Temporal Voting fuer Klassifikator-Top-k einfuehren.

### Danach

1. Groesseres Modell oder 1024/1536-Vergleich fahren, aber nur mit gleicher Eval-Methodik.
2. GPU/TensorRT fuer Sidecar-Klassifikation pruefen.
3. Multi-Frame-Entrauschen/Frame-Integration erforschen.
4. Eval-Set auf produktionsnaehere Breite ausbauen.

## Nicht tun

- Qwen-Prompt weiter haerten und LEER global verbieten.
- KI-Hochskalierung oder Frame Generation als Befund- oder Trainingsgrundlage verwenden.
- Auf kontaminierten Alt-Datensaetzen trainieren und danach Erfolg messen.
- Ein Modell wegen 57er-Gesamtaccuracy in Produktion nehmen, wenn LEER oder kleine Schadensklassen leiden.
- Modellpfade im Sidecar still fallbacken lassen.

## Konkrete naechste Code-Schritte

1. `ClassifierExperimentReport` als kleines gemeinsames Report-Format definieren.
2. `eval_cls.py` und `eval_threshold.py` auf JSON-Ausgabe erweitern.
3. `PipelineConfig` um `ClassifierOptions` erweitern.
4. Sidecar `/classify/yolo` mit Modellmetadaten und konfigurierbarem `imgsz`/`device` ausstatten.
5. `MultiModelAnalysisService` um einen expliziten Klassifikator-Codepfad ergaenzen.
6. Danach erst v4/v5 fachlich vergleichen und entscheiden, ob Bilateral oder No-Crop in die Pipeline kommt.

## Schlussurteil

Die Architektur ist nicht falsch, aber sie ist gerade im Uebergang. Der Daten- und Trainingspfad ist inzwischen sauberer als die Produktionsintegration. Der naechste sinnvolle Schritt ist deshalb nicht noch mehr Recherche und nicht Prompt-Tuning, sondern die saubere Integration des eigenen Klassifikators mit reproduzierbarem Eval, LEER-Gate, Modellmetadaten und harten Abnahmekriterien.
