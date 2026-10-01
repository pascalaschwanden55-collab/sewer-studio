**SewerStudio – umfangreiche Fehleranalyse vom 17.09.2026**

**Ergebnis: Sieben Fehler wurden mit künstlichen Daten nachgestellt. Vier haben hohe, drei mittlere Priorität.**

Der vollständige Release-Build und sämtliche ausgeführten Bestandstests bestehen. Trotzdem bestehen Fehler bei der Projektwiederherstellung, Fotozuordnung, Videoanalyse und Projektprüfung. Die Gegenproben untersuchen zusätzliche Fälle, welche die grünen Bestandstests nicht ausreichend absichern.

Geprüft wurde der aktuelle Arbeitsordner auf Basis von `93afdc4df77c15557176ddd03838d4143e7b0023`, einschliesslich der bereits vorhandenen, nicht eingecheckten Änderungen. Dazu gehört die neue Projektprüfung. Diese Analyse verändert keinen produktiven Quellcode. Es wurden keine Kundenprojekte für Gegenproben verwendet und keine Reparaturen durchgeführt.

| Kennung | Priorität | Fehler | Praktische Folge |
|---|---|---|---|
| F1 | Hoch | Neueres Projektformat löst Wiederherstellung aus | Eine ältere Sicherung kann den aktiven Projektstand ersetzen. |
| F3 | Hoch | Foto wird allein anhand seines Dateinamens neu zugeordnet | Ein Befund kann plötzlich das falsche Foto zeigen. |
| F4 | Hoch | Abbruch des Videolesers geht im Mehrmodellweg verloren | Ein stark verkürzter Lauf kann als vollständig erscheinen. |
| F5 | Hoch | SAM-Verluste fehlen im abschliessenden Ergebnisstatus | Verlorene Schadensregionen können als null Befunde erscheinen. |
| F2 | Mittel | „Speichern unter“ berücksichtigt einen neuen Projektordner nicht | Relative Verweise zeigen danach auf fehlende Medien. |
| F6 | Mittel | Fortsetzung übernimmt Ergebnisse trotz geänderter Einstellungen | Alte Meterwerte vermischen sich mit dem neuen Lauf. |
| F7 | Mittel | Die Projektprüfung übersieht das Gegenfahrtvideo | Ein fehlendes Video ergibt trotzdem null Hinweise. |

„Hoch“ bedeutet: zuerst korrigieren, weil falsche Arbeitsstände oder fachlich irreführende Ergebnisse entstehen können. „Mittel“ betrifft begrenztere Abläufe, die ebenfalls einen Verhaltenstest und eine Korrektur benötigen. Eine Häufigkeit im täglichen Betrieb wurde nicht gemessen.

**F1 – Neueres Projektformat wird fälschlich als beschädigtes Projekt behandelt**

Auslöser: Eine gültige Projektdatei besitzt beispielsweise Formatversion 4. Daneben liegt eine ältere, lesbare Sicherung mit Formatversion 3.

Der JSON-Leser erkennt die neuere Version korrekt und liefert `APP-VERSION`. Die Oberfläche unterscheidet diesen Fehler jedoch nicht von einer beschädigten Datei. Sie startet bei jedem erfolglosen Laden den Wiederherstellungsdienst. Dieser verschiebt die neuere Datei in Quarantäne und liefert die ältere Sicherung zurück.

Anschliessend markiert die Oberfläche den zurückgeholten Stand als geändert und versucht ihn automatisch am ursprünglichen Pfad zu speichern. Die Meldung behauptet dabei, das Projekt sei beschädigt gewesen. Tatsächlich war lediglich das Programm zu alt.

Die Gegenprobe ergab:

```text
Laden: APP-VERSION
Wiederherstellung: erfolgreich
Geladener Name: ALTER STAND
Neuere Datei am ursprünglichen Pfad: nein
Neuere Datei in Quarantäne vorhanden: ja
```

