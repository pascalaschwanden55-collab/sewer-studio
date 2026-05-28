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

### BAK - Feststellung der Innenauskleidung

Quelle: PDF-Dateiseite 27 / gedruckte Seite 25, Anhang C, Tabelle 17:
Feststellung der Innenauskleidung.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAK | A | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | alle |
| BAK | B | - | D | - | - | - | - | - | alle | alle |
| BAK | C | - | D | - | - | - | alle | - | - | alle |
| BAK | C | - | B | - | - | - | alle | - | - | alle |
| BAK | D | A,B,D | B | % | - | - | - | alle | - | alle |
| BAK | D | C | S | % | - | - | alle | - | - | alle |
| BAK | D | C | B | % | - | - | alle | - | - | alle |
| BAK | E | - | S | % | - | - | - | alle | - | alle |
| BAK | E | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | alle |
| BAK | F | - | S | mm | - | - | - | alle | - | alle |
| BAK | G | - | B | - | - | - | - | - | alle | alle |
| BAK | H | - | B | - | - | - | - | - | alle | alle |
| BAK | I | - | D | mm | - | - | alle | - | - | alle |
| BAK | J | - | D | mm | - | alle | - | - | - | alle |
| BAK | K | - | D | - | - | - | alle | - | - | alle |
| BAK | K | - | B | - | - | - | alle | - | - | alle |
| BAK | L | - | D | - | - | - | - | alle | - | alle |
| BAK | L | - | S | - | - | - | - | alle | - | alle |
| BAK | M | - | D | - | - | - | alle | - | - | alle |
| BAK | N | - | D | - | - | - | alle | - | - | alle |
| BAK | Z | - | D | % | - | - | - | alle | - | alle |
| BAK | Z | - | S | % | - | - | - | alle | - | alle |
| BAK | Z | - | B | % | - | - | - | alle | - | alle |

### BAL - Schadhafte Reparatur

Quelle: PDF-Dateiseite 27 / gedruckte Seite 25, Anhang C, Tabelle 18:
Schadhafte Reparatur.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAL | A | - | D | mm | - | alle | - | - | - | alle |
| BAL | B | - | D | mm | - | alle | - | - | - | alle |
| BAL | C | - | D | % | - | - | alle | - | - | alle |
| BAL | D | - | D | mm | - | - | alle | - | - | alle |
| BAL | E | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | alle |
| BAL | F | - | D | mm | - | alle | - | - | - | alle |
| BAL | G | A,B,C,D | D | mm | - | - | - | alle | - | alle |
| BAL | Z | - | D | - | - | - | alle | - | - | alle |
| BAL | Z | - | B | - | - | - | alle | - | - | alle |

### BAM - Schadhafte Schweissnaht

Quelle: PDF-Dateiseite 28 / gedruckte Seite 26, Anhang C, Tabelle 19:
Schadhafte Schweissnaht.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAM | A,C | - | D | - | - | - | alle | - | - | alle |
| BAM | A,C | - | S | - | - | - | - | alle | - | alle |
| BAM | B | - | D | - | - | - | alle | - | - | alle |
| BAM | B | - | S | - | - | - | - | - | alle | alle |

### BAN - Poroese Leitung

Quelle: PDF-Dateiseite 28 / gedruckte Seite 26, Anhang C, Tabelle 20:
Poroese Leitung.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAN | - | - | D | - | - | - | alle | - | - | alle |
| BAN | - | - | S | - | - | - | alle | - | - | alle |

### BAO - Boden sichtbar

Quelle: PDF-Dateiseite 28 / gedruckte Seite 26, Anhang C, Tabelle 21:
Boden sichtbar.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAO | - | - | D | - | - | alle | - | - | - | alle |
| BAO | - | - | S | - | - | alle | - | - | - | alle |

### BAP - Hohlraum sichtbar

Quelle: PDF-Dateiseite 28 / gedruckte Seite 26, Anhang C, Tabelle 22:
Hohlraum sichtbar.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BAP | - | - | D | - | - | alle | - | - | - | alle |
| BAP | - | - | S | - | alle | - | - | - | - | alle |

