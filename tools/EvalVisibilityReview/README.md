# Eval-Prüfplätze

Die Werkzeuge zeigen Eval-Bilder lokal im Browser. Sie verändern das eingefrorene
Eval-Set nicht. Ergebnisse werden immer in einer getrennten Datei gespeichert.

## Ereignis-ID und Schadensstufe

Der Prüfplatz lädt `C:\KI_BRAIN\eval_set` nur lesend. Aus den 120 Bildern werden
automatisch nur die Schadenscodes der Gruppen BA und BB angezeigt.
Zu jedem Code zeigt er den Klartext aus dem aktiven VSA-KEK-2020-Katalog.

Start:

```powershell
.\tools\EvalVisibilityReview\start_eval_metadata_review.ps1
```

Pro Schadensbild werden diese Angaben erfasst:

- ob der Vorgabe-Code zum Bild passt
- bei Bedarf ein anderer BA-/BB-Schadencode mit Katalog-Klartext
- alternativ: kein passender Schaden sichtbar
- Schadensstufe 1 bis 5
- Ereignis-ID
- optionaler Meterbereich und Bemerkung
- Prüfer und Prüfzeitpunkt

Die Schadensstufe ist nur eine fachliche Gewichtung für den KI-Prüfsatz:

- Sie verändert weder den VSA-Code noch eine Zustandsklasse.
- Stufe 4 und 5 werden zusätzlich als wichtige Schäden ausgewertet.
- Für eine belastbare KI-Freigabe braucht der Prüfsatz mindestens 20
  unterschiedliche Ereignisse der Stufen 4 oder 5.

Mehrere Bilder desselben realen Schadens erhalten dieselbe Ereignis-ID. Der Knopf
`Wie vorheriger Schaden` übernimmt dafür die Angaben des vorherigen Bildes.
Eine Ereignis-ID darf innerhalb derselben Haltung nicht für verschiedene Codes,
Stufen oder Meterbereiche verwendet werden. Solche Konflikte werden wieder als
offen angezeigt.

Nur `Speichern und weiter` speichert eine Entscheidung. `Überspringen (nicht
speichern)` blättert bewusst ohne Speichern. Bei `Kein passender Schaden sichtbar`
sind Schadensstufe und Ereignis-ID nicht erforderlich; das Bild wird für die
spätere Schadensauswertung als ausgeschlossen gekennzeichnet.

Der Zwischenstand wird nach jedem Bild atomar gespeichert:

```text
C:\KI_BRAIN\eval_review\v1_event_metadata_review.json
```

Der Zielpfad muss ausserhalb von `C:\KI_BRAIN\eval_set` liegen. Ein bereits
vorhandener Zwischenstand wird nur fortgesetzt, wenn er zum unveränderten
`_candidates.json` gehört.

Nur Vorlage erzeugen, ohne den Browser-Prüfplatz zu starten:

```powershell
python .\tools\EvalVisibilityReview\eval_metadata_review_server.py --prepare-only
```

## Geprüfte Schadensbilder auswerten

Nach einer vollständigen und konfliktfreien Review kann das aktuelle
Ollama-Bildmodell direkt gegen diese menschlichen Entscheidungen gemessen werden:

```powershell
dotnet run --project tools\EvalSetBenchmark -c Release --no-build -- `
  --eval-set C:\KI_BRAIN\eval_set `
  --review-file C:\KI_BRAIN\eval_review\v1_event_metadata_review.json `
  --model qwen3-vl:8b-q8 `
  --out C:\KI_BRAIN\eval_review\benchmarks
```

Der Lauf prüft vor dem ersten KI-Aufruf den SHA-256 des eingefrorenen
`_candidates.json`, die Vollständigkeit und die Ereigniskonflikte. V1 bleibt
unverändert. Gemessen werden Schadenspräsenz, Fehlalarme, exakter Code,
Hauptcode, Schadensstufe und Ereignistreffer.

Dieser Modus misst das Ollama-Bildmodell ohne Hinweise von YOLO, DINO oder SAM.
Das QualityGate ist deshalb nicht Teil dieser Messung. Der JSON-Bericht weist
diese Grenze ausdrücklich aus.

## Vollstaendige Pruefkette auswerten

Fuer den produktionsnahen DINO-SAM-Qwen-QualityGate-Weg denselben Befehl um
`--full-chain` ergaenzen:

```powershell
dotnet run --project tools\EvalSetBenchmark -c Release --no-build -- `
  --eval-set C:\KI_BRAIN\eval_set `
  --review-file C:\KI_BRAIN\eval_review\v1_event_metadata_review.json `
  --full-chain `
  --model qwen3-vl:8b-q8 `
  --out C:\KI_BRAIN\eval_review\benchmarks
