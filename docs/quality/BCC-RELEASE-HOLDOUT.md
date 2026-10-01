# BCC-Release-Holdout

## Aktueller Stand

Am 28. Juli 2026 wurde dieser unabhaengige Pruefbestand eingefroren:

```text
C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921
```

- 60 Bilder aus 60 unterschiedlichen Haltungen
- 25 Bilder aus Feldliweg, 25 aus Jagdmatt und 10 aus Fuerlauwi
- verdeckte Vorauswahl: 30 BCC-Hinweise und 30 Nicht-BCC-Hinweise
- keine Bild- oder Haltungsueberschneidung mit dem beim Einfrieren bekannten Bestand
- abgeglichen gegen 398 bekannte Bild-Hashes und 306 Haltungs-Aliase
- alle 60 Bilder und Haltungen durch `TrainingDataInventory` geschuetzt und geprueft
- Dynamischer Status: `ready_for_binary_evaluation`
- Review: 60 von 60 Bildern beurteilt, davon 29 positiv und 31 negativ
- Auswertung: `comparison_complete_not_release_qualified`
- Modellfreigabe: keine

Wichtige Pruefsummen:

```text
_manifest.json:   58d2b26b02950b5deccc850fcc63edb8734aebfc81d1a4c24059110054f28ef4
_candidates.json: 997e8619df0ae80720941bb48e401e72991f953326ff797c785f984a148cb1ea
Review:          d3c71fa37bca6bc189e2beebef75986c43a819da4094bf5eb0a36228664de663
```

Der Inventarbericht liegt unter:

```text
C:\KI_BRAIN\training\reports\bcc_release_holdout_inventory_20260728.json
```

Die abgeschlossene, an beide Pruefsummen gebundene Review-Datei liegt unter:

```text
C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json
```

Die 30/30-Vorauswahl ist nur ein unsichtbarer Hinweis aus den
Inspektionsmetadaten. Sie ist keine Bildwahrheit. Jedes Bild muss neu und blind
von einem Menschen beurteilt werden.

Das eingefrorene Manifest behaelt als Erstellungsbeleg seinen damaligen
`dataset_status=review_incomplete`. Der aktuelle Zustand wird immer aus der
unveraenderten Review-Datei und den heutigen Kontaminationsbindungen berechnet.

## Holdout erneut vorbereiten

Ohne `--execute` ist der Lauf nur eine Pruefung und schreibt nichts:

```powershell
python .\training\scripts\bcc_release_holdout.py prepare `
  --knowledge-root C:\KI_BRAIN `
  --source "D:\Videoprojekte\Altdorf_Feldliweg_41649_0626" "D:\Videoprojekte\Altdorf_Feldliweg_41649_0626\Altdorf_Feldliweg_41649_0626.xtf" `
  --source "D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426_Export" "D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426.xtf" `
  --source "D:\Videoprojekte\Meien_Fürlauwi_40671_0626" "D:\Videoprojekte\Meien_Fürlauwi_40671_0626\Dokumente\Meien_Fürlauwi_40671_0626.xtf"
```

`--execute` veroeffentlicht nur in einen neuen Zielordner. Ein vorhandener
Holdout wird nie ueberschrieben. Kundenoriginale werden nur gelesen.

## Blinde Bildpruefung

Der lokale Pruefplatz zeigt bei jedem Bild den festen Auftrag `BCC — Bogen`.
Modellvorhersagen, bildbezogene XTF-Untercodes und die Vorauswahl bleiben
verborgen. Er bietet genau drei Entscheidungen:

- `positive`: BCC-Bogen ist im Bild sichtbar
- `negative`: kein BCC-Bogen ist im Bild sichtbar
- `exclude`: Bild ist fuer diese Entscheidung ungeeignet

Start:

```powershell
python .\tools\EvalVisibilityReview\bcc_release_holdout_review_server.py `
  --holdout C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921 `
  --output C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json `
  --reviewer Besitzer
