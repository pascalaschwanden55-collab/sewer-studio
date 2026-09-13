# Vergleich Haltung 525145–505377

Prüfung vom 11. September 2026. Grundlage sind die neun gelieferten WebGIS-Fotos, die tatsächliche GeoShop-XTF und das zuletzt gespeicherte SewerStudio-Projekt. Es wurden keine Kunden-, Projekt- oder Programmdateien geändert. Prüfdateien wurden separat erstellt.

## Ergebnis

Die drei Datenstände sind **nicht vollständig gleich**. Es gibt unterschiedliche Quellen und echte Übernahmelücken.

- **Länge:** WebGIS und XTF enthalten 11,44 m. SewerStudio enthält 10,55 m. Der gespeicherte Inspektionsbefund endet bei 10,549 m; gerundet sind das 10,55 m. Die Differenz beträgt 0,89 m.
- **Zustand:** WebGIS und XTF enthalten Z2. Im Projekt und in der laufenden Anwendung steht Z4. Das Projekt enthält eine Inspektion vom 01.09.2026; der Kanal in der XTF trägt das Änderungsdatum 21.11.2024. Unterschiedliche Erhebungsstände sind eine plausible Erklärung. Der Vergleich beweist nicht, dass Z4 fachlich falsch ist.
- **Endhöhe:** 505,910 m wird in der XTF geliefert, ist im gespeicherten Projekt aber noch leer. Ein erneuter Abgleich mit dem aktuellen Code würde sie ergänzen.
- **Profil:** WebGIS zeigt Kreisprofil und 250 mm Breite. Das verknüpfte XTF-Rohrprofil enthält ausdrücklich `unbekannt` und kein Höhen-Breiten-Verhältnis. Kreisprofil und Breite dürfen daraus nicht behauptet werden.
- **Einlauf:** Die Anschlussverbindung ist in der XTF enthalten. Die Distanz lässt sich mit 6,36 m aus der Geometrie bestätigen. Der aktuelle Haltungsabgleich nimmt diese Verbindung nicht mit.
- **Eigentümer:** Die XTF enthält nur einen Verweis. Die mitgelieferte JSON ordnet ihn „Abwasser Uri“ zu. Dies stimmt mit SewerStudio überein, aber nicht wörtlich mit dem WebGIS-Namen „AWU_von_privat“.
- **Bruttokosten und unbekannte Werte:** `0.00` sowie `unbekannt` sind geliefert. Der aktuelle Abgleich filtert diese Werte aus.

## So ist die Tabelle zu lesen

**Leer** bezeichnet ein vorhandenes, nicht ausgefülltes Feld. **Nicht geliefert** bedeutet: kein entsprechender Wert in den untersuchten XTF-Objekten. Ein Verweis ist eine technische Kennung, kein ausgeschriebener Name. Aus Geometrie oder einem Sammelwert berechnete Angaben sind ausdrücklich als **abgeleitet** bezeichnet.

Die Spalte SewerStudio stammt vollständig aus der zuletzt gespeicherten Projektdatei. Zusätzlich bestätigt die laufende Anwendung dieselbe Haltung, Z4 und Mischabwasser. Nicht gespeicherte Eingaben in anderen Feldern konnte die verfügbare Leseschnittstelle nicht vollständig prüfen. Die neue Feldanzeige wurde mit dem aktuellen Programmcode aus dem gespeicherten Projekt ausgelesen.

## Kopf

| Angabe | WebGIS-Foto | GeoShop-XTF | SewerStudio gespeichert | Einordnung |
|---|---|---|---|---|
| Bezeichnung | 525145-505377 | 525145-505377 | 525145-505377 | Gleich |
| OBJECTID | 454675 | Nicht als OBJECTID geliefert | Leer | XTF-TID ist eine andere Kennung |
| Bezeichnung alternativ | u-80480 | Nicht geliefert | Leer | Keine Importquelle in diesem Verbund |
| Bezeichnung historisch | 1729-5729 | Nicht geliefert | Leer | Keine Importquelle in diesem Verbund |
| Knoten von | Normschacht: 525145 | Knoten 525145 mit Normschacht-Verknüpfung | 525145 | Gleiches Objekt |
| Knoten bis | Normschacht: 505377 | Knoten 505377 mit Normschacht-Verknüpfung | 505377 | Gleiches Objekt |

