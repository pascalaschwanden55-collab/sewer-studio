# Bogen-Copilot im Programm — Entwurf

Stand 2026-08-08. Grundlage: `docs/quality/BCC-COPILOT-2026-08-08.md`.

Der Vorabdurchlauf existiert heute als Prototypskript mit einem Prüfplatz im
Browser. Pascal hat ihn benutzt: 15 Vorschläge auf sieben Haltungen, 13 echte
Bögen, alle sechs starken richtig. Dieser Entwurf bringt denselben Weg ins
Programm.

## 1. Entscheidungen

Von Pascal am 2026-08-08 festgelegt:

| Frage | Entscheidung |
|---|---|
| Wo erscheint die Liste? | **Im Training Studio**, als eigener Bereich |
| Wann läuft der Durchgang? | Auf Knopfdruck, ein Video |
| Was tut ein Klick? | Zeigt Spitzenbild und kurzen Clip der Stelle |

Das Training Studio ist der richtige Ort: Es kennt die gepinnten Kandidaten
bereits, prüft ID und Gewicht-Hash und hat den Weg zur KI-Bereitschaft. Das
Codierfenster bleibt unberührt und unüberladen.

Die Folge davon: Das Training Studio spielt keine Videos, der Klick kann also
nicht in den Player springen. Er zeigt stattdessen Spitzenbild und Clip — genau
die Bedienung des Browser-Prüfplatzes, mit der Pascal am 2026-08-08 gearbeitet
hat. Das vermeidet zugleich eine Verbindung zwischen zwei Fenstern, die im
Player-Code teuer würde. Ein Sprung lässt sich später nachrüsten; der Weg
zurück wäre Umbau.

Bewusst **nicht** gewählt: Vorbelegung des Codes im Codierdialog. Bei conf 0,50
ist jeder zweite schwache Vorschlag falsch; eine Vorbelegung verleitet zum
Durchwinken.

## 2. Der schwierige Teil: Herkunft je Sitzung statt je Ereignis

Heute hält `TrainingSample.SuggestionProvenance` fest, ob beim einzelnen
Ereignis ein KI-Kontext vorlag. Das genügt für den Prüfplatz, aber nicht für
eine offene Vorschlagsliste.

**Sobald die Liste für eine Haltung angesehen wurde, ist die ganze folgende
Codierung dieser Haltung beeinflusst** — auch an Stellen ohne Vorschlag. Das
Wissen „dort hat die KI nichts gemeldet" verändert die Entscheidung genauso wie
ein sichtbarer Rahmen. Belegt am 2026-08-07: Dieselbe Stelle wurde mit
sichtbarem Modellrahmen als Bogen codiert und ohne ihn als verschobene
Rohrverbindung mit Knick.

Deshalb ein Sitzungsgedächtnis:

- `ICodingSuggestionExposure` (Application) merkt sich je Programmlauf, für
  welche Haltungen eine Vorschlagsliste angesehen wurde.
- `CodingEventToSampleMapper` fragt es zusätzlich zum KI-Kontext. Ist die
  Haltung betroffen, gilt `SuggestionShown` — unabhängig vom einzelnen Ereignis.
- Das Gedächtnis lebt nur im Programmlauf. Ein Neustart setzt es zurück; das ist
  bewusst optimistisch, aber die Alternative wäre, jede Haltung dauerhaft zu
  verbrennen.

Damit wird der Ein-/Aus-Schalter real: Wer Messmaterial erzeugen will, öffnet die
Liste nicht. Nur so entsteht der unbeeinflusste Bestand, gegen den ein neues
Modell später gemessen wird (`ModelPromotionPolicy`).

## 3. Bausteine

Alles Neue ist additiv. Der Player, das VSA-Codierfenster und der bestehende
Codierweg werden nicht umgebaut.

| Baustein | Schicht | Aufgabe |
|---|---|---|
| `BendSuggestionScanWorkflow` | Application/UseCases | Busy, Fortschritt, Abbruch, Fehlermeldung. Keine Datei- und keine Modelllogik. |
| `ICodingSuggestionExposure` / `CodingSuggestionExposure` | Application / Infrastructure | Sitzungsgedächtnis der angesehenen Haltungen |
| `BendSuggestionListViewModel` | UI | Liste, Auswahl, Anzeige von Bild und Clip |
| Bereich im `TrainingStudioWindow` | UI (XAML) | Videowahl, Startknopf, Liste, Vorschau |

