# Schächte: Protokolle, SchachtPro und Schachtgrafik

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Der PDF-Textleser wird geprueft gewaehlt (19.09.2026)
- SchachtPro-QR aus Bildern (19.09.2026)
- SchachtPro-Archive bis Format 3 / Schema 23 (19.09.2026)
- Schachtgrafik Stammkarte (19.09.2026)
- Ausdrücklich ausgewählte Schacht-PDF (14.09.2026)

## Der PDF-Textleser wird geprueft gewaehlt (19.09.2026)

Anlass: Messung an allen 264 SchachtPro-Protokollen aus Goeschenen, rein lesend, drei
unabhaengige Zaehlungen je PDF (Kennungsspalte, Datenzeilen, Skizzenlegende).

- **Welches `pdftotext` ein Rechner findet, entschied bisher der Zufall** — `tools\`,
  daneben, PATH, WinGet, in dieser Reihenfolge, ohne Pruefung. Mit Poppler 25.07 werden
  alle 704 Anschluesse gelesen, mit Xpdf 4.00 nur 627, und die gelesenen tragen teils
  FREMDE Werte: In Schacht 10039 bekommt Einlauf 1 Tiefe und Durchmesser von Einlauf 2,
  weil diese Fassung die Tabellenspalten zeilenweise verschiebt. Ein falscher Messwert
  ist schlimmer als ein fehlender — er sieht im Programm richtig aus.
- `PdfLeserEignung.Beurteile` (Application/Import, reine Regel) liest die Ausgabe von
  `pdftotext -v`: Poppler ab Hauptversion 21 ist geeignet, Xpdf nie, unbekannt nie.
  **Poppler nennt in seinem Copyright ebenfalls «Glyph & Cog»** (es stammt von Xpdf ab);
  nur das FEHLEN von «Poppler» macht eine Ausgabe zu Xpdf. Die Regel ist fail-closed.
- `PdfTextExtractionService` fragt die Version je Programmpfad genau einmal
  (`ConcurrentDictionary`, ein Import liest hunderte PDFs) und verwendet ein ungeeignetes
  Programm gar nicht erst. Der Rueckfall ist der mitgelieferte eingebaute Leser (PdfPig),
  der in derselben Messung gleich gut ist (704 von 704). `PdfTextExtractionResult` traegt
  dafuer additiv `Leser` und `LeserHinweis`. Auf Pascals PC aendert sich nichts: Der
  Windows-PATH liefert Poppler 25.07. Im Programmordner liegt weiterhin keine eigene
  `pdftotext.exe`, nur `PLACE_PDFTOTEXT_HERE.txt`.
- **Die LV95-Koordinaten des Protokolls werden uebernommen** (212 von 212 im Bestand):
  `SchachtProtocolZusatzParser.Koordinaten` liest «Koordinaten (LV95): E … / N …» mit
  Punkt UND Komma als Dezimaltrenner (beides kommt vor) und nur innerhalb der amtlichen
  LV95-Ausdehnung. Ein halbes Paar, eine vertauschte Reihenfolge oder eine Zahl ausserhalb
  ergibt nichts — eine falsche Koordinate setzt den Schacht an den falschen Ort. Ziel sind
  dieselben Felder wie beim Archivweg (`Koordinate_East`/`Koordinate_North`); Hand- und
  Katasterwerte schuetzt `SchachtRecord.SetFieldValue` selbst (`IsUserEdited`,
  `KatasterFeldschutz`), deshalb genuegt der normale Schreibweg. Die Lage ist die AUSNAHME zur
  Regel «Kanalfirma ersetzt Kataster» vom 23.09.2026: Die Katasterkoordinate ist vermessen,
  die des Protokolls meist Handy-GPS — ein Protokoll fuellt nur eine leere Lage.
- **Der Anschlusszustand gehoert an seinen Anschluss.** `SchachtAnschluss.Zustand` traegt
  «in Ordnung», «Mangelhaft eingebunden», «Einragend» …, mehrere mit « • ». Im Bestand
  tragen alle 705 Anschluesse einen Zustand. **Das PDF kuerzt eine zu lange Zelle**, mit
  «…» oder mit «+3» fuer drei weitere Befunde; dann steht der Rest nirgends im Dokument.
  `ZustandUnvollstaendig` haelt das fest (24 Faelle), und die Grafik schreibt
  «… (im Protokoll gekuerzt)». Nie eine gekuerzte Angabe als vollstaendig speichern.
  Der Archiv- und QR-Weg liefert dieselben Zustaende als Liste und damit vollstaendig;
  `SchachtProProtocolMapper` fuellt seither dasselbe Feld.
- Das Trennzeichen kommt als «●» an, weil `NormalizeCheckboxGlyphs` runde Punkte des
  Uri-Formulars zu Ankreuzmarken vereinheitlicht. Im Zustandstext wird es zurueckgesetzt.
- Tests: `PdfLeserEignungTests` (8, echte Versionsausgaben beider Programme),
  `PdfLeserWahlTests` (4, vorgetaeuschtes pdftotext mit Markierungsdatei: ein ungeeignetes
  Programm wird nachweislich nicht einmal aufgerufen), `SchachtProtocolVollstaendigkeitTests`
  (15, Vorlagen `10039_…` und `10091_…` mit echtem Seitentext). Nicht gemessen ist, ob der
  eingebaute Leser auch fuer Haltungsprotokolle und Dichtheitsberichte gleichwertig ist;
  dort greift der Rueckfall nur, wenn ohnehin kein geeignetes Programm vorhanden waere.
  Abnahme und Grenzen fuer die Bedienung: `docs/SCHACHTPROTOKOLL-PDF-IMPORT.md`.

## SchachtPro-QR aus Bildern (19.09.2026)

- Nachtrag Verteilung: `SchachtProQrAblage` nutzt `ProjectStructure.SchachtVerteiltDir`,
  `ImportDateStampResolver` und `StageCopyAs`: `<Datum>_<Schacht>_QR.<png/jpg>`.
  Relative Bildzuordnung unter `SchachtPro.QR.Bild.<sourceKey>`. Originalbild bleibt
  Bild; bestehendes PDF_Path bleibt unveraendert. Datei vor der Datenuebernahme
  vorbereiten, Source-Readlock schuetzt Lesen/Kopieren derselben Version. Gleiche
  Dateien werden wiederverwendet, Kollisionen erhalten freie Namen, Rollback ueber
  die bestehende Importsitzung. Ohne Staging expliziter Hinweis statt falscher Ablagemeldung.
  Verhaltenstests: `SchachtProQrImportTests.Ablage`.

- `ISchachtProQrImportService` / `SchachtProQrImportService` importieren genau einen
  eindeutigen SPQR1-Inhalt je PNG/JPG. `IQrImageReader` wird durch den Windows-Adapter
  `UI/Services/QrImageReader` umgesetzt. ZXing.Net 0.16.11 ist vom Nutzer freigegeben,
  im UI-Projekt samt Lockdateien festgelegt. Kein Java, Python oder Online-Dienst zur Laufzeit.
- `SchachtProQrPayload` prueft CRC32 der komprimierten Bytes, Zlib/Adler32, UTF-8,
  Schema/Version/Quelle, doppelte JSON-Felder und Pflichtkennungen vor Datenuebernahme.
  Grenzen: 8192 Textzeichen, 64 KiB entpackt, JSON-Tiefe 24, 100 Anschluesse;
  Bilder maximal 32 MiB / 24 Megapixel. Quellpfadschutz und Abbruch gelten ebenfalls.
- `SchachtProQrMapping` bildet den separaten SPQR1-Vertrag aus
  `C:/SchachtPro_5/pdf/src/main/java/com/pascal/schachtpro/pdf/ProtocolQrPayload.kt`
  auf den bestehenden Archivvertrag ab. LV95 wird explizit geprueft; leere
  Anschluss-Standardzeilen entfallen. Vollstaendige Original-JSON unter
  `Project.Metadata[SchachtPro.QR.<sourceKey>]` erhaelt auch nicht angezeigte Angaben.
- `SchachtProProtocolImport` enthaelt die aus dem Archivdienst ausgelagerte gemeinsame
  Datenuebernahme samt Handwert-/Protokoll-Reimportschutz. Archivfoto-Staging bleibt
  im Archivdienst. Quelle bleibt kompatibel `FieldSource.Spro`; kein neues Projektformat.
  Mehrdeutige Schachtnummern im Ziel sperren den QR-Import.
- Neuer Menuepunkt auf der Importseite: «SchachtPro-QR aus Bild (PNG/JPG)».
  Bestehender Importlauf mit Projektkopie, Vorschau, Speichern, Quellablage und
  gemeinsamer Importsperre. Fehler pro Bild; ausschliesslich defekte Bilder fuehren
  zu keiner Projektuebernahme. Neue ServiceProvider-Registrierung, insgesamt 169.
- Tests: `SchachtProQrImportTests`, `SchachtProQrImageTests`,
  `ImportManualWorkflowControllerTests.Qr`; Archivtests schuetzen die Auslagerung.
  Anleitung und Grenzen: `docs/SCHACHTPRO-QR-IMPORT.md`.

## SchachtPro-Archive bis Format 3 / Schema 23 (19.09.2026)

- `SchachtProArchiveReader` akzeptiert jetzt die aktuellen `.spro`-ZIPs der App
  in `C:\SchachtPro_5` sowie alte Formate 1 und 2. Neuere Versionen bleiben gesperrt.
  Der Vertrag wurde an `ProjectArchive.kt`, `ProjectExporter.kt`, `ProjectImporter.kt`
  und `AppDatabase.kt` abgeglichen; nicht nur die Versionsgrenze wurde angehoben.
- `SchachtProArchiveIntegrity` prueft ab Format 2 verpflichtend `integrity.json`:
  SHA-256 jedes Dateieintrags einschliesslich Manifest und Fotos, genaue Dateiliste,
  eindeutige normalisierte Pfade, hoechstens 5 MB Nachweis. Ein vorhandener Nachweis
  wird auch bei Format 1 geprueft. Fehlende oder falsche Pruefsummen sperren das ganze
  Archiv VOR jeder Projekt-/Fotouebernahme. Doppelte ZIP-Pfade werden abgelehnt.
  Datei-Hashes werden blockweise mit Abbruchpruefung gelesen; keine neue Paketabhaengigkeit.
- `SchachtProImportService` uebernimmt auch `connectionPhoto.photoPath` als eigenes
  Originalfoto ueber die vorhandene Staging-Sitzung (`<Protokollindex>_connection.jpg`).
  Ausrichtung und Schachtgrafik-Ueberlagerung werden nicht nachgebaut; ein Importhinweis
  nennt diese Grenze. Normale Fotos und Handwertschutz bleiben beim bestehenden Weg.
- Keine neue Dienstregistrierung oder Aenderung am SewerStudio-Projektformat.
  QR-Lesen (SPQR1), PDF-Textextraktion und verschluesselte `.spro`-Archive sind nicht
  Teil dieser Korrektur. Die SchachtPro-App wurde nicht veraendert.
- Verhaltenstests: `SchachtProImportServiceTests.CurrentArchives.cs` ergaenzt den
  Bestand um aktuelle/alte Archive, Daten/Fotos, Anschlussfoto allein und kombiniert,
  fehlerhafte Pruefsummen, mehrdeutige Pfade, Groessenlimit und Abbruch.
  Nachweis und Bedienung: `docs/SCHACHTPRO-ARCHIVIMPORT.md`.

## Schachtgrafik Stammkarte (19.09.2026)

Anlass: Pascals Bild der Schachtansicht 80409 (Zone 1.15) — drei feste Zonen, 27 Symbole in
einer Spalte, keine Anschluesse. Vorschlag mit Zeichnungen:
`https://claude.ai/artifact/RxGFLtq6ZWHgUDebu1QVsa`, Plan
`docs/superpowers/plans/2026-09-19-schachtgrafik-stammkarte.md`.

