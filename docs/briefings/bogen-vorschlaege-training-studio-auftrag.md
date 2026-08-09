# Auftrag: Bogen-Vorschläge ins Training Studio

Stand 2026-08-08. Entwurf und Begründung:
`docs/superpowers/specs/2026-08-08-bogen-copilot-codiermodus-design.md`.
Messgrundlage: `docs/quality/BCC-COPILOT-2026-08-08.md`.

Der Vorabdurchlauf existiert als Prototypskript mit Browser-Prüfplatz und wurde
benutzt: 15 Vorschläge auf sieben Haltungen, 13 echte Bögen, alle sechs starken
richtig. Dieser Auftrag bringt ihn ins Programm.

## Was bereits fertig und geprüft ist

Nichts davon muss neu gebaut werden.

| Baustein | Ort | Zweck |
|---|---|---|
| `IBendSuggestionScanService` / `BendSuggestionScanService` | Application/UseCases/BendSuggestions, Infrastructure/Ai/BendSuggestions | Ganzer Durchlauf über ein Video |
| `BendSuggestionScanUseCase` | Application | Ablauf, Laufzeitmessung, drei Bild-Ausgänge |
| `BendSuggestionAggregator` | Application | Treffer zu Stellen, Meter- und Zeitregel |
| `BendSuggestionCalibrationPolicy` / `BendSuggestionCalibrationFileStore` | Application / Infrastructure | Arbeitspunkt je Gewicht, fail-closed |
| `MeterSequencePlausibility`, `MeterSequenceGapFiller` | Application | unmögliche Werte raus, kurze Lücken füllen |
| `VideoFrameSequenceExtractor` | Infrastructure/Media | Bilder in einem ffmpeg-Durchgang |
| `BendFrameDetector` | Infrastructure/Ai/BendSuggestions | Sidecar-Antwort in drei Ausgänge |
| `ModelPromotionPolicy` | Application/UseCases/ModelPromotion | Tauschregel (hier nicht gebraucht, nur zur Einordnung) |

Kandidat: `bcc_nc15_seed46_20260808`, `not_deployed`, Arbeitspunkt
`conf 0,50` / stark ab `0,80` in `workpoint.json` neben dem Kandidaten.

## Paket 0 — Vorbedingung: Meterquelle einhängen

**Ohne dieses Paket ist die Liste im Programm schlechter als die des Prototyps**
(Zeitregel statt Meterregel, 2,8 statt 1,0 Fehlalarme je Haltung).

`BendSuggestionScanService` nimmt bereits einen optionalen `resolveMeter` im
Konstruktor, hängt ihn aber nicht ein. Der Sidecar liefert seit `e9f3d44ed`
`meter_value` in `BccTestYoloResponse`.

Zu tun:

1. `BendFrameDetector` gibt den Meterwert der Antwort mit zurück (heute wirft er
   ihn weg). Zusätzliches Feld am Rückgabetyp, additiv.
2. `BendSuggestionScanService` reicht ihn als `resolveMeter` durch.
3. **Der eigentliche Eingriff:** `BendSuggestionScanUseCase` ruft `ResolveMeter`
   heute erst **hinter** der Treffer-Prüfung auf (`BendSuggestionScanUseCase.cs`,
   nach `if (outcome.Outcome != BendFrameOutcome.Detected) continue;`). Die
   Meterfolge bestünde damit nur aus Treffer-Bildern — die Sequenzprüfung hätte
   keine Nachbarn und liefe wirkungslos, das Lückenfüllen fände keine Klammern.

   Der UseCase muss die Meterfolge über **alle** Bilder aufbauen, wie es der
   Prototyp tut (`roh_meter` je Bild → `plausibilisiere_sequenz` →
   `luecken_fuellen`), und erst danach den Treffern ihren Meterwert zuordnen.
   Reihenfolge zwingend: **erst** `MeterSequencePlausibility`, **dann**
   `MeterSequenceGapFiller` — ein unmöglicher Wert darf nie Klammer einer
   Interpolation werden.

   Der Meterstand kommt dabei aus derselben Sidecar-Antwort wie die Erkennung.
   Ein zusätzlicher Aufruf je Bild ist nicht nötig und wäre die doppelte
   Laufzeit.

**Abnahme:** Ein Durchgang über eine SD-Haltung liefert dieselben Stellen wie
das Prototypskript (`training/scripts/bcc_copilot_durchlauf.py`). Weicht etwas
ab, gilt C# — dann ist der Prototyp anzupassen, nicht umgekehrt.

## Paket 1 — Sitzungsgedächtnis der Beeinflussung

Der heikelste Teil. Bitte den Abschnitt 2 des Entwurfs vorher lesen.

Sobald für eine Haltung eine Vorschlagsliste angesehen wurde, ist **die ganze
folgende Codierung dieser Haltung beeinflusst** — auch an Stellen ohne
Vorschlag. Das Wissen „dort hat die KI nichts gemeldet" wirkt genauso wie ein
sichtbarer Rahmen.

1. `ICodingSuggestionExposure` (Application/UseCases/BendSuggestions):
   `void MarkExposed(string caseId)` und `bool WasExposed(string caseId)`.

   **Schlüssel ist die `caseId`, die `CodingEventToSampleMapper.FromCodingEvent`
   ohnehin schon bekommt** — dieselbe Zeichenkette, die am Sample landet. Damit
   entfällt die Frage nach dem richtigen Normalisierer: Beide Seiten benutzen
   denselben Wert, und eine Abweichung kann gar nicht entstehen. Der Vergleich
   ist dann nur noch `StringComparison.OrdinalIgnoreCase` plus Trimmen.
