# SIA405 Abwasser 2020 LV95 — verbindliche Wertelisten

**Quelle:** Modelldatei `SIA405_Abwasser_2020_1_2_d_LV95-20251129.ili` aus der
VSA-Modellablage (`https://vsa.ch/models/?dir=2020_1`), Modell
`SIA405_ABWASSER_2020_1_LV95`, VERSION 29.11.2025. Dazu das Basismodell
`SIA405_Base_Abwasser_1_2_d_LV95-20231018.ili`, Modell `SIA405_Base_Abwasser_1_LV95`,
VERSION 18.10.2023.

**Achtung, zwei Generationen:** Der Kantonsexport von Abwasser Uri deklariert im
Dateikopf `SIA405_ABWASSER_2020_LV95` / 26.06.2021 und `SIA405_Base_Abwasser_LV95` /
03.11.2020 — die VORIGE Generation. Die aktuelle traegt eine `_1` im Modellnamen. Ein
Pruefer loest ueber den Modellnamen auf und findet die alte Fassung in der Ablage
`2020_1` nicht mehr. Die Wertelisten selbst sind zwischen den beiden Generationen fuer
alle hier gefuehrten Felder identisch — geprueft an 15 Domaenen.

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

## Was der Export jetzt schreibt (Stand 2026-09-02)

| Feld | Klasse | Projektfeld | Umsetzung |
|---|---|---|---|
| `Nutzungsart_Ist` | Kanal | `Nutzungsart` | `NutzungsartVokabular`, modellabhaengig |
| `BaulicherZustand` | Kanal | `Zustandsklasse` | Ziffer wird zu `Z0`..`Z4` |
| `FunktionHierarchisch` | Kanal | `FunktionHierarchisch` | `SiaKanalVokabular`, 14 Blattwerte |
| `Verbindungsart` | Kanal | `Verbindungsart` | `SiaKanalVokabular`, 13 Werte |
| `Bettung_Umhuellung` | Kanal | `Bettung_Umhuellung` | `SiaKanalVokabular`, 14 Werte |
| `FunktionHydraulisch` | Kanal | `FunktionHydraulisch` | `SiaKanalVokabular`, 12 Werte |
| `Status` | Kanal | `Status` | `SiaKanalVokabular`, 5 Werte |
| `Sanierungsbedarf` | Kanal | `Sanierungsbedarf` | `SiaKanalVokabular`, 6 Werte |
| `Baujahr` | Kanal | `Baujahr` | ganze Jahreszahl, 1800..2100 |
| `Bruttokosten` | Kanal | `Bruttokosten` | Franken mit zwei Stellen, 0..99999999.99 |
| `EigentuemerRef` | Kanal | `Eigentuemer` | Verweis auf eine Organisation |
| `Material` | **Haltung** | `Rohrmaterial` | `MaterialVokabular`, modellabhaengig |
| `Lichte_Hoehe` | **Haltung** | `DN_mm` | ganze Millimeter, 1..99999 |
| `LaengeEffektiv` | **Haltung** | `Haltungslaenge_m` | Meter mit zwei Stellen, 0..30000 |
| `Lagebestimmung` | **Haltung** | `Lagebestimmung` | `SiaKanalVokabular`, 3 Werte |
| `Profiltyp` | **Rohrprofil** | `Profiltyp` | ueber `RohrprofilRef`, 7 Werte |
| `Funktion` | **Normschacht** | `Funktion` | `SchachtFunktionVokabular` |
| `Material` | **Normschacht** | `Material` | `SchachtMaterialVokabular`, nur 4 Werte |
| `Dimension1`/`2` | **Normschacht** | `Dimension` | aus "600 mm" bzw. "1100 x 900 mm" |
| `BaulicherZustand` | **Normschacht** | `Zustandsklasse` | Ziffer wird zu `Z0`..`Z4` |
| `EigentuemerRef` | **Normschacht** | `Eigentuemer` | Verweis auf eine Organisation |

