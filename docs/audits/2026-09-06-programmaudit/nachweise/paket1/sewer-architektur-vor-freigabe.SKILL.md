---
name: sewer-architektur
description: Aktuelle Codebase-Karte für SewerStudio mit Schichten, ServiceProvider, Importen, KI-Pipeline, KnowledgeBase, TrainingDataInventory und YOLO-Klassenkarten. Bei jeder Arbeit am SewerStudio-Projekt verwenden, besonders bei neuen Services, Import/Export, KI-Pipeline, Training, Datenpfaden und Abhängigkeitsregistrierung.
---

# SewerStudio-Architektur

Geprüfter Stand: 2026-09-05

Zuletzt mit Code geprüft: 2026-09-05

## Verbindliche Grundlage

- Vor Änderungen `AGENTS.md` und `CLAUDE.md` vollständig lesen.
- Aktuellen Code und Tests als Nachweis verwenden. Geplante Klassen nie als vorhanden darstellen.
- Kundenoriginale nie verändern. Dateioperationen absichern und Fehler je Datei melden.
- Keine God-Class erweitern. Fachlogik in kleine Services legen; UI-Code bleibt dünn.
- Öffentliche Fassaden und gespeicherte Formate bei Aufräumarbeiten erhalten.
- Keine neuen NuGet-Pakete oder großen Umbauten ohne Rücksprache.

## Projektimport: nachgeprüfte Regeln (05.09.2026)

- `PdfDokumentTypErkennung` erkennt Schachtprotokolle als eigenen Typ.
  `KanalImportDistributionService` schliesst diese vor dem Haltungs-Split aus.
  Göschenen 2026 enthält 261 solche PDFs; deren Schachtmasse wurden sonst als
  Haltungspaare fehlgelesen und durch den teuren Haltungs-/OCR-Weg geschickt.
  Positive TV-Merkmale behalten Vorrang für gemischte Sammelberichte.
- Umgekehrt prüft `ShaftPdfRelevance` vor dem Schachtverteiler alle Seiten ohne
  OCR. Nur durchgehend eindeutig fremde Dokumente werden übersprungen; unklare,
  leere/Bild- und Schachtseiten bleiben erhalten. WinCan-Projektdeckblätter werden
  nur mit den drei Merkmalen Projekt/Kunde/Unternehmer und ohne Schachtbezug erkannt.
  Im gemischten Bericht trennen eindeutige Haltungsseiten die Schachtabschnitte,
  ohne selbst OCR auszulösen oder an den vorherigen Schacht angehängt zu werden.
- Umgekehrt prüft `ShaftPdfRelevance` vor dem Schachtverteiler alle Seiten ohne
  OCR. Nur durchgehend eindeutig fremde Dokumente werden übersprungen; unklare,
  leere/Bild- und Schachtseiten bleiben erhalten. WinCan-Projektdeckblätter werden
  nur mit den drei Merkmalen Projekt/Kunde/Unternehmer und ohne Schachtbezug erkannt.
  Im gemischten Bericht trennen eindeutige Haltungsseiten die Schachtabschnitte,
  ohne selbst OCR auszulösen oder an den vorherigen Schacht angehängt zu werden.
- `XtfQuellenPruefer` liest Modell und XML-Objekte mit Unterlesern und liefert
  `XtfQuellenmerkmale.Inhaltsbelege`. `XtfExportAuswahl` verwirft eine Quelle nur
  bei belegter Inhalts-Teilmenge, nie allein wegen gleicher Objektzahlen.
  Umnummerierte TID-/REF-Graphen werden konservativ als weitere Quelle behalten.
- `Befahrungsrollen` trennt Kamerarichtung von Videorolle. Die aktive WinCan-
  Untersuchung bestimmt das Hauptvideo; upstream allein belegt keine Gegenfahrt.
- `ProtocolRevision.ImportFingerprint` und `ImportVideoPaths` sind optionale,
  rückwärtskompatible Quellmetadaten. WinCan und Schacht-XTF vermeiden damit
  identische zusätzliche Revisionen. Der Kanalverteiler übernimmt auch Videos
  weiterer Untersuchungen ins Projekt und relativiert die Revisionsverweise.
- `ShaftDistributionService` liest vorbereitete Archiv-PDFs über die gemeinsame
  Importtransaktion. `StagedDistributionOutput` erhält logische Dateiendungen und
  trennt gleichnamige Eingaben in privaten Unterordnern.
- `ImportProjektdateiPruefer` prüft vorhandene Verweise gegen sichere und lesbare
  Projektdateien bzw. die vorbereitete Lesesicht. `ImportBestandsbilanz.DateienGeprueft`
  unterscheidet diese Prüfung vom blossen Zählen gespeicherter Pfade. Fehlende
  referenzierte WinCan-Medien und Verteilfehler gehören in die Fehlerbilanz.
- XTF-Feldherkunft beweist nicht, dass eine TID ersetzbar ist. Abweichende
  importierte Kennungen blockieren den Katasterabgleich als Prüffall;
  nachweislich eigene `chSST`-Exportkennungen dürfen weiterhin ergänzt werden.
- Nach dem letzten Importschritt und vor Veröffentlichung prüft der Ein-Knopf-
  Controller erneut den Abbruch. Keine Veröffentlichung bei abgebrochenem Lauf.
- Nachweis und Abnahmegrenzen: `docs/PROJEKTIMPORT-TERRA-FORTSCHRITT.md`, oberster Abschnitt.

## Projektaufbau

```text
src/AuswertungPro.Next.Domain/          Fachmodelle und Feldschlüssel, kein Datei-I/O
src/AuswertungPro.Next.Application/     Schnittstellen, Verträge und reine Fachregeln
src/AuswertungPro.Next.Infrastructure/  Datei-I/O, Import, SQLite, HTTP und KI-Dienste
src/AuswertungPro.Next.UI/              WPF, ViewModels und Zusammensetzung der Dienste
tests/                                  fokussierte .NET-Tests
tools/                                  eigenständige Kommandozeilen-Werkzeuge
sidecar/sidecar/                        lokaler FastAPI-Sidecar für YOLO, DINO und SAM
```

`AuswertungPro.sln` enthaelt die vier produktiven Projekte, die vier Testprojekte
und alle 44 `tools/**/*.csproj`. Neue Werkzeugprojekte sofort aufnehmen, damit
Projektverweis- und Namespacebrueche im normalen Release-Build sichtbar werden.

Nachaudit 2026-08-22: Der IBAK-FDB-Import verwendet `fbclient.dll` nur aus
`AppContext.BaseDirectory`, nie aus einem Kunden- oder Importordner. Die acht
zuvor ungeschützten lokalen Review-Server teilen Host-, JSON- und
64-KiB-Body-Prüfungen in `tools/EvalVisibilityReview/review_server_security.py`.
Blockierende Trainingsskripte besitzen endliche Subprocess-Zeitlimits;
`osd_hd_validierung_vorbereiten.py` ersetzt nur ausdrücklich freigegebene,
markierte Arbeitsordner. `TrainingCenterViewModel` gibt seinen eigenen
Knowledge-Base-HTTP-Client beim Schließen des Fensters frei.

SewerStudio ist eine Windows-WPF-Anwendung auf .NET 10. Der aktive Fachkatalog ist
`vsa_kek_2020_catalog_manifest.json` für VSA-KEK und EN 13508-2.

## ServiceProvider und Registrierung

Die zentrale Zusammensetzung liegt in `src/AuswertungPro.Next.UI/ServiceProvider.cs`.
Neue gemeinsam genutzte Dienste dort genau einmal erzeugen und ueber
`ServiceProviderRegistrationMap.Create(...)` unter ihrem Interface eintragen.
Kein verstreutes `new` in ViewModels oder Fenstern.

Wichtige Zugriffe:

- `Projects`: `IProjectRepository`
- `PdfImport`, `XtfImport`, `WinCanImport`, `IbakImport`, `KinsImport`
- `PdfFileReplacement`: `IAtomicPdfFileReplacer`; `PdfTextLayerRewrite`: `IPdfTextLayerRewriter`
- `ShellOpen`: `ISafeShellOpenService`; `ExplorerReveal`: `IExplorerRevealService`
- `StoredImportFiles`/`StoredImportFilePaths`: Schreiben und Auflösen gespeicherter Importquellen
- `ImportFileStaging`: `IImportFileStagingService`; `ImportMediaDistribution`: `IImportMediaDistributionService`
- `ShaftDistribution`: `IShaftDistributionService`; projektinterne Ziele verwenden die Importtransaktion
- `CodeCatalog`: aktiver VSA-Codekatalog
- `KnowledgePaths` und `KnowledgeRoot`: einmal aufgelöster KB-Ort
- `Retrieval`: optionale KB-Suche; kann `null` sein
- `AiSettings`: `IAiPlatformSettingsResolver`
- `GpuModels`: `IGpuModelSelector`
- `TrainingSamples`: `ITrainingSampleStore`
- `TrainingCenterDocuments`: `ITrainingCenterDocumentStore`; die UI-Fassade mappt nur
- `TrainingDataInventory`: `ITrainingDataInventoryService`
- `TrainingExportRegistry`: `ITrainingExportRegistryStore`
- `TrainingExportPlanInput`: `ITrainingExportPlanInputBuilder`
- `TrainingExportPlans`: `ITrainingExportPlanService`
- `TrainingExportSidecarRequests`: `ITrainingExportSidecarRequestBuilder`
- `TrainingExportLocalExecutor`: `ITrainingExportPlanLocalExecutor`
- `TrainingExportCompletion`: `ITrainingExportCompletionService`
- `TrainingExportExecution`: `ITrainingExportExecutionService`
- `TrainingYoloExportCoordinator`: `ITrainingYoloExportCoordinator`
- `TrainingYoloExport`: dünner Anschluss des Coordinators für den Trainingsbereich
- `VsaYoloClasses`: `IVsaYoloClassMapStore` für den bewusst erweiterbaren Live-Teacher
- `TrainingYoloClasses`: `ITrainingYoloClassMapStore` für den strikt lesenden Detect-Export

`IToastService.Success(message, aktionText, aktion)` ist die kompatible Standardmethode
fuer einen Erfolgs-Toast mit Link. `ToastService` reicht Text und Aktion ueber die einmalig
vom `MainWindow` angeschlossene Senke an `ToastHost` weiter. Excel- und XTF-Export verwenden
den Link zum Ausgabeort; `ImportReportNavigationController` bietet den zuletzt abgelegten
Bericht an. Die Datei wird weiterhin nur ueber die bestehenden sicheren Oeffnungsdienste
geoeffnet.

Die Einstellungssuche bleibt lokal in der UI und braucht keine ServiceProvider-Registrierung.
`SettingsSearchMatcher` ist der reine, Umlaut-tolerante UND-Abgleich.
`SettingsSearchController` liest Ueberschrift, Texte, Beschriftungen und Tooltips der
`GroupBox`-Gruppen, blendet Nichttreffer aus und waehlt bei Bedarf den ersten passenden Reiter.

Die PDF-Textkorrektur ist ueber `IPdfTextLayerRewriter` angebunden.
`TryRewriteHoldingNumber` erzeugt eine korrigierte Kopie und laesst die Quelle
unveraendert. `RewriteIdentifierInPlace` ist der ausdruecklich schreibende
Stapelweg fuer bereits veroeffentlichte Protokoll-PDFs: `PdfTextLayerRewriteService`
veroeffentlicht jede Korrektur ueber `IAtomicPdfFileReplacer`, zaehlt `Rewritten`,
`Skipped` und `Failed` je Datei, liefert Fehlerdetails, protokolliert Fehler mit
Dateipfad und bewahrt die vorige Fassung als `.bak`.
`DataPageViewModel` und `SchaechtePageViewModel` erhalten den Dienst im produktiven
Weg aus dem `ServiceProvider`; die Umbenennungswege duerfen dafuer nicht direkt
`HoldingFolderDistributor` aufrufen. `HoldingFolderDistributor.RewriteHoldingInPdfFiles`
bleibt nur als kompatible Fassade. Bekannter Altfall: Bereits aufgeloeste absolute
PDF-Pfade besitzen noch keinen Projektwurzel-/Eigentumsnachweis; diesen schreibenden
Weg nicht auf weitere Aufrufer ausdehnen, bevor die fachliche Regel geklaert ist.

Die Haltungsumbenennung laeuft ueber `IHoldingRenameService` und
`HoldingRenameFileService`. Schreibzugriffe sind auf `Haltungen_Verteilt`,
das alte `Haltungen` und `Fotos\Haltungen` innerhalb des echten Projekt-Roots
begrenzt; gleich benannte externe Kundenordner werden weder verschoben noch
umverlinkt. `HoldingFolderRenameTransaction` benennt Dateien und Unterordner
rekursiv, ueberspringt Reparse Points und protokolliert jeden Move fuer einen
vollstaendigen Rollback. Bei den SewerStudio-Namen
`JJJJMMTT_<Haltung>`/`JJJJMMTT-<Haltung>` erkennt sie auch eine alte Nummer,
die vom bisherigen Datenwert abweicht. `DataPageHoldingRenameController` bleibt
die duenne UI-Orchestrierung fuer Dienst, Datenwert, Korrekturmetadaten und
PDF-Textkorrektur. Fuer diesen Haltungsweg gelangen nur PDFs unter den
projektinternen Wurzeln `Haltungen_Verteilt` oder `Haltungen` in den schreibenden
PDF-Stapel; externe Kunden-PDFs werden ausgeschlossen.

`MediaConflictsPageViewModel`, `DataPageViewModel`, `BuilderPageViewModel` und
`SchaechtePageViewModel` erhalten `ISafeShellOpenService` gezielt aus dem
`ServiceProvider`; die Datenseiten erhalten dort auch `IExplorerRevealService`.
`SafeShellOpenService` übernimmt Video-Endungen ausschliesslich aus
`Infrastructure.Media.MediaFileTypes`; insbesondere `.wmv`, `.mp2` und `.webm`
dürfen nicht durch eine zweite Öffnen-Teilliste verloren gehen.
Foto-, Original-PDF-, Dichtheits-PDF-, Druck-, Druckcenter- und
Schachtprotokoll-Aufrufe verwenden den injizierten Oeffnungsdienst.
`BeobachtungenWindow` erhaelt `IInspectionProtocolFileLocator` und
`ISafeShellOpenService` nur als gezielte Konstruktorabhaengigkeiten. Die WPF-freie
`BeobachtungenPhotoOpenController` loest den Fotopfad auf und oeffnet ihn; das
Fenster besitzt weder eigene Dateiexistenzpruefung noch einen `ServiceProvider`.
Datei- und Ordnerbefehle duerfen dort weder `Process.Start` noch die statische
`SafeShellOpen`-Fassade verwenden. Nur ausdruecklich obsolete Kompatibilitaetswege
duerfen noch auf die unveraenderliche Fassade zurueckfallen.

`SchaechteFileActionController` loest Schachtprotokoll- und Explorerziele ueber
`ISchachtFileTargetResolver` auf und verwendet `ISafeShellOpenService`,
`IExplorerRevealService` sowie den injizierten `IDialogService`. Er ist
zustandslos und WPF-frei. `SchaechtePage` erzeugt ihn bei jedem Datei-Klick aus
dem lokal festgehaltenen aktuellen `SchaechtePageViewModel` und uebergibt Auswahl
und `Settings.LastProjectPath` aus derselben Instanz. Den Controller nicht ueber
DataContext-Wechsel speichern; Resolver-, Oeffnungs- und Dialoglogik nicht wieder
in die Seite kopieren. XAML-Kontextmenue und Schachtansicht routen weiterhin ueber
`ProtokollMenu_Click` beziehungsweise `OpenContainingFolderMenu_Click`.

`DataPageVideoPlaybackController` besitzt den Start normaler und
gegeninspizierter Haltungsvideos. `PlayCounterInspection` liest `Link_G`, laesst
den Pfad ueber den vom ViewModel uebergebenen aktuellen Projektresolver aufloesen
und delegiert einen gueltigen Pfad an `PlayResolved`. Ein fehlender Pfad zeigt die
bestehende Gegeninspektions-Meldung. Die Pfadaufloesung liegt bewusst vor der
Startfehlerbehandlung in `PlayResolved`; Resolverfehler nicht als Playerfehler
umdeuten. `DataPageViewModel` behaelt die beiden oeffentlichen Commands, darf aber
Feldzugriff, Meldung und Playerstart nicht wieder lokal duplizieren.

Im Dossier-Cockpit traegt `DossierHoldingRow` die stabile `HoldingId`.
`DossierHoldingActionController` loest sie bei jeder Aktion gegen das aktuelle
Projekt auf und delegiert Video, Originalprotokoll und Navigation. Die
`DossierHoldingActionFactory` verbindet ihn mit `DataPageVideoPlaybackController`,
`DataPageOriginalPdfController`, `IInspectionProtocolFileLocator` und
`ISafeShellOpenService`; Fenster-, Such- und Oeffnungslogik nicht in
`DossiersPage` oder `DossiersPageViewModel` kopieren. Der Code-Behind darf nur
die rechts angeklickte Zeile auswaehlen. `ShellViewModel.NavigateToHolding` ist
der gemeinsame direkte Navigationsweg fuer Dossier und Karte und selektiert den
Originaldatensatz auf der Seite `Haltungen`.

`DossierShaftRow` traegt entsprechend die stabile `ShaftId`.
`DossierShaftActionController` loest sie vor Protokoll- und Navigationsaktion gegen
das aktuelle Projekt auf. `DossierShaftActionFactory` delegiert die PDF-Oeffnung an
denselben `SchaechteFileActionController` wie die Schachtseite und den Sprung an
`ShellViewModel.NavigateToShaft`; keine zweite Pfad-, Oeffnungs- oder
Navigationslogik in `DossiersPageViewModel` einfuehren. Der Code-Behind waehlt auch
hier nur die rechts angeklickte Zeile. `NavigateToShaft` oeffnet die Seite
`Schaechte` und selektiert dort den Originaldatensatz.

`MediaSearchApplyController` wendet markierte Video-, PDF- und Fototreffer auf
Haltungen und Protokolle an. `MediaSearchWindow` behaelt nur Suchablauf,
Ergebniszaehler, `AppSettings.Save`, `DialogResult` und `Close`; diese Reihenfolge
nicht in den Controller verschieben. `PhotoFileMeterParser` ist die gemeinsame
Dateinamen-Meterregel fuer `PhotoImportService` und Medien-Suche; die historische
Suffixauswertung mit ein bis drei Ziffern bleibt kompatibel. Beide Wege verwenden
`PhotoProtocolEntryMatcher` fuer den naechsten nicht geloeschten Eintrag mit
`MeterStart` bis einschliesslich 1,0 m. Parser und Auswahlregel nicht wieder in
Fenster oder Importdienst duplizieren.

`ProtocolEntryOrdering` ist die WPF-freie Sortierregel fuer
`ProtocolObservationsWindow`: aktive Eintraege nach `MeterStart`, `MeterEnd`,
`vsa.distanz`/`Distance`, zweitem Meter und Code; stabile Gleichstaende bleiben
erhalten, geloeschte Eintraege folgen in ihrer bisherigen Reihenfolge. Das
Fenster schreibt das Ergebnis in `ProtocolRevision.Entries`, zeigt nur aktive
Eintraege und behaelt Auswahl, Refresh-Sperre und Grid-Refresh. Meterparser oder
Sortierschluessel nicht wieder in das Fenster kopieren.

`ProtocolTimeParser` ist die gemeinsame Application-Regel fuer MPEG- und
Protokollzeiten. Er akzeptiert die sechs bisherigen Kurz-, Lang- und
Millisekundenformate und danach den invariant-kulturellen `TimeSpan`-Fallback.
`ProtocolEntryInputNormalizer`, `ProtocolPdfObservationText`,
`XtfValueNormalizer`, `VsaFindingToProtocolEntryMapper` sowie die Film-Link-Wege
delegieren dorthin; die PDF- und XTF-Methoden bleiben als Kompatibilitaetsfassaden
bestehen. `ProtocolFindingRawParser` besitzt getrennt davon nur die historischen
Meter- und Zeit-Tokenregeln fuer Finding-Rohtext. PDF-Resolver und
`ProtocolObservationsWindow` muessen dieselben Methoden verwenden. Regex-Suffixe,
Millimeter-Ausschluss, ungepruefte Zeit-Tokens und Null-Ausnahmen nicht in einem
reinen Refactoring veraendern.

`DataPageDropdownOptionSynchronizer` ergaenzt die Haltungs-Dropdownlisten aus den
Datensaetzen und zerlegt empfohlene Massnahmen mit den bisherigen Trenner-,
Normalisierungs-, Entdoppelungs- und Reihenfolgeregeln. Das ViewModel behaelt das
Speichern aller Listen und erzwingt dort weiterhin die feste Eigentuemer-Liste.
`DataPageDropdownOptionGroupFactory` konfiguriert die fuenf
Bearbeiten-/Vorschau-/Reset-/Hinzufuegen-/Entfernen-Gruppen mit dem gemeinsamen
`DropdownOptionGroupController`; die oeffentlichen Commands bleiben ueber
`DataPageDropdownCommandFactory` verdrahtet. Die Eigentuemer-Gruppe bleibt im
normalen Controller-Modus, damit Nullaktionen nicht speichern; erst die gemeinsame
Speichergrenze stellt die feste Liste wieder her.

`MeasureRecommendationService` darf Sanierungsmassnahmen ausschliesslich aus echten
BA-/BB-Schadenscodes lernen und empfehlen. `MeasureRecordParser` delegiert die
Zeilenformate von `Primaere_Schaeden` an `PrimaryDamageLineParser`; Meterwerte,
Operator-Codes, BC-Bestandesmerkmale und BD-Allgemeinzustand bleiben ausgeschlossen.
`MeasureRecommendationPersistence` kapselt die reine Bereinigung/Migration ausserhalb
des IO-Service. Der Lernspeicher `measures_learning.json` hat Version 3 und migriert Altbestand beim
ersten Laden atomar mit `.bak`; Modelle der alten Version 1 werden nicht verwendet.
Die VSA-Bewertung hat keine Abhaengigkeit zum Empfehlungsdienst und veraendert keine
Sanierungsmassnahmen. Die UI erzeugt einen Vorschlag nur bewusst fuer die ausgewaehlte
Haltung; der fruehere Stapelbefehl ist entfernt. Dieser Automatikvorschlag ist noch kein Lernfall; erst die von der Fachperson
bearbeitete und gespeicherte Fassung wird durch `MeasureRecommendationService.Learn`
uebernommen. Hand- und Importwerte bleiben geschuetzt.

`ProjectPageDropdownCommandFactory` verdrahtet die beiden Dropdown-Gruppen der
Projektseite mit `DropdownOptionGroupController` und `DropdownCommandFactory`.
Die zehn oeffentlichen Command-Eigenschaften des ViewModels bleiben die Fassade.
Der Sanieren-Edit ergaenzt erst nach bestaetigtem Dialog den dann aktuellen Wert
case-insensitiv an Position 0; der Eigentuemer-Reset nutzt `EnsureExact` und
speichert auch ohne Sammlungsänderung einmal. `ProjectPageViewModel` behaelt
Metadaten-Synchronisierung und `SaveDropdownOptions`, das beide Listen speichert.
Diese beiden Sonderregeln nicht durch den allgemeinen Gruppenstandard ersetzen.

`SchaechteDropdownCommandFactory` konfiguriert die vier Dropdown-Gruppen fuer
Sanieren, Eigentuemer, Pruefungsresultat und Referenzpruefung mit
`DropdownOptionGroupController` und `DropdownCommandFactory`. Die zwanzig
oeffentlichen Command-Eigenschaften von `SchaechtePageViewModel` bleiben die
Fassade. Anders als bei DataPage ist die Eigentuemer-Gruppe hier direkt auf die
festen Resetwerte gesperrt: Bearbeiten, Hinzufuegen und Entfernen stellen die
exakte Liste wieder her und speichern einmal. `SchaechtePageViewModel` behaelt
`SaveDropdownOptions`; dort werden zuerst Eigentuemer erzwungen und die
datensatzbasierten Listen synchronisiert, danach werden alle vier Listen
gespeichert. Diese Speichergrenze nicht in die Factory verschieben.

`ObservationCatalogWindow` verwendet fuer optionale Zahlen und Zeiten sowie
Uhrposition, Strecke, EZ und Schachtbereich ausschliesslich
`ProtocolEntryInputNormalizer`. Die sechs Regeln nicht wieder lokal im Fenster
kopieren. Live-Validierung, Fehlermeldungen, Control-Markierung,
Lost-Focus-Normalisierung und die Reihenfolge Normalisieren -> Validieren ->
`ApplyToEntry` bleiben UI-Verantwortung des Fensters. Auch
`ObservationCatalogViewModel` delegiert optionale Meter- und Zeiteingaben sowie
`FormatDouble` und `FormatTime` an diesen Normalizer. Es besitzt keine lokalen
Kopien dieser Parsing- oder Anzeigeformate; die Reihenfolge MeterStart -> MeterEnd
-> Zeit -> optionaler VSA-Distanz-Fallback bleibt im ViewModel.

`ProtocolEntryVM.EnsureVsaDefaults` verwendet dieselben zentralen Zahlen- und
Zeitformatierer. Sobald einer der kanonischen oder alten Aliaswerte vorhanden ist,
wird die jeweilige Aliasgruppe weiterhin nicht repariert oder ueberschrieben.
`ApplyStreckenLogik` delegiert A/B/C-Normalisierung an
`ProtocolEntryInputNormalizer.TryNormalizeStrecke`, behaelt fuer leer und
ungueltig aber den historischen `A1`-Fallback. Jeder Streckenpfad ruft weiterhin
genau einmal den Parameter-Setter auf; auch ein Einzelschaden ohne vorhandene
Metadaten erzeugt dadurch das bisherige leere `CodeMeta`.

