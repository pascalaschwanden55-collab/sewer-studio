# GeoShop-Abgleich – Feldvergleich vom 14.09.2026

## Aktuelle Bedienung

Auf **Schächte → Weitere Aktionen → GeoShop-Abgleich (XTF)** die Lieferung auswählen.
Die Einzelaktion **Mehr → Fehlende Felder aus GeoShop-XTF** verwendet denselben Vergleich.
Vorhandene Projektschächte müssen bereits angelegt sein; der Abgleich legt keine neuen Schächte an.

Liegt `eigentuemer_zuordnung.json` im selben Ordner wie die XTF, wird sie automatisch
berücksichtigt. Eine anders benannte Zuordnungsdatei lässt sich zusätzlich auswählen.
Die Kennung `ch20p3q400002009` ist in der geprüften Begleitdatei **Abwasser Uri** zugeordnet.
Ein technischer Verweis verhindert die Namensergänzung nicht mehr. Ein bereits gespeicherter
abweichender Wert bleibt im Vergleich wählbar; Handwerte bleiben geschützt.
Ist die Zusatzdatei fehlerhaft, nennt der Bericht den Fehler und erhält den Originalverweis.

Die Kurzansicht bietet auch **Saniert** aus der Objektmaske an. Abweichende Gross-/Kleinschreibung
wie `Kurzfristig` und `kurzfristig` lässt die Auswahl nicht mehr leer erscheinen.
Das Öffnen verändert keine gespeicherten Angaben und erweitert keine DSS-Normwerteliste.

Die gleiche Absicherung gilt jetzt für alle festen Auswahlfelder der Kurzansicht:
Ein gespeicherter Wert bleibt sichtbar, auch wenn die Kurzliste ihn nicht enthält.
Das gilt beispielsweise für `Kreisprofil`, Materialdetails und eigene Angaben.
Bekannte Begriffe aus «Alle Angaben» verursachen keinen falschen Auswahlhinweis.
Nicht zuordenbare Alteinträge bleiben sichtbar und erhalten ihren Prüfhinweis.
`Kreisprofil` wird als gleichbedeutend mit `Rund` erkannt; bestehende Eingaben werden
beim Öffnen oder Wechseln der Listen nicht umgeschrieben.

`KurzansichtAbgleichTests` prüfen alle 13 gemeinsam bearbeitbaren Schachtfelder mit
176 Katalog-/Eingabefällen: Funktion, Nutzungsart, Status, Baujahr, Material, Form,
beide Innenmasse, Tiefe, Bemerkung, Eigentümer, Zustand und Sanierungsbedarf.
Der echte WPF-Test prüft zusätzlich die sichtbare Auswahl, Listenwechsel, eine
externe Aktualisierung ohne Rückschreiben und bewusstes Leeren.

Abschluss Kurzansicht: Release-Build erfolgreich, 347 weitere UI-Prüfungen sowie
der isolierte Formularlauf bestanden; zusätzlich 35 Infrastrukturprüfungen.
Der isolierte Test lädt die echten Darstellungsressourcen ohne den produktiven
Programmstart. Protokolle: `kurzansicht-ui.trx`, `kurzansicht-wpf.trx` und
`kurzansicht-infra.trx` unter `.tmp/geoshop-pruefung`.
Der zunächst fehlgeschlagene Formularlauf ist im separaten WPF-Abschlussprotokoll korrigiert nachgewiesen.

- Die Tabelle zeigt Objekt, Feld, bisherigen Wert, bisherige Herkunft und GeoShop-Wert.
- Leere Felder sind angehakt. Bei Abweichungen bleibt zunächst der bisherige Wert.
  Zum Beispiel lässt sich ein alter Importwert `NOD` ausdrücklich durch `Kontrollschacht` ersetzen.
- Handergänzungen und bewusst geleerte Handfelder sind gesperrt.
  Bei Materialabweichungen prüfen, ob Bauwerksmaterial und Auskleidung unterschiedliche Angaben beschreiben.
- Abweichende Koordinaten und Schachtmasse werden paarweise gewählt, damit keine gemischten Werte entstehen.
- **Gezeigte Änderungen übernehmen** sichert zuerst den aktuellen Projektstand und übernimmt danach die Auswahl.
  Abbrechen verändert keine Projektdaten. Anschliessend das Projekt speichern.

Kennungen und belegte Originaldaten werden ebenfalls übernommen; der Bericht zeigt diese Änderungen.
Haltungen benutzen denselben Dialog. Die bereits vereinbarte Ausnahme bleibt bestehen:
Die Haltungslänge kommt immer aus der XTF, auch bei einer früheren Handänderung.

## Welche Angaben kommen an?

