# SIA405 Abwasser 2020 LV95 — verbindliche Wertelisten

**Quelle:** Modelldatei `SIA405_Abwasser_2020_2_d_LV95` aus der VSA-Modellablage
(`https://vsa.ch/models/`, Eintrag `SIA405_ABWASSER_2020_LV95`).

**Warum dieses Dokument:** Eine Exportdatei zeigt, was *vorkommt* — nicht, was *erlaubt*
ist. Die Auszählung des Kantons Uri (1544 Objekte in Göschenen, 109'871 Haltungen
kantonsweit) hat für `Haltung.Material` 21 Werte gezeigt; das Modell erlaubt 24.
`Beton_Pressrohrbeton`, `Ton` und `Zement` fehlten in der Auszählung und wurden
deshalb zweimal fälschlich als „ohne Gegenstück" eingestuft — einmal in SewerStudio,
einmal im SchachtPro-Bericht.

Die Schreibweise ist zeichengenau verbindlich. `Kunststoff_Polyvinilchlorid` trägt
ein **i**, nicht y — das ist die Norm, kein Tippfehler.

---

## Normschacht.Funktion — 22 Werte

```
Absturzbauwerk · andere · Be_Entlueftung · Behandlungsanlage · Bodenablauf
Dachwasserschacht · Einlaufschacht · Entwaesserungsrinne
Entwaesserungsrinne_mit_Schlammsack · Fettabscheider · Geleiseschacht
Kombischacht · Kontroll_Einsteigschacht · Oelabscheider · Pumpwerk
Regenueberlauf · Schlammsammler · Schwimmstoffabscheider · Spuelschacht
Trennbauwerk · unbekannt · Vorbehandlungsanlage
```

## Normschacht.Material — 4 Werte

```
andere · Beton · Kunststoff · unbekannt
```

