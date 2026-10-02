# Sicherung, Spiegel und Dateischutz

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Zielwahl und geschütztes Laufprotokoll (02.10.2026)
- Gemeinsame Verknüpfungsprüfung (02.10.2026)
- Auditkorrekturen: Originalschutz und stabile Sicherungen (18.09.2026)
- Sicherung und Ausfallschutz (09.09.2026)
- Audit Paket 1 — Dateischutz und Wiederherstellung (06.09.2026)
- (Fortsetzung aus «Wichtige Klassen»)

## Zielwahl und geschütztes Laufprotokoll (02.10.2026)

`SettingsFullBackupWorkflow` normalisiert die Zielwahl vor der Analyse. Ein
ausgewählter Unterordner namens `SewerStudio_Datensicherung` wird auf seinen
Elternordner zurückgeführt; der Dienst hängt den Namen einmal an. Laufwerks-
und UNC-Freigabewurzeln bleiben Elternziele, auch bei einer gleichnamigen
Freigabe. Ein bereits angeforderter Abbruch hat Vorrang vor der Pfadnormalisierung.
Erst ein erfolgreicher Lauf aktualisiert die zuletzt gespeicherten Einstellungen.
Marker-, Quellenkonflikt- und Verknüpfungsprüfungen bleiben beim Sicherungsdienst.

Die Bestätigung benennt den gesamten Datenbestand. Dieser entspricht nicht dem
zusätzlichen freien Platz. Unveränderte gewöhnliche Dateien werden nach Inhaltsprüfung
weiterverwendet; Datenbanken erhalten Schnappschüsse. Änderungen, vorübergehende
Vorherkopien und die Reserve kosten zusätzlichen Platz. Die tatsächliche Platzprüfung
erfolgt weiterhin im Dienst. `BackupVersionRetention.MaxStaende` bleibt **0**:
Vorherkopien dienen dem Zurücksetzen und sollen nach Erfolg entfernt werden. Scheitert
die Löschung an einer Sperre oder fehlenden Rechten, meldet der Dienst eine Warnung;
ältere Kopien können dann trotz erfolgreichem Lauf liegen bleiben. Die Bestätigung
nennt diese bestehende Ausnahme ausdrücklich.

`FullBackupService.Protokolliere` löst den Protokollpfad vor jedem Append über
`BackupTargetPathGuard.ResolveRelativePath` auf. Damit gelten die bestehende
Prüfung der Elternkette und der Logdatei auch für Start- und Fehlerprotokollierung.
Eine blockierte zusätzliche Protokollierung verändert keine verknüpfte Fremddatei
und verdeckt nicht den eigentlichen Sicherungsfehler. Das Protokoll bleibt optional.
Die bestehende Grenze einer nicht atomaren Pfadprüfung bleibt erhalten.

Nachweise: `SettingsFullBackupWorkflowTests.TargetFolder`,
`SettingsFullBackupPresentationBuilderTests`, `FullBackupProtokollPathSafetyTests`
und bestehende `BackupAbschlussFortschrittTests`. Der Junction-Wächter zählt 123 Fälle.
Keine neuen Pakete, Registrierungen, öffentlichen Schnittstellen oder gespeicherten Formate.

## Gemeinsame Verknüpfungsprüfung (02.10.2026)

Deepscan 02.10.2026, Befund A5. «Keine Verknüpfungen/Junctions betreten» hat einen gemeinsamen
Baustein: `VerknuepfungsSchutz` mit `VerknuepfungsRegel` (`Application/Common`). Die vier Punkte, in
denen sich die lokalen Kopien unterschieden, sind jetzt ausdrücklich: Wurzel einschliessen, oberhalb
der Wurzel prüfen, bei Lesefehler sperren, fehlender Pfad erlaubt. Meldung und Ausnahmetyp wählt
weiter der Aufrufer; `Fehlt`/`NichtPruefbar` tragen die ursprüngliche Ausnahme zum Weitergeben.

- **Benannte Regeln:** `Spiegel` (Wurzel aus, Lesefehler offen: `ReparsePointGuard`,
  PDF-Prüfablage), `ProjektSchreibgrenze` (Wurzel und alle Vorfahren bis zum Laufwerk:
  `ProjectMutationPathPolicy`, `BackupTargetPathGuard`), `Streng` (jedes Glied vorhanden und lesbar:
  Gold-Speicher, Gold-Eingang, Goldprüfung, Gold-Altarchiv, Import-Staging, Importquellen),
  `GanzerPfad` (ganzer Pfad ab Laufwerk, fehlender Rest erlaubt: `TrainingInventoryPaths`).