```

Die Review-Datei liegt ausserhalb des Holdouts. Sie wird nach jeder Entscheidung
atomar gespeichert und ist an die Pruefsummen von Manifest und Kandidatenliste
gebunden. Ein exklusiver Datei-Lock verhindert gleichzeitiges Schreiben; ein
zweiter Pruefplatz mit veraltetem Stand wird abgewiesen und muss neu laden.

## Status pruefen

```powershell
python .\training\scripts\bcc_release_holdout.py status `
  --knowledge-root C:\KI_BRAIN `
  --holdout C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921 `
  --review C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json
```

Moegliche Ergebnisse:

- `review_incomplete`: Entscheidungen fehlen noch
- `blocked`: alle Bilder sind beurteilt, aber eine Mindestmenge wird nicht erreicht
- `ready_for_binary_evaluation`: alle Bilder sind beurteilt und mindestens 20
  positive sowie 20 negative Haltungen sind vorhanden

Integritaets-, Herkunfts- oder Kontaminationsprobleme liefern stattdessen
`FEHLER` und keinen fachlichen Datensatzstatus.

`ready_for_binary_evaluation` ist keine Modellfreigabe. Das eingefrorene
Manifest wird nicht nachtraeglich veraendert und behaelt
`release_status=not_evaluated`. Die getrennte Auswertung dokumentiert ihren
aktuellen Zustand ausschliesslich im gebundenen Bericht.

## Kandidatenvergleich vom 28. Juli 2026

Der Vergleich verwendet fuer alle vier eingefrorenen Kandidaten exakt dieselben
60 Bildbytes. Das Protokoll ist fest: `conf=0.25`, `imgsz=1280`, nur Klasse 14
`BCC_bogen`, kein Schwellwert-Sweep. Technische Fehler zaehlen nie als
Negativbefund. Der reale Lauf hatte null technische Fehler.

| Kandidat | Rolle | TP | FN | TN | FP | Sensitivitaet | Spezifitaet | Balanced Accuracy |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| `bcc_bogen_30ec62ed706f` | Diagnose, fehlgeschlagener Lauf | 29 | 0 | 0 | 31 | 100,0 % | 0,0 % | 50,0 % |
| `bcc_bogen_30ec62ed706f_full40` | Diagnose, aufgehoben | 19 | 10 | 12 | 19 | 65,5 % | 38,7 % | 52,1 % |
| `bcc_bogen_af8020b688ac_v3_negatives` | noch relevant | 24 | 5 | 9 | 22 | 82,8 % | 29,0 % | 55,9 % |
| `bcc_bogen_b50b37ab8a4f` | noch relevant | 26 | 3 | 6 | 25 | 89,7 % | 19,4 % | 54,5 % |

Es gibt keinen eindeutigen Spitzenreiter: Der v3-Negativ-Kandidat hat drei
Fehlalarme weniger, verpasst aber zwei positive Bilder mehr als `b50...`.
Beide relevanten Kandidaten erzeugen zu viele Fehlalarme. Keiner darf aktiviert
werden.

Der labelblinde Vorhersagebeleg und der daraus neu eingelesene
Auswertungsbericht liegen unter:

```text
C:\KI_BRAIN\training\reports\bcc_release_holdout_predictions_64d06094c921_20260728_185438_532805.json
C:\KI_BRAIN\training\reports\bcc_release_holdout_evaluation_64d06094c921_20260728_185438_532805.json
```

Pruefsummen:

```text
Vorhersagebeleg: eef278c97ee12492f3788d421dcbe1a20257de62affa9b12934c730193b30c2f
Auswertung:       00fe6aae0dbdc2f1df2c3fcc510448631545cf164a99f9b91b1c25f9e674e957
```

Wiederholung nur mit der Sidecar-Umgebung und bei beendetem Sidecar:

```powershell
.\sidecar\.venv\Scripts\python.exe `
  .\training\scripts\evaluate_bcc_release_holdout.py `
  --knowledge-root C:\KI_BRAIN `
  --holdout C:\KI_BRAIN\eval_set\subsets\bcc_release_holdout_64d06094c921 `
  --review C:\KI_BRAIN\eval_review\bcc_release_holdout_64d06094c921_review.json
```

