# Objektakten, DSS-Export und Lieferungs-Editor

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Objektakten aus dem WebGIS-Plan (11.09.2026)
- DSS-Neuexport aus GeoShop-Objektakten (11.09.2026)
- Vollständige Objektakte in der Aufklappliste (11.09.2026)
- Nova-Anordnung analog WebGIS (11.09.2026)
- WebGIS-Namensschutz, Exportabdeckung und Unterhalt (12.09.2026)
- Vollständigkeit der Dropdown-Inhalte und Haltungspunktakten (12.09.2026)
- WebGIS-Einbauten und geerbte Schachtanzeigen (12.09.2026)
- Freier SIA405-Lieferungs-Editor (12.09.2026)
- WebGIS-Nachbau: bestehendes Nova weiterverwenden (12.09.2026)

## Objektakten aus dem WebGIS-Plan (11.09.2026)

- Domain: `FieldCatalog.Objektfelder` lädt `Objektakten.Katalog.json` als eingebettete Ressource.
  200 Quellfelddefinitionen, 204 Anzeigen, 91 Dropdownstellen, 78 Kataloge/1158 Einträge;
  Zusatzfelder ergänzen den Bestand, ohne FieldKeys oder die Tabellenexportfolge umzubenennen.
- `Project.Objektakten` enthält eigene `ObjektAkte`-Datensätze mit lokalen GUIDs, Bezuegen,
  Hauptdeckelwahl, Feldwerten, Quellbelegen und offenen Unterlisten. Originalcodes und lokale
  Auswahlindizes sind getrennt. JsonExtensionData schützt unbekannte Akten-/Wert-/Quellangaben.
- `ObjektaktenBearbeitung`, `ObjektaktenBestandsfelder`, `ObjektaktenSuche`, `ObjektaktenListen`,
  `GeoShopObjektaktenImport`, `GeoShopEigentuemerErgaenzung`, `ObjektaktenPaketImport` und
  `ObjektaktenExportBegleitung` liegen in Application/UseCases/Objektakten. Bestehende
  Fields/FieldMeta bleiben für bereits vorhandene Felder die fachliche Wahrheit.
- `GeoShopXtfLeser` liest Originalwerte und rückwärts gerichtete Deckel-/Punkt-/Ereignisbeziehungen
  in begrenzten Streaming-Durchläufen. Assoziationen ohne TID erhalten ausdrücklich lokale
  Belegkennungen. Organisationen aus beiden SIA405-Basismodellen und die ausdrückliche
  Ereignis-Firmenbeziehung werden gelesen; Hersteller und Operateur bleiben getrennt.
  Nur explizit gewählte Eigentümer-JSON wird über Originalreferenzen verwendet.
- `IObjektaktenPaketService` / `ObjektaktenPaketService` schreiben neue JSON-Dateien und prüfen
  sie durch Wiedereinlesen. Der Service ist über ServiceProvider.Objektakten registriert.
  Eigene Zusatzdateien ergänzen nur dasselbe Projekt; der GEONIS-Importvertrag bleibt offen.
- `ObjektakteViewModel`/`ObjektFeldViewModel` und `ObjektakteWindow` verwenden Nova-Ressourcen.
  Die gemeinsame Anbindung `ObjektaktenDialog.Befehl` hält beide Seiten-ViewModels dünn.
  Persönliche Sichtbarkeit/Favoriten/Gruppen stehen in AppSettings, nicht in den Fachdaten.
- `JsonProjectRepository` unterstützt Format 3. Format 1 wird weiterhin auf 2 gehoben;
  Format 2 bleibt ohne Objektakten unverändert. Neue Akten erfordern Format 3 zum Schutz
  gegen ältere Programmversionen. Projektkopie, Signatur und Backup erfassen die Akten mit.
- Die XTF-Vorschau nennt offene Zusatzangaben; neue Aktenobjekte werden nicht ungeprüft in
  ein Normmodell geschrieben. Bestätigte Altwege bleiben erhalten, Zusatzdaten gehen in JSON.
  Keine neue NuGet-Abhängigkeit. Anleitung und Abnahmestand: docs/OBJEKTAKTEN.md.