- **Gleich geblieben:** Meldungen, Ausnahmetypen und welche Glieder geprüft werden. Kleine
  benannte Unterschiede: Bei mehreren Verknüpfungen nennt die Trainingsablage das unterste statt
  des obersten Glieds; ausserhalb der Gold-Schutzwurzel werden erst alle Vorfahren geprüft;
  `ReparsePointGuard` wertet auch ungültige Pfade als frei; das Sicherungsziel meldet auch
  `SecurityException`/`ArgumentException` als «nicht sicher geprüft».
- **Tests:** Tabellentests `VerknuepfungsSchutzTests` (eingespielte Attribute) und je umgestelltem
  Weg ein Junction-Test (`JunctionFact`). Sperrklinke `VerknuepfungsKopienWaechterTests`: Zahl der
  Produktdateien mit eigener `FileAttributes.ReparsePoint`-Prüfung, darf nur sinken (am 02.10.2026
  von 30 auf 19). Wer eine weitere Kopie umstellt, senkt den Wert.
- **Noch lokal (19 Dateien, folgen bei der nächsten Berührung):** Dateisuche und Zähler
  (`SafeFileEnumeration`, `UebersprungeneOrdner`, `TrainingInventoryFileEnumerator`,
  `TrainingPdfFolderDiscoveryService`) brauchen die Attribute zusätzlich; ferner Eval-Schutz-Leser,
  Haltungsumbenennung, Trainings-Export-Registry, Sicherungsquelle und externe Verweise,
  Wissensspiegel, Kostenspeicher-Probe, Diagnosepaket, Verteilabgleich, Importjournal und
  -wiederherstellung, Aufräumdienste und Protokolleditor.

## Auditkorrekturen: Originalschutz und stabile Sicherungen (18.09.2026)

- `HoldingRenamePathGuard` verwendet die vorhandene Schreibgrenze aus
  `ProjectPathResolver`/`ProjectMutationPathPolicy`. Haltungs- und Fotobaum werden
  gemeinsam vor der ersten Umbenennung geprueft; Projektwurzel, Vorfahren sowie
  Datei-/Ordnerverknuepfungen sind eingeschlossen. Eine unsichere Stelle sperrt
  den ganzen Rename. `HoldingFolderRenameTransaction` prueft zudem jeden Vorwaerts-
  und Rueckwaertsschritt unmittelbar vor dem Move. Auch die nachgelagerte PDF-
  Korrektur im `DataPageHoldingRenameController` prueft die Schreibgrenze erneut.
- `BackupProjectTargetMapping` liest vorhandene Quell-Ziel-Zuordnungen aus dem
  bisherigen Manifest erst nach Journal-Recovery und unter der Laufsperre.
  `BackupPlanBuilder.Build` behaelt seinen bisherigen Aufruf und ergaenzt einen
  Overload mit diesen Zuordnungen. Der normalisierte Quellpfad bindet das Ziel;
  neue Quellen reservieren freie Namen statt alte Kopien zu verdraengen.
  Weiterhin konfigurierte bekannte Kindquellen behalten ihre eigene Kopie auch
  nach Aufnahme einer Elternquelle. Unklare Altzuordnungen sperren die Sicherung.
  `ForManifest` erhaelt die Herkunft bekannter Restkopien entfernter Quellen,
  etwa geschuetzter Altvideos, ohne diese Quellen erneut zu kopieren. Sicher
  dateileere Ordner aus einem abgebrochenen Erstlauf erlauben einen neuen Versuch.
- `DirectoryMirror` haelt normale Quelldateien mit `FileShare.Read` waehrend der
  Kopie und Inhaltspruefung offen. Laenge/Zeit stammen vom selben Handle; Bytes
  werden beim Kopieren gezaehlt. Vorhandene Schreiber fuehren zur Dateiwarnung
  und erhalten die alte Kopie. Der Unveraendert-Vergleich verwendet denselben
  Schutz; ein unlesbarer Vergleich zaehlt in der Platzschaetzung konservativ als
  kopierbeduerftig. SQLite bleibt beim bestehenden Online-Schnappschuss.
