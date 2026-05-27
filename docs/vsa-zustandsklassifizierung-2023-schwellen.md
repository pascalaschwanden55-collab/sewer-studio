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

## Noch offen

- Restliche Kanaltabellen aus Anhang C: Tabellen 10-32.
- Schachttabellen aus Anhang D: Tabellen 33-63.
- Klaeren, aus welchen Stammdaten `biegesteif`/`biegeweich` im Programm sicher
  bestimmt wird.