## DSS-Neuexport aus GeoShop-Objektakten (11.09.2026)

- `DssExportPlanBuilder` in Application/Xtf/Dss ergänzt den vollständigen Neu-Weg von
  `XtfNeuExportService`. Bei Objektakten mit DSS-Belegen/Zusatzwerten wird DSS statt des
  kleineren SIA405-Modells verwendet. Reine Änderungsaufträge bleiben auf dem bisherigen Weg.
- Der reproduzierbar erzeugte `Dss.ExportSchema.json` enthält 368 Attribute für 23 Klassen
  aus den eingebetteten offiziellen ILI-Modellen (DSS/Base 18.10.2023). `DssExportSchema`
  prüft Normtexte, Datum, Präzision, Bereiche und Längen; keine lokalen Dropdown-Indizes als Codes.
- Kleine Plan-/Bearbeitungshelfer trennen Quellverbund, Feldzuordnung, Profile, Koordinaten
  und Richtungsumkehr. Original-TIDs und optionale Rohangaben bleiben erhalten; aktuelle
  Fields/FieldMeta und Aktenwerte gewinnen. Pflichtlücken, Konflikte und fehlende interne
  Bezugsobjekte sperren die ganze DSS-Lieferung. Externe Organisations-TIDs werden ausgewiesen.
- Gemeinsame Originalprofile werden bei Änderungen nicht überschrieben. Firmenverweise
  werden am Unterhalt eingebettet, Bauwerks-Ereignis-Beziehungen ohne künstliche TID geschrieben.
- `ObjektQuellbeleg.Strukturen` speichert ab diesem Stand vollständige XML-Geometrien additiv
  in Format 3. GeoShopXtfLeser, Kopie/Vergleich und Paketprüfung berücksichtigen sie. Ältere
  Projekte benötigen einen erneuten Abgleich, um damals nicht gespeicherte Geometrien zu ergänzen.
- `XtfDssWriter` schreibt modellgerechte Reihenfolge und passende ILI-Dateien. Der XTF-Kopf
  enthält den Hinweisbericht. Vorhandene Dateien werden nicht überschrieben. UI-Texte benennen
  den vollständigen Normexport; Nova-Ressourcen und Bestands-Änderungsmodus bleiben erhalten.
- Keine neuen Pakete oder ServiceProvider-Registrierungen. Anleitung: docs/DSS-XTF-EXPORT.md.
  Normprüfung mit ilivalidator ist kein Nachweis eines echten GEONIS-/FME-Rückimports.


## Vollständige Objektakte in der Aufklappliste (11.09.2026)

- `ObjektakteView` ist die gemeinsame Nova-Maske für das optionale `ObjektakteWindow`
  und die offene Haltungs-/Schachtzeile. `Alle Angaben` ist dort vorausgewählt;
  `Kurzansicht` erhält die alten RecordDetails-Gruppen und deren persönliche Anordnung.
- Beide Aufklappcontroller verbinden über `ObjektaktenDialog.Fabrik` dieselbe Bearbeitung
  und dieselben Speicher-/Projektguards wie die Objektakte. Zusätzliche Aktenänderungen
  rufen auch die vorhandene automatische Speicherung der jeweiligen Seite auf.
- `ObjektaktenInlineBindung` hält nur das Modell der offenen Zeile. Ein Nachziehen derselben
  Zeile erhält Suche und Objektauswahl. Zuklappen, Löschen, Projektwechsel und Dispose geben
  das Modell frei. Die letzte TextBox-Eingabe wird vor dem Wechsel zurückgeschrieben.
- Die volle Maske entsteht nur im aufgeklappten ContentTemplate. Bei Registerwechsel wird
  über die bestehende Bearbeitung neu gelesen; Fields/FieldMeta bleiben die Wahrheit.
  Der bisherige Live-Abgleich der Kurzansicht bleibt erhalten. Keine neue Fachlogik im View.
- Die Objektliste wird vor der Auswahl eines neuen Deckels/einer Sanierung aktualisiert.
  Eine vorübergehend leere WPF-Themenauswahl beim Listenwechsel verwirft nicht das aktive Thema.