## Daten I

| Angabe | WebGIS-Foto | GeoShop-XTF | SewerStudio gespeichert | Einordnung |
|---|---|---|---|---|
| Typ AA | PAA | In `PAA.Sammelkanal` enthalten | Eigenes Feld leer | Aus Funktion ableitbar, wird nicht getrennt befüllt |
| Nutzungsart | Mischabwasser | Mischabwasser | Mischabwasser | Gleich |
| Funktion hierarchisch | Sammelkanal | PAA.Sammelkanal | PAA.Sammelkanal | Fachlich gleich, andere Aufteilung |
| Funktion hydraulisch | Freispiegelleitung | Freispiegelleitung | Freispiegelleitung | Gleich |
| Status | In Betrieb | in_Betrieb | in_Betrieb | Gleich |
| Baujahr | Leer | Nicht geliefert am Kanal | Leer | Keine fehlende Übernahme; Schachtbaujahr nicht auf Haltung übertragen |
| Materialgruppe | Beton | In Beton_Normalbeton enthalten | Eigenes Feld leer | Ableitbar, nicht getrennt befüllt |
| Materialdetail | Normalbeton (NB) | Beton_Normalbeton | Normalbeton | Fachlich gleich |
| Profiltyp | Kreisprofil (K) | unbekannt | Leer; technische Zuordnung enthält unbekannt | Abweichung bereits WebGIS ↔ XTF |
| Breite | 250 mm | Nicht geliefert; kein Höhen-Breiten-Verhältnis | Leer | Nicht sicher aus DN ableitbar |
| Höhe / DN | 250 mm | 250 | 250 | Gleich |
| Rohrprofil | Unbekannt: unbekannt () | Referenz auf Profil mit Bezeichnung/Typ unbekannt | Sichtbare Referenz leer; technische Referenz gespeichert | Darstellung noch unvollständig |
| Ringsteifigkeit [kN/m²] | Leer | Nicht geliefert | Leer | Gleich leer |
| Anfangshöhe | Leer | Keine Kote an A454675 | Leer | Darf nicht erfunden werden |
| Endhöhe | 505.91 | Kote 505.910 an E454675 | Leer | Echte Übernahmelücke im gespeicherten Stand |
| -nr. | Leer | Nicht geliefert | Leer | Gleich leer |
| / null | 2 | Kein eindeutig zuordenbarer Wert | Leer | Bedeutung des WebGIS-Felds nicht geklärt |
| Länge geometrisch | 11.44 m | Aus Verlauf: 11.440143 m (abgeleitet) | Eigenes Feld leer | Geometrie bestätigt WebGIS |
| Länge effektiv | Leer | LaengeEffektiv 11.44 | 10.55 | XTF und WebGIS ordnen Länge unterschiedlich ein; Inspektionslänge separat erhalten |
| Gefälle | Leer | Anfangskote fehlt | Leer | Nicht zuverlässig berechenbar |
| Plangefälle | Leer | Nicht geliefert | Leer | Gleich leer |
| Auslaufform VP / NP | Beide leer | Nicht geliefert an den beiden Haltungspunkten | Beide leer | Gleich leer |
| Höhengenauigkeit VP / NP | Beide leer | Nicht geliefert an A454675/E454675 | Beide leer | Knotengenauigkeit ist nicht dieselbe Angabe |
| Lageanschluss Zifferblatt VP / NP | Beide leer | Nicht geliefert | Beide leer | Gleich leer |
| Ebene | Leer | Nicht geliefert | Leer | Gleich leer |
| Bemerkung | Leer | Keine Bemerkung an Haltung oder Kanal | Leer | Bei dieser Haltung geht kein Bemerkungstext verloren |

## Daten II

