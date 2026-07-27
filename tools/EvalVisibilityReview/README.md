# Eval-Prüfplätze

Die Werkzeuge zeigen Eval-Bilder lokal im Browser. Sie verändern das eingefrorene
Eval-Set nicht. Ergebnisse werden immer in einer getrennten Datei gespeichert.

## Ereignis-ID und Schadensstufe

Der Prüfplatz lädt `C:\KI_BRAIN\eval_set` nur lesend. Aus den 120 Bildern werden
automatisch nur die Schadenscodes der Gruppen BA und BB angezeigt.

Start:

```powershell
.\tools\EvalVisibilityReview\start_eval_metadata_review.ps1
```

Pro Schadensbild werden diese Angaben erfasst:

- Schadensstufe 1 bis 5
- Ereignis-ID
- optionaler Meterbereich und Bemerkung
- Prüfer und Prüfzeitpunkt

Mehrere Bilder desselben realen Schadens erhalten dieselbe Ereignis-ID. Der Knopf
`Wie vorheriger Schaden` übernimmt dafür die Angaben des vorherigen Bildes.

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

## Sichtbarkeitsprüfung

Der ältere Drei-Tasten-Prüfplatz bleibt unverändert verfügbar:

```powershell
.\tools\EvalVisibilityReview\start_visibility_review.ps1
```
