# Training, Goldbestand, Modelle und Messungen

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Geplant / nicht implementiert (nicht als Ist-Zustand behandeln)
- (Fortsetzung aus «XTF-Aenderungslieferung und Bauwerksarten (2026-09-07)»)
- Plan-gesteuerter YOLO-Export (AP 0.3)
- (Fortsetzung aus «KI-Vorschlaege im Codiermodus (seit 2026-09-05)»)
- SAM-Review im Training Center
- Aktive Few-Shot-Wege
- Schutz persistenter KI-Dateien
- Eval-Schutz: eine Regel an allen Lesewegen (2026-10-02)
- Ereignisbasierte Eval-Messung (AP 0.4a, technische Grundlage)
- Training Center: Verteilung und Scan (03.10.2026, Deepscan R6/R8c)
- Training Center: Aufteilung des Importdienstes (03.10.2026, Paket A)

## Geplant / nicht implementiert (nicht als Ist-Zustand behandeln)
- `ByteTrack` / `OC-SORT`: kein Tracking im aktuellen HEAD.
- `DetectionAggregator` / meterbasierter Merge-Radius / echtes Multi-Object-Tracking: nicht im aktuellen HEAD. Temporal Voting existiert als `TemporalCodeVotingService`, kein separater Aggregator.
- `InferenceOrchestratorService`: keine C#-Klasse im aktuellen HEAD; GPU-Slots liegen im Sidecar.
- Einen produktiven `KbDeduplicationService` gibt es aktuell nicht. Similarity-Checks im
  Trainings-/Review-Kontext nicht mit dem Retrieval-Ranking verwechseln.
- Automatische 8B->32B-Laufzeit-Eskalation: nicht als implementiert annehmen.
- Das aktive Detect-Altmodell (yolo26m, 2026-04-11) ist seit 2026-07-25 als NICHT
  qualifiziert markiert (`sidecar/models/model_qualification.json`, BBox-Kollaps,
  alter Trainingsdatensatz fehlt). `/health` meldet den Sidecar als `degraded` samt
  `detector_qualification`. Nur ein ausdrueckliches `qualified=true` gibt das
  Standardmodell frei. Bei false, fehlendem Feld oder Lesefehler sperrt das Training
  Studio den Fototest; Standard-Endpunkt und Warmup laden/verwenden YOLO nicht.
  Die Freigabedatei bindet PT, TensorRT-Engine und ONNX jeweils an Dateiname und
  SHA-256; Abweichungen sperren fail-closed. Gewichte bleiben unveraendert erhalten;
  das getrennte BCC-Testmodell ist davon unberuehrt.
- `training/scripts/model_collapse_check.py` ist das schreibfreie Kollaps-Pruefwerkzeug:
  Box-Statistik (Paar-IoU, Streuung), IoU gegen Gold-Boxen, Aktivierungen auf dem
  Negativ-Pool, optional mAP via `--dataset`. Ein echter Geometrie-Kollaps ergibt
  `FAIL` (Exit 1); Inferenzfehler, zu wenig Bilder/Treffer oder unter 20 %
  Detektionsrate ergeben `INCONCLUSIVE` (Exit 2), niemals einen falschen PASS.
  `PASS` heisst nur „kein BBox-Kollaps", keine Qualitaetsfreigabe. Pruefbestand unabhaengig
  (`--images-dir`, Default eval_set/images), einheitliche Aufloesung (`--imgsz` 1280),
  Bericht unter `<KnowledgeRoot>/training/reports`. Der Altmodell-Kollaps ist belegt;
  ein Kandidat bleibt ohne den ganzen Release-Weg immer `not_deployed`.
- Batch-Video und Player-Einzelframe verwenden YOLO nur bei ausdruecklichem
  `qualified=true`. Bei false, fehlendem Feld oder Health-Lesefehler wird YOLO weder
  als Frame-Filter noch als Confidence-Beweis verwendet. DINO/SAM laufen ohne
  YOLO-Gate weiter; Health-Ampel und Ergebnis bleiben `Degraded`/orange und verlangen
  eine manuelle Pruefung. Der Ollama-only-Pfad traegt die Kennzeichnung nicht.
- `training/scripts/gold_stock_audit.py` prueft den Goldbestand schreibfrei
  (persoenliche Freigabe, lesbares Bild, randgueltige Box, echte Maskenpixel in der
  Hand-Box, Katalogcode, Bildhash und komplette Eval-Haltung). Haltungsnummern werden
  normalisiert; identische Bildbytes verbinden betroffene Haltungen zu einer
  gemeinsamen Split-Gruppe. Ein Pilot braucht >= 30 Samples sowie Train und Val/Test.
  Platzhalter-Beschreibungen sind fuer das reine BBox-Training zulaessig und werden
  nur als `kb_text_offen` markiert — KB-Index und Qwen-Retrieval sperren sie.
  Der aktuelle, an Register und Exportdatensatz gebundene Bericht
  `gold_stock_audit_20260803_191255_470.json` hat SHA-256
  `5d036fd74dbdc6e80dae1ca2600b648fc99073f9b8a0157bee5da1a6027a0987`.
  Er prueft 1391 Eintraege: 14 Drafts werden uebersprungen, 24 Kandidaten werden
  verworfen und 1353 sind verwendbar. Der Haltungssplit umfasst 961 Train-,
  264 Validation- und 128 eingefrorene Testinstanzen. Gebunden ist der dabei
  gepruefte `training_samples.json`-Snapshot mit SHA-256
  `fd5340ce35d5b317273e9d34e340d70e319448c78c23d640ec682b94fb9c6a1b`.
  Der Audit vom 2026-08-02 bleibt der unveraenderte Beleg des weiterhin
  `not_deployed` stehenden vorigen Kandidaten.
- `training/scripts/repair_gold_holding_ids.py` repariert persoenlich bestaetigte
  `foto_*`-Goldsamples nur ueber einen eindeutigen bytegleichen SHA-256-Treffer in
  einem Quellenordner. Standard ist ein schreibfreier Prueflauf. `--execute`
  verlangt eine ruhige App/SQLite-DB, sichert JSON und SQLite konsistent und
  aktualisiert Sample, Signatur, Notiz, Teacher-Haltung und `Samples.CaseId`
  gemeinsam; Kundenbilder werden nie veraendert.
- `training/scripts/prepare_detect_gold.py` baut daraus fail-closed das getrennte
  Mehrklassen-Register `DETECT_ALL`. Es verlangt den expliziten Audit, die daran
  gebundene persoenliche Codefreigabe, unveraenderte Bild-/Sample-Hashes und nur
  streng reviewte `all_classes_clear`-Negative. Das aktuelle Register enthaelt
  894 Goldinstanzen (710 Train, 184 Validation) und 9 strikte Negative (7/2).
  Der gebundene Exportplan
  `ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2`
  fuehrt gleiche Bildbytes zusammen und enthaelt deshalb 852 Bilder
  (686 Train, 166 Validation) mit 894 Boxen. Belegt sind 13 der 15 festen Klassen.
  Register, Beleg und Archive sind gegen Links/Junctions
  geschuetzt; ein Transaktionsmarker setzt einen abgebrochenen Zwei-Datei-Wechsel
  bytegenau zurueck oder erkennt einen bereits vollstaendigen Commit. Negative
  Bilder werden zusaetzlich gegen alle Auditrollen geprueft: derselbe Bildhash ist
  immer gesperrt, Testhaltungen samt Gegenrichtung sind gesperrt und abweichende
  Train-/Validation-Rollen sind ebenfalls ein harter Fehler. Bei einer Erneuerung
  duerfen neue eingefrorene Eval-Sets nur monoton ergaenzt werden; jedes bereits im
  Register gebundene Schutzmanifest muss unveraendert vorhanden bleiben.
- `training/scripts/derive_negative_set_for_gold_audit.py` leitet dafuer einen
  neuen unveraenderlichen Negativsatz ab, ohne Quelle oder Review umzuschreiben.
  Es entfernt nur Audit-Testhaltungen und Splitkonflikte; bytegleiche
  Gold-/Negativbilder bleiben ein harter Fehler. Der aktuelle Satz
  `bcc_hn_c25fd2f9d33f` besitzt 9 Bilder (7 Train, 2 Validation), Set-ID
  `c25fd2f9d33f09454e03c2e0ed2e25d5fa8faafcd2b50c9af68c03288fbbe0f2`
  und Manifest-SHA-256
  `518a341419b285da88ce674accfe7b0b41330f8cae736ef87a95ea9a48221772`.
- `training/scripts/train_detect_gold.py` prueft danach Plan, Exportbeleg,
  Klassenkarte, alle Datei-Hashes und jedes YOLO-Label erneut. Es trainiert nur
  einen getrennten Kandidaten mit Status `not_deployed`; produktive Gewichte oder
  Modellzeiger werden nie veraendert. Ein laufender Sidecar oder weniger als
  28000 MB freier VRAM sperrt den Start. Der neue Kandidat
  `detect_gold_9eb020e30322` hat 40/40 Epochen beendet und bleibt
  `not_deployed`. Die interne Validation ergibt P 0,3917, R 0,3129,
  mAP50 0,3026 und mAP50-95 0,1726. Der Gewichtshash ist
  `fdf30f77b6aa6271014d130248fde99089854bfc0e58b44d75d462b3b9172ebf`,
  der Kandidatenmanifest-Hash
  `dd40258fd531198be7a781f265cad6e6f74b8d6704ec762b80b8012a140c392d`.
  Der vorige Kandidat `detect_gold_ffbb8612fe50`
  beendete 40/40 Epochen mit P 0,4156, R 0,2575, mAP50 0,2417 und
  mAP50-95 0,1286. Auch diese internen Werte sind keine Produktfreigabe.
- Der rohe Detect-Testanteil des an `detect_gold_ffbb8612fe50` gebundenen Audits
  umfasste 83 Instanzen auf 79 Bildern. Die Haltung `77457-77453`
  ueberschnitt sich jedoch mit einem
  Trainingsnegativ. Nach Ausschluss dieser ganzen Haltung bleiben 81 Instanzen auf
  77 Bildern aus 30 physischen Haltungen als sicherer positiver Testbestand.
- `training/scripts/detect_gold_holdout_provenance.py`,
  `detect_gold_holdout_scoring.py` und `evaluate_detect_gold_holdout.py` pruefen
  Kandidat, Gewicht, Basisgewicht, Dataset, DETECT_ALL-Beleg, Klassenkarte,
  Migration, beide Gold-Audits, aktuelle Samples sowie Bild-/Sample-/Haltungs-
  Ueberschneidungen erneut. Das feste Protokoll ist `conf=0,25`, `imgsz=1280` und
  `IoU=0,5`; zuerst entsteht ein labelblinder, SHA-gebundener Vorhersagebeleg.
  Technische Fehler werden nie als Negativtreffer gewertet. Mehrfachboxen werden
  mit maximaler Trefferzahl und danach maximalem Gesamt-IoU zugeordnet.
- Der korrigierte GPU-Lauf vom 2026-08-02 hat Bericht-SHA-256
  `9ce6aaad85317061953796085ff7daf921b554295f2bad21e904cc5dc78789f6`
  und Vorhersagebeleg-SHA-256
  `87002b0aa6cca5d6a5ec33ef05d5662ff80be2f71458ddaba3374916633aa450`.
  Ergebnis: TP 17, FP 24, FN 64, Precision 41,5 %, Recall 21,0 %, F1 27,9 %.
  `BCC_bogen` traf 14/16 (Recall 87,5 %), `BCA_anschluss` 3/17; die elf
  weiteren gemessenen Klassen hatten keinen exakten Treffer. Der Status ist
  `positive_holdout_only_not_release_qualified`: Es fehlen insbesondere frische,
  saubere Negativbilder, und das Modell bleibt `not_deployed`. Der fruehere Lauf
  `..._20260802_120445_930796.json` ist wegen einer dabei entdeckten falschen
  RGB/BGR-Uebergabe aufgehoben und darf nicht verwendet werden.
- Der allgemeine Detect-Release-Holdout wird ohne Modellvorhersagen aus frischen
  PDF-/Video-Haltungen vorbereitet. `prepare_detect_release_pdf_extraction.py`
  plant hoechstens ein PDF-/Video-Paar je physischer Haltung. Der getrennte
  `tools/DetectReleaseHoldoutPdfExtractor` uebernimmt ueber den bestehenden
  `TrainingPdfReviewImportService` nur eindeutig zugeordnete Operateurfotos und
  optional einen deterministischen Video-Hintergrundframe je PDF. Kundenoriginale
  bleiben unveraendert; der Extraktionsbeleg sperrt Training und Gold.
- `training/scripts/prepare_detect_release_holdout.py` prueft Kandidat, Gewicht,
  Basismodell, class_map v3, VSA-Hash, Extraktionsbeleg sowie bekannte Bildhashes
  und beide Richtungen jeder Haltung erneut. Erst `--execute` veroeffentlicht
  atomar einen eingefrorenen Ordner `detect_release_holdout_<sha>`. Dieser Bestand
  darf nie fuer Training, Gold, Few-Shot oder Kandidatenauswahl verwendet werden.
- `tools/EvalVisibilityReview/detect_release_holdout_review_server.py` zeigt keine
  Modellvorhersagen; PDF-Angaben sind sichtbar als Operateur-Referenz bezeichnet.
  Bei `positive` markiert der Mensch alle sichtbaren Objekte der 15 Klassen mit
  einer oder mehreren Boxen. `negative` gilt nur, wenn keine dieser Klassen
  sichtbar ist; unklare Bilder erhalten `exclude`. Die getrennte Review ist an
  Holdout, Kandidat, Gewicht, Klassenkarte, VSA-Manifest und Bildbytes gebunden.
- `training/scripts/detect_release_holdout_status.py` prueft Holdout und Review
  schreibfrei. `ready_for_detect_evaluation` verlangt eine vollstaendige Review,
  mindestens 20 Instanzen je Klasse, 75 echte Negativbilder und 30 negative
  physische Haltungen; diese Grenzen duerfen nur erhoeht werden. Der Status startet
  kein Modell und ist keine Freigabe. Der aktuelle Review ist mit 400/400 Bildern
  abgeschlossen: 241 positiv, 74 negativ und 85 ausgeschlossen. Wegen fehlender
  Klassenabdeckung und eines fehlenden Negativbilds bleibt er
  `coverage_incomplete`.
- `training/scripts/evaluate_detect_release_holdout.py` fuehrt deshalb nur eine
  klar bezeichnete Mehrklassen-Diagnose aus. Mit festem `conf=0,25`,
  `imgsz=1280` und `IoU=0,5` inferiert es bei ausgeschaltetem Sidecar zuerst alle
  400 Bilder ueber die private Kandidatenkopie und den geprueften RGB-zu-BGR-Weg.
  Der labelblinde SHA-Beleg wird geschrieben und erneut validiert, bevor die
  Review geladen wird. Nur die 315 positiven und negativen Bilder werden
  bewertet; `exclude` wird ignoriert. Technische Fehler auf gewerteten Bildern
  brechen die Diagnose ab und gelten nie als Negativtreffer. Das Werkzeug
  trainiert oder aktiviert nichts und kann keine Modellfreigabe erteilen.
- Der erste GPU-Lauf vom 2026-08-03 hatte 400/400 technisch fehlerfreie
  Vorhersagen. Auf 350 Soll-Boxen ergaben sich TP 36, FP 59 und FN 314
  (Precision 37,9 %, Recall 10,3 %, F1 16,2 %); 9/74 echte Negativbilder hatten
  mindestens einen Fehlalarm. `BCC_bogen` traf 27/37, `BCA_anschluss` 8/39 und
  `BAF_oberflaeche` 1/89; die elf weiteren gemessenen Klassen hatten null exakte
  Treffer. Bericht-SHA-256:
  `64bd6ae370bc1a0bc7320aca5a0921a89cfa467fc9b7ff1c5e926780dc00dcbc`,
  Ledger-SHA-256:
  `a771cbd7fa1a959b49ecf41621df700259471494b7e110d73c7b96eb919adbf2`.
  Details stehen in `docs/quality/DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md`.
- `training/scripts/detect_gold_error_review.py` erzeugt aus genau diesem
  korrigierten Bericht und seinem labelblinden Vorhersagebeleg eine eingefrorene,
  rein diagnostische Fehlfall-Queue. Sie enthaelt jede nicht exakt getroffene
  Goldinstanz sowie jede geometrisch unzugeordnete Vorhersage genau einmal,
  kopiert keine Bilder und setzt alle Trainings-/Exportrechte ausdruecklich auf
  `false`. Der aktuelle gueltige Stand
  `detect_gold_failure_a46a82535c82` umfasst 80 Faelle auf 67 Bildern:
  56 verpasst, 8 falsche Klasse und 16 zusaetzliche KI-Boxen.
- Der lokale Pruefplatz
  `tools/EvalVisibilityReview/detect_gold_error_review_server.py` zeigt Gold- und
  KI-Boxen mit Klasse und erlaubt nur `confirmed_model_error`, `gold_suspect`
  oder `exclude_uncertain`. Review und Queue liegen getrennt unter
  `<KnowledgeRoot>/eval_review/detect_gold_failure_review`. Bericht, Ledger,
  Kandidatenmanifest, Gewicht, Gold-Audit, Trainingssamples und Klassenkarte
  werden per SHA-256 gebunden und vor jeder Entscheidung erneut geprueft.
  Browser-Revision und Dateisperre verhindern stilles Ueberschreiben durch zwei
  Tabs oder Prozesse. Dieser Weg mutiert weder Gold, KB, Trainingsdaten,
  Registry noch Modell. Werden seine Erkenntnisse zur Modellentwicklung genutzt,
  darf derselbe Holdout danach nicht erneut als unabhaengige Release-Abnahme gelten.
- `training/scripts/publish_detect_gold_collection_plan.py` akzeptiert nur eine
  vollstaendige, zur Queue und zum Reviewer passende Review. Der Standardlauf ist
  schreibfrei; `--execute` publiziert atomar und idempotent ausschliesslich einen
  aggregierten Klassen-Sammelplan ohne Bildpfade, Bildhashes, Sample-, Prediction-,
  Fall-IDs oder Kommentare. Gold-fragliche Faelle bleiben getrennte
  Annotation-Audit-Ziele; ausgeschlossene Faelle erzeugen kein Sammelziel. Eine
  bestaetigte falsche Klasse zaehlt zugleich als Positivbedarf der Sollklasse und
  als konkrete Soll-zu-Vorhersage-Verwechslung. Die Review der Queue
  `detect_gold_failure_a46a82535c82` ist abgeschlossen: 80/80 Entscheidungen,
  davon 75 bestaetigte Modellfehler, 0 Gold-Verdachtsfaelle und 5 Ausschluesse.
  Der gueltige Sammelplan ist `detect_gold_collection_874ec160e346`: 60 positive
  Fehlerhinweise, 15 Fehlalarm-Hinweise und 6 Verwechslungen in 4 Klassenpaaren.
  Der fruehere Plan `detect_gold_collection_44a08fe9895e` ist wegen der damals
  fehlenden Verwechslungsliste aufgehoben und darf nicht verwendet werden.
