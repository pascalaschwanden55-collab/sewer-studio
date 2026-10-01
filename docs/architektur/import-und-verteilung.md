# Import und Verteilung

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- AP07: Ein-Knopf-Import und Medienverteilung (28.09.2026)
- Auditkorrektur: Portabilitaet ordnet keine fremden PDFs zusammen (18.09.2026)
- Dichtheitsverteilung: Zielordner, Seitenfehler, Behaelter (18.09.2026)
- Ein-Knopf-Import: Fortschritt (09.09.2026)
- Befundfotos gehoeren nie in den Temp-Ordner (12.09.2026)
- (Fortsetzung aus «Wichtige Klassen»)
- (Fortsetzung aus «Begleitprotokolle der Sanierung und Videodopplung (Bürglen, 09.09.2026)»)

## AP07: Ein-Knopf-Import und Medienverteilung (28.09.2026)

- `ProjectImportOrchestrator` steuert weiter den Import. `ImportMediaPhase` bündelt
  die Medienfolge mit derselben gemeinsamen Fehlergrenze; `HoldingVideoSearch`
  entscheidet getrennt von der Dateiablage über Standardvideo und Haltung.
- KINS- und SIA405-Anreicherung reichen einen Benutzerabbruch weiter. Das
  Abbruchsignal wird vor und nach längeren Schritten geprüft. Ein Abbruch wird
  weder als KINS-Fehler gezählt noch als unkritischer SIA405-Fehler übergangen.
- Verhaltenstests schützen die Suche mit einer korrigierten PDF-Haltungsnummer,
  die Reihenfolge bei mehrdeutigen Videotreffern und beide Abbruchwege.
  Restgrenzen und Nachweise: `docs/audits/2026-09-27-wartbarkeit/AP07-PROTOKOLL.md`.

## Auditkorrektur: Portabilitaet ordnet keine fremden PDFs zusammen (18.09.2026)

- **Der Inhaltsvergleich gilt fuer alle Medientypen, nicht nur fuer Fotos.** Die Bedingung
  in `ProjectPortabilityService.ResolvePortable` hing an `copyExternalInto != null`.
  Video (`Link`), `PDF_Path` und `PDF_All` rufen aber mit `null` auf — bei ihnen entfiel
  der Vergleich ganz, und eine gleichnamige fremde Projektdatei wurde ungeprueft
  uebernommen. Weicht der Inhalt ab, wird jetzt nicht umgebogen: Fotos werden wie bisher
  ins Projekt kopiert, die uebrigen Verweise bleiben stehen und heissen `nicht aufgeloest`.
- **Eine Projektdatei gehoert je Haltung genau einem Quellverweis.**
  `PortableTargetAssignments` (Infrastructure/Import) haelt das fest. Ohne diese Sperre
  landeten zwei verschiedene externe PDFs auf derselben lokalen Datei, weil
  `PickPreferred` ohne Namenstreffer auf die Datei mit dem KUERZESTEN Namen zurueckfaellt.
  Derselbe Verweis darf dieselbe Datei mehrfach beanspruchen — `PDF_Path` und ein Eintrag
  in `PDF_All` nennen oft dasselbe Hauptprotokoll.
- **Der Namensrueckfall bleibt trotzdem erlaubt, wenn er eindeutig ist.** Ein einzelner
  Verweis auf eine verschwundene Quelle muss weiterhin auf die umbenannte Projektkopie
  zeigen: Die Verteilung schreibt `H_22149-3.01.mpg` als `20260616_22149-3.01.mpg`. Eine
  Regel «nur bei Namensgleichheit» haette genau diesen Weg zerstoert. Waechter ist der
  bestehende `MakePortable_AbsoluteExternalVideoLink_RelinksToHoldingCopyRelative`.
  Fehlen zwei Quellen bei nur einer Kandidatin, erhaelt sie der in der gespeicherten
  Reihenfolge erste Verweis; der zweite bleibt extern und wird gemeldet.
- **`PDF_All` wird ueber `StoredFileListParser` gelesen**, nicht mehr nur mit Semikolon
  zerlegt — eine gespeicherte JSON-Liste blieb sonst als ein einziger unbrauchbarer
  «Pfad» stehen. Das gespeicherte Format bleibt erhalten: Was als JSON kam, geht als JSON
  zurueck, damit ein Semikolon im Dateinamen nichts zerreisst.
