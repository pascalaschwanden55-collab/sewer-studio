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

## Sichtbarkeitsprüfung

Der ältere Drei-Tasten-Prüfplatz bleibt unverändert verfügbar:

```powershell
.\tools\EvalVisibilityReview\start_visibility_review.ps1
```