- Die Lernkurven- und Klassenbreitenlaeufe vom 2026-08-30 liegen ausschliesslich
  unter `C:\KI_BRAIN\training\diagnostics` und
  `C:\KI_BRAIN\training\cls_runs`. Sie sind Diagnose ohne Kandidatenmanifest
  und duerfen nie aktiviert werden. Die Lernkurve spricht auf diesem Datensatz
  fuer weiteren Nutzen zusaetzlicher Goldboxen; sie beweist weder Materialmangel
  als alleinigen Engpass noch die Uebertragbarkeit auf die produktive Linie. Die
  Klassenverengung zeigte keinen belegten Vorteil, veraenderte aber zugleich den
  Hintergrunddruck. Die einheitliche Nachmessung mit `half=False, batch=4` ergab
  fuer `BCC_bogen` AP50 0,827 / 0,815 / 0,845 bei 15 / 5 / 2 Klassen. Gegenueber
  den alten gemischten Pruefeinstellungen aenderte sich AP50 hoechstens um 0,004;
  diese erklaeren die Stufenunterschiede daher nicht allein. Die Diagnose
  trainierte mit `fliplr=0.5`, `hsv_h=0.015`,
  `hsv_s=0.7`, `hsv_v=0.4`, `mosaic=1.0`; der produktive Trainer verwendet
  `fliplr=0.0`, `hsv_h=0.01`, `hsv_s=0.3`, `hsv_v=0.3`. Zahlen, Belege und
  Grenzen stehen in
  `docs/quality/DETECT-LERNKURVE-UND-KLASSENBREITE-2026-08-30.md`.
  Das vom Referenzlauf abgelegte `yolo26n.pt` wurde am 2026-09-02 in
  `C:\KI_BRAIN\training\diagnostics\quarantine` verschoben; der Gold-Validator
  akzeptiert den Plan-Datensatz wieder mit 852 Bildern und 894 Instanzen.
