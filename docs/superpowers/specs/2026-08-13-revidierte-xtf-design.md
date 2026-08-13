# Revidierte XTF — Entwurf

Stand: 2026-08-13 · Status: **Etappe 1 bis 3 gebaut, Etappe 4 teilweise**

## Ziel

Aus SewerStudio soll am Schluss eine **revidierte XTF-Datei** entstehen, die alle im
Programm gemachten Änderungen enthält — Codierungen ebenso wie Stammdaten.

## Oberste Regel

> **Vorhandene eigene Eingaben müssen erhalten bleiben.**

Das ist keine Randbedingung, sondern die Vorgabe, an der dieser ganze Entwurf gemessen
wird. Jede Etappe unten ist darauf ausgelegt. Konkret gelten drei Versprechen:

1. **Kein erneuter Import.** Um an fehlende Herkunftsangaben zu kommen, wird ein
   bestehendes Projekt niemals neu eingelesen. Ein Re-Import würde genau den Bestand
   anfassen, der geschützt werden soll.
2. **Kein Überschreiben von Kundenoriginalen.** Die eingelesene XTF wird ausschliesslich
   gelesen. Die Revision entsteht als neue Datei daneben.
3. **Kein stilles Verwerfen.** Was beim Export nicht eindeutig zugeordnet werden kann,
   wird dem Menschen gezeigt und von ihm entschieden — nie automatisch weggelassen.

## Ist-Zustand

### Zwei XTF-Welten im Bestand

Geprüft am Projekt `Zone 1.15`:

| Modell | Erzeuger | Inhalt | Beispieldatei |
| --- | --- | --- | --- |
| `VSA_KEK_2020_LV95` | IKAS / IBAK | 41 Untersuchungen, 608 Kanalschäden, 513 Dateiverweise, 1 Datenträger | `Bürglen_UR_..._32953_1225.xtf` |
| `SIA405_ABWASSER_2015_LV95` | WinCan | Haltungen (`_SEC`), Schächte (`_NOD`) | `GEP_Altdorf_2025_Zone_1.15_..._SEC.xtf` |

Ein Kanalschaden im KEK-Modell:

```xml
<VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch100000004EB1AB">
  <Letzte_Aenderung>20260114</Letzte_Aenderung>
  <UntersuchungRef REF="ch100000004EB182" />
  <Einzelschadenklasse>unbekannt</Einzelschadenklasse>
  <Distanz>0.00</Distanz>
  <KanalSchadencode>BCD</KanalSchadencode>
  <Verbindung>nein</Verbindung>
  <Videozaehlerstand>00:00:15:00</Videozaehlerstand>
</VSA_KEK_2020_LV95.KEK.Kanalschaden>
```

Eine Untersuchung verweist über `AbwasserbauwerkRef` auf die Haltung und führt
`vonPunktBezeichnung` / `bisPunktBezeichnung` — das entspricht dem Haltungsnamen in
SewerStudio (z. B. `59220-10.1036545`).

### Was schon gut steht

- `ProtocolEntry.EntryId` — jede Beobachtung hat eine stabile eigene Kennung.
- `ProtocolEntry.Source` — `Imported`, `Manual` oder `Ai`. **Handeinträge sind dadurch
  eindeutig erkennbar.**
- `ProtocolDocument.Original` gegenüber `.Current` — der importierte Ausgangsstand liegt
  unverändert neben dem Arbeitsstand.
- `HaltungRecord.FieldMeta` mit `UserEdited` — bei Stammdaten ist feldgenau festgehalten,
  was von Hand gesetzt wurde. Im geprüften Projekt sind das u. a. 43 von 96
  Zustandsklassen.
- `SchachtRecord.FieldMeta` — seit 2026-08-13 dasselbe für Schächte.

Damit ist der Unterschied „kam so herein" gegenüber „habe ich gemacht" bereits
gespeichert. Das ist die halbe Miete für eine Revision.

### Was fehlt

