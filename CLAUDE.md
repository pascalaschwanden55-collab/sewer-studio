# SewerStudio — AI Sewer Inspection System

## Projekt-Kontext
- **App:** WPF / .NET 10, MVVM, Windows 11
- **Zweck:** Automatisierte Kanalinspektion, ~3000 Videos aus Kanal-TV-Exporten
- **Standards:** EN 13508-2, VSA-KEK; aktive Quelle: `vsa_kek_2020_catalog_manifest.json`
- **Entwickler:** Solo, kein kommerzielles Ziel
- **Hardware:** Intel Core Ultra 9 285K · ASUS RTX 5090 32GB · 64GB DDR5

## AI-Pipeline (Ist-Zustand, HEAD)
- C# steuert Geschaeftslogik, UI, Dedup, QualityGate und Persistenz.
- Sidecar `sidecar/sidecar/` liefert YOLO, Grounding DINO und SAM ueber HTTP.
- YOLO: Standard-Gewicht `yolo26m.pt` bzw. TensorRT-Engine, wenn vorhanden; COCO-Fallback `yolo11m.pt`, wenn eigene Gewichte fehlen und Fallback erlaubt ist.
- Qwen3-VL laeuft ueber Ollama fuer Bild-/Code-Analyse. GPU-Auto waehlt ab 24 GB VRAM `qwen3-vl:8b-q8`, sonst Default/Fallback `qwen3-vl:2b`; NIE auf qwen2.5 zurueckfallen. Keine Doku-Annahme zu automatischer 8B->32B-Laufzeit-Eskalation treffen.
- Grounding DINO: on-demand im Sidecar; Loader bevorzugt Swin-B (`grounding_dino_swinb`), Fallback Swin-T OGC (`grounding_dino_1.5`). Swin-B Stresstest 2026-06-20 bestanden (1000 Frames, 0 Timeouts, Forward ~107 ms, VRAM-Peak ~21,3 GB ≪ 29 GB) → behalten.
- SAM: **SAM 2.1** (`sam2.1_hiera_large.pt` unter `models/sam2.1/`, via `SAM2ImagePredictor`, box-getrieben). SAM-1 `vit_h` ist im Sidecar entfernt. SAM 3 nur deaktivierte Experiment-Option (`sam3_weights_path`, Default aus, kein Wrapper/keine Route); alte `models/sam3/`-Ablage entfernt.
- Bogen-Geometrie (`bend_geometry.py`, Fluchtpunkt/Bogen-Veto): im HEAD per Default DEAKTIVIERT (`bend_geometry_enabled=false`).
- Dedup/Merge: C#-framebasiert ueber `TemporalFindingDeduplicator` und `TemporalCodeVotingService`. Keine Annahme zu alten `UpdateActive`-Duplikaten treffen.
- Kein ByteTrack/OC-SORT und kein echtes Multi-Object-Tracking in HEAD.
- Der YOLO-Trainings-Export ist seit AP 0.3 plan-gesteuert: C# erzeugt vor dem
  Sidecar-Healthcheck genau einen unveraenderlichen Plan. Sidecar und lokaler
  Ausfuehrer schreiben nur noch diesen Plan und treffen keine eigene Klassen-,
  Split-, Quarantaene- oder Dateinamenentscheidung.

## Architektur-Prinzipien (NICHT brechen)
- Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung
- Kein grosses Refactoring ohne explizite Diskussion
- Laptop-Mode / Workstation-Mode Hardware-Abstraktion erhalten
- VRAM-Budget: max 29GB stabil, niemals alle Modelle gleichzeitig
- QualityGate Green/Yellow/Red muss immer durchlaufen
- Neue Workflow-/Orchestrierungsklassen (Request/Actions/Result) nach
  `src/AuswertungPro.Next.Application/UseCases/` statt nach `UI/Ai/`; der UI/Ai-Bestand
  ist per `UiAiFreezeArchitectureTests` eingefroren (Referenzbeispiel: `CodingModeBackgroundServicesWorkflow`).

### Checkliste bei jedem neuen Service / Tool (vor dem Commit pruefen)
1. **Interface + eigener Service:** Neue Logik als eigener Service mit Interface, nicht in bestehende Klassen quetschen. Neue Workflow-/Orchestrierungsklassen (Request/Actions/Result-Muster) gehoeren nach `src/AuswertungPro.Next.Application/UseCases/`, nicht nach `UI/Ai/` (eingefroren per `UiAiFreezeArchitectureTests`).
2. **Schichten trennen:** Geschaeftslogik in C# (nicht in UI-Code, nicht im Sidecar). UI ruft ViewModel/Service, nie direkt Infrastruktur.
3. **Registrierung:** Service im `ServiceProvider` (DI) eingetragen, kein `new` verstreut im Code.
4. **Fokussierter Test:** Mindestens ein Test fuer die Kernlogik (Parser/Pipeline/ViewModel/QualityGate). Keine riskante Logik ohne Test.
5. **Budget & Gate:** VRAM-Budget (max 29GB) nicht gebrochen, QualityGate laeuft weiter durch.
6. **Klein bleiben:** Kein grosses Refactoring am Bestand ohne Rueckfrage — neues Feature additiv bauen.

## Eigentuemer-Dossiers
- `Export_Vorlage/Eigentuemerdossier.docx` ist die verbindliche Word-Geometrie. Seitenraender,
  Abstaende, Zeilenhoehen, Tabellen, Deckblatt, Logo, Wappen und Fusszeile werden beim
  Ersetzen der Platzhalter nicht neu berechnet oder umgebaut.
- `DossierPdfAssemblyService` wandelt die fertige Word-Datei zuerst mit Microsoft Word
  und ersatzweise mit LibreOffice in PDF um. `DossierWordPdfConverter` sucht LibreOffice
  zuerst gebuendelt unter `LibreOffice/program/soffice.exe` neben SewerStudio, danach in
  den normalen Windows-Installationsordnern und im `PATH`. LibreOffice laeuft kopflos mit
  einem eigenen Temp-Profil; die Kundendatei bleibt unveraendert. Scheitern beide Wege,
  darf weiterhin kein scheinbar vollstaendiges Teil-PDF nur aus Beilagen entstehen.
- `IDossierConditionClassPdfService` liefert das feste einseitige A4-Erklaerblatt.
  Produktiv liest `DossierConditionClassPdfTemplateService` die persoenlich freigegebene
  Datei `Export_Vorlage/Zustandsklassen_Eigentuemer_Dossier.pdf` genau einmal, prueft
  Lesbarkeit, eine Seite und die Pflichtblatt-Marke und gibt ihre Bytes unveraendert weiter.
  Dieselben Bytes werden fuer Vorschau, Gesamt-PDF und die eigene Datei im neu angelegten
  Liegenschaftsordner verwendet. `DossierConditionClassPdfService` bleibt der reproduzierbare
  Erzeuger fuer Tests und kompatible direkte Aufrufer. Es beschreibt Z0 bis Z4 und zeigt je
  Klasse rechts eine kompakte zeitliche Orientierung von sofort bis zur naechsten
  Zustandsbeurteilung. Die WPF-freien Texte liegen in `DossierConditionClassDefinitions`;
  die Farben stammen aus `ExcelReportStyle`. Logo und Wappen sind optionale Vorlagen-Assets.
  Der Grundlagenkasten fasst VSA
  "Zustandsbeurteilung von Entwaesserungsanlagen", Kapitel 2.2-2.3, zusammen: Grundlage
  sind vollstaendige Bauwerksdaten und korrekt erfasste Befundcodes; Fachpersonen pruefen
  Daten und Ergebnis. Schutzbereich, Nutzung, Grundwasserlage und Netzbedeutung veraendern
  nur die Sanierungsdringlichkeit, nicht die Zustandsnote. Die Zeitspanne rechts ist deshalb
  ausdruecklich nur als Orientierung bezeichnet. Die Klassenzeilen enthalten die
  fachlichen VSA-Beschreibungen samt typischen Defizitbeispielen. Ein Strich ist der
  getrennte Status `nicht berechnet` und darf niemals als Z4 ausgegeben werden. Zustandsklasse
  und Dringlichkeitszahl bleiben getrennte Skalen ohne feste 1:1-Zuordnung. Das Erklaerblatt
  zeigt deshalb keine numerischen Dringlichkeitsbereiche und ordnet Z4 keinem `NULL`-Wert zu.
- `IDossierHoldingListPdfService` und `IDossierShaftListPdfService` rendern die
  freigegebenen A4-Layouts der Haltungs- und Schachtliste. Die zugehoerigen ModelBuilder
  verbinden Dossierkopf und `DossierSnapshot`; die Renderer stellen nur dar und schreiben
  nie selbst Dateien. Tabellenkoepfe werden auf Folgeseiten wiederholt. Haltungszustand und
  Nutzungsart sind farblich gekennzeichnet; die Schachtliste zeigt Nummer, Strasse, Funktion
  und Zustandsklasse. Fehlende Angaben heissen `nicht erfasst`.
- `IDossierComponentListExportService` veroeffentlicht eine Liste erst nach dem bewussten
  Klick auf `Haltungsliste erstellen` oder `Schachtliste erstellen`. Er bindet das Ziel an
  den ausgewaehlten Liegenschaftsordner, schreibt ueber eine Temp-Datei und waehlt bei einer
  vorhandenen Liste einen freien Namen. Keine bestehende Datei wird ersetzt. Diese
  Dateien im Liegenschaftsordner sind der getrennte Weg fuer eine einzelne Liste; das
  Gesamt-PDF verwendet sie NICHT, sondern rendert seine Listen selbst neu.
- `DossierPdfPackageComposer` wird von Ausgabe und Vorschau gemeinsam verwendet. Die feste
  Reihenfolge ist Word-Dossier, einseitiges Erklaerblatt, Haltungsliste, Schachtliste und
  danach die normalen Beilagen. Der Composer prueft, dass der Erklaeranhang genau eine
  Seite hat und alle selbst erzeugten Blaetter wirklich eingefuegt wurden. Seine
  Arbeitsdateien liegen nur im Temp-Ordner; Kundenoriginale,
  Beilagenordner und Manifest bleiben davon unberuehrt. Das Erklaerblatt wird auch ohne
  weitere Beilagen erzeugt und erscheint in der Vorschau als automatisch erzeugte, nicht
  bearbeitbare Beilage. Unsichtbare eindeutige Seitenmarken verhindern
  die Verwechslung mit Kundenseiten, die denselben sichtbaren Titel tragen: Ihre drei
  Werte und die zugehoerigen Beschriftungen liegen WPF-frei in
  `DossierMandatoryPageMarkers`, damit Ausgabe, Vorschau und Seitenauswahl dieselbe Regel
  lesen. Die Seitenauswahl kennzeichnet jedes so erkannte Blatt namentlich als
  Pflichtblatt und kann es nicht abwaehlen; die Ausgabe erzwingt das zusaetzlich
  unabhaengig von der Oberflaeche.
- `DossierComponentListPdfRenderer` erzeugt Haltungs- und Schachtliste fuer Gesamt-PDF und
  Vorschau frisch aus dem aktuellen `DossierSnapshot` — genau wie die Protokolle vorher neu
  gesammelt werden. Er schreibt keine Datei: Die Bytes gehen direkt in den Composer, im
  Beilagenordner entsteht dadurch nichts. Ohne Haltungen entfaellt die Haltungsliste, ohne
  Schaechte die Schachtliste; ein Blatt mit blossem Tabellenkopf wird nie erzeugt.
  `IDossierPdfAssemblyService.AssembleAsync` besitzt dafuer eine Ueberladung mit dem
  ganzen `DossierExportRequest`; der alte Weg nur ueber den Ordnerpfad bleibt ohne Listen
  bestehen. Die Erfolgsmeldung nennt die tatsaechlich enthaltenen Listen.
- `Alles zu einem PDF` sammelt vor der Umwandlung die Protokolle aller aktuell im Dossier
  gewaehlten Haltungen und danach aller Schaechte. Bei Haltungen wird das Original und nur
  ersatzweise das SewerStudio-Protokoll verwendet; Schaechte verlangen ihr Original. Fehlt
  ein ausgewaehltes Protokoll, wird kein unvollstaendiges Gesamt-PDF erzeugt.
- `DossierAttachmentCollector` kennzeichnet nur seine eigenen Protokollkopien im
  Beilagenordner ueber `.sewerstudio-dossier-beilagen.v1.json`: direkter PDF-Dateiname,
  SHA-256, Typ und Objekt. Eine abgewaehlte, unmittelbar vor dem Entfernen nochmals
  hash-gepruefte automatische Kopie wird aus der Ausgabe genommen. Unbekannte, manuelle,
  nachtraeglich veraenderte oder aus der Zeit vor dem Manifest stammende PDFs gelten
  fail-closed als manuell und werden weder ersetzt noch geloescht. Kopien, generierte PDFs
  und Manifest werden ueber eindeutige Temp-Dateien veroeffentlicht. Ein benannter
  `DossierAttachmentFolderLock` serialisiert den gesamten Lauf je kanonischem
  Beilagenordner auch zwischen Prozessen. Eine alte eigene Kopie wird zuerst atomar unter
  einem eindeutigen Sicherungsnamen weggestellt und erst dort erneut hash-geprueft; die neue
  Kopie darf danach nur ohne Overwrite an den freien Zielpfad. Das Manifest uebernimmt nur
  den beim Publizieren bekannten Hash und prueft den Zielinhalt unmittelbar vor seinem
  Schreiben nochmals. Abgewaehlte eigene Kopien gehen mit derselben Move-first-Regel in
  einen versteckten Quarantaeneordner. Ist eine bisher eigene PDF voruebergehend gesperrt, bleibt ihre
  Eigentumskennzeichnung fuer einen spaeteren sicheren Versuch erhalten. Scheitert das
  abschliessende Manifest-Schreiben, werden die in diesem Lauf veroeffentlichten PDFs nur
  bei weiterhin passender SHA-256 auf den vorherigen Stand zurueckgesetzt. Kundenoriginale
  werden immer nur gelesen.
- `IDossierOutputPreviewService`/`DossierOutputPreviewService` erzeugt die Vorschau ueber
  denselben Word-Export und denselben Word-/LibreOffice-PDF-Wandler wie die Ausgabe. Word-
  und PDF-Arbeitsdateien liegen in einem eindeutigen System-Temp-Ordner; Dossier und Gebiet
  werden tief kopiert, relative Planpfade nur in dieser Kopie aufgeloest und der Kundenordner
  bleibt unveraendert. Aus dem echten Beilagenordner werden nur manuelle beziehungsweise
  nicht sicher als automatisch erkannte PDFs in den Temp-Stand kopiert. Dort sammelt
  `DossierAttachmentCollector` die Protokolle aller aktuell gewaehlten Haltungen und danach
  aller Schaechte neu; die Vorschau fuegt ausschliesslich diesen kurzlebigen Stand in
  Dateinamenreihenfolge an. So verschwinden abgewaehlte automatische Protokolle sofort aus
  Vorschau und Gesamt-PDF, manuelle Beilagen bleiben sichtbar, und eine Vorschau legt im
  echten Projekt keine Datei an, ersetzt nichts und loescht nichts.
- `DocxFieldMarkerWriter` setzt fuer jedes bearbeitbare Dossierfeld eine deterministische,
  hoechstens 40 Zeichen lange Word-Textmarke. Word und LibreOffice exportieren diese Marken
  als benannte PDF-Ziele. Da `PdfMergeService` beim Anfuegen von Beilagen nur Seiten kopiert,
  liest `DossierOutputPreviewService` die Ziele vorher aus der reinen Word-PDF und reicht sie
  getrennt mit der zusammengefuehrten Vorschau weiter. So bleibt die Feldzuordnung auch mit
  Beilagen exakt.
- Leere Texte der elf bekannten Standardthemen bleiben im Kundendokument leer; nur ein leerer
  frei angelegter Zusatzpunkt wird als `unbekannt` kenntlich gemacht. Der Standardtext der
  Ausgangslage setzt `Gebiet_Perimeter` nur bei vorhandenem Gebietsort ein und bleibt sonst
  als vollstaendiger deutscher Satz erhalten.
- `WindowsDossierPreviewPageRasterizer` zeichnet jede echte PDF-Seite. Damit stammen
  Seitenzahl, Blattformat, Abstaende, Umbrueche, Tabellen, Farben, Bilder, Logo und Fusszeile
  nicht mehr aus einer WPF-Nachbildung. Nach 300 ms Schreibpause wird die Ausgabe neu
  erzeugt; das alte Blatt wird sofort gesperrt und durch einen Aktualisierungshinweis
  ersetzt. Erst eine erfolgreich gerasterte Seite des neuesten Ausgabestands gibt Klicks
  und `Uebernehmen` wieder frei. Ein veraltetes oder fehlgeschlagenes Zwischenergebnis wird
  nie eingeblendet. `PdfPig` liefert die
  Wortlagen fuer transparente Klickflaechen, sodass ein Klick auf sichtbaren Text weiterhin
  direkt zum passenden Editor springt. `DossierOutputPreviewInteractionMapper` haelt die
  Seiten-/Editor-Zuordnung und Textziele WPF-frei; Treffer werden auf die wirklich sichtbare
  PDF-Seite begrenzt. `DossierOutputPreviewHitMatcher` erkennt auch abweichende PDF-Wortgrenzen
  und lange Tabellenzellen; gleiche Texte verschiedener normaler Felder werden ohne
  geometrischen Beleg nicht nach Katalogreihenfolge geraten. Nur echte Geschwister derselben
  Wiederholspalte werden in Zeilenreihenfolge verteilt. `DossierOutputPreviewHitAreaBuilder`
  fasst sichere Worttreffer zusammen. `DossierOutputPreviewTableCellMapper` verarbeitet die
  PDF-Blaetter dagegen gemeinsam in Dokumentreihenfolge: Er liest Spalten und erste
  Zeilenoberkante einmal am eindeutigen Tabellenkopf, traegt den naechsten globalen
  `RowIndex` auf Folgeseiten weiter und beginnt dort am echten Vorlagenrand. Die Word-Vorlage
  braucht dafuer keinen neu erzwungenen Wiederholungskopf. Jede sicher erkannte physische
  Tabellenzeile erhaelt fuer gefuellte UND leere Werte die ganze Zellflaeche. Bei einer
  unsicheren Zeile ersetzt der Mapper keine bisherigen Wortziele. Der bestehende
  `DossierOutputPreviewEmptyRowCellMapper` bleibt der konservative Rueckfall fuer ein
  einzelnes Blatt. Telefon, Mail, Objektbewohner und ihre bearbeitbaren Beschriftungen bleiben
  kleine Textziele innerhalb der gemeinsamen Eigentuemerzelle und werden nicht als erfundene
  Spalten behandelt. `DossierOutputPreviewEmptyFixedCellMapper`
  ergaenzt die leere Aktennotiz-Zelle und nur den oberen Eingabeabsatz der Rueckmeldung;
  Punktlinien, Ort/Datum und Unterschriften bleiben eigene Vorlageninhalte. Mehrdeutige
  Tabellen werden nicht geraten. `DossierPreviewTableRow.MinimumHeightPx` bewahrt dabei die
  Mindesthoehe der Word-Zeile; mehrzeilige, vollstaendig erkannte Nachbartexte erweitern
  die Hoehe. So ist die ganze leere Zelle anklickbar und nicht nur ein Streifen neben dem
  Text. Die im Word sichtbaren Grundzeilen fuer Aenderungswesen, Eigentuemer und Themen
  besitzen rechts sofort alle zugehoerigen Eingaben; ein normaler Zellklick zeigt die ganze
  Zeile und setzt den Schreibfokus nur in die gewaehlte Zelle. Die gemeinsamen bearbeitbaren
  Eigentuemer-Beschriftungen bleiben dabei selbst sichtbar, statt von einer einzelnen
  Zeilenkarte verdeckt zu werden. Mehrere titellose Themen-Altdaten werden in Listenreihenfolge
  an ihre jeweils eigene Editorzeile gebunden. Erhaelt eine Grundzeile einen Titel wie
  `Schaeden`, erscheint ihre passende Fachaktion (`Import aus Liste`) sofort. Unbenutzte
  Eingabe-Grundzeilen entfernen `DossierChangeRows`, `DossierOwnerRows` und
  `DossierTopicRows` vor dem Uebernehmen; sie werden nicht als fachliche Eintraege in
  `dossiers.json` gespeichert.
  `DossierTextUndoController` stellt im Feldbereich die zentralen Pfeile fuer
  Rueckgaengig und Wiederholen bereit. Sie verwenden die native Texthistorie des zuletzt
  aktiven Textfelds und funktionieren dadurch auch fuer dynamisch erzeugte Zeilen; beim
  Neuaufbau wird ein entferntes Textfeld als Ziel verworfen. Das Vorschaufenster
  orchestriert nur.
  Original-Beilagen erscheinen als eigene Gruppe und sind bewusst nur lesbar.
- `DossierTextStyleRange` speichert Schriftfarbe, Fett, Kursiv und Unterstrichen als
  Zeichenbereiche. Themen verwenden `DossierTopicRow.StyleRanges`, andere Textfelder
  `DossierDefinition.FieldStyles`; das alte `ColorHex` bleibt fuer bestehende Projekte lesbar.
- `DossierTopicTextFormatting` ist die WPF-freie Bereichs-, Platzhalter- und
  Serialisierungsregel. Vorschau und Word-Export muessen dieselben Bereiche verwenden,
  auch wenn ein bearbeitbares Feld in einer beschrifteten Zeile wie `Datum: {{Datum}}`
  steht.
- `DossierPreviewTarget` adressiert anklickbare Vorschautexte fachlich ueber Feld,
  Zeile und Spalte statt ueber feste Pixelpositionen. Die genaueste vorhandene Adresse
  fuehrt direkt zum passenden Editor; auch geaenderte Vorlagentexte bleiben anklickbar.
  Fusszeilen-Platzhalter werden beim Lesen der Word-Vorlage jeder Dossierseite als
  gemeinsame Felder zugeordnet. Zusatzpunkt-Titel und deren Seitenzahl sowie Thementitel
  und Bemerkung besitzen getrennte Klickziele. Zusatzpunkt-Titel speichern ihre
  Zeichenformatierung in `DossierTocAttachment.TitleStyles` und geben sie an Word weiter.
- `DossierPreviewNavigation` ordnet die Vorlagenseiten den Editoren zu; die sichtbare
  Navigation verwendet dagegen die tatsaechlichen Seiten des erzeugten PDF. Fortsetzungs-
  seiten bleiben beim erkannten Kapitel; rechts erscheinen weiterhin nur die Felder der
  zugeordneten Seite.
- Inhaltsverzeichniszeilen werden strukturell aus dem echten Word-Feld gelesen:
  `DossierPreviewTocEntry` trennt Nummer, bearbeitbaren Kapiteltitel und PAGEREF-Seitenzahl.
  `DocxTocEntryEditor` ersetzt nur den Titel; Nummer, Tabulatoren und Seitenzahl bleiben
  Word-Felder. Der gleichnamige Kapitelkopf wird weiterhin ueber `TextOverrides` geaendert,
  damit eine spaetere Word-Aktualisierung den eigenen Titel nicht zuruecksetzt.
- Zusaetzliche Verzeichnispunkte stehen gemeinsam in
  `DossierDefinition.TocAttachments`; jedes `DossierTocAttachment` verbindet Titel und
  Seitenzahl untrennbar. Schema 8 uebernimmt die zwei alten parallelen Listen einmalig und
  entfernt sie danach. `DossierTocAttachments` nummeriert erst hinter den in der Vorschau
  sichtbaren Kapiteln und schlaegt bei Altdaten die naechste freie Seite vor; Titel und
  Seitenzahl bleiben je Punkt frei bearbeitbar.
  `DocxTocAttachmentWriter` schreibt jeden Punkt direkt hinter den letzten echten
  Word-Eintrag als eigenen Absatz in dessen Format samt rechtem Seitenzahl-Tabulator.
  `DocxTocLayoutFormatter` entfernt nur im Inhaltsverzeichnis die alte gesperrte
  Zeichenweite und verdichtet die Zeilen; Arial, Punktlinie und rechte Seitenzahl
  bleiben erhalten. `DossierPreviewTocLayout` verwendet in der Vorschau dieselben Masse.
  Die Vorschau verwendet dasselbe Zeilenraster samt Punktlinie, zaehlt ausgeblendete
  Kapitel nicht mit und adressiert jeden Zusatzpunkt einzeln fuer den direkten Klick zum
  Editor. `DossierTocChapterPageClickMapper` erkennt auch von PDFPig mit Punktlinie und
  Seitenzahl verklebte Titel fail-closed und ordnet getrennt gelieferte Seitenzahlen anhand
  derselben Inhaltsverzeichniszeile eindeutig ihrem Seitenzahlfeld zu. Vorhandene Titel und
  `+ Punkt ergaenzen` stehen rechts im selben Abschnitt, damit der Knopf auch nach einem
  direkten Titelklick sichtbar bleibt.
- Die Dossier-Vorschau startet mit einer vollstaendig eingepassten Seite.
  `DossierPreviewFitCalculator` berechnet den Zoom aus der echten PDF-Blattgroesse und der
  Vorschauflaeche;
  der Benutzer kann danach manuell vergroessern und mit `Ganze Seite` zurueckkehren.
- In `Schäden` und `Sanierungskonzept` kopiert `Import aus Liste` die aktuelle,
  fortlaufend nummerierte Bauteilliste als normalen Dossiertext: zuerst alle Haltungen,
  danach alle Schächte. Die Kürzel `Z0` bis `Z4` tragen dabei dieselbe Zustandsklassenfarbe
  wie Haltungen und Schächte; nur das Zustandskürzel, nicht die ganze Zeile, wird gefärbt.
  Diese Kopie ist frei bearbeitbar und aendert weder die Auswahl noch Projektdatensaetze.
  `DossierTopicComponentListComposer` loest nur noch alte
  `Bauteile_Text`-/`Haltungen_Text`-/`Schaechte_Text`-Marken kompatibel auf.
- Bearbeitete Beschriftungen und Ueberschriften speichern ihre Zeichenformatierung unter
  `DossierTopicTextFormatting.LiteralStyleKey(...)` ebenfalls in `FieldStyles`; Vorschau
  und Word wenden Farbe, Fett, Kursiv und Unterstrichen gleich an. Der Export merkt sich
  diese Benutzereingaben als Literalbereiche, damit darin geschriebener Text wie
  `{{Datum}}` nicht nachtraeglich als Vorlagen-Platzhalter ausgewertet wird.
- Die frueher erzeugten Eigentuemer-Praefixe `Tel.:`, `Mail:` und `Objektbewohner:` sind
  ueber `DossierOwnerCellLabels` ebenfalls bearbeitbare, formatierbare Dossierfelder. Der
  Word-Export setzt Beschriftung und Wert mit getrennten Zeichenbereichen in dieselbe
  physische Eigentuemerzelle; das gespeicherte Schema bleibt kompatibel.
- `DossierTopicTitleEditing` speichert eine eigene Fassung eines Thementitels unter einem
  stabilen Feldschluessel im einzelnen Dossier. `DossierTopicResolver` behaelt den
  urspruenglichen Gebietstitel als Quelle, waehrend Vorschau und Export den eigenen Titel
  samt Zeichenformatierung verwenden; die Gebietsvorgabe wird nicht umbenannt.
- Dossiertext wird in Vorschau und Word direkt als Arial ausgegeben. Schriftgroessen,
  Absatzabstaende und Tabellenmasse stammen weiterhin unveraendert aus der Vorlage.
