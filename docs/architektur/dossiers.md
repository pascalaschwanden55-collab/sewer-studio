# Eigentümerdossiers

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Eigentuemer-Dossiers

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