```

Sidecar und Ollama muessen dafuer bereits erreichbar sein. YOLO-Detect und
YOLO-cls bleiben im Prueflauf gesperrt, weil das aktive Altmodell nicht
qualifiziert ist. Der KB-Kontext bleibt ebenfalls aus. Der Bericht trennt
DINO-, SAM-, Qwen- und Code-Mapping-Aufrufe, technische Fehler sowie Erkennung
und gruenes QualityGate je Ereignis.

## Sichtbarkeitsprüfung

Der ältere Drei-Tasten-Prüfplatz bleibt unverändert verfügbar:

```powershell
.\tools\EvalVisibilityReview\start_visibility_review.ps1
```

## Blinder BCC-Release-Review

Der BCC-Pruefplatz zeigt das Bild, den festen Pruefcode `BCC — Bogen` und die
drei menschlichen Entscheidungen `positive`, `negative` und `exclude`.
Bildbezogener XTF-Untercode, Vorauswahl und Modellvorhersagen bleiben verborgen.
Die Review-Datei liegt ausserhalb des
eingefrorenen Holdouts und wird atomar an Holdout-ID, Manifest-SHA-256,
Kandidaten-SHA-256 und Reviewer gebunden. Ein prozessweiter Datei-Lock plus
Versionspruefung verhindert unbemerktes Ueberschreiben durch parallele
Pruefplaetze.

Start:

```powershell
python .\tools\EvalVisibilityReview\bcc_release_holdout_review_server.py `
  --holdout C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921 `
  --output C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json `
  --reviewer Besitzer
```

Nur die gebundene Datei vorbereiten:

```powershell
python .\tools\EvalVisibilityReview\bcc_release_holdout_review_server.py `
  --holdout C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921 `
  --output C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json `
  --reviewer Besitzer `
  --prepare-only
```

Der Server bindet ausschliesslich an `127.0.0.1`. Verknuepfte Holdout-,
Bild- oder Ausgabeordner werden abgelehnt.

Status danach pruefen:

```powershell
python .\training\scripts\bcc_release_holdout.py status `
  --knowledge-root C:\KI_BRAIN `
  --holdout C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921 `
  --review C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json
```

Der reale V1-Stand ist abgeschlossen: 60 von 60 Bildern sind beurteilt,
davon 29 positiv und 31 negativ. Der dynamische Status ist
`ready_for_binary_evaluation`.

Der feste Vierervergleich laeuft anschliessend mit der Sidecar-Umgebung:

```powershell
.\sidecar\.venv\Scripts\python.exe `
  .\training\scripts\evaluate_bcc_release_holdout.py `
  --knowledge-root C:\KI_BRAIN `
  --holdout C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921 `
  --review C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json
```

Stand 28. Juli 2026 ist der Vergleich vollstaendig, aber nicht
release-qualifiziert. Beide noch relevanten Kandidaten erzeugen auf den 31
Negativbildern zu viele Fehlalarme. Details stehen unter
`docs/quality/BCC-RELEASE-HOLDOUT.md`.

## Blinde BCC-Hard-Negative-Pruefung

`training/scripts/bcc_hard_negative_review.py` sucht frische Fehlalarme der
angegebenen BCC-Kandidaten. Holdout-Bilder, bekannte Bildhashes sowie gleiche
oder umgedrehte Haltungen werden gesperrt. Die Queue enthaelt hoechstens ein
Vollbild je physischer Haltung und ist an die aktive 15er-Klassenkarte gebunden.
Modellwerte und XTF-Hinweise bleiben im Browser unsichtbar.

Abgeschlossene Pruefliste:

```text
C:\KI_BRAIN\training\hard_negative_review\queues\bcc_hn_d37e1e0e481c
```

Start:

```powershell
python .\tools\EvalVisibilityReview\bcc_hard_negative_review_server.py `
  --queue C:\KI_BRAIN\training\hard_negative_review\queues\bcc_hn_d37e1e0e481c `
  --output C:\KI_BRAIN\training\hard_negative_review\reviews\bcc_hn_d37e1e0e481c_review.json `
  --reviewer Besitzer
```

Nur `1 · Sauberer Hintergrund` darf spaeter als Trainingsnegativ verwendet
werden. Diese Wahl bedeutet: Auf dem gesamten Bild ist keine der 15 gebundenen
Detect-Klassen sichtbar. `2` markiert eine sichtbare trainierte Klasse, `3`
schliesst ein unklares Bild aus. Der Review ist mit 14/14 Bildern abgeschlossen:
10 wurden als `all_classes_clear` bestaetigt, 4 wegen einer sichtbaren Klasse
ausgeschlossen, 0 als unklar markiert.

