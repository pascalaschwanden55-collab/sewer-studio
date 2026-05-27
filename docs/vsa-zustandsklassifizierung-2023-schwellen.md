# VSA-Zustandsklassifizierung 2023 - Schwellen und Anwendungsregeln

**Status:** Draft  
**Quelle:** `D:\Fachwissen\2_1_4_7_4 Zustandsbeurteilung\VSA_Rili_ Zustandsbeurteilung von Entwässerungsanlagen.pdf`  
**Bezug:** ADR-007

Diese Datei ist die menschlich pruefbare Zwischenquelle fuer die spaetere
strukturierte JSON-Tabelle. Sie wird aus der VSA-Richtlinie 2023 gelesen und
bewusst nicht aus `classification_channels.json` abgeleitet.

## Format

| Feld | Bedeutung |
|---|---|
| `Code` | VSA-Hauptcode oder Hauptcodegruppe |
| `Titel` | Tabellen-Titel aus der Richtlinie |
| `Ch1` | erlaubte Charakterisierung 1 |
| `Ch2` | erlaubte Charakterisierung 2 |
| `Anforderung` | `D`, `S` oder `B` |
| `Einheit` | Einheit der Quantifizierung |
| `EZ 0..4` | Einzelzustand-Schwellen aus der Richtlinie |
| `Geltung` | z. B. biegesteif/biegeweich/DN-Grenze |
| `Quelle` | PDF-Seite und Tabelle |

## Anhang C - Kanaele und Entwaesserungsleitungen

### BAA - Verformung

Quelle: PDF-Dateiseite 22 / gedruckte Seite 20, Anhang C, Tabelle 7:
Verformung.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAA | A,B | - | S | % | >= 7 | >= 4 und < 7 | >= 3 und < 4 | >= 1 und < 3 | < 1 | biegesteif |
| BAA | A,B | - | S | % | >= 15 | >= 10 und < 15 | >= 6 und < 10 | >= 2 und < 6 | < 2 | biegeweich |
| BAA | A,B | - | B | % | >= 50 | >= 40 und < 50 | >= 25 und < 40 | >= 10 und < 25 | < 10 | alle |

Hinweise:

- Fuer `BAA` gibt es in Tabelle 7 keine `D`-Bewertung.
- Die aktuelle Legacy-Datei `classification_channels.json` ist fuer `BAA`
  fachlich falsch: Sie modelliert `BAA` als Riss mit Rissbreite in mm.
- `BAA` darf deshalb nicht als einfache Code-Regel modelliert werden. Der Scope
  biegesteif/biegeweich ist fuer `S` entscheidend.

### BAB - Risse

Quelle: PDF-Dateiseite 22 / gedruckte Seite 20, Anhang C, Tabelle 8: Risse.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAB | A | A,B,C,D,E | S | keine Quantifizierung | - | - | - | - | alle | alle |
| BAB | B,C | A,C,D,E | S | mm | >= 8 | >= 5 und < 8 | >= 3 und < 5 | >= 1 und < 3 | < 1 | alle |
| BAB | B,C | B | S | mm | - | - | - | - | alle | alle |
| BAB | B | A,B,C,D,E | D | mm | - | - | alle | - | - | alle |
| BAB | C | A,B,C,D,E | D | mm | - | alle | - | - | - | alle |

Hinweise:

- `BAB` ist Riss, nicht Bruch/Einsturz.
- Die aktuelle Legacy-Datei `classification_channels.json` ist fuer `BAB`
  fachlich falsch: Sie modelliert `BAB` als Bruch/Einsturz mit Ausmass in %.
- `BAB` braucht `Ch1/Ch2` als Diskriminator. Eine einzelne Code-Regel reicht
  nicht.

### BAC - Leitungsbruch/Einsturz

Quelle: PDF-Dateiseite 22 / gedruckte Seite 20, Anhang C, Tabelle 9:
Leitungsbruch/Einsturz.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAC | A | - | D | mm | - | alle | - | - | - | alle |
| BAC | A | - | S | mm | - | - | alle | - | - | alle |
| BAC | A | - | B | mm | - | - | alle | - | - | alle |
| BAC | B | - | D | mm | - | alle | - | - | - | alle |
| BAC | B | - | S | mm | - | - | alle | - | - | alle |
| BAC | C | - | D | mm | alle | - | - | - | - | alle |
| BAC | C | - | S | mm | alle | - | - | - | - | alle |
| BAC | C | - | B | mm | alle | - | - | - | - | alle |

