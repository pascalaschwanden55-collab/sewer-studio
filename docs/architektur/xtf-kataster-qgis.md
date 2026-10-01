# XTF, SIA405/DSS, GeoShop, Kataster und QGIS

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Robuster GeoShop-XTF-Export (14.09.2026)
- GeoShop-Feldvergleich und Importsicherung (14.09.2026)
- QGIS-Bruecke: eine leere Ebene geht nie ohne Spalten hinaus (11.09.2026)
- GeoShop-Abgleich aus Original-XTF (09.09.2026, Testfassung)
- Redesign-Feldkorrekturen (08.09.2026)
- (Fortsetzung aus «Begleitprotokolle der Sanierung und Videodopplung (Bürglen, 09.09.2026)»)
- Aufbau des SIA405-Rueckwegs (30.09.2026, Wartbarkeitspaket AP08)
- XTF-Import: nichts still vermischen (30.09.2026)

## Robuster GeoShop-XTF-Export (14.09.2026)

- `XtfNeuExportService` verwendet den vorhandenen DSS-Planer jetzt auch für
  Änderungslieferungen aus Objektakten. Der alte SIA405-Planer akzeptiert gültige
  Organisations-TIDs direkt. Eine unvollständige Anzahl Projektobjekte sperrt die Ausgabe.
- `DssAenderungsPlanBuilder` vergleicht Normwerte/Verweise mit den Originalbelegen nach
  TID. Vollständiger Normkontext bleibt erhalten; nur `Aenderung`-Einträge sind Aufträge.
  Optionale Leerungen und die Bauwerksbeziehungen neuer Ereignisse werden ausgewiesen.
- `DssProjektAngaben` erhält aktuelle Felder, Objektfelder, Originalcodes, Hand-Leerungen
  und Listen im bestehenden Zusatzmodell als versioniertes `Erfasste_Angaben`-Paket.
  `DssQuellabweichungen` trennt ungültige optionale Quell-Auswahlcodes ab. Keine Änderung
  der ILI-Modelle, keine erfundenen DSS-Attribute. Saniert/Jahr ohne Datum bleiben separat.
- Materialdetail Schacht-Code 104 wird zu Normmaterial Beton; Detail bleibt zusätzlich.
  Zustandstexte mit Z0–Z4 werden ausdrücklich auf die korrekten Normwerte abgebildet.
- `XtfQuellverbundErgaenzung` (Infrastructure/Import/Xtf) liest fehlende interne
  Bezugsobjekte aus vorhandenen/ausgewählten Quellen nur in eine Ausgabekopie nach.
  Sie verwendet den bestehenden XML-Leser, hält den Dateistrom lesend gesperrt und
  verwirft mehrdeutige TIDs. Request ergänzt optionale Quelldateien, Result QuelleFehlt.
  `XtfNeuErstellenUseCase` verbindet die bestehende Dateiwahl mit erneuter Prüfung.
- Die Exportseite liefert auch beim Vollmodus die Zusatzangaben mit. Bauwerkskennungen
  müssen dem Originalverweis entsprechen. Namenskonflikte nennen beide Original-TIDs;
  Namen werden nicht automatisch umbenannt. Keine neue Dienstregistrierung oder Pakete.
- Synthetische Voll-/Änderungslieferung: ilivalidator mit allObjectsAccessible bestanden.
  Reales Projekt Bürglen (14.09. abends): beide Lieferungen ilivalidator null Fehler. Mit
  allObjectsAccessible fehlt nur der Datenherr `ch20p3q400002009`: GeoShop liefert keine
  Organisationsobjekte, der Verweis ist echt extern. Originale unangetastet, kein GEONIS-Rückimport.
- **Ein Haltungspunkt gehört zu genau einer Haltung.** Punkte fremder Leitungen an
  Projektknoten (Bürglen: 129 von 167 Punktakten) werden nur mit eigenen Eingaben geliefert
  (`DssObjektarten.HatEigeneEingaben`: Handwert, Unterliste oder bewusst behaltener
  Vergleichswert). Ohne Eingabe bleiben sie in GeoShop, und ein Quellkonflikt dort sperrt
  nichts mehr. So war der doppelte Quellname A76157 (Projekthaltung gegen Hausanschluss
  ausserhalb des Projekts) keine Sperre mehr. Nie wieder alle Punkte am Knoten holen.
- **Gleiches Profil = Originalverweis.** `DssProfilBearbeitung` legt ein eigenes Rohrprofil
  nur an, wenn Profiltyp, Verhältnis oder Datenherr/-lieferant vom Original abweichen. Eine DN
  aus dem alten Import (`FieldSource.Legacy`) gilt sonst als Änderung und erzeugte in Bürglen
  19 identische Kreisprofile. Tests: `XtfDssFremdeHaltungspunkteTests`, `XtfDssRohrprofilTests`.
- **Ein fremdes Modell im Transfer macht die ganze Datei unlesbar, nicht nur seinen Teil.**
  Gemessen 14.09.2026: Ohne `SewerStudio_Zusatz_2026.ili` bricht der INTERLIS-Leser mit
  `model(s) not found` ab. Wer Zusatzangaben oder Feldaufträge liefert, muss die `.ili`
  mitliefern UND der Empfänger muss sie in sein Modellverzeichnis legen.
- **Die reine Normlieferung (`MitZusatzangaben: false`) ist der Weg ohne diese Bedingung**
  und war bis 14.09.2026 kaputt: Drei fachlich immer gültige Regeln hingen an
  `mitZusatzangaben` (Sanierungsbedarf «Saniert», blosses Sanierungsjahr, gespiegelter
  ungültiger Quellcode), dazu `DssQuellabweichungen.Trenne`. Aus einem echten Projekt
  entstand deshalb gar keine reine Normdatei. Diese Regeln nie wieder an das Zusatzmodell
  koppeln — sie sagen «kein Normwert vorhanden», nicht «woanders untergebracht».
- Die Vorschau der Änderungslieferung zeigt die Feldaufträge als Tabelle Objekt/Feld/Alt/Neu
  (`XtfNeuPlan.Auftraege` → `XtfNeuExportResult.Aenderungen` → `XtfExportVorschau.AusBericht`).
  Gleichartige Feldlücken werden in `KurzeWarnungen` gebündelt; die volle Liste bleibt in
  `Warnungen` und in den Details. Die Aussage «Diese Datei erhält neue Kennungen» ist weg —
  eine DSS-Lieferung behält die Originalkennungen.
- **«Paket für GEONIS erstellen»** (`XtfKatasterPaketUseCase`) erzeugt beide Fassungen in
  einem Durchgang, legt Berichte und eine erzeugte `LIESMICH.txt` dazu und packt alles als
  ZIP. Eine Vorschau für beide; scheitert eine Fassung, entfernt `IXtfPaketAblage.Verwirf`
  das angefangene Paket — ein halbes Paket könnte versehentlich verschickt werden. `Verwirf`
  löscht nur, was derselbe Lauf angelegt hat; nichts wird überschrieben. Die Liesmich-Datei
  (`KatasterPaketLiesmich`, reine Textregel) ist für den Empfänger geschrieben und nennt nur
  Zahlen, die in den Berichten stehen.
- **Ein `Aenderung`-Eintrag trägt nie einen Wert.** Der neue Wert steht am Objekt mit der
  genannten `ObjektTid`; nur ein dort fehlendes Attribut bedeutet Leeren. Die Anleitung sagte
  bis 14.09.2026 abends das Gegenteil und hätte jedes beauftragte Feld geleert. Feldnamen mit
  Doppelpunkt (`Beziehung:`, `Zusatz:`) sind keine DSS-Attribute und folgen einer eigenen Regel.
- Die Auftragszahl der Vorschau muss der des Berichts entsprechen; die `Zusatz:`-Aufträge
  gehören dazu (Bürglen: 534, nicht 447). `DssAenderungsPlanBuilder` legt für jeden Auftrag
  eine Anzeigezeile an.
- Die Vorschau zählt Quellobjekte, die nicht zum Projekt gehören, nicht mehr als fehlende
  Angaben (Bürglen: 239 von 483). Bis zu drei bleiben namentlich, darüber werden sie zu
  einer Zeile. Ein gewöhnlicher Export sah sonst wie ein Datenverlust aus.
- Prüfanleitung und genaue Empfängerregeln: `docs/XTF-EXPORT-KONTROLLE.md`.
  Tests: `XtfDssAenderungsExportTests`, `XtfQuellverbundErgaenzungTests` und XTF-Bestand.
- Begleitende Push-Reparatur: `StartupSplashWindow.Impulse` enthält FirePulse und
  SpawnFlare unverändert ausgelagert; die Animationsdatei bleibt unter 1000 Zeilen.

## GeoShop-Feldvergleich und Importsicherung (14.09.2026)

- Frühere Exportgrenze, am 14.09.2026 anhand des gespeicherten Projektstands erkannt:
  `XtfNeuPlanBuilder.Organisationsbuch` behandelt originale Organisations-TIDs in
  Datenherr/Datenlieferant noch als Namen. Dadurch werden betroffene Schächte im
  SIA405-Änderungsexport trotz Handkorrekturen ausgelassen. Importierte Kennungen
  bleiben korrekt erhalten. Behoben durch den oben beschriebenen DSS-Änderungsweg
  und direkte TID-Erkennung; neue Quellkonflikte werden ausdrücklich gesperrt.
  Siehe `docs/XTF-EXPORT-KONTROLLE.md`.