Die Eigentuemer-Dossiers verwenden `Export_Vorlage/Eigentuemerdossier.docx` als
verbindliche Geometrie. Platzhalterersetzung und Arial-Vereinheitlichung duerfen
Seitenraender, Abstaende, Absatzmasse, Tabellenzeilen, Deckblatt, Logo, Wappen oder
Fusszeile nicht umbauen. `DossierTextStyleRange` speichert Farbe, Fett, Kursiv und
Unterstrichen zeichenweise: Themen in `DossierTopicRow.StyleRanges`, andere
Textfelder in `DossierDefinition.FieldStyles`. Das alte `ColorHex` bleibt als
lesbarer Kompatibilitaetsweg bestehen. `DossierTopicTextFormatting` ist die
WPF-freie gemeinsame Bereichs-, Platzhalter- und Serialisierungsregel; Vorschau
und Word-Export muessen dieselben Bereiche verwenden.
`DossierPdfAssemblyService` wandelt die fertige Word-Datei zuerst mit Microsoft
Word und ersatzweise mit LibreOffice in PDF um. `DossierWordPdfConverter` sucht
LibreOffice zuerst gebuendelt unter `LibreOffice/program/soffice.exe` neben der
App, danach in den Windows-Installationsordnern und im `PATH`. Der kopflose Lauf
verwendet ein eigenes Temp-Profil und veraendert das Kundenoriginal nicht. Erst
nach erfolgreicher Umwandlung darf `IPdfMergeService` die Beilagen anfuegen; ohne
Word und LibreOffice entsteht weiterhin kein scheinbar vollstaendiges Teil-PDF.
`IDossierConditionClassPdfService` liefert das feste einseitige A4-Erklaerblatt.
Produktiv liest `DossierConditionClassPdfTemplateService` die persoenlich freigegebene
Datei `Export_Vorlage/Zustandsklassen_Eigentuemer_Dossier.pdf` genau einmal, prueft
Lesbarkeit, eine Seite und die Pflichtblatt-Marke und gibt ihre Bytes unveraendert weiter.
Dieselben Bytes werden fuer Vorschau, Gesamt-PDF und die eigene Datei im neu angelegten
Liegenschaftsordner verwendet. `DossierConditionClassPdfService` bleibt der reproduzierbare
Erzeuger fuer Tests und kompatible direkte Aufrufer. Es beschreibt Z0 bis Z4 und zeigt je
Klasse rechts eine kompakte zeitliche Orientierung von sofort bis zur naechsten
Zustandsbeurteilung. Die WPF-freien Texte liegen in `DossierConditionClassDefinitions`,
die Farben stammen aus `ExcelReportStyle`; Logo und Wappen sind optionale Vorlagen-Assets.
Der Grundlagenkasten fasst VSA
"Zustandsbeurteilung von Entwaesserungsanlagen", Kapitel 2.2-2.3, zusammen: Grundlage
sind vollstaendige Bauwerksdaten und korrekt erfasste Befundcodes; Fachpersonen pruefen
Daten und Ergebnis. Schutzbereich, Nutzung, Grundwasserlage und Netzbedeutung veraendern
nur die Sanierungsdringlichkeit, nicht die Zustandsnote. Die Zeitspanne rechts ist deshalb
ausdruecklich nur als Orientierung bezeichnet. Die Klassenzeilen enthalten die
fachlichen VSA-Beschreibungen samt typischen Defizitbeispielen. Ein Strich ist der
getrennte Status `nicht berechnet` und darf niemals als Z4 ausgegeben werden. Zustandsklasse
und Dringlichkeitszahl bleiben getrennte Skalen ohne feste 1:1-Zuordnung. Das Blatt zeigt
keine numerischen Dringlichkeitsbereiche und ordnet Z4 keinem `NULL`-Wert zu.
`IDossierHoldingListPdfService` und `IDossierShaftListPdfService` rendern die
freigegebenen A4-Layouts der Haltungs- und Schachtliste. Die zugehoerigen ModelBuilder
verbinden Dossierkopf und `DossierSnapshot`; die PDF-Services stellen nur dar und
schreiben nie selbst Dateien. Tabellenkoepfe werden auf Folgeseiten wiederholt.
`IDossierComponentListExportService` ist die sichere manuelle Schreibgrenze: Erst nach
den fachlichen Korrekturen erzeugen die zwei Schaltflaechen im Dossier-Cockpit die
gewuenschte Liste im exakt zugeordneten Liegenschaftsordner. Bestehende Dateien werden
nicht ersetzt; bei einer Namenskollision wird ein freier Dateiname verwendet. Beide
Listen bleiben eigene Dateien und werden nicht still in das Gesamt-PDF eingefuegt.
`DossierPdfPackageComposer` ist die gemeinsame Zusammenfuehrungsgrenze fuer Ausgabe und
Vorschau. Die Reihenfolge lautet Word-Dossier, einseitiges Erklaerblatt, normale
Beilagen. Er validiert die Erklaerseite und ihre Einfuegung, arbeitet nur mit
einer Temp-Datei und veraendert weder Kundenoriginale noch den echten Beilagenordner oder
das Manifest. Das Blatt wird auch ohne weitere Beilagen erzeugt und in der Vorschau als
automatisch erzeugte, nicht bearbeitbare Beilage markiert. Eine unsichtbare eindeutige
Seitenmarke verhindert die Verwechslung mit Kundenseiten gleichen Titels. Die
Seitenauswahl sperrt die echte Erklaerseite sichtbar als Pflichtblatt,
und die Ausgabe verhindert ihre Entfernung zusaetzlich unabhaengig von der Oberflaeche.
`Alles zu einem PDF` ruft davor `DossierAttachmentCollector` fuer alle aktuell
gewaehlten Haltungen und danach alle Schaechte auf. Bei Haltungen gilt Original vor
erzeugtem Ersatzprotokoll, bei Schaechten ist das Original erforderlich. Sobald ein
ausgewaehltes Protokoll fehlt, darf kein unvollstaendiges Gesamt-PDF entstehen.
Nur eigene Protokollkopien werden im Beilagenordner ueber
`.sewerstudio-dossier-beilagen.v1.json` an direkten Dateinamen, SHA-256, Typ und
Objekt gebunden. Abgewaehlte automatische Kopien duerfen erst nach erneuter
  Hashpruefung entfernt werden. Unbekannte, manuelle, veraenderte und vor dem Manifest
  entstandene PDFs gelten fail-closed als manuell und bleiben unangetastet. Kopien,
  generierte PDFs und Manifest verwenden eindeutige Temp-Dateien. Der benannte
  `DossierAttachmentFolderLock` serialisiert Load, Publikation, Manifest und Abschluss je
  kanonischem Beilagenordner auch zwischen Prozessen. Eine alte eigene Kopie wird zuerst
  atomar unter einem eindeutigen Sicherungsnamen weggestellt und erst dort hash-geprueft;
  die neue Kopie darf nur ohne Overwrite an den freien Zielpfad. Das Manifest uebernimmt
  ausschliesslich den beim Publizieren bekannten Hash und prueft den Zielinhalt davor
  nochmals. Abgewaehlte eigene Kopien gehen mit derselben Move-first-Regel in einen
  versteckten Quarantaeneordner. Eine
  voruebergehend gesperrte eigene PDF behaelt ihren Manifest-Eintrag fuer den spaeteren
  sicheren Versuch. Scheitert das Manifest-Schreiben, werden die in diesem Lauf
  veroeffentlichten PDFs nur bei weiterhin passender SHA-256 auf den vorherigen Stand
  zurueckgesetzt.
`IDossierOutputPreviewService`/`DossierOutputPreviewService` erzeugt die sichtbare
Vorschau ueber denselben Word-Export und denselben Word-/LibreOffice-PDF-Wandler wie
die Ausgabe. Alle Arbeitsdateien liegen in einem eindeutigen System-Temp-Ordner;
Dossier und Gebiet werden tief kopiert, relative Planpfade nur in dieser Kopie
aufgeloest und der Kundenordner bleibt unveraendert. Nur manuelle beziehungsweise
nicht sicher als automatisch erkannte Beilagen werden in den Temp-Ordner kopiert;
dort sammelt derselbe `DossierAttachmentCollector` die aktuell gewaehlten Haltungen
und danach Schaechte neu. Gemergt werden ausschliesslich diese temporaeren PDFs in
Dateinamenreihenfolge. Dadurch verschwinden abgewaehlte eigene Protokolle auch aus
der Vorschau, ohne den echten Projektordner zu veraendern.
`DocxFieldMarkerWriter` setzt pro bearbeitbarem Feld eine deterministische, fuer
Word gueltige Textmarke mit hoechstens 40 Zeichen. Word und LibreOffice exportieren
sie als benannte PDF-Ziele. Weil `PdfMergeService` beim Anfuegen von Beilagen nur
Seiten kopiert, liest `DossierOutputPreviewService` die Ziele vorher aus der reinen
Word-PDF und gibt sie getrennt mit der Gesamtvorschau zurueck. Nicht versuchen, die
Ziele erst aus dem zusammengefuehrten PDF zu lesen. Leere bekannte Standardthemen
bleiben im Kundendokument leer; nur leere freie Zusatzpunkte werden als `unbekannt`
markiert. `Gebiet_Perimeter` ist eine optionale ganze Wortgruppe, damit die
Ausgangslage auch ohne Gebietsort grammatisch vollstaendig bleibt.
`WindowsDossierPreviewPageRasterizer` zeichnet die echten PDF-Seiten;
Seitenformat, Umbrueche, Abstaende, Tabellen, Farben, Bilder, Logo und Fusszeile
duerfen nicht mehr parallel in WPF nachgebaut werden. Nach 300 ms Schreibpause wird
neu erzeugt; das alte Blatt wird sofort gesperrt und durch einen Aktualisierungshinweis
ersetzt. Erst die erfolgreich gerasterte Seite des neuesten Ausgabestands gibt Klicks
und `Uebernehmen` frei. Ein veraltetes Ergebnis darf nie eingeblendet werden. PdfPig-Wortlagen
tragen transparente Klickflaechen fuer den direkten Sprung zum Editor.
`DossierOutputPreviewInteractionMapper` haelt die Seiten-/Editor-Zuordnung und Textziele
WPF-frei und begrenzt sie auf die wirklich sichtbare PDF-Seite.
`DossierOutputPreviewHitMatcher` gleicht auch abweichende PDF-Wortgrenzen und lange
Tabellenzellen sicher ab. Gleiche Texte verschiedener normaler Felder werden ohne
geometrischen Beleg nicht nach Katalogreihenfolge geraten; nur echte Geschwister
derselben Wiederholspalte werden geordnet. `DossierOutputPreviewTableCellMapper`
verarbeitet die Blaetter gemeinsam, liest die physische Spaltengeometrie am eindeutigen
ersten Tabellenkopf und traegt den globalen `RowIndex` auf Folgeseiten weiter. Jede
sicher erkannte gefuellte oder leere Tabellenzelle erhaelt ihre volle Flaeche. Bei
Unsicherheit bleiben die bisherigen Wortziele sowie
`DossierOutputPreviewEmptyRowCellMapper` als konservativer Rueckfall erhalten.
Telefon, Mail, Objektbewohner und ihre bearbeitbaren Praefixe bleiben kleine Ziele
innerhalb der gemeinsamen Eigentuemerzelle. `DossierOutputPreviewEmptyFixedCellMapper`
deckt die ganze leere Aktennotiz-Zelle und nur den oberen Eingabeabsatz der
Rueckmeldung ab; Punktlinien und Unterschriftsbereich bleiben ausgeschlossen.
`DossierPreviewTableRow.MinimumHeightPx` bewahrt dafuer die Mindesthoehe der echten
Word-Zeile; vollstaendig erkannte mehrzeilige Nachbartexte koennen die Zeile erweitern.
`DossierPreviewTextInventory` ist die gemeinsame Quelle der festen
Vorlagentexte. `DossierTextUndoController` bietet zentral Rueckgaengig/Wiederholen fuer
die native Texthistorie des zuletzt aktiven Dossierfelds und verwirft entfernte Ziele
bei dynamischem Neuaufbau. Das Vorschaufenster orchestriert nur. Original-Beilagen
erscheinen als eigene, nicht bearbeitbare Gruppe.
Die sichtbaren Grundzeilen fuer Aenderungswesen, Eigentuemer und Themen bekommen
rechts sofort alle Eingaben. `DossierChangeRows`, `DossierOwnerRows` und
`DossierTopicRows` entfernen unbenutzte Eingabehilfen vor der Uebernahme. Mehrere
titellose Themen-Altdaten werden in Reihenfolge an ihre eigene Editorzeile gebunden;
eine neu als `Schaeden` benannte Zeile zeigt `Import aus Liste` sofort. Gemeinsame
Eigentuemer-Beschriftungen ausserhalb einer Zeilenkarte bleiben beim Vorschauklick
selbst sichtbar.
`DossierPreviewTarget` ist die gemeinsame semantische Klickadresse fuer die Vorschau:
Feld, dynamische Zeile und Spalte ersetzen feste Pixelzuordnungen. Das genaueste
vorhandene Ziel fuehrt direkt zum passenden Editor; auch geaenderte Vorlagentexte
bleiben ueber ihren urspruenglichen Wortlaut adressierbar. Fusszeilen-Platzhalter
werden beim Lesen der Word-Vorlage jeder Dossierseite als gemeinsame Felder
zugeordnet. Zusatzpunkt-Titel und Seitenzahl sowie Thementitel und Bemerkung haben
getrennte Klickziele. Zusatzpunkt-Titel tragen eigene `TitleStyles`, damit Farbe,
Fett, Kursiv und Unterstreichen auch in der echten Word-Zeile erhalten bleiben.
`DossierPreviewNavigation` ordnet die Vorlagenseiten den Editoren zu; die sichtbare
Navigation verwendet dagegen die tatsaechlichen Seiten des erzeugten PDF.
Fortsetzungsseiten ohne neue Ueberschrift bleiben beim zuletzt begonnenen Kapitel;
rechts erscheinen nur die Felder der zugeordneten Seite.
Inhaltsverzeichniszeilen werden strukturell aus dem echten Word-Feld gelesen:
`DossierPreviewTocEntry` trennt Nummer, bearbeitbaren Kapiteltitel und
PAGEREF-Seitenzahl. `DocxTocEntryEditor` ersetzt ausschliesslich den Titel; Nummer,
Tabulatoren und Seitenzahl bleiben Word-Felder. Derselbe `TextOverrides`-Schluessel
aendert die Kapitelueberschrift, damit eine spaetere Word-Aktualisierung den eigenen
Titel beibehaelt. Zusaetzliche Punkte stehen gemeinsam in
`DossierDefinition.TocAttachments`; jedes `DossierTocAttachment` verbindet Titel und
Seitenzahl untrennbar. Schema 8 uebernimmt die zwei alten parallelen Listen einmalig
und entfernt sie danach; ausser der Migration darf kein Code diese Altlisten verwenden.
`DossierTocAttachments` nummeriert nach den in der Vorschau sichtbaren Kapiteln und
schlaegt bei Altdaten die naechste freie Seite vor. `DocxTocAttachmentWriter` schreibt
jeden Zusatzpunkt direkt hinter den letzten echten Word-Eintrag als eigenen Absatz in
dessen Format samt rechtem Seitenzahl-Tabulator. Die Vorschau verwendet dasselbe
Zeilenraster samt Punktlinie, zaehlt ausgeblendete Kapitel nicht mit und adressiert
jeden Zusatzpunkt einzeln fuer den direkten Klick zum Editor.
`DossierTocChapterPageClickMapper` ordnet die rechten Seitenzahlen ueber die jeweilige
Inhaltsverzeichniszeile eindeutig ihrem Seitenzahlfeld zu.
`DocxTocLayoutFormatter` entfernt ausschliesslich im Inhaltsverzeichnis die alte
gesperrte Zeichenweite und verdichtet die Zeilen. Arial, Punktlinie, Word-Felder und
rechte Seitenzahlen bleiben erhalten; `DossierPreviewTocLayout` verwendet dieselben
Schriftgroessen und Abstaende. Die Vorschau startet ueber
`DossierPreviewFitCalculator` und die echte PDF-Blattgroesse mit einer vollstaendig
sichtbaren Seite. Manueller Zoom bleibt moeglich; `Ganze Seite` stellt die Einpassung
wieder her.
In `Schäden` und `Sanierungskonzept` kopiert `Import aus Liste` die aktuelle,
fortlaufend nummerierte Bauteilliste als normalen Dossiertext: zuerst alle Haltungen,
danach alle Schächte. `DossierComponentConditionClassFormatting` markiert darin nur
die Kürzel `Z0` bis `Z4` mit den Zustandsklassenfarben aus `ExcelReportStyle`; die
Bereiche gelangen über `Bauteile_Text__Formatbereiche` unverändert in Editor, Dossier
und Word. Diese Momentaufnahme ist frei bearbeitbar und darf weder
`HoldingIds`/`ShaftNumbers` noch Projektdatensaetze veraendern.
`DossierTopicComponentListComposer` loest nur noch alte
`Bauteile_Text`-/`Haltungen_Text`-/`Schaechte_Text`-Marken kompatibel auf.
Formatierte Beschriftungen und Ueberschriften verwenden
`DossierTopicTextFormatting.LiteralStyleKey(...)` in `FieldStyles`; Vorschau und
Word muessen Farbe, Fett, Kursiv und Unterstrichen gleich anwenden. Dasselbe gilt
fuer bearbeitbare Platzhalter innerhalb beschrifteter Zeilen wie `Datum: {{Datum}}`.
`DossierOwnerCellLabels` macht auch `Tel.:`, `Mail:` und `Objektbewohner:` ohne
Schemaaenderung bearbeitbar. Der Word-Export setzt Beschriftung und Wert mit getrennten
Zeichenbereichen in dieselbe physische Eigentuemerzelle.
`DocxLiteralFormatting` markiert Benutzereingaben als Literalbereiche, damit darin
geschriebener Text wie `{{Datum}}` nicht als neu entstandener Vorlagen-Platzhalter
ausgewertet wird.
`DossierTopicTitleEditing` speichert eine eigene Fassung eines Thementitels unter
einem stabilen Feldschluessel im einzelnen Dossier. `DossierTopicResolver` behaelt
den urspruenglichen Gebietstitel als Quelle; Vorschau und Export verwenden den
eigenen Titel samt Zeichenformatierung, ohne die Gebietsvorgabe umzubenennen.
Der Word-Export entfernt nur den bekannten manuellen Seitenumbruch unmittelbar vor
`Aenderungswesen:`: Das volle Deckblatt bricht selbst auf Seite 2 um, weshalb dieser
zusaetzliche Umbruch dort sonst eine leere Seite erzeugt. Andere Vorlagenumbrueche
bleiben unangetastet.
Der Werkleitungsplan verwendet weiter den kompatiblen Vorlagenschluessel
`Uebersichtsplan`. `WindowsPdfPlanImageConverter` uebernimmt JPG, JPEG, PNG, BMP
oder die erste PDF-Seite immer als neue, gepruefte PNG-Kopie. Im Vorschaufenster
liegen Import, Drehen und Zuschneiden in einer eigenen `DossierPlanWorkSession`;
erst `Uebernehmen` reicht die letzte Datei an `DossierPlanPublicationService`
weiter. Der Dienst prueft Projektgrenze und Junctions ueber
`ProjectWritePathGuard` und veroeffentlicht unter einem freien Namen. Sein
Hash-Beleg bleibt in `DossierPreviewChoice`, bis das Dossier-Dokument erfolgreich
gespeichert ist. Verschwindet das Dossier oder scheitert das Speichern, wird nur
die gerade erzeugte, unveraenderte PNG zurueckgenommen. `Verwerfen` entfernt nur
den eigenen Temporaerordner. Quelle und vorhandene Dossierdateien werden nie
ueberschrieben oder geloescht. Auf der echten Planseite fuehrt eine sichtbare
Foto-Schaltflaeche direkt in diesen vorhandenen Planeditor mit Dateiwahl, Drehen und
Zuschneiden; ein zweiter Importweg ist nicht erlaubt.
Das Planbild wird proportional innerhalb der Referenzflaeche (maximal ca.
15 x 21,5 cm) eingepasst; der aeussere Word-Rahmen behaelt immer die volle
Vorlagenhoehe. Dadurch bleibt ein JPG unverzerrt, Folgekapitel ruecken bei
Querformat nicht hoch und es entsteht trotzdem keine Zusatzseite. Eine gespeicherte
Breite wird auf die 15 cm der Vorlage begrenzt.
Ohne lesbaren Plan entfernt `DocxImagePlaceholderFiller` den ganzen
Plan-Platzhalterabsatz samt grossem schwebendem Vorlagenrahmen. Sonst liegt der
leere Rahmen in Word ueber den folgenden Kapiteln. Ein bewusst leerer Planpfad
bleibt dabei ohne Fehlhinweis; ein gesetzter, aber unlesbarer Pfad wird weiterhin
gemeldet.
Die Reihenfolge der Liegenschaften ist die Reihenfolge von
`DossierDocument.Dossiers` in `dossiers.json`; das Cockpit darf sie nicht erneut
alphabetisch sortieren. `MoveDossierUpCommand` und `MoveDossierDownCommand`
verschieben die Auswahl um genau eine Stelle und speichern sofort. Scheitert das
Speichern, muss die vorige Reihenfolge wiederhergestellt werden.

`DossierFileStore` legt beim Speichern den eigenen Ordner jeder Liegenschaft sofort
direkt unter `<Projekt>\Dossiers` an. Damit gilt dieselbe Regel fuer Einzel- und
Stapelanlage. Beim Laden einer vorhandenen, lesbaren `dossiers.json` zieht der Store
fehlende, bereits benannte Liegenschaftsordner nach, ohne die JSON-Datei zu aendern.
In jeden dabei neu erzeugten Ordner legt er nur die freigegebene Datei atomar und
bytegleich als `Zustandsklassen_Eigentuemer_Dossier.pdf`. Haltungs- und Schachtlisten
werden bewusst nicht automatisch erzeugt, damit zuerst fachliche Korrekturen erfolgen
koennen. Sie entstehen spaeter ueber `IDossierComponentListExportService`. Die
Haltungsliste zeigt Material, DN, Laenge, Zustandsklasse und Nutzungsart; die Schachtliste
zeigt Schachtnummer, Strasse, Funktion und Zustandsklasse. `ExcelReportStyle` liefert die
Zustandsfarben; `NutzungsartReportColors` ist die gemeinsame Nutzungsfarben-Regel fuer
Haltungsliste und bestehende PDF-Berichte. Fehlende Werte werden als `nicht erfasst`
ausgewiesen. Bestehende Ordner und Dateien werden nicht ersetzt. Bei einem folgenden
Speicherfehler entfernt der Store nur seine weiterhin hashgleiche Zustandsklassen-Datei
und danach den leeren neuen Ordner; veraenderte Dateien bleiben erhalten.
Alle Store-Instanzen teilen pro laufendem Programm eine Sperre. Hashpruefung und
Loeschmarkierung erfolgen unter Windows am selben exklusiven Dateihandle, damit kein
fremder Ersatz zwischen Pruefung und Ruecknahme geloescht werden kann.
`DossierFolderPlanner.ResolveDossierFolder` verhindert verschachtelte
Ordner und Pfade ausserhalb dieses Sammelordners. Scheitert das Speichern, duerfen
nur in diesem Lauf neu erzeugte und weiterhin leere Ordner entfernt werden;
bestehende Ordner und Benutzerdateien bleiben unangetastet.

`VsaParameterMerger` ist die gemeinsame Application-Wahrheit fuer die sieben
Aliasgruppen Code, Distanz, Video, Uhr-von, Uhr-bis, Q1 und Q2. `Merge` mutiert das
uebergebene Dictionary und laesst vorhandene Werte bei leerer Eingabe stehen.
`NormalizeAliases` erzeugt dagegen einen neuen `OrdinalIgnoreCase`-Snapshot,
entfernt leere Keys/Werte, trimmt, spiegelt mit kanonischem Vorrang und setzt den
ausgewaehlten Code zuletzt auf beide Code-Aliase. Diese Wege nicht vermischen.
`ProtocolEntryVM.ApplyCodeSelection` delegiert nur die Alias-Normalisierung;
Modellwerte, `CodeMeta.UpdatedAt` und `PropertyChanged` bleiben im ViewModel.

`HydraulikPanelViewModel.LoadFromRecord` loest Datensatzmaterial ueber
`HydraulikMaterialCatalog.ResolveRecordMaterial` auf. Als Fallback wird das
aktuelle `MaterialOption`-Objekt uebergeben, nicht nur sein Key: unbekannte Werte
muessen benutzerdefinierte kb-Werte und auch eine `null`-Auswahl unveraendert
lassen. Der Bericht nutzt weiterhin `HydraulikMaterialCatalog.Resolve` mit dem
gespeicherten Material-Key und garantiert damit einen Katalogfallback.

`DistributionTargetConfigViewModel` besitzt Commands, Konfigurationsmutation und
Speicher-Callbacks der Export-Zielkarten. Die reine Pfad- und grafische
Ordnerbaum-Vorschau liegt im internen `DistributionTargetPreviewBuilder`; diese
Logik nicht ins ViewModel zurueckkopieren. Der Builder erhaelt die vorhandenen
`IDistributionPatternResolver`- und `IDistributionDirectoryTreeResolver`-Instanzen
und ist nicht im `ServiceProvider` registriert. Excel liefert keinen Baum.
Schacht und Dichtheit liefern keinen Video-Knoten, Haltung schon; nur Ziele mit
`SupportsSanierung` duerfen die Sanierungsebene anzeigen. Knotenreihenfolge und
Einruecktiefe bleiben Ordner, Unterordner, Objekt, optionale Sanierung, PDF und
optionales Video. Sichtbare Knotennamen muessen dieselbe
`ProjectPathResolver.SanitizePathSegment`-Bereinigung wie der echte Zielpfad
verwenden.

`SchaechteRecordCollectionController` verwaltet Hinzufuegen, Loeschen,
Verschieben und Renummerieren der Schaechte unter `ShellViewModel.CollectionLock`.
Er bezieht Records und Spalten ueber Getter, damit `ShellViewModel.ReplaceProject`
keine alte Sammlung festhaelt. `SchaechtePageViewModel.RecordCollection.cs`
behaelt Auswahl ohne Pflichtfeldwarnung, Suchstatus, Dirty-/Zeitmarkierung und
Command-Benachrichtigungen. Schacht-Loeschen bleibt ohne Rueckfrage und diese
Operationen planen weiterhin kein AutoSave. Die Protokoll-Import-Partials duerfen
neue Schaechte direkt einfuegen, muessen dabei aber denselben Collection-Lock halten.

Der Schachtprotokoll-Ordnerimport bleibt als asynchroner Ablauf in
`SchaechtePageViewModel.ProtocolFolderImport.cs`. Seine reinen Regeln liegen in
der internen `SchachtProtocolFolderImportPolicy`: rekursive PDF-Suche,
Mehrfachfund-Auswahl, Ergebniszusammenfassung und kanonische Schachtordner-
Aufloesung. Die Zusammenfassung behaelt vier Pflichtzeilen, eine ehrliche Zeile fuer
uebersprungene und erhaltene aeltere PDFs sowie hoechstens acht konkrete Fehler plus
Restzaehler; sie darf nicht behaupten, deren Protokolldaten seien archiviert worden.
Der kanonische Schachtname wird innerhalb des modernen oder alten Verteilroots ueber
die PDF-Vorfahren gesucht. Ein vorhandener Projektschacht hat Vorrang vor der
eindeutigen aus dem PDF gelesenen Nummer. Sanierungsordner sind nie Schachtnummern;
mehrdeutige tiefere Strukturen liefern keinen Treffer. Beide Roots muessen gemeinsam,
case-insensitiv und mit normalisierten Abschlussseparatoren geprueft werden.
Ungueltige Vergleichspfade liefern keinen Treffer. Diese Regeln nicht wieder in das
ViewModel kopieren; der produktive Aufruf muss moderne und alte Root in dieser
Reihenfolge uebergeben.

Der PDF-Stammdaten-Hintergrunddienst bleibt
`ISchachtStammdatenErgaenzungsService`; er veraendert keine UI-Datensaetze. Erst
nach dem Hintergrundlauf uebernimmt der interne, WPF-freie
`SchachtStammdatenResultApplier` das Ergebnis in die aktuelle Schachtsammlung.
Er indiziert alle Records vor dem `beforeApply`-Haken. Das ViewModel erzeugt in
diesem Haken den Wiederherstellungspunkt, wenn mindestens eine Ergaenzungszeile
vorliegt; erst danach duerfen Felder mutieren. Nur leere `Schachtform`,
`Dimension` und `Schachttiefe` werden in genau dieser Reihenfolge mit getrimmten,
nicht-leeren Werten ergaenzt. Unbekannte Record-IDs bleiben unbeachtet. Die
historische Anzeige zaehlt geaenderte Ergaenzungszeilen, nicht eindeutige
Schacht-IDs. Der Applier baut auch die bestehende Zusammenfassung und hoechstens
zwoelf konkrete Hinweise plus Restzaehler. Dirty/Save, Speicherwarnung, Dialog,
Fortschritt, Abbruch und Fehlerbehandlung bleiben im ViewModel. Diese Regeln
nicht in `SchaechtePageViewModel.Stammdaten.cs` zurueckkopieren.