Die bestehenden Fachzuordnungen bleiben erhalten. Ergänzt wurden LV95-Rechts-/Hochwert aus der Lage
des Knotens, des Deckels und der Haltungspunkte. Standort, Bruttokosten (auch 0),
Datenherr/Datenlieferant sowie Änderungsdatum werden in die passenden Felder übernommen.
Ausdrücklich gelieferte Angaben wie `unbekannt` gehen beim GeoShop-Import nicht mehr verloren;
Masszahlen behalten ihre Genauigkeit. Der bisherige QGIS-Filter bleibt unverändert.
Organisationsobjekte liefern den Namen; fehlt das Objekt, bleibt der gelieferte Verweis sichtbar.
Bei Haltungen wird Typ AA aus `PAA.…`/`SAA.…` in `Kanal.FunktionHierarchisch` abgefüllt.
Die Organisations- und Kostenfelder erscheinen bei Schächten unter **Bisherige Angaben**.
Ohne separat gelieferte OBJECTID zeigt dieses Feld die Schachtbezeichnung mit Hinweis.
Eine vorhandene eigene OBJECTID bleibt bestehen; XTF-TIDs werden dabei nie ersetzt.

Alle durch die vorhandene Zuordnung belegten Zusatzfelder und
verknüpften Objektakten nehmen am Feldvergleich teil. Nicht zugeordnete Originalattribute bleiben
in den Quellbelegen erhalten; sie werden dadurch nicht automatisch zu einem Eingabefeld.

Die Deckelhöhe kommt vom ausdrücklich gewählten Hauptdeckel oder vom einzigen zugeordneten Deckel.
Bei mehreren Deckeln ohne Auswahl bleibt sie leer. Eine Hauptdeckelmarkierung wird nicht automatisch gesetzt.
Die Berechnung funktioniert direkt in den drei Feldern in allen Richtungen:

- **Tiefe = Deckelhöhe − Sohlenhöhe**
- **Deckelhöhe = Sohlenhöhe + Tiefe**
- **Sohlenhöhe = Deckelhöhe − Tiefe**

Zwei vorhandene Zahlen ergänzen die dritte Anzeige, solange das Feld leer und nicht von Hand geschützt ist.
Beispiel: 520.600 − 517.710 = **2.890 m**. Die berechnete Anzeige folgt späteren Eingaben
und bleibt nach erneutem Öffnen aus den gespeicherten Ausgangswerten verfügbar.
Die Formel steht im Feldhinweis. Bestehende Werte und bewusst geleerte Handfelder werden nicht überschrieben.
Widersprechen sich drei vorhandene Werte um mehr als 1 mm, erscheint ein Prüfhinweis.
Auch ungültige Zahlen, negative Tiefe und eine unklare Deckelwahl werden gemeldet.
Es wird kein Deckelobjekt aus einer Rechnung angelegt; berechnete Anzeigen verändern keine Originalbelege.
Die vorhandene Deckelhöhe wird weiterhin am zugeordneten Deckel bearbeitet.

Nachtrag Höhenrechnung: Release-Build erfolgreich; 104 gezielte Infrastrukturtests und
518 UI-/Architekturtests bestanden (drei isolierte Kindtests im Elternlauf übersprungen).
Die neuen Fälle prüfen alle drei Richtungen, sofortige Anzeige nach Eingaben, erneutes Laden,
Handwerte, bewusstes Leeren, Zahlen mit Komma, widersprüchliche Werte und mehrere Deckel.
Protokolle: `.tmp/geoshop-pruefung/hoehen-infra.trx` und `hoehen-ui.trx`.

Eine eindeutige Materialgruppe wird aus dem gewählten Material angezeigt. Aus `Beton` wird kein erfundenes `Beton, Fertigteil`.

Am vom Benutzer gezeigten Schacht **60248** wurde die Original-XTF zuvor nur lesend untersucht:

| Angabe | In der untersuchten XTF |
|---|---|
| Funktion / Material | Kontroll-/Einsteigschacht / Beton |
| Masse / Baujahr | 1100 × 900 mm / 1974 |
| Rechtswert / Hochwert | 2692748.532 / 1192136.855 |
| Sohle / zugehöriger Deckel | 503.680 / 505.920 m |
| Daraus berechenbare Tiefe | 2.24 m |

Die dort gezeigten WebGIS-Zusatzangaben `27.1`, `PAA`, `Oval`, `165.1` und `Beton, Fertigteil`
sind in dieser XTF am Objekt nicht geliefert. Die numerische WebGIS-OBJECTID ist keine XTF-TID.
Für solche Angaben wird eine zusätzliche passende GeoShop-Lieferung benötigt; der Import rät sie nicht.
Die Vorschau nennt nicht gelieferte oder nicht belegbar zugeordnete Felder.