- Nachtrag Abgleich «Alle Angaben»/«Kurz»: `RecordDetailItem.AnzeigeOptionen` nimmt
  den tatsächlich gespeicherten Wert nur für die Anzeige dieses Feldes auf, wenn er
  in der kürzeren Liste fehlt. Die festen Optionslisten bleiben unverändert.
  `KurzansichtAuswahl` erkennt Begriffe des gemeinsamen Objektkatalogs, einschliesslich
  Materialdetails und Form-Aliasen. Die bauwerksabhängige Funktionsprüfung bleibt bestehen.
  `SchachtformVokabular` erkennt «Kreisprofil» als «Rund»; vorhandene Datensätze werden
  beim Anzeigen nicht umgeschrieben. Listenwechsel und kurzzeitige WPF-Abwahl löschen
  keine Angaben; bewusstes Wählen des leeren Eintrags bleibt möglich.
  Nachweise: `KurzansichtAbgleichTests` (alle 13 gemeinsam bearbeitbaren Schachtfelder,
  176 Katalog-/Eingabefälle), echter Formularablauf in `NachschlagKontextmenueTests`.
- Nachtrag Eigentümer/Kurzansicht: `GeoShopEigentuemerDatei.ErgaenzeBegleitdatei`
  verwendet die genau benannte `eigentuemer_zuordnung.json` neben der XTF automatisch.
  Fehler erscheinen im Importhinweis; die XTF bleibt lesbar. Eine TID im Eigentümerfeld
  ist kein widersprechender Name. Echte Namenskonflikte und Handwerte bleiben geschützt;
  wiederholte Zuordnung erzeugt keine doppelten Belege.
  `SanierungsbedarfOptionen` vereint für beide Projektseiten Normwerte und Angaben der
  Objektmaske, einschliesslich «Saniert». `RecordDetailItem.SelectedOption` erkennt
  unterschiedliche Gross-/Kleinschreibung ohne Rückschreiben. DSS-Wertelisten bleiben unverändert.
- Beide produktiven GeoShop-Wege (Projektseite und einzelne Objektakte) nutzen
  `GeoShopAbgleichDialog`, den bestehenden Planer mit `mitVergleich: true` und
  `GeoShopGesicherteUebernahme`. Die alten Application-Aufrufe bleiben kompatibel.
- `GeoShopImportVergleich` erstellt über `GeoShopObjektaktenImport` einen leeren
  Entwurf; `GeoShopFeldWahl` vergleicht Bestandsfelder und verknüpfte Objektakten.
  Leerfelder sind vorausgewählt, Abweichungen abgewählt. Handwerte, auch bewusst
  leere, sind geschützt. Die frühere Ausnahme «Haltungslänge immer aus XTF» ist seit 23.09.2026
  aufgehoben: Die Länge der Kanalfirma bleibt, GeoShop ergänzt nur eine leere Länge.
  Abweichende Koordinatenpaare/Schachtmasse werden gemeinsam gewählt.
- Entscheidungen stehen additiv in `Project.Metadata["GeoShop.Vergleich.<Root-ID>"]`.
  Gleiche Lieferung und gleicher Bestand wiederholen keine erledigten Fragen.
  Neue Quellwerte oder geänderte Bestandswerte erscheinen erneut. Originalbelege
  derselben Quellidentität werden aktualisiert, auch bei gemeinsam referenzierten
  Objekten. DSS-Export berücksichtigt bewusst beibehaltene Werte und Koordinaten.
- `GeoShopAttributZuordnung` in Application/UseCases/Objektakten ergaenzt die
  Bestandsfelder anhand der belegten DSS-Ziele des Feldkatalogs. Der GeoShop-Leser
  verwendet sie nach dem Aufbau des Quellverbunds; QGIS behaelt seinen eigenen Filter.
  Ausdruecklich gelieferte Werte `unbekannt` und 0 bleiben erhalten. XTF-Masse werden
  nicht auf die zwei Nachkommastellen der QGIS-Anzeige gerundet. Ergaenzt sind Standort,
  Bruttokosten, Datenherr/Datenlieferant und Aenderungsdatum. Organisationsobjekte liefern
  den Namen; bei externen Verweisen bleibt die tatsaechlich gelieferte TID sichtbar.
  Die Zusatzfelder `schacht.geaendert_am`/`haltung.changed` erhalten das Quell-Datum;
  `haltung.aatype` erhaelt PAA/SAA aus `Kanal.FunktionHierarchisch`. Kein Typ-AA-Raten am Schacht.
- `GeoShopHaltungspunktImport` fuellt auch die vorhandenen Rechts-/Hochwertfelder aus
  `Haltungspunkt.Lage`. Vergleich und DSS-Koordinatenexport schuetzen gemeinsam behaltene
  Punktkoordinaten auch nach einem erneuten Import.
- `SchachtObjektId` uebernimmt eine ausdruecklich gelieferte OBJID/OBJECTID unter Erhalt
  fuehrender Nullen. Widerspruechliche Aliaswerte sperren das betroffene Objekt. Ohne
  eigene OBJECTID zeigt die Maske die Schachtbezeichnung mit erklaerendem Hinweis;
  vorhandene Kennungen und bewusst leere Handwerte bleiben erhalten. Diese Anzeige
  erzeugt keine neue Normkennung und veraendert keine XTF-TID.
- `GeoShopKoordinaten` liest vollständige LV95-Paare ohne Rundung aus `Lage`.
  `SchachtDeckelAnzeige` zeigt den ausdrücklich gewählten oder einzigen verknüpften
  Deckel; eine Hauptdeckelmarkierung wird dadurch nicht gesetzt. `SchachtHoehenRechnung`
  zeigt aus zwei vorhandenen Angaben die dritte direkt im leeren Feld: Tiefe = Deckel − Sohle,
  Deckel = Sohle + Tiefe, Sohle = Deckel − Tiefe. Berechnete Anzeigen werden nicht
  als Handwerte zurückgeschrieben; Originaldaten und bewusst geleerte Handfelder bleiben erhalten.
  Unklare Deckelwahl, ungültige Zahlen, negative Tiefe und Widersprüche über 1 mm
  werden gemeldet. Die Objektakte aktualisiert alle drei Felder nach Eingaben.
  Tests: `SchachtHoehenRechnungTests` und die Höhen-/Tiefenfälle in `ObjektakteUiTests`.
  Eine eindeutige Materialgruppe
  wird nur aus dem tatsächlich gewählten Material angezeigt; kein Fertigteil geraten.
- `IGeoShopSicherung` (Application) / `GeoShopSicherungsdatei` (Infrastructure),
  registriert in `ServiceProvider.GeoShop`/`ServiceProviderRegistrationMap`, speichern
  vor jeder Übernahme den vollständigen aktuellen Projektstand einschließlich
  ungespeicherter Angaben als neue geprüfte JSON unter
  `AppSettings.AppDataDir/GeoShop-Sicherungen`. Medien werden nicht kopiert.
  Sicherungsfehler verhindern die Übernahme; geänderter Projektstand sperrt den
  Plan. `GeoShopRuecknahme` setzt einen fehlgeschlagenen Schreiblauf vollständig zurück.
- `KatasterFeldschutz` in beiden Datensätzen und die Katasterpriorität im
  `MergeEngine` verhinderten bis 23.09.2026 das spätere Zurücksetzen durch Protokollimporte.
  ÜBERHOLT (Entscheid Pascal 23.09.2026 spät): Die Kanalfirma ist der Ist-Zustand und ersetzt
  Katasterwerte ohne Handmarke; gesperrt wird nur noch ein Schreibversuch unbekannter Herkunft,
  Kataster hat im `MergeEngine` die unterste Priorität. Handwerte bleiben geschützt. Ausnahme:
  vermessene Katasterkoordinaten ersetzt kein Import.
  Manuelle Korrekturen bleiben möglich. Beide Projektseiten aktualisieren nach
  erfolgreicher Übernahme die offene Objektakte und planen die vorhandene automatische Speicherung ein.
- Nicht alle WebGIS-Felder sind im DSS-XTF enthalten. Die Vergleichshinweise benennen
  nicht gelieferte/nicht belegbar zugeordnete Angaben. Die TID wird nicht als
  numerische WebGIS-OBJECTID ausgegeben. Quellen und Kundenprojekte werden bei Tests nicht verändert.
- Tests: `GeoShopRobusterImportTests`, bestehende GeoShop-/DSS-Tests und
  `GeoShopAbgleichUiTests` (isolierter WPF-Feldvergleich). Bedienung, Grenzen und
  Wiederherstellung: `docs/GEOSHOP-ABGLEICH-TEST.md`. Diese Regeln ersetzen die
  ältere reine Leerfeldbeschreibung der produktiven GeoShop-Dialoge.

## QGIS-Bruecke: eine leere Ebene geht nie ohne Spalten hinaus (11.09.2026)

Eine GeoJSON-Datei traegt keine eigene Spaltenliste — QGIS liest die Spalten aus den
Objekten. Bei `"features":[]` hat der Layer deshalb KEINE Spalten, und jede gespeicherte
Abfrage darauf scheitert (`geometrie_quelle not recognised as an available field`).
QGIS kann die Quelle dann nicht mehr oeffnen und meldet den Layer als „unsicher verortet".
Real aufgetreten am 11.09. an `SewerStudio_damages.geojson`: Ohne offenes Projekt schrieb
die Bruecke 115 Byte, und alle gefilterten Schaden-Ebenen wurden rot.

- `QgisLeerschema` (UI/QgisBridge) haelt je Live-Ebene die Spaltennamen und baut daraus
  eine einzelne Schemazeile: alle Spalten, alle Werte `null`, **keine Geometrie**.
  `QgisBridgeEndpointRouter.MitSpalten` setzt sie an genau einer Stelle ein — jede
  `GeoJsonFeatureCollection` mit null Objekten geht als Schemazeile hinaus. Eine
  unbekannte Ebene liefert weiterhin die leere Sammlung statt erfundener Spalten.
