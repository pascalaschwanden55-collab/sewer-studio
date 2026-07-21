# Code-Refactoring-Plan SewerStudio

Stand: 19.07.2026

## Ziel

SewerStudio wird schrittweise leichter wartbar, ohne bestehendes Verhalten,
oeffentliche Fassaden oder gespeicherte Datenformate zu brechen. Jeder Schritt ist
klein, einzeln pruefbar und kann bei Bedarf getrennt zurueckgestellt werden.

## Arbeitsweise

1. Vor jedem Paket werden Git-Status, Aufrufer und vorhandene Tests geprueft.
2. Zuerst wird das heutige Verhalten mit einem fokussierten Test festgehalten.
3. Pro Paket wird genau eine zusammenhaengende Verantwortung verschoben.
4. Danach laufen die betroffenen Tests und der schnelle Release-Build.
5. Sol prueft Architektur und Ergebnis; Terra uebernimmt Bestands- und Testsuche.
6. Bei Architektur-Aenderungen wird `sewer-architektur` mit dem echten Code
   abgeglichen, aktualisiert und validiert.

Bestehende, fremde Aenderungen werden nicht ueberschrieben. Import, Training und
KI-Anzeige bleiben unangetastet, solange die dort laufenden Arbeiten nicht sauber
abgeschlossen sind.

## Reihenfolge

### Phase 0 - Sicherheitsnetz

- [x] `AGENTS.md`, `CLAUDE.md` und die Architektur-/Codequalitaetsregeln lesen.
- [x] Arbeitsbaum und Ueberschneidungen pruefen.
- [x] Schnellen Release-Build als Ausgangspunkt ausfuehren: 0 Warnungen, 0 Fehler.

### Phase 1 - Kleine gemeinsame Regeln

- [x] Gemeinsame Konfidenzbereiche fuer Timeline-Marker einfuehren.
- [x] Bestehende Enums, Signaturen, Parameterreihenfolgen und Schwellen erhalten.
- [x] Grenzwerte, fehlende Konfidenz, `NaN` und Unendlich direkt testen.
- [x] Paket durch Sol gegenpruefen und Gesamt-Build abschliessen.

Die beiden Timeline-Skalenrechner werden nicht zusammengelegt: Sie behandeln eine
fehlende Canvas-Breite heute bewusst unterschiedlich. Eine Zusammenlegung waere
damit keine reine Aufraeumarbeit.

### Phase 2 - Schichtgrenzen der Datenseiten

Jedes Teilpaket startet erst nach einer eigenen Ueberschneidungspruefung. Der
PDF-Schnitt konnte isoliert umgesetzt werden; in `ServiceProvider.cs` wurde nur
die bereits vorhandene Konstruktorzeile gezielt ergaenzt.

- [x] Direkte Infrastructure-Aufrufe bei Haltungs- und Schachtumbenennung durch
  den vorhandenen `IPdfTextLayerRewriter` ersetzen.
- [x] Stapelkorrektur und atomare PDF-Ersetzung als injizierten Infrastructure-
  Dienst aufbauen; alte Tuple-Fassade nur delegieren lassen.
- [x] Erfolg, Teilfehler, defekte PDF, unveraendertes Original, Temp-Cleanup und
  UI-Schichtgrenze fokussiert testen.
- [x] Foto-, Original-PDF-, Dichtheits-PDF-, Druck-, Druckcenter- und
  Schachtprotokoll-Oeffnen in `DataPage`, `BuilderPage` und `SchaechtePage` ueber den bereits vorhandenen,
  injizierten `ISafeShellOpenService` fuehren. Explorer-Aufrufe waren bereits
  sauber injiziert.
- [ ] Fachlich klaeren und absichern, ob absolut verknuepfte PDFs ausserhalb des
  Projektordners jemals in-place geaendert werden duerfen. Das bestehende
  Verhalten wurde in diesem reinen Schichtenschnitt noch nicht veraendert.

Geschaetzter Aufwand: 3 bis 5 Arbeitstage.

### Phase 3 - WPF-Fenster weiter verkleinern

- [x] Haltungs-Umbenennung aus `DataPage.xaml.cs` in den testbaren
  `DataPageHoldingRenameController` verschieben; Code-behind nur delegieren lassen.
- [x] Mehrfach-Loeschen aus `DataPage.xaml.cs` in den vorhandenen
  `DataPageRecordCollectionController` verschieben; Abbruch, Restauswahl und
  genau ein AutoSave mit Verhaltenstests absichern.
- [x] Normalen Schacht-Feldabschluss aus `SchaechtePage.xaml.cs` in den
  WPF-freien `SchaechteFieldEditController` verschieben und fuer Tabelle sowie
  einfachen Detailabschluss gemeinsam verwenden.
- [x] Hinzufuegen, Loeschen, Verschieben und Renummerieren der Schaechte in den
  WPF-freien `SchaechteRecordCollectionController` verschieben; Auswahl,
  Suchstatus und Projektmarkierung bleiben in einer duennen ViewModel-Partial.
  Den gemeinsamen Collection-Lock mit echten Mutationstests und einem
  nicht-leeren Guard fuer alle Schaechte-ViewModel-Partials absichern.