Das Neueinlesen eines bereits verknuepften Schachtprotokolls laeuft ueber den
internen, WPF-freien `SchachtProtocolRefreshController`. Das
`SchaechtePageViewModel` besitzt genau eine Instanz und bindet gezielt den
Projektordner, einen `ProjectOperationContext`,
`ProjectPathResolver.ResolveFilePathFromProjectFolder`, den gemeinsamen
`ReadProtocolAsync`-Weg, `ProjectIsStillOpen` mit `ProjectOperationImpact`,
`ISchachtProtocolImportService.Apply`, den an den aktiven Vorgang gebundenen internen
Speicherweg und `LastResult`.
Keinen `ServiceProvider` in den Controller reichen und den Ablauf nicht in das
Import-Partial zurueckkopieren.

Beim Start werden der ausgewaehlte `SchachtRecord`, sein originaler relativer
`PDF_Path`, die Projektinstanz und der Pfad der `projekt.json` festgehalten. Die
destruktive Bestaetigung bleibt vor der Pfadauflosung und verwendet
`defaultNo: true`. Nur nach erfolgreichem Lesen und bestandenem
`ActiveProjectGuard` darf `Apply` laufen. `Apply` ist die Commit-Grenze: Danach
werden am festgehaltenen Projekt sofort `ModifiedAtUtc = DateTime.UtcNow` und
`Dirty = true` gesetzt. Eine erneute Identitaetspruefung verhindert anschliessend,
dass ein rueckrufbedingter Projektwechsel das andere Shell-Projekt speichert. In
diesem Fall lautet die Meldung ehrlich "uebernommen, aber nicht gespeichert";
andernfalls folgen genau ein Speicherversuch und der bisherige Erfolgstext. Ein
`false` vom gebundenen Speicherversuch aendert diesen Altvertrag noch nicht. Der
Ablauf besitzt weiterhin keine Arbeitskopie oder fachliche Ruecknahme.

Der Import genau einer neu ausgewaehlten Schacht-PDF laeuft ueber den internen,
WPF-freien `SchachtProtocolSingleImportController`. Er erhaelt gezielt
`IDialogService`, `ISchachtProtocolImportService` sowie Actions fuer den
gemeinsamen Leseweg, `ProjectIsStillOpen`, den gemeinsamen `CollectionLock`,
den gebundenen internen Speicherweg, `Selected`, das referenzgenaue Aufraeumen einer veralteten
Auswahl und `LastResult`. Der Aufruf erhaelt ausserdem den
beim Start gebildeten `ProjectOperationContext`.
Kein Interface, keine eigene `ServiceProvider`-Registrierung und keine zweite
Parse-/Projektwechsel-Implementierung einfuehren.

Die Reihenfolge ist verbindlich: lesen, erste Projektidentitaetspruefung,
Protokoll- und Nummerpruefung, Zielsuche samt Ja/Nein/Abbrechen, Kopierstatus,
`DistributePdf` in `Task.Run`, zweite Projektidentitaetspruefung mit dem echten
Dateieffekt, `Apply`,
`SchaechteData.Contains`, nur `SchaechteData.Add` unter dem Collection-Lock,
UTC-Zeitmarkierung und Dirty als Commit-Grenze, erneute Pruefung, `Selected`,
nochmals Pruefung, bei Wechsel referenzgenau die alte Auswahl loeschen, genau ein
Speicherversuch und der bisherige Erfolgstext. Eine inzwischen gesetzte Auswahl
des Ersatzprojekts darf dabei nicht geloescht werden. Find,
Contains, Add und Dirty verwenden durchgehend die im Kontext festgehaltene
Projektinstanz. Ein Wechsel nach der Commit-Grenze speichert kein Ersatzprojekt
und wird als "uebernommen, aber nicht gespeichert" gemeldet. Rohwerte
fuer Projektordner, Schachtnummer, Quell- und relativen Zielpfad nicht still
normalisieren. Die oeffentliche `string ISchachtProtocolImportService.DistributePdf`
-Fassade bleibt erhalten. Der optionale additive
`ISchachtProtocolDistributionResultService` liefert einen
`SchachtProtocolDistributionResult`: `RelativePath` ist der gespeicherte Pfad,
`FileCreated` unterscheidet eine neu geschriebene PDF von einer bereits vorhandenen.
Ein Kopierfehler faengt weiterhin jede `Exception`, meldet sie ueber
`UserError` und beginnt keine Record-Mutation. `SaveProject() == false` bleibt
vorerst ein historischer Erfolgsweg. Datei-Staging/Kopier-Rollback, Arbeitskopie und
sichtbarer Speicherstatus nur als eigene, fachlich entschiedene Aenderung einfuehren.
Ein zusaetzlicher Restorepoint allein ist kein
Rollback und wuerde beim folgenden Speichern meist dieselbe gespeicherte
`projekt.json` doppelt sichern.

`ProjectOperationImpact` ist ein kombinierbares Flags-Enum mit `None`,
`ProjectFilesWritten` und `ProjectDataChanged`. Ein Projektwechsel nach der
Dateiverteilung, aber vor `Apply`, muss die bereits geschriebenen PDFs ehrlich
melden und darf nicht behaupten, es sei gar nichts uebernommen worden. Ein
Projektwechsel nach `Apply` meldet dagegen veraenderte, noch nicht gespeicherte
Projektdaten. Sind Datei- und Datenwirkung eingetreten, muessen beide Flags bis
zum letzten Guard erhalten bleiben und die Meldung muss beide Folgen nennen.

Der Schachtprotokoll-Ordnerimport bildet denselben `ProjectOperationContext` vor
der Quellenauswahl. Er prueft nach Dateisuche, nach Bestaetigung, nach Verteilung,
nach Parsing, nach dem Apply-Loop und nach `Selected`. `DistributeShaftFiles`,
`FindSchacht`, Collection-Add sowie Modified/Dirty erhalten ausschliesslich die
festgehaltene Projektinstanz. Der Projektordner bleibt getrennt davon der
unveraenderte Dateipfad-Kontext. Vor der Dateiverteilung stoppt ein Wechsel ohne
Uebernahme. Nach einer erfolgreichen Verteilung wird der Effekt bis zur Daten-
Commit-Grenze als `ProjectFilesWritten` weitergereicht. Nach der Commit-Grenze
bleibt das gestartete Projekt Dirty und der Save des Ersatzprojekts wird verhindert.
Scheitert die Pruefung nach `Selected`, wird die Auswahl nur geloescht, wenn sie
noch genau auf den alten Schacht zeigt.

Einzel- und Ordnerimport, Neueinlesen sowie PDF-Stammdatennachlauf verwenden auf der
Schachtseite denselben je Shell geteilten lokalen Zustand und dieselbe zentrale
Shell-Projektreservierung wie Import- und Exportseite. Die Reservierung erfolgt vor
Dialog oder Hintergrundarbeit. Sie sperrt Navigation, Projektwechsel, Schliessen und
oeffentliche Save-/SaveAs-Wege; nur der registrierte zentrale Besitzer darf den
gebundenen internen Save ausfuehren. Projektinstanz, Projektpfad und Datensaetze werden
vor der Arbeit gebunden und vor Apply sowie Save erneut geprueft. Fehler,
Benachrichtigungsausnahmen und `Dispose` duerfen den Besitzer nicht vorzeitig freigeben.

Der Review-SAM-Weg im Training Center läuft über `TrainingReviewSamWorkflow` und
`ITrainingReviewSamSegmentationService`. Prüfung, SAM-Aufruf, erste Maske mit
nicht-leerem RLE-Text und Statustext gehören nicht ins Fenster. Der Durchmesser kommt aus
`TrainingCenterWindowDependencyFactory`; nur `null` wird im Workflow zu 300 mm.
Die drei XAML-Anzeigehelfer des Fensters liegen getrennt in
`TrainingCenterConverters.cs`; keine `IValueConverter`-Klassen ins Fenster zurückschieben.

Der Pruefplatz im `TrainingStudioWindow` bezieht Workbench, Warteschlange und
KI-Bereitschaft gemeinsam aus `TrainingStudioWindowDependencyFactory`.
Der dort verzögert erzeugte Knowledge-Base-`HttpClient` gehört dem
`DelegatingKnowledgeBaseIndexer`. `AnnotationWorkbenchService.Dispose` gibt den
Indexer und damit diesen Client genau einmal frei; die Besitzkette nicht wieder in
eine nicht freigebbare Lambda-Variable verschieben.
`TrainingStudioAiReadinessWorkflow` prueft zuerst den bestehenden Sidecar und ruft nur
bei einem Offline-Sidecar den zentralen `AiStartupService` auf. Automatischer Start beim
ersten Laden und manueller `KI starten`-Befehl laufen ueber das ViewModel; Prozesslogik
bleibt aus dem Fenster heraus. Segmentierung und Vorschlag duerfen parallel laufen, aber
ein bereits erfolgreiches Teilergebnis muss bei einem Fehler des anderen Aufrufs
sichtbar bleiben.

Der PDF-Pruefimport des Training Studios liegt hinter dem Application-Vertrag
`ITrainingPdfReviewImportService`; die Implementation
`TrainingPdfReviewImportService` und Reader/Matcher liegen unter
`Infrastructure/Ai/Training/PdfReview`. Kunden-PDFs bleiben unveraendert und werden
vor sowie nach dem Lesen per SHA-256 kontrolliert. Erlaubte Zuordnungen sind nur:
Code im selben Fotoblock, exakte Foto-ID beziehungsweise exakter Dateiname oder die
vollstaendige Kombination aus Videozeit, Meter und normalisiert identischem
Operateurbefund. Keine Reihenfolge-, Meter-allein- oder unscharfe Textzuordnung
einfuehren. Mehrdeutige Bildkandidaten werden gemeldet und uebersprungen; mehrere
Codes am selben Foto bleiben getrennte `WorkbenchItem`s. Extrahierte Arbeitsbilder
liegen inhaltsadressiert unter
`<KnowledgeRoot>\training\pdf_review_imports\<vollstaendiger-pdf-sha256>`.

Mehrere ausgewaehlte PDF-Ordner laufen ueber
`Application/UseCases/PdfTrainingReview/TrainingPdfReviewBatchImportUseCase`.
Der Vertrag `ITrainingPdfFolderDiscoveryService` trennt die Suche vom Import;
`TrainingPdfFolderDiscoveryService` sucht rekursiv und stabil sortiert, dedupliziert
ueberlappende Wurzeln und betritt keine Reparse Points. Die komplette Root-Pfadkette
und jeder aus dem Arbeitsstapel geholte Ordner werden unmittelbar vor dem Lesen
erneut geprueft. PDFs werden bewusst sequenziell importiert. Ein Dateifehler wird
dem Benutzer je PDF gemeldet und stoppt die restlichen PDFs nicht; identische
Dokument-SHA-256 werden nur einmal in die Pruefliste uebernommen. Kundenoriginale
bleiben unveraendert.

Der zentral registrierte `ServiceProvider.TrainingPdfReviews` ist die geschuetzte
Fassade `TrainingPdfReviewProtectedImportService`. Nur der interne
`TrainingPdfReviewReader` stellt dem Batch den rohen Reader bereit, damit der
Eval-Schutz einmal vor dem Stapel und nicht erneut je PDF geladen wird.
`TrainingPdfReviewProtectionSnapshot` kopiert und validiert ausschliesslich
64-stellige SHA-256-Werte sowie normalisierte numerische Haltungskeys. Bei
konfiguriertem Eval-Root muss der PDF-Weg mindestens Haltungskeys besitzen, weil
die sichere CMYK/YCCK-Normalisierung Bildbytes veraendern kann. Exakte Bildbytes
und gleiche oder umgedrehte Eval-Haltungen werden je Foto vor Matching und vor
der Arbeitsablage ausgelassen. Unlesbare oder semantisch ungueltige Schutzdaten
sperren den Einzel- und Ordnerimport.

`TrainingStudioWindow` bleibt fuer Ordnerdialog, Fortschritt und Abbruch
zustaendig. Waehrend des Imports sind Quellenwechsel, Box-/Goldaktionen und
Tastaturkuerzel gesperrt. Die horizontale Warteschlange verwendet einen
recycelnden `VirtualizingStackPanel` und dekodiert Vorschaubilder nur mit
160 Pixel Breite; grosse PDF-Stapel duerfen nicht wieder in ein nicht
virtualisiertes `ItemsControl` zurueckgebaut werden.
PDF-JPEGs mit exaktem `DeviceCMYK`/Adobe-YCCK oder einer nicht identischen
`Decode`-Regel laufen vor Vorschau, SAM und Trainingsablage ueber
`ITrainingPdfJpegColorNormalizer`. Der WPF-Adapter rekonstruiert bei CMYK zuerst
die PDF-DCT-Kanalpolaritaet, wendet danach die echte PDF-`Decode`-Regel an und
speichert ein RGB-PNG. `TrainingPdfEmbeddedImageReader` kapselt Format-, Mass-
und Farbraumpruefung ausserhalb des Dokument-Readers. `DeviceGray` und
`DeviceRGB` ohne Farbtransformation
bleiben unveraendert; Cal-, ICC-, Indexed-, DeviceN- und unbekannte Farbraeume
sowie CMYK-JPEGs ohne eindeutigen Adobe-Farbmarker werden fail-closed ausgelassen.
JPEG- und PNG-Abmessungen muessen exakt zu den PDF-Deklarationen passen.
`TrainingPdfHaltungId` normalisiert echte Haltungsnummern ohne Eval-Abkuerzungen.
Kompakte Datumsbloecke vor der Datei-ID werden nur bei passendem Elternordner
abgetrennt. Der Reader erkennt einen sicheren Custom-Font-Shift einmal je Seite
und wendet ihn auch auf jeden lokalen Fotoblock an. Ein
`Haltungsinspektion`-Haupttitel ist kanonisch; nur die zweizeilige Fretz-Tabelle
derselben Titelseite darf einen internen Alias belegen. `Haltungsbilder`-Titel
lernen keine Aliase, waehrend direkte `Haltung`-Felder ohne Haupttitel echte
Abschnittsmarker bleiben. Sammel-PDFs tragen die explizite Abschnittshaltung pro Foto bis
`WorkbenchItem.CaseId` weiter, statt mehrere Haltungen zusammenzulegen;
mehrdeutige Abschnitte und globale Befund-Fallbacks ueber Haltungsgrenzen werden
ausgelassen. Sichere Abschnittstexte werden einmal je Haltung materialisiert und
bewahren lokale Meter-, Befund- und Streckenschadendaten.
`TrainingPdfProtocolFindingParser` liest Befundzeilen und paart Start/Ende;
`TrainingPdfProtocolMetadataParser` behaelt Dokument- und Haltungsmetadaten.
Inspektionsdatum, vollstaendiger
mehrzeiliger Befundtext und sicher verbundene Von-Bis-Meter eines Streckenschadens
bleiben als Referenz erhalten. Der Reader begrenzt zusaetzlich die kumulierten
Fotobytes und -pixel, bevor ein grosses PDF den Arbeitsspeicher erschoepfen kann.
Die Referenz steht ausschliesslich in `WorkbenchItem.SourceSuggestion`;
`ExistingCode` bleibt Reparaturen vorhandener Samples vorbehalten. KI-Vorschlaege
duerfen die sichtbare Operateurvorgabe nicht still ueberschreiben. Gold, KB und
Teacher entstehen weiterhin erst nach persoenlicher BBox, gueltiger sichtbarer
SAM-Maske und Akzeptieren.
Ein bestaetigtes PDF-Sample traegt `SourceType=PdfPhoto`; `Notes` bewahrt
Dokumentname, vollstaendigen PDF-Hash, Seite, Foto-ID und Zuordnungsart als
Pruefspur. `SourceReferenceCode` und `SourceReferenceDescription` bewahren die
urspruengliche Operateurangabe und sind fuer PDF-Gold beide Pflicht. Reparaturen
duerfen diese Felder, Inspektionsdatum oder die persoenliche Beschreibung nicht
still verlieren oder aus dem heutigen Endcode neu erfinden.

Der persoenliche Goldstand je Hauptcode wird durch
`PersonalGoldProgressCalculator` rein lesend mit dem Zielbereich 30-50 berechnet.
Das Training Studio aktualisiert ihn beim Oeffnen und nach jedem erfolgreichen
Speichern. Nur `Approved`-Samples mit lesbarer Bilddatei und vollstaendiger
Gold-Geometrie zaehlen als fertig; Drafts oder fehlende/unlesbare Bilder werden nie
hochgezaehlt. Album und Fortschritt zeigen eigene Drafts sowie persoenlich
bestaetigte Reparaturfaelle, ohne sie als fertiges Gold zu zaehlen.
`Segmentierung abarbeiten` baut ueber `WorkbenchQueueService` eine getrennte
Masken-Reparaturliste: nur lesbare eigene Bilder mit fehlender oder ungueltiger
SAM-Maske. RLE, Maskenflaeche, 80-Prozent-Boxregel und gespeicherte Maskenmasse
werden geprueft; `TrainingImageFileProbe` vergleicht die Masse mit dem echten Bild.
Eine gueltige vorhandene Hand-Box wird als `WorkbenchItem.ExistingBox` uebernommen
und beim Anzeigen automatisch erneut an SAM sowie den Codevergleich gegeben. Ohne
gueltige Box bleibt die allgemeine Foto-KI nur Orientierung und der Mensch zeichnet
selbst. Akzeptieren ohne gueltige sichtbare Maske ist in dieser Liste gesperrt.
Beim Nachlabeln traegt `WorkbenchItem.ExistingSampleId` die Identitaet;
`AnnotationWorkbenchService` ersetzt dasselbe Sample, statt ein Duplikat oder eine
zweite Arbeitskopie anzulegen. Spaete Box-Ergebnisse nach einem Bildwechsel werden
verworfen.
Ein alter `PdfPhoto`-Entwurf mit exakt gleicher PDF-Herkunft, Bilddatei, Haltung und
Code wird nicht erneut angeboten, wenn bereits ein geometrisch gueltiges
`Approved`-Sample fuer diese Referenz existiert. Die historische Zeile bleibt
gespeichert und wird nur aus der Reparaturliste ausgeblendet.
Die Thumbnail-Auswahl nutzt `SelectQueueItemAsync`; in der Reparaturliste kann sie
keinen noch offenen Fall vor dem persoenlichen Akzeptieren ueberspringen.

`Goldpruefung (90)` startet eine fortsetzbare Qualitaetsrunde mit je 15
freigegebenen Goldbildern fuer `BAB`, `BAF`, `BAI`, `BAJ`, `BBC` und `BBF`.
`Application/UseCases/GoldQualityReview/GoldQualityReviewQueueUseCase` waehlt nur
die einzeln freigegebenen Sample-IDs des Exportregisters. Der Infrastructure-
`GoldQualityReviewSnapshotProvider` verlangt einen erfolgreichen strikten
Live-Inventarlauf und schliesst Eval-Bild-Hashes sowie Eval-Haltungen aus.
`GoldQualityReviewSessionFileStore` speichert unter
`<KnowledgeRoot>\training\gold_quality_reviews` ein unveraenderliches Manifest,
das Register-Hash, Schutzfingerprint, Bild-Hashes und Ausgangsbestaetigungen bindet.
Ein unveraenderlicher Abschlussbeleg je Sample weist die persoenliche
Wiederbestaetigung nach; eine externe Neuspeicherung allein zaehlt nicht. Vor dem
Schreiben werden der gebundene Sample-Zeitstand und exakt dieselben Bildbytes
geprueft. Korrigierte Uhrlage und Schadensstufe bleiben in `TrainingSample.CodeMeta`
erhalten. `WorkbenchItem.ExistingSegmentation` zeigt die gespeicherte Maske ohne
neuen SAM-Lauf; erst eine neue Hand-Box ersetzt sie. Gespeichert wird ueber
`ExistingSampleId`, nicht als Dublette.
Die Bestandsmetadaten-Uebernahme liegt in
`UI/Services/AnnotationWorkbenchService.SampleMapping.cs`. Die reine, nicht
speichernde Modellvorschau ist in
`UI/ViewModels/TrainingStudioViewModel.PreviewDetection.cs` getrennt.

Die parallele SAM-/Code-Analyse liegt in
`Application/UseCases/TrainingStudioSegmentation/TrainingStudioBoxAnalysisUseCase`;
die UI-Koordination der Liste ist in `TrainingStudioViewModel.RepairQueue.cs`
getrennt. `TrainingImageFileProbe` prueft Bildkopf und volle Dekodierbarkeit.
`TrainingStudioBoxAnalysisUseCase.ValidateSegmentation` bewahrt Fehlerart und
Klartextgrund. Eine sichtbare, aber nicht goldfaehige Maske wird im Training Studio
orange statt gruen gezeichnet und nennt etwa den echten Anteil innerhalb der Box;
sie wird nicht still auf die Hand-Box zugeschnitten.
`WorkbenchSaveResult.GoldApproved` ist nur nach dem vollstaendigen persoenlichen
Gold-Gate wahr. Training Studio und `PhotoAnnotationUseCase` duerfen einen nur als
Draft gespeicherten Fall deshalb weder als Gold melden noch als erledigt behandeln.

Der allgemeine Pruefplatz-Befehl `Foto allgemein mit KI pruefen` laeuft ueber
`AnnotationWorkbenchService.SuggestPhotoAsync` und den zentralen
`IProtocolAiService`. Er uebergibt das ganze Foto sowie die erlaubten Codes des
aktiven VSA-Katalogs. Das Ergebnis ist nur ein anklickbarer Vorschlag; Hand-Box,
SAM-Maske, vorhandene Codierung und Beschreibung duerfen dabei nicht veraendert
werden, und es gibt keinen Schreibweg zu Goldsample oder KB. Der automatische
Vorschlag beim Box-Ziehen bleibt ein getrennter, schneller YOLO-Classifier-Aufruf.
Unbekannte Klassen und ein nicht geladenes Modell duerfen nie als VSA-Vorschlag
erscheinen. Fuer diesen Befehl muss `AiInput.RequireImage` gesetzt sein, damit ohne
wirklich lesbares Foto kein reiner Text-/KB-Vorschlag entsteht. Nach einem Bildwechsel
wird ein spaet eintreffendes Ergebnis verworfen.

Eine persoenlich uebernommene Auswahl aus dem VSA-Codierfenster ist eine bewusste
Handcodierung. `WorkbenchCodeSelectionMapper` uebernimmt dafuer Code,
`ProtocolEntry.Beschreibung`, Uhrlage und Stufe. Das ViewModel ersetzt mit dieser
Katalogbeschreibung nur ein leeres Beschreibungsfeld oder den automatischen
Platzhalter; einen selbst geschriebenen Text darf es nicht ueberschreiben.
KI-Vorschlaege und direkt eingetippte Codes bleiben davon getrennt. Fuer Gold sind
weiterhin rote Hand-Box, gueltige SAM-Maske und persoenliches Akzeptieren Pflicht.

`PersonalGoldAlbumWindow` liest ueber `IPersonalGoldAlbumService` und
`PersonalGoldAlbumService` ausschliesslich persoenlich bestaetigte Handlabels.
Es gruppiert nach Hauptcode und zeigt Kacheln sowie eine grosse Detailansicht mit
Code, Beschreibung, Datei- und Geometriestatus. Das Album ist rein lesend; keine
Bild-, Sample- oder KB-Mutation in Fenster oder ViewModel einfuehren.

Der vorbereitende Bildeingang liegt unter
`<KnowledgeRoot>\training\gold_inbox`. `PersonalGoldInboxFileService` legt die
produktiven Hauptcode-Unterordner mit Code und Klartext an, zum Beispiel
`BAB - Riss` und `BCA - Seitlicher Anschluss`, sowie `_OHNE_ZUORDNUNG`.
Alte reine Codeordner wie `BAB` bleiben lesbar. Es liest nur JPG/JPEG/PNG aus der
Wurzel und der ersten Ordnerebene, folgt keinen Reparse Points und veraendert keine
Eingangsdatei. `Gold-Eingang oeffnen` verwendet den zentralen
`IFolderOpenService`; `Eingang laden` baut ueber `WorkbenchQueueService` die
Pruefplatz-Liste. Ein Ordnername ist nur `SuggestedMainCode` und darf nie automatisch
zum finalen VSA-Code werden. Goldstandard entsteht weiterhin erst nach Codierung,
BBox, SAM-Segmentierung und persoenlichem Akzeptieren.
Der ebenfalls angelegte Ordner `_ERLEDIGT` wird beim Laden uebersprungen; das
Programm verschiebt Dateien nicht selbst dorthin.

`PersonalGoldMainCodeCatalog` verbindet die Pflichtcodes mit dem Klartext des aktiven
VSA-Katalogs. Goldstand, Goldalbum und Ordnerhinweis zeigen dadurch Code plus Klartext.
Der nicht als Basiscode vorhandene BBD-Anker bleibt der ausdrueckliche Sonderfall
`BBD - Eindringender Boden`; niemals den allgemeinen BB-Gruppentext anzeigen.

Beim Bestaetigen kopiert `AnnotationWorkbenchService` das unveraenderte Bild zuerst
ueber `ITrainingFrameStore.StoreExistingAsync` inhaltsadressiert nach
`<KnowledgeRoot>\gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>`.
Der endgueltig gespeicherte Code bestimmt den Ordner, auch nach einer persoenlichen
Korrektur. Das Original bleibt unveraendert.
Erst danach referenzieren `training_samples.json`, der SQLite-Wissensindex und der
abgeleitete Teacher-Eintrag diesen Goldpfad. Ohne sichere Goldkopie entsteht kein
Goldsample.

Der getrennte Foto-Assistent bindet vor und nach SAM dieselben Originalbytes per
SHA-256 in einem privaten `WorkbenchImageSnapshot`. Sein additiver
`IAnnotationWorkbenchService.SaveAsync`-Overload verwendet eine einzige Arbeitskopie
dieses Snapshots sowohl fuer `EvalContaminationGuard` als auch fuer
`ITrainingFrameStore.StoreBytesAsync`; der veraenderbare Quellpfad wird beim
Speichern nicht erneut gelesen. `WorkbenchItem.IsStreckenschaden` wird in
`TrainingSample.IsStreckenschaden` uebernommen und beim Laden der Reparatur-Queue
beibehalten.

Mehrere Foto-Drafts laufen vor der ersten Speicherung durch
`PhotoAnnotationBatchSaveUseCase`. Bei einem spaeten Teilerfolg bleibt der vorher
eingefrorene Protokolleintrag verbindlich und wird mit den schon geschriebenen
Sample-IDs markiert; der generische Coding-Speicherweg muss ihn danach ueberspringen.
Bei einem noch offenen Streckenschaden steht dieses Goldfoto nur fuer den Startpunkt;
das spaetere automatische Ende aktualisiert es nicht und erzeugt kein zweites
Foto-/Maskensample.

Der Gold-Wahrheitspfad verwendet zentral `ManualGoldTrainingPolicy`,
`SamMaskFormatValidator` und `GoldDescriptionPolicy`. Die normalisierte Hand-Box
muss vollstaendig im Bild liegen. SAM-RLE darf entsprechend dem echten Encoder eine
gerade oder ungerade Tokenzahl besitzen, muss aber mit Startwert 0/1, positiven Runs,
exakter Laufsumme und mindestens einem Vordergrundpixel formal gueltig sein.
Mindestens 80 Prozent aller echten Maskenpixel-Mittelpunkte muessen in der
Hand-Box liegen; eine nur ueberlappende Masken-Huelle reicht nicht.
Maskendimensionen muessen zum echten Goldbild passen. Die Maskenflaeche wird aus
der RLE abgeleitet; widerspruechliche Sidecar- oder gespeicherte Metadaten sperren.
Fuer neue Goldsamples sperrt
`GoldBeschreibungGuard` unfertige Platzhalter. Historische Platzhalter duerfen den
reinen YOLO-BBox-Export nicht entwerten, bleiben aber fuer KB-Index und
Qwen-Retrieval gesperrt. `KnowledgeBaseManager.IsIndexWorthy` verlangt die komplette
persoenliche ManualGold-Freigabe, den zugelassenen Benutzer, vorhandenes Bild,
gueltige Geometrie, fertigen Text und einen exakt auswaehlbaren, nicht nur ueber
den Hauptcode beschriftbaren Code des aktiven Katalogs.