- **Der PDF-Import zaehlt im Kaestchenformular nur Marken.** `SchachtProtocolKaestchenformular`
  erkennt das Uri-Schachtprotokoll (Kopfzeile «Zustand der Bauteile» mit «Maengelfrei» oder die
  Tabelle «Aus/Ein … Tiefe m»). Dort bindet eine Marke (●/✔) an das FOLGENDE Wort; Text vor der
  ersten Marke zaehlt nie. Vorher nahm der Freitextweg jedes bekannte Wort der Zeile, und die
  Zeile «Anschluss» beendete den Abschnitt (`^ANSCHL`): 80409 stand mit 27 statt 4 Schaeden im
  Projekt, und Verkalkung, Fremdwasser, Steigeisen, Tauchbogen fehlten in jedem Uri-Protokoll.
  Abschnittsende ist jetzt die Tabelle «Anschluesse» (Plural). SchachtPro-PDFs (Punkte als
  Trennzeichen, nur vorhandene Schaeden je Zeile) bleiben auf dem Freitextweg. Bestehende
  Schaechte holen sich den Stand ueber «Protokoll neu einlesen».
- **Die Anschlusstabelle ist Daten, kein Text.** `SchachtRecord.Anschluesse`
  (`SchachtAnschluss`: Nr, Art, DN, Tiefe ab Deckel-OK, Material, Uhr, Richtung, Haltungsname,
  Quelle) ist additiv, kein Feld, kein Export. `SchachtProtocolZusatzParser` liest sie samt
  Medium, Material Schacht/Deckel, «Deckel DN m» (als mm) und den Kaestchen Steighilfe/Tauchbogen;
  `SchachtProtocolApplier.ApplyZusatz` schreibt nur Genanntes und leert beim Neuaufbau nichts
  («Material» kann aus XTF stammen). SchachtPro fuellt dieselbe Struktur (mit Uhr und Richtung).