- [x] Schachtprotokoll-Oeffnen und Ordneranzeige aus `SchaechtePage` in den
  WPF-freien `SchaechteFileActionController` verschieben. Der zustandslose
  Controller wird pro Klick aus genau dem aktuellen ViewModel aufgebaut; Auswahl,
  Projektpfad, Resolver, sichere Oeffnungsdienste und injizierter Dialogdienst
  koennen dadurch nicht aus verschiedenen DataContexts stammen. Meldungen,
  Fehlertexte und Dienstreihenfolge bleiben erhalten. Direkte Tests schuetzen
  beide Aktionen, alle Leerpfade und Fehler; Architekturtests sichern XAML- sowie
  Schachtansicht-Einstiege und verbieten direkte Rueckfaelle. Die Seite sank von
  775 auf 742 nicht-leere Zeilen; der Controller hat 68. 42 gezielte und alle
  5.154 UI-Tests bestanden, der Release-Build blieb bei 0 Warnungen und 0 Fehlern.
  Ein maschinengebundener Smoke-Test wurde wie zuvor uebersprungen.
- [x] Ergebniszusammenfassung und kanonische Schachtordner-Aufloesung des
  Schachtprotokoll-Ordnerimports in die vorhandene
  `SchachtProtocolFolderImportPolicy` verschieben. Pflicht- und optionale
  Textzeilen, Acht-Fehler-Grenze, moderner und alter Verteilordner,
  Gross-/Kleinschreibung, Abschlussseparator und ungueltige Pfade sind direkt
  getestet; ein Architekturtest schuetzt die genaue produktive
  Argumentreihenfolge. Das Import-Partial sank von 272 auf 209 Zeilen.
- [x] Anwendung und Abschlussbericht des PDF-Stammdaten-Nachlaufs aus
  `SchaechtePageViewModel.Stammdaten` in den internen, WPF-freien
  `SchachtStammdatenResultApplier` verschieben. Feldreihenfolge, Nur-wenn-leer-
  Regel, Trimmen, unbekannte IDs, historische Zaehlung pro Ergaenzungszeile,
  Sicherung-vor-Mutation sowie die Zwoelf-Hinweise-Grenze sind direkt getestet.
  Wiederherstellungspunkt, Dirty/Save, Warnung, Abbruch und Fortschritt bleiben
  im ViewModel; das Partial sank von 178 auf 142 Zeilen.
- [x] Das Aktualisieren eines bereits verknuepften Schachtprotokolls aus
  `SchaechtePageViewModel.ProtocolImport` in den internen, WPF-freien
  `SchachtProtocolRefreshController` verschieben. Auswahl und relativer Pfad
  werden vor dem Lesen festgehalten; Bestaetigung mit sicherem Nein-Standard,
  Pfadpruefung, Lesefehler, Projektwechsel, Parserhinweis, Anwenden, UTC-
  Zeitmarkierung, Dirty/Save und der historische Erfolgstext trotz fehlgeschlagenem
  Speichern bleiben in derselben Reihenfolge. Fuenfzehn direkte Controller-Faelle,
  bestehende Command-Tests und ein exakter Verdrahtungs-Guard schuetzen den Ablauf.
  Das Partial sank in diesem Strukturpaket von 196 auf 154 Zeilen.
- [x] Den Import einer einzelnen neuen Schacht-PDF samt Kollisionsentscheidung,
  Projektkopie und Datensatzuebernahme in den internen, WPF-freien
  `SchachtProtocolSingleImportController` verschieben. Der gemeinsame Leseweg und
  Projektwechsel-Schutz bleiben im ViewModel; Find, Ja/Nein/Abbrechen, Kopier-
  Hintergrundlauf, Apply, Collection-Lock, Auswahl, UTC/Dirty/Save und Erfolgstext
  behalten ihren Altvertrag. Fuenfundzwanzig
  direkte Controller-Faelle und ein exakter Verdrahtungs-/Reihenfolge-Guard
  schuetzen Rohwerte, 0/3 Beobachtungen, technische Kopierfehler und alle
  Abbruchgrenzen. Das Partial sank im Strukturpaket von 154 auf 80 Zeilen.
- [x] Die Projektidentitaet fuer Aktualisierung, Einzel- und Ordnerimport separat
  absichern. `ProjectOperationContext` bindet die gestartete Projektinstanz und
  den normalisierten Pfad der `projekt.json`; der Projektordner bleibt nur fuer
  PDF-Pfade zustaendig. Vor und nach jedem asynchronen oder rueckrufbaren Abschnitt
  wird erneut geprueft. Der Ordnerimport verwendet fuer Verteilung, Suche, Add und
  Dirty durchgehend das festgehaltene Projekt. Vier Guard-Faelle, sechs echte
  Ordnerimport-Wiringtests, zwei Meldungsfaelle und die Controller-Ruecksprungtests schuetzen auch
  gleiche Projekt-ID, `Speichern unter` im selben Ordner und Wechsel waehrend
  Parse/Apply/Selected. `ProjectOperationImpact` unterscheidet dabei ohne
  irrefuehrende Meldung zwischen noch keiner Wirkung, bereits geschriebenen
  Projektdateien und veraenderten Projektdaten; beide Wirkungen bleiben als
  kombinierbare Flags gemeinsam sichtbar. Die oeffentliche
  `string DistributePdf(...)`-Fassade bleibt kompatibel. Der optionale additive
  `ISchachtProtocolDistributionResultService` meldet mit
  `SchachtProtocolDistributionResult.FileCreated`, ob wirklich eine neue PDF
  angelegt wurde. Ein frischer Dateirundlauf prueft die alte Fassade separat;
  ein Legacy-only Controller-Fake schuetzt den kompatiblen Rueckfall. Ab der
  ersten Datenmutation gilt eine klare Commit-Grenze:
  Das gestartete Projekt wird Dirty markiert; ein spaeter Wechsel verhindert den
  falschen Save und meldet ehrlich "uebernommen, aber nicht gespeichert". Eine
  nach dem Wechsel veraltete Auswahl wird nur dann geloescht, wenn sie noch genau
  auf den Schacht des alten Projekts zeigt; eine neue gueltige Auswahl bleibt
  erhalten. Ein echter `LockCheckingSchachtCollection`-Fall beweist zusaetzlich,
  dass der Ordnerimport neue Schaechte wirklich innerhalb des Collection-Locks
  einfuegt. Das gemeinsame Import-Partial liegt danach bei 144 Zeilen.
