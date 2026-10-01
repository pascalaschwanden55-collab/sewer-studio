# Trainierbarer OSD-Meterleser — Entwurf

**Stand:** 2026-08-15 · **Status:** freigegeben zur Planung

Der Meterleser ist der gemessene Engpass für alles Meterbezogene. Der heutige
`sidecar/sidecar/osd_meter.py` ist ein fester Vorlagenleser: 801 Zeilen, für jeden
neuen Anzeigestil braucht er eine neue Regel von Hand. Nach der
Auflösungsreparatur vom 2026-08-14 liest er 45,8 % des Archivs und auf dem
eingefrorenen Gold 138 von 197 richtig — bei **null falschen Werten**.

Dieser Entwurf ergänzt ihn um ein trainiertes Lesemodell. Er ersetzt ihn nicht.

---

## 1. Entscheidungen

Getroffen im Gespräch am 2026-08-15:

| Frage | Entscheidung |
|---|---|
| Fehlerregel | **Null falsch bleibt absolut.** Ein falscher Wert ist teurer als zehn fehlende. 170 richtig / 2 falsch wäre ein Rückschritt gegenüber 138 richtig / 0 falsch. |
| Lesemodell | **Zeichen-Detektor auf YOLO.** Jedes Zeichen ist ein Objekt mit eigener Sicherheit; nutzt den vorhandenen Ultralytics-Stapel, die Trainingsskripte und den Modellplatz-Mechanismus im Sidecar. |
| Handbeschriftung | **200 schwere Fälle, ein Abend** — aber erst nach der ersten Messung, damit der Beitrag der Handarbeit sichtbar wird. |
| Freigabemarke | Null falsch auf allen drei Goldsätzen **und** mindestens **170 von 197** richtig. |

Verworfen: CRNN mit CTC (zweiter Trainings-Werkzeugkasten, Sicherheit nur je
Zeichenkette statt je Zeichen), fertiges OCR feinabstimmen (~330 MB statt ~6 MB,
hängt an der wegen Grounding DINO auf 4.57.6 festgenagelten
Transformer-Bibliothek, zu langsam für 1 Bild/Sekunde über ganze Videos).

---

## 2. Der Ablauf

```
Videobild
   │
   ├─ 1. FINDEN   unverändert: ZONEN unten rechts (0.62, 0.84, 1.00, 1.00)
   │              → Ausschnitt (roh, nicht binarisiert)
   │
   ├─ 2. LESEN    NEU: Zeichen-Detektor
   │              → Boxen je Zeichen + Klasse + Sicherheit
   │              → nach x sortiert → "LZ1: 9.42m"
   │
   └─ 3. DEUTEN   unverändert: parse_meter()
                  → 9.42  oder  None
```

**`parse_meter` bleibt unverändert.** Dort stecken zwei teuer erkaufte Regeln:
keine Ziffer hinter der Einheit, höchstens ein Dezimalpunkt — beides heisst
verwerfen, nicht raten. Ohne sie wurde aus `LZ:::6.4m3` eine 6,4 statt 26,4 und
aus `ZLZ1:.0.1m` eine 0,1 statt −0,1. Das Modell liefert nur eine bessere
Zeichenkette in dieselbe Prüfung.

### Einbau in die bestehende Kette

`lese_meter()` hat heute die Reihenfolge Vorlagen → Tesseract-Vierziffern →
Tesseract-Zwei-Dezimal. Das Modell wird ein **weiteres Glied hinter dem
Vorlagenweg**.

Damit gilt: Wo der Vorlagenweg heute vollständig liest, ändert sich bitgenau
nichts. Die 138 richtigen Werte sind gegen Regression immun; das Modell kann im
schlechtesten Fall null beitragen, aber nichts kaputt machen.

Die genaue Position innerhalb der Rückfallkette (vor oder nach den beiden
Tesseract-Wegen) wird nach der ersten Messung festgelegt — wer mehr beiträgt,
kommt weiter nach vorn. In Stufe 1 steht das Modell am Ende.

---

## 3. Das Modell

- **15 Klassen:** `0123456789.mLZ:` — exakt der Zeichenvorrat der Konstanten
  `ZEICHEN` in `osd_meter.py`. Bewusst **ohne Minus**: Als eigenes Zeichen
  aufgenommen kostete es sieben richtige Lesungen und rettete eine. Negative
  Zählerstände vor dem Rohranfang bleiben mehrdeutig und werden nicht gelesen.