- Keine neuen Pakete, Dienstregistrierungen oder Projekt-/Manifestfelder.
  Neue Verhaltenstests: `HoldingRenamePathSafetyTests`,
  `BackupProjectIdentityTests`, `DirectoryMirrorStableSourceTests` sowie die
  PDF-Linkfaelle in `DataPageHoldingRenameControllerTests`.
  Der Verknuepfungswaechter zaehlt 92 statt 85 Tests.
- Grenzen: Die managed Pfadpruefung verhindert keinen atomaren Austausch durch
  einen zweiten Prozess zwischen Pruefung und Move. Ein fehlgeschlagener Rollback
  bleibt sichtbar; es gibt weiterhin kein dauerhaftes Rename-Journal.
  Eine normale Quelldatei bleibt fuer die Dauer von Kopie und Inhaltspruefung
  gegen Schreibzugriffe gesperrt. Die Sicherung ist kein gemeinsamer Zeitpunkt
  aller Projektdateien.
  Andere Auditbefunde, insbesondere alte Originalfoto-/Revisionsvideopfade und
  die Videoanalyse, sind nicht Teil dieses ersten Reparaturpakets.
  Abnahme: `docs/audits/2026-09-18-dateischutz/BEHEBUNG.md`.

## Sicherung und Ausfallschutz (09.09.2026)

- `SchaechtePageViewModel.AutoSave` bindet Schachtfelder und Listenänderungen an
  `DataPageTimerController`. Ein Timer ist an die konkrete Projektinstanz gebunden;
  nach Projektwechsel speichert er kein anderes Projekt.
- `DirectoryMirror` vergleicht auch bei gleicher Grösse und exakt gleicher Zeit den
  Dateiinhalt. Die neue Prüfsumme kann eine so beschädigte Kopie nicht mehr als
  unveränderten Bestand übernehmen. Die vollständige Inhaltsprüfung benötigt zusätzliche Lesezeit.
- `BackupRunJournal` hält vor Zieländerungen dauerhafte Vorherkopien und deren Hashes
  unter `_Versionen/.unterbrochener-lauf`. Eine exklusive Dateisperre verhindert
  gleichzeitige neue Sicherungsläufe. Ein abgebrochener Lauf wird zurückgesetzt;
  nach Prozessausfall erledigt dies der nächste Sicherungslauf oder
  `FullBackupSmoke --recover-backup <Sicherungsordner>` am ursprünglichen Sicherungspfad.
  Fremde Pfade, Verknüpfungen und beschädigte Vorherkopien sperren das Zurücksetzen.
  `BackupManifestIntegrity` verweigert die Freigabe bei offenem Rücksetzprotokoll.
- Erst nach dauerhaftem Abschluss werden Vorherkopien als datierter Stand verfügbar
  und die ältesten Stände ausgedünnt. Läufe ohne geänderte Nutzdaten verdrängen keine
  alte Dateiversion. Das bleibt eine Dateihistorie mit drei Ständen, kein Windows-Abbild.
- `BackupExternalReferences` liest Verweise aus `projekt.json` und ergänzt einzeln
  referenzierte Dateien ausserhalb der Projektwurzeln. Seit 12.09.2026 wird `PDF_All`
  mit `StoredFileListParser` als Semikolon- oder gespeicherte JSON-Liste aufgelöst;
  echte JSON-Arrays werden ebenfalls einzeln gelesen. Relative PDF-Verweise gelten
  ab Projektwurzel. Einzelpfade behalten Semikolons im Dateinamen unverändert.
  `BackupExternalReferencesTests` prüft Kopien, Dubletten und Inhaltsnachweise.
  `FullBackupSources` erweitert
  den Sicherungsplan additiv um `AdditionalRoots` und `ReferencedFiles`.
  Rückwege stehen in der Wiederherstellungsanleitung. Fehlende verknüpfte Dateien
  erzeugen sichtbare Hinweise; eine unvollständige Sicherung erhält keinen grünen Erfolgs-Toast.
- `IBackupAdditionalFolders` / `BackupAdditionalFoldersStore` speichern weitere Quellen
  separat in `AppData/Local/SewerStudio/backup-additional-folders.json`.
  `ServiceProvider.FullBackup` verbindet diesen Dienst mit Quellensuche und Einstellungen;
  das Interface ist in `ServiceProviderRegistrationMap` registriert.
