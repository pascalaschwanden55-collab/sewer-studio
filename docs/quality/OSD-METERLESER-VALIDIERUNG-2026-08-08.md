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

## Ergebnis der KI-Vorprüfung (95 Frames, davon 94 lesbar)

Zwei getrennte Grössen, nie verrechnet:

| Stil | Richtigkeit | Abdeckung |
|---|---|---|
| dunkel-auf-Kasten (dominant) | **67/67** | 91 % |
| dunkel-auf-Video (Göschenen) | **4/4** | 31 % |
| hell-auf-Video | — | 0 % (immer None) |
| **gesamt** | **71/71** | **75 %** |

Null falsche Werte. Obere Vertrauensgrenze der Fehlerquote bei 71 Antworten:
~4 % (Drittelregel) — deshalb bleibt die menschliche Ablesung Pflicht.

## Warum die Richtigkeit hoch ist: der Formvalidator

Die Vorprüfung ohne Validator lag bei 89 % Richtigkeit — 9 falsche Werte, alle
mit verstümmelter Zeichenfolge. Der Validator lässt nur drei vollständige
Formen durch (`\d{1,3}[.?]\d`, `\d{4}[.?]\d{1,2}`, und die punktlose Form
`\d{2,3}` **nur** im Ein-Dezimalen-Layout). Alles andere wird None.

Das kostet Abdeckung (korrekte, aber unsauber segmentierte Lesungen fallen
weg) und kauft Sicherheit: Wo der Leser antwortet, stimmt die Antwort.

## Offene Punkte für die Integration

1. **Menschliche Ablesung** der 95 Frames (`wahrheit.txt`, Kontaktblätter
   liegen bereit). Erst danach gelten die Zahlen.
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