| Angabe | WebGIS-Foto | GeoShop-XTF | SewerStudio gespeichert | Einordnung |
|---|---|---|---|---|
| Lagebestimmung | Genau | genau | genau | Gleich |
| Lagegenauigkeit | Leer | Nicht geliefert | Leer | Gleich leer |
| Höhenbestimmung | Genau | Nicht geliefert als entsprechende Haltungsangabe | Leer | WebGIS-Wert fehlt in Lieferung |
| Höhengenauigkeit | Leer | Nicht als entsprechende Haltungsangabe geliefert | Leer | Genauigkeiten der Knoten nicht damit verwechseln |
| Kanal-Referenz | {2550AD76-E424-44EF-935D-0BDC989F70DA} | Andere Kennung: ch24gwkd2FabRvaW | WebGIS-Feld leer; XTF-Kanal-TID gespeichert | GUID und XTF-TID sind verschieden |
| VP-REF | 17f83156-064c-4727-86b4-1564529aeb75 | Andere Kennung: ch24gwkdcFPcB0IM | WebGIS-Feld leer; XTF-Punkt-TID gespeichert | Nicht gegeneinander ersetzen |
| NP-REF | 461a1c84-d4cd-4f0f-853c-d2a629b778f9 | Andere Kennung: ch24gwkdsMxuOqnt | WebGIS-Feld leer; XTF-Punkt-TID gespeichert | Nicht gegeneinander ersetzen |
| Reibungsbeiwert / Wandrauhigkeit | Beide leer | Nicht geliefert | Beide leer | Gleich leer |
| Innenschutz | Leer | Nicht geliefert | Leer | Gleich leer |
| Umhüllung | Unbekannt | Bettung_Umhuellung unbekannt | Leer | Import filtert unbekannt aus |
| Verbindungsart | Unbekannt | unbekannt | Leer | Import filtert unbekannt aus |
| Geplante Nutzungsart | Leer | Nicht geliefert | Leer | Gleich leer |
| Rohrlänge | Leer | Nicht geliefert | Leer | Gleich leer |
| Haltungslänge | 11.44 m | LaengeEffektiv 11.44 | 10.55 | Dieselbe Längenabweichung wie oben |
| Ringsteifigkeitsklasse / Rohrserie | Beide leer | Nicht geliefert | Beide leer | Gleich leer |

## Bauwerksteile und Haltungspunkte

| Angabe | WebGIS-Foto | GeoShop-XTF | SewerStudio gespeichert | Einordnung |
|---|---|---|---|---|
| Bauwerksteile | Tabelle leer | Keine entsprechenden Teilobjekte im vom Leser geladenen Verbund | Keine Objektakten | Kein Beweis für sämtliche nicht geladenen Unterlisten |
| Einlauf Bezeichnung | 10.527526 | Knoten 10.527526; anschliessende Haltung 80642-10.527526 | Keine Einlauf-Objektakte | Verbindung wird beim Haltungsabgleich nicht gesammelt |
| Einlauf Art | Einspitz | Nicht als solcher Klartextwert in den geprüften Anschlussobjekten | Keine Einlauf-Objektakte | Nicht ohne weitere Regel als Einspitz ausgeben |
| Einlauf Höhe | Leer | Keine Kote am Anschluss-Haltungspunkt | Keine Einlauf-Objektakte | Keine Kote ergänzen |
| Einlauf Distanz | 6.36 m | Aus Geometrie: 6.357366 m (abgeleitet) | Keine Einlauf-Distanz | Gerundet stimmt die Geometrie mit WebGIS überein |
| Haltungspunkt von | A454675, ohne Kote | A454675, ohne Kote | Bezeichnung/TID technisch gespeichert; neues sichtbares Feld leer | Anzeige nutzt gespeicherte Zuordnung bisher nicht vollständig |
| Haltungspunkt bis | E454675: 505.91 | E454675, Kote 505.910 | Bezeichnung/TID technisch gespeichert; neues sichtbares Feld/Kote leer | Neuer Abgleich würde TID und Kote ergänzen |