Nachtrag Feldzuordnung vom 14.09.2026: Release-Build erfolgreich. 89 gezielte
Import-/Exporttests und 525 Oberflächen-/Architekturtests bestanden; drei isolierte
Kindtests im Elternlauf erwartungsgemäss übersprungen. Geprüft sind die ergänzten
Felder bis zur Formularanzeige, wiederholter Import, externe Organisationsverweise,
ausdrücklich unbekannte Angaben, Nullwerte, Massgenauigkeit, OBJECTID-Anzeige und
der Export beibehaltener Haltungspunktkoordinaten.
Protokolle: `.tmp/geoshop-pruefung/abschluss-infra.trx` und `abschluss-ui.trx`.

## Wiederimport und Schutz

Die Entscheidungen bleiben im Projekt gespeichert. Dieselbe Lieferung erzeugt bei unverändertem
Bestand keine erneuten Fragen. Neue Lieferwerte oder eine zwischenzeitliche Bestandsänderung
werden wieder verglichen. Bestätigte Katasterwerte werden durch spätere PDF-/Protokollimporte
nicht still zurückgesetzt; eine Handkorrektur bleibt möglich.

Originalbelege derselben Quellidentität werden auf den neuen Lieferstand aktualisiert.
Es entstehen keine widersprüchlichen Doppelversionen für den DSS-Export. Bewusst beibehaltene
Feldwerte und Koordinaten bleiben auch beim späteren DSS-Export massgebend.
Ein Projektwechsel oder eine Änderung während der Vorschau verhindert die Übernahme.
Ein Schreibfehler setzt alle Änderungen dieses Übernahmelaufs zurück.

## Importsicherung wiederherstellen

Vor jeder Übernahme entsteht eine neue geprüfte JSON-Sicherung unter
`%LOCALAPPDATA%\SewerStudio\GeoShop-Sicherungen`. Bei gesetztem `SEWERSTUDIO_APPDATA_DIR`
liegt der Unterordner dort. Der Dateiname enthält Projekt-ID, UTC-Zeit und eine eindeutige Kennung.
Auch noch ungespeicherte Projektangaben sind enthalten. Medien und Original-XTF werden nicht kopiert oder verändert.
Kann die Sicherung nicht geschrieben und geprüft werden, wird nichts übernommen.

Zur Wiederherstellung das betreffende Projekt schliessen. Die aktuelle Projektdatei separat behalten.
Die passende Sicherung als `projekt.json` **an den ursprünglichen Ort der Projektdatei** kopieren
und das Projekt erneut öffnen. Dadurch bleiben relative Medienverweise auf ihre bisherigen Ordner bezogen.
Die Sicherung enthält den Stand unmittelbar vor dem betreffenden Import.

## Prüfungen für diese Erweiterung

`GeoShopRobusterImportTests` prüft unter anderem Handfelder/Leerwerte, wiederholtes Laden,
veränderte Lieferungen, Koordinatenpaare, Deckelwahl, Materialgruppe, Sicherungsfehler,
vollständige Rücknahme und DSS-Export beibehaltener Werte. Testdaten sind synthetisch.
`GeoShopAbgleichUiTests` prüft die wirkliche WPF-Tabelle einschließlich Auswahl und gesperrtem Handfeld.
Prüfprotokolle dieses Laufs: `.tmp/geoshop-pruefung`; Bild: `.tmp/geoshop-vorschau.png`.

Abschluss am 14.09.2026:

- Release-Build der Entwicklungslösung erfolgreich, 0 Fehler; zwei bereits vorhandene
  Nullability-Warnungen in `ObjektaktenListenErgaenzungenStore` und `VsaFotoAblageTests`.
- 96 gezielte Infrastrukturtests bestanden, darunter 18 neue robuste Importfälle,
  DSS-Export, wiederholtes Laden, Material-/Koordinatenanzeige und Schutzregeln.
- 549 gezielte UI-/Architektur-/Aufklapplistenprüfungen bestanden; sechs isolierte
  Kindtests im Elternlauf erwartungsgemäss übersprungen. Der WPF-Vergleich wurde gerendert und visuell geprüft.
- Im vorherigen vollständigen Infrastrukturlauf bestanden 6623 Tests, sechs wurden übersprungen.
  Ein alter Test verlangte noch das Überschreiben unmarkierter Katasterwerte. Seine Erwartung
  wurde auf den neuen Importschutz umgestellt und im abschliessenden gezielten Lauf erfolgreich geprüft.
  Der vollständige Infrastrukturlauf wurde danach nicht erneut ausgeführt.
- Architekturquelle und Architektur-Skill aktualisiert; Skill-Validierung und `git diff --check` erfolgreich.

Die folgenden Abschnitte dokumentieren frühere Ausbaustände. Für die Übernahmeregeln gilt die Beschreibung oben.

---

# Früherer Teststand 09.–11.09.2026

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

## Je Haltung / je Schacht: «Fehlende Felder aus GeoShop-XTF» (11.09.2026)

