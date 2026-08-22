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

Logo, sechs Diagramme, die Farblegende oben links, alle Kennzahlenbloecke mit
Formeln, die bedingte Formatierung, Titelband, Kopfzeile, Druckeinrichtung und
**genau eine gestaltete Musterzeile**. Der C#-Export schreibt nur Werte, kopiert
den Stil dieser Musterzeile nach unten und setzt die Zeilenhoehe.

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