Der Anschluss-Haltungspunkt `ch24gwkdSHRmhNha` verweist mit `AbwassernetzelementRef` auf unsere Haltung `ch24gwkdVy2uiPfJ`. Die anschliessende Haltung `80642-10.527526` endet an diesem Punkt. Seine Koordinate liegt weniger als 1 mm neben der gelieferten Haltungslinie. Die berechnete Entfernung vom Haltungsanfang beträgt 6,357366 m. Das ist eine nachvollziehbare räumliche Zuordnung; kein direkt geliefertes Distanzattribut.

## Administrativ

| Angabe | WebGIS-Foto | GeoShop-XTF / Begleitdatei | SewerStudio gespeichert | Einordnung |
|---|---|---|---|---|
| Eigentümer | AWU_von_privat (Abwasserverband) | Ref ch20p3q400002009; JSON nennt Abwasser Uri | Abwasser Uri | Stimmt mit JSON überein; WebGIS-Bezeichnung weicht ab |
| Betreiber | AWU_von_privat (Abwasserverband) | Dieselbe Ref ch20p3q400002009, kein Organisationsobjekt | Leer | Aktueller Abgleich ergänzt nur technische Ref, keinen ausgeschriebenen Namen |
| Strang-ID / Bezeichnung | Leer | Nicht geliefert | Leer | Gleich leer |
| Standort | Leer | Kein entsprechender Standortwert am Kanal | Neumühleweg | Zusätzlicher Projektwert; Herkunft daraus allein nicht bewiesen |
| Ort / Zugänglichkeit | Beide leer | Nicht geliefert am Kanal | Beide leer | Schacht-Zugänglichkeit nicht auf Haltung übertragen |
| Baulos | Leer | Nicht geliefert | Leer | Gleich leer |
| Bruttokosten | 0 | 0.00 | Leer | Null wird durch positiven Zahlenfilter verworfen |
| Subventionen | Leer | Nicht geliefert | Leer | Gleich leer |
| Baujahr / Ersatzjahr | Beide leer | Nicht geliefert am Kanal | Beide leer | Gleich leer |
| Finanzierung | Leer | Nicht geliefert | Leer | Gleich leer |
| Wiederbeschaffungswert / Basisjahr / Bauart | Sichtbare Felder leer | Nicht geliefert am Kanal | Leer | Gleich leer |

Die Eigentümer-JSON wurde nur gelesen. Sie wurde weder ausgewählt noch importiert. Ob „AWU_von_privat“ und „Abwasser Uri“ hier fachlich dieselbe Organisation mit anderer Bezeichnung meinen, lässt sich aus diesen Dateien nicht abschliessend bestätigen.

## Unterhalt

| Angabe | WebGIS-Foto | GeoShop-XTF | SewerStudio gespeichert | Einordnung |
|---|---|---|---|---|
| Zustand | Mittlere Mängel (Z2) | Z2 am Kanal | 4; auch live Z4 | Abweichender Zustand/Erhebungsstand |
| Sanierungsbedarf | Mittelfristig | mittelfristig | mittelfristig | Gleich |
| Erhebungsjahr Zustand | Leer | Nicht geliefert als Zustandsjahr | Leer | Inspektionsdatum 01.09.2026 ist ein separates Projektfeld |
| Inspektionsintervall / Spülintervall | Beide leer | Nicht geliefert am Kanal | Beide leer | Gleich leer |
| Unterhaltsmassnahmen | Sichtbare Tabelle leer | Keine entsprechenden Objekte im geladenen Verbund | Keine Objektakten | Keine vollständige Aussage zu nicht sichtbaren Unterlisten |
| Sanierungsmassnahmen | Sichtbare Tabelle leer | Keine entsprechenden Objekte im geladenen Verbund | Keine entsprechenden Objektakten; Inspektionsbefunde und PDF-Verknüpfungen vorhanden | Befunde sind keine Kataster-Massnahmenliste |
| Dichtheitsprüfungen | Sichtbare Tabelle leer | Keine entsprechenden Objekte im geladenen Verbund | Keine entsprechende Objektakte; DP-PDF verknüpft | Dokument und ausgefüllter Datensatz sind verschieden |