### BBA - Wurzeln

Quelle: PDF-Dateiseite 28 / gedruckte Seite 26, Anhang C, Tabelle 23:
Wurzeln.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBA | A,B,C | - | D | % | - | - | alle | - | - | alle |
| BBA | A,B,C | - | B | % | >= 30 | >= 20 und < 30 | >= 10 und < 20 | < 10 | - | alle |

Hinweis: Die aktuelle Legacy-Datei `classification_channels.json` ist fuer
`BBA` fachlich falsch: Sie modelliert `BBA` als Deformation.

### BBB - Anhaftende Stoffe

Quelle: PDF-Dateiseite 28 / gedruckte Seite 26, Anhang C, Tabelle 24:
Anhaftende Stoffe.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBB | A | - | D | % | - | - | - | alle | - | alle |
| BBB | A,B,C,Z | - | B | % | >= 30 | >= 20 und < 30 | >= 10 und < 20 | >= 5 und < 10 | < 5 | alle |

### BBC - Ablagerungen an der Rohrsohle

Quelle: PDF-Dateiseite 29 / gedruckte Seite 27, Anhang C, Tabelle 25:
Ablagerungen an der Rohrsohle.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBC | A,B | - | B | % | - | - | - | - | alle | alle |
| BBC | C,Z | - | B | % | >= 50 | >= 40 und < 50 | >= 25 und < 40 | >= 10 und < 25 | < 10 | alle |

### BBD - Eindringen von Bodenmaterial

Quelle: PDF-Dateiseite 29 / gedruckte Seite 27, Anhang C, Tabelle 26:
Eindringen von Bodenmaterial.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBD | A,B,C,D,Z | - | D | % | - | alle | - | - | - | alle |
| BBD | A,B,C,D,Z | - | S | % | alle | - | - | - | - | alle |
| BBD | A,B,C,D,Z | - | B | % | >= 30 | >= 20 und < 30 | >= 10 und < 20 | < 10 | - | alle |

### BBE - Andere Hindernisse

Quelle: PDF-Dateiseite 29 / gedruckte Seite 27, Anhang C, Tabelle 27:
Andere Hindernisse.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBE | D,G | - | D | % | - | - | alle | - | - | alle |
| BBE | A,B,C,D,E,F,G,H,Z | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | alle |

### BBF - Infiltration

Quelle: PDF-Dateiseite 29 / gedruckte Seite 27, Anhang C, Tabelle 28:
Infiltration.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBF | A,B | - | D | - | - | - | alle | - | - | alle |
| BBF | A,B | - | S | - | - | - | - | alle | - | alle |
| BBF | A,B | - | B | - | - | - | - | - | alle | alle |
| BBF | C | - | S | - | - | - | alle | - | - | alle |
| BBF | C,D | - | D | - | - | alle | - | - | - | alle |
| BBF | C,D | - | B | - | - | - | - | - | alle | alle |
| BBF | D | - | S | - | - | alle | - | - | - | alle |

### BBG - Exfiltration

Quelle: PDF-Dateiseite 29 / gedruckte Seite 27, Anhang C, Tabelle 29:
Exfiltration.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBG | - | - | D | - | - | alle | - | - | - | alle |
| BBG | - | - | S | - | - | - | - | alle | - | alle |

### BBH - Ungeziefer

Quelle: PDF-Dateiseite 30 / gedruckte Seite 28, Anhang C, Tabelle 30:
Ungeziefer.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BBH | A,B,Z | A,B,C,Z | B | Anzahl | - | - | - | - | alle* | alle |

Hinweis: `alle*` verweist auf die Fussnote der Richtlinie: nicht relevant fuer
die Leistungsfaehigkeit, aber ggf. betrieblich zu beheben.

### BDD - Wasserspiegel

