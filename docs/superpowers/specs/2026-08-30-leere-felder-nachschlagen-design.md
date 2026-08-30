# Leere Felder per Rechtsklick nachschlagen — Design

Datum: 2026-08-30
Status: Entwurf zur Durchsicht

## Ausgangslage

Nach einem Import bleiben Felder leer, weil die Quelle sie nicht kennt. Der
Kanton Uri fuehrt diese Angaben aber: im Abwasserkataster und im Grundbuch.
Beide Quellen sind im Programm bereits angeschlossen — nur nicht mit den
Feldern verbunden.

Alle Zahlen unten sind an den echten Projekten unter `D:\Projekte` gemessen,
nicht geschaetzt.

### Wo die Luecken wirklich sind

Ueber 13 Projekte mit zusammen 390 Schaechten:

| Feld | kommt vor | davon leer | Quelle |
|---|---|---|---|
| `Eigentuemer` | 257 | **233 (90 %)** | Grundbuch |
| `Strasse` | 374 | **102 (27 %)** | Grundbuch |
| `Funktion` | 378 | **40 (10 %)** | Kataster |

Bei **Haltungen** ist die Lage deutlich besser. Ueber alle 475 Haltungen aller
Projekte gemessen: `Rohrmaterial` ist 25-mal leer, `Haltungslaenge_m` 22-mal.
In den drei zuerst geprueften Projekten (147 Haltungen) waren beide Felder
sogar vollstaendig gefuellt.

Die grossen Haltungs-Luecken sind `Eigentuemer` und `FunktionHierarchisch` —
und fuer beide hat der Kataster keine Antwort (siehe Nicht-Ziele).

Der Schwerpunkt liegt damit eindeutig bei den **Schaechten**: 335 Faelle
gegen 47.

### Was schon da ist

- `HaltungCadastreExtractor` liest die Kataster-XTF und legt eine Tabelle an.
  Er verarbeitet heute nur Haltungen, nicht Schaechte.
- `IParcelLookup`, `ILandRegistryLookup`, `ISewerNetworkLookup` und
  `GeoUrHttpGateway` sind fuer das Eigentuemerdossier vollstaendig angebunden.
- `SchachtRecord.SetFieldValue(feld, wert, FieldSource, userEdited)` schreibt
  bereits mit Herkunft und schuetzt dabei jeden von Hand gesetzten Wert.
- `RecordDetailsView` besitzt bereits ein Feld-Kontextmenue
  (`ManagedOptionsContextMenu`) — allerdings nur an Auswahlfeldern.

## Ziel und Nicht-Ziel

**Ziel:** Ein Rechtsklick in ein leeres Schachtfeld schlaegt den Wert beim
Kanton nach, zeigt ihn als Vorschlag und uebernimmt ihn erst nach
ausdruecklicher Bestaetigung — mit nachvollziehbarer Herkunft.

**Nicht-Ziele:**

- **Kein Sammellauf.** Es gibt keinen Knopf "alle leeren Felder fuellen". Die
  Grundbuchauskunft erlaubt ausdruecklich nur Einzelabfragen mit Bestaetigung;
  der Dienst drosselt zusaetzlich mit HTTP 429.
- **Keine gefuellten Felder.** Der Menuepunkt erscheint nur an leeren Feldern.
  Importierte und selbst eingetragene Werte bleiben unberuehrt.
- **Kein `Eigentuemer` aus dem Abwasserkataster.** Die XTF hat zwar
  `EigentuemerRef`, aber alle 174'291 Verweise zeigen auf dieselbe
  Organisation ("Abwasser Uri", Kanton). Als Feldwert wertlos.
- **Kein `FunktionHierarchisch`.** Das Attribut kommt in der Kataster-XTF
  nicht vor (0 Treffer in 467 MB).
- **Keine Aenderung an der QGIS-Bruecke, am Dossier-Weg oder am Import.**

## Die Kette

Der entscheidende Punkt des Entwurfs: Der Grundbuchweg braucht Koordinaten,
und die hat nur der Kataster. Schacht-Datensaetze im Projekt fuehren keine
Lage, und die Haltungsnamen nennen meist keine Parzelle (bei
Jagdmatt_Erstfeld sind es reine Schachtpaare wie `36262-36275`).