Das ist kein nachgewiesenes endgültiges Löschen: Die neuere Datei bleibt in Quarantäne erhalten. Trotzdem wird der aktive Arbeitsstand unbegründet zurückgesetzt. Ohne passende Sicherung ist bereits die Beschädigungsmeldung irreführend.

Belege: [Ladeentscheidung](../../src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs), Zeilen 667–672; Übernahme/Neuspeicherung dort ab Zeile 729 und 807. [Versionsschutz](../../src/AuswertungPro.Next.Infrastructure/Projects/JsonProjectRepository.cs), Zeilen 51–57. [Wiederherstellung](../../src/AuswertungPro.Next.Infrastructure/Projects/ProjectRecoveryService.cs), Zeilen 63 und 424.

Korrektur: `APP-VERSION` muss das Öffnen ohne Wiederherstellungsversuch beenden. Den vorhandenen Hinweis auf die benötigte neuere Programmversion anzeigen. Andere Ladefehler sollten ebenfalls ausdrücklich nach reparierbar und nicht reparierbar unterschieden werden.

Benötigter Test: Den kompletten Öffnungsablauf mit Version 4 und vorhandener Version-3-Sicherung prüfen. Beide Dateien müssen unverändert bleiben. Kein Zurücksetzen, kein automatisches Speichern.

**F3 – Eine bestehende Fotoverknüpfung kann auf ein anderes Bild umgeschrieben werden**

Auslöser: Ein Protokolleintrag verweist auf ein vorhandenes externes `foto.jpg`. Im zentralen Fotoordner derselben Haltung existiert ebenfalls ein `foto.jpg`, aber mit anderem Inhalt.

Die Foto-Normalisierung bevorzugt das zentrale Bild allein wegen seines Namens. Sie prüft weder, ob der ursprüngliche Verweis noch funktioniert, noch, ob beide Dateien denselben Inhalt besitzen. Diese Normalisierung läuft beim Laden und Speichern. Sie betrifft auch die ursprüngliche Protokollfassung und die Historie.

In der Gegenprobe wurden zwei kleine Dateien mit unterschiedlichem Inhalt erstellt. Der bestehende Verweis wurde von „BEABSICHTIGTES FOTO“ auf „ANDERES FOTO“ geändert. Die Dateien selbst wurden nicht verändert. Das Problem ist die falsche Zuordnung.

Beleg: [ProjectPhotoReferenceNormalizationService](../../src/AuswertungPro.Next.Infrastructure/Projects/ProjectPhotoReferenceNormalizationService.cs), Zeilen 89–110. Aufruf beim Laden/Speichern: [JsonProjectRepository](../../src/AuswertungPro.Next.Infrastructure/Projects/JsonProjectRepository.cs), Zeilen 69 und 131.

Korrektur: Einen gültigen bestehenden Verweis erhalten. Eine automatische Umstellung nur bei belegter Dateigleichheit oder eindeutiger Importzuordnung durchführen. Gleichnamige Dateien mit anderem Inhalt als Konflikt behandeln. Ein Dateiname allein ist kein Identitätsnachweis.

Benötigter Test: Zwei vorhandene, unterschiedlich befüllte Dateien mit gleichem Namen. Laden und Speichern müssen die gewählte Fotozuordnung erhalten. Zusätzlich Current, Original und History prüfen.

**F4 – Ein vorzeitig beendeter Videoleser wird im Mehrmodellweg als Erfolg abgeschlossen**

Auslöser: Der Videoleser liefert nur einen Teil der erwarteten Bilder und endet mit einem Prozessfehler.

`VideoFrameStream` erkennt diesen Fall bereits. Er hält die tatsächliche Bildzahl, die erwartete Bildzahl und den Prozessfehler in `Completion` fest. Der Mehrmodellweg reicht aber nur die Bilder weiter. Den Abschlussstatus liest er nicht aus.