In der aufgeklappten Haltung oder dem aufgeklappten Schacht (und im Objektakten-Fenster) unter **Mehr**:

- **Fehlende Felder aus GeoShop-XTF** liest nur dieses eine Bauteil aus der gemerkten XTF (Sekunden statt
  ganzes Projekt), zeigt einen kurzen Text «Haltung «A-B»: 4 leere Felder ergänzen, 1 ersetzen, Kennungen
  übernehmen» mit Ja/Nein und schreibt danach genau wie der grosse Abgleich: leere Felder, Kennungen,
  Objektakte (Originalwerte, Deckel, Ereignisse). Ist nichts zu übernehmen, sagt er warum
  (nicht gefunden, mehrdeutig, bereits abgeglichen).
- **GeoShop-XTF wählen…** — die Datei wird programmweit gemerkt (`AppSettings.GeoShopXtfPath`); beim ersten
  Mal wird gefragt, danach nicht mehr. Der grosse Abgleich merkt sich seine Datei ebenfalls.
- Dieselben Regeln, kein zweiter Weg: `GeoShopEinzelErgaenzung` ruft `GeoShopAbgleichPlanBuilder` und
  `GeoShopAbgleichAnwender` mit genau einem Ziel auf; der Datensatz-Stand wird vor dem Schreiben erneut geprüft.

**Die Haltungslänge kommt immer aus der XTF** (Entscheid Pascal 11.09.2026): Ein vorhandener Wert — auch ein
von Hand gesetzter — wird ersetzt und als Katasterwert markiert (gilt danach nicht mehr als Handänderung).
Gleiche Werte in anderer Schreibweise («12.5» / «12.50») sind keine Änderung. Die Vorschau zeigt
«alt → neu (ersetzt)». Alle anderen Felder werden weiterhin nur gefüllt, wenn sie leer sind. Liste:
`GeoShopAbgleichPlanBuilder.ImmerAusXtf`. Gilt für beide Wege.

Wächter: `GeoShopAbgleichTests.Haltungslaenge_kommt_immer_aus_der_XTF_auch_wenn_von_Hand_gesetzt`,
`Einzelergaenzung_liest_nur_dieses_Bauteil_und_schreibt_erst_beim_Anwenden`,
`ObjektakteUiTests.GeoShop_Befehle_gibt_es_nur_mit_Anbindung_und_die_Maske_liest_danach_neu`.

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

### Bekannte Grenze beim Änderungsexport (14.09.2026)

GeoShop kann Datenherr und Datenlieferant nur als Organisationskennung liefern.
Der Import erhält diese Kennung korrekt, wenn kein belegter Name vorhanden ist.
`XtfNeuPlanBuilder.Organisationsbuch` erwartet beim SIA405-Änderungsexport bisher
jedoch Organisationsnamen. Eine gelieferte TID scheitert an dieser Namensprüfung;
das betroffene Bauwerk wird vollständig ausgelassen, obwohl Handeingaben vorliegen.
Das ist eine offene Exportlücke, kein Beleg für fehlende Schachtkorrekturen im Projekt.
Die Prüfung des Exportumfangs muss diese Grenze berücksichtigen. Die Kennungen im
Projekt dürfen nicht durch einen geratenen Namen ersetzt werden.

### Vollständige Release-Prüfung vor Push (14.09.2026)

- `dotnet build AuswertungPro.sln -c Release --no-restore`: erfolgreich, 0 Fehler,
  2 bestehende Nullable-Warnungen in `ObjektaktenListenErgaenzungenStore` und `VsaFotoAblageTests`.
- Infrastruktur: 6647 bestanden, 6 übersprungen.
- Pipeline: 2658 bestanden, 3 übersprungen.
- UI: 7276 bestanden, 27 übersprungen.
- ProjectModernizer: 62 bestanden, keine übersprungen.
- Summe: 16643 bestanden, 36 bekannte Umgebungs-/Kindprozess-Skips, keine Fehler
  in den abschliessenden Läufen. Die tatsächlichen WPF-Kindläufe werden über ihre Elterntests geprüft.
- Ein zunächst fehlgeschlagener Quelltextwächter erkannte eine Methode wegen gemischter
  Zeilenumbrüche nicht. Nach Vereinheitlichung bestand die vollständige UI-Suite erneut.
- Prüfprotokolle: `.tmp/geoshop-pruefung/push-AuswertungPro.Next.Infrastructure.Tests.trx`,
  `push-AuswertungPro.Next.Pipeline.Tests.trx`, `push-ui-final.trx`, `push-ProjectModernizer.Tests.trx`.
- `git diff --cached --check` und Architektur-Skill-Validierung erfolgreich.
  Diese Prüfungen ersetzen keine Abnahme der oben beschriebenen offenen Exportgrenze.