Der Publisher prueft Queue, Review, Klassenkarte, Auswahlmodelle, Registry- und
Eval-Schutz erneut. Ohne `--execute` schreibt er nichts:

```powershell
python .\training\scripts\bcc_hard_negative_review.py publish `
  --knowledge-root C:\KI_BRAIN `
  --queue C:\KI_BRAIN\training\hard_negative_review\queues\bcc_hn_d37e1e0e481c `
  --review C:\KI_BRAIN\training\hard_negative_review\reviews\bcc_hn_d37e1e0e481c_review.json
```

Mit `--execute` wurde daraus der eingefrorene Satz
`C:\KI_BRAIN\training\negatives\sets\bcc_hn_54f6608b975a`: 8 Train- und
2 Validierungsbilder aus 10 getrennten Haltungen. `_manifest.json` bindet jedes
Bild, den vollstaendigen Review, Queue-Manifest, Kandidatenliste und class_map v3
per SHA-256. Vorhandene Saetze werden nie ueberschrieben.

## Detect-Gold-Fehlfaelle pruefen

Dieser Pruefplatz ist bewusst vom Training Studio getrennt. Er zeigt die
Gold-Boxen gruen beziehungsweise gelb und die KI-Boxen blau beziehungsweise rot.
Er veraendert weder Goldbilder, Trainingssamples, KB, Registry noch Modell.

Der aktuelle korrigierte Lauf wurde als Queue
`detect_gold_failure_a46a82535c82` eingefroren. Sie enthaelt 80 Prueffaelle auf
67 Bildern:

- 56 Gold-Situationen wurden verpasst.
- 8 Boxen treffen die Geometrie, aber die KI-Klasse ist falsch.
- 16 KI-Boxen besitzen keine geometrisch zugeordnete Gold-Box.

Start mit automatischem Browseraufruf:

```powershell
.\tools\EvalVisibilityReview\start_detect_gold_error_review.ps1
```

Alternativ in Klartext:

```powershell
.\sidecar\.venv\Scripts\python.exe `
  .\tools\EvalVisibilityReview\detect_gold_error_review_server.py `
  --knowledge-root C:\KI_BRAIN `
  --queue C:\KI_BRAIN\eval_review\detect_gold_failure_review\queues\detect_gold_failure_a46a82535c82 `
  --output C:\KI_BRAIN\eval_review\detect_gold_failure_review\reviews\detect_gold_failure_a46a82535c82_review.json `
  --reviewer Besitzer `
  --open-browser
```

Die drei Entscheidungen bedeuten:

- `1 · Gold korrekt – KI-Fehler bestaetigt`: Der Fall darf in die aggregierte
  Sammelplanung einfliessen, das Holdout-Bild selbst aber niemals ins Training.
- `2 · Gold oder Box fraglich`: Der bestehende Goldfall braucht eine getrennte
  fachliche Nachpruefung.
- `3 · Unklar – ausschliessen`: Der Fall wird nicht fuer die Sammelplanung benutzt.

Der Zwischenstand wird nach jeder Entscheidung atomar gespeichert. Zwei offene
Browser-Tabs koennen sich dank Revision nicht still ueberschreiben. Eine
vollstaendige Review kann spaeter nur aggregierte Klassenbedarfe liefern. Sobald
diese Erkenntnisse zur Modellentwicklung verwendet werden, ist derselbe Holdout
keine unabhaengige Release-Abnahme mehr; dafuer ist ein neuer, unberuehrter
Holdout erforderlich.

Nach einer vollstaendigen Review zuerst schreibfrei pruefen:

```powershell
.\sidecar\.venv\Scripts\python.exe `
  .\training\scripts\publish_detect_gold_collection_plan.py `
  --knowledge-root C:\KI_BRAIN `
  --queue C:\KI_BRAIN\eval_review\detect_gold_failure_review\queues\detect_gold_failure_a46a82535c82 `
  --review C:\KI_BRAIN\eval_review\detect_gold_failure_review\reviews\detect_gold_failure_a46a82535c82_review.json `
  --reviewer Besitzer
```

Erst `--execute` schreibt den aggregierten Plan. Darin stehen nur Klassen und
Anzahlen. Bildpfade, Bildhashes, Sample-/Prediction-/Fall-IDs und Kommentare
werden bewusst nicht uebernommen. Eine bestaetigte falsche Klasse erscheint als
Positivbedarf der Sollklasse und zusaetzlich als Soll-zu-Vorhersage-Verwechslung.
Fuer die abgeschlossene Queue ist ausschliesslich
`detect_gold_collection_874ec160e346.json` gueltig. Der fruehere Plan
`detect_gold_collection_44a08fe9895e.json` besitzt keine Verwechslungsliste und
darf nicht verwendet werden.
