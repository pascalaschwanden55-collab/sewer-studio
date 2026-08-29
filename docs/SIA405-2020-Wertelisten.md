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