Hinweise:

- `BAC` ist Leitungsbruch/Einsturz.
- Die aktuelle Legacy-Datei `classification_channels.json` ist fuer `BAC`
  mindestens zu grob: Sie modelliert Q1-Prozentbereiche, waehrend Tabelle 9
  fixe Einzelzustaende nach `Ch1` und Anforderung vorgibt.

### BAD - Defektes Mauerwerk

Quelle: PDF-Dateiseite 23 / gedruckte Seite 21, Anhang C, Tabelle 10:
Defektes Mauerwerk.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAD | A | - | D | - | - | - | alle | - | - | alle |
| BAD | A | - | S | - | - | - | alle | - | - | alle |
| BAD | A | - | B | - | - | - | - | alle | - | alle |
| BAD | B | - | D | - | - | - | alle | - | - | alle |
| BAD | B | - | S | - | - | - | alle | - | - | alle |
| BAD | C | - | D | mm | alle | - | - | - | - | alle |
| BAD | C | - | S | mm | alle | - | - | - | - | alle |
| BAD | C | - | B | mm | alle | - | - | - | - | alle |
| BAD | D | - | D | - | alle | - | - | - | - | alle |
| BAD | D | - | S | - | alle | - | - | - | - | alle |
| BAD | D | - | B | - | alle | - | - | - | - | alle |

### BAE - Fehlender Moertel

Quelle: PDF-Dateiseite 23 / gedruckte Seite 21, Anhang C, Tabelle 11:
Fehlender Moertel.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAE | - | - | D | mm | - | - | >= 100 | - | < 100 | alle |
| BAE | - | - | S | mm | - | - | >= 100 | >= 10 und < 100 | < 10 | alle |

### BAF - Oberflaechenschaden

Quelle: PDF-Dateiseite 24 / gedruckte Seite 22, Anhang C, Tabelle 12:
Oberflaechenschaden.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAF | A | A,B,C,D,E,Z | S | - | - | - | - | - | alle | alle |
| BAF | A | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | B | A,E,Z | S | - | - | - | - | alle | - | alle |
| BAF | B | A,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | C | A,B,C,D,E,Z | S | - | - | - | - | alle | - | alle |
| BAF | C | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | D | A,B,C,D,E,Z | S | - | - | - | alle | - | - | alle |
| BAF | D | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | E | A,B,C,D,E,Z | S | - | - | alle | - | - | - | alle |
| BAF | E | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | F | A,B,C,D,E,Z | S | - | - | - | - | alle | - | alle |
| BAF | F | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | G | A,B,C,D,E,Z | S | - | - | - | alle | - | - | alle |
| BAF | G | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | H | B,C,D,E | S | - | - | alle | - | - | - | alle |
| BAF | H | B,C,D,E | B | - | - | - | - | - | alle | alle |
| BAF | I | A,B,C,D,E,Z | D | - | - | alle | - | - | - | alle |
| BAF | I | A,B,C,D,E,Z | S | - | - | alle | - | - | - | alle |
| BAF | I | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | J | B,C,D,E,Z | S | - | - | - | - | - | alle | alle |
| BAF | J | B,C,D,E,Z | B | - | - | - | - | - | alle | alle |
| BAF | K | A,E,Z | B | - | - | - | - | alle | - | alle |
| BAF | Z | A,B,C,D,E,Z | D | - | - | - | - | - | alle | alle |
| BAF | Z | A,B,C,D,E,Z | S | - | - | - | - | - | alle | alle |
| BAF | Z | A,B,C,D,E,Z | B | - | - | - | - | - | alle | alle |

Hinweise:

- `BAF` ist Oberflaechenschaden, nicht Verformung.
- Die aktuelle Legacy-Datei `classification_channels.json` ist fuer `BAF`
  zu grob: Sie modelliert Prozent-Schwellen, waehrend Tabelle 12 nach `Ch1`,
  `Ch2` und Anforderung feste Einzelzustaende vorgibt.