Quelle: PDF-Dateiseite 30 / gedruckte Seite 28, Anhang C, Tabelle 31:
Wasserspiegel.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BDD | A,C,D,E | - | B | % | - | - | >= 50 | >= 10 und < 50 | < 10* | alle |

Hinweise:

- `BDD` ist Wasserspiegel, nicht Deformation.
- `*` verweist auf die Fussnote der Richtlinie: Es gilt die groesste
  Quantifizierung an der Station mit Streckencode C.

### BDE - Abwasserzufluss aus einem seitlichen Anschluss, Fehlanschluss

Quelle: PDF-Dateiseite 30 / gedruckte Seite 28, Anhang C, Tabelle 32:
Abwasserzufluss aus einem seitlichen Anschluss, Fehlanschluss.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| BDE | A,C,D,E,Y | A | B | % | - | alle | - | - | - | alle |
| BDE | A,C,D,E,Y | B | B | % | - | - | alle | - | - | alle |
| BDE | A,C,D,E,Y | C | B | % | - | - | alle* | - | alle* | fachlich pruefen: Tabelle zeigt `alle*` in EZ 2 und EZ 4 |
| BDE | Y | Y | B | % | - | - | - | - | alle | alle |

Hinweis: `alle*` verweist auf die Fussnote der Richtlinie: vorsorgliche
Klassifizierung in Einzelzustand 2, da vermeintlicher Fehlanschluss fuer
Inspekteur evtl. nicht erkennbar, nicht relevant oder es sich tatsaechlich
nicht um einen Fehlanschluss handelt.

## Anhang D - Schaechte

Hinweis: Anhang D wird bewusst nur abschnittsweise uebernommen. Die
Tabellenextraktion aus dem PDF verliert einzelne Zeilen; deshalb werden die
Eintraege gegen gerenderte PDF-Seiten visuell gegengeprueft.

### DAA - Verformung

Quelle: PDF-Dateiseite 31 / gedruckte Seite 29, Anhang D, Tabelle 33:
Verformung.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAA | A,B | - | S | % | >= 7 | >= 4 und < 7 | >= 3 und < 4 | >= 1 und < 3 | < 1 | Bereiche B,D,F; biegesteif |
| DAA | A,B | - | S | % | - | - | - | alle | - | Bereiche B,D,F; biegeweich |
| DAA | A,B | - | B | % | >= 40 | >= 30 und < 40 | >= 20 und < 30 | >= 10 und < 20 | < 10 | Bereiche B,D,F |

### DAB - Risse

Quelle: PDF-Dateiseite 32 / gedruckte Seite 30, Anhang D, Tabelle 34:
Risse.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAB | A | A,B | S | keine Quantifizierung | - | - | - | - | alle | Bereiche B,D,F,I,J |
| DAB | A | C,D,E | S | keine Quantifizierung | - | - | - | alle | - | Bereiche B,D,F,I,J |
| DAB | B,C | A | S | mm | >= 8 | >= 5 und < 8 | >= 3 und < 5 | >= 1 und < 3 | < 1 | Bereiche B,D,F |
| DAB | B,C | B | S | mm | - | - | - | - | alle | Bereiche B,D,F |
| DAB | B,C | C,D,E | S | mm | - | - | - | alle | - | Bereiche B,D,F |
| DAB | B | A,B,C,D,E | D | mm | - | - | - | alle | - | Bereiche D,F |
| DAB | B | A,B,C,D,E | D | mm | - | - | alle | - | - | Bereiche I,J |
| DAB | C | A,B,C,D,E | D | mm | - | - | alle | - | - | Bereiche D,F |
| DAB | C | A,B,C,D,E | D | mm | - | alle | - | - | - | Bereiche I,J |

### DAC - Bruch/Einsturz