- `yolo_wrapper._pil_rgb_to_ultralytics_bgr` wandelt dekodierte PIL-RGB-Bilder vor
  jeder Ultralytics-NumPy-Inferenz explizit in zusammenhaengendes BGR um. Detect,
  Legacy-Classification, beide Holdout-Auswerter und seit 2026-08-09 auch der
  BCC-Test-Endpunkt verwenden denselben Helfer; Rot und Blau duerfen nicht erneut
  still vertauscht werden. Der BCC-Endpunkt hatte die Umkehrung verpasst — erst
  die Copilot-Abnahme (C# gegen Prototyp) machte das sichtbar: 7 statt 4 Stellen,
  systematisch verschobene Konfidenzen. Mit dem Helfer sind die Einzelbildfolgen
  beider Wege exakt gleich (226 Treffer, null Abweichungen).
- SAM-Video-Regel (Goldgewinnung): SAM 2.1 kann Masken durch Videos propagieren,
  darf aber nur als Pruefwerkzeug fuer den Menschen dienen, nicht als automatische
  Goldfabrik. Propagierte Nachbarframes sind stark voneinander abhaengige
  Vorschlaege; sie werden einzeln ausgewaehlt und menschlich bestaetigt, bevor sie
  Gold werden. Kein automatischer Gold-Export aus Video-Propagation.
- Negativ-/Hintergrundbilder sind seit 2026-07-25 im gemeinsamen Detect-Plan
  angeschlossen. Alte reine Registrys duerfen weiter den flachen Pool
  `<KnowledgeRoot>/training/negatives/bcc_pilot` mit Pfad + SHA-256 lesen. Neue
  Trainingslaeufe verwenden stattdessen nur explizite `--negative-set`-Ordner unter
  `training/negatives/sets`. Deren Manifest bindet Bild, echte Haltung, festen
  Train-/Validation-Split, All-Class-Review, Queue, Kandidatenliste und class_map v3.
  Gold-Audit, `prepare_bcc_pilot.py` und C# pruefen diese Kette erneut; Legacy und
  strikte Saetze duerfen in einem neuen Register nicht gemischt werden. Der aktuelle
  strikte Lauf verwendet deshalb nur die 10 reviewten Bilder aus
  `bcc_hn_54f6608b975a`; die 14 Altnegative ohne All-Class-Beleg bleiben draussen.
  `prepare_bcc_pilot.py` verlangt einen expliziten aktuellen `--gold-audit`,
  uebernimmt nur dessen Rollen `train` und `val` und schliesst `test` strikt aus.
  Ein bestehendes Register wird nur mit `--execute --renew-existing` nach erneuter
  Hash-Pruefung ersetzt; der Altstand wird bytegenau unter
  `training/pilots/BCC/registry_history/<sha256>.json` archiviert. Der Plan prueft
  Hashes, echte Haltungen, Gegenrichtungen, Split und Eval-Schutz auch fuer Negative
  und schreibt leere Labeldateien (`IsNegative` nur bei `true` serialisiert).
  `train_bcc_pilot.py` akzeptiert leere Labeldateien als Negative (Positive ohne Labeldatei
  stoppen weiter), trainiert mit `flipud=0.0, fliplr=0.0` (Uhrlage!) und leichter
  HSV-Augmentierung (`hsv_h=0.01, hsv_s=0.3, hsv_v=0.3`) sowie `--patience` Default 10.
  Der strikte Lauf vom 2026-07-28 exportierte Plan
  `f23a95b149addf9d24365834b563b7784f76132190d9e4e60f4c61e84a652bc9`
  mit 57 BCC-Positiven und 10 Negativen (48 Train, 19 Validation). Der Kandidat
  `bcc_bogen_f23a95b149ad_hn10_strict` stoppte nach 33/40 Epochen und bleibt
  `not_deployed`; Gewicht-SHA-256:
  `89331f637fe59cd2c321c3330733cc0278c57b4bd3a5c512662c12fef4a1ee78`.
  Seine interne, fuer Early Stopping verwendete Validation ist noch kein
  Release-Beweis (P=0,5371, R=0,4706, mAP50=0,4829, mAP50-95=0,1613).
  Bei `conf=0,25` aktiviert er auf beiden strikten Validation-Negativen und auf
  7/14 nicht mittrainierten Altnegativen. Deshalb nicht aktivieren: zuerst mehr
  unterschiedliche BCC-Boxen und streng reviewte Hard-Negatives sammeln, danach
  einen frischen, zuvor unberuehrten Release-Holdout pruefen.
- Aufbau der Negativsatz-Pruefung (seit 2026-09-30, AP10): Die reinen
  Pruefschritte fuer `bcc_hn_*` und `proto_hn_*` liegen in
  `training/scripts/negativsatz_pruefung.py` (Satzkopf, Belegbindung, Queue,
  Kandidatenliste, Review, Bildbelege, Proto-Ausnahmen, Split, Ergebnisformat)
  und lesen keine Dateien. `gold_stock_audit.py` behaelt die Dateigrenzen
  (Satzordner, Datei-Hashes, aktive Klassenkarte, Bilddateien, Eval-Schutz,
  Gold-Split) und gibt einen Satz erst nach der Unveraendert-Pruefung frei.
  Alle Unterschiede zwischen BCC und Proto (Zweck, Felder, Meldungstexte)
  stehen in `BCC_VERTRAG`/`PROTO_VERTRAG`; eine gemeinsame Funktion nur bei
  gleicher Regel. `tests/test_negativsatz_vertrag.py` haelt jede Ablehnung mit
  ihrer Meldung fest; eine neue Regel bekommt dort zuerst einen Fall.
- Klassenkarte v3 mit festen Bytes (2026-10-01): Negativsaetze, Register und
  Berichte unter `C:\KI_BRAIN\training` binden `detect_class_map_v3.json` ueber
  die SHA-256 der LF-Fassung `58f1160f…` (116 Dateien, keine mit CRLF). Ein
  Windows-Checkout mit CRLF ergab `89bf03e3…`; Python (`gold_stock_audit`) und
  C# (`TrainingNegativeClassMapBinding`) lehnten darauf ALLE Negativsaetze mit
  «passt nicht zur aktiven Detect-Klassenkarte» ab. `.gitattributes` haelt die
  Datei ohne Zeilenende-Umwandlung; die drei echten Saetze werden wieder
  angenommen (10 + 9 + 286 Bilder), ohne neue Review. Nicht umstellen: Die
  Migrationsdatei v3 und das VSA-Manifest sind mit ihrer CRLF-Fassung gebunden
  (`registry_setup_v1.json`). Waechter: `TrainingYoloClassMapHashBindungTests`.
  Alte Arbeitskopien: Datei loeschen und `git checkout -- <datei>`.
- Proto-Satz ohne Ueberschneidung (Entscheid 2026-09-30): Neben
  `gesehen ∪ ausgeschlossen == akzeptiert` muss die Schnittmenge leer sein. Ein
  Satzbild, das zugleich in `excluded_eval_protected` oder
  `excluded_not_normalizable` steht, lehnt `pruefe_proto_keine_ueberschneidung`
  mit Bild-ID und Haltung ab; die Pruefung laeuft immer, auch ohne Luecke im Satz.
  Der C#-Store verlangt Bilder == akzeptiert ohne Ausnahmelisten und lehnt den
  Fall schon ab (Test `ReadBundle_blockiert_proto_Satzbild_das_zugleich_eval_ausgeschlossen_ist`).
- Proto-Satz ohne Gold-Testhaltung (Entscheid 2026-09-30): Ein Negativbild, dessen
  physische Haltung (samt Gegenrichtung) die Gold-Rolle `test` hat, steht in keinem
  Split, auch nicht in `validation` (steuert Early Stopping). Quellen:
  `_gold_test_haltungen` = aktueller Gold-Split aus `training_samples.json` (wie die
  Gold-Ausrichtung) und Rolle `test` im juengsten Gold-Audit (Samples und Gruppen).
  Der Pruefer lehnt den Satz mit Bild-ID, Haltung und Split ab
  (`pruefe_proto_ohne_gold_testhaltung`); die fruehere Ausrichtung test -> validation
  ist damit gesperrt. Die Ableitung `proto_hard_negative_review.py` schliesst
  Audit-Testgruppen schon ueber den Eval-Schutz begruendet aus; eine Gold-Testhaltung
  ausserhalb dieses Schutzes stoppt sie vorab, weil ein begruendeter Ausschluss nicht
  moeglich ist. C# kennt den Gold-Split nicht und sperrt nur Ausrichtungen mit
  `gold_role: test`; die volle Pruefung leisten Python-Leser und `prepare_detect_gold`.

## (Fortsetzung aus «XTF-Aenderungslieferung und Bauwerksarten (2026-09-07)»)

Beim Teacher-Store ist die JSON-Karte verbindlich und `classes.txt` nur abgeleitet.
Scheitert das Schreiben der JSON-Karte, wird die vorherige `classes.txt`
wiederhergestellt oder eine neu angelegte Kopie entfernt.

Die versionierten Vorlagen liegen unter `training/class_maps/` und werden beim Build
nach `Data/Training/` kopiert. Die v2-Karte mit 14 Klassen und 124
Migrationszeilen bleibt eingefroren. Aktiv ist v3 mit 15 festen Klassen und 153
Migrationszeilen: 103 Teacher-Codes, 35 Legacy-Schluessel, 10 produktive Modellnamen
und 5 Annotation-Overrides. Davon sind 89 Zeilen `approved`, 64 bleiben `pending`.
`personal_gold_approval` bindet diese Entscheidungen an den Audit-SHA-256
`04f405acaa8b072b1dbd961b08d74a2baf0231b21613820933888aa966617da0`
und den gebundenen `training_samples.json`-SHA-256
`502f8d842b6b457403717a807aed6471ac503263a409caf8c9437844bd58583c`.
Die Freigabe umfasst 88 beobachtete Quellcodes; neu ist insbesondere
`BAFCZ -> BAF_oberflaeche`.
Unbekannte oder offene Klassen werden vor jeder Exportausgabe hart gestoppt; es
gibt keine stille neue ID und keinen automatischen SONST-Rueckfall.
Die Migrationsdatei prueft die Herkunfts- und persoenlichen Beleg-Hashes, die
deklarierte Zeilenzahl und die feste Aufloesungsreihenfolge. Nur der VSA-Hash wird
beim Lesen gegen die echte Datei neu berechnet; die historischen Herkunftshashes
bleiben Auditwerte der Erzeugung. `BBD_boden` wird im produktiven Befundweg ueber
`CodingFindingCodeResolver`/`VsaCodeResolver` zu `BBDZ`, nie zum nackten `BBD`.

## Plan-gesteuerter YOLO-Export (AP 0.3)

Der Datenfluss ist verbindlich:

```text
TrainingCenter
  -> duenne UI-Huelle ruft ITrainingYoloExportCoordinator
  -> freigegebenes export_registry_v1.json lesen
  -> einen aktuellen TrainingDataInventoryRuntimeSnapshot erzeugen
  -> aktive class_map v3 strikt lesen
  -> ITrainingExportPlanService erzeugt genau einen Plan
  -> ITrainingExportExecutionService nutzt Sidecar ODER lokalen Ausfuehrer
  -> ITrainingExportCompletionService markiert nur bestaetigte TrainingSamples
```

Wichtige Regeln:

- Fuer neues Training sind ausschliesslich persoenlich manuell codierte und
  bestaetigte `TrainingSample`-Eintraege zulaessig. `ConfirmedByUser` muss exakt
  mit `ApprovedBy` der Export-Registry uebereinstimmen; BBox und SAM-Segmentierung
  sind Pflicht. Teacher-, Auto-, Fremdbestaetigungen und unvollstaendige Handlabels
  bleiben im Inventar, werden aber nicht in train/val exportiert.
- `TrainingExportRegistryFileStore` liest
  `<KnowledgeRoot>\training\export_registry_v1.json` strikt. Status `candidate`,
  unbekannte Felder, fehlende Schutz-Sets oder abweichende Manifest-Hashes stoppen.
  Hauptablauf, Validierung und interne JSON-Dokumente liegen wartbar getrennt in
  `TrainingExportRegistryFileStore.cs`, `.Validation.cs` und
  `TrainingExportRegistryFileDocuments.cs`.
  Das optionale Feld `approved_sample_ids` begrenzt einen menschlich freigegebenen
  Pilot auf exakt diese TrainingSample-IDs. Ist es leer, bleibt das bisherige
  Verhalten mit allen geeigneten Goldsamples erhalten.
- Der Plan ist pfadfrei und enthaelt feste Klassen, Haltungs-Splits, Ausschluesse,
  Quell-Hashes und stabile `img_<sha256>.<endung>`-Namen. Gleiche Bild-SHAs werden
  einmal geschrieben; unterschiedliche Labels werden zusammengefuehrt.
  Beim Runden auf sechs YOLO-Nachkommastellen werden randbuendige BBox-Groessen
  erforderlichenfalls minimal nach innen begrenzt, damit eine vorher gueltige Box
  nicht durch reine Rundung ausserhalb des Bildes liegt.
- Sidecar und lokaler Ausfuehrer schreiben zuerst unter `.staging` und
  veroeffentlichen atomar nach `<KnowledgeRoot>\training\datasets\<plan_id>`.
  Bestehende unvollstaendige oder abweichende Ziele werden nie repariert oder ersetzt.
- Der Sidecar-v2-Vertrag bindet Klassen, Split, Dateiname, Labels, Klassenkarten-,
  VSA- und Registry-Hash an das C#-Manifest. `plan_sha256` muss `plan_id` entsprechen.
- Der KI-Start uebergibt `SEWER_SIDECAR_TRAINING_EXPORT_ROOT` aus demselben
  `KnowledgeRoot`. Eine abweichende Sidecar-Antwort stoppt vor der Abschlussmarkierung.
- Ein Release-Kandidat erhaelt absichtlich einen eigenen Plan mit Inventar-Run und
  Erzeugungszeit. HTTP-Wiederholungen desselben Plans sind idempotent; ein neuer
  Exportbefehl ist ein neuer Kandidat.
- Die gemeinsame Fixture unter `tests/Fixtures/TrainingExport/` fuehrt Train,
  Dev-Val und ein Multi-Label-Bild durch beide Ausfuehrer. Relative Pfade,
  SHA-256 und Bytes aller Ausgabedateien muessen identisch bleiben.
- `TrainingYoloExportWorkflow` enthaelt nur Busy-, Fortschritts- und Fehlermeldungen.
  Auswahl, Dateizugriff, Sidecar-Rueckfall und Abschluss gehoeren nicht in die UI.
- `TrainingYoloExportRuntime` in Infrastructure ist der gemeinsame Aufbaupunkt.
  WPF verwendet `CreateHybrid` (Sidecar mit lokalem Rueckfall), die StageA-CLI
  `CreateLocal`. Roots, Registry und Dataset-Ziel werden einmal gebunden und koennen
  pro Befehl nicht ausgetauscht werden. `TrainingYoloExportComposition` ist nur die
  duenne WPF-Huelle darum.
- Der Live-Inventar-Snapshot ist die einzige Sample-Wahrheit fuer Auswahl und Plan.
  Die sichtbare UI-Liste darf nur nach einem bestaetigten Export die drei abgeleiteten
  Felder `TrainingEligible`, `TrainingEligibilityReason` und `ExportedUtc` empfangen.
  Eligibility und Abschluss werden erst danach gemeinsam einmal gespeichert.
- `PlanOnly` durchlaeuft Registry, Inventar, Klassenkarte und Planer, schreibt aber
  weder `training_samples.json` noch Datensatzdateien und mutiert auch die UI-Liste nicht.
- Die reine Registrierungsliste liegt in `ServiceProviderRegistrationMap`. Sie darf
  keine Dienste erzeugen. `ServiceProvider.cs` enthaelt dadurch nur noch Aufbau und
  den abschliessenden Aufruf der Map.

Das aktive `DETECT_ALL`-Register ist fuer die 73 fachlich entschiedenen Teacher-Codes
freigegeben und nennt jede erlaubte Goldsample-ID einzeln. Nicht belegte oder weiter
offene Codes bleiben gesperrt. Diese Exportfreigabe ist keine Modellfreigabe: Der
fertig trainierte Mehrklassen-Kandidat `detect_gold_9eb020e30322` bleibt getrennt
und `not_deployed`; seine interne Validation ist keine Release-Freigabe. Der BCC-Bogen-Pilot mit
`BCC_bogen` und fester ID 14 bleibt als enger Altweg erhalten. Diese Sperren nie
automatisch umgehen.

`tools/StageAExporter` ist jetzt eine reine Kompatibilitaets-CLI vor derselben Runtime
und demselben Coordinator wie WPF. Sie besitzt keine eigene Klassen-, Split-, Label-
oder Dateilogik mehr. `--dry-run` ist ein echter `PlanOnly`-Lauf; `--val-ratio` und
`--allow-dummy-bbox` sind harte Fehler. Quelle und Ziel muessen den kanonischen Pfaden
`<KnowledgeRoot>\training_samples.json` und `<KnowledgeRoot>\training\datasets`
entsprechen. Das Tool ist Teil der vollstaendigen Solution, aber bewusst nicht des
entwicklungsnahen `AuswertungPro.Dev.slnf` ohne Hilfsprogramme.

Der fruehere `YoloDatasetExportService` ist entfernt. Er war nicht registriert und
duplizierte Klassenbildung, Bild-Split und Dateischreiben ohne den vollstaendigen
Eval-/Registerschutz. Keinen zweiten YOLO-Datensatzschreiber neben dem gemeinsamen
Coordinator und seinen beiden plan-gesteuerten Ausfuehrern einfuehren.

`training/scripts/prepare_bcc_pilot.py` erzeugt nach einer schreibfreien Vorpruefung
das enge BCC-Register und den Auditbeleg. `training/scripts/train_bcc_pilot.py`
akzeptiert danach nur einen vollstaendig gehashten Export unter
`<KnowledgeRoot>\training\datasets`, trainiert vom unveraenderten
`sidecar/models/yolo26m/yolo26m.pt` und schreibt ausschliesslich einen nicht
aktivierten Kandidaten unter `training\models\candidates`. Es startet nie bei
erreichbarem Sidecar oder weniger als 28000 MB freiem VRAM und ersetzt keine
produktiven Gewichte. Der kleine BCC-Pilot verwendet die auf dieser Hardware
gemessene Batch-Groesse 3 und standardmaessig `patience=10`; nur ein ausdrueckliches
`patience=0` erzwingt alle verlangten Epochen. Von Ultralytics erzeugte
`train.cache`/`val.cache` werden nach jedem Lauf entfernt, damit der
plan-gesteuerte Datensatz unveraendert bleibt.

Der nicht aktivierte BCC-Kandidat kann ausschliesslich im Training Studio als
reiner Fototest verwendet werden. `TrainingPreviewDetectionService` ruft dafuer
zuerst die pfadfreie, manifest- und hashgepruefte Liste ueber
`GET /detect/yolo/bcc-test/candidates` ab. Der Benutzer waehlt im bestehenden
Modellfeld genau einen Kandidaten. `POST /detect/yolo/bcc-test` erhaelt nur dessen
ID und erwartete SHA-256, niemals einen Modellpfad. Antwort-ID und -Hash muessen
exakt zur Auswahl passen; sonst werden alle Boxen verworfen. Zusaetzlich liest der
Endpunkt pro Anfrage den OSD-Meterstand desselben Bildes
(`sidecar/sidecar/osd_meter.py`, der validierte Ziffernleser; der Prototyp
`training/scripts/osd_meter_leser.py` delegiert dorthin) und liefert ihn als
additives `meter_value` (None = nicht lesbar, niemals 0,0). Das optionale
Request-Feld `meter_format` erzwingt das Zahlenlayout (`ein_dezimal` /
`vierziffern`, Default auto); die Lesung ist roh und zustandslos —
Sequenz-Plausibilitaet und Lueckenfuellen bleiben C#-Sache
(`MeterSequencePlausibility`, `MeterSequenceGapFiller`). Der Sidecar akzeptiert
nur direkte, unverknuepfte Unterordner mit `not_deployed`, Pilot `BCC_bogen`,
mindestens 30 Bildern und passender Gewicht-SHA. Die freigegebene
15er-Klassenkarte wird beim Modellladen fuer alle IDs und Namen exakt geprueft.
Der Sidecar kopiert einen einmal gelesenen, hashgeprueften Byte-Strom in eine
private temporaere Momentaufnahme; YOLO oeffnet nie erneut den veraenderbaren
Kandidatenpfad, und die Momentaufnahme wird nach dem Laden nochmals gehasht.
Dieser Pilot liefert ausschliesslich Treffer der geprueften Klasse 14 `BCC_bogen`;
Klassen 0 bis 13 werden im BCC-Endpunkt verworfen. Der Kandidat laeuft im eigenen
GPU-Slot `YOLO_TEST` und ersetzt den produktiven Artefaktzeiger nicht. Bei
VRAM-Mangel kann der allgemeine LRU-Manager den geladenen Slot `YOLO`
voruebergehend entladen; das aktive Modell wird bei Bedarf wieder geladen. Der
alte Request ohne ID bleibt nur als kompatibler automatischer Sidecar-Weg
erhalten und wird vom Training Studio nicht mehr angeboten.

## (Fortsetzung aus «KI-Vorschlaege im Codiermodus (seit 2026-09-05)»)

Der gleich parametrische Vergleich vom 2026-07-28 (`conf=0,25`, `imgsz=1280`)
zeigt fuer `bcc_bogen_b50b37ab8a4f` auf den drei wirklich unbekannten,
geschuetzten BCC-Bildern 3/3 Treffer und eine mittlere Box-IoU von 0,8607.
Auf 9/14 kuratierten Negativbildern entstand jedoch mindestens eine Aktivierung.
Der Kandidat bleibt deshalb `not_deployed`. Die drei aelteren Kandidaten kannten
die heutigen Positivbilder bereits; der v3-Negativ-Kandidat und der neue Kandidat
kannten den heutigen Negativpool. Aus diesen 17 Bildern darf deshalb kein fairer
Gesamtsieger oder eine Produktfreigabe abgeleitet werden.

Der unabhaengige BCC-Release-Holdout wird mit
`training/scripts/bcc_release_holdout.py` aus nach dem lokalen
Basismodell-Zeitstempel aufgenommenen XTF-Fotoquellen vorbereitet. Ohne das
urspruengliche Trainingsinventar ist diese Zeitgrenze nicht vollstaendig
beweisbar. Das Werkzeug gleicht Bild-SHA-256 und beide Richtungen jeder Haltung
gegen alle lokal nachvollziehbaren Kandidaten, Trainingssamples, Negativpools,
Collapse-Berichte und Eval-Sets ab. Es kopiert Originale nur lesend in einen neuen,
atomar veroeffentlichten Ordner unter `eval_set/subsets`; vorhandene Holdouts werden
nie ueberschrieben. Kandidaten-Datensaetze werden einschliesslich Receipt,
Bild-/Labelbytes sowie der lokalen `data.yaml`- und `classes.txt`-Hashes geprueft.
`train_bcc_pilot.py` erlaubt nur `path: .`, `images/train` und `images/val` und
bindet Receipt-, YAML- und Klassen-Hash in jedes neu erzeugte Kandidatenmanifest.
Die vier Alt-Kandidaten besitzen diese direkte Manifestbindung noch nicht; ihre
heutigen drei Datensaetze sind dennoch vollstaendig gegen ihre Receipts geprueft.
Die Legacy-Ausnahme gilt nur fuer ihre exakt bekannten Kandidaten-IDs und
Manifest-SHAs; jeder neue oder veraenderte Kandidat ohne diese drei Bindungen
stoppt den Scan. Ein Manifest im BCC-Kandidatenordner ohne exakt
`pilot=BCC_bogen` stoppt ebenfalls, statt unbemerkt uebersprungen zu werden.
Bestehende eingefrorene Eval-Manifeste werden gegen jede deklarierte Datei und
gegen die exakte Bild-/Labelmenge validiert; Legacy-Collapse-Berichte werden ueber
ihre heutigen Bildpfade nachvollzogen und nicht mehr still ignoriert. Ein alter
`dateien`-Eintrag muss ein Array nichtleerer Dateinamen sein und jeder Name muss
eindeutig auf ein bekanntes Bild aufloesbar sein, sonst stoppt der Scan. Der am
2026-07-28 eingefrorene Bestand
`bcc_release_holdout_64d06094c921` enthaelt 60 Bilder aus 60 Haltungen. Seine
verdeckte Vorauswahl war 30/30 und keine Ground-Truth. Der getrennte
Blind-Review ist abgeschlossen: 60/60 Bilder, davon 29 positiv, 31 negativ und
0 ausgeschlossen. Der dynamische Status ist `ready_for_binary_evaluation`; das
eingefrorene Manifest behaelt als Erstellungsbeleg unveraendert
`dataset_status=review_incomplete` und `release_status=not_evaluated`.
Er zeigt fuer alle Bilder nur den festen Pruefauftrag `BCC — Bogen`; bildbezogener
XTF-Untercode, verdeckte Vorauswahl und Modellvorhersage bleiben unsichtbar.
Mindestens 20 bestaetigte Positiv- und 20 Negativhaltungen bedeuten nur
`ready_for_binary_evaluation`, niemals eine Modellfreigabe. Ohne menschliche Boxen
misst dieser Bestand keine Lokalisation und kein mAP. Fuer den eingefrorenen
V1-Bestand muessen Kandidatenumfang sowie die aggregierten Fingerprints der
bekannten Bild-Hashes und Haltungs-Aliase exakt gleich bleiben. Eine Aenderung,
die einen dieser Werte veraendert, sperrt den Status; eingefrorene Eval-Manifeste
werden zusaetzlich dateiexakt geprueft. Danach wird ein neuer Holdout benoetigt. Die
gebundene Review-Datei unter `C:\KI_BRAIN\eval_review` hat SHA-256
`d3c71fa37bca6bc189e2beebef75986c43a819da4094bf5eb0a36228664de663`.

`training/scripts/evaluate_bcc_release_holdout.py` vergleicht exakt den
eingefrorenen Kandidatenumfang mit festem `conf=0,25`, `imgsz=1280` und nur
Klasse 14 `BCC_bogen`. Es braucht die Python-Umgebung des Sidecars, sperrt einen
parallel laufenden Sidecar, liest alle Modelle aus privaten hashgeprueften
Momentaufnahmen und schreibt zuerst einen labelblinden Vorhersagebeleg. Nur
dessen neu eingelesene, SHA-gebundene Bytes werden gegen die gebundene
Review-Momentaufnahme bewertet. Technische Fehler zaehlen nie als Negativbefund;
ein Teilfehler verhindert den endgueltigen Auswertungsbericht. Aufhebungsmarker,
Klassenkarte, Bildbytes, Geraet, Qualitaetsgrenzen und Laufzeitversionen werden
mitgebunden. Das Werkzeug trainiert, aktiviert und ersetzt kein Modell.

`tools/PdfCodeScanner` erzeugt daneben eine rein lesende protokollbasierte
BCC-Positionsliste. Sie fuehrt die acht gueltigen Untercodes `BCCAA`, `BCCAB`,
`BCCAY`, `BCCBA`, `BCCBB`, `BCCBY`, `BCCYA` und `BCCYB` fuer die grobe
Modellklasse `BCC_bogen` gemeinsam. Pro Befund werden PDF, Meteranfang/-ende,
exakter Videozaehlerstand und nur ein eindeutig zugeordnetes Video ausgegeben;
fehlende oder mehrdeutige Werte werden sichtbar gelassen. Der bekannte Rohcode
`BCC.YB` schliesst die ganze betroffene Haltung fail-closed aus. Der JSON-Bericht
wird atomar ausserhalb der Kundenoriginale geschrieben. Diese Liste ist erst
die Messgrundlage; ohne Modelllauf und Zuordnungstoleranz ist sie noch kein
Recall- oder Praezisionswert. Mit `--expect-holdings` und `--expect-findings`
stoppt das Werkzeug fail-closed, wenn der gescannte Bestand nicht zur zuvor
freigegebenen Ausgangszahl passt.

Die Archivmessung des BCC-Copiloten wird mit
`training/scripts/bcc_pdf_recall_bericht.py` strikt in Kalibrierung und Messung
getrennt. Gesamt-, SD- und HD-Ausgaben besitzen verschiedene Dateinamen; ein
Gruppenlauf darf den Gesamtbeleg nie ueberschreiben. Der additive
`vergleichsbestand_*.json` kennzeichnet die verbrauchte Messhaelfte ausdruecklich
nur als bekannten Vergleichsbestand, nicht als neue Release-Abnahme.
`bcc_pdf_precision_queue.py` rekonstruiert den gemessenen Arbeitspunkt aus den
gespeicherten Einzelbildern und baut eine blinde Clip-Pruefung aller Vorschlaege.
Konfidenz und PDF-Zuordnung bleiben unsichtbar. Erst die vollstaendige, an den
Queue-Hash gebundene Review darf `bcc_pdf_precision_bericht.py` auswerten;
unsichere Urteile erscheinen als untere und obere Precision-Grenze.

Der reale Blindreview des Archiv-Arbeitspunkts ist abgeschlossen: 154/154
Vorschlaege, davon 91 mit sichtbarem Bogen, 60 ohne Bogen und 3 unsicher.
Vorschlags-Precision ohne unsichere Faelle: 60,3 %; harte Grenze bei anderer
Wertung der drei unsicheren Faelle: 59,1-61,0 %. Das ist keine
Ereignis-Precision, weil zwei Vorschlaege denselben Bogen zeigen koennen. Aus
diesem Wert und dem PDF-Recall darf deshalb kein F1-Wert gebildet werden.

`training/scripts/osd_wahrheit_aus_protokoll.py` erzeugt OSD-Bilder aus dem
PDF-Meterstand am PDF-Videozaehlerstand. Das Ziel darf nicht unter dem
Kundenbestand liegen, wird ueber einen Arbeitsordner atomar veroeffentlicht und
nie ueberschrieben. Gleiche oder umgedrehte Haltungen bleiben im selben
Train-/Validation-/Test-Teil; bytegleiche Bilder werden nur einmal aufgenommen.
Das Werkzeug wird mit `sidecar\.venv\Scripts\python.exe` gestartet, weil der
Meterleser OpenCV aus dieser Umgebung benoetigt.
Der automatisch beschriftete Bestand startet mit `status=qa_offen`: Die zwei
belegten Zeitpunkte pruefen die grundsaetzliche Zeitachse, ersetzen aber keine
Sichtprobe ueber den ganzen Archivbestand.

`training/scripts/bcc_pdf_messreserve.py` reserviert deterministisch einen neuen
reinen SD-Messbestand. Es sperrt alte Mess-, Trainings- und Eval-Haltungen samt
Gegenrichtung und akzeptiert nur die acht gueltigen BCC-Untercodes. Der aktuelle
V2-Beleg umfasst 50 SD-Haltungen mit 130 Boegen und startet mit
`reserved_not_evaluated`. Eine unabhaengige HD-Reserve existiert weiterhin nicht.

Der reale OSD-V1-Lauf enthaelt nach Schutzfiltern und Byte-Deduplizierung 897
Bilder aus 364 physischen Haltungen: 674 Train, 135 Validation und 88 Test.
`osd_protokoll_qa_queue.py` hat daraus eine blinde Sichtprobe mit 30 Bildern aus
30 Haltungen erzeugt. `tools/EvalVisibilityReview/start_osd_protokoll_qa.ps1`
oeffnet den Eingabeplatz; erst `osd_protokoll_qa_bericht.py` vergleicht die
persoenliche Lesung mit den bis dahin verdeckten PDF-Sollwerten.

Die reale OSD-Sichtprobe ergab 25/30 Uebereinstimmungen auf 1 cm und 29/30
innerhalb 10 cm; ein Fall wich grob ab. Die kleinen Differenzen passen zur
Kamerabewegung zwischen Protokollmoment und Bild, der grobe Fall ist ein falsches
PDF-Label. Die Sichtprobe misst die PDF-/Video-Zuordnung und nicht den Leser;
bei allen fuenf Differenzen hatte er `nicht_gelesen` geliefert. Die 897 Werte
bleiben schwache Labels mit Zeit- und Zuordnungsrauschen. Nur die 30
persoenlich abgelesenen Werte sind exaktes Gold. Der aktuelle
`sidecar/sidecar/osd_meter.py` ist ein fester Vorlagenleser ohne Trainingsweg;
ein neues trainierbares OCR-Modell ist noch nicht vorhanden.

`osd_layout_review_queue.py` zieht deshalb 40 weitere physische Haltungen, je
ein Bild und ohne Ueberschneidung mit der 30er-Sichtprobe. Der lokale
`osd_layout_review_server.py` zeigt weder PDF-Wert noch Lesergebnis. Die
Meteranzeige wird direkt im Bild angeklickt und getrennt nach Polaritaet, Farbe
und Schreibweise eingeordnet. `osd_layout_review_bericht.py` zaehlt erst eine
vollstaendige, an Queue- und Bild-SHA gebundene Review. Die Lage wird nur aus dem
menschlichen Klick abgeleitet; Kopftext darf nicht automatisch als Meterstand
gelten.

Die reale 40er-Sichtung ist abgeschlossen: 38 Meteranzeigen liegen unten rechts,
2 unten links und keine oben. Polaritaet: 18 hell auf dunkel, 18 dunkel auf hell,
4 andere. Farbe: 20 weiss/grau, 7 gelb, 13 andere. Format: 19 mit Praefix oder
fuehrenden Nullen, 15 Zahlen mit Einheit, 6 ohne Einheit. Das belegt mehrere
Hauptstile, aber wegen der kleinen Stichprobe keine exakten Archivanteile.

Der Diagnosekandidat fuer den Vierziffern-Stil nutzt nach einer gescheiterten
oder unvollstaendigen Vorlagenlesung das bereits lokal installierte Tesseract.
Er prueft beide unteren Ecken und beide Polaritaeten, akzeptiert aber nur die
vollstaendige Form `LZ... + 0000.00 m`; fehlt Tesseract oder ist die Form
unsicher, bleibt der Wert `None`. Auf dem Zielstil liest er 8/12. Sein neuer
Rueckfallweg liefert in der 40er-Probe 12 Werte, alle 12 passend zu den schwachen
PDF-Labels; der gesamte Leser liefert dort 13 Werte mit einem falschen oder
nicht pruefbaren Fall.

Seit 2026-08-14 ist die Zeichenfindung aufloesungsunabhaengig. Ihre Abstandsschranken
standen als feste Pixelwerte da, eingestellt auf SD mit rund 18 Pixel hohen Ziffern;
auf HD sind dieselben Zeichen doppelt so gross, und der Leser verlor Dezimalpunkt und
Einheit ("LZ1: 3.2m" -> "L132"). `glyphen_skala` misst jetzt die tatsaechliche
Zeichenhoehe und richtet die Schranken daran aus, skaliert aber nie nach unten —
SD bleibt dadurch unveraendert.

Zwei Regeln halten die wichtigste Eigenschaft des Lesers fest: keine Ziffer hinter
der Einheit, hoechstens ein Dezimalpunkt. Beides heisst verwerfen, nicht raten.
Ohne sie entstand aus `LZ:::6.4m3` der Wert 6,4 statt 26,4 und aus `ZLZ1:.0.1m`
der Wert 0,1 statt -0,1. Das Minus als eigenes Zeichen wurde geprueft und wieder
verworfen: Es rettete eine Lesung und kostete sieben. Negative Zaehlerstaende vor
dem Rohranfang gelten deshalb als mehrdeutig und werden nicht gelesen.

`training/scripts/osd_goldmessung.py` misst den Leser wiederholbar gegen die drei
eingefrorenen Goldsaetze und trennt richtig / falsch / nicht gelesen streng; ein
falscher Wert wandert unbemerkt ins Protokoll, ein fehlender faellt auf. Stand
(Leser `85d3a107e5b3`, Bericht-SHA-256
`3ddce99516ee8866c7bdba24fcc9cd52c01f499a1a103ff5bb3c6e124ab06aa2`):
SD 80/95, HD 15/30, HD2 43/72, zusammen 138 richtig und **0 falsch** — vorher
82 richtig und 0 falsch. Die zwei SD-Verluste waren Zufallstreffer aus erkennbar
kaputten Zeichenfolgen.

Die hashgebundene Archivwiederholung mit `osd_archiv_abdeckung_messung.py`
verarbeitet 83 eindeutige Videos an je 20 gleichmaessigen Stellen. Mit demselben
Leser: SD 43,0 % und HD 53,3 %, zusammen 45,8 % (vorher SD 22,1 %, HD 3,1 %,
zusammen 16,8 %). Videos ohne jede Lesung: SD 33 -> 16 von 60, HD 21 -> 4 von 23.
Diese Zahl misst nur, OB gelesen wurde — die Richtigkeit belegt allein die
Goldmessung. Bericht-SHA-256
`a2e13deaf17d20ca61e35feee6b042307e0a01c3d463db79022b49cc94682f6a`.
Der Bericht bindet Leser und feste Auswahl; Video-Inhalte sind ueber Pfad,
Groesse und Aenderungszeit, aber nicht per Vollhash gebunden. Der Kandidat bleibt
`diagnostic_not_deployed`.

### Trainierbarer OSD-Zeichenleser — Stufe 1 gemessen, GESCHEITERT (2026-08-16)

Entwurf `docs/superpowers/specs/2026-08-15-osd-meterleser-modell-design.md`, Plan
`docs/superpowers/plans/2026-08-15-osd-meterleser-stufe1.md`. Werkzeugkette:
`osd_frames_ziehen.py` -> `osd_ernte.py` -> `osd_kunstbilder.py` ->
`osd_datensatz.py` -> `train_osd_zeichen.py` -> `osd_schwelle_kalibrieren.py` ->
`osd_modell_goldmessung.py`, gemeinsamer Inferenzweg `osd_modell_leser.py`,
Laufzeitteil `sidecar/sidecar/osd_modell.py`. `osd_meter.py` wurde nicht angefasst.

Der Kandidat `osd_zeichen_1daf5433416d` (Gewicht-SHA-256
`1daf5433416dd4aadf33c249419cd1ff305570630eb02227670f87c0226f9cf0`) erreicht auf
den drei eingefrorenen Goldsaetzen **120 richtig und 1 falsch** (SD 67/95, HD
14/30, HD2 39/72 richtig; der falsche Wert liegt in HD2). Bericht-SHA-256
`eb07c04c7700e640c642308bc29498e553ba89ade1e56c644ed29590cbc8fdfb`.
Der Vorlagenleser steht bei 138 richtig / 0 falsch — das Modell ist in BEIDEN
Richtungen schlechter. Freigabemarke (null falsch UND >= 170 richtig) doppelt
verfehlt; Status bleibt `diagnostic_not_deployed`.

Die Ursachenkette ist gemessen, nicht vermutet:

- Die Lehrer-Ernte lieferte 932 Ausschnitte aus nur **229 von 1361** Haltungen.
  Der Lehrer liest nur seine eigenen Stile weiter; die uebrigen 1132 Haltungen
  tragen genau die Stile, die er nicht kann.
- Die kuenstlichen Bilder haben ihren Zweck erfuellt, aber nur ihren: Das
  Ziffernverhaeltnis ging von 1:10,5 auf 1:2,9. Die Stilluecke schliessen sie
  nicht — sie zeigen Rauschen mit Farbstich, kein echtes Kanalvideo.
- Auf dem Reservebestand (88 vom Training ausgeschlossene Bilder) las das Modell
  **10 von 88, davon 4 grob falsch**. Die interne Validierung sah dagegen
  hervorragend aus (P 0,965, R 0,966, mAP50 0,984) — der Abstand zwischen beiden
  Zahlen IST das Ergebnis.
- Schwaechstes Zeichen ist der Dezimalpunkt: Recall 0,761 gegen 0,98 bei allen
  anderen. Genau das Zeichen, dessen Fehlen den Wert um Faktor zehn verschiebt.

Die Schwelle wurde nur mit `--trotz-wenig-vergleichbaren-faellen` eingefroren:
Bei 10 vergleichbaren Faellen verweigert `osd_schwelle_kalibrieren.py` sonst
(Mindestmass 20). Diese Zahl ist deshalb eine Standortbestimmung, keine Abnahme.

Was trotzdem belegt funktioniert: Der Schutz sperrte 63 Goldhaltungen VOR dem
Extrahieren; keine physische Haltung lag in Train und Validation zugleich; die
Kalibrierung verweigerte von sich aus. **Stufe 1 hat ihre Frage beantwortet:**
Lehrer-Ernte und kuenstliche Bilder allein reichen nicht — die 200
handbeschrifteten schweren Faelle aus Stufe 2 sind noetig, und die Messung sagt
auch wofuer.

### Stufe 2 gemessen — der Goldbestand vertritt das Archiv nicht (2026-08-17)

Pascal hat alle 200 Karten der Handliste entschieden: 97 uebernommen, 86 "boxen
passen nicht", 17 unleserlich. Werkzeuge: `osd_frames_ziehen.py` (Bilder nach
Haltung ablegen), `osd_handlabel.py` (Modi `queue`/`publizieren`) und
`tools/EvalVisibilityReview/osd_handlabel_server.py` (Zone 4x, Mensch zieht EINEN
Kasten, Server segmentiert per `zeichen_in_kasten`).