Persoenliche Annahmen und Korrekturen im Player-Codiermodus laufen ueber
`CodingTrainingSamplePersistenceCoordinator`. `CodingEventToSampleMapper` markiert
nur `Accepted` oder `AcceptedWithEdit` mit Benutzer und Bestaetigungszeitpunkt als
`ManualCoding` sowie `ReviewApproved`/`ReviewCorrected`. Vorhandene Fotos werden mit
`ITrainingFrameStore.StoreExistingAsync`, bestaetigte Player-Frames mit
`StoreBytesAsync` inhaltsadressiert in den Klartext-Hauptcode-Unterordner von
`gold_frames` geschrieben. Der Store prueft vorhandene und neue Bildbytes und
ersetzt ein beschaedigtes Inhaltsziel nur durch eine gepruefte atomare Kopie. Erst danach
folgen `training_samples.json` und der KB-Index. BBox und vorhandene SAM-RLE-Daten
werden uebernommen. Fehlt Bild, Box oder SAM, bleibt der Eintrag unvollstaendig und
ist fuer den Trainings-Export gesperrt.
Stapel-Speicherfehler muessen bis zum roten Player-Overlay
`Training nicht gespeichert` weitergegeben werden; kein reines Hintergrund-Logging.
`CodingSessionService` darf aus diesem Weg ebenfalls nur strikt persoenlich
bestaetigte Goldsamples mit vorhandenem Goldbild indexieren. Der allgemeine
Session-Abschluss darf weder fremde Freigaben aufnehmen noch persoenliche
Gold-Metadaten ueberschreiben.

`PersonalGoldFrameMigrationService` und `tools/PersonalGoldMigration` uebernehmen
bestehende persoenliche Handlabels wiederholbar in die Klartext-Hauptcode-Unterordner
von `gold_frames`. Die Migration
prueft die Quellen vor dem Umschalten und haelt `training_samples.json` sowie
`KnowledgeBase.db` gemeinsam konsistent; bei Fehlern werden beide Pfadstaende
zurueckgesetzt. `PersonalGoldMigrationCommitter` kapselt Umschalten, Nachpruefung
und Ruecksetzung getrennt von Auswahl und Dateivorbereitung und erneuert nach Erfolg
das Dateimanifest. Das Inventar liegt unter
`<KnowledgeRoot>\training\gold_standard\main_code_inventory_v1.json`, die Pruefspur
unter `<KnowledgeRoot>\training\gold_migrations`. Wissens-ZIP-Sicherungen enthalten
`gold_frames` rekursiv.

`PersonalGoldBrainSeparationService` und `tools/GoldBrainSeparation` sind der
einmalige, sichere Trennweg fuer einen bestehenden Mischbestand. Der Standardlauf
prueft nur. `--execute` erstellt zuerst einen geprueften Gold-only-Arbeitsstand und
benennt danach Quelle, Altarchiv und Arbeitsstand nur innerhalb desselben
Datentraegers atomar um. Der vollstaendige lokale Altstand bleibt als
`<KnowledgeRoot>_ALT_<Zeitstempel>`, der bisherige Elements-Spiegel als
`<Elements>\Brain_Archiv\KI_BRAIN_ALT_<Zeitstempel>`. Das neue aktive Gehirn
enthaelt ausschliesslich persoenlich bestaetigte Handlabels und ihre Embeddings;
Teacher- und Protokoll-Kontext starten leer. Beleg und Dateimanifest liegen unter
`<KnowledgeRoot>\training\gold_standard`. Altarchive tragen den Marker
`.sewerstudio-legacy-brain-archive` und duerfen nicht als aktive Wissenswurzel
verwendet werden.
Die Fassade bleibt klein; Eingabe, Dateigrenze, Arbeitsbereich, Datenbankaufbau,
Manifest, Commit und Wiederaufnahme liegen in `PersonalGoldBrainSeparationInput`,
`PersonalGoldBrainFileService`, `PersonalGoldBrainWorkspace`,
`PersonalGoldBrainDatabaseBuilder`, `PersonalGoldBrainManifestWriter`,
`PersonalGoldBrainCommitExecutor`, `PersonalGoldBrainCommitJournalStore` und
`PersonalGoldBrainCommitRecovery`.
Vor der ersten Umbenennung muss
`<KnowledgeRoot>.gold-brain-separation.commit.json` atomar vorliegen. Ein spaeterer
Ausfuehrungslauf setzt einen unterbrochenen Commit anhand exakter Pfade, TxId und
Inhaltssignaturen sicher auf den geprueften Ausgangsstand zurueck; ein Dry-Run
veraendert ein offenes Journal nie.
Alte absolute Framepfade unter der bisherigen Wissenswurzel werden auf das lokale
Archiv abgebildet, echte externe Pfade bleiben extern. Wissens-, Archiv-, Spiegel-,
Staging- und Legacy-Protokollpfade duerfen sich nicht ueberlappen. Vor dem Umschalten
muessen JSON und SQLite bei `SampleId` und allen 13 verbindlichen Gold-Feldern exakt
uebereinstimmen; genau ein Embedding muss vorhanden sein.
`PersonalGoldArchiveRecoveryService` vergleicht danach die aktive Trainingsliste
mit der archivierten SQLite-KB. `LegacyPersonalGoldDatabaseReader` waehlt nur
zulaessige Altzeilen aus, `PersonalGoldArchiveDatabaseImporter` uebernimmt sie.
Es werden ausschliesslich persoenlich bestaetigte `ManualCoding`-Zeilen mit
vorhandenem Bild und Embedding nachgeholt. Alte
`TeacherAnnotation`- und `VideoTimestamp`-Zeilen bleiben ausgeschlossen. Der
Nachholbeleg `gold_brain_archive_recovery_v1.json` dokumentiert IDs und Zielpfade.
`tools/GoldBrainSeparation --recover-from <Altarchiv>` stellt dafuer einen
wiederholbaren Pruef- und Ausfuehrungsweg bereit.
Vor der ersten Nachholmutation schreibt der Dienst
`<KnowledgeRoot>.gold-archive-recovery.transaction.json` sowie gepruefte
Vorherkopien von SQLite, Trainings-JSON, Inventar, Beleg und Manifest. Ein Neustart
setzt einen unterbrochenen Lauf samt neu angelegten Frames idempotent zurueck.
Fremde Artefakte, Hashabweichungen, falsche Besitzer, unsichere Pfade oder Junctions
fuehren sicher zum Abbruch; das Journal wird erst nach dem vollstaendigen neuen
Manifest entfernt. Die
Teilaufgaben liegen in `PersonalGoldArchiveRecoveryInput`,
`PersonalGoldArchiveRecoveryValidator`, `PersonalGoldArchiveRecoveryJournalStore`,
`PersonalGoldArchiveRecoveryTransaction`, `PersonalGoldArchiveRecoveryArtifacts`
und `PersonalGoldArchiveRecoveryOutput`.

Die Maus-, Bild- und Box-Koordinaten des Pruefplatzes werden ausschliesslich durch
`TrainingStudioImageGeometryMapper` abgebildet. Er beruecksichtigt den echten Ursprung
des WPF-Bildes, freie `Uniform`-Raender und begrenzt die Auswahl schon beim Ziehen am
Bildrand. Neue Boxen entfernen die alte Maske und den alten Vorschlag sofort, damit
keine geometrisch unpassenden Zustaende gemeinsam sichtbar sind.

Die öffentliche Fotomessungs-Fassade bleibt `PhotoMeasurementGeometryService`.
Winkel-, Abzweig-, Kreis- und Bogenplanung liegt zustandslos im internen
`PhotoMeasurementAnglePlanBuilder`; diese Rechenlogik nicht in WPF-Code zurückschieben.
Der synchrone Messfoto-Export liegt dagegen bewusst in der UI-Schicht im internen
`PhotoMeasurementOverlayExporter`: Er bekommt `BitmapSource`, den lebenden
Overlay-`Visual`, das bereits berechnete Letterbox-Rechteck und den Quellpfad. Er
rendert weiterhin in Original-Pixelgroesse mit 96 DPI/Pbgra32 und schreibt PNG
nach `<Name>_overlay.png`; nicht per `Task.Run` auf einen anderen Dispatcher
verschieben. `PhotoMeasurementCompletionWorkflow` versucht den Export nur bei
vorhandener Geometrie und liefert auch bei `null` oder Exportfehler ein
bestaetigtes Messergebnis mit derselben Geometrie und Kalibrierung. Der Fehler
wird weiterhin ueber `UserError` beschrieben. Beide UI-Helfer werden lokal am
Fenster verwendet und brauchen keine `ServiceProvider`-Registrierung.

`LegacyMeasureTemplateConverter` unter `Infrastructure/Costs` ist der reine,
zustandslose Adapter vom ehemaligen Editorformat aus `Domain.Models.Costs` zum
aktiven `MeasureTemplateCatalog`. Das `MeasureTemplateEditorViewModel` ruft ihn
direkt nach der toleranten JSON-Deserialisierung und vor
`LoadUserOverrides()` auf. Zeitstempelvergleich, Rueckfrage, Datei-I/O,
Zusammenfuehren, Speichern und Meldungen bleiben im ViewModel. Der Konverter
bewahrt Reihenfolge und Duplikate, trimmt IDs, Namen, Gruppen und Positionskeys,
filtert leere IDs und Positionskeys und verwendet bei fehlender oder ungueltiger
Altmenge weiterhin `1`. Keine Mengenbegrenzung, Entdoppelung oder stillen
Null-Fallbacks hinzufuegen; das waere eine Verhaltensaenderung. Die statische
Klasse braucht weder Interface noch `ServiceProvider`-Registrierung.

`ConnectionQuantityPolicy` unter `Infrastructure/Costs` ist die gemeinsame reine
Entscheidungsquelle fuer Anschlussmengen bestehender Kostenzeilen. Sowohl
`MeasurePricingEngine.ApplyConnectionsToLines` als auch
`MeasureBlockVm.ApplyConnectionsToLines` verwenden `Evaluate`; Domain- und UI-
Mutation bleiben getrennt. Im UI-Weg muss eine Null-Zeile zuerst reaktiviert und
erst danach mit `ResolveSuggestedQuantity` der dann aktuelle Mengen-Override
geprueft werden. So duerfen synchrone `LineChanged`-Empfaenger die automatische
Menge weiterhin verhindern. 0 und negative Anschlusszahlen setzen Menge und
Override zurueck, deaktivieren die Zeile und entfernen die Uebertragungsmarke.
Positive manuelle Mengen bleiben erhalten; eine abgewahlte Zeile mit positiver
Menge bleibt abgewahlt. `AddLineFromCatalogKey` ist bewusst kein Verbraucher der
Policy: Sein historischer Sonderweg setzt die Anschlussmenge trotz des beim
Erzeugen bereits gesetzten Override-Flags und behaelt dieses Flag. Diesen Weg nur
als eigene Verhaltensaenderung vereinheitlichen. Die statische Policy braucht
weder Interface noch `ServiceProvider`-Registrierung.

`ProtocolEntryEditorMediaPathResolver` ist die dialoggebundene, WPF-freie Grenze
fuer Projekt-, Video- und Bildpfade des `ProtocolEntryEditorDialog`. Der Dialog
uebergibt den ausdruecklichen Projektordner einmal und eine Funktion fuer den
jeweils aktuellen `Settings.LastProjectPath`; Einstellungen nicht beim
Fensteraufbau einfrieren. Der Resolver verwendet `ProjectFileLocator` fuer den
echten Root bei `Projektdateien\\projekt.json` und faellt zuletzt auf
`AppDomain.CurrentDomain.BaseDirectory` zurueck. Bestehendes Verhalten bleibt
vorerst: Ein direkt existierender, auch relativer Pfad gewinnt vor der
Projektaufloesung, ein fehlender verwurzelter Pfad ergibt `null`, relative Pfade
werden mit dem Projektordner kombiniert und Bildpfade in Eingabereihenfolge ohne
Beachtung der Grossschreibung entdoppelt. Pfadfehler laufen weiterhin vor dem
KI-Busy-Zustand nach aussen. Projektgrenzen fuer `..`, laufwerksrelative
`C:datei`-Pfade und eine moegliche `null`-Fotoliste nur als getrennte fachliche
Verhaltensaenderung entscheiden. Die lokale Instanz braucht keine
`ServiceProvider`-Registrierung.

`PlayerWindowLiveDetectionStatusInitializer` baut direkt nach
`InitializeComponent()` die fenstergebundenen
`LiveDetectionPulseController`- und `LiveDetectionStatusController`-Instanzen.
Die Zuordnung der Badge-, YOLO-, Coding-, Erkennungs- und Zusammenfassungs-
Controls bleibt in einem ausdruecklichen Control-Buendel sichtbar. Der
Initializer verwendet den vorhandenen `LiveDetectionPulseStateController` und
den Dispatcher des Fensters. Er erzeugt den Puls zuerst und bindet genau diese
Instanz als Start-/Stop-Aktion an den Status-Controller; keinen zweiten Puls oder
Zustand anlegen. Diese lokale WPF-Komposition braucht keine
`ServiceProvider`-Registrierung und darf nicht wieder als verschachtelte
Controller-Erzeugung in den `PlayerWindow`-Konstruktor zurueckwandern.

`PlayerWindowLiveDetectionControllerSetFactory` setzt den fenstergebundenen
`LiveDetectionStopController` und `LiveDetectionLifecycleController` gemeinsam
zusammen. Sie erzeugt den Stop-Controller zuerst, bindet genau dessen `Stop`-Weg
an den Lifecycle-Controller und gibt beide Instanzen als Set an `PlayerWindow`
zurueck. Runtime-Zustand, Shutdown, manueller Markiermodus, Ereigniszahl und
Wiedergabestatus werden erst beim Starten oder Stoppen gelesen; keine Werte beim
Fensteraufbau einfrieren. Canvas, Overlay, Statusanzeige, Zusammenfassung und
Live-Detection-Schalter bleiben als ausdrueckliches Control-Buendel sichtbar.
Der produktive Start laeuft weiter ueber `LiveDetectionStartupDisplayWorkflow`,
das verzoegerte Ausblenden ueber `LiveDetectionHideStatusTimerWorkflow` und die
erste Erkennung mit dem Operationsnamen `LiveDetection`. Diese lokale
WPF-Komposition braucht keine `ServiceProvider`-Registrierung und darf nicht
wieder als direkte Doppelverdrahtung in den `PlayerWindow`-Konstruktor wandern.

`PlayerWindowLiveDetectionMarkToolControllerFactory` setzt den
fenstergebundenen `LiveDetectionMarkToolController` aus den vorhandenen
`PlayerMarkToolControls`, `CodingRuntimeStateControllerSet`,
`CodingSchemaStateControllerSet` und genau dem `CodingSessionRuntime` zusammen,
dessen ViewModel-, Session- und Overlay-Hosts auch `PlayerWindow` verwendet.
Keine zweiten Owner oder Zustandsbuendel anlegen. Overlay-Bereitschaft,
Session-/Overlay-Dienste, Video-Pfad, Einstellungen, Trainingsspeicher,
Codiermodus und laufende Erkennung werden erst bei der jeweiligen Aktion gelesen.
Der Punktweg darf keinen Coding-Zustand erzeugen; der Zeichenweg uebernimmt beim
Kaltstart Session-, Overlay- und ViewModel-Owner in dieser Reihenfolge und setzt
das ViewModel weiterhin mit `observePropertyChanged: false`. Vorhandene Dienste
referenzgleich wiederverwenden. Im Codiermodus bleibt das Coding-Overlay beim
Deaktivieren bestehen, bei laufender Erkennung das Detection-Overlay. Die
booleschen Rueckgaben von `SetActiveTool` und `CancelDraw` bleiben bewusst ohne
neue Fehlerlogik. Der Eingabemarker verwendet danach dieselbe Controller-Instanz
ueber `_liveDetectionMarkToolController.EnsureOverlayReady`. Diese lokale
Komposition braucht keine `ServiceProvider`-Registrierung und darf nicht wieder
als direkte Actions-Verdrahtung in den `PlayerWindow`-Konstruktor wandern.
Der Zeichenweg aktualisiert den Coding-Viewport vor dem Aktivieren der Eingabe.
`PlayerMediaRuntime.TryGetVideoAspect` liest dafuer die native LibVLC-Videogroesse
ueber `PlayerVideoAspectResolver` und beruecksichtigt dabei Sample-Aspect-Ratio und
Videoausrichtung; bei fehlenden oder fehlerhaften Metadaten bleibt der bisherige
Zustand erhalten. Dadurch rechnen Hand-Box und SAM-Maske auch bei
Letterbox/Pillarbox und alten PAL-Videos in dasselbe sichtbare Video-Rechteck.

`PlayerWindowCodingEingabemarkerControllerSetFactory` setzt direkt nach dem
Markierwerkzeug die drei fenstergebundenen Eingabemarker-Controller zusammen.
Sie erzeugt zuerst `CodingEingabemarkerInteractionController`, bindet genau
diese Instanz an `CodingEingabemarkerSubmissionController` und bindet danach
beide Instanzen an `CodingEingabemarkerInputController`. Das zurueckgegebene Set
wird den drei bestehenden Interface-Feldern in `PlayerWindow` zugewiesen; keine
zweiten Controller oder Zustaende anlegen. Text, Session-Service, Events,
Overlay, OSD-/Session-Meter, Session-/Player-Zeit, Label und Foto werden erst bei
der jeweiligen Aktion gelesen. Die Operationsnamen `TrainingSaveSingle` und
`SubmitEingabemarker` bleiben unveraendert. Controls und fensterspezifische
Aktionen sind ausdruecklich benannt, waehrend die Entscheidungslogik in den
vorhandenen Controllern und Workflows bleibt. Diese lokale WPF-Komposition
braucht keine `ServiceProvider`-Registrierung. Markierwerkzeug, Segmentierung und
KI-Analyse nicht als weitere Controller in dieses Set aufnehmen.

`PlayerWindowLiveDetectionMarkSegmentationControllerFactory` setzt danach den
fenstergebundenen `LiveDetectionMarkSegmentationController` aus genau dem
vorhandenen `CodingAiController`, `ICodingOverlayToolHost`,
`CodingOverlayCanvas` und dem Resolver fuer das aktuelle Coding-Inhaltsrechteck
zusammen. Verfuegbarkeit und Zugriff auf `BoxSegmentation` muessen beide erst bei
jedem Segmentierungsaufruf ueber dieselbe mutable AI-Controller-Instanz erfolgen;
den beim Fensteraufbau noch leeren Dienst nicht einfrieren. Kalibrierung und
Inhaltsrechteck ebenfalls erst bei der Aktion lesen. Die manuelle Vorschau zeigt
immer die echte SAM-Maske auf dem `CodingOverlayCanvas`; ein Bogen-Signal darf sie
nie durch den ovalen Bogenmarker ersetzen. Der Segmentierungsaufruf bleibt bei
`CancellationToken.None`, Fehler laufen weiter ueber `PlayerTrace.WriteLine`.
Entscheidungen und Quantifizierung verbleiben in
`LiveDetectionMarkBoxSegmentationWorkflow`,
`LiveDetectionMarkSamMaskRenderWorkflow` und
`CodingMarkBoxQuantificationOverlayPolicy`. Die echte Maske wird als
`OverlayGeometry.SamMask` ohne erfundenen `AiContext` am manuellen Ereignis
weitergegeben, bei Kopien tief geklont und von `CodingEventToSampleMapper` nach
strenger RLE-Pruefung ins Trainingssample uebernommen. Vor einem neuen
Segmentierungsversuch wird eine alte Maske entfernt, damit ein Fehlschlag nie eine
veraltete Maske als aktuelles Ergebnis behaelt. Diese lokale WPF-Komposition braucht
keine `ServiceProvider`-Registrierung; Markierwerkzeug, Eingabemarker, KI-Analyse
und Training nicht in diese Factory aufnehmen.

`PlayerWindowCodingModeExitControllerFactory` setzt den fenstergebundenen
`CodingModeExitController` nach dem vorhandenen Protocol-Match-Controller
zusammen. Sie verwendet dieselben Runtime-, Schema-, Overlay-, AI- und
Protocol-State-Sets, denselben `CodingSessionRuntime`, denselben
`CodingStreckenschadenTrackingController`, `CodingBoundaryContext`,
`LiveDetectionController`, Pipeline-Health-Controller und
`CodingOverlayInputVisibilityController` wie das Fenster. Keine zweiten Owner
oder Controller anlegen. Codiermodus, Ereignisse, OSD-Meter, Endmeter,
Videodauer, analysierter Frame, Live-AI-Timer, ViewModel und laufende Erkennung
werden erst bei `Exit()` gelesen. Der Modus wird vor der Finalisierung auf
`false` gesetzt; ein abgebrochener Streckenschaden-Abschluss stellt genau diesen
Zustand auf `true` zurueck und darf keinen Teardown starten. Der erfolgreiche
Abbau bleibt vollstaendig im bestehenden `CodingModeExitTeardownWorkflow`; seine
27 Schritte und ihre Reihenfolge nicht in der Factory nachbauen oder aendern.
Lesen, Stoppen, Entsorgen und Zuruecksetzen des OSD-Zustands muessen ueber
dieselbe `CodingOsdMeterController`-Instanz laufen. Der Endzeit-Parameter bleibt
beim Aufruf von `CodingBoundaryContext.EnsureEnd` bewusst ungenutzt. Die 19
WPF-Controls werden ausdruecklich benannt und synchron auf dem UI-Thread
bedient. Diese lokale Komposition braucht keine `ServiceProvider`-Registrierung;
`CodingApply`, Protocol-Match und andere Controller nicht in diese Factory
aufnehmen.

Beim bestaetigten Coding-Apply werden automatisch geplante `BCD`-/`BCE`-Grenzen
ueber `CodingApplyController` in genau dieselbe `ICodingSessionService`-Sitzung
aufgenommen. Der naechste Apply verwendet sie dadurch als echten Ausgangsstand;
er darf sie weder erneut anlegen noch als geloescht behandeln. Die Metadaten setzen
`SkipAutomaticPersistence=true`, damit eine technische Grenze kein Trainingsfall
wird. Abbrechen bleibt ohne Seiteneffekt. `CodingHaltungslaengeResolver` akzeptiert
nur ein gueltiges `Haltungslaenge_m`, `Laenge_m` unter Erhalt seiner `FieldSource`
oder genau ein aktives, nicht durch `BDC` abgebrochenes `BCE`. Die BCE-Ableitung
traegt `FieldSource.Protocol` und bleibt in `MergeEngine` unter echten Importquellen.

`PlayerWindowCodingConfirmationControllerFactory` setzt die beiden
fenstergebundenen Bestaetigungs-Controller zusammen. Sie erzeugt zuerst den
`CodingConfirmationDecisionController` und bindet dessen Accept-, Edit- und
Reject-Aktionen an den zurueckgegebenen `ICodingConfirmationController`. Beide
verwenden genau denselben `CodingPendingConfirmationStateController`. Session,
Ereignissammlung, aktueller Statustext, Live-AI-Schalter und Modellname werden
ueber die vorhandenen Owner, Hosts und Controls erst bei der Benutzeraktion
gelesen; keine Werte beim Fensteraufbau einfrieren. Panel-Owner, Seitenpanel,
Trainingspersistenz und Live-Detection-Status muessen vorher initialisiert sein.
Persistieren bleibt Fire-and-forget mit dem vom Decision-Controller gelieferten
Operationsnamen. Die lokale Factory besitzt keinen eigenen Lebenszyklus und
braucht keine `ServiceProvider`-Registrierung; die direkte Doppelverdrahtung nicht
in den `PlayerWindow`-Konstruktor zurueckschieben.

`CodingEventListVisualController` besitzt die WPF-Darstellung der beiden Coding-
Ereignislisten: Zone, Konfidenz und Status der KI-Liste sowie Leeren oder Setzen
der Protokollabgleich-Hervorhebung in Coding- und Importliste. `PlayerWindow`
erzeugt ihn nach dem Seitenpanel-Aufbau mit `LstCodingEvents`, `LstImportEvents`
und dem langlebigen `CodingProtocolMatchStateController`. Listen nicht
vertauschen und keinen neuen Match-Zustand erzeugen; Ergebnisse entstehen erst
nach dem Controller-Aufbau. Die Aufrufe bleiben über den vorhandenen
`DispatcherPriority.Loaded`-Nachlauf geplant. Der UI-Controller braucht keine
`ServiceProvider`-Registrierung.

`CodingStreckenschadenTrackingController` besitzt pro `PlayerWindow` genau einen
`CodingStreckenschadenTrackerOwner`. Derselbe Controller wird fuer jeden
Multi-Modell-Tick, plausibles BCE, Codiermodus-Ausstieg und den Reset nach der
Import-Initialisierung verwendet. Auch eine leere Segmentliste muss
`ApplyTracking` erreichen, damit eine mehr als einen Meter nicht mehr sichtbare
Strecke am letzten Sichtmeter geschlossen wird. Der Controller wird direkt nach
`CodingEventsRefreshController` aufgebaut, liest Session-Service,
Ereignissammlung und aktuelle Player-Zeit erst beim Aufruf und verwendet die
vorhandenen Tracking-/Apply-Workflows, Observation-Builder und Action-Applier.
Diese Logik nicht in ein neues `PlayerWindow`-Partial zurueckschieben und nicht
mit dem getrennten manuellen Streckenschaden-Schliessen vereinheitlichen. Der
fenstergebundene Controller braucht keine `ServiceProvider`-Registrierung.

`CodingOverlayInputVisibilityController` ist die fenstergebundene Grenze fuer
verschachteltes Sperren und garantiertes Wiederherstellen der Coding-
Zeichenflaeche. Er verwendet genau den vorhandenen
`CodingOverlayInputVisibilityStateController` aus `CodingOverlayStateControllerSet`;
keinen zweiten Zustand anlegen. Dialoge, asynchrone Codeauswahl,
Fenster-Deaktivierung/-Aktivierung und der Reset beim Codiermodus-Ausstieg muessen
dieselbe Controller-Instanz verwenden. WPF-Controls werden nur einmal im
`PlayerWindow`-Konstruktor als Aktionen gebunden. Die vorhandenen Visibility- und
Interaction-Workflows bleiben die Entscheidungsquelle; insbesondere muss Resume
auch nach synchronen und asynchronen Fehlern im `finally` erfolgen. Der Controller
braucht keine `ServiceProvider`-Registrierung und die Logik darf nicht in ein neues
`PlayerWindow.Coding.OverlayInput.Visibility`-Partial zurueckwandern.

`CodingPhotoAttachmentController` ist die fenstergebundene Grenze fuer das
Anhaengen von KI-Frames und fuer manuell aufgenommene Coding-Fotos. Genau eine
Instanz wird im `PlayerWindow`-Konstruktor aufgebaut; sie braucht keine
`ServiceProvider`-Registrierung. Der normale KI-Pfad versucht zuerst die
asynchrone Frame-Extraktion, danach den gepufferten Bestaetigungsframe und erst
bei fehlgeschlagenem `ICodingFramePhotoStore`-Anhaengen den bestehenden
Snapshot-Rueckfall. Nur ein nichtleerer Ergebnispfad aktualisiert die Ereignisliste;
der Fire-and-forget-Kontext bleibt `AttachAnalyzedFramePhoto`. Der getrennte
Rohrgrenzen-Pfad haengt den bereits gelieferten Frame synchron an und darf weder
einen Snapshot-Rueckfall noch eine eigene Listenaktualisierung bekommen. Bei
manuellen Fotos werden Ereignis- und Videozeit vor dem Snapshot auf die aktuelle
Player-Zeit gesetzt: nur ein Capture-Fehler stellt die Originalzeiten wieder her;
Erfolg wendet den Foto-Slot mit dem aktuellen `ICodingSessionService` an und
aktualisiert die Liste. VideoPath, Frame-Store und Session-Service werden im
Composition Root aus den vorhandenen Laufzeitquellen gebunden. Die Orchestrierung
nicht in `PlayerWindow`-Partials zurueckschieben.