- Nachweis: `ObjektakteAufklappTests` nutzt echte Seiten-ViewModels und beide Controller in
  einem isolierten WPF-Prozess. Er prüft Inline-Start, letzte Eingabe, neue Deckel,
  Registerwechsel, schmale Darstellung sowie Sperren nach Löschen/Projektwechsel.


## Nova-Anordnung analog WebGIS (11.09.2026)

- `ObjektaktenWebGisBereiche.json` enthält die ursprünglichen Quellbereiche aller 200
  Planfelder (`sourceOccurrences[0].section` aus dem Feldkatalog vom 10.09.2026).
  `ObjektaktenWebGisLayout` ordnet Kopfangaben, Daten I/II, Bauwerksteile, Haltungspunkte,
  Stammkarte, Administrativ, Unterhalt, Hydraulik und Metadaten für die Ansicht zu.
  Alle 200 sichtbaren Feldbezeichnungen wurden mit der Planquelle verglichen: identisch.
- `ObjektakteView` zeigt Kopf und Bereichszeilen untereinander, ohne Themen-Seitenleiste
  und ohne Einzelkarten. Die Flächen/Schrift/Schaltflächen bleiben Nova. Pro Feld gibt es
  genau eine sichtbare Eingabe; editierbare ComboBoxen erhalten Auswahlcodes und Freitext.
- `ObjektWebGisAbschnitt` hält den Aufklappzustand unter neuen `webgis.<Art>.<Bereich>`-
  Schlüsseln in bestehenden AppSettings. Standard zu, Suche öffnet Treffer. Aktualisierte
  verknüpfte Listen ersetzen nicht die Feldeditoren oder deren Fokus.
- Seltene Aktionen und persönliche Anzeigeoptionen liegen unter Mehr. Die bestehenden
  fachlichen Gruppen-APIs bleiben erhalten; Speicher-/Import-/Exportzuordnungen unverändert.
- `ObjektaktenWebGisLayoutTests` prüft vollständige, duplikatfreie Feldabdeckung aller vier
  Objektarten sowie Bereichsfolge, Kopf und Aufklappzustand. Der WPF-Test ergänzt echte
  Dropdownauswahl mit Originalcode, freie Sonderwerte und nur einen sichtbaren Editor.
- Kompakt (11.09. abends, Entscheid Pascal «ich scrolle viel zu viel»): Eingaben 28 px,
  Zeile ~30 px, Hinweis als Info-Symbol mit Tooltip; Spalten nach Breite ueber die EINE Regel
  `ObjektakteView.SpaltenFuerBreite` (700/1100/1500 -> 1/2/3/4) fuer Liste und Fenster;
  der Kasten in der Aufklappliste nimmt ueber `FormularHoeheConverter` die Listenhoehe minus
  150 px (mindestens 360) statt fest 620 — ein Scrollbalken statt zwei. Nie wieder eine
  zweite Spaltenregel im Fenster-Code-behind.
- **Abhaengige Auswahllisten ziehen nach (12.09., Entscheid Pascal «das moechte ich so»):**
  Materialgruppe -> Materialdetail, Bauwerksteil Art -> Subart, Unterhalt Art -> Verfahren.
  `ObjektaktenBearbeitung.ZieheAbhaengigeFelderNach` laeuft am Ende von `Schreibe` — wechselt die
  Gruppe, springt das Detail auf den **ersten Eintrag der neuen Liste** wie im WebGIS, statt
  sichtbar falsch stehen zu bleiben. Drei Ausnahmen: ein **leeres** Feld bleibt leer (ein
  Gruppenwechsel erfindet keinen Wert — im WebGIS ist das Feld Pflicht, bei uns heisst leer
  «nicht erfasst»), ein Wert der auch zur neuen Gruppe gehoert bleibt, und eine leere Kindliste
  laesst den alten Wert stehen. Nachgezogen wird NUR ueber die Maske: Importe schreiben direkt
  in `ObjektAkte.Werte` und duerfen sich nicht gegenseitig ueberschreiben. Der nachgezogene Wert
  gilt als Handwert und geht damit in die XTF-Aenderungslieferung. Das UI zieht ueber
  `ObjektFeldViewModel.LiesWertNeu` mit; nie den `Text`-Setter dafuer verwenden — der schreibt.