Kandidat `osd_zeichen_c668e35d59cb` (Gewicht-SHA-256
`c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845`,
Bericht-SHA-256
`cc8cdd0d9da5dbb9825010996b37b69a76c2725db2dcd0e83876a92c8c983b90`),
Schwelle 0,25 ohne Zwang eingefroren (20 vergleichbare Faelle, Mindestmass
erreicht):

| Stand | richtig | falsch |
|---|---|---|
| Vorlagenleser | **138** | **0** |
| v1 ohne Handfaelle | 120 | 1 |
| v2 mit den 97 Handfaellen | **104** | **0** |

**Was die Handfaelle bewirkt haben:** Der falsche Wert ist weg, und die Lesequote
auf ungesehenen Haltungen hat sich verdoppelt (20 von 88 statt 10 von 88). Auf
fremden Stilen ist das Modell belegbar besser — genau das, was 3000 kuenstliche
Bilder nicht geschafft hatten.

**Warum Gold trotzdem faellt:** 64 der 97 handbeschrifteten Anzeigen bestehen NUR
aus Zahlen ohne `LZ`-Beschriftung. Beim Lehrer sind es 3 von 932 — er liest
praktisch nur einen Anzeigetyp. Die drei Goldsaetze bestehen fast ausschliesslich
aus diesem beschrifteten Stil. Das Modell hat also einen Stil gelernt, der auf
Gold kaum vorkommt, und ist beim Goldstil unsicherer geworden.

**Der eigentliche Befund ist damit nicht "zu wenige Daten", sondern eine
Messlatte, die den Bestand nicht vertritt.** Solange die drei Goldsaetze nur den
beschrifteten Stil messen, sieht jede Verbesserung auf den anderen zwei Dritteln
des Archivs wie eine Verschlechterung aus. Ein vierter, stilgemischter Goldsatz
muesste her, BEVOR weitere Handarbeit ins Training geht.

Weiter offen: Die Ablehnungsquote der Handliste lag bei 43 % (86 von 200), fast
ausschliesslich wegen der Segmentierung — Zeichen ausserhalb des Satzes wurden nur
EINMAL versucht, die Sorge um das `+` war unbegruendet. `zeichen_in_kasten`
reagiert nicht monoton auf die Kastengroesse (bei +5 px Rand brach ein
verifizierter Fall von 8 auf 5 Zeichen ein, bei +10 und +20 px stimmte er wieder);
die Live-Vorschau macht das handhabbar, eine erneute Runde sollte es aber
vorher verbessern.

Beide Kandidaten bleiben `diagnostic_not_deployed`. Der Vorlagenleser bleibt
vorne in der Kette.

### Vierte Messlatte gemessen — die 0 falsch gehoerten der Messlatte (2026-08-17)

`training/scripts/osd_goldsatz.py` zieht mit `queue`/`einfrieren` einen
stilgemischten Goldsatz gleichmaessig ueber die freien physischen Haltungen,
ausdruecklich NICHT nach Lesbarkeit. Dreifache Sperre: Gold, Reservebestand UND
Trainingsmaterial (Lehrer-Ernte und Handliste, fail-closed ueber ihre Belege).
Die Trainingssperre steht bewusst NICHT in `osd_schutz.lade_schutz()` — diesen
Schutz laden auch die Trainingsskripte, dort ist Trainingsmaterial per
Definition erlaubt.

Der eingefrorene Satz `osd_mix_v1` (Manifest-SHA-256
`5ed76a78087021803b9084ac0ff643aab347d2357abee43e11ef1277099d9015`) enthaelt
120 Bilder aus 120 physischen Haltungen, gezogen aus 1064 freien (366
gesperrt), persoenlich abgelesen: 119 mit sichtbarer Anzeige, 1 ohne.

Zwei Belege, dass der Satz den Bestand vertritt: Der Vorlagenleser liest hier
40,8 % der Bilder, die unabhaengige Archivmessung ergab 45,8 %. Und die
Auflösungen sind 117 SD gegen 3 HD — die drei alten Saetze wiegen mit 95 SD
gegen 102 HD das seltene HD mehr als doppelt.

| Leser | richtig | falsch | nicht gelesen |
|---|---|---|---|
| Vorlagenleser | 45 | **4** | 71 |
| v1 `osd_zeichen_1daf5433416d` | 7 | 2 | 111 |
| v2 `osd_zeichen_c668e35d59cb` | 7 | **0** | 113 |

**Die wichtigste Zahl ist die 4.** Der Vorlagenleser stand auf den drei alten
Saetzen bei 138 richtig / null falsch, und diese null galt als seine
Kerneigenschaft. Auf vertretendem Material produziert er 4 falsche Werte bei 49
Lesungen. Die null war eine Eigenschaft der Messlatte, nicht des Lesers.

Zwei der vier haben dieselbe Ursache: ein verlorenes Minus (Soll -0,01 gelesen
als 0,01; Soll -2,41 gelesen als 2,41). Das widerspricht der bisherigen Annahme,
negative Zaehlerstaende wuerden als mehrdeutig verworfen — sie werden gelesen
und liefern das Vorzeichen falsch. Von 3 negativen Sollwerten wurden 2 gelesen,
beide falsch. Die anderen zwei Faelle sind Ziffernfehler (21,7 -> 24,7;
13,8 -> 13,88). Von 20 Bildern mit Rohranfang 0,00 liest er nur 4 richtig.

`messe_satz()` zaehlt eine Lesung auf einem Bild mit ausdruecklichem
`menschlich_lesbar=false` jetzt als `falsch` mit Grund `erfunden` — eine
erfundene Zahl wandert unbemerkt ins Protokoll. Die drei alten Saetze aendern
sich dadurch nicht; sie tragen das Feld nur mit `true`.

Beide Messwerkzeuge kennen `--satz`. Ein Lauf ueber einen anderen Bestand
beurteilt die Freigabemarke NICHT (`freigabe_erreicht=null`, nicht `false`):
"170 von 197" ist an die drei Standardsaetze gebunden. Der Berichtsname trug
zuvor nur die Kandidaten-ID; da ein bestehender Bericht nie ueberschrieben wird,
ging die zweite Messung desselben Kandidaten still verloren (real passiert).
Zusatzmessungen tragen ihren Bestand jetzt im Namen.

Berichte: Vorlagenleser
`9ad96e2d51a1da60415398d9f2a4c9bc539dbe2663a3f43a8e107055643b2d21`, v2
`117e94892466473f2a83893a6d80cbf10c3d9f313df6adf22aca67a2852a1d38`, v1
`ecabed4bc28d1089ee2b3e31c02a4c8cc9709fddfcbac99368f6a375b107b487`.

Beide Kandidaten bleiben `diagnostic_not_deployed`, und der Vorlagenleser bleibt
vorne in der Kette — er liest sechsmal mehr.

### Zwei Ursachen im Vorlagenleser behoben (2026-08-17)

Erste Eingriffe in `osd_meter.py` seit langem, beide mit gemessener Ursache. Der
neue Satz hat sie erst sichtbar gemacht.

**1. Verlorenes Vorzeichen.** Der Zwei-Dezimal-Pfad konnte ein Minus
strukturell nicht ausdruecken: Es fehlt in `ZWEI_DEZIMAL_WHITELIST`, und der
flache Strich faellt in `_zwei_dezimal_zeile` durch beide Filter — die
Zeichenpruefung verlangt `h>=6`, die Satzzeichenpruefung die Grundlinie. Im Bild
ist er klar sichtbar (`- 2.41  m`) und wird als 4x1-Fleck gefunden, aber nie
weitergegeben. `_hat_vorzeichenstrich()` verwirft die ganze Lesung, arbeitet aber
NEBEN der Maske und veraendert deren Inhalt nicht: Ein zusaetzlicher Fleck in der
OCR-Zeile hat in diesem Leser schon mehrfach belegte richtige Werte gekostet.
Gelesen wird das Vorzeichen bewusst nicht — der Vertrag ist 0..400 m, und ein
Zaehlerstand vor dem Rohranfang traegt fuer das Protokoll nichts. Die zwei
Schranken sind an der Sache begruendet: Ein Vorzeichen ist ein eigenes Zeichen
(von der Ziffer getrennt) und sitzt auf halber Zeichenhoehe. Die verschaerfte
Variante gegen die lose gemessen: null Kosten und 6,8 % Fehlalarm gegen zwei
verlorene richtige Werte und 13,2 %.

**2. Erster Treffer statt Mehrheit.** Der Vierziffern-Pfad nahm den ersten
vollstaendigen Treffer einer einzigen Schwelle; eine verlesene Ziffer wurde damit
mit voller Zuversicht geliefert (`LZ1: +0021.70 m` ergab 24,7). Der
Zwei-Dezimal-Pfad hat gegen genau diesen Fehler ein Quorum ueber fuenf
Schwellen — hier fehlte es. Neu sind `_vierziffern_masken()` (Schwellenfaecher
aus Bruchteilen des 95. Perzentils, beide Polaritaeten, bisherige Kandidaten
vorne) und `_mehrheit()`. Ohne Mindeststimmenzahl, und das ist gemessen: Eine
Mindestzahl von 2 brachte null falsche Werte, kostete aber 8 belegte richtige
(10 bei 3 Stimmen); Einzelstimmen sind hier in 10 von 11 Faellen richtig.
Abbruch bei zwei uebereinstimmenden Stimmen, weil der Pfad im Bogen-Copiloten je
Einzelbild laeuft — Laufzeit 264 -> 374 ms je Bild (Faktor 1,42) statt rund elf
Tesseract-Prozessen.

| Satz | vorher | nachher |
|---|---|---|
| `osd_sd_v1` | 80 richtig | **88 richtig** |
| `osd_hd_v1` | 15 richtig | 15 richtig |
| `osd_hd2_v1` | 43 richtig | 43 richtig |
| drei alte zusammen | 138 / 0 falsch | **146 / 0 falsch** |
| `osd_mix_v1` | 45 / 4 falsch | **48 / 1 falsch** |
| alle 317 Bilder | 183 / 4 falsch | **194 / 1 falsch** |

Der Faecher bringt also nicht nur den einen falschen Wert weg, sondern liest acht
bisher unlesbare SD-Bilder richtig.

Die hashgebundene Archivwiederholung (83 Videos, je 20 Stellen, derselbe
gebundene Bestand) steigt von 45,8 % auf 47,2 %: SD 43,0 -> 44,8 %, HD
unveraendert 53,3 %. Deutlicher wird es je Haltung — SD-Haltungen mit mindestens
70 % Abdeckung 19 -> 23 von 60, denn erst eine dichte Folge ergibt eine
brauchbare Meterspur. Haltungen ohne jede Lesung bleiben 16 (SD) und 4 (HD).
Bericht `osd_archiv_abdeckung_nach_mehrheit_20260817.json`.

Produktive Abnahme: `BendSuggestionLiveAcceptanceTests` laeuft mit echtem
Sidecar, echtem Video und dem gebundenen Kandidaten `bcc_nc15_seed46_20260808`
durch. Der Bogen-Copilot findet dieselben fuenf Stellen, die Meterwerte bleiben
innerhalb der 0,6-m-Toleranz der Repo-Fixture.

**Gemessen und ausdruecklich NICHT umgesetzt:** Die Zwei-Dezimal-Form auf dem
Vorlagenweg freizuschalten wuerde den letzten 0,00-Fall retten (`LZ1:0.00m` wird
heute verworfen), kostet aber 3 falsche Werte und zerstoert einen bereits
richtigen Goldwert (1,4 -> 31,1). Der Vorlagenleser haengt nach dem Punkt Zeichen
an (`.9` wird `.01`), und die Form unterscheidet echte von erfundenen
Nachkommastellen nicht. Die alte Entscheidung ist damit gegen vertretendes
Material bestaetigt. Der eine verbleibende falsche Wert (13,8 -> 13,88) bekommt
im ganzen Faecher nur eine Stimme; dort gibt es keine Mehrheit, die ihn
ueberstimmen koennte.