```text
Schachtnummer
   -> Kataster-XTF          Bezeichnung -> Abwasserknoten -> Lage (E/N)
   -> Parzelle              raeumlicher WFS-Treffer auf der Lage
   -> Grundbuch             Eigentuemer, Strasse, Hausnummer, PLZ, Ort
```

Daraus ergeben sich zwei Nutzenstufen auf einer Kette:

1. **Kataster** fuellt `Funktion` (34 nachgewiesene Faelle) und liefert die
   Lage.
2. **Grundbuch** setzt auf dieser Lage auf und fuellt `Eigentuemer` und
   `Strasse` (335 Faelle).

Stufe 2 funktioniert ohne Stufe 1 nicht. Beide gehoeren deshalb in einen
Entwurf; ob sie in einem oder zwei Schritten gebaut werden, entscheidet der
Umsetzungsplan.

### Ein Nebeneffekt, der ein Risiko beseitigt

Weil ueber die **Lage** gesucht wird und nicht ueber eine Parzellennummer,
entfaellt die Gemeinde-Falle: Parzellennummern sind je Gemeinde vergeben, und
genau daran ist beim Dossier-Bau schon einmal ein Brief an einen
Unbeteiligten entstanden. Ein raeumlicher Treffer ist eindeutig.

## Bausteine

Jeder Baustein hat eine Aufgabe, ein Interface und einen eigenen Test.

### 1. `SchachtCadastreExtractor` (Infrastructure/Map)

Analog zum bestehenden `HaltungCadastreExtractor`, aber fuer Schaechte. Liest
aus der Kataster-XTF je Schacht: `Bezeichnung`, `Funktion`, `Material`,
`Dimension1`, `Dimension2`, `Status` und die Lage aus dem zugehoerigen
`Abwasserknoten` (ueber `AbwasserbauwerkRef`).

Legt wie das Vorbild eine Tabellendatei an und prueft ueber
`IsTableFresh(tabelle, xtf)`, ob sie noch zur XTF passt. Die XTF ist 467 MB —
sie wird genau einmal gelesen, danach nur noch die Tabelle.

```text
Bezeichnung  Funktion                  Material   Dim1  Dim2  Status  E          N
80401        Kontroll_Einsteigschacht  unbekannt  0     0     unbek.  2692606.9  1192380.7
```

### 2. `IFeldWertNachschlag` (Application/Lookup)

Der gemeinsame Vertrag beider Quellen:

```csharp
Task<FeldVorschlag?> SucheAsync(FeldNachschlagAnfrage anfrage, CancellationToken ct);
```

`FeldNachschlagAnfrage` traegt Schachtnummer, Feldname und den Projektbezug.
`FeldVorschlag` traegt den gefundenen Wert, die Quelle im Klartext
("Abwasserkataster", "Grundbuch Uri"), eine Herkunftsangabe fuer die
Feldmetadaten und — bei mehreren Treffern — die Auswahlliste.

Zwei Implementierungen:

- `KatasterFeldNachschlag` — rein lokal, liest die Schacht-Tabelle.
- `GrundbuchFeldNachschlag` — verkettet Lage, `IParcelLookup.FindTouchedAsync`
  und `ILandRegistryLookup.ReadAsync`.

#### Platzhalter sind kein Wert

Der Kataster fuehrt fehlende Angaben nicht als leeres Feld, sondern als
ausgeschriebenen Platzhalter. Im echten Bestand steht woertlich:

```xml
<Material>unbekannt</Material>
<Dimension1>0</Dimension1>
<Status>unbekannt</Status>
```

Wuerde der Nachschlag diese Werte durchreichen, stuende danach "unbekannt"
im Protokoll — schlechter als ein leeres Feld, weil es wie eine gepruefte
Aussage aussieht. `KatasterFeldNachschlag` behandelt `unbekannt`, `unbek.`,
`0` und Leerstring deshalb als **kein Treffer** und meldet "nicht gefunden".