- [ ] Die verbleibenden Verhaltensaenderungen separat fachlich freigeben und
  umsetzen: Arbeitskopie beziehungsweise gezielte Ruecknahme statt eines blossen
  zusaetzlichen Restorepoints, sichtbarer Status bei `SaveProject() == false`,
  Datei-Staging mit gezielter Ruecknahme neu kopierter PDFs sowie ein gemeinsamer Schutz vor
  parallelen Aktualisierungen. Ein weiterer Restorepoint allein sichert nur die
  bereits gespeicherte `projekt.json`, nicht den ungespeicherten Zustand, und
  wuerde beim folgenden Speichern meist eine doppelte Sicherung erzeugen.
- [x] Debounce, Abbruch und Gewinnerregel der Projektvorschau aus
  `OverviewPageViewModel` in den WPF-freien `OverviewPreviewLoadController`
  verschieben; A-B-A-Auswahlrennen und Dispatcher-Shutdown mit Tests absichern.
- [x] Identische Ctrl-Mausrad-Zoomregel von `DataPage` und `SchaechtePage`
  ueber den vorhandenen `DataPageGridZoomController` vereinheitlichen.
- [x] Die kopierte Spaltenausrichtung der `DataPage` auf den bereits in
  `SchaechtePage` produktiven `DataGridColumnAlignmentToolbar` fuehren.
  Code-behind delegiert nur noch markierte Zelle, aktuelle Zelle, Kopfzeilenklick
  und die sechs Ausrichtungsbefehle. Standardausrichtung und gespeichertes Layout
  bleiben beim vorhandenen `DataGridColumnLayoutController`; echte STA-Faelle
  pruefen markierte Zelle und realisierte Kopfzeile. Das Partial
  `DataPage.ColumnLayout.cs` sank von 190 auf 93 Zeilen. Der vollstaendige
  Release-Build blieb bei 0 Warnungen und 0 Fehlern; alle 5.126 UI-Tests
  bestanden, ein maschinengebundener Smoke-Test blieb wie zuvor uebersprungen.
- [x] Aufloesung und Start des Gegeninspektions-Videos aus
  `DataPageViewModel.PlayGegenVideo` in den vorhandenen
  `DataPageVideoPlaybackController` verschieben. Der oeffentliche Command bleibt
  unveraendert; `Link_G` wird weiterhin erst beim Klick gelesen und ausserhalb
  der Player-Startfehlerbehandlung aufgeloest. Nullfall, null/leerer/Whitespace-
  Pfad, Erfolg, Resolver-Ausnahme und die produktive Command-Verdrahtung sind
  direkt geschuetzt. Das ViewModel delegiert nur noch und liegt bei 805
  nicht-leeren Zeilen. 14 gezielte sowie alle 5.139 UI-Tests bestanden; der
  Release-Build blieb bei 0 Warnungen und 0 Fehlern. Ein maschinengebundener
  Smoke-Test wurde wie zuvor uebersprungen.
- [x] Reine Excel-Zielpfad-, Fallback- und Kollisionsregeln aus
  `ExportPageViewModel` in `ExportExcelPathPolicy` verschieben; Dialog und
  Ordnererstellung bewusst im ViewModel belassen.
- [x] Pfad- und Ordnerbaum-Vorschau aus
  `DistributionTargetConfigViewModel` in den internen, zustandslosen
  `DistributionTargetPreviewBuilder` verschieben. Konstruktor, Commands,
  Konfiguration und Speicher-Callbacks bleiben unveraendert. Direkte Tests
  schuetzen Excel, Haltung, Schacht, Dichtheit, Normal/Sanierung, Video-Regel,
  Einruecktiefe und bereinigte Baumtexte. Das ViewModel sank von 419 auf 349
  Zeilen.
- [x] Fotopfad-Suche und sicheres Oeffnen aus `BeobachtungenWindow` in den
  WPF-freien `BeobachtungenPhotoOpenController` verschieben; das Fenster zeigt
  nur noch die bisherigen Meldungen an und behaelt seine oeffentliche Signatur.
- [x] Anwenden von Video-, PDF- und Fototreffern aus `MediaSearchWindow` in den
  WPF-freien `MediaSearchApplyController` verschieben. Dateinamen-Meter und die
  Auswahl des naechsten aktiven Protokolleintrags bis 1,0 m fuer Medien-Suche
  und Fotoimport in gemeinsamen Application-Regeln buendeln; Einstellungen und
  Fensterabschluss bleiben im Fenster.
- [x] Sortierung aktiver Protokolleintraege aus `ProtocolObservationsWindow` in
  `ProtocolEntryOrdering` verschieben. Meterquellen, Code-Reihenfolge,
  Gleichstand, geloeschte Eintraege sowie das bisherige NaN-/Infinity-Verhalten
  mit Charakterisierungstests sichern; Auswahl und Collection-Refresh bleiben
  im Fenster.