- **Grösse:** Nano-Klasse (~6 MB). Ein Ausschnitt von rund 400×40 Pixeln braucht
  kein grosses Netz.
- **Skalennormierung vor der Inferenz:** Der Ausschnitt wird auf eine feste
  Zeichenhöhe gebracht, bevor das Modell ihn sieht. SD und HD sehen dadurch
  gleich aus.

Der letzte Punkt zielt auf die Lehre aus dem Fehler vom 2026-08-14: Die
Abstandsschranken der Zeichenfindung standen als feste Pixelwerte da, eingestellt auf
SD mit rund 18 Pixel hohen Ziffern. Auf HD sind dieselben Zeichen doppelt so
gross, und der Leser verlor Dezimalpunkt und Einheit (`LZ1: 3.2m` → `L132`).

> **Korrektur vom 2026-08-16 — die ursprüngliche Behauptung war falsch.**
> Hier stand, die Normierung schliesse diese Fehlerklasse „durch die Bauart" aus.
> Nachgemessen, was bei `imgsz=320` tatsächlich beim Modell ankommt:
>
> | Zone | ohne Normierung | mit Normierung |
> |---|---|---|
> | SD 576 (273×92) | 21,1 px | 21,1 px |
> | HD 720 (486×115) | 14,8 px | 14,8 px |
> | HD 1080 (729×172) | 14,8 px | 14,8 px |
>
> Ultralytics letterboxt ohnehin seitenverhältnistreu auf `imgsz`; die Normierung
> tut dasselbe, und die Verkettung landet am selben Punkt. Sie leistet also eine
> feste, vom Letterbox-Verhalten unabhängige Eingangsgrösse — **nicht** aber gleiche
> Zeichenhöhe über verschiedene Bildseitenverhältnisse. SD und HD unterscheiden sich
> weiterhin um Faktor 1,4, weil die Zone bei 5:4 und 16:9 verschiedene Proportionen
> hat.
>
> Echte Gleichheit bräuchte ein festes Ziel-Seitenverhältnis mit Polsterung, quer
> durch Ernte, Kunstbilder und Inferenz. Das bleibt für Stufe 1 bewusst offen: Der
> gemessene Rest liegt im Bereich, den ein Detektor über seine Mehrskalen-Augmentierung
> trägt, und der Ausfall vom 14.08. war kategorisch (keine Schranke traf mehr), nicht
> ein Faktor 1,4. Die Goldmessung weist SD, HD und HD2 getrennt aus und würde einen
> echten Nachteil auf HD sichtbar machen.

---

## 4. Trainingsdaten

Drei Quellen. Keine davon fasst die Kundenoriginale an.

### 4.1 Lehrer-Ernte (mehrere tausend, kostenlos)

Der heutige Leser wird über Archivbilder gefahren. Überall wo er eine
**vollständige** Lesung liefert (Zweig `vorlagen`, `_zeichenfolge_ist_vollstaendig`
wahr), sind Ausschnitt, Zeichenboxen und Zeichen exakte Wahrheit — dieser Zweig
hat auf dem gesamten Goldbestand null falsche Werte.

`boxen_aus_maske()` liefert die Boxen bereits; sie müssen nur mitgeschrieben
werden. Damit fallen YOLO-Labels ohne Zusatzarbeit an.

**Grenze:** Diese Quelle lehrt nur, was der Lehrer schon kann. Sie allein hebt
die Abdeckung nicht. Dafür sind 4.2 und 4.3 da.

### 4.2 Künstlich erzeugte Anzeigen (beliebig viele, kostenlos)

Nachbau der Stile aus der 40er-Sichtung vom 2026-08-14:

| Merkmal | Verteilung in der Sichtung |
|---|---|
| Lage | 38 unten rechts, 2 unten links, 0 oben |
| Polarität | 18 hell auf dunkel, 18 dunkel auf hell, 4 andere |
| Farbe | 20 weiss/grau, 7 gelb, 13 andere |
| Format | 19 mit Präfix/führenden Nullen, 15 mit Einheit, 6 ohne Einheit |

Gerendert über echte Videohintergründe (aus geschützten Bereichen
ausgeschlossen), mit Rauschen, Kompressionsartefakten und Unschärfe. Die
Wahrheit ist per Konstruktion exakt, inklusive Boxen.