### OSD-Modell als diagnostischer Rueckfall verdrahtet und gemessen (2026-08-17)

`osd_meter.lese_meter` besitzt additiv einen optionalen Modell-Rueckfall. Er wird
erst nach Vorlagenleser sowie beiden Tesseract-Wegen aufgerufen und kann deshalb
keinen vorhandenen Wert ersetzen. Der Bogen-Copilot reicht ihn nur bei
`SEWER_SIDECAR_OSD_MODEL_FALLBACK_ENABLED=true` durch; Standard ist `false`.
Damit ist die Kette messbar, aber noch nicht produktiv freigegeben.

Der Laufzeitanschluss `models/osd_model_wrapper.py` bindet fest den Kandidaten
`osd_zeichen_c668e35d59cb`, den Gewicht-SHA-256
`c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845`
und die Schwelle 0,25. Er akzeptiert nur den Status `diagnostic_not_deployed`,
die feste 15er-Zeichenkarte und `weights/best.pt`. Das Gewicht wird vor dem
Laden aus einer privaten, erneut geprueften Momentaufnahme geoeffnet. Das Modell
belegt den eigenen, durch Busy-Lease und Watchdog geschuetzten GPU-Platz
`YOLO_OSD`; es ersetzt weder Standard- noch BCC-Modell.

`osd_kettenmessung.py` hat die vier bereits verwendeten Goldsaetze auf exakt
demselben Stand verglichen. Ergebnis: Vorlagenkette 194 richtig / 1 falsch,
Kette 224 richtig / 1 falsch. Das Modell liefert 30 neue richtige Werte und
keinen neuen falschen. Mit dem echten Bogen-Kandidaten gleichzeitig geladen
belegt der OSD-Rueckfall zusaetzlich rund 9 MB VRAM. Warm braucht eine
Modelllesung im Mittel 61 ms (Median 35 ms, p95 115 ms); ueber alle 317 Bilder
steigt die mittlere Zeit um rund 24 ms je Bild. Bericht-SHA-256:
`ef25b19df5ae1a169ea91da5b3e14e931b5c196084c596aa05732c810dcd1093`.

Diese Messung waehlt die Kette, erteilt aber keine Produktfreigabe: Alle vier
Saetze einschliesslich `osd_mix_v1` sind jetzt fuer diese Entscheidung verwendet.
Vor dem Einschalten des Standardschalters ist ein frischer, unberuehrter Bestand
Pflicht. Die 22 Sollbilder ohne gefundene Zeichen bleiben eine getrennte Baustelle
vor der Erkennung.

`training/scripts/bcc_pdf_messreserve.py` reserviert deterministisch einen neuen
reinen SD-Messbestand. Es sperrt alte Mess-, Trainings- und Eval-Haltungen samt
Gegenrichtung und akzeptiert nur die acht gueltigen BCC-Untercodes. Der aktuelle
V2-Beleg umfasst 50 SD-Haltungen mit 130 Boegen und startet mit
`reserved_not_evaluated`. Eine unabhaengige HD-Reserve existiert weiterhin nicht.

Die Archivmessung des BCC-Copiloten wird mit
`training/scripts/bcc_pdf_recall_bericht.py` strikt in Kalibrierung und Messung
getrennt. Gesamt-, SD- und HD-Ausgaben besitzen verschiedene Dateinamen; ein
Gruppenlauf darf den Gesamtbeleg nie ueberschreiben. Der additive
`vergleichsbestand_*.json` kennzeichnet die verbrauchte Messhaelfte ausdruecklich
nur als bekannten Vergleichsbestand, nicht als neue Release-Abnahme.
`bcc_pdf_precision_queue.py` rekonstruiert den gemessenen Arbeitspunkt aus den
gespeicherten Einzelbildern und baut eine blinde Clip-Pruefung aller Vorschlaege.
Konfidenz und PDF-Zuordnung bleiben unsichtbar. Erst die vollstaendige, an den
Queue-Hash gebundene Review darf `bcc_pdf_precision_bericht.py` auswerten;
unsichere Urteile erscheinen als untere und obere Precision-Grenze.

`training/scripts/osd_wahrheit_aus_protokoll.py` erzeugt OSD-Bilder aus dem
PDF-Meterstand am PDF-Videozaehlerstand. Das Ziel darf nicht unter dem
Kundenbestand liegen, wird ueber einen Arbeitsordner atomar veroeffentlicht und
nie ueberschrieben. Gleiche oder umgedrehte Haltungen bleiben im selben
Train-/Validation-/Test-Teil; bytegleiche Bilder werden nur einmal aufgenommen.
Der automatisch beschriftete Bestand startet mit `status=qa_offen`: Die zwei
belegten Zeitpunkte pruefen die grundsaetzliche Zeitachse, ersetzen aber keine
Sichtprobe ueber den ganzen Archivbestand.

Der reale Vergleich vom 2026-07-28 hatte 240 Vorhersagen und null technische
Fehler. Die zwei aufgehobenen Altlaeufe bleiben reine Diagnose. Bei den zwei noch
relevanten Kandidaten erreichte `bcc_bogen_af8020b688ac_v3_negatives`
TP/FN/TN/FP = 24/5/9/22 (Balanced Accuracy 55,9 %);
`bcc_bogen_b50b37ab8a4f` erreichte 26/3/6/25 (54,5 %). Der erste hat weniger
Fehlalarme, der zweite weniger verpasste Boegen; es gibt keinen eindeutigen
Spitzenreiter. Beide erzeugen zu viele Fehlalarme und bleiben `not_deployed`.
Der Bericht sagt deshalb `comparison_complete_not_release_qualified`. Da dieser
Holdout vier Kandidaten verglichen hat, braucht ein spaeterer Spitzenreiter vor
Aktivierung einen frischen, zuvor unberuehrten Bestaetigungsholdout.

`training/scripts/bcc_hard_negative_review.py` bereitet getrennt davon frische
BCC-Fehlalarmbilder fuer ein menschliches All-Class-Review vor. Es sperrt bekannte
Bildhashes sowie gleiche oder umgedrehte Trainings-/Eval-Haltungen, bindet class_map
v3 samt VSA-Hash und waehlt genau ein Vollbild je physischer Haltung. Die
Modellvorhersagen bleiben im lokalen Browser unsichtbar. Der eigene Pruefplatz
`tools/EvalVisibilityReview/bcc_hard_negative_review_server.py` akzeptiert nur
`all_classes_clear`, `mapped_object_visible` oder `exclude_uncertain`; das alte
Holdout-Urteil `negative` ist ausdruecklich kein Trainingsnegativ. Queue und Review
werden getrennt und atomar gespeichert. Der Review `bcc_hn_d37e1e0e481c` ist mit
14/14 Bildern abgeschlossen: 10 `all_classes_clear`, 4
`mapped_object_visible`, 0 unklar. Der Publisher hat nur die 10 vollstaendig
klassenfreien Bilder als unveraenderlichen Satz `bcc_hn_54f6608b975a`
veroeffentlicht (8 Train, 2 Validation, eine physische Haltung je Bild). Sein
Manifest bindet Bildbytes, Review, Queue-Manifest, Kandidatenliste und class_map v3
ueber SHA-256-Belege; Originale und ausgeschlossene Bilder bleiben unangetastet.

## SAM-Review im Training Center

`TrainingReviewSamWorkflow` prueft Kandidat, Box und Frame, startet den bedarfsgesteuert
erzeugten `ITrainingReviewSamSegmentationService` und bereitet Speichermaske sowie
Statustext auf. Der Rohrdurchmesser kommt ueber die zentrale Fenster-Fabrik; nur bei
fehlendem Wert gilt weiter 300 mm. `TrainingCenterWindow` bleibt fuer Schaltflaeche,
Maskenanzeige und Dialoge zustaendig. Datei-, Einstellungs- und Maskenlogik nicht wieder
in den Fenster-Code verschieben.

Der Pruefplatz im `TrainingStudioWindow` baut Workbench, Warteschlange und
KI-Bereitschaft gemeinsam ueber `TrainingStudioWindowDependencyFactory`. Beim ersten
Oeffnen prueft `TrainingStudioAiReadinessWorkflow` die Sidecar-Gesundheit und verwendet
nur bei einem Offline-Sidecar den zentralen `AiStartupService`; die Schaltflaeche
`KI starten` bietet denselben Weg fuer einen manuellen Wiederholungsversuch. Das Fenster
startet nur den ViewModel-Befehl und enthaelt keine Prozesslogik. Segmentierung und
Code-Vorschlag laufen parallel. Wenn nur einer der beiden Aufrufe scheitert, behaelt das
ViewModel das bereits abgeschlossene Teilergebnis sichtbar.

`PDF-Protokoll laden` verwendet den Application-Vertrag
`ITrainingPdfReviewImportService` und den Infrastructure-Dienst
`TrainingPdfReviewImportService`. Das Kunden-PDF wird nur gelesen und vor sowie nach
der Extraktion per SHA-256 kontrolliert. Als sicher gelten in dieser Reihenfolge:
ein Code im selben Fotoblock, eine exakte Foto-ID beziehungsweise ein exakter
Dateiname und zuletzt nur die vollständige Kombination aus Videozeit, Meter und
normalisiert identischem Befundtext. Unsichere Bilder oder Seitengrafiken werden
mit Hinweis übersprungen; mehrere Operateur-Codes am selben Foto bleiben getrennte
Prüffälle. Die extrahierten Prüfbilder liegen inhaltsadressiert unter
`<KnowledgeRoot>\training\pdf_review_imports\<vollstaendiger-pdf-sha256>`.
`PDF-Ordner laden` verwendet zusätzlich
`ITrainingPdfFolderDiscoveryService` und
`TrainingPdfReviewBatchImportUseCase`. Der Benutzer kann mehrere Wurzeln
wählen; darunter werden PDF-Dateien rekursiv, stabil sortiert und ohne
Verzeichnis-/Dateiverknüpfungen gesucht. Überlappende Wurzeln und identische
PDF-Inhalte werden dedupliziert. Die PDFs werden bewusst nacheinander gelesen;
ein Kunden-PDF wird niemals verändert und ein defektes PDF stoppt die
restlichen Dateien nicht. Der WPF-Weg zeigt Fortschritt und Abbruch, sperrt
währenddessen widersprüchliche Prüfaktionen und virtualisiert die kleinen
Vorschaubilder.

Einzel- und Ordnerimport binden vor dem ersten PDF über
`TrainingPdfReviewProtectedImportService` beziehungsweise den Batch-UseCase
einen unveränderlichen Eval-Schutzstand. Konfigurierte Schutzdaten müssen
echte SHA-256-Werte und kanonische Haltungsnummern enthalten; beim PDF-Import
ist wegen einer möglichen Farbnormalisierung mindestens eine Haltungsmenge
Pflicht. Gleiche und umgedrehte Eval-Haltungen sowie exakte Bildbytes werden
pro Foto vor Matching und Arbeitsablage ausgelassen. Ist der Schutz nicht
lesbar oder semantisch ungültig, beginnt kein PDF-Import.
`ServiceProvider.TrainingPdfReviews` registriert deshalb die geschützte
Fassade; nur der interne `TrainingPdfReviewReader` steht dem einmal geschützten
Batch als Rohleser zur Verfügung.
Der Reader stoppt grosse Protokolle fail-closed bei insgesamt mehr als 256 MiB
extrahierten Fotobytes oder 250 Millionen Fotopixeln.
JPEG-Fotos mit `DeviceCMYK`/Adobe-YCCK oder einer nicht identischen PDF-`Decode`-
Regel werden vor Vorschau, SAM und Trainingsablage ueber
`ITrainingPdfJpegColorNormalizer` in ein sichtbares RGB-PNG umgewandelt. Die
Format-, Mass- und Farbraumpruefung liegt im kleinen
`TrainingPdfEmbeddedImageReader`; die WPF-Implementierung trennt dabei die
DCT-Kanalpolaritaet von der eigentlichen PDF-`Decode`-Regel. Ein unbekannter
Farbraum, ein CMYK-JPEG ohne eindeutigen Adobe-Farbmarker oder eine fehlgeschlagene
Normalisierung wird fail-closed ausgelassen; normale RGB-JPEGs bleiben bytegleich.
Custom-Font-Verschiebungen werden einmal je Seite erkannt und identisch auf
Seitentext und lokalen Fotoblock angewandt. Ein eindeutiger Protokolltitel ist die
kanonische Haltungsnummer; nur die zweizeilige Fretz-Tabelle auf derselben
`Haltungsinspektion`-Titelseite darf eine interne Haltungsnummer als Alias binden.
`Haltungsbilder`-Titel erzeugen selbst keine Aliase, direkte `Haltung`-Felder ohne
Fretz-Haupttitel bleiben echte Abschnittsmarker. Kompakte Datumsblöcke vor der Datei-ID
werden nur bei passendem Elternordner abgetrennt. Sammel-PDFs halten die explizite
Haltung je Abschnitt und damit je `WorkbenchItem` getrennt; mehrdeutige Abschnitte
werden ausgelassen. Globale Befund-Fallbacks sind bei mehreren Haltungen gesperrt,
damit kein Foto Daten aus einem fremden Abschnitt übernimmt. Meter, Befundtext und
Start-/Ende-Daten eines Streckenschadens werden stattdessen nur aus dem einmal
materialisierten Text der sicher zugeordneten Haltung ergänzt.
`TrainingPdfProtocolFindingParser` kapselt dabei Befundzeilen und die
Start-/Ende-Paarung; der Metadaten-Parser bleibt für Dokument- und Haltungsdaten
zuständig.
`TrainingPdfProtocolFindingParser` kapselt dabei Befundzeilen und die
Start-/Ende-Paarung; der Metadaten-Parser bleibt für Dokument- und Haltungsdaten
zuständig.
Inspektionsdatum, vollständiger mehrzeiliger Befundtext und
sichere Von-Bis-Meter eines Streckenschadens werden als Referenz übernommen.
Code und Befund stehen nur in `WorkbenchItem.SourceSuggestion`; `ExistingCode`
bleibt Reparaturen vorhandener Samples vorbehalten. Eine unabhängige KI-Anzeige
darf diese Operateurvorgabe nicht überschreiben. Gold, KB und Teacher werden erst
nach persönlicher BBox, gültiger sichtbarer SAM-Maske und Akzeptieren geschrieben.
Das bestätigte Sample behält die Prüfspur als `SourceType=PdfPhoto` und in `Notes`
mit Dokumentname, vollständigem PDF-Hash, Seite, Foto-ID und Zuordnungsart.
`SourceReferenceCode` und `SourceReferenceDescription` bewahren zusätzlich die
ursprüngliche Operateurangabe; beide sind für PDF-Gold Pflicht.

Das Fenster bietet fuer den reinen Fototest `Aktives Standardmodell` und nach
erfolgreicher KI-Bereitschaft jeden manifest- und hashgeprueften BCC-Kandidaten
einzeln mit ID und gekuerzter SHA-256 an. Es gibt dort keine automatische
BCC-Auswahl. `TrainingStudioPreviewModelCatalog` baut diese fail-closed Liste;
`TrainingStudioPreviewPresenter` formatiert das reine Anzeigeergebnis ausserhalb
des ViewModels. Automatische Treffer erscheinen nur als blaue Vorschau-Boxen
mit Code und Klartext. Sie werden nie in `CurrentBox`, die SAM-Maske oder einen
Goldsample uebernommen. Ein Bild- oder Kandidatenwechsel verwirft ein spaetes
Vorschauergebnis. Ein Katalogfehler entfernt alte Kandidaten; ein spaetes
Katalogergebnis ueberschreibt keine neuere Benutzerauswahl. Fehlt der exakte
ID-/SHA-Pin, bleibt der Kandidat gesperrt. Ein qualitaetsbedingt nicht
ausgewertetes Foto wird ausdruecklich als `nicht geprueft` und nicht als
Negativtreffer gemeldet. Solange der Modelltest laeuft, beginnen weder ein neuer
Box-Lauf noch ein Speichervorgang. Nur die rote, vom Menschen gezogene Box kann
ueber Akzeptieren/Korrigieren gespeichert werden.
Das aktive Standardmodell darf nur bei ausdruecklichem `qualified=true` laufen.
Fehlende oder unlesbare Qualifikation sperrt den Fototest ebenfalls. Der await im
ViewModel bleibt auf dem WPF-UI-Kontext; danach gesetzte Anzeige-Eigenschaften duerfen
nicht mit `ConfigureAwait(false)` vom UI-Thread abgekoppelt werden.

Die Schaltflaeche `Foto allgemein mit KI pruefen` ist davon getrennt. Sie ruft ueber
`AnnotationWorkbenchService.SuggestPhotoAsync` den zentralen `IProtocolAiService`
mit dem ganzen Foto und dem aktiven VSA-Codekatalog auf. Der kataloggepruefte
Qwen-/KB-Vorschlag wird nur angezeigt und muss bewusst angeklickt werden. Rote
Hand-Box, SAM-Maske, bestehender Code und Beschreibung bleiben unveraendert; der
Aufruf schreibt weder Goldsamples noch KB-Daten. Der schnelle Vorschlag beim
Box-Ziehen bleibt der getrennte YOLO-Classifier-Weg. Nicht geladene Modelle und
unbekannte Klassen werden sichtbar abgewiesen statt als VSA-Code ausgegeben.
`AiInput.RequireImage` erzwingt fuer diesen Weg ein wirklich lesbares Foto; ein
reiner Text-/KB-Vorschlag ohne Bild ist verboten. Wechselt der Nutzer waehrend des
Aufrufs das Bild, wird das spaete Ergebnis verworfen.

Eine persoenlich uebernommene Auswahl aus dem VSA-Codierfenster ist dagegen eine
bewusste Handcodierung. `WorkbenchCodeSelectionMapper` uebernimmt deshalb neben
Code, Uhrlage und Stufe auch `ProtocolEntry.Beschreibung`.
`TrainingStudioViewModel.ApplyCodeSelection` ersetzt damit nur ein leeres Feld oder
den automatischen Platzhalter durch eine fertige Katalogbeschreibung mit Code;
selbst geschriebener Text bleibt erhalten. KI-Vorschlaege und direkt eingetippte
Codes erhalten weiterhin keine automatische Goldfreigabe. Rote Hand-Box, gueltige
SAM-Maske und persoenliches Akzeptieren bleiben fuer Gold immer Pflicht.