- [x] Datensatzbasierte Dropdown-Ergaenzung und Zerlegung empfohlener Massnahmen
  aus `DataPageViewModel` in `DataPageDropdownOptionSynchronizer` verschieben;
  Speichern und die feste Eigentuemer-Liste bleiben im ViewModel.
- [x] Die fuenf DataPage-Dropdown-Gruppen fuer Bearbeiten, Vorschau,
  Zuruecksetzen, Hinzufuegen und Entfernen ueber den gemeinsamen
  `DropdownOptionGroupController` fuehren; oeffentliche Commands und
  Speichergrenze bleiben unveraendert.
- [x] Die zwei ProjectPage-Dropdown-Gruppen ueber
  `ProjectPageDropdownCommandFactory`, `DropdownOptionGroupController` und
  `DropdownCommandFactory` fuehren. Die zehn oeffentlichen Commands,
  Metadaten-Synchronisierung, der aktuelle Sanieren-Wert nach Edit und die fest
  gesperrte Eigentuemer-Liste bleiben verhaltensgleich.
- [x] Die vier Schaechte-Dropdown-Gruppen samt Command-Erzeugung in der internen
  `SchaechteDropdownCommandFactory` buendeln. Die 20 oeffentlichen Commands,
  Titel, Resetwerte, feste Eigentuemer-Liste und Speichergrenzen bleiben
  unveraendert. Direkte Verhaltenstests pruefen alle vier Gruppen; ein
  Architekturtest schuetzt zusaetzlich die genaue Listen-, Aktions- und
  Command-Zuordnung. Das Haupt-ViewModel liegt nun bei 390 nicht-leeren Zeilen,
  die gebuendelte Factory bei 64. 48 gezielte Tests und alle 5.132 UI-Tests bestanden;
  der Release-Build blieb bei 0 Warnungen und 0 Fehlern. Ein maschinengebundener
  Smoke-Test wurde wie zuvor uebersprungen.
- [x] Sechs kopierte Zahlen-, Zeit- und VSA-Normalisierer aus
  `ObservationCatalogWindow` entfernen und alle Validierungs- sowie
  Lost-Focus-Wege auf `ProtocolEntryInputNormalizer` fuehren. UI-Meldungen,
  Kontrollzustaende und Apply-Reihenfolge bleiben im Fenster. Die drei
  verbliebenen Zahlen-/Anzeigehelfer im `ObservationCatalogViewModel` sind
  ebenfalls entfernt; Konstruktor und Apply-Weg delegieren direkt und behalten
  Meter-, Zeit-, VSA-Fallback- sowie Fehlermeldungsreihenfolge.
- [x] VSA-Defaults und Strecken-Normalisierung in `ProtocolEntryVM` auf
  `ProtocolEntryInputNormalizer` fuehren. Kanonische und alte Aliaswerte,
  idempotente Defaults, `A1`-Fallback, Metadaten-Mutation und bestehende
  `Parameters`-Benachrichtigungen bleiben verhaltensgleich.
- [x] Bereinigung, Konfliktprioritaet und Spiegelung der sieben VSA-/WinCan-
  Aliasgruppen aus `ProtocolEntryVM` nach `VsaParameterMerger.NormalizeAliases`
  verschieben. `Merge` bleibt der mutierende Weg, `NormalizeAliases` liefert einen
  neuen case-insensitiven Snapshot; Code-Override, Key-Schreibweise, UpdatedAt und
  UI-Benachrichtigungen bleiben kompatibel.
- [x] Materialaufloesung in `HydraulikPanelViewModel.LoadFromRecord` auf
  `HydraulikMaterialCatalog.ResolveRecordMaterial` fuehren. Der aktuelle
  Material-Objektfallback bleibt bei unbekannten Werten erhalten, einschliesslich
  benutzerdefinierter und `null`-Auswahl; nur echte Wechsel rechnen und speichern.
- [x] Die sechs identischen MPEG-/Protokoll-Zeitparser in `ProtocolTimeParser`
  buendeln und Meter-/Zeit-Tokens aus Finding-Rohtext in
  `ProtocolFindingRawParser` halten. PDF- und XTF-Fassaden bleiben als delegierende
  Kompatibilitaetswege bestehen; Fenster, Mapper und Eingaben verwenden dieselbe
  Zeitregel. Historische Fallbacks, Regex-Randfaelle und die Prioritaet von
  expliziten Werten vor Timestamp und Rohtext sind durch Tests gesichert.
- [x] Die reine Umwandlung alter Messvorlagen aus dem
  `MeasureTemplateEditorViewModel` in den zustandslosen
  `LegacyMeasureTemplateConverter` unter `Infrastructure/Costs` verschieben.
  Zeitstempel, Rueckfrage, JSON-Einlesen, Zusammenfuehren, Speichern und Meldungen
  bleiben unveraendert im ViewModel. Version, Reihenfolge, Duplikate, Trimmen,
  Namensfallback, leere IDs/Positionen und alle alten Mengenregeln bleiben exakt
  erhalten. 26 direkte Konverterfaelle, ein vollstaendiger Migrationslauf ueber
  das ViewModel und zwei Architekturtests sichern auch Ersetzen und Speichern.
  Das ViewModel sank von 521 auf 479 nichtleere Zeilen; der reine Konverter hat
  61 und braucht weder Interface noch ServiceProvider-Registrierung. Der
  Release-Build blieb bei 0 Warnungen und 0 Fehlern; alle 2.959 Infrastruktur-
  und 5.168 UI-Tests bestanden. Je ein daten- beziehungsweise maschinengebundener
  Test wurde wie zuvor uebersprungen.