## Hydraulik und Metadaten

| Angabe | WebGIS-Foto | GeoShop-XTF | SewerStudio gespeichert | Einordnung |
|---|---|---|---|---|
| Rohrprofil Hydraulik | Unbekannt: unbekannt () | Verknüpftes Rohrprofil unbekannt | Sichtbares Hydraulikfeld leer | Technische Profilreferenz besteht bereits |
| Strang / Meliorationsfunktion / Sickerung / Leckschutz | Leer | Nicht geliefert im geprüften Verbund | Leer | Keine belegte Übernahmelücke |
| Hydraulische Belastung Ist / Fliesszeit Trockenwetter | Leer | Nicht geliefert | Leer | Gleich leer |
| Qmax / Qvoll / Vmax / Auslastungsgrad | Leer | Nicht geliefert | Leer | Gleich leer |
| Hmax oben/unten / Hfrei oben/unten | Leer | Nicht geliefert | Leer | Gleich leer |
| Länge effektiv in Hydraulik | Leer | Haltung enthält LaengeEffektiv 11.44 | Haltungsfeld enthält 10.55 | Keine zusätzliche hydraulische Länge belegt |
| Erstellt am UTC | 05.06.2020 07:36:51 | Nicht geliefert | Leer | Kein Ersatzwert erfinden |
| Erstellt von | Acht Grad Ost AG | Nicht geliefert | Leer | Kein Ersatzwert erfinden |
| Geändert am UTC | 21.11.2024 07:08:12 | Letzte_Aenderung 20241121, ohne Uhrzeit | Neues UTC-Feld leer; Bestandsfeld Letzte Änderung 21.11.2024 | Datum vorhanden; exakter Zeitpunkt nicht geliefert |
| Geändert von | anonymous | Nicht geliefert | Leer | Kein Ersatzwert erfinden |

Nicht sichtbare Bereiche unterhalb der Fotos wurden nicht als leer gewertet. Diese Prüfung betrifft eine Haltung und ihre konkret untersuchten Verknüpfungen. Sie ist keine Vollständigkeitsprüfung aller Objekte der 456-MB-Lieferung.

## Was ein erneuter Abgleich heute bewirken würde

Der echte aktuelle Leser und die Abgleichlogik wurden mit einer **Kopie im Arbeitsspeicher** ausgeführt. Die Projektdatei wurde nicht gespeichert.

- Im gespeicherten Projekt gibt es **keine Objektakten**. Die vorhandenen Werte liegen in den bisherigen Haltungsfeldern und technischen Kataster-Zuordnungen.
- Vor der Simulation sind 32 von 131 verfügbaren Haltungs-Anzeigedefinitionen gefüllt, danach 37. Diese Zählung enthält auch zusätzliche SewerStudio-Felder und doppelte fachliche Bezüge. Sie ist **keine Qualitätsquote** und keine Anzahl aller WebGIS-Felder.
- Ergänzt würden: Endhöhe 505.910, Rohrprofilreferenz, Haltungspunkt von, Haltungspunkt bis und Betreiberreferenz.
- Die Punktfelder würden zunächst technische TIDs anzeigen. Der Betreiber würde als `ch20p3q400002009` erscheinen. Das ist noch keine vollständige WebGIS-Darstellung mit Bezeichnungen.
- Länge 10.55 und Zustand 4 bleiben bestehen: Der Abgleich füllt nur leere Bestandsfelder. Er löst keine Wertkonflikte.
- Bruttokosten 0 und unbekannte Angaben bleiben leer. Typ AA und Materialgruppe bleiben trotz ableitbarer Werte ebenfalls leer.
- Der Leser nimmt neun verknüpfte Quellobjekte mit. Die rückwärts auf die Haltung zeigende Anschlussverbindung fehlt darin.

## Fachlich nötige Korrekturen, noch nicht ausgeführt

