# OSD-Meterleser: Validierung — 2026-08-08

Prototyp `training/scripts/osd_meter_leser.py` (Ziffern-OCR ohne Bildmodell,
für den BCC-Copiloten: Meterstand statt Videozeit → Fehlalarme 2,8 → 1,0 je
Haltung). Hier steht, was die Validierung ergab und was noch aussteht.

## Aufbau

- **95 Frames** aus den 7 nutzbaren Messvideos des BCC-Videolaufs, geschichtet
  statt zufällig: je Video 10 gleichverteilte Sekunden plus bis zu 4 Frames an
  Gruppen-Spitzen (Kamera in Bewegung, Pendel-Stellen — die harten Fälle).
- **Drei OSD-Stile** im Material: dunkel-auf-Kasten (`LZ2: 14.1m`, dominant),
  dunkel-auf-Video (`LZ2: 0000.30 m`, Göschenen-Layout), hell-auf-Video.
- **Wahrheit:** Menschablesung über Kontaktblätter
  (`C:\KI_BRAIN\training\diagnostics\osd_meter_reader_20260808\validierung\`,
  `wahrheit.txt` ausfüllen). **Die KI-Vorprüfung ersetzt das nicht** — sie hat
  nur verhindert, dass ein defekter Leser zur Prüfung geht.

## Ergebnis — bindend, gegen menschliche Wahrheit (2026-08-08, abgeschlossen)

Pascal hat alle 95 Frames abgelesen (94 davon lesbar, 1 unleserlich). Der
Neulauf des heutigen Lesers (Formvalidator + Sequenzprüfung) dagegen:

| Stil | Richtigkeit | Abdeckung |
|---|---|---|
| dunkel-auf-Kasten (dominant) | **67/67 = 100 %** | 91 % |
| dunkel-auf-Video (Göschenen) | **4/4 = 100 %** | 31 % |
| hell-auf-Video | — | 0 % (immer None) |
| **gesamt** | **71/71 = 100 %** | **76 %** |

Je Haltung: fünf bei 93–100 % Abdeckung, die zwei Göschenen-Haltungen bei
17 % (2/12) und 23 % (3/13). **Null falsche Werte.** Die Richtigkeit ist
kein Thema mehr; offen bleibt nur die Abdeckung auf dem Göschenen-Stil.

Zwei Prüfungen tragen das Ergebnis zusätzlich:

- **Die C#-Portierung der Python-Funktion ist gegen alle 95 Lesungen
  identisch** — Abweichung null, sobald gegen die aktuelle Funktion
  verglichen wird.
- **Lektion zur Datenhaltung:** Eine frühere Meldung „1 Fehler in 72"
  stammte aus einer gespeicherten Ergebnisdatei, die Spalten aus
  verschiedenen Läufen mischte (Roh None, Sequenz 3,0 — unmöglich aus einem
  Lauf). `leser_ergebnisse.json` wird seither nur noch in einem konsistenten
  Durchgang geschrieben. Gegen veraltete Dateien rechnen erzeugt Fehler,
  die es im Code nicht gibt.

## Warum die Richtigkeit hoch ist: der Formvalidator

Die Vorprüfung ohne Validator lag bei 89 % Richtigkeit — 9 falsche Werte, alle
mit verstümmelter Zeichenfolge. Der Validator lässt nur drei vollständige
Formen durch (`\d{1,3}[.?]\d`, `\d{4}[.?]\d{1,2}`, und die punktlose Form
`\d{2,3}` **nur** im Ein-Dezimalen-Layout). Alles andere wird None.

Das kostet Abdeckung (korrekte, aber unsauber segmentierte Lesungen fallen
weg) und kauft Sicherheit: Wo der Leser antwortet, stimmt die Antwort.

## Offene Punkte für die Integration

1. ~~Menschliche Ablesung~~ — **erledigt** (Pascal, 2026-08-08; Zahlen oben
   sind damit bindend).
2. **Göschenen-Abdeckung (31 %):** Die Segmentierung liefert fast immer die
   richtigen Zeichen, aber mit Rauschpunkten dazwischen — der Validator lehnt
   dann ab. Ein Format-Lock pro Video („diese Haltung ist Vierziffern-Layout",
   aus den erfolgreichen Frames gelernt, danach erzwungen) würde die Abdeckung
   heben. Gehört in die Integrationslogik, nicht in den Leserkern.
3. **Verdrahtung (Relay):** gelesene Werte mit `MeterIsEstimated=false`,
   Lückenfüller (Median über ±3 s, nur wo None steht — nie als Glättung) mit
   `true`. Der Aggregator fasst nur über gelesene Meter zusammen.

## Nachtrag: Defektbericht und Sequenz-Plausibilität

Der Copilot-Lauf lieferte vier Defekte mit Wahrheitswerten. Zwei Lehren daraus:

- **133,08 m auf einer < 20-m-Haltung** zeigte die Lücke des Formvalidators:
  `0133.08` ist formal gültig. Antwort ist `plausibilisiere_sequenz()` im
  Leser: pro Video wird ein Wert verworfen, wenn er über der robusten
  Videodecke (max(4×Median, 30 m)) liegt oder mit **allen** zeitnahen
  Nachbarn unverträglich ist (Sprung > 5 m/s). Verworfen heisst None — wie
  ein unlesbarer Frame. Die Frame-Ebene bleibt zustandslos; Plausibilität
  gehört der Sequenz. Die gleiche Prüfung gegen die bekannte Haltungslänge
  läuft zusätzlich im Verbraucher (dort, wo die Länge bekannt ist).
- **Ein Rettungsversuch ist am Validator gescheitert — absichtlich.** Eine
  Sechs-Ziffern-Regel für das Vierziffern-Layout hätte `0.00.300` als
  `0003.00` gelesen statt `0000.30` → 3,0 statt 0,3: ein falscher Wert,
  den keine Plausibilitätsprüfung mehr fängt. Die Regel wurde nach dem
  Gegenbeweis ausgebaut und kommt nicht wieder. Die Göschenen-Abdeckung
  bleibt ehrlich niedrig, statt falsch hoch.

## Nachtrag 2026-08-08 (abends): Format-Parameter und Sidecar-Feld umgesetzt

- Der Leser hat den Format-Lock: `parse_meter`/`lese_meter` akzeptieren
  `format=` (`auto` wie bisher, `ein_dezimal`, `vierziffern`; unbekannt ist
  ein Fehler, kein stiller Rückfall). Die Leser-Logik liegt jetzt in
  `sidecar/sidecar/osd_meter.py`; dieser Prototyp delegiert dorthin, damit
  Diagnose und Sidecar eine Quelle teilen. Portierung gegen alle 95
  Validierungsframes erneut geprüft: **95/95 identisch** (Rohfolge, Wert,
  Stil).
- `POST /detect/yolo/bcc-test` liefert die rohe Einzelbild-Lesung als
  additives `meter_value` (None = nicht lesbar) und akzeptiert
  `meter_format`. C#-Vertrag: `BccTestYoloRequest.MeterFormat` /
  `BccTestYoloResponse.MeterValue`.
- Offen bleibt die Integrationslogik: den Lock pro Video aus erfolgreichen
  Frames lernen und über `ResolveMeter` in `BendSuggestionScanService`
  einhängen (C#).

## Nachtrag 2026-08-08 (HD-Material): ein verwechselter Buchstabe, Faktor zehn

Befund auf 1080p-Material (feinere, weichere Striche als SD): Der
Klassifikator verwechselt das `Z` der `LZ`-Beschriftung mit `1`. Aus
`LZ 3.2m` wird `L132`, und der Parser las die vermeintliche Eins als erste
Ziffer: **13,2 statt 3,2**. Alle sieben gemessenen Werte lagen exakt eine
Zehnerpotenz zu hoch (`L132`→3,2; `L107`→0,7; `L145`→4,5). Die
Sequenzprüfung fängt das nicht: Die Nachbarn sind gemeinsam verschoben und
damit untereinander verträglich.

Behoben am Parser (der robuste der zwei diskutierten Wege): Nach einem `L`
darf nur das `Z` folgen — oder sein bekanntes verlesenes `2`. Jede andere
Ziffer ist eine Verlesung und wird verworfen (None), statt geraten. Die
Regel fängt den Fehler auch, wenn die Vorlagen irgendwann wieder kippen.
Verifikation: 95/95 Validierungsframes weiterhin identisch; die drei
HD-Rohfolgen sind als Regressionstests verankert.

Folge für den Assistenten: Auf HD-Material liefert der Leser jetzt None —
der Durchlauf zeigt dort Sekunden statt erfundener Meter. Das ist genau die
geforderte Regel: Ein Wert, der immer um zehn danebenliegt, ist schlimmer
als keiner. **Offen:** die HD-Abdeckung selbst heben (Vorlagen für die
feinere Schriftgrösse am Klassifikator) — bewusst zurückgestellt, weil ohne
eingefrorenen HD-Prüfbestand nicht verifizierbar.

## Nachtrag 2026-08-08 (spät): Prüfbestände eingefroren, Messung dagegen

Drei getrennte, an Bildbytes und menschliche Ablesung gebundene Bestände —
Monotonie ohne Umnummerieren: `osd_sd_v1` (95 Bilder, 7 Haltungen),
`osd_hd_v1` (30 Bilder, 5 Haltungen), `osd_hd2_v1` (72 Bilder, 12 Haltungen).
Eine Messung läuft über alle drei; `osd_hd_v1` bleibt unangetastet.

Gemessener Stand des Lesers (mit beiden Parser-Regeln) gegen die Wahrheit:

| Bestand | Abdeckung | richtig | falsch |
|---|---:|---:|---:|
| osd_sd_v1 | 71/94 = 76 % | 71 | **0** |
| osd_hd_v1 | 0/29 = 0 % | 0 | **0** |
| osd_hd2_v1 | 3/71 = 4 % | 2 | **1** |

Die Faktor-zehn-Fehler sind vollständig verschwunden (None statt falsch).
Der eine Restfehler (`f0046`: 11,7 statt 13,7) ist eine einzelne
Ziffernverwechslung 3→1 — Sache des Klassifikators oder der Sequenzschicht,
nicht der Präfixregeln.

Zweite Parser-Regel (Pascal, vier Zeilen): Führende Störzeichen vor der
Beschriftung werden bis Fenster 4 abgeschnitten (`2L111`, `??L122`,
`???.L10.1` — der Erkenner setzt auf HD-Schrift gelegentlich Zeichen VOR
das L; sonst lief die Präfixregel ins Leere). Bewusst nur vorn gesucht:
Ein L weiter hinten gehört nicht zur Beschriftung.

Damit ist der Weg für HD vorbereitet und messbar: HD-Vorlagen im
Klassifikator, geprüft gegen `osd_hd_v1` + `osd_hd2_v1`, mit `osd_sd_v1`
als Sperre gegen Rückschritt (95/95 muss identisch bleiben).

## Nachtrag 2026-08-09: enger Rueckfall fuer Praefix und fuehrende Nullen

Die 40er-Layoutsichtung zeigte 12 Bilder, bei denen Lage, Polaritaet und Farbe
zum Leser passen und nur die Schreibweise `LZ... + 0000.00 m` abweicht. Die
erste Gegenprobe widerlegte jedoch die reine Parser-Hypothese: Der Parser kannte
Praefix und Vierziffern bereits, las auf den echten Bildern aber 0/12. Ursache
war die Geraeteschrift mit dunklem Rand; Maskenwahl und Arial-Vorlagen zerlegten
sie falsch.

`sidecar/sidecar/osd_meter.py` besitzt deshalb additiv einen engen Tesseract-
Rueckfall. Er startet nur nach gescheiterter oder unvollstaendiger
Vorlagenlesung, prueft beide unteren Ecken und beide Polaritaeten und akzeptiert
ausschliesslich ein vollstaendiges Vierziffern-Format. Tesseract wird nur
verwendet, wenn es lokal bereits installiert ist; fehlt es oder laeuft es in
einen Fehler, bleibt das Ergebnis `None`. Es wird kein Paket installiert und
kein unbekanntes Format geraten.

Messung des Stands, SHA-gebunden in `prefix_fallback_bericht.json`:

| Bestand | Ergebnis |
|---|---:|
| Zielstil der 40er-Sichtung | 8/12 gelesen, 8/8 passend zum schwachen PDF-Label |
| Neuer Rueckfallweg in allen 40 Bildern | 12 gelesen, 12/12 passend zum schwachen PDF-Label |
| Gesamter Leser in allen 40 Bildern | 13 geliefert, 12 passend, 1 falsch oder nicht pruefbar |
| `osd_sd_v1` Gold | 82 geliefert, 82 richtig, 0 falsch |
| `osd_hd_v1` Gold | 0 geliefert |
| `osd_hd2_v1` Gold | 0 geliefert, 0 falsch |

Der einzelne HD2-Fehler wurde als `f0046.jpg`, Haltung `35722-35724`, Soll
13,7 m, gelesen 11,7 m identifiziert. Er stammte aus dem Vorlagenweg
(`L211.7m1.`), nicht aus Tesseract. Eine enge Sicherheitsregel verwirft nun
Folgen, in denen nach `L2` oder `LZ2` sofort eine weitere Ziffer statt eines
Trenners steht. Der HD2-Fehlwert verschwindet; die 82 richtigen SD-Goldwerte
bleiben unveraendert.

Der aktuelle Leser wurde ausserdem auf der festen Archivauswahl neu gerechnet.
Von 86 Eintraegen besitzen 83 ein eindeutiges Video; alle 83 wurden ohne
technischen Abbruch verarbeitet. Je Video wurden 20 gleichmaessige Stellen
angefahren. Ergebnis: SD 262/1187 Bilder = 22,1 %, HD 14/452 = 3,1 %. Nur der
von der 40er-Kalibrierung getrennte Anteil ergibt SD 235/1108 = 21,2 % und HD
14/354 = 4,0 %. Der Bericht bindet Leser- und Quellen-SHA; Videos sind ueber
Pfad, Groesse und Aenderungszeit gebunden, nicht ueber einen Vollhash.

Die Archivlabels bleiben wegen PDF-/Video-Zuordnungsrauschen schwach und sind
keine Freigabe. Der aktuelle Kandidat liefert im SD-Goldbestand 82/82 richtig;
in HD und HD2 liefert er keinen Wert und damit auch keinen bekannten Fehlwert.
Die niedrige unabhaengige Abdeckung und der eine unklare 40er-Fall verhindern
weiterhin eine Freigabe. Der Stand bleibt `diagnostic_not_deployed`.