- `GeoJsonFeature.Geometry` ist dafuer `object?`. Eine Zeile ohne Geometrie wird nie
  gezeichnet; mit dem Geometrietyp-Filter des Layers (`|geometrytype=Point`) zaehlt sie
  auch nicht als Objekt. Nur ein Layer ganz ohne diesen Filter zeigt sie als leere Zeile
  in der Attributtabelle.
- Am echten QGIS 4.2 gemessen (nicht abgeleitet): leere Datei -> Layer ungueltig;
  Schemazeile -> `isValid=True`, `featureCount=0`, alle 19 Spalten vorhanden.
- Die Feldlisten in `QgisLeerschema` sind eine zweite Aufschreibung der Felder aus
  `QgisBridgeSnapshotBuilder`. `QgisLeerschemaTests` haelt sie je Ebene gegen ein echtes
  Objekt derselben Ebene — ein neues Builder-Feld ohne Schema-Eintrag macht den Waechter
  rot (Sabotageprobe bestanden). Nie eine dritte Feldliste anlegen.

## GeoShop-Abgleich aus Original-XTF (09.09.2026, Testfassung)

Der Menuepunkt `GeoShop-Abgleich (XTF)` ersetzt auf beiden Datenseiten die alten
Katasterkennungen. Die bestehenden Befehlsnamen bleiben kompatibel. `ServiceProvider.GeoShop`
registriert `IGeoShopLeser` / `GeoShopXtfLeser`. Der Leser arbeitet nur lesend mit
INTERLIS-2.3-Dateien der Modellfamilien DSS_2020_1_LV95 und SIA405_ABWASSER_2020[_1]_LV95.
Mehrere Durchlaeufe sammeln nur angefragte Namen/Gegenrichtungen und deren Objektverbund;
DTD und externe XML-Aufloesung sind gesperrt. Doppelte TIDs und defekte Kernverweise sperren die Uebernahme.

`GeoShopXtfZuordnung` nutzt vorhandene Fachvokabulare fuer Leerfelder. `GeoShopZiel`,
`GeoShopAbgleichPlanBuilder`, `GeoShopAbgleichAnwender` und `GeoShopAbgleichBericht`
in Application/UseCases trennen Planung, Bestandsschutz und Schreiben. Namen muessen
eindeutig sein; Gegenrichtungen tauschen die Haltungspunkte. Endschacht- oder Bauwerksart-
Widersprueche bleiben zur Pruefung offen. Gefuellte Fachwerte werden nie ersetzt.
Die bestaetigte Uebernahme ersetzt den Kennungsverbund im vorhandenen Geonis-Objekt
und zieht GEONIS_Kennung/Objekt_ID als Haupt-TID nach. Abweichende handgeschuetzte
Kennungsfelder sperren den Datensatz. Kein neues gespeichertes Format.

`GeoShopAbgleichDialog` und `GeoShopAbgleichWindow` bieten Dateiauswahl und eine
abbrechbare Vorschau. Vor dem Schreiben werden Projektbestand und Datensatzstand erneut
geprueft. Kein Kundenprojekt wurde zum Test veraendert. `Letzte_Aenderung` der XTF
ist kein GN_LAST_EDITED_DATE; GeonisGeaendert wird daher nicht erfunden. Der produktive
FME-Rueckweg bleibt unbestaetigt. Anleitung, Feldumfang und Grenzen: `docs/GEOSHOP-ABGLEICH-TEST.md`.

Seit 11.09.2026 gibt es denselben Abgleich auch je Bauteil: In der Objektakte unter Mehr
«Fehlende Felder aus GeoShop-XTF» (`GeoShopEinzelErgaenzung` ruft Planer und Anwender mit EINEM
Ziel; `GeoShopEinzelErgaenzungDialog` haelt Datei, Ja/Nein und Hintergrundlesen; die XTF wird
in `AppSettings.GeoShopXtfPath` gemerkt). **Die Haltungslaenge kommt immer aus der XTF**
(`GeoShopAbgleichPlanBuilder.ImmerAusXtf`, Entscheid Pascal): auch ein Handwert wird ersetzt und
als Katasterwert markiert; alle anderen Felder nur, wenn leer. Nie einen zweiten Planer fuer
den Einzelweg bauen. **UEBERHOLT am 23.09.2026** (Entscheid Pascal: Haltungslaenge der
Kanalfirma bleibt, GeoShop ergaenzt nur leere Felder) — umgesetzt, `ImmerAusXtf` ist entfernt.

## Redesign-Feldkorrekturen (08.09.2026)

- `DataPageRecordDetailsBuilder` erzeugt Schacht oben/unten auch bei neuen Haltungen.
  Die feste CSV-/Excel-Spaltenfolge bleibt erhalten. SIA405-Felder sind ihren
  Fachgruppen zugeordnet; Eigentümer steht zusätzlich in der kompakten Tabelle.
- `SchaechteColumnPolicy` verbindet Funktion, Material, Status und Sanierungsbedarf
  mit festen Auswahllisten. `SchachtNormoptionen` filtert die Funktion in Tabelle
  und Formular nach Bauwerksart. `SiaBegriffAnzeige` ändert nur die Darstellung,
  niemals den ausgewählten Normwert. Altwerte ausserhalb der Liste bleiben
  gespeichert und werden mit einem Hinweis sichtbar gemacht.
- `SiaAbmessung.AusMillimeterfeld` verwendet für die getrennten Schachtfelder
  Millimeter ohne Altwert-Heuristik. Ausdrückliche Einheiten haben Vorrang.
  `SchachtmassFehler` prüft dieselbe Obergrenze (4000 mm) für Eingabe und Export.
  Ungültige getrennte Masse fallen nicht auf einen alten Ersatzwert zurück;
  beide Masse fehlen dann mit Hinweis im Export. Reale grössere Bauwerke müssen
  fachlich passend erfasst werden, nicht durch gekürzte Masse.
- `SchachtFunktionVokabular` schreibt Fettabscheider jetzt zeichengenau. Die
  frühere Verallgemeinerung zu andere ist mit diesem Korrekturauftrag aufgehoben.
- Der neue Schreiber deklariert `SIA405_ABWASSER_2020_LV95`, Version 29.11.2025,
  mit `SIA405_Base_Abwasser_LV95`, Version 03.11.2020. Die Modellfamilie bleibt
  2020; sie darf nicht mit 2020_1 und deren neuerem Basismodell verwechselt werden.
- `ExportPageViewModel.XtfVollstaendig` ist standardmässig false: der FME-Abgleich
  liefert weiterhin Handänderungen samt Feldaufträgen. Der bewusste Erstexport
  liefert reine SIA405-Angaben ohne Zusatzmodell, auch ohne Importvorlage.
  `XtfNeuExportRequest.MitZusatzangaben` ist additiv, Standard true für bestehende
  Aufrufer. Bei Änderungsaufträgen bleibt das Zusatzmodell immer erforderlich.
- ilivalidator 1.15.0: synthetischer Erstexport, Zusatzexport und Änderungsabgleich
  bestehen gegen die festgehaltenen offiziellen Modelle. Ohne auflösbares
  Zusatzmodell scheitert die Zusatzdatei erwartungsgemäss. Das ersetzt keine
  Empfängerabnahme eines konkreten GEONIS-/FME-Abgleichs.
- Nachweise und Prüfumfang: `docs/reviews/2026-09-08-redesign-sia405/BEHEBUNG.md`.

## (Fortsetzung aus «Begleitprotokolle der Sanierung und Videodopplung (Bürglen, 09.09.2026)»)

Der Export `IXtfRevisionExportService`/`XtfRevisionExportService` erzeugt aus den
unveraenderten Projektkopien unter `Imports\XTF` beziehungsweise
`Importdateien\XTF` und dem aktuellen Projektstand neue revidierte XTF-Dateien.
Fehlt eine solche Projektkopie, laesst `ExportPageViewModel` fuer diesen Lauf eine
oder mehrere externe XTF-Dateien waehlen. `XtfRevisionExportRequest.Quelldateien`
wird vor dem Lauf auf Existenz und Endung geprueft, normalisiert und dedupliziert;
dieselbe Auswahl gilt fuer Pruefung und Schreiben und bleibt immer rein lesend.
Gleichnamige Projektkopien werden nur bei belegtem gleichem SHA-256 einmal verwendet;
unterschiedlicher Inhalt stoppt mit beiden Pfaden, statt still die erste Datei zu nehmen.
Eine Vorschau mit offenen Entscheidungen liefert `Ok=false`; die UI darf danach
nicht nach einer Schreibbestaetigung fragen.
`VsaFinding` traegt dafuer additiv Kanalschaden- und Untersuchungs-TID;
`HaltungRecord` bewahrt die importierte `XtfHerkunft`. Eine separate Herkunftsklasse
am `SchachtRecord` gibt es nicht: `LegacyXtfImportService` bewahrt die TID von Haltung
und Normschacht als `Objekt_ID`, denn in SIA405 ist die TID die Katasteridentitaet.
Altprojekte werden nicht neu importiert: `XtfKanalschadenElementReader` und
`XtfFindingMatcher` bilden nur beidseitig eindeutige Zuordnungen im Arbeitsspeicher.
`XtfRevisionPlanBuilder` plant geaenderte, neue und entfernte Befunde;
`XtfStammdatenPlanBuilder` nimmt nur eindeutig zugeordnete, vom Menschen bearbeitete
Felder auf: am `Kanal` `Nutzungsart_Ist`, `BaulicherZustand`, `FunktionHierarchisch`,
`FunktionHydraulisch`, `Verbindungsart`, `Bettung_Umhuellung`, `Status`,
`Sanierungsbedarf`, `Baujahr`, `Bruttokosten` und `Bemerkung`; an der `Haltung`
`Material`, `Lichte_Hoehe`, `LaengeEffektiv` und `Lagebestimmung`; am verwiesenen
`Rohrprofil` den `Profiltyp`. Das ist die Feldliste der Kataster-Infobox von geo.ur.ch.
`XtfSchachtPlanBuilder` tut dasselbe fuer den `Normschacht` (`Funktion`, `Material`,
`Dimension1`/`2`, `BaulicherZustand`, `Bemerkung`, `Status`, `Sanierungsbedarf`, `Baujahr`) — Schaechte kommen seit 2026-08-30
aus der XTF und gehen seit 2026-09-02 auch wieder hinaus. Offene Faelle sperren den
Schreibweg. `XtfRevisionWriter` wendet nur den geprueften Plan an, veraendert das
Original nie, ueberschreibt kein Ziel und veroeffentlicht jede Revision ueber eine
Nebendatei. `ExportPageViewModel` zeigt zuerst den Pruefbericht und schreibt erst
nach ausdruecklicher Bestaetigung in einen neuen Zeitstempelordner.