2. `CodingSuggestionExposure` (Infrastructure): Gedächtnis nur für den
   Programmlauf, threadsicher. Kein Speichern auf Platte — ein Neustart setzt
   zurück. Das ist bewusst optimistisch; die Alternative wäre, jede Haltung
   dauerhaft zu verbrennen.
3. `CodingEventToSampleMapper` fragt es **zusätzlich** zum KI-Kontext. Ist die
   Haltung betroffen, entsteht `SuggestionShown` statt `Independent` — auch ohne
   `AiContext`. Der Mapper ist heute statisch; ein optionaler Parameter mit
   Vorgabe `null` hält alle bestehenden Aufrufer gültig.

**Tests:** Ereignis ohne KI-Kontext in angesehener Haltung → `SuggestionShown`;
in nicht angesehener → `Independent`; unbekannte Haltung → `Independent`;
Normalisierung greift.

**Warum das zählt:** Nur unbeeinflusste Samples dürfen später ein Modell messen
(`SuggestionProvenancePolicy`). Ohne dieses Paket verbrennt der Assistent
stillschweigend genau den Bestand, den `ModelPromotionPolicy` braucht.

## Paket 2 — Workflow

`BendSuggestionScanWorkflow` in `Application/UseCases/BendSuggestions`, Muster
Request/Actions/Result wie `CodingModeBackgroundServicesWorkflow`.

Enthält: Busy-Zustand, Fortschrittstext, Abbruch, Fehlermeldung im Klartext.
Enthält **nicht**: Dateizugriff, Modellwahl, Aggregation.

Fehlermeldungen wörtlich durchreichen, nicht glätten — „ffmpeg ist
fehlgeschlagen: moov atom not found" sagt dem Benutzer, dass die Datei defekt
ist.

## Paket 3 — ViewModel und Anzeige

`BendSuggestionListViewModel` (UI), Bereich im `TrainingStudioWindow`.

Je Zeile: **Ort**, **Stufe**, Konfidenz, Anzahl Bilder.

- Ort: gelesener Meterstand als `Meter 9,42`; geschätzter mit Zusatz
  „(geschätzt)"; keiner als `Sekunde 214 (Meterstand nicht lesbar)`.
  **Niemals `0,0` schreiben, wenn kein Wert vorliegt.**
- Stufe: stark und schwach farblich getrennt. In der Messung war stark 6 von 6
  richtig, schwach 7 von 9 — der Unterschied muss sichtbar sein.
- Kopfzeile: Kandidat, Arbeitspunkt, Herkunftsbeleg. Ohne hinterlegten
  Arbeitspunkt startet kein Durchgang; die Meldung dazu kommt aus
  `BendSuggestionCalibrationPolicy` und ist bereits verständlich formuliert.
- Zusätzlich sichtbar: nicht ausgewertete Bilder (`FramesNotAssessed`) und
  Laufzeit.

Klick auf eine Zeile zeigt **Spitzenbild und kurzen Clip** der Stelle. Kein
Sprung in den Player — das Training Studio spielt keine Videos, und die
Verbindung zwischen zwei Fenstern wäre im Player-Code teuer. Später
nachrüstbar.

Beim Anzeigen der Liste `MarkExposed(haltung)` aufrufen.

## Paket 4 — Registrierung

`IBendSuggestionScanService` und `ICodingSuggestionExposure` in
`ServiceProviderRegistrationMap` eintragen. Kein `new` verstreut im Code.

## Regeln, die nicht gebrochen werden dürfen

1. **Kein Durchgang ohne hinterlegten Arbeitspunkt.** Ohne ihn wählt der Sidecar
   selbst — nach höchster interner mAP50, also derzeit den Kandidaten mit den
   meisten Fehlalarmen.
2. **Kandidaten-ID und Gewicht-Hash gehen mit jeder Anfrage mit** und werden an
   der Antwort erneut geprüft. Ein stiller Modellwechsel ist schlimmer als ein
   Abbruch: Der Arbeitspunkt gilt nur für genau ein Gewicht.
3. **„Nichts gefunden" und „nichts gesehen" bleiben getrennt.** Technischer
   Fehler, leerer ffmpeg-Lauf und qualitätsbedingt nicht bewertetes Bild dürfen
   nie als „kein Bogen" erscheinen.
4. **Der produktive Modellzeiger wird nicht angefasst.** Der Kandidat bleibt
   `not_deployed`.
5. **Keine Vorbelegung des Codes** im Codierdialog.

## Ausdrücklich nicht in diesem Schritt

- Stapellauf über ein ganzes Projekt
- Sprung des Players an die Stelle
- Meterstand auf HD-Material — der Leser ist dort unbelegt (0–4 % Abdeckung,
  ein Fehler von drei). HD zeigt Sekunden.
- Andere Klassen als BCC

## Grenzen, die ins Fenster gehören

Nicht in eine Fussnote, sondern sichtbar:

- Nur Bögen. Eine leere Liste ist ein gültiges Ergebnis.
- Jeder zweite schwache Vorschlag ist falsch.
- Auf HD-Material gibt es keine Meterangabe.