Das Rohr-Radar der Videoanalyse liegt zustandslos im internen
`PipelinePipeRadarRenderer`. `VideoAnalysisPipelineWindow` liefert nur Canvas,
Leerhinweis, Befunde, Anzeigeart und Groesse. Uhrparser und Ringgeometrie kommen aus
`LiveDetectionGeometryMapper`; diese Regeln nicht wieder im Fenster duplizieren.
Beim Ersetzen der Ergebnisliste das Radar-Ereignis kurz abhaengen und genau einmal
danach zeichnen; bis zu 250 Einzel-Neuzeichnungen nicht wieder einfuehren.
Der Renderer ist ein UI-Helfer und wird deshalb nicht im `ServiceProvider` registriert.

Der Live-Ring ueber dem Videobild liegt gemeinsam im internen
`LiveFrameRingOverlayRenderer`. `Compact` bewahrt den eingebetteten Pipeline-Stil,
`Detail` den abgedockten Stil und `Interactive` den klickbaren Player-Rueckfall.
`PipelineLiveFrameOverlayRenderer` besitzt nur die besonderen Leer- und Groessenregeln
des Hauptfensters. `LiveFrameWindow` zeichnet bei Groessenaenderungen neu. Eine
Fortschrittsmeldung mit Bild und Befunden darf den Ring nur einmal zeichnen.
Uhrparser und Uhrwinkel kommen aus `LiveDetectionGeometryMapper`, die Ringform aus
`RingSectorGeometry` und die gesaettigten Schadensfarben aus dem zentralen
`StatusColors.Current.SeverityOverlay`. Diese Regeln oder sichtbaren Stilunterschiede
nicht wieder in Fenster kopieren. Beide Renderer sind zustandslose UI-Helfer und
brauchen keine `ServiceProvider`-Registrierung.

Die Fortschrittsabbildung der Videoanalyse liegt im internen
`PipelineProgressMapper`. Pro Analyselauf wird genau eine Instanz erzeugt; sie besitzt
die laufbezogene ETA und uebertraegt Phase, Status, Zaehler, Vorschaubild und hoechstens
acht Live-Befunde in das `VideoAnalysisPipelineViewModel`. `LiveFindings == null`
behaelt die bisherige Liste, eine bewusst leere Liste leert sie. `Apply` liefert nur
die Hinweise `RenderLiveFrameOverlay` und `ForwardLiveFrame`. Canvas-Zeichnung,
abgedocktes Fenster und `Progress<PipelineProgress>` auf dem UI-Kontext bleiben im
`VideoAnalysisPipelineWindow`. Parser- oder ETA-Zuweisungen nicht wieder ins Fenster
kopieren. Der laufbezogene UI-Helfer braucht keine `ServiceProvider`-Registrierung.

Die erfolgreiche Abschlussabbildung der Videoanalyse liegt im internen, zustandslosen
`PipelineResultPresenter`. Er uebernimmt Abschlussstatistik, Telemetrie und baut die
hoechstens 250 sichtbaren `DetectionItem`-Zeilen. Sobald gemappte Eintraege vorhanden
sind, haben sie vollstaendig Vorrang; DetectionCount und die drei Saeulenzaehler kommen
weiter aus allen Rohbefunden. Fehler, `IsDone`/Status, `_result`, die gesammelte
`Vm.Detections`-Ersetzung, Radarzeichnung, Dialoge und Dokumentuebernahme bleiben im
`VideoAnalysisPipelineWindow`. Den Presenter nicht an Canvas, Fenster oder
`ServiceProvider` koppeln. Wichtig: Die 250 sichtbaren Zeilen liefern derzeit auch die
EntryIds fuer `Accept_Click`; weitere gemappte Eintraege waeren nicht auswaehlbar und
wuerden verworfen. Diese bestehende fachliche Grenze nicht still in einem Refactor
aendern, sondern separat entscheiden.

`TrainingYoloExportRuntime` in Infrastructure erzeugt die Training-Export-Dienste
genau einmal. `CreateHybrid` ist der WPF-Weg mit Sidecar und lokalem Rückfall;
`CreateLocal` ist der reine lokale CLI-Weg. Beide binden Knowledge-, Eval- und
Dataset-Root unveränderlich und liefern denselben Coordinator, Planer und lokalen
Dateischreiber. `TrainingYoloExportComposition` ist nur die dünne WPF-Hülle darum.
Der zentrale `ServiceProvider` hält diese Komposition; seine kompatiblen öffentlichen
Export-Zugriffe liegen in `ServiceProvider.TrainingYoloExport.cs` und werden weiter
unter ihren Interfaces registriert. `TrainingYoloExportDependencies` reicht nur den
Coordinator an Fenster und ViewModel. UI-Code erzeugt keine Export-Infrastruktur.

Die reine Zuordnung der bereits erzeugten Dienste liegt in
`ServiceProviderRegistrationMap`. Sie enthält aktuell 154 Vertragstypen und darf
selbst keine Dienste erzeugen. `ServiceProvider.cs` ruft die Map erst nach dem
vollständigen Aufbau auf.
`IProtocolPdfLayoutSettings` ist dort dieselbe einzelne, live aus `AppSettings`
lesende Instanz fuer `ProtocolPdfExporter` und die produktiven Dossier-Dialogwege.
Der Dialog darf `settings.json` beim Klick nicht erneut laden. Erlaubt sind 1, 2,
4 oder 6 Fotos je Seite; unbekannte Werte fallen auf 2 zurueck und explizite
`HaltungsprotokollPdfOptions` haben Vorrang.
Der aktive `CodeCatalog` wird vor dem gemeinsam genutzten `ProtocolPdfExporter`
erzeugt und diesem als Standardkatalog uebergeben. Ein explizit in
`HaltungsprotokollPdfOptions.CodeCatalog` gesetzter Katalog hat weiterhin Vorrang.
`ObservationZustandBuilder` ist die gemeinsame Klartextquelle fuer Befundetabelle,
Haltungsgrafik und Fototitel. Bei bekanntem Code kombiniert er Katalogtitel,
Operateurtext und vorhandene Parameter ohne doppelte Titel oder Uhrlagen.
`WinCanFindingFactory` haelt Meterstand (`MeterStart`/`MeterEnd`) und Uhrlage
(`SchadenlageAnfang`/`SchadenlageEnde`) strikt getrennt. `ProtocolTextHelpers` und
`ProtocolPdfEntryResolver` verwerfen alte, als Uhrlage gespeicherte WinCan-Meterwerte
wie `2.62136` und verwenden den naechsten gueltigen ClockPos-Wert beziehungsweise den
erfassten Befundtext.
Die Haltungsgrafik stationiert immer oben nach unten in Aufnahmerichtung. `flowDown`
steuert nur den Fliesspfeil und spiegelt den Anschluss nicht. In der WinCan-Draufsicht
liegen 1-5 Uhr auf dem Blatt links und 7-11 Uhr rechts; 3/9 Uhr sind waagrecht,
2/10 zeigen nach oben und 4/8 nach unten.

`FullBackupComposition` in Infrastructure baut Zielmarker, SQLite-Schnappschüsse,
Manifestprüfung und Vollsicherung genau einmal. Die UI behält AppSettings und
`IFullBackupSourcesProvider` und liefert nur die Quellenfunktion hinein. Öffentliche
Zugriffe liegen in `ServiceProvider.FullBackup.cs`; `BackupTargetGuard.UseMarkerGuard`
darf beim zentralen Aufbau nicht verwendet werden.

Die Wissens-ZIP-Dateiarbeit liegt ebenfalls in Infrastructure:
`KnowledgeBackupEngine`, `KnowledgeBackupFileCatalog` und
`KnowledgeBackupImportPostProcessor` unter `Infrastructure/Ai/Backup`. Die öffentliche
statische `KnowledgeBackupService`-Fassade und ihr verschachteltes `BackupResult`
bleiben kompatibel. `KnowledgeBackupTransferService` hält nur Aufrufsperre,
rechnerabhängige Pfade und Ergebnis-Mapping. Im zentralen `ServiceProvider` verwendet
die Wissenssicherung dieselbe `ISqliteSnapshotCopier`-Instanz wie die Vollsicherung.

Der Training-Center-Zustand liegt hinter `ITrainingCenterDocumentStore` und
`TrainingCenterDocumentFileStore`. Das neutrale Dokument bewahrt die bestehenden
JSON-Namen und den numerischen Status. Die eingefrorene UI-Datei
`Ai/Training/TrainingCenterStore.cs` bleibt als kleine Mapping-Fassade; `.bak`-Rückfall,
atomarer Austausch und `.bad_<Zeit>`-Quarantäne gehören in die Infrastructure.

`BackupSourcePathGuard` und `BackupTargetPathGuard` prüfen Pflichtquellen und jedes
Mutationsziel erneut. `DirectoryMirror`, `BackupTargetMarkerGuardService` und
`KnowledgeMirrorMarker` sichern Spiegelung und Zielidentität. Quellfehler stoppen
Löschung/Rotation. Im Standardplan dürfen Einstellungen, Logs und Desktop-Skripte
fehlen. Programm- und Projektkomponenten dürfen nur dann leer sein, wenn keine
entsprechenden Wurzeln konfiguriert sind. `KnowledgeRoot` und jede tatsächlich
konfigurierte Projektquelle bleiben Pflicht; bei optional fehlenden Quellen bleiben
bestehende Spiegelziele erhalten.
Die Programmquelle betritt regenerierbare Build-, Werkzeug- und Testordner nicht.
Das gilt insbesondere für `.tmp`; dadurch dürfen unlesbare Reste isolierter
Audit-/pytest-Läufe die Vollsicherung nicht blockieren. Der Ausschluss gilt nicht
für Projekt- oder Wissensquellen.
Die konfigurierte Projektwurzel und das aktuelle Projekt sind Pflichtquellen.
Historische externe Projekte aus `RecentProjectPaths` werden als optionale Quellen
geführt: vorhandene Ordner werden gesichert; ein wirklich fehlender Ordner erzeugt
eine sichtbare Warnung und sein bisheriger Spiegelstand bleibt erhalten. Ein
vorhandener, aber unlesbarer Ordner bleibt ein harter Fehler.

`KnowledgeRealtimeMirrorService` in Infrastructure wird durch `App` nach dem
vollständigen `ServiceProvider`-Aufbau gestartet. Er gleicht den gesamten aktiven
`KnowledgeRoot` zuerst inkrementell mit `<Datenträger Elements>\Brain` ab und
verarbeitet danach Dateiänderungen in einem Ein-Sekunden-Takt. Der Laufwerksbuchstabe
wird über die Datenträgerbezeichnung aufgelöst. Normale Dateien werden geprüft und
atomar ersetzt; SQLite-Dateien werden als verifizierte Online-Schnappschüsse geschrieben,
ohne Live-WAL/SHM-Dateien zu übernehmen. Ein eigener Zielmarker, Pfadgrenzen und
Verknüpfungsschutz sichern jede Löschung ab. Bei fehlender Platte bleibt die Quelle
unverändert; nach dem Wiederanschliessen folgt automatisch ein Vollabgleich. Diesen
Dienst nicht mit der manuellen Vollsicherung oder dem alten Desktop-Abschluss-Skript
zusammenlegen.

Die getrennte Programm-Momentaufnahme läuft über `IProgramSnapshotService` und
`ProgramSnapshotService`. Sie liest den Programmordner, folgt keinen Reparse Points
und veröffentlicht die ZIP erst nach vollständigem Schreiben atomar.
`ProgramSnapshotFileCatalog` behält Quellcode, vollständigen Git-Verlauf und
Modellgewichte, lässt aber ableitbare Build-Ausgabe, Python-Umgebung, Kartenkacheln,
Arbeitskopien und `.playwright-cli` weg. `_manifest.json` dokumentiert Dateizahl,
übersprungene Verknüpfungen und den lesbaren Git-Commit. Die UI-Orchestrierung liegt
in `SettingsProgramSnapshotWorkflow`; `SettingsPageViewModel` wählt nur Ziel, zeigt
Fortschritt und meldet das Ergebnis. Der Dienst wird einmal im `ServiceProvider`
erzeugt und unter `IProgramSnapshotService` registriert.

Die gemeinsame Schachtprotokoll-Suche liegt hinter `ISchachtProtocolFileLocator`
und `SchachtProtocolFileLocator`. Sie bevorzugt den gespeicherten `PDF_Path`, sucht
danach nur im passenden Schachtordner und meldet fehlende oder mehrdeutige Treffer.
Import, Stammdatennachlauf und Neueinlesen verwenden dieselbe Instanz aus dem
`ServiceProvider`. `SchachtProtocolFileCompatibility` bleibt nur die kleine Fassade
für bestehende UI-Aufrufer; Suchlogik nicht wieder in ViewModels kopieren.

`ProtocolTrainingFileStore` speichert den zweiten produktiven Prompt-Kontext unter
`<KnowledgeRoot>\protocol_training.json`. Keine Rueckkehr zum alten
LocalAppData-Pfad: Nur so werden aktives Gehirn, Sicherung und Elements-Spiegel
gemeinsam umgeschaltet.

Die aktuellen Fabriken verwenden `AiRuntimeSettings`, nicht den entfernten Typ
`AiRuntimeConfig`:

```csharp
IVideoAnalysisPipelineService CreateVideoAnalysisPipeline(
    AiRuntimeSettings cfg,
    IAiSuggestionPlausibilityService plausibility,
    HttpClient http)

IAiSanierungOptimizationService CreateSanierungOptimization(
    AiRuntimeSettings cfg,
    HttpClient? http = null)
```

## HaltungRecord und Importe

Felder über `FieldKeys` ansprechen, nicht mit neuen String-Literalen:

```csharp
var name = record.GetFieldValue(FieldKeys.HoldingName);
record.SetFieldValue(FieldKeys.HoldingName, value, source, userEdited);
```

`HaltungRecord` enthält `Fields`, `FieldMeta`, `VsaFindings`, `ProtocolEntry` und
`Protocol`. Es besitzt aktuell keine `DeepClone()`-Methode.

Aktuelle Import-Schnittstellen:

```csharp
Result<ImportStats> ImportPdf(
    string pdfPath, Project project, string? pdfToTextPath,
    bool fillMissingOnly = false, ImportRunContext? ctx = null)

Result<ImportStats> ImportXtfFiles(
    IEnumerable<string> xtfPaths, Project project, ImportRunContext? ctx = null)

Result<ImportStats> ImportWinCanExport(
    string exportRoot, Project project, ImportRunContext? ctx = null)

Result<ImportStats> ImportIbakExport(
    string exportRoot, Project project, ImportRunContext? ctx = null)

Result<ImportStats> ImportKinsExport(
    string exportRoot, Project project, ImportRunContext? ctx = null)
```

`ImportStats` enthält `Found`, `Created`, `Updated`, `Errors`, `Uncertain` und
`Messages`.

Die sechs manuellen Wege PDF, XTF, WinCan, IBAK, KINS und SchachtPro laufen über
`ImportManualWorkflowController`; das ViewModel verbindet nur Befehle und UI-Zustand.
Die gemeinsame Sperre des `ImportPageViewModel` umfasst diese Wege, den
Schacht-PDF-Ordnerimport, den Ein-Knopf-Import sowie Portabilität, Fotozuordnung
und Protokoll-Neugenerierung. Auch direkte parallele Befehlsaufrufe werden
abgewiesen; ein Fehler gibt die Sperre im `finally` frei. Der Zustand wird je Shell
über alle sichtbaren und neu erzeugten Importseiten geteilt. Ein registrierter
Shell-Guard sperrt währenddessen Navigation, Schliessen, Neu/Oeffnen/Projektwechsel
sowie manuelles Speichern und „Speichern unter". Nur ein an den registrierten,
aktiven Import-Guard gebundener interner Delegate darf die Abschlussspeicherung
des laufenden Imports ausführen.
Import-, Export-/Verteil- und Schacht-PDF-Guards reservieren zusaetzlich genau einen
atomaren Projektvorgang in der Shell. Auch nicht sichtbare oder neu erzeugte Seiten
duerfen sich deshalb nicht gegenseitig ueberholen. Ein interner Save braucht
Registrierung, Referenzidentitaet zum zentralen Besitzer und dessen lokale Freigabe.
`ImportRunWorkflowController` bindet Projektinstanz, normalisierten Projektpfad und
Berichtsordner und prüft Identität sowie Abbruch nach jedem längeren Abschnitt.
Der Echtlauf legt eine `IImportFileStagingSession` in `ImportRunContext.FileStaging` ab.
`StoredImportFileService` und `IImportMediaDistributionService` bereiten damit Dateien
neben der Projektdatei unter `.import-staging/<Lauf-GUID>` vor. Vor dem ersten
Datei-Move schreibt `FileImportTransactionJournal` die
`.import-transaction.json` mit allen vorbereiteten Rollback-Zielen samt SHA-256.
Lesen, eigentumsgebundenes Schreiben und Löschen laufen je Projekt unter einer
prozessübergreifenden Sperre. Nur ein fehlender oder derselben TxId gehörender
Marker darf geschrieben werden; ein fremder oder unlesbarer Marker bleibt
unverändert. Cleanup und Recovery löschen nur mit der erwarteten TxId;
`ImportFileStagingPathGuard` begrenzt die Pfade und `VerifiedImportFileCopy`
verifiziert Kopien. Nach `Publish` folgt der Ist-Stand. `Accept` erfolgt erst nach
`ReplaceProject`; vorher entfernt `Dispose` nur vom Lauf neu veröffentlichte,
unveränderte Dateien. Vorhandene oder wiederverwendete Ziele nie löschen. Gleichheit
wird am Inhalt geprüft, nicht nur an der Dateigröße.
Projektroot und Markerpfad werden selbst als untrusted behandelt: Staging, Publish,
Markerlesen, -schreiben und -loeschen weisen Junctions und Symlinks dort fail-closed ab.
Speicher- und Nachlaufhinweise bleiben sichtbar. Den PDF-Stapellauf nicht mit dem
fehlertoleranten PDF-Scan des `ImportPostProcessingController` zusammenlegen. Eine
XTF-Vorschau darf das Rohdatenarchiv weder beschreiben noch migrieren.
`ImportTransactionRecoveryService` vergleicht beim Projektladen Marker-TxId und
`Project.LastCommittedImportTxId`. Bei gleichem Commit-Beweis wird nur aufgeräumt,
sonst werden ausschließlich unveränderte, SHA-geprüfte Markerziele zurückgenommen.
Der schreibfreie Preflight prüft zusätzlich Schreibschutz, exklusiven Lesezugriff,
Datei-Verknüpfungen und rekursiv den Staging-Baum, ohne Verknüpfungen zu betreten.
Unlesbare Marker, unsichere Pfade, Hashabweichungen oder Aufräumreste ergeben
`Blocked`; der Marker bleibt erhalten. Ein Preflight-Hindernis verändert nichts.
Kann ein danach gestartetes rekursives Staging-Aufräumen teilweise gescheitert
sein, meldet `ProjectFolderModified` konservativ eine mögliche Änderung. Beim
asynchronen Öffnen läuft die dateiintensive Recovery im Hintergrund. Wurde zuvor
eine kaputte Projektdatei quarantänisiert, stellt `ProjectRecoveryService` die
geprüfte Sicherung bei anschließendem Import-Block atomar und ohne Überschreiben am
Originalpfad für den nächsten Öffnungsversuch bereit. Die Shell kombiniert nur die
strukturierten Ergebnisse und formuliert aus dem endgültigen Änderungsflag die
Zusage „verändert" oder „nicht verändert".
Restore-Point-Erstellung und -Ausduennen sowie Sicherungssuche, Quarantaene und
Materialisierung pruefen Root und Ziele mit derselben Reparse-Grenze; rekursive
Suchen betreten keine Verknuepfungen.
Bei einem fehlgeschlagenen Save bleibt der Marker ebenfalls bis zur eindeutigen
Klärung erhalten.
`ImportOneClickProjectController` bindet Projektinstanz und Projektpfad und prüft
beide vor der Übernahme erneut. Ergebnisdateien werden inhaltlich signiert. Manueller
und Ein-Knopf-Import verwenden gemeinsam `ImportFileTransaction`; keinen zweiten
Markertyp einführen. Im Ein-Knopf-Weg laufen Archiv, Plan-PDF, Medien, namensbasierte
Protokolle, Kanal und Dichtheit über dieselbe Staging-Sitzung. Vorbereitete Dateien
werden über `ResolveReadPath`/`EnumerateReadableFiles` gelesen; neu erzeugte PDF-Seiten
über `StagedDistributionOutput` und `StageGeneratedFile` aufgenommen. Das alte
`IImportedFileLedger` darf nur vor `Publish` zurücknehmen und wird beim Beginn der
Veröffentlichung abgeschaltet.

Die manuelle Schachtverteilung liegt hinter `IShaftDistributionService` und in der
UI-Partialdatei `ExportPageViewModel.ShaftDistribution.cs`. Ziele innerhalb des
Projektroots verwenden dieselbe Transaktion. Externe konfigurierte Ziele bleiben
direkte Exporte, weil der Projektmarker ausserhalb seines Roots nichts löschen darf.

Alle manuellen Haltungs-, Dichtheits- und Schachtverteilungen halten waehrend des
Laufs einen registrierten Shell-Operations-Guard. Er sperrt Navigation,
Projektwechsel, Schliessen und die oeffentlichen Save-/SaveAs-Wege. Der Lauf bindet
Projektinstanz und Projektpfad vor dem Hintergrundteil; nur der an genau diesen Guard
gebundene interne Save darf den Abschluss speichern. `ExportPageViewModel` meldet den
Guard bei `Dispose` ab.

`Application.Common.SafeFileEnumeration` ist die gemeinsame rekursive Lesergrenze.
Der ausdruecklich gewaehlte Quellroot bleibt lesbar; untergeordnete Verzeichnis- und
Datei-ReparsePoints werden nicht betreten oder geliefert. Die normalisierte
Visited-Menge schuetzt zusaetzlich vor doppelten Pfaden und Zyklen. Kanal-/WinCan-
Erkennung, KIAS, Import-Staging, Protokollsuche und Portabilitaet duerfen keine rohe
`AllDirectories`-Aufzaehlung daneben verwenden. KIAS behandelt `Data`, `Film` und
`Report` als untrusted Kinder und lehnt dort auch direkte Datei-Symlinks ab.
Einzelne fremde Medienquellen laufen vor dem ersten Existenz-, Zeitstempel- oder
Kopierzugriff ueber `ImportSourcePathGuard`. Er prueft jede vorhandene Pfadkomponente,
weist UNC-/Netzlaufwerke und Datei-/Verzeichnis-Verknuepfungen ab und wird gemeinsam
von XTF-Medienresolver, Medienverteilung, Haltungs-Videozuordnung, Kanal-
Verteilfallback und Projektportabilitaet verwendet. Einen lokal aussehenden Alias als
Umgehung der UNC-Sperre nicht wieder zulassen.
Auch `LegacyXtfImportService.VsaKek` erkennt Video-Bezeichnungen über
`MediaFileTypes.HasVideoExtension`; dort keine eigene, kürzere Endungsliste wieder
einführen. Die fachliche `Film`-Pfaderkennung bleibt nur der zusätzliche Altformat-
Fallback.

Direkte Projekt-Schreibziele laufen ueber `ProjectWritePathGuard`; er prueft auch den
Projektroot selbst. Projektstruktur, gespeicherte Importkopien, Rohdatenarchiv,
Plan-PDF, Medien, namensbasierte Protokolle, Protokoll-Neuerzeugung,
Dichtheits-KI-Fallback, Fotozuordnung, Portabilitaet, Kanal-Fallback und
Schachtprotokollimport duerfen ihre Ziele nicht wieder nur per Zeichenkettenvergleich
pruefen. Das gilt ebenso fuer Restore/Recovery und produktive Importberichte unter
`__IMPORT_REPORTS`. Wunschziel, freier Kollisionspfad und atomare Temp-Datei werden vor der
Mutation erneut geprueft; Staging bleibt unter seiner eigenen gleichwertigen Grenze.
Direkte Haltungs-/Dichtheits-/Schachtverteilungen laufen ueber
`DistributionWritePathGuard`. Er bindet an den bewusst gewaehlten Zielroot und sperrt
auch den Root selbst, wenn er eine Junction oder ein Symlink ist.

Dateiidentitaet wird bei Foto, Originalfoto, Video, Archiv-/Plan-PDF,
Medienkonfliktcenter, Dichtheitsprotokoll und Schachtprotokoll am vollstaendigen Inhalt
geprueft, nie nur an Name, Groesse oder Teilproben. Das Medienkonfliktcenter prueft
Haltungsroot, Zielordner und Info-Datei vor Kopieren oder Loeschen gegen
Verknuepfungen. Ohne passenden Haltungsdatensatz bleiben Marker und Dateibaum
unveraendert. Abweichender Inhalt braucht einen freien Namen oder einen sichtbaren
Konflikt. Haltungszuordnungen verwenden Zeichen-/Segmentgrenzen; ein fachlicher
Praefix-Fallback in IBAK, KINS und WinCan ist nur bei genau einem Kandidaten erlaubt.
`ProjectPortabilityService` verarbeitet auch `OriginalFotoPaths` und relativiert nur
unter der separatorbewusst nachgewiesenen Projektgrenze. `HoldingFolderDistributor`
behaelt Links ohne echten Projektroot absolut; erst der zentrale
`ProjectVideoReferenceNormalizer` relativiert beim Speichern nachgewiesen interne
Videos.

Ein KINS-Header ohne Beobachtungen darf ein vorhandenes Protokoll nicht leeren.
Schacht-PDF-Stammdaten und -Protokolle respektieren `fillMissingOnly`,
benutzerbearbeitete Felder und Revisionshistorie. `SchachtProtocolImportService`
verwendet ein gleichnamiges Ziel nur bei Inhaltsgleichheit und waehlt sonst einen
freien Namen. XTF-Medienpfade weisen Elternsegmente auch im Dateinamen ab;
`FindingEntryMatcher` liefert ohne Code- oder Meterbezug keinen beliebigen Eintrag.

Bei mehreren WinCan-Untersuchungen darf der technische Datensatz-Zeitstempel nur als
Sortierhilfe dienen. `2007-12-31` und Daten vor 1990 sind unglaubwuerdige Platzhalter:
Sie werden weder nach `Datum_Jahr` geschrieben noch im Bericht als Aufnahmedatum gezeigt.
Der Bericht bezeichnet den Platzhalter ausdruecklich und nennt uebersprungene
Untersuchungen, damit fehlende Befunde und Medien sichtbar bleiben.

`FachzahlParser` ist die gemeinsame kulturunabhängige Quelle in den geldrelevanten
Kostenrechner-, Matrix- und Exportpfaden. Punkt/Komma sowie korrekt gruppierte
Schweizer Apostroph-/Leerzeichenwerte müssen unter de-DE, de-CH und en-US gleich
entscheiden; Mehrdeutiges ablehnen.
`CostCatalogStore`, `MeasureTemplateStore` und `PositionTemplateStore` melden
beschädigte/unlesbare Default- oder Override-Dateien. Kostenrechner, Haltungs-/
Schachtmatrix, Builder und Katalog-/Vorlageneditoren müssen in diesem Zustand
Speichern, Neuberechnen und Geld-Exporte sperren. NPK-Codes in CSV/Excel immer als
Text behandeln, damit `612.110` nicht gekürzt wird.

