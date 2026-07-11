# Eval-Set V2 und Feldqualitaet

Stand: 2026-07-11

## Ziel

V2 misst die KI an neuen, menschlich geprueften Felddaten. Das bestehende V1-Set bleibt
unangetastet. Gleichzeitig sammelt der Feldqualitaetsbericht die Ergebnisse aus dem
Pruefmodus und aus der Schattenauswertung.

## V2 vorbereiten

Die Kandidatendatei ist ein JSON-Array. Jeder Eintrag braucht diese Angaben:

```json
{
  "id": "uri-100-200-0123-bab",
  "source_image_path": "D:\\Eval-V2-Quellen\\frame_0123.png",
  "source_label_path": "D:\\Eval-V2-Quellen\\frame_0123.txt",
  "haltung_key": "100-200",
  "meter": 12.3,
  "expected_code": "BAB",
  "group": "damage",
  "dn_mm": 300,
  "pipe_material": "Beton",
  "image_quality": "limited",
  "human_reviewed": true,
  "reviewed_by": "Pascal",
  "reviewed_at_utc": "2026-07-11T12:00:00Z"
}
```

`source_label_path` ist optional. Alle anderen fachlichen Angaben sind Pflicht.

V2 muss alle fuenf Gruppen enthalten:

- `damage`: echte Schaeden aus BA/BB
- `empty`: schadenfreie Bilder mit Code `LEER`
- `structure`: Rohranfang, Rohrende und bauliche Hinweise BCC/BCD/BCE
- `condition`: Betriebszustand BDA/BDB/BDC/BDD
- `pre_roll_data_board`: Vorspann und Datentafeln

Vor dem Einfrieren werden mindestens 20 verschiedene Haltungen, drei DN-Bereiche,
zwei Rohrmaterialien und alle Bildqualitaeten `good`, `limited`, `poor` verlangt.

## V2 pruefen und bauen

Zuerst nur pruefen:

```powershell
dotnet run --project tools/EvalSetV2Builder -- --candidates D:\Eval-V2-Quellen\candidates.json --v1-root C:\KI_BRAIN\eval_set --dry-run
```

Danach einmalig bauen:

```powershell
dotnet run --project tools/EvalSetV2Builder -- --candidates D:\Eval-V2-Quellen\candidates.json --v1-root C:\KI_BRAIN\eval_set
```

Das Ergebnis liegt in `C:\KI_BRAIN\eval_set\v2`. Der Zielordner muss vorher leer bzw.
nicht vorhanden sein. Der Builder erzeugt `_manifest.json` mit SHA-256-Hashes und
`_candidates.json`. Ein Bild aus V1 oder ein doppeltes Bild wird abgewiesen.

Die Trainingssperre sitzt an drei Stellen:

- zentral beim Speichern in `TrainingSamplesStore`
- beim lokalen Stage-A-Export
- beim direkten Sidecar-Export in `TrainingExportService`

Der Hauptordner `C:\KI_BRAIN\eval_set` schuetzt automatisch V1 und jedes darunterliegende
Set mit eigenem `_manifest.json`.

## Feldqualitaetsbericht

Der Bericht kann parallel zum Aufbau von V2 laufen:

```powershell
dotnet run --project tools/AiQualityReport -- --training-samples C:\KI_BRAIN\training_samples.json --project D:\Projekte\ProjektA\projekt.json --project D:\Projekte\ProjektB\projekt.json --output docs\quality\aktuell
```

`--project` darf mehrfach vorkommen. Pro Lauf entstehen ein lesbarer Markdown-Bericht,
eine JSON-Datei und eine CSV-Liste der zu pruefenden Faelle.

Gezahlt wird pro dedupliziertem Befund, nicht pro Frame. Befunde derselben Haltung,
Code-Familie und Position innerhalb von 0.5 m werden zusammengefasst.

Ein menschlich bestaetigter manueller Schaden ohne KI-Partner innerhalb von 0.5 m wird
als `possible_miss` gemeldet. Das ist bewusst nur ein moeglicher uebersehener Schaden:
bei geschaetzter oder widerspruechlicher Meterposition setzt der Bericht zusaetzlich
`MeterNeedsReview`.

Weitere Fehlergruppen sind falscher Code, falscher Untertyp, falscher Positivbefund,
moeglicher Meterfehler sowie Quantifizierungs- oder Detailkorrektur. Die letzte Gruppe
ist derzeit ein ehrlicher Ersatzwert: Der Code blieb gleich, der Mensch hat den Befund
aber bearbeitet. Uhrlage, Ausdehnung und Schweregrad koennen erst getrennt ausgewertet
werden, wenn Vorher- und Nachherwerte einzeln gespeichert werden.

Die Schattenauswertung ergaenzt die Erkennungsebene um Zustandsklasse, Massnahme und
Kosten. Veraltete Schattenresultate werden nicht als normaler Vergleich gezaehlt.

## Freigabekriterium

Automatisch gruene Befunde gelten erst als ausreichend belegt, wenn alle Bedingungen
erfuellt sind:

- mindestens 300 menschlich gepruefte, deduplizierte gruene Befunde
- aus mindestens 20 Haltungen
- hoechstens ein fachlicher Fehler
- obere 95-Prozent-Grenze der Fehlerrate unter 2 Prozent

Bei 300 Faellen und keinem Fehler liegt diese obere Grenze bei rund 1 Prozent. Bei einem
Fehler liegt sie bei rund 1.6 Prozent. Alte Samples ohne gespeicherte zentrale
Freigabeentscheidung zaehlen nicht rueckwirkend als gruene Faelle.

V1 darf weder verschoben noch neu erzeugt werden. Vor und nach dem V2-Bau muessen die
Hashes von `C:\KI_BRAIN\eval_set\_manifest.json`, `_candidates.json` und den V1-Bildern
gleich bleiben.
