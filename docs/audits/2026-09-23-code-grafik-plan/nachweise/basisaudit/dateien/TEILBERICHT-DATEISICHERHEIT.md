# Teilbericht Dateisicherheit – 23.09.2026

## Prüfgrundlage

Geprüft wurde der aktuelle Arbeitsbaum, einschließlich der vorhandenen fremden Änderungen an der Sicherung. Keine Produktdatei wurde geändert. Die drei Gegenproben verwenden ausschließlich künstliche Dateien unter diesem Auditordner und direkt referenzierte, zentral frisch gebaute Release-DLLs aus `tests/AuswertungPro.Next.Infrastructure.Tests/bin/Release/net10.0`. Das Probeprojekt hat keine Produkt-Projektverweise. Keine echte Anwendung wurde gestartet oder beendet, keine Kundenquelle verwendet, kein externer Dienst angesprochen.

Untersucht: JsonProjectRepository, Projekt-Speicheraufrufer der Shell, Foto-/Videonormalisierung, RestorePointStore, ProjectRecoveryService, ImportFileStaging/VerifiedImportFileCopy, ImportFileTransaction/Recovery/Journal, FullBackupService, DirectoryMirror, BackupRunJournal, Backup-Zielgrenzen, Sicherungsoberfläche und benachbarte Tests.

## D1 – P1: Übersprungener Quellordner verliert seine bestehende Sicherung

**Bestätigt durch zwei echte FullBackupService-Läufe.**

Fundstellen:

- `src/AuswertungPro.Next.Infrastructure/Backup/DirectoryMirror.cs:757`: Eine Junction im Quell-Unterbaum wird nur mit Warnung übersprungen. Der zugehörige alte Sicherungs-Unterbaum wird nicht als erwartet markiert.
- Derselbe Dienst schützt einzelne übersprungene Dateien ausdrücklich bei Zeile 150 und 180. Dieser Schutz fehlt für Ordner.
- `DirectoryMirror.cs:280` und `:299`: Nicht erwartete Dateien werden beim Aufräumen gelöscht.
- `src/AuswertungPro.Next.Infrastructure/Backup/FullBackupService.cs:299` und `:301`: Lauf wird bestätigt, danach alte Stände entfernt.
- `src/AuswertungPro.Next.Application/Backup/BackupVersionRetention.cs:27`: Aktuell `MaxStaende = 0`. Das ist ein bewusster Benutzerentscheid und für sich kein Fehler; es macht die falsche Entfernung der übersprungenen Kopien endgültig.
- `src/AuswertungPro.Next.UI/Settings/SettingsFullBackupWorkflow.cs:137`: Die Warnung verspricht dagegen, vorhandene ältere Kopien blieben erhalten.

Gegenprobe: Ein echter Ordner `quelle/Medien` mit `alt.txt` wird gesichert. Danach wird ausschließlich dieser künstliche Ordner nach `ausgelagert` verschoben und am alten Ort eine Junction dorthin angelegt. Die zweite Sicherung meldet Erfolg mit einer Warnung, löscht aber die bisherige Sicherungsdatei. Der Quellinhalt bleibt bestehen.

Nachweise:

- `fixture-junction/first-result.json`: `Success=true`, `MirrorExists=true`.
- `fixture-junction/second-result.json`: `Success=true`, `FilesDeleted=1`, `SkippedFileTotal=1`, `MirrorExists=false`, `AllBackupMatches=[]`, `OriginalStillExists=true`.
- Beide Läufe erzeugten ein Manifest. Die einzige alte Kopie fehlt danach sowohl im aktuellen Spiegel als auch in den Versionsständen.

Behebung: Für einen aus Sicherheitsgründen übersprungenen Quell-Unterordner den vorhandenen Ziel-Unterbaum erhalten oder den Lauf vor dem Aufräumen sperren. Die Quelle darf weiterhin nicht durch die Junction gelesen werden.

