# Excel-Vorlagenbauer

Erzeugt die beiden Dateien in `Export_Vorlage/`, die der Excel-Export fuellt:

- `Haltungen.xlsx`
- `Schächte.xlsx`

## Warum ein Werkzeug und keine von Hand gepflegte Datei

In der alten Vorlage liefen Farben und Formeln unbemerkt auseinander. Gefunden
wurden unter anderem:

- Zustandsklasse 3 war bei den Haltungen `AEB135`, bei den Schaechten `A5A832` —
  gleiche Bedeutung, zwei Toene.
- Eine Zaehlformel reichte nur bis Zeile 60 statt bis 500, deshalb blieb die
  Zustandsklasse 0 immer auf null.
- Zwei Eigentuemer-Zaehler waren feste Nullen statt Formeln.
- In einer Summe stand ein kaputter Bezug (`#BEZUG!`), sichtbar als `#NAME?`.
- Eine Haltung fiel aus beiden Kostensummen heraus: **CHF 3'975.55 fehlten**.

Solche Fehler sieht in einer `.xlsx` niemand. Aus dem Werkzeug ist die Vorlage
jederzeit reproduzierbar und die Regeln stehen als lesbarer Text da.

## Aufruf

```
python vorlage.py                 # baut nur nach ausgabe/
python vorlage.py --uebernehmen   # ersetzt die Dateien in Export_Vorlage/
```

Gebraucht werden die festgelegten Versionen von `openpyxl` und dessen
Bildbaustein `Pillow` aus `requirements.txt`.
Sie ist Teil des Werkzeugvertrags, damit derselbe Quellstand wieder dieselbe
OOXML-Struktur erzeugt. Ein eigenes Python genuegt:

```
python -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python vorlage.py
```

## Was die Vorlage traegt

Logo, sieben Diagramme, die Farblegende oben links, alle Kennzahlenbloecke mit
Formeln, die bedingte Formatierung, Titelband, Kopfzeile, Druckeinrichtung und
**genau eine gestaltete Musterzeile**. Der C#-Export schreibt nur Werte, kopiert
den Stil dieser Musterzeile nach unten und setzt die Zeilenhoehe.

Auf ausdruecklichen Nutzerwunsch bleibt seit der Korrektur vom 08.09.2026
alles auf genau einem Tabellenblatt je Export. Diagramme und Projektkennzahlen
stehen oben, die vollstaendige Liste darunter. Keine weiteren Blaetter und keine
zusaetzliche Lesefassung. Die 27 Haltungs-/17 Schachtspalten bleiben erhalten.
`arbeitsliste.py` ergaenzt SUBTOTAL(103/109) in Zeile 24 fuer sichtbare Anzahl,
Haltungslaenge und Kosten. Gesamtsummen in Zeile 23 bleiben unabhaengig vom Filter.
Titel/Kopf/Daten behalten die Vertragszeilen 25/26/27. Kennungen bleiben beim
Scrollen fixiert. Lange Texte bleiben vollstaendig in ihren Originalzellen;
der volle Inhalt ist in Excels Bearbeitungsleiste zugaenglich.
Alle Spalten werden gemeinsam auf einer A3-Seitenbreite gedruckt; die Hoehe
folgt der Zeilenzahl. `ExcelArbeitsansicht` begrenzt nur den Druckbereich und
setzt den Projektdruckkopf. `ExcelDrucktitel` normalisiert die Wiederholungs-
bereiche vor der erneuten Dateipruefung weiterhin auf absolute Bezüge.
Keine neue Registrierung und keine Aenderung von Projektformaten/Schnittstellen.
`ExcelArbeitsansichtTests` prueft genau ein Blatt, Spaltenbestand, Textinhalt,
Filter-/Gesamtsummen, Leerprojekte und Druckeinstellungen.

Der Export faerbt bewusst nichts selbst ein. Die Ampelfarben kommen aus der
bedingten Formatierung — nur so folgt die Farbe dem Wert auch dann noch, wenn
jemand die fertige Datei in Excel von Hand nachbearbeitet.

## Zwei Fallen, die schon zugeschnappt sind

**Zeilennummern.** Kopfzeile und erste Datenzeile stehen in
`ExcelVorlagenLayout` (Application-Schicht). Verschiebt sich hier etwas, muessen
die Werte dort mitwandern — sonst liest der Export stillschweigend die falsche
Zeile.

**Zahl oder Text.** Der Export schreibt die Zustandsklasse als Text (`"2"`), von
Hand getippt waere sie eine Zahl. Excel wandelt beim Vergleich nicht um. Deshalb
steht das Zaehlkriterium in Anfuehrungszeichen (`COUNTIF(...;"2")`, trifft
beides) und die Farbregel prueft beide Formen. Ohne das bleiben Kennzahlen und
Balken auf null.

Beides ist mit Tests abgesichert:
`tests/AuswertungPro.Next.Infrastructure.Tests/Export/ExcelExportVorlagentreueTests.cs`

**Prüfungsresultat.** SewerStudio besitzt zwei belegte Wertefamilien:
`i.O. / beobachten / Sanierungsbedarf` und die älteren Texte der
Dichtheitsprüfung. Die Vorlage schreibt keinen Wert um. Sie zählt und färbt beide
Familien mit derselben Ampelbedeutung. Beim Öffnen erzwingt Excel eine vollständige
Neuberechnung, damit Kennzahlen und Diagramme keine alten Zwischenwerte zeigen.
