# Dossier-Stapelanlage aus Grundbuch- und Netzdaten

**Datum:** 2026-08-23 (überarbeitet nach der Prüfung desselben Tages)
**Vorgänger:** `2026-08-22-eigentuemerdossier-design.md`, `2026-08-23-eigentuemerdossier-felder-design.md`

## Ziel

Pascal drückt im Dossier-Bereich einen Knopf und bekommt für sein Projekt die
Eigentümerdossiers auf einmal — Parzellen, Eigentümer, Adressen und die betroffenen privaten
Abwasserleitungen automatisch ermittelt. Er tippt keine Parzellennummer.

**Telefonnummern gehören ausdrücklich nicht dazu.** Begründung unten.

## Belegte Machbarkeit

Alles Folgende wurde am 2026-08-23 gegen die echten Dienste und das echte Projekt
`D:\Projekte\Jagdmatt_2026` gemessen. Personennamen sind in diesem Dokument durch Platzhalter
ersetzt — echte Namen gehören nicht in ein Konzept.

| Quelle | Abfrage | Ergebnis |
|---|---|---|
| WFS `av:ch059_liegenschaften_flaechen` | `nummer='439' AND bfsnr=1206` | Nummer, Gemeinde, BFS, EGRID, 1'139 m², Umriss, `url_grundbuch` |
| `geo.ur.ch/grundbuchauskunft/?gem=1206&nr=439` | HTML, **ISO-8859-1** | zwei Miteigentümer als `Lit.A:` und `Lit.B:`, je 1/2, mit Gebäudeadresse |
| dieselbe Auskunft, Parzelle 13 | — | `Eigentümer: Keine` — dieser Fall existiert wirklich |
| WFS `leitungen:abw_haltungen` | `ne_bezeichnung IN (…)` | 110'632 Haltungen im Dienst, Bezeichnungsformat identisch zu SewerStudio |
| dieselbe Ebene | `INTERSECTS(wkb_geometry, <Umriss Pz. 439>)` | 6 Haltungen: 5 privat, 1 Abwasser Uri; 5 davon im Projekt |
| WFS `av:ch062_hoheitsgrenzen_gemeindegrenzen` | — | 19 Urner Gemeinden mit BFS-Nummer |

### Der ganze Durchlauf für Jagdmatt, wirklich ausgeführt

| Messgrösse | Wert |
|---|---|
| Haltungen im Projekt | 71 |
| davon beim Kanton gefunden | **37 (52 %)** |
| von diesen 37 berührte Parzellen | **12** |
| Abfragen dafür | 3 Sammelabfragen + 1 Parzellensuche + 12 × 2 = **28** |

**Die fehlenden 34 sind kein Zufall.** Ihre Knotennamen haben fast alle die Form
`<Parzellennummer>.<lfd>` — `439.01-36051`, `952.02-952.03`, `438.03-438.04`. Das sind die
**privaten Hausanschlüsse**, und genau die führt der Kanton in seiner öffentlichen Netzebene
weitgehend nicht. Da das Eigentümerdossier gerade von diesen Leitungen handelt, wäre die
räumliche Suche allein zu wenig.

### Daraus folgt ein zweiter, kostenloser Weg

Aus den 71 Haltungsnamen lassen sich über die Knotenform `<Nummer>.<lfd>` **8 Parzellen**
ableiten — ohne eine einzige Abfrage. Gegenprobe gegen die 12 räumlich gefundenen:

```
in beiden:        438, 439, 797, 905, 952, 975, 982, 1273   (8)
nur aus Namen:    keine
nur raeumlich:    435, 901, 993, 1356                        (4)
```

**Kein Widerspruch in eine Richtung.** Die Namensregel liefert nichts Falsches, die räumliche
Suche liefert mehr. Beide Wege werden verwendet, und die Herkunft steht je Fund in der Liste.

Die Namensregel bleibt trotzdem **fail-closed**: Eine aus dem Namen abgeleitete Nummer gilt
erst als Parzelle, wenn der Parzellendienst sie bestätigt. Belegt ist die Regel bisher nur an
diesem einen Projekt.

## Warum keine Telefonsuche

Die Nutzungsbedingungen des Telefonverzeichnisses (Swisscom Directories AG) untersagen
wörtlich:

> „Maschinelle Massenabfragen, beispielsweise zur Erstellung oder Aktualisierung von
> Adressdatenbanken"

Das **ist** die Stapelanlage. Ausdrücklich erlaubt sind dagegen „Suche und Darstellung von
Einträgen", „Abspeicherung des Eintrags in unternehmenseigenen Systemen" und die Nutzung „für
den unternehmensinternen Gebrauch" — also die Einzelabfrage, die ein Mensch auslöst und
bestätigt.

**Später möglich, aber nicht in diesem Vorhaben:** eine Telefonsuche je Eigentümerzeile, von
Hand ausgelöst, mit bestelltem API-Schlüssel und der pflichtigen sichtbaren Quellenangabe
„Swisscom Directories AG". Das braucht ein eigenes Konzept.

Gemessene Randbedingungen für später: Der volle Grundbuchname findet nichts — gesucht werden
muss über **Nachname + Strasse + Ort**. Und ein Vorname kann abweichen (Kurzform im
Verzeichnis); das ist ohne menschliche Bestätigung nicht entscheidbar.

## Entscheidungen

1. **Stapelanlage zuerst.** Der Einzelknopf in der Liegenschaftsmaske fällt später ab.
2. **Die Parzellen findet das Programm selbst** — aus den Haltungsnamen und über die Lage.
3. **Keine Telefonsuche** (siehe oben).
4. **Nie stillschweigend.** Nichts wird geschrieben, bevor der Benutzer bestätigt.
5. **Kein Riesen-Modul** (ausdrückliche Vorgabe): ein Leser je Quelle, die Regeln getrennt und
   ohne Netzzugriff, das Fenster ohne Regel.
6. **Uri.** Andere Kantone bleiben draussen; die Verträge erlauben später einen zweiten Satz Leser.

## Ablauf

```
[ Dossiers aus dem Projekt erzeugen ]  ->  Gemeinde waehlen

  A  Parzellennummern aus den Haltungsnamen ableiten     0 Abfragen
  B  Haltungsnamen -> Lage beim Kanton                   3 Sammelabfragen (25 je Anfrage)
  C  EINE Abfrage: welche Parzellen beruehren diese Linien
  D  Kandidaten aus A beim Parzellendienst bestaetigen   (meist in C schon enthalten)
  E  Je Parzelle: welche Haltungen liegen darauf         12 Abfragen
  F  Je Parzelle: Grundbuchauskunft                      12 Abfragen
  ->  Vorschlagsliste zum Abhaken  ->  [ Dossiers erzeugen ]
```

Gemessen für Jagdmatt: **28 Abfragen**. Der Reihe nach, nicht gleichzeitig, mit Fortschritt
und Abbruch.

## Was ein erzeugtes Dossier enthält

| Feld | Quelle |
|---|---|
| Name | `Liegenschaft Nr. <Nummer> <Nachname erster Eigentümer>` — Pascals bisherige Schreibweise |
| ParcelNumbers, Municipality, MunicipalityBfsNr | Parzellendienst |
| Address, HouseNumbers, PostalCode, Town | Grundbuchauskunft (Gebäudeadresse) |
| Owners (eine Zeile je Eigentümer, **ohne Telefon**) | Grundbuchauskunft |
| HoldingIds | private Haltungen der Parzelle, die im Projekt existieren |

### Welche Haltungen vorausgewählt werden

Vorausgewählt ist eine Haltung genau dann, wenn sie **privat** ist **und im Projekt existiert**.

Für Parzelle 439 sind das **genau vier**: von den sechs Haltungen auf der Parzelle gehört eine
Abwasser Uri (im Projekt, aber nicht privat), und eine private fehlt im Projekt. Diese Zahl ist
die Zusicherung des zugehörigen Tests.

Haltungen aus der Namensregel sind per Definition im Projekt. Ihre Eigentümerangabe kennt das
Programm nicht; sie werden als privat behandelt, weil die Knotenform `<Parzelle>.<lfd>` den
Hausanschluss bezeichnet. Diese Annahme steht sichtbar in der Vorschlagsliste.

### Neue Felder am Dossier

`DossierDefinition` bekommt additiv:

```csharp
public string Municipality { get; set; } = "";   // "Erstfeld"
public int? MunicipalityBfsNr { get; set; }      // 1206
```

`DossierDocument.CurrentSchemaVersion` geht von 2 auf 3.

**Achtung — hier steckt eine Falle, die bei der Prüfung gefunden wurde.**
`DossierDocumentMigration` erkennt Altbestand heute an `SchemaVersion < CurrentSchemaVersion`.
Mit der Erhöhung auf 3 gälte jede Version-2-Datei wieder als Altbestand, und die Ableitung der
Eigentümerzeile aus `OwnerName` liefe erneut — **eine bewusst gelöschte Zeile käme zurück**.
Genau der Fehler, der als W1 schon einmal behoben wurde.

Die Bedingung wird deshalb an die Herkunft gebunden statt an „kleiner als aktuell":

```csharp
// Nur Dateien aus Version 1 bekommen die Eigentuemerzeile abgeleitet.
// "kleiner als aktuell" waere falsch: bei jeder kuenftigen Versionserhoehung liefe die
// Ableitung erneut und braechte geloeschte Zeilen zurueck.
var stammtAusVersion1 = document.SchemaVersion < 2;
```

Ein Test sichert das ab: eine Version-2-Datei mit leerer `Owners`-Liste und gefülltem
`OwnerName` bleibt nach der Umstellung auf 3 **ohne** Eigentümerzeile.

Leer bleiben Occupancy, Mail, ConstructionProcess, Remarks — keine Quelle kennt sie.

## Bausteine

### Application — rechnet, kennt kein Netz

Verträge, je eine Datei:

```csharp
public interface IParcelLookup
{
    Task<ParcelInfo?> FindAsync(int bfsNr, string parcelNumber, CancellationToken ct);
    Task<IReadOnlyList<ParcelInfo>> FindTouchedAsync(IReadOnlyList<string> wktLines, CancellationToken ct);
    Task<IReadOnlyList<Municipality>> ListMunicipalitiesAsync(CancellationToken ct);
}

public interface ILandRegistryLookup
{
    Task<LandRegistryEntry?> ReadAsync(ParcelInfo parcel, CancellationToken ct);
}

public interface ISewerNetworkLookup
{
    Task<IReadOnlyList<NetworkHolding>> FindByNamesAsync(IReadOnlyList<string> names, CancellationToken ct);
    Task<IReadOnlyList<NetworkHolding>> FindOnParcelAsync(ParcelInfo parcel, CancellationToken ct);
}
```

Ergebnisse sind schlichte Datensätze: `Municipality(BfsNr, Name)`,
`ParcelInfo(Number, BfsNr, Municipality, AreaSqm, Egrid, OutlineWkt, LandRegistryUrl)`,
`LandRegistryOwner(Designation, Name, AddressLine, Share)`,
`LandRegistryEntry(BuildingAddress, PostalCode, Town, IReadOnlyList<LandRegistryOwner> Owners, bool NoOwnerRegistered)`,
`NetworkHolding(Designation, Owner, LengthMeters, GeometryWkt)`.

Regeln:

- `ParcelNumberFromHoldingName` — pure Funktion: `439.01-36051` → Kandidat `439`. Kennt kein Netz.
- `DossierBatchProposalUseCase` — nimmt die Leser als Abhängigkeit, führt beide Wege zusammen,
  hält je Fund die Herkunft fest und markiert Übersprungenes mit Grund.
- `DossierBatchCreationUseCase` — macht aus bestätigten Vorschlägen `DossierDefinition`-Einträge
  und gibt sie zurück. Kein Dateizugriff; der Aufrufer speichert.
- `DossierNameBuilder` — pure Funktion für den Dossiernamen.

### Infrastructure — nur Netz, kennt kein Dossier