Die Gegenprobe nutzte einen echten lokalen Unterprozess als ffmpeg-Ersatz. Dieser lieferte zwei PNG-Bilder und beendete sich mit Fehlercode 1. Die erwartete Bildzahl betrug 100. Dabei wurde der produktive `VideoFrameStream` verwendet.

```text
Videoleser: IsComplete=false, FramesRead=2, ExpectedFrames=100, ExitCode=1
Mehrmodell-Ergebnis: IsSuccess=true, FramesAnalyzed=2
Degraded=false, Incomplete=false
```

Der abschliessende Fortschritt kann trotzdem auf fertig gesetzt werden. Ein aktives Fortsetzungsprotokoll wird ebenfalls als abgeschlossen markiert. Der getrennte Ollama-Analyseweg wertet `Completion` bereits aus; dort ist dieser konkrete Fehler nicht derselbe.

Belege: [DefaultFrameSource](../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.Helpers.cs), Zeilen 41–50; [Abschluss des Mehrmodelllaufs](../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs), ab Zeile 952. Vorhandene Abschlussbewertung: [VideoFrameStream](../../src/AuswertungPro.Next.Infrastructure/Ai/VideoFrameStream.cs), ab Zeile 140.

Korrektur: Den Abschlussstatus bis zum Ergebnis durchreichen. Bei frühem Ende das Ergebnis ausdrücklich unvollständig kennzeichnen und den Grund erhalten. Ein unvollständiger Lauf darf sein Fortsetzungsprotokoll nicht als vollständig erledigt abschliessen.

Benötigter Test: Wenige gelieferte Bilder bei deutlich längerer erwarteter Laufzeit. Sowohl Prozessfehler als auch frühes Ende ohne Prozessfehler prüfen. Die bestehende Rundungstoleranz von einem Bild erhalten.

**F5 – Verlorene SAM-Regionen erscheinen nicht im endgültigen Warnstatus**

Auslöser: DINO findet eine Schadensregion, aber SAM liefert für diese Region keine verwertbare Maske und meldet `Degraded=true` sowie verlorene Boxen.

Der Mehrmodellweg protokolliert die Warnung für das Einzelbild und meldet sie während des Laufs. Er übernimmt sie jedoch nicht in die gesammelten Gründe des Endergebnisses. Auch der Zähler für fehlerbedingt übersprungene Bilder wird an dieser Stelle nicht erhöht.

Die Gegenprobe simulierte drei Bilder mit jeweils einer gefundenen, anschliessend verlorenen Region:

```text
Als eingeschränkt protokollierte Bilder: 3
Abschliessende Befunde: 0
IsSuccess=true, Degraded=false, Incomplete=false
```

Belege: [SAM-Verarbeitung](../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs), Zeilen 767–773; [BuildResult](../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.Helpers.cs), ab Zeile 208. Der Einzelframe-Dienst übernimmt den SAM-Warnstatus bereits ausdrücklich.

Korrektur: SAM-Verluste dauerhaft im Laufergebnis sammeln. Anzahl und Grund der verlorenen Regionen anzeigen. Ein vollständiger Maskenverlust darf nicht als unauffälliges Bild abgeschlossen werden. Für die Fortsetzung muss festgelegt werden, welche fehlerhaften Bilder erneut bearbeitet werden.

Benötigter Test: Teilweiser und vollständiger Maskenverlust bei ansonsten erreichbaren Diensten. Die Warnung muss im abschliessenden Ergebnis und im übergeordneten Pipeline-Ergebnis ankommen.

**Wichtige Einordnung zu F4 und F5:** Die Gegenproben verwenden einen freigegebenen Detektor, um die Fehler isoliert sichtbar zu machen. Die vorhandene `model_qualification.json` markiert das Altmodell weiterhin als nicht freigegeben. Dadurch kann im heutigen Standardbetrieb ohnehin eine allgemeine Warnung erscheinen. Diese Warnung beschreibt aber weder den Decoderabbruch noch den konkreten SAM-Verlust. Der Fehler in der Ergebnisbildung besteht unabhängig davon.