1. Katasterlänge und Inspektionslänge sowie Katasterzustand und Inspektionszustand getrennt erhalten. Bei Abweichungen beide Quellen zeigen.
2. Endkoten und bestehende Punktbezeichnungen in der Oberfläche verfügbar machen. Fehlende Anfangskote offen lassen.
3. Gelieferte Nullwerte und ausdrücklich „unbekannt“ von wirklich fehlenden Angaben unterscheiden.
4. Typ AA und Materialgruppe aus den gelieferten zusammengesetzten Werten ableiten, ohne die Originalwerte zu verlieren.
5. Anschlussverbindungen rückwärts über `AbwassernetzelementRef` mitnehmen. Eine berechnete Distanz als berechnet kennzeichnen.
6. Organisationen mit einer nachvollziehbaren, ausdrücklich ausgewählten Zuordnung auflösen. Unterschiedliche Namen aus WebGIS und JSON sichtbar lassen.
7. WebGIS-interne GUIDs, OBJECTID und historische Bezeichnungen nicht aus anderen Kennungen erfinden. Für fehlende Angaben wird eine zusätzliche Quelle benötigt.

Dieser Vergleich bestätigt **noch keinen korrekten Neu-XTF-Export nach GEONIS**. Dafür müsste eine erzeugte Datei separat gegen die erhaltenen Originalwerte und im Zielsystem geprüft werden. Insbesondere dürfen unterschiedliche Zustands- und Längenquellen nicht unbemerkt vermischt werden.

## Quellen und Nachweise

- WebGIS: neun vom Nutzer gelieferte Screenshots der Haltung 525145-505377, OBJECTID 454675. Es wurde dafür kein WebGIS-Datensatz bearbeitet.
- [GeoShop-Original-XTF](D:/QGIS_V4.2/GeoShop/2026-09/34UR_Abwasser_DSS_2020_1.xtf), 456586997 Bytes. SHA256: `ed6bfdc290f66e8bdefd5fcc6cff59013337adcd763df754d1eadec41da34830`.
- [Eigentümer-Zuordnung](D:/QGIS_V4.2/GeoShop/2026-09/eigentuemer_zuordnung.json), geprüfter Eintrag ch20p3q400002009 → Abwasser Uri.
- [Gespeichertes Projekt](D:/Projekte/Sanierungsabnahme_Zone_5.01_GKS_Bürglen/Projektdateien/projekt.json), Version 2; ModifiedAtUtc 2026-09-10T17:12:20.2687951Z. Haltungs-ID 3ed56dd0-5bc7-4891-af95-1624a49ea20d.
- [Aktueller ausgewählter Datensatz aus der laufenden Anwendung](nachweise/sewer-live.geojson). Gelesen über die dokumentierte lokale QGIS-Schnittstelle; Zugangsdaten wurden nicht in die Nachweise übernommen.
- [Originalwerte des XTF-Verbunds mit XML](nachweise/xtf-verbund.json), [gespeicherte Haltungswerte](nachweise/sewer-gespeichert.json), [heutige Anzeigewerte](nachweise/anzeige-vorher.json).
- [Ergebnis des echten aktuellen Lesers](nachweise/aktueller-leser.json), [Vorschau](nachweise/abgleich-vorschau.txt), [Anzeigewerte nach reiner Simulation](nachweise/anzeige-nach-simulation.json).
- [Anschluss-Haltungspunkt](nachweise/einlauf-rueckverweise.json), [anschliessende Haltung](nachweise/einlauf-haltung.json), [geometrische Berechnung](nachweise/geometrie-pruefung.json).
- [Nachvollziehbare Auswertung als Notebook](PRUEFUNG.ipynb). Die Prüfskripte liegen unter nachweise; sie sind Dokumentation des ausgeführten Prüfwegs, keine Änderung am Programm.

Die Ursache für übersprungene Bestandswerte steht in `GeoShopAbgleichPlanBuilder.cs`. Der Null-/Unbekannt-Filter steht in `QgisFeldKarte.cs`. Die Übernahme der neuen Felder steht in `GeoShopObjektaktenImport.cs`. Das Sammeln der verknüpften Objekte steht in `GeoShopXtfLeser.cs`.