**`Standortname` wird nicht mehr geschrieben.** Das Feld `Strasse` bleibt im Programm
vollstaendig erhalten und bearbeitbar — es geht nur nicht mehr in die Revision
(Entscheid 2026-09-02). `XtfStammdatenPlanBuilderTests.Die_Strasse_wird_nicht_mehr_exportiert`
haelt den Verzicht fest, damit die Zeile nicht als vergessene Luecke wieder eingebaut wird.

### Acht Felder bleiben bewusst im Programm

`XtfStammdatenPlanBuilder.NichtExportierteFelder` fuehrt sie namentlich, ein Test
haelt die Liste gegen die Exportkarten:

| Feld | Warum nicht |
|---|---|
| `Strasse` | haette mit `Kanal.Standortname` ein Ziel — Entscheid 2026-09-02 |
| `Lichte_Breite_mm` | im Modell gibt es keine lichte Breite |
| `Objekt_ID` | die XTF kennt kein Feld dafuer, die Identitaet ist die TID |
| `Datenherr` | in SIA405 ein Organisationsverweis; SewerStudio ist nicht der Datenherr |
| `Datenlieferant` | dasselbe |
| `Organisation` | im Kataster bei allen 110297 Leitungen leer |
| `Letzte_Aenderung` | fuehrt `XtfRevisionWriter` selbst nach, wo die Datei es kennt |
| `Aktualisierungsdatum` | Buchhaltung des QGIS-Exports, kein SIA405-Feld |

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

## Schaechte aus der XTF importieren (Stand 2026-08-30)

Bis dahin legte kein XTF-Weg Schaechte an. Gemessen an allen 17 echten Projekten waren
**alle 122 vorhandenen Eigentumsangaben von Hand gesetzt** (`FieldSource.Manual`), keine
einzige aus einem Import — obwohl der QGIS-Export Zone 1.17 sie mitliefert.

Uebernommen wird nur, was in der Schachttabelle gebraucht wird:

| XTF (`Normschacht`) | Projektfeld | Umsetzung |
|---|---|---|
| `Bezeichnung` | `Schachtnummer` | unveraendert |
| `Funktion` | `Funktion` | `SchachtFunktionVokabular` |
| `Material` | `Material` | `SchachtMaterialVokabular`, `unbekannt` faellt weg |
| `Dimension1`/`Dimension2` | `Dimension` | `600 mm` bzw. `1100 x 900 mm` |
| `Eigentuemer` | `Eigentuemer` | `EigentumVokabular` |

`Status`, `Sanierungsbedarf`, `Baujahr`, `Sohlenkote`, `Lagebestimmung` und die
Deckelangaben bleiben ausdruecklich draussen — sie sind informativ und stehen im
Protokoll.

Beleglauf gegen die drei echten Lieferungen:

| Datei | Schaechte | Eigentuemer | Funktion | Material | Dimension |
|---|---|---|---|---|---|
| Zone 1.17 | 295 | 289 (`Privat` 204, `AWU` 68, `Kanton` 17) | 295 | 84 | 280 |
| Zone 1.15 | 99 | 0 (Datei fuehrt das Feld nicht) | 86 | 99 | 0 |
| Goeschenen | 0 | – | – | – | – |

Im echten Projekt `Zone 1.15` treffen **99 von 99** XTF-Schaechten auf einen bereits
vorhandenen Datensatz; es entsteht keine Dublette.

### Zwei Fallen, die erst die Messung gezeigt hat

**`Abwasser Uri` faerbt die Excel-Zelle nicht.** Beide Berichtsvorlagen vergleichen die
Eigentuemerspalte exakt: `Haltungen.xlsx` Spalte O und `Schaechte.xlsx` Spalte J pruefen
je `="AWU"`, `="Kanton"`, `="Bund"`, `="Gemeinde"`, `="Privat"`. Die XTF schreibt
`Abwasser Uri` und `Kanton Uri`. Ohne `EigentumVokabular` waere die Spalte gefuellt und
zwei Drittel der Zeilen ohne Farbe — genau das, wofuer die Angabe gebraucht wird, kaputt.
Der Haltungs-Import schrieb den Rohwert schon vorher; auch das ist behoben.