Regressionstest: Zwei vollständige Sicherungsläufe wie oben; im zweiten Lauf alte Kopie erhalten, fremdes neues Junction-Ziel nicht kopieren, Warnung melden. Gegenprobe: bewusst ausgeschlossene Ordner bleiben ausgeschlossen.

## D2 – P1: Fehlgeschlagenes Speichern unter verändert gültige Fotoverweise im Live-Projekt

**Bestätigt durch echten JsonProjectRepository-Speicheraufruf.**

Fundstellen:

- `src/AuswertungPro.Next.Infrastructure/Projects/JsonProjectRepository.cs:148`: Fotoverweise im übergebenen Live-Projekt werden bereits vor der Dateioperation gegen den neuen Speicherort normalisiert.
- `src/AuswertungPro.Next.Infrastructure/Projects/ProjectPhotoReferenceNormalizationService.cs:50`: Der bestehende Fotoverweis wird unmittelbar geändert.
- Derselbe Dienst `:123` und `:126`: Fehlt die alte relative Datei am neuen Root, wird die Umstellung als Reparatur erlaubt. Dass die Datei am noch aktiven alten Root gültig ist, weiß der Dienst nicht.
- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.ProjectSaving.cs:347`: Die Shell übergibt das Live-Projekt. Bei Fehler bleibt der alte Speicherort aktiv; für die veränderten Fotoverweise gibt es keinen Rückweg.

Gegenprobe: Im alten Projekt existiert `Originalfotos/beleg.jpg`. Der neue Ordner enthält einen anderen gleichnamigen Beleg unter `Fotos/Haltungen/H1/beleg.jpg`. Das neue Speicherziel ist absichtlich ein schon bestehender Ordner namens `gesperrt.json`, damit das Schreiben scheitert.

Nachweis `fixture-failedsave/failedsave-result.json`:

```text
OldSaveOk=true
NewSaveOk=false
BeforePhoto=Originalfotos/beleg.jpg
AfterPhoto=Fotos/Haltungen/H1/beleg.jpg
ChangedDespiteFailure=true
BeforeOriginalStillExists=true
AfterInOriginalProjectExists=false
```

Die Originaldatei wurde nicht gelöscht. Der laufende Datensatz verliert nach einem fehlgeschlagenen Speichern aber seinen bisher gültigen Verweis. Mit einer gleichnamigen Datei im aktiven Root wäre auch eine falsche Bildbindung möglich; diese zusätzliche Variante wurde nicht ausgeführt.

Behebung: Speicheraufbereitung auf einer unabhängigen Kopie, Bindung der Verweise an den bisherigen Projektroot und erst nach bestätigtem Speichern eine kontrollierte Übernahme ins Live-Projekt. Bei Fehlschlag ursprüngliche Verweise und Metadaten erhalten.

Regressionstest: Fehler beim Speichern unter anderem Root auslösen; Fotoverweise, Dirty-Zustand und bisheriger Speicherpfad bleiben fachlich konsistent, kein fremdes gleichnamiges Bild wird übernommen.

## D3 – P2: Erfolgreiches Speichern unter anderem Ordner verliert relative Medienverknüpfungen

**Bestätigt durch Speichern und erneutes Laden. Gemeinsame Ursache mit D2: Der alte Projektroot wird im Speicherwechsel nicht berücksichtigt; die Wirkung ist hier der erfolgreiche statt des gescheiterten Schreibens.**

Fundstellen:

- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.ProjectSaving.cs:323`: Speichern-unter-Ablauf.
- `:347`: Speichert das Projekt unverändert an den neuen Ort; keine Umrechnung relativer Medienverweise, keine Medienübernahme.
- `:355`: Stellt anschließend den aktiven Speicherpfad auf den neuen Ort um.
- `src/AuswertungPro.Next.Infrastructure/Projects/ProjectVideoReferenceNormalizer.cs:42`: Nur absolute Videopfade werden normalisiert; ein vorhandener relativer Link bleibt bestehen.