Wie stark die Regel greift, ist je Attribut sehr verschieden. Ueber 34'403
Normschaechte gemessen:

- **`Funktion` ist zu 97 % ein echter Wert** (21'664 Kontroll_Einsteigschacht,
  7'177 Schlammsammler, 3'604 Einlaufschacht, 440 Oelabscheider, 229
  Dachwasserschacht, 207 Pumpwerk). Nur 1'073 tragen "andere" oder
  "unbekannt". Die Platzhalter-Regel kostet hier also kaum Treffer.
- **`Material` und die Dimensionen** tragen dagegen fast durchgehend
  Platzhalter. `Material` bleibt in der Zuordnungstabelle, wird in der Praxis
  aber selten einen Wert liefern.

### 3. `FeldNachschlagUseCase` (Application/UseCases)

Waehlt anhand des Feldnamens die zustaendige Quelle, ruft sie auf und liefert
ein Ergebnis mit klarem Zustand: gefunden, nicht gefunden, mehrdeutig oder
Fehler. Er schreibt selbst nichts.

Die Feld-zu-Quelle-Zuordnung liegt als eigene, WPF-freie Tabelle vor, damit
sie testbar bleibt und spaeter ohne UI-Aenderung erweitert werden kann:

| Feld | Quelle | Kataster-Attribut |
|---|---|---|
| `Funktion` | Kataster | `Funktion` |
| `Material` | Kataster | `Material` |
| `Eigentuemer` | Grundbuch | Owners |
| `Strasse` | Grundbuch | BuildingStreet + HouseNumber |

### 4. Punkt statt Linie

`IParcelLookup.FindTouchedAsync` erwartet heute Linien
(`MULTILINESTRING`), weil es fuer Haltungen gebaut wurde. Ein Schacht ist ein
Punkt. Statt den bewaehrten WFS-Client zu aendern, baut der
`GrundbuchFeldNachschlag` aus der Lage eine sehr kurze Linie (0,5 m in beide
Richtungen) und uebergibt sie unveraendert. Das laesst den bestehenden
Dossier-Weg vollstaendig unberuehrt.

### 5. Bedienung: Kontextmenue und Vorschlag

`RecordDetailsView` erhaelt einen zweiten Menuepunkt an Textfeldern:
**"Beim Kanton nachschlagen"**. Er erscheint nur, wenn das Feld leer ist und
in der Zuordnungstabelle steht.

Der Klick oeffnet ein kleines Fenster, das zeigt:

```text
Schacht 33429 · Feld "Eigentuemer"

  Gefunden:  Muster, Hans
             Musterweg 4, 6472 Erstfeld
  Quelle:    Grundbuch Uri, Parzelle 439 (Erstfeld)

  [ Uebernehmen ]  [ Abbrechen ]
```

Bei mehreren Eigentuemern (Miteigentum, Stockwerkeigentum) erscheinen alle
zur Auswahl. Bei mehreren beruehrten Parzellen wird **nicht geraten**: Das
Fenster zeigt die Kandidaten und verlangt eine Entscheidung.

## Herkunft und Schutz

`FieldSource` erhaelt zwei neue Werte: `Kataster` und `Grundbuch`. Damit
bleibt in den Feldmetadaten sichtbar, dass ein Wert nicht aus dem Import
stammt.

Geschrieben wird ueber die vorhandene Ueberladung
`SetFieldValue(feld, wert, source, userEdited: true)`. Der vorhandene
Handwert-Schutz von `SchachtRecord` bleibt unveraendert und wird nicht
umgangen.

### Der Schutz haengt an genau einem Faden

`userEdited: true` ist nicht nur "auch richtig", sondern der **einzige**
Schutz. Das muss ausdruecklich festgehalten werden, weil die naheliegende
Gegenannahme falsch ist:

`MergeEngine.cs:124` entscheidet mit
`if (GetPriority(importSource) > GetPriority(existingSource))` — die
**hoehere** Prioritaet gewinnt. `GetPriority` endet auf `_ => 0`, neue
Herkuenfte wie `Kataster` und `Grundbuch` bekommen also Prioritaet 0 und
verlieren damit gegen **jeden** Import (`Xtf` = 80, `Pdf` = 60, sogar
`Protocol` = 40). Eine niedrige Prioritaet schuetzt nicht, sie ist das
Gegenteil davon.

