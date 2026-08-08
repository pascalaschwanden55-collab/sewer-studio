# OSD-Kontrollexperiment — Plan und Teilergebnis, 2026-08-08

Auslöser: Der eingebrannte OSD-Text (Strasse/Durchmesser oben links, ID oben
rechts, Datum unten links, Meter unten rechts) kann vom Modell als Abkürzung
gelernt werden — Text lesen statt Schaden sehen. Diese Entscheidung muss vor
der BAH-Massensammlung fallen, weil eine nötige Maskierung den Zuschnitt jedes
Trainingsbildes ändert.

## Teil A — Verdeckt das OSD die Schäden? (bereits beantwortet)

Reine Geometrie über den aktiven Export `3f45c1e945fe…` (997 Boxen,
normalisierte YOLO-Koordinaten) gegen grosszügige OSD-Zonen in den vier
Ecken:

| Messgrösse | Wert |
|---|---:|
| Boxen gesamt | 997 |
| davon > 25 % der Boxfläche in einer OSD-Zone | **32 (3,2 %)** |
| davon > 50 % | 20 (2,0 %) |

**Befund:** Verdeckung ist ein Randphänomen. Das OSD sitzt in den Ecken, die
Boxen in der Bildmitte. Eine Maskierung wegen Verdeckung ist **nicht** nötig.

Nebenbefund, der in die Label-Anleitung gehört, nicht hierher: Die Boxen sind
gross — median 32 % der Bildfläche, ein Viertel über 62 %. Kein Fehler, aber
bei künftigen Korrekturrunden wäre „eng am Schaden" eine sinnvolle Vorgabe.

## Teil B — Liest das Modell den Text? (noch offen)

Geometrie beantwortet die Lese-Frage nicht: YOLO sieht das ganze Bild, die
Ecke kann als Merkmal dienen, ohne die Box zu berühren. Also Modellversuch
über `detect_benchmark_v1` (417 Bilder), drei Varianten, **dasselbe Modell**
(nc:15, gepinnter Seed) — Inferenz ist deterministisch, jede Differenz ist
echt, kein Seed-Rauschen:

- **V0 Original** — Referenz, liegt bereits vor.
- **V1 maskiert** — erkannte Schriftzeichen unscharf (adaptiv je Bild, nur
  Glyphenpixel — kein Balken, der Rohrwand entfernt).
- **V2 vertauscht** — die eigenen OSD-Streifen werden in 32-px-Blöcken
  streifenweit verwürfelt (gleiche Pixel, gleiche Stellen, Inhalt
  unleserlich — kein Fremdmaterial, kein Stilbruch).
- **V3 Kontrolle (nur bei Bedarf)** — dieselbe Verwürfelung auf den
  seitlichen Randstreifen mittlerer Höhe: kein Text, üblicherweise glatte
  Rohrwand. Beantwortet die Störfrage, ob Boxen schon durch beliebige
  Pixeländerung kippen, ohne dass ein Buchstabe gelesen wurde. Wird nur
  gefahren, wenn V2 tatsächlich abfällt; bei V2 ≈ V0 entfällt sie.

Die Texterkennung braucht keine festen Zonen: helle Kleinkomponenten im
oberen und unteren Bildrahmen (6–34 px hoch). Feste Eckzonen verfehlen fremde
OSD-Layouts — in der Sichtprobe blieb damit Text lesbar.

Gemessen wird bei den beiden Protokoll-Punkten conf 0,25 (Standbild) und
0,50 (Videoweg): TP der 37 BCC-Sollboxen, Fehlalarme auf 75 Negativbildern,
Feuer auf 220 Fremdschaden-Bildern.

**Entscheidungsregel, vor dem Lauf festgelegt:**

| Befund | Deutung | Konsequenz |
|---|---|---|
| V1 ≈ V0 (±2 TP, ±2 FA) und V2 ≈ V0 | OSD wird ignoriert | nichts tun, nie wieder drüber reden |
| V1 fällt, V2 ≈ V0 | Reaktion auf die Maskierung selbst | Füllmethode prüfen, dann erneut |
| V2 fällt > 2 TP | zunächst unentschieden | **V3 fahren** |
| V3 fällt wie V2 | allgemeine Pixelempfindlichkeit, kein Lesen | OSD-Thema geschlossen |
| nur V2 fällt | **Das Modell liest den Text** | OSD-Bereiche beim Training randomisiert ersetzen (Augmentation), nicht löschen |

Die Schwelle 2 TP ist gegen die deterministische Inferenz kalibriert (derselbe
Lauf ergäbe dieselbe Zahl); die Kipper-Störgrösse deckt V3 ab.

**Warnung zur Einordnung:** Die V0-Zahlen dieses Versuchs gelten nur intern.
Der Abgleich hier ist gierig je Sollbox (IoU ≥ 0,5); der offizielle Auswerter
ordnet nach maximaler Trefferzahl und maximalem Gesamt-IoU zu. V0 darf nie
neben Werten aus `messung_benchmark_v1.json` stehen — das wäre ein Vergleich
zweier Messverfahren, nicht zweier Modelle.

**Ablaufhinweis:** 1251 Inferenzen (417 × 3), auf CPU grob 15–25 min, während
die Seeds die GPU belegen; V3 (417) kommt bei Bedarf gleichartig dazu. Das
Skript (`training/scripts/osd_kontrollexperiment.py`) ist schreibfrei gegenüber
den Beständen; Varianten entstehen in einem Diagnoseordner.

## Ergebnis (2026-08-08, CPU, nc:15 Seed 44, 332 gewertete Bilder)

| conf | V0 Original | V1 unscharf | V2 verwürfelt | V3 Kontrolle |
|---:|---:|---:|---:|---:|
| 0,10 | 30 | 30 | 29 | 30 |
| 0,25 | 28 | 27 | 26 | **26** |
| 0,50 | 22 | 21 | 22 | 22 |
| FA Negative (0,25) | 2 | 2 | 2 | 1 |
| FA Fremdschaden (0,25) | 14 | 14 | 14 | 15 |

**Entscheid nach der festgelegten Regel:** V2 fiel mit −2 genau auf die
Zweifelsschwelle, also lief V3 — und V3 fällt **gleich** (−2 bei 0,25,
unverändert bei 0,10/0,50). Der Dip ist allgemeine Pixelempfindlichkeit,
kein Lesen. **Das Modell liest den OSD-Text nicht.**

Konsequenz: keine Maskierung, keine OSD-Augmentation, kein Eingriff in die
Bilder vor dem Training. Die Frage ist geschlossen; die BAH-Sammlung und alle
künftigen Trainings laufen auf den Originalbildern.

Rohdaten: `C:\KI_BRAIN\training\diagnostics\osd_kontrollexperiment_20260808\
ergebnis.json` (inkl. der Warnung, dass V0 nur versuchsintern gilt).