- Der bekannte manuelle Seitenumbruch unmittelbar vor `Aenderungswesen:` wird beim
  Export gezielt entfernt, weil das volle Deckblatt bereits selbst auf Seite 2 umbricht.
  Andere Seitenumbrueche der Vorlage bleiben unveraendert.
- Der Werkleitungsplan verwendet weiter den kompatiblen Vorlagenschluessel
  `Uebersichtsplan`. `WindowsPdfPlanImageConverter` uebernimmt JPG, JPEG, PNG, BMP oder
  die erste PDF-Seite immer als neue, gepruefte PNG-Kopie. In der Vorschau liegen Import,
  Drehen und Zuschneiden in der eigenen `DossierPlanWorkSession`; erst `Uebernehmen`
  reicht die letzte Datei an `DossierPlanPublicationService` weiter. Der Dienst prueft
  Projektgrenze und Junctions ueber `ProjectWritePathGuard` und veroeffentlicht unter
  einem freien Namen. Der Hash-Beleg bleibt in `DossierPreviewChoice`, bis das
  Dossier-Dokument erfolgreich gespeichert ist. Verschwindet das Dossier oder scheitert
  das Speichern, wird nur die gerade erzeugte, unveraenderte PNG zurueckgenommen.
  `Verwerfen` entfernt nur den eigenen Temporaerordner. Quelle und vorhandene
  Dossierdateien werden nie ueberschrieben oder geloescht. Auf der echten Planseite zeigt
  die Vorschau eine sichtbare Foto-Schaltflaeche. Sie springt direkt zum vorhandenen
  Planeditor mit Dateiwahl, Drehen und Zuschneiden; es gibt keinen zweiten Importweg.
- Das Planbild wird proportional innerhalb der Referenzflaeche (maximal ca. 15 x 21,5 cm)
  eingepasst; der aeussere Word-Rahmen behaelt dabei immer die volle Vorlagenhoehe.
  Damit bleibt ein JPG unverzerrt, Folgekapitel ruecken bei Querformat nicht hoch und
  es entsteht trotzdem keine Zusatzseite. Eine gespeicherte Breite wird auf die 15 cm
  der Vorlage begrenzt.
  Ist kein lesbarer Plan gewaehlt, entfernt der Bildfueller den ganzen
  Platzhalterabsatz samt grossem schwebendem Vorlagenrahmen; sonst liegt dieser
  Rahmen in Word ueber den folgenden Kapiteln. Ein bewusst leerer Plan erzeugt
  dabei keinen Fehlhinweis.
- Jede Zeile im Dossier-Cockpit behaelt die eindeutige `HoldingId`. Das Rechtsklick-Menue
  routet Video, Originalprotokoll und den Sprung zur Datenseite ueber
  `DossierHoldingActionController`; die Seite selbst enthaelt nur die Zeilenauswahl.
  `DossierHoldingActionFactory` verwendet dafuer die bestehenden
  `DataPageVideoPlaybackController`-, `DataPageOriginalPdfController`- und sicheren
  Pfadaufloesungswege. `ShellViewModel.NavigateToHolding` ist der gemeinsame direkte
  Sprung fuer Dossier und Karte und selektiert den Projektdatensatz im Menue `Haltungen`.
- Auch `DossierShaftRow` behaelt seine eindeutige `ShaftId`. Das Schacht-Rechtsklick-Menue
  delegiert Protokolloeffnung und Navigation an `DossierShaftActionController`.
  `DossierShaftActionFactory` verwendet fuer die PDF denselben
  `SchaechteFileActionController` wie die Seite `Schaechte`; die Seite selbst waehlt nur
  die rechts angeklickte Zeile aus. `ShellViewModel.NavigateToShaft` oeffnet `Schaechte`
  und selektiert dort den Originaldatensatz.
- Ein linker Klick auf eine Haltungs- oder Schachtzeile im Dossier meldet den sichtbaren
  Namen an dieselbe `QgisBridgeSelection` wie die Seiten `Haltungen` und `Schaechte`.
  Auch ein erneuter Klick auf dieselbe Zeile erhoeht den Auswahlstempel und loest den
  QGIS-Zoom nochmals aus; die eigentliche Zoomlogik bleibt in der QGIS-Bruecke.
- Die Reihenfolge der Liegenschaften ist die Reihenfolge von `DossierDocument.Dossiers` in
  `dossiers.json`. Das Cockpit sortiert nicht mehr still alphabetisch. `Nach oben` und
  `Nach unten` verschieben die Auswahl um genau eine Stelle und speichern sofort; bei einem
  Speicherfehler wird die vorige Reihenfolge wiederhergestellt.
- `DossierFileStore` legt den eigenen Ordner jeder neu gespeicherten Liegenschaft sofort
  direkt unter `<Projekt>\Dossiers` an. Die gleiche Regel gilt fuer Einzel- und Stapelanlage.
  In jeden dabei neu erzeugten Ordner kommt nur die freigegebene, bytegleiche
  `Zustandsklassen_Eigentuemer_Dossier.pdf`. Dynamische Haltungs- und Schachtlisten werden
  bewusst nicht beim Anlegen oder Laden erzeugt, damit zuerst die Projektdaten korrigiert
  werden koennen. Sie entstehen spaeter ueber die beiden Erstellen-Schaltflaechen.
  Ein bestehender Ordner oder eine vorhandene Datei wird nie ersetzt. Scheitert der
  anschliessende Save, entfernt der Store nur sein weiterhin hashgleiches Erklaerblatt
  und danach den leeren neuen Ordner; eine inzwischen veraenderte Datei bleibt unangetastet.
  Alle Store-Instanzen
  teilen dafuer pro laufendem Programm eine Sperre. Die Hashpruefung und Loeschmarkierung
  erfolgen unter Windows am selben exklusiven Dateihandle, sodass kein fremder Ersatz
  zwischen Pruefung und Ruecknahme geloescht werden kann.
  Beim Laden einer vorhandenen, lesbaren `dossiers.json` zieht der Store fehlende,
  bereits benannte Liegenschaftsordner nach, ohne die JSON-Datei zu veraendern.
  Ordnernamen duerfen diese Ebene nicht verlassen. Scheitert das anschliessende Speichern,
  werden nur in diesem Lauf neu erzeugte und weiterhin leere Ordner zurueckgenommen;
  bestehende Ordner und Benutzerdateien bleiben unangetastet.

## Aktueller Pipeline-Ablauf
1. UI/Service startet Analyse ueber `VideoAnalysisPipelineService`, `SingleFrameMultiModelService` oder `VideoFullAnalysisService`.
2. C# ruft den Sidecar ueber `VisionPipelineClient` auf.
3. Sidecar verwaltet Modell-Locks und GPU-Slots in `sidecar/sidecar/gpu_manager.py`.
4. Multi-Model-Pfad: YOLO -> DINO -> SAM -> Quantifizierung -> optional Qwen.
5. C# mappt VSA-Code, dedupliziert framebasiert und laesst `QualityGateService` laufen.

## Geplant / nicht implementiert (nicht als Ist-Zustand behandeln)
- `ByteTrack` / `OC-SORT`: kein Tracking im aktuellen HEAD.
- `DetectionAggregator` / meterbasierter Merge-Radius / echtes Multi-Object-Tracking: nicht im aktuellen HEAD. Temporal Voting existiert als `TemporalCodeVotingService`, kein separater Aggregator.
- `InferenceOrchestratorService`: keine C#-Klasse im aktuellen HEAD; GPU-Slots liegen im Sidecar.
- Einen produktiven `KbDeduplicationService` gibt es aktuell nicht. Similarity-Checks im
  Trainings-/Review-Kontext nicht mit dem Retrieval-Ranking verwechseln.
- Automatische 8B->32B-Laufzeit-Eskalation: nicht als implementiert annehmen.
- Das aktive Detect-Altmodell (yolo26m, 2026-04-11) ist seit 2026-07-25 als NICHT
  qualifiziert markiert (`sidecar/models/model_qualification.json`, BBox-Kollaps,
  alter Trainingsdatensatz fehlt). `/health` meldet den Sidecar als `degraded` samt
  `detector_qualification`. Nur ein ausdrueckliches `qualified=true` gibt das
  Standardmodell frei. Bei false, fehlendem Feld oder Lesefehler sperrt das Training
  Studio den Fototest; Standard-Endpunkt und Warmup laden/verwenden YOLO nicht.
  Die Freigabedatei bindet PT, TensorRT-Engine und ONNX jeweils an Dateiname und
  SHA-256; Abweichungen sperren fail-closed. Gewichte bleiben unveraendert erhalten;
  das getrennte BCC-Testmodell ist davon unberuehrt.
- `training/scripts/model_collapse_check.py` ist das schreibfreie Kollaps-Pruefwerkzeug:
  Box-Statistik (Paar-IoU, Streuung), IoU gegen Gold-Boxen, Aktivierungen auf dem
  Negativ-Pool, optional mAP via `--dataset`. Ein echter Geometrie-Kollaps ergibt
  `FAIL` (Exit 1); Inferenzfehler, zu wenig Bilder/Treffer oder unter 20 %
  Detektionsrate ergeben `INCONCLUSIVE` (Exit 2), niemals einen falschen PASS.
  `PASS` heisst nur „kein BBox-Kollaps", keine Qualitaetsfreigabe. Pruefbestand unabhaengig
  (`--images-dir`, Default eval_set/images), einheitliche Aufloesung (`--imgsz` 1280),
  Bericht unter `<KnowledgeRoot>/training/reports`. Der Altmodell-Kollaps ist belegt;
  ein Kandidat bleibt ohne den ganzen Release-Weg immer `not_deployed`.
- Batch-Video und Player-Einzelframe verwenden YOLO nur bei ausdruecklichem
  `qualified=true`. Bei false, fehlendem Feld oder Health-Lesefehler wird YOLO weder
  als Frame-Filter noch als Confidence-Beweis verwendet. DINO/SAM laufen ohne
  YOLO-Gate weiter; Health-Ampel und Ergebnis bleiben `Degraded`/orange und verlangen
  eine manuelle Pruefung. Der Ollama-only-Pfad traegt die Kennzeichnung nicht.
- `training/scripts/gold_stock_audit.py` prueft den Goldbestand schreibfrei
  (persoenliche Freigabe, lesbares Bild, randgueltige Box, echte Maskenpixel in der
  Hand-Box, Katalogcode, Bildhash und komplette Eval-Haltung). Haltungsnummern werden
  normalisiert; identische Bildbytes verbinden betroffene Haltungen zu einer
  gemeinsamen Split-Gruppe. Ein Pilot braucht >= 30 Samples sowie Train und Val/Test.
  Platzhalter-Beschreibungen sind fuer das reine BBox-Training zulaessig und werden
  nur als `kb_text_offen` markiert — KB-Index und Qwen-Retrieval sperren sie.
  Der aktuelle, an Register und Exportdatensatz gebundene Bericht
  `gold_stock_audit_20260803_191255_470.json` hat SHA-256
  `5d036fd74dbdc6e80dae1ca2600b648fc99073f9b8a0157bee5da1a6027a0987`.
  Er prueft 1391 Eintraege: 14 Drafts werden uebersprungen, 24 Kandidaten werden
  verworfen und 1353 sind verwendbar. Der Haltungssplit umfasst 961 Train-,
  264 Validation- und 128 eingefrorene Testinstanzen. Gebunden ist der dabei
  gepruefte `training_samples.json`-Snapshot mit SHA-256
  `fd5340ce35d5b317273e9d34e340d70e319448c78c23d640ec682b94fb9c6a1b`.
  Der Audit vom 2026-08-02 bleibt der unveraenderte Beleg des weiterhin
  `not_deployed` stehenden vorigen Kandidaten.
- `training/scripts/repair_gold_holding_ids.py` repariert persoenlich bestaetigte
  `foto_*`-Goldsamples nur ueber einen eindeutigen bytegleichen SHA-256-Treffer in
  einem Quellenordner. Standard ist ein schreibfreier Prueflauf. `--execute`
  verlangt eine ruhige App/SQLite-DB, sichert JSON und SQLite konsistent und
  aktualisiert Sample, Signatur, Notiz, Teacher-Haltung und `Samples.CaseId`
  gemeinsam; Kundenbilder werden nie veraendert.
- `training/scripts/prepare_detect_gold.py` baut daraus fail-closed das getrennte
  Mehrklassen-Register `DETECT_ALL`. Es verlangt den expliziten Audit, die daran
  gebundene persoenliche Codefreigabe, unveraenderte Bild-/Sample-Hashes und nur
  streng reviewte `all_classes_clear`-Negative. Das aktuelle Register enthaelt
  894 Goldinstanzen (710 Train, 184 Validation) und 9 strikte Negative (7/2).
  Der gebundene Exportplan
  `ea8e715f3c4cee8a5e43adae35c734e4c8890be389ab0bba91148126d785bfc2`
  fuehrt gleiche Bildbytes zusammen und enthaelt deshalb 852 Bilder
  (686 Train, 166 Validation) mit 894 Boxen. Belegt sind 13 der 15 festen Klassen.
  Register, Beleg und Archive sind gegen Links/Junctions
  geschuetzt; ein Transaktionsmarker setzt einen abgebrochenen Zwei-Datei-Wechsel
  bytegenau zurueck oder erkennt einen bereits vollstaendigen Commit. Negative
  Bilder werden zusaetzlich gegen alle Auditrollen geprueft: derselbe Bildhash ist
  immer gesperrt, Testhaltungen samt Gegenrichtung sind gesperrt und abweichende
  Train-/Validation-Rollen sind ebenfalls ein harter Fehler. Bei einer Erneuerung
  duerfen neue eingefrorene Eval-Sets nur monoton ergaenzt werden; jedes bereits im
  Register gebundene Schutzmanifest muss unveraendert vorhanden bleiben.
- `training/scripts/derive_negative_set_for_gold_audit.py` leitet dafuer einen
  neuen unveraenderlichen Negativsatz ab, ohne Quelle oder Review umzuschreiben.
  Es entfernt nur Audit-Testhaltungen und Splitkonflikte; bytegleiche
  Gold-/Negativbilder bleiben ein harter Fehler. Der aktuelle Satz
  `bcc_hn_c25fd2f9d33f` besitzt 9 Bilder (7 Train, 2 Validation), Set-ID
  `c25fd2f9d33f09454e03c2e0ed2e25d5fa8faafcd2b50c9af68c03288fbbe0f2`
  und Manifest-SHA-256
  `518a341419b285da88ce674accfe7b0b41330f8cae736ef87a95ea9a48221772`.
- `training/scripts/train_detect_gold.py` prueft danach Plan, Exportbeleg,
  Klassenkarte, alle Datei-Hashes und jedes YOLO-Label erneut. Es trainiert nur
  einen getrennten Kandidaten mit Status `not_deployed`; produktive Gewichte oder
  Modellzeiger werden nie veraendert. Ein laufender Sidecar oder weniger als
  28000 MB freier VRAM sperrt den Start. Der neue Kandidat
  `detect_gold_9eb020e30322` hat 40/40 Epochen beendet und bleibt
  `not_deployed`. Die interne Validation ergibt P 0,3917, R 0,3129,
  mAP50 0,3026 und mAP50-95 0,1726. Der Gewichtshash ist
  `fdf30f77b6aa6271014d130248fde99089854bfc0e58b44d75d462b3b9172ebf`,
  der Kandidatenmanifest-Hash
  `dd40258fd531198be7a781f265cad6e6f74b8d6704ec762b80b8012a140c392d`.
  Der vorige Kandidat `detect_gold_ffbb8612fe50`
  beendete 40/40 Epochen mit P 0,4156, R 0,2575, mAP50 0,2417 und
  mAP50-95 0,1286. Auch diese internen Werte sind keine Produktfreigabe.
- Der rohe Detect-Testanteil des an `detect_gold_ffbb8612fe50` gebundenen Audits
  umfasste 83 Instanzen auf 79 Bildern. Die Haltung `77457-77453`
  ueberschnitt sich jedoch mit einem
  Trainingsnegativ. Nach Ausschluss dieser ganzen Haltung bleiben 81 Instanzen auf
  77 Bildern aus 30 physischen Haltungen als sicherer positiver Testbestand.
- `training/scripts/detect_gold_holdout_provenance.py`,
  `detect_gold_holdout_scoring.py` und `evaluate_detect_gold_holdout.py` pruefen
  Kandidat, Gewicht, Basisgewicht, Dataset, DETECT_ALL-Beleg, Klassenkarte,
  Migration, beide Gold-Audits, aktuelle Samples sowie Bild-/Sample-/Haltungs-
  Ueberschneidungen erneut. Das feste Protokoll ist `conf=0,25`, `imgsz=1280` und
  `IoU=0,5`; zuerst entsteht ein labelblinder, SHA-gebundener Vorhersagebeleg.
  Technische Fehler werden nie als Negativtreffer gewertet. Mehrfachboxen werden
  mit maximaler Trefferzahl und danach maximalem Gesamt-IoU zugeordnet.
- Der korrigierte GPU-Lauf vom 2026-08-02 hat Bericht-SHA-256
  `9ce6aaad85317061953796085ff7daf921b554295f2bad21e904cc5dc78789f6`
  und Vorhersagebeleg-SHA-256
  `87002b0aa6cca5d6a5ec33ef05d5662ff80be2f71458ddaba3374916633aa450`.
  Ergebnis: TP 17, FP 24, FN 64, Precision 41,5 %, Recall 21,0 %, F1 27,9 %.
  `BCC_bogen` traf 14/16 (Recall 87,5 %), `BCA_anschluss` 3/17; die elf
  weiteren gemessenen Klassen hatten keinen exakten Treffer. Der Status ist
  `positive_holdout_only_not_release_qualified`: Es fehlen insbesondere frische,
  saubere Negativbilder, und das Modell bleibt `not_deployed`. Der fruehere Lauf
  `..._20260802_120445_930796.json` ist wegen einer dabei entdeckten falschen
  RGB/BGR-Uebergabe aufgehoben und darf nicht verwendet werden.
- Der allgemeine Detect-Release-Holdout wird ohne Modellvorhersagen aus frischen
  PDF-/Video-Haltungen vorbereitet. `prepare_detect_release_pdf_extraction.py`
  plant hoechstens ein PDF-/Video-Paar je physischer Haltung. Der getrennte
  `tools/DetectReleaseHoldoutPdfExtractor` uebernimmt ueber den bestehenden
  `TrainingPdfReviewImportService` nur eindeutig zugeordnete Operateurfotos und
  optional einen deterministischen Video-Hintergrundframe je PDF. Kundenoriginale
  bleiben unveraendert; der Extraktionsbeleg sperrt Training und Gold.
- `training/scripts/prepare_detect_release_holdout.py` prueft Kandidat, Gewicht,
  Basismodell, class_map v3, VSA-Hash, Extraktionsbeleg sowie bekannte Bildhashes
  und beide Richtungen jeder Haltung erneut. Erst `--execute` veroeffentlicht
  atomar einen eingefrorenen Ordner `detect_release_holdout_<sha>`. Dieser Bestand
  darf nie fuer Training, Gold, Few-Shot oder Kandidatenauswahl verwendet werden.
- `tools/EvalVisibilityReview/detect_release_holdout_review_server.py` zeigt keine
  Modellvorhersagen; PDF-Angaben sind sichtbar als Operateur-Referenz bezeichnet.
  Bei `positive` markiert der Mensch alle sichtbaren Objekte der 15 Klassen mit
  einer oder mehreren Boxen. `negative` gilt nur, wenn keine dieser Klassen
  sichtbar ist; unklare Bilder erhalten `exclude`. Die getrennte Review ist an
  Holdout, Kandidat, Gewicht, Klassenkarte, VSA-Manifest und Bildbytes gebunden.
- `training/scripts/detect_release_holdout_status.py` prueft Holdout und Review
  schreibfrei. `ready_for_detect_evaluation` verlangt eine vollstaendige Review,
  mindestens 20 Instanzen je Klasse, 75 echte Negativbilder und 30 negative
  physische Haltungen; diese Grenzen duerfen nur erhoeht werden. Der Status startet
  kein Modell und ist keine Freigabe. Der aktuelle Review ist mit 400/400 Bildern
  abgeschlossen: 241 positiv, 74 negativ und 85 ausgeschlossen. Wegen fehlender
  Klassenabdeckung und eines fehlenden Negativbilds bleibt er
  `coverage_incomplete`.
- `training/scripts/evaluate_detect_release_holdout.py` fuehrt deshalb nur eine
  klar bezeichnete Mehrklassen-Diagnose aus. Mit festem `conf=0,25`,
  `imgsz=1280` und `IoU=0,5` inferiert es bei ausgeschaltetem Sidecar zuerst alle
  400 Bilder ueber die private Kandidatenkopie und den geprueften RGB-zu-BGR-Weg.
  Der labelblinde SHA-Beleg wird geschrieben und erneut validiert, bevor die
  Review geladen wird. Nur die 315 positiven und negativen Bilder werden
  bewertet; `exclude` wird ignoriert. Technische Fehler auf gewerteten Bildern
  brechen die Diagnose ab und gelten nie als Negativtreffer. Das Werkzeug
  trainiert oder aktiviert nichts und kann keine Modellfreigabe erteilen.
- Der erste GPU-Lauf vom 2026-08-03 hatte 400/400 technisch fehlerfreie
  Vorhersagen. Auf 350 Soll-Boxen ergaben sich TP 36, FP 59 und FN 314
  (Precision 37,9 %, Recall 10,3 %, F1 16,2 %); 9/74 echte Negativbilder hatten
  mindestens einen Fehlalarm. `BCC_bogen` traf 27/37, `BCA_anschluss` 8/39 und
  `BAF_oberflaeche` 1/89; die elf weiteren gemessenen Klassen hatten null exakte
  Treffer. Bericht-SHA-256:
  `64bd6ae370bc1a0bc7320aca5a0921a89cfa467fc9b7ff1c5e926780dc00dcbc`,
  Ledger-SHA-256:
  `a771cbd7fa1a959b49ecf41621df700259471494b7e110d73c7b96eb919adbf2`.
  Details stehen in `docs/quality/DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md`.
- `training/scripts/detect_gold_error_review.py` erzeugt aus genau diesem
  korrigierten Bericht und seinem labelblinden Vorhersagebeleg eine eingefrorene,
  rein diagnostische Fehlfall-Queue. Sie enthaelt jede nicht exakt getroffene
  Goldinstanz sowie jede geometrisch unzugeordnete Vorhersage genau einmal,
  kopiert keine Bilder und setzt alle Trainings-/Exportrechte ausdruecklich auf
  `false`. Der aktuelle gueltige Stand
  `detect_gold_failure_a46a82535c82` umfasst 80 Faelle auf 67 Bildern:
  56 verpasst, 8 falsche Klasse und 16 zusaetzliche KI-Boxen.
- Der lokale Pruefplatz
  `tools/EvalVisibilityReview/detect_gold_error_review_server.py` zeigt Gold- und
  KI-Boxen mit Klasse und erlaubt nur `confirmed_model_error`, `gold_suspect`
  oder `exclude_uncertain`. Review und Queue liegen getrennt unter
  `<KnowledgeRoot>/eval_review/detect_gold_failure_review`. Bericht, Ledger,
  Kandidatenmanifest, Gewicht, Gold-Audit, Trainingssamples und Klassenkarte
  werden per SHA-256 gebunden und vor jeder Entscheidung erneut geprueft.
  Browser-Revision und Dateisperre verhindern stilles Ueberschreiben durch zwei
  Tabs oder Prozesse. Dieser Weg mutiert weder Gold, KB, Trainingsdaten,
  Registry noch Modell. Werden seine Erkenntnisse zur Modellentwicklung genutzt,
  darf derselbe Holdout danach nicht erneut als unabhaengige Release-Abnahme gelten.
- `training/scripts/publish_detect_gold_collection_plan.py` akzeptiert nur eine
  vollstaendige, zur Queue und zum Reviewer passende Review. Der Standardlauf ist
  schreibfrei; `--execute` publiziert atomar und idempotent ausschliesslich einen
  aggregierten Klassen-Sammelplan ohne Bildpfade, Bildhashes, Sample-, Prediction-,
  Fall-IDs oder Kommentare. Gold-fragliche Faelle bleiben getrennte
  Annotation-Audit-Ziele; ausgeschlossene Faelle erzeugen kein Sammelziel. Eine
  bestaetigte falsche Klasse zaehlt zugleich als Positivbedarf der Sollklasse und
  als konkrete Soll-zu-Vorhersage-Verwechslung. Die Review der Queue
  `detect_gold_failure_a46a82535c82` ist abgeschlossen: 80/80 Entscheidungen,
  davon 75 bestaetigte Modellfehler, 0 Gold-Verdachtsfaelle und 5 Ausschluesse.
  Der gueltige Sammelplan ist `detect_gold_collection_874ec160e346`: 60 positive
  Fehlerhinweise, 15 Fehlalarm-Hinweise und 6 Verwechslungen in 4 Klassenpaaren.
  Der fruehere Plan `detect_gold_collection_44a08fe9895e` ist wegen der damals
  fehlenden Verwechslungsliste aufgehoben und darf nicht verwendet werden.
- Die Lernkurven- und Klassenbreitenlaeufe vom 2026-08-30 liegen ausschliesslich
  unter `C:\KI_BRAIN\training\diagnostics` und
  `C:\KI_BRAIN\training\cls_runs`. Sie sind Diagnose ohne Kandidatenmanifest
  und duerfen nie aktiviert werden. Die Lernkurve spricht auf diesem Datensatz
  fuer weiteren Nutzen zusaetzlicher Goldboxen; sie beweist weder Materialmangel
  als alleinigen Engpass noch die Uebertragbarkeit auf die produktive Linie. Die
  Klassenverengung zeigte keinen belegten Vorteil, veraenderte aber zugleich den
  Hintergrunddruck. Die einheitliche Nachmessung mit `half=False, batch=4` ergab
  fuer `BCC_bogen` AP50 0,827 / 0,815 / 0,845 bei 15 / 5 / 2 Klassen. Gegenueber
  den alten gemischten Pruefeinstellungen aenderte sich AP50 hoechstens um 0,004;
  diese erklaeren die Stufenunterschiede daher nicht allein. Die Diagnose
  trainierte mit `fliplr=0.5`, `hsv_h=0.015`,
  `hsv_s=0.7`, `hsv_v=0.4`, `mosaic=1.0`; der produktive Trainer verwendet
  `fliplr=0.0`, `hsv_h=0.01`, `hsv_s=0.3`, `hsv_v=0.3`. Zahlen, Belege und
  Grenzen stehen in
  `docs/quality/DETECT-LERNKURVE-UND-KLASSENBREITE-2026-08-30.md`.
  Das vom Referenzlauf abgelegte `yolo26n.pt` wurde am 2026-09-02 in
  `C:\KI_BRAIN\training\diagnostics\quarantine` verschoben; der Gold-Validator
  akzeptiert den Plan-Datensatz wieder mit 852 Bildern und 894 Instanzen.
