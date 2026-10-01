# Anpassen-Modus für die Detailansicht

Datum: 2026-08-30
Betrifft: Detailansicht der Haltungen und Schächte (`RecordDetailsView`, kompakte Spaltenansicht)

## Warum

Innerhalb eines Vormittags kamen fünf Wünsche zur selben Sache: Karten anders
sortieren, Spalten anders sortieren, Anfangs-/Endschacht in eine andere Spalte,
Bemerkung in eine andere Spalte, zwei Felder ganz weg. Jeder einzelne liesse sich fest
ins Programm schreiben — dann folgt der sechste Wunsch und wieder eine Code-Änderung.

Das ist die persönliche Arbeitsansicht eines einzelnen Anwenders. Sie gehört ihm, nicht
dem Quelltext.

## Was der Anwender bekommt

Ein Knopf **„Ansicht anpassen"** in der Kopfzeile der Detailansicht. Solange er nicht
gedrückt ist, verhält sich alles wie bisher — es lässt sich nichts versehentlich
verschieben.

Im Anpassen-Modus:

- **Karte ziehen** — innerhalb ihrer Spalte oder in eine andere Spalte.
- **Spalte ziehen** — am Spaltentitel, tauscht den Platz.
- **Karte ausblenden** — ✕ an der Karte.
- **Leiste unten** mit den ausgeblendeten Feldern; von dort zurückziehen.
- **„Standard wiederherstellen"** — zurück auf Werkseinstellung.
- **„Fertig"** — Modus verlassen.

Die Spalten verteilen sich gleichmässig auf die volle Breite, unabhängig von ihrer
Anzahl. Haltungen und Schächte haben getrennte Layouts.

## Grenzen

- **`FieldCatalog.ColumnOrder` bleibt unangetastet.** Daran hängen CSV-Export,
  Excel-Export und der Import-Merge. Der Anpassen-Modus betrifft ausschliesslich die
  Anzeige.
- **Ausblenden heisst nicht löschen.** Der Wert bleibt gespeichert und geht in alle
  Exporte weiterhin mit. Auch eine Verknüpfung wie `PDF_Path` bleibt wirksam.
- **Kein leeres Layout erfinden.** Ohne gespeichertes Layout gilt exakt das heutige
  Verhalten. Das ist die Rückfallebene, wenn etwas schiefgeht.

## Werkseinstellung

Fachlich richtige Zuordnungen bleiben fest im Code — sie sind kein Geschmack:

- `Schacht_oben` und `Schacht_unten` gehören zu den **Stammdaten** einer Haltung.
- Freie Projektfelder laufen durch dieselbe Gruppenregel wie Katalogfelder. Bisher
  wurden sie ungefragt in „Weitere Angaben" geworfen, ohne die Regel überhaupt zu
  fragen.

Alles Weitere (Bemerkung umhängen, Felder ausblenden) macht der Anwender selbst.

## Aufbau

### Datenmodell — rein, ohne Oberfläche

```
RecordDetailLayoutColumn(Title, Fields)      eine Spalte: Titel + Feldnamen in Reihenfolge
RecordDetailLayout(Columns, HiddenFields)    das ganze Layout einer Datensatzart
```

Leeres Layout = nichts gespeichert = heutiges Verhalten.

### Anwenden und Erfassen

`RecordDetailLayoutApplier`:

- `Apply(groups, layout)` — ordnet die vom Builder gelieferten Gruppen nach dem
  gespeicherten Layout: Spalten in gespeicherter Reihenfolge, Felder in ihrer
  gespeicherten Spalte und Reihenfolge, ausgeblendete Felder als solche markiert.
- `Capture(groups)` — liest den aktuellen Anzeigezustand als Layout aus. Genau das wird
  gespeichert.

Beides nutzt `RecordDetailOrderRanking` — dieselbe Sortierregel wie die Feldkarten:
Bekanntes in gespeicherter Reihenfolge, **Unbekanntes bleibt an dem Eintrag hängen,
hinter dem es bisher stand**. Ein Feld, das ein Programm-Update später hinzufügt,
rutscht dadurch nicht ans Ende, sondern erscheint bei seinem bisherigen Nachbarn.

Ein Feld, das das Layout nicht kennt, bleibt in seiner Builder-Gruppe. Ein Feldname im
Layout, den es nicht mehr gibt, wird übergangen.

### Ausblenden

`RecordDetailItem` bekommt additiv `IsHiddenByUser`. Das bestehende `IsVisible` bleibt
unberührt — es steuert die Sanierungs-Folgefelder („Sanieren = Nein") und ist eine
fachliche Regel, keine persönliche Einstellung. Eine Karte erscheint, wenn beides
zutrifft.

Ausgeblendete Karten bleiben in ihrer Gruppe stehen, nur unsichtbar. Damit geht beim
Zurückholen kein Zustand verloren.

### Spaltenbreite

Das feste Raster (`226 | 226 | * | *`) weicht einem `UniformGrid` mit einer Zeile. Die
Spaltenzahl folgt damit der Zahl der sichtbaren Gruppen, und die Position einer Gruppe
in der Liste ist unmittelbar ihre Spalte — die feste Zuordnung Gruppenart → Spaltennummer
im XAML entfällt ersatzlos.

Die Gruppe „Dokumente & Medien" wird in der kompakten Ansicht wie bisher nicht gezeigt.
Sie wird jetzt aus der Liste gefiltert statt nur unsichtbar geschaltet, sonst
verbrauchte sie eine Zelle des Rasters.

### Persistenz

`DataPageLayoutSettings.DetailLayout` — je einmal unter `DataPageLayout` (Haltungen) und
`SchaechtePageLayout` (Schächte), neben Zeilenhöhe und Zoom. Die während der Umsetzung
entstandene Zwischenstufe `DetailFieldOrder` (nur Feldreihenfolge, keine Spalten) geht
darin auf und entfällt; sie war nie in einem Commit.

### Oberfläche

`RecordDetailsView` erhält:

- `IsCustomizing` — schaltet Ziehen, ✕ und die Leiste unten frei.
- `LayoutChanged` — meldet das erfasste Layout nach jeder Änderung.

Die beiden Ansichten (`HaltungsansichtView`, `SchachtansichtView`) verbinden das mit
ihrer jeweiligen Einstellung und speichern. Sie haben bereits Zugriff auf `AppSettings`
und einen Speicherweg — es entsteht keine neue Kette durch die Seiten.

## Was schon steht

Aus derselben Sitzung, gebaut und gegen vier Sabotagen geprüft:

- `RecordDetailOrderRanking` — die gemeinsame Sortierregel.
- `RecordDetailItem.FieldName` — stabiler Feldschlüssel; ohne ihn liesse sich nichts
  speichern, die Beschriftung taugt dafür nicht.
- `RecordDetailInsertionAdorner` — die Einfügemarke beim Ziehen, als Adorner gezeichnet,
  damit die Karten dabei nicht verrutschen.
- Das Ziehen der Karten samt Ablageberechnung.

## Prüfung

Die Rechenteile (`Apply`, `Capture`, Verschieben, Ausblenden) sind rein und werden
direkt geprüft. Für die Verdrahtung gilt die Lehre aus dieser Sitzung: **Ein Test, der
bei zwei verschiedenen Regeln dasselbe Ergebnis erwartet, beweist keine von beiden.**
Jeder Test zur Reihenfolge braucht einen Fall, bei dem sich die richtige und die
naheliegende falsche Regel unterscheiden.

Zusätzlich ein Wächter, dass die Werkseinstellung ohne gespeichertes Layout exakt dem
heutigen Verhalten entspricht.