**F2 – „Speichern unter“ kann relative Medienverweise unbrauchbar machen**

Auslöser: Ein Projekt mit relativen Verweisen wird in einen anderen Projektordner gespeichert. Beispiel: `Videos/aufnahme.mp4` liegt unter Ordner A, das Projekt wird unter Ordner B gespeichert.

Der Speicherablauf schreibt die Projektdatei unverändert in den neuen Ordner und stellt den aktiven Projektpfad um. Die Medien werden nicht mitgenommen, und die relativen Verweise werden nicht auf ihre bisherigen Ziele angepasst.

Die Gegenprobe bestätigte erfolgreiches Speichern in beiden Ordnern. Der gespeicherte Verweis blieb `Videos/aufnahme.mp4`. Unter A war die Datei vorhanden, unter B fehlte sie.

Das Problem tritt beim Wechsel des tatsächlichen Projektroots auf. Ein anderer JSON-Dateiname im selben Projektroot löst diesen Fall nicht aus. Die ursprüngliche Videodatei wird nicht gelöscht. Der neue Projektstand verliert ihre korrekte Auflösung.

Belege: [TrySaveProjectAs](../../src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.ProjectSaving.cs), Zeilen 288–320; [ProjectVideoReferenceNormalizer](../../src/AuswertungPro.Next.Infrastructure/Projects/ProjectVideoReferenceNormalizer.cs), ab Zeile 42; [Projektroot-Auflösung](../../src/AuswertungPro.Next.Application/Common/ProjectFileLocator.cs), ab Zeile 43.

Korrektur: Vor dem Ordnerwechsel eine Projektkopie vorbereiten. Relative Verweise gegen den alten Root auflösen. Je nach festgelegtem Speichervertrag die Medien geschützt kopieren oder gültige Verweise auf die bisherigen Dateien erhalten. Erst nach erfolgreichem Speichern den aktiven Stand wechseln.

Benötigter Test: Projekt mit Video, Gegenfahrt, PDF und Befundfotos in einen anderen Ordner speichern. Danach müssen alle Verweise weiterhin auf dieselben Inhalte zeigen. Ein Schreibfehler muss den alten Arbeitsstand erhalten.

**F6 – Fortsetzung verwendet alte Meterwerte trotz geänderter Haltungslänge**

Auslöser: Eine Analyse wird abgebrochen. Vor der Fortsetzung wird die Haltungslänge korrigiert.

Das Fortsetzungsprotokoll vergleicht nur Videopfad, Dateigrösse, Änderungszeit und zeitlichen Bildabstand. Haltungslänge, Rohrdurchmesser, Modellstand und weitere auswertungsrelevante Einstellungen fehlen in diesem Vergleich. Alte Ergebnisse können deshalb in einen anders eingestellten Lauf übernommen werden.

Die Gegenprobe erzeugte das Protokoll durch einen echten ersten Analyselauf. Dieser verwendete 100 m und brach nach drei Bildern ab. Danach wurde dasselbe Video mit 10 m fortgesetzt. Die beiden neu bearbeiteten Bilder erhielten weiterhin 40 m. Ursache ist zusätzlich das Übernehmen des alten Maximalmeters in `lastMeter`.

Belege: [IdentityMatches](../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/AnalysisCheckpointJournal.cs), Zeilen 352–357; [RestoreCheckpointAsync](../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.Helpers.cs), Zeilen 169–188; Meterfortschreibung dort Zeilen 134–141. Die echte Haltungslänge wird im [VideoAnalysisPipelineService](../../src/AuswertungPro.Next.Infrastructure/Ai/VideoAnalysisPipelineService.cs) ab Zeile 198 gesetzt.

