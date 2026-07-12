# Produktionsreife-Audit: Nachlauf der fünf fehlenden Bereiche

Stand: 2026-07-12

## Ergebnis

Die fünf bisher offenen Bereiche sind geprüft. Es kam **kein neues P0- oder P1-Problem** hinzu. Die Gesamtnote bleibt **B**. Die größten neuen Wartbarkeitsthemen sind synchrone Vollspeicherungen bei jeder Änderung, nur im Debug-Fenster sichtbare Fehlermeldungen und noch vorhandene Großklassen.

Die Prüfung war eine Code- und Testprüfung. Ein Nachtlauf, GPU-Speicher-Messungen und die Bedienprüfung der wiederhergestellten Oberfläche waren nicht Teil dieses Nachlaufs.

| Bereich | Note | Begründung |
|---|---|---|
| Nebenläufigkeit | B | Single-Instance-Schutz, Locks, `Interlocked`, Abbruchsignale und begrenzte KI-Parallelität sind vorhanden. Einzelne Hintergrundaufgaben laufen aber ohne direkte Fehlerauswertung. |
| Externe Dienste | A− | Timeouts, ein begrenzter Sidecar-Wiederholungsversuch, verständlicher Ollama-Fallback, Token-Schutz und ein im Hauptpfad erzwungener Sidecar-Versionscheck sind vorhanden. |
| Codequalität | C+ | Schichtung und Tests sind stark, neue Großdateien werden verhindert. Es bestehen aber noch 20 Produktionsdateien mit mehr als 1.000 Zeilen. |
| Logging/Diagnose | B- | Tageslogs, Aufbewahrung und globale Ausnahmebehandlung sind vorhanden. Viele wichtige Hinweise landen jedoch weiterhin nur in `Debug.WriteLine`. |
| Performance | B | Große Tabellen sind überwiegend virtualisiert, lange Importe laufen im Hintergrund und Video-Frames werden gestreamt. Projektdateien werden aber synchron und bei der Einstellung „jede Änderung“ vollständig neu geschrieben. |

## Befunde und Maßnahmen

### Nebenläufigkeit

| ID | Prio | Befund | Beleg | Maßnahme |
|---|---|---|---|---|
| N-01 | P2 | Das Kartennetz-Vorladen startet eine unbeobachtete `Task.Run`-Aufgabe. Ein Fehler beim Netzaufbau wird erst verspätet über den globalen Handler sichtbar oder bleibt für den Nutzer unsichtbar. | `UI/Mapping/KarteNetzVorladen.cs:29` | AP-35: Hintergrundaufgaben zentral mit Dateilog und verständlichem Kontext beobachten. |
| N-02 | P3 | Live-Control und QGIS-Bridge starten je Verbindung eine nicht nachverfolgte Aufgabe. Bei vielen gleichzeitigen lokalen Anfragen gibt es keine feste Obergrenze und beim Beenden wird nicht auf alle Anfragen gewartet. | `LiveControlServer.cs:136`, `QgisBridgeServer.cs:80` | AP-35: kleine Parallelitätsgrenze und laufende Aufgaben beim Stoppen einsammeln. |
| N-03 | P2 | Die wichtigsten langen Vorgänge besitzen Abbruchsignale, aber die geplanten Abbruchtests für Analyse, Batch-Import und KB-Neuaufbau fehlen teilweise weiterhin. | AP-41; vorhandene `CancellationTokenSource`-Nutzung in UI und Pipeline | AP-41 vollständig abarbeiten. |

### Externe Dienste

| ID | Prio | Befund | Beleg | Maßnahme |
|---|---|---|---|---|
| E-01 ✅ | P2 | Im Nachlauf gefunden und direkt behoben: Der Entscheid „Multi-Model oder Ollama“ verwendet jetzt den detaillierten Check und blockiert eine falsche Sidecar-Version. | `VisionPipelineClient.CheckHealthDetailedAsync`, `VideoAnalysisPipelineService.ShouldUseMultiModelAsync`; 2 neue Entscheidungstests | AP-36 abgeschlossen. |
| E-02 | P2 | Die Einrichtung von ffmpeg, pdftotext, Ollama, Sidecar und Modellen ist dokumentiert, aber es gibt noch keinen einzigen Start-Check, der alle fehlenden Voraussetzungen zusammenfasst. | `docs/NEUER-PC-SETUP.md`, verteilte Prüfungen in Settings/KI-Start | AP-18 ergänzen: Diagnose „Voraussetzungen prüfen“ mit einer gemeinsamen Ergebnisliste. |
| E-03 | P3 | Einzelne seltene UI-Dienste erzeugen eigene `HttpClient`-Objekte statt den langlebigen Client aus dem ServiceProvider zu verwenden. | `TrainingReviewSamSegmentationService.cs:23-25` | Beim nächsten Anfassen den gemeinsamen Client einspeisen. |

### Codequalität