Quelle: PDF-Dateiseite 32 / gedruckte Seite 30, Anhang D, Tabelle 35:
Bruch/Einsturz.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAC | A | - | D | mm | - | - | alle | - | - | Bereiche D,F |
| DAC | A | - | D | mm | - | alle | - | - | - | Bereiche I,J |
| DAC | A | - | S | mm | - | - | - | alle | - | Bereiche B,D,F |
| DAC | A | - | B | mm | - | - | - | alle | - | Bereiche B,D,F |
| DAC | B | - | D | mm | - | - | alle | - | - | Bereiche D,F |
| DAC | B | - | D | mm | - | alle | - | - | - | Bereiche I,J |
| DAC | B | - | S | mm | - | - | - | alle | - | Bereiche B,D,F |
| DAC | C | - | D | mm | - | alle | - | - | - | Bereiche D,F |
| DAC | C | - | D | mm | alle | - | - | - | - | Bereiche I,J |
| DAC | C | - | S | mm | alle | - | - | - | - | Bereiche B,D,F |
| DAC | C | - | B | mm | alle | - | - | - | - | Bereiche B,D,F |

### DAD - Defektes Mauerwerk

Quelle: PDF-Dateiseite 33 / gedruckte Seite 31, Anhang D, Tabelle 36:
Defektes Mauerwerk.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAD | A | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAD | A | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAD | A | - | S | - | - | - | alle | - | - | Bereiche D,F |
| DAD | A | - | B | - | - | - | - | alle | - | Bereiche D,F |
| DAD | A | - | B | - | - | - | alle | - | - | Bereiche H,I,J |
| DAD | B | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAD | B | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAD | B | - | S | - | - | - | alle | - | - | Bereiche D,F |
| DAD | B | - | B | - | - | - | alle | - | - | Bereiche H,I,J |
| DAD | C | - | D | - | - | alle | - | - | - | Bereiche D,F |
| DAD | C | - | D | - | alle | - | - | - | - | Bereiche I,J |
| DAD | C | - | S | - | alle | - | - | - | - | Bereiche D,F |
| DAD | C | - | B | - | alle | - | - | - | - | Bereiche D,F,H,I,J |

### DAE - Fehlender Moertel

Quelle: PDF-Dateiseite 33 / gedruckte Seite 31, Anhang D, Tabelle 37:
Fehlender Moertel.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAE | - | - | D | mm | - | - | - | >= 100 | < 100 | Bereiche D,F |
| DAE | - | - | D | mm | - | - | >= 100 | - | < 100 | Bereiche I,J |
| DAE | - | - | S | mm | - | - | >= 100 | >= 10 und < 100 | < 10 | Bereiche D,F |

### DAF - Oberflaechenschaden

Quelle: PDF-Dateiseite 34 / gedruckte Seite 32, Anhang D, Tabelle 38:
Oberflaechenschaden.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAF | A | A,B,C,D,E,Z | S | - | - | - | - | - | alle | Bereiche B,D,F |
| DAF | B | A,E,Z | S | - | - | - | - | alle | - | Bereiche B,D,F |
| DAF | C | A,B,C,D,E,Z | S | - | - | - | - | alle | - | Bereiche B,D,F |
| DAF | D | A,B,C,D,E,Z | S | - | - | - | alle | - | - | Bereiche B,D,F |
| DAF | E | A,B,C,D,E,Z | S | - | - | alle | - | - | - | Bereiche B,D,F |
| DAF | F | A,B,C,D,E,Z | S | - | - | - | - | alle | - | Bereiche B,D,F |
| DAF | G | A,B,C,D,E,Z | S | - | - | - | alle | - | - | Bereiche B,D,F |
| DAF | H | B,C,D,E | S | - | - | alle | - | - | - | Bereiche B,D,F |
| DAF | I | A,B,C,D,E,Z | D | - | - | alle | - | - | - | Bereiche D,F,I,J |
| DAF | I | A,B,C,D,E,Z | S | - | - | - | alle | - | - | Bereiche B,D,F |
| DAF | J | B,C,D,E,Z | S | - | - | - | alle | - | - | Bereiche B,D,F |
| DAF | J | B,C,D,E,Z | B | - | - | - | - | - | alle | Bereiche B,D,F |
| DAF | K | A,E,Z | B | - | - | - | - | alle | - | Bereiche I,J |
| DAF | Z | A,B,C,D,E,Z | D | - | - | - | - | alle | - | Bereiche D,F,I,J |
| DAF | Z | A,B,C,D,E,Z | S | - | - | - | - | alle | - | Bereiche B,D,F |