Die Stichprobe ist klein (40 Haltungen) — sie belegt mehrere Hauptstile, aber
keine exakten Archivanteile. Die künstliche Verteilung wird deshalb bewusst
breiter gezogen als die gemessene.

### 4.3 Handbeschriftete schwere Fälle (200, ein Abend)

Gezielt aus Bildern gezogen, an denen der heutige Leser **scheitert**. Genau die
Lücke, die 4.1 nicht abdecken kann.

Eingabeplatz nach dem Muster der bestehenden Prüfplätze unter
`tools/EvalVisibilityReview/`: Bild anzeigen, Zeichenkette abtippen, Urteil
`unleserlich` möglich. Kein Modellvorschlag sichtbar.

**Zeitpunkt:** erst nach der Messung von Stufe 1 (siehe Abschnitt 8).

### 4.4 Schutz und Aufteilung

Aus **jeder** Trainingsquelle ausgeschlossen:

- die 197 Goldbilder, geprüft über `bild_sha256` aus den drei Manifesten
- ihre Haltungen **in beiden Richtungen**, über normalisierte Haltungsnummer
- alle bestehenden Eval-Set-Haltungen nach der Regel aus dem Detect-Register

Splits gehen nach **physischer Haltung**, nie nach Bild. Bytegleiche Bilder
werden nur einmal aufgenommen und binden ihre Haltungen zu einer gemeinsamen
Split-Gruppe zusammen — dasselbe Verfahren wie in `gold_stock_audit.py`.

---

## 5. Die Null-Fehler-Schwelle

Jedes Zeichen bringt seine Sicherheit mit. Die Sicherheit einer Lesung ist die
**kleinste** Zeichensicherheit — ein wackliges Zeichen macht die ganze Lesung
wacklig. Zusätzlich müssen die Grammatikregeln aus `parse_meter` durchgehen.
Beides zusammen entscheidet über Wert oder `None`.

### Die Kalibrierung darf Gold nicht berühren

Wer die Schwelle so lange dreht, bis auf Gold null Fehler stehen, hat Gold zum
Anpassen benutzt und misst danach sich selbst. Die Zahl wäre wertlos.

Verfahren:

1. Schwelle auf einem **getrennten Reservebestand** einstellen. In Stufe 1 ist das
   der Testteil der 897 schwach beschrifteten Bilder; ab Stufe 2 kommt ein
   abgetrennter Teil der Handfälle dazu.

   Die 897 taugen dafür, weil hier nur die Frage zählt, ob eine Lesung grob
   danebenliegt — dafür reicht ein auf wenige Zentimeter genaues Etikett. Als
   Zeichenwahrheit fürs Training sind sie weiterhin gesperrt (Abschnitt 10).
2. Schwelle einfrieren und in das Kandidatenmanifest schreiben.
3. **Danach einmal** `osd_goldmessung.py` laufen lassen. Das Ergebnis gilt.

Eine zweite Goldmessung mit nachjustierter Schwelle ist keine unabhängige
Messung mehr und wird als solche gekennzeichnet.

---

## 6. Einbau in den Sidecar

- Eigener Modellplatz `YOLO_OSD` neben YOLO, YOLO_TEST, DINO, SAM. Wenige MB
  VRAM; das 29-GB-Budget bleibt unberührt.
- Nutzt die am 2026-08-15 gehärtete Slot-Logik: `SlotState.content_id` mit dem
  Gewichts-SHA-256, `ensure_loaded(..., content_id=...)`.
- Gewicht an Dateiname **und** SHA-256 gebunden, wie bei den BCC-Kandidaten.
  Hashabweichung sperrt fail-closed.
- Kandidat startet als `diagnostic_not_deployed` und läuft erst nach
  ausdrücklicher Freigabe mit.

### Die Schnittstelle zu C# ändert sich nicht

`meter_value` bleibt Zahl oder `None`; `None` heisst „nicht lesbar", niemals 0,0.
Im Antwort-Wörterbuch von `lese_meter()` trägt das vorhandene Feld `leseweg`
künftig zusätzlich den Wert `"modell"`.