- **Schnitt und Grundriss statt Zonen.** `SchachtgrafikModellBuilder` (Application/Reports)
  baut das WPF-freie `SchachtgrafikModell`, `SchachtgrafikSvgBuilder` zeichnet es: Tiefe
  massstaeblich (110 bis 130 Einheiten je Meter), Anschluesse auf ihrer Tiefe (Hauptauslauf
  rechts, gegenueberliegender Einlauf links, uebrige als Kreise auf der Rueckwand nach Richtung),
  Deckel, Konus und Steigeisen SCHEMATISCH und so beschriftet; Grundriss mit Auslauf oben
  (12 Uhr nach VSA), Rohre nach Azimut, Nordpfeil nur aus Koordinaten. Sind Richtungen
  bekannt, bekommt ein Anschluss ohne Richtung KEINEN erfundenen Winkel: Er fehlt im Grundriss
  und steht unten als «ohne Richtung». Die Stundenmarken 3/6/9 weichen einem Rohr an derselben
  Stelle (dort steht schon dessen Kennung; «12» bleibt rechts neben dem Auslauf). Beschriftet
  wird mit Kennungen (A1, E3) und Nummern, der Text steht in der Legende
  (`SchachtgrafikLegende`) und in den Hinweisflaechen. Nichts wird erfunden: fehlende Tiefe = kein Massstab plus Hinweis,
  fehlende Richtung = «Richtung nicht erfasst», Anschluss ohne Haltung = «nicht im Projekt»,
  Bemerkung «Einlauf 3 ausgebrochen» = Schaden am Anschluss 3 (nur wenn es ihn gibt).
