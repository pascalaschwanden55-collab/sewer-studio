# Dossier-Stapelanlage aus Grundbuch- und Netzdaten

**Datum:** 2026-08-23
**Vorgänger:** `2026-08-22-eigentuemerdossier-design.md`, `2026-08-23-eigentuemerdossier-felder-design.md`

## Ziel

Pascal drückt im Dossier-Bereich einen Knopf und bekommt für sein Projekt alle
Eigentümerdossiers auf einmal — Parzellen, Eigentümer, Adressen, Telefonnummern und die
betroffenen privaten Abwasserleitungen automatisch ermittelt. Er tippt keine Parzellennummer.

## Belegte Machbarkeit

Alles Folgende wurde am 2026-08-23 gegen die echten Dienste und das echte Projekt
`D:\Projekte\Jagdmatt_2026` gemessen, nicht angenommen.

| Quelle | Abfrage | Ergebnis |
|---|---|---|
| `geo.ur.ch` WFS `av:ch059_liegenschaften_flaechen` | `nummer='439' AND bfsnr=1206` | Nummer, Gemeinde, BFS, EGRID `CH114627077847`, 1'139 m², Umriss, `url_grundbuch` |
| `geo.ur.ch/grundbuchauskunft/?gem=1206&nr=439` | HTML, ISO-8859-1 | Lit.A Kurt Beispiel, Lit.B Rita Beispiel, beide Musterstrasse 30, 6472 Erstfeld, je 1/2 Miteigentum |
| `tel.search.ch/api/` | `was=Beispiel&wo=Musterstrasse 30 Erstfeld` | „Beispiel, Kurt" · 041 000 00 00 |
| `geo.ur.ch` WFS `leitungen:abw_haltungen` | `ne_bezeichnung='36051-36329'` | Treffer mit Linienlage; 110'632 Haltungen im Dienst, Bezeichnungsformat identisch zu SewerStudio |
| dieselbe Ebene | `INTERSECTS(wkb_geometry, <Umriss Pz. 439>)` | 6 Haltungen, davon 5 privat und 5 im Projekt vorhanden |

Die 19 Urner Gemeinden samt BFS-Nummern liefert
`av:ch062_hoheitsgrenzen_gemeindegrenzen`. Die Suche läuft über die BFS-Nummer, nicht über
den Namen — Schreibweisen wie „Altdorf (UR)" sind damit kein Thema.

**Nebenbefund:** Das bestehende Dossier „Liegenschaft Nr. 439 Beispiel" enthält eine der vier
privaten Leitungen auf dieser Parzelle und nur einen der zwei Miteigentümer.

## Entscheidungen

1. **Stapelanlage zuerst.** Der Einzelknopf in der Liegenschaftsmaske fällt später ab; die
   Bausteine sind dieselben.
2. **Die Parzellen findet das Programm selbst** aus den Haltungen des Projekts. Kein Tippen.
3. **Telefonnummern werden mitgesucht**, aber nur vorausgewählt, wenn der Vorname passt.
4. **Nie stillschweigend.** Nichts wird geschrieben, bevor der Benutzer bestätigt.
5. **Kein Riesen-Modul** (ausdrückliche Vorgabe): ein Leser je Quelle, die Regeln getrennt
   davon und ohne Netzzugriff, das Fenster ohne Regel.
6. **Uri.** Andere Kantone bleiben draussen; die Verträge erlauben später einen zweiten Satz
   Leser, ohne die Regeln anzufassen.

## Ablauf

```
[ Dossiers aus dem Projekt erzeugen ]  ->  Gemeinde waehlen
  1. Haltungsnamen des Projekts  ->  Sammelabfragen (25 je Anfrage)  ->  Lage je Haltung
  2. EINE Abfrage: welche Parzellen beruehren diese Linien
  3. Je Parzelle eine Abfrage: welche Haltungen liegen darauf, wem gehoeren sie
  4. Je Parzelle: Grundbuchauskunft -> Eigentuemer und Adressen
  5. Je Eigentuemer: Telefonverzeichnis -> Treffer mit Sicherheitsstufe
  ->  Vorschlagsliste zum Abhaken  ->  [ Dossiers erzeugen ]
```