- `UriParcelWfsClient`
- `UriLandRegistryClient` — liest **ISO-8859-1**, erkennt `Lit.A:`/`Lit.B:` und `Eigentümer: Keine`
- `UriSewerNetworkWfsClient` — Sammelabfrage über `IN (…)`, räumliche Abfrage per **POST**
  (der Filter für 37 Linien ist 2'342 Zeichen lang und gehört nicht in eine URL)
- `GeoUrHttpGateway` — gemeinsames Zeitlimit, Abbruch, Aufrufe der Reihe nach

Jeder Leser: eine Datei, eine Quelle, schlichte Rückgabe.

### UI

`DossierBatchWindow` + ViewModel: Gemeindeauswahl, Fortschritt, Vorschlagsliste zum Abhaken,
Abbrechen. Enthält keine Regel.

## Fehlerpfade

| Fall | Verhalten |
|---|---|
| Kein Netz, Dienst nicht erreichbar | Klare Meldung, nichts erzeugt |
| Zeitüberschreitung einer Abfrage | Diese Parzelle gilt als „nicht abgefragt", der Lauf geht weiter |
| Grundbuchseite anders aufgebaut | Leser liefert nichts. Nie ein geratener Name |
| `Eigentümer: Keine` | Parzelle erscheint, ist aber nicht auswählbar |
| Parzelle hat schon ein Dossier | Erscheint als solche, nicht auswählbar |
| Aus dem Namen abgeleitete Nummer ist keine Parzelle | Verworfen, nicht gezeigt |
| Haltung auf der Parzelle fehlt im Projekt | Nur Hinweis, nicht vorausgewählt |
| Abbruch mitten im Lauf | Nichts erzeugt |
| Bestätigung | Alle Dossiers in einem Speichervorgang |

## Tests

Die Anwendungsfälle werden mit erfundenen Lesern geprüft — **ohne Internet**. Jeder Testfall ist
ein am 2026-08-23 real gemessener Fall:

| Test | Belegt durch |
|---|---|
| Zwei Miteigentümer ergeben zwei Zeilen | Pz. 439, `Lit.A`/`Lit.B` |
| `Eigentümer: Keine` erzeugt kein Dossier | Pz. 13 Erstfeld |
| **Genau vier** Haltungen vorausgewählt | Pz. 439: 6 auf der Parzelle, 5 im Projekt, 1 dem Kanton, 1 private fehlt im Projekt |
| Namensregel liefert Kandidaten und nichts Falsches | 8 abgeleitet, alle 8 räumlich bestätigt |
| Unbestätigte Namensnummer wird verworfen | fail-closed |
| Version-2-Datei ohne Eigentümerzeile bleibt ohne | die W1-Falle bei der Erhöhung auf 3 |
| Bestehendes Dossier wird übersprungen | Pz. 439 hat eins |
| Abbruch erzeugt nichts | — |
| Dienstfehler meldet sich klar | — |
| Dossiername | `Liegenschaft Nr. 439 <Nachname>` |

Dazu **ein** maschinengebundener Abnahmetest gegen die echten Dienste mit Parzelle 439 und den
oben gemessenen Werten, im Stil der bestehenden Live-Abnahmetests. **Kein neuer übersprungener
Test** — der Wächter über die sieben zulässigen Skip-Stellen würde rot.

## Risiken

- **Die Grundbuchauskunft ist eine Webseite, keine Schnittstelle.** Ändert der Kanton das
  Aussehen, liefert der Leser nichts mehr. Der Abnahmetest macht das sichtbar.
- **Nur gut die Hälfte der Haltungen kennt der Kanton** (37 von 71). Die Namensregel fängt die
  privaten Hausanschlüsse auf, ist aber erst an einem Projekt belegt.
- **Personendaten.** Eigentümernamen und Adressen landen in der Projektdatei. Die Quelle ist
  öffentlich und wird für ihren Zweck verwendet. Echte Namen gehören nicht in Testdaten,
  Konzepte oder Commit-Nachrichten.
- **Nur Uri.** Ein Projekt ausserhalb des Kantons kann die Funktion nicht nutzen; das Fenster
  sagt das, statt leere Ergebnisse zu zeigen.

## Bewusst nicht enthalten

- **Telefonnummern** (Nutzungsbedingungen, siehe oben).
- Andere Kantone.
- Automatisches Nachführen erzeugter Dossiers bei geänderten Grundbuchdaten.
- Objektbewohner, E-Mail, Bauvorgang, Bemerkungen.
- Eine eigene Geometrie-Bibliothek: alle räumlichen Fragen beantwortet der Dienst.