- **Lage und Koten kommen von der Seite, nicht aus dem Control.** `ISchachtLageQuelle` /
  `QgisGpkgSchachtLageLeser` (168. Registrierung) liest Schachtpunkt und Leitungslinien gezielt
  je Name aus den QGIS-Kopien (mehrdeutig = nichts); `SchachtAnschlussRichtung` rechnet den
  Azimut am schachtseitigen Ende (Toleranz 1 m). `SchachtKotenQuelle` liest Deckel-, Sohlen-
  und Anschlusskoten aus den GeoShop-Objektakten. `SchaechteNovaWorkspaceController.
  LadeSchachtansichtZusatz` setzt die Koten sofort und die Lage aus dem Hintergrund mit
  Generationszaehler; ein Lesefehler wird als Hinweis in der Grafik sichtbar, nie verschluckt.
  **Der Lade-Aufruf haengt an `Selected` (beide Ansichten) UND an der Zellenauswahl der
  Tabelle.** In der Aufklapp-Liste zog ein Auswahlwechsel bis 19.09.2026 abends nur das
  Formular nach: Der Grundriss blieb schematisch, obwohl die Kopie alles hatte (80792: drei
  Leitungen und der Hausanschluss `u-80792` enden exakt auf dem Schachtpunkt). Waechter:
  `SchaechteNovaLayoutIsolatedSmokeTests.Kindprozess_Schachtansicht_laedt_Lage_auch_in_der_Aufklapp_Liste`.
  `SchachtHaltungsseite` ist die EINE Regel «Schacht oben oder unten»: Felder zuerst, sonst
  der Haltungsname (in Zone 1.15 sind die Schachtfelder aller 96 Haltungen leer).