Für 71 Haltungen sind das rund 20 Abfragen. Der Reihenfolge nach, nicht gleichzeitig, mit
Fortschritt und Abbruch.

## Was ein erzeugtes Dossier enthält

| Feld | Quelle |
|---|---|
| Name | `Liegenschaft Nr. <Nummer> <Nachname erster Eigentümer>` — Pascals bisherige Schreibweise |
| ParcelNumbers, Gemeinde (BFS + Name) | Parzellendienst |
| Address, HouseNumbers, PostalCode, Town | Grundbuchauskunft (Gebäudeadresse) |
| Owners (eine Zeile je Eigentümer, mit Telefon) | Grundbuch + Verzeichnis |
| HoldingIds | private Haltungen auf der Parzelle, die im Projekt existieren |

### Neue Felder am Dossier

Das Datenmodell kennt heute nur `Town` (Ort). Gemeinde und Ort sind nicht dasselbe, und für
die Abfrage wird die BFS-Nummer gebraucht. `DossierDefinition` bekommt deshalb additiv:

```csharp
public string Municipality { get; set; } = "";   // "Erstfeld"
public int? MunicipalityBfsNr { get; set; }      // 1206
```

`DossierDocument.CurrentSchemaVersion` geht von 2 auf 3. Die Umstellung ist für diese Felder
ein Nichts-Tun (leer bleibt leer); der bestehende Ablauf in `DossierDocumentMigration`
bleibt unverändert und wird nur um die neue Zielversion erweitert.

Leer bleiben Occupancy, Mail, ConstructionProcess, Remarks — keine Quelle kennt sie.
Haltungen von Abwasser Uri werden gezeigt, aber nicht vorausgewählt. Haltungen, die das
Projekt nicht kennt, erscheinen nur als Hinweis: ohne Projektdaten gäbe es in der
Leitungstabelle weder Zustand noch Kosten.

## Bausteine

### Application — rechnet, kennt kein Netz

Verträge (je eine Datei):

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

