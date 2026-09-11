# GeoShop-Abgleich – Teststand 09.09.2026

## So testen

1. Ein Testprojekt öffnen. Bei Haltungen **Weitere Aktionen → GeoShop-Abgleich (XTF)** wählen.
2. `C:\Users\Besitzer\Downloads\order\34UR_Abwasser_DSS_2020_1.xtf` auswählen.
3. Die Vorschau prüfen: bisherige und neue TIDs, Verknüpfungen, ergänzbare Felder und ausgelassene Zuordnungen.
4. **Gezeigte Änderungen übernehmen** wählen und das Projekt speichern.
5. Auf der Seite **Schächte** denselben Abgleich ausführen. Beide Seiten gleichen jeweils ihre eigene Projektliste ab.

Der frühere Menüeintrag „Katasterkennungen“ ist ersetzt. „Leere Felder aus QGIS“ bleibt als eigener Bestandsweg bestehen.
Abbrechen oder das Schliessen der Vorschau übernimmt nichts. Die Originaldatei wird nur gelesen.
Die Suche umfasst alle Datensätze der jeweiligen Projektseite, nicht nur die gerade sichtbaren gefilterten Zeilen.

## Übernahme

- Die unveränderten XTF-TIDs werden im vorhandenen `Geonis`-Objekt gespeichert. Keine Umrechnung alter Präfixe.
- Haltung: Haltung, Kanal, beide Haltungspunkte samt Bezeichnungen und Rohrprofil.
- Schacht: Abwasserknoten und Bauwerk; Normschacht, Spezialbauwerk, Versickerungsanlage und Einleitstelle werden unterschieden.
- Das sichtbare Feld **SIA405-TID** und **Objekt-ID (Quelle)** zeigen anschliessend die Hauptkennung.
- Bestehende Fachwerte bleiben erhalten. Ergänzungen bekommen die Herkunft Kataster und gelten nicht als Handänderung.
- Ausgefüllte, von Hand geschützte und abweichende Kennungsfelder sperren die gesamte betreffende Übernahme.
- Ohne Quelle für eine Angabe bleibt das Feld leer. „unbekannt“ wird nicht als Ergänzung übernommen.

Unterstützte Fachwerte: Rohrmaterial, DN, Haltungslänge, lichte Breite aus Höhe/Profilverhältnis, Profiltyp, Schacht oben/unten,
Nutzungsart der Haltung, hierarchische/hydraulische Funktion, Lagebestimmung, Innenschutz, Verbindung, Bettung,
Status, Sanierungsbedarf, ausdrücklich gelieferte Zustandsklasse, Baujahr, Bruttokosten (Haltung), Eigentümer mit aufgelöstem
Organisationsobjekt, XTF-Änderungsdatum; bei Schächten zusätzlich Material, Funktion, Dimension 1/2, Bemerkungen und Bauwerksart.
Schachtzustände werden niemals aus Schäden berechnet. Spezialfunktionen und Versickerungsart werden getrennt übernommen.

Diese erste Testfassung ergänzt keine Geometrien, Deckelobjekte, Medien oder Inspektionsbeobachtungen.
Externe Organisationsverweise ohne mitgeliefertes Objekt liefern keinen Eigentümernamen. Dazu erscheint ein Hinweis.
Die zusätzliche Eigentümer-JSON-Datei im Downloadordner wird nicht stillschweigend als zweite Datenquelle verwendet.

## Zuordnung und Schutz

Ein Name muss im Projekt und in der XTF eindeutig sein. Direkter Name und Gegenrichtung werden zusammen geprüft.
Bei Gegenrichtung werden Punktkennungen und Schachtfelder getauscht. Widersprechende vorhandene Endschächte oder Bauwerksarten
sperren die Zuordnung. Doppelte TIDs, falsche Objektklassen und fehlende Kernverknüpfungen werden ausgelassen.
Bei abweichender vorhandener Höhe oder Profilart wird keine alte lichte Breite ergänzt; dazu erscheint ein Hinweis.
Es entstehen keine neuen Projektobjekte. Zwischen Vorschau und Übernahme werden Datensatzidentität, Listenbestand und die
Felder/Metadaten/Kennungen aller zur Übernahme vorgesehenen Datensätze erneut geprüft.