- [x] Die doppelte Anschlussmengen-Entscheidung fuer bestehende Kostenzeilen aus
  `MeasureBlockVm` und `MeasurePricingEngine` in der reinen
  `ConnectionQuantityPolicy` buendeln. Erkennung ueber Schluessel oder Text,
  Deaktivierung bei 0/negativ, Reaktivierung nur bei abgewahlter Null-Zeile und
  Schutz manueller Mengen haben nun eine gemeinsame Quelle. Domain- und UI-Weg
  wenden den Plan getrennt an, damit `SetSuggestedQty`, Preisnachlauf,
  Installationsregel und die bisherige Ereignisreihenfolge erhalten bleiben.
  Insbesondere wird ein waehrend der Reaktivierung gesetzter Mengen-Override erst
  nach dem `Selected`-Ereignis gelesen. Der historisch abweichende Weg fuer neu
  hinzugefuegte Zeilen bleibt bewusst separat: Er uebernimmt die Anschlusszahl,
  behaelt aber sein bereits gesetztes Override-Flag. 26 fokussierte Infrastruktur-
  und 20 UI-/Architekturtests sichern beide Vertraege. Der Release-Build blieb bei
  0 Warnungen und 0 Fehlern; alle 2.968 Infrastruktur- und 5.177 UI-Tests
  bestanden. Je ein daten- beziehungsweise maschinengebundener Test wurde wie
  zuvor uebersprungen.
- [x] Projekt-, Video- und Bildpfade fuer den KI-Auftrag aus dem
  `ProtocolEntryEditorDialog` in den instanzgebundenen, WPF-freien
  `ProtocolEntryEditorMediaPathResolver` verschieben. Der ausdrueckliche
  Projektordner bleibt unveraendert; `Settings.LastProjectPath` wird weiterhin
  erst bei Bedarf gelesen und ueber `ProjectFileLocator` auf den echten Root
  gefuehrt. Arbeitsverzeichnis-Treffer, fehlende verwurzelte Pfade, relative
  Projektpfade, Fehlerweitergabe vor dem Busy-Zustand sowie Reihenfolge und
  gross-/kleinschreibungsunabhaengige Bild-Dubletten behalten ihren Altvertrag.
  Sieben direkte Resolver-Tests und ein Architekturtest schuetzen Verhalten,
  spate Einstellungswechsel und die dateifreie Fenstergrenze. Der Dialog sank von
  672 auf 629 nichtleere Zeilen; der Resolver hat 61. Release-Build mit 0
  Warnungen und 0 Fehlern, 20 fokussierte Tests sowie alle 5.165 UI-Tests sind
  gruen; ein maschinengebundener Smoke-Test wurde wie zuvor uebersprungen.
- [ ] Die bestehenden Medienpfad-Risiken separat fachlich entscheiden: Eine
  moegliche `null`-Fotoliste aus alten oder defekten Daten, laufwerksrelative
  Windows-Pfade wie `C:foto.jpg`, direkte Arbeitsverzeichnis-Treffer und `..`
  ausserhalb des Projektordners passen nicht zum heutigen `*PathAbs`-Vertrag.
  Eine Korrektur waere eine bewusste Verhaltens- und Sicherheitsaenderung, kein
  reines Struktur-Refactoring.
- Fotomessung in Werkzeugsteuerung, Eingabe und Rendering teilen. Erst beginnen,
  wenn die aktuellen Geometrieaenderungen abgeschlossen sind.
- [x] Den davon unabhaengigen Messfoto-Overlay-Export vorziehen: Der interne
  `PhotoMeasurementOverlayExporter` rendert den lebenden WPF-Canvas synchron in
  Original-Pixelgroesse und schreibt die bestehende PNG-Ableitung; der
  `PhotoMeasurementCompletionWorkflow` schuetzt Erfolg, stilles `null`, Fehler
  und das trotzdem bestaetigte Messergebnis. STA-Pixeltests pruefen
  Letterbox-Ausschnitt, 96 DPI, Originalschutz, Ueberschreiben und Dateisperre.
  Die parallelen Geometrieaenderungen blieben unangetastet; die beiden
  Fenster-Partials sanken zusammen von 1.411 auf 1.354 Zeilen.
- `DataPage` und `SchaechtePage` nur entlang bereits sichtbarer Verantwortungen
  weiter teilen; keine neuen Sammel-Controller bauen.
- [x] Einen isolierten WPF-Smoke-Testprozess fuer Fenster mit app-weiten
  `StaticResource`-Abhaengigkeiten aufbauen. Der Elterntest bleibt ohne
  `Application`; ein begrenzter `vstest`-Kindprozess laedt die echten
  `App.xaml`-Ressourcen und oeffnet, layoutet sowie schliesst
  `BeobachtungenWindow`. Eine zufaellige Empfangsbestaetigung verhindert einen
  stillen Erfolg bei einem falschen Testfilter. Timeout, Prozessbaum-Abbruch und
  ein Verbot verschachtelter Kindprozesse sichern Fehler und Haenger ab.

Geschaetzter Aufwand: 1 bis 2 Wochen.

### Phase 4 - Grosse, gekoppelte Bereiche