### DAG - Einragender Anschluss

Quelle: PDF-Dateiseite 34 / gedruckte Seite 32, Anhang D, Tabelle 39:
Einragender Anschluss.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAG | - | - | B | mm | >= 400 | >= 300 und < 400 | >= 200 und < 300 | >= 100 und < 200 | < 100 | Bereiche D,F |
| DAG | - | - | B | mm | - | - | - | alle | - | Bereiche I,J |

### DAH - Schadhafter Anschluss

Quelle: PDF-Dateiseite 34 / gedruckte Seite 32, Anhang D, Tabelle 40:
Schadhafter Anschluss.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAH | B,C,D | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAH | B,C,D | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAH | A,E | - | B | - | - | - | - | - | alle | Bereiche D,F,I |
| DAH | Z | - | D | - | - | - | - | alle | - | Bereiche D,F,I,J |

### DAI - Einragendes Dichtungsmaterial

Quelle: PDF-Dateiseite 35 / gedruckte Seite 33, Anhang D, Tabelle 41:
Einragendes Dichtungsmaterial.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAI | A | A,B,C | D | - | - | - | - | alle | - | Bereiche D,F |
| DAI | A | A,B,C | D | - | - | - | alle | - | - | Bereiche I,J |
| DAI | A | A,B,C | B | - | - | - | - | - | alle | Bereiche B,D,F |
| DAI | Z | - | B | - | - | - | - | - | alle | Bereiche B,D,F |

### DAJ - Verschobene Verbindung

Quelle: PDF-Dateiseite 35 / gedruckte Seite 33, Anhang D, Tabelle 42:
Verschobene Verbindung.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAJ | A,B,C | - | D | mm | - | - | - | alle | - | Bereiche D,F |
| DAJ | A,B,C | - | S | mm | - | - | - | - | alle | Bereiche B,D,F |

### DAK - Feststellung der Innenauskleidung

Quelle: PDF-Dateiseite 36 / gedruckte Seite 34, Anhang D, Tabelle 43:
Feststellung der Innenauskleidung.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAK | A | - | B | % | >= 40 | >= 30 und < 40 | >= 20 und < 30 | >= 10 und < 20 | < 10 | Bereiche D,F,H |
| DAK | A | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | Bereiche I,J |
| DAK | B | - | D | - | - | - | - | - | alle | Bereiche D,F,I,J |
| DAK | C | - | D | - | - | - | - | alle | - | Bereiche D,F,H |
| DAK | C | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAK | C | - | B | - | - | - | alle | - | - | Bereiche I,J |
| DAK | D | A,B,C,D | B | % | - | - | - | alle | - | Bereiche I,J |
| DAK | D | C | S | % | - | - | - | alle | - | Bereiche D,F,H,I,J |
| DAK | E | - | S | % | - | - | - | alle | - | Bereiche D,F,H,I,J |
| DAK | E | - | B | % | >= 40 | >= 30 und < 40 | >= 20 und < 30 | >= 10 und < 20 | < 10 | Bereiche D,F,H |
| DAK | E | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | Bereiche I,J |
| DAK | F | - | D | mm | - | - | - | - | alle | Bereiche D,F,H,I,J |
| DAK | G | - | B | - | - | - | - | - | alle | Bereiche D,F,H,I,J |
| DAK | H | - | B | - | - | - | - | - | alle | Bereiche I,J |
| DAK | I | - | D | mm | - | - | - | alle | - | Bereiche D,F |
| DAK | I | - | D | mm | - | - | alle | - | - | Bereiche I,J |
| DAK | J | - | D | mm | - | - | alle | - | - | Bereiche D,F |
| DAK | J | - | D | mm | - | alle | - | - | - | Bereiche I,J |
| DAK | K | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAK | K | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAK | L | - | D | - | - | - | - | - | alle | Bereiche D,F |
| DAK | L | - | D | - | - | - | - | alle | - | Bereiche I,J |
| DAK | L | - | S | - | - | - | - | alle | - | Bereiche D,F,H,I,J |
| DAK | M | - | D | - | - | - | - | - | alle | Bereiche D,F |
| DAK | M | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAK | N | - | D | - | - | - | - | alle | - | Bereiche D,F,I,J |
| DAK | Z | - | D | - | - | - | - | alle | - | Bereiche D,F,I,J |
| DAK | Z | - | S | - | - | - | - | alle | - | Bereiche D,F,I,J |
| DAK | Z | - | B | - | - | - | - | alle | - | Bereiche D,F,I,J |

