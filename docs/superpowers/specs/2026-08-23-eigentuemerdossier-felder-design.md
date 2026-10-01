# Eigentümerdossier — alle sichtbaren Felder ausfüllbar

**Datum:** 2026-08-23
**Vorbild:** `C:\Users\Besitzer\Documents\Eigentümerdossier_Pz.170.pdf` (5 Seiten, Abwasser Uri,
Sanierung Private Abwasserleitungen Erstfeld West, Parzelle 170 / Talweg 3)
**Vorgänger:** `docs/superpowers/specs/2026-08-22-eigentuemerdossier-design.md`

## Ziel

Pascal füllt **alles** im Programm aus. Danach erzeugt ein Knopf die Word-Datei, ein zweiter das
Gesamt-PDF mit den Beilagen. In der Word-Datei muss nichts mehr von Hand eingetippt werden.
Das Aussehen bleibt das seines Originals.

## Ausgangslage

Der Dossier-Bereich existiert seit 2026-08-22: Datenmodell, zweistufige Eingabe
(Gebiet → Dossier), Word-Vorlage aus `DossierWordTemplateBuilder`, Platzhalterfüllung über
`DocxPlaceholderFiller`, PDF-Zusammenbau über `DossierPdfAssemblyService`.

Vergleich der erzeugten Vorlage mit dem Original zeigt zwei Arten von Lücken:

**A — im Dokument sichtbar, im Programm nicht ausfüllbar:**

| Stelle | heute |
|---|---|
| Logo links, Wappen rechts auf dem Deckblatt | nur Hinweistext `[Logo hier einfügen]`; `DossierAreaSettings.LogoPath` hat kein Eingabefeld und wird nie ins Word eingebaut |
| Kapitel 1 Übersichtsplan | nur Hinweistext, Bild von Hand in Word |
| Eigentumsverhältnisse | genau **eine** Zeile möglich |
| Autoren | nimmt stumm `Environment.UserName` — auf diesem Rechner „Besitzer", nicht „Pascal Aschwanden" |

**B — Aufbau weicht vom Original ab:**

| | Original | erzeugte Vorlage heute |
|---|---|---|
| Kapitel | 3 | 5 |
| Rückmeldung / Einverständnis | letzte **Zeile** der Info-Tabelle, Unterschriftslinien in der Zelle | eigenes Kapitel 5 |
| Leitungstabelle | nicht vorhanden | Kapitel 3 |

## Entscheidungen

1. **Aufbau:** Original-Aufbau, **plus** die Leitungstabelle als eigenes Kapitel 3. Sie ist der
   Mehrwert, den nur dieses Programm liefert — die Zahlen kommen aus der Auswertung.
2. **Logo und Wappen:** werden aus dem Original-PDF gezogen und **fest mitgeliefert**. Kein
   Eingabefeld, kein Auswählen — sie sind in jedem Dossier gleich.
3. **Übersichtsplan:** Bilddatei je Dossier, vom Benutzer gewählt.
4. **Mehrere Zeilen:** nur bei den Eigentumsverhältnissen. Das Änderungswesen bleibt wie im
   Original: Zeile A automatisch, zwei Leerzeilen zum Handeintragen.
5. **Ausgabe:** wie heute zwei Knöpfe — Word erzeugen, daraus PDF mit Beilagen. Word bleibt als
   Sicherheitsnetz für den seltenen Fall einer Handkorrektur.