- [x] Einfaerbung der Coding-Ereignisliste und Hervorhebung des
  Protokollabgleichs aus zwei `PlayerWindow`-Partials in den eigenstaendigen
  `CodingEventListVisualController` verschieben. Coding- und Importliste nutzen
  weiterhin denselben laufenden Match-Zustand und denselben Loaded-Nachlauf;
  echte WPF-Verhaltenstests schuetzen Farben, Statussymbol, Badge, Bereinigung
  und unterschiedlich lange Listen. `PlayerWindow` sank von 76 auf 74 Partials
  und von 4.263 auf 4.196 aggregierte Zeilen.
- [x] Zustandsbehaftete Streckenschaden-Verfolgung aus dem letzten
  `PlayerWindow`-Partial in den `CodingStreckenschadenTrackingController`
  verschieben. Genau eine Tracker-Instanz bedient Analyse-Ticks, BCE, Exit und
  Session-Reset; auch leere Ticks erreichen weiterhin den Tracker. Direkte
  Controller-Tests schuetzen Oeffnen, automatisches Schliessen am letzten
  Sichtmeter, aktuellen Videozeitbezug, fehlende Session und echten Reset.
  `PlayerWindow` sank von 74 auf 73 Partials und von 4.196 auf 4.125
  aggregierte Zeilen.
- [x] Sperren und Wiederherstellen der Coding-Zeichenflaeche in den
  `CodingOverlayInputVisibilityController` verschieben. Dialoge, asynchrone
  Codeauswahl, Fenster-Deaktivierung und Codiermodus-Reset verwenden dieselbe
  Instanz und denselben vorhandenen Zustand. Fuenf Controller-Tests schuetzen
  Verschachtelung, Fehler-Rueckweg, offene und geschlossene Popups sowie Reset;
  die bestehenden Workflow- und Architekturtests pruefen alle echten
  Anschluesse. `PlayerWindow` sank von 73 auf 72 Partials und von 4.125 auf
  4.051 aggregierte Zeilen.
- [x] KI-Frame-Anhaengen, Snapshot-Rueckfall und manuelle Fotoaufnahme im
  `CodingPhotoAttachmentController` buendeln. Der Controller verwendet die
  vorhandenen Foto-Workflows und den zentralen `ICodingFramePhotoStore`; das
  Fenster liefert nur aktuellen Frame, Video, Sitzung und UI-Aktionen. Sieben
  Controller-Tests schuetzen bevorzugten und gepufferten Frame, Snapshot-
  Rueckfall, Leerfall, den produktiven Hintergrundstart, den synchronen
  Rohrgrenzen-Pfad sowie Zeitruecksetzung und Session-Update bei manuellen Fotos.
  Die private Foto-Fassade sank von 67 auf 19 Zeilen; `PlayerWindow` hat aktuell
  72 Partials mit 4.019 aggregierten Zeilen.
- [x] Die Erzeugung der beiden Coding-Timeline-Commands aus
  `PlayerWindow.Coding.Lifecycle.Timeline` in die interne
  `CodingTimelineCommandFactory` verschieben. Die Factory erzeugt bei jeder
  Initialisierung frische Commands; Dienst und Laufstatus werden weiterhin erst
  bei jedem Klick gelesen. Auch der austauschbare Ereignislisten-Controller wird
  wie zuvor erst beim Marker-Klick aufgeloest. Direkte Factory-Tests sichern
  Move-Pending-Sync, Jump-Select, Null-/Fremdmarker und getrennte Bindings; der
  Architekturtest schuetzt die positionsgenaue Uebergabe beider Commands. Das
  Lifecycle-Partial sank von 44 auf 34 Zeilen. Release-Build und alle 5.129
  UI-Tests blieben gruen; ein maschinengebundener Smoke-Test wurde wie zuvor
  uebersprungen.
- [x] Die verschachtelte Erzeugung von Live-Detection-Status und Puls aus dem
  `PlayerWindow`-Konstruktor in den lokalen
  `PlayerWindowLiveDetectionStatusInitializer` verschieben. Ein ausdrueckliches
  Buendel ordnet weiterhin dieselben 14 WPF-Controls zu; vorhandener Pulszustand
  und Fenster-Dispatcher bleiben unveraendert. Der Puls wird zuerst erzeugt und
  genau dieselbe Instanz wird an den Status-Controller gebunden und an das Fenster
  zurueckgegeben. Ein direkter STA-Test prueft alle Anzeigen sowie Start, Stop,
  Neustart und Ruecksetzen der Ring- und Skalierungsanimation. Der Architekturtest
  sichert Control-Zuordnung, Dispatcher, Erzeugungsreihenfolge und den Anschluss
  direkt nach `InitializeComponent()`. Die Hauptdatei sank physisch von 733 auf
  714 Zeilen; der neue Initializer hat 77 nichtleere Zeilen. Release-Build mit
  0 Warnungen und 0 Fehlern, 31 fokussierte Tests sowie alle 5.156 UI-Tests sind
  gruen; ein maschinengebundener Smoke-Test wurde wie zuvor uebersprungen.