| ID | Prio | Befund | Beleg | Maßnahme |
|---|---|---|---|---|
| Q-01 | P2 | 20 Produktionsdateien sind länger als 1.000 Zeilen. Die größten sind `HoldingFolderDistributor.PdfParsing.cs` (1.758), `BuilderPageViewModel.cs` (1.576) und `PhotoMeasurementWindow.xaml.cs` (1.419). | Zeilenzählung ohne `bin/obj`; `MaintainabilityFitnessTests` | AP-34: schrittweise genau eine Verantwortung pro Paket herauslösen. |
| Q-02 | P2 | `HoldingFolderDistributor` bleibt eine große statische Fassade, obwohl Dateiablage und Konflikthinweise inzwischen ausgelagert wurden. | Commit `6c656ef6`; `docs/WARTBARKEITS-SCHULDEN.md` | Als Nächstes Haltung-, Schacht- und Dichtheitsabläufe trennen; öffentliche Fassade beibehalten. |
| Q-03 | P3 | Positiv: Es gibt keine offenen `TODO`, `HACK` oder `FIXME` in den C#-Produktionsdateien. Neue Dateien über 1.000 Zeilen und neue statische DI-Umgehungen werden durch Tests blockiert. | `MaintainabilityFitnessTests` | Schutztest beibehalten und die erlaubte Altliste bei jeder echten Zerlegung verkleinern. |

### Logging und Diagnose

| ID | Prio | Befund | Beleg | Maßnahme |
|---|---|---|---|---|
| L-01 | P2 | 35 Produktionsdateien schreiben in `Debug.WriteLine`, aber nur 13 verwenden `ILogger`. Fehler aus Fire-and-forget, KI-Hilfsdiensten oder best-effort-Pfaden fehlen dadurch oft im normalen Tageslog. | Quelltextzählung; `TaskExtensions.cs` | AP-55: wichtige Debug-Meldungen schrittweise auf `ILogger` umstellen. |
| L-02 | P3 | Tageslogs und Aufbewahrung sind vorhanden; diese frühere Sorge ist damit widerlegt. Es fehlt aber ein Diagnosepaket, das Log, Einstellungen ohne Geheimnisse, Versionen und letzte Pipeline-Spur gesammelt bereitstellt. | `FileLoggerProvider`, `App.xaml.cs:91-95` | AP-55: Schaltfläche „Diagnosepaket erstellen“. |
| L-03 | P3 | Der Dateilogger hängt jede Zeile synchron an die Datei. Bei hoher Meldungsrate kann der aufrufende Thread kurz blockieren; ein Schreibfehler im Logger wird nicht intern abgefangen. | `FileLoggerProvider.FileLogger.Log` | Gepufferte Warteschlange oder mindestens internes best-effort-Fangnetz. |

### Performance und Ressourcen

| ID | Prio | Befund | Beleg | Maßnahme |
|---|---|---|---|---|
| P-01 | P2 | Standard ist „bei jeder Änderung speichern“. Dabei wird das ganze Projekt eingerückt serialisiert und synchron atomar ersetzt. Bei großen Projekten erzeugt das UI-Pausen und unnötige Schreiblast. | `AppSettings.DataAutoSaveMode`, `DataPageAutoSaveController`, `JsonProjectRepository.Save` | AP-54: Änderungen kurz bündeln und nur einen Speicherlauf nach z. B. 750 ms Ruhe starten. |
| P-02 | P2 | Projekt Laden, Speichern und Speichern unter laufen weiterhin synchron auf dem UI-Thread. | `ShellViewModel.TryOpenProject`, `TrySaveProject`, `TrySaveProjectAs` | AP-50: Hintergrundarbeit mit Busy-Anzeige; Projektzustand vorher sicher erfassen. |
| P-03 | P3 | Für Nachtlauf, Speicherwachstum, sehr große Projekte und GPU-OOM gibt es noch keine dokumentierte Messreihe. | AP-70 | AP-70 mit festen Messwerten durchführen: Dauer, RAM/VRAM Start/Ende, Logfehler, Durchsatz. |

## Gegenprüfung früherer Aussagen

| Frühere Aussage | Neues Verdikt |
|---|---|
| Sidecar-Vertrag habe gar keinen Versionscheck | **Widerlegt und Hauptpfad gehärtet:** Der detaillierte Check vergleicht Version `1.2.0`; AP-36 bindet ihn nun auch in die Analyseentscheidung ein. |
| Logging könne unbegrenzt wachsen | **Widerlegt:** Tageslogs werden nach einer festen Aufbewahrungszeit gelöscht. |
| Große Tabellen seien möglicherweise nicht virtualisiert | **Weitgehend widerlegt:** Daten-, Builder-, Medien- und Schachttabellen aktivieren Zeilen-Recycling. Zwei kleine Player-Listen deaktivieren es bewusst. |
| Fire-and-forget sei überall ungeschützt | **Teilweise widerlegt:** Es gibt `SafeFireAndForget` und einen globalen Handler; einzelne Aufrufe verwenden beides nicht direkt oder loggen nur ins Debug-Fenster. |
| God-Klasse `HoldingFolderDistributor` sei unverändert | **Teilweise widerlegt:** Dateiablage und Konflikthinweise sind ausgelagert; Parsing und mehrere Verteilabläufe bleiben groß. |

## Empfohlene Reihenfolge aus dem Nachlauf

1. AP-54 und AP-50: Speichern bündeln und UI-Blockaden entfernen.
2. AP-55: wichtige Debug-Meldungen ins Tageslog und Diagnosepaket.
3. AP-35: Hintergrundaufgaben beobachten und lokale Server begrenzen.
4. AP-34: Großklassen nur schrittweise weiter zerlegen.