`MeterSequencePlausibility` und `MeterSequenceGapFiller` auf C#-Seite bleiben
unangetastet. Die Lesung bleibt roh und zustandslos; Sequenz-Plausibilität und
Lückenfüllen bleiben C#-Sache.

---

## 7. Messung und Freigabe

Beide Werkzeuge existieren und werden unverändert benutzt.

| Werkzeug | Misst | Rolle |
|---|---|---|
| `training/scripts/osd_goldmessung.py` | richtig / falsch / nicht gelesen gegen die drei eingefrorenen Goldsätze | **Tor** |
| `training/scripts/osd_archiv_abdeckung_messung.py` | ob überhaupt gelesen wurde, 83 Videos × 20 Stellen | Bericht, kein Tor |

**Freigabe nur wenn beides gilt:**

1. **Null falsch** auf allen drei Goldsätzen. Nicht verhandelbar.
2. Mindestens **170 von 197** richtig (heute 138).

Die Archivabdeckung misst nur *ob* gelesen wurde, nie *ob richtig*. Sie ist
deshalb Bericht und niemals Freigabegrund.

Es gilt die Regel aus dem Goldmanifest: **ein Lauf ändert genau eine Sache —
Leser oder Bestand.**

---

## 8. Stufen

Jede Stufe endet mit einer Zahl, nicht mit einem Gefühl.

**Stufe 1 — Grundaufbau ohne deine Zeit** *(Umfang des ersten Umsetzungsplans)*
Ernte-Werkzeug, künstlicher Erzeuger, Trainingsskript, Kalibrierung,
Goldmessung. Ergebnis: eine Zahl auf Gold und eine auf dem Archiv.

**Stufe 2 — Handfälle, nur falls nötig**
Zeigt Stufe 1, dass genau die schweren Stile fehlen: Eingabeplatz bauen, deine
200 Fälle abtippen, nachtrainieren, erneut messen. Der Beitrag der Handarbeit
wird dadurch als Differenz sichtbar.

**Stufe 3 — Einbau und Freigabe**
Modellplatz im Sidecar, Hashbindung, Kandidatenmanifest, Kette umstellen.
Erst wenn die Marke aus Abschnitt 7 erreicht ist.

---

## 9. Was bewusst nicht gebaut wird

- **Kein gelerntes Finden.** Die Zonenlogik trifft 38 von 40. Kein Problem, das
  gelöst werden muss.
- **Kein Ersatz des Vorlagenlesers.** Er bleibt vorne in der Kette.
- **Keine neue C#-Schnittstelle.** Auf der Programmseite ist nichts anzufassen.
- **Keine negativen Zählerstände.**
- **Kein Anfassen der Sequenzlogik.**
- **Keine zweite Zone unten links.** Am 2026-08-09 entfernt, weil dort in vielen
  Videos das Aufnahmedatum steht und Tesseract `05.09.2023` statt des
  Meterstands las. Die 2 von 40 Haltungen wiegen das Risiko nicht auf.

---

## 10. Risiken

| Risiko | Gegenmassnahme |
|---|---|
| Die Ernte lehrt nur, was der Lehrer kann → Abdeckung steigt kaum | Stufe 1 misst genau das. Ist die Differenz klein, ist Stufe 2 die Antwort. |
| Künstliche Bilder treffen die echten Stile nicht | Verteilung breiter als die gemessene Stichprobe; Gold entscheidet, nicht die Trainingsverteilung. |
| Schwelle wird unbemerkt an Gold angepasst | Getrennter Reservebestand, Schwelle vor der Goldmessung eingefroren und im Manifest festgehalten. |
| Schwach beschriftete 897 werden als Zeichenwahrheit missbraucht | Sie sind auf 1 cm nur in 25 von 30 Fällen richtig (Sichtprobe) und werden **nie** als Zeichenwahrheit verwendet — nur für Auswahl, Splits und Reservebestand. |
| Modell wird versehentlich produktiv | `diagnostic_not_deployed`, Hashbindung, ausdrückliche Freigabe. |

---

## 11. Offene Punkte

- Genaue Zielgrösse der Skalennormierung (feste Zeichenhöhe in Pixeln) — wird in
  Stufe 1 empirisch bestimmt, nicht vorab gesetzt.
- Endgültige Position des Modells in der Rückfallkette — nach der ersten Messung.
- Ob Stufe 2 überhaupt nötig ist — entscheidet die Zahl aus Stufe 1.