Das Werkzeug bindet Review, Kandidatenmanifeste, Gewichte, Aufhebungsmarker,
Bildbytes, Klassenkarte, Geraet und Laufzeitversionen per SHA-256. Es schreibt
zuerst einen labelblinden Beleg, liest genau diesen Beleg fuer das Scoring neu
ein und aktiviert oder trainiert kein Modell.

## Grenzen

- Ohne menschliche Boxen misst der Bestand nur BCC-Praesenz und Fehlalarme.
- Lokalisation, Box-IoU und mAP werden damit nicht gemessen.
- Der Holdout wurde fuer den Vergleich von vier Kandidaten verwendet. Auch ein
  spaeter verbesserter Spitzenreiter braucht vor einer Aktivierung einen neuen,
  zuvor unberuehrten Bestaetigungsholdout.
- Nicht protokollierte fruehere manuelle Modelltests sind rueckwirkend nicht
  beweisbar.
- Fuer diesen eingefrorenen V1-Bestand muessen der Kandidatenumfang sowie die
  aggregierten Fingerprints der bekannten Bild-Hashes und Haltungs-Aliase exakt
  gleich bleiben. Eine Aenderung, die einen dieser Werte veraendert, sperrt die
  Statuspruefung. Danach ist ein neuer Holdout erforderlich. Eingefrorene
  Eval-Manifeste werden zusaetzlich dateiexakt geprueft.
- Fuenf aeltere Eval-Saetze besitzen einen geprueften Hash-Freeze. Ein
  bilateraler Legacy-Satz besitzt kein eigenes Manifest; seine heutigen Bytes
  sind im Gesamt-Freeze dieses Holdouts enthalten.
- Von fuenf Collapse-Berichten besitzt einer vollstaendige Hash-Provenienz. Vier
  Legacy-Berichte wurden aus ihren heutigen Bildpfaden rekonstruiert. Ihre 31
  reinen Dateinamen-Verweise konnten eindeutig auf 14 gehashte Bilder im
  Negativpool aufgeloest werden; aktuell bleibt kein Name unaufgeloest. Ein
  falscher Feldtyp, ein leerer Eintrag oder ein nicht eindeutig aufloesbarer
  Legacy-Dateiname ist ein harter Fehler.
- Die vier beim Freeze vorhandenen Kandidaten stammen noch aus Manifesten ohne
  gebundenen Receipt-, `data.yaml`- und Klassen-Hash. Ihre heutigen drei
  Datensaetze wurden kanonisch und konsistent geprueft. Diese Ausnahme gilt nur
  fuer die exakt bekannten vier Kandidaten-IDs mit unveraenderter Manifest-SHA.
  Jedes Manifest im BCC-Kandidatenordner muss den Pilot `BCC_bogen` nennen. Neu
  trainierte oder veraenderte Kandidaten muessen alle drei Hashes direkt im
  Kandidatenmanifest binden.
- Beim ersten realen V1-Holdout ist die XTF-Herkunft im Manifest nur gesammelt
  gespeichert. Ein separater Audit hat die 60 Bildzuordnungen rekonstruiert.
  Neu erzeugte Holdouts speichern zusaetzlich `item_provenance` je Bild.
- Das erste reale V1-Manifest besitzt noch kein Feld `purpose`. Der
  Review-Server akzeptiert diese eine Altversion nur ueber den exakten Namen
  `SewerStudio BCC Release Holdout`; neue Holdouts schreiben `purpose`.
- Der zeitliche Quellenstichtag nutzt die lokale Dateizeit des Basismodells.
  Ohne das urspruengliche Trainingsinventar ist diese Herkunftsgrenze nicht
  vollstaendig beweisbar.
- Dieselbe Haltung wird auch bei umgekehrter Fahrtrichtung fuer Training und
  Export gesperrt.