Das Training Studio zeigt den durch `PersonalGoldProgressCalculator` berechneten
Goldstand je Hauptcode mit Ziel 30-50 an und aktualisiert ihn nach jedem erfolgreichen
Speichern. Album und Fortschritt zeigen auch eigene Entwürfe und persönlich bestätigte
Reparaturfälle; sie zählen erst nach vollständiger Geometrie als Gold.
Die Schaltfläche `Segmentierung abarbeiten` lädt über `WorkbenchQueueService` eine
gezielte Reparaturliste aus demselben Sample-Bestand. Aufgenommen werden nur lesbare
eigene Bilder mit fehlender oder ungültiger SAM-Maske. Neben RLE, Maskenfläche und der
80-Prozent-Boxregel werden die gespeicherten Maskenmaße über
`TrainingImageFileProbe` gegen die echten Bildmaße geprüft. Fehlende oder unlesbare
Dateien erscheinen nicht als leere Arbeitskarten. Eine weiterhin gültige Hand-Box
steht als `WorkbenchItem.ExistingBox` bereit und startet beim Anzeigen automatisch
SAM und den getrennten Codevergleich. Ohne gültige Box zeigt die allgemeine Foto-KI
nur einen Vorschlag; der Mensch zeichnet danach selbst die Box. In dieser Liste ist
Akzeptieren ohne gültige sichtbare Maske gesperrt. Beim Nachlabeln wird das bestehende
Sample anhand seiner ID ersetzt; es entsteht kein doppelter Datensatz und es werden
keine zweiten Arbeitskopien als neue Samples angelegt. Ein Bildwechsel verwirft einen
noch laufenden Box-Lauf sicher.
Ein alter `PdfPhoto`-Entwurf wird nicht erneut angeboten, wenn exakt dieselbe
unveränderliche PDF-Referenz (Dokument-Hash, Seite, Foto), dasselbe Bild, dieselbe
Haltung und derselbe Code bereits als geometrisch gültiges `Approved`-Sample
vorliegen. Der Altentwurf bleibt zur Nachvollziehbarkeit gespeichert; nur die
Arbeitsliste blendet die erledigte Dublette aus.
Die Vorschaubild-Auswahl ist mit dem tatsaechlich bearbeiteten Bild verbunden; in
der Reparaturliste kann sie keinen noch offenen Fall ueberspringen.

Die Schaltflaeche `Goldpruefung (90)` startet eine feste persoenliche
Qualitaetspruefung mit je 15 Bildern fuer `BAB`, `BAF`, `BAI`, `BAJ`, `BBC` und
`BBF`. `GoldQualityReviewQueueUseCase` waehlt nur einzeln im freigegebenen
Exportregister enthaltene Train-/Development-Validation-Sample-IDs. Der
`GoldQualityReviewSnapshotProvider` verlangt davor einen erfolgreichen strikten
Live-Inventarlauf mit vollstaendigem Eval-Schutz; geschuetzte Bild-Hashes und
Haltungen sind ausgeschlossen. Die Auswahl bevorzugt verschiedene physische
Haltungen und verwendet kein Bild doppelt. Das unveraenderliche Sitzungsmanifest
unter `<KnowledgeRoot>\training\gold_quality_reviews` bindet Register-Hash,
Schutzfingerprint, Bild-Hash und Ausgangsbestaetigung und wird bei einem Neustart
fortgesetzt. Pro abgeschlossenem Fall entsteht zusaetzlich ein unveraenderlicher
persoenlicher Abschlussbeleg; eine blosse externe Neuspeicherung zaehlt nicht als
Pruefung. Das Training Studio zeigt vorhandene Box und gespeicherte Goldmaske zuerst
unveraendert; die KI ist nur ein Vergleich. Erst eine neu gezogene Box startet SAM
neu. Vor dem Schreiben werden der beim Laden gebundene Sample-Zeitstand und exakt
dieselben Bildbytes nochmals geprueft. Korrigierte Uhrlage und Schadensstufe werden
auch in `TrainingSample.CodeMeta` uebernommen. Ein Fall zaehlt erst nach erneutem
persoenlichem Gold-Akzeptieren und erfolgreichem Abschlussbeleg; gespeichert wird
mit derselben Sample-ID, sodass keine Dublette entsteht.
Die Metadaten-Uebernahme bestehender Samples liegt seit AP06 (30.09.2026) in
`GoldSampleAufbau` (Application/UseCases/GoldSampleSpeichern); die reine blaue
Modellvorschau liegt in `TrainingStudioViewModel.PreviewDetection.cs` und schreibt
selbst keine Gold-Daten.

Die parallele SAM-/Code-Analyse liegt als
`TrainingStudioBoxAnalysisUseCase` in der Application-Schicht. Die UI-Koordination
der Liste ist in `TrainingStudioViewModel.RepairQueue.cs` getrennt. Vor der Aufnahme
in die Arbeitsliste prueft `TrainingImageFileProbe` neben den Bildmassen auch eine
vollstaendige Dekodierung. Der Rueckgabevertrag `WorkbenchSaveResult.GoldApproved`
ist nur nach dem vollstaendigen persoenlichen Gold-Gate wahr. Training Studio und
Foto-Annotation duerfen einen gespeicherten Entwurf deshalb weder als Gold melden
noch automatisch zum naechsten Bild weiterschalten.

Bei einer neuen normalen Pruefplatzkarte bleibt das Foto nach jedem erfolgreichen
Gold-Save sichtbar. Der Benutzer waehlt ausdruecklich `Weiteres Ereignis auf diesem
Bild` oder `Bild fertig`; Pfeiltasten, Queuewechsel, Modelltest und weitere
Codieraenderungen duerfen diese Entscheidung nicht umgehen. Ein zusaetzliches
Ereignis erhaelt eine neue Hand-Box, eigene sichtbare SAM-Maske, eigenen VSA-Code
und eine eigene Sample-ID. Es wird als `ManualCoding` ohne geerbte PDF- oder
Bestandsmetadaten gespeichert, aber per SHA-256 an exakt dieselben Bildbytes
gebunden. Der Pruefplatz setzt solche Zusatzereignisse bewusst als Punktbefund bei
`MeterStart`; ein zweiter Streckenschaden mit eigener Von-Bis-Strecke gehoert in
einen dafuer erweiterten Fachdialog und darf hier nicht geraten werden.
Mehrere Operateurbefunde desselben PDF-Fotos bleiben zuerst als getrennte
`WorkbenchItem`s zusammen und werden vollstaendig geprueft, bevor der Dialog ein
weiteres manuelles Ereignis anbietet. Ein noch nicht goldfaehiger Save wird mit
seiner zurueckgegebenen Sample-ID und dem gespeicherten Bildhash an dieselbe
Arbeitskarte gebunden; die Korrektur ersetzt diesen Draft. Abgeschlossene Karten
sind in der aktuellen Warteschlange gesperrt und koennen weder erneut gespeichert
noch doppelt gezaehlt werden. Vorhandene Reparatur- und Goldpruefungsfaelle bleiben
weiterhin Einzelfall-Queues.
`TrainingStudioBoxAnalysisUseCase.ValidateSegmentation` liefert der UI den exakten
Grund fuer eine Ablehnung. Eine formal sichtbare, aber noch nicht goldfaehige Maske
wird im Training Studio orange statt gruen gezeichnet und meldet zum Beispiel den
echten Pixelanteil innerhalb der Hand-Box. SAM-Masken werden dafuer nicht still auf
die Box beschnitten; die 80-Prozent-Schranke bleibt fail-closed.

Die Schaltflaeche `Goldalbum` oeffnet `PersonalGoldAlbumWindow`. Das Fenster liest
ueber `IPersonalGoldAlbumService` ausschliesslich persoenlich bestaetigte Handlabels,
gruppiert sie nach Hauptcode und zeigt Bild, Code, Beschreibung, Datei- und
Geometriestatus. Es ist rein lesend und veraendert weder Bilder noch Trainingsdaten.

Neue Bilder koennen unter `<KnowledgeRoot>\training\gold_inbox` vorbereitet werden.
`PersonalGoldInboxFileService` legt die Hauptcode-Unterordner mit Code und Klartext,
zum Beispiel `BAB - Riss` und `BCA - Seitlicher Anschluss`, sowie
`_OHNE_ZUORDNUNG` sowie `_ERLEDIGT` an. Es liest nur JPG/JPEG/PNG aus der Wurzel
und der ersten Ordnerebene; alte reine Codeordner wie `BAB` bleiben lesbar,
`_ERLEDIGT` wird uebersprungen,
und folgt keinen Datei- oder Ordnerverknuepfungen. `Gold-Eingang oeffnen` zeigt den
Ordner, `Eingang laden` uebergibt den Stapel an den vorhandenen Pruefplatz. Der
Ordnername ist nur ein sichtbarer Hauptcode-Hinweis und wird nie automatisch als
finaler VSA-Code akzeptiert. Eingangsdateien bleiben unveraendert. Erst Codieren,
BBox, SAM-Segmentierung und persoenliches Akzeptieren erzeugen das Goldsample und
die inhaltsadressierte Kopie unter
`gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>`.

Goldstand, Goldalbum und Ordnerhinweis zeigen Hauptcodes ebenfalls mit Klartext.
Der nicht als Basiscode vorhandene BBD-Anker wird dabei fachlich als
`BBD - Eindringender Boden` bezeichnet und nicht mit der allgemeinen BB-Gruppe.

Beim Bestaetigen legt `AnnotationWorkbenchService` das unveraenderte Bild zuerst
inhaltsadressiert unter
`<KnowledgeRoot>\gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>` ab.
Der endgueltig gespeicherte Code bestimmt den Ordner; das gilt auch nach einer
persoenlichen Korrektur eines KI-Vorschlags.
Das Kundenoriginal bleibt unberuehrt. Scheitert die sichere Goldkopie, wird
nichts gespeichert. `TrainingFrameFileStore` prueft bestehende und neue Bildbytes;
eine beschaedigte alte Zieldatei wird nicht als Treffer akzeptiert, sondern durch
eine gepruefte atomare Kopie ersetzt.