- **Der SVG-Vertrag bleibt.** Gestrichelte Kreise sind Pfade (`KreisPfad`), Gruppen tragen
  nur `transform`, Striche in Muted-/Akzentfarbe sind Pfade oder mindestens 4 breit, Schrift
  mindestens 10. Die Schachtgrafik hat keine feste Hoehe mehr: Die Viewbox folgt dem
  Seitenverhaeltnis, der Bildlauf des Panels traegt den Rest.
- Tests: `SchachtProtocolKaestchenformularTests` (echter Text von 80409, 27 -> 4),
  `SchachtAnschlussRichtungTests`, `GpkgGeometriePunktTests`, `SchachtKotenQuelleTests`,
  `SchachtUhrlageTests`, `QgisGpkgSchachtLageLeserTests` (mit Raumindex),
  `SchachtgrafikModellBuilderTests` (Typzaehlung, Tiefe gegen gleichen DN, Katasterleitung,
  Uhrlage, Bemerkung «Anschluss N»),
  `SchachtgrafikSvgBuilderStammkarteTests`, `SchachtgrafikAnsichtBuilderStammkarteTests`;
  angepasst `SchachtgrafikSvgBuilderTeilmengeTests`, `SchachtgrafikControlIsolatedSmokeTests`,
  `ServiceProviderRegistrationTests` (168). Nicht erfasst bleiben Ovalausrichtung, Konushoehe,
  Steigeisenseite und Deckellage; die Sichtprobe im Programm macht Pascal.
- **Kennungen zaehlen je Typ (A1, E1, E2 …), wie die Skizze des Inspekteurs (74 von 74
  Uri-PDFs) und wie SchachtPro.** Die Tabellennummer bleibt `SchachtgrafikAnschluss.Nr`
  (Marken, Zuordnung); `TypNr` traegt die Zaehlung je Typ. Eine Bemerkung «Einlauf 3» meint
  den DRITTEN Einlauf (Skizzen-E3, bei 80409 die vierte Tabellenzeile), «Anschluss 3» die
  Tabellennummer 3. Vorher zaehlte die Grafik E2..E4 und haengte «Einlauf 3» an die dritte Zeile.
- **Tabellenzeile und Leitung finden sich ueber Seite, Durchmesser und Tiefe.** Seite
  (Aus/Ein), Durchmesser mit 5 % Spiel (`DnSpiel`: 148 mm in der Kopie, DN 150 im Protokoll),
  Tiefe mit 30 cm Spiel (`TiefenSpielM`: Deckelkote minus Punktkote aus den Objektakten gegen
  die Tabellentiefe). Jeder Schritt engt nur ein; bleibt mehr als eine Haltung, wird nichts
  zugeordnet — zwei DN 150 ohne Koten bleiben getrennt stehen statt geraten.
- **Katasterleitungen am Schachtpunkt.** `QgisGpkgSchachtLageLeser` liest ueber den R-Tree der
  Kopie (`rtree_<Tabelle>_geom`; ohne ihn keine Suche) alle Leitungen, die innerhalb 1 m am
  Schachtpunkt beginnen oder enden und nicht unter den Projektnamen sind
  (`SchachtLage.WeitereLeitungen`; `SchachtAnschlussRichtung.EndetImSchacht`: Ende im Schacht =
  Einlauf). DN und Material laufen durch `QgisFeldKarte`. Eine Tabellenzeile ohne
  Projekthaltung bekommt so ihre Richtung (80792: `u-80792` DN 115, Azimut 107°); eine
  Katasterleitung ohne Zeile erscheint als eigener Anschluss «nur im Kataster» (heller Rand).
  Eine Projekthaltung, deren Name der Kopie fehlt, bekommt die Richtung einer Katasterleitung
  gleicher Seite und gleichen Durchmessers — mit Hinweis.