Die Gegenprobe belegt die falsche Meterfortschreibung. Änderungen an Modellen oder Rohrdurchmessern wurden nicht separat nachgestellt; deren fehlende Berücksichtigung ist im gespeicherten Vergleichsvertrag sichtbar.

Korrektur: Einen gespeicherten Vergleichswert aller ergebnisrelevanten Einstellungen ergänzen. Bei Abweichung neu analysieren. Alte Protokolle ohne diesen Vergleichswert dürfen nicht ungeprüft fortgesetzt werden.

Benötigter Test: Abbruch und Fortsetzung mit geänderter Länge, geändertem Rohrdurchmesser und geändertem Modellstand. Unveränderte Einstellungen müssen weiterhin eine korrekte Fortsetzung erlauben.

**F7 – Die neue Projektprüfung kontrolliert das Gegenfahrtvideo nicht**

Auslöser: Im Feld `Link_G` ist ein Gegenfahrtvideo hinterlegt, dessen Datei fehlt.

Die Dateiprüfung liest ausschliesslich `Link`, `PDF_Path`, `PDF_Eigen` und `PDF_All`. Das tatsächlich verwendete Gegenfahrtfeld fehlt. Auch die Video-Normalisierung kennt `Link_G` ausdrücklich, es handelt sich also nicht um ein hypothetisches Feld.

Die Gegenprobe enthielt eine Haltung mit gültiger Länge und einem nicht vorhandenen Gegenfahrtvideo. Ergebnis: **null Dateihinweise und null Hinweise insgesamt**.

Belege: [ProjektPruefregeln](../../src/AuswertungPro.Next.Application/UseCases/ProjektPruefung/ProjektPruefregeln.cs), Zeile 61; [Video-Feldliste](../../src/AuswertungPro.Next.Infrastructure/Projects/ProjectVideoReferenceNormalizer.cs), Zeile 13. Die [Bedienbeschreibung](../PROJEKTPRUEFUNG.md) spricht allgemein von hinterlegten Video- und PDF-Pfaden.

Korrektur: `Link_G` in die Dateiprüfung aufnehmen. Die unterstützten Medienfelder möglichst aus einer gemeinsamen Liste beziehen. Zusätzlich den Umfang für Befundfotos und historische Medienverweise ausdrücklich beschreiben: Diese prüft der jetzige Lauf ebenfalls nicht umfassend.

Benötigter Test: Vorhandene, fehlende und leere Gegenfahrtdatei. Hauptvideo vorhanden, Gegenfahrt fehlt: Der Hinweis muss trotzdem erscheinen und zur passenden Zuordnung führen.

**Durchgeführte Prüfungen**

| Prüfung | Bestanden | Fehlgeschlagen | Übersprungen / ausgeschlossen |
|---|---:|---:|---:|
| Vollständiger Release-Build | erfolgreich | 0 Fehler | 0 Warnungen |
| Infrastructure-Tests | 6.714 | 0 | 6 |
| Pipeline-Tests | 2.658 | 0 | 3 |
| UI-Tests | 7.298 | 0 | 28 |
| ProjectModernizer-Tests | 62 | 0 | 0 |
| Python-Sidecar, `not gpu` | 572 | 0 | 2 ausgeschlossen |
| QGIS-Python-Tests | 14 | 0 | 0 |
| **Ausgeführte Bestandstests gesamt** | **17.318** | **0** | **37 übersprungen, 2 ausgeschlossen** |

Die UI-Zahl enthält bewusst im Elternprozess übersprungene Kindprozess-Einstiege. Ihre jeweiligen Elterntests führen die isolierten WPF-Prüfungen separat aus. Die 28 Einträge dürfen deshalb nicht pauschal als 28 ungeprüfte Oberflächenfälle verstanden werden.