6. **Kein Rich-Text:** Zeilenumbrüche bleiben erhalten, aber keine roten Wörter und keine
   Aufzählungspunkte. Im Original war Rot Pascals Arbeitsmarkierung („noch offen"), keine
   Gestaltung für den Empfänger.

## Zielaufbau des erzeugten Dokuments

```
Deckblatt   [Logo]                              [Wappen]
            {{Gebietstitel}}
            ┌ Eigentümerdossier ┐
            {{Parzellen_Zeile}} / {{Adresse_Zeile}} / {{Eigentuemer_Block}}
            Datum: {{Datum}}                    Revision: {{Revision}}

Seite 2     Änderungswesen (A | {{Datum}} | | Ersterstellung, + 2 Leerzeilen)
            Erstellungsdatum: {{Datum_Lang}}
            Autoren: {{Autoren}}
            Inhaltsverzeichnis (4 Zeilen)

1.          Übersichtsplan Werkleitungen        {{@Uebersichtsplan}}
2.          Eigentumsverhältnisse               {{#Eigentuemer}} — beliebig viele Zeilen
3.          Betroffene Abwasserleitungen        {{#Haltungen}} — aus der Auswertung
4.          Informationen Sanierung             eine Tabelle, letzte Zeile
                                                „Rückmeldung / Einverständnis Eigentümer"
                                                mit Unterschriftslinien in der Zelle

Fusszeile   {{Fusszeile}}                       Seite X von Y
```

## Bausteine

### 1. Bild-Platzhalter — `DocxImagePlaceholderFiller` (neu, Infrastructure)

`DocxPlaceholderFiller` kann nur Text. Daneben kommt ein zweiter, kleiner Baustein mit einer
Aufgabe: einen Platzhalter `{{@Name}}` durch ein echtes Bild ersetzen.

- Eingabe: geöffnetes `WordprocessingDocument`, Zuordnung Platzhaltername → Bildpfad, je
  Platzhalter eine Höchstbreite in Zentimetern.
- Ablauf: Bilddatei lesen, `ImagePart` anlegen, Seitenverhältnis aus den echten Bildmassen
  berechnen, Text-Run durch ein `Drawing` ersetzen.
- **Fehlt eine Bilddatei oder ist sie unlesbar, bleibt die Stelle leer** — nie darf ein
  `{{@...}}` im fertigen Dokument stehen bleiben.
- Er läuft **vor** dem Textfüller, damit ein nicht ersetzter Bildplatzhalter vom Textfüller nicht
  fälschlich als Textplatzhalter behandelt wird.

Drei Verwendungen mit fester Breite: `Logo` (4,5 cm), `Wappen` (2,0 cm), `Uebersichtsplan`
(15,0 cm, seitenfüllend).

### 2. Feste Bilddateien

Einmalig aus Seite 1 des Original-PDFs gezogen (dort liegen genau zwei Bilder: 716×297 Logo,
177×213 Wappen) und als Datei abgelegt:

```
Export_Vorlage/Dossier_Logo.png
Export_Vorlage/Dossier_Wappen.jpg
```

Beide werden wie `Eigentuemerdossier.docx` über `AuswertungPro.Next.UI.csproj` mit
`PreserveNewest` ins Ausgabeverzeichnis kopiert. Der Export löst sie relativ zu
`AppContext.BaseDirectory` auf — derselbe Weg wie die Word-Vorlage.

### 3. Datenmodell (`DossierModels.cs`)

Neu:

```csharp
public sealed class DossierOwnerRow
{
    public string HouseNumber { get; set; } = "";
    public string ParcelNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Mail { get; set; } = "";
    public string Occupancy { get; set; } = "";
}
```

- `DossierDefinition.Owners` : `List<DossierOwnerRow>`
- `DossierDefinition.OverviewPlanPath` : `string`
- `DossierAreaSettings.Authors` : `string`

**Umstellung bestehender Dateien.** `DossierDocument.SchemaVersion` geht von 1 auf 2.
`DossierFileStore` weist heute schon jede Version über der eigenen ab — das bleibt so und ist
gewollt: eine ältere Programmversion überschreibt eine neuere Datei nicht.

Beim Laden einer Version-1-Datei wandern die bisherigen Einzelfelder (`HouseNumbers`,
`ParcelNumbers`, `OwnerName`, `ContactPhone`, `ContactMail`, `Occupancy`) in **eine** Zeile der
neuen Liste. Ist dort nichts gesetzt, entsteht keine Leerzeile. Die alten Felder bleiben im
Modell bestehen — Deckblatt und Dateiname verwenden sie weiterhin, und ein Feld zu entfernen
würde Altdaten wegwerfen.

### 4. Werte-Aufbau (`DossierWordTemplateExportService`)

- `BuildValues` erhält `Autoren` (Gebietsangabe; ist sie leer, weiterhin `Environment.UserName`
  als Rückfall, damit die Zeile nie leer bleibt).
- Neu `BuildOwnerRows(DossierDefinition)` → Wiederholzeilen `{{#Eigentuemer}}` mit den Spalten
  `Haus_Nr`, `Pz_Nr` und einem mehrzeiligen Block aus Name / `Tel.:` / `Mail:` /
  `Objektbewohner:` — genau die Zellenaufteilung des Originals.
- Ist die Liste leer, greift derselbe Weg wie bei den Haltungen: eine Zeile mit klarem Hinweis
  statt einer stehengebliebenen Platzhalterzeile.
- `Eigentuemer_Block` auf dem Deckblatt listet alle Namen der Zeilen untereinander; gibt es keine
  Zeilen, gilt weiterhin `OwnerName`.

### 5. Vorlage (`DossierWordTemplateBuilder`)

Umbau auf den Zielaufbau oben. Betroffen: Deckblatt (Bildzeile statt Hinweistext),
Inhaltsverzeichnis (4 statt 5 Zeilen), Kapitel 1 (Bildplatzhalter statt Hinweistext),
Kapitel 2 (Wiederholzeile), Kapitel 4 (Rückmeldung als letzte Tabellenzeile). Kapitel 3
(Leitungstabelle) bleibt unverändert und rückt an die dritte Stelle.

Die Deckblatt-Bildzeile ist eine randlose Tabelle mit zwei Spalten: links `{{@Logo}}`,
rechts `{{@Wappen}}` rechtsbündig — die Anordnung des Originals.

### 6. Eingabemaske

`DossierEditWindow`: unter „Liegenschaft" eine Tabelle „Eigentumsverhältnisse" mit den sechs
Spalten, Knöpfen „+ Zeile" und „Zeile entfernen"; darunter „Übersichtsplan wählen" mit
Dateiname und kleiner Vorschau. `DossierAreaWindow`: ein Textfeld „Autoren".

Die bisherigen Einzelfelder für Eigentümer/Kontakt bleiben sichtbar — sie speisen weiterhin das
Deckblatt.

### 7. Tests

| Test | Sichert |
|---|---|
| Kein `{{` im fertigen Dokument (Text **und** Bild) | Die Falle vom 2026-08-22: Word zerlegt Platzhalter in mehrere Run-Elemente |
| Bild landet als `ImagePart` im Dokument und der Platzhaltertext ist weg | Bild-Einbettung |
| Fehlende Bilddatei → Stelle leer, Dokument sonst vollständig | Fail-closed ohne Absturz |
| Drei Eigentümerzeilen erscheinen als drei Tabellenzeilen | Wiederholzeile |
| Leere Eigentümerliste → Hinweiszeile, keine Platzhalterzeile | Randfall |
| Version-1-Datei laden → genau eine Eigentümerzeile mit den Altwerten | Umstellung |
| Version-1-Datei ohne Eigentümerangaben → keine Leerzeile | Umstellung |
| Version 3 in der Datei → Laden verweigert, nichts überschrieben | Bestehender Schutz bleibt |

## Bewusst nicht enthalten

- Roter Text und Aufzählungspunkte in den Eingabefeldern.
- Automatisch erzeugter Übersichtsplan aus der Programmkarte.
- Mehrzeiliges Änderungswesen und Visum-Feld.
- Seitenzahlen im Inhaltsverzeichnis. Word berechnet sie nur nach einem Feldupdate; ein von Hand
  eingetragener Wert wäre beim ersten Nachbearbeiten falsch. Die Zeilen bleiben ohne Zahl.

## Risiken

- **Bildrechte:** Logo und Wappen stammen aus einem Dokument von Abwasser Uri. Sie werden für
  dessen eigene Dossiers wiederverwendet — das ist der vorgesehene Zweck. Für ein Dossier
  ausserhalb dieses Arbeitgebers müssten die Dateien ersetzt werden.
- **Wappenwechsel:** Bei einer anderen Gemeinde als Erstfeld ist das Wappen falsch. Bewusste
  Entscheidung; die Datei lässt sich im Ordner `Export_Vorlage` austauschen.
- **Vorlagendatei:** Der Umbau erzeugt `Eigentuemerdossier.docx` neu. Eine von Hand angepasste
  Vorlage würde dabei überschrieben. Stand heute ist sie unverändert erzeugt.