**`NR.` ist nicht die Schachtnummer.** Der SchachtPro-Import fuellt `NR.` und `Nr.` mit
der Schachtnummer, und es lag nahe, das nachzuahmen. In den 17 echten Projekten tragen
diese Felder aber bei **257 von 257** Schaechten eine laufende Nummer (1, 2, 3 …) und in
keinem einzigen Fall die Schachtnummer. Der XTF-Import schreibt deshalb nur
`Schachtnummer` und sucht auch nicht ueber `NR.` — sonst traefe ein Schacht mit der
Nummer `1` auf den ersten Schacht der Liste.

Die Schluesselfelder fuer das Wiederfinden sind dieselben wie bei WinCan und SchachtPro;
`XtfSchachtSchluesselfelderTests` haelt das per Reflection fest, damit die drei Listen
nicht auseinanderlaufen.

---

## Kanal.FunktionHierarchisch — 14 Blattwerte

Zweistufig: `PAA` ist die primaere, `SAA` die sekundaere Abwasseranlage. Nur die
Blaetter sind gueltige Werte; die beiden Gruppennamen allein sind keine Angabe.

```text
PAA.andere · PAA.Gewaesser · PAA.Hauptsammelkanal · PAA.Hauptsammelkanal_regional
PAA.Liegenschaftsentwaesserung · PAA.Sammelkanal · PAA.Sanierungsleitung
PAA.Strassenentwaesserung · PAA.unbekannt
SAA.andere · SAA.Liegenschaftsentwaesserung · SAA.Sanierungsleitung
SAA.Strassenentwaesserung · SAA.unbekannt
```

Die Auswahl im Programm fuehrte bis 2026-09-02 nur sieben PAA-Werte. Die ganze
sekundaere Abwasseranlage fehlte, obwohl der Kataster sie fuehrt.

## Kanal.Verbindungsart — 13 Werte

```text
andere · Elektroschweissmuffen · Flachmuffen · Flansch · Glockenmuffen · Kupplung
Schraubmuffen · spiegelgeschweisst · Spitzmuffen · Steckmuffen · Ueberschiebmuffen
unbekannt · Vortriebsrohrkupplung
```

## Kanal.Bettung_Umhuellung — 14 Werte

```text
andere · erdverlegt · in_Kanal_aufgehaengt · in_Kanal_einbetoniert · in_Leitungsgang
in_Vortriebsrohr_Beton · in_Vortriebsrohr_Stahl · Sand · SIA_Typ1 · SIA_Typ2
SIA_Typ3 · SIA_Typ4 · Sohlbrett · unbekannt
```

## Organisation.Organisationstyp — 7 Werte

Aus dem Basismodell `SIA405_Base_Abwasser_1_LV95`. Pflichtfeld, ebenso `Status`
(`aktiv` · `untergegangen`).

```text
Abwasserverband · Bund · Gemeinde · Gemeindeabteilung
Genossenschaft_Korporation · Kanton · Privat
```

Es gibt **kein** `unbekannt`. Die Assoziation zum Eigentuemer hat Kardinalitaet 1 —
jedes Abwasserbauwerk muss genau eine Organisation referenzieren, Weglassen ist nicht
erlaubt. Ein Eigentuemer `unbekannt` bekommt deshalb eine eigene Organisation mit
genau dieser Bezeichnung; als Typ bleibt nur `Privat`, die schwaechste der sieben
Behauptungen.

`UNIQUE Bezeichnung, Organisationstyp, UID` — dieselbe Bezeichnung darf also nicht
zweimal mit demselben Typ vorkommen. SewerStudio legt eine Organisation deshalb nur
an, wenn die Datei sie noch nicht fuehrt, und Haltungen und Schaechte teilen sich
EIN Organisationsbuch je Datei.

## Der Eigentuemer ist ein Verweis, kein Text