Gegenprobe: Altes Projekt mit vorhandener Datei `alt/Videos/beleg.mp4` und relativem Link `Videos/beleg.mp4` speichern. Dasselbe Projekt nach `neu/projekt.json` speichern und laden.

Nachweis `fixture-saveas/saveas-result.json`: Altes Speichern, neues Speichern und neues Laden melden jeweils Erfolg. Der Link bleibt `Videos/beleg.mp4`, ist unter dem neuen Root aber nicht vorhanden. Die ursprüngliche Videodatei bleibt erhalten.

Das ist kein physischer Verlust der Quelldatei. Der neu aktive Projektstand kann seinen Beleg jedoch nicht mehr auflösen. Eine Kopie aller Medien ist nicht zwingend erforderlich; mindestens die bisherige Dateibindung muss erhalten werden.

Behebung: Beim Rootwechsel alle gespeicherten Medienverweise mit dem bisherigen Root auflösen und entweder korrekt neu referenzieren oder über den vorhandenen Portabilitätsweg in den neuen Projektordner übernehmen.

Regressionstest: Speichern unter anderem Root mit relativen Video-, PDF- und Fotoverweisen; jeder Verweis zeigt danach auf denselben Inhalt wie vorher. Auch gleichnamige fremde Dateien im neuen Root berücksichtigen.

## D4 – P2: Fehlerprotokoll großer Sicherungen ist trotz entsprechender Meldung unvollständig

**Statisch anhand des vollständigen Aufrufwegs bestätigt; keine zusätzliche Laufprobe erforderlich.**

Fundstellen:

- `src/AuswertungPro.Next.Infrastructure/Backup/FullBackupService.cs:245` und `:316`: Warntexte werden vor Manifest und Rückgabe auf 200 gekappt. Nur die Gesamtzahl bleibt vollständig.
- `src/AuswertungPro.Next.UI/Settings/SettingsFullBackupWorkflow.cs:129`: Protokolliert lediglich die zurückgegebene gekappte Liste.
- `:138`: Behauptet, die vollständige Liste stehe im Programmlog.

Folge: Bei z. B. 517 Dateilücken fehlen mindestens 317 konkrete Pfade für die Nacharbeit. Die Oberfläche meldet die Gesamtzahl korrekt und zeigt eine Warnung; es handelt sich nicht um einen fälschlich grünen Lauf, sondern um fehlende Diagnoseinformationen.

Behebung: Vollständige Liste während des Laufs in eine feste Protokolldatei schreiben, die gekappte Liste nur für die Anzeige verwenden. Den tatsächlichen Protokollpfad nennen.

Regressionstest: 517 kontrollierte Warnungen; 517 konkrete Pfade im genannten Log, begrenzte Dialoganzeige und korrekte Gesamtzahl.

## Wirksame Schutzmaßnahmen

- Repository schreibt eine dauerhaft geleerte Tempdatei und tauscht sie atomar; `.bak` erhält den Vorzustand (`JsonProjectRepository.cs:167`, `:174`, `:209`). Prozessweiter Save-Lock bei `:131`.
- Nicht lesbare Datei und ungültiger Inhalt werden unterschieden; neuere Projektformate werden gesperrt (`JsonProjectRepository.cs:54`, `:104`).
- Importdateien werden vorbereitet und ohne Überschreiben veröffentlicht (`ImportFileStagingSession.cs:539`). Der Rückweg löscht nur unverändert gebliebene selbst veröffentlichte Dateien (`:574`).
- Importjournal prüft Eigentümer vor Änderung/Löschung und verwendet eine prozessübergreifende Sperre (`FileImportTransactionJournal.cs:159` und `ExecuteSynchronized`).
- Der Import arbeitet auf einer unabhängigen Projektkopie und kontrolliert vor Übernahme Projektidentität, Pfad und Inhalt (`ImportRunWorkflowController`).
- Backupjournal hält vor Änderung dauerhafte Vorherkopien, sperrt parallele Läufe und prüft sämtliche Rücksetzdateien vor dem ersten Rückschreiben (`BackupRunJournal.cs:30`, `:145`, `:155`).
- Normale Sicherungsquellen werden während Kopie und Inhaltsprüfung gegen Änderungen gesperrt; auch gleicher Zeitstempel/gleiche Größe genügt nicht als Gleichheitsnachweis (`DirectoryMirror`).
- Die Backup-Zielgrenze prüft Root, Vorfahren und vorhandene Pfadbestandteile gegen Verknüpfungen (`BackupTargetPathGuard`).