- Feld markieren (11.09. spaet): Rechtsklick -> Farbe (5 Theme-Tokens `Markierung<Farbe>Brush`
  in Theme.xaml UND ThemeLight.xaml), programmweit je Feld in `AppSettings.ObjektakteFarben`,
  nie im Projekt. Der Speicherstatus der Haltungsseite ist eine Einblendung UEBER der Liste
  (Row 2, ZIndex), nicht mehr eine Zeile im Kopf - sonst rutscht beim Speichern die ganze
  Liste (Entscheid Pascal «das Bild verschiebt sich»). Keine Statuszeile mehr in den Kopfbereich.

## WebGIS-Namensschutz, Exportabdeckung und Unterhalt (12.09.2026)

- `GeoShopZiel.ProjektnameEindeutig` prüft bei projektgebundenen Zielen den gesamten
  Bestand derselben Objektart. Einzel- und Gesamtabgleich nutzen dieselbe Sperre;
  `GeoShopAbgleichAnwender` prüft sie vor dem Schreiben erneut, auch nach Umbenennung
  anderer Projektzeilen. Bestehende Fassaden und Namen anderer Objektarten bleiben erhalten.
- `DssObjektaktenAbdeckung` und `DssObjektarten` in Application/Xtf/Dss sperren nicht
  lieferbare oder verwaiste Akten mit vollständiger Namens-/Feldliste. Der Neuexport
  verarbeitet Haltung, Schacht, Deckel, Sanierung und allgemeinen Unterhalt. Fehlende
  Einzelangaben und Quellobjekte nennt der Bericht; `XtfExportVorschau.AusBericht`
  zeigt deren Umfang vor dem Schreiben. Weitere Objektarten sind noch nicht angebunden.
- `GeoShopObjektaktenImport` übernimmt auch nicht sanierende Unterhaltsereignisse als
  `unterhalt`. Original-TID, Firmenbelege, Handeingaben und mehrere Bauwerksbezüge
  bleiben erhalten. Wiederimport legt keine zweite Ereignisakte an; abgeleitete
  Unterhaltslisten unterdrücken die entsprechende rohe Doppelzeile.
- `DssFeldZuordnung` verbindet zwölf allgemeine Unterhaltsattribute. Eindeutige
  Normtexte erscheinen beim Import als WebGIS-Auswahltext. Auftrag/Nummer, nicht
  modellierte Status- und Ereignisarten erhalten keine erfundene Zuordnung.
- `DssMaterialZuordnung` erfasst alle 68 Materialdetails: 27 geprüfte Normziele,
  41 ausdrücklich offene fachliche Entscheidungen. UI-Hinweis und DSS-Export verwenden
  denselben Vertrag. Offene Werte bleiben im Projekt und sperren den Normexport.
- `ObjektFeldDefinition` liest die bestehenden WebGIS-Metadaten. `ObjektFeldPruefung`
  prüft belegte Pflicht-/Längen-/Zahl-/Datumsregeln vor dem Schreiben. Noch kein
  vollständiger Entwurfs-/Verwerfen-Ablauf und keine vollständige Subtypsteuerung.
- `ObjektaktenWebGisBereiche.json` ergänzt 260 eindeutige Bereichszuordnungen anhand
  WEBGIS-LAYOUT-KOMPLETT.md. Listen verwenden ihren belegten `WebgisAbschnitt`,
  insbesondere Einbauten. `ObjektListenAnzeige` blättert mit sechs Zeilen. Einzelnes
  Aufklappen schliesst andere Bereiche; Suche und ausdrückliches Alle-auf bleiben möglich.
- Die Oberfläche enthält 661 Katalogfelder für 16 Aktenarten. Vollständige
  Feldsichtbarkeit ist getestet; dies ist keine Abnahme aller WebGIS-Funktionen.