- [x] Die gemeinsame Erzeugung von Entscheidungs- und Anzeige-Controller der
  Coding-Bestaetigung in die lokale
  `PlayerWindowCodingConfirmationControllerFactory` verschieben. Beide verwenden
  denselben offenen Bestaetigungszustand; der Decision-Controller bleibt eine
  interne Einzelheit der Factory. Sitzung, Ereignissammlung, Statustext,
  Live-AI-Schalter und Modellname werden weiterhin erst bei der Benutzeraktion
  gelesen. Panel, Ereignisauswahl, Training-Persistenz mit unveraendertem
  Fire-and-forget-Operationsnamen, Pause/Fortsetzen und Erfolgsstatus behalten
  ihre bisherigen Anschluesse. Ein direkter STA-Durchstich prueft spaet gesetzte
  Laufzeitwerte, Pause, Panel, Ablehnen, Persistenzstart, Loeschen, Aktualisieren,
  Fortsetzen und gemeinsamen Zustand; der Architekturtest sichert die zwoelf
  produktiven Abhaengigkeiten und nur die echten Initialisierungs-Voraussetzungen.
  Die Hauptdatei sank physisch von 714 auf 699 Zeilen; die Factory hat 74
  nichtleere Zeilen. Release-Build mit 0 Warnungen und 0 Fehlern, 12 fokussierte
  Tests sowie alle 5.157 UI-Tests sind gruen; ein maschinengebundener Smoke-Test
  wurde wie zuvor uebersprungen.
- [x] `LiveDetectionStopController` und `LiveDetectionLifecycleController` im
  lokalen `PlayerWindowLiveDetectionControllerSetFactory` gemeinsam erzeugen.
  Die Factory baut den Stop-Controller zuerst, bindet genau dessen Stop-Weg an
  den Lifecycle und gibt beide Instanzen als Set zurueck. Runtime-Zustand,
  Shutdown, manueller Markiermodus, Ereigniszahl und Wiedergabestatus werden
  weiterhin erst bei der Aktion gelesen. Sechs WPF-Controls bleiben als
  ausdrueckliches Buendel sichtbar; Startdialog, verzoegertes Ausblenden,
  Statuswerte, Pause, Timer-Tick und erste Erkennung behalten ihre vorhandenen
  Wege und Operationsnamen. Direkte STA-Tests pruefen normalen Stop, spaet
  geaenderte Werte, entsorgte Wiedergabe, manuelle Markierung, erfolgreichen
  Start und Startabbruch am echten Schalter. Der Konstruktorblock sank von 47
  auf 19 Zeilen; die lokale Factory hat 126 nichtleere Zeilen und braucht keine
  `ServiceProvider`-Registrierung. 19 fokussierte Tests und alle 5.183 UI-Tests
  bestanden; ein maschinengebundener Smoke-Test wurde wie zuvor uebersprungen.
  Der Release-Build blieb bei 0 Warnungen und 0 Fehlern; drei unabhaengige
  Gegenpruefungen sind nach zwei geschlossenen Testluecken ohne Befund.
- [x] Die drei zusammengehoerigen Verdrahtungsphasen des manuellen
  Live-Detection-Markierwerkzeugs aus dem `PlayerWindow`-Konstruktor in die
  lokale `PlayerWindowLiveDetectionMarkToolControllerFactory` verschieben.
  Die Factory verwendet die vorhandenen Runtime- und Schema-Zustandsbuendel und
  genau denselben `CodingSessionRuntime` wie das Fenster. Punktwerkzeuge erzeugen
  weiterhin keinen Coding-Zustand; Zeichenwerkzeuge bauen beim Kaltstart
  Session-, Overlay- und ViewModel-Owner auf und verwenden vorhandene Dienste
  referenzgleich wieder. Video-Pfad, Einstellungen, Trainingsspeicher,
  Owner-Zustand, Codiermodus und laufende Erkennung werden erst bei der Aktion
  gelesen. `observePropertyChanged: false`, beide Popup-Wege, das bewusste
  Ignorieren boolescher Host-Rueckgaben und beide Deaktivierungsregeln bleiben
  erhalten. Der Eingabemarker verwendet weiterhin dieselbe Controller-Instanz.
  Der Konstruktor sank um 31 Zeilen auf 615 nichtleere Zeilen; die Factory hat
  89 nichtleere Zeilen und braucht keine `ServiceProvider`-Registrierung. 32
  fokussierte Tests sowie alle 5.189 UI-Tests bestanden; ein maschinengebundener
  Smoke-Test wurde wie zuvor uebersprungen. Release-Build mit 0 Warnungen und
  0 Fehlern; drei unabhaengige Gegenpruefungen sind nach den geschlossenen
  Kaltstart-, Popup- und Guard-Luecken ohne Befund.
- [x] `CodingEingabemarkerInteractionController`,
  `CodingEingabemarkerSubmissionController` und
  `CodingEingabemarkerInputController` in der lokalen
  `PlayerWindowCodingEingabemarkerControllerSetFactory` als gemeinsamen
  Objektgraph erzeugen. Die Factory baut Interaction, Submission und Input in
  dieser Reihenfolge und bindet genau dieselben Instanzen weiter. Controls und
  Fensteraktionen bleiben einzeln benannt; Text, Session-Service, Ereignisse,
  Overlay, OSD-/Session-Meter, Session-/Player-Zeit, Label und Foto werden erst
  bei der Aktion gelesen. Statusmeldungen, KI-Fallback und die
  Fire-and-forget-Namen `TrainingSaveSingle` und `SubmitEingabemarker` sind
  unveraendert. Vier direkte STA-Durchstiche pruefen echte WPF-Controls,
  gemeinsame Instanzen, den blockierten Analyse-Zwischenzustand, spaet gesetzte
  Direkt-Ereignisquellen und beide Operationsnamen. Der Konstruktor sank um 51
  Zeilen auf 564 nichtleere Zeilen; die reine Composition-Factory hat 211
  nichtleere Zeilen und braucht keine `ServiceProvider`-Registrierung. 89
  fokussierte Tests sowie alle 5.193 UI-Tests bestanden; ein maschinengebundener
  Smoke-Test wurde wie zuvor uebersprungen. Release-Build mit 0 Warnungen und
  0 Fehlern; die unabhaengigen Verhaltens-, Qualitaets- und Testreviews sind nach
  den geschlossenen Guard- und Zwischenzustandsluecken ohne Befund.