- **Die Uhrlage ist die dritte Richtungsquelle.** `SchachtUhrlage.Grad` liest «12», «4»,
  «4:30», «4.5», «7 Uhr» als Winkel ab dem Auslauf; SchachtPro schreibt sie je Anschluss
  (Goeschenen 8705: A1 12, E1 4, E2 6, E3 7). Vermessen (Azimut) schlaegt Uhrlage, Uhrlage
  schlaegt Schematik; der Nordpfeil bleibt an Koordinaten gebunden, und die Hinweise sagen
  «Richtungen teilweise aus der Uhrlage des Protokolls (nicht vermessen)». Ohne jede Richtung
  liegt der Durchlauf (gleicher Durchmesser und gleiche Tiefe wie der Auslauf) bei 6 Uhr.
- Die Handskizze des Uri-Formulars (Pascals kuenftiger Standard) ist eine Vektorzeichnung
  (Linien plus Kennungen als Text, 74 von 74 PDFs) und parsebar, aber nur ungefaehr: 80409
  weicht bis 48 Grad von der Vermessung ab, 80792 bis 24 Grad. Ein Skizzenparser bleibt der
  Rueckfall fuer Anschluesse ohne Katasterleitung.
- **Der SchachtPro-Export (PDF) hat seine eigene Tabelle.** `SchachtProtocolZusatzParser`
  erkennt ihn an der Kopfzeile «SCHACHTPRO» oder der Tabelle «Ansc… Uhrzeit Tiefe» und liest je
  Zeile Kennung (A1, E1 …), Uhrzeit, Tiefe und Material; den Durchmesser aus der
  Skizzenlegende («A1 DN150»), ersatzweise aus der Spalte «150 mm Auslauf», die pdftotext um
  eine Zeile nach unten schiebt (nur, wenn sie so viele Werte hat wie Zeilen). Dazu Material,
  Deckelmaterial, «Deckeldurchmesser (m)» und der ausgeschriebene Tauchbogen. Stammdaten
  («Tiefe (m)», «Durchmesser (m)», «Form») und die Schaeden («Schachthals Ausgebrochen • Riss»)
  las der bestehende Parser schon richtig — Goeschenen 2026 (447 Schaechte) wurde nur nie
  eingelesen, die PDFs sind bloss verteilt. Der Anschluss-Zustand («Mangelhaft eingebunden»)
  bleibt offen. Der PDF-Weg ist ein Notnagel: Pascal bekommt spaeter einen direkten
  SchachtPro-Export; `SchachtProArchiveReader` liest das JSON-Archiv (uhr, richtung, zustand je
  Anschluss) und ist an Format 3 / Schema 23 des aktuellen Android-Exporters abgeglichen.
  Fixture: `tests/Fixtures/Schachtprotokolle/8705_schachtpro_seite1_layout.txt`, Tests
  `SchachtProtocolZusatzParserSchachtProTests`.

## Ausdrücklich ausgewählte Schacht-PDF (14.09.2026)

- `SchaechtePageViewModel.ProtocolImport` übergibt den ausgewählten Schacht an den
  bestehenden `SchachtProtocolSingleImportController.ExecuteAsync` (optionaler Parameter;
  alte Aufrufer bleiben kompatibel). Der Controller hält die Auswahl vor dem Lesen fest.
- Ohne Protokollerkennung oder ohne erkannte Schachtnummer kann eine einzelne PDF
  am ausdrücklich ausgewählten, noch vorhandenen Schacht abgelegt werden. Dafür bleibt
  der bestehende geschützte Kopierweg mit eindeutigen Dateinamen zuständig.
  `SchachtPdfVerknuepfung` in Application/UseCases setzt nur `PDF_Path` als bewusste
  Dateiauswahl. Stammdaten, Katasterwerte, Beobachtungen, Handwerte und `Link` bleiben erhalten.
- Vor und nach dem Kopieren gelten die bisherigen Projektprüfungen. Entfernte oder
  umbenannte Ziele werden nicht wiederhergestellt; Kopierfehler verhindern die Verknüpfung.
  Ohne ausgewählten Schacht bleibt ein nicht zuordenbares Dokument abgewiesen.
  Erkanntes Protokoll und Ordnerimport behalten ihre bisherigen Zuordnungsregeln.
- Tests: `SchachtProtocolSingleImportControllerTests`, `SchaechtePageArchitectureGuardTests`
  und bestehende Import-/Kopiertests. Anleitung: `docs/SCHACHT-PDF-ANHANG.md`.