- `tools/PruefeWebGisLieferung.py` zählt Original-XTF nur lesend. Die aktuelle
  order-Lieferung enthält 630246 Objekte/Beziehungen, 22 Klassen und fünf doppelte
  Haltungs-TIDs mit unterschiedlichen Inhalten. Kein pauschaler Kennungsersatz.
- Keine neuen NuGet-Pakete oder ServiceProvider-Registrierungen, Projektformat 3 bleibt
  kompatibel. Stand, Nachweise und Restumfang: docs/reviews/2026-09-12-webgis/UMSETZUNGSSTAND.md.

## Vollständigkeit der Dropdown-Inhalte und Haltungspunktakten (12.09.2026)

- `Objektakten.Katalog.json` enthält 235 Dropdownfelder und 148 Kataloge mit 1924
  Einträgen, einschliesslich Wiederholungen nach Elternwert. Alle 269 ausgezählten
  Dropdown-Vorkommen aus WEBGIS-LAYOUT-KOMPLETT.md sind mit Code, Text, Gruppe und
  Reihenfolge enthalten. Fünf Quellvorkommen sind nicht ausgezählt und bleiben im
  Nachweis offen; die aktuelle Ortsliste wurde direkt als leer beobachtet.
- `sanierung.s_procedure` folgt `sanierung.s_art` über
  `sanierung.verfahren-je-eltern`: 37 Verfahren plus bisherige Leerwahl. Die Codes
  von 20 Deckel-/Sanierungskatalogen sind ergänzt, alte Indizes und Texte bleiben.
  `ObjektFeldViewModel.Auswahl` erkennt gespeicherte Alteinträge ohne Code über
  Position und Text. Gleichnamige Fabrikate mit Codes 14/15 werden nicht vereinigt.
- `unterhalt.witterung` enthält neun Werte, die volle Liste gilt für Art 5/8/9/11.
  Andere Arten haben keine belegte Witterungsliste. Bestehende Gruppenwechselregeln
  erhalten leere Felder und Werte bei leeren Unterlisten; kein erfundenes Normziel.
- `GeoShopHaltungspunktImport` ist ein kleiner Application-Service ohne neue
  Registrierung. Er ergänzt vorhandene Quellpunkte als Akten und erhält TIDs,
  gemeinsame Bezüge und Handeingaben. `HatNeueQuellen` erkennt auch fehlende Akten
  in bereits abgeglichenen Projekten. Öffnen bleibt lesend; der Abgleich ergänzt.
- Die Punktmaske enthält 15 Felder und fünf Dropdowns mit Leerwahl; Zugriff über
  die bestehende Objektauswahl. Vollständige Dokument-/Geometriefunktionen bleiben
  offen. `DssObjektarten` und `DssExportBearbeitung` verarbeiten bestehende Punkte;
  keine neue Punkt-TID oder Netzverbindung wird aus einer solchen Akte erfunden.
- `DssFeldZuordnung` übersetzt die Höhengenauigkeit des Punkts ausdrücklich und
  `KatalogAnzeige` zeigt eindeutige importierte Normwerte als Auswahltexte. Nicht
  vorhandene Normattribute werden wie bisher mit Namen/Wert im Exportbericht genannt.
- `PruefeWebGisDropdowns.py` ist ein lesender Inhaltsabgleich mit Quellzeilen und
  Prüfsummen, kein Beweis aller UI-Feldbindungen oder dynamischen Objektlisten.
  Nachweis: docs/reviews/2026-09-12-webgis/DROPDOWN-ABGLEICH.md. Regressionen:
  `ObjektaktenDropdownVollstaendigkeitTests` und `XtfHaltungspunktAkteTests`.
- Keine neuen Pakete, keine Änderungen an Kundenoriginalen/WebGIS-Daten, Format 3
  bleibt erhalten. Persönliche Listenergänzungen werden weiterhin berücksichtigt.

## WebGIS-Einbauten und geerbte Schachtanzeigen (12.09.2026)