- Tests: `ProjectPortabilityPdfIdentityTests` (4) neben dem bestehenden
  `ProjectPortabilityServiceTests`. Sabotageprobe 18.09.2026: Inhaltsvergleich wieder an
  `copyExternalInto` gebunden und `TryClaim` entfernt -> beide Zuordnungstests rot.
  Abnahme und Grenzen: `docs/audits/2026-09-18-portabilitaet/BEHEBUNG.md`.

## Dichtheitsverteilung: Zielordner, Seitenfehler, Behaelter (18.09.2026)

Anlass: Acht KIT-PDFs, null Erfolge. Drei Ursachen, die nichts miteinander zu tun hatten.

- **Die Verteilwurzel wird vor dem Lauf geprueft.** `VerteilzielPruefung`
  (`Application/UseCases/Verteilung`, reine Regel) prueft den Laufwerks- beziehungsweise
  Freigabestamm, NICHT den ganzen Pfad — ein fehlender Unterordner ist normal und wird
  angelegt. Eine leere Wurzel heisst "nicht konfiguriert" und ist kein Fehler. Alle drei
  Verteilwege (Haltung, Schacht, Dichtheit) rufen `VerteilzielErreichbar` vor dem Start.
  Anlass: In den Einstellungen stand `I:\`, ein Laufwerk das es nicht mehr gab; jede Datei
  meldete nur `Could not find a part of the path 'I:\...'`. `SchachtDistribution` stand
  gleichzeitig auf dem ebenfalls fehlenden `F:\verteilt`.
- **Ein Fehler auf EINER Seite beendet nicht mehr die ganze Datei.** Der Seitenrumpf liegt
  jetzt in `HoldingFolderDistributor.VerteileDichtheitSeite`, der Aufrufer faengt je Seite.
  Vorher lag der `catch` nur um das ganze PDF: Der erste nicht anlegbare Ordner verschluckte
  alle weiteren Haltungen desselben Sammelberichts — bei drei KIT-Sammelberichten waren das
  21 echte Haltungen hinter genau einem gemeldeten Fehler. Dasselbe Muster wie in Goeschenen,
  nur eine Ebene hoeher.
- **Eine Pegel-Dichtheitspruefung an einem Behaelter ist kein Haltungsprotokoll.**
  `BehaelterPruefungParser` erkennt sie nur, wenn BEIDE Merkmale vorkommen: die Kopfzeile
  `Pegel-Dichtheitspruefung` UND der Pruefgegenstand `Behaelter`. Die echten Haltungs-
  Pruefberichte derselben Firma (`Kanal-Ueberdruck Luft`) tragen keines von beiden — an den
  fuenf realen PDFs gemessen. Abgelegt wird das ganze Dokument in einem eigenen
  Bauwerksordner nach Pruefobjekt (`RB2`) mit dem Kuerzel `BP` statt `DP`; die Referenzmessung
  landet im selben Ordner, weil sie dasselbe Bauwerk nennt. Erkannt werden nur die bekannten
  Kuerzel `RB`, `RUEB`/`RUB`, `RKB`, `SKB`, `PW` samt Nummer. **Ohne lesbare Bauwerkskennung
  wird NICHTS abgelegt** und der Bericht sagt warum — lieber kein Ordner als ein geratener.
- **Zwei Rauschquellen erfanden Haltungen.** Die Hersteller-Fusszeile
  `© 2005-2025 MesSen Nord GmbH` sah aus wie das Schachtpaar `2005-2025`, und die Masstabelle
  der Anlage (`Hohe oberer Schachtring [m] 0.000` / `0.100`) ergab `000-100`. Beide landeten
  dank Katasterabgleich in `keine_Zuordnung` — der Schutz griff, der Ordnername blieb erfunden.
  `ShaftCandidateScanner.IsNoiseLine` kennt jetzt Copyright-/Herstellermarken und
  **Masseinheiten in eckigen Klammern**: Eine Schachtnummer traegt nie `[m]`. Wichtig war die
  zweite Haelfte — `TryExtractFromShafts` hat seine Zeilenschleife bis dahin GAR NICHT durch
  `IsNoiseLine` geschickt, auch nicht beim Blick auf die Folgezeile. Die Zeile
  `Hohe oberer Schachtring [m]` traegt "oberer" und "Schacht" und galt deshalb als Schachtzeile.
- Tests: `DichtheitBehaelterpruefungTests` (10, Textausschnitte woertlich aus dem PdfPig-Lauf
  der echten Dateien), `DichtheitVerteilungRobustheitTests` (3, echte PDFs und echte Ordner),
  `VerteilzielPruefungTests` (5) und der neue Fall in `ExportPageDistributionProjectGuardTests`.

## Ein-Knopf-Import: Fortschritt (09.09.2026)

`ImportOneClickProjectController` reicht einen echten UI-Fortschrittskanal weiter.
`ProjectImportOrchestrator` buendelt den bestehenden Ablauf in sieben Anzeigeschritte:
Vorbereiten, Archivieren, Quelldaten, Medien, Haltungsprotokolle, Schachtprotokolle,
Abschliessen. `ImportFortschrittText` und `ImportRestzeitSchaetzer` sind reine Regeln
in `Application/Import`; die Restzeit gilt erst ab drei erledigten Einheiten und
ausschliesslich fuer den laufenden Schritt. Medien zaehlen Haltungen. Der vorbereitete
`ShaftDistributionService` meldet Quellenversuche ueber alle PDFs statt wiederholt
1 von 1; seine Verarbeitungs- und Fehlerregeln bleiben gleich. Unzaehlbare Arbeit
erscheint unbestimmt. Details und Nachweise: `docs/IMPORT-FORTSCHRITT.md`.

## Befundfotos gehoeren nie in den Temp-Ordner (12.09.2026)

Anlass: Die Datensicherung meldete 125 fehlende verknuepfte Dateien, darunter 16 aus
`%TEMP%`. `D:\Projekte\Im_Dorf_2_6466_Bauen` trug 34 Fotoverweise dorthin. Pascal hatte den
Temp-Ordner geleert; diese Befundfotos sind verloren.

- **Ursache:** Der VSA-Code-Explorer schrieb sein Foto nach `%TEMP%\vsa_foto<N>_<guid>.png`
  beziehungsweise uebernahm den Live-Snapshot `%TEMP%\coding_live_<guid>.png` unveraendert.
  Genau dieser Pfad ging in `FotoPaths` UND `OriginalFotoPaths` und wurde mit dem Projekt
  gespeichert. Einen Kopierschritt ins Projekt gab es nicht:
  `ProjectPhotoReferenceNormalizationService` prueft nur, ob unter
  `<Projekt>\Fotos\Haltungen\...` schon etwas liegt, und laesst den absoluten Pfad sonst
  stehen. Nach dem Messen kam ueber `PhotoMeasurementOverlayExporter` noch
  `<temp>_overlay.png` dazu, also zwei verlorene Dateien je vermessener Beobachtung.
- `VsaFotoAblagePolicy` (Application/UseCases/VsaFotos, reine Rechnung) bestimmt das Ziel:
  `<Videoordner>\Fotos\vsa_foto<N>_<yyyyMMdd_HHmmss>.png`, bei Namensgleichheit `_2`, `_3`.
  Dieselbe Ablage wie `CodingSnapshotTargetPolicy`, die es im Codiermodus immer schon
  richtig machte. Ohne bekanntes Video bleibt nur der Temp-Ordner; das ist ein ehrlicher
  Rueckfall und keine dauerhafte Ablage.
- `VsaFotoAblage.Uebernehme` verschiebt das Bild dorthin. **Scheitert das Verschieben,
  bleibt die Quelle liegen und der alte Pfad wird zurueckgegeben** - ein Foto im
  Temp-Ordner ist schlecht, ein geloeschtes Foto ist schlimmer. Nie zu `File.Move` mit
  `overwrite: true` wechseln.
- `VsaCodeExplorerPhotoCaptureRequest.PersistPhoto` ist der eine Ort, an dem beide Wege
  (Live-Snapshot und aus dem Video geschnittener Frame) durchlaufen. Der Parameter ist
  additiv und optional; `CaptureWithDefaultsAsync` setzt ihn immer. Nie einen zweiten
  Ablageweg daneben bauen.
- Das Mess-Overlay braucht keine eigene Regel: Es entsteht neben seinem Quellfoto, und das
  liegt jetzt im Projekt.
- `ProgramCleanupService.SewerStudioTempFilePatterns` kennt `coding_live_*` und `vsa_foto*`
  bewusst NICHT. Diese Muster dort nicht ergaenzen, solange Altprojekte noch Verweise ins
  Temp tragen - sonst loescht das Programm die letzten vorhandenen Bilder selbst.
- Waechter: `VsaFotoAblagePolicyTests` (3), `VsaFotoAblageTests` (3, echte Dateien) und die
  zwei neuen Faelle in `VsaCodeExplorerPhotoCaptureWorkflowTests`.

## (Fortsetzung aus «Wichtige Klassen»)

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

Ein WinCan-GEP liefert ALLE Haltungsprotokolle in EINER Sammeldatei (`Misc\Docu\<Projekt>.pdf`,
List & Label); Einzeldateien je Haltung gibt es nicht. `HoldingFolderDistributor` teilt sie
anhand der Titelzeile `Haltungsinspektion - <Datum> - <Haltung>` auf. Drei Regeln dieses Wegs
nie zurueckdrehen (Goeschenen 2026-09-04: 239 Haltungsordner, 0 Protokolle, Bericht meldete
trotzdem „0 Fehler"):

- **Das Seitenbudget traegt ein ganzes Gemeinde-GEP.** `PdfImportSafetyPolicy.DefaultMaxPages`
  ist 5000. Goeschenen brauchte fuer 239 aufgenommene Haltungen 1003 Seiten (rund vier je
  Haltung) und fiel mit dem alten Budget 1000 um drei Seiten durch. Der vorsorgliche Schutz
  gegen pathologische Dateien (Audit K10/S3) bleibt; `SEWERSTUDIO_MAX_PDF_PAGES` hebt ihn
  weiter an.
- **Ein Stolperstein bei einer Haltung reisst die uebrigen nicht mit.** Der `try/catch` in
  `HoldingFolderDistributor.DistributeFiles` sitzt INNERHALB der Chunk-Schleife. Umschloss er
  wie frueher alle Chunks, machte ein einziger Fehlschlag aus 239 Erfolgen ein einziges
  Fehlerergebnis.
- **Ein nicht verteiltes Protokoll steht im Bericht.** `KanalImportDistributionService` zaehlt
  jedes Fehlerergebnis als Fehler und schreibt die ersten zehn Gruende namentlich
  (`Haltungsprotokolle nicht verteilt: <n>`); frueher uebersprang ein blosses `continue` sie
  still. Genau dieses Muster hatte schon in Hellgasse 38 Protokolle verschluckt.

### Begleitprotokolle der Sanierung und Videodopplung (Bürglen, 09.09.2026)

Anlass: In `Sanierungsabnahme_Zone_5.01_GKS_Bürglen` lagen alle zehn Dichtheitsprüfungen
und alle neun Aushärteprotokolle im **Schachtordner** statt bei ihrer Haltung — eines
unter der Nummer `1009336029`, der **Chargen-Nr. des Liners**. Gleichzeitig lag in jedem
der 19 Haltungsordner dasselbe Video zweimal (3397 von 6803 MB reine Dopplung).

- **OCR entscheidet über den Typ eines reinen Scans.** Die Quelldateien (Toshiba-Kopierer)
  haben keine Textebene. `PdfDokumenttext.LiesKopf` liest zuerst die Textebene und nur bei
  einem reinen Scan die ersten zwei Seiten per OCR; Ergebnisse werden je Programmlauf
  gemerkt (Schlüssel: Pfad, Grösse, Änderungszeit), weil Verteiler und Vorfilter dieselben
  Seiten lesen. Ohne das dauert der Ein-Knopf-Import doppelt so lange.
- **`ShaftPdfRelevance` beurteilt einen reinen Scan als EIN Dokument, nicht Seite für
  Seite.** Seite 2 eines Druckprüf- oder Aushärteprotokolls ist ein Diagramm ohne Aussage;
  die Regel „jede geprüfte Seite muss fremd sein" liess am 09.09. alle 19 Protokolle wieder
  durch, obwohl Seite 1 sie klar benannte. Gemischte Dokumente (Textseiten + Bildseite)
  bleiben unverändert im Schachtweg — eine Bildseite kann ein eingescannter Schachtteil sein.
- **`PdfDokumentTyp.Aushaerteprotokoll` ist ein eigener Typ.** Erkannt über den
  umlauttoleranten Titel `Aush.{0,2}rt(e|ungs)?protokoll` — dasselbe Blatt las das OCR als
  `Aushärteprotokoll`, `Aushirteprotokoll` und `Aushfarteprotokoll` — oder über das
  umlautfreie Begriffspaar `Linertyp` + `Lampenleistung`. Ein einzelnes der beiden Wörter
  genügt nicht.
- **Das Kürzel `DP` im Dateinamen zählt nur beim Scan ohne Textebene.** Ist Text lesbar,
  entscheidet weiterhin allein der Inhalt (bewusste Regel aus R1-R3, Waechter
  `ErkenneText_DpDateinameAlleinReichtNicht`).
- **Zugeordnet wird über die Haltungsbezeichnung des Dokuments**
  (`SanierungsprotokollZuordnung`, WPF-frei in `Application/UseCases/Import/Quellen`).
  Diese Protokolle nennen ihre Haltung nur als WinCan-Laufnummer (`H66`), und eine
  Dichtheitsprüfung deckt oft eine ganze Prüfstrecke ab: `DP H12_H13.pdf` misst von Schacht
  59435 bis 60191 über ZWEI Haltungen. Das Schachtpaar ergibt dort keine Haltung des
  Projekts, sondern einen erfundenen Ordner. **Dateiname UND Dokumenttext müssen dieselbe
  Bezeichnung nennen** — der Name allein ist eine Vermutung, dieselbe Falle wie früher bei
  der Gegenbefahrung. Mehrdeutig oder unbekannt heisst: nichts, mit Meldung.
- **`HaltungRecord.ImportBezeichnung`** trägt dafür den `OBJ_Key` der WinCan-Quelle. Additiv
  wie `XtfHerkunft`/`Geonis`, **kein Feld**: keine Tabellenspalte, kein Export, keine XTF —
  die Nummer ist nur innerhalb eines Quellprojekts eindeutig.
- **Eine Sammelprüfung landet in JEDEM betroffenen Haltungsordner** (Entscheid Pascal
  09.09.), benannt `<JJJJMMTT>_<Haltung>_DP.pdf` bzw. `_AH.pdf`. Der bestehende Weg über das
  Schachtpaar bleibt unverändert der Rückfall für Dichtheitsprüfungen; ein Aushärteprotokoll
  geht dort nie hinein, weil es keinen Schacht nennt.
- **Ein Name, der ein Begleitprotokoll verspricht, dessen Inhalt es aber nicht bestätigt,
  wird gemeldet.** Genau dort verschwanden drei Aushärteprotokolle still, weil das OCR die
  Titelzeile verlas.
- **Videodopplung:** `KanalImportDistributionService` suchte eine bereits vorhandene Kopie
  nur unter dem einen Wunschnamen. Dieselbe Aufnahme kommt aber über zwei Wege mit zwei
  Namen an (`20260902_<H>.mp4` vom Haltungs-Verteiler, `00000000_<H>-aufnahme-<Hash>.mp4`
  aus `ImportVideoPaths`); die Sperre `verteiltePfade` greift nie, weil sie den bereits
  relativierten `Link` als Schlüssel merkt, während `ImportVideoPaths` noch den absoluten
  Quellpfad trägt. Gesucht wird jetzt im GANZEN Zielordner nach einer **inhaltsgleichen**
  Datei (byteweise, staging-bewusst über `EnumerateReadableFiles`). Ein abweichender Bestand
  bekommt weiterhin einen freien Namen.
- Abnahme am echten Kundenbestand (`SanierungsprotokollEchteQuelleTests`, maschinengebunden,
  rein lesend): 30 Ablagen aus 19 Dokumenten, 0 der 19 laufen noch in den Schachtweg. Nicht
  zugeordnet bleibt genau `Aushärtungsprotokoll H73_H74.pdf` — das OCR liest sein
  Haltungsfeld als `HH`, die Ziffern fehlen. Es wird gemeldet, nicht geraten.
- Weitere Wächter: `SanierungsprotokollZuordnungTests` (10), `SanierungsprotokollVerteilungTests` (6),
  `VideoDoppelkopieTests` (3), erweiterte `PdfDokumentTypErkennungTests` und
  `ShaftPdfRelevanceTests`.

## (Fortsetzung aus «Begleitprotokolle der Sanierung und Videodopplung (Bürglen, 09.09.2026)»)

`PdfPrimaryDamageFindingBuilder` wandelt die aus PDF-Tabellen gelesenen Zeilen aus
`Primaere_Schaeden` in strukturierte `VsaFinding`-Eintraege um. Passende A-/B-
Streckenmarker mit gleicher Nummer und gleichem VSA-Code werden zu einem Bereich
verbunden. `PdfPrimaryDamageStructureSynchronizer` legt daraus bei fehlenden
Strukturdaten auch das Protokoll an. Bereits vorhandene Findings oder manuelle
Protokolle werden nicht ersetzt. Dadurch kann ein erneuter PDF-Import auch bestehende
Text-only-Haltungen sicher nachziehen.

### Projektimport: nachgeprüfte Regeln (05.09.2026)

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