Die professionellen Excel-Berichte verwenden die datenfreien Vorlagen
`Export_Vorlage/Haltungen.xlsx` und `Export_Vorlage/Schächte.xlsx`; deren
reproduzierbare Quelle liegt unter `tools/ExcelVorlagenBauer`. Titel, Kopf und Daten
beginnen in Zeile 25, 26 und 27; Formeln und bedingte Formatierung reichen bis 5000,
daher sind maximal 4'974 Datensätze zulässig. `ExcelTemplateExportService` lässt
Leerwerte als echte Leerzellen, behandelt Kennungen als Text und Mengen/Kosten als
Zahlen. Ungültige Zahlen blockieren vor Veröffentlichung. Relative Medienpfade werden
mit dem gebundenen Projektdateipfad gegen den echten Projektroot aufgelöst; Schacht-
Links verwenden `Link`, `PDF_Path`, `PDF_Eigen`, dann `PDF_All`. Der Dienst schreibt
in eine Temp-Datei, prüft die XLSX erneut und benennt erst danach atomar um. Der
additive `IExcelExportService`-Vertrag mit `CancellationToken` prüft Abbruch beim
Laden, je Datensatz und vor jeder Veröffentlichungsgrenze. ClosedXML selbst schreibt
synchron; ein währenddessen angeforderter Abbruch wird am nächsten sicheren Punkt
wirksam, entfernt die Temp-Datei und lässt ein vorhandenes Ausgabeziel unverändert.
Der Haltungs-Export synchronisiert abgeleitete Kosten nur auf einer
`HoldingExcelExportSnapshotFactory`-Kopie und verändert das Live-Projekt nicht.
`CostStoreFileProbe` prüft Datei, Verzeichnis und Verknüpfung fehlersicher.
`ProjectCostStoreRepository` blockiert direkte Saves, wenn `costs.json`,
`schacht_costs.json` oder `schacht_empfehlungen.json` bereits vorhanden, aber nicht
sicher lesbar ist. Nichtpositive oder ungültige Haltungslängen und Schachtmengen
dürfen keine Kostenberechnung und keinen Save auslösen. Ausgewählte
Kostenrechner-Zeilen mit negativer Menge oder negativem Preis dürfen weder summiert
noch gespeichert, übernommen oder exportiert werden.
Normalisierte doppelte Kostenkeys, Massnahmen-IDs und Positionsgruppennamen sowie
Nullstrukturen oder negative Mengen sind ungültig und dürfen nie still den letzten
Eintrag gewinnen lassen. Ein direktes `SaveUserOverrides` muss die vorhandene
Override-Datei selbst erneut prüfen, auch wenn vorher kein `Load` aufgerufen wurde.
Editoren dürfen ungültige Zeilen nicht herausfiltern und ungültige oder negative
Mengen nicht still zu `1` umdeuten.

`StoredImportFilePathResolver` liest Listen über `StoredImportFileRegistry.Load`, prüft
zuerst den echten Projekt-Root und danach alte `Projektdateien\Imports`-Ablagen. Absolute
bestehende Pfade bleiben erlaubt; fehlende, unsichere und doppelte Treffer werden
verworfen. `VsaPageViewModel` und `InspectionProtocolFileLocator` dürfen diese Logik
nicht duplizieren. `ImportFileStoreService` bleibt eine öffentliche, dateifreie
Kompatibilitätsfassade; den Leser-Rückfall für alte Bestandsdaten nicht entfernen.
Der direkte, nicht gestagte Schreibweg von `StoredImportFileService` muss Projektroot,
`Imports\<Art>`, Wunschziel und Kollisionsziel über `ProjectWritePathGuard` prüfen.

`PdfPrimaryDamageFindingBuilder` liest das vom PDF-Parser erzeugte Feld
`Primaere_Schaeden` und erzeugt daraus strukturierte `VsaFinding`-Eintraege.
A-/B-Streckenmarker werden nur bei gleicher Nummer und gleichem VSA-Code verbunden.
`PdfPrimaryDamageStructureSynchronizer` legt daraus bei fehlenden Strukturdaten das
Protokoll an. Vorhandene Findings oder bereits befuellte manuelle Protokolle werden
nicht ersetzt. Der Aufruf liegt direkt nach dem Feld-Merge im
`LegacyPdfImportService`, damit auch ein erneuter Import bestehende Text-only-
Haltungen nachziehen kann.

Der revidierte XTF-Export läuft über `IXtfRevisionExportService` und
`XtfRevisionExportService`. Standardquellen sind unveränderte Projektkopien unter
`Imports\XTF` beziehungsweise `Importdateien\XTF`. Fehlen sie, darf
`ExportPageViewModel` pro Lauf externe `.xtf`-Dateien wählen; sie werden geprüft,
dedupliziert, für Prüfung und Schreiben identisch verwendet und ausschliesslich gelesen.
Gleichnamige Projektkopien werden nur bei belegtem gleichem SHA-256 dedupliziert;
unterschiedlicher Inhalt stoppt mit beiden Pfaden, statt eine Quelle still zu bevorzugen.
Eine Vorschau mit offenen Entscheidungen liefert `Ok=false`; die UI darf danach nicht
nach einer Schreibbestaetigung fragen.
Jeder Lauf schreibt in einen neuen Zeitstempelordner. `VsaFinding` trägt additiv
Kanalschaden- und Untersuchungs-TID, `HaltungRecord` die importierte `XtfHerkunft`.
SIA405-TIDs von Haltung und Normschacht landen als `Objekt_ID` im Datensatz; der
`SchachtRecord` besitzt dafür keine eigene Herkunftsklasse. Altprojekte werden nie neu
importiert: `XtfKanalschadenElementReader` und `XtfFindingMatcher` bilden nur beidseitig
eindeutige Zuordnungen im Arbeitsspeicher. `XtfRevisionPlanBuilder` plant geänderte,
neue und entfernte Befunde. `XtfStammdatenPlanBuilder` und `XtfSchachtPlanBuilder`
nehmen nur eindeutig zugeordnete, menschlich bearbeitete SIA405-Felder auf; die zweite
Haltungsdimension geht als `Rohrprofil.HoehenBreitenverhaeltnis` hinaus. Ein Wechsel
auf rund entfernt ein vorhandenes Verhältnis nur über die ausdrückliche Aktion
`XtfRevisionFeldAktion.Entfernen`. Eine leere Breite darf nur bei handbearbeitetem
Breitenfeld als bewusste Rund-Angabe löschen; eine nur geänderte Höhe oder ein geerbter
Leerwert löscht nichts. Profiltyp Kreis mit zwei verschiedenen Massen bleibt ein offener
Fall; Abmessung und Profil bleiben dann gemeinsam draussen, unabhaengige Haltungsfelder
duerfen weiter geplant werden. Offene Fälle sperren bereits Prüfung und Schreibweg. `XtfRevisionWriter` wendet nur den Plan an, verändert das
Original nie, überschreibt kein Ziel und veröffentlicht über eine Nebendatei.

Die feste Uri-Auswahl für Haltungsprofile liegt in `ProfiltypVokabular`:
`Unbekannt`, `Kreisprofil`, `Eiprofil`, `Maulprofil`, `Offenes Profil`,
`Rechteckprofil`, `Spezialprofil`. `Offenes Profil` wird als `offenes_Profil`
geschrieben; alte Werte `Anderes`/`andere` werden auf `Spezialprofil` angehoben.
Die Schachtmaske führt über `SchachtformVokabular` `Unbekannt`, `Rund`, `Oval`,
`Quadratisch`, `Rechteckig`, `Vieleckig`. Diese Form bleibt im Projekt, weil der
SIA405-`Normschacht` nur `Dimension1` und `Dimension2` kennt. Beide Innenmasse müssen
auch mit alten Excel-Vorlagen als eigene Spalten editierbar bleiben.

`IXtfNeuExportService`/`XtfNeuExportService` erzeugt eine neue SIA405-XTF nur für
Objekte ohne Katastervorlage. Haltung oder Schacht mit `Objekt_ID` bleiben draussen.
Bei Altprojekten bleibt auch ein Schacht ohne eigene ID draussen, wenn er Endpunkt einer
Haltung mit Kataster-ID ist. Eine neue Haltung an diesem ausgeschlossenen Schacht bleibt
ohne belegte Abwasserknoten-TID ebenfalls vollständig draussen; vor der Sperre entstehen
keine Organisationen, Profile oder Haltungspunkte. Ein nur zu dieser gesperrten Haltung
gehoerender Neuschacht und seine alleinige Organisation werden ebenfalls entfernt;
eigenstaendige oder wirklich verwendete Neuschaechte bleiben. Datenherr und Datenlieferant kommen aus ihren eigenen
Projektfeldern; nur leere Felder fallen auf den Eigentümer zurück, unbekannte gesetzte
Organisationen sperren das Objekt mit Bericht. `LegacyXtfImportService` löst
Eigentümer-, Datenherr- und Datenlieferant-Verweise getrennt auf und bewahrt die TIDs.
Die Zeichenfolge `unbekannt` bleibt in Bemerkungen und Organisationsnamen erhalten;
nur semantische Platzhalterfelder wie Funktion, Material oder Zustand behandeln sie als leer.
Der Rundreisetest muss neue XTF schreiben, wieder importieren und Felder, Masse sowie
Katasterkennungen vergleichen.

## KI-Pipeline: aktueller Stand

1. C# startet über `VideoAnalysisPipelineService`, `SingleFrameMultiModelService` oder
   `VideoFullAnalysisService`.
2. `VisionPipelineClient` ruft den Sidecar mit `X-Sidecar-Token` auf. Die gemeinsame
   `SidecarEndpointPolicy` erlaubt diesen Header ausschliesslich fuer Loopback-Ziele;
   Hauptpfad, KI-Start und kontrollierter Neustart verwenden dieselbe Regel. Bei
   LAN-/Remote-URLs wird kein Token-Header aufgebaut.
3. Der Multi-Model-Pfad verbindet YOLO, DINO, SAM, Quantifizierung und optional Qwen.
4. C# mappt VSA-Codes, führt zeitliche Zusammenführung aus und wendet das QualityGate an.

Der aktive YOLO-Detektor darf nur bei ausdruecklichem
`detector_qualification.qualified=true` als Filter oder Confidence-Beweis dienen.
False, ein fehlendes Feld und ein Health-Lesefehler sind fail-closed: Batch und
Player-Einzelframe umgehen YOLO, lassen DINO/SAM weiterlaufen und markieren Health
sowie Ergebnis `Degraded`/review-pflichtig. Das Training Studio sperrt in denselben
Faellen nur den Standardmodell-Fototest. Der getrennte BCC-Test bleibt nutzbar.

Wichtige Klassen:

- `VideoAnalysisPipelineService`: Wahl zwischen Multi-Model- und Ollama-Pfad
- `MultiModelAnalysisService`: gemeinsame Bildanalyse
- `VideoFullAnalysisService`: Vollanalyse und Rückfallpfad
- `SingleFrameMultiModelService`: Live-Einzelframe
- `VisionPipelineClient`: HTTP-Verbindung zum Sidecar
- `OllamaVisionFindingsService`: einfacher Bild-/OSD-Pfad ueber Ollama `/api/chat`;
  Antwort nur mit strengem JSON-Schema (`meter`, `findings`, `severity`), festen
  Modelloptionen und fail-closed bei unvollstaendigen strukturierten Antworten
- `OsdMeterDetectionService`: liest den OSD-Meterstand ueber diesen strukturierten
  Bildpfad und faellt danach auf die lineare Schaetzung zurueck
- `QualityGateService`: Green/Yellow/Red-Entscheidung
- `TemporalFindingDeduplicator`: framebasierte Befundzusammenführung
- `TemporalCodeVotingService`: zeitliche Code-Abstimmung

Es gibt aktuell kein ByteTrack, OC-SORT oder echtes Multi-Object-Tracking.

### Modellstart korrekt behandeln

- Der KI-Start läuft nur, wenn `AiStartOnProgramStart` aktiv ist; Standard ist `false`.
- Der Sidecar lädt in `main.py` beim eigenen Prozessstart keine Modelle. Er richtet Auth ein
  und entlädt Modelle beim Beenden.
- Der Start-Orchestrator lädt die konfigurierten Ollama-Modelle vor. Gleiche Modellnamen
  werden nur einmal geladen.
- Der Start-Orchestrator löst den Sidecar-Token für jeden Health-Versuch neu auf.
  Das ist nötig, weil ein Sidecar beim ersten Start die Token-Datei erst anlegt.
- Danach ruft er `/warmup` auf. Dieser Endpunkt versucht YOLO, YOLO-cls, DINO und SAM zu
  laden. Fehlende Modelle und ein nur eingeschränkt geladener DINO werden als Fehler
  gemeldet; vollständige Belegung ist trotzdem nicht garantiert.
- Ohne automatischen KI-Start oder erfolgreichen Warmup laden Sidecar-Modelle beim ersten
  passenden Aufruf.
- DINO kann nach Warmup resident bleiben, ist aber nicht garantiert permanent. Explizites
  Entladen, LRU-Entladen und Shutdown sind möglich.

### Modelle und VRAM

- YOLO: bevorzugt eigenes `yolo26m`-Gewicht beziehungsweise TensorRT-Engine;
  erlaubter COCO-Rückfall ist `yolo11m.pt`.
- Grounding DINO: bevorzugt `grounding_dino_swinb`, Rückfall
  `grounding_dino_1.5` (Swin-T OGC).
- SAM: SAM 2.1 mit `sam2.1_hiera_large.pt` und `SAM2ImagePredictor`.
- Qwen: GPU-Automatik wählt ab 24.000 MB VRAM `qwen3-vl:8b-q8`, sonst
  `qwen3-vl:2b`. Konfigurierte Modelle haben Vorrang.
- Ohne eigene Text-/Embedding-Einstellung gelten `qwen3-vl:2b` und
  `nomic-embed-text`.
- Es gibt keine automatische Laufzeit-Eskalation von 8B auf 32B und keinen
  `ReferenceVisionModel` im aktuellen Laufzeitvertrag.
- Nie still auf Qwen 2.5 zurückfallen.
- Das Sidecar-Budget ist standardmäßig 29 GB. Es warnt bei Überschreitung, lehnt
  Anfragen aber nicht automatisch ab. Die Slots YOLO, DINO und SAM können gleichzeitig
  resident sein.
- `bend_geometry_enabled` ist standardmäßig `false`.

### Sidecar-Ausfallschutz

- Predict-Wege ziehen nach ihrem Modell-Lock eine besitzgebundene Busy-Lease. Nur
  deren Besitzer darf sie lösen; CPU- und GPU-Inferenzen werden vom Watchdog erfasst.
- Gleichzeitige CUDA-Modellladungen reservieren ihren geschätzten VRAM atomar unter
  einem kurzen globalen Lock. LRU-Auswahl und Registerentnahme sind ebenfalls atomar;
  Modellreferenzen, `empty_cache` und GC laufen danach ohne diesen Lock, damit
  Health und Watchdog auch bei blockierter CUDA-Bereinigung ansprechbar bleiben.
- `insufficient_vram` ist ein eigener 503-Vertrag mit Top-Level-`code`,
  `free_gb`, `required_gb` und `reserved_gb`. C# behandelt ihn als Kapazitätsfehler,
  nicht als Transportausfall: kein HTTP-Retry, kein Outage-Zähler, kein Restart.
- Ein kontrollierter Restart darf nur einen ausdrücklich als `Sidecar` getrackten
  Prozessbaum beenden. `Unknown`, Ollama, PID-/Startzeitwechsel sowie ein erwarteter,
  aber nicht lesbarer oder abweichender Programmpfad sperren fail-closed. Ohne
  Health-PID darf nur ein eigener lebender Sidecar nach verifiziertem Kill oder ein
  früher eigener, inzwischen beendeter Sidecar neu gestartet werden.

## Zusammenführung und KnowledgeBase

- Video-Befunde werden mit `TemporalFindingDeduplicator` und
  `TemporalCodeVotingService` verarbeitet.
- `TrainingSampleGenerator` vermeidet Duplikate über die kanonische `Signature`.
- `KnowledgeBaseManager` blockiert Eval-kontaminierte Samples, akzeptiert nur
  indexwürdige menschlich bestätigte Daten und schreibt per UPSERT nach `SampleId`.
- `RetrievalService` sucht über Kosinus-Ähnlichkeit und prüft das Embedding-Modell.
- Produktiv gibt es zwei Laufzeit-Kontextwege. Beide liefern Prompt-Beispiele und
  trainieren keine Modellgewichte.
- Ähnliche bestätigte Fälle kommen aus `KnowledgeBase.db` über `RetrievalService`.
- Freigegebene Protokolleinträge laufen getrennt über `ProtocolTrainingFileStore`
  und `<KnowledgeRoot>\protocol_training.json`.
- `TeacherAnnotationFileStore`, `ProtocolTrainingFileStore` und
  `AiOptimizationSessionFileStore` behandeln nur eine fehlende Datei als leeren
  Erstlauf. Eine vorhandene, aber unlesbare oder strukturell ungueltige JSON-Datei
  wirft und sperrt jeden nachfolgenden Speicherschritt; der Bestand bleibt
  unveraendert. Der Teacher-Store legt bei ungueltigem JSON zusaetzlich `.corrupt`
  als forensische Kopie an.
- `tools/SelfTrainingHarness` startet nur ohne laufendes `SewerStudio.exe`. Sein
  bytegenauer Store-Snapshot wird nur zurueckgespielt, wenn waehrend des Laufs keine
  App beobachtet wurde und der letzte Harness-Stand bis zur Wiederherstellung
  denselben SHA-256 behaelt. Bei Konflikten bleiben aktueller Store und eindeutige
  Harness-Sicherung unangetastet.
- Der frühere Bildweg mit `FewShotExampleStore`, Builder und `Zu FewShot`-Knopf
  ist entfernt, weil kein KI-Prompt ihn las. `fewshot_examples.json` und
  `fewshot_images` sind nur noch unveränderte Legacy-Daten im Sicherungskatalog;
  nie wieder als Prompt- oder Trainingsquelle anschliessen.
- Einen produktiven `KbDeduplicationService` gibt es aktuell nicht. Nicht nach altem
  Beispielcode instanziieren.

## TrainingDataInventory (AP 0.1)

Der Aufbau ist bewusst in Verträge, Fachregeln, Datei-I/O und Kommandozeile getrennt.

### Application

Ordner: `src/AuswertungPro.Next.Application/Ai/Training/Inventory/`

- `ITrainingDataInventoryService` und `TrainingDataInventoryRequest`
- `InspectRuntimeSnapshotAsync` liefert in einem einzigen Live-Scan Bericht,
  typisierte Teacher-/TrainingSample-Daten und den vollständigen Schutz-Snapshot;
  dieser Laufzeit-Snapshot wird nicht als zweite Inventardatei gespeichert
- versionierter Bericht mit Schema `2.2` und Scanner `ap0.1-v5`
- getrennte Zustände für Pfad, Schutz und Hash
- Eval-Schutz wird je gefundenem Eval-Set ausgewiesen; ein defektes Set kann kein
  vollständiges Set verdecken
- gruppierte Zusammenfassung für Daten, Haltungen, Triage, Pfade, Eval und Quellen
- Teacher-Regeln sind auf `TeacherInventoryPolicy`, `TeacherInventoryTriagePolicy`
  und `TeacherInventoryReasonPolicy` verteilt
- `TrainingInventorySummaryBuilder` baut die abgeleitete Zusammenfassung
- `TrainingInventoryReportValidator` prüft Pfad-, Triage-, Quellen- und
  Zusammenfassungsregeln vor Schreiben und nach Lesen; aktuelle Quellen sind fest
  an `<KnowledgeRoot>\teacher_annotations.json` und `training_samples.json` gebunden
- `TrainingDataInventoryJson`: strenger gemeinsamer JSON-Vertrag mit String-Enums,
  Pflichtfeldern und Ablehnung unbekannter Felder; abgeleitete Werte werden nicht
  als zweite Wahrheit gespeichert
- `TrainingInventoryExitPolicy`: Erfolg nur bei genau je einer aktuellen, typisiert
  gelesenen Teacher- und Training-Sample-Quelle und ohne Error-Issue

### Infrastructure

Ordner: `src/AuswertungPro.Next.Infrastructure/Ai/Training/Inventory/`

- `TrainingDataInventoryService`: rein lesende Orchestrierung
- `TrainingInventorySourceReader`: liest stabile Datei-Schnappschüsse ohne
  Store-Migrationen und protokolliert SHA-256, Größe und Änderungszeit
- aktuelle Teacher- und Training-Sample-Quellen werden typisiert geprüft
- Backups und Legacy-Dateien werden mindestens als JSON-Array geprüft
- relative gespeicherte Pfade werden gegen `KnowledgeRoot` aufgelöst
- `TrainingInventoryPathResolver` trennt Existenz, Schutz, Reparaturvorschlag und Hash
- `TrainingInventoryFileEnumerator` folgt keinen Links, Junctions oder anderen
  Reparse Points
- `TrainingInventoryEvalProtectionReader` verlangt pro Eval-Set `frozen=true`, prüft
  echte Bild-Hashes und vergleicht `_candidates.json` mit seinem Manifest-Hash;
  jedes Eval-Bild braucht genau einen Kandidaten mit passendem `frame_path`.
  UTF-8-Dateien mit oder ohne BOM werden unterstützt
- `TrainingInventoryIssueCollector` hält die Orchestrierung frei von Meldeformatierung
- `TrainingInventoryReportOutputPolicy` prüft das Ziel vor dem Scan und nochmals vor
  dem Schreiben; Bericht, Prüfsumme und Sicherungen dürfen keine Quelle oder
  Verknüpfung treffen. Der Eval-Root bleibt auch bei eigenen `--protected-root`-Werten
  immer geschützt

Das Inventar verändert keine Annotationen, Bilder oder gespeicherten Pfade. Eindeutige
Dateinamen-Treffer bleiben manuelle Vorschläge. Eval-Schutz arbeitet fehlersicher:
unvollständiger Schutz gibt keine Train/Val-Freigabe.

### CLI

Ordner: `tools/TrainingDataInventory/`

```powershell
dotnet run --project tools\TrainingDataInventory -c Release --no-build --
```

Standardwurzel ist `C:\KI_BRAIN`. Bericht und SHA-256-Prüfsumme landen unter
`<KnowledgeRoot>\training\reports\`. Optionen, Ablauf, Ausgabe und Berichtsschreiben liegen
in getrennten Klassen. Der Writer hasht exakt die geschriebenen UTF-8-Bytes.
`Strg+C` bricht kontrolliert ab.

Fokussierte Tests liegen unter
`tests/AuswertungPro.Next.Infrastructure.Tests/Ai/Training/Inventory/`.

## YOLO-Detect-Klassenkarte v2 (AP 0.2, Freigabe noch offen)

Die Teacher-Karte und die Trainingskarte sind absichtlich getrennt:

- `VsaYoloClassMapFileStore` liest Legacy-Flat und das versionierte Format
  `{version,vsa_manifest_hash,classes}`. Vorhandenes kaputtes JSON, doppelte,
  negative oder lückenhafte IDs sind harte Fehler; es gibt keinen stillen
  Default-Rückfall mehr.
- `IVsaYoloClassMapStore.GetClassId` ist strikt und schreibt nie.
  Nur der Live-Teacher ruft den ausdrücklichen `GetOrAddClassId` auf. Die
  Konstruktoroption kann diese Erweiterung sperren.
- `VsaYoloClassMapDocumentWriter` behandelt die JSON-Karte als verbindlich und
  `classes.txt` als abgeleitete Datei. Scheitert das Schreiben der JSON-Karte,
  wird die vorherige `classes.txt` wiederhergestellt beziehungsweise eine neu
  angelegte Kopie entfernt.
- `TrainingYoloClassMapFileStore` liefert über `ITrainingYoloClassMapStore` einen
  unveränderlichen Snapshot. Er prüft Version 2, exakt 15 feste Klassen/IDs,
  den SHA-256 des tatsächlich verwendeten VSA-Manifests und die Migration.
  Zusätzlich prüft er alle vier Quell-Hashfelder auf SHA-256-Format, die feste
  Quellenreihenfolge und `entry_counts`; gekürzte oder falsch zusammengesetzte
  Tabellen brechen hart ab. Nur der VSA-Hash wird beim Lesen gegen die echte Datei
  neu berechnet. Die drei übrigen Hashes sind Auditwerte der Erzeugung und kein
  Laufzeitnachweis gegen die veränderlichen Quelldateien.
- Der lokale Export löst alle nicht durch Eval-Schutz ausgeschlossenen Codes vor
  der ersten Ordner-, Bild- oder Labelausgabe auf. Offene oder unbekannte Klassen
  stoppen den Lauf; explizit freigegebene `discard`-Zeilen werden ausgelassen.
- `BBD_boden` ist eine Detect-Klasse. Der produktive Befundweg läuft über
  `CodingFindingCodeResolver` und `VsaCodeResolver` zu
  `YoloClassVsaMapper.ToPersistableVsaCode`; er gibt dafür `BBDZ` zurück, nie den
  ungültigen nackten Wert `BBD`. Ein Integrationstest schützt diese Kette.
- `BCC_bogen` ist die getrennte Pilotklasse mit fester ID 14. Das Rückmapping
  liefert den gültigen Hauptcode `BCC`.

Versionierte Konfiguration:

```text
training/class_maps/detect_class_map_v2.json
training/class_maps/detect_class_migration_v2.candidate.json
training/class_maps/detect_class_migration_v2_review.md
```

Die UI-Projektdatei kopiert diese Dateien nach `Data/Training/`. Die Kandidatentabelle
enthält 124 Zeilen: 74 beobachtete Teacher-Codes, 35 alte Map-Schlüssel, 10 produktive
englische Modellnamen und 5 Einzelprüfungen. Nur die 10 BCC-Zeilen sind für den
persönlich bestätigten Bogen-Pilot freigegeben; 114 Zeilen bleiben `pending`.

## Plan-gesteuerter YOLO-Export (AP 0.3, technisch umgesetzt)

Der aktuelle Anschluss lautet:

```text
TrainingCenterViewModel
  -> dünner TrainingYoloExportWorkflow
  -> TrainingYoloExportRuntime.CreateHybrid
tools/StageAExporter
  -> TrainingYoloExportRuntime.CreateLocal
beide -> ITrainingYoloExportCoordinator mit fest gebundenen Roots
  -> freigegebenes export_registry_v1.json
  -> TrainingDataInventoryRuntimeSnapshot aus genau einem Live-Scan
  -> strikt gelesene class_map v3 mit 15 festen Klassen/IDs
  -> ITrainingExportPlanService erzeugt genau einen unveränderlichen Plan
  -> ITrainingExportExecutionService nutzt Sidecar ODER lokalen Ausführer
  -> ITrainingExportCompletionService markiert nur bestätigte TrainingSamples