Fuenf Regeln dieses Wegs nie zurueckdrehen:

- **Die Bemerkung ist `TEXT*80` und einzeilig.** `XtfStammdatenPlanBuilder.AlsBemerkung`
  macht Umbrueche und Tabulatoren zu Leerzeichen und zieht mehrfache zusammen —
  `TEXT` ist in INTERLIS einzeilig, mehrzeilig waere `MTEXT`. Ueberlaenge wird dagegen
  NICHT gekuerzt, sondern abgelehnt; der Bericht nennt Haltung beziehungsweise Schacht
  und die Zeichenzahl. Kuerzen verloere Inhalt unsichtbar: Im Programm staende der ganze
  Satz, in der Datei der halbe. Genau so kappt der Kantonsexport heute — seine laengste
  Bemerkung ist exakt achtzig Zeichen lang und endet mitten im Wort. Am Schacht laeuft
  die Bemerkung bewusst VOR der `unbekannt`-Regel heraus: Bei `Funktion` und `Material`
  ist das eine Leerformel, in einem Freitext eine Aussage.

- **Sieben Felder bleiben bewusst im Programm.** `XtfStammdatenPlanBuilder.NichtExportierteFelder`
  fuehrt sie namentlich, ein Test haelt die Liste gegen die Exportkarten: `Strasse`
  (haette mit `Kanal.Standortname` ein Ziel — Entscheid 2026-09-02) sowie die sechs
  Herkunftsangaben `Objekt_ID`, `Datenherr`, `Datenlieferant`, `Organisation`,
  `Letzte_Aenderung` und `Aktualisierungsdatum`. Der Datenherr einer Kantonsleitung ist
  der Kanton, nicht der Operateur; `Letzte_Aenderung` fuehrt der Schreiber ohnehin
  selbst nach.
- **Die Breite einer Haltung geht als Verhaeltnis ans Rohrprofil** (seit 2026-09-03,
  Entscheid Pascal: Haltungen haben zwei Masse wie Schaechte). Die Haltung kennt in
  SIA405 nur `Lichte_Hoehe`; `Rohrprofil.HoehenBreitenverhaeltnis` (Hoehe geteilt durch
  Breite, 0.00001 bis 100, in 2020 und 2020_1 gleich) traegt die zweite Dimension.
  `XtfRohrprofilVerhaeltnis` rechnet hin und zurueck: `DN_mm` ist die Hoehe,
  `Lichte_Breite_mm` die Breite. Rund heisst Breite leer oder gleich; ein Kreisprofil
  traegt dann seit 2026-09-04 das Verhaeltnis `1` (Wunsch Trigonet: GEONIS fuehrt die
  Breite neben der Hoehe und rechnet sie daraus; ohne den Wert bliebe sie leer). Zwei
  verschiedene Masse ohne Profiltyp oder mit `Kreisprofil` werden gemeldet, nicht
  geraten. Erstexport: ein Rohrprofil je Profiltyp UND Verhaeltnis
  (`Rechteckprofil 1.666`), vom ilivalidator angenommen. Revision: Hoehe oder Breite
  von Hand zaehlt als Aenderung am Profil, ein geteiltes Profil bleibt unangetastet.
  Der Wechsel auf rund setzt ein vorhandenes altes
  `HoehenBreitenverhaeltnis` auf `1` und ergaenzt ein fehlendes (frueher wurde es mit
  `XtfRevisionFeldAktion.Entfernen` geloescht; die Aktion bleibt fuer den Writer
  erhalten). Eine leere Breite gilt nur dann als bewusste
  Rund-Angabe, wenn genau dieses Breitenfeld von Hand bearbeitet wurde; eine bloss
  geaenderte Hoehe oder ein geerbter Leerwert schreibt nie XML. Wird nur der Profiltyp
  auf Kreis gesetzt, setzen konsistente Masse das Verhaeltnis auf `1`; zwei verschiedene
  Masse sperren Abmessung und Profil gemeinsam, statt eine halbe Aenderung mit
  Kreis und Altverhaeltnis zu schreiben. Unabhaengige Haltungsfelder duerfen bleiben.
  Import: `RohrprofilRef` wird aufgeloest, `Profiltyp` uebernommen, Breite = Hoehe /
  Verhaeltnis; beim Kreisprofil ist die Breite gleich der Hoehe. Im Bestand fuehren
  alle 110887 Kantonsprofile `Kreisprofil` ohne Verhaeltnis, und keine der 477
  Projekt-Haltungen trug eine Breite oder einen Profiltyp; das aendert sich erst mit
  echten Rechteck- und Eiprofilen.
- **Die Profilform-Auswahl folgt Uri, der Export dem aktuellen Modell.**
  `ProfiltypVokabular` zeigt `Unbekannt`, `Kreisprofil`, `Eiprofil`, `Maulprofil`,
  `Offenes Profil`, `Rechteckprofil`, `Spezialprofil`. Die alte Uri-Auswahl
  `Anderes (A)` ist in SIA405 2020 aufgehoben; alte Werte werden beim Laden auf
  `Spezialprofil` angehoben. `Offenes Profil` geht zeichengenau als
  `offenes_Profil` in die XTF. In der Haltungsansicht stehen lichte Hoehe/DN,
  Profilform und lichte Breite direkt nebeneinander.
- **Der Eigentuemer ist ein Verweis, kein Text.** `XtfOrganisationsbuch` bindet ihn an
  eine `Organisation` im Topic `Administration` und legt fehlende an; Haltungen und
  Schaechte teilen sich EIN Buch je Datei. Fuehrt die Datei ueberhaupt keine
  Organisation, wird auch keine erfunden. Ohne bekannten `Organisationstyp` (Pflichtfeld)
  entsteht nichts. `Abwasser Uri` ist ein **Abwasserverband**, kein Kanton. Der Name
  geht zeichengleich hinaus — die Faltung in `EigentumVokabular` dient nur dem
  Typvergleich.
- **Der `Profiltyp` haengt am `Rohrprofil`**, auf das die Haltung ueber `RohrprofilRef`
  zeigt. Ein von mehreren Haltungen geteiltes Profil wird nicht geaendert.

Die Exportseite spricht seit 2026-09-03 Klartext: **„Bestehende Katasterdaten aktualisieren"**
(technisch Revision) und **„XTF erstellen"**. Dort ist der Änderungsabgleich Standard;
„Vollständiger Erstexport (reine SIA405-Datei)" muss bewusst gewählt werden.
`XtfExportAuswahl` (`Application/UseCases/Xtf`, reine Rechnung) entscheidet aus den
Importkopien des Projekts (`IXtfRevisionExportService.FindeProjektkopien`, dieselbe Suche wie
beim Schreiben), welcher Weg das Abzeichen „empfohlen" traegt: mit Kopie Aktualisieren
(sonst Duplikate im Kataster), ohne Kopie Neu (dann gibt es nichts zu aktualisieren). Die
Zeile „Original: <Datei> — Importkopie vom <Datum>" steht vor dem Start; fehlt die Kopie,
sagt sie das, und der Lauf fragt beim Start nach der Datei. Nach dem Schreiben zeigt
„Ordner oeffnen" den Ausgabeordner ueber `IExplorerRevealService`. Die XTF-Logik des
ViewModels liegt in `ExportPageViewModel.Xtf.cs` (Hauptdatei bleibt unter 1000 Zeilen).