### BAG - Einragender Anschluss

Quelle: PDF-Dateiseite 25 / gedruckte Seite 23, Anhang C, Tabelle 13:
Einragender Anschluss.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAG | - | - | B | % | >= 50 | >= 30 und < 50 | >= 20 und < 30 | >= 10 und < 20 | < 10 | <= DN 250 |
| BAG | - | - | B | % | >= 80 | >= 60 und < 80 | >= 40 und < 60 | >= 10 und < 40 | < 10 | > DN 250 |

### BAH - Schadhafter Anschluss

Quelle: PDF-Dateiseite 25 / gedruckte Seite 23, Anhang C, Tabelle 14:
Schadhafter Anschluss.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAH | B,C,D | - | D | - | - | - | alle | - | - | alle |
| BAH | A,E | - | B | - | - | - | - | - | alle* | alle |
| BAH | Z | - | D | - | - | - | - | alle | - | alle |
| BAH | Z | - | S | - | - | - | - | alle | - | alle |

Hinweis: `alle*` verweist auf die Fussnote der Richtlinie: Beim schadhaften
Anschluss handelt es sich um ein eigenes Inspektionsobjekt. Die Funktion der
Anschlussleitung sollte ggf. ueberprueft oder die Anschlussleitung inspiziert
werden.

### BAI - Einragendes Dichtungsmaterial

Quelle: PDF-Dateiseite 25 / gedruckte Seite 23, Anhang C, Tabelle 15:
Einragendes Dichtungsmaterial.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAI | A | A | D | - | - | - | alle | - | - | alle |
| BAI | A | A | B | - | - | - | - | - | alle | alle |
| BAI | A | B,C,D | D | - | - | - | alle | - | - | alle |
| BAI | A | B,C,D | B | - | - | - | - | alle | - | alle |
| BAI | Z | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | alle |

### BAJ - Verschobene Rohrverbindung

Quelle: PDF-Dateiseite 26 / gedruckte Seite 24, Anhang C, Tabelle 16:
Verschobene Rohrverbindung.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAJ | A | - | D | mm | >= 70 | >= 50 und < 70 | >= 30 und < 50 | >= 20 und < 30 | < 20 | <= DN 400 |
| BAJ | A | - | D | mm | >= 80 | >= 60 und < 80 | >= 40 und < 60 | >= 20 und < 40 | < 20 | > DN 400 |
| BAJ | A | - | S | mm | - | - | - | - | alle | alle |
| BAJ | B | - | D | mm | >= 30 | >= 20 und < 30 | >= 15 und < 20 | >= 10 und < 15 | < 10 | alle |
| BAJ | B | - | S | mm | missing-vsa-source | missing-vsa-source | missing-vsa-source | missing-vsa-source | missing-vsa-source | PDF-Zeile ohne sichtbaren Einzelzustand |
| BAJ | B | - | B | mm | - | - | - | >= 10 | < 10 | alle |
| BAJ | C | - | D | deg | >= 12 | >= 9 und < 12 | >= 7 und < 9 | >= 5 und < 7 | < 5 | <= DN 200 |
| BAJ | C | - | D | deg | >= 6 | >= 4 und < 6 | >= 3 und < 4 | >= 2 und < 3 | < 2 | > DN 200 |
| BAJ | C | - | S | deg | - | - | - | - | alle | PDF-Zeile zeigt `alle` vor dem S-x; fachlich pruefen |

Hinweise:

- Die Fussnote der Richtlinie sagt, dass die Geometrie von Rohrverbindungen je
  nach Verbindungsart, Werkstoff und Baujahr stark variiert. Konkrete
  Erkenntnisse sollen in die Klassifizierung einfliessen.
- Zwei BAJ-Zeilen sind bewusst als pruefbeduerftig markiert. Sie werden nicht
  geraten.

## Noch offen

- Restliche Kanaltabellen aus Anhang C: Tabellen 17-32.
- Schachttabellen aus Anhang D: Tabellen 33-63.
- Klaeren, aus welchen Stammdaten `biegesteif`/`biegeweich` im Programm sicher
  bestimmt wird.