- `DssEinbautenZuordnung` in Application/Xtf/Dss verbindet die sechs Originalklassen
  FoerderAggregat, Absperr_Drosselorgan, Streichwehr, Leapingwehr, Einstiegshilfe und
  Trockenwetterfallrohr mit vorhandenen Masken, Sachfeldern und Elternrollen. Der
  aus lokalen ILI-Dateien erzeugte Schreibvertrag umfasste bei der Einbauten-Erweiterung 19 Klassen / 323
  Attribute. Einbauten ohne Originalbezug sowie unzulässige Klassenwechsel werden
  gesperrt; fehlende Zielfelder bleiben mit Namen und Wert im Bericht sichtbar.
- `GeoShopXtfLeser` verfolgt zusätzliche rückwärts gerichtete Einbaubeziehungen an
  angefragten Schächten und Anschlussknoten der Haltung. Er nimmt nur den Verbund
  auf; Originalgeometrien und TIDs bleiben erhalten. Netzknoten ohne Bauwerksref
  sind über den Haltungsverbund möglich. Der bisherige Schachtabgleich verlangt
  weiterhin einen Bauwerksbezug; der freie Lieferungs-Editor umgeht diese Grenze.
- `GeoShopEinbautenImport` liegt unter Application/UseCases/Objektakten. Er ergänzt
  deterministische Akten, aktuelle Originalbelege und gemeinsame Projektbezüge,
  schützt Handwerte einschliesslich Leeren und vermeidet doppelte Akten. Fehlende
  Einbauakten lösen auch bei vorhandenen Quellen einen erneuten Abgleich aus.
- `DssExportBearbeitung` bearbeitet den Original-Einbau entsprechend seiner echten
  Klasse. `DssExportPruefung` prüft neue Rollen/Zielklassen und gemeinsame Namensräume
  von Ueberlauf und BauwerksTeil. Numeric-Codes werden nicht zu Normwerten; die
  konkrete Schreibweise Senden, empfangen und Wehr-Art wird ausdrücklich übersetzt.
- `ObjektFeldDefinition.ErbtVon` liest die bereits vorhandene Katalogmetainformation.
  `ObjektaktenSchachtVererbung` liest schreibgeschützte Einbauanzeigen am eindeutig
  zugehörigen Schacht oder belegten Quellknoten. Aktuelle Projektwerte und manuelles
  Leeren gehen vor. Eigene Pumpen-/Wehrbemerkungen werden nicht mit geerbten Anzeigen
  verwechselt. `ObjektaktenListen` unterdrückt rohe Bauwerksteil-Doppelzeilen.
- Prüfungen: `XtfEinbautenTests`, `XtfEinbautenDropdownTests` (28 Dropdownfälle mit
  allen angebotenen Werten), `ObjektaktenEinbautenTests`; synthetische 18-Objekt-XTF
  mit allen skalaren Einbauattributen besteht ilivalidator --allObjectsAccessible.
  Echte Lieferstichprobe, bestehende Importgrenzen und Restumfang stehen in
  docs/reviews/2026-09-12-webgis/EINBAUTEN-ABNAHME.md. Die vier damals noch fehlenden
  Lieferklassen sind inzwischen im freien Lieferungs-Editor zugänglich. Keine vollständige WebGIS-Abnahme.
- Keine neue Registrierung oder NuGet-Abhängigkeit. Gespeicherte Projektformate
  bleiben kompatibel; Kundenoriginale und WebGIS-Daten werden nicht verändert.

## Freier SIA405-Lieferungs-Editor (12.09.2026)

- `IXtfLieferungsAblage` und die Ergebnis-/Felddatensätze liegen unter
  Application/Xtf/Lieferung. `XtfLieferungsNorm` nutzt den bestehenden DSS-Vertrag
  für Pflichtfelder, Originalkennungen, Rollen/Zielklassen und gemeinsame Namensräume.
  Textausrichtung und Plantyp sowie ARABauwerk und Messstelle erweitern den aus lokalen
  ILI-Dateien erzeugten Vertrag auf 23 Klassen / 368 Attribute.