Im Kantonsexport vom 2026-08-21 gibt es genau **eine** Organisation
(`ch1000f000000001`, "Abwasser Uri", Typ `Kanton`), und alle **174'291**
`EigentuemerRef` zeigen auf sie. Bei einem gemischten Export (ASTRA, Kanton, Gemeinden,
Privat) ist die Eigentuemerangabe damit schlicht falsch.

Der echte Bestand fuehrt 27 Eigentuemerwerte plus den leeren:

| Wert | Haltungen | Organisationstyp |
|---|---|---|
| Privat | 67'081 | Privat |
| ASTRA - Bundesamt für Strassen | 14'497 | Bund |
| Abwasser Uri | 11'063 | **Abwasserverband** |
| Kanton Uri | 7'239 | Kanton |
| unbekannt | 4'712 | Privat (erzwungen) |
| Meliorationsgenossenschaft Reussebene Uri | 1'041 | Genossenschaft_Korporation |
| Korporation Uri | 908 | Genossenschaft_Korporation |
| Meliorationsgesellschaft Seedorf | 608 | Genossenschaft_Korporation |
| 19 Urner Gemeinden | 2'722 | Gemeinde |
| (leer) | 426 | — |

**`Abwasser Uri` ist ein Abwasserverband, kein Kanton.** Der alte Kantonsexport traegt
dort `Kanton`; Abwasser Uri hat das am 2026-09-02 korrigiert, SewerStudio folgt
derselben Entscheidung.

**Der Name wird nie veraendert.** `EigentumVokabular.NachOrganisationstyp` faltet fuer
den Vergleich (Umlaute auf, Kantonszusatz weg), damit "Bürglen (UR)" als Gemeinde
erkannt wird — der exportierte Name bleibt zeichengleich `Bürglen (UR)`. Wofuer kein
Typ belegt ist, entsteht fail-closed keine Organisation.

Noch offen: `Meliorationsgesellschaft Seedorf` steht auf
`Genossenschaft_Korporation`. Das Modell sagt dazu "Koerperschaft oeffentlichen
Rechts. Falls privaten Rechtes dann als Privat abbilden." — 608 Haltungen haengen
daran.

## Lichte_Breite hat kein Ziel

`SIA405_ABWASSER_2020_1_LV95` kennt an der Klasse `Haltung` nur `Lichte_Hoehe`. Eine
lichte Breite gibt es im ganzen Modell nicht. Das Programmfeld `Lichte_Breite_mm` ist
deshalb bewusst eine reine Programmangabe fuer Ei-, Maul- und Rechteckprofile und
wird nicht exportiert — wie `Strasse`.

## Profiltyp haengt am Rohrprofil, nicht an der Haltung

Die Haltung zeigt ueber `RohrprofilRef` auf ein eigenes Objekt der Klasse
`Rohrprofil`, und dort steht der `Profiltyp`. Im Kantonsexport besitzt jede der
109'871 Haltungen ihr eigenes Rohrprofil (109'871 Objekte, 1:1) — der Profiltyp laesst
sich dort also gefahrlos aendern.

Verlassen wird sich darauf nicht: Zeigen mehrere Haltungen auf dasselbe Profil,
aendert SewerStudio es nicht und meldet den Grund. Sonst wuerde eine Korrektur an
einer Haltung fremde Haltungen mit umschreiben.

## Die Feldreihenfolge endet vor den Verweisen

Zur bekannten Regel (zuerst ein Geschwister-Objekt der Datei, dann die
Modellreihenfolge) kommt eine dritte Stufe: Findet keine von beiden einen Nachfolger,
landet ein neues Attribut **vor dem ersten Verweis-Element**, nicht am Ende des
Objekts. In INTERLIS stehen die Rollenverweise (`DatenherrRef`, `EigentuemerRef`,
`RohrprofilRef`) hinter den Attributen.