Vorhanden und unverändert genutzt: `IBendSuggestionScanService`,
`BendSuggestionScanUseCase`, `BendSuggestionAggregator`,
`BendSuggestionCalibrationPolicy`, `VideoFrameSequenceExtractor`,
`BendFrameDetector`.

## 4. Ablauf

```text
Training Studio öffnen, Bereich "Bogen-Vorschläge"
  → Video wählen
  → "Durchgang starten"
      → IBendSuggestionScanService.ScanAsync
      → Fortschritt, Abbrechen möglich
  → Liste erscheint; Haltung wird im Sitzungsgedächtnis vermerkt
  → Klick auf eine Zeile → Spitzenbild und kurzer Clip der Stelle
  → Pascal codiert danach im Player wie immer selbst
```

## 5. Was die Liste zeigt

Je Zeile: Ort, Stufe, Konfidenz, Anzahl Bilder.

- **Ort:** gelesener Meterstand, sonst „Sekunde 214 (Meterstand nicht lesbar)".
  Ein geschätzter Meter wird als solcher gekennzeichnet. Niemals `0,0`.
- **Stufe:** stark ab dem kalibrierten Wert des Gewichts (heute 0,80), sonst
  schwach. Farblich getrennt, weil die Trefferquote sich unterscheidet: stark
  war in der Messung 6 von 6 richtig, schwach 7 von 9.
- **Kopfzeile:** Kandidat, Arbeitspunkt und Herkunftsbeleg — dieselben drei
  Zeilen wie im Prototyp. Ohne hinterlegten Arbeitspunkt startet kein Durchgang.

Zusätzlich sichtbar: Anzahl nicht ausgewerteter Bilder (`FramesNotAssessed`) und
die Laufzeit. Ein blinder Fleck darf sich nicht als sauberes Rohr tarnen.

## 6. Grenzen, die im Fenster stehen müssen

- **HD-Material zeigt Sekunden, keine Meter.** Der OSD-Leser ist auf SD belegt
  (76 % Abdeckung, 100 % richtig) und auf HD nicht (0–4 %, ein Fehler von drei).
- **Nur Bögen.** Kein Riss, kein Anschluss, keine Ablagerung. Eine leere Liste
  ist ein gültiges Ergebnis.
- **Jeder zweite schwache Vorschlag ist falsch.** Das gehört sichtbar ins
  Fenster, nicht in eine Fussnote.

## 7. Tests

- `BendSuggestionScanWorkflow`: Busy-Zustände, Abbruch, Fehler ohne
  Arbeitspunkt, Fehler bei defektem Video.
- `CodingSuggestionExposure`: Haltung vermerkt/nicht vermerkt, Normalisierung
  der Haltungsnummer, Rücksetzung bei Programmstart.
- `CodingEventToSampleMapper`: Ereignis ohne KI-Kontext in einer angesehenen
  Haltung ergibt `SuggestionShown`; in einer nicht angesehenen `Independent`.
- ViewModel: Ortstext für gelesenen, geschätzten und fehlenden Meterstand;
  Auswahl einer Zeile zeigt Bild und Clip der richtigen Stelle.

## 8. Nicht in diesem Schritt

- Stapellauf über ein ganzes Projekt
- Vorbelegung des Codes
- Anzeige im Player während des Codierens
- Sprung des Players an die Stelle (spaeter nachruestbar)
- Meterstand auf HD-Material

## 9. Offene Punkte

- ~~Meterquelle im C#-Weg~~ — **erledigt 2026-08-09** (`a160b49c5`): `meter_value`
  aus der Sidecar-Antwort, Folge ueber alle Bilder, erst Plausibilitaet, dann
  Lueckenfuellen. Abgenommen gegen den Prototypen (226 Einzeltreffer ohne
  Abweichung, fuenf Stellen feldgleich; Fixture
  `tests/Fixtures/BendSuggestions/soll_36053-36052_vorschlaege.json`).
- **Clip-Zwischendateien bleiben liegen** (`auswertungpro_clip_*.mp4` im
  Temp-Ordner), weil das MediaElement die Datei offen haelt. Bei vielen
  Durchlaeufen fuellt sich der Temp-Ordner — ein Durchgang ueber ein
  Neun-Minuten-Video sind rund 550 Bilder plus Clips. Aufräumstrategie ist
  ein Follow-up (z. B. Loeschen beim naechsten Fensterstart oder nach dem
  Schliessen des Fensters).
- **Sichtpruefung des Bereichs** am laufenden Fenster (Dark-Theme des
  DataGrid, Lesbarkeit der Grenzen-Zeile, Clip-Abspielung): keine
  Testabdeckung moeglich, gehoert dem Menschen.
