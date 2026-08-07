# BCC-Einzelklasse, Paket 4: erste Video-Messung — 2026-08-07

Der erste Lauf des BCC-Kandidaten über vollständige Inspektionsvideos war als
Messung deklariert, nicht als Betrieb. Dieser Bericht hält die Zahlen fest.

## Aufbau

- **Modell:** `bcc_single_fullbg_20260807`, Seed 44, `best.pt`
  (Epoche 274 von 300, internes mAP50(B) 0,8246). Scratchpad-Modell, kein
  registrierter Kandidat, nichts aktiviert.
- **Arbeitspunkt:** conf = 0,10, imgsz = 1280 (aus dem Schwellenlauf, Paket 2).
- **Material:** 8 Haltungen, deterministisch ausgewählt aus
  `artifacts/klassen-messung-20260804/messung.json` (Projekte absteigend nach
  Kandidatenzahl, je Projekt die ersten zwei Haltungen mit auffindbarem Video,
  H_- vor L_-Inspektionen). Die Liste ist goldfrei — keine dieser Haltungen
  steckt im Trainingsbestand des Modells.
- **Protokoll:** 12 codierte BCC-Befunde auf diesen 8 Haltungen, Zeitpunkt über
  den Videozaehlerstand (hh:mm:ss:ff) aus XTF/db3. Die Zeitcode-Abbildung wurde
  vorab verifiziert: Der Frame am Protokoll-Zeitpunkt entspricht exakt dem
  Operateurfoto (gleiche Szene, gleicher OSD-Meterstand).
- **Verfahren:** 1 Frame/Sekunde via ffmpeg, Inferenz pro Frame, zeitlicher
  Merge positiver Sekunden zu Gruppen (Lücke > 3 s trennt). Ein protokollierter
  Befund gilt als gefunden, wenn eine Gruppe das Fenster ±15 s um den
  Protokoll-Zeitpunkt überlappt. Gruppen ohne Überlappung zählen roh als
  Fehlalarm.
- **Skript:** `training/scripts/bcc_video_messung.py` (Diagnose, schreibfrei
  für Kundenoriginale). Rohdaten: `C:\KI_BRAIN\training\diagnostics\
  bcc_video_messung_20260807\report.json` plus Spot-Check-Frames jeder Gruppe.

## Ergebnis

Ein Video (`10.1031722-58943`, L_) ist ein 3,4-Sekunden-Stumpfen — Datei
defekt oder abgebrochen, beide Befunde unprüfbar. Effektiv also 7 Videos,
10 prüfbare protokollierte Bögen, 49,7 Video-Minuten.

### Gefundene Bögen (Recall)

| conf | gefunden |
|---:|:---|
| 0,10 (Arbeitspunkt) | **10/10** |
| 0,15 | 9/10 |
| 0,25 (Produktionsprotokoll) | 8/10 |
| 0,50 | 7/10 |

Die zwei verlorenen Bögen bei 0,25 liegen bei Gruppen-Maxima 0,24 und 0,12.
Der Arbeitspunkt 0,10 ist für Video richtig; das 0,25-Protokoll würde hier
spürbar Recall kosten.

### Positive Gruppen und Sichtprüfung

Roh: 74 Gruppen, davon 64 ohne Protokoll-Bezug (8,0 je Haltung). Alle 64
wurden einzeln angesehen (Peak-Frame je Gruppe) und klassifiziert:

| Kategorie | Anzahl | Anteil |
|---|---:|---:|
| plausibel echter, nur nicht codierter Bogen | 39 | 61 % |
| Schatten/Wandkontakt, Fugen, Sternriss | 15 | 23 % |
| Seitenanschluss | 3 | |
| Schacht-/Rohranfang (Videobeginn) | 3 | |
| unklar / Wassertrübung | 4 | |

Drei Beobachtungen dazu:

1. **Der grösste „Fehlalarm"-Treiber sind echte Bögen, die der Operateur nicht
   codiert hat.** Für einen Vorschlags-Assistenten ist das ambivalent: fachlich
   wertvoll (das Modell findet mehr als das Protokoll), aber ein Vertrauens-
   risiko, wenn es unkommentiert bleibt.