Die XTF wird mit verbotenem DTD und ohne externe XML-Auflösung gelesen. Mehrere Durchläufe halten nur die angefragten Objekte
und Verknüpfungen im Speicher. Ein durchgehend lesend geöffneter Dateistrom verhindert paralleles Schreiben an der Quelle.

## Grenze des GEONIS-Rückwegs

Der bestehende XTF-Exporter nutzt den übernommenen Kennungsverbund. Das bestätigt noch keinen produktiven FME-Abgleich.
`Letzte_Aenderung` aus der XTF wird **nicht** als `GN_LAST_EDITED_DATE` ausgegeben. `GeonisGeaendert` bleibt bei dieser Quelle leer.
Die von Trigonet genannte Kanal-TID muss mit dem aktuellen Lieferstand abgestimmt werden.

Nur lesend am Original geprüft: Haltung `78998-79002` hat TID `ch24gwkdH2Ny5Esg` und verweist auf Kanal `ch24gwkduL3A2Sjp`.
Schacht `78998` hat Knoten `ch24gwkdftlGdbHU` und Bauwerk `ch24gwkdUmcgr2UF`.
Die beiden Abfragen dauerten gemeinsam etwa 8 Sekunden bei maximal 164 MB Arbeitsspeicher. Kein Kundenprojekt wurde verändert.

## Code und Prüfung

- `IGeoShopLeser` / `GeoShopXtfLeser`: Dateilesen; `GeoShopXtfZuordnung`: Quellattribute über bestehende Fachvokabulare abbilden.
- `GeoShopZiel`, `GeoShopAbgleichPlanBuilder`, `GeoShopAbgleichAnwender`, `GeoShopAbgleichBericht`: Planung, Bestandsschutz, Übernahme.
- `ServiceProvider.GeoShop` registriert den Leser; `GeoShopAbgleichDialog` und `GeoShopAbgleichWindow` zeigen die abbrechbare Vorschau.
- Die alten Katasterdienste und Befehlsnamen bleiben für bestehende Aufrufer kompatibel; die beiden Menüaktionen verwenden GeoShop.
- `GeoShopAbgleichTests`: synthetische XTFs, unveränderte Quelldatei, Feldschutz, IDs bis zum Export, Gegenrichtung, Duplikate,
  fehlende/falsche Referenzen, Projektänderung nach Vorschau, DTD/Abbruch und Bauwerksklassen.
- `GeoShopAbgleichUiTests`: Registrierung und isoliert gerenderte Vorschau mit gesperrter/freigegebener Übernahme.
  Bild: `.tmp/geoshop-vorschau.png`. Ein echter FME-Rückimport ist nicht Teil dieser Tests.

### Abschlussnachweise

- Release: `dotnet build AuswertungPro.Dev.slnf -c Release --no-restore` erfolgreich, 0 Warnungen, 0 Fehler.
  Protokoll: `.tmp/geoshop-abschluss-build.log`.
- 70 gezielte Infrastrukturprüfungen erfolgreich: neue GeoShop-Fälle, bestehende Katasterregeln und XTF-Verbundexport.
  Protokoll: `.tmp/geoshop-abschluss-infra.log`.
- 87 gezielte UI-/Architekturprüfungen erfolgreich. Der isolierte Kindtest wird im Elternlauf einmal erwartungsgemäss
  übersprungen; sein tatsächlicher Lauf wird vom erfolgreichen Elterntest bestätigt. Protokoll: `.tmp/geoshop-abschluss-ui.log`.
- Gesamtlauf Infrastruktur: 6430 bestanden, 6 übersprungen, 2 Fehler in `SanierungsprotokollEchteQuelleTests`
  (Begleitprotokoll-Erkennung und fehlender Verteilordner). Diese Fälle liegen ausserhalb des GeoShop-Abgleichs.
- Gesamtlauf UI: 6938 bestanden, 21 übersprungen, zunächst 8 Fehler. Die fünf GeoShop-bezogenen Prüfungen
  (Schriftskala, Fenstername, Fensterauftritt, Abbruchbehandlung und Registrierungszahl) sind im Abschlusslauf behoben.
  Die übrigen Befunde betrafen PlayerWindow-Grösse, Player-Verdrahtung und den QGIS-Bridge-Vertrag.
  Protokolle: `.tmp/geoshop-infrastructure-gesamt.log`, `.tmp/geoshop-ui-gesamt.log`.
- Architekturkarte aktualisiert; `quick_validate.py` meldet `Skill is valid!`.