Achtung: Der AWU-Kantonsexport enthält bei `Normschacht.Material` die Werte
`Beton_unbekannt` (28'080) und `Kunststoff_unbekannt` (526). Beide stehen **nicht**
in dieser Liste — das sind Werte aus `Haltung.Material`. Ob der Exporter dort
modellwidrig schreibt oder eine ältere Modellfassung sie erlaubte, ist offen und
sollte gegen einen INTERLIS-Prüfer geklärt werden.

## Haltung.Material — 24 Werte

```
andere · Asbestzement · Beton_Normalbeton · Beton_Ortsbeton
Beton_Pressrohrbeton · Beton_Spezialbeton · Beton_unbekannt · Faserzement
Gebrannte_Steine · Guss_duktil · Guss_Grauguss · Kunststoff_Epoxydharz
Kunststoff_Hartpolyethylen · Kunststoff_Polyester_GUP · Kunststoff_Polyethylen
Kunststoff_Polypropylen · Kunststoff_Polyvinilchlorid · Kunststoff_unbekannt
Stahl · Stahl_rostfrei · Steinzeug · Ton · unbekannt · Zement
```

Geführt in `MaterialVokabular`. Ohne sicheres Gegenstück bleiben dort nur `Guss`
(sagt nicht, ob duktil oder Grauguss) sowie `GFK`/`Glasfaser` (nicht dasselbe wie
`Kunststoff_Polyester_GUP`).

## Kanal.Nutzungsart_Ist und Nutzungsart_geplant — 9 Werte

```
andere · Bachwasser · entlastetes_Mischabwasser · Industrieabwasser
Mischabwasser · Niederschlagsabwasser · Reinabwasser · Schmutzabwasser · unbekannt
```

Geführt in `NutzungsartVokabular`. Ältere Modellfassungen verlangen statt
`Niederschlagsabwasser` den Wert `Regenabwasser`; beide schliessen sich aus.

## Rohrprofil.Profiltyp — 7 Werte

```
Eiprofil · Kreisprofil · Maulprofil · offenes_Profil · Rechteckprofil
Spezialprofil · unbekannt
```

## Deckel — eigene Klasse mit Attributen

Der `Normschacht` führt keine Deckelangaben, die Klasse `Deckel` dagegen schon:

| Attribut | Werte |
|---|---|
| `Material` | `andere` · `Beton` · `Guss` · `Guss_mit_Belagsfuellung` · `Guss_mit_Betonfuellung` · `unbekannt` |
| `Deckelform` | `andere` · `eckig` · `rund` · `unbekannt` |
| `Durchmesser` | Abmessung (mm) |
| `Kote` | Höhe |
| `Lagegenauigkeit` | `groesser_50cm` · `plusminus_10cm` · `plusminus_3cm` · `plusminus_50cm` · `unbekannt` |
| `Entlueftung` | `entlueftet` · `nicht_entlueftet` · `unbekannt` |
| `Verschluss` | `nicht_verschraubt` · `unbekannt` · `verschraubt` |
| `Schlammeimer` | `nicht_vorhanden` · `unbekannt` · `vorhanden` |
| `Instandstellung` | `nicht_notwendig` · `notwendig` · `unbekannt` |
| `Fabrikat` | Text |

Keine Entsprechung im Modell: **Belastungsklasse** (D400 usw.).

## Einstiegshilfe.Art — 9 Werte

```
andere · Drucktuere · keine · Leiter · Steigeisen · Treppe · Trittnischen
Tuere · unbekannt
```

---

## Was die Norm an dieser Stelle NICHT kennt

- **Belastungsklasse** des Deckels
- Die Zerlegung des Schachts in **Schachthals, Konus, Ober- und Unterteil**
  mit je Form, Mass und Höhe. Der `Normschacht` ist ein Punktobjekt mit Lage,
  Funktion, Material und zwei Massen.
- **Zustandserfassung** — die gehört in VSA-DSS / EN 13508-2, nicht in diese XTF.
- Die **Uhrposition** eines Anschlusses. SIA405 beschreibt Anschlüsse über
  Start- und Endknoten mit Koordinaten.

## Zwei Fallen

**Regenabwasser.** Das QGIS-Shapefile derselben AWU-Ausgabe schreibt
`Regenabwasser` (Modell 2015), die XTF korrekt `Niederschlagsabwasser` (2020).
Wer sich am Shapefile orientiert, baut einen Wert ein, den ein 2020er Prüfer
ablehnt — und der Fehler sieht wie ein Erfolg aus.

**Das Shapefile ist nicht massgebend.** Bei einer von 117 Göschenen-Haltungen
widersprechen sich die beiden Ausgaben: Shapefile `Zement`, XTF
`Beton_Normalbeton`. Massgebend ist die XTF.

## Einheiten — zwei Typen, kein Widerspruch

Die Norm mischt Meter und Millimeter, aber nach einer klaren Regel:
**Bauteilquerschnitte in Millimetern, Lagen und Höhen im Gelände in Metern.**

| Typ im Modell | Einheit | Felder | Werte im Kantonsexport |
|---|---|---|---|
| `SIA405_Base_Abwasser.Abmessung` | **mm** | `Dimension1/2`, `Lichte_Hoehe`, `Deckel.Durchmesser` | 1 – 5000 |
| `Base_LV95.Hoehe` | **m** | `Sohlenkote`, `Deckel.Kote` | 0,95 – 2429 m ü. M. |
| Länge | **m** | `Haltung.LaengeEffektiv` | 0,06 – 5237 |

Die **Schachttiefe** ist danach eine Höhe und gehört in Meter — sie hat in SIA405
ohnehin kein Zielfeld. `SiaAbmessung` darf deshalb nie auf sie angewandt werden:
aus 2,02 m würde 2020, ein Faktor-1000-Fehler in der Gegenrichtung.

Ein pauschales ×1000 auf alle Massfelder wäre also selbst der Fehler. `SiaAbmessung`
benutzt stattdessen dieselbe Regel wie die SchachtPro-Zeichnung: ein Wert über 10
gilt bereits als Millimeter.

## Zement: geprueft und ausdruecklich NICHT umgebogen

Am 2026-08-29 sah es kurz so aus, als wuerde Abwasser Uri ihr `Zement` beim Export
systematisch zu `Beton_Normalbeton` uebersetzen: Fuer die Haltung `78623-77600` zeigt
der QGIS-Layer `ha_material = Zement`, die XTF `Material = Beton_Normalbeton`. Die
Messung an einer zweiten Datei hat das widerlegt.

**`Zement` ist ein gueltiger SIA405-2020-Wert.** Er steht in der offiziellen
`SIA405_Abwasser_2020_2_d_LV95-20251129.ili` in der Liste der 24 Materialwerte an der
Klasse `Haltung`.

Es gibt keine Uebersetzung, sondern zwei verschiedene Datenstaende. Belegt an den 77
Haltungen, die sowohl im GEP-Export Zone 1.15 (SIA405 **2015**) als auch im
Kantonsexport (SIA405 **2020**) vorkommen:

| `Zement` (2015) wird 2020 zu | Anzahl |
|---|---|
| `Beton_unbekannt` | 21 |
| `Beton_Normalbeton` | 18 |
| `unbekannt` | 6 |
| `Beton_Spezialbeton` | 2 |
| `Beton_Ortsbeton` | 1 |

Eine Fassungsuebersetzung waere eindeutig. Fuenf Ziele sind eine **Datenverfeinerung**:
Zwischen den beiden Staenden hat jemand die Betonart nachgetragen. Der QGIS-Layer
`Leitungen Lokal` ist damit ein aelterer Stand als die XTF, keine parallele Sicht
derselben Daten.

Wuerde SewerStudio `Zement` auf `Beton_Normalbeton` abbilden, wuerde eine Handaenderung
an einer Zement-Haltung eine echte Angabe durch eine feinere ersetzen, die nie erhoben
wurde. `Zement` bleibt deshalb `Zement`.

## Die 2015-Fassung fuehrt eine andere Werteliste

Dieselben Materialien heissen in SIA405 2015 kuerzer. Gemessen an den echten
Kundendateien:

| 2015 | 2020 |
|---|---|
| `Zement` (106x) | `Zement` |
| `Polyethylen` (58x) | `Kunststoff_Polyethylen` |
| `Polyvinylchlorid` (10x) | `Kunststoff_Polyvinilchlorid` |
| `Polypropylen` (6x) | `Kunststoff_Polypropylen` |
| `Beton` (4x) | `Beton_unbekannt` |

Das ist dieselbe Falle wie bei `Regenabwasser` / `Niederschlagsabwasser`: Die
Modellfassung im Dateikopf entscheidet ueber die gueltige Schreibweise. Ein Export
in eine 2015-Datei darf keine 2020-Praefixe schreiben.

VSA veroeffentlicht unter `https://vsa.ch/models/` nur noch die 2020-Fassungen; die
2015-Liste ist deshalb aus den echten Kundendateien belegt, nicht aus dem Modell.

## Material und Lichte_Hoehe haengen an `Haltung`, nicht an `Kanal`

Im Kantonsexport tragen alle 109'871 `Kanal`-Objekte **kein** `Material` und **kein**
`Lichte_Hoehe`. Beide Felder gehoeren zur physischen Klasse `Haltung`:

- `Kanal` (logisch): `Nutzungsart_Ist`, `Standortname`, `BaulicherZustand`
- `Haltung` (physisch): `Material`, `Lichte_Hoehe`, `LaengeEffektiv`, `Lagebestimmung`

Beide tragen dieselbe `Bezeichnung` — in allen 109'871 Faellen identisch. Die Zuordnung
ueber den Haltungsnamen funktioniert deshalb fuer beide Klassen gleich; ein Umweg ueber
`AbwasserbauwerkRef` ist nicht noetig.

**`Lichte_Hoehe` ist amtlich Millimeter:** `DOMAIN Lichte_Hoehe = 0 .. 99999 [Units.mm]`.
Der Wert `0` bedeutet unbekannt — im Kantonsexport bei 39'486 von 109'871 Haltungen, in
Goeschenen bei allen 17.

## BaulicherZustand: SewerStudio fuellt eine Luecke

`bw_baulicherzustand` steht in AWUs Datenbank (`Z2`), kommt in beiden gelieferten
XTF-Dateien aber **null Mal** vor. SewerStudio schreibt dort `Z0` bis `Z4` — die
Schreibweise stimmt also mit ihrer Datenbank ueberein, und der Wert ergaenzt etwas,
das ihr eigener Export nicht liefert.

## Was der Export jetzt schreibt (Stand 2026-08-29)

| Feld | Klasse | Projektfeld | Umsetzung |
|---|---|---|---|
| `Nutzungsart_Ist` | Kanal | `Nutzungsart_Ist` | `NutzungsartVokabular`, modellabhaengig |
| `Standortname` | Kanal | `Standortname` | unveraendert |
| `BaulicherZustand` | Kanal | Zustandsklasse | Ziffer wird zu `Z0`..`Z4` |
| `Material` | **Haltung** | `Rohrmaterial` | `MaterialVokabular`, modellabhaengig |
| `Lichte_Hoehe` | **Haltung** | `DN_mm` | ganze Millimeter, 1..99999 |

Geschrieben wird weiterhin ausschliesslich, was der Mensch von Hand gesetzt hat
(`FieldMeta.UserEdited`). Ein importierter Wert geht nie in die Datei zurueck, aus der
er stammt.

Belegt an zwei echten Lieferungen (Planer und Schreiber gegen das unveraenderte
Original, Ziel im Temp-Ordner):

- **Goeschenen** (SIA405 2020, 17 Haltungen): `Material unbekannt -> Steinzeug`,
  `Lichte_Hoehe 0 -> 250`. Feldreihenfolge erhalten, `Letzte_Aenderung` nachgefuehrt.
- **GEP Altdorf Zone 1.15** (SIA405 **2015**, 92 Haltungen): `Material Zement ->
  Polyethylen` — also die 2015-Kurzform, nicht `Kunststoff_Polyethylen`. Auch die
  abweichende Feldreihenfolge dieser Datei (`AbwasserbauwerkRef` direkt hinter der
  Bezeichnung) bleibt erhalten.

In beiden Faellen blieb das Original bytegleich.

## Die Feldreihenfolge kommt aus der Datei, nicht aus einer Liste

INTERLIS gibt die Reihenfolge der Elemente vor; ein neu eingefuegtes Feld darf nicht
hinten angehaengt werden. Eine feste Liste je Klasse reicht dafuer nicht — gemessen an
drei echten Lieferungen ordnen sie die Haltung verschieden:

| Datei | Reihenfolge (Anfang) |
|---|---|
| Kantonsexport 2020 | `Letzte_Aenderung, Bezeichnung, LaengeEffektiv, Lichte_Hoehe, Material, Lagebestimmung, …` |
| Zone 1.17 | `Letzte_Aenderung, Bezeichnung, LaengeEffektiv, Lichte_Hoehe, Material, Verbindungsart, …` |
| Zone 1.15 | `Bezeichnung, AbwasserbauwerkRef, LaengeEffektiv, Lichte_Hoehe, Material, …` |

**Innerhalb** einer Datei ist sie dagegen konsistent (Kantonsexport: 2 Muster auf 3000
Objekte, Zone 1.15: 1 Muster auf 92). `XtfRevisionWriter` fragt deshalb zuerst ein
Geschwister-Objekt derselben Klasse, das das Feld bereits fuehrt, und faellt erst dann
auf die Modellreihenfolge zurueck. Das funktioniert unabhaengig davon, welchem
Modellableger die Datei folgt.

Der Umweg ist noetig, weil die gelieferten Dateien **nicht** dem reinen VSA-Modell
folgen: Zone 1.17 traegt an der Haltung `Verbindungsart`, `Bettung_Umhuellung`,
`Spuelintervall` und `Letzte_Aenderung` — vier Felder, die
`SIA405_Abwasser_2020_2_d_LV95` an dieser Klasse gar nicht kennt. Der Modellname im
Dateikopf lautet dementsprechend `SIA405_ABWASSER_2020_LV95`, nicht
`SIA405_Abwasser_2020_2_d_LV95`.

## Auch die Schreibweise kommt aus der Datei

Dieselbe Falle wie bei der Reihenfolge, eine Ebene tiefer. Zwei echte Lieferungen
schreiben dasselbe Feld verschieden:

| Datei | Zustandsfeld |
|---|---|
| GEP Altdorf Zone 1.15 | `BaulicherZustand` (wie das Modell) |
| Zone 1.17 | `Baulicherzustand` — kleines z, an 446 Kanal- und 295 Normschacht-Objekten |
| Kantonsexport, Goeschenen | fuehren das Feld gar nicht |

Ein zeichengenauer Vergleich findet das vorhandene Feld in Zone 1.17 nicht und legt ein
zweites daneben. Das Objekt traegt danach denselben Wert zweimal in verschiedener
Schreibweise. `XtfRevisionWriter` sucht deshalb zuerst zeichengenau und danach ohne
Ruecksicht auf Gross-/Kleinschreibung; ein wirklich neues Feld uebernimmt die
Schreibweise eines Geschwister-Objekts. Zwei Felder, die sich nur darin unterscheiden,
kennt INTERLIS nicht — die zweite Runde kann nichts Falsches treffen.

Zone 1.17 weicht auch sonst ab: `Funktion_hierarchisch` statt `FunktionHierarchisch`,
`Datenherr`/`Eigentuemer` als Textfelder statt `DatenherrRef`/`EigentuemerRef`.

## Schaechte in der XTF: ja, aber nur eine der vier Klassen traegt Daten

SIA405 beschreibt einen Schacht mit vier Klassen. Gemessen am Kantonsexport
(64'420 Schaechte) und an Zone 1.17 (295):

| Klasse | Objekte | Was tatsaechlich gefuellt ist |
|---|---|---|
| `Normschacht` | 64'420 | `Funktion` 100 %, `Material` 100 %, `Dimension1/2` 89 %, `Status` 100 %, `Sanierungsbedarf` 56 %, `Baujahr` 33 % |
| `Deckel` | 64'420 | **nichts** — nur Bezeichnung, Lage, Verweise |
| `Einstiegshilfe` | 64'420 | **nichts** — nur Bezeichnung und Verweise |
| `Abwasserknoten` | 113'559 | `Sohlenkote` 31 % |

Das Modell haette fuer Deckelmaterial (`Deckel.Material`), Deckelform
(`Deckel.Deckelform`), Deckeldurchmesser (`Deckel.Durchmesser`) und Steighilfe
(`Einstiegshilfe.Art`) durchaus ein Ziel. Abwasser Uri liefert diese Felder nur nicht —
in **keiner** der vier gepruefte Dateien steht dort ein Wert.

**Die Schachttiefe hat weiterhin kein Ziel.** Sie waere aus `Deckel.Kote` minus
`Abwasserknoten.Sohlenkote` ableitbar, aber `Deckel.Kote` ist ueberall leer.

Zwei Fallen fuer einen spaeteren Schacht-Export:

- **`Normschacht.Material` hat 2020 nur vier Werte** (`andere`, `Beton`, `Kunststoff`,
  `unbekannt`) — eine viel kuerzere Liste als beim Rohr. In der 2015-Datei Zone 1.15
  stehen dort aber `Zement` (53x), `Polyethylen` (36x), `Polyvinylchlorid` (5x),
  `Polypropylen` (2x), `Beton` (2x), also die **Rohrmaterialliste**. Die Fassung
  entscheidet hier ueber die ganze Werteliste, nicht nur ueber die Schreibweise.
- **Goeschenen enthaelt ueberhaupt keine Schaechte** (0 Normschacht, 0 Deckel,
  0 Einstiegshilfe, 19 Abwasserknoten). Ein Schacht-Export laesst sich an dieser Datei
  nicht pruefen; dafuer braucht es Zone 1.17.

`Normschacht.Bezeichnung` ist die Schachtnummer und in Zone 1.17 eindeutig (295 von
295). Die Zuordnung ueber den Namen funktioniert also wie bei den Haltungen.