- `yolo_wrapper._pil_rgb_to_ultralytics_bgr` wandelt dekodierte PIL-RGB-Bilder vor
  jeder Ultralytics-NumPy-Inferenz explizit in zusammenhaengendes BGR um. Detect,
  Legacy-Classification, beide Holdout-Auswerter und seit 2026-08-09 auch der
  BCC-Test-Endpunkt verwenden denselben Helfer; Rot und Blau duerfen nicht erneut
  still vertauscht werden. Der BCC-Endpunkt hatte die Umkehrung verpasst — erst
  die Copilot-Abnahme (C# gegen Prototyp) machte das sichtbar: 7 statt 4 Stellen,
  systematisch verschobene Konfidenzen. Mit dem Helfer sind die Einzelbildfolgen
  beider Wege exakt gleich (226 Treffer, null Abweichungen).
- SAM-Video-Regel (Goldgewinnung): SAM 2.1 kann Masken durch Videos propagieren,
  darf aber nur als Pruefwerkzeug fuer den Menschen dienen, nicht als automatische
  Goldfabrik. Propagierte Nachbarframes sind stark voneinander abhaengige
  Vorschlaege; sie werden einzeln ausgewaehlt und menschlich bestaetigt, bevor sie
  Gold werden. Kein automatischer Gold-Export aus Video-Propagation.
- Negativ-/Hintergrundbilder sind seit 2026-07-25 im gemeinsamen Detect-Plan
  angeschlossen. Alte reine Registrys duerfen weiter den flachen Pool
  `<KnowledgeRoot>/training/negatives/bcc_pilot` mit Pfad + SHA-256 lesen. Neue
  Trainingslaeufe verwenden stattdessen nur explizite `--negative-set`-Ordner unter
  `training/negatives/sets`. Deren Manifest bindet Bild, echte Haltung, festen
  Train-/Validation-Split, All-Class-Review, Queue, Kandidatenliste und class_map v3.
  Gold-Audit, `prepare_bcc_pilot.py` und C# pruefen diese Kette erneut; Legacy und
  strikte Saetze duerfen in einem neuen Register nicht gemischt werden. Der aktuelle
  strikte Lauf verwendet deshalb nur die 10 reviewten Bilder aus
  `bcc_hn_54f6608b975a`; die 14 Altnegative ohne All-Class-Beleg bleiben draussen.
  `prepare_bcc_pilot.py` verlangt einen expliziten aktuellen `--gold-audit`,
  uebernimmt nur dessen Rollen `train` und `val` und schliesst `test` strikt aus.
  Ein bestehendes Register wird nur mit `--execute --renew-existing` nach erneuter
  Hash-Pruefung ersetzt; der Altstand wird bytegenau unter
  `training/pilots/BCC/registry_history/<sha256>.json` archiviert. Der Plan prueft
  Hashes, echte Haltungen, Gegenrichtungen, Split und Eval-Schutz auch fuer Negative
  und schreibt leere Labeldateien (`IsNegative` nur bei `true` serialisiert).
  `train_bcc_pilot.py` akzeptiert leere Labeldateien als Negative (Positive ohne Labeldatei
  stoppen weiter), trainiert mit `flipud=0.0, fliplr=0.0` (Uhrlage!) und leichter
  HSV-Augmentierung (`hsv_h=0.01, hsv_s=0.3, hsv_v=0.3`) sowie `--patience` Default 10.
  Der strikte Lauf vom 2026-07-28 exportierte Plan
  `f23a95b149addf9d24365834b563b7784f76132190d9e4e60f4c61e84a652bc9`
  mit 57 BCC-Positiven und 10 Negativen (48 Train, 19 Validation). Der Kandidat
  `bcc_bogen_f23a95b149ad_hn10_strict` stoppte nach 33/40 Epochen und bleibt
  `not_deployed`; Gewicht-SHA-256:
  `89331f637fe59cd2c321c3330733cc0278c57b4bd3a5c512662c12fef4a1ee78`.
  Seine interne, fuer Early Stopping verwendete Validation ist noch kein
  Release-Beweis (P=0,5371, R=0,4706, mAP50=0,4829, mAP50-95=0,1613).
  Bei `conf=0,25` aktiviert er auf beiden strikten Validation-Negativen und auf
  7/14 nicht mittrainierten Altnegativen. Deshalb nicht aktivieren: zuerst mehr
  unterschiedliche BCC-Boxen und streng reviewte Hard-Negatives sammeln, danach
  einen frischen, zuvor unberuehrten Release-Holdout pruefen.

## Gesamtaudit 2026-08-14 — umgesetzte Haertungen

Bericht: `docs/audits/2026-08-14-gesamtaudit.md`. Folgendes ist umgesetzt und darf nicht
zurueckgedreht werden:

- **Python-Sperrdatei:** `sidecar/requirements-lock.txt` ist von 40 bekannten Luecken in
  10 Paketen auf 5 in 2 Paketen gehoben. torch/torchvision/tensorrt wurden NICHT
  angefasst (cu128/sm_120 bleibt). Belegt geprueft: CUDA verfuegbar, 273 Sidecar-Tests
  gruen, echter Grounding-DINO-Lauf mit identischen Treffern. `transformers` bleibt
  bewusst auf 4.57.6: 5.3.0 bricht Grounding DINO
  (`'BertModel' object has no attribute 'get_head_mask'`, real getestet). `setuptools`
  bleibt unter 82, weil torch das verlangt. Beide Ausnahmen stehen mit Beleg in
  `sidecar/security/lock_audit_exceptions.json`; `sidecar/security/audit_lock.py` prueft
  sie in der CI und wird auch bei einer VERALTETEN Ausnahme rot.
- **CI:** `dotnet restore --locked-mode`, NuGet-Schwachstellenpruefung
  (`.github/scripts/check-dotnet-vulnerable.ps1`, wertet JSON aus — die Textmeldung ist
  uebersetzt und ein englischer Textvergleich fand nie etwas), Sperrdatei-Audit,
  Abdeckungsgrenze und auf Commit-Hashes gepinnte Actions.
- **Programm-Momentaufnahme:** Ein unlesbarer Ordner ist kein stiller Uebersprung mehr.
  `ProgramSnapshotFileCatalog.IsRequiredDirectory` (src, tests, tools, sidecar, .git)
  laesst die Sicherung fehlschlagen; alle anderen erscheinen in Ergebnis, Manifest und
  Dialog. Die fertige ZIP wird vor der Veroeffentlichung geprueft — mit SELBST
  nachgerechneter CRC-Summe, weil System.IO.Compression beim Lesen keine CRC prueft und
  ein Bitfehler in einer unkomprimiert abgelegten Modellgewichtsdatei sonst durchgeht.
  Die SHA-256 der Sicherung liegt als Nebendatei `<name>.zip.sha256` daneben.
- **QGIS-Bruecke:** Token-Pflicht auf BEIDEN Wegen (eigener Server und Live-Control auf
  demselben Port) ueber `QgisBridgeToken`; das Plugin liest den Token aus
  `.qgis_bridge_token` im AppData-Ordner. Fehlermeldungen nach aussen sind neutral.
- **KI-Ampel:** Gruen verlangt zwei unabhaengige BELEGQUELLEN, nicht zwei Zahlenfelder
  (`EvidenceSourceGrouping`). Sprachmodell, die daraus abgeleitete Plausibilitaet, die
  Bild-Beschreibung desselben Modells und die Aehnlichkeit der Prompt-Beispiele sind EINE
  Quelle. Die Gewichtung im Zahlenwert ist unveraendert. Der Anzeigetext heisst
  „KI-Kriterien erfüllt – prüfen" statt „Sicher".
- **Ein-Knopf-Import:** Verwendet fuer Archiv, Plan-PDF, Medien, namensbasierte
  Protokolle, Kanal und Dichtheit dieselbe `IImportFileStagingSession` und denselben
  `.import-transaction.json`-Marker wie der manuelle Import. Das alte
  `IImportedFileLedger` bleibt nur bis zum Beginn von `Publish` ein zusaetzliches
  Sicherheitsnetz und darf danach nichts mehr loeschen. Der Importbericht bleibt
  absichtlich ausserhalb der Transaktion liegen.
- **CSV:** `CsvCell` entschaerft Formelanfaenge (`=`, `+`, `-`, `@`, Tab, CR) zentral;
  negative Zahlen bleiben Zahlen. Kein Exportweg darf das erneut halb umsetzen.
- **Medienpfade:** Der Protokolleditor zeigt nur Mediendateien, keine `..`-Ausbrueche und
  absolute Pfade nur innerhalb erlaubter Wurzeln (`ProtocolEntryEditorMediaRoots`).
- **`async void`:** `VsaCodeExplorerWindow.ApplyAndCloseAsync` ist ein `Task`; die
  Aufrufer gehen ueber `StartApplyAndClose`, das Ausnahmen anzeigt statt die Oberflaeche
  zu beenden.
- **Uebersprungene Tests:** `UebersprungeneTestsWaechterTests` haelt die sieben zulaessigen
  Skip-Stellen namentlich fest. Ein neuer oder entfernter Skip macht den Waechter rot.

### Nachaudit 2026-08-22 — verifizierte Randhaertungen

- Der IBAK-FDB-Import laedt `fbclient.dll` nur noch aus dem Programmordner. Eine DLL
  aus einem Kunden- oder Importordner wird nie als nativer Treiber verwendet.
- Die acht zuvor ungeschuetzten Review-Server unter `tools/EvalVisibilityReview`
  pruefen nun den Loopback-Host. POST akzeptiert nur JSON mit deklarierter Laenge
  und hoechstens 64 KiB; ihre gemeinsame Regel liegt in
  `review_server_security.py`.
- Der KI-Erststart liest den Sidecar-Token nach dem Prozessstart fuer jeden
  Health-Versuch neu. Dadurch funktioniert auch ein Token, den der Sidecar beim
  ersten Start erst auf die Platte schreibt.
- Blockierende Trainingsskripte besitzen endliche Subprocess-Zeitlimits.
  `osd_hd_validierung_vorbereiten.py` ersetzt vorhandene Ziele nur mit `--force`
  und eigenem Arbeitsmarker; fremde Ordner und Verknuepfungen bleiben unangetastet.
- `TrainingCenterViewModel` gibt seinen eigenen Knowledge-Base-HTTP-Client beim
  Schliessen des Fensters frei. Dispose ist mehrfach sicher aufrufbar.

### Design-Feinschliff 2026-09-03 (Audit `docs/DESIGN-AUDIT-2026-09-03.md`, Q1-Q6)

Waechter `DesignAuditFeinschliffTests` (7 Tests) haelt fest, was nicht zurueckfallen darf:

- Sichtbare Texte (`Content/Header/Text/ToolTip/Title`, auch `StringFormat` in Bindungen)
  schreiben echte Umlaute. Die `ae/oe/ue`-Konvention gilt nur fuer Quellcode und
  Kommentare. Schweizer `ss` bleibt (kein `ß`).
- Menuepunkte mit literalem `Header` tragen ein `MenuItem.Icon` (Fluent-Glyph); nur
  checkbare Punkte, Menueleisten-Koepfe (`_Datei`) und Punkte mit eigenem Header-Inhalt
  sind frei.
- Bedienelemente verwenden `ui:FluentIcon` statt Textsymbolen (`▲▼✕⚠📷↶↷⟲⟳`); gerade
  Pfeile im Fliesstext bleiben erlaubt. Im Code liefert die Erweiterung
  `FluentGlyphKnopf.MitGlyph(...)` Glyph plus zugaenglichen Namen aus dem Tooltip.
  Ausnahmen mit Grund: `DataPageConverters` (Tabellen-Haekchen), `ShellViewModel` (Punkt
  fuer „ungespeichert" im Fenstertitel).
- Jedes Fenster traegt `ui:WindowFx.Entrance="True"`; ausgenommen MainWindow, PlayerWindow,
  LiveFrameWindow, StartupSplashWindow.
- `KeyboardFocusVisual` sitzt auch auf CheckBox, RadioButton, ComboBox, Expander,
  TreeViewItem, TabItem, Slider und GridViewColumnHeader.
- Feste Farbwerte (`#RRGGBB`) gibt es nur in den sechs Video-Dateien (PlayerWindow,
  PlayerCodingSidePanel, LiveFrameWindow, PhotoMeasurementWindow, StartupSplashWindow,
  PipeGraphTimeline). Neue Tokens: `ScrimBrush`, `StatusBadgeTextBrush` (je Theme) und
  `Video*Brush` (theme-unabhaengig in `Controls.xaml`) fuer die Player-Abdunkelungen.

Der isolierte `NachschlagKontextmenueTests`-Kindprozess hat ein 60-s-Limit und faellt im
Gesamtlauf unter Last gelegentlich um; allein besteht er in rund 26 s.

**Schriftskala (M1, 2026-09-03, Waechter `DesignAuditSchriftskalaTests`):** Sieben
`sys:Double`-Tokens in `Controls.xaml` — `TextXS` 11, `TextS` 12, `TextM` 13, `TextL` 15,
`TextXL` 18, `TextTitle` 22, `TextDisplay` 28 — plus `IconHero` 36 fuer grosse
Leerzustand-Glyphen. **11 px ist die Untergrenze im ganzen Programm** (Entscheid Pascal).
Seiten, Fenster, Controls und Dialoge setzen `FontSize` nur noch als
`{DynamicResource Text…}`; feste Zahlen sind nur in `Theme/*.xaml` (nie unter 11) und im
`StartupSplashWindow` erlaubt. Im Code duerfen nur gezeichnete Beschriftungen auf Video,
Grafik und PDF-Nachbildung kleiner als 11 sein (Positivliste im Test). Umgestellt wurden
885 Stellen: 8-11 -> XS, 12 -> S, 13 -> M, 14-16 -> L, 17-21 -> XL, 22/24 -> Title,
30-40 -> Display bzw. IconHero.

Nachgelagerte Grossumbauten vom 2026-08-14:

- **Import-Staging vervollstaendigt.** `ImportFileTransaction` ist der gemeinsame
  Markerablauf fuer manuell und Ein-Knopf. `StageCopyAs`, `ResolveReadPath`,
  `EnumerateReadableFiles` und `StageGeneratedFile` bilden auch umbenannte Ziele,
  Zwischenlesepfade und neu erzeugte PDF-Seiten ab. Die manuelle projektinterne
  Schachtverteilung verwendet denselben Weg. Nur bewusst externe Schacht-Zielordner
  bleiben direkte Exporte, weil ein Projektmarker ausserhalb des Projekts nichts
  loeschen darf.
- **UI-Dateilogik ausgelagert.** `TrainingCenterStore` bleibt als 61-zeilige
  Kompatibilitaetsfassade; `ITrainingCenterDocumentStore` und
  `TrainingCenterDocumentFileStore` bewahren JSON, numerischen Status, `.bak` und
  Quarantaeneformat. Die Wissens-ZIP-Engine, ihr Dateikatalog und ihre Nachbearbeitung
  liegen unter `Infrastructure/Ai/Backup`; `KnowledgeBackupService.BackupResult` und
  die bisherigen Aufrufer bleiben unveraendert.

## Build & Test
```bash
dotnet build AuswertungPro.sln
dotnet test AuswertungPro.sln
```

`AuswertungPro.sln` enthaelt die vier produktiven Projekte, die vier Testprojekte
und alle 44 `tools/**/*.csproj`. Neue Werkzeugprojekte sofort aufnehmen, damit
verschobene Klassen oder Projektverweise im normalen Release-Build sichtbar brechen.

## Wichtige Klassen
- `VideoAnalysisPipelineService`  → waehlt Multi-Model- oder Fallback-Pfad fuer Videoanalyse
- `MultiModelAnalysisService`     → YOLO/DINO/SAM/Qwen-Pipeline mit framebasiertem Dedup; Ausfallschutz und Checkpoint/Resume unten
- `IAnalysisCheckpointJournal`/`AnalysisCheckpointJournal` → append-only JSONL-Checkpoint pro Video (neben der Trace-Datei, Name = SHA256-Kurzhash des Videopfads). Jeder bearbeitete Frame schreibt genau einen Zustand: `update` (mit Befunden), `advance` (normal uebersprungen), `retry_required` (Transport-/Modell-/Verarbeitungsfehler). Ein Resume uebernimmt nur den lueckenlosen, gueltigen Anfang ab Frame 1 und replayt ihn exakt ueber `TemporalFindingDeduplicator.Update(...)` bzw. `AdvanceAll()` — dadurch liefert Abbruch+Fortsetzung dieselben Detections wie ein ununterbrochener Lauf. `retry_required` beendet den verwendbaren Bereich (ab dort neu inferieren, stale Schweif wird abgeschnitten); fehlende/doppelte/ruecklaufende Frame-Nummern, unbekannte Zeilentypen oder eine beschaedigte mittlere Zeile verwerfen das Resume vollstaendig (frischer Start + Logwarnung); nur eine unvollstaendige letzte Zeile wird sicher gekuerzt. Fehlende Pflichtfelder (Zeit, Meter, Schaetzflag, bei update Findings/Meterquelle) werden NICHT durch Standardwerte erfunden, sondern verwerfen das Resume ebenfalls. `CleanupCompletedJournals` loescht ausschliesslich streng lesbare, abgeschlossene, aeltere Journale — offene oder beschaedigte nie; prozessweit auf hoechstens einen Lauf pro Tag gebremst, Alter wird vor dem Einlesen geprueft; Fehler beim Aufloesen/Aufzaehlen der Ablage ueberspringen nur die Bereinigung mit Warnung — die Analyse laeuft immer weiter
- `SidecarOutageGuard`/`QwenOutageTracker` → Ausfallschutz des Multi-Model-Laufs: 8 Folge-Frames mit Sidecar-Transportfehler (YOLO/DINO/SAM gemeinsam, Reset implizit ueber Frame-Indizes) brechen den Lauf degraded ab; Qwen/Ollama ist ein eigener Prozess und erzeugt ab 8 Folgefehlern nur eine Degraded-Notiz (`NotedErrorCount` bleibt nach spaeterem Erfolg erhalten). Ein Nutzerabbruch per CancellationToken wird sofort weitergeworfen und zaehlt nie als Ausfall. Mehr als 10 % fehlerbedingt uebersprungene Frames setzen `Incomplete=true` an `VideoAnalysisResult` und `PipelineResult` (Surfacing ueber den Warnungspfad)
- Sidecar-Haertung (Paket 2): Der Sidecar arbeitet mit besitzbasierten Busy-Leases (`gpu_manager.acquire_busy/release_busy`, uuid-Besitzer-ID): Predict-Lock ZUERST, Lease DANACH; nur der Besitzer entfernt seine Lease; Wartende koennen weder Busy-Uhr noch Zustand verschieben. Einheitlich fuer YOLO (GPU+CPU als logische Lease `YOLO_CPU`), DINO, SAM, BCC und YOLO-cls (`YOLO_CLS`); CPU-Inferenzen werden bewusst ueberwacht, der Watchdog laeuft daher unabhaengig vom Geraet. VRAM-Eviction ist atomar (Auswahl + letzte Lease-Pruefung + Reservierung unter einem kurzen `_global_lock`); Modellreferenzen, `empty_cache` und GC werden danach ohne diesen Lock bereinigt, damit Health/Watchdog auch bei blockierter CUDA-Bereinigung ansprechbar bleiben. `unload` verweigert bei laufender Inferenz; kein sicherer Kandidat → `insufficient_vram` (mit free/required/reserved_gb im 503-Detail). Den GETEILTEN Slot `YOLO_TEST` benutzen BCC-Pilot und Lernstufen-Klassifikation gemeinsam. Welches Gewicht drinliegt, sagt allein `SlotState.content_id` (Gewichts-SHA-256): `ensure_loaded(..., content_id=...)` laedt bei Abweichung neu, `discard_foreign_content` raeumt fremden Inhalt VOR der eigenen Lease (die eigene Lease wuerde das Entladen sonst sperren), und beide Wrapper teilen sich `yolo_test_slot.PREDICT_LOCK`. Nie wieder eine Modulvariable je Wrapper als Slot-Wahrheit einfuehren: Damit sah keiner den Wechsel des anderen und es inferierte still das fremde Modell (Audit 2026-08-14, S-H1)
- `SidecarInsufficientVramException` → C#-Antwort auf `insufficient_vram`: `VisionPipelineClient` parst 503-Bodys defensiv (echter Vertrag: `code` + Zahlen auf Top-Ebene, `detail` als Klartext; verschachteltes Format toleriert, korrupt = allgemeiner Fehler; Vertragstest mit woertlichem Python-JSON); nur dieser Code wird zum eigenen Kapazitaetsfehler (kein HTTP-Retry, kein Outage-Zaehlen, kein Sidecar-Restart; Frame-Catch: Skip-Quote + Trace degraded + Checkpoint retry_required + Degraded-Grund mit VRAM-Zahlen). `model_unloaded` bleibt gezielt retryfaehig, unbekanntes 503 bleibt Transportfehler. Sidecar-seitig sind gleichzeitige Modell-Ladungen ueber In-flight-Reservierungen koordiniert (`_inflight_loads` unter dem kurzen `_global_lock`): zwei Ladevorgaenge sehen nie denselben freien VRAM (effektiv frei = frei − laufende Reservierungen; `reserved_gb` = Ollama-Reserve + In-flight-Summe)
- `SidecarRestartService` → kontrollierter Neustart nur des EIGENEN Sidecars (max 1 Versuch pro Analyselauf): Prozess-Tracking mit PID + Startzeit + Prozessart (`AiStartedProcessKind` Sidecar/Ollama) + Programmpfad; veraltete Eintraege werden bei jeder Abfrage entfernt. Nur die ausdrueckliche Art `Sidecar` beweist Besitz und erlaubt einen Kill; `Unknown`, Ollama oder ein hinterlegter, aktuell nicht lesbarer/abweichender Programmpfad sperren fail-closed. Kill-Fehler oder Timeout → kein Neustart (kein zweiter Sidecar). Ohne /health-PID: ein lebender eigener Sidecar wird zuerst verifiziert beendet (nie daneben gestartet), ein frueher eigener, beendeter Sidecar bleibt ueber `HadTrackedSidecarProcess` wiederstartbar (Start- ≠ Kill-Berechtigung, auch nach Watchdog-Exit), nur Ollama/Unknown → kein Blindstart. Ein Python-Kindprozess ohne eigenen Tracking-Eintrag muss ein Python-Image tragen; Baseline-Snapshot + Re-Probe direkt vor dem Kill binden Startzeit und Programmdatei. Erfolg erst nach 2 aufeinanderfolgenden /health-Polls
- `SidecarRequestTimeoutException` → interner Inferenz-Timeout (getrennt vom Benutzerabbruch, der OCE bleibt): zaehlt als Transportfehler, kein Retry, Meldung mit Modell-Label + Endpunkt, keine Tokens; Health-/Trainingsaufrufe und Ollama-Timeout bleiben unabhaengig
- `VideoFullAnalysisService`      → Vollanalyse-/Fallback-Pfad mit eigener Dedup-Logik
- `SingleFrameMultiModelService`  → Live-Einzelframe YOLO/DINO/SAM
- `VisionPipelineClient`          → C#-HTTP-Client zum Sidecar
- `SidecarEndpointPolicy`         → gemeinsame Token-Grenze fuer Haupt-, Start- und Neustartpfad: `X-Sidecar-Token` wird ausschliesslich an Loopback-Endpunkte gesendet; bei LAN-/Remote-URLs bleibt der Header leer
- `QualityGateService`            → Green/Yellow/Red aus verfuegbaren Evidence-Signalen
- `FullProtocolGenerationService` → KI-Befunde zu Protokolleintraegen mappen
- `IOfferPdfExportService`         → Vertrag (Application/Output): kapselt Vorlagen-/Logo-Pfadbau + PDF-Renderer; ViewModels newen keinen Renderer mehr
- `OfferPdfExportService`          → Impl (Infrastructure): loest Pfade auf, delegiert an `OfferHtmlToPdfRenderer` (injizierbarer Render-Delegate als Test-Seam); Modell typsicher ueber `IOfferPdfModel`
- `IQuickScanService`/`IQuickScanSession` → Vertraege (Application/Ai): KI-Schnellscan + kurzlebige Sitzung (eigener Ollama-Client); DTOs `QuickScanSegment/Progress/Result` liegen ebenfalls in Application.Ai
- `QuickScanSession`               → Impl (Infrastructure): baut ffmpeg-Pfad, eigenen `OllamaClient` und `QuickScanService`, besitzt den Client (`IDisposable`). Erzeugt ueber `ServiceProvider.CreateQuickScanSession(cfg)`; der Player-`QuickScanController` newt keine KI-Infrastruktur mehr
- `KnowledgeBaseManager`          → SQLite-KB: Samples + Embeddings indexieren/retrieven
- `TrainingSamplesStore`          → JSON-Trainingssamples speichern/mergen
- `PhotoMeasurementGeometryService` → stabile oeffentliche Fassade fuer reine Fotomessungs-Geometrie
- `PhotoMeasurementAnglePlanBuilder` → getrennte Winkel-, Abzweig-, Kreis- und Bogenplanung ohne UI-Zustand
- `PipelinePipeRadarRenderer` → zustandslose WPF-Zeichnung des Rohr-Radars; das Fenster liefert nur Daten, Modus und Groesse
- `PipelineLiveFrameOverlayRenderer` → bewahrt Leer-/Groessenregeln des eingebetteten Live-Rings und delegiert die Zeichnung
- `LiveFrameRingOverlayRenderer` → gemeinsame Ring-Zeichnung fuer Hauptfenster, abgedocktes Fenster und Player mit drei getrennten Stilen
- `LiveDetectionGeometryMapper` → gemeinsamer Uhrparser, Uhrwinkel und Fassade auf die zentrale Ringgeometrie
- `PipelineProgressMapper` → laufbezogene Fortschritts-, ETA- und Live-Frame-Abbildung; liefert dem Fenster nur Render-/Weiterleitungs-Hinweise
- `PipelineResultPresenter` → zustandslose Abschlussabbildung fuer Statistik, Telemetrie und hoechstens 250 sichtbare Befunde

- `ManualGoldTrainingPolicy`      -> erlaubt fuer neues Training nur persoenlich bestaetigte `ManualCoding`- oder streng belegte `PdfPhoto`-Samples mit vorhandenem Bild, randgueltiger BBox und SAM-Segmentierung; mindestens 80 % der Maskenpixel muessen in der Hand-Box liegen
- `CodingTrainingSamplePersistenceCoordinator` -> uebernimmt persoenliche Annahmen/Korrekturen aus dem Player-Codiermodus nach `gold_frames`, Trainingsliste und KB
- `PersonalGoldProgressCalculator` -> berechnet den Live-Goldstand je Hauptcode (Ziel 30-50), ohne Daten zu veraendern
- `IPersonalGoldAlbumService`/`PersonalGoldAlbumService` -> liefert das rein lesende Fotoalbum der persoenlichen Handlabels nach Hauptcode
- `IPersonalGoldInboxService`/`PersonalGoldInboxFileService` -> verwaltet den vorbereitenden Bildeingang unter `training/gold_inbox`
- `PersonalGoldFrameMigrationService` -> kopiert Altbestand inhaltsadressiert in `gold_frames` und stellt JSON/SQLite gemeinsam um
- `PersonalGoldMigrationCommitter` -> haelt Umschalten, Nachpruefung und Ruecksetzung von JSON/SQLite getrennt von der Auswahl
- `tools/PersonalGoldMigration`   -> wiederholbares Migrations-/Pruefwerkzeug; schreibt Inventar und Pruefspur unter `<KnowledgeRoot>/training`
- `PersonalGoldBrainSeparationService` -> duenne Fassade fuer Gold-only-Arbeitsstand und atomare Umschaltung; Input/Pfade, Workspace, Commit-Journal und Recovery liegen in getrennten internen Diensten
- `PersonalGoldArchiveRecoveryService` -> duenne Fassade zum Nachholen bestaetigter `ManualCoding`-Faelle; Journal, Pfadpruefung, Vorherkopien und Rollback liegen in getrennten internen Diensten
- `tools/GoldBrainSeparation`     -> sicherer Pruef-/Ausfuehrungsweg fuer Altarchiv, Gold-only-Datenbank und neuen Elements-Spiegel
- `TrainingDataInventoryService`  -> rein lesendes Inventar fuer Teacher-/Trainingsquellen, Pfade und Eval-Schutz je Eval-Set
- `TrainingInventoryReportValidator` -> strenger Vertrag fuer Schema 2.2, Triage, Pfade, Quellen und Zusammenfassung
- `tools/TrainingDataInventory`   -> AP-0.1-Werkzeug; Bericht plus SHA-256 unter `<KnowledgeRoot>/training/reports`
- `tools/DetectReleaseHoldoutPdfExtractor` -> liest codierte PDF-Protokolle und erzeugt einen hashgebundenen, nicht trainierbaren Extraktionsbeleg fuer den Mehrklassen-Release-Holdout
- `ITrainingYoloClassMapStore`    -> rein lesender, unveraenderlicher class_map-Snapshot (aktiv v3, v2 eingefroren lesbar) fuer den lokalen Detect-Export
- `TrainingYoloClassMapFileStore` -> prueft feste Klassenzahl je Version (v2 = 14, v3 = 15 inkl. BCC_bogen), echten VSA-Manifest-Hash, Quell-Hashfelder, Zeilenzahlen, Quellenreihenfolge und menschlich freigegebene Migration
- `VsaYoloClassMapFileStore`      -> Teacher-Karte; `GetClassId` liest strikt, nur `GetOrAddClassId` darf bewusst erweitern
- `TrainingExportPlanInputBuilder` -> baut den Planner-Input nur aus freigegebenen persoenlichen Gold-TrainingSamples; Teacher-Daten bleiben Inventar
- `TrainingExportPlanService`      -> legt Split, Klassen-IDs, Dateinamen, Ausschluesse und SHA-Zusammenfuehrung fest
- `TrainingExportPlanLocalExecutor` -> atomarer lokaler Ausfuehrer desselben Plans
- `TrainingExportSidecarRequestBuilder` -> verpackt den Plan fuer den strikten Sidecar-v2-Vertrag
- `TrainingExportCompletionService` -> markiert nur vom passenden Plan bestaetigte `TrainingSample`-Quellen
- `TrainingExportExecutionService` -> waehlt Sidecar oder den gleichwertigen lokalen Weg und prueft Antwort sowie Zielpfade
- `TrainingYoloExportCoordinator` -> steuert Auswahl, Inventar, Plan, Ausfuehrung und Abschluss ausserhalb der UI
- `TrainingYoloExportComposition` -> baut das Export-Subsystem einmalig zusammen; der zentrale ServiceProvider delegiert nur
- `FullBackupComposition`         -> baut Marker, SQLite-Schnappschuss, Manifestpruefung und Vollsicherung einmalig zusammen; die UI liefert nur die aktuelle Quellenfunktion
- `KnowledgeRealtimeMirrorService` -> gleicht den gesamten KnowledgeRoot beim Start ab und spiegelt danach jede Dateiaenderung auf den Datentraeger `Elements` nach `Brain`
- `HoldingNameFromShafts`          -> leitet den Haltungsnamen aus `Schacht_oben`/`Schacht_unten` ab und BEHAELT dabei die vorhandene Reihenfolge (im Bestand steht bei Gegenbefahrung auch der untere Schacht vorn). Ein Name, der auf keines der beiden Muster passt, bleibt unangetastet. `DataPageCellEditController.ApplySchachtChange` ist der gemeinsame Weg fuer Tabellen-Edit und Formular-Editor; die Namensaenderung laeuft danach ueber den normalen Umbenennungsweg, damit Verteilordner, Dateien und PDF-Text mitgehen
- `HoldingRenameFileService`       -> benennt eine Haltung samt Projekt-Verteilordnern und gespeicherten Medienpfaden um; externe Kundenordner sind ausgeschlossen
- `HoldingFolderRenameTransaction` -> benennt Dateien und Unterordner rekursiv, erkennt abweichende datumsbasierte Alt-Dateinamen und kann jeden ausgefuehrten Schritt zurueckrollen
- `StoredImportFileService`       -> kopiert Importquellen, loest Namenskollisionen und schreibt die Pfadlisten zentral
- `StoredImportFilePathResolver`  -> liest gespeicherte XTF-/PDF-Listen zentral und loest moderne sowie bestehende Projektpfade sicher auf
- `ImportFileStagingService`      -> bereitet projektbezogene Importkopien geprueft vor und nimmt sie bis zur Projektuebernahme zurueck
- `MediaDistributionService`      -> verteilt Medien hinter `IImportMediaDistributionService`; die UI erzeugt ihn nicht selbst
- `ShaftDistributionService`      -> kapselt die Schachtverteilung und staged projektinterne Ziele ueber dieselbe Importtransaktion
- `TrainingCenterDocumentFileStore` -> speichert das UI-unabhaengige Training-Center-Dokument atomar mit Backup und Rueckfall
- `KnowledgeBackupEngine`         -> exportiert/importiert Wissens-ZIPs, SQLite-Snapshot, Ruecknahme und Nachbearbeitung ausserhalb der UI
- `ServiceProviderRegistrationMap` -> ordnet die bereits gebauten Dienste ihren 141 Vertragstypen zu und erzeugt selbst nichts

Der Vollsicherungsaufbau liegt in Infrastructure. `ServiceProvider.FullBackup.cs`
reicht die bisherigen oeffentlichen Dienste unveraendert weiter. Der zentrale
`ServiceProvider` darf `BackupTargetGuard.UseMarkerGuard` nicht aufrufen; der passende
Marker wird direkt an `FullBackupService` uebergeben.

`KnowledgeRealtimeMirrorService` startet durch `App` nach dem Aufbau des
`ServiceProvider`. Er gleicht den gesamten aktiven `KnowledgeRoot` zuerst
inkrementell mit `<Datentraeger Elements>\Brain` ab und verarbeitet danach
Dateiaenderungen in einem Ein-Sekunden-Takt. Der Laufwerksbuchstabe wird ueber die
Datentraegerbezeichnung `Elements` ermittelt. SQLite-Dateien werden als gepruefte
Online-Schnappschuesse geschrieben; WAL/SHM-Dateien werden nicht als halbfertige
Datenbankkopien uebernommen. Ein eigener Zielmarker, Pfadgrenzen und
Verknuepfungsschutz sichern jede Loeschung ab. Ist die Platte nicht angeschlossen,
bleibt die Quelle unveraendert und der Abgleich wird nach dem Wiederanschliessen
automatisch vollstaendig nachgeholt.

`BackupSourcePathGuard` und `BackupTargetPathGuard` pruefen Quelle und Ziel vor
jedem kritischen Dateizugriff erneut. Ein unlesbarer oder verknuepfter Pflichtpfad
bricht Spiegelung/Vollsicherung ab, bevor veraltete Zieldateien entfernt oder
Versionen rotiert werden. Unter `_Versionen` bleiben hoechstens die drei neuesten
Sicherungsstaende; aeltere werden erst nach einem bis dahin fehlerfreien Lauf
entfernt. Einstellungs-, Log- und Desktop-Skriptquellen duerfen
fehlen; Programm- und Projektkomponenten sind nur dann leer, wenn fuer sie keine
Wurzel konfiguriert wurde. Bestehende Spiegeldateien bleiben bei optionalen
Fehlstellen erhalten. `KnowledgeRoot` und jede tatsaechlich konfigurierte
Projektquelle bleiben Pflicht. `DirectoryMirror`, `BackupTargetMarkerGuardService`
und `KnowledgeMirrorMarker` bilden die zentralen Datei-, Zielbesitz- und
Spiegelbesitz-Grenzen. Die Programmquelle betritt regenerierbare Arbeits- und
Testordner wie `.tmp` nicht; Projekt- und Wissensquellen bleiben davon unberuehrt.
Die konfigurierte Projektwurzel und das aktuelle Projekt sind Pflichtquellen.
Historische externe Projekte aus `RecentProjectPaths` bleiben dagegen optionale
Quellen: vorhandene Ordner werden weiter gesichert; ein wirklich fehlender Ordner
erzeugt nach erfolgreichem Lauf eine sichtbare Warnung und sein bisheriger
Spiegelstand bleibt erhalten. Ein vorhandener, aber unlesbarer Ordner bleibt ein
harter Sicherungsfehler.

Die getrennte Programm-Momentaufnahme laeuft ueber `IProgramSnapshotService` und
`ProgramSnapshotService`. Sie liest den Programmordner, folgt keinen Verknuepfungen
und veroeffentlicht die ZIP erst nach vollstaendigem Schreiben atomar. Der
`ProgramSnapshotFileCatalog` laesst Quellcode, Git-Verlauf und Modellgewichte zu,
schliesst aber ableitbare Build-Ausgaben, Python-Umgebung, Kartenkacheln,
Arbeitskopien und `.playwright-cli` aus. `_manifest.json` dokumentiert Dateizahl,
uebersprungene Verknuepfungen und den lesbaren Git-Commit. Die UI-Orchestrierung
liegt in `SettingsProgramSnapshotWorkflow`; `SettingsPageViewModel` waehlt nur das
Ziel, zeigt Fortschritt und meldet das Ergebnis.

Die gemeinsame Suche nach einer Schachtprotokoll-PDF liegt hinter
`ISchachtProtocolFileLocator` und `SchachtProtocolFileLocator`. Sie bevorzugt den
gespeicherten `PDF_Path`, sucht danach ausschliesslich im passenden Schachtordner
und liefert fehlende oder mehrdeutige Treffer sichtbar zurueck. Import,
Stammdatennachlauf und Neueinlesen verwenden denselben Dienst; die kleine
`SchachtProtocolFileCompatibility`-Fassade bleibt nur fuer alte UI-Aufrufer.

`StoredImportFileService` plant neue Importkopien fuer beide Projektdatei-Strukturen
unter `<Projekt>\Imports\<Art>`. Im manuellen Import schreibt er zunaechst ueber die
laufbezogene `IImportFileStagingSession`; ausserhalb dieses Ablaufs bleibt sein bisheriger
direkter Kompatibilitaetsweg erhalten. Dieser Direktweg prueft Projektroot,
`Imports\<Art>`, Wunschziel und Kollisionsziel ueber `ProjectWritePathGuard`, bevor er
Ordner anlegt oder kopiert. `StoredImportFilePathResolver` liest die Metadaten
ueber `StoredImportFileRegistry`, prueft zuerst den echten Projekt-Root und faellt fuer
bestehende Ablagen auf den Ordner der `projekt.json` zurueck. Dadurch bleiben alte
`Projektdateien\Imports`-Dateien lesbar. Fehlende oder unsichere Einzelpfade werden
uebersprungen. `VsaPageViewModel` und `InspectionProtocolFileLocator` besitzen fuer
gespeicherte Importlisten keine eigene JSON- oder Pfadlogik mehr. Die Protokollsuche
behaelt nur PDF-Auswahl und Suchreihenfolge und erhaelt zentral dieselbe Resolver-Instanz.
Die oeffentliche `ImportFileStoreService`-API bleibt nur als duenne
Kompatibilitaetsfassade und delegiert ohne eigene Dateioperationen an dieselbe
Schreib-Implementierung.

Die sechs manuellen Importwege PDF, XTF, WinCan, IBAK, KINS und SchachtPro liegen im
internen `ImportManualWorkflowController`. Er kennt weder `ServiceProvider` noch Shell oder
ViewModel und verwendet fuer Vorschau, Commit, Bericht, Speichern und Projekttausch
weiter den `ImportRunWorkflowController`. `ImportPageViewModel` verbindet nur Befehle
und aktuellen UI-Zustand. Seine gemeinsame Importsperre umfasst diese sechs Wege,
den Schacht-PDF-Ordnerimport, den Ein-Knopf-Import sowie Portabilitaet,
Fotozuordnung und Protokoll-Neugenerierung. Auch direkte parallele Befehlsaufrufe
werden abgewiesen; ein Fehler gibt die Sperre im `finally` frei. Der Zustand liegt
je `ShellViewModel` gemeinsam und gilt deshalb auch fuer neu erzeugte oder gerade
nicht sichtbare Importseiten. Solange er aktiv ist, sperrt die Shell Navigation,
Fensterschliessen, Neu/Oeffnen/Projektwechsel sowie manuelles Speichern und
„Speichern unter". Nur der an den registrierten, aktiven Import-Guard gebundene
interne Delegate darf die abschliessende Speicherung des Importablaufs ausfuehren.
Import-, Export-/Verteil- und Schacht-PDF-Guards reservieren zusaetzlich denselben
atomaren Projektvorgang der `ShellViewModel`. Dadurch koennen sich auch verdeckte oder
neu erzeugte Seiteninstanzen nicht gegenseitig ueberholen; ein interner Save ist nur
fuer den registrierten zentralen Besitzer erlaubt. Auf der Schachtseite umfasst der
Schutz Einzel- und Ordnerimport, Neueinlesen eines verknuepften Protokolls sowie den
PDF-Stammdatennachlauf. Er gilt bereits waehrend der Quellenauswahl, sperrt Navigation,
Projektwechsel, Schliessen und die oeffentlichen Speicherwege und bindet Projekt,
Projektpfad und Datensaetze vor der Hintergrundarbeit. Fehler und auch fehlgeschlagene
UI-Benachrichtigungen geben den Besitz wieder frei; `Dispose` meldet einen inaktiven
Guard sofort und einen noch laufenden Guard erst nach dessen sicherer Freigabe ab.
Der gemeinsame Importlauf bindet beim Start Projektinstanz,
normalisierten Projektpfad und Berichtsordner. Nach jedem asynchronen Abschnitt prueft
er Projektidentitaet und Abbruch erneut; bei einem Wechsel wird die Arbeitskopie nicht
uebernommen. PDF-/XTF-Quellkopien und die Medienverteilung verwenden dabei dieselbe
`IImportFileStagingSession`. Sie schreibt gepruefte Kopien zuerst neben der Projektdatei
unter `.import-staging/<Lauf-GUID>`, veroeffentlicht sie erst nach den Nacharbeiten und
nimmt nur die vom Lauf neu angelegten Dateien zurueck, solange das Live-Projekt noch
nicht getauscht ist. Vor dem ersten Datei-Move schreibt der Lauf alle vorbereiteten
Rollback-Ziele samt SHA-256 atomar in `.import-transaction.json`; nach `Publish` wird
der Marker mit dem tatsaechlichen Ist-Stand erneuert. `FileImportTransactionJournal`
fuehrt Markerlesen, eigentumsgebundenes Schreiben und Loeschen je Projekt unter
derselben prozessuebergreifenden Sperre aus. Nur ein fehlender oder derselben TxId
gehoerender Marker darf geschrieben werden. Ein fremder oder unlesbarer Marker bleibt
unveraendert und sperrt den Import. Cleanup und Recovery loeschen nur mit der erwarteten
TxId; ein inzwischen ersetzter Marker bleibt erhalten. Staging, Publish und Journal
weisen auch einen Projektroot oder Markerpfad ab, der selbst eine Verknuepfung ist.
Bereits vorhandene oder wiederverwendete Dateien werden nie geloescht. Unvollstaendige
Nacharbeiten und fehlgeschlagenes Speichern bleiben als
eigene Zustaende sichtbar; nach Vorschau plus Echtlauf zeigt der letzte Bericht auf den
Echtlauf. Eine XTF-Vorschau darf weder Quellen ins Rohdatenarchiv kopieren noch das
alte Rohdatenarchiv migrieren; beides geschieht nur beim echten Import.

Beim Projektladen vergleicht `ImportTransactionRecoveryService` die Marker-TxId mit
`Project.LastCommittedImportTxId` aus dem atomar gespeicherten `projekt.json`.
Gleiche TxId bedeutet: Dateien behalten und nur den eigenen Arbeitsordner aufraeumen.
Ohne Commit-Beweis werden ausschliesslich die im Marker genannten, unveraenderten
Dateien SHA-geprueft zurueckgenommen. Der Preflight prueft vor jeder Loeschung auch
Schreibschutz, exklusiven Lesezugriff, Datei-Verknuepfungen und den gesamten
Staging-Baum, ohne Verknuepfungen zu betreten. Unlesbare Marker, Hashabweichungen,
unklare Dateiarten, Verknuepfungen oder Aufraeumfehler sperren das Projektoeffnen;
der Marker bleibt zur Pruefung erhalten. Der vollstaendige Preflight veraendert bei
einem Hindernis nichts. Scheitert ein erst danach gestartetes rekursives
Staging-Aufraeumen teilweise, meldet `ProjectFolderModified` dagegen konservativ
eine moegliche Aenderung. Beim asynchronen Projektoeffnen laeuft diese dateiintensive
Recovery im Hintergrund; nur Dialoge und Projektuebernahme bleiben auf dem UI-Thread.
Hat die vorgelagerte Projektrecovery eine kaputte `projekt.json` bereits in
Quarantaene verschoben und blockiert danach der Importmarker, stellt
`ProjectRecoveryService` die gepruefte Sicherung ueber einen dauerhaften Zwischenstand
atomar und ohne Ueberschreiben wieder am Originalpfad bereit. Die Shell fuehrt nur die
strukturierten Recovery-Ergebnisse zusammen und leitet „veraendert" oder
„nicht veraendert" ausschliesslich aus deren gemeinsamem Flag ab.
Restore-Point-Erstellung, Ausduennen, Sicherungssuche, Quarantaene und Materialisierung
pruefen Projektroot und Ziele ueber dieselbe Verknuepfungsgrenze; rekursive Suchen
betreten keine Junctions oder Symlinks.
Auch bei einem normalen Speicherfehler bleibt der Marker stehen.
Ein spaeterer erfolgreicher Save persistiert die Commit-TxId; entfernt wird der Marker
erst durch den anschliessenden eindeutigen Recovery-Lauf.

Der Ein-Knopf-Import verwendet `ImportFileTransaction` und dieselbe persistente
Wiederherstellung wie der manuelle Lauf. Archiv, Plan-PDF, Medien, namensbasierte
Protokolle, Kanal und Dichtheit schreiben in die gemeinsame Staging-Sitzung. Die
Leseseite verwendet `ResolveReadPath`/`EnumerateReadableFiles`; aus PDF-Seiten erzeugte
Dateien werden ueber `StagedDistributionOutput` und `StageGeneratedFile` aufgenommen.
Erst danach folgen Marker, `Publish`, Projekt-TxId und atomarer Projekt-Save. Bei einem
Absturz entscheidet `ImportTransactionRecoveryService` anhand derselben TxId. Das alte
Ordner-Ledger ist nur vor `Publish` aktiv und deckt noch nicht migrierte Altpfade ab.
Die Live-Referenz wird erst bei Erfolg getauscht; Projektinstanz, Pfad und inhaltliche
Projektsignatur werden vor der Uebernahme erneut geprueft. Ein fehlgeschlagener
Projekt-Save wird laut gemeldet und laesst den Marker zur eindeutigen Recovery stehen.

Die manuelle Schachtverteilung liegt hinter `IShaftDistributionService`. Ziele im
Projekt laufen ueber dieselbe Transaktion; die UI-Logik ist in
`ExportPageViewModel.ShaftDistribution.cs` getrennt. Bewusst externe Zielordner bleiben
direkte Exporte: Der Projektmarker besitzt dort keine sichere Loeschberechtigung.
Alle drei manuellen Verteilungen halten waehrend des Laufs einen Shell-weiten
Operations-Guard. Dadurch sind Navigation, Projektwechsel, Fensterschliessen sowie
manuelles Speichern und „Speichern unter" gesperrt. Haltung und Dichtheit arbeiten
mit der beim Start gebundenen Projektinstanz und pruefen diese nach dem Hintergrundlauf;
der Schachtweg verwendet dieselbe Regel auch ohne Staging. Nur ein an genau diesen
aktiven Guard gebundener interner Save darf den Abschluss speichern. Die Exportseite
meldet den Guard beim `Dispose` wieder ab.

Rekursive Import- und Quellsuchen verwenden `Application.Common.SafeFileEnumeration`.
Der ausdruecklich vom Benutzer gewaehlte Leseroot darf selbst eine Verknuepfung sein;
untergeordnete Verzeichnis- und Datei-Verknuepfungen werden dagegen nie betreten oder
geliefert. Eine normalisierte Visited-Menge verhindert doppelte Pfade und Zyklen.
Kanal-/WinCan-Suche, KIAS-Standardordner `Data`, `Film`, `Report`, die Import-Staging-
Lesesicht, Protokollsuche, Portabilitaet und Verteilquellen verwenden diese Grenze.
KIAS prueft die direkten Standardordner zusaetzlich als untrusted Kinder des
gewaehlten Roots; Datei-Symlinks zaehlen nicht als Exportbestand.
Einzelne fremde Medienquellen laufen vor dem ersten `File.Exists`, Zeitstempel- oder
Kopierzugriff ueber `ImportSourcePathGuard`. Er prueft jede vorhandene Pfadkomponente,
weist UNC-/Netzlaufwerke sowie Datei- und Verzeichnis-Verknuepfungen ab und wird vom
XTF-Medienresolver, der Medienverteilung, der Haltungs-Videozuordnung, dem Kanal-
Verteilfallback und der Projektportabilitaet gemeinsam verwendet. Ein lokal
aussehender Alias darf die UNC-Sperre nicht umgehen.

Direkte Schreibwege sind getrennt abgesichert. `ProjectWritePathGuard` verwendet die
Projektgrenze und die bestehende Reparse-Pruefung des Import-Stagings. Er prueft auch
den Projektroot selbst und wird fuer Projektstruktur, gespeicherte Importkopien,
Rohdatenarchiv, Plan-PDF, Medien, namensbasierte Protokolle, Protokoll-Neuerzeugung,
Dichtheits-KI-Fallback, Portabilitaet, Fotozuordnung, Kanal-Fallback und direkten
Schachtprotokollimport verwendet. Dazu gehoeren auch Restore-Points, Projektrecovery
und die produktiven Importberichte unter `__IMPORT_REPORTS`. Wunschziel, freier
Kollisionspfad und atomare Temp-Datei werden jeweils vor der Mutation erneut geprueft;
die Staging-Zweige bleiben unter ihrer eigenen gleichwertigen Grenze.
`DistributionWritePathGuard` bindet Haltung, Dichtheit und Schacht an den bewusst
gewaehlten Verteilroot und sperrt auch diesen Root selbst, falls er eine Junction oder
ein Symlink ist. Vor PDF-/TXT-/Video-/Info-/Unmatched- und Schacht-Mutationen werden
alle bekannten Ziele vorgeprueft. Der kleine unvermeidbare Austauschzeitraum zwischen
letzter Pfadpruefung und einer pfadbasierten Dateioperation bleibt als dokumentiertes
Restrisiko: Die verwalteten .NET-Datei-APIs halten kein durchgehendes Handle auf den
geprueften Pfad.

Dateigleichheit wird nicht aus Name, Groesse oder Teilproben abgeleitet. Fotozuordnung,
Portabilitaet, Importarchiv, Plan-PDF, Haltungsvideo, Medienkonfliktcenter,
Dichtheitsprotokoll und Schachtprotokoll vergleichen den vollstaendigen Inhalt; ein
abweichender Bestand erhaelt einen freien Zielnamen oder einen sichtbaren Konflikt.
Das Medienkonfliktcenter prueft ausserdem Haltungsroot, Zielordner und Info-Datei gegen
Verknuepfungen, bevor es kopiert oder loescht. Ohne passenden Haltungsdatensatz bleibt
der Konfliktmarker offen und es wird keine Datei kopiert. `ProjectPortabilityService` bearbeitet auch
`OriginalFotoPaths`, bewahrt Kundenbytes und relativiert nur innerhalb der echten,
separatorbewusst geprueften Projektgrenze. Haltungsmedien bleiben waehrend einer
direkten Verteilung absolut verlinkt, solange der echte Projektroot nicht bekannt ist;
`ProjectVideoReferenceNormalizer` macht nur nachgewiesen projektinterne Links beim
zentralen Projektspeichern relativ.

Haltungszuordnungen verwenden echte Zeichen-/Segmentgrenzen. `100-200` darf weder
Medien noch PDFs von `100-2000` uebernehmen. Ein fachlicher Segment-Praefix wird bei
IBAK, KINS und WinCan nur bei genau einem Kandidaten verwendet; bei mehreren
Segmenten wird ein neuer exakter Datensatz angelegt. XTF-Medienpfade weisen
Elternsegmente sowohl im Ordner als auch im Dateinamen ab. Ein Befundfoto wird nur bei
Code- oder Meterbezug einem Protokolleintrag zugeordnet; ohne Bezug gibt es keinen
beliebigen Fallback.

Ein KINS-Header ohne Beobachtungen ersetzt kein bestehendes Protokoll. Beim
Schacht-PDF-Import respektieren Stammdaten und Protokoll `fillMissingOnly`, bewahren
benutzerbearbeitete Felder und legen bei einer echten Protokollaenderung eine Revision
an. Der direkte Schachtprotokollimport verwendet eine gleichnamige Zieldatei nur bei
gleichem Inhalt. `SchachtProtocolFolderImportPolicy` sucht die Schachtnummer ueber
gueltige Vorfahren unter modernen und alten Verteilroots, ueberspringt Sanierungs-
ebenen und laesst mehrdeutige tiefe Strukturen offen. Nicht uebernommene aeltere PDFs
werden ehrlich als uebersprungen und erhalten gemeldet, nicht als archivierte
Protokollrevisionen.

Der manuelle PDF-Stapellauf bleibt bewusst getrennt vom fehlertoleranten PDF-Scan des
`ImportPostProcessingController`, weil beide verschiedene Fehlerregeln haben.

Geldrelevante Kosten-, Mengen- und Laengentexte in Kostenrechner, Matrix und Export
laufen zentral ueber `FachzahlParser` und nie ueber `CurrentCulture`: Punkt oder Komma
als Dezimaltrenner sowie korrekt gruppierte Schweizer Apostroph-/Leerzeichenwerte
werden auf de-DE, de-CH und en-US identisch behandelt; mehrdeutige Werte werden
abgelehnt. `CostCatalogStore` und
`MeasureTemplateStore` und `PositionTemplateStore` melden beschaedigte Default- oder
Override-Dateien mit
`loadError`. Kostenrechner, Haltungs-/Schachtmatrix und Builder sperren dann
Neuberechnung, Speichern und Geld-Exporte, statt mit leerem Katalog plausible
Nullwerte zu erzeugen. Fehlende, nichtpositive oder ungueltige Haltungslaengen
blockieren laengenbasierte Positionen im Kostenrechner und in der Matrix;
nichtpositive Schachtmengen blockieren Berechnung und Speichern ebenfalls.
Der Codiermodus darf `Haltungslaenge_m` nur aus einem bereits gueltigen Feld,
aus `Laenge_m` unter Erhalt seiner `FieldSource` oder aus genau einem aktiven
`BCE` ableiten. Ein BCE-Wert wird als `FieldSource.Protocol` markiert und bleibt
unterhalb echter Importquellen priorisiert. Schadensmeter und das daraus gebaute
Video-Overlay sind keine Laengenquelle; fehlt eine sichere Quelle, fragt der
Codiermodus nach einer manuellen Eingabe.
Nach einer bestaetigten Uebernahme fuegt `CodingApplyController` automatisch erzeugte
`BCD`-/`BCE`-Grenzereignisse derselben `ICodingSessionService`-Sitzung hinzu. Damit gehoeren
sie beim naechsten Uebernehmen zum echten Ausgangsstand und werden weder erneut vorgeschlagen
noch als geloeschte Ereignisse behandelt. Automatische Grenzen werden nicht als Trainingsfall
gespeichert; Abbrechen veraendert die Sitzung nicht.
`ServiceProvider` erzeugt genau eine live lesende `IProtocolPdfLayoutSettings`-Instanz aus
`AppSettings`. `ProtocolPdfExporter` sowie die produktiven Dossier-Dialogwege verwenden
dieselbe Instanz; beim Klick wird `settings.json` nicht erneut geladen. Erlaubt sind 1, 2, 4
oder 6 Fotos je Seite, unbekannte Werte fallen auf 2 zurueck; explizite Exportoptionen haben
Vorrang vor der Programmeinstellung.
Der aktive `CodeCatalog` wird dem gemeinsam genutzten `ProtocolPdfExporter` als Standard
mitgegeben; ein expliziter Katalog in `HaltungsprotokollPdfOptions` hat Vorrang.
`ObservationZustandBuilder` liefert fuer Befundetabelle, Haltungsgrafik und Fototitel
denselben deduplizierten Klartext aus Katalogtitel, Operateurtext und vorhandenen Parametern.
Uhrlagen werden nur aus gueltigen Uhrwerten gelesen; ein alter WinCan-Meterwert wie
`2.62136` darf weder als `2 Uhr` noch als `Schadenlage` weitergereicht werden.
Die Haltungsgrafik laeuft oben nach unten in Aufnahmerichtung. Deshalb spiegelt
`flowDown` nur den Fliesspfeil, nie die Kamera-Uhrlage: 1-5 Uhr liegen auf dem Blatt links,
7-11 Uhr rechts; der Stutzenwinkel folgt der erfassten Stunde in 30-Grad-Schritten.
Ausgewaehlte Kostenrechner-Zeilen mit negativer Menge oder negativem Preis werden
weder summiert noch gespeichert, uebernommen oder exportiert. NPK-Codes werden in
CSV und Excel als Text ausgegeben, damit etwa `612.110` nicht zu `612.11` gekuerzt
wird.

Die Haltungs- und Schachtberichte verwenden die datenfreien Vorlagen
`Export_Vorlage/Haltungen.xlsx` und `Export_Vorlage/Schächte.xlsx`. Ihre lesbare,
reproduzierbare Quelle liegt unter `tools/ExcelVorlagenBauer/`; die dort gepinnten
Werkzeuge erzeugen Logo, sechs Diagramme, Kennzahlenformeln, Bedeutungsfarben,
Druckeinrichtung und genau eine gestaltete Musterzeile. Titel, Kopfzeile und Daten
beginnen verbindlich in den Zeilen 25, 26 und 27. Formeln und bedingte Formatierung
reichen bis Zeile 5000, deshalb lehnt der Export mehr als 4'974 Datensaetze klar ab.
Beide im Bestand belegten Pruefresultat-Familien werden gezaehlt und gefaerbt, ohne
gespeicherte Werte umzudeuten.

`ExcelTemplateExportService` fuellt ausschliesslich eine geladene Arbeitsmappe:
fehlende Werte bleiben echte Leerzellen, Kennungen und Datumsangaben bleiben Text,
definierte Mengen und Kosten werden als Zahlen geschrieben. Ein nichtleerer,
ungueltiger Zahlenwert blockiert den Export mit Zeile und Spalte; ein bestehendes
Ausgabeziel bleibt dabei unveraendert. Schachtspalten verwenden einen expliziten
Aliasvertrag. Die Linkspalte liest der Reihe nach `Link`, `PDF_Path`, `PDF_Eigen` und
`PDF_All`. Relative Projektverweise werden mit dem beim Start gebundenen Projektpfad
zu absoluten Dateilinks aufgeloest; Pfadausbrueche bleiben unanklickbarer Text. Der
Dienst schreibt zuerst eine Temp-Datei im Zielordner, prueft XLSX-Pflichtteile und
Blatt erneut und veroeffentlicht erst danach. Die additiven
`IExcelExportService`-Overloads mit `CancellationToken` pruefen den Abbruch beim
Laden, je Datensatz und an jeder Veroeffentlichungsgrenze. Ein Abbruch waehrend des
synchronen ClosedXML-Schreibens wird am naechsten sicheren Punkt wirksam: Die
Temp-Datei wird entfernt und ein bestehendes Ausgabeziel bleibt unveraendert. Die
Vorlage darf nie selbst Ziel sein und bleibt bytegleich.

Der Haltungs-Export zieht abgeleitete Kosten nur auf einer unabhaengigen
`HoldingExcelExportSnapshotFactory`-Kopie nach. Das geoeffnete Projekt, seine
Feldmetadaten, Zeitstempel und sein Dirty-Status bleiben durch den reinen Export
unveraendert. Beide Exporte pruefen die ausgelieferte Vorlage vor Ziel- und
Kostenarbeit. Eine fehlende Vorlage oder unlesbare Kostendaten erscheinen bewusst
nur einmal als blockierender Dialog; Status und Ergebnistext werden weiterhin gesetzt.
`ExcelExportVorlagentreueTests` schuetzen Datenfreiheit, Formeln, Diagramme, Farben,
Logo, Fixierung, Druck und Neuberechnung fuer beide Blaetter.

Die drei Stammdaten-Stores lehnen `null`-Strukturen, doppelte normalisierte
Kosten-/Vorlagen-Identitaeten und negative Mengen ab. Vor jedem Save wird auch eine
vorhandene Override-Datei neu gelesen; ein frisch erzeugter Store darf deshalb keine
beschaedigte Datei ueberschreiben, selbst wenn vorher kein Load aufgerufen wurde.
`CostStoreFileProbe` unterscheidet fehlende Dateien von Ordnern, Verknuepfungen und
unlesbaren Pfaden. `ProjectCostStoreRepository` verwendet diese Pruefung fuer
`costs.json`, `schacht_costs.json` und `schacht_empfehlungen.json`, liest ein
vorhandenes Ziel unmittelbar vor jedem Save erneut und ueberschreibt bei einem
Lesefehler nichts. Der Schacht-Massnahmendialog oeffnet in diesem Zustand nicht.

`PdfPrimaryDamageFindingBuilder` wandelt die aus PDF-Tabellen gelesenen Zeilen aus
`Primaere_Schaeden` in strukturierte `VsaFinding`-Eintraege um. Passende A-/B-
Streckenmarker mit gleicher Nummer und gleichem VSA-Code werden zu einem Bereich
verbunden. `PdfPrimaryDamageStructureSynchronizer` legt daraus bei fehlenden
Strukturdaten auch das Protokoll an. Bereits vorhandene Findings oder manuelle
Protokolle werden nicht ersetzt. Dadurch kann ein erneuter PDF-Import auch bestehende
Text-only-Haltungen sicher nachziehen.

Der Export `IXtfRevisionExportService`/`XtfRevisionExportService` erzeugt aus den
unveraenderten Projektkopien unter `Imports\XTF` beziehungsweise
`Importdateien\XTF` und dem aktuellen Projektstand neue revidierte XTF-Dateien.
`VsaFinding` traegt dafuer additiv Kanalschaden- und Untersuchungs-TID;
`HaltungRecord` bewahrt die importierte `XtfHerkunft`; `SchachtRecord` hat keine
(Schaechte entstehen heute nicht aus XTF).
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

Vier Regeln dieses Wegs nie zurueckdrehen:

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
  `Lichte_Breite_mm` die Breite. Rund heisst Breite leer oder gleich, dann gibt es kein
  Verhaeltnis. Zwei verschiedene Masse ohne Profiltyp oder mit `Kreisprofil` werden
  gemeldet, nicht geraten. Erstexport: ein Rohrprofil je Profiltyp UND Verhaeltnis
  (`Rechteckprofil 1.666`), vom ilivalidator angenommen. Revision: Hoehe oder Breite
  von Hand zaehlt als Aenderung am Profil, ein geteiltes Profil bleibt unangetastet.
  Import: `RohrprofilRef` wird aufgeloest, `Profiltyp` uebernommen, Breite = Hoehe /
  Verhaeltnis; beim Kreisprofil ist die Breite gleich der Hoehe. Im Bestand fuehren
  alle 110887 Kantonsprofile `Kreisprofil` ohne Verhaeltnis, und keine der 477
  Projekt-Haltungen trug eine Breite oder einen Profiltyp; das aendert sich erst mit
  echten Rechteck- und Eiprofilen.
- **Der Eigentuemer ist ein Verweis, kein Text.** `XtfOrganisationsbuch` bindet ihn an
  eine `Organisation` im Topic `Administration` und legt fehlende an; Haltungen und
  Schaechte teilen sich EIN Buch je Datei. Fuehrt die Datei ueberhaupt keine
  Organisation, wird auch keine erfunden. Ohne bekannten `Organisationstyp` (Pflichtfeld)
  entsteht nichts. `Abwasser Uri` ist ein **Abwasserverband**, kein Kanton. Der Name
  geht zeichengleich hinaus — die Faltung in `EigentumVokabular` dient nur dem
  Typvergleich.
- **Der `Profiltyp` haengt am `Rohrprofil`**, auf das die Haltung ueber `RohrprofilRef`
  zeigt. Ein von mehreren Haltungen geteiltes Profil wird nicht geaendert.

`IXtfNeuExportService`/`XtfNeuExportService` ist der Gegenweg fuer Objekte, die es im
Kataster noch NICHT gibt — im Projekt Jagdmatt sind das 33 von 72 Haltungen, offenbar
private Anschlussleitungen. Der Revisionsweg braucht eine Originaldatei; diese Objekte
haben keine. `XtfNeuPlanBuilder` (reine Rechnung) baut den vollstaendigen SIA405-Verbund
je Haltung: `Kanal` (logisch), `Haltung` (physisch), `Rohrprofil` und ZWEI
`Haltungspunkt`e; je Schacht `Normschacht` und `Abwasserknoten`. `XtfNeuWriter` setzt den
Plan in XML um und entscheidet nichts. Es gilt dasselbe Vokabular wie beim Revisionsweg —
kein zweiter Uebersetzer.

Fuenf Regeln dieses Wegs nie zurueckdrehen:

- **Die Objektkennungen sind stabil.** Sie werden aus Projekt-Id, Klasse und fachlichem
  Schluessel abgeleitet (SHA-256, Praefix `chSST`, 16 Zeichen). Waeren sie zufaellig oder
  ein Zaehler, legte das Zielsystem bei jedem Export neue Objekte an — aus einer Korrektur
  wuerde eine Verdopplung.
- **Haltungspunkte heissen nach der HALTUNG, nicht nach dem Schacht.**
  `Haltungspunkt.Constraint1` verlangt Eindeutigkeit von Bezeichnung plus Datenherr. In
  einer Kette 1-2, 2-3 teilen sich Nachbarhaltungen ihre Schaechte; nach ihnen benannt,
  weist der ilivalidator die ganze Datei ab (real passiert, 2026-09-03). Der
  Kantonsexport macht es aus demselben Grund so. Bei Ueberlaenge (`TEXT*20`) wird gekuerzt
  und durchnummeriert — die fachliche Zuordnung traegt der Verweis auf den Abwasserknoten,
  nicht der Text.
- **Drei Verweise sind Pflicht ({1}):** `DatenherrRef`, `DatenlieferantRef` und am
  Abwasserbauwerk `EigentuemerRef`. Ohne bekannten Eigentuemer entsteht das Objekt NICHT.
  Datenherr und Datenlieferant tragen denselben Eintrag — fuer eine Ersterfassung die
  naheliegende Annahme. Der Bericht verweist auf "Leere Felder aus QGIS ergaenzen".
- **`Organisation.Status` ist MANDATORY** (`aktiv` | `untergegangen`). Fehlt es, weist der
  Pruefer die ganze Datei ab.
- **Die Geometrie kommt aus der QGIS-Kopie**, ueber `IXtfVerlaufQuelle`/
  `QgisGpkgVerlaufLeser` und die reine Byte-Logik `GpkgGeometrie` (GeoPackage-Kopf plus
  WKB, LineString und MultiLineString, EPSG:2056). Ein mehrdeutiger Name liefert nichts.
  `Verlauf` ist im Modell nicht Pflicht: Ohne Treffer geht das Objekt ohne Geometrie
  hinaus, und der Bericht sagt es.

Belegt am 2026-09-03: 44 Haltungen aus dem echten Projekt Jagdmatt, mit Verlaeufen aus
`Leitungen Lokal.gpkg`, ergeben eine vom ilivalidator 1.15.0 fehlerfrei akzeptierte Datei.

Der Rueckweg ueber `LegacyXtfImportService` war dabei an zwei Stellen kaputt, beide auch
fuer Kantonsdateien:

- **`BaulicherZustand` wurde gar nicht gelesen.** Die nachlaufende VSA-Bewertung fand in
  einer Stammdaten-XTF keine Befunde und setzte "Leitung i.O." (Klasse 4) — aus einem
  exportierten `Z0` wurde beim Zurueckimportieren eine `4`. Der Import uebernimmt den Wert
  jetzt als `FieldSource.Xtf405`, und `VsaEvaluationService.ApplyRecordFields` laesst ihn
  stehen, solange KEIN bewertbarer Befund vorliegt. Mit Befunden rechnet SewerStudio
  weiterhin selbst. Entscheid Pascal 2026-09-03: Beim Import gewinnt die Datei — nur so
  sind GEONIS und SewerStudio nach einem Austausch identisch.
- **Die Schachtmasse leben nur noch in `Dimension 1 mm` / `Dimension 2 mm`** (Entscheid
  Pascal 2026-09-03: rund = 600 / 600, oval = 1100 / 900). `SchachtMasse` in
  `Application/Schacht` ist die eine Regel dafuer: Sie liest die alten Texte ("600 mm",
  "1100 x 900 mm", "0.60/1.00"), schreibt beide Felder unter der Schreibweise des
  Datensatzes und stellt Bestandsprojekte beim Laden um (`JsonProjectRepository.Load`,
  markiert das Projekt als geaendert). Die alten Textfelder `Dimension` und
  `Durchmesser` werden dabei entfernt; nur ein unlesbarer Text bleibt sichtbar stehen.
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
- **Der Erstexport ueberspringt, was schon im Kataster steht.** Traegt eine Haltung eine
  `Objekt_ID`, wuerde ein Erstexport sie in GEONIS ein zweites Mal anlegen; sie gehoert
  in die Revision. Der Bericht sagt das mit Nummer. `Datenherr` und `Datenlieferant`
  kommen jetzt aus ihren Feldern statt pauschal vom Eigentuemer (Eigentuemer `Privat`,
  Datenherr `Abwasser Uri` ergab dreimal `Privat`).
- **WinCan: Zwei Untersuchungen je Haltung waehlen nach glaubwuerdigem Datum.** Der
  Vorgabetag `2007-12-31` und alles vor 1990 zaehlen als Platzhalter; dann entscheidet
  der Zeitstempel des Datensatzes. In Seilergasse (`07.638905-78998`) gewann sonst die
  Untersuchung mit 4 Befunden gegen die mit 12, und 9 Fotos und 1 Video fehlten still
  bei "0 Fehler". Eine uebersprungene Untersuchung erscheint jetzt namentlich im
  Importbericht.

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

Beim Teacher-Store ist die JSON-Karte verbindlich und `classes.txt` nur abgeleitet.
Scheitert das Schreiben der JSON-Karte, wird die vorherige `classes.txt`
wiederhergestellt oder eine neu angelegte Kopie entfernt.

Die versionierten Vorlagen liegen unter `training/class_maps/` und werden beim Build
nach `Data/Training/` kopiert. Die v2-Karte mit 14 Klassen und 124
Migrationszeilen bleibt eingefroren. Aktiv ist v3 mit 15 festen Klassen und 153
Migrationszeilen: 103 Teacher-Codes, 35 Legacy-Schluessel, 10 produktive Modellnamen
und 5 Annotation-Overrides. Davon sind 89 Zeilen `approved`, 64 bleiben `pending`.
`personal_gold_approval` bindet diese Entscheidungen an den Audit-SHA-256
`04f405acaa8b072b1dbd961b08d74a2baf0231b21613820933888aa966617da0`
und den gebundenen `training_samples.json`-SHA-256
`502f8d842b6b457403717a807aed6471ac503263a409caf8c9437844bd58583c`.
Die Freigabe umfasst 88 beobachtete Quellcodes; neu ist insbesondere
`BAFCZ -> BAF_oberflaeche`.
Unbekannte oder offene Klassen werden vor jeder Exportausgabe hart gestoppt; es
gibt keine stille neue ID und keinen automatischen SONST-Rueckfall.
Die Migrationsdatei prueft die Herkunfts- und persoenlichen Beleg-Hashes, die
deklarierte Zeilenzahl und die feste Aufloesungsreihenfolge. Nur der VSA-Hash wird
beim Lesen gegen die echte Datei neu berechnet; die historischen Herkunftshashes
bleiben Auditwerte der Erzeugung. `BBD_boden` wird im produktiven Befundweg ueber
`CodingFindingCodeResolver`/`VsaCodeResolver` zu `BBDZ`, nie zum nackten `BBD`.

## Plan-gesteuerter YOLO-Export (AP 0.3)

Der Datenfluss ist verbindlich:

```text
TrainingCenter
  -> duenne UI-Huelle ruft ITrainingYoloExportCoordinator
  -> freigegebenes export_registry_v1.json lesen
  -> einen aktuellen TrainingDataInventoryRuntimeSnapshot erzeugen
  -> aktive class_map v3 strikt lesen
  -> ITrainingExportPlanService erzeugt genau einen Plan
  -> ITrainingExportExecutionService nutzt Sidecar ODER lokalen Ausfuehrer
  -> ITrainingExportCompletionService markiert nur bestaetigte TrainingSamples
```

Wichtige Regeln:

- Fuer neues Training sind ausschliesslich persoenlich manuell codierte und
  bestaetigte `TrainingSample`-Eintraege zulaessig. `ConfirmedByUser` muss exakt
  mit `ApprovedBy` der Export-Registry uebereinstimmen; BBox und SAM-Segmentierung
  sind Pflicht. Teacher-, Auto-, Fremdbestaetigungen und unvollstaendige Handlabels
  bleiben im Inventar, werden aber nicht in train/val exportiert.
- `TrainingExportRegistryFileStore` liest
  `<KnowledgeRoot>\training\export_registry_v1.json` strikt. Status `candidate`,
  unbekannte Felder, fehlende Schutz-Sets oder abweichende Manifest-Hashes stoppen.
  Hauptablauf, Validierung und interne JSON-Dokumente liegen wartbar getrennt in
  `TrainingExportRegistryFileStore.cs`, `.Validation.cs` und
  `TrainingExportRegistryFileDocuments.cs`.
  Das optionale Feld `approved_sample_ids` begrenzt einen menschlich freigegebenen
  Pilot auf exakt diese TrainingSample-IDs. Ist es leer, bleibt das bisherige
  Verhalten mit allen geeigneten Goldsamples erhalten.
- Der Plan ist pfadfrei und enthaelt feste Klassen, Haltungs-Splits, Ausschluesse,
  Quell-Hashes und stabile `img_<sha256>.<endung>`-Namen. Gleiche Bild-SHAs werden
  einmal geschrieben; unterschiedliche Labels werden zusammengefuehrt.
  Beim Runden auf sechs YOLO-Nachkommastellen werden randbuendige BBox-Groessen
  erforderlichenfalls minimal nach innen begrenzt, damit eine vorher gueltige Box
  nicht durch reine Rundung ausserhalb des Bildes liegt.
- Sidecar und lokaler Ausfuehrer schreiben zuerst unter `.staging` und
  veroeffentlichen atomar nach `<KnowledgeRoot>\training\datasets\<plan_id>`.
  Bestehende unvollstaendige oder abweichende Ziele werden nie repariert oder ersetzt.
- Der Sidecar-v2-Vertrag bindet Klassen, Split, Dateiname, Labels, Klassenkarten-,
  VSA- und Registry-Hash an das C#-Manifest. `plan_sha256` muss `plan_id` entsprechen.
- Der KI-Start uebergibt `SEWER_SIDECAR_TRAINING_EXPORT_ROOT` aus demselben
  `KnowledgeRoot`. Eine abweichende Sidecar-Antwort stoppt vor der Abschlussmarkierung.
- Ein Release-Kandidat erhaelt absichtlich einen eigenen Plan mit Inventar-Run und
  Erzeugungszeit. HTTP-Wiederholungen desselben Plans sind idempotent; ein neuer
  Exportbefehl ist ein neuer Kandidat.
- Die gemeinsame Fixture unter `tests/Fixtures/TrainingExport/` fuehrt Train,
  Dev-Val und ein Multi-Label-Bild durch beide Ausfuehrer. Relative Pfade,
  SHA-256 und Bytes aller Ausgabedateien muessen identisch bleiben.
- `TrainingYoloExportWorkflow` enthaelt nur Busy-, Fortschritts- und Fehlermeldungen.
  Auswahl, Dateizugriff, Sidecar-Rueckfall und Abschluss gehoeren nicht in die UI.
- `TrainingYoloExportRuntime` in Infrastructure ist der gemeinsame Aufbaupunkt.
  WPF verwendet `CreateHybrid` (Sidecar mit lokalem Rueckfall), die StageA-CLI
  `CreateLocal`. Roots, Registry und Dataset-Ziel werden einmal gebunden und koennen
  pro Befehl nicht ausgetauscht werden. `TrainingYoloExportComposition` ist nur die
  duenne WPF-Huelle darum.
- Der Live-Inventar-Snapshot ist die einzige Sample-Wahrheit fuer Auswahl und Plan.
  Die sichtbare UI-Liste darf nur nach einem bestaetigten Export die drei abgeleiteten
  Felder `TrainingEligible`, `TrainingEligibilityReason` und `ExportedUtc` empfangen.
  Eligibility und Abschluss werden erst danach gemeinsam einmal gespeichert.
- `PlanOnly` durchlaeuft Registry, Inventar, Klassenkarte und Planer, schreibt aber
  weder `training_samples.json` noch Datensatzdateien und mutiert auch die UI-Liste nicht.
- Die reine Registrierungsliste liegt in `ServiceProviderRegistrationMap`. Sie darf
  keine Dienste erzeugen. `ServiceProvider.cs` enthaelt dadurch nur noch Aufbau und
  den abschliessenden Aufruf der Map.

Das aktive `DETECT_ALL`-Register ist fuer die 73 fachlich entschiedenen Teacher-Codes
freigegeben und nennt jede erlaubte Goldsample-ID einzeln. Nicht belegte oder weiter
offene Codes bleiben gesperrt. Diese Exportfreigabe ist keine Modellfreigabe: Der
fertig trainierte Mehrklassen-Kandidat `detect_gold_9eb020e30322` bleibt getrennt
und `not_deployed`; seine interne Validation ist keine Release-Freigabe. Der BCC-Bogen-Pilot mit
`BCC_bogen` und fester ID 14 bleibt als enger Altweg erhalten. Diese Sperren nie
automatisch umgehen.

`tools/StageAExporter` ist jetzt eine reine Kompatibilitaets-CLI vor derselben Runtime
und demselben Coordinator wie WPF. Sie besitzt keine eigene Klassen-, Split-, Label-
oder Dateilogik mehr. `--dry-run` ist ein echter `PlanOnly`-Lauf; `--val-ratio` und
`--allow-dummy-bbox` sind harte Fehler. Quelle und Ziel muessen den kanonischen Pfaden
`<KnowledgeRoot>\training_samples.json` und `<KnowledgeRoot>\training\datasets`
entsprechen. Das Tool ist Teil der vollstaendigen Solution, aber bewusst nicht des
entwicklungsnahen `AuswertungPro.Dev.slnf` ohne Hilfsprogramme.

Der fruehere `YoloDatasetExportService` ist entfernt. Er war nicht registriert und
duplizierte Klassenbildung, Bild-Split und Dateischreiben ohne den vollstaendigen
Eval-/Registerschutz. Keinen zweiten YOLO-Datensatzschreiber neben dem gemeinsamen
Coordinator und seinen beiden plan-gesteuerten Ausfuehrern einfuehren.

`training/scripts/prepare_bcc_pilot.py` erzeugt nach einer schreibfreien Vorpruefung
das enge BCC-Register und den Auditbeleg. `training/scripts/train_bcc_pilot.py`
akzeptiert danach nur einen vollstaendig gehashten Export unter
`<KnowledgeRoot>\training\datasets`, trainiert vom unveraenderten
`sidecar/models/yolo26m/yolo26m.pt` und schreibt ausschliesslich einen nicht
aktivierten Kandidaten unter `training\models\candidates`. Es startet nie bei
erreichbarem Sidecar oder weniger als 28000 MB freiem VRAM und ersetzt keine
produktiven Gewichte. Der kleine BCC-Pilot verwendet die auf dieser Hardware
gemessene Batch-Groesse 3 und standardmaessig `patience=10`; nur ein ausdrueckliches
`patience=0` erzwingt alle verlangten Epochen. Von Ultralytics erzeugte
`train.cache`/`val.cache` werden nach jedem Lauf entfernt, damit der
plan-gesteuerte Datensatz unveraendert bleibt.

Der nicht aktivierte BCC-Kandidat kann ausschliesslich im Training Studio als
reiner Fototest verwendet werden. `TrainingPreviewDetectionService` ruft dafuer
zuerst die pfadfreie, manifest- und hashgepruefte Liste ueber
`GET /detect/yolo/bcc-test/candidates` ab. Der Benutzer waehlt im bestehenden
Modellfeld genau einen Kandidaten. `POST /detect/yolo/bcc-test` erhaelt nur dessen
ID und erwartete SHA-256, niemals einen Modellpfad. Antwort-ID und -Hash muessen
exakt zur Auswahl passen; sonst werden alle Boxen verworfen. Zusaetzlich liest der
Endpunkt pro Anfrage den OSD-Meterstand desselben Bildes
(`sidecar/sidecar/osd_meter.py`, der validierte Ziffernleser; der Prototyp
`training/scripts/osd_meter_leser.py` delegiert dorthin) und liefert ihn als
additives `meter_value` (None = nicht lesbar, niemals 0,0). Das optionale
Request-Feld `meter_format` erzwingt das Zahlenlayout (`ein_dezimal` /
`vierziffern`, Default auto); die Lesung ist roh und zustandslos —
Sequenz-Plausibilitaet und Lueckenfuellen bleiben C#-Sache
(`MeterSequencePlausibility`, `MeterSequenceGapFiller`). Der Sidecar akzeptiert
nur direkte, unverknuepfte Unterordner mit `not_deployed`, Pilot `BCC_bogen`,
mindestens 30 Bildern und passender Gewicht-SHA. Die freigegebene
15er-Klassenkarte wird beim Modellladen fuer alle IDs und Namen exakt geprueft.
Der Sidecar kopiert einen einmal gelesenen, hashgeprueften Byte-Strom in eine
private temporaere Momentaufnahme; YOLO oeffnet nie erneut den veraenderbaren
Kandidatenpfad, und die Momentaufnahme wird nach dem Laden nochmals gehasht.
Dieser Pilot liefert ausschliesslich Treffer der geprueften Klasse 14 `BCC_bogen`;
Klassen 0 bis 13 werden im BCC-Endpunkt verworfen. Der Kandidat laeuft im eigenen
GPU-Slot `YOLO_TEST` und ersetzt den produktiven Artefaktzeiger nicht. Bei
VRAM-Mangel kann der allgemeine LRU-Manager den geladenen Slot `YOLO`
voruebergehend entladen; das aktive Modell wird bei Bedarf wieder geladen. Der
alte Request ohne ID bleibt nur als kompatibler automatischer Sidecar-Weg
erhalten und wird vom Training Studio nicht mehr angeboten.

### Bogen-Vorschlags-Subsystem (Bogen-Copilot, seit 2026-08-09 produktiv)

Vorabdurchlauf ueber ein Video im Training Studio (Expander "Bogen-Vorschlaege"),
der verdaechtige Bogen-Stellen als Liste zum Bestaetigen liefert — bewusst kein
Live-Overlay im Player. Aufbau: `BendSuggestionScanWorkflow` →
`IBendSuggestionScanService`/`BendSuggestionScanService` (Verdrahtung) →
`BendSuggestionScanUseCase` (Ablauf: alle Bilder fragen, Meterfolge ueber ALLE
Bilder, erst `MeterSequencePlausibility`, dann `MeterSequenceGapFiller`,
Zusammenfassung im `BendSuggestionAggregator`). Der Meterstand kommt als
`meter_value` in derselben Sidecar-Antwort (`/detect/yolo/bcc-test`), der
Format-Lock (`meter_format`) erzwingt optional das OSD-Zahlenlayout. Ohne
kalibrierten Arbeitspunkt (`workpoint.json` neben dem Kandidaten, gelesen ueber
`BendSuggestionCalibrationFileStore`/`BendSuggestionCalibrationPolicy`) laeuft
gar nichts; Kandidaten-ID und Gewicht-Hash gehen mit jeder Anfrage und werden an
der Antwort erneut geprueft. Zeilen zeigen Ort (gelesen als `Meter 9,42`,
geschaetzt mit Zusatz, fehlend als `Sekunde … (Meterstand nicht lesbar)` —
niemals `0,0`), Stufe (stark/schwach), Konfidenz und Bildzahl; Doppelklick oder
"Gross anzeigen" oeffnet `BendSuggestionPreviewWindow` mit Spitzenbild und Clip
(`IVideoClipExtractor`/`VideoClipExtractionService`, drei Haerten wie der
Bildfolgen-Extraktor). Das Sitzungsgedaechtnis `ICodingSuggestionExposure`
(`CodingSuggestionExposure`, Singleton) merkt je Programmlauf, fuer welche
Haltungen eine Liste angesehen wurde: Der `CodingEventToSampleMapper` meldet
dann auch ohne Ereignis-KI-Kontext `SuggestionShown` statt `Independent`, damit
der Assistent den unbeeinflussten Messbestand fuer `ModelPromotionPolicy` nicht
verbrennt. Abnahme: `BendSuggestionLiveAcceptanceTests` (maschinengebunden)
gegen die Repo-Fixture `tests/Fixtures/BendSuggestions/` — 226 Einzeltreffer
ohne Abweichung zum Prototyp, fuenf Stellen feldgleich. Messgrundlagen:
`docs/quality/BCC-COPILOT-2026-08-08.md` und `BCC-PDF-RECALL-2026-08-09.md`
(77,6 % Recall auf 85 protokollierten Boegen, 60,3 % Precision nach blinder
Clip-Pruefung).

Der gleich parametrische Vergleich vom 2026-07-28 (`conf=0,25`, `imgsz=1280`)
zeigt fuer `bcc_bogen_b50b37ab8a4f` auf den drei wirklich unbekannten,
geschuetzten BCC-Bildern 3/3 Treffer und eine mittlere Box-IoU von 0,8607.
Auf 9/14 kuratierten Negativbildern entstand jedoch mindestens eine Aktivierung.
Der Kandidat bleibt deshalb `not_deployed`. Die drei aelteren Kandidaten kannten
die heutigen Positivbilder bereits; der v3-Negativ-Kandidat und der neue Kandidat
kannten den heutigen Negativpool. Aus diesen 17 Bildern darf deshalb kein fairer
Gesamtsieger oder eine Produktfreigabe abgeleitet werden.

Der unabhaengige BCC-Release-Holdout wird mit
`training/scripts/bcc_release_holdout.py` aus nach dem lokalen
Basismodell-Zeitstempel aufgenommenen XTF-Fotoquellen vorbereitet. Ohne das
urspruengliche Trainingsinventar ist diese Zeitgrenze nicht vollstaendig
beweisbar. Das Werkzeug gleicht Bild-SHA-256 und beide Richtungen jeder Haltung
gegen alle lokal nachvollziehbaren Kandidaten, Trainingssamples, Negativpools,
Collapse-Berichte und Eval-Sets ab. Es kopiert Originale nur lesend in einen neuen,
atomar veroeffentlichten Ordner unter `eval_set/subsets`; vorhandene Holdouts werden
nie ueberschrieben. Kandidaten-Datensaetze werden einschliesslich Receipt,
Bild-/Labelbytes sowie der lokalen `data.yaml`- und `classes.txt`-Hashes geprueft.
`train_bcc_pilot.py` erlaubt nur `path: .`, `images/train` und `images/val` und
bindet Receipt-, YAML- und Klassen-Hash in jedes neu erzeugte Kandidatenmanifest.
Die vier Alt-Kandidaten besitzen diese direkte Manifestbindung noch nicht; ihre
heutigen drei Datensaetze sind dennoch vollstaendig gegen ihre Receipts geprueft.
Die Legacy-Ausnahme gilt nur fuer ihre exakt bekannten Kandidaten-IDs und
Manifest-SHAs; jeder neue oder veraenderte Kandidat ohne diese drei Bindungen
stoppt den Scan. Ein Manifest im BCC-Kandidatenordner ohne exakt
`pilot=BCC_bogen` stoppt ebenfalls, statt unbemerkt uebersprungen zu werden.
Bestehende eingefrorene Eval-Manifeste werden gegen jede deklarierte Datei und
gegen die exakte Bild-/Labelmenge validiert; Legacy-Collapse-Berichte werden ueber
ihre heutigen Bildpfade nachvollzogen und nicht mehr still ignoriert. Ein alter
`dateien`-Eintrag muss ein Array nichtleerer Dateinamen sein und jeder Name muss
eindeutig auf ein bekanntes Bild aufloesbar sein, sonst stoppt der Scan. Der am
2026-07-28 eingefrorene Bestand
`bcc_release_holdout_64d06094c921` enthaelt 60 Bilder aus 60 Haltungen. Seine
verdeckte Vorauswahl war 30/30 und keine Ground-Truth. Der getrennte
Blind-Review ist abgeschlossen: 60/60 Bilder, davon 29 positiv, 31 negativ und
0 ausgeschlossen. Der dynamische Status ist `ready_for_binary_evaluation`; das
eingefrorene Manifest behaelt als Erstellungsbeleg unveraendert
`dataset_status=review_incomplete` und `release_status=not_evaluated`.
Er zeigt fuer alle Bilder nur den festen Pruefauftrag `BCC — Bogen`; bildbezogener
XTF-Untercode, verdeckte Vorauswahl und Modellvorhersage bleiben unsichtbar.
Mindestens 20 bestaetigte Positiv- und 20 Negativhaltungen bedeuten nur
`ready_for_binary_evaluation`, niemals eine Modellfreigabe. Ohne menschliche Boxen
misst dieser Bestand keine Lokalisation und kein mAP. Fuer den eingefrorenen
V1-Bestand muessen Kandidatenumfang sowie die aggregierten Fingerprints der
bekannten Bild-Hashes und Haltungs-Aliase exakt gleich bleiben. Eine Aenderung,
die einen dieser Werte veraendert, sperrt den Status; eingefrorene Eval-Manifeste
werden zusaetzlich dateiexakt geprueft. Danach wird ein neuer Holdout benoetigt. Die
gebundene Review-Datei unter `C:\KI_BRAIN\eval_review` hat SHA-256
`d3c71fa37bca6bc189e2beebef75986c43a819da4094bf5eb0a36228664de663`.

`training/scripts/evaluate_bcc_release_holdout.py` vergleicht exakt den
eingefrorenen Kandidatenumfang mit festem `conf=0,25`, `imgsz=1280` und nur
Klasse 14 `BCC_bogen`. Es braucht die Python-Umgebung des Sidecars, sperrt einen
parallel laufenden Sidecar, liest alle Modelle aus privaten hashgeprueften
Momentaufnahmen und schreibt zuerst einen labelblinden Vorhersagebeleg. Nur
dessen neu eingelesene, SHA-gebundene Bytes werden gegen die gebundene
Review-Momentaufnahme bewertet. Technische Fehler zaehlen nie als Negativbefund;
ein Teilfehler verhindert den endgueltigen Auswertungsbericht. Aufhebungsmarker,
Klassenkarte, Bildbytes, Geraet, Qualitaetsgrenzen und Laufzeitversionen werden
mitgebunden. Das Werkzeug trainiert, aktiviert und ersetzt kein Modell.

`tools/PdfCodeScanner` erzeugt daneben eine rein lesende protokollbasierte
BCC-Positionsliste. Sie fuehrt die acht gueltigen Untercodes `BCCAA`, `BCCAB`,
`BCCAY`, `BCCBA`, `BCCBB`, `BCCBY`, `BCCYA` und `BCCYB` fuer die grobe
Modellklasse `BCC_bogen` gemeinsam. Pro Befund werden PDF, Meteranfang/-ende,
exakter Videozaehlerstand und nur ein eindeutig zugeordnetes Video ausgegeben;
fehlende oder mehrdeutige Werte werden sichtbar gelassen. Der bekannte Rohcode
`BCC.YB` schliesst die ganze betroffene Haltung fail-closed aus. Der JSON-Bericht
wird atomar ausserhalb der Kundenoriginale geschrieben. Diese Liste ist erst
die Messgrundlage; ohne Modelllauf und Zuordnungstoleranz ist sie noch kein
Recall- oder Praezisionswert. Mit `--expect-holdings` und `--expect-findings`
stoppt das Werkzeug fail-closed, wenn der gescannte Bestand nicht zur zuvor
freigegebenen Ausgangszahl passt.

Die Archivmessung des BCC-Copiloten wird mit
`training/scripts/bcc_pdf_recall_bericht.py` strikt in Kalibrierung und Messung
getrennt. Gesamt-, SD- und HD-Ausgaben besitzen verschiedene Dateinamen; ein
Gruppenlauf darf den Gesamtbeleg nie ueberschreiben. Der additive
`vergleichsbestand_*.json` kennzeichnet die verbrauchte Messhaelfte ausdruecklich
nur als bekannten Vergleichsbestand, nicht als neue Release-Abnahme.
`bcc_pdf_precision_queue.py` rekonstruiert den gemessenen Arbeitspunkt aus den
gespeicherten Einzelbildern und baut eine blinde Clip-Pruefung aller Vorschlaege.
Konfidenz und PDF-Zuordnung bleiben unsichtbar. Erst die vollstaendige, an den
Queue-Hash gebundene Review darf `bcc_pdf_precision_bericht.py` auswerten;
unsichere Urteile erscheinen als untere und obere Precision-Grenze.

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
nicht pruefbaren Fall.

Seit 2026-08-14 ist die Zeichenfindung aufloesungsunabhaengig. Ihre Abstandsschranken
standen als feste Pixelwerte da, eingestellt auf SD mit rund 18 Pixel hohen Ziffern;
auf HD sind dieselben Zeichen doppelt so gross, und der Leser verlor Dezimalpunkt und
Einheit ("LZ1: 3.2m" -> "L132"). `glyphen_skala` misst jetzt die tatsaechliche
Zeichenhoehe und richtet die Schranken daran aus, skaliert aber nie nach unten —
SD bleibt dadurch unveraendert.

Zwei Regeln halten die wichtigste Eigenschaft des Lesers fest: keine Ziffer hinter
der Einheit, hoechstens ein Dezimalpunkt. Beides heisst verwerfen, nicht raten.
Ohne sie entstand aus `LZ:::6.4m3` der Wert 6,4 statt 26,4 und aus `ZLZ1:.0.1m`
der Wert 0,1 statt -0,1. Das Minus als eigenes Zeichen wurde geprueft und wieder
verworfen: Es rettete eine Lesung und kostete sieben. Negative Zaehlerstaende vor
dem Rohranfang gelten deshalb als mehrdeutig und werden nicht gelesen.

`training/scripts/osd_goldmessung.py` misst den Leser wiederholbar gegen die drei
eingefrorenen Goldsaetze und trennt richtig / falsch / nicht gelesen streng; ein
falscher Wert wandert unbemerkt ins Protokoll, ein fehlender faellt auf. Stand
(Leser `85d3a107e5b3`, Bericht-SHA-256
`3ddce99516ee8866c7bdba24fcc9cd52c01f499a1a103ff5bb3c6e124ab06aa2`):
SD 80/95, HD 15/30, HD2 43/72, zusammen 138 richtig und **0 falsch** — vorher
82 richtig und 0 falsch. Die zwei SD-Verluste waren Zufallstreffer aus erkennbar
kaputten Zeichenfolgen.

Die hashgebundene Archivwiederholung mit `osd_archiv_abdeckung_messung.py`
verarbeitet 83 eindeutige Videos an je 20 gleichmaessigen Stellen. Mit demselben
Leser: SD 43,0 % und HD 53,3 %, zusammen 45,8 % (vorher SD 22,1 %, HD 3,1 %,
zusammen 16,8 %). Videos ohne jede Lesung: SD 33 -> 16 von 60, HD 21 -> 4 von 23.
Diese Zahl misst nur, OB gelesen wurde — die Richtigkeit belegt allein die
Goldmessung. Bericht-SHA-256
`a2e13deaf17d20ca61e35feee6b042307e0a01c3d463db79022b49cc94682f6a`.
Der Bericht bindet Leser und feste Auswahl; Video-Inhalte sind ueber Pfad,
Groesse und Aenderungszeit, aber nicht per Vollhash gebunden. Der Kandidat bleibt
`diagnostic_not_deployed`.

### Trainierbarer OSD-Zeichenleser — Stufe 1 gemessen, GESCHEITERT (2026-08-16)

Entwurf `docs/superpowers/specs/2026-08-15-osd-meterleser-modell-design.md`, Plan
`docs/superpowers/plans/2026-08-15-osd-meterleser-stufe1.md`. Werkzeugkette:
`osd_frames_ziehen.py` -> `osd_ernte.py` -> `osd_kunstbilder.py` ->
`osd_datensatz.py` -> `train_osd_zeichen.py` -> `osd_schwelle_kalibrieren.py` ->
`osd_modell_goldmessung.py`, gemeinsamer Inferenzweg `osd_modell_leser.py`,
Laufzeitteil `sidecar/sidecar/osd_modell.py`. `osd_meter.py` wurde nicht angefasst.

Der Kandidat `osd_zeichen_1daf5433416d` (Gewicht-SHA-256
`1daf5433416dd4aadf33c249419cd1ff305570630eb02227670f87c0226f9cf0`) erreicht auf
den drei eingefrorenen Goldsaetzen **120 richtig und 1 falsch** (SD 67/95, HD
14/30, HD2 39/72 richtig; der falsche Wert liegt in HD2). Bericht-SHA-256
`eb07c04c7700e640c642308bc29498e553ba89ade1e56c644ed29590cbc8fdfb`.
Der Vorlagenleser steht bei 138 richtig / 0 falsch — das Modell ist in BEIDEN
Richtungen schlechter. Freigabemarke (null falsch UND >= 170 richtig) doppelt
verfehlt; Status bleibt `diagnostic_not_deployed`.

Die Ursachenkette ist gemessen, nicht vermutet:

- Die Lehrer-Ernte lieferte 932 Ausschnitte aus nur **229 von 1361** Haltungen.
  Der Lehrer liest nur seine eigenen Stile weiter; die uebrigen 1132 Haltungen
  tragen genau die Stile, die er nicht kann.
- Die kuenstlichen Bilder haben ihren Zweck erfuellt, aber nur ihren: Das
  Ziffernverhaeltnis ging von 1:10,5 auf 1:2,9. Die Stilluecke schliessen sie
  nicht — sie zeigen Rauschen mit Farbstich, kein echtes Kanalvideo.
- Auf dem Reservebestand (88 vom Training ausgeschlossene Bilder) las das Modell
  **10 von 88, davon 4 grob falsch**. Die interne Validierung sah dagegen
  hervorragend aus (P 0,965, R 0,966, mAP50 0,984) — der Abstand zwischen beiden
  Zahlen IST das Ergebnis.
- Schwaechstes Zeichen ist der Dezimalpunkt: Recall 0,761 gegen 0,98 bei allen
  anderen. Genau das Zeichen, dessen Fehlen den Wert um Faktor zehn verschiebt.

Die Schwelle wurde nur mit `--trotz-wenig-vergleichbaren-faellen` eingefroren:
Bei 10 vergleichbaren Faellen verweigert `osd_schwelle_kalibrieren.py` sonst
(Mindestmass 20). Diese Zahl ist deshalb eine Standortbestimmung, keine Abnahme.

Was trotzdem belegt funktioniert: Der Schutz sperrte 63 Goldhaltungen VOR dem
Extrahieren; keine physische Haltung lag in Train und Validation zugleich; die
Kalibrierung verweigerte von sich aus. **Stufe 1 hat ihre Frage beantwortet:**
Lehrer-Ernte und kuenstliche Bilder allein reichen nicht — die 200
handbeschrifteten schweren Faelle aus Stufe 2 sind noetig, und die Messung sagt
auch wofuer.

### Stufe 2 gemessen — der Goldbestand vertritt das Archiv nicht (2026-08-17)

Pascal hat alle 200 Karten der Handliste entschieden: 97 uebernommen, 86 "boxen
passen nicht", 17 unleserlich. Werkzeuge: `osd_frames_ziehen.py` (Bilder nach
Haltung ablegen), `osd_handlabel.py` (Modi `queue`/`publizieren`) und
`tools/EvalVisibilityReview/osd_handlabel_server.py` (Zone 4x, Mensch zieht EINEN
Kasten, Server segmentiert per `zeichen_in_kasten`).

Kandidat `osd_zeichen_c668e35d59cb` (Gewicht-SHA-256
`c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845`,
Bericht-SHA-256
`cc8cdd0d9da5dbb9825010996b37b69a76c2725db2dcd0e83876a92c8c983b90`),
Schwelle 0,25 ohne Zwang eingefroren (20 vergleichbare Faelle, Mindestmass
erreicht):

| Stand | richtig | falsch |
|---|---|---|
| Vorlagenleser | **138** | **0** |
| v1 ohne Handfaelle | 120 | 1 |
| v2 mit den 97 Handfaellen | **104** | **0** |

**Was die Handfaelle bewirkt haben:** Der falsche Wert ist weg, und die Lesequote
auf ungesehenen Haltungen hat sich verdoppelt (20 von 88 statt 10 von 88). Auf
fremden Stilen ist das Modell belegbar besser — genau das, was 3000 kuenstliche
Bilder nicht geschafft hatten.

**Warum Gold trotzdem faellt:** 64 der 97 handbeschrifteten Anzeigen bestehen NUR
aus Zahlen ohne `LZ`-Beschriftung. Beim Lehrer sind es 3 von 932 — er liest
praktisch nur einen Anzeigetyp. Die drei Goldsaetze bestehen fast ausschliesslich
aus diesem beschrifteten Stil. Das Modell hat also einen Stil gelernt, der auf
Gold kaum vorkommt, und ist beim Goldstil unsicherer geworden.

**Der eigentliche Befund ist damit nicht "zu wenige Daten", sondern eine
Messlatte, die den Bestand nicht vertritt.** Solange die drei Goldsaetze nur den
beschrifteten Stil messen, sieht jede Verbesserung auf den anderen zwei Dritteln
des Archivs wie eine Verschlechterung aus. Ein vierter, stilgemischter Goldsatz
muesste her, BEVOR weitere Handarbeit ins Training geht.

Weiter offen: Die Ablehnungsquote der Handliste lag bei 43 % (86 von 200), fast
ausschliesslich wegen der Segmentierung — Zeichen ausserhalb des Satzes wurden nur
EINMAL versucht, die Sorge um das `+` war unbegruendet. `zeichen_in_kasten`
reagiert nicht monoton auf die Kastengroesse (bei +5 px Rand brach ein
verifizierter Fall von 8 auf 5 Zeichen ein, bei +10 und +20 px stimmte er wieder);
die Live-Vorschau macht das handhabbar, eine erneute Runde sollte es aber
vorher verbessern.

Beide Kandidaten bleiben `diagnostic_not_deployed`. Der Vorlagenleser bleibt
vorne in der Kette.

### Vierte Messlatte gemessen — die 0 falsch gehoerten der Messlatte (2026-08-17)

`training/scripts/osd_goldsatz.py` zieht mit `queue`/`einfrieren` einen
stilgemischten Goldsatz gleichmaessig ueber die freien physischen Haltungen,
ausdruecklich NICHT nach Lesbarkeit. Dreifache Sperre: Gold, Reservebestand UND
Trainingsmaterial (Lehrer-Ernte und Handliste, fail-closed ueber ihre Belege).
Die Trainingssperre steht bewusst NICHT in `osd_schutz.lade_schutz()` — diesen
Schutz laden auch die Trainingsskripte, dort ist Trainingsmaterial per
Definition erlaubt.

Der eingefrorene Satz `osd_mix_v1` (Manifest-SHA-256
`5ed76a78087021803b9084ac0ff643aab347d2357abee43e11ef1277099d9015`) enthaelt
120 Bilder aus 120 physischen Haltungen, gezogen aus 1064 freien (366
gesperrt), persoenlich abgelesen: 119 mit sichtbarer Anzeige, 1 ohne.

Zwei Belege, dass der Satz den Bestand vertritt: Der Vorlagenleser liest hier
40,8 % der Bilder, die unabhaengige Archivmessung ergab 45,8 %. Und die
Auflösungen sind 117 SD gegen 3 HD — die drei alten Saetze wiegen mit 95 SD
gegen 102 HD das seltene HD mehr als doppelt.

| Leser | richtig | falsch | nicht gelesen |
|---|---|---|---|
| Vorlagenleser | 45 | **4** | 71 |
| v1 `osd_zeichen_1daf5433416d` | 7 | 2 | 111 |
| v2 `osd_zeichen_c668e35d59cb` | 7 | **0** | 113 |

**Die wichtigste Zahl ist die 4.** Der Vorlagenleser stand auf den drei alten
Saetzen bei 138 richtig / null falsch, und diese null galt als seine
Kerneigenschaft. Auf vertretendem Material produziert er 4 falsche Werte bei 49
Lesungen. Die null war eine Eigenschaft der Messlatte, nicht des Lesers.

Zwei der vier haben dieselbe Ursache: ein verlorenes Minus (Soll -0,01 gelesen
als 0,01; Soll -2,41 gelesen als 2,41). Das widerspricht der bisherigen Annahme,
negative Zaehlerstaende wuerden als mehrdeutig verworfen — sie werden gelesen
und liefern das Vorzeichen falsch. Von 3 negativen Sollwerten wurden 2 gelesen,
beide falsch. Die anderen zwei Faelle sind Ziffernfehler (21,7 -> 24,7;
13,8 -> 13,88). Von 20 Bildern mit Rohranfang 0,00 liest er nur 4 richtig.

`messe_satz()` zaehlt eine Lesung auf einem Bild mit ausdruecklichem
`menschlich_lesbar=false` jetzt als `falsch` mit Grund `erfunden` — eine
erfundene Zahl wandert unbemerkt ins Protokoll. Die drei alten Saetze aendern
sich dadurch nicht; sie tragen das Feld nur mit `true`.

Beide Messwerkzeuge kennen `--satz`. Ein Lauf ueber einen anderen Bestand
beurteilt die Freigabemarke NICHT (`freigabe_erreicht=null`, nicht `false`):
"170 von 197" ist an die drei Standardsaetze gebunden. Der Berichtsname trug
zuvor nur die Kandidaten-ID; da ein bestehender Bericht nie ueberschrieben wird,
ging die zweite Messung desselben Kandidaten still verloren (real passiert).
Zusatzmessungen tragen ihren Bestand jetzt im Namen.

Berichte: Vorlagenleser
`9ad96e2d51a1da60415398d9f2a4c9bc539dbe2663a3f43a8e107055643b2d21`, v2
`117e94892466473f2a83893a6d80cbf10c3d9f313df6adf22aca67a2852a1d38`, v1
`ecabed4bc28d1089ee2b3e31c02a4c8cc9709fddfcbac99368f6a375b107b487`.

Beide Kandidaten bleiben `diagnostic_not_deployed`, und der Vorlagenleser bleibt
vorne in der Kette — er liest sechsmal mehr.

### Zwei Ursachen im Vorlagenleser behoben (2026-08-17)

Erste Eingriffe in `osd_meter.py` seit langem, beide mit gemessener Ursache. Der
neue Satz hat sie erst sichtbar gemacht.

**1. Verlorenes Vorzeichen.** Der Zwei-Dezimal-Pfad konnte ein Minus
strukturell nicht ausdruecken: Es fehlt in `ZWEI_DEZIMAL_WHITELIST`, und der
flache Strich faellt in `_zwei_dezimal_zeile` durch beide Filter — die
Zeichenpruefung verlangt `h>=6`, die Satzzeichenpruefung die Grundlinie. Im Bild
ist er klar sichtbar (`- 2.41  m`) und wird als 4x1-Fleck gefunden, aber nie
weitergegeben. `_hat_vorzeichenstrich()` verwirft die ganze Lesung, arbeitet aber
NEBEN der Maske und veraendert deren Inhalt nicht: Ein zusaetzlicher Fleck in der
OCR-Zeile hat in diesem Leser schon mehrfach belegte richtige Werte gekostet.
Gelesen wird das Vorzeichen bewusst nicht — der Vertrag ist 0..400 m, und ein
Zaehlerstand vor dem Rohranfang traegt fuer das Protokoll nichts. Die zwei
Schranken sind an der Sache begruendet: Ein Vorzeichen ist ein eigenes Zeichen
(von der Ziffer getrennt) und sitzt auf halber Zeichenhoehe. Die verschaerfte
Variante gegen die lose gemessen: null Kosten und 6,8 % Fehlalarm gegen zwei
verlorene richtige Werte und 13,2 %.

**2. Erster Treffer statt Mehrheit.** Der Vierziffern-Pfad nahm den ersten
vollstaendigen Treffer einer einzigen Schwelle; eine verlesene Ziffer wurde damit
mit voller Zuversicht geliefert (`LZ1: +0021.70 m` ergab 24,7). Der
Zwei-Dezimal-Pfad hat gegen genau diesen Fehler ein Quorum ueber fuenf
Schwellen — hier fehlte es. Neu sind `_vierziffern_masken()` (Schwellenfaecher
aus Bruchteilen des 95. Perzentils, beide Polaritaeten, bisherige Kandidaten
vorne) und `_mehrheit()`. Ohne Mindeststimmenzahl, und das ist gemessen: Eine
Mindestzahl von 2 brachte null falsche Werte, kostete aber 8 belegte richtige
(10 bei 3 Stimmen); Einzelstimmen sind hier in 10 von 11 Faellen richtig.
Abbruch bei zwei uebereinstimmenden Stimmen, weil der Pfad im Bogen-Copiloten je
Einzelbild laeuft — Laufzeit 264 -> 374 ms je Bild (Faktor 1,42) statt rund elf
Tesseract-Prozessen.

| Satz | vorher | nachher |
|---|---|---|
| `osd_sd_v1` | 80 richtig | **88 richtig** |
| `osd_hd_v1` | 15 richtig | 15 richtig |
| `osd_hd2_v1` | 43 richtig | 43 richtig |
| drei alte zusammen | 138 / 0 falsch | **146 / 0 falsch** |
| `osd_mix_v1` | 45 / 4 falsch | **48 / 1 falsch** |
| alle 317 Bilder | 183 / 4 falsch | **194 / 1 falsch** |

Der Faecher bringt also nicht nur den einen falschen Wert weg, sondern liest acht
bisher unlesbare SD-Bilder richtig.

Die hashgebundene Archivwiederholung (83 Videos, je 20 Stellen, derselbe
gebundene Bestand) steigt von 45,8 % auf 47,2 %: SD 43,0 -> 44,8 %, HD
unveraendert 53,3 %. Deutlicher wird es je Haltung — SD-Haltungen mit mindestens
70 % Abdeckung 19 -> 23 von 60, denn erst eine dichte Folge ergibt eine
brauchbare Meterspur. Haltungen ohne jede Lesung bleiben 16 (SD) und 4 (HD).
Bericht `osd_archiv_abdeckung_nach_mehrheit_20260817.json`.

Produktive Abnahme: `BendSuggestionLiveAcceptanceTests` laeuft mit echtem
Sidecar, echtem Video und dem gebundenen Kandidaten `bcc_nc15_seed46_20260808`
durch. Der Bogen-Copilot findet dieselben fuenf Stellen, die Meterwerte bleiben
innerhalb der 0,6-m-Toleranz der Repo-Fixture.

**Gemessen und ausdruecklich NICHT umgesetzt:** Die Zwei-Dezimal-Form auf dem
Vorlagenweg freizuschalten wuerde den letzten 0,00-Fall retten (`LZ1:0.00m` wird
heute verworfen), kostet aber 3 falsche Werte und zerstoert einen bereits
richtigen Goldwert (1,4 -> 31,1). Der Vorlagenleser haengt nach dem Punkt Zeichen
an (`.9` wird `.01`), und die Form unterscheidet echte von erfundenen
Nachkommastellen nicht. Die alte Entscheidung ist damit gegen vertretendes
Material bestaetigt. Der eine verbleibende falsche Wert (13,8 -> 13,88) bekommt
im ganzen Faecher nur eine Stimme; dort gibt es keine Mehrheit, die ihn
ueberstimmen koennte.

### OSD-Modell als diagnostischer Rueckfall verdrahtet und gemessen (2026-08-17)

`osd_meter.lese_meter` besitzt additiv einen optionalen Modell-Rueckfall. Er wird
erst nach Vorlagenleser sowie beiden Tesseract-Wegen aufgerufen und kann deshalb
keinen vorhandenen Wert ersetzen. Der Bogen-Copilot reicht ihn nur bei
`SEWER_SIDECAR_OSD_MODEL_FALLBACK_ENABLED=true` durch; Standard ist `false`.
Damit ist die Kette messbar, aber noch nicht produktiv freigegeben.

Der Laufzeitanschluss `models/osd_model_wrapper.py` bindet fest den Kandidaten
`osd_zeichen_c668e35d59cb`, den Gewicht-SHA-256
`c668e35d59cb4feba82b60b857663a11ac6f493104d03bf1b0414103a4a75845`
und die Schwelle 0,25. Er akzeptiert nur den Status `diagnostic_not_deployed`,
die feste 15er-Zeichenkarte und `weights/best.pt`. Das Gewicht wird vor dem
Laden aus einer privaten, erneut geprueften Momentaufnahme geoeffnet. Das Modell
belegt den eigenen, durch Busy-Lease und Watchdog geschuetzten GPU-Platz
`YOLO_OSD`; es ersetzt weder Standard- noch BCC-Modell.

`osd_kettenmessung.py` hat die vier bereits verwendeten Goldsaetze auf exakt
demselben Stand verglichen. Ergebnis: Vorlagenkette 194 richtig / 1 falsch,
Kette 224 richtig / 1 falsch. Das Modell liefert 30 neue richtige Werte und
keinen neuen falschen. Mit dem echten Bogen-Kandidaten gleichzeitig geladen
belegt der OSD-Rueckfall zusaetzlich rund 9 MB VRAM. Warm braucht eine
Modelllesung im Mittel 61 ms (Median 35 ms, p95 115 ms); ueber alle 317 Bilder
steigt die mittlere Zeit um rund 24 ms je Bild. Bericht-SHA-256:
`ef25b19df5ae1a169ea91da5b3e14e931b5c196084c596aa05732c810dcd1093`.

Diese Messung waehlt die Kette, erteilt aber keine Produktfreigabe: Alle vier
Saetze einschliesslich `osd_mix_v1` sind jetzt fuer diese Entscheidung verwendet.
Vor dem Einschalten des Standardschalters ist ein frischer, unberuehrter Bestand
Pflicht. Die 22 Sollbilder ohne gefundene Zeichen bleiben eine getrennte Baustelle
vor der Erkennung.

`training/scripts/bcc_pdf_messreserve.py` reserviert deterministisch einen neuen
reinen SD-Messbestand. Es sperrt alte Mess-, Trainings- und Eval-Haltungen samt
Gegenrichtung und akzeptiert nur die acht gueltigen BCC-Untercodes. Der aktuelle
V2-Beleg umfasst 50 SD-Haltungen mit 130 Boegen und startet mit
`reserved_not_evaluated`. Eine unabhaengige HD-Reserve existiert weiterhin nicht.

Die Archivmessung des BCC-Copiloten wird mit
`training/scripts/bcc_pdf_recall_bericht.py` strikt in Kalibrierung und Messung
getrennt. Gesamt-, SD- und HD-Ausgaben besitzen verschiedene Dateinamen; ein
Gruppenlauf darf den Gesamtbeleg nie ueberschreiben. Der additive
`vergleichsbestand_*.json` kennzeichnet die verbrauchte Messhaelfte ausdruecklich
nur als bekannten Vergleichsbestand, nicht als neue Release-Abnahme.
`bcc_pdf_precision_queue.py` rekonstruiert den gemessenen Arbeitspunkt aus den
gespeicherten Einzelbildern und baut eine blinde Clip-Pruefung aller Vorschlaege.
Konfidenz und PDF-Zuordnung bleiben unsichtbar. Erst die vollstaendige, an den
Queue-Hash gebundene Review darf `bcc_pdf_precision_bericht.py` auswerten;
unsichere Urteile erscheinen als untere und obere Precision-Grenze.

`training/scripts/osd_wahrheit_aus_protokoll.py` erzeugt OSD-Bilder aus dem
PDF-Meterstand am PDF-Videozaehlerstand. Das Ziel darf nicht unter dem
Kundenbestand liegen, wird ueber einen Arbeitsordner atomar veroeffentlicht und
nie ueberschrieben. Gleiche oder umgedrehte Haltungen bleiben im selben
Train-/Validation-/Test-Teil; bytegleiche Bilder werden nur einmal aufgenommen.
Der automatisch beschriftete Bestand startet mit `status=qa_offen`: Die zwei
belegten Zeitpunkte pruefen die grundsaetzliche Zeitachse, ersetzen aber keine
Sichtprobe ueber den ganzen Archivbestand.

Der reale Vergleich vom 2026-07-28 hatte 240 Vorhersagen und null technische
Fehler. Die zwei aufgehobenen Altlaeufe bleiben reine Diagnose. Bei den zwei noch
relevanten Kandidaten erreichte `bcc_bogen_af8020b688ac_v3_negatives`
TP/FN/TN/FP = 24/5/9/22 (Balanced Accuracy 55,9 %);
`bcc_bogen_b50b37ab8a4f` erreichte 26/3/6/25 (54,5 %). Der erste hat weniger
Fehlalarme, der zweite weniger verpasste Boegen; es gibt keinen eindeutigen
Spitzenreiter. Beide erzeugen zu viele Fehlalarme und bleiben `not_deployed`.
Der Bericht sagt deshalb `comparison_complete_not_release_qualified`. Da dieser
Holdout vier Kandidaten verglichen hat, braucht ein spaeterer Spitzenreiter vor
Aktivierung einen frischen, zuvor unberuehrten Bestaetigungsholdout.

`training/scripts/bcc_hard_negative_review.py` bereitet getrennt davon frische
BCC-Fehlalarmbilder fuer ein menschliches All-Class-Review vor. Es sperrt bekannte
Bildhashes sowie gleiche oder umgedrehte Trainings-/Eval-Haltungen, bindet class_map
v3 samt VSA-Hash und waehlt genau ein Vollbild je physischer Haltung. Die
Modellvorhersagen bleiben im lokalen Browser unsichtbar. Der eigene Pruefplatz
`tools/EvalVisibilityReview/bcc_hard_negative_review_server.py` akzeptiert nur
`all_classes_clear`, `mapped_object_visible` oder `exclude_uncertain`; das alte
Holdout-Urteil `negative` ist ausdruecklich kein Trainingsnegativ. Queue und Review
werden getrennt und atomar gespeichert. Der Review `bcc_hn_d37e1e0e481c` ist mit
14/14 Bildern abgeschlossen: 10 `all_classes_clear`, 4
`mapped_object_visible`, 0 unklar. Der Publisher hat nur die 10 vollstaendig
klassenfreien Bilder als unveraenderlichen Satz `bcc_hn_54f6608b975a`
veroeffentlicht (8 Train, 2 Validation, eine physische Haltung je Bild). Sein
Manifest bindet Bildbytes, Review, Queue-Manifest, Kandidatenliste und class_map v3
ueber SHA-256-Belege; Originale und ausgeschlossene Bilder bleiben unangetastet.

## SAM-Review im Training Center

`TrainingReviewSamWorkflow` prueft Kandidat, Box und Frame, startet den bedarfsgesteuert
erzeugten `ITrainingReviewSamSegmentationService` und bereitet Speichermaske sowie
Statustext auf. Der Rohrdurchmesser kommt ueber die zentrale Fenster-Fabrik; nur bei
fehlendem Wert gilt weiter 300 mm. `TrainingCenterWindow` bleibt fuer Schaltflaeche,
Maskenanzeige und Dialoge zustaendig. Datei-, Einstellungs- und Maskenlogik nicht wieder
in den Fenster-Code verschieben.

Der Pruefplatz im `TrainingStudioWindow` baut Workbench, Warteschlange und
KI-Bereitschaft gemeinsam ueber `TrainingStudioWindowDependencyFactory`. Beim ersten
Oeffnen prueft `TrainingStudioAiReadinessWorkflow` die Sidecar-Gesundheit und verwendet
nur bei einem Offline-Sidecar den zentralen `AiStartupService`; die Schaltflaeche
`KI starten` bietet denselben Weg fuer einen manuellen Wiederholungsversuch. Das Fenster
startet nur den ViewModel-Befehl und enthaelt keine Prozesslogik. Segmentierung und
Code-Vorschlag laufen parallel. Wenn nur einer der beiden Aufrufe scheitert, behaelt das
ViewModel das bereits abgeschlossene Teilergebnis sichtbar.

`PDF-Protokoll laden` verwendet den Application-Vertrag
`ITrainingPdfReviewImportService` und den Infrastructure-Dienst
`TrainingPdfReviewImportService`. Das Kunden-PDF wird nur gelesen und vor sowie nach
der Extraktion per SHA-256 kontrolliert. Als sicher gelten in dieser Reihenfolge:
ein Code im selben Fotoblock, eine exakte Foto-ID beziehungsweise ein exakter
Dateiname und zuletzt nur die vollständige Kombination aus Videozeit, Meter und
normalisiert identischem Befundtext. Unsichere Bilder oder Seitengrafiken werden
mit Hinweis übersprungen; mehrere Operateur-Codes am selben Foto bleiben getrennte
Prüffälle. Die extrahierten Prüfbilder liegen inhaltsadressiert unter
`<KnowledgeRoot>\training\pdf_review_imports\<vollstaendiger-pdf-sha256>`.
`PDF-Ordner laden` verwendet zusätzlich
`ITrainingPdfFolderDiscoveryService` und
`TrainingPdfReviewBatchImportUseCase`. Der Benutzer kann mehrere Wurzeln
wählen; darunter werden PDF-Dateien rekursiv, stabil sortiert und ohne
Verzeichnis-/Dateiverknüpfungen gesucht. Überlappende Wurzeln und identische
PDF-Inhalte werden dedupliziert. Die PDFs werden bewusst nacheinander gelesen;
ein Kunden-PDF wird niemals verändert und ein defektes PDF stoppt die
restlichen Dateien nicht. Der WPF-Weg zeigt Fortschritt und Abbruch, sperrt
währenddessen widersprüchliche Prüfaktionen und virtualisiert die kleinen
Vorschaubilder.

Einzel- und Ordnerimport binden vor dem ersten PDF über
`TrainingPdfReviewProtectedImportService` beziehungsweise den Batch-UseCase
einen unveränderlichen Eval-Schutzstand. Konfigurierte Schutzdaten müssen
echte SHA-256-Werte und kanonische Haltungsnummern enthalten; beim PDF-Import
ist wegen einer möglichen Farbnormalisierung mindestens eine Haltungsmenge
Pflicht. Gleiche und umgedrehte Eval-Haltungen sowie exakte Bildbytes werden
pro Foto vor Matching und Arbeitsablage ausgelassen. Ist der Schutz nicht
lesbar oder semantisch ungültig, beginnt kein PDF-Import.
`ServiceProvider.TrainingPdfReviews` registriert deshalb die geschützte
Fassade; nur der interne `TrainingPdfReviewReader` steht dem einmal geschützten
Batch als Rohleser zur Verfügung.
Der Reader stoppt grosse Protokolle fail-closed bei insgesamt mehr als 256 MiB
extrahierten Fotobytes oder 250 Millionen Fotopixeln.
JPEG-Fotos mit `DeviceCMYK`/Adobe-YCCK oder einer nicht identischen PDF-`Decode`-
Regel werden vor Vorschau, SAM und Trainingsablage ueber
`ITrainingPdfJpegColorNormalizer` in ein sichtbares RGB-PNG umgewandelt. Die
Format-, Mass- und Farbraumpruefung liegt im kleinen
`TrainingPdfEmbeddedImageReader`; die WPF-Implementierung trennt dabei die
DCT-Kanalpolaritaet von der eigentlichen PDF-`Decode`-Regel. Ein unbekannter
Farbraum, ein CMYK-JPEG ohne eindeutigen Adobe-Farbmarker oder eine fehlgeschlagene
Normalisierung wird fail-closed ausgelassen; normale RGB-JPEGs bleiben bytegleich.
Custom-Font-Verschiebungen werden einmal je Seite erkannt und identisch auf
Seitentext und lokalen Fotoblock angewandt. Ein eindeutiger Protokolltitel ist die
kanonische Haltungsnummer; nur die zweizeilige Fretz-Tabelle auf derselben
`Haltungsinspektion`-Titelseite darf eine interne Haltungsnummer als Alias binden.
`Haltungsbilder`-Titel erzeugen selbst keine Aliase, direkte `Haltung`-Felder ohne
Fretz-Haupttitel bleiben echte Abschnittsmarker. Kompakte Datumsblöcke vor der Datei-ID
werden nur bei passendem Elternordner abgetrennt. Sammel-PDFs halten die explizite
Haltung je Abschnitt und damit je `WorkbenchItem` getrennt; mehrdeutige Abschnitte
werden ausgelassen. Globale Befund-Fallbacks sind bei mehreren Haltungen gesperrt,
damit kein Foto Daten aus einem fremden Abschnitt übernimmt. Meter, Befundtext und
Start-/Ende-Daten eines Streckenschadens werden stattdessen nur aus dem einmal
materialisierten Text der sicher zugeordneten Haltung ergänzt.
`TrainingPdfProtocolFindingParser` kapselt dabei Befundzeilen und die
Start-/Ende-Paarung; der Metadaten-Parser bleibt für Dokument- und Haltungsdaten
zuständig.
`TrainingPdfProtocolFindingParser` kapselt dabei Befundzeilen und die
Start-/Ende-Paarung; der Metadaten-Parser bleibt für Dokument- und Haltungsdaten
zuständig.
Inspektionsdatum, vollständiger mehrzeiliger Befundtext und
sichere Von-Bis-Meter eines Streckenschadens werden als Referenz übernommen.
Code und Befund stehen nur in `WorkbenchItem.SourceSuggestion`; `ExistingCode`
bleibt Reparaturen vorhandener Samples vorbehalten. Eine unabhängige KI-Anzeige
darf diese Operateurvorgabe nicht überschreiben. Gold, KB und Teacher werden erst
nach persönlicher BBox, gültiger sichtbarer SAM-Maske und Akzeptieren geschrieben.
Das bestätigte Sample behält die Prüfspur als `SourceType=PdfPhoto` und in `Notes`
mit Dokumentname, vollständigem PDF-Hash, Seite, Foto-ID und Zuordnungsart.
`SourceReferenceCode` und `SourceReferenceDescription` bewahren zusätzlich die
ursprüngliche Operateurangabe; beide sind für PDF-Gold Pflicht.

Das Fenster bietet fuer den reinen Fototest `Aktives Standardmodell` und nach
erfolgreicher KI-Bereitschaft jeden manifest- und hashgeprueften BCC-Kandidaten
einzeln mit ID und gekuerzter SHA-256 an. Es gibt dort keine automatische
BCC-Auswahl. `TrainingStudioPreviewModelCatalog` baut diese fail-closed Liste;
`TrainingStudioPreviewPresenter` formatiert das reine Anzeigeergebnis ausserhalb
des ViewModels. Automatische Treffer erscheinen nur als blaue Vorschau-Boxen
mit Code und Klartext. Sie werden nie in `CurrentBox`, die SAM-Maske oder einen
Goldsample uebernommen. Ein Bild- oder Kandidatenwechsel verwirft ein spaetes
Vorschauergebnis. Ein Katalogfehler entfernt alte Kandidaten; ein spaetes
Katalogergebnis ueberschreibt keine neuere Benutzerauswahl. Fehlt der exakte
ID-/SHA-Pin, bleibt der Kandidat gesperrt. Ein qualitaetsbedingt nicht
ausgewertetes Foto wird ausdruecklich als `nicht geprueft` und nicht als
Negativtreffer gemeldet. Solange der Modelltest laeuft, beginnen weder ein neuer
Box-Lauf noch ein Speichervorgang. Nur die rote, vom Menschen gezogene Box kann
ueber Akzeptieren/Korrigieren gespeichert werden.
Das aktive Standardmodell darf nur bei ausdruecklichem `qualified=true` laufen.
Fehlende oder unlesbare Qualifikation sperrt den Fototest ebenfalls. Der await im
ViewModel bleibt auf dem WPF-UI-Kontext; danach gesetzte Anzeige-Eigenschaften duerfen
nicht mit `ConfigureAwait(false)` vom UI-Thread abgekoppelt werden.

Die Schaltflaeche `Foto allgemein mit KI pruefen` ist davon getrennt. Sie ruft ueber
`AnnotationWorkbenchService.SuggestPhotoAsync` den zentralen `IProtocolAiService`
mit dem ganzen Foto und dem aktiven VSA-Codekatalog auf. Der kataloggepruefte
Qwen-/KB-Vorschlag wird nur angezeigt und muss bewusst angeklickt werden. Rote
Hand-Box, SAM-Maske, bestehender Code und Beschreibung bleiben unveraendert; der
Aufruf schreibt weder Goldsamples noch KB-Daten. Der schnelle Vorschlag beim
Box-Ziehen bleibt der getrennte YOLO-Classifier-Weg. Nicht geladene Modelle und
unbekannte Klassen werden sichtbar abgewiesen statt als VSA-Code ausgegeben.
`AiInput.RequireImage` erzwingt fuer diesen Weg ein wirklich lesbares Foto; ein
reiner Text-/KB-Vorschlag ohne Bild ist verboten. Wechselt der Nutzer waehrend des
Aufrufs das Bild, wird das spaete Ergebnis verworfen.

Eine persoenlich uebernommene Auswahl aus dem VSA-Codierfenster ist dagegen eine
bewusste Handcodierung. `WorkbenchCodeSelectionMapper` uebernimmt deshalb neben
Code, Uhrlage und Stufe auch `ProtocolEntry.Beschreibung`.
`TrainingStudioViewModel.ApplyCodeSelection` ersetzt damit nur ein leeres Feld oder
den automatischen Platzhalter durch eine fertige Katalogbeschreibung mit Code;
selbst geschriebener Text bleibt erhalten. KI-Vorschlaege und direkt eingetippte
Codes erhalten weiterhin keine automatische Goldfreigabe. Rote Hand-Box, gueltige
SAM-Maske und persoenliches Akzeptieren bleiben fuer Gold immer Pflicht.

Das Training Studio zeigt den durch `PersonalGoldProgressCalculator` berechneten
Goldstand je Hauptcode mit Ziel 30-50 an und aktualisiert ihn nach jedem erfolgreichen
Speichern. Album und Fortschritt zeigen auch eigene Entwürfe und persönlich bestätigte
Reparaturfälle; sie zählen erst nach vollständiger Geometrie als Gold.
Die Schaltfläche `Segmentierung abarbeiten` lädt über `WorkbenchQueueService` eine
gezielte Reparaturliste aus demselben Sample-Bestand. Aufgenommen werden nur lesbare
eigene Bilder mit fehlender oder ungültiger SAM-Maske. Neben RLE, Maskenfläche und der
80-Prozent-Boxregel werden die gespeicherten Maskenmaße über
`TrainingImageFileProbe` gegen die echten Bildmaße geprüft. Fehlende oder unlesbare
Dateien erscheinen nicht als leere Arbeitskarten. Eine weiterhin gültige Hand-Box
steht als `WorkbenchItem.ExistingBox` bereit und startet beim Anzeigen automatisch
SAM und den getrennten Codevergleich. Ohne gültige Box zeigt die allgemeine Foto-KI
nur einen Vorschlag; der Mensch zeichnet danach selbst die Box. In dieser Liste ist
Akzeptieren ohne gültige sichtbare Maske gesperrt. Beim Nachlabeln wird das bestehende
Sample anhand seiner ID ersetzt; es entsteht kein doppelter Datensatz und es werden
keine zweiten Arbeitskopien als neue Samples angelegt. Ein Bildwechsel verwirft einen
noch laufenden Box-Lauf sicher.
Ein alter `PdfPhoto`-Entwurf wird nicht erneut angeboten, wenn exakt dieselbe
unveränderliche PDF-Referenz (Dokument-Hash, Seite, Foto), dasselbe Bild, dieselbe
Haltung und derselbe Code bereits als geometrisch gültiges `Approved`-Sample
vorliegen. Der Altentwurf bleibt zur Nachvollziehbarkeit gespeichert; nur die
Arbeitsliste blendet die erledigte Dublette aus.
Die Vorschaubild-Auswahl ist mit dem tatsaechlich bearbeiteten Bild verbunden; in
der Reparaturliste kann sie keinen noch offenen Fall ueberspringen.

Die Schaltflaeche `Goldpruefung (90)` startet eine feste persoenliche
Qualitaetspruefung mit je 15 Bildern fuer `BAB`, `BAF`, `BAI`, `BAJ`, `BBC` und
`BBF`. `GoldQualityReviewQueueUseCase` waehlt nur einzeln im freigegebenen
Exportregister enthaltene Train-/Development-Validation-Sample-IDs. Der
`GoldQualityReviewSnapshotProvider` verlangt davor einen erfolgreichen strikten
Live-Inventarlauf mit vollstaendigem Eval-Schutz; geschuetzte Bild-Hashes und
Haltungen sind ausgeschlossen. Die Auswahl bevorzugt verschiedene physische
Haltungen und verwendet kein Bild doppelt. Das unveraenderliche Sitzungsmanifest
unter `<KnowledgeRoot>\training\gold_quality_reviews` bindet Register-Hash,
Schutzfingerprint, Bild-Hash und Ausgangsbestaetigung und wird bei einem Neustart
fortgesetzt. Pro abgeschlossenem Fall entsteht zusaetzlich ein unveraenderlicher
persoenlicher Abschlussbeleg; eine blosse externe Neuspeicherung zaehlt nicht als
Pruefung. Das Training Studio zeigt vorhandene Box und gespeicherte Goldmaske zuerst
unveraendert; die KI ist nur ein Vergleich. Erst eine neu gezogene Box startet SAM
neu. Vor dem Schreiben werden der beim Laden gebundene Sample-Zeitstand und exakt
dieselben Bildbytes nochmals geprueft. Korrigierte Uhrlage und Schadensstufe werden
auch in `TrainingSample.CodeMeta` uebernommen. Ein Fall zaehlt erst nach erneutem
persoenlichem Gold-Akzeptieren und erfolgreichem Abschlussbeleg; gespeichert wird
mit derselben Sample-ID, sodass keine Dublette entsteht.
Die Metadaten-Uebernahme bestehender Samples ist in
`AnnotationWorkbenchService.SampleMapping.cs` getrennt; die reine blaue
Modellvorschau liegt in `TrainingStudioViewModel.PreviewDetection.cs` und schreibt
selbst keine Gold-Daten.

Die parallele SAM-/Code-Analyse liegt als
`TrainingStudioBoxAnalysisUseCase` in der Application-Schicht. Die UI-Koordination
der Liste ist in `TrainingStudioViewModel.RepairQueue.cs` getrennt. Vor der Aufnahme
in die Arbeitsliste prueft `TrainingImageFileProbe` neben den Bildmassen auch eine
vollstaendige Dekodierung. Der Rueckgabevertrag `WorkbenchSaveResult.GoldApproved`
ist nur nach dem vollstaendigen persoenlichen Gold-Gate wahr. Training Studio und
Foto-Annotation duerfen einen gespeicherten Entwurf deshalb weder als Gold melden
noch automatisch zum naechsten Bild weiterschalten.

Bei einer neuen normalen Pruefplatzkarte bleibt das Foto nach jedem erfolgreichen
Gold-Save sichtbar. Der Benutzer waehlt ausdruecklich `Weiteres Ereignis auf diesem
Bild` oder `Bild fertig`; Pfeiltasten, Queuewechsel, Modelltest und weitere
Codieraenderungen duerfen diese Entscheidung nicht umgehen. Ein zusaetzliches
Ereignis erhaelt eine neue Hand-Box, eigene sichtbare SAM-Maske, eigenen VSA-Code
und eine eigene Sample-ID. Es wird als `ManualCoding` ohne geerbte PDF- oder
Bestandsmetadaten gespeichert, aber per SHA-256 an exakt dieselben Bildbytes
gebunden. Der Pruefplatz setzt solche Zusatzereignisse bewusst als Punktbefund bei
`MeterStart`; ein zweiter Streckenschaden mit eigener Von-Bis-Strecke gehoert in
einen dafuer erweiterten Fachdialog und darf hier nicht geraten werden.
Mehrere Operateurbefunde desselben PDF-Fotos bleiben zuerst als getrennte
`WorkbenchItem`s zusammen und werden vollstaendig geprueft, bevor der Dialog ein
weiteres manuelles Ereignis anbietet. Ein noch nicht goldfaehiger Save wird mit
seiner zurueckgegebenen Sample-ID und dem gespeicherten Bildhash an dieselbe
Arbeitskarte gebunden; die Korrektur ersetzt diesen Draft. Abgeschlossene Karten
sind in der aktuellen Warteschlange gesperrt und koennen weder erneut gespeichert
noch doppelt gezaehlt werden. Vorhandene Reparatur- und Goldpruefungsfaelle bleiben
weiterhin Einzelfall-Queues.
`TrainingStudioBoxAnalysisUseCase.ValidateSegmentation` liefert der UI den exakten
Grund fuer eine Ablehnung. Eine formal sichtbare, aber noch nicht goldfaehige Maske
wird im Training Studio orange statt gruen gezeichnet und meldet zum Beispiel den
echten Pixelanteil innerhalb der Hand-Box. SAM-Masken werden dafuer nicht still auf
die Box beschnitten; die 80-Prozent-Schranke bleibt fail-closed.

Die Schaltflaeche `Goldalbum` oeffnet `PersonalGoldAlbumWindow`. Das Fenster liest
ueber `IPersonalGoldAlbumService` ausschliesslich persoenlich bestaetigte Handlabels,
gruppiert sie nach Hauptcode und zeigt Bild, Code, Beschreibung, Datei- und
Geometriestatus. Es ist rein lesend und veraendert weder Bilder noch Trainingsdaten.

Neue Bilder koennen unter `<KnowledgeRoot>\training\gold_inbox` vorbereitet werden.
`PersonalGoldInboxFileService` legt die Hauptcode-Unterordner mit Code und Klartext,
zum Beispiel `BAB - Riss` und `BCA - Seitlicher Anschluss`, sowie
`_OHNE_ZUORDNUNG` sowie `_ERLEDIGT` an. Es liest nur JPG/JPEG/PNG aus der Wurzel
und der ersten Ordnerebene; alte reine Codeordner wie `BAB` bleiben lesbar,
`_ERLEDIGT` wird uebersprungen,
und folgt keinen Datei- oder Ordnerverknuepfungen. `Gold-Eingang oeffnen` zeigt den
Ordner, `Eingang laden` uebergibt den Stapel an den vorhandenen Pruefplatz. Der
Ordnername ist nur ein sichtbarer Hauptcode-Hinweis und wird nie automatisch als
finaler VSA-Code akzeptiert. Eingangsdateien bleiben unveraendert. Erst Codieren,
BBox, SAM-Segmentierung und persoenliches Akzeptieren erzeugen das Goldsample und
die inhaltsadressierte Kopie unter
`gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>`.

Goldstand, Goldalbum und Ordnerhinweis zeigen Hauptcodes ebenfalls mit Klartext.
Der nicht als Basiscode vorhandene BBD-Anker wird dabei fachlich als
`BBD - Eindringender Boden` bezeichnet und nicht mit der allgemeinen BB-Gruppe.

Beim Bestaetigen legt `AnnotationWorkbenchService` das unveraenderte Bild zuerst
inhaltsadressiert unter
`<KnowledgeRoot>\gold_frames\<Hauptcode - Klartext>\gold_<sha256>.<endung>` ab.
Der endgueltig gespeicherte Code bestimmt den Ordner; das gilt auch nach einer
persoenlichen Korrektur eines KI-Vorschlags.
Das Kundenoriginal bleibt unberuehrt. Scheitert die sichere Goldkopie, wird
nichts gespeichert. `TrainingFrameFileStore` prueft bestehende und neue Bildbytes;
eine beschaedigte alte Zieldatei wird nicht als Treffer akzeptiert, sondern durch
eine gepruefte atomare Kopie ersetzt.

Der Speicherweg trennt seit 2026-07-25 streng zwischen Entwurf und Gold
(„Gold-Wahrheit"). Vor dem Schreiben lehnt `GoldBeschreibungGuard`
Platzhalter-Texte („Ausmass ergaenzen") ab, und `SamMaskValidator`
(Infrastructure, neben `SamMaskDecoder`) prueft die Maske: nicht `Degraded`,
RLE strikt dekodierbar (Laufsumme = Breite x Hoehe), mindestens ein gesetztes
Pixel und mindestens 80 % aller Maskenpixel-Mittelpunkte innerhalb der Hand-Box.
Maskendimensionen müssen den echten Pixelmassen des Goldbilds entsprechen; die
Maskenfläche wird aus der RLE abgeleitet und nicht aus Sidecar-Metadaten vertraut.
Gerade und ungerade RLE-Tokenzahlen sind erlaubt, weil der echte Sidecar-Encoder
keinen kuenstlichen Abschlussrun anhaengt; Startwert und Runs bleiben streng.
Nur mit gueltiger Maske entsteht ein
Goldsample (`Status = Approved`, Gruen) mit KB-Index und Teacher-Eintrag; die
Teacher-Annotation traegt dabei `SourceSampleId` als Fremdschluessel. Ohne
gueltige Maske wird nur ein Entwurf (`TrainingSampleStatus.Draft`, Gelb, kein
KB-/Teacher-Eintrag) gespeichert, der in der Warteschlange „Unvollstaendige
Goldframes" zur Reparatur erscheint; das Nachlabeln mit Maske fuehrt ueber
denselben Weg zum Goldsample. Zusaetzlich verlangt
`KnowledgeBaseManager.IsIndexWorthy` die vollstaendige persoenliche
`ManualGoldTrainingPolicy`, Box, Maske und einen fertigen Text. Platzhalter duerfen
fuer historischen reinen YOLO-BBox-Export weiterverwendet werden, gelangen aber
weder beim Neuindexieren noch aus vorhandenen KB-Zeilen ins Qwen-Retrieval.
Damit werden Entwuerfe, fremde/alte Auto-Freigaben und unfertige Texte auch bei
Nachhol-/Rebuild-Laeufen gesperrt. Das Akzeptieren ist waehrend
eines laufenden SAM-Laufs sowie bei bereits laufendem Speichern gesperrt
(ViewModel-Flags).

Die Sample-Identitaet ist die `SampleId`: `MergeOrUpdateAsync` matcht zuerst
per Id, erst danach per Signatur (Alt-Aufrufer). Eine Codekorrektur an einem
Bestandssample ersetzt den Eintrag atomar ueber
`ITrainingSampleStore.ReplaceBySampleIdAsync` (ein Sperrvorgang, ein Schreiben)
und bereinigt den alten Stand: KB-Deindex ist produktiv verdrahtet
(`TrainingKnowledgeBaseSampleDeindexer`, kein No-op), alte Teacher-Eintraege
werden per `SourceSampleId` entfernt (Mehrdeutigkeit im Altbestand → Warnung
statt Loeschen). `MergeAndSaveAsync` dedupliziert per Signatur als Sperre
gegen versehentliches Doppel-Akzeptieren; der Neuanlage-Pfad nutzt
`TryAddNewAsync`, das eine uebersprungene Dublette sichtbar abweist statt
still fortzufahren (fruehere KB-Waisen entstanden genau so).

Mehrfachobjekte werden seit 2026-07-25 unterstuetzt: Neue Samples bauen ihre
Signatur mit Box als `caseId|code|meter|meter|b:x,y,w,h` (normalisiert, 3
Dezimalstellen). Zwei Schaeden mit gleichem Code am selben Meter, aber
verschiedenen Boxen, werden dadurch als zwei eigenstaendige Objekte mit
eigener SampleId, KB- und Teacher-Eintrag gespeichert; ein erneutes
Akzeptieren desselben Objekts (gleiche Box) wird weiterhin entdoppelt.
Altbestand mit 4-teiliger Signatur (ohne Box) bleibt gueltig.
Der Player-Codiermodus prueft Masken mit demselben strengen Format
(`SamMaskFormatValidator` in Application; `SamMaskValidator` in Infrastructure
delegiert dorthin und ergaenzt Degraded/Dekodierung/Box-Schnitt); ungueltige
Masken werden nicht uebernommen, das Sample bleibt sichtbar unvollstaendig.
Bei einer manuellen Rechteckmarkierung liest der Player vor der Eingabe das native
Video-Seitenverhaeltnis samt Pixel-Seitenverhaeltnis und Ausrichtung aus LibVLC,
damit Box und Maske auch bei Letterbox/Pillarbox und alten PAL-Videos auf demselben
Bildausschnitt liegen. Nach dem Loslassen wird die Box mit SAM
segmentiert und die echte Maske drei Sekunden angezeigt, bevor sich das
VSA-Codierfenster oeffnet. Das Bogen-Geometriesignal darf diese Vorschau nie durch
ein Oval ersetzen. Die bestaetigte Maske bleibt als `OverlayGeometry.SamMask` am
manuellen Ereignis erhalten und wird ohne erfundenen KI-Kontext streng geprueft in
das Trainingssample uebernommen. Schlaegt eine erneute Segmentierung fehl, wird eine
vorherige Maske entfernt und kann nicht als aktuelles Ergebnis gespeichert werden.

Eine zweite, bewusst getrennte Handmarkierung lebt im Foto-Assistenten einer bereits
geöffneten VSA-Beobachtung. `PhotoAnnotationUseCase` liest das unveränderte
Originalfoto vor und nach SAM, vergleicht den SHA-256 und bindet danach eine private
Byte-Momentaufnahme fest an Box und Maske. Das Foto-Fenster zeigt die echte Maske auf
einer eigenen Vorschau-Ebene; der Overlay-Export enthält sie nicht. Erst eine
zusätzliche sichtbare Goldbestätigung im VSA-Fenster ruft den geschützten
`AnnotationWorkbenchService.SaveAsync` mit dem finalen Code auf. Eval-Hashprüfung
und `StoreBytesAsync` verwenden dabei exakt dieselbe Momentaufnahme; der
veränderbare Originalpfad wird beim Speichern nicht erneut gelesen.
Damit bleiben Eval-Schutz, inhaltsadressierte Goldkopie, Maskenprüfung,
Dublettschutz, KB-Index und Teacher-Eintrag zentral. Dieser Foto-Weg ist der einzige
Persistenzbesitzer seiner Maske und hängt sie nicht zusätzlich an das Coding-Ereignis.
`ProtocolEntry.OriginalFotoPaths` hält dabei je Fotoslot die unveränderte Quelle,
während `FotoPaths` das vermessene Anzeigebild enthalten darf. Altprotokolle ohne
dieses additive Feld übernehmen beim ersten VSA-Laden ihren bisherigen Fotopfad als
Original; eine neue Videoaufnahme setzt beide Listen auf den neuen Frame.
`PhotoAnnotationBatchSaveUseCase` prueft bei mehreren Fotos zuerst das gesamte
eingefrorene Paket. Scheitert ein spaeterer externer Speicherschritt nach einem
Teilerfolg, wird genau der vorher eingefrorene Protokolleintrag uebernommen und mit
den bereits geschriebenen Sample-IDs verknuepft; eine nachtraegliche Umcodierung
oder ein Abbruch kann das Goldsample dadurch nicht verwaisen lassen.
`PhotoAnnotationBatchSaveUseCase` prueft bei mehreren Fotos zuerst das gesamte
eingefrorene Paket. Scheitert ein spaeterer externer Speicherschritt nach einem
Teilerfolg, wird genau der vorher eingefrorene Protokolleintrag uebernommen und mit
den bereits geschriebenen Sample-IDs verknuepft; eine nachtraegliche Umcodierung
oder ein Abbruch kann das Goldsample dadurch nicht verwaisen lassen.
Der finale Eintrag gibt auch `IsStreckenschaden` an das Goldsample weiter. Das
automatisch erzeugte Ende eines Streckenschadens erhält deshalb weiterhin weder Foto
noch Overlay, SAM-Maske oder die Trainings-Sprungmarkierung des Anfangs.
Bei einem noch offenen Streckenschaden repraesentiert das Goldfoto nur den
Startpunkt (`MeterEnd = MeterStart`); das spaetere Ende aktualisiert dieses
Bildsample nicht und erzeugt bewusst kein zweites Bildsample.
Bei einem noch offenen Streckenschaden repraesentiert das Goldfoto nur den
Startpunkt (`MeterEnd = MeterStart`); das spaetere Ende aktualisiert dieses
Bildsample nicht und erzeugt bewusst kein zweites Bildsample.

Das additive `ProtocolEntry.Training` dokumentiert separat erzeugte
`PhotoAnnotationSampleIds`. Bei `SkipAutomaticPersistence=true` ueberspringt
`CodingTrainingSamplePersistenceCoordinator` diesen Eintrag sowohl einzeln als auch
beim Session-Abschluss, damit kein zweites Goldsample entsteht. Die allgemeinen
Protokoll- und Coding-Kopierwege klonen diese Metadaten samt ID-Liste tief.

Persoenliche Entscheidungen im Player-Codiermodus verwenden denselben Goldspeicher.
`CodingEventToSampleMapper` markiert nur `Accepted` oder `AcceptedWithEdit` mit
gesetztem Benutzer und Bestaetigungszeitpunkt als `ManualCoding` sowie
`ReviewApproved`/`ReviewCorrected`. `CodingTrainingSamplePersistenceCoordinator`
prueft zuerst den Eval-Schutz, kopiert vorhandene Fotos oder den bestaetigten
Player-Frame inhaltsadressiert in den Klartext-Hauptcode-Unterordner von
`gold_frames` und speichert danach
`training_samples.json` und den KB-Status. BBox und vorhandene SAM-RLE-Daten werden
aus dem Coding-Ereignis uebernommen. Fehlt Bild, Box oder SAM, bleibt der Eintrag
sichtbar unvollstaendig und darf nicht in den Trainings-Export.
Auch die Stapelspeicherung liefert ein echtes Ergebnis zurueck. Ein Fehler wird im
Player als rotes Overlay „Training nicht gespeichert" angezeigt und nicht mehr nur
im Hintergrundprotokoll versteckt.
Auch `CodingSessionService` indexiert aus diesem Weg nur strikt persoenlich
bestaetigte Goldsamples mit vorhandenem Goldbild. Der allgemeine Session-Abschluss
darf weder fremde Freigaben aufnehmen noch persoenliche Gold-Metadaten ueberschreiben.

`tools/PersonalGoldMigration` uebernimmt bestehende persoenliche Handlabels
wiederholbar in dieselben Klartext-Hauptcode-Unterordner. Vor dem Umschalten werden alle Quelldateien
geprueft; SQLite und `training_samples.json` werden bei einem Fehler zurueckgesetzt.
Nach erfolgreicher Umstellung wird auch das Gold-Gehirn-Dateimanifest erneuert.
Die nachvollziehbare Verteilung liegt unter
`<KnowledgeRoot>\training\gold_standard\main_code_inventory_v1.json`, die Pruefspuren
unter `<KnowledgeRoot>\training\gold_migrations`. Wissens-ZIP-Sicherungen enthalten
`gold_frames` rekursiv; Kundenoriginale werden nie veraendert.

`tools/GoldBrainSeparation` trennt einen vorhandenen Mischbestand einmalig vom neuen
Gold-Gehirn. Ohne `--execute` wird nur geprueft. Im Ausfuehrungsmodus baut
`PersonalGoldBrainSeparationService` zuerst einen vollstaendigen Arbeitsstand auf,
prueft JSON, Frames und SQLite feldgenau und benennt erst dann die Ordner auf demselben
Datentraeger atomar um. Alte absolute Goldbildpfade unter der bisherigen Wissenswurzel
werden dabei sicher auf das Lokalarchiv abgebildet; externe Bildpfade bleiben
unveraendert. Ueberlappende Wissens-, Archiv-, Spiegel-, Staging- oder
Legacy-Protokollpfade sperren den Lauf vor dem Commit.
Vor der ersten Umbenennung wird neben der Wissenswurzel das atomare Journal
`<KnowledgeRoot>.gold-brain-separation.commit.json` geschrieben. Ein spaeterer
Ausfuehrungslauf setzt einen unterbrochenen Commit nur anhand kanonisch gebundener
Pfade, Besitzmarker und gepruefter Vorherzustaende auf den Ausgangsstand zurueck;
ein Dry-Run meldet das
offene Journal nur und veraendert nichts. Die Fassade bleibt klein: Input-/
Pfadpruefung, Arbeitsstand, Journal, Commit und Recovery liegen in getrennten
internen Diensten. Der komplette lokale Altstand bleibt als
`<KnowledgeRoot>_ALT_<Zeitstempel>` erhalten; der bisherige Elements-Spiegel liegt
unter `<Elements>\Brain_Archiv\KI_BRAIN_ALT_<Zeitstempel>`. Das neue aktive Gehirn
enthaelt nur persoenlich bestaetigte Handlabels und deren Embeddings. Teacher- und
Protokoll-Kontext starten leer. Der Pruefbeleg und ein Dateimanifest liegen unter
`<KnowledgeRoot>\training\gold_standard`. Altarchive tragen einen Schutzmarker und
duerfen nicht wieder als aktive Wissenswurzel angeschlossen werden.
Nach dem Umschalten prueft `PersonalGoldArchiveRecoveryService`, ob persoenlich
bestaetigte `ManualCoding`-Faelle nur noch in der archivierten SQLite-KB stehen.
Nur solche Faelle mit vorhandenem Bild und Embedding werden inhaltsadressiert
nachgeholt. Alte `TeacherAnnotation`- und `VideoTimestamp`-Zeilen werden dadurch
nicht zu Hand-Gold umgedeutet. Vor der ersten Mutation liegt das atomare Journal
`<KnowledgeRoot>.gold-archive-recovery.transaction.json` samt geprueften
Vorherkopien fuer SQLite, Trainings-JSON, Inventar, Beleg und Manifest vor. Ein
Neustart setzt diese Dateien und neu angelegte Frames idempotent zurueck. Fremde
Audit-Artefakte, Hashabweichungen, unsichere Pfade oder Junctions werden niemals
geloescht, sondern sperren die automatische Recovery zur manuellen Pruefung. Das
Journal wird erst nach dem vollstaendigen neuen Manifest entfernt. Der Nachholbeleg
`gold_brain_archive_recovery_v1.json` dokumentiert IDs und neue Goldpfade.

Die Maus-/Bildabbildung des Pruefplatzes liegt im reinen
`TrainingStudioImageGeometryMapper`. Er beruecksichtigt die tatsaechliche Lage des
`Image` im Overlay, freie Raender durch `Uniform`-Darstellung und begrenzt das Ziehen
bereits sichtbar am Bildrand. Eine Auswahl darf nur im sichtbaren Bild beginnen.
Beim Beginn einer neuen Box entfernt das ViewModel die alte Maske und den alten
Vorschlag sofort; eine alte Maske darf nie zusammen mit einer neuen Box erscheinen.

## Aktive Few-Shot-Wege

- Produktiv gibt es zwei Laufzeit-Kontextwege. Beide liefern Prompt-Beispiele und
  trainieren keine Qwen-Modellgewichte.
- Aehnliche bestaetigte Faelle kommen aus `KnowledgeBase.db` ueber `RetrievalService`.
- Freigegebene Protokolleintraege kommen getrennt aus
  `<KnowledgeRoot>\protocol_training.json`
  ueber `ProtocolTrainingFileStore`.
- Der fruehere bildbasierte `FewShotExampleStore` samt Builder und der Schaltflaeche
  `Zu FewShot` ist entfernt: Er schrieb Bilder, wurde aber von keinem KI-Prompt gelesen.
- Bestehende `fewshot_examples.json` und `fewshot_images` sind Legacy-Daten. Sie werden
  nicht veraendert und bleiben fuer alte Wissenssicherungen im Dateikatalog enthalten.
  Diese Dateien nie wieder als Prompt- oder Trainingsquelle anschliessen.

## Schutz persistenter KI-Dateien

`TeacherAnnotationFileStore`, `ProtocolTrainingFileStore` und
`AiOptimizationSessionFileStore` unterscheiden strikt zwischen Erstlauf und
Lesefehler: Eine fehlende Datei bedeutet leer; eine vorhandene, aber unlesbare oder
strukturell ungueltige JSON-Datei bricht den Vorgang ab. Danach wird nichts
gespeichert und der vorhandene Bestand bleibt unveraendert. Der Teacher-Store legt
bei ungueltigem JSON zusaetzlich eine `.corrupt`-Kopie zur Beweissicherung an.

`tools/SelfTrainingHarness` startet nicht, solange `SewerStudio.exe` laeuft. Vor
einem Harness-Lauf wird der Trainings-Store bytegenau gesichert. Die automatische
Wiederherstellung erfolgt nur, wenn SewerStudio auch waehrend und nach dem Lauf
nicht beobachtet wurde und der letzte Harness-Stand weiterhin denselben SHA-256
besitzt. Bei einer parallelen Aenderung bleibt der aktuelle Store unangetastet und
die eindeutige Harness-Sicherung fuer die manuelle Pruefung erhalten.

## Ereignisbasierte Eval-Messung (AP 0.4a, technische Grundlage)

Die fruehere Sammeldatei `EvalSetBenchmark.cs` ist entfernt. Dataset-Laden,
Benchmark-Scoring, YOLO-Baseline, Router-Plan, Klassen-Mapping, Coverage, Kontext und
CSV-Helfer liegen jeweils in einer gleichnamigen eigenen Datei. Die oeffentlichen
Klassennamen und Signaturen sind unveraendert. Verhaltenstests sichern alle sieben
CSV-/JSON-Ausgaben, inklusive Kopfzeilen und Escaping.

- `EvalSetBenchmarkCase` traegt additiv `HoldingKey`, `ExpectedSeverity`, `EventId`
  sowie den optionalen Bereich `MeterStart`/`MeterEnd`. Alte Eval-Sets bleiben ueber
  `EvalSetBenchmarkDataset.Load` lesbar.
- Ein Release-Kandidat muss stattdessen durch
  `EvalSetReleaseDatasetValidator.LoadAndValidate`. Fehlende Bilder oder
  Haltungskennungen, bei Schaeden fehlende Ereignis-IDs, ungueltige Severity und
  widerspruechliche Meterbereiche stoppen. Nicht-Schaeden brauchen keine kuenstliche
  Ereignis-ID.
- `EvalSetV2Builder` uebernimmt die neuen Felder und verlangt Severity sowie
  Ereignis-ID fuer Schadensfaelle.
- `EvalSetEventScorer` zaehlt ein Schadensereignis ueber mehrere Frames nur einmal.
  Der Schluessel besteht aus Haltung plus EventId; gleiche EventIds in verschiedenen
  Haltungen bleiben deshalb unabhaengige Ereignisse.
  Detect-Treffer und nachgelagertes Gate werden getrennt ausgewiesen. Fuer Severity
  4/5 gilt ein Mindestumfang von 20 unabhaengigen Ereignissen; Wilson- und exakte
  95-Prozent-Fehlergrenzen werden mit ausgegeben.
- Das vorhandene 120er-Set ist noch nicht menschlich mit Severity und EventId
  nachgepflegt. AP 0.4 ist deshalb nicht abgeschlossen und keine Modellfreigabe darf
  allein aus der neuen technischen Messlogik abgeleitet werden.
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

- `EvalReviewedDamageDataset` bindet diese getrennte Review nur bei passendem
  `_candidates.json`-SHA-256, vollstaendigen Entscheidungen und null Konflikten an
  den Benchmark. `EvalReviewedDamageScorer` misst Schadenspraesenz, Fehlalarme,
  exakten Code, Hauptcode, Stufe und Ereignisse. `EvalSetBenchmark --review-file`
  verwendet dafuer das Ollama-Bildmodell ohne YOLO-/DINO-/SAM-Hinweise; das
  QualityGate wird in diesem Modus ausdruecklich nicht als gemessen ausgegeben.
- `EvalSetBenchmark --review-file <Datei> --full-chain` fuehrt dieselben geprueften
  Bilder durch den produktiven DINO -> SAM -> Qwen-Bildanalyse ->
  Text-Code-Mapping -> QualityGate-Weg. Ein fail-closed Client sperrt dabei sowohl
  YOLO-Detect als auch YOLO-cls; der KB-Kontext bleibt ebenfalls ausgeschaltet.
  Der ausdrueckliche Pruefbefehl aktiviert nur fuer diesen Lauf das Code-Mapping,
  auch wenn der allgemeine App-KI-Schalter aus ist. CSV und JSON weisen die
  erreichten Stufen, technische Fehler, exakte numerische Stufe, QualityGate sowie
  Erkennung und gruenes Gate je Ereignis getrennt aus.
- `RawVideoDetection.SeverityLevel` traegt additiv die exakte Stufe 1-5 aus dem
  `TemporalFindingDeduplicator`. Das bestehende Textfeld `Severity` bleibt fuer
  Anzeige und Kompatibilitaet unveraendert.

## Fachdomaene Kanalinspektion

### Grundbegriffe
- **Haltung:** Kanalabschnitt zwischen zwei Schaechten (typisch 30-80m)
- **Schacht:** Zugang zum Kanal (Anfangs-/Endknoten einer Haltung)
- **DN:** Nennweite in mm (DN150=Hausanschluss, DN300=Standard, DN600+=Sammler)
- **OSD:** On-Screen Display im Video — zeigt Meterstand, Haltungsname, Datum
- **Meterstand:** Position der Kamera in der Haltung (0.00m = Anfang, z.B. 45.30m = Ende)

### Schadenscodierung (VSA-KEK / EN 13508-2)
Codes sind hierarchisch aufgebaut: **Hauptcode** (2-3 Buchstaben) + **Char1** (Untertyp) + **Char2** (Lage)

**Grundgeruest (BC-Gruppe, Bestandsaufnahme):**
- BCD = Rohranfang (Kamera faehrt in Rohr ein, Schacht sichtbar)
- BCE = Rohrende (Endknoten erreicht)
- BCA = Seitlicher Anschluss (runde/ovale Oeffnung in Rohrwand)
- BCC = Bogen (Richtungsaenderung, ueber mehrere Frames sichtbar)

**Strukturelle Schaeden (BA-Gruppe):**
- BAA = Verformung (A=vertikal, B=horizontal)
- BAB = Riss (A=laengs, B=quer, C=diagonal, D=ringfoermig, E=verzweigt)
- BAC = Bruch (A=partiell, B=total)
- BAF = Oberflaechenschaden (rauhe Rohrwandung, chemischer Angriff, Korrosion)
- BAH = Schadhafter Anschluss
- BAI = Einragendes Dichtungsmaterial
- BAJ = Verschobene Rohrverbindung (breit, versetzt, Knick)

**Betriebliche Stoerungen (BB-Gruppe):**
- BBA = Wurzeln/Bewuchs
- BBB = Anhaftende Stoffe/Inkrustation/Fett
- BBC = Ablagerung (A=Sand, B=Kies, C=verfestigt)
- BBD* = Eindringender Boden (kein Basiscode BBD, nur Untercodes)
- Die Detect-Klasse `BBD_boden` ist erlaubt; beim Rueckmapping speichert C# den
  gueltigen allgemeinen Untercode `BBDZ`, niemals den nackten Basiswert `BBD`.

### Quantifizierung
- **Uhrlage:** 12:00=Scheitel (oben), 6:00=Sohle (unten), 3:00=rechts, 9:00=links
- **Severity 1-5:** 1=optisch, 2=leicht, 3=mittel (Sanierung mittelfristig), 4=schwer (kurzfristig), 5=kritisch (Sofortmassnahme)
- **Ausdehnung:** Prozent des Rohrumfangs
- **Querschnittsverringerung:** Prozent des freien Querschnitts
- Das VSA-Codierfenster zeigt an jedem sichtbaren Q1-/Q2-Feld die fachliche
  Einheit direkt neben der Zahl (`mm`, `%`, `°` oder `Stk.`) und den erlaubten
  Bereich. Dieselbe Regel validiert die Eingabe; ein sichtbares Mengenfeld ohne
  Einheit ist nicht zulässig.
- Der aktive VSA-KEK-2020-Manifestkatalog entscheidet, welche Endcodes
  auswählbar sind. Die code- und charakterabhängigen Einheiten und Grenzwerte
  des Kanal-Pickers sind gegen den lokal installierten WinCan-Katalog
  `EN13508_VSA-2019_CH_DEU_SEC.xml` abgeglichen. WinCan-Zwischenüberschriften
  wie `Status` oder `Vertikale Richtung` sind keine Code-Klartexte.

### Punktschaden vs. Streckenschaden
- **Punktschaden:** An einer Stelle (z.B. Riss, Anschluss) — ein Meterstand
- **Streckenschaden:** Ueber Laenge (z.B. Korrosion 2.5m-8.0m) — MeterStart bis MeterEnd
- Beim manuellen Schliessen wird der Endmeter aus dem aktuellen Videoframe
  ermittelt: frische OSD-Metrierung vor Timeline-Schaetzung vor dem letzten
  Sessionwert. So darf ein sichtbarer neuer Meterstand nicht durch einen alten
  Startwert ersetzt werden.

## Coding-Regeln
- Bestehenden Code nur aendern wenn explizit gefragt
- Neue Features als separate Services mit Interface
- Tests breit einsetzen: Parser, Import, Pipeline, KnowledgeBase, UI-ViewModels und QualityGate. Keine riskanten Logik-Aenderungen ohne fokussierten Test.
- Keine NuGet-Pakete ohne Rueckfrage
- Kommentare auf Deutsch
- JSON-Schema fuer alle Qwen-Outputs (strict, kein freier Text)