Das sind durch Code und vorhandene Tests belegte Schutzmaßnahmen, keine Aussage, dass alle Betriebssystem- oder Stromausfallfälle erneut dynamisch geprüft wurden.

## Gegenproben wiederholen

Vom Repositoryroot aus, nach frischem zentralem Release-Build. Für jeden Wiederholungslauf einen neuen synthetischen Fixture-Namen unter diesem Auditordner wählen; bestehende Befunde nicht überschreiben.

```powershell
dotnet run --project .tmp\audit-gesamt-2026-09-23\dateien\Repro.csproj -c Release -- saveas C:\Sewer-Studio_KI_5.0\.tmp\audit-gesamt-2026-09-23\dateien\fixture-saveas-neu
dotnet run --project .tmp\audit-gesamt-2026-09-23\dateien\Repro.csproj -c Release --no-build -- failedsave C:\Sewer-Studio_KI_5.0\.tmp\audit-gesamt-2026-09-23\dateien\fixture-failedsave-neu
dotnet run --project .tmp\audit-gesamt-2026-09-23\dateien\Repro.csproj -c Release --no-build -- first C:\Sewer-Studio_KI_5.0\.tmp\audit-gesamt-2026-09-23\dateien\fixture-junction-neu
```

Für den zweiten Sicherungslauf ausschließlich innerhalb der neuen eigenen Fixture:

```powershell
$fixtureRoot = [IO.Path]::GetFullPath('C:\Sewer-Studio_KI_5.0\.tmp\audit-gesamt-2026-09-23\dateien\fixture-junction-neu')
$moveFrom = [IO.Path]::GetFullPath((Join-Path $fixtureRoot 'quelle\Medien'))
$moveTo = [IO.Path]::GetFullPath((Join-Path $fixtureRoot 'ausgelagert'))
if (-not $moveFrom.StartsWith($fixtureRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or -not $moveTo.StartsWith($fixtureRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsicherer Testpfad' }
Move-Item -LiteralPath $moveFrom -Destination $moveTo
New-Item -ItemType Junction -Path $moveFrom -Target $moveTo
dotnet run --project .tmp\audit-gesamt-2026-09-23\dateien\Repro.csproj -c Release --no-build -- second C:\Sewer-Studio_KI_5.0\.tmp\audit-gesamt-2026-09-23\dateien\fixture-junction-neu
```

Die Probe löscht keine Fixture rekursiv. Eine synthetische Junction verbleibt im Auditordner; deren Ziel liegt ebenfalls ausschließlich in der eigenen Fixture. Bei späterem Aufräumen zuerst nur die Junction selbst entfernen, niemals rekursiv durch den Link löschen.

## Grenzen

Keine Live-WPF-Bedienung, keine Kundenprojekte, keine echte USB-/Netzlaufwerk- oder Stromausfallprobe. Die SaveAs-Gegenprobe führt denselben Repositoryweg aus; die anschließende Pfadumstellung der Shell ist am produktiven Caller belegt. D4 ist eine statische Feststellung. Drei dynamische Gegenproben sind bestätigt; daraus folgt keine Vollständigkeitsgarantie für jede Dateioperation des Gesamtprogramms.