2. **Der zeitliche Dedup dieses Skripts ist ein Artefakt der Messung, nicht des
   Programms.** Die Kamera durchfährt Stellen mehrfach (Erkennen, Zurückfahren,
   nochmal anfahren); im Skript zählt jede Passage als eigene Gruppe (neun
   Gruppen bei LZ2 6,9–7,4 m in 36053-36052 sind ein und dieselbe Stelle). Die
   produktive Kette hat das längst gelöst: `TemporalFindingDeduplicator` mit
   `MeterMergeGapMaxMeters = 1,0`, gespeist aus der OSD-Metrierung
   (`VideoFullAnalysisService.cs:144`). Die hier gemeldeten 8 Gruppen je
   Haltung lägen im echten Pfad deutlich tiefer.
3. **Drei Gruppen sind Videobeginn** (Blick vom Schacht ins Rohr) und gratis
   vermeidbar: erste Sekunden bzw. Meter < 0,2 auslassen.

Bleibt als echte Fehlalarm-Last: grob 3–4 Gruppen je Haltung, Tendenz fallend
mit Schacht-Trimmung und Meter-Dedup. Treiber sind Wandkontakt der Kamera
(dunkle Fugen/Schatten) und einmal ein Sternriss, der wiederholt bogenähnlich
wirkte.

### Laufzeit

Inferenz 50–56 fps bei 1-fps-Abtastung: die 49,7 Video-Minuten waren in 55 s
gerechnet (plus 21 s Extraktion). Das ist Faktor ~50 über Echtzeit und lässt
auch feinere Abtastung (2–4 fps) oder ganze Projektbestände zu.

## Einordnung

- Die Messung ist klein (10 Bögen, 7 Videos, 4 Projekte). Sie zeigt Richtung,
  keine Endabnahme.
- Die „bogen"-Klassifikation der 39 Gruppen ist grosszügig ausgelegt (dunkle
  Öffnung voraus). Ein strenger Leser käme auf weniger — die Gruppen-Frames
  liegen zur Nachprüfung unter `spotchecks/`.
- Der defekte Videostumpf zeigt: Datei-Dauer gegen Protokoll-Meter zu prüfen
  gehört in jeden produktiven Lauf.

## Konsequenz für den Einbau

1. **conf 0,10 bleibt der Video-Arbeitspunkt** (10/10 vs. 8/10 bei 0,25).
2. **Vertragsbefund und Weg zum Einbau:** Der Sidecar-Kandidatenpfad
   `/detect/yolo/bcc-test` erzwingt die freigegebene 15er-Klassenkarte und
   filtert fest auf ID 14 (`bcc_test_wrapper.py:305`). Der gemessene
   Ein-Klassen-Kandidat (`{0: BCC_bogen}`) passt nicht durch. **Erste Wahl ist
   ein Neutraining als `nc: 15` mit voller Klassenkarte, aber nur BCC-Boxen**
   (dieselben Bilder, Labels 0 → 14 gemappt): Der Wächter bleibt unangetastet,
   der Kandidat erfüllt Manifest-, Hash- und Klassenkarten-Prüfung von
   allein. Kosten: rund 4 h GPU; das Ergebnis ist ein neues Artefakt und
   bekommt denselben knappen Nachweis (Schwellenlauf gegen
   `detect_benchmark_v1`) wie das Ausgangsmodell. Eine Vertragserweiterung des
   fail-closed Pfads ist zweite Wahl und bleibt zurückgestellt. conf wird pro
   Request übergeben (Default 0,25) — der Arbeitspunkt 0,10 ist sauber
   setzbar.
3. **Vor der Aktivierung:** Schacht-Trimmung (gratis). Meter-Dedup existiert
   produktiv bereits, siehe Beobachtung 2. Und die Kommunikation an den
   Operateur: Vorschläge ohne Protokoll-Bezug sind überwiegend echte,
   uncodierte Bögen — das muss im UI so heissen, sonst wird der Assistent als
   fehlerhaft wahrgenommen.

## Offene Prüfung — entscheidet über Weiterführung

Die Einstufung der 39 Gruppen als „echte, nur nicht codierte Bögen" stammt
aus einer KI-Sichtprüfung und ist grosszügig ausgelegt („dunkle Öffnung
voraus"). **Sie muss von einem Kanalinspekteur bestätigt werden** — die Frames
liegen unter `C:\KI_BRAIN\training\diagnostics\bcc_video_messung_20260807\
spotchecks\` (Dateien ohne `_TREFFER`). Bestätigt sich der Befund, beträgt die
echte Fehlalarmlast 3–4 je Haltung und der Assistent ist brauchbar. Zerfällt
er, sind es 8 je Haltung, und die Fehlalarme müssen zuerst runter. Bis zu
dieser Antwort ist das Neutraining vorbereitet, aber nicht gestartet.