```

Verbindliche Details:

- `ManualGoldTrainingPolicy` und `TrainingExportPlanInputBuilder` lassen fuer neues
  Training nur persoenlich bestaetigte `ManualCoding`- oder streng belegte
  `PdfPhoto`-Eintraege mit vorhandener Bilddatei, randgueltiger BBox und der zentral
  geprueften SAM-Segmentierung zu. Mindestens 80 Prozent der Maskenpixel muessen
  innerhalb der Hand-Box liegen. PDF-Gold braucht die unveraenderte PDF-Pruefspur
  sowie Operateur-Code und Operateur-Befundtext. `ConfirmedByUser` muss exakt mit `ApprovedBy`
  der Registry uebereinstimmen. Der Beschreibungstext ist fuer den reinen
  YOLO-Geometrieexport kein Label und blockiert deshalb historische Platzhalter nicht.
  Teacher-, Auto-, Fremdbestaetigungen und
  unvollstaendige Handlabels bleiben im Inventar, gelangen aber nicht in train/val.
- `TrainingExportRegistryFileStore` liest strikt
  `<KnowledgeRoot>\training\export_registry_v1.json`. Nur Status `approved` mit
  menschlicher Freigabe, festen Haltungsrollen und exakt passenden Schutz-Set-
  Manifest-Hashes darf einen Plan erzeugen. Das optionale Feld
  `approved_sample_ids` schränkt einen Pilot zusätzlich auf exakt aufgezählte
  TrainingSample-IDs ein. Leer erhält das bisherige Verhalten.
  Hauptablauf, Validierung und interne JSON-Dokumente liegen getrennt in
  `TrainingExportRegistryFileStore.cs`, `.Validation.cs` und
  `TrainingExportRegistryFileDocuments.cs`.
- Der Plan entsteht vor dem Sidecar-Healthcheck. Er legt Klassen-IDs, Haltungssplit,
  Ausschlüsse, Dateinamen und Labels fest. Gleiche Bild-SHAs werden zu einer Datei
  mit allen eindeutigen Labels zusammengeführt; widersprüchliche Haltungen,
  Splits oder Endungen sind harte Fehler.
  Randbündige BBox-Grössen werden bei der Sechsstellen-Kanonisierung nötigenfalls
  minimal nach innen gerundet, damit die Rundung keine gültige Box ungültig macht.
- Plan und Manifest enthalten keine absoluten Kundenpfade. Lokale Originalpfade
  existieren nur im Laufzeit-Bundle.
- Sidecar und lokaler Ausführer schreiben byte-stabile Bilder, Labels, `classes.txt`,
  `data.yaml`, `manifest.json` und `_export_receipt.json` zuerst unter `.staging`.
  Erst ein vollständig geprüfter Ordner wird atomar als
  `<KnowledgeRoot>\training\datasets\<plan_id>` veröffentlicht. Vorhandene
  abweichende Ziele bleiben unverändert und führen zum Konflikt.
- Der Sidecar-v2-Vertrag erlaubt keine alten Felder. Er bindet Klassen, Split,
  Dateiname, Labels sowie class_map-, VSA- und Registry-Hash an das C#-Manifest.
  `plan_sha256` muss gleich `plan_id` sein. HTTP 4xx führt nicht zum lokalen Bypass.
- Der KI-Start setzt `SEWER_SIDECAR_TRAINING_EXPORT_ROOT` aus dem aktiven
  `KnowledgeRoot`; die Workflow-Antwort prüft denselben Zielpfad. Sidecar offline
  oder Transportausfall verwendet dasselbe Plan-Bundle lokal.
- `TrainingExportExecutionService` besitzt Healthcheck, Anmeldung, Request-Grenze,
  Transport-Rückfall und Zielpfadprüfung. HTTP- und Dateilogik gehören nicht in die UI.
- `TrainingYoloExportCoordinator` besitzt Auswahl, Inventar, Klassenkarte, Plan,
  Ausführung und Abschluss. Sein Befehl kann keine Root-Pfade austauschen. Nur der
  aktuelle `TrainingDataInventoryRuntimeSnapshot` bestimmt die Samples im Plan;
  eine sichtbare oder veraltete UI-Liste darf die Auswahl nicht beeinflussen.
- Eligibility und `ExportedUtc` werden erst nach bestätigter Ausführung auf den
  Inventar-Snapshot angewandt und genau einmal gemeinsam gespeichert. Danach werden
  nur `TrainingEligible`, `TrainingEligibilityReason` und `ExportedUtc` in passende
  UI-Samples gespiegelt. Ein Plan- oder Ausführungsfehler verändert keine Samples.
- `PlanOnly` durchläuft Register, Live-Inventar, Klassenkarte und Planer, führt aber
  weder Execution noch Completion aus, speichert nichts und mutiert keine UI-Liste.
- `TrainingYoloExportRuntime` ist der gemeinsame Aufbaupunkt dieses Subsystems.
  Die WPF-Komposition delegiert dorthin; so wächst `ServiceProvider.cs` nicht um
  einzelne Konstruktoraufrufe und CLI/WPF können fachlich nicht auseinanderlaufen.
- `TrainingExportCompletionService` markiert nur geplante `TrainingSample`-Quellen,
  deren Bild-SHA vom passenden Plan bestätigt wurde. Teacher-, Ausschluss- und
  fremde Sample-Quellen bleiben unverändert.
- Inventar-Run und Erzeugungszeit gehören zum Release-Kandidaten. Wiederholungen
  desselben HTTP-Plans sind idempotent; ein neuer Exportbefehl erzeugt bewusst einen
  neuen Kandidaten.
- Der gemeinsame Golden-Test unter `tests/Fixtures/TrainingExport/` führt denselben
  Fall mit Train, Dev-Val und Multi-Label-Bild durch beide Ausführer. Relative Pfade,
  SHA-256 und Bytes aller Ausgabedateien müssen identisch sein.
- Kuratierte Negativ-/Hintergrundbilder sind ueber das optionale Registry-Feld
  `negative_images` an denselben C#-Plan angeschlossen. Eine reine Alt-Registry darf
  nur direkte Dateien aus `training/negatives/bcc_pilot` verwenden. Neue Eintraege
  muessen aus einem vollstaendig reviewten Satz stammen und binden Set, Bild,
  Haltung, Split, Queue, Modellvorhersagen, Review und class_map. Der Export schreibt
  fuer Negative eine leere Labeldatei. Sie sind Trainingsdaten, aber keine Goldbefunde.

Freigabestatus: Das aktive Mehrklassen-Register `DETECT_ALL` ist fuer 73
persoenlich entschiedene Teacher-Codes freigegeben und nennt jede erlaubte
Goldsample-ID einzeln. Die aktive Migration v3 besitzt 143 Zeilen: 74 sind
`approved`, 69 bleiben `pending`; 61 Teacher-Codes werden gemappt und 12 bewusst
verworfen. Der getrennte BCC-Bogen-Pilot bleibt als enger Altweg erhalten.
Register- und Exportfreigabe sind niemals eine Modellfreigabe.

`tools/StageAExporter` ist jetzt die lokale Kompatibilitäts-CLI vor derselben Runtime.
Sie besitzt keine eigene Split-, Klassen-, Label- oder Dateilogik. `--dry-run` und
`--plan-only` bedeuten echten schreibfreien Planlauf. `--val-ratio` und
`--allow-dummy-bbox` sind harte Fehler; `--source` und `--out` werden nur akzeptiert,
wenn sie exakt den kanonischen Pfaden unter dem aktiven KnowledgeRoot entsprechen.
Das Tool liegt in `AuswertungPro.sln`, aber bewusst nicht im hilfsprogrammfreien
`AuswertungPro.Dev.slnf`. Keine UI/WPF-Projektreferenz in das Tool einführen.

Der frühere, nicht registrierte `YoloDatasetExportService` ist entfernt. Er war ein
zweiter Schreiber mit eigener Klassenbildung, eigenem Bild-Split und freien Zielpfaden.
Keinen Ersatzadapter dafür bauen: YOLO-Datensätze entstehen ausschließlich über den
gemeinsamen Coordinator und einen seiner beiden plan-gesteuerten Ausführer.

`training/scripts/prepare_detect_gold.py` baut fail-closed das Mehrklassen-Register
`DETECT_ALL` aus einem expliziten Gold-Audit, persönlicher Codefreigabe und nur
streng reviewten `all_classes_clear`-Negativen. Negative Bilder werden gegen alle
Auditrollen geprüft: derselbe Bildhash ist immer gesperrt, Testhaltungen samt
Gegenrichtung sind gesperrt und abweichende Train-/Validation-Rollen stoppen.
Beim Erneuern dürfen neue eingefrorene Eval-Sets monoton hinzukommen; jedes
bereits im Register gebundene Schutzmanifest muss unverändert vorhanden bleiben.
Der aktuelle Plan
`ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2`
enthält 852 Bilder, 894 Instanzen, 686 Train-, 166 Validation-Bilder und 9
strikte Negative. `train_detect_gold.py` validiert Receipt, Klassenkarte, Hashes
und Labels erneut und erzeugt nur `not_deployed`-Kandidaten. Kandidat
`detect_gold_9eb020e30322` beendete 40/40 Epochen; seine interne Validation
P 0,3917, R 0,3129, mAP50 0,3026 und mAP50-95 0,1726 ist keine Freigabe.

Die reinen Diagnosewerkzeuge `detect_klassenbreite.py` und
`detect_klassenbreite_messung.py` liegen ausserhalb der Kandidaten- und
Freigabekette. Der Builder hält Bilder und Splits vollständig, filtert und
nummeriert nur Labels um; dadurch ändert sich neben der Klassenbreite auch der
Negativdruck. Er prüft Klassenkarte, Bilder und Labels vor jeder Ausgabe und
veröffentlicht nur über einen eigenen markierten Staging-Ordner in ein freies Ziel.
Das Messwerkzeug nutzt eine absolute Laufzeit-YAML und einen temporären
Arbeitsordner, nie den Datensatz als Prozessordner. Neues Training ist bei laufendem
`SewerStudio.exe`, aktivem Sidecar, unklarem VRAM oder weniger als 28'000 MB freiem
VRAM gesperrt. Bestehende Lauf-/Berichtsziele werden nie überschrieben; nach jedem
Training und bei reiner Nachmessung wird `best.pt` ausdrücklich mit `half=False`
und `batch=4` ausgewertet. Gewichtsklassen müssen exakt zur Datensatzkarte passen;
Klassen ohne Validation-Sollbox erscheinen mit `null` und Grund, und die Belege
enthalten SHA-256.

Die einheitliche Nachmessung vom 2026-09-02 ergab für `BCC_bogen` AP50 0,8272 /
0,8151 / 0,8448 bei 15 / 5 / 2 Klassen. Gegenüber der früher gemischten Prüfung
änderte sich AP50 je Klasse höchstens um 0,0039; sie erklärte die
Stufenunterschiede nicht allein. Das fremde `yolo26n.pt` des Referenzlaufs wurde
hashgleich aus dem Plan-Datensatz nach `training/diagnostics/quarantine` verschoben;
der Gold-Validator akzeptiert den Plan wieder mit 852 Bildern und 894 Instanzen.
Die historischen Kopien in `diagnostics/klassen_5` und `klassen_2` bleiben
unverändert und werden vom gezielten Messpfad nicht gelesen.

`detect_gold_holdout_provenance.py`, `detect_gold_holdout_scoring.py` und
`evaluate_detect_gold_holdout.py` bilden den unabhängigen positiven
Mehrklassen-Holdout. Sie binden Kandidat, private Gewichtskopie, Basisgewicht
unter `sidecar/models`, Dataset, DETECT_ALL-Beleg, Klassenkarte, Migration,
Basis-/Aktuell-Audit, aktuelle Samples und alle Bild-/Sample-/Haltungsrollen.
Von 83 gemappten Testinstanzen auf 79 Bildern bleiben nach Ausschluss der mit
einem Trainingsnegativ kontaminierten Haltung `77457-77453` genau 81 Instanzen
auf 77 Bildern aus 30 physischen Haltungen. Das feste Protokoll ist
`conf=0,25`, `imgsz=1280`, `IoU=0,5`; zuerst wird ein labelblinder SHA-Beleg
geschrieben. Technische Fehler zählen nie negativ. Mehrfachboxen werden zuerst
nach maximaler Trefferzahl, danach maximalem Gesamt-IoU zugeordnet.
Der gültige GPU-Lauf vom 2026-08-02 hat Bericht-SHA-256
`9ce6aaad85317061953796085ff7daf921b554295f2bad21e904cc5dc78789f6`:
TP 17, FP 24, FN 64, Precision 41,5 %, Recall 21,0 %, F1 27,9 %.
`BCC_bogen` traf 14/16, `BCA_anschluss` 3/17; elf weitere gemessene Klassen
hatten keinen exakten Treffer. Ohne frische saubere Negativbilder lautet der
Status `positive_holdout_only_not_release_qualified`; das Modell bleibt
`not_deployed`. Der frühere Lauf mit Zeitstempel `20260802_120445_930796`
ist wegen falscher RGB/BGR-Übergabe aufgehoben und darf nicht bewertet werden.

`training/scripts/prepare_bcc_pilot.py` verlangt mit `--gold-audit` einen expliziten
aktuellen Bericht unter `<KnowledgeRoot>\training\reports`. Es prüft dessen
Samples-, Registry-, Bild-, Negativpool- und Split-Bezug erneut, berechnet die
deterministische Split-Rolle nach und übernimmt nur BCC-`train` und BCC-`val`;
`test` bleibt strikt ausgeschlossen. Ohne `--execute` bleibt der Lauf schreibfrei.
Alte reine Registrys dürfen den flachen Legacy-Pool weiter lesen. Neue Läufe
verwenden wiederholbare `--negative-set`-Quellen unter
`training/negatives/sets`: Set-ID, Manifest, Bildbytes, echte Haltung,
Train-/Validation-Split, Review, Queue, Kandidatenliste und class_map v3 werden
vollständig neu geprüft. Legacy und strikte Einträge dürfen in einer neuen
Registry nicht gemischt werden. Der C#-Registry-Leser prüft dieselbe
Manifestmitgliedschaft, die exakte Set-Dateimenge, alle vier Receipt-Dateien,
Queue-Modellscope und -Vorhersagen, Review-Vollständigkeit, class_map/VSA-Bindung,
Bildsignatur sowie Bild-, Haltungs-, Split- und Provenienzfelder. Die Registry muss
jedes Manifestbild genau einmal enthalten; Teilmengen sind ungueltig. Der
Holdout-Kontaminationsscan schützt beide Bestandsarten nach Bildhash und
physischer Haltung samt Gegenrichtung.
Ein vorhandenes Register darf nur mit `--execute --renew-existing` erneuert werden:
Der Altstand wird vorher bytegenau unter
`training/pilots/BCC/registry_history/<sha256>.json` archiviert, der Pilotbeleg
ebenfalls versioniert und der aktive Wechsel bei Fehlern zurückgesetzt.
`training/scripts/train_bcc_pilot.py` akzeptiert ausschließlich einen vollständigen,
über `_export_receipt.json` gehashten Export unter
`<KnowledgeRoot>\training\datasets`. Es verlangt die BCC-Klasse ID 14, mindestens
30 Bilder, ausschliesslich BCC-Labels und getrennte Train-/Val-Splits. `data.yaml`
darf nur `.`, `images/train` und `images/val` referenzieren. Receipt-, YAML- und
Klassen-Hash werden in jedes neue Kandidatenmanifest gebunden. Es trainiert
vom unveränderten `sidecar/models/yolo26m/yolo26m.pt` und schreibt nur einen
`not_deployed`-Kandidaten nach `training\models\candidates`. Ein erreichbarer
Sidecar, nicht messbarer VRAM oder weniger als 28000 MB freier VRAM sperrt den Start.
Das Skript beendet SewerStudio nie und ersetzt keine produktiven Gewichte. Der
BCC-Pilot verwendet Batch 3, richtungsneutrale Flip-Werte `flipud=0`/`fliplr=0`,
leichte HSV-Augmentierung und Early Stopping mit `patience=10` als Standard.
Die von Ultralytics erzeugten `train.cache`- und
`val.cache`-Dateien werden vor und nach dem Lauf entfernt; die plan-gesteuerten
Bilder, Labels und Belege bleiben unverändert.

Der nicht aktivierte BCC-Kandidat darf im Training Studio nur über
`TrainingPreviewDetectionService` und die getrennten Sidecar-Endpunkte geprüft
werden. `GET /detect/yolo/bcc-test/candidates` liefert nur ID, SHA-256 und
pfadfreie Metadaten direkter, manifest- und hashgeprüfter Unterordner.
Das Training Studio zeigt jeden Kandidaten einzeln und bietet keine automatische
BCC-Auswahl an. `POST /detect/yolo/bcc-test` erhält nur die gewählte ID und
deren erwartete SHA-256, niemals einen Modellpfad. Unbekannte oder unsichere IDs,
eine Hashabweichung sowie eine abweichende Antwort-ID/-SHA brechen fail-closed ab;
es gibt dann keine Vorschau-Box. Der kompatible Request ohne Kandidaten-Pin darf
intern weiter automatisch wählen, wird aber von der UI nicht verwendet.
Der Sidecar akzeptiert nur `not_deployed`, Pilot `BCC_bogen`, mindestens 30
Bilder und passende Gewicht-SHA; die freigegebene 15er-Klassenkarte wird beim
Modellladen für alle IDs und Namen exakt geprüft. Der Sidecar kopiert einen
einmal gelesenen, hashgeprüften Byte-Strom in eine private temporäre
Momentaufnahme. YOLO öffnet nie erneut den veränderbaren Kandidatenpfad; die
Momentaufnahme wird nach dem Laden nochmals gehasht. Der BCC-Pilot liefert
ausschließlich Treffer der geprüften Klasse 14 `BCC_bogen`; Klassen 0 bis 13
werden verworfen.
Das Modell läuft im eigenen GPU-Slot `YOLO_TEST` und ersetzt den produktiven
Artefaktzeiger nicht. Bei VRAM-Mangel darf der allgemeine LRU-Manager den
geladenen Slot `YOLO` vorübergehend entladen; er wird bei Bedarf neu geladen.
`TrainingStudioPreviewModelCatalog` baut die fail-closed Auswahlliste;
`TrainingStudioPreviewPresenter` formatiert das reine Anzeigeergebnis außerhalb
des ViewModels. Im Training Studio sind die Treffer blaue Vorschau-Boxen mit
Code und Klartext.
Sie dürfen nie `CurrentBox`, SAM-Maske oder Goldsample setzen. Ein Bild- oder
Kandidatenwechsel verwirft späte Ergebnisse. Katalogfehler entfernen alte
Kandidaten; ein spätes Katalogergebnis überschreibt keine neuere
Benutzerauswahl. Ohne exakten ID-/SHA-Pin bleibt der Kandidat gesperrt. Ein
qualitätsbedingt nicht ausgewertetes Foto wird als `nicht geprüft`, nicht als
Negativtreffer gemeldet. Während des Modelltests beginnen weder ein neuer
Box-Lauf noch ein Speichervorgang. Nur die rote, persönlich gezogene Box geht
über Akzeptieren/Korrigieren in den Goldspeicher.

Der gleich parametrische Kandidatenvergleich vom 2026-07-28 (`conf=0,25`,
`imgsz=1280`) belegt für `bcc_bogen_b50b37ab8a4f` auf drei wirklich unbekannten
BCC-Testbildern 3/3 Treffer und eine mittlere IoU von 0,8607, aber Aktivierungen
auf 9/14 kuratierten Negativbildern. Der Kandidat bleibt `not_deployed`.
Die älteren Kandidaten kannten die heutigen Positivbilder bereits; der
v3-Negativ-Kandidat und der neue Kandidat kannten den heutigen Negativpool.
Diese 17 Bilder liefern deshalb keinen fairen Gesamtsieger und keine Freigabe.

`training/scripts/bcc_release_holdout.py` baut den unabhaengigen
BCC-Release-Holdout aus XTF-Fotoquellen, deren Inspektionsdatum nach dem lokalen
Basismodell-Zeitstempel liegt. Ohne urspruengliches Trainingsinventar ist diese
Zeitgrenze nicht vollstaendig beweisbar. Vor der Auswahl scannt
das Werkzeug alle lokal nachvollziehbaren Kandidaten-Manifeste samt Gewichten,
Dataset-Receipts, lokaler YOLO-Konfiguration, TrainingSamples, Negativpools,
Collapse-Berichte und Eval-Sets. Bestehende eingefrorene Eval-Manifeste werden
gegen jede deklarierte Datei sowie die exakte Bild-/Labelmenge validiert. Alte
Collapse-Berichte ohne Provenienz werden ueber ihre heutigen Bildpfade
rekonstruiert und im Beleg als Legacy ausgewiesen. Reine Legacy-Dateinamen
muessen als nichtleere Strings vorliegen und eindeutig auf ein bekanntes Bild
aufloesbar sein; andernfalls stoppt der Scan. Kandidaten ohne direkte Receipt-,
YAML- und Klassen-Hashbindung werden nur
fuer die exakt vier historischen Kandidaten-IDs mit unveraenderter Manifest-SHA
akzeptiert. Jedes Manifest im BCC-Kandidatenordner muss `pilot=BCC_bogen`
tragen. Jeder neue oder veraenderte Kandidat muss diese Hashes binden.
Gleiche Bild-SHA-256 sowie dieselbe physische Haltung in beiden Richtungen werden
gesperrt. Veraltete oder nicht aufloesbare Kandidatenlinien sind harte Fehler.
Die Originale werden nur gelesen; verifizierte Kopien entstehen zuerst in einem
Staging-Ordner und werden als neues
`<KnowledgeRoot>\eval_set\subsets\bcc_release_holdout_<sha>` atomar
veroeffentlicht. Vorhandene Ziele werden nie ueberschrieben.

Der lokale Blind-Pruefplatz
`tools/EvalVisibilityReview/bcc_release_holdout_review_server.py` zeigt keine
bildbezogenen XTF-Untercodes und keine Modellvorhersagen. Er zeigt allen Bildern
nur den festen Pruefauftrag `BCC — Bogen`; die verdeckte Vorauswahl bleibt
unsichtbar. Er speichert `positive`, `negative` oder `exclude` atomar in einer
Datei ausserhalb des Holdouts und bindet sie an
Holdout-ID, Manifest-SHA und Kandidaten-SHA. Ein prozessweiter Datei-Lock und
eine Versionspruefung verhindern unbemerktes Ueberschreiben durch parallele
Pruefplaetze. Der Status
`ready_for_binary_evaluation` verlangt eine vollstaendige Review sowie mindestens
20 positive und 20 negative Haltungen. Er ist keine Modellfreigabe;
das eingefrorene Manifest behaelt `release_status=not_evaluated`. Ohne Boxen
misst der Holdout keine
Lokalisation und kein mAP. Beim eingefrorenen V1-Bestand muessen
Kandidatenumfang sowie die aggregierten Fingerprints der bekannten Bild-Hashes
und Haltungs-Aliase exakt gleich bleiben. Eine Aenderung dieser Werte sperrt;
eingefrorene Eval-Manifeste werden zusaetzlich dateiexakt geprueft. Danach ist
ein neuer Holdout erforderlich. Der am
2026-07-28 eingefrorene Bestand `bcc_release_holdout_64d06094c921` hat 60 Bilder
aus 60 Haltungen. Seine gebundene Review ist abgeschlossen: 60/60 Bilder,
29 positiv, 31 negativ und 0 ausgeschlossen. Der dynamische Status lautet
`ready_for_binary_evaluation`; das eingefrorene Manifest behaelt seinen
Erstellungsstand `review_incomplete`.

`training/scripts/evaluate_bcc_release_holdout.py` vergleicht exakt den
eingefrorenen Kandidatenumfang mit `conf=0.25`, `imgsz=1280` und nur Klasse 14
`BCC_bogen`. Es sperrt einen parallel laufenden Sidecar und bindet
Review-Momentaufnahme, Bildbytes, Kandidatenmanifeste, Gewichte,
Aufhebungsmarker, Klassenkarte, Geraet, Qualitaetsgrenzen und Laufzeitversionen.
Zuerst wird ein labelblinder Vorhersagebeleg atomar geschrieben. Das Scoring
liest genau diesen SHA-gebundenen Beleg neu ein. Technische Fehler zaehlen nie
als negative Vorhersage; ein Teilfehler verhindert den endgueltigen
Auswertungsbericht. Training, Aktivierung und produktive Modellzeiger werden
nicht veraendert.

`tools/PdfCodeScanner` erzeugt daneben eine rein lesende protokollbasierte
BCC-Positionsliste. Sie fuehrt die acht gueltigen Untercodes `BCCAA`, `BCCAB`,
`BCCAY`, `BCCBA`, `BCCBB`, `BCCBY`, `BCCYA` und `BCCYB` fuer die grobe
Modellklasse `BCC_bogen` gemeinsam. Pro Befund werden PDF, Meteranfang/-ende,
exakter Videozaehlerstand und nur ein eindeutig zugeordnetes Video ausgegeben;
fehlende oder mehrdeutige Werte bleiben sichtbar. Der bekannte Rohcode `BCC.YB`
schliesst die ganze betroffene Haltung fail-closed aus. Der JSON-Bericht wird
atomar ausserhalb der Kundenoriginale geschrieben. Diese Liste ist erst die
Messgrundlage; ohne Modelllauf und Zuordnungstoleranz ist sie noch kein Recall-
oder Praezisionswert. Mit `--expect-holdings` und `--expect-findings` stoppt das
Werkzeug fail-closed, wenn der gescannte Bestand nicht zur zuvor freigegebenen
Ausgangszahl passt.

Die Archivmessung des BCC-Copiloten wird mit
`training/scripts/bcc_pdf_recall_bericht.py` strikt in Kalibrierung und Messung
getrennt. Gesamt-, SD- und HD-Ausgaben besitzen verschiedene Dateinamen; ein
Gruppenlauf darf den Gesamtbeleg nie ueberschreiben. Der additive
`vergleichsbestand_*.json` kennzeichnet die verbrauchte Messhaelfte ausdruecklich
nur als bekannten Vergleichsbestand, nicht als neue Release-Abnahme.
`bcc_pdf_precision_queue.py` rekonstruiert den gemessenen Arbeitspunkt aus den
gespeicherten Einzelbildern und baut eine blinde Clip-Pruefung aller Vorschlaege.
Konfidenz und PDF-Zuordnung bleiben unsichtbar. Queue und Clipbytes sind per
SHA-256 gebunden. Erst die vollstaendige Review darf
`bcc_pdf_precision_bericht.py` auswerten; unsichere Urteile erscheinen als
untere und obere Precision-Grenze.

Der reale Blindreview des Archiv-Arbeitspunkts ist abgeschlossen: 154/154
Vorschlaege, davon 91 mit sichtbarem Bogen, 60 ohne Bogen und 3 unsicher.
Vorschlags-Precision ohne unsichere Faelle: 60,3 %; harte Grenze bei anderer
Wertung der drei unsicheren Faelle: 59,1-61,0 %. Das ist keine
Ereignis-Precision, weil zwei Vorschlaege denselben Bogen zeigen koennen. Aus
diesem Wert und dem PDF-Recall darf deshalb kein F1-Wert gebildet werden.

`training/scripts/osd_wahrheit_aus_protokoll.py` erzeugt OSD-Bilder aus dem
PDF-Meterstand am PDF-Videozaehlerstand. Das Ziel darf nicht unter dem
Kundenbestand liegen, wird ueber einen Arbeitsordner atomar veroeffentlicht und
nie ueberschrieben. Gleiche oder umgedrehte Haltungen bleiben im selben
Train-/Validation-/Test-Teil; bytegleiche Bilder werden nur einmal aufgenommen.
Das Werkzeug wird mit `sidecar\.venv\Scripts\python.exe` gestartet, weil der
Meterleser OpenCV aus dieser Umgebung benoetigt.
Der automatisch beschriftete Bestand startet mit `status=qa_offen`: Die zwei
belegten Zeitpunkte pruefen die grundsaetzliche Zeitachse, ersetzen aber keine
Sichtprobe ueber den ganzen Archivbestand.

`training/scripts/bcc_pdf_messreserve.py` reserviert deterministisch einen neuen
reinen SD-Messbestand. Es sperrt alte Mess-, Trainings- und Eval-Haltungen samt
Gegenrichtung und akzeptiert nur die acht gueltigen BCC-Untercodes. Der aktuelle
V2-Beleg umfasst 50 SD-Haltungen mit 130 Boegen und startet mit
`reserved_not_evaluated`. Eine unabhaengige HD-Reserve existiert weiterhin nicht.

Der reale OSD-V1-Lauf enthaelt nach Schutzfiltern und Byte-Deduplizierung 897
Bilder aus 364 physischen Haltungen: 674 Train, 135 Validation und 88 Test.
`osd_protokoll_qa_queue.py` hat daraus eine blinde Sichtprobe mit 30 Bildern aus
30 Haltungen erzeugt. `tools/EvalVisibilityReview/start_osd_protokoll_qa.ps1`
oeffnet den Eingabeplatz; erst `osd_protokoll_qa_bericht.py` vergleicht die
persoenliche Lesung mit den bis dahin verdeckten PDF-Sollwerten.

Die reale OSD-Sichtprobe ergab 25/30 Uebereinstimmungen auf 1 cm und 29/30
innerhalb 10 cm; ein Fall wich grob ab. Die kleinen Differenzen passen zur
Kamerabewegung zwischen Protokollmoment und Bild, der grobe Fall ist ein falsches
PDF-Label. Die Sichtprobe misst die PDF-/Video-Zuordnung und nicht den Leser;
bei allen fuenf Differenzen hatte er `nicht_gelesen` geliefert. Die 897 Werte
bleiben schwache Labels mit Zeit- und Zuordnungsrauschen. Nur die 30
persoenlich abgelesenen Werte sind exaktes Gold. Der aktuelle
`sidecar/sidecar/osd_meter.py` ist ein fester Vorlagenleser ohne Trainingsweg;
ein neues trainierbares OCR-Modell ist noch nicht vorhanden.

`osd_layout_review_queue.py` zieht deshalb 40 weitere physische Haltungen, je
ein Bild und ohne Ueberschneidung mit der 30er-Sichtprobe. Der lokale
`osd_layout_review_server.py` zeigt weder PDF-Wert noch Lesergebnis. Die
Meteranzeige wird direkt im Bild angeklickt und getrennt nach Polaritaet, Farbe
und Schreibweise eingeordnet. `osd_layout_review_bericht.py` zaehlt erst eine
vollstaendige, an Queue- und Bild-SHA gebundene Review. Die Lage wird nur aus dem
menschlichen Klick abgeleitet; Kopftext darf nicht automatisch als Meterstand
gelten.

Die reale 40er-Sichtung ist abgeschlossen: 38 Meteranzeigen liegen unten rechts,
2 unten links und keine oben. Polaritaet: 18 hell auf dunkel, 18 dunkel auf hell,
4 andere. Farbe: 20 weiss/grau, 7 gelb, 13 andere. Format: 19 mit Praefix oder
fuehrenden Nullen, 15 Zahlen mit Einheit, 6 ohne Einheit. Das belegt mehrere
Hauptstile, aber wegen der kleinen Stichprobe keine exakten Archivanteile.

Der Diagnosekandidat fuer den Vierziffern-Stil nutzt nach einer gescheiterten
oder unvollstaendigen Vorlagenlesung das bereits lokal installierte Tesseract.
Er prueft beide unteren Ecken und beide Polaritaeten, akzeptiert aber nur die
vollstaendige Form `LZ... + 0000.00 m`; fehlt Tesseract oder ist die Form
unsicher, bleibt der Wert `None`. Auf dem Zielstil liest er 8/12. Sein neuer
Rueckfallweg liefert in der 40er-Probe 12 Werte, alle 12 passend zu den schwachen
PDF-Labels; der gesamte Leser liefert dort 13 Werte mit einem falschen oder
nicht pruefbaren Fall. Im Goldbestand liefert SD 82/82 richtig, HD nichts und
HD2 0 geliefert/0 falsch. Der fruehere HD2-Fehler `f0046.jpg` aus Haltung
`35722-35724` (Soll 13,7 m, Vorlagenlesung 11,7 m aus `L211.7m1.`) wird durch
eine enge Trennzeichen-Sperre nach `L2`/`LZ2` verworfen; SD bleibt 82/82. Die
hashgebundene Archivwiederholung mit `osd_archiv_abdeckung_messung.py` verarbeitet 83
eindeutige Videos an je 20 gleichmaessigen Stellen: insgesamt SD 22,1 % und HD
3,1 %, im von der 40er-Kalibrierung getrennten Anteil SD 21,2 % und HD 4,0 %.
Der Bericht bindet Leser und feste Auswahl; Video-Inhalte sind ueber Pfad,
Groesse und Aenderungszeit, aber nicht per Vollhash gebunden. Der Kandidat bleibt
`diagnostic_not_deployed`.

Der trainierbare OSD-Zeichenleser besteht aus dem reinen Laufzeitkern
`sidecar/sidecar/osd_modell.py`, dem Diagnose-/Messpfad
`training/scripts/osd_modell_leser.py` und dem Sidecar-Anschluss
`sidecar/sidecar/models/osd_model_wrapper.py`. Der Laufzeitkern besitzt den
gemeinsamen Zonen-Zuschnitt, die feste Hoehennormierung, Box-Entdoppelung,
Zeichenreihenfolge und die Deutung ueber `osd_meter.parse_meter`. Der
Trainingspfad bleibt die kompatible Dateipfad-Fassade fuer Kalibrierung und
Goldmessung. Keine zweite Cropping-, Zeichen- oder Formatregel daneben bauen.

`osd_meter.lese_meter` nimmt additiv einen optionalen Modellleser entgegen.
Die Reihenfolge ist verbindlich: Vorlagenleser, Tesseract-Vierziffern,
Tesseract-Zwei-Dezimal und erst danach das Modell. Ein vorhandener bisheriger
Wert wird nie ersetzt. Der Modellleser erhaelt denselben Format-Lock; rohe
Einzelbildlesung bleibt von `MeterSequencePlausibility` und
`MeterSequenceGapFiller` getrennt.

Der Sidecar-Anschluss bindet fest Kandidat `osd_zeichen_c668e35d59cb`,
Gewicht-SHA-256
`c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845`,
Schwelle 0,25, Status `diagnostic_not_deployed`, die 15 Zeichenklassen und
`weights/best.pt`. Das Gewicht wird aus einer privaten, erneut gehashten
Momentaufnahme geladen. Der eigene GPU-Platz `YOLO_OSD` besitzt Busy-Lease,
Watchdog und eine konservative Zulassungsschaetzung von 0,5 GB. Der BCC-Wrapper
reicht das Modell nur bei `osd_model_fallback_enabled=true` durch; Default ist
`false`. Diesen Standard nicht ohne frischen, unberuehrten Freigabebestand
aendern.

`training/scripts/osd_kettenmessung.py` vergleicht Basis und Kette gegen dieselben
hashgebundenen Saetze und misst Laufzeit sowie optional den gemeinsamen VRAM mit
dem echten Bogen-Kandidaten. Stand 2026-08-17 auf SD, HD, HD2 und Mix: Basis
194 richtig / 1 falsch, Kette 224 / 1; 30 neue richtige und null neue falsche.
Mit geladenem BCC-Modell kamen rund 9 MB VRAM hinzu. Warme Modelllesung: Mittel
61 ms, Median 35 ms, p95 115 ms; ueber alle 317 Bilder rund 24 ms Zusatz je Bild.
Bericht-SHA-256:
`ef25b19df5ae1a169ea91da5b3e14e931b5c196084c596aa05732c810dcd1093`.
Alle vier Saetze sind durch diese Entscheidung verwendet und koennen keine
spaetere Produktfreigabe mehr liefern. Die 22 Sollbilder ohne gefundene Zeichen
sind eine getrennte Baustelle vor der Erkennung.

`training/scripts/bcc_pdf_messreserve.py` reserviert deterministisch einen neuen
reinen SD-Messbestand. Es sperrt alte Mess-, Trainings- und Eval-Haltungen samt
Gegenrichtung und akzeptiert nur die acht gueltigen BCC-Untercodes. Der aktuelle
V2-Beleg umfasst 50 SD-Haltungen mit 130 Boegen und startet mit
`reserved_not_evaluated`. Eine unabhaengige HD-Reserve existiert weiterhin nicht.

Der reale Vierervergleich vom 2026-07-28 hatte 240 Vorhersagen und null
technische Fehler. Zwei aufgehobene Altlaeufe sind nur Diagnose. Die zwei noch
relevanten Kandidaten erreichten TP/FN/TN/FP 24/5/9/22
(`bcc_bogen_af8020b688ac_v3_negatives`) und 26/3/6/25
(`bcc_bogen_b50b37ab8a4f`). Der erste hat weniger Fehlalarme, der zweite
weniger verpasste Boegen. Es gibt keinen eindeutigen Spitzenreiter; beide
Fehlalarmraten sind zu hoch. Der Bericht lautet
`comparison_complete_not_release_qualified`, beide bleiben `not_deployed`.
Da der Holdout fuer die Kandidatenauswahl verwendet wurde, braucht jede
spaetere Aktivierung einen frischen, zuvor unberuehrten
Bestaetigungsholdout.

Der allgemeine Detect-Release-Holdout wird getrennt davon aus frischen,
eindeutig zugeordneten PDF-/Video-Haltungen gebaut. Der C#-Adapter
`tools/DetectReleaseHoldoutPdfExtractor` nutzt den bestehenden geschuetzten
PDF-Import, uebernimmt nur Operateur-Code und Befund eindeutig zugeordneter
Fotos und erzeugt optional genau einen deterministischen Video-Frame. PDF,
Video, Haltung und Bildbytes bleiben per SHA-256 gebunden; Kundenoriginale
werden nie veraendert. `prepare_detect_release_pdf_extraction.py` waehlt nur
ein exakt stemgleiches Video und verwendet keine Modellvorhersagen.
`prepare_detect_release_holdout.py` sperrt bekannte Bildhashes, beide
Haltungsrichtungen, mehrdeutige Quellen sowie alle gebundenen und noch
ungebundenen Trainingsdatasets. Es veroeffentlicht erst nach erneutem Scan und
vollstaendiger Staging-Hashpruefung atomar unter
`eval_set/subsets/detect_release_holdout_<sha>`. Der Bestand ist fuer Training,
Gold, Few-Shot und Kandidatenauswahl gesperrt.
`detect_release_holdout_review_server.py` zeigt keine KI-Vorhersage. Positive
Bilder brauchen Boxen fuer alle sichtbaren Objekte, `negative` ist nur bei
keiner sichtbaren Detect-Klasse erlaubt und unklare Bilder werden
ausgeschlossen. `detect_release_holdout_status.py` verlangt vor einer
Auswertung eine vollstaendige Review, mindestens 20 Instanzen je Klasse,
75 Negative und 30 negative physische Haltungen; diese Grenzen koennen nicht
gesenkt werden. Der aktuelle Review ist mit 400/400 Bildern abgeschlossen:
241 positiv, 74 negativ und 85 ausgeschlossen. Wegen fehlender
Klassenabdeckung und eines fehlenden Negativbilds bleibt er
`coverage_incomplete`.

`evaluate_detect_release_holdout.py` ist der getrennte, immer diagnostische
Mehrklassen-Auswerter. Bei ausgeschaltetem Sidecar prueft er Kandidat,
Basismodell, class_map v3, VSA-Manifest, Holdout und Bildbytes erneut, kopiert
das Gewicht privat und verwendet den geprueften RGB-zu-BGR-Weg. Mit festem
`conf=0,25`, `imgsz=1280` und `IoU=0,5` werden zuerst alle 400 Bilder
labelblind inferiert und der SHA-gebundene Ledger erneut eingelesen. Erst danach
wird die Review geladen; 315 positive und negative Bilder werden bewertet,
`exclude` wird ignoriert. Technische Fehler auf gewerteten Bildern brechen ab
und zaehlen nie als Negativtreffer. Der Workflow trainiert oder aktiviert kein
Modell und kann keine Freigabe erteilen.

Der erste technisch fehlerfreie GPU-Lauf vom 2026-08-03 erreichte auf 350
Soll-Boxen TP/FP/FN 36/59/314 (P 37,9 %, R 10,3 %, F1 16,2 %). Neun von 74
echten Negativbildern hatten mindestens einen Fehlalarm. `BCC_bogen` traf
27/37, `BCA_anschluss` 8/39 und `BAF_oberflaeche` 1/89; alle weiteren
gemessenen Klassen hatten null exakte Treffer. Bericht-SHA-256:
`64bd6ae370bc1a0bc7320aca5a0921a89cfa467fc9b7ff1c5e926780dc00dcbc`,
Ledger-SHA-256:
`a771cbd7fa1a959b49ecf41621df700259471494b7e110d73c7b96eb919adbf2`.
Der Kandidat bleibt `not_deployed`. Wenn diese Diagnose die naechste
Trainingsrunde steuert, ist fuer eine spaetere Aktivierung ein neuer,
unberuehrter Release-Holdout erforderlich.

`training/scripts/bcc_hard_negative_review.py` baut getrennt davon eine
eingefrorene Review-Queue fuer frische BCC-Fehlalarme. Bekannte Bildhashes sowie
gleiche oder umgedrehte Trainings-/Eval-Haltungen werden gesperrt. Der Builder
bindet class_map v3 samt VSA-Hash, Trainings-/Registry-Fingerprints,
Auswahlmodelle und geschuetzte Eval-Sets und kopiert genau ein Vollbild je
physischer Haltung. `bcc_hard_negative_review_server.py` zeigt weder
Modellvorhersagen noch XTF-Hinweise. Nur `all_classes_clear` bestaetigt, dass
keine der 15 Detect-Klassen sichtbar ist; das BCC-Holdout-Urteil `negative`
reicht nicht. Der aktuelle Bestand `bcc_hn_d37e1e0e481c` umfasst 14 Bilder aus
14 Haltungen. Sein Review ist vollständig: 10 `all_classes_clear`, 4
`mapped_object_visible`, 0 unklar. Der Publisher hat ausschliesslich die 10
freigegebenen Bilder als unveränderlichen Satz `bcc_hn_54f6608b975a`
veröffentlicht (8 Train, 2 Validation). `_manifest.json` und die kopierten
Receipts binden alle Bildbytes, Queue, Kandidatenliste, Review und class_map v3
per SHA-256; vorhandene Sätze und Kundenoriginale werden nie überschrieben.

`EvalContaminationGuard.IsEvalHaltung` blockiert bei allen Eval-Sets neben der
normalisierten Haltung auch die umgekehrte Richtung desselben Schachtpaars.
Das aktive Standardmodell darf dort nur bei ausdruecklichem `qualified=true`
laufen. Fehlender oder unlesbarer Status sperrt ebenfalls. Der ViewModel-await vor
UI-gebundenen Status-/Optionsaenderungen muss den WPF-UI-Kontext beibehalten.

`training/scripts/detect_gold_error_review.py` erzeugt aus dem gebundenen
Mehrklassen-Bericht und seinem labelblinden Vorhersagebeleg eine eingefrorene,
rein diagnostische Fehlfall-Queue. Jede nicht exakt getroffene Goldinstanz und
jede geometrisch unzugeordnete Vorhersage erscheint genau einmal. Es werden keine
Bilder kopiert; Training, Export und Quellenmutation sind in der Queue
ausdruecklich gesperrt. Der aktuelle gueltige Stand
`detect_gold_failure_a46a82535c82` umfasst 80 Faelle auf 67 Bildern:
56 verpasst, 8 falsche Klasse und 16 zusaetzliche KI-Boxen.

`tools/EvalVisibilityReview/detect_gold_error_review_server.py` zeigt Gold- und
KI-Boxen samt Klassen und speichert ausschliesslich `confirmed_model_error`,
`gold_suspect` oder `exclude_uncertain` in einer getrennten Review-Datei unter
`<KnowledgeRoot>/eval_review/detect_gold_failure_review`. Queue, Bericht, Ledger,
Kandidatenmanifest, Gewicht, aktueller Gold-Audit, Trainingssamples und
Klassenkarte sind SHA-gebunden und werden vor jeder Entscheidung erneut geprueft.
Browser-Revision, prozessweiter Lock, Dateiversion und atomarer Austausch
verhindern stilles Ueberschreiben. Der Pruefplatz mutiert weder Gold, KB,
Trainingsdaten, Registry noch Modell. Eine Sammelplanung darf nur aggregierte
Klassenbedarfe enthalten. Sobald ihre Erkenntnisse die Modellentwicklung
beeinflussen, darf derselbe Holdout nicht erneut als unabhaengige Release-Abnahme
verwendet werden.

`training/scripts/publish_detect_gold_collection_plan.py` verlangt eine
vollstaendige, an Queue-ID, Manifest-/Kandidaten-SHA und Reviewer gebundene Review.
Der Standardlauf ist schreibfrei. Nur `--execute` publiziert atomar und idempotent
einen `aggregate_only`-Plan mit Klassenzaehlern fuer neue positive Beispiele,
Negativ-/Verwechslungsfaelle und einen getrennten Annotation-Audit. Bildpfade,
Bildhashes, Sample-, Prediction-, Fall-IDs und Kommentare duerfen diesen Plan nie
verlassen. Eine bestaetigte falsche Klasse zaehlt sowohl als Positivbedarf der
Sollklasse als auch als konkrete Soll-zu-Vorhersage-Verwechslung. Die Review
`detect_gold_failure_a46a82535c82` ist abgeschlossen: 80/80 Entscheidungen,
75 bestaetigte Modellfehler, 0 Gold-Verdachtsfaelle und 5 Ausschluesse. Der
gueltige Plan `detect_gold_collection_874ec160e346` enthaelt 60 positive
Fehlerhinweise, 15 Fehlalarm-Hinweise und 6 Verwechslungen in 4 Klassenpaaren.
Der fruehere Plan `detect_gold_collection_44a08fe9895e` ist wegen seiner fehlenden
Verwechslungsliste aufgehoben und darf nicht verwendet werden.

`yolo_wrapper._pil_rgb_to_ultralytics_bgr` ist der verbindliche Farbübergang für
Ultralytics mit NumPy-Quellen: `decode_image` liefert PIL-RGB, Ultralytics erwartet
NumPy-BGR. Der produktive Detect-Pfad, die Legacy-Classification sowie BCC- und
Mehrklassen-Holdout-Auswerter verwenden denselben Helfer. Nicht wieder direkt
`np.array(PIL_RGB)` an `model.predict` übergeben; das vertauscht Rot und Blau.

Die produktive Detektorfreigabe ist fail-closed in
`sidecar/models/model_qualification.json` gebunden. Fuer das konkret aktive
PT-, TensorRT- oder ONNX-Artefakt muessen Dateiname und SHA-256 exakt passen;
Fallback, fehlende/defekte Datei, Hashabweichung oder unbekannter Status sperren.
`/detect/yolo` validiert dann nur die Eingabe und liefert ohne Modellinferenz einen
offenen DINO/SAM-Pfad; `/warmup` laedt das gesperrte YOLO ebenfalls nicht.
Batch-Video und Player-Einzelframe verwenden YOLO nur bei `qualified == true`.
Andernfalls laufen DINO/SAM weiter, Trace/Ergebnis/Health bleiben sichtbar
`Degraded` und verlangen eine manuelle Pruefung. Der getrennte
`/detect/yolo/bcc-test`-Kandidat ist davon nicht betroffen.

`training/scripts/model_collapse_check.py` ist ein schreibfreier Geometriecheck mit
den drei Ergebnissen `PASS`, `FAIL` und `INCONCLUSIVE`. Inferenzfehler, weniger als
10 Testbilder, weniger als 5 Detektionen oder unter 20 Prozent Detektionsrate duerfen
nie als PASS erscheinen. Ein PASS beweist nur, dass kein Box-Kollaps sichtbar war,
und ist keine Modellfreigabe.

`training/scripts/gold_stock_audit.py` liest den Goldbestand und schreibt nur einen
Bericht unter `<KnowledgeRoot>\training\reports`. Es prueft persoenliche Metadaten,
gueltigen UTC-Bestaetigungszeitpunkt, bei PDF die vollstaendige Operateurreferenz,
Bild, Box, echte Maskenpixel, echte Bilddimensionen, RLE-Maskenflaeche,
exakt auswaehlbaren Katalogcode, Bildhash und komplette reservierte
Eval-Haltungen. Haltungsnummern werden normalisiert; gleiche Haltungen und identische
Bildbytes bleiben ueber verbundene Komponenten in genau einer Split-Rolle. Ein Pilot
braucht mindestens 30 Samples sowie Daten in Train und Val/Test. Stand 2026-07-28:
219 Eintraege, 24 Drafts, 195 verwendbar, 0 Bildduplikatgruppen, 186 offene
KB-Texte und ein release-faehiger Split 128/53/14 aus 70 Gruppen. BCC besitzt
60 Samples (40 train, 17 val, 3 eingefroren test). Der aktuelle strikte
Vorbereitungslauf verwendet 57 positive Train-/Val-Samples plus die 10
All-Class-reviewten Negative aus `bcc_hn_54f6608b975a`; die 14 Altnegative ohne
vollstaendigen Review-Beleg bleiben bewusst ausserhalb der neuen Registry.
Der Exportplan
`f23a95b149addf9d24365834b563b7784f76132190d9e4e60f4c61e84a652bc9`
enthaelt 67 Bilder (48 Train, 19 Validation). Kandidat
`bcc_bogen_f23a95b149ad_hn10_strict` stoppte nach 33/40 Epochen und bleibt
`not_deployed` (Gewicht-SHA-256
`89331f637fe59cd2c321c3330733cc0278c57b4bd3a5c512662c12fef4a1ee78`).
Interne Validation und Negativkontrolle reichen nicht zur Freigabe: 2/2 strikte
Validation-Negative und 7/14 nicht mittrainierte Altnegative aktivierten bei
`conf=0.25`. Mehr unterschiedliche BCC-Boxen, weitere streng reviewte
Hard-Negatives und ein frischer Holdout bleiben Pflicht.

## Ereignisbasierte Eval-Messung (AP 0.4a, technische Grundlage)

Die frühere Sammeldatei `EvalSetBenchmark.cs` ist entfernt. Dataset, Scorer,
YOLO-Baseline, Router, Klassen-Mapping, Coverage, Kontext und CSV-Helfer liegen in
eigenen Dateien. Öffentliche Klassennamen und Signaturen bleiben gleich. Ein
Verhaltenstest schützt alle sieben CSV-/JSON-Ausgaben samt Kopfzeilen und Escaping.

- `EvalSetBenchmarkCase` enthält additiv `HoldingKey`, `ExpectedSeverity`, `EventId`
  und optional `MeterStart`/`MeterEnd`. Der normale Loader bleibt für alte Sets
  tolerant.
- Für eine Release-Abnahme muss `EvalSetReleaseDatasetValidator.LoadAndValidate`
  verwendet werden. Fehlende Bilder oder Haltungen sowie ungültige oder
  widersprüchliche Werte stoppen den Lauf. Eine `EventId` ist nur für Schäden
  verpflichtend; Nicht-Schäden brauchen keine künstliche Ereignis-ID.
- `EvalSetV2Builder` übernimmt die Felder und verlangt Severity sowie Ereignis-ID
  bei Schadensfällen.
- `EvalSetEventScorer` zählt ein Ereignis über mehrere Frames einmal und trennt
  Detect-Treffer vom bestandenen Gate. Der Ereignisschlüssel ist Haltung plus
  `EventId`, damit gleiche IDs in verschiedenen Haltungen getrennt zählen. Severity
  4/5 braucht mindestens 20 unabhängige Ereignisse. Wilson- und exakte
  95-Prozent-Fehlergrenzen werden ausgewiesen.
- `EvalReviewedDamageDataset` bindet die getrennte menschliche V1-Schadensreview
  nur bei passendem Kandidaten-SHA-256, vollständigen Entscheidungen und null
  Ereigniskonflikten an den Benchmark. `EvalReviewedDamageScorer` misst
  Schadenspräsenz, Fehlalarme, exakten Code, Hauptcode, Stufe und Ereignisse.
- `EvalSetBenchmark --review-file` misst dabei nur das Ollama-Bildmodell ohne
  YOLO-/DINO-/SAM-Hinweise. Das QualityGate wird ausdrücklich nicht als gemessen
  ausgegeben.
- `EvalSetBenchmark --review-file <Datei> --full-chain` fuehrt dieselben geprueften
  Bilder durch DINO, SAM, Qwen-Bildanalyse, Text-Code-Mapping und QualityGate.
  Ein fail-closed Client sperrt YOLO-Detect und YOLO-cls; der KB-Kontext bleibt
  ausgeschaltet. Der ausdrueckliche Pruefbefehl aktiviert nur fuer diesen Lauf das
  Code-Mapping, auch wenn der allgemeine App-KI-Schalter aus ist. CSV und JSON
  trennen erreichte Stufen, technische Fehler, exakte numerische Stufe, QualityGate
  sowie Erkennung und gruenes Gate je Ereignis.
- `RawVideoDetection.SeverityLevel` traegt additiv die exakte Stufe 1-5 aus dem
  `TemporalFindingDeduplicator`; das bestehende Textfeld `Severity` bleibt fuer
  Anzeige und Kompatibilitaet erhalten.
- Das reale 120er-Set ist noch nicht menschlich mit Severity und EventId nachgepflegt.
  AP 0.4 ist deshalb nicht abgeschlossen und die technische Grundlage ist noch keine
  Modellfreigabe.
- `tools/EvalVisibilityReview/start_eval_metadata_review.ps1` öffnet dafür einen
  lokalen Bild-Prüfplatz. Er zeigt nur BA-/BB-Schadensframes, schreibt Stufe,
  Ereignis-ID und optionalen Meterbereich atomar nach
  `C:\KI_BRAIN\eval_review\v1_event_metadata_review.json` und verändert das
  eingefrorene Eval-Set nie. Ein Zwischenstand wird nur bei gleicher SHA-256 der
  ursprünglichen `_candidates.json` fortgesetzt. Der Prüfplatz zeigt Code und
  Klartext aus dem aktiven VSA-Katalog. Die Stufe verändert weder Code noch
  Zustandsklasse; nur Ereignisse der Stufen 4/5 werden zusätzlich als wichtige
  Fälle ausgewertet. Pro Bild wird zuerst bestätigt, korrigiert oder festgehalten,
  dass kein passender BA-/BB-Schaden sichtbar ist. Korrekturen müssen aus dem
  aktiven Katalog stammen; Ausschlüsse brauchen keine Stufe oder Ereignis-ID.
  Widersprüchlich wiederverwendete Ereignis-IDs bleiben sichtbar offen.

## Build und Tests

Schneller Alltags-Build:

```powershell
dotnet build AuswertungPro.Dev.slnf -c Release --no-restore
```

Vor einem Commit die vollständigen Befehle aus `AGENTS.md` ausführen. Bei Sidecar-Arbeit
zusätzlich die CPU-Tests mit `sidecar\.venv\Scripts\python.exe -m pytest -m "not gpu" -q`
starten. SewerStudio nie automatisch beenden; nur einen hängen gebliebenen `testhost`
darf man vor einem Build beenden.

Fenster mit app-weiten `StaticResource`-Abhängigkeiten dürfen keine kurzlebige
`Application` im gemeinsamen UI-Testprozess erzeugen. Der Elterntest für
`BeobachtungenWindow` startet über `WpfIsolatedTestProcess` einen begrenzten
`vstest`-Kindprozess. Nur dort lädt `App.InitializeComponent()` die echten
Ressourcen. Ein zufälliger Receipt bestätigt den wirklich ausgeführten Fensterlauf;
ein falscher Filter, Timeout oder verschachtelter Kindprozess gilt nicht als Erfolg.

## Verbindlicher Pflegeablauf

1. `CLAUDE.md` vollständig lesen.
2. Jede konkrete Klasse, Signatur, Standardoption und Startaussage mit `rg`, Code und
   passenden Tests prüfen.
3. Ist-Zustand und Planung klar trennen; Vermutungen nicht eintragen.
4. Bei bestätigten Architekturänderungen diese Karte und das Prüfdatum aktualisieren.
5. Danach `quick_validate.py` für den Skill-Ordner ausführen.