- Projektvideos sind standardmässig aktiv. Bestehende Einstellungen erhalten diese
  sichere Vorgabe einmal über `FullBackupSafetyVersion`; spätere bewusste Abwahl bleibt
  erhalten. `BackupExcludedVideos` schützt bereits gesicherte Videos auch bei Abwahl
  vor automatischem Entfernen. Diese alten Kopien werden dann nicht aktualisiert.
- Projektdateiformat und Kundenoriginale bleiben unverändert. Ein Ersatz-PC benötigt
  weiterhin .NET/Python und gegebenenfalls Ollama/QGIS. Eine Hardwareprüfung oder ein
  vollständiger Windows-Wiederherstellungsversuch ist damit nicht ersetzt.


Details: `docs/reviews/2026-09-09-sicherung-behebung.md`.

## Audit Paket 1 — Dateischutz und Wiederherstellung (06.09.2026)

- `ProjectPathResolver.EnsureWritableProjectPath` verlangt den Projektroot aus
  `ProjectFileLocator`. `ProjectMutationPathPolicy` prueft mit injizierten
  Dateiattributen die Ordnergrenze und alle bestehenden Pfadvorfahren auf
  Verknuepfungen/Junctions. Fremdpfade und Schreibziele in `Imports`,
  `Importdateien`, `Projektdateien` und `__RESTORE_POINTS` werden abgewiesen.
- `ShaftRenameFileService` behaelt Interface, statische Fassade und Registrierung.
  Sein vorhandenes Datei-I/O bleibt als begrenzte Altlast in `Application/Common`;
  eine Verlagerung ist kein Bestandteil dieses Pakets.
- `UseCases/Schaechte/ShaftRenamePlan` plant Dateien, tiefste Unterordner, Haupt-
  und Fotoordner gemeinsam. Der Ausfuehrer prueft alle Zielkonflikte vorab, schreibt
  ohne Ueberschreiben und nimmt ausgefuehrte Schritte bei Fehlern rueckwaerts zurueck.
  Ein bekannter Schachtwurzelordner hat Vorrang vor gleichnamigen Unterordnern.
- `ShaftProtocolPathChanges` zieht Foto-/Originalfoto-/Videoverweise in Original,
  Current und History mit demselben Plan nach. Unbewegte Archiv-/Quellverweise
  bleiben gleich. Inhalte und historische Aenderungsprotokolltexte werden nicht
  neu geschrieben. `SchaechteShaftRenameController` prueft PDF-Schreibpfade erneut.
- `JsonProjectRepository.Load` weist JSON-`null` mit `APP-LOAD` ab. Dadurch
  ueberspringt die bestehende Wiederherstellung ungueltige Sicherungen und sucht
  die naechste gueltige. `{}` und alte Version-1-Projekte bleiben lesbar.
- Feste Nachweise: `ShaftRenameSafetyTests`, `ProjectNullRecoveryTests`,
  `SchaechteRecordDetailsBuilderTests`. Der Junction-Bestandswaechter erwartete am 06.09.2026
  85 statt 84 echte Tests, weil ein neuer Schutzfall dazugekommen ist.
- Grenzen: Kein dauerhaftes Transaktionsjournal gegen Stromausfall. Eine
  rueckgaengige Dateioperation kann selbst scheitern; dann wird der Fehler gemeldet.
  PDF-Inhaltskorrekturen sind weiterhin ein nachgelagerter, separat gemeldeter Schritt.
  Gleichzeitiges Austauschen eines Pfades durch andere Prozesse ist durch die
  managed Pfadvorpruefung nicht atomar ausgeschlossen.
- Bericht und genaue Testergebnisse: `docs/audits/2026-09-06-programmaudit/PAKET-1-ERGEBNIS.md`.
- Auditpraezisierung: Am 06.09.2026 sechs dokumentierte Python-Sicherheitsausnahmen
  in zwei Paketen; die fuenf vom 14.08. sind ein historischer Stand.
  CI-Lauf 33967264002 belegt 46,61 % bei 45,35 % Grenze. A10 bleibt Paket 2.

## (Fortsetzung aus «Wichtige Klassen»)

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
Versionen rotiert werden. Seit 23.09.2026 (Entscheid Pascal) behaelt die
Vollsicherung nur den aktuellen Stand (`BackupVersionRetention.MaxStaende = 0`):
Vorherkopien schuetzen nur den laufenden Lauf (Ruecksetzen bei Abbruch) und
werden nach dem erfolgreichen Abschluss entfernt. Einstellungs-, Log- und Desktop-Skriptquellen duerfen
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