Der Sidecar-Lauf meldete zwei zusätzliche Warnungen: eine veraltende Nutzung von `httpx` durch den Testclient und fehlendes Schreibrecht auf den pytest-Zwischenspeicher. Beide verhinderten die Tests nicht. Für diese Analyse wurden keine Pakete geändert.

Ausgeführte Befehle:

```powershell
dotnet build AuswertungPro.sln -c Release --no-restore -v quiet
dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/ProjectModernizer.Tests/ProjectModernizer.Tests.csproj -c Release --no-build --no-restore
# Im Ordner sidecar:
.\.venv\Scripts\python.exe -m pytest -m "not gpu" -q
# Im Projektroot:
python -m unittest discover integrations/qgis/tests -v
```

Die Läufe verwendeten zusätzlich Ergebnisdateien und Protokollumleitungen. Rohdaten: [Testordner](../../.tmp/fehleranalyse-2026-09-17). Gegenproben: [Program.cs](../../.tmp/fehleranalyse-2026-09-17/repro/Program.cs), [Ausgabe](../../.tmp/fehleranalyse-2026-09-17/gegenproben.txt).

Wiederholung der Gegenproben vom Projektroot:

```powershell
dotnet run --project .tmp/fehleranalyse-2026-09-17/repro/Repro.csproj -c Release --no-restore
```

Der Prüfhelfer erzeugt ausschliesslich neue künstliche Dateien in seinem eigenen Ausgabeordner. Seine Ausgabe beschreibt das beobachtete Fehlverhalten. Er ist kein neuer Bestandstest und wurde nicht in die Testanzahl eingerechnet. Die Dateien unter `.tmp` bleiben verfügbar, bis dieser Arbeitsordner aufgeräumt wird.

**Abdeckung und Grenzen**

Die Analyse kombiniert vollständige vorhandene Testläufe mit gezielter Codeprüfung und sieben Gegenproben. Schwerpunkte waren Speichern/Laden, Wiederherstellung, Medienzuordnung, neue Projektprüfung sowie Fehlerweitergabe und Fortsetzung der KI-Verarbeitung. Sicherungs-, Import-/Export-, Sidecar- und QGIS-Verhalten wurden zusätzlich durch die bestehenden Tests geprüft. Das ist keine vollständige Zeilenprüfung aller Programmdateien.

Keine neuen Messungen der Erkennungsgenauigkeit an echten Kanalvideos, keine echten GPU-Modellläufe und kein GEONIS-Rückimport wurden durchgeführt. Ein vollständiger Ersatz-PC-Wiederherstellungsversuch gehört ebenfalls nicht zu diesem Nachweis. Grüne Tests belegen die geprüften Fälle, keine allgemeine Fehlerfreiheit.

Einige Arbeitsanleitungen sind zudem widersprüchlich. Der Pipeline-Auditor-Skill beschreibt noch ältere Ablaufteile; die aktuelle Architektur nennt bereits andere Klassen und kein ByteTrack. Für die Befunde wurde deshalb der aktuelle Code herangezogen. Dieser Dokumentationsstand wurde nicht als zusätzlicher Programmfehler gezählt.

**Reihenfolge der Korrekturen**

1. F1 und F3: Projektstand und Fotozuordnung schützen. Zuerst die beiden Gegenfälle als feste Verhaltenstests aufnehmen.
2. F4 und F5: Unvollständigkeit bis zur abschliessenden Anzeige erhalten. Decoderstatus, Maskenverluste und Fortsetzungsabschluss zusammen prüfen.
3. F2 und F6: Ordnerwechsel und geänderte Analysebedingungen ausdrücklich behandeln.
4. F7: Gegenfahrtprüfung ergänzen und den Medienumfang verständlich dokumentieren.

Nach jeder Korrektur den passenden Verhaltenstest ausführen. Vor einem Commit den vollständigen Release-Prüfweg wiederholen. Änderungen an Diensten, Speichern oder KI-Verarbeitung müssen anschliessend mit der Architekturquelle abgeglichen werden.
