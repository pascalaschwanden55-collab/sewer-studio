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

## Noch offen

- Restliche Kanaltabellen aus Anhang C: Tabellen 8-32.
- Schachttabellen aus Anhang D: Tabellen 33-63.
- Klaeren, aus welchen Stammdaten `biegesteif`/`biegeweich` im Programm sicher
  bestimmt wird.