### DAL - Schadhafte Reparatur

Quelle: PDF-Dateiseite 37 / gedruckte Seite 35, Anhang D, Tabelle 44:
Schadhafte Reparatur.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAL | A | - | D | mm | - | - | alle | - | - | Bereiche D,F |
| DAL | A | - | D | mm | - | alle | - | - | - | Bereiche I,J |
| DAL | B | - | D | mm | - | - | - | alle | - | Bereiche D,F,I,J |
| DAL | C | - | D | % | - | - | - | alle | - | Bereiche D,F |
| DAL | C | - | D | % | - | - | alle | - | - | Bereiche I,J |
| DAL | D | - | D | - | - | - | - | - | alle | Bereiche D,F |
| DAL | D | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAL | E | - | B | % | >= 40 | >= 30 und < 40 | >= 20 und < 30 | >= 10 und < 20 | < 10 | Bereiche A,B,D,E,H |
| DAL | E | - | B | % | >= 50 | >= 35 und < 50 | >= 20 und < 35 | >= 5 und < 20 | < 5 | Bereiche I,J |
| DAL | F | - | D | mm | - | - | alle | - | - | Bereiche D,F |
| DAL | F | - | D | mm | - | alle | - | - | - | Bereiche I,J |
| DAL | G | - | D | mm | - | - | - | - | alle | Bereiche D,F |
| DAL | G | - | D | mm | - | - | - | alle | - | Bereiche I,J |
| DAL | Z | - | D | - | - | - | - | alle | - | Bereiche alle |
| DAL | Z | - | B | - | - | - | - | alle | - | Bereiche alle |

### DAM - Schadhafte Schweissnaht

Quelle: PDF-Dateiseite 37 / gedruckte Seite 35, Anhang D, Tabelle 45:
Schadhafte Schweissnaht.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAM | A | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAM | A | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAM | A | - | S | - | - | - | - | alle | - | Bereiche B,D,F |
| DAM | B | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAM | B | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAM | B | - | S | - | - | - | - | - | alle | Bereiche B,D,F |
| DAM | C | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAM | C | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAM | C | - | S | - | - | - | - | alle | - | Bereiche B,D,F |

### DAN - Poroese Schachtwand

Quelle: PDF-Dateiseite 37 / gedruckte Seite 35, Anhang D, Tabelle 46:
Poroese Schachtwand.

| Code | Ch1 | Ch2 | Anforderung | Einheit | EZ 0 | EZ 1 | EZ 2 | EZ 3 | EZ 4 | Geltung |
|---|---|---|---|---|---|---|---|---|---|---|
| DAN | - | - | D | - | - | - | - | alle | - | Bereiche D,F |
| DAN | - | - | D | - | - | - | alle | - | - | Bereiche I,J |
| DAN | - | - | S | - | - | - | alle | - | - | Bereiche B,D,F |

## Noch offen

- Anhang C ist als Draft vollstaendig dokumentiert (Tabellen 7-32).
- Anhang D ist begonnen: Tabellen 33-46 sind visuell geprueft dokumentiert.
- Schachttabellen aus Anhang D offen: Tabellen 47-63.
- Klaeren, aus welchen Stammdaten `biegesteif`/`biegeweich` im Programm sicher
  bestimmt wird.