- [x] Die Erzeugung des `CodingModeExitController` aus dem `PlayerWindow`-
  Konstruktor in die lokale `PlayerWindowCodingModeExitControllerFactory`
  verschieben. Die Factory verdrahtet weiterhin die vorhandenen Finalisierungs-
  und Teardown-Workflows; Ereignisse, Meter, Videozeit, Frame und die drei
  optionalen Laufzeitzustaende werden erst beim Ausstieg gelesen. Ein
  abgebrochener Abschluss stellt denselben Codiermodus wieder her und fuehrt
  keinen Teardown aus. Die 27 Teardown-Schritte, ihre Reihenfolge, der vorhandene
  `CodingBoundaryContext`, dieselben zustandsbehafteten Controller und die
  gemeinsame `ResetFrameReadiness`-Regel bleiben erhalten. Sieben direkte
  STA-Testfaelle pruefen Abbruch, erfolgreichen Abschluss, unabhaengige
  ViewModel-/Live-AI-/Detection-Zustaende, gemeinsame Owner und wichtige
  Reihenfolgen. Architekturguards sichern alle benannten Bindings, genau einen
  Factory-Aufruf und die Erzeugung nach den drei benoetigten Controllern. Der
  Konstruktor sank um weitere 32 Zeilen auf 532 nichtleere Zeilen; die reine
  Composition-Factory hat 207 nichtleere Zeilen und keine ServiceProvider-
  Registrierung. 93 fokussierte Tests sowie alle 5.200 UI-Tests bestanden; ein
  maschinengebundener Smoke-Test wurde wie zuvor uebersprungen. Der vollstaendige
  Release-Build blieb bei 0 Warnungen und 0 Fehlern; die unabhaengigen
  Verhaltens-, Qualitaets- und Testreviews sind ohne Befund.
- [x] Die Erzeugung des `LiveDetectionMarkSegmentationController` aus dem
  `PlayerWindow`-Konstruktor in die lokale
  `PlayerWindowLiveDetectionMarkSegmentationControllerFactory` verschieben. Die
  Factory verwendet genau den vorhandenen `CodingAiController`, Overlay-Host,
  `CodingOverlayCanvas` und Inhaltsrechteck-Resolver. Der beim Fensteraufbau noch
  leere Box-Segmentierungsdienst, die Kalibrierung und das Inhaltsrechteck werden
  weiterhin erst bei der jeweiligen Aktion gelesen. Bogenmarker und SAM-Masken
  verwenden dasselbe Canvas; `CancellationToken.None`, `PlayerTrace` sowie die
  vorhandenen Segmentierungs-, Render- und Quantifizierungsworkflows bleiben
  unveraendert. Zwei direkte STA-Testfaelle pruefen Kaltstart ohne Dienst,
  nachtraeglich gesetzten Runtime-Dienst und Kalibrierung, DN und Token, spaetes
  Rechteck, Masken und Bogenmarker auf demselben Canvas sowie Nullschutz. Zwei
  Architekturguards sichern die vier produktiven Zuordnungen, genau einen
  Factory-Aufruf, dieselbe mutable AI-Instanz und die Controller-Grenzen. Der
  Konstruktor sank um weitere 16 Zeilen auf 516 nichtleere Zeilen; die reine
  Composition-Factory hat 52 nichtleere Zeilen und keine ServiceProvider-
  Registrierung. 26 fokussierte Tests sowie alle 5.202 UI-Tests bestanden; ein
  maschinengebundener Smoke-Test wurde wie zuvor uebersprungen. Der vollstaendige
  Release-Build blieb bei 0 Warnungen und 0 Fehlern; die unabhaengigen
  Verhaltens-, Qualitaets- und Testreviews sind ohne Befund.
- `PlayerWindow` weiter in kleine Controller mit eindeutigem Lebenszyklus zerlegen.
- Verbleibende direkte Dateioperationen einzeln hinter Instanzdienste verschieben;
  reine Rechner duerfen statisch bleiben.
- Training und KI-Pipeline erst nach Abschluss der aktuellen Pipeline-Arbeiten
  wieder in den Wartbarkeitslauf aufnehmen.

Geschaetzter Aufwand: 2 bis 4 Wochen in kleinen Paketen.

## Abnahme je Paket

Ein Paket gilt erst als fertig, wenn:

- das sichtbare und gespeicherte Verhalten gleich bleibt,
- ein passender Verhaltenstest besteht,
- der schnelle Release-Build ohne Warnung und Fehler besteht,
- keine neue God-Class, Schichtverletzung oder doppelte Wahrheitsquelle entsteht,
- der Architektur-Skill geprueft und bei echtem Architekturwechsel aktualisiert ist,
- vor einem Commit der vollstaendige Release-Testweg aus `AGENTS.md` gruen ist.

## Realistische Gesamtdauer

Die wichtigsten offenen Wartbarkeitsbremsen lassen sich voraussichtlich in 4 bis 7
Wochen bereinigen. Eine vollstaendige Perfektion ist kein sinnvoller Endpunkt;
danach wird die Qualitaet ueber Tests, Architekturregeln und kleine Pakete dauerhaft
gehalten.