public interface IPhoneDirectoryLookup
{
    Task<IReadOnlyList<PhoneMatch>> FindAsync(
        string surname, string street, string houseNumber, string postalCode, string town,
        CancellationToken ct);
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
`PhoneMatch(ListedName, Street, HouseNumber, Town, Phone, PhoneMatchConfidence)`,
`NetworkHolding(Designation, Owner, LengthMeters, GeometryWkt)`.

Regeln:

- `DossierBatchProposalUseCase` — nimmt die Leser als Abhängigkeit, fügt die Funde je Parzelle
  zu einem `DossierProposal` zusammen, markiert Übersprungenes mit Grund.
- `DossierBatchCreationUseCase` — macht aus den bestätigten Vorschlägen
  `DossierDefinition`-Einträge und gibt sie zurück. Kein Dateizugriff; der Aufrufer speichert.
- `DossierNameBuilder` — pure Funktion für den Dossiernamen.
- `PhoneMatchConfidence` — `VornameStimmt` / `VornameWeichtAb` / `NurNachname`. Nur
  `VornameStimmt` wird vorausgewählt.

### Infrastructure — nur Netz, kennt kein Dossier

- `UriParcelWfsClient`
- `UriLandRegistryClient` — liest die HTML-Seite **ISO-8859-1**; erkennt `Lit.A:`/`Lit.B:`-Blöcke
  und `Eigentümer: Keine`
- `SearchChPhoneDirectoryClient` — sucht über **Nachname + Strasse + Ort**, nie über den vollen
  Grundbuchnamen (dieser findet nichts, gemessen)
- `UriSewerNetworkWfsClient`
- `GeoUrHttpGateway` — gemeinsames Zeitlimit, Abbruch, Aufrufe der Reihe nach

Jeder Leser: eine Datei, eine Quelle, schlichte Rückgabe.

### UI

`DossierBatchWindow` + ViewModel: Gemeindeauswahl, Fortschritt, Vorschlagsliste zum Abhaken,
Abbrechen. Enthält keine Regel.

## Fehlerpfade

| Fall | Verhalten |
|---|---|
| Kein Netz, Dienst nicht erreichbar | Klare Meldung, nichts erzeugt |
| Zeitüberschreitung einer Abfrage | Diese Parzelle wird als „nicht abgefragt" gemeldet, der Lauf geht weiter |
| Grundbuchseite anders aufgebaut als erwartet | Leser liefert nichts. Nie ein geratener Name |
| `Eigentümer: Keine` | Parzelle erscheint, ist aber nicht auswählbar |
| Parzelle hat schon ein Dossier | Erscheint als „hat schon ein Dossier", nicht auswählbar |
| Haltung auf der Parzelle fehlt im Projekt | Nur Hinweis, nicht vorausgewählt |
| Abbruch mitten im Lauf | Nichts erzeugt |
| Bestätigung | Alle Dossiers in einem Speichervorgang |

## Tests

Die Anwendungsfälle werden mit erfundenen Lesern geprüft — **ohne Internet**. Jeder Testfall
ist ein am 2026-08-23 real gemessener Fall:

| Test | Belegt durch |
|---|---|
| Zwei Miteigentümer ergeben zwei Zeilen | Pz. 439, Lit.A/Lit.B |
| `Eigentümer: Keine` erzeugt kein Dossier | Pz. 13 Erstfeld |
| Abweichender Vorname bleibt unangehakt | Johanna ↔ „Beispiel, Rita" |
| Voller Grundbuchname liefert keinen Telefontreffer | „Kurt Beispiel" → 0 Treffer |
| Nur Projekthaltungen werden vorausgewählt | 6 auf Pz. 439, 5 im Projekt, 1 dem Kanton |
| Bestehendes Dossier wird übersprungen | Pz. 439 hat eins |
| Abbruch erzeugt nichts | — |
| Dienstfehler meldet sich klar | — |
| Dossiername | „Liegenschaft Nr. 439 Beispiel" |

Dazu **ein** maschinengebundener Abnahmetest gegen die echten Dienste mit Parzelle 439 und
den oben gemessenen Werten, im Stil der bestehenden Live-Abnahmetests. **Kein neuer
übersprungener Test** — der Wächter über die sieben zulässigen Skip-Stellen würde rot.

## Risiken

- **Die Grundbuchauskunft ist eine Webseite, keine Schnittstelle.** Ändert der Kanton das
  Aussehen, liefert der Leser nichts mehr. Der Abnahmetest macht das sichtbar.
- **Personendaten.** Eigentümernamen und Telefonnummern landen in der Projektdatei. Beide
  Quellen sind öffentlich zugänglich und werden für ihren Zweck verwendet.
- **Namensgleichheit im Telefonverzeichnis.** Mehrere Parteien an derselben Adresse können zu
  einem falschen Treffer führen. Deshalb nur Vorschlag, nie automatische Übernahme.
- **Nur Uri.** Ein Projekt ausserhalb des Kantons kann diese Funktion nicht nutzen; das Fenster
  sagt das, statt leere Ergebnisse zu zeigen.

## Bewusst nicht enthalten

- Andere Kantone.
- Automatisches Nachführen bereits erzeugter Dossiers bei geänderten Grundbuchdaten.
- Objektbewohner, E-Mail, Bauvorgang, Bemerkungen — keine Quelle kennt sie.
- Eine eigene Geometrie-Bibliothek: alle räumlichen Fragen beantwortet der Dienst.