**Die Herkunfts-ID am Befund.** Der Import liest die TIDs
(`LegacyXtfImportService`), speichert sie aber nicht: `VsaFinding` kennt Code, Meter,
Quantifizierung und Video — kein Feld für die Kanalschaden- oder Untersuchungs-TID.

Folge: SewerStudio kann heute nicht sagen, welcher gespeicherte Befund welchem Element
der Originaldatei entspricht.

### Was es nicht gibt

Keinen XTF-Writer. Unter `Infrastructure/Import/Xtf/` liegen 17 Lesedateien, exportiert
werden nur Excel, CSV und PDF.

## Ankerdatei: beim Import festhalten

Vorschlag von Pascal (2026-08-13): beim Import die Originale mitnehmen, davon eine Kopie
anlegen und diese revidierbar machen.

**Übernommen**, mit einer Präzisierung.

Der Import legt die Quellen bereits im Projekt ab (`Imports\XTF`, älter
`Importdateien\XTF`) und archiviert sie zusätzlich. Was fehlt, ist die **Bindung**:
welche Datei hat welche Daten erzeugt. Diese Bindung wird künftig beim Import
festgehalten — der Moment, in dem sie ohnehin bekannt ist. Damit ist beim Export
eindeutig, gegen welche Datei revidiert wird. Ohne sie wäre bei mehreren XTF im Ordner
gar nicht bestimmt, welche die Ankerdatei ist.

**Präzisierung: Die Revision wird erzeugt, nicht fortgeschrieben.**

Eine laufend mitveränderte Kopie wäre eine zweite Wahrheit neben den Projektdaten.
Laufen beide auseinander, ist nicht mehr entscheidbar, welche gilt. Stattdessen:

> Die Revision entsteht auf Anforderung neu — aus der unveränderten Originalkopie plus
> dem aktuellen Projektstand.

Folgen:

- Gleicher Projektstand ergibt immer dieselbe Revision (wiederholbar und prüfbar).
- Die Revision kann beliebig oft neu erzeugt, verglichen und verworfen werden.
- Die Projektdaten bleiben die einzige Wahrheit; es entsteht kein zweiter Schreiber
  neben dem bestehenden Speicherweg.

## Der Weg

### Etappe 1 — Herkunft mitführen (additiv) · **gebaut**

`VsaFinding` erhält zwei zusätzliche Felder für die Kanalschaden-TID und die
Untersuchungs-TID; der XTF-Import füllt sie. Rein additiv: keine Anzeige ändert sich,
kein bestehendes Verhalten, keine Migration.

**Wirkung nur auf künftige Importe.** Bestehende Projekte bekommen dadurch nichts —
das ist Absicht, siehe Versprechen 1.

### Etappe 2 — Zuordnung für den Altbestand, ohne ihn anzufassen · **gebaut**

Umgesetzt in `XtfKanalschadenElementReader` (schreibfreies Lesen der Originaldatei) und
`XtfFindingMatcher` (reine Zuordnung, kein Dateizugriff, keine Mutation).

Der Inhaltsweg zählt nur bei **beidseitiger Eindeutigkeit**: genau ein Element passt zum
Befund UND genau ein Befund passt zu diesem Element. Dadurch ist das Ergebnis unabhängig
von der Reihenfolge der Befunde, und Mehrdeutiges wird nie geraten.

Für bereits importierte Projekte wird die Zuordnung **erst beim Export** hergestellt,
gegen die Originaldatei und ohne das Projekt zu verändern:

- Haltung über `vonPunktBezeichnung`/`bisPunktBezeichnung` gegen den Haltungsnamen.
- Befund über den unangetasteten `Original`-Stand des Protokolls: Code, Distanz und
  Videozählerstand.

Das Ergebnis ist eine Zuordnungstabelle im Arbeitsspeicher, kein gespeicherter Zustand.
Nicht eindeutige Fälle gehen in den Prüfbericht (Versprechen 3).

### Etappe 3 — Schreiben · **gebaut**

Zweiteilig, nach dem Muster des plan-gesteuerten YOLO-Exports:

1. `XtfRevisionPlanBuilder` erzeugt genau einen Plan — reine Rechnung, ohne Dateizugriff.
   Der Vergleich laeuft ueber zwei Ketten, damit eine Codekorrektur als Aenderung erkannt
   wird und nicht als „geloescht plus neu":
   `Arbeitsstand --(feste Eintrags-ID)--> Ausgangsstand --(Inhalt)--> Element der Datei`.
2. `XtfRevisionWriter` schreibt ausschliesslich diesen Plan und trifft keine eigenen
   Entscheidungen mehr.

Feste Grenzen des Ausfuehrers, jede mit eigenem Test:

- Das Original wird nur gelesen und bleibt bytegleich.
- Eine vorhandene Zieldatei wird nie ueberschrieben.
- Ein Plan mit offenen Faellen wird gar nicht erst geschrieben.
- Nicht geplante Elemente bleiben unveraendert stehen — auch Dateiverweise und
  Elemente, deren Zuordnung nicht gelang.
- Geschrieben wird zuerst in eine Nebendatei und danach umbenannt; ein Abbruch
  hinterlaesst nie eine halbe Revision unter dem Zielnamen.

Noch nicht abgedeckt: SIA405-Stammdaten (Rohrmaterial, Nutzungsart), Streckenschaden-
Details und Dateiverweise zu neuen Befunden.

### Etappe 3 (urspruengliche Beschreibung) — Schreiben

Die Original-XTF wird als XML geladen und **nur an den geänderten Stellen** verändert:

| Fall | Vorgehen |
| --- | --- |
| Befund geändert | Betroffene Elemente im vorhandenen `Kanalschaden` ersetzen, `Letzte_Aenderung` nachführen |
| Befund neu (`Source = Manual`) | Neues `Kanalschaden`-Element mit neuer TID im Muster der Datei, `UntersuchungRef` auf die passende Untersuchung |
| Befund entfernt | Element entfernen |
| Stammdaten geändert (`UserEdited = true`) | Entsprechendes Attribut in `Kanal` bzw. Knoten setzen |
| Alles Übrige | Unverändert stehen lassen — Geometrie, Dateiverweise, Datenträger, unbekannte Elemente |

Beide Modelle werden bedient: KEK für die Befunde, SIA405 für die Stammdaten. Da sie in
getrennten Dateien liegen, entstehen entsprechend getrennte Revisionen.

Geschrieben wird immer in eine **neue** Datei; das Original bleibt unberührt.

### Etappe 4 — Abnahme

- Prüfbericht **vor** dem Schreiben: was ändert sich, was kommt dazu, was fällt weg,
  was konnte nicht zugeordnet werden.
- Vergleich Original gegen Revision nach dem Schreiben.
- Optional eine Prüfung mit dem offiziellen INTERLIS-Prüfprogramm.

## Offene Punkte

- **Freitext-Rohrmaterial.** SIA405 führt das Material als feste Auswahl. Selbst
  eingetragene Werte wie „Spezialbetonrohr" haben dort keine Entsprechung. Regel:
  Solche Werte werden beim Export sichtbar gemeldet und nicht still verschluckt.
  Ob sie zusätzlich auf einen gültigen Wert abgebildet werden sollen, ist offen.
- **Vergabe neuer TIDs.** Das Muster der Datei ist erkennbar (`ch1000...`), die
  verbindliche Regel für neu erzeugte Objekte ist noch zu klären.
- **Zustandsklasse.** In welches Feld welchen Modells sie beim Export gehört, ist noch
  nicht geprüft.
- **Untersuchung ohne Gegenstück.** Wie mit Haltungen umzugehen ist, die in
  SewerStudio bearbeitet wurden, aber in der gewählten Originaldatei fehlen.

## Nicht Teil dieses Entwurfs

Ein zweiter Schreibweg neben dem hier beschriebenen. Es gibt genau einen Ort, der XTF
schreibt — so wie es beim YOLO-Export genau einen plan-gesteuerten Schreiber gibt.