Was tatsaechlich schuetzt, steht dreissig Zeilen frueher in
`MergeEngine.cs:95`: `if (userEdited) { AddConflict(...); continue; }` —
noch vor jeder Prioritaetsrechnung. Genau deshalb ist `userEdited: true`
Pflicht.

Wer spaeter auf den Gedanken kommt, `userEdited: false` sei richtiger ("der
Wert kommt schliesslich vom Kanton und nicht vom Menschen"), macht jeden
nachgeschlagenen Wert vollstaendig ungeschuetzt. Ein Test haelt das fest
(siehe Tests).

`MergeEngine` selbst wird nicht angefasst.

## Grenzen und Risiken

- **Nicht jeder Schacht ist im Kataster.** Gemessen: 34 von 40 Schaechten des
  Projekts Jagdmatt_Erstfeld (85 %). Fehlt die Nummer, meldet das Fenster das
  ehrlich, statt einen Wert zu erfinden.
- **Der Grundbuchdienst drosselt (HTTP 429).** Ein Klick ist eine Abfrage; es
  gibt kein automatisches Nachladen und keinen Stapellauf. Eine Drosselung
  wird als solche gemeldet.
- **Der Dienst ist auf den Kanton Uri begrenzt.** Ausserhalb liefert er
  nichts; das Fenster sagt das.
- **Das Telefonverzeichnis wird nicht verwendet.** Fuer Massenabfragen ist es
  woertlich verboten; der Entwurf braucht es nicht.
- **Die XTF muss konfiguriert sein.** Fehlt `AbwasserkatasterXtfPath`, bleibt
  der Menuepunkt gesperrt mit sichtbarem Grund.
- **Keine Personendaten im Protokoll.** Die Grundbuchauskunft liefert Namen
  und Wohnadressen echter Personen. In die Logdatei gehen nur Status, Dauer
  und Fehlerklasse — nie ein Name, nie eine Adresse. Der Wert selbst steht
  danach im Projekt, wo er hingehoert; die Logdatei wandert dagegen in
  Diagnosepakete und Sicherungen.

## Was unberuehrt bleibt

Import, QGIS-Bruecke, Eigentuemerdossier, Export und Verteilung werden nicht
angefasst. Der Dossier-Weg nutzt dieselben Lookup-Dienste weiter, ohne
Aenderung an deren Vertraegen. Kundendateien werden ausschliesslich gelesen.

## Tests

- `SchachtCadastreExtractor`: Liest Bezeichnung, Funktion und Lage aus einem
  kleinen XTF-Ausschnitt; erkennt eine veraltete Tabelle.
- `KatasterFeldNachschlag`: `unbekannt`, `unbek.` und `0` ergeben "nicht
  gefunden" und werden nie als Wert weitergereicht.
- `FeldNachschlagUseCase`: Waehlt je Feldname die richtige Quelle; meldet
  "nicht gefunden" und "mehrdeutig" als eigene Zustaende statt als Wert.
- `GrundbuchFeldNachschlag`: Baut aus einer Lage die erwartete kurze Linie;
  verarbeitet mehrere Eigentuemer; behandelt HTTP 429 als eigenen Fehler.
- Feldzuordnung: Die Tabelle nennt fuer jedes unterstuetzte Feld genau eine
  Quelle.
- Uebernahme: Schreibt mit der neuen `FieldSource` und `userEdited: true`;
  ein bereits gefuelltes Feld wird nicht angeboten.
- **Merge-Schutz (der wichtigste Test):** Ein Feld mit `FieldSource.Kataster`
  und `userEdited: true` ueberlebt einen anschliessenden XTF-Import. Der
  Gegentest gehoert dazu: Dasselbe Feld mit `userEdited: false` wird vom
  Import ueberschrieben. Ohne diesen zweiten Test sieht die Absicherung
  staerker aus, als sie ist — er belegt, dass der Schutz wirklich an
  `userEdited` haengt und nicht an der Herkunft.
- Kein Personenname und keine Adresse erscheinen in der Logausgabe.
- Ein Waechter haelt fest, dass es keinen Sammellauf-Befehl gibt.

## Beweis

Belegt ist die Arbeit erst, wenn alles zutrifft:

- `dotnet build AuswertungPro.sln` ohne Fehler und ohne neue Warnungen.
- `dotnet test AuswertungPro.sln` vollstaendig gruen.
- Im Programm: Rechtsklick auf das leere Feld `Funktion` eines Schachts aus
  Jagdmatt_Erstfeld schlaegt den Kataster-Wert vor und uebernimmt ihn nach
  Bestaetigung.
- Rechtsklick auf `Eigentuemer` liefert den Grundbucheintrag derselben
  Parzelle, die das Eigentuemerdossier fuer diese Liegenschaft anzeigt.
- Ein bereits gefuelltes Feld bietet den Menuepunkt nicht an.
- Nach dem Uebernehmen steht in den Feldmetadaten die neue Herkunft.

## Geprueft und verworfen

Damit diese Wege nicht ein zweites Mal untersucht werden:

**Ein Lagefehler im `XtfManholeExtractor` existiert nicht.** Die Vermutung
lautete, der Leser koenne einen Punkt ausserhalb von `Lage/COORD` erwischen
(etwa vom Deckel) und brauche einen zusaetzlichen `inLage`-Guard. Nachgeprueft
an den echten Daten: Von **34'403 untersuchten Abwasserknoten hat kein
einziger mehr als eine Koordinate** — die Verteilung ist exakt `{1: 34403}`.
Der Leser setzt `inKnoten` am Start- und am Endelement sauber und liest C1/C2
nur innerhalb eines Abwasserknotens; ein `Deckel` ist ein Geschwister-Element,
kein Kind. Ein Guard und ein Regressionstest wuerden hier einen Fehler
absichern, den es nicht gibt.

**Eine niedrige Merge-Prioritaet als Schutzmechanismus.** Siehe "Der Schutz
haengt an genau einem Faden" — die Prioritaetsregel wirkt umgekehrt.

**`ManholeGeometry` erweitern statt eine eigene Schachttabelle zu bauen.**
`ManholeGeometry` und sein Cache dienen der Kartendarstellung und tragen nur
Name und Koordinaten. Eine Erweiterung um Fachattribute wuerde einen
funktionierenden Anzeigeweg fuer einen Datenzweck umbauen. Der eigene
`SchachtCadastreExtractor` bleibt davon getrennt — dieselbe Trennung, die
`HaltungCadastreExtractor` schon vormacht.

## Offene Punkte fuer den Umsetzungsplan

1. **Reihenfolge:** Kataster und Grundbuch in einem Schritt oder in zwei? Die
   Kette verlangt Kataster zuerst; der Nutzen liegt beim Grundbuch.
2. **Haltungen:** Der Entwurf beschraenkt sich auf Schaechte, weil dort die
   grossen Luecken liegen (335 gegen 47 Faelle). `Rohrmaterial` (25 leer) und
   `Haltungslaenge_m` (22 leer) waeren grundsaetzlich aus dem Kataster
   erreichbar — das ist eine spaetere, additive Entscheidung. Der Vertrag
   `IFeldWertNachschlag` ist bewusst so geschnitten, dass ein Haltungsfeld
   ohne Vertragsaenderung andocken kann.
3. **`Material`:** bleibt in der Zuordnungstabelle, liefert aber wegen der
   Platzhalter-Regel voraussichtlich selten einen Wert. Ob der Menuepunkt
   dafuer ueberhaupt erscheinen soll, entscheidet sich am besten nach dem
   ersten Praxistest.
4. **Parallele Arbeit:** An `SchachtansichtView.xaml` und
   `RecordDetailsView` wird derzeit gearbeitet (Detailansicht-Umbau). Der
   Umsetzungsplan muss den Stand zu seinem Beginn erneut pruefen.