Im Echtlauf am Kantonsausschnitt stand `Verbindungsart` vorher hinter
`EigentuemerRef` — die Datei fuehrt das Feld an keinem Kanal, also gab es kein
Vorbild, und in der Modellliste ist es das letzte. Ein Verweis selbst wird von dieser
Regel nicht vorgezogen.


## Was in diesen Feldern wirklich steht

Gemessen am 2026-09-02 an `D:\QGIS_V4.2\Layer\Leitungen Lokal.gpkg`, 110297
Leitungen. "echt" heisst: weder leer noch `unbekannt`.

| Feld | echt gefuellt | haeufigster Wert |
|---|---|---|
| `ka_funktionhierarchisch` | **100,0 %** | SAA.Liegenschaftsentwaesserung (62801) |
| `ha_lagebestimmung` | 98,0 % | genau |
| `bw_status` | 97,8 % | in_Betrieb |
| `ka_funktionhydraulisch` | 93,5 % | Freispiegelleitung (92872) |
| `bw_baujahr` | 43,0 % | — |
| `bw_baulicherzustand` | 33,8 % | Z4 |
| `bw_sanierungsbedarf` | 30,6 % | keiner |
| `bw_bruttokosten` | 18,2 % | — |
| `ka_verbindungsart` | **1,0 %** | Steckmuffen (646) |
| `ka_bettung_umhuellung` | **1,0 %** | SIA_Typ1 (476) |
| `ha_innenschutz` | 0,1 % | andere |
| `org_organisation`, `gemeinde` | **0 %** | leer |

Zwei Folgerungen: Die funktionale Hierarchie ist zu **78 % ein SAA-Wert**
(62801 + 17729 + 5753 von 110297). Die Auswahl in SewerStudio kannte bis
2026-09-02 keinen einzigen SAA-Wert — fuer vier Fuenftel des Bestands gab es also
gar keinen passenden Eintrag. Und `Verbindungsart` und `Bettung_Umhuellung` sind im
Kataster praktisch leer: Dort hat der Rueckweg wenig zu korrigieren, aber viel
beizutragen.

### 383 Leitungen tragen einen Wert, den das Modell nicht kennt

Ebenfalls gemessen, bei `ka_funktionhierarchisch` — 21 verschiedene Werte, das
Modell erlaubt 14:

| Wert | Anzahl | Warum ungueltig |
|---|---|---|
| `SAA.Sammelkanal` | 247 | `SAA` hat kein `Sammelkanal` |
| `SAA.Gewaesser` | 94 | `SAA` hat kein `Gewaesser` |
| `SAA.Hauptsammelkanal` | 15 | `SAA` hat kein `Hauptsammelkanal` |
| (leer) | 17 | — |
| `.` | 8 | offensichtlich kaputt |
| `unbekannt.unbekannt` | 1 | — |
| `.andere` | 1 | — |

`PAA` hat neun Blaetter, `SAA` nur fuenf (andere, Liegenschaftsentwaesserung,
Sanierungsleitung, Strassenentwaesserung, unbekannt). Ein INTERLIS-Pruefer lehnt
diese Werte ab; SewerStudio schreibt sie fail-closed nicht.

## Melioration: kein Eigentuemertyp, aber eine Leitungsfunktion

`Organisationstyp` kennt kein `Melioration` — deshalb stehen
`Meliorationsgenossenschaft Reussebene Uri` und `Meliorationsgesellschaft Seedorf`
auf `Genossenschaft_Korporation`.

Das Modell fuehrt den Begriff dafuer an anderer Stelle:

| Feld | Werte |
|---|---|
| `Kanal.FunktionMelioration` | Hauptkanal · Sammelkanal · Sauger · unbekannt |
| `Abwasserknoten.Funktion_Knoten_Melioration` | 8 Werte |

Der Kataster fuehrt diese Spalte nicht: In den 54 Spalten von `Leitungen Lokal`
kommt "Melioration" nur im Eigentuemernamen vor. 1649 Leitungen gehoeren zwei
Meliorations-Organisationen, keine einzige ist als Meliorationsleitung
gekennzeichnet.