- `XtfLieferungsAblage`, `XtfLieferungsImport`, `XtfLieferungsDatenbank`,
  `XtfLieferungsXml` und `XtfLieferungsAusgabe` liegen unter
  Infrastructure/Import/Xtf/Lieferung. Die getrennte SQLite-Arbeitsdatei `.ssxtf`
  hat Formatversion 1 und application_id 1397971028. Sie enthält Original-XML,
  optionales aktuelles XML, Korb, Original-TID, lokale Zeilen-ID und Änderungsversion.
  Sie verändert kein Projektformat. Pooling ist aus; Import/Export veröffentlichen
  erst nach Abschluss ihre selbst erzeugte temporäre Datei ohne Überschreiben.
- Alle Originalobjekte bleiben erhalten, auch ohne Projektzeile, Namen oder
  Bauwerksbezug am Netzknoten. TID-Dubletten sind getrennte Zeilen und sperren die
  Ausgabe. Speicherung prüft die geladene Version. Externe Organisationsverweise
  werden ausgewiesen; fehlende interne Bezugsobjekte werden nicht erfunden.
- `XtfLieferungViewModel`/`XtfLieferungWindow` öffnen, suchen (100 Zeilen pro Seite),
  speichern/verwerfen und prüfen die Lieferung. Die volle Normauswahl stammt aus
  `DssExportSchema`; unbekannte Altwerte bleiben sichtbar. Punktkoordinaten sind
  bearbeitbar, Linien-/Flächengeometrien bleiben erhalten. Originalklasse/TID sind
  fest. Nicht zugeordnete Zusatzfelder sperren einen verlustbehafteten Export.
- `ServiceProvider.GeoShop` und `ServiceProviderRegistrationMap` registrieren
  `IXtfLieferungsAblage`. ExportPage öffnet über `XtfLieferungDialog` das eigene Fenster.
  Prüfberichte liegen vollständig in der Arbeitsdatei; die UI zeigt nur eine Vorschau
  und kann den vollen Bericht speichern. Änderungen entwerten den alten Prüfbericht.
- Nachweis: `XtfLieferungsAblageTests`, `XtfLieferungUiTests` mit echtem WPF-Kindtest.
  Die echte order-Datei: 630246 Objekte/Beziehungen, 22 Klassen, 99795 Objekte mit
  gemeldeten Erstfehlern, 26 externe Organisations-TIDs. Alle Klassen wurden geöffnet.
  Eine synthetische 29-Objekt-Datei besteht ilivalidator --allObjectsAccessible.
  Anleitung/Grenzen: docs/LIEFERUNGS-EDITOR.md; Zahlen: lieferung-order-abnahme.json
  unter docs/reviews/2026-09-12-webgis. Keine vollständige WebGIS-/GEONIS-Abnahme.
- Keine neuen Pakete, keine Original-/WebGIS-Änderungen. Neuobjekte, Klassenwechsel,
  Dublettenbereinigung, grafische Geometriearbeit und weitere WebGIS-Funktionen fehlen
  im freien Editor noch. Der projektbezogene Schachtabgleich bleibt separat.

## WebGIS-Nachbau: bestehendes Nova weiterverwenden (12.09.2026)

- Fachlicher Massstab bleiben die WebGIS-Felder, Funktionen und vollständigen
  Dropdown-Inhalte. Die Darstellung folgt ausdrücklich dem vorhandenen Nova-Stil.
  Vorhandene Masken, Kataloge und Abläufe zuerst abgleichen und weiterverwenden.
- `XtfLieferungWindow` nutzt `NovaPageHeader`, `Card`, `ToolbarButton` und
  `ToolbarButtonAccent` aus den vorhandenen Ressourcen. Farbangaben, Feldbeschriftungen
  und der Entwurfsstatus folgen dem Themenwechsel; alle bestehenden Bindungen bleiben.
  Die Exportseite platziert den Einstieg im Aktionen-Bereich ihres Nova-Seitenkopfs.
- `XtfLieferungUiTests` prüft den bestehenden Ablauf weiterhin im echten Fenster und
  zusätzlich den Themenwechsel bei 1240 und 900 Pixel Fensterbreite. Kein neuer
  Daten-/Exportdienst, keine neuen Kataloge und keine Änderungen an Bestandsmasken.