Der Ablauf selbst liegt seit Schritt 2 in `XtfAktualisierenUseCase` und
`XtfNeuErstellenUseCase` (`Application/UseCases/Xtf`, Request/Actions/Ergebnis): pruefen,
bei fehlender Kopie die Original-XTF erfragen, Vorschau bestaetigen lassen, erst dann
schreiben. Eine gescheiterte Pruefung zeigt den Fehler und fragt NIE nach Bestaetigung;
vor der Bestaetigung wird nie geschrieben (Tests `XtfExportUseCaseTests`). Die Vorschau
`XtfExportVorschau` ist reine Darstellung des `XtfRevisionPlan`: eine Zeile
„3 Objekte geaendert · 0 neu · 0 entfernt", eine Tabelle Objekt / Feld / Original /
Neuer Wert (Feldnamen in Klartext, `Dimension1`+`Dimension2` als eine Zeile
„500 x 500 -> 1100 x 900", Entfernen sichtbar), hoechstens drei sichtbare Warnungen,
der ganze Bericht unter „Details anzeigen". Dafuer traegt `XtfRevisionExportResult`
additiv `Plaene`, und `XtfRevisionPosition` das Feld `Objekt` („Haltung"/„Schacht";
Befunde bleiben leer und heissen „Befund <Code> bei <m> m"). Das Fenster
`XtfExportVorschauWindow` zeigt im Fehlerfall dieselbe Anordnung rot mit nur
„Schliessen"; `IXtfExportVorschauDialog` ist im ServiceProvider registriert. Das
ViewModel enthaelt keinen eigenen Ablauf mehr — es leiht dem UseCase Dateiwahl und
beide Fenster.

`IXtfNeuExportService`/`XtfNeuExportService` kann eine eigenständige XTF aus den
SIA405-Angaben des ganzen Projektstands oder eine Lieferung der Handänderungen erzeugen. Der Revisionsweg aktualisiert dagegen eine Originaldatei an ihren
echten XTF-TIDs. Eine einzelne `Objekt_ID` reicht nicht fuer die Kennungen von Kanal,
Haltung, Punkten, Knoten und Profil; sie verhindert den vollstaendigen Neu-Export deshalb
nicht. `XtfNeuPlanBuilder` (reine
Rechnung) baut den vollstaendigen SIA405-Verbund je Haltung: `Kanal` (logisch),
`Haltung` (physisch), `Rohrprofil` und ZWEI
`Haltungspunkt`e; je Schacht `Normschacht` und `Abwasserknoten`. `XtfNeuWriter` setzt den
Plan in XML um und entscheidet nichts. Es gilt dasselbe Vokabular wie beim Revisionsweg —
kein zweiter Uebersetzer.

Sechs Regeln dieses Wegs nie zurueckdrehen:

- **Die Objektkennungen sind stabil.** Sie werden aus Projekt-Id, Klasse und fachlichem
  Schluessel abgeleitet (SHA-256, Praefix `chSST`, 16 Zeichen). Waeren sie zufaellig oder
  ein Zaehler, legte das Zielsystem bei jedem Export neue Objekte an — aus einer Korrektur
  wuerde eine Verdopplung. Das gilt fuer Objekte, die mit einer frueheren SewerStudio-XTF
  angelegt wurden; ein schon fremd vorhandenes Katasterobjekt wird dadurch nicht erkannt.
- **Haltungspunkte heissen nach der HALTUNG, nicht nach dem Schacht.**
  `Haltungspunkt.Constraint1` verlangt Eindeutigkeit von Bezeichnung plus Datenherr. In
  einer Kette 1-2, 2-3 teilen sich Nachbarhaltungen ihre Schaechte; nach ihnen benannt,
  weist der ilivalidator die ganze Datei ab (real passiert, 2026-09-03). Der
  Kantonsexport macht es aus demselben Grund so. Bei Ueberlaenge (`TEXT*20`) wird gekuerzt
  und durchnummeriert — die fachliche Zuordnung traegt der Verweis auf den Abwasserknoten,
  nicht der Text.
- **Drei Verweise sind Pflicht ({1}):** `DatenherrRef`, `DatenlieferantRef` und am
  Abwasserbauwerk `EigentuemerRef`. Ohne bekannten Eigentuemer entsteht das Objekt NICHT.
  Gesetzte Projektwerte fuer Datenherr und Datenlieferant gewinnen; nur ein leeres Feld
  faellt auf den Eigentuemer zurueck. Ein gesetzter Name ohne bekannten
  `Organisationstyp` sperrt das Bauteil mit Hinweis, statt still eine andere Organisation
  einzutragen. Der Bericht verweist auf "Leere Felder aus QGIS ergaenzen".
- **`Organisation.Status` ist MANDATORY** (`aktiv` | `untergegangen`). Fehlt es, weist der
  Pruefer die ganze Datei ab.
- **Eine vorhandene Objekt-ID sperrt den Neu-Export nicht.** Haltung und Schacht erhalten eigene
  stabile `chSST`-Kennungen und der Bericht warnt sichtbar: Beim Import in einen bereits
  gefuellten Kataster koennen Duplikate entstehen. Fuer eine echte Aktualisierung ist
  `Revidierte XTF` mit der Originaldatei der sichere Weg.
- **Die Geometrie kommt aus der QGIS-Kopie**, ueber `IXtfVerlaufQuelle`/
  `QgisGpkgVerlaufLeser` und die reine Byte-Logik `GpkgGeometrie` (GeoPackage-Kopf plus
  WKB, LineString und MultiLineString, EPSG:2056). Ein mehrdeutiger Name liefert nichts.
  `Verlauf` ist im Modell nicht Pflicht: Ohne Treffer geht das Objekt ohne Geometrie
  hinaus, und der Bericht sagt es.

### XTF-Aenderungslieferung und Bauwerksarten (2026-09-07)

Im FME-Exportbereich ist `XtfNurAenderungen` fest true und nicht abschaltbar.
Die UI reicht fuer Vorschau und Schreiben `XtfNeuExportRequest.NurAenderungen=true`
weiter. Es gibt dort keine Vollexport-Auswahl. Der optionale API-Parameter bleibt
fuer bestehende interne Aufrufer standardmaessig false. Der separate Revisionsweg
mit Kundenoriginalen ist unveraendert und ist keine FME-Aenderungslieferung.

`XtfBauwerkFelder` schreibt die gemeinsamen und klassenspezifischen Standardfelder.
`AbwasserbauwerkVokabular` trennt Bauwerksart von Funktion: Normschacht,
Spezialbauwerk, Versickerungsanlage, Einleitstelle. Unbekannte explizite Arten werden
gemeldet statt als Normschacht geraten. Ohne Typfeld werden Sickerschacht,
Spezialbauwerk und Einleitstelle anhand eindeutiger Funktionsbegriffe erkannt;
sonst bleibt Normschacht der Rueckfall. Die Schachtmaske bietet Bauwerksart und
Versickerungsart an. Standort, Bruttokosten und parsebares Untersuchungsjahr haben
Standardfelder. Keine Normschacht-Material-/Dimensionsfelder an falschen Klassen.

`XtfZusatzangaben` liefert freigegebene Sachwerte ohne verwendetes Standardfeld,
darunter Schachtform, Tiefe, Massnahmen, Schadenstext und volles Inspektionsdatum.
Pfade und interne IDs werden nicht als Sachwerte ausgegeben. Haltungsprofil und
Breite koennen zusaetzlich als direkte Werte geliefert werden. Die XML-Klassen
`SewerStudio_Zusatz_2026.Zusatzdaten.Zusatzangabe` und `.Aenderung` liegen in derselben
XTF. Das eingebettete Zusatzmodell wird als `.ili` daneben geschrieben; ein bereits
vorhandenes anderes Modell wird nicht ersetzt. FME muss dieses eigene Modell kennen.

`XtfAenderungsPlanBuilder` plant nur handmarkierte, nichtleere und lieferbare Felder
(`FieldMetadata.UserEdited`, `LastUpdatedUtc`). Jeder Auftrag enthaelt `ObjektTid`,
`Feld` und `GeaendertAm` in UTC; Zusatzfelder heissen im Auftrag `Zusatz:<Feld>`.
Uebrige Standardattribute werden bis auf Pflichtnamen entfernt. Benoetigte
Referenzobjekte bleiben Kontext, ohne Schreibberechtigung. Doppelte Objekt-TIDs
sperren die Aenderungslieferung. Das ist ein eigener feldweiser Liefervertrag,
kein INTERLIS-Inkrementaltransfer mit `xtf_operation`.

Ein Export setzt keine Handmarkierung zurueck. Es gibt noch keinen bestaetigten
GEONIS-Vergleichsstand, keine Erfolgsquittierung und keinen Loeschauftrag. Daher
koennen bereits frueher exportierte Handaenderungen erneut vorkommen. FME darf
genau die beauftragten Felder an eindeutig gefundenen bestehenden `SIA405_ID`s
aktualisieren; identische Werte bleiben unveraendert. Der Exporttag in
`Letzte_Aenderung` ist nicht der GEONIS-Quellstand `GN_LAST_EDITED_DATE`.

`LegacyXtfImportService` liest alle vier Bauwerksarten. `XtfZusatzReader` nimmt nur
bekannte Zusatzfelder mit eindeutigem Dateiziel an; Standardfelder haben Vorrang,
Handwerte bleiben geschuetzt. Form, Typ, Material und Masse sind durch Rundreise-
und Negativtests abgesichert. Nachweise und FME-Vertrag liegen unter
`Ausgaben/XTF_FME_Aenderungen_2026-09-07/`; Produktiv-GEONIS und FME sind nicht getestet.

Der automatische Rundreisetest schreibt eine echte neue SIA405-XTF mit Haltung,
Schaechten, Profil, Organisationsverweisen und Geometrie und importiert sie wieder.
Er vergleicht dabei die Katasterfelder, beide Masse und die erhaltenen TIDs.
Der Seilergasse-Verhaltenstest bildet den gemeldeten Fall nach: Haltung `78998-79002`
mit Objekt-ID und Schacht `78998` ergeben eine neue Datei mit einer Haltung und
einem Schacht. Die Revision aendert Kanal, Haltung und Normschacht weiterhin an ihren
Original-TIDs. Der technische Neu-Export wird vom ilivalidator 1.15.0
mit null Fehlern akzeptiert. Der originalgetreue Revisionsausschnitt behaelt exakt die
zwei bereits in der Quelle vorhandenen, ungueltigen Werte `Beton_unbekannt` und fuegt
keinen neuen Validatorfehler hinzu; der Revisionsweg repariert fremde Ausgangsdaten
nicht stillschweigend.

Der Rueckweg ueber `LegacyXtfImportService` war dabei an zwei Stellen kaputt, beide auch
fuer Kantonsdateien:

- **`BaulicherZustand` wurde gar nicht gelesen.** Die nachlaufende VSA-Bewertung fand in
  einer Stammdaten-XTF keine Befunde und setzte "Leitung i.O." (Klasse 4) — aus einem
  exportierten `Z0` wurde beim Zurueckimportieren eine `4`. Der Import uebernimmt den Wert
  jetzt als `FieldSource.Xtf405`, und `VsaEvaluationService.ApplyRecordFields` laesst ihn
  stehen, solange KEIN bewertbarer Befund vorliegt. Mit Befunden rechnet SewerStudio
  weiterhin selbst. Entscheid Pascal 2026-09-03: Beim Import gewinnt die Datei — nur so
  sind GEONIS und SewerStudio nach einem Austausch identisch.
- **Am Schacht wird die Zustandsklasse NIE berechnet** (Entscheid Pascal 2026-09-05).
  Bei der Haltung rechnet `VsaEvaluationService` aus den Befunden; am Schacht setzt sie
  die Fachperson von Hand. Ein Import darf allenfalls einen Wert uebernehmen, den die
  Quelle ausdruecklich nennt (SIA405 `BaulicherZustand`, WinCan `Condition`), aber
  niemals selbst einen aus Schachtschaeden ableiten. Der VSA-KEK-Schachtimport legt die
  Schaeden deshalb nur als Protokoll ab; Waechter:
  `AlteVsaKekSchachtImportTests.SchachtbegehungBerechnetKeineZustandsklasse`.
- **Die Schachtmasse leben nur noch in `Dimension 1 mm` / `Dimension 2 mm`** (Entscheid
  Pascal 2026-09-03: rund = 600 / 600, oval = 1100 / 900). `SchachtMasse` in
  `Application/Schacht` ist die eine Regel dafuer: Sie liest die alten Texte ("600 mm",
  "1100 x 900 mm", "0.60/1.00"), schreibt beide Felder unter der Schreibweise des
  Datensatzes und stellt Bestandsprojekte beim Laden um (`JsonProjectRepository.Load`,
  markiert das Projekt als geaendert). Die alten Textfelder `Dimension` und
  `Durchmesser` werden dabei entfernt; nur ein unlesbarer Text bleibt sichtbar stehen.
  Ist erst eines der zwei Zahlenfelder vorhanden, wird das fehlende nur ergaenzt, wenn
  das vorhandene Mass zur entsprechenden Seite des Alttexts passt. Bei Widerspruch bleibt
  der Alttext zur Kontrolle stehen; eine alte Handmarkierung auf einem leeren Zielfeld
  blockiert die sichere Ergaenzung nicht.
  PDF-, WinCan-, SchachtPro-, XTF-Import, QGIS-Nachfuellen und der Stammdaten-Nachlauf
  schreiben alle die zwei Zahlen. Anlass: 61 von 392 Schaechten trugen nur den Text,
  2 die Zahlen, und Export und Anzeige zeigten verschiedene Werte.
  `XtfSchachtPlanBuilder.Masse` liest den Text nur noch als Rueckfall fuer ein Projekt,
  das nie ueber `Load` gegangen ist. Ist nur eines der zwei Felder gefuellt, gilt der
  Schacht als rund und der Wert steht in beiden. `Schachtform` bleibt ein eigenes Feld
  (220 Schaechte tragen eine Form, 160 davon ohne Mass); SIA405 hat dafuer kein Ziel.
- **SIA405 kennt am `Normschacht` keine Form.** Ein ovaler Schacht ist dort einer mit
  zwei verschiedenen Massen. Das Programmfeld `Schachtform` geht deshalb nicht in die
  Datei; `Formwiderspruch(...)` meldet nur, wenn Form und Masse sich widersprechen
  ("Rund" bei 1100 x 900).
- **Die Schachtform-Auswahl folgt Uri:** `Unbekannt`, `Rund`, `Oval`, `Quadratisch`,
  `Rechteckig`, `Vieleckig`. `SchachtformVokabular` hebt bekannte WinCan-,
  SchachtPro- und Bestandswerte (zum Beispiel `rund`, `circular`, `polygonal`) auf
  diese Schreibweise. `SchaechteColumnPolicy.ErgaenzeFormUndMasse` stellt sicher,
  dass Schachtform, groesstes Innenmass und kleinstes Innenmass auch mit einer alten
  Excel-Vorlage editierbar bleiben.
- **Der `Normschacht` kennt beim `Material` nur vier Werte** (andere, Beton, Kunststoff,
  unbekannt) — eine viel kuerzere Liste als beim Rohr. `SchachtMaterialVokabular` bildet
  zehn Programmbegriffe darauf ab; ein Waechter haelt fest, dass jeder waehlbare Wert ein
  Ziel hat. Importierte Fremdwerte wie "Steinzeug" stehen nicht im Dropdown und werden
  beim Export namentlich gemeldet.
- **Schachtfelder muessen ueber `SchachtFeldnamen` gelesen werden.** Sie heissen nach
  der Kopfzeile der Excel-Vorlage: Der Eigentuemer steht dort unter `Eigentümer` mit
  Umlaut, `FieldKeys.Owner` lautet aber `Eigentuemer`. Beide Exportwege griffen direkt
  auf den Katalognamen zu und fanden nichts — und weil der Eigentuemer in SIA405 Pflicht
  ist, fiel dadurch JEDER Schacht aus dem Export. `XtfSchachtPlanBuilder.Wert(...)` und
  `IstHandgesetzt(...)` sind der gemeinsame Weg; direkt `record.GetFieldValue(...)` auf
  einem `SchachtRecord` ist im XTF-Kontext ein Fehler.
- **Der Eigentuemer wurde nie aufgeloest.** In SIA405 ist er ein Verweis auf eine
  `Organisation` im Topic `Administration`, kein Text. Der Import suchte nur nach einem
  Element `Eigentuemer` und fand in einer normkonformen Datei nichts — ausgerechnet die
  Angabe, die der Export zwingend braucht (`EigentuemerRef` ist `{1}`). Ohne sie kam kein
  einziger Schacht aus dem Projekt heraus. Beide Wege lesen jetzt die Organisationen der
  Datei und loesen den Verweis auf.
- **Katasteridentitaet und Verwaltungsrollen bleiben erhalten.** Der Import schreibt die
  TID von `Haltung` und `Normschacht` nach `Objekt_ID` und loest `DatenherrRef` sowie
  `DatenlieferantRef` getrennt auf. Ein leeres altes Textelement darf einen gueltigen
  Organisationsverweis nicht verdecken. `unbekannt` bleibt als echter Freitext und als
  Organisationsname erhalten; nur semantische Platzhalterfelder wie Funktion, Material
  oder Zustand behandeln es als leer. Damit fuehrt ein Rueckimport nicht spaeter zu einer
  Doppelanlage im Erstexport.
- **Am `Normschacht` fehlte `BaulicherZustand` ebenso** wie am Kanal.
- **`ResolveSchachtLabel` nahm zuerst die Bezeichnung des Haltungspunkts.** Die ist ein
  technischer Name (`u-80401_von` im Kantonsexport, `<Haltung>_von` bei uns) und landete
  so in `Schacht_oben`. Jetzt gilt zuerst der `Abwasserknoten` — er IST der Schacht —,
  danach der Haltungsname (`78998-79002_nach` bei Haltung `78998-79002` ergibt `79002`),
  und erst zuletzt die Bezeichnung selbst. Das benachbarte `ResolveKnotenName` machte es
  immer schon richtig herum.
- **Sieben weitere Kanalfelder wurden nie gelesen** (2026-09-03): `Status`,
  `Sanierungsbedarf`, `FunktionHydraulisch`, `Verbindungsart`, `Bettung_Umhuellung`,
  `Bruttokosten` und an der Haltung `Lagebestimmung`. Der Export schrieb sie, der Import
  warf sie weg. `FunktionHierarchisch` fehlte sogar in genau der Schreibweise des
  Modells (gelesen wurden nur `Funktionhierarchisch` und `Funktion_hierarchisch`), und
  jeder Wert wurde auf `PAA.` umgeschrieben — ein `SAA.`-Wert ging dabei verloren.
- **`Letzte_Aenderung` ist kein Inspektionsdatum.** Es landete in `Datum_Jahr` und
  ueberschrieb dort den echten Aufnahmetag: Aus dem 06.10.2025 wurde der 03.09.2026.
  Jetzt geht es nach `Letzte_Aenderung` (Herkunftsfeld), `Baujahr` nach `Baujahr`.
- **Der Neu-Export schreibt auch Datensaetze mit `Objekt_ID`.** Diese Nummer stammt aus
  QGIS oder einem frueheren XTF-Import. Eine einzelne ID wird nicht als Kennung des
  ganzen SIA405-Objektverbunds missverstanden. Die Datei bekommt eigene Kennungen;
  der Bericht warnt vor moeglichen Duplikaten im vorhandenen GEONIS. `Datenherr` und
  `Datenlieferant` kommen aus ihren eigenen Feldern statt pauschal vom Eigentuemer.
- **WinCan: Zwei Untersuchungen je Haltung waehlen nach glaubwuerdigem Datum.** Der
  Vorgabetag `2007-12-31` und alles vor 1990 zaehlen als Platzhalter; dann entscheidet
  der Zeitstempel des Datensatzes. In Seilergasse (`07.638905-78998`) gewann sonst die
  Untersuchung mit 4 Befunden gegen die mit 12, und 9 Fotos und 1 Video fehlten still
  bei "0 Fehler". Eine uebersprungene Untersuchung erscheint jetzt namentlich im
  Importbericht. Das Platzhalterdatum darf nur die Auswahl steuern und wird selbst nie
  nach `Datum_Jahr` geschrieben; auch der technische Datensatz-Zeitstempel wird nicht zum
  erfundenen Inspektionsdatum. Im Bericht heisst ein solcher Wert ausdruecklich
  `WinCan-Platzhalterdatum`, nicht Aufnahmedatum.

Was im Programm waehlbar ist, muss auch in die Datei gelangen koennen.
`DropdownExportierbarkeitTests` prueft jeden Eintrag jeder Auswahlliste, die nach SIA405
fuehrt, gegen `NachXtfWert`. Ein neuer Wert ohne Ziel macht den Waechter rot und muss
entweder einen Normwert bekommen oder namentlich als Ausnahme eingetragen werden.

Zwei Ausnahmen sind belegt und bleiben waehlbar: `GFK` und `Guss`. Das WebGIS von Uri
fuehrt beide (GFK als Kunststoffart, Code 1001; Guss als Gruppe ueber duktil und
Grauguss), beide ohne `NORM_CODE` — SIA405 kennt sie nicht. Ein leerer `NORM_CODE`
heisst also NICHT "kein offizieller Begriff", sondern nur "kein Gegenstueck in der
Norm"; diese Verwechslung hat am 2026-09-03 fast dazu gefuehrt, `GFK` aus der Auswahl
zu werfen. Der Export meldet solche Werte stattdessen namentlich im Bericht.

Die Zustandsklasse bietet seit 2026-09-03 nur noch `0` bis `4` an. Die fruehere `5`
gibt es in SIA405 nicht und kam in 21 Projekten kein einziges Mal vor.

Das Material fuehrt Uri im WebGIS zweistufig: erst die Gruppe (Unbekannt, Beton, Stahl,
Kunststoff, Guss, Andere), dann die Art. SewerStudio kennt bisher nur die Art — die
Gruppe laesst sich daraus ableiten, wenn sie einmal gebraucht wird.

Wertelisten, Messwerte und die belegten Fallen stehen in
`docs/SIA405-2020-Wertelisten.md`.

`Leere Felder aus QGIS ergaenzen` ist der Gegenweg dazu: je ein Knopf auf der
Haltungs- und der Schachtseite fuellt LEERE Felder aus den lokalen QGIS-Kopien
(`IQgisBestandLeser`/`QgisGpkgBestandLeser`, GeoPackage = SQLite, offline). Er
laeuft ueber `LeereFelderPlanBuilder` (reine Rechnung) und zeigt erst einen
Bericht; geschrieben wird nach Bestaetigung durch `LeereFelderAnwender`.

Vier Regeln dieses Wegs:

- **Ein gefuelltes Feld wird nie angefasst** — unabhaengig von seiner Herkunft.
  Der Ausfuehrer prueft das ein zweites Mal, weil zwischen Bericht und
  Bestaetigung getippt worden sein kann.
- **Ein mehrdeutiger Name bekommt nichts.** Im Bestand tragen 2574
  Haltungsnamen und 334 Schachtnamen mehr als ein Objekt.
- **Geschrieben wird mit `FieldSource.Kataster` und `userEdited: false`.** Ein
  nachgefuellter Wert ist keine Handeingabe und geht deshalb NICHT in die
  revidierte XTF zurueck — er stammt aus derselben Quelle.
- **`unbekannt` fuellt nichts.** Zwei Sperren in `QgisFeldKarte` decken das
  gemeinsam ab (Rohwert und umgesetzter Wert); keine der beiden entfernen.

Die Pfade stehen in `AppSettings.QgisHaltungenGpkgPath` und
`QgisSchaechteGpkgPath`. Der bestehende Einzelnachschlag per Rechtsklick
(`FeldNachschlagUseCase`) bleibt unangetastet — er bedient das Grundbuch, das
nur Einzelabfragen erlaubt.

`Katasterkennungen ergaenzen` (seit 2026-09-04, je ein Knopf auf Haltungs- und
Schachtseite neben `Leere Felder aus QGIS`) uebernimmt die SIA405-Kennungen, unter
denen GEONIS jedes Bauteil fuehrt, aus der Kennungstabelle
`AppSettings.KatasterKennungenGpkgPath` (Standard
`D:\QGIS_V4.2\Layer\Kataster_Kennungen_GEONIS_2024-12.gpkg`, gebaut aus der
GEONIS-Kopie `D:\Fachwissen\ArcGis\Stand_Dezember_2024_uri_abwasser.gdb`; Tabellen
`haltungen`, `schaechte`, `herkunft`). Weg: `IKatasterKennungLeser`/
`KatasterKennungGpkgLeser` -> `KatasterKennungPlanBuilder` (reine Rechnung) ->
Bericht -> `KatasterKennungAnwender`. Ergebnis ist das additive typisierte Objekt
`HaltungRecord.Geonis` bzw. `SchachtRecord.Geonis` (`GeonisKennungen`: Haltung, Kanal,
beide Haltungspunkte samt GEONIS-Namen wie `A75394`, Rohrprofil samt Typ; Knoten,
Bauwerk). Der Neu-Export (`XtfNeuPlanBuilder`) schreibt diese Kennungen als TID, damit
GEONIS die Objekte wiedererkennt statt Duplikate anzulegen.

Fuenf Regeln dieses Wegs nie zurueckdrehen:

- **Der Schluessel ist die GEONIS-`SIA405_ID`** (16 Zeichen, Praefix `ch23h1a4`,
  bei 99,97 % der Haltungen/Schaechte belegt). Das sichtbare Feld `Objekt_ID` (Label
  `Objekt-ID (Lisag)`) bleibt unangetastet: Dort steht bei aus QGIS gefuellten
  Haltungen die Lisag-Nummer aus dem WFS-Dienst geo.ur.ch, die bei jeder
  Veroeffentlichung neu vergeben wird (866789 -> 867034) und in GEONIS nicht existiert. Sichtbar ist die Kennung im getrennten Anzeigefeld
  `FieldKeys.GeonisId` (`GEONIS_Kennung`, Label `GEONIS-Kennung`; am Schacht ueber
  `SchaechteColumnPolicy.ErgaenzeKatasterKennung`). Es spiegelt nur die Hauptkennung,
  die Wahrheit bleibt das `Geonis`-Objekt; ein leeres Anzeigefeld bei vorhandener
  Kennung zieht der Knopf als `NurAnzeige`-Position nach. Das Feld steht in
  `NichtExportierteFelder` und geht nie als Sachfeld in eine XTF.
- **Nur bei genau einem Treffer.** Direkter Name zuerst, bei Haltungen danach die
  Gegenrichtung (dann werden die zwei Punktkennungen vertauscht und
  `RichtungGedreht` gesetzt). Mehrdeutig heisst nichts: In der Kopie tragen 389 echte
  Haltungsnamen und 467 echte Schachtnamen mehr als ein Objekt.
- **Eine vorhandene Kennung wird nie ersetzt** — sie kann aus einem neueren
  GEONIS-Export stammen. Das gilt auch fuer eine TID, die ein XTF-Import nur in
  `Objekt_ID` abgelegt hat: Hat sie SIA405-Form und widerspricht der Kopie, bekommt das
  Bauteil nichts (`Abweichend`). Nur Kennungen, keine Fachwerte: Die Kopie ist alt.
- **Das Anzeigefeld `GEONIS-Kennung` ist schreibgeschuetzt** (Formular beider Seiten,
  Haltungstabelle, Schachtraster). Der Export liest ausschliesslich das `Geonis`-Objekt.
- **`GeonisKennungen.GeonisGeaendert`** traegt das GEONIS-Aenderungsdatum aus der Kopie
  (`GN_LAST_EDITED_DATE`) als Ausgangsstand fuer einen spaeteren Konfliktschutz. In die
  XTF geht es NICHT; `Letzte_Aenderung` ist dort der Exporttag. Ein Aenderungsmanifest
  mit Ausgangswerten je Objekt existiert noch nicht — der Neu-Export ist ein Voll-Export.
- **Ein Rohrprofil wird in GEONIS geteilt** (56 Profile fuer 102'317 Haltungen). Seine
  Kennung verwendet der Export nur bei gleichem Profiltyp und ohne
  Hoehen-Breiten-Verhaeltnis; sonst eigenes Profil plus Hinweis
  `Rohrprofil weicht vom Kataster ab`.
- **Nur STANDARDOID-Form** (`SiaObjektkennung.IstGueltig`): Alles andere wird
  ignoriert, statt eine ungueltige TID zu schreiben.

Bauskript der Kennungstabelle: `tools/KatasterKennungen/bau_kennungen.sh` (ogr2ogr aus
der gdb-Kopie, Git Bash, GDAL_DATA und TEMP auf Windows-Pfade setzen).
Belegt 2026-09-04 (Live-Abfragen, nicht archiviert; Praefix-Muster an 13 Stichproben
beobachtet): Der oeffentliche WFS-Dienst der Lisag (geo.ur.ch, Layer
`leitungen:abw_abwasserknoten`)
traegt fuer Schaechte `xtf_id` = `ch24gwkd` + dieselben acht Objektzeichen wie die
GEONIS-Kennung; fuer Haltungen fehlt sie dort. Die GEONIS-Konfiguration
(`D:\Fachwissen\ArcGis\GEONIS_AWU_2022`) enthaelt einen FME-Import im
UPDATE-Modus (Match ueber GlobalId, keine Geometrie) fuer SIA405 2015.

## Aufbau des SIA405-Rueckwegs (30.09.2026, Wartbarkeitspaket AP08)

`LegacyXtfImportService.ParseSia405` ist nur noch die Abfolge von drei Klassen unter
`Infrastructure/Import/Xtf/Sia405/`: `Sia405ObjektLeser` liest Kanal, Haltung,
Rohrprofil, Haltungspunkt, Abwasserknoten und Organisation mit ihren Rohwerten und
Kennungen (auch fuer den Normschacht-Leser); `Sia405Beziehungen` loest Kanal,
Rohrprofil, Organisationen und Schachtnamen auf und laesst Fehlendes sichtbar leer
(`Sia405KanalBezug.Fehlt`, `RohrprofilVerweisOhneZiel`); `Sia405HaltungAbbildung`
enthaelt die fachlichen Feldregeln dieses Abschnitts. Eine neue Feldregel gehoert in die
Abbildung, ein neues Dateiobjekt in den Leser. Die Uebernahme ins Projekt
(Handwertschutz, Konflikte) bleibt in `MergeRecordIntoProject`. VSA-KEK ist seit AP08b
gleich aufgebaut (siehe unten). Waechter: `XtfReferenzfallTests` vergleicht fuer synthetische Dateien unter
`tests/Fixtures/XtfReferenz/` das vollstaendige Projektergebnis mit einem Schnappschuss,
der vor dem Umbau aufgenommen wurde; ein neuer Schnappschuss entsteht nur, wenn die
Datei fehlt, und muss dann bewusst geprueft werden.

**VSA-KEK (AP08b, 30.09.2026).** `ParseVsaKek` steht neben `ParseSia405` und ist nur noch
die Abfolge unter `Infrastructure/Import/Xtf/VsaKek/`: `VsaKekObjektLeser` liest
Untersuchung, Kanal- und Normschachtschaden, Datei und mitgelieferte Bauwerke mit Rohwerten
und Kennungen; `VsaKekBeziehungen` ordnet Schaeden und Fotos/Videos ueber die TID
(`UntersuchungRef`, `Datei.Objekt` als OBJ_ID oder TID) zu und teilt die Untersuchungen
ueber `VsaKekUntersuchungsart` in Haltung, Schacht und ungeklaert; danach waehlt
`VsaKekUntersuchungsWahl` je Haltung die Haupt-Untersuchung; `VsaKekAbbildung` setzt die
Haltungsfelder, baut die Schachtprotokollzeilen und den Importbeleg. Nicht Zuordenbares
(Untersuchung ohne Bezeichnung, verwaiste Schaeden, Foto ohne Befund) steht sichtbar in
`VsaKekBezuege` und wird seit 01.10.2026 gemeldet (siehe unten). **Importbeleg:** Der gespeicherte
`ImportFingerprint` von Schachtbegehung und weiterer Untersuchung ist SHA-256 ueber die
JSON-Form von `VsaKekUntersuchung` samt `VsaKekKanalschaden`/`VsaKekSchachtschaden`.
Diese drei Klassen nie umbenennen, umordnen oder ergaenzen (Eigenschaften), sonst erkennt
ein Wiederholungsimport bestehende Begehungen nicht mehr; Waechter:
`XtfVsaKekFingerabdruckTests` (fester Wert) und `vsakek-referenz`.

## XTF-Import: nichts still vermischen (30.09.2026)

- **Doppelte SIA405-Haltungsbezeichnung: nur die erste.** Zwei `Haltung`-Objekte mit
  gleicher Bezeichnung (verglichen wie die Uebernahme den Datensatz findet:
  `HoldingKeyNormalizer`, Gross-/Kleinschreibung egal) und verschiedener TID sind zwei
  Katasterobjekte. Bisher landeten beide im selben Datensatz, die zweite ueberschrieb
  Objekt_ID, DN, Material, Eigentuemer, andere Felder blieben von der ersten. Jetzt wird
  die erste in Dateireihenfolge wie bisher uebernommen, jede weitere nicht; der
  Importbericht nennt sie mit beiden TIDs («Haltung 'X' kommt zweimal vor (TID a, TID b)
  – nur die erste übernommen»). Gleiche TID zweimal bleibt wie bisher.
  Ort: `Sia405DoppelteBezeichnungen` zwischen Bezuegen und Abbildung. Test:
  `XtfDoppelteHaltungsbezeichnungTests`.
- **VSA-KEK: Haupt-Untersuchung ist die vollstaendigste, jede weitere wird Protokollfassung**
  (Entscheid Pascal 30.09.2026, «Variante C»). Bisher ueberschrieb die zweite Untersuchung
  derselben Haltung (z.B. die Gegenbefahrung) Datum, Laenge, Richtung, Video,
  `XtfHerkunft` und Bemerkung der ersten, und der Datensatz trug die Befunde beider
  (Zuordnung ueber den Namen). Jetzt waehlt `VsaKekUntersuchungsWahl` je Haltung
  (Bezeichnung normalisiert wie die Uebernahme) die Haupt-Untersuchung: nicht abgebrochen
  (kein Kanalschaden BDC*; ein Abbruchfeld gibt es in der Datei nicht) vor abgebrochen,
  dann laengere `Inspizierte_Laenge` (ohne sie die groesste Schadensdistanz), dann das
  glaubwuerdige Datum wie bei WinCan (`UntersuchungsAuswahl.Sortierschluessel`:
  `2007-12-31` und vor 1990 sind Platzhalter), dann die Dateireihenfolge. Nur fuer
  VSA-KEK; WinCan waehlt unveraendert nach Datum (`UntersuchungsAuswahl.Ordne`). Die
  Haupt-Untersuchung liefert Felder, Befunde (ueber `UntersuchungRef`), `Primaere_Schaeden`
  und das Protokoll. Jede weitere wird wie bei WinCan als zusaetzliche `ProtocolRevision`
  in `History` abgelegt: eigene Befunde und Meter, `ImportFingerprint` (SHA-256 wie beim
  Schacht) gegen Duplikate, Video in `ImportVideoPaths`; ohne Befunde keine Fassung.
  `VsaFindingProtocolSynchronizer` gleicht Fassungen mit fremdem Importbeleg nicht mehr mit
  den Befunden der Haupt-Untersuchung ab. Der Importbericht nennt Haupt-Untersuchung und
  Fassung mit Datum, TID und Befundzahl. `Link_G` siehe unten. Schachtbegehungen und ihr
  `ImportFingerprint` sind unveraendert. Tests: `XtfVsaKekMehrereUntersuchungenTests`,
  `UntersuchungsAuswahlTests`.
- **Link_G beim VSA-KEK-Weg (01.10.2026).** `Link_G` ist wie bei WinCan (`Befahrungsrollen`)
  und in der Kanalverteilung das Video der Gegenbefahrung. `VsaKekAbbildung.Gegenvideo` setzt
  es auf das Video der weiteren Untersuchung, wenn genau eine weitere Untersuchung belegt
  aus der Gegenrichtung der Haupt-Untersuchung kommt (beide `Fliessrichtung` bekannt und
  verschieden, eigenes Video, nicht das Hauptvideo). Unterschied zu WinCan: Dort belegt nur
  die Dateinamenskonvention (`~G`/`_G`/`-G`) eine Gegenbefahrung, weil die Richtung einer
  Datenbank-Untersuchung allein Wiederholung oder Teilaufnahme nicht ausschliesst; in der
  VSA-KEK-Datei sind Video und Richtung an dieselbe, bereits als eigene Befahrung erkannte
  Untersuchung gebunden. Uebernahme ueber `MergeEngine` wie `Link` (Handwert bleibt). Das
  Video bleibt zusaetzlich in `ImportVideoPaths` der Fassung; der Kanalverteiler kopiert es
  einmal (`-g`). Test: `XtfVsaKekMehrereUntersuchungenTests`.
- **Importbericht nennt stille Luecken (01.10.2026).** Nur Meldungen, kein Wert und kein
  Zaehler aendert sich. SIA405 (`Sia405Bezugsmeldungen`, Warn, Kontext XTF405): Kanal-,
  Rohrprofil- und Haltungspunktverweis ins Leere, Organisation in der Datei ohne
  Bezeichnung, Haltung ohne Namen (je mit Haltung, TID, Verweis). Organisationsverweise auf
  Kennungen ausserhalb der Datei sind nach Norm EXTERNAL (wie `DssExportPruefung`) und
  stehen nur als ein gesammelter Info-Hinweis. VSA-KEK (`VsaKekLueckenmeldungen`, Warn):
  Untersuchung ohne Bezeichnung, verwaiste Kanal-/Normschachtschaeden, Fotos ohne
  Kanalschaden und Videos ohne Untersuchung je mit TID; ab 11 Objekten einer Art eine
  gebuendelte Zeile «n verwaiste …: a, b, … (+k weitere)». SIA405 und VSA-KEK in einer
  Datei: SIA405 gewinnt weiter, eine Warnung nennt die Zahl liegengebliebener
  Untersuchungen. `Letzte_Aenderung` im ISO-Format (`2025-10-06`, auch mit Uhrzeit) wird
  ueber `XtfValueNormalizer.NormalizeDate` zu `dd.MM.yyyy`; der VSA-KEK-`Zeitpunkt` laeuft
  unveraendert ueber `NormalizeDate_yyyymmdd`. Tests: `XtfSia405BezugsmeldungenTests`,
  `XtfVsaKekLueckenmeldungenTests`, `XtfSia405MitVsaKekTests`, `XtfSia405LetzteAenderungTests`.