Der Speicherweg trennt seit 2026-07-25 streng zwischen Entwurf und Gold
(„Gold-Wahrheit"). Vor dem Schreiben lehnt `GoldBeschreibungGuard`
Platzhalter-Texte („Ausmass ergaenzen") ab, und `SamMaskValidator`
(Infrastructure, neben `SamMaskDecoder`) prueft die Maske: nicht `Degraded`,
RLE strikt dekodierbar (Laufsumme = Breite x Hoehe), mindestens ein gesetztes
Pixel und mindestens 80 % aller Maskenpixel-Mittelpunkte innerhalb der Hand-Box.
Maskendimensionen müssen den echten Pixelmassen des Goldbilds entsprechen; die
Maskenfläche wird aus der RLE abgeleitet und nicht aus Sidecar-Metadaten vertraut.
Gerade und ungerade RLE-Tokenzahlen sind erlaubt, weil der echte Sidecar-Encoder
keinen kuenstlichen Abschlussrun anhaengt; Startwert und Runs bleiben streng.
Nur mit gueltiger Maske entsteht ein
Goldsample (`Status = Approved`, Gruen) mit KB-Index und Teacher-Eintrag; die
Teacher-Annotation traegt dabei `SourceSampleId` als Fremdschluessel. Ohne
gueltige Maske wird nur ein Entwurf (`TrainingSampleStatus.Draft`, Gelb, kein
KB-/Teacher-Eintrag) gespeichert, der in der Warteschlange „Unvollstaendige
Goldframes" zur Reparatur erscheint; das Nachlabeln mit Maske fuehrt ueber
denselben Weg zum Goldsample. Zusaetzlich verlangt
`KnowledgeBaseManager.IsIndexWorthy` die vollstaendige persoenliche
`ManualGoldTrainingPolicy`, Box, Maske und einen fertigen Text. Platzhalter duerfen
fuer historischen reinen YOLO-BBox-Export weiterverwendet werden, gelangen aber
weder beim Neuindexieren noch aus vorhandenen KB-Zeilen ins Qwen-Retrieval.
Damit werden Entwuerfe, fremde/alte Auto-Freigaben und unfertige Texte auch bei
Nachhol-/Rebuild-Laeufen gesperrt. Das Akzeptieren ist waehrend
eines laufenden SAM-Laufs sowie bei bereits laufendem Speichern gesperrt
(ViewModel-Flags).

Die Sample-Identitaet ist die `SampleId`: `MergeOrUpdateAsync` matcht zuerst
per Id, erst danach per Signatur (Alt-Aufrufer). Eine Codekorrektur an einem
Bestandssample ersetzt den Eintrag atomar ueber
`ITrainingSampleStore.ReplaceBySampleIdAsync` (ein Sperrvorgang, ein Schreiben)
und bereinigt den alten Stand: KB-Deindex ist produktiv verdrahtet
(`TrainingKnowledgeBaseSampleDeindexer`, kein No-op), alte Teacher-Eintraege
werden per `SourceSampleId` entfernt (Mehrdeutigkeit im Altbestand → Warnung
statt Loeschen). `MergeAndSaveAsync` dedupliziert per Signatur als Sperre
gegen versehentliches Doppel-Akzeptieren; der Neuanlage-Pfad nutzt
`TryAddNewAsync`, das eine uebersprungene Dublette sichtbar abweist statt
still fortzufahren (fruehere KB-Waisen entstanden genau so).

Seit 30.09.2026 (Wartbarkeit AP06) steuert `GoldSampleSpeichernUseCase` unter
`Application/UseCases/GoldSampleSpeichern/` diesen Speicherweg; beide
`AnnotationWorkbenchService.SaveAsync`-Ueberladungen delegieren nur noch dorthin.
Phasen: Eingaben pruefen, Herkunft pruefen (`GoldSampleHerkunft`), Bild binden mit
Eval-Schutz und Goldkopie, Sample dauerhaft speichern (`GoldSampleAblage`), KB-Nachtrag
und Teacher-Nachlauf (`GoldSampleNachlauf`), gemeinsames Ergebnis. Die Metadaten-
Uebernahme aus `AnnotationWorkbenchService.SampleMapping.cs` liegt jetzt in
`GoldSampleAufbau`. Die Grenze "dauerhaft gespeichert" ist der Typ
`DauerhaftGespeichertesGoldSample`: Vorher fuehrt jeder Fehler zu "nicht gespeichert" und
ein Abbruch wird weitergeworfen; danach entsteht nur noch ein Ergebnis mit `Saved=true`,
KB- und Teacher-Fehler (auch Abbruch) erscheinen als Warnung. Eval-Schutzdaten laden,
strenge Maskenpruefung (`WorkbenchGoldMask`/`SamMaskValidator`) und den WPF-Teacher-Export
reicht der UI-Dienst als Delegates hinein. Tests: `GoldSampleSpeichernUseCaseTests`
(Pipeline.Tests) und `AnnotationWorkbenchServiceTests`.

Seit 01.10.2026 meldet `GoldSampleAblage` einen Schreibfehler in allen drei Wegen
(Neuanlage, Nachlabeln, Ersatz bei geaendertem Code) als dieselbe Ablehnung
«Goldsample konnte nicht gespeichert werden: …»; vorher kam er bei der Neuanlage als
Ausnahme beim Aufrufer an. Gefangen wird wie in den Reparaturwegen jede Ausnahme ausser
`OperationCanceledException` (Abbruch wird weitergeworfen). Ein halbes Sample bleibt nicht
liegen, weil der Store ueber Temp-Datei und atomares Ersetzen schreibt; die
inhaltsadressierte Goldkopie bleibt wie in den Reparaturwegen stehen (kann einem anderen
Sample gehoeren). Die Ablehnung «Die PDF-Herkunft konnte nicht eindeutig gebunden werden»
in `GoldSampleHerkunft` greift heute nie (ohne Bestand und mit PDF-Vorschlag ist die
Herkunft immer `PdfPhoto`), bleibt aber als kommentierte Doppelsicherung stehen.

Mehrfachobjekte werden seit 2026-07-25 unterstuetzt: Neue Samples bauen ihre
Signatur mit Box als `caseId|code|meter|meter|b:x,y,w,h` (normalisiert, 3
Dezimalstellen). Zwei Schaeden mit gleichem Code am selben Meter, aber
verschiedenen Boxen, werden dadurch als zwei eigenstaendige Objekte mit
eigener SampleId, KB- und Teacher-Eintrag gespeichert; ein erneutes
Akzeptieren desselben Objekts (gleiche Box) wird weiterhin entdoppelt.
Altbestand mit 4-teiliger Signatur (ohne Box) bleibt gueltig.
Der Player-Codiermodus prueft Masken mit demselben strengen Format
(`SamMaskFormatValidator` in Application; `SamMaskValidator` in Infrastructure
delegiert dorthin und ergaenzt Degraded/Dekodierung/Box-Schnitt); ungueltige
Masken werden nicht uebernommen, das Sample bleibt sichtbar unvollstaendig.
Bei einer manuellen Rechteckmarkierung liest der Player vor der Eingabe das native
Video-Seitenverhaeltnis samt Pixel-Seitenverhaeltnis und Ausrichtung aus LibVLC,
damit Box und Maske auch bei Letterbox/Pillarbox und alten PAL-Videos auf demselben
Bildausschnitt liegen. Nach dem Loslassen wird die Box mit SAM
segmentiert und die echte Maske drei Sekunden angezeigt, bevor sich das
VSA-Codierfenster oeffnet. Das Bogen-Geometriesignal darf diese Vorschau nie durch
ein Oval ersetzen. Die bestaetigte Maske bleibt als `OverlayGeometry.SamMask` am
manuellen Ereignis erhalten und wird ohne erfundenen KI-Kontext streng geprueft in
das Trainingssample uebernommen. Schlaegt eine erneute Segmentierung fehl, wird eine
vorherige Maske entfernt und kann nicht als aktuelles Ergebnis gespeichert werden.

Eine zweite, bewusst getrennte Handmarkierung lebt im Foto-Assistenten einer bereits
geöffneten VSA-Beobachtung. `PhotoAnnotationUseCase` liest das unveränderte
Originalfoto vor und nach SAM, vergleicht den SHA-256 und bindet danach eine private
Byte-Momentaufnahme fest an Box und Maske. Das Foto-Fenster zeigt die echte Maske auf
einer eigenen Vorschau-Ebene; der Overlay-Export enthält sie nicht. Erst eine
zusätzliche sichtbare Goldbestätigung im VSA-Fenster ruft den geschützten
`AnnotationWorkbenchService.SaveAsync` mit dem finalen Code auf. Eval-Hashprüfung
und `StoreBytesAsync` verwenden dabei exakt dieselbe Momentaufnahme; der
veränderbare Originalpfad wird beim Speichern nicht erneut gelesen.
Damit bleiben Eval-Schutz, inhaltsadressierte Goldkopie, Maskenprüfung,
Dublettschutz, KB-Index und Teacher-Eintrag zentral. Dieser Foto-Weg ist der einzige
Persistenzbesitzer seiner Maske und hängt sie nicht zusätzlich an das Coding-Ereignis.
`ProtocolEntry.OriginalFotoPaths` hält dabei je Fotoslot die unveränderte Quelle,
während `FotoPaths` das vermessene Anzeigebild enthalten darf. Altprotokolle ohne
dieses additive Feld übernehmen beim ersten VSA-Laden ihren bisherigen Fotopfad als
Original; eine neue Videoaufnahme setzt beide Listen auf den neuen Frame.
`PhotoAnnotationBatchSaveUseCase` prueft bei mehreren Fotos zuerst das gesamte
eingefrorene Paket. Scheitert ein spaeterer externer Speicherschritt nach einem
Teilerfolg, wird genau der vorher eingefrorene Protokolleintrag uebernommen und mit
den bereits geschriebenen Sample-IDs verknuepft; eine nachtraegliche Umcodierung
oder ein Abbruch kann das Goldsample dadurch nicht verwaisen lassen.
`PhotoAnnotationBatchSaveUseCase` prueft bei mehreren Fotos zuerst das gesamte
eingefrorene Paket. Scheitert ein spaeterer externer Speicherschritt nach einem
Teilerfolg, wird genau der vorher eingefrorene Protokolleintrag uebernommen und mit
den bereits geschriebenen Sample-IDs verknuepft; eine nachtraegliche Umcodierung
oder ein Abbruch kann das Goldsample dadurch nicht verwaisen lassen.
Der finale Eintrag gibt auch `IsStreckenschaden` an das Goldsample weiter. Das
automatisch erzeugte Ende eines Streckenschadens erhält deshalb weiterhin weder Foto
noch Overlay, SAM-Maske oder die Trainings-Sprungmarkierung des Anfangs.
Bei einem noch offenen Streckenschaden repraesentiert das Goldfoto nur den
Startpunkt (`MeterEnd = MeterStart`); das spaetere Ende aktualisiert dieses
Bildsample nicht und erzeugt bewusst kein zweites Bildsample.
Bei einem noch offenen Streckenschaden repraesentiert das Goldfoto nur den
Startpunkt (`MeterEnd = MeterStart`); das spaetere Ende aktualisiert dieses
Bildsample nicht und erzeugt bewusst kein zweites Bildsample.

Das additive `ProtocolEntry.Training` dokumentiert separat erzeugte
`PhotoAnnotationSampleIds`. Bei `SkipAutomaticPersistence=true` ueberspringt
`CodingTrainingSamplePersistenceCoordinator` diesen Eintrag sowohl einzeln als auch
beim Session-Abschluss, damit kein zweites Goldsample entsteht. Die allgemeinen
Protokoll- und Coding-Kopierwege klonen diese Metadaten samt ID-Liste tief.

Persoenliche Entscheidungen im Player-Codiermodus verwenden denselben Goldspeicher.
`CodingEventToSampleMapper` markiert nur `Accepted` oder `AcceptedWithEdit` mit
gesetztem Benutzer und Bestaetigungszeitpunkt als `ManualCoding` sowie
`ReviewApproved`/`ReviewCorrected`. `CodingTrainingSamplePersistenceCoordinator`
prueft zuerst den Eval-Schutz, kopiert vorhandene Fotos oder den bestaetigten
Player-Frame inhaltsadressiert in den Klartext-Hauptcode-Unterordner von
`gold_frames` und speichert danach
`training_samples.json` und den KB-Status. BBox und vorhandene SAM-RLE-Daten werden
aus dem Coding-Ereignis uebernommen. Fehlt Bild, Box oder SAM, bleibt der Eintrag
sichtbar unvollstaendig und darf nicht in den Trainings-Export.
Auch die Stapelspeicherung liefert ein echtes Ergebnis zurueck. Ein Fehler wird im
Player als rotes Overlay „Training nicht gespeichert" angezeigt und nicht mehr nur
im Hintergrundprotokoll versteckt.
Auch `CodingSessionService` indexiert aus diesem Weg nur strikt persoenlich
bestaetigte Goldsamples mit vorhandenem Goldbild. Der allgemeine Session-Abschluss
darf weder fremde Freigaben aufnehmen noch persoenliche Gold-Metadaten ueberschreiben.

`tools/PersonalGoldMigration` uebernimmt bestehende persoenliche Handlabels
wiederholbar in dieselben Klartext-Hauptcode-Unterordner. Vor dem Umschalten werden alle Quelldateien
geprueft; SQLite und `training_samples.json` werden bei einem Fehler zurueckgesetzt.
Nach erfolgreicher Umstellung wird auch das Gold-Gehirn-Dateimanifest erneuert.
Die nachvollziehbare Verteilung liegt unter
`<KnowledgeRoot>\training\gold_standard\main_code_inventory_v1.json`, die Pruefspuren
unter `<KnowledgeRoot>\training\gold_migrations`. Wissens-ZIP-Sicherungen enthalten
`gold_frames` rekursiv; Kundenoriginale werden nie veraendert.

`tools/GoldBrainSeparation` trennt einen vorhandenen Mischbestand einmalig vom neuen
Gold-Gehirn. Ohne `--execute` wird nur geprueft. Im Ausfuehrungsmodus baut
`PersonalGoldBrainSeparationService` zuerst einen vollstaendigen Arbeitsstand auf,
prueft JSON, Frames und SQLite feldgenau und benennt erst dann die Ordner auf demselben
Datentraeger atomar um. Alte absolute Goldbildpfade unter der bisherigen Wissenswurzel
werden dabei sicher auf das Lokalarchiv abgebildet; externe Bildpfade bleiben
unveraendert. Ueberlappende Wissens-, Archiv-, Spiegel-, Staging- oder
Legacy-Protokollpfade sperren den Lauf vor dem Commit.
Vor der ersten Umbenennung wird neben der Wissenswurzel das atomare Journal
`<KnowledgeRoot>.gold-brain-separation.commit.json` geschrieben. Ein spaeterer
Ausfuehrungslauf setzt einen unterbrochenen Commit nur anhand kanonisch gebundener
Pfade, Besitzmarker und gepruefter Vorherzustaende auf den Ausgangsstand zurueck;
ein Dry-Run meldet das
offene Journal nur und veraendert nichts. Die Fassade bleibt klein: Input-/
Pfadpruefung, Arbeitsstand, Journal, Commit und Recovery liegen in getrennten
internen Diensten. Der komplette lokale Altstand bleibt als
`<KnowledgeRoot>_ALT_<Zeitstempel>` erhalten; der bisherige Elements-Spiegel liegt
unter `<Elements>\Brain_Archiv\KI_BRAIN_ALT_<Zeitstempel>`. Das neue aktive Gehirn
enthaelt nur persoenlich bestaetigte Handlabels und deren Embeddings. Teacher- und
Protokoll-Kontext starten leer. Der Pruefbeleg und ein Dateimanifest liegen unter
`<KnowledgeRoot>\training\gold_standard`. Altarchive tragen einen Schutzmarker und
duerfen nicht wieder als aktive Wissenswurzel angeschlossen werden.
Nach dem Umschalten prueft `PersonalGoldArchiveRecoveryService`, ob persoenlich
bestaetigte `ManualCoding`-Faelle nur noch in der archivierten SQLite-KB stehen.
Nur solche Faelle mit vorhandenem Bild und Embedding werden inhaltsadressiert
nachgeholt. Alte `TeacherAnnotation`- und `VideoTimestamp`-Zeilen werden dadurch
nicht zu Hand-Gold umgedeutet. Vor der ersten Mutation liegt das atomare Journal
`<KnowledgeRoot>.gold-archive-recovery.transaction.json` samt geprueften
Vorherkopien fuer SQLite, Trainings-JSON, Inventar, Beleg und Manifest vor. Ein
Neustart setzt diese Dateien und neu angelegte Frames idempotent zurueck. Fremde
Audit-Artefakte, Hashabweichungen, unsichere Pfade oder Junctions werden niemals
geloescht, sondern sperren die automatische Recovery zur manuellen Pruefung. Das
Journal wird erst nach dem vollstaendigen neuen Manifest entfernt. Der Nachholbeleg
`gold_brain_archive_recovery_v1.json` dokumentiert IDs und neue Goldpfade.

Die Maus-/Bildabbildung des Pruefplatzes liegt im reinen
`TrainingStudioImageGeometryMapper`. Er beruecksichtigt die tatsaechliche Lage des
`Image` im Overlay, freie Raender durch `Uniform`-Darstellung und begrenzt das Ziehen
bereits sichtbar am Bildrand. Eine Auswahl darf nur im sichtbaren Bild beginnen.
Beim Beginn einer neuen Box entfernt das ViewModel die alte Maske und den alten
Vorschlag sofort; eine alte Maske darf nie zusammen mit einer neuen Box erscheinen.

## Aktive Few-Shot-Wege

- Produktiv gibt es zwei Laufzeit-Kontextwege. Beide liefern Prompt-Beispiele und
  trainieren keine Qwen-Modellgewichte.
- Aehnliche bestaetigte Faelle kommen aus `KnowledgeBase.db` ueber `RetrievalService`.
- Freigegebene Protokolleintraege kommen getrennt aus
  `<KnowledgeRoot>\protocol_training.json`
  ueber `ProtocolTrainingFileStore`.
- Der fruehere bildbasierte `FewShotExampleStore` samt Builder und der Schaltflaeche
  `Zu FewShot` ist entfernt: Er schrieb Bilder, wurde aber von keinem KI-Prompt gelesen.
- Bestehende `fewshot_examples.json` und `fewshot_images` sind Legacy-Daten. Sie werden
  nicht veraendert und bleiben fuer alte Wissenssicherungen im Dateikatalog enthalten.
  Diese Dateien nie wieder als Prompt- oder Trainingsquelle anschliessen.

## Schutz persistenter KI-Dateien

`TeacherAnnotationFileStore`, `ProtocolTrainingFileStore` und
`AiOptimizationSessionFileStore` unterscheiden strikt zwischen Erstlauf und
Lesefehler: Eine fehlende Datei bedeutet leer; eine vorhandene, aber unlesbare oder
strukturell ungueltige JSON-Datei bricht den Vorgang ab. Danach wird nichts
gespeichert und der vorhandene Bestand bleibt unveraendert. Der Teacher-Store legt
bei ungueltigem JSON zusaetzlich eine `.corrupt`-Kopie zur Beweissicherung an.

`tools/SelfTrainingHarness` startet nicht, solange `SewerStudio.exe` laeuft. Vor
einem Harness-Lauf wird der Trainings-Store bytegenau gesichert. Die automatische
Wiederherstellung erfolgt nur, wenn SewerStudio auch waehrend und nach dem Lauf
nicht beobachtet wurde und der letzte Harness-Stand weiterhin denselben SHA-256
besitzt. Bei einer parallelen Aenderung bleibt der aktuelle Store unangetastet und
die eindeutige Harness-Sicherung fuer die manuelle Pruefung erhalten.

## Eval-Schutz: eine Regel an allen Lesewegen (2026-10-02)

Deepscan 02.10.2026 (A1/R2), Entscheid Pascal E1 «sperren». Vorher hatte «Ordner fehlt»
drei Bedeutungen: Der Gold-Speicher (`TrainingSampleFileStore`) liess bei fehlendem,
leerem oder unlesbarem Pruefdaten-Ordner alle Samples ungefiltert durch, und ein
unlesbarer Unterordner beendete in `EvalContaminationGuard.EnumerateEvalSetRoots` still
die Suche (weitere Saetze wie `v2` fehlten, auch im «strengen» Weg).

Jetzt liest `EvalProtectionSetReader.LoadStrict` (Application/Ai/Training) fuer
Gold-Speicher, Wissenssuche (`GuardedRetrievalFactory.Sperrliste` ueber
`EvalContaminationGuard.LoadEvalHaltungKeysStrict`) und die UI-Lader
(`EvalContaminationSetProvider`). Regel an allen drei Wegen:

| Fall | Ergebnis |
| --- | --- |
| Eintrag bewusst leer (`""`/Leerzeichen) | Schutz aus — die einzige Abschaltung |
| Ordner fehlt | `DirectoryNotFoundException` |
| Ordner selbst Verknuepfung, Unterordner unlesbar oder Verknuepfung/Junction | `IOException` (nie betreten, nie still weglassen) |
| `_manifest.json`/`_candidates.json` unlesbar oder ungueltig, Kandidatenliste leer, Kandidat ohne gueltige Haltungskennung | `InvalidDataException` |
| Keine einzige Haltungskennung (auch: nur Bildhashes) | `InvalidDataException` |

- Der Gold-Speicher wandelt diese Fehler in eine `InvalidOperationException` mit
  Ordnerpfad und Hinweis auf die Einstellungen; `UserError` zeigt sie unveraendert.
  Gesperrt sind `SaveAsync`, `MergeAndSaveAsync`, `TryAddNewAsync`, `MergeOrUpdateAsync`,
  `ReplaceBySampleIdAsync` (lehnt zusaetzlich ein Sample aus einer Pruefhaltung ab, statt
  es wie bisher ungeprueft zu ersetzen) und auch `LoadAsync` (kein ungefilterter Bestand).
  `RemoveBySampleIdAsync` bleibt erlaubt (schreibt nichts Neues). Der Schutz wird je
  Vorgang VOR dem ersten Dateizugriff geladen (Review PR #69): Laden kann sonst eine
  Signatur-Migration zurueckschreiben oder eine Rettungskopie (`.bad_*`) anlegen; bei
  gesperrtem Schutz bleibt der Ordner byte-gleich. Auch ein leeres `SaveAsync` sperrt.
- `ConfigureEvalProtection(null)` heisst «nicht konfiguriert» (Umgebungsvariable
  `SEWERSTUDIO_EVAL_SET_ROOT`, sonst `C:\KI_BRAIN\eval_set`); fehlt dieser Ordner, sperrt
  der Speicher. Ein leerer Eintrag schaltet ab, `EffectiveEvalSetRoot` ist dann `""`.
- Programmstart und Codiermodus blockieren nicht: Die Sperre wirkt erst beim Laden/Speichern
  von Samples. Die Wissenssuche meldet beim Start wie bisher «ohne Vergleichswissen»; der
  Codiermodus sperrt ueber `CodingTrainingSampleEvalProtector` (bisher nur Logzeile).
- Die milden Lader `LoadEvalImageHashes`/`LoadEvalHaltungKeys` bleiben fuer Mess- und
  Werkzeugwege (`EvalSetV2Builder`, `tools/ClassifierDatasetBuilder`); sie ueberspringen
  einen unlesbaren Ordner einzeln (Protokollzeile) statt die ganze Suche abzubrechen.
- Der Inventar-Leser des YOLO-Exports (`TrainingInventoryEvalProtectionReader`) bleibt
  unveraendert und strenger (eingefrorenes Manifest, Kandidaten-Hash, Bildabgleich); er
  kennt keine Abschaltung: ohne Eintrag kein Export.
- Tests: `EvalSchutzLesewegeTests` (Infrastructure.Tests, Tabellentest je Fall fuer Leser,
  Wissenssuche und Speicher, ACL- und Junction-Fall), `EvalContaminationSetProviderTests`.
  Tests ohne echte Pruefdaten nutzen `EvalSchutzTestOrdner.Anlegen` (gueltiger Ordner mit
  einer unbenutzten Haltung) statt eines fehlenden Ordners.

## Ereignisbasierte Eval-Messung (AP 0.4a, technische Grundlage)

Die fruehere Sammeldatei `EvalSetBenchmark.cs` ist entfernt. Dataset-Laden,
Benchmark-Scoring, YOLO-Baseline, Router-Plan, Klassen-Mapping, Coverage, Kontext und
CSV-Helfer liegen jeweils in einer gleichnamigen eigenen Datei. Die oeffentlichen
Klassennamen und Signaturen sind unveraendert. Verhaltenstests sichern alle sieben
CSV-/JSON-Ausgaben, inklusive Kopfzeilen und Escaping.

- `EvalSetBenchmarkCase` traegt additiv `HoldingKey`, `ExpectedSeverity`, `EventId`
  sowie den optionalen Bereich `MeterStart`/`MeterEnd`. Alte Eval-Sets bleiben ueber
  `EvalSetBenchmarkDataset.Load` lesbar.
- Ein Release-Kandidat muss stattdessen durch
  `EvalSetReleaseDatasetValidator.LoadAndValidate`. Fehlende Bilder oder
  Haltungskennungen, bei Schaeden fehlende Ereignis-IDs, ungueltige Severity und
  widerspruechliche Meterbereiche stoppen. Nicht-Schaeden brauchen keine kuenstliche
  Ereignis-ID.
- `EvalSetV2Builder` uebernimmt die neuen Felder und verlangt Severity sowie
  Ereignis-ID fuer Schadensfaelle.
- `EvalSetEventScorer` zaehlt ein Schadensereignis ueber mehrere Frames nur einmal.
  Der Schluessel besteht aus Haltung plus EventId; gleiche EventIds in verschiedenen
  Haltungen bleiben deshalb unabhaengige Ereignisse.
  Detect-Treffer und nachgelagertes Gate werden getrennt ausgewiesen. Fuer Severity
  4/5 gilt ein Mindestumfang von 20 unabhaengigen Ereignissen; Wilson- und exakte
  95-Prozent-Fehlergrenzen werden mit ausgegeben.
- Das vorhandene 120er-Set ist noch nicht menschlich mit Severity und EventId
  nachgepflegt. AP 0.4 ist deshalb nicht abgeschlossen und keine Modellfreigabe darf
  allein aus der neuen technischen Messlogik abgeleitet werden.
- `tools/EvalVisibilityReview/start_eval_metadata_review.ps1` öffnet dafür einen
  lokalen Bild-Prüfplatz. Er zeigt nur BA-/BB-Schadensframes, schreibt Stufe,
  Ereignis-ID und optionalen Meterbereich atomar nach
  `C:\KI_BRAIN\eval_review\v1_event_metadata_review.json` und verändert das
  eingefrorene Eval-Set nie. Ein Zwischenstand wird nur bei gleicher SHA-256 der
  ursprünglichen `_candidates.json` fortgesetzt. Der Prüfplatz zeigt Code und
  Klartext aus dem aktiven VSA-Katalog. Die Stufe verändert weder Code noch
  Zustandsklasse; nur Ereignisse der Stufen 4/5 werden zusätzlich als wichtige
  Fälle ausgewertet. Pro Bild wird zuerst bestätigt, korrigiert oder festgehalten,
  dass kein passender BA-/BB-Schaden sichtbar ist. Korrekturen müssen aus dem
  aktiven Katalog stammen; Ausschlüsse brauchen keine Stufe oder Ereignis-ID.
  Widersprüchlich wiederverwendete Ereignis-IDs bleiben sichtbar offen.

- `EvalReviewedDamageDataset` bindet diese getrennte Review nur bei passendem
  `_candidates.json`-SHA-256, vollstaendigen Entscheidungen und null Konflikten an
  den Benchmark. `EvalReviewedDamageScorer` misst Schadenspraesenz, Fehlalarme,
  exakten Code, Hauptcode, Stufe und Ereignisse. `EvalSetBenchmark --review-file`
  verwendet dafuer das Ollama-Bildmodell ohne YOLO-/DINO-/SAM-Hinweise; das
  QualityGate wird in diesem Modus ausdruecklich nicht als gemessen ausgegeben.
- `EvalSetBenchmark --review-file <Datei> --full-chain` fuehrt dieselben geprueften
  Bilder durch den produktiven DINO -> SAM -> Qwen-Bildanalyse ->
  Text-Code-Mapping -> QualityGate-Weg. Ein fail-closed Client sperrt dabei sowohl
  YOLO-Detect als auch YOLO-cls; der KB-Kontext bleibt ebenfalls ausgeschaltet.
  Der ausdrueckliche Pruefbefehl aktiviert nur fuer diesen Lauf das Code-Mapping,
  auch wenn der allgemeine App-KI-Schalter aus ist. CSV und JSON weisen die
  erreichten Stufen, technische Fehler, exakte numerische Stufe, QualityGate sowie
  Erkennung und gruenes Gate je Ereignis getrennt aus.
- `RawVideoDetection.SeverityLevel` traegt additiv die exakte Stufe 1-5 aus dem
  `TemporalFindingDeduplicator`. Das bestehende Textfeld `Severity` bleibt fuer
  Anzeige und Kompatibilitaet unveraendert.
- Eval-Schutz der Negativsatz-Bilder im Python-Leser (2026-10-01): `read_training_negative_sources` verhaelt sich wie der C#-Export (`TrainingExportPlanInputBuilder`, `EvalContaminationGuard.IsEvalHaltung`): Ein Negativbild aus einer Eval-Haltung stoppt den ganzen Satz (nicht nur das Bild); die Meldung nennt jedes Bild mit Haltung und Split. Schluessel: `haltung_key` aller `_candidates.json` unter `eval_set`, normalisiert wie `NormalizeHaltungKey`, beide Richtungen, Gross-/Kleinschreibung egal (`negativsatz_pruefung.eval_haltungsschluessel`, `ist_eval_haltung`, `pruefe_negativbilder_gegen_eval`; Leser `_lade_eval_schutz_haltungen`). Fail-closed wie C#: fehlender Eval-Ordner, fehlende, defekte oder leere Kandidatenliste oder ein Eintrag ohne gueltigen `haltung_key` stoppt mit «Der Eval-Schutz ist nicht vollstaendig lesbar». Nicht nachgebildet: Manifest-Hash der Eval-Sets. Wirkung: `proto_hn_fefb59779b86` wird abgelehnt (1 von 286 Bildern, Haltung 34738-34741, train); beide BCC-Saetze bleiben angenommen. Der Satz braucht eine neue Ableitung ohne dieses Bild.
- Nachtrag Eval-Schutz Negativsaetze (2026-10-01): Jede leere `_candidates.json` unter der Eval-Wurzel stoppt den Leser (wie C# je Set). `build_audit` liest den Eval-Schutz der Negativsaetze aus der Wurzel von `--eval-images` (Eltern von `images`), nicht fest aus `<KnowledgeRoot>/eval_set`.
- Proto-Negativsatz neu aufgebaut (2026-10-02, Freigabe Pascal): `proto_hn_e3e08b655e7d` (set_id `e3e08b655e7d...`), 285 Bilder (228 train, 57 val), aus derselben Queue `proto_hn_77a38be00f3c` und demselben Review mit `proto_hard_negative_review.py --publish-set`; kein neues Review. Der Satzbau prueft den heutigen Eval-Schutz und fuehrt `proto-hn-5d70d3e6b91e9d209159` (Haltung 34738-34741, erst am 06.08. in die Eval-Liste gekommen) als `excluded_eval_protected`; kein anderes Bild wechselt den Split, beide Gold-Ausrichtungen bleiben. Der alte Satz `proto_hn_fefb59779b86` bleibt unveraendert liegen und wird vom Leser abgelehnt. Offen: `export_registry_v1.json`/`registry_setup_v1.json` binden noch den alten Satz; der Wechsel geschieht mit dem naechsten Gold-Audit (`--negative-set <neu> --approved-by Besitzer`) und `prepare_detect_gold`.

### Tests der Gold-Schreibskripte (02.10.2026)

`import_gold_labels.py`, `remove_eval_contaminated_from_register.py` und `repair_inbox_gold_holding_ids.py` haben Tests unter `training/scripts/tests/` (synthetische Daten im Temp-Ordner, laufen in der CI). Sie halten fest: Standardlauf bzw. Vorschau schreibfrei, Sicherung vor dem Schreiben, Eval-Haltung (beide Richtungen) und Eval-Bild-Hash werden nicht ins Training uebernommen, harte Sperren bei laufendem Programm, paralleler Aenderung und Kollisionen (`import_gold_labels.py` haelt den gelesenen Stand der `training_samples.json` fest und schreibt nicht, wenn sie sich bis dahin geaendert hat; fehlende Datei wird als GESPERRT gemeldet), Rueckrollen bei Schreibfehlern. Wer eine dieser Schutzzeilen aendert, muss einen roten Test begruenden.

## Training Center: Verteilung und Scan (03.10.2026, Deepscan R6/R8c)

- **Ausserhalb des UI-Threads, mit Abbruch:** `TrainingCenterImportService.DistributeByHaltungAsync`
  und `ScanAsync` laufen per `Task.Run` und nehmen ein Abbruch-Token an. Geprueft wird vor dem
  PDF-Lesen und vor jeder Haltung bzw. vor jedem Ordner. Das Fenster reicht das Token des
  vorhandenen «Abbrechen»-Knopfs durch (`ResetGenerationCancellation`, erst nach der
  Busy-Pruefung). Ein Abbruch meldet «Verteilung abgebrochen.» bzw. «Scan abgebrochen.», kein
  Fehler; ein abgebrochener Scan wird nicht gespeichert.
- **Pfadwaechter:** Die Verteilung schreibt nach `<Eltern des PDF-Ordners>\<PDF-Name>_Training`,
  also neben die Kundenablage. Vor jedem Schreiben gilt `DistributionWritePathGuard` mit dem
  Ausgabeordner als Wurzel: Ausgabeordner, Haltungsordner, Protokoll-JSON und `.link`-Datei.
  Ein verknuepfter Ausgabeordner wird ohne Schreiben abgelehnt («Ausgabeordner … wird nicht
  beschrieben: Er ist eine Verknüpfung …»), ein verknuepfter Haltungsordner wird uebersprungen
  und benannt. Weil der Waechter nur ab dem Ausgabeordner abwaerts prueft, prueft die Verteilung
  vorher den ganzen Pfad bis zum Laufwerk (`VerknuepfungsSchutz.PruefePfadAbLaufwerk`, Regel
  `GanzerPfad`); liegt das PDF unter einer Verknuepfung, wird nichts geschrieben (PR #85).
- **Kein Symlink:** `File.CreateSymbolicLink` ist gestrichen. Der Videoverweis steht immer in
  `<Video>.link` (Pfad des Originalvideos); ein aus frueheren Laeufen vorhandenes Ziel bleibt
  unberuehrt. `VideosMatched` und «, Video: …» gelten nur fuer einen geschriebenen Verweis oder ein
  vorhandenes Video (PR #85).
- **Videoverweis im Scan (PR #85):** `ScanAsync` loest `<name>.<videoendung>.link` nur lesend zum
  Originalvideo auf (eine Zeile, absoluter Pfad, Videoendung, Datei vorhanden); ein echtes Video im
  Ordner hat Vorrang. Ungueltige oder unlesbare Verweise laden den Fall ohne Video und stehen in der
  Hinweisliste (`ScanAsync(root, uebersprungeneOrdner, hinweise, token)`); das Training Center nennt
  sie im Protokoll und als «n Videoverweis(e) ungültig». Batch-Import und Selbsttraining profitieren
  ueber `ScanAsync(root)` mit, ohne Hinweisliste. Vor dem Lesen prueft `VerknuepfungsSchutz.PruefeEintrag`
  (Regel `Streng`) den Verweis selbst: Ist er eine Verknuepfung oder nicht pruefbar, wird er nicht
  gelesen, sondern als ungueltig gemeldet. Dieselbe Pruefung gilt fuer das gelesene Ziel: ist das
  Originalvideo selbst eine Verknuepfung, bleibt der Fall ohne Video. Tests: `TrainingCenterVideoverweisTests`.
- **Alte Video-Symlinks (PR #85):** Fruehere Laeufe legten im Haltungsordner symbolische Links auf
  das Video an. Der Scan uebernimmt eine Videodatei, die eine Verknuepfung oder nicht pruefbar ist
  (`VerknuepfungsSchutz.PruefeEintrag`, Regel `Streng`), nicht als Video, sondern meldet sie und nutzt
  einen gueltigen `.link`-Verweis im selben Ordner. Die Verteilung zaehlt einen solchen alten Link
  nicht als vorhandenes Video, laesst ihn unberuehrt und schreibt den `.link`-Verweis. Fuer Ordner
  aus alten Laeufen: die Verteilung einmal neu ausfuehren.
- **Ordnerliste waehrend eines Laufs gesperrt (PR #85):** «Ordner wählen…» und «Ordnerauswahl
  zurücksetzen» sind bei `IsBusy` nicht ausfuehrbar (`KannOrdnerAendern`, neu ausgewertet bei jedem
  `IsBusy`-Wechsel). So speichert der Scan nie eine inzwischen geaenderte Ordnerliste zu Faellen der
  alten; die Momentaufnahme im Scan-Workflow bleibt als zweite Sicherung.
- **Fallordner-Dateien (PR #85, Eigenpruefung):** `TrainingCenterFallDateien` buendelt die Regeln.
  Videos und Protokolle im Fallordner: Eintrag selbst keine Verknuepfung (Regel `Streng`); die Ordner
  darueber bis zur Scan-Wurzel betritt `SafeFileEnumeration` nur ohne Verknuepfung (Beleg:
  `Scan_betritt_keinen_verknuepften_fallordner_und_nennt_ihn`), die Scan-Wurzel selbst ist wie bei
  allen Importquellen Nutzerwahl. Verweisziele und das Video der Verteilung liegen ausserhalb des
  Baums: ganzer Pfad bis zum Laufwerk (`PruefePfadAbLaufwerk`, Regel `GanzerPfad`) vor `File.Exists`.
  Wird kein verwendbares Direktvideo ausgewaehlt (keines, nur ausgeschlossene, mehrdeutig), gelten die
  `.link`-Verweise als Rueckfall. Die Verteilung nennt ausgelassene Unterordner und Videodateien des
  Videoordners, prueft den Abbruch auch waehrend der Videosuche und zeigt Fehler nur ueber `UserError`.
- **Weitere Review-Runden PR #85:** Ausschlussmuster (Grafikvideo `*_g.mpg`, Uebersicht) gelten auch
  fuer ein EINZELNES Direktvideo (`PickBestVideo` filtert vor dem Einzelfall); dann gilt der
  `.link`-Verweis. Ein Dateifehler einer Haltung (Ordner, Protokoll, Verweis, Bereinigung, auch die
  `AggregateException` des Schreibbausteins) wird fuer diese Haltung gemeldet, die Verteilung faehrt
  fort. Ein seit dem Videoindex verschwundenes Quellvideo ergibt keinen Verweis und keinen Treffer.
  Ein Verweis mit syntaktisch ungueltigem Zielpfad verwirft nur sich selbst, nicht den Fall.
- **Erneut verteilen (PR #85):** Nach dem Schreiben des neuen Verweises entfernt die Verteilung andere
  Videoverweise (`*.<videoendung>.link`) im selben Fallordner ueber den Pfadwaechter; Videos werden nie
  geloescht, ein verknuepfter oder nicht loeschbarer Verweis wird gemeldet. Liegen im Scan mehrere
  gueltige Verweise, wird keiner verwendet (Hinweis «bitte die Verteilung erneut ausführen»). Der
  Scan-Status nennt alle Datei-Hinweise allgemein als «n Dateihinweise (siehe Protokoll)».
- **Folgepaket nach PR #85 (03.10.2026):** (1) Ein Scan-Abbruch protokolliert die bis dahin gesammelten
  uebersprungenen Ordner und Dateihinweise genau einmal und nennt die Zaehler im Status. (2) Die Verteilung
  hat die Ueberladung `DistributeByHaltungAsync(pdf, video, ausgabe, meldungen, token)`: der Sammler erhaelt
  alle Meldungen auch bei Abbruch oder Fehler; der Workflow protokolliert sie vor «Verteilung abgebrochen.».
  Die bisherigen Ueberladungen bleiben. (3) Mehrere echte Videos ohne eindeutigen Haltungsschluessel melden
  einen Dateihinweis statt still kein Video; ausgeschlossene Videos (Grafik, Uebersicht) zaehlen dabei
  nicht mit, ein einzelnes echtes Video neben einem Grafikvideo wird verwendet.
- **Unlesbare Ordner (R8c):** `ScanAsync(root, uebersprungeneOrdner, token)` sammelt Ordner, deren
  Dateiliste scheitert, und die von `SafeFileEnumeration` ausgelassenen (gesperrt, Verknuepfung).
  `TrainingCenterScanWorkflow` schreibt je Ordner die Zeile von `UebersprungeneOrdner.Meldung` ins
  Protokoll und haengt «n Ordner übersprungen (siehe Protokoll)» an die Zusammenfassung.
  `ScanAsync(root)` (Batch-Import, Selbsttraining, Werkzeuge) bleibt ohne Liste und ohne Abbruch.
- Tests: `TrainingCenterImportServiceVerteilungTests` (Rueckkehr vor Ende der Arbeit, Abbruch
  vor dem zweiten Chunk -> ein Ordner, Abbruch im Scan, unlesbarer Ordner),
  `TrainingCenterImportServiceVerknuepfungTests` (zwei `JunctionFact`, keine Symlink-Datei),
  `TrainingCenterScanWorkflowTests`, `TrainingCenterDistributionWorkflowTests`.

## Training Center: Aufteilung des Importdienstes (03.10.2026, Paket A)

Reines Verschieben ohne Verhaltensaenderung (Entscheid Pascal: vor der naechsten Erweiterung). Die
bestehenden Training-Center-Tests laufen unveraendert; angepasst wurde nur die Dateiliste von
`AtomicPersistenceArchitectureTests` (die Schreibstellen liegen jetzt in den neuen Dateien).

- `TrainingCenterImportService` bleibt die oeffentliche Fassade: alle oeffentlichen und internen Signaturen
  (inkl. der binaer kompatiblen Ueberladungen, `DistributeResult`, `ProtocolEntry` und des internen
  Testnaht-Konstruktors) delegieren an die Teile. Der Dienst wird weiter direkt erzeugt (keine neue
  DI-Registrierung; ServiceProvider unveraendert).
- `TrainingCenterFallScan`: Fallordner unter der Wurzel durchsuchen, Dateien pruefen (ueber
  `TrainingCenterFallDateien`), paaren, Verweis-Rueckfall, Inspektionsdatum; reiner Protokoll-Scan.
- `TrainingCenterHaltungsverteilung`: Sammel-PDF nach Haltungen, je Haltung Ordner, JSON und Videoverweis
  ueber den Verteil-Pfadwaechter, alte Verweise bereinigen.
- `TrainingCenterPaarung`: bestes Video/Protokoll, Haltungsschluessel-Abgleich, Mehrdeutigkeit,
  Widerspruchsregel (`DropContradiction`), Grafik-/Uebersichtsausschluss.
- `TrainingCenterVideoIndex`: Videoindex Haltungs-ID -> Video mit Meldung ausgelassener Ordner/Dateien.
- `TrainingCenterProtokollJson`: Protokolltext -> Beobachtungen (nur bekannte VSA-Codes) und atomares JSON.
- `TrainingCenterFallDateien` (seit PR #85): Verknuepfungs- und Verweisregeln der Fallordner-Dateien.
- Tests je Teil: `TrainingCenterTeileTests`.
- **Widerspruch melden (Paket B, 03.10.2026):** Widersprechen sich die Haltungsschluessel von Video und Protokoll,
  verwirft `TrainingCenterPaarung` wie bisher einen Teil (Regel unveraendert), liefert aber den Widerspruch mit
  (Video, Protokoll, beide Schluessel, verworfener Teil). Der Scan meldet ihn als Dateihinweis; ohne Hinweisliste
  (`ScanAsync(root)`: Batch-Import, Selbsttraining) nur im Trace. `ResolveProtocolOnlyPair` (nur Protokoll-Scan,
  derzeit nur in Tests) verwirft bei Widerspruch das Video ebenso still; dort ist keine Hinweisliste vorhanden,
  deshalb unveraendert. Tests: `TrainingCenterWiderspruchTests`, auch fuer `.link`-Videos.
  **Meldungsgrenze (Korrektur nach PR #92):** Nur wenn beide Dateinamen ein numerisches Schachtpaar enthalten,
  liefert die Paarung einen `Widerspruch`; dann bleibt die Meldung im Scan und Trace sichtbar.
  `NormalizeHaltungKey` liefert ohne Haltungsnummer den Namen selbst. Die bisherige Auswahl-/Verwerfungsregel
  bleibt auch fuer solche Namen unveraendert, aber `aufnahme.mp4` und `bericht.pdf` belegen keinen
  Haltungswiderspruch und ergeben deshalb keinen Hinweis. Das gilt auch, wenn nur eine Datei eine Nummer traegt.
